using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>
/// Extracts every template variable referenced anywhere in a document — subject, preheader, block
/// content and URLs, visibility conditions and raw block content — as a merged, de-duplicated set.
/// </summary>
public static class EmailDocumentVariableExtractor
{
    /// <summary>Returns the variables referenced across the whole document.</summary>
    public static IReadOnlyList<TemplateVariableInfo> Extract(EmailTemplateDocument document)
    {
        var merged = new Dictionary<string, VariableKind>(StringComparer.Ordinal);

        foreach (var text in EnumerateTemplateStrings(document))
            foreach (var info in TemplateVariableExtractor.ExtractInfos(text))
            {
                // Collection wins over scalar when the same path appears both ways.
                if (info.Kind == VariableKind.Collection || !merged.ContainsKey(info.Path))
                    merged[info.Path] = info.Kind;
            }

        return merged.Select(kv => new TemplateVariableInfo(kv.Key, kv.Value)).ToList();
    }

    private static IEnumerable<string> EnumerateTemplateStrings(EmailTemplateDocument document)
    {
        yield return document.Subject;
        if (document.Preheader is not null) yield return document.Preheader;

        foreach (var block in DocumentTree.AllBlocks(document))
        {
            // VisibleWhen is a bare Scriban expression; wrap it so its variables are extracted.
            if (!string.IsNullOrWhiteSpace(block.VisibleWhen)) yield return "{{ " + block.VisibleWhen + " }}";

            switch (block)
            {
                case EmailTextBlock t: yield return t.Content; break;
                case EmailButtonBlock b:
                    yield return b.Text;
                    if (b.Href is not null) yield return b.Href;
                    break;
                case EmailImageBlock i:
                    yield return i.Alt;
                    if (i.Href is not null) yield return i.Href;
                    if (i.Src is not null) yield return i.Src;
                    break;
                case EmailRawBlock r: yield return r.Content; break;
                case EmailTableBlock table:
                    foreach (var row in table.Rows)
                        foreach (var cell in row.Cells) yield return cell.Text;
                    break;
                case EmailSocialBlock social:
                    foreach (var e in social.Elements)
                    {
                        if (e.Label is not null) yield return e.Label;
                        if (e.Href is not null) yield return e.Href;
                    }
                    break;
                case EmailNavbarBlock navbar:
                    foreach (var link in navbar.Links)
                    {
                        yield return link.Text;
                        if (link.Href is not null) yield return link.Href;
                    }
                    break;
                case EmailCarouselBlock carousel:
                    foreach (var img in carousel.Images)
                    {
                        yield return img.Alt;
                        if (img.Href is not null) yield return img.Href;
                    }
                    break;
                case EmailAccordionBlock accordion:
                    foreach (var item in accordion.Items)
                    {
                        yield return item.Title;
                        yield return item.Content;
                    }
                    break;
            }
        }
    }
}
