using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionAnalyticsContractTests
{
    [Fact]
    public void PageAnalyticsDto_RoundtripsThroughJson()
    {
        var dto = new PageAnalyticsDto
        {
            PageId = Guid.Parse("cf310000-0000-0000-0000-000000000001"),
            Views = 42,
            UniqueVisitors = 7,
            LastViewedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            ViewsByDay =
            [
                new PageAnalyticsPointDto { Date = new DateOnly(2026, 1, 14), Views = 12 },
                new PageAnalyticsPointDto { Date = new DateOnly(2026, 1, 15), Views = 30 }
            ]
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<PageAnalyticsDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        restored.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task INotionAnalyticsProvider_RecordsViewsUniqueVisitorsAndTopPages()
    {
        var provider = new InMemoryAnalyticsProvider();
        var pageA = Guid.Parse("cf310000-0000-0000-0000-000000000001");
        var pageB = Guid.Parse("cf310000-0000-0000-0000-000000000002");

        await provider.RecordViewAsync(pageA, "ada");
        await provider.RecordViewAsync(pageA, "ada");
        await provider.RecordViewAsync(pageA, "grace");
        await provider.RecordViewAsync(pageB, "linus");

        var pageAnalytics = await provider.GetPageAnalyticsAsync(pageA);
        var topPages = await provider.GetTopPagesAsync("team", new NotionAnalyticsRange { Take = 2 });

        pageAnalytics.Should().NotBeNull();
        pageAnalytics!.Views.Should().Be(3);
        pageAnalytics.UniqueVisitors.Should().Be(2);
        pageAnalytics.ViewsByDay.Should().ContainSingle().Which.Views.Should().Be(3);
        topPages.Select(page => page.PageId).Should().Equal(pageA, pageB);
    }

    private sealed class InMemoryAnalyticsProvider : INotionAnalyticsProvider
    {
        private readonly Dictionary<Guid, AnalyticsState> _states = [];

        public Task RecordViewAsync(Guid pageId, string? userId, CancellationToken cancellationToken = default)
        {
            var state = GetOrCreate(pageId);
            var day = DateOnly.FromDateTime(DateTime.UtcNow);
            state.Views++;
            state.Visitors.Add(userId ?? "anonymous");
            state.LastViewedAt = DateTime.UtcNow;
            state.ViewsByDay[day] = state.ViewsByDay.GetValueOrDefault(day) + 1;
            return Task.CompletedTask;
        }

        public Task<PageAnalyticsDto?> GetPageAnalyticsAsync(Guid pageId, CancellationToken cancellationToken = default)
            => Task.FromResult(_states.TryGetValue(pageId, out var state) ? ToDto(state) : null);

        public Task<IReadOnlyList<PageAnalyticsDto>> GetTopPagesAsync(string spaceId, NotionAnalyticsRange range, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PageAnalyticsDto>>(_states.Values
                .Select(ToDto)
                .OrderByDescending(dto => dto.Views)
                .Take(range.Take)
                .ToArray());

        private AnalyticsState GetOrCreate(Guid pageId)
        {
            if (_states.TryGetValue(pageId, out var state))
                return state;

            state = new AnalyticsState(pageId);
            _states[pageId] = state;
            return state;
        }

        private static PageAnalyticsDto ToDto(AnalyticsState state)
            => new()
            {
                PageId = state.PageId,
                Views = state.Views,
                UniqueVisitors = state.Visitors.Count,
                LastViewedAt = state.LastViewedAt,
                ViewsByDay = state.ViewsByDay
                    .OrderBy(point => point.Key)
                    .Select(point => new PageAnalyticsPointDto { Date = point.Key, Views = point.Value })
                    .ToArray()
            };

        private sealed class AnalyticsState(Guid pageId)
        {
            public Guid PageId { get; } = pageId;
            public int Views { get; set; }
            public HashSet<string> Visitors { get; } = new(StringComparer.OrdinalIgnoreCase);
            public DateTime? LastViewedAt { get; set; }
            public Dictionary<DateOnly, int> ViewsByDay { get; } = [];
        }
    }
}
