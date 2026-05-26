namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Persistence normalization helpers for image blocks and drawing runs.</summary>
public static class DocumentImagePersistence
{
    /// <summary>Converts legacy top-level image blocks into paragraph drawing runs for provider persistence.</summary>
    public static void ConvertImageBlocksToDrawingRuns(DocumentEditorDocument? document)
    {
        if (document is null)
        {
            return;
        }

        ConvertImageBlocksToDrawingRuns(document.Blocks);
        foreach (var headerFooter in document.HeadersFooters)
        {
            ConvertImageBlocksToDrawingRuns(headerFooter.Blocks);
        }

        foreach (var note in document.Notes)
        {
            ConvertImageBlocksToDrawingRuns(note.Blocks);
        }
    }

    /// <summary>Normalizes image references that are about to cross a provider or export boundary.</summary>
    public static void Sanitize(DocumentEditorDocument? document)
    {
        if (document is null)
        {
            return;
        }

        foreach (var block in EnumerateBlocks(document.Blocks))
        {
            Sanitize(block);
        }

        foreach (var headerFooter in document.HeadersFooters)
        {
            foreach (var block in EnumerateBlocks(headerFooter.Blocks))
            {
                Sanitize(block);
            }
        }

        foreach (var note in document.Notes)
        {
            foreach (var block in EnumerateBlocks(note.Blocks))
            {
                Sanitize(block);
            }
        }

        foreach (var comment in document.Comments)
        {
            foreach (var entry in comment.Entries)
            {
                entry.Inlines ??= [];
                foreach (var drawing in entry.Inlines.OfType<DocumentDrawingRun>())
                {
                    Sanitize(drawing);
                }
            }
        }
    }

    /// <summary>Normalizes image references in a single block and its table descendants.</summary>
    public static void Sanitize(DocumentBlock? block)
    {
        if (block is null)
        {
            return;
        }

        block.Content ??= new ParagraphBlockContent();
        if (block.Content is ImageBlockContent image)
        {
            Sanitize(image);
        }

        foreach (var drawing in EnumerateDrawingRuns(block.Content))
        {
            Sanitize(drawing);
        }

        if (block.Content is TableBlockContent table)
        {
            table.Rows ??= [];
            foreach (var cell in table.Rows.SelectMany(row => row.Cells ?? []))
            {
                cell.Blocks ??= [];
                foreach (var nested in cell.Blocks)
                {
                    Sanitize(nested);
                }
            }
        }
    }

    /// <summary>Normalizes a legacy image block payload for storage.</summary>
    public static void Sanitize(ImageBlockContent image)
    {
        image.Size ??= new DocumentImageSize();
        image.NaturalSize ??= new DocumentImageSize();
        image.Layout ??= DocumentObjectLayout.Inline();
        NormalizeLayout(image.Layout);
        image.Url = ToPersistentImageUrl(image.Source, image.AssetId, image.Url);
    }

    /// <summary>Normalizes a drawing run payload for storage.</summary>
    public static void Sanitize(DocumentDrawingRun drawing)
    {
        if (string.IsNullOrWhiteSpace(drawing.Id))
        {
            drawing.Id = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(drawing.ObjectId))
        {
            drawing.ObjectId = Guid.NewGuid().ToString("N");
        }

        drawing.Marks ??= [];
        drawing.Size ??= new DocumentImageSize();
        drawing.NaturalSize ??= new DocumentImageSize();
        drawing.Layout ??= DocumentObjectLayout.Inline();
        drawing.Metadata ??= [];
        if (drawing.Docx is not null)
        {
            NormalizeDocxMetadata(drawing.Docx);
        }

        NormalizeLayout(drawing.Layout);
        drawing.Url = ToPersistentImageUrl(drawing.Source, drawing.AssetId, drawing.Url);
    }

    /// <summary>Enumerates drawing runs in a full document, including table cells, headers, footers, and notes.</summary>
    public static IEnumerable<DocumentDrawingRun> EnumerateDrawingRuns(DocumentEditorDocument? document)
    {
        if (document is null)
        {
            yield break;
        }

        foreach (var block in EnumerateBlocks(document.Blocks))
        {
            foreach (var drawing in EnumerateDrawingRuns(block.Content))
            {
                yield return drawing;
            }
        }

        foreach (var headerFooter in document.HeadersFooters)
        {
            foreach (var block in EnumerateBlocks(headerFooter.Blocks))
            {
                foreach (var drawing in EnumerateDrawingRuns(block.Content))
                {
                    yield return drawing;
                }
            }
        }

        foreach (var note in document.Notes)
        {
            foreach (var block in EnumerateBlocks(note.Blocks))
            {
                foreach (var drawing in EnumerateDrawingRuns(block.Content))
                {
                    yield return drawing;
                }
            }
        }

        foreach (var comment in document.Comments)
        {
            foreach (var entry in comment.Entries)
            {
                foreach (var drawing in (entry.Inlines ?? []).OfType<DocumentDrawingRun>())
                {
                    yield return drawing;
                }
            }
        }
    }

    /// <summary>Determines whether an image URL is safe to persist in the document JSON.</summary>
    public static bool IsSafePersistentImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (url.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        if (url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
            && url.Contains(";base64,", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private static string? ToPersistentImageUrl(DocumentImageSource source, string? assetId, string? url)
    {
        if (source == DocumentImageSource.Asset && !string.IsNullOrWhiteSpace(assetId))
        {
            return null;
        }

        return IsSafePersistentImageUrl(url) ? url : null;
    }

    private static void NormalizeLayout(DocumentObjectLayout layout)
    {
        layout.Anchor ??= new DocumentObjectAnchor();
        layout.Position ??= new DocumentObjectPosition();
        layout.Wrap ??= new DocumentObjectWrap();
        layout.Transform ??= new DocumentObjectTransform();
        layout.Transform.Crop ??= new DocumentObjectCrop();
        layout.Stacking ??= new DocumentObjectStacking();
    }

    private static void NormalizeDocxMetadata(DocumentDocxDrawingMetadata metadata)
    {
        metadata.Media ??= new DocumentImageMediaInfo();
        metadata.EffectExtent ??= new DocumentObjectEffectExtent();
    }

    private static void ConvertImageBlocksToDrawingRuns(List<DocumentBlock>? blocks)
    {
        if (blocks is null)
        {
            return;
        }

        foreach (var block in blocks)
        {
            if (block.Content is ImageBlockContent image)
            {
                block.Type = DocumentBlockType.Paragraph;
                block.Content = new ParagraphBlockContent
                {
                    Inlines = [CreateDrawingRun(block, image)]
                };
            }

            if (block.Content is not TableBlockContent table)
            {
                continue;
            }

            foreach (var cell in table.Rows.SelectMany(row => row.Cells ?? []))
            {
                ConvertImageBlocksToDrawingRuns(cell.Blocks);
            }
        }
    }

    private static DocumentDrawingRun CreateDrawingRun(DocumentBlock block, ImageBlockContent image)
    {
        var layout = image.Layout ?? DocumentObjectLayout.Inline();
        NormalizeLayout(layout);
        if (string.IsNullOrWhiteSpace(layout.Anchor.BlockId))
        {
            layout.Anchor.BlockId = block.Id;
        }

        return new DocumentDrawingRun
        {
            Id = $"{block.Id}-drawing",
            ObjectId = block.Id,
            Source = image.Source,
            Url = image.Url,
            AssetId = image.AssetId,
            AltText = image.AltText,
            IsDecorative = image.IsDecorative,
            Caption = image.Caption,
            Size = image.Size ?? new DocumentImageSize(),
            NaturalSize = image.NaturalSize ?? new DocumentImageSize(),
            Layout = layout,
            LinkUrl = image.LinkUrl
        };
    }

    private static IEnumerable<DocumentDrawingRun> EnumerateDrawingRuns(DocumentBlockContent? content)
    {
        var inlines = content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => null
        };

        if (inlines is null)
        {
            yield break;
        }

        foreach (var inline in inlines.OfType<DocumentDrawingRun>())
        {
            yield return inline;
        }
    }

    private static IEnumerable<DocumentBlock> EnumerateBlocks(IEnumerable<DocumentBlock>? blocks)
    {
        if (blocks is null)
        {
            yield break;
        }

        foreach (var block in blocks)
        {
            yield return block;
            if (block.Content is not TableBlockContent table)
            {
                continue;
            }

            foreach (var nested in table.Rows
                         .SelectMany(row => row.Cells ?? [])
                         .SelectMany(cell => EnumerateBlocks(cell.Blocks)))
            {
                yield return nested;
            }
        }
    }
}
