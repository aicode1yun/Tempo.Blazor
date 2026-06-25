using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionAuditContractTests
{
    [Fact]
    public void TmActivityEntry_RoundtripsThroughJson()
    {
        var entry = Entry("audit-001", "alice", "edit", new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero));
        entry.Metadata = new Dictionary<string, object>
        {
            ["title"] = "Audit workspace",
            ["field"] = "Description"
        };

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<TmActivityEntry>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        restored.Should().NotBeNull();
        restored!.Id.Should().Be(entry.Id);
        restored.Actor!.Id.Should().Be("alice");
        restored.Action.Should().Be("edit");
        restored.EntityRef.EntityType.Should().Be("page");
        restored.EntityRef.EntityId.Should().Be("audit-001");
        restored.Metadata!["title"].ToString().Should().Be("Audit workspace");
    }

    [Fact]
    public async Task ITmActivityProvider_FiltersAndPagesEntries()
    {
        ITmActivityProvider provider = new InMemoryActivityProvider();
        await provider.AppendAsync(Entry("1", "alice", "create", new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero)));
        await provider.AppendAsync(Entry("2", "bob", "edit", new DateTimeOffset(2026, 1, 11, 8, 0, 0, TimeSpan.Zero)));
        await provider.AppendAsync(Entry("3", "alice", "edit", new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero)));

        var result = await provider.QueryAsync(new TmActivityQuery
        {
            SearchText = "alice",
            Action = "edit",
            From = new DateTimeOffset(2026, 1, 11, 0, 0, 0, TimeSpan.Zero),
            To = new DateTimeOffset(2026, 1, 12, 23, 59, 59, TimeSpan.Zero),
            Skip = 0,
            Take = 1
        });

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Id.Should().Be("3");
        result.Page.Should().Be(1);
    }

    private static TmActivityEntry Entry(string id, string userId, string action, DateTimeOffset timestamp)
        => new()
        {
            Id = id,
            Timestamp = timestamp,
            Actor = new TmUserRef { Id = userId, DisplayName = userId },
            Action = action,
            EntityRef = TmEntityRef.Create("page", id),
            Metadata = new Dictionary<string, object> { ["title"] = $"Entry {id}" }
        };

    private sealed class InMemoryActivityProvider : ITmActivityProvider
    {
        private readonly List<TmActivityEntry> _entries = [];

        public TmActivityProviderCapabilities Capabilities
            => TmActivityProviderCapabilities.Read
            | TmActivityProviderCapabilities.Query
            | TmActivityProviderCapabilities.Append;

        TmActivityProviderCapabilities ITmCapabilityProvider<TmActivityProviderCapabilities>.Capabilities => Capabilities;

        public Task<IReadOnlyList<TmActivityEntry>> GetForEntityAsync(TmEntityRef entityRef, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TmActivityEntry>>(_entries.Where(entry => entry.EntityRef.Equals(entityRef)).ToArray());

        public Task<TmActivityEntry> AppendAsync(TmActivityEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.FromResult(entry);
        }

        public Task<PagedResult<TmActivityEntry>> QueryAsync(TmActivityQuery query, CancellationToken cancellationToken = default)
        {
            var matches = _entries
                .Where(entry => string.IsNullOrWhiteSpace(query.SearchText)
                    || entry.Actor?.Id.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) == true
                    || entry.Actor?.DisplayName.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) == true)
                .Where(entry => string.IsNullOrWhiteSpace(query.Action) || string.Equals(entry.Action, query.Action, StringComparison.OrdinalIgnoreCase))
                .Where(entry => query.From is null || entry.Timestamp >= query.From.Value)
                .Where(entry => query.To is null || entry.Timestamp <= query.To.Value)
                .OrderByDescending(entry => entry.Timestamp)
                .ToArray();

            var take = Math.Clamp(query.Take, 1, 100);
            return Task.FromResult(new PagedResult<TmActivityEntry>
            {
                Items = matches.Skip(Math.Max(0, query.Skip)).Take(take).ToArray(),
                TotalCount = matches.Length,
                Page = Math.Max(0, query.Skip) / take + 1,
                PageSize = take
            });
        }
    }
}
