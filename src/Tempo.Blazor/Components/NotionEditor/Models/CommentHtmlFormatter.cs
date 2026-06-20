using System.Text;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.Components.NotionEditor.Models;

/// <summary>Transforms plain-text quote lines (starting with '>') into HTML blockquotes.</summary>
public static class CommentHtmlFormatter
{
    /// <summary>
    /// Finds consecutive lines starting with <c>&gt;</c> and wraps them in
    /// <c>&lt;blockquote class="tm-comment-quote"&gt;...&lt;/blockquote&gt;</c>.
    /// Existing <c>&lt;br&gt;</c> tags are normalized to newlines first.
    /// </summary>
    public static string Format(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;

        // Normalize <br> and <br/> to newlines so we can split by lines
        var normalized = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

        var lines = normalized.Split('\n');
        var sb = new StringBuilder();
        var inQuote = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var isQuoteLine = line.TrimStart().StartsWith(">");

            if (isQuoteLine && !inQuote)
            {
                sb.Append("<blockquote class=\"tm-comment-quote\">");
                inQuote = true;
            }
            else if (!isQuoteLine && inQuote)
            {
                sb.Append("</blockquote>");
                inQuote = false;
            }

            if (isQuoteLine)
            {
                // Strip the leading '>' and optional space
                var content = line.TrimStart();
                if (content.Length > 0 && content[0] == '>')
                    content = content[1..].TrimStart();
                sb.Append(content);
                if (i < lines.Length - 1)
                    sb.Append("<br>");
            }
            else
            {
                sb.Append(line);
                if (i < lines.Length - 1)
                    sb.Append("<br>");
            }
        }

        if (inQuote)
            sb.Append("</blockquote>");

        return sb.ToString();
    }
}
