using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>
/// Produces the plain-text alternative of a document: HTML stripped, links rendered as
/// <c>text (url)</c>, sections separated by blank lines, tables row-by-row.
/// </summary>
public sealed partial class TextVersionGenerator
{
    /// <summary>Generates the plain-text version of the document.</summary>
    public string Generate(EmailTemplateDocument document)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(document.Preheader))
            sb.AppendLine(document.Preheader).AppendLine();

        for (int i = 0; i < document.Sections.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            foreach (var column in document.Sections[i].Columns)
                foreach (var block in column.Blocks)
                    WriteBlock(sb, block);
        }

        return Collapse(sb.ToString());
    }

    private static void WriteBlock(StringBuilder sb, EmailBlockBase block)
    {
        switch (block)
        {
            case EmailTextBlock t:
                sb.AppendLine(StripHtml(t.Content));
                break;
            case EmailButtonBlock b:
                sb.AppendLine(string.IsNullOrEmpty(b.Href) ? b.Text : $"{b.Text} ({b.Href})");
                break;
            case EmailImageBlock i when !string.IsNullOrEmpty(i.Alt):
                sb.AppendLine(i.Alt);
                break;
            case EmailDividerBlock:
                sb.AppendLine("----------------------------------------");
                break;
            case EmailSpacerBlock:
                sb.AppendLine();
                break;
            case EmailRawBlock r:
                sb.AppendLine(StripHtml(r.Content));
                break;
            case EmailTableBlock table:
                foreach (var row in table.Rows)
                    sb.AppendLine(string.Join("\t", row.Cells.Select(c => c.Text)));
                break;
            case EmailSocialBlock social:
                foreach (var e in social.Elements)
                    sb.AppendLine(string.IsNullOrEmpty(e.Href) ? (e.Label ?? e.Name ?? "") : $"{e.Label ?? e.Name} ({e.Href})");
                break;
            case EmailNavbarBlock navbar:
                foreach (var link in navbar.Links)
                    sb.AppendLine(string.IsNullOrEmpty(link.Href) ? link.Text : $"{link.Text} ({link.Href})");
                break;
            case EmailCarouselBlock carousel:
                foreach (var img in carousel.Images.Where(im => !string.IsNullOrEmpty(im.Alt)))
                    sb.AppendLine(img.Alt);
                break;
            case EmailAccordionBlock accordion:
                foreach (var item in accordion.Items)
                {
                    sb.AppendLine(item.Title);
                    sb.AppendLine(StripHtml(item.Content));
                }
                break;
            case EmailHeroBlock hero:
                foreach (var b in hero.Blocks) WriteBlock(sb, b);
                break;
            case EmailGroupBlock group:
                foreach (var column in group.Columns)
                    foreach (var b in column.Blocks) WriteBlock(sb, b);
                break;
            case EmailWrapperBlock wrapper:
                foreach (var section in wrapper.Sections)
                    foreach (var column in section.Columns)
                        foreach (var b in column.Blocks) WriteBlock(sb, b);
                break;
        }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var withoutTags = TagRegex().Replace(html, " ");
        return WebUtility.HtmlDecode(withoutTags);
    }

    private static string Collapse(string text)
    {
        // Collapse 3+ newlines to a single blank line and trim trailing spaces on each line.
        var normalized = SpacesRegex().Replace(text, " ");
        normalized = BlankLinesRegex().Replace(normalized, "\n\n");
        return normalized.Trim();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex("[ \\t]+")]
    private static partial Regex SpacesRegex();

    [GeneratedRegex("\\n{3,}")]
    private static partial Regex BlankLinesRegex();
}
