using Tempo.ReportServer.Api.Rendering;
using Tempo.ReportServer.Api.Storage;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Api.Scheduling;

/// <summary>Renders a scheduled report to a deliverable artifact (PDF/CSV/XLSX bytes).</summary>
public interface IScheduledReportRenderer
{
    /// <summary>Renders the report referenced by <paramref name="schedule"/> in its configured format.</summary>
    Task<ScheduledReportArtifact> RenderAsync(ReportScheduleDto schedule, CancellationToken cancellationToken = default);
}

/// <summary>Default renderer backed by the report server catalog store and engine renderer.</summary>
public sealed class ScheduledReportRenderer : IScheduledReportRenderer
{
    private readonly IReportServerStore _store;
    private readonly IReportServerRenderer _renderer;
    private readonly ReportServerRequestContext _requestContext;

    /// <summary>Creates the renderer.</summary>
    public ScheduledReportRenderer(
        IReportServerStore store,
        IReportServerRenderer renderer,
        ReportServerRequestContext requestContext)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
    }

    /// <inheritdoc />
    public async Task<ScheduledReportArtifact> RenderAsync(ReportScheduleDto schedule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        // Establish the ambient tenant so the catalog query filter resolves the report.
        _requestContext.Set(new ReportExecutionContext(schedule.TenantId, "schedule-worker", schedule.CultureName));
        var report = await _store.GetReportAsync(schedule.TenantId, schedule.ReportId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Report '{schedule.ReportId}' was not found for tenant '{schedule.TenantId}'.");

        var request = new RenderReportRequestDto
        {
            TenantId = schedule.TenantId,
            ReportId = schedule.ReportId,
            Format = MapFormat(schedule.Format),
            CultureName = schedule.CultureName,
            Parameters = schedule.Parameters
                .Select(pair => new ReportParameterValueDto { Name = pair.Key, Values = [pair.Value] })
                .ToList(),
        };

        var result = await _renderer.RenderAsync(report, request, _requestContext.ExecutionContext, cancellationToken).ConfigureAwait(false);
        return new ScheduledReportArtifact(result.FileName, result.ContentType, result.Bytes);
    }

    private static ReportRenderFormat MapFormat(ReportScheduleFormat format)
        => format switch
        {
            ReportScheduleFormat.Csv => ReportRenderFormat.Csv,
            ReportScheduleFormat.Xlsx => ReportRenderFormat.Xlsx,
            _ => ReportRenderFormat.Pdf,
        };
}
