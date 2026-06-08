using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoNotionAuditProvider : INotionAuditProvider
{
    public const string ActionCreate = "create";
    public const string ActionEdit = "edit";
    public const string ActionDelete = "delete";
    public const string ActionMove = "move";
    public const string ActionRestrict = "restrict";

    private static readonly DateTime E2ESeedNow = new(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
    private readonly object _syncRoot = new();
    private readonly List<AuditEntryDto> _entries = [];

    public DemoNotionAuditProvider()
    {
        Reset();
    }

    public Task LogAsync(AuditEntryDto entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = Normalize(entry);
        lock (_syncRoot)
        {
            _entries.Add(normalized);
        }

        return Task.CompletedTask;
    }

    public Task<PagedResult<AuditEntryDto>> GetEntriesAsync(
        AuditLogFilter filter,
        NotionAuditQuery paging,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(paging);
        cancellationToken.ThrowIfCancellationRequested();

        var skip = Math.Max(0, paging.Skip);
        var take = Math.Clamp(paging.Take, 1, 100);

        lock (_syncRoot)
        {
            var matches = _entries
                .Where(entry => Matches(entry, filter))
                .OrderByDescending(entry => entry.Timestamp)
                .ThenByDescending(entry => entry.Id, StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult(new PagedResult<AuditEntryDto>
            {
                Items = matches.Skip(skip).Take(take).Select(Clone).ToArray(),
                TotalCount = matches.Length,
                Page = skip / take + 1,
                PageSize = take
            });
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
            AddSeed("audit-seed-001", DateTime.UtcNow.AddHours(-8), "alice", "Alice Morgan", ActionCreate, MockNotionDataStore.Page1Id, new Dictionary<string, string>
            {
                ["title"] = "Getting Started with Notion Editor"
            });
            AddSeed("audit-seed-002", DateTime.UtcNow.AddHours(-5), "demo", "Demo User", ActionEdit, MockNotionDataStore.Page1Id, new Dictionary<string, string>
            {
                ["field"] = "Title"
            });
            AddSeed("audit-seed-003", DateTime.UtcNow.AddHours(-2), "grace", "Grace Hopper", ActionMove, MockNotionDataStore.Page2Id, new Dictionary<string, string>
            {
                ["newParentId"] = MockNotionDataStore.Page1Id.ToString("D")
            });
        }
    }

    public void SeedE2EAuditPage()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
            AddSeed("cf32-audit-001", E2ESeedNow.AddMinutes(-55), "alice", "Alice Morgan", ActionCreate, MockNotionDataStore.Page1Id, new Dictionary<string, string>
            {
                ["title"] = "CF32 Audit Workspace"
            });
            AddSeed("cf32-audit-002", E2ESeedNow.AddMinutes(-42), "bob", "Bob Stone", ActionEdit, MockNotionDataStore.Page1Id, new Dictionary<string, string>
            {
                ["field"] = "Description"
            });
            AddSeed("cf32-audit-003", E2ESeedNow.AddMinutes(-31), "alice", "Alice Morgan", ActionMove, MockNotionDataStore.Page2Id, new Dictionary<string, string>
            {
                ["newParentId"] = MockNotionDataStore.Page1Id.ToString("D")
            });
            AddSeed("cf32-audit-004", E2ESeedNow.AddMinutes(-18), "security", "Security Lead", ActionRestrict, MockNotionDataStore.Page1Id, new Dictionary<string, string>
            {
                ["mode"] = "Restricted"
            });
        }
    }

    public void SeedE2EEmptyAuditPage()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
        }
    }

    public void SeedE2EManyAuditEntries()
    {
        lock (_syncRoot)
        {
            _entries.Clear();
            var actions = new[] { ActionCreate, ActionEdit, ActionMove, ActionRestrict, ActionDelete };
            for (var i = 0; i < 26; i++)
            {
                var action = actions[i % actions.Length];
                AddSeed(
                    $"cf32-audit-many-{i + 1:00}",
                    E2ESeedNow.AddMinutes(-i),
                    i % 2 == 0 ? "alice" : "demo",
                    i % 2 == 0 ? "Alice Morgan" : "Demo User",
                    action,
                    i % 3 == 0 ? MockNotionDataStore.Page2Id : MockNotionDataStore.Page1Id,
                    new Dictionary<string, string>
                    {
                        ["title"] = $"CF32 paging entry {i + 1:00}"
                    });
            }
        }
    }

    private void AddSeed(string id, DateTime timestamp, string userId, string userDisplayName, string action, Guid pageId, IReadOnlyDictionary<string, string> details)
        => _entries.Add(new AuditEntryDto
        {
            Id = id,
            Timestamp = timestamp,
            UserId = userId,
            UserDisplayName = userDisplayName,
            Action = action,
            TargetType = "page",
            TargetId = pageId.ToString("D"),
            Details = details
        });

    private static AuditEntryDto Normalize(AuditEntryDto entry)
    {
        var userId = string.IsNullOrWhiteSpace(entry.UserId) ? "demo" : entry.UserId.Trim();
        return new AuditEntryDto
        {
            Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id.Trim(),
            Timestamp = entry.Timestamp == default ? DateTime.UtcNow : EnsureUtc(entry.Timestamp),
            UserId = userId,
            UserDisplayName = string.IsNullOrWhiteSpace(entry.UserDisplayName) ? userId : entry.UserDisplayName.Trim(),
            Action = entry.Action.Trim(),
            TargetType = entry.TargetType.Trim(),
            TargetId = entry.TargetId.Trim(),
            Details = entry.Details
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static AuditEntryDto Clone(AuditEntryDto entry)
        => new()
        {
            Id = entry.Id,
            Timestamp = entry.Timestamp,
            UserId = entry.UserId,
            UserDisplayName = entry.UserDisplayName,
            Action = entry.Action,
            TargetType = entry.TargetType,
            TargetId = entry.TargetId,
            Details = entry.Details.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };

    private static bool Matches(AuditEntryDto entry, AuditLogFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.UserId) && !Contains(entry.UserId, filter.UserId) && !Contains(entry.UserDisplayName, filter.UserId))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.Action) && !string.Equals(entry.Action, filter.Action, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.TargetType) && !string.Equals(entry.TargetType, filter.TargetType, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.TargetId) && !string.Equals(entry.TargetId, filter.TargetId, StringComparison.OrdinalIgnoreCase))
            return false;

        var entryDate = DateOnly.FromDateTime(entry.Timestamp.ToUniversalTime());
        return (filter.From is null || entryDate >= filter.From.Value)
            && (filter.To is null || entryDate <= filter.To.Value);
    }

    private static bool Contains(string value, string filter)
        => value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static DateTime EnsureUtc(DateTime timestamp)
        => timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };
}
