using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionAuditContractTests
{
    [Fact]
    public void AuditEntryDto_RoundtripsThroughJson()
    {
        var dto = new AuditEntryDto
        {
            Id = "audit-001",
            Timestamp = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            UserId = "alice",
            UserDisplayName = "Alice Morgan",
            Action = "edit",
            TargetType = "page",
            TargetId = "11111111-1111-1111-1111-111111111111",
            Details = new Dictionary<string, string>
            {
                ["title"] = "Audit workspace",
                ["field"] = "Description"
            }
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<AuditEntryDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        restored.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task INotionAuditProvider_FiltersAndPagesEntries()
    {
        var provider = new InMemoryAuditProvider();
        await provider.LogAsync(Entry("1", "alice", "create", new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc)));
        await provider.LogAsync(Entry("2", "bob", "edit", new DateTime(2026, 1, 11, 8, 0, 0, DateTimeKind.Utc)));
        await provider.LogAsync(Entry("3", "alice", "edit", new DateTime(2026, 1, 12, 8, 0, 0, DateTimeKind.Utc)));

        var result = await provider.GetEntriesAsync(
            new AuditLogFilter
            {
                UserId = "alice",
                Action = "edit",
                From = new DateOnly(2026, 1, 11),
                To = new DateOnly(2026, 1, 12)
            },
            new NotionAuditQuery { Skip = 0, Take = 1 });

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Id.Should().Be("3");
        result.Page.Should().Be(1);
    }

    private static AuditEntryDto Entry(string id, string userId, string action, DateTime timestamp)
        => new()
        {
            Id = id,
            Timestamp = timestamp,
            UserId = userId,
            UserDisplayName = userId,
            Action = action,
            TargetType = "page",
            TargetId = id,
            Details = new Dictionary<string, string> { ["title"] = $"Entry {id}" }
        };

    private sealed class InMemoryAuditProvider : INotionAuditProvider
    {
        private readonly List<AuditEntryDto> _entries = [];

        public Task LogAsync(AuditEntryDto entry, CancellationToken cancellationToken = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<PagedResult<AuditEntryDto>> GetEntriesAsync(AuditLogFilter filter, NotionAuditQuery paging, CancellationToken cancellationToken = default)
        {
            var matches = _entries
                .Where(entry => string.IsNullOrWhiteSpace(filter.UserId) || entry.UserId.Contains(filter.UserId, StringComparison.OrdinalIgnoreCase))
                .Where(entry => string.IsNullOrWhiteSpace(filter.Action) || string.Equals(entry.Action, filter.Action, StringComparison.OrdinalIgnoreCase))
                .Where(entry => filter.From is null || DateOnly.FromDateTime(entry.Timestamp) >= filter.From.Value)
                .Where(entry => filter.To is null || DateOnly.FromDateTime(entry.Timestamp) <= filter.To.Value)
                .OrderByDescending(entry => entry.Timestamp)
                .ToArray();

            var take = Math.Clamp(paging.Take, 1, 100);
            return Task.FromResult(new PagedResult<AuditEntryDto>
            {
                Items = matches.Skip(Math.Max(0, paging.Skip)).Take(take).ToArray(),
                TotalCount = matches.Length,
                Page = Math.Max(0, paging.Skip) / take + 1,
                PageSize = take
            });
        }
    }
}
