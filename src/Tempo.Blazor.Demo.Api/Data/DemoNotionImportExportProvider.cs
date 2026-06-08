using System.Text;
using Tempo.Blazor.Demo.Api.Services;
using Dm = Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Html;
using Tempo.Blazor.DocumentFormats.Markdown;
using Tempo.Blazor.DocumentFormats.Notion;
using Tempo.Blazor.DocumentFormats.Odt;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

/// <summary>Demo implementation of Notion page import/export backed by real page stores and DocumentFormats exporters.</summary>
public sealed class DemoNotionImportExportProvider : INotionImportExportProvider
{
    private readonly MockNotionDataStore _pages;
    private readonly MockNotionBlockStore _blocks;
    private readonly DemoDocumentPdfExportProvider _pdfExporter;

    public DemoNotionImportExportProvider(
        MockNotionDataStore pages,
        MockNotionBlockStore blocks,
        DemoDocumentPdfExportProvider pdfExporter)
    {
        _pages = pages;
        _blocks = blocks;
        _pdfExporter = pdfExporter;
    }

    public async Task<Stream> ExportPageAsync(string pageId, NotionExportFormat format)
    {
        var artifact = await ExportPageArtifactAsync(pageId, format, includeSubpages: false);
        return new MemoryStream(artifact.Content, writable: false);
    }

    public async Task<Stream> ExportPageWithSubpagesAsync(string pageId, NotionExportFormat format)
    {
        var artifact = await ExportPageArtifactAsync(pageId, format, includeSubpages: true);
        return new MemoryStream(artifact.Content, writable: false);
    }

    public async Task<INotionPage> ImportAsync(Stream content, NotionImportFormat format, string? targetParentPageId)
    {
        return await ImportPageArtifactAsync(content, format, targetParentPageId, fileName: null);
    }

    public async Task<INotionPage> ImportPageArtifactAsync(
        Stream content,
        NotionImportFormat format,
        string? targetParentPageId,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        var document = await ImportDocumentAsync(content, format, fileName, cancellationToken);
        var title = ExtractImportTitle(document, fileName);
        var page = await _pages.CreatePageAsync(targetParentPageId, title);
        var pageId = page.Id.ToString("D");

        try
        {
            var conversion = DocumentModelToNotionConverter.ConvertDocument(document, page.Id);
            if (conversion.Blocks.Count > 0)
            {
                await _blocks.CreateImportedBlocksAsync(pageId, conversion.Blocks, cancellationToken);
            }

            return page;
        }
        catch
        {
            await _pages.PermanentlyDeletePageAsync(pageId);
            throw;
        }
    }

    public async Task<NotionExportArtifact> ExportPageArtifactAsync(
        string pageId,
        NotionExportFormat format,
        bool includeSubpages,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var document = includeSubpages
            ? await BuildSubtreeDocumentAsync(pageId, cancellationToken)
            : await BuildSinglePageDocumentAsync(pageId, cancellationToken);

        return await ExportDocumentAsync(document, format, cancellationToken);
    }

    private async Task<Dm.DocumentEditorDocument> BuildSinglePageDocumentAsync(
        string pageId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var page = await _pages.GetPageAsync(pageId);
        var blocks = GetPageBlocks(page.Id);
        var document = NotionToDocumentModelConverter.ConvertPage(page, blocks).Document;
        if (!HasMeaningfulExportContent(document))
        {
            document.Blocks.Clear();
            document.Blocks.Add(new Dm.DocumentBlock
            {
                Id = $"empty-page-title-{page.Id:N}",
                Type = Dm.DocumentBlockType.Heading,
                Order = 0,
                Content = new Dm.HeadingBlockContent
                {
                    Level = 1,
                    Inlines = [new Dm.TextRun { Text = string.IsNullOrWhiteSpace(page.Title) ? page.Id.ToString("D") : page.Title }]
                }
            });
        }

        return document;
    }

    private static bool HasMeaningfulExportContent(Dm.DocumentEditorDocument document)
        => document.Blocks.Any(HasMeaningfulExportContent);

    private static bool HasMeaningfulExportContent(Dm.DocumentBlock block)
    {
        return block.Content switch
        {
            Dm.ParagraphBlockContent paragraph => !string.IsNullOrWhiteSpace(DmText(paragraph.Inlines)),
            Dm.HeadingBlockContent heading => !string.IsNullOrWhiteSpace(DmText(heading.Inlines)),
            Dm.ListBlockContent list => !string.IsNullOrWhiteSpace(DmText(list.Inlines)),
            Dm.QuoteBlockContent quote => !string.IsNullOrWhiteSpace(DmText(quote.Inlines)),
            Dm.TableBlockContent table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks).Any(HasMeaningfulExportContent),
            Dm.ImageBlockContent image => !string.IsNullOrWhiteSpace(FirstNonEmpty(image.Caption, image.AltText, image.Url, image.AssetId)),
            Dm.PageBreakBlockContent => false,
            _ => true
        };
    }

    private async Task<Dm.DocumentEditorDocument> BuildSubtreeDocumentAsync(
        string pageId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = await _pages.GetPageAsync(pageId);
        var allPages = _pages.GetAllPages().ToDictionary(page => page.Id);
        var orderedPages = EnumeratePageSubtree(root, allPages).ToList();
        var document = Dm.DocumentEditorDocument.Empty(root.Id.ToString("N"));
        document.Metadata.Title = string.IsNullOrWhiteSpace(root.Title) ? root.Id.ToString("D") : root.Title;
        document.Metadata.Description = root.Description;
        document.Metadata.CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(root.CreatedAt, DateTimeKind.Utc));
        document.Metadata.ModifiedAt = new DateTimeOffset(DateTime.SpecifyKind(root.LastEditedAt, DateTimeKind.Utc));
        document.Metadata.Tags = root.Labels.ToList();

        foreach (var page in orderedPages)
        {
            document.Blocks.Add(new Dm.DocumentBlock
            {
                Id = $"page-title-{page.Id:N}",
                Type = Dm.DocumentBlockType.Heading,
                Order = document.Blocks.Count,
                Content = new Dm.HeadingBlockContent
                {
                    Level = page.Id == root.Id ? 1 : 2,
                    Inlines = [new Dm.TextRun { Text = string.IsNullOrWhiteSpace(page.Title) ? page.Id.ToString("D") : page.Title }]
                }
            });

            var converted = NotionToDocumentModelConverter.ConvertPage(page, GetPageBlocks(page.Id));
            foreach (var block in converted.Document.Blocks)
            {
                block.Order = document.Blocks.Count;
                document.Blocks.Add(block);
            }
        }

        return document;
    }

    private IReadOnlyList<IPageBlock> GetPageBlocks(Guid pageId)
    {
        return _blocks.GetAllBlocksSnapshot()
            .Where(block => block.PageId == pageId)
            .OrderBy(block => block.ParentBlockId.HasValue)
            .ThenBy(block => block.Order)
            .Cast<IPageBlock>()
            .ToList();
    }

    private static IEnumerable<INotionPage> EnumeratePageSubtree(
        INotionPage root,
        IReadOnlyDictionary<Guid, INotionPage> allPages)
    {
        yield return root;

        foreach (var child in allPages.Values
                     .Where(page => page.ParentId == root.Id)
                     .OrderBy(page => page.Title, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var descendant in EnumeratePageSubtree(child, allPages))
            {
                yield return descendant;
            }
        }
    }

    private async Task<NotionExportArtifact> ExportDocumentAsync(
        Dm.DocumentEditorDocument document,
        NotionExportFormat format,
        CancellationToken cancellationToken)
    {
        var fileName = SanitizeFileName(document.Metadata.Title);
        switch (format)
        {
            case NotionExportFormat.Markdown:
            {
                var markdown = new DocumentMarkdownExporter().Export(document);
                return new NotionExportArtifact(
                    Encoding.UTF8.GetBytes(markdown),
                    "text/markdown; charset=utf-8",
                    $"{fileName}.md");
            }
            case NotionExportFormat.Html:
            {
                var html = new DocumentHtmlExporter().Export(document, new DocumentHtmlExportOptions
                {
                    IncludeDocumentWrapper = true,
                    RootCssClass = "tm-notion-export"
                });
                return new NotionExportArtifact(
                    Encoding.UTF8.GetBytes(html),
                    "text/html; charset=utf-8",
                    $"{fileName}.html");
            }
            case NotionExportFormat.Pdf:
            {
                var exported = await _pdfExporter.ExportPdfAsync(new Dm.DocumentPdfExportRequest
                {
                    DocumentId = document.DocumentId,
                    Document = document,
                    FileName = fileName
                }, cancellationToken);
                return new NotionExportArtifact(exported.Content, exported.ContentType, exported.FileName);
            }
            case NotionExportFormat.Docx:
            {
                var exported = await new DocumentDocxExporter().ExportAsync(document, new DocumentFormatExportOptions
                {
                    FileName = fileName,
                    AllowImagePlaceholders = true,
                    AllowExternalImageDownload = false
                }, cancellationToken);
                return new NotionExportArtifact(exported.Content, exported.ContentType, exported.FileName);
            }
            case NotionExportFormat.Odt:
            {
                var exported = await new DocumentOdtExporter().ExportAsync(document, new DocumentFormatExportOptions
                {
                    FileName = fileName,
                    AllowImagePlaceholders = true,
                    AllowExternalImageDownload = false
                }, cancellationToken);
                return new NotionExportArtifact(exported.Content, exported.ContentType, exported.FileName);
            }
            default:
                throw new NotSupportedException($"Unsupported Notion export format '{format}'.");
        }
    }

    private static async Task<Dm.DocumentEditorDocument> ImportDocumentAsync(
        Stream content,
        NotionImportFormat format,
        string? fileName,
        CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        switch (format)
        {
            case NotionImportFormat.Word:
            {
                var imported = await new DocumentDocxImporter().ImportAsync(content, new DocumentFormatImportOptions
                {
                    FileName = fileName,
                    AllowExternalImageDownload = false
                }, cancellationToken);
                return imported.Document;
            }
            case NotionImportFormat.Html:
            {
                using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                var html = await reader.ReadToEndAsync(cancellationToken);
                return new DocumentHtmlImporter().Import(html, new DocumentHtmlImportOptions
                {
                    Title = null
                });
            }
            case NotionImportFormat.Markdown:
            {
                using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                var markdown = await reader.ReadToEndAsync(cancellationToken);
                return new DocumentMarkdownImporter().Import(markdown, new DocumentMarkdownImportOptions
                {
                    Title = null
                });
            }
            default:
                throw new NotSupportedException($"Unsupported Notion import format '{format}'.");
        }
    }

    private static string ExtractImportTitle(Dm.DocumentEditorDocument document, string? fileName)
    {
        var title = FirstNonEmpty(
            document.Metadata.Title,
            document.Blocks.Select(ExtractBlockTitleCandidate).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            FileTitle(fileName));

        if (string.IsNullOrWhiteSpace(title))
        {
            return "Imported page";
        }

        return title.Length <= 80 ? title : title[..80];
    }

    private static string ExtractBlockTitleCandidate(Dm.DocumentBlock block)
    {
        return block.Content switch
        {
            Dm.HeadingBlockContent heading => DmText(heading.Inlines),
            Dm.ParagraphBlockContent paragraph => DmText(paragraph.Inlines),
            Dm.ListBlockContent list => DmText(list.Inlines),
            Dm.QuoteBlockContent quote => DmText(quote.Inlines),
            Dm.TableBlockContent table => table.Rows.SelectMany(row => row.Cells)
                .SelectMany(cell => cell.Blocks)
                .Select(ExtractBlockTitleCandidate)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
            Dm.ImageBlockContent image => FirstNonEmpty(image.Caption, image.AltText, image.Url, image.AssetId),
            _ => string.Empty
        };
    }

    private static string DmText(IEnumerable<Dm.InlineContent> inlines)
        => string.Concat(inlines.Select(inline => inline switch
        {
            Dm.TextRun run => run.Text,
            Dm.TokenRun token => FirstNonEmpty(token.DisplayName, token.Key),
            Dm.DocumentFieldRun field => FirstNonEmpty(field.DisplayText, field.FallbackText, field.FieldType.ToString()),
            Dm.DocumentNoteReferenceRun note => FirstNonEmpty(note.DisplayMarker, note.NoteId),
            Dm.DocumentDrawingRun drawing => FirstNonEmpty(drawing.Caption, drawing.AltText, drawing.Url, drawing.AssetId),
            _ => string.Empty
        })).Trim();

    private static string? FileTitle(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var title = Path.GetFileNameWithoutExtension(fileName);
        return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string SanitizeFileName(string value)
    {
        var sanitized = value;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalid, '-');
        }

        sanitized = sanitized.Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "notion-page" : sanitized;
    }
}

public sealed record NotionExportArtifact(byte[] Content, string ContentType, string FileName);
