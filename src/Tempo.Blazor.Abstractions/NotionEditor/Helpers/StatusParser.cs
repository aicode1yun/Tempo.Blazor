using System.Net;
using System.Text.RegularExpressions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Helpers;

/// <summary>Extracts inline status chips from stored Notion editor HTML.</summary>
public static class StatusParser
{
    private static readonly Regex StatusRegex = new(
        @"<span\b(?=[^>]*\btm-notion-status\b)(?<attrs>[^>]*)>(?<inner>.*?)</span>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[\w:-]+)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClassColorRegex = new(
        @"(?:^|\s)tm-notion-status--(?<color>gray|blue|green|yellow|red|purple)(?:\s|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex TagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <summary>Returns every inline status chip found in the supplied HTML fragment.</summary>
    public static IReadOnlyList<InlineStatus> ExtractStatuses(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var statuses = new List<InlineStatus>();
        foreach (Match match in StatusRegex.Matches(html))
        {
            var attributes = ParseAttributes(match.Groups["attrs"].Value);
            var label = ResolveLabel(attributes, match.Groups["inner"].Value);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var color = ResolveColor(attributes);
            statuses.Add(new InlineStatus(label, color));
        }

        return statuses;
    }

    private static Dictionary<string, string> ParseAttributes(string attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attributes))
        {
            result[match.Groups["name"].Value] = WebUtility.HtmlDecode(match.Groups["value"].Value);
        }

        return result;
    }

    private static string ResolveLabel(IReadOnlyDictionary<string, string> attributes, string innerHtml)
    {
        if (attributes.TryGetValue("data-status-label", out var dataLabel))
        {
            var trimmed = dataLabel.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                return trimmed;
            }
        }

        var text = TagRegex.Replace(innerHtml, string.Empty);
        return WebUtility.HtmlDecode(text).Trim();
    }

    private static NotionStatusColor ResolveColor(IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.TryGetValue("data-status-color", out var dataColor) &&
            Enum.TryParse<NotionStatusColor>(dataColor, ignoreCase: true, out var color))
        {
            return color;
        }

        if (attributes.TryGetValue("class", out var classes))
        {
            var match = ClassColorRegex.Match(classes);
            if (match.Success &&
                Enum.TryParse<NotionStatusColor>(match.Groups["color"].Value, ignoreCase: true, out color))
            {
                return color;
            }
        }

        return NotionStatusColor.Gray;
    }
}
