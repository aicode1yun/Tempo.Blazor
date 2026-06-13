using System.Globalization;
using System.Net;
using System.Text;
using Tempo.Blazor.DocumentFormats.Internal;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Html;

/// <summary>Options used when exporting an editor document to HTML.</summary>
public sealed class DocumentHtmlExportOptions
{
    /// <summary>Whether to wrap the output in a complete HTML document.</summary>
    public bool IncludeDocumentWrapper { get; set; }

    /// <summary>Optional CSS class applied to the root document element.</summary>
    public string? RootCssClass { get; set; } = "tm-document-export";

    /// <summary>Optional callback used to resolve asset-backed image URLs.</summary>
    public Func<ImageBlockContent, string?>? ImageUrlResolver { get; set; }
}

/// <summary>Exports a <see cref="DocumentEditorDocument"/> to safe, semantic HTML.</summary>
public sealed class DocumentHtmlExporter
{
    /// <summary>Exports a document to HTML.</summary>
    public string Export(DocumentEditorDocument document, DocumentHtmlExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= new DocumentHtmlExportOptions();
        var body = new StringBuilder();
        var rootClass = string.IsNullOrWhiteSpace(options.RootCssClass)
            ? "tm-document-export"
            : options.RootCssClass.Trim();

        body.Append("<main class=\"")
            .Append(HtmlAttr(rootClass))
            .Append("\" data-document-id=\"")
            .Append(HtmlAttr(document.DocumentId))
            .Append("\">");
        AppendBlocks(body, document.Blocks.OrderBy(block => block.Order), options);
        body.Append("</main>");

        if (!options.IncludeDocumentWrapper)
        {
            return body.ToString();
        }

        var title = string.IsNullOrWhiteSpace(document.Metadata.Title)
            ? "Document"
            : document.Metadata.Title;

        return "<!doctype html><html><head><meta charset=\"utf-8\"><title>"
            + Html(title)
            + "</title></head><body>"
            + body
            + "</body></html>";
    }

    private static void AppendBlocks(StringBuilder html, IEnumerable<DocumentBlock> blocks, DocumentHtmlExportOptions options)
    {
        var orderedBlocks = blocks.ToList();
        for (var index = 0; index < orderedBlocks.Count; index++)
        {
            var block = orderedBlocks[index];
            if (block.Content is ListBlockContent list)
            {
                AppendList(html, orderedBlocks, ref index, list, options);
                continue;
            }

            AppendBlock(html, block, options);
        }
    }

    private static void AppendBlock(StringBuilder html, DocumentBlock block, DocumentHtmlExportOptions options)
    {
        switch (block.Content)
        {
            case ParagraphBlockContent paragraph:
                html.Append("<p>");
                AppendInlines(html, paragraph.Inlines, options);
                html.Append("</p>");
                break;
            case HeadingBlockContent heading:
                var level = Math.Clamp(heading.Level, 1, 6);
                html.Append("<h").Append(level).Append('>');
                AppendInlines(html, heading.Inlines, options);
                html.Append("</h").Append(level).Append('>');
                break;
            case QuoteBlockContent quote:
                html.Append("<blockquote>");
                AppendInlines(html, quote.Inlines, options);
                html.Append("</blockquote>");
                break;
            case TableBlockContent table:
                AppendTable(html, table, options);
                break;
            case ImageBlockContent image:
                AppendImage(html, image, options);
                break;
            case PageBreakBlockContent:
                html.Append("<hr class=\"tm-document-page-break\">");
                break;
            default:
                html.Append("<p>").Append(Html(DocumentModelText.GetBlockText(block))).Append("</p>");
                break;
        }
    }

    private static void AppendList(StringBuilder html, IReadOnlyList<DocumentBlock> blocks, ref int index, ListBlockContent firstItem, DocumentHtmlExportOptions options)
    {
        var ordered = firstItem.Ordered;
        html.Append(ordered ? "<ol>" : "<ul>");
        AppendListItem(html, firstItem, options);

        while (index + 1 < blocks.Count
            && blocks[index + 1].Content is ListBlockContent item
            && item.Ordered == ordered)
        {
            index++;
            AppendListItem(html, item, options);
        }

        html.Append(ordered ? "</ol>" : "</ul>");
    }

    private static void AppendListItem(StringBuilder html, ListBlockContent item, DocumentHtmlExportOptions options)
    {
        html.Append("<li>");
        AppendInlines(html, item.Inlines, options);
        html.Append("</li>");
    }

    private static void AppendTable(StringBuilder html, TableBlockContent table, DocumentHtmlExportOptions options)
    {
        html.Append("<table><tbody>");
        foreach (var row in table.Rows)
        {
            html.Append("<tr>");
            foreach (var cell in row.Cells)
            {
                html.Append("<td");
                if (cell.ColumnSpan > 1)
                {
                    html.Append(" colspan=\"").Append(cell.ColumnSpan.ToString(CultureInfo.InvariantCulture)).Append('"');
                }

                if (cell.RowSpan > 1)
                {
                    html.Append(" rowspan=\"").Append(cell.RowSpan.ToString(CultureInfo.InvariantCulture)).Append('"');
                }

                html.Append('>');
                AppendBlocks(html, cell.Blocks.OrderBy(block => block.Order), options);
                html.Append("</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</tbody></table>");
    }

    private static void AppendImage(StringBuilder html, ImageBlockContent image, DocumentHtmlExportOptions options)
    {
        var source = ResolveImageUrl(image, options);
        html.Append("<figure>");
        if (!string.IsNullOrWhiteSpace(source) && IsSafeUri(source, allowImageDataUri: true))
        {
            html.Append("<img src=\"")
                .Append(HtmlAttr(source))
                .Append("\" alt=\"")
                .Append(HtmlAttr(image.AltText ?? string.Empty))
                .Append('"');

            if (image.Size.Width is > 0)
            {
                html.Append(" width=\"").Append(Math.Round(image.Size.Width.Value).ToString(CultureInfo.InvariantCulture)).Append('"');
            }

            if (image.Size.Height is > 0)
            {
                html.Append(" height=\"").Append(Math.Round(image.Size.Height.Value).ToString(CultureInfo.InvariantCulture)).Append('"');
            }

            html.Append('>');
        }

        if (!string.IsNullOrWhiteSpace(image.Caption))
        {
            html.Append("<figcaption>").Append(Html(image.Caption)).Append("</figcaption>");
        }

        html.Append("</figure>");
    }

    private static string? ResolveImageUrl(ImageBlockContent image, DocumentHtmlExportOptions options)
    {
        if (image.Source == DocumentImageSource.Url)
        {
            return image.Url;
        }

        return options.ImageUrlResolver?.Invoke(image);
    }

    private static void AppendInlines(StringBuilder html, IEnumerable<InlineContent> inlines, DocumentHtmlExportOptions options)
    {
        foreach (var inline in inlines)
        {
            var content = inline switch
            {
                TextRun text => Html(text.Text),
                TokenRun token => RenderToken(token),
                DocumentNoteReferenceRun note => "<sup data-note-id=\"" + HtmlAttr(note.NoteId) + "\">" + Html(note.DisplayMarker ?? note.NoteId) + "</sup>",
                DocumentDrawingRun drawing => RenderDrawing(drawing, options),
                DocumentSigningFieldRun signing => "<span data-signing-field=\"" + HtmlAttr(signing.Uuid) + "\">" + Html(Internal.SigningFieldPlaceholder.Text(signing)) + "</span>",
                _ => string.Empty
            };

            html.Append(ApplyMarks(content, inline.Marks));
        }
    }

    private static string RenderDrawing(DocumentDrawingRun drawing, DocumentHtmlExportOptions options)
    {
        var html = new StringBuilder();
        AppendImage(html, ToImageBlockContent(drawing), options);
        return html.ToString();
    }

    private static ImageBlockContent ToImageBlockContent(DocumentDrawingRun drawing)
        => new()
        {
            Source = drawing.Source,
            Url = drawing.Url,
            AssetId = drawing.AssetId,
            AltText = drawing.AltText,
            IsDecorative = drawing.IsDecorative,
            Caption = drawing.Caption,
            Size = drawing.Size ?? new DocumentImageSize(),
            NaturalSize = drawing.NaturalSize ?? new DocumentImageSize(),
            Layout = drawing.Layout ?? DocumentObjectLayout.Inline(),
            LinkUrl = drawing.LinkUrl
        };

    private static string RenderToken(TokenRun token)
    {
        var label = string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName;
        return "<span class=\"tm-document-token\" data-token-key=\""
            + HtmlAttr(token.Key)
            + "\">"
            + Html(label)
            + "</span>";
    }

    private static string ApplyMarks(string content, IReadOnlyList<InlineMark> marks)
    {
        foreach (var mark in marks)
        {
            content = mark.Type switch
            {
                InlineMarkType.Bold => "<strong>" + content + "</strong>",
                InlineMarkType.Italic => "<em>" + content + "</em>",
                InlineMarkType.Underline => "<u>" + content + "</u>",
                InlineMarkType.Strikethrough => "<s>" + content + "</s>",
                InlineMarkType.Superscript => "<sup>" + content + "</sup>",
                InlineMarkType.Subscript => "<sub>" + content + "</sub>",
                InlineMarkType.Link when mark.Link is not null && IsSafeUri(mark.Link.Href) => "<a href=\"" + HtmlAttr(mark.Link.Href) + "\">" + content + "</a>",
                InlineMarkType.CommentAnchor when mark.CommentAnchor is not null => "<span data-comment-id=\"" + HtmlAttr(mark.CommentAnchor.CommentId) + "\">" + content + "</span>",
                InlineMarkType.Revision when !string.IsNullOrWhiteSpace(mark.RevisionId) => "<span data-revision-id=\"" + HtmlAttr(mark.RevisionId) + "\">" + content + "</span>",
                InlineMarkType.Highlight when IsSafeCssColor(mark.Value) => "<mark style=\"background-color:" + HtmlAttr(mark.Value!) + "\">" + content + "</mark>",
                InlineMarkType.TextColor when IsSafeCssColor(mark.Value) => "<span style=\"color:" + HtmlAttr(mark.Value!) + "\">" + content + "</span>",
                _ => content
            };
        }

        return content;
    }

    private static bool IsSafeUri(string? value, bool allowImageDataUri = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (allowImageDataUri && value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "mailto";
    }

    private static bool IsSafeCssColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not '#' and not '(' and not ')' and not ',' and not '.' and not '%' and not ' ' and not '-')
            {
                return false;
            }
        }

        return !value.Contains("url", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("expression", StringComparison.OrdinalIgnoreCase);
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string HtmlAttr(string value) => WebUtility.HtmlEncode(value);
}
