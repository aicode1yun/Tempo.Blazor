using System.Text.RegularExpressions;

namespace Tempo.Blazor.NotionEditor.Helpers;

/// <summary>
/// Parses and transforms <c>@username</c> mentions in comment text.
/// </summary>
public static class MentionParser
{
    private static readonly Regex MentionRegex = new(
        @"@([a-zA-Z0-9_-]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Extracts all unique usernames (without the leading @) from text.</summary>
    public static IEnumerable<string> ExtractUsernames(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        return MentionRegex.Matches(text)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Replaces every <c>@username</c> with a <c>&lt;span class="tm-mention" data-user-id="..."&gt;@username&lt;/span&gt;</c>
    /// when the resolver returns a non-null user id. Unresolved mentions are left as plain text.
    /// </summary>
    public static string ReplaceMentions(string text, Func<string, string?> userIdResolver)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return MentionRegex.Replace(text, match =>
        {
            var username = match.Groups[1].Value;
            var userId = userIdResolver(username);
            if (string.IsNullOrEmpty(userId))
                return match.Value;

            var encodedUserId = System.Net.WebUtility.HtmlEncode(userId);
            var encodedUsername = System.Net.WebUtility.HtmlEncode(username);
            return $"<span class=\"tm-mention\" data-user-id=\"{encodedUserId}\">@{encodedUsername}</span>";
        });
    }
}
