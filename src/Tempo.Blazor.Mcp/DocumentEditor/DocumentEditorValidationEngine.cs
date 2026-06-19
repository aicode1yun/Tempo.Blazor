using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>The outcome of validating a document editor snapshot.</summary>
public sealed record DocumentEditorValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>Validates document editor snapshots before MCP write tools persist them.</summary>
public static class DocumentEditorValidationEngine
{
    public static DocumentEditorValidationResult Validate(DocumentEditorDocument document)
    {
        var errors = new List<string>();

        if (document.SchemaVersion <= 0 || document.SchemaVersion > DocumentEditorDocument.CurrentSchemaVersion)
        {
            errors.Add($"schemaVersion: unsupported schema version {document.SchemaVersion}.");
        }
        if (string.IsNullOrWhiteSpace(document.DocumentId))
        {
            errors.Add("documentId: document id is required.");
        }

        ValidatePageSettings(document.PageSettings, errors);

        var blockIds = new HashSet<string>(StringComparer.Ordinal);
        ValidateBlocks(document.Blocks, "blocks", blockIds, errors);
        ValidateComments(document, blockIds, errors);
        ValidateRevisions(document, blockIds, errors);

        return new DocumentEditorValidationResult(errors.Count == 0, errors);
    }

    private static void ValidatePageSettings(DocumentPageSettings? settings, List<string> errors)
    {
        if (settings is null)
        {
            errors.Add("pageSettings: page settings are required.");
            return;
        }

        if (settings.Size is null)
        {
            errors.Add("pageSettings.size: page size is required.");
        }
        else
        {
            if (settings.Size.Width <= 0)
            {
                errors.Add($"pageSettings.size.width: width must be greater than 0 (was {settings.Size.Width}).");
            }
            if (settings.Size.Height <= 0)
            {
                errors.Add($"pageSettings.size.height: height must be greater than 0 (was {settings.Size.Height}).");
            }
        }

        if (settings.Margins is null)
        {
            errors.Add("pageSettings.margins: margins are required.");
        }
        else
        {
            ValidateNonNegative(settings.Margins.Top, "pageSettings.margins.top", errors);
            ValidateNonNegative(settings.Margins.Right, "pageSettings.margins.right", errors);
            ValidateNonNegative(settings.Margins.Bottom, "pageSettings.margins.bottom", errors);
            ValidateNonNegative(settings.Margins.Left, "pageSettings.margins.left", errors);
        }
    }

    private static void ValidateBlocks(
        IEnumerable<DocumentBlock> blocks,
        string path,
        HashSet<string> blockIds,
        List<string> errors)
    {
        var index = 0;
        foreach (var block in blocks)
        {
            var blockPath = $"{path}[{index}]";
            if (string.IsNullOrWhiteSpace(block.Id))
            {
                errors.Add($"{blockPath}.id: block id is required.");
            }
            else if (!blockIds.Add(block.Id))
            {
                errors.Add($"{blockPath}.id: duplicate block id '{block.Id}'.");
            }

            ValidateBlockContent(block, blockPath, blockIds, errors);
            index++;
        }
    }

    private static void ValidateBlockContent(
        DocumentBlock block,
        string path,
        HashSet<string> blockIds,
        List<string> errors)
    {
        if (!IsCompatible(block.Type, block.Content))
        {
            errors.Add($"{path}.content: content type '{block.Content.GetType().Name}' is not compatible with block type '{block.Type}'.");
        }

        switch (block.Content)
        {
            case TableBlockContent table:
                ValidateTable(table, path, blockIds, errors);
                break;
            case ImageBlockContent image:
                ValidateImage(image, path, errors);
                break;
            case ContentControlBlockContent control:
                ValidateBlocks(control.Blocks, $"{path}.content.blocks", blockIds, errors);
                break;
        }
    }

    private static bool IsCompatible(DocumentBlockType type, DocumentBlockContent content)
        => type switch
        {
            DocumentBlockType.Paragraph => content is ParagraphBlockContent,
            DocumentBlockType.Heading => content is HeadingBlockContent,
            DocumentBlockType.List => content is ListBlockContent,
            DocumentBlockType.Quote => content is QuoteBlockContent,
            DocumentBlockType.Table => content is TableBlockContent,
            DocumentBlockType.Image => content is ImageBlockContent,
            DocumentBlockType.PageBreak => content is PageBreakBlockContent,
            DocumentBlockType.ContentControl => content is ContentControlBlockContent,
            _ => false
        };

    private static void ValidateTable(
        TableBlockContent table,
        string path,
        HashSet<string> blockIds,
        List<string> errors)
    {
        for (var ri = 0; ri < table.Rows.Count; ri++)
        {
            var row = table.Rows[ri];
            if (row.Cells.Count == 0)
            {
                errors.Add($"{path}.content.rows[{ri}].cells: row must contain at least one cell.");
            }

            for (var ci = 0; ci < row.Cells.Count; ci++)
            {
                var cell = row.Cells[ci];
                var cellPath = $"{path}.content.rows[{ri}].cells[{ci}]";
                if (cell.ColumnSpan <= 0)
                {
                    errors.Add($"{cellPath}.columnSpan: column span must be greater than 0.");
                }
                if (cell.RowSpan <= 0)
                {
                    errors.Add($"{cellPath}.rowSpan: row span must be greater than 0.");
                }
                ValidateBlocks(cell.Blocks, $"{cellPath}.blocks", blockIds, errors);
            }
        }
    }

    private static void ValidateImage(ImageBlockContent image, string path, List<string> errors)
    {
        if (image.Source == DocumentImageSource.Url && string.IsNullOrWhiteSpace(image.Url))
        {
            errors.Add($"{path}.content.url: URL image blocks require a url.");
        }
        if (image.Source is DocumentImageSource.Asset or DocumentImageSource.Clipboard
            && string.IsNullOrWhiteSpace(image.AssetId))
        {
            errors.Add($"{path}.content.assetId: asset-backed image blocks require an asset id.");
        }
        if (image.Size.Width is <= 0)
        {
            errors.Add($"{path}.content.size.width: width must be greater than 0 when specified.");
        }
        if (image.Size.Height is <= 0)
        {
            errors.Add($"{path}.content.size.height: height must be greater than 0 when specified.");
        }
    }

    private static void ValidateComments(
        DocumentEditorDocument document,
        HashSet<string> blockIds,
        List<string> errors)
    {
        for (var i = 0; i < document.Comments.Count; i++)
        {
            var comment = document.Comments[i];
            if (!string.IsNullOrWhiteSpace(comment.Anchor?.BlockId)
                && !blockIds.Contains(comment.Anchor.BlockId))
            {
                errors.Add($"comments[{i}].anchor.blockId: references missing block '{comment.Anchor.BlockId}'.");
            }
        }
    }

    private static void ValidateRevisions(
        DocumentEditorDocument document,
        HashSet<string> blockIds,
        List<string> errors)
    {
        for (var i = 0; i < document.Revisions.Count; i++)
        {
            var revision = document.Revisions[i];
            if (!string.IsNullOrWhiteSpace(revision.Range?.BlockId)
                && !blockIds.Contains(revision.Range.BlockId))
            {
                errors.Add($"revisions[{i}].range.blockId: references missing block '{revision.Range.BlockId}'.");
            }
        }
    }

    private static void ValidateNonNegative(double value, string path, List<string> errors)
    {
        if (value < 0)
        {
            errors.Add($"{path}: value must not be negative (was {value}).");
        }
    }
}
