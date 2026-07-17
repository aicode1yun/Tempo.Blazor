using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tempo.ReportServer.Api.Scheduling;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>EF Core backed <see cref="IReportScheduleStore"/> persisting schedules and run history.</summary>
public sealed class EfReportScheduleStore : IReportScheduleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ReportServerDbContext _dbContext;

    /// <summary>Creates the store over a report server context.</summary>
    public EfReportScheduleStore(ReportServerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportScheduleDto>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.Schedules
            .AsNoTracking()
            .Where(schedule => schedule.TenantId == tenantId)
            .OrderBy(schedule => schedule.NextRunUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ReportScheduleDto?> GetAsync(string tenantId, string scheduleId, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.Schedules
            .AsNoTracking()
            .FirstOrDefaultAsync(schedule => schedule.TenantId == tenantId && schedule.ScheduleId == scheduleId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToDto(row);
    }

    /// <inheritdoc />
    public async Task<ReportScheduleDto> UpsertAsync(UpsertReportScheduleRequestDto request, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            throw new ArgumentException("TenantId is required.", nameof(request));
        }

        // Validate the cron eagerly so a bad expression surfaces as a 400 at the endpoint, not a
        // background worker crash later.
        var nextRun = ReportScheduleCalculator.ComputeNextRun(request.CronExpression, nowUtc);
        var scheduleId = string.IsNullOrWhiteSpace(request.ScheduleId) ? Slug(request.Name) : request.ScheduleId;

        var row = await _dbContext.Schedules
            .FirstOrDefaultAsync(schedule => schedule.TenantId == request.TenantId && schedule.ScheduleId == scheduleId, cancellationToken)
            .ConfigureAwait(false);
        var isNew = row is null;
        row ??= new ReportScheduleEntity
        {
            ScheduleId = scheduleId,
            TenantId = request.TenantId,
            NextRunUtc = nextRun,
        };

        row.OwnerUserId = request.OwnerUserId;
        row.Name = request.Name;
        row.ReportId = request.ReportId;
        row.CronExpression = request.CronExpression;
        row.Format = request.Format.ToString();
        row.CultureName = request.CultureName;
        row.ParametersJson = JsonSerializer.Serialize(request.Parameters, JsonOptions);
        row.DeliveryKind = request.DeliveryKind.ToString();
        row.DeliveryTarget = request.DeliveryTarget;
        row.MissedRunPolicy = request.MissedRunPolicy.ToString();
        row.MaxAttempts = request.MaxAttempts < 1 ? 1 : request.MaxAttempts;
        row.IsEnabled = request.IsEnabled;
        if (isNew)
        {
            row.NextRunUtc = nextRun;
            row.LastStatus = ReportScheduleRunStatus.NeverRun.ToString();
            row.LastStatusMessage = "Never run";
            _dbContext.Schedules.Add(row);
        }
        else if (row.NextRunUtc == default)
        {
            row.NextRunUtc = nextRun;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(row);
    }

    /// <inheritdoc />
    public async Task<bool> SetEnabledAsync(string tenantId, string scheduleId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.Schedules
            .FirstOrDefaultAsync(schedule => schedule.TenantId == tenantId && schedule.ScheduleId == scheduleId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        row.IsEnabled = isEnabled;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string tenantId, string scheduleId, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.Schedules
            .FirstOrDefaultAsync(schedule => schedule.TenantId == tenantId && schedule.ScheduleId == scheduleId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        var runs = _dbContext.ScheduleRuns.Where(run => run.TenantId == tenantId && run.ScheduleId == scheduleId);
        _dbContext.ScheduleRuns.RemoveRange(runs);
        _dbContext.Schedules.Remove(row);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportScheduleRunDto>> GetRunsAsync(string tenantId, string scheduleId, int max, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(max, 1, 200);
        var rows = await _dbContext.ScheduleRuns
            .AsNoTracking()
            .Where(run => run.TenantId == tenantId && run.ScheduleId == scheduleId)
            .OrderByDescending(run => run.OccurrenceUtc)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportScheduleDto>> GetDueSchedulesAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.Schedules
            .AsNoTracking()
            .Where(schedule => schedule.IsEnabled
                && (schedule.NextRunUtc <= nowUtc || (schedule.RetryAfterUtc != null && schedule.RetryAfterUtc <= nowUtc)))
            .OrderBy(schedule => schedule.NextRunUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task ApplyRunOutcomeAsync(
        string tenantId,
        string scheduleId,
        ScheduleStateUpdate update,
        IReadOnlyList<ScheduleRunRecord> runs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(runs);

        var row = await _dbContext.Schedules
            .FirstOrDefaultAsync(schedule => schedule.TenantId == tenantId && schedule.ScheduleId == scheduleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Schedule '{scheduleId}' for tenant '{tenantId}' was not found.");

        row.LastRunUtc = update.LastRunUtc;
        row.LastDeliveredUtc = update.LastDeliveredUtc;
        row.NextRunUtc = update.NextRunUtc;
        row.RetryAfterUtc = update.RetryAfterUtc;
        row.FailureCount = update.FailureCount;
        row.LastStatus = update.LastStatus.ToString();
        row.LastStatusMessage = update.LastStatusMessage;
        row.PendingOccurrencesJson = update.PendingOccurrences.Count == 0
            ? null
            : JsonSerializer.Serialize(update.PendingOccurrences, JsonOptions);

        foreach (var run in runs)
        {
            _dbContext.ScheduleRuns.Add(new ReportScheduleRunEntity
            {
                RunId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                ScheduleId = scheduleId,
                OccurrenceUtc = run.OccurrenceUtc,
                StartedUtc = run.StartedUtc,
                CompletedUtc = run.CompletedUtc,
                Status = run.Status.ToString(),
                Attempt = run.Attempt,
                DeliveryKind = run.DeliveryKind.ToString(),
                DeliveryTarget = run.DeliveryTarget,
                ArtifactFileName = run.ArtifactFileName,
                ArtifactContentType = run.ArtifactContentType,
                ArtifactByteCount = run.ArtifactByteCount,
                ErrorMessage = run.ErrorMessage,
            });
        }

        // Single SaveChanges => the schedule mutation and run inserts commit as one transaction.
        // The schedule row carries a RowVersion concurrency token: if a second worker already applied
        // an outcome for the same schedule, SaveChanges throws DbUpdateConcurrencyException. Surface it
        // as a typed signal so the processor skips the losing pass instead of corrupting state or
        // appending duplicate run history.
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ReportScheduleConcurrencyException(tenantId, scheduleId, ex);
        }
    }

    private static ReportScheduleDto ToDto(ReportScheduleEntity row)
        => new()
        {
            TenantId = row.TenantId,
            ScheduleId = row.ScheduleId,
            OwnerUserId = row.OwnerUserId,
            Name = row.Name,
            ReportId = row.ReportId,
            CronExpression = row.CronExpression,
            Format = ParseEnum(row.Format, ReportScheduleFormat.Pdf),
            CultureName = row.CultureName,
            Parameters = DeserializeParameters(row.ParametersJson),
            DeliveryKind = ParseEnum(row.DeliveryKind, ReportScheduleDeliveryKind.Email),
            DeliveryTarget = row.DeliveryTarget,
            MissedRunPolicy = ParseEnum(row.MissedRunPolicy, ReportScheduleMissedRunPolicy.Skip),
            MaxAttempts = row.MaxAttempts,
            IsEnabled = row.IsEnabled,
            NextRunUtc = row.NextRunUtc,
            LastRunUtc = row.LastRunUtc,
            LastDeliveredUtc = row.LastDeliveredUtc,
            RetryAfterUtc = row.RetryAfterUtc,
            FailureCount = row.FailureCount,
            LastStatus = ParseEnum(row.LastStatus, ReportScheduleRunStatus.NeverRun),
            LastStatusMessage = row.LastStatusMessage,
            PendingOccurrencesUtc = DeserializeOccurrences(row.PendingOccurrencesJson),
        };

    private static IReadOnlyList<DateTimeOffset> DeserializeOccurrences(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<DateTimeOffset>>(json, JsonOptions) ?? [];
    }

    private static ReportScheduleRunDto ToDto(ReportScheduleRunEntity row)
        => new()
        {
            RunId = row.RunId,
            TenantId = row.TenantId,
            ScheduleId = row.ScheduleId,
            OccurrenceUtc = row.OccurrenceUtc,
            StartedUtc = row.StartedUtc,
            CompletedUtc = row.CompletedUtc,
            Status = ParseEnum(row.Status, ReportScheduleRunStatus.Queued),
            Attempt = row.Attempt,
            DeliveryKind = ParseEnum(row.DeliveryKind, ReportScheduleDeliveryKind.Email),
            DeliveryTarget = row.DeliveryTarget,
            ArtifactFileName = row.ArtifactFileName,
            ArtifactContentType = row.ArtifactContentType,
            ArtifactByteCount = row.ArtifactByteCount,
            ErrorMessage = row.ErrorMessage,
        };

    private static IReadOnlyDictionary<string, string> DeserializeParameters(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
        return parsed is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(parsed, StringComparer.Ordinal);
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static string Slug(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim().ToLowerInvariant();
        var chars = text.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("N") : slug;
    }
}
