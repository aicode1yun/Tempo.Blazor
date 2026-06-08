using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class MockNotionReactionStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, Dictionary<string, HashSet<string>>> _pageReactions = new();

    public MockNotionReactionStore()
    {
        Reset();
    }

    public IReadOnlyList<PageReactionDto> GetReactions(Guid pageId)
    {
        lock (_syncRoot)
        {
            return Snapshot(pageId);
        }
    }

    public IReadOnlyList<PageReactionDto> ToggleLike(Guid pageId, string userId)
        => ToggleReaction(pageId, PageReactionDto.LikeReaction, userId);

    public IReadOnlyList<PageReactionDto> ToggleReaction(Guid pageId, string reaction, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", nameof(userId));

        if (string.IsNullOrWhiteSpace(reaction))
            throw new ArgumentException("Reaction is required.", nameof(reaction));

        lock (_syncRoot)
        {
            var reactions = GetPageBucket(pageId);
            if (!reactions.TryGetValue(reaction, out var users))
            {
                users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                reactions[reaction] = users;
            }

            if (!users.Add(userId))
                users.Remove(userId);

            if (users.Count == 0)
                reactions.Remove(reaction);

            return Snapshot(pageId);
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            _pageReactions.Clear();
            SeedReaction(MockNotionDataStore.Page1Id, PageReactionDto.LikeReaction, "alice");
            SeedReaction(MockNotionDataStore.Page1Id, "🎉", "bob");
            SeedReaction(MockNotionDataStore.Page2Id, PageReactionDto.LikeReaction, "bob");
        }
    }

    public void SeedE2EEmptyPage()
    {
        lock (_syncRoot)
        {
            _pageReactions.Clear();
        }
    }

    public void SeedE2EManyPage()
    {
        lock (_syncRoot)
        {
            _pageReactions.Clear();
            SeedReaction(MockNotionDataStore.Page1Id, PageReactionDto.LikeReaction, "ben");
            SeedReaction(MockNotionDataStore.Page1Id, "🎉", "ben");
            SeedReaction(MockNotionDataStore.Page1Id, "🎉", "camila");
            SeedReaction(MockNotionDataStore.Page1Id, "❤️", "dina");
            SeedReaction(MockNotionDataStore.Page1Id, "👀", "eli");
            SeedReaction(MockNotionDataStore.Page1Id, "✅", "frank");
            SeedReaction(MockNotionDataStore.Page1Id, "👍", "grace");
        }
    }

    private Dictionary<string, HashSet<string>> GetPageBucket(Guid pageId)
    {
        if (!_pageReactions.TryGetValue(pageId, out var reactions))
        {
            reactions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            _pageReactions[pageId] = reactions;
        }

        return reactions;
    }

    private void SeedReaction(Guid pageId, string reaction, string userId)
    {
        var reactions = GetPageBucket(pageId);
        if (!reactions.TryGetValue(reaction, out var users))
        {
            users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            reactions[reaction] = users;
        }

        users.Add(userId);
    }

    private IReadOnlyList<PageReactionDto> Snapshot(Guid pageId)
    {
        if (!_pageReactions.TryGetValue(pageId, out var reactions))
            return [];

        return reactions
            .Where(pair => pair.Value.Count > 0)
            .OrderBy(pair => pair.Key == PageReactionDto.LikeReaction ? 0 : 1)
            .ThenByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new PageReactionDto
            {
                Reaction = pair.Key,
                UserIds = pair.Value.OrderBy(userId => userId, StringComparer.OrdinalIgnoreCase).ToArray()
            })
            .ToArray();
    }
}
