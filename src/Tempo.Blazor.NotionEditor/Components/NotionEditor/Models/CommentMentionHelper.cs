using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Components.NotionEditor.Models;

/// <summary>
/// Helper for encoding @mentions in comment text and sending mention notifications.
/// </summary>
public static class CommentMentionHelper
{
    /// <summary>
    /// Replaces <c>@username</c> mentions with HTML span tags containing <c>data-user-id</c>.
    /// Unresolved mentions are left as plain text.
    /// </summary>
    public static async Task<string> EncodeAsync(string text, ITmPeopleProvider? mentionProvider)
    {
        if (mentionProvider is null || string.IsNullOrEmpty(text))
            return text;

        var usernames = MentionParser.ExtractUsernames(text).ToList();
        if (usernames.Count == 0)
            return text;

        var userMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var username in usernames)
        {
            try
            {
                var match = await ResolveUserAsync(mentionProvider, username);
                if (match is not null)
                    userMap[username] = match.Id;
            }
            catch { /* best-effort */ }
        }

        if (userMap.Count == 0)
            return text;

        return MentionParser.ReplaceMentions(text, u =>
            userMap.TryGetValue(u, out var id) ? id : null);
    }

    /// <summary>
    /// Notifies users mentioned in <paramref name="rawText"/> that they were mentioned in a comment entry.
    /// </summary>
    public static async Task NotifyAsync(
        string rawText,
        TmCommentEntry entry,
        string threadId,
        string pageId,
        ITmPeopleProvider? mentionProvider,
        CommentNotificationOrchestrator? orchestrator)
    {
        if (orchestrator is null || mentionProvider is null || string.IsNullOrEmpty(rawText))
            return;

        var usernames = MentionParser.ExtractUsernames(rawText).ToList();
        if (usernames.Count == 0)
            return;

        var userIds = new List<string>();
        foreach (var username in usernames)
        {
            try
            {
                var match = await ResolveUserAsync(mentionProvider, username);
                if (match is not null && match.Id != entry.Author.Id)
                    userIds.Add(match.Id);
            }
            catch { /* best-effort */ }
        }

        if (userIds.Count > 0)
            await orchestrator.OnMentionAsync(entry, userIds, threadId, pageId);
    }

    private static async Task<TmUser?> ResolveUserAsync(ITmPeopleProvider mentionProvider, string username)
    {
        var user = await mentionProvider.GetByIdAsync(username);
        if (user is not null)
            return user;

        var users = await mentionProvider.SearchAsync(new TmPeopleQuery { SearchText = username, Take = 8 });
        return users.FirstOrDefault(u =>
            string.Equals(u.Id, username, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.UserName, username, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.DisplayName, username, StringComparison.OrdinalIgnoreCase));
    }
}
