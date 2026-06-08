using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public class DemoNotionAnalyticsProvider : INotionAnalyticsProvider
{
    private static readonly DateTime E2ESeedNow = new(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);

    private readonly object _syncRoot = new();
    private readonly MockNotionDataStore _dataStore;
    private readonly Dictionary<Guid, PageAnalyticsState> _analytics = new();

    public DemoNotionAnalyticsProvider(MockNotionDataStore dataStore)
    {
        _dataStore = dataStore;
        Reset();
    }

    public Task RecordViewAsync(Guid pageId, string? userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var visitorId = string.IsNullOrWhiteSpace(userId)
            ? $"anonymous:{DateTime.UtcNow:yyyyMMddHHmm}"
            : userId.Trim();
        var now = DateTime.UtcNow;
        var day = DateOnly.FromDateTime(now);

        lock (_syncRoot)
        {
            var state = GetOrCreateState(pageId);
            state.Views++;
            state.LastViewedAt = now;
            state.UniqueVisitors.Add(visitorId);
            state.ViewsByDay[day] = state.ViewsByDay.GetValueOrDefault(day) + 1;
        }

        return Task.CompletedTask;
    }

    public Task<PageAnalyticsDto?> GetPageAnalyticsAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_syncRoot)
        {
            return Task.FromResult(_analytics.TryGetValue(pageId, out var analytics) ? ToDto(analytics) : null);
        }
    }

    public async Task<IReadOnlyList<PageAnalyticsDto>> GetTopPagesAsync(string spaceId, NotionAnalyticsRange range, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spaceId);
        cancellationToken.ThrowIfCancellationRequested();

        var pageIds = (await _dataStore.GetPagesInSpaceAsync(spaceId, cancellationToken))
            .Where(page => !page.IsDeleted)
            .Select(page => page.Id)
            .ToHashSet();
        var take = Math.Clamp(range.Take, 1, 50);

        lock (_syncRoot)
        {
            return _analytics.Values
                .Where(state => pageIds.Contains(state.PageId))
                .Select(state => ToDto(state, range))
                .Where(dto => dto.Views > 0)
                .OrderByDescending(dto => dto.Views)
                .ThenByDescending(dto => dto.LastViewedAt)
                .Take(take)
                .ToArray();
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            _analytics.Clear();
            AddSeed(MockNotionDataStore.Page1Id, 84, ["ada", "grace", "linus", "margaret"], DateTime.UtcNow.AddHours(-3));
            AddSeed(MockNotionDataStore.Page2Id, 143, ["ada", "grace", "linus", "margaret", "alan"], DateTime.UtcNow.AddHours(-8));
            AddSeed(MockNotionDataStore.Page3Id, 57, ["ada", "grace", "linus"], DateTime.UtcNow.AddDays(-1));
        }
    }

    public void SeedE2EPageInfoPage()
    {
        lock (_syncRoot)
        {
            _analytics.Clear();
            AddSeed(MockNotionDataStore.Page1Id, 127, ["ada", "grace", "linus", "margaret"], E2ESeedNow.AddHours(-1));
        }
    }

    public void SeedE2EEmptyPageInfoPage()
    {
        lock (_syncRoot)
        {
            _analytics.Clear();
        }
    }

    public void SeedE2EAnalyticsPage()
    {
        lock (_syncRoot)
        {
            var now = DateTime.UtcNow;
            _analytics.Clear();
            AddSeed(MockNotionDataStore.Page1Id, 12, ["ada", "grace", "linus"], now.AddMinutes(-25));
            AddSeed(MockNotionDataStore.Page2Id, 29, ["ada", "grace", "linus", "margaret", "alan"], now.AddHours(-2));
            AddSeed(MockNotionDataStore.Page4Id, 18, ["ada", "grace", "linus", "margaret"], now.AddHours(-4));
        }
    }

    public void SeedE2EEmptyAnalyticsPage()
    {
        lock (_syncRoot)
        {
            _analytics.Clear();
        }
    }

    private PageAnalyticsState GetOrCreateState(Guid pageId)
    {
        if (_analytics.TryGetValue(pageId, out var existing))
            return existing;

        var created = new PageAnalyticsState(pageId);
        _analytics[pageId] = created;
        return created;
    }

    private void AddSeed(Guid pageId, int views, IReadOnlyList<string> visitors, DateTime lastViewedAt)
    {
        var state = GetOrCreateState(pageId);
        state.Views = views;
        state.LastViewedAt = lastViewedAt;
        foreach (var visitor in visitors)
            state.UniqueVisitors.Add(visitor);

        var lastDay = DateOnly.FromDateTime(lastViewedAt.Date);
        state.ViewsByDay[lastDay.AddDays(-5)] = Math.Max(1, views / 8);
        state.ViewsByDay[lastDay.AddDays(-4)] = Math.Max(1, views / 7);
        state.ViewsByDay[lastDay.AddDays(-3)] = Math.Max(1, views / 6);
        state.ViewsByDay[lastDay.AddDays(-2)] = Math.Max(1, views / 5);
        state.ViewsByDay[lastDay.AddDays(-1)] = Math.Max(1, views / 4);
        state.ViewsByDay[lastDay] = Math.Max(1, views - state.ViewsByDay.Values.Sum());
    }

    private static PageAnalyticsDto ToDto(PageAnalyticsState state, NotionAnalyticsRange? range = null)
    {
        var points = state.ViewsByDay
            .Where(point => range?.From is null || point.Key >= range.From.Value)
            .Where(point => range?.To is null || point.Key <= range.To.Value)
            .OrderBy(point => point.Key)
            .Select(point => new PageAnalyticsPointDto { Date = point.Key, Views = point.Value })
            .ToArray();

        var views = range is null ? state.Views : points.Sum(point => point.Views);

        return new PageAnalyticsDto
        {
            PageId = state.PageId,
            Views = views,
            UniqueVisitors = state.UniqueVisitors.Count,
            LastViewedAt = state.LastViewedAt,
            ViewsByDay = points
        };
    }

    private sealed class PageAnalyticsState(Guid pageId)
    {
        public Guid PageId { get; } = pageId;
        public int Views { get; set; }
        public HashSet<string> UniqueVisitors { get; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTime? LastViewedAt { get; set; }
        public Dictionary<DateOnly, int> ViewsByDay { get; } = [];
    }
}

public sealed class MockNotionAnalyticsStore : DemoNotionAnalyticsProvider
{
    public MockNotionAnalyticsStore(MockNotionDataStore dataStore)
        : base(dataStore)
    {
    }
}
