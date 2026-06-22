using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoNotionAuditProvider : ITmActivityProvider
{
    public const string ActionCreate = "create";
    public const string ActionEdit = "edit";
    public const string ActionDelete = "delete";
    public const string ActionMove = "move";
    public const string ActionRestrict = "restrict";

    private static readonly DateTimeOffset E2ESeedNow = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
    private readonly object _syncRoot = new();
    private readonly List<TmActivityEntry> _entries = [];

    public DemoNotionAuditProvider()
    {
        Reset();
    }

    public TmActivityProviderCapabilities Capabilities
        => TmActivityProviderCapabilities.Read
        | TmActivityProviderCapabilities.Query
        | TmActivityProviderCapabilities.Append;

    TmActivityProviderCapabilities ITmCapabilityProvider<TmActivityProviderCapabilities>.Capabilities => Capabilities;

    public Task<IReadOnlyList<TmActivityEntry>> GetForEntityAsync(
        TmEntityRef entityRef,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var matches = _entries
                .Where(entry => entry.EntityRef.Equals(entityRef))
                .OrderByDescending(entry => entry.Timestamp)
                .ThenByDescending(entry => entry.Id, StringComparer.Ordinal)
                .Select(Clone)
                .ToArray();

            return Task.FromResult<IReadOnlyList<TmActivityEntry>>(matches);
        }
    }

    public Task<TmActivityEntry> AppendAsync(TmActivityEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = Normalize(entry);
        lock (_syncRoot)
        {
            _entries.Add(normalized);
        }

        return Task.FromResult(Clone(normalized));
    }

    public Task<PagedResult<TmActivityEntry>> QueryAsync(
        TmActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take, 1, 100);

        lock (_syncRoot)
        {
            var matches = _entries
                .Where(entry => Matches(entry, query))
                .OrderByDescending(entry => entry.Timestamp)
                .ThenByDescending(entry => entry.Id, StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult(new PagedResult<TmActivityEntry>
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
            AddSeed("audit-seed-001", DateTimeOffset.UtcNow.AddHours(-8), "alice", "Alice Morgan", ActionCreate, MockNotionDataStore.Page1Id, new Dictionary<string, string>
            {
                ["title"] = "Getting Started with Notion Editor"
            });
            AddSeed("audit-seed-002", DateTimeOffset.UtcNow.AddHours(-5), "demo", "Demo User", ActionEdit, MockNotionDataStore.Page1Id, new Dictionary<string, string>
            {
                ["field"] = "Title"
            });
            AddSeed("audit-seed-003", DateTimeOffset.UtcNow.AddHours(-2), "grace", "Grace Hopper", ActionMove, MockNotionDataStore.Page2Id, new Dictionary<string, string>
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

    private void AddSeed(string id, DateTimeOffset timestamp, string userId, string userDisplayName, string action, Guid pageId, IReadOnlyDictionary<string, string> details)
        => _entries.Add(Normalize(new TmActivityEntry
        {
            Id = id,
            Timestamp = timestamp,
            Actor = new TmUserRef { Id = userId, DisplayName = userDisplayName },
            Action = action,
            EntityRef = TmEntityRef.Create("page", pageId.ToString("D")),
            Metadata = details.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.OrdinalIgnoreCase)
        }));

    private static TmActivityEntry Normalize(TmActivityEntry entry)
    {
        var actorId = string.IsNullOrWhiteSpace(entry.Actor?.Id) ? "demo" : entry.Actor!.Id.Trim();
        var actorName = string.IsNullOrWhiteSpace(entry.Actor?.DisplayName) ? actorId : entry.Actor!.DisplayName.Trim();
        return new TmActivityEntry
        {
            Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id.Trim(),
            Timestamp = entry.Timestamp == default ? DateTimeOffset.UtcNow : entry.Timestamp.ToUniversalTime(),
            Actor = new TmUserRef
            {
                Id = actorId,
                DisplayName = actorName,
                UserName = CleanOptional(entry.Actor?.UserName),
                Email = CleanOptional(entry.Actor?.Email),
                AvatarUrl = CleanOptional(entry.Actor?.AvatarUrl),
                Color = CleanOptional(entry.Actor?.Color),
                IsVirtual = entry.Actor?.IsVirtual == true,
                SourceKey = CleanOptional(entry.Actor?.SourceKey),
                TenantId = CleanOptional(entry.Actor?.TenantId)
            },
            Action = CleanRequired(entry.Action),
            EntityRef = entry.EntityRef.IsValid ? entry.EntityRef.Normalize() : TmEntityRef.Create("page", CleanRequired(entry.EntityRef.EntityId)),
            Summary = CleanOptional(entry.Summary),
            Before = CleanOptional(entry.Before),
            After = CleanOptional(entry.After),
            Diff = CleanOptional(entry.Diff),
            CorrelationId = CleanOptional(entry.CorrelationId),
            Metadata = NormalizeMetadata(entry.Metadata)
        };
    }

    private static TmActivityEntry Clone(TmActivityEntry entry)
        => new()
        {
            Id = entry.Id,
            EntityRef = entry.EntityRef.Normalize(),
            Actor = entry.Actor is null
                ? null
                : new TmUserRef
                {
                    Id = entry.Actor.Id,
                    DisplayName = entry.Actor.DisplayName,
                    UserName = entry.Actor.UserName,
                    Email = entry.Actor.Email,
                    AvatarUrl = entry.Actor.AvatarUrl,
                    Color = entry.Actor.Color,
                    IsVirtual = entry.Actor.IsVirtual,
                    SourceKey = entry.Actor.SourceKey,
                    TenantId = entry.Actor.TenantId
                },
            Action = entry.Action,
            Timestamp = entry.Timestamp,
            Summary = entry.Summary,
            Before = entry.Before,
            After = entry.After,
            Diff = entry.Diff,
            CorrelationId = entry.CorrelationId,
            Metadata = entry.Metadata?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };

    private static bool Matches(TmActivityEntry entry, TmActivityQuery query)
    {
        if (query.EntityRef?.IsValid == true && !entry.EntityRef.Equals(query.EntityRef))
            return false;

        if (!string.IsNullOrWhiteSpace(query.EntityType) && !string.Equals(entry.EntityRef.EntityType, query.EntityType, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(query.EntityId) && !string.Equals(entry.EntityRef.EntityId, query.EntityId, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(query.ActorId) && !string.Equals(entry.Actor?.Id, query.ActorId, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(query.SearchText)
            && !Contains(entry.Actor?.Id ?? string.Empty, query.SearchText)
            && !Contains(entry.Actor?.DisplayName ?? string.Empty, query.SearchText)
            && !Contains(entry.Summary ?? string.Empty, query.SearchText))
            return false;

        if (!string.IsNullOrWhiteSpace(query.Action) && !string.Equals(entry.Action, query.Action, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(query.CorrelationId) && !string.Equals(entry.CorrelationId, query.CorrelationId, StringComparison.Ordinal))
            return false;

        return (query.From is null || entry.Timestamp >= query.From.Value)
            && (query.To is null || entry.Timestamp <= query.To.Value);
    }

    private static Dictionary<string, object>? NormalizeMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata is null)
            return null;

        var result = metadata
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return result.Count == 0 ? null : result;
    }

    private static bool Contains(string value, string filter)
        => value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string CleanRequired(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? CleanOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
