using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Renderer metrics used by the document layout engine.</summary>
public class DocumentLayoutRendererMetrics
{
    /// <summary>Default text measurement zoom. A value of 1 means unscaled document units.</summary>
    public double Zoom { get; set; } = 1;

    /// <summary>Fallback font family used when neither document theme nor inline marks specify one.</summary>
    public string DefaultFontFamily { get; set; } = "Aptos, Arial, sans-serif";

    /// <summary>Fallback font size in points.</summary>
    public double DefaultFontSize { get; set; } = 11;

    /// <summary>Fallback line-height multiplier.</summary>
    public double DefaultLineHeightMultiplier { get; set; } = 1.15;

    /// <summary>Fallback width for images without any explicit size.</summary>
    public double DefaultImageWidth { get; set; } = 220;

    /// <summary>Fallback height for images without any explicit size.</summary>
    public double DefaultImageHeight { get; set; } = 124;

    /// <summary>Minimum horizontal text interval that is still considered usable.</summary>
    public double MinimumLineIntervalWidth { get; set; } = 4;

    /// <summary>Whether anchored objects are clamped into the page body when they would otherwise overflow it.</summary>
    public bool ClampAnchoredObjectsToBody { get; set; } = true;
}

/// <summary>Text measurement input used by document layout.</summary>
public class DocumentTextMeasurementRequest
{
    /// <summary>Measured text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Font family CSS value.</summary>
    public string FontFamily { get; set; } = string.Empty;

    /// <summary>Font size in points.</summary>
    public double FontSize { get; set; } = 11;

    /// <summary>Font weight CSS value.</summary>
    public string FontWeight { get; set; } = "400";

    /// <summary>Font style CSS value.</summary>
    public string FontStyle { get; set; } = "normal";

    /// <summary>Letter spacing in points.</summary>
    public double LetterSpacing { get; set; }

    /// <summary>Measurement zoom.</summary>
    public double Zoom { get; set; } = 1;
}

/// <summary>Measured text dimensions.</summary>
public class DocumentTextMeasurement
{
    /// <summary>Measured width.</summary>
    public double Width { get; set; }

    /// <summary>Measured height.</summary>
    public double Height { get; set; }
}

/// <summary>Text measurement cache statistics.</summary>
public class DocumentTextMeasurementCacheStats
{
    /// <summary>Number of cache misses that required measuring text.</summary>
    public int MeasureCount { get; set; }

    /// <summary>Number of cache hits.</summary>
    public int CacheHits { get; set; }

    /// <summary>Number of cache invalidations.</summary>
    public int Invalidations { get; set; }

    /// <summary>Current cache entry count.</summary>
    public int CacheSize { get; set; }

    /// <summary>Ratio of cache hits to all measurement lookups.</summary>
    public double CacheHitRatio
    {
        get
        {
            var total = MeasureCount + CacheHits;
            return total <= 0 ? 0 : (double)CacheHits / total;
        }
    }
}

/// <summary>Reason why a document layout needs invalidation.</summary>
public enum DocumentLayoutInvalidationReason
{
    /// <summary>No specific invalidation reason was supplied.</summary>
    Unknown,

    /// <summary>Text content or inline formatting changed.</summary>
    TextChanged,

    /// <summary>An image object changed position, wrapping, size, or metadata affecting layout.</summary>
    ImageChanged,

    /// <summary>Page size, margins, header, or footer layout changed.</summary>
    PageLayoutChanged,

    /// <summary>Zoom changed. The document model is unchanged, but measurements must be refreshed.</summary>
    ZoomChanged,

    /// <summary>An image drag preview or commit caused a layout reflow.</summary>
    ImageDragReflow,

    /// <summary>An image resize preview or commit caused a layout reflow.</summary>
    ImageResizeReflow
}

/// <summary>Input for layout invalidation planning.</summary>
public class DocumentLayoutInvalidationRequest
{
    /// <summary>Reason why layout is being recomputed.</summary>
    public DocumentLayoutInvalidationReason Reason { get; set; } = DocumentLayoutInvalidationReason.Unknown;

    /// <summary>Primary changed block id, if known.</summary>
    public string? BlockId { get; set; }

    /// <summary>Additional changed block ids, if known.</summary>
    public IReadOnlyCollection<string> BlockIds { get; set; } = [];

    /// <summary>Previous layout snapshot used to compute the affected page range.</summary>
    public DocumentPageLayoutSnapshot? PreviousSnapshot { get; set; }
}

/// <summary>Result of layout invalidation planning.</summary>
public class DocumentLayoutInvalidationResult
{
    /// <summary>Reason that produced this invalidation result.</summary>
    public DocumentLayoutInvalidationReason Reason { get; set; } = DocumentLayoutInvalidationReason.Unknown;

    /// <summary>Zero-based invalidated page indexes.</summary>
    public List<int> InvalidatedPageIndices { get; set; } = [];

    /// <summary>Whether the whole previous snapshot should be treated as invalid.</summary>
    public bool InvalidatesWholeDocument { get; set; }

    /// <summary>Whether the invalidation affects persisted document model data.</summary>
    public bool InvalidatesModel { get; set; } = true;

    /// <summary>Whether only render measurements need to be refreshed.</summary>
    public bool InvalidatesMeasurementsOnly { get; set; }

    /// <summary>Number of invalidated pages.</summary>
    public int InvalidatedPageCount => InvalidatedPageIndices.Count;
}

/// <summary>Plans which pages need layout invalidation for common document editor changes.</summary>
public static class DocumentLayoutInvalidationPlanner
{
    /// <summary>Creates an invalidation plan from a previous snapshot and change request.</summary>
    public static DocumentLayoutInvalidationResult Plan(DocumentLayoutInvalidationRequest? request)
    {
        request ??= new DocumentLayoutInvalidationRequest();
        var previous = request.PreviousSnapshot;
        var pageCount = previous?.Pages.Count ?? 0;
        var result = new DocumentLayoutInvalidationResult
        {
            Reason = request.Reason
        };

        if (pageCount <= 0)
        {
            result.InvalidatesWholeDocument = true;
            return result;
        }

        switch (request.Reason)
        {
            case DocumentLayoutInvalidationReason.PageLayoutChanged:
                result.InvalidatesWholeDocument = true;
                result.InvalidatedPageIndices.AddRange(AllPages(pageCount));
                return result;

            case DocumentLayoutInvalidationReason.ZoomChanged:
                result.InvalidatesModel = false;
                result.InvalidatesMeasurementsOnly = true;
                result.InvalidatedPageIndices.AddRange(AllPages(pageCount));
                return result;

            case DocumentLayoutInvalidationReason.TextChanged:
                var startPage = FindFirstBlockPage(previous!, EnumerateBlockIds(request));
                result.InvalidatedPageIndices.AddRange(startPage is null ? AllPages(pageCount) : PagesFrom(startPage.Value, pageCount));
                return result;

            case DocumentLayoutInvalidationReason.ImageChanged:
            case DocumentLayoutInvalidationReason.ImageDragReflow:
            case DocumentLayoutInvalidationReason.ImageResizeReflow:
                var imagePages = FindTouchedImagePages(previous!, EnumerateBlockIds(request));
                result.InvalidatedPageIndices.AddRange(imagePages.Count == 0 ? AllPages(pageCount) : imagePages);
                return result;

            default:
                result.InvalidatesWholeDocument = true;
                result.InvalidatedPageIndices.AddRange(AllPages(pageCount));
                return result;
        }
    }

    private static IEnumerable<string> EnumerateBlockIds(DocumentLayoutInvalidationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.BlockId))
        {
            yield return request.BlockId!;
        }

        foreach (var blockId in request.BlockIds)
        {
            if (!string.IsNullOrWhiteSpace(blockId))
            {
                yield return blockId;
            }
        }
    }

    private static int? FindFirstBlockPage(DocumentPageLayoutSnapshot snapshot, IEnumerable<string> blockIds)
    {
        var ids = blockIds.ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return null;
        }

        return snapshot.Pages
            .Where(page => page.Paragraphs.Any(paragraph => ids.Contains(paragraph.BlockId))
                || page.Objects.Any(obj => ids.Contains(obj.BlockId)))
            .Select(page => (int?)page.PageIndex)
            .OrderBy(index => index)
            .FirstOrDefault();
    }

    private static List<int> FindTouchedImagePages(DocumentPageLayoutSnapshot snapshot, IEnumerable<string> blockIds)
    {
        var ids = blockIds.ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return [];
        }

        var pages = new SortedSet<int>();
        foreach (var page in snapshot.Pages)
        {
            if (page.Objects.Any(obj => ids.Contains(obj.BlockId) || (!string.IsNullOrWhiteSpace(obj.AnchorBlockId) && ids.Contains(obj.AnchorBlockId!)))
                || page.Exclusions.Any(zone => ids.Contains(zone.BlockId)))
            {
                pages.Add(page.PageIndex);
            }
        }

        return pages.ToList();
    }

    private static IEnumerable<int> AllPages(int pageCount)
        => Enumerable.Range(0, pageCount);

    private static IEnumerable<int> PagesFrom(int startPage, int pageCount)
    {
        var start = Math.Clamp(startPage, 0, Math.Max(0, pageCount - 1));
        return Enumerable.Range(start, Math.Max(0, pageCount - start));
    }
}

/// <summary>Abstraction for text measurement used by the layout engine.</summary>
public interface IDocumentTextMeasurer
{
    /// <summary>Measures a text run.</summary>
    DocumentTextMeasurement Measure(DocumentTextMeasurementRequest request);

    /// <summary>Clears any cached measurements.</summary>
    void ClearCache();

    /// <summary>Gets current cache statistics.</summary>
    DocumentTextMeasurementCacheStats GetCacheStats();
}

/// <summary>Deterministic .NET text measurer used for tests and non-browser fallback layout.</summary>
public class ApproximateDocumentTextMeasurer : IDocumentTextMeasurer
{
    private readonly ConcurrentDictionary<string, DocumentTextMeasurement> _cache = new();
    private int _measureCount;
    private int _cacheHits;
    private int _invalidations;

    /// <inheritdoc />
    public DocumentTextMeasurement Measure(DocumentTextMeasurementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = CreateCacheKey(request);
        if (_cache.TryGetValue(key, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        var measured = MeasureCore(request);
        _cache[key] = measured;
        Interlocked.Increment(ref _measureCount);
        return measured;
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        if (!_cache.IsEmpty)
        {
            Interlocked.Increment(ref _invalidations);
        }

        _cache.Clear();
    }

    /// <inheritdoc />
    public DocumentTextMeasurementCacheStats GetCacheStats()
        => new()
        {
            MeasureCount = _measureCount,
            CacheHits = _cacheHits,
            Invalidations = _invalidations,
            CacheSize = _cache.Count
        };

    private static string CreateCacheKey(DocumentTextMeasurementRequest request)
        => string.Join('|',
            request.Text,
            request.FontFamily,
            request.FontSize.ToString("0.###", CultureInfo.InvariantCulture),
            request.FontWeight,
            request.FontStyle,
            request.LetterSpacing.ToString("0.###", CultureInfo.InvariantCulture),
            request.Zoom.ToString("0.###", CultureInfo.InvariantCulture));

    private static DocumentTextMeasurement MeasureCore(DocumentTextMeasurementRequest request)
    {
        var fontSize = Math.Max(1, request.FontSize) * Math.Max(0.1, request.Zoom);
        var weightMultiplier = IsBold(request.FontWeight) ? 1.08 : 1;
        var styleMultiplier = string.Equals(request.FontStyle, "italic", StringComparison.OrdinalIgnoreCase) ? 1.03 : 1;
        var width = 0d;

        foreach (var ch in request.Text ?? string.Empty)
        {
            width += fontSize * GetCharacterWidthFactor(ch) * weightMultiplier * styleMultiplier;
        }

        if (!string.IsNullOrEmpty(request.Text) && request.LetterSpacing != 0)
        {
            width += Math.Max(0, request.Text.Length - 1) * request.LetterSpacing * Math.Max(0.1, request.Zoom);
        }

        return new DocumentTextMeasurement
        {
            Width = Math.Max(0, width),
            Height = fontSize * 1.2
        };
    }

    private static bool IsBold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value, "bold", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "bolder", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
            && numeric >= 600;
    }

    private static double GetCharacterWidthFactor(char ch)
    {
        if (ch == '\t') return 1.6;
        if (char.IsWhiteSpace(ch)) return 0.34;
        if (char.IsDigit(ch)) return 0.55;
        if (char.IsUpper(ch)) return 0.66;
        if (char.IsPunctuation(ch) || char.IsSymbol(ch)) return 0.32;
        if (ch > 0x2E80) return 1.0;
        return 0.52;
    }
}

/// <summary>Layouts document pages, text line boxes, positioned objects, and text exclusion zones.</summary>
public class DocumentLayoutEngine
{
    private readonly IDocumentTextMeasurer _textMeasurer;

    /// <summary>Creates a layout engine.</summary>
    public DocumentLayoutEngine(IDocumentTextMeasurer? textMeasurer = null)
    {
        _textMeasurer = textMeasurer ?? new ApproximateDocumentTextMeasurer();
    }

    /// <summary>Computes a page layout snapshot for the given document.</summary>
    public DocumentPageLayoutSnapshot Layout(
        DocumentEditorDocument document,
        DocumentPageSettings? pageSettings = null,
        DocumentLayoutRendererMetrics? rendererMetrics = null,
        DocumentLayoutInvalidationRequest? invalidationRequest = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var stopwatch = Stopwatch.StartNew();
        var invalidation = DocumentLayoutInvalidationPlanner.Plan(invalidationRequest);
        var metrics = rendererMetrics ?? new DocumentLayoutRendererMetrics();
        var effectivePageSettings = pageSettings ?? document.PageSettings;
        var state = new LayoutState(document, effectivePageSettings, metrics, _textMeasurer);
        state.EnsurePage();

        foreach (var block in document.Blocks.OrderBy(block => block.Order))
        {
            if (block.Type == DocumentBlockType.PageBreak)
            {
                state.StartNewPage();
                continue;
            }

            if (block.Content is ImageBlockContent imageContent)
            {
                LayoutImageBlock(state, block, imageContent);
                continue;
            }

            var inlines = GetTextInlines(block.Content);
            if (inlines is not null)
            {
                LayoutTextBlock(state, block, inlines);
                continue;
            }

            LayoutFallbackBlock(state, block);
        }

        stopwatch.Stop();
        ApplyPerformanceMetrics(state.Snapshot, stopwatch.Elapsed.TotalMilliseconds, invalidation);
        return state.Snapshot;
    }

    private void ApplyPerformanceMetrics(
        DocumentPageLayoutSnapshot snapshot,
        double layoutPassMs,
        DocumentLayoutInvalidationResult invalidation)
    {
        var stats = _textMeasurer.GetCacheStats();
        snapshot.Performance = new DocumentLayoutPerformanceMetrics
        {
            LayoutPassMs = Math.Max(0, layoutPassMs),
            ReflowAfterDragMs = invalidation.Reason == DocumentLayoutInvalidationReason.ImageDragReflow ? Math.Max(0, layoutPassMs) : 0,
            ReflowAfterResizeMs = invalidation.Reason == DocumentLayoutInvalidationReason.ImageResizeReflow ? Math.Max(0, layoutPassMs) : 0,
            InvalidatedPageCount = invalidation.InvalidatedPageCount,
            InvalidatedPageIndices = invalidation.InvalidatedPageIndices.ToList(),
            TextMeasureCount = stats.MeasureCount,
            TextMeasureCacheHits = stats.CacheHits,
            TextMeasureInvalidations = stats.Invalidations,
            TextMeasureCacheSize = stats.CacheSize,
            TextMeasureCacheHitRatio = stats.CacheHitRatio,
            InvalidatesModel = invalidation.InvalidatesModel,
            InvalidatesMeasurementsOnly = invalidation.InvalidatesMeasurementsOnly,
            Reason = invalidation.Reason.ToString()
        };
    }

    private static void LayoutImageBlock(LayoutState state, DocumentBlock block, ImageBlockContent image)
    {
        var layout = image.Layout ?? DocumentObjectLayout.Inline();
        var size = GetImageSize(image, state.Metrics);
        layout.Anchor.BlockId ??= block.Id;

        if (layout.IsInline)
        {
            LayoutInlineImageBlock(state, block, image, layout, size.Width, size.Height);
            return;
        }

        var page = state.CurrentPage;
        var body = page.BodyRect;
        var anchorRect = ResolveObjectAnchorRect(state, block, layout);
        var unclamped = DocumentLayoutGeometryHelper.ResolveObjectRect(
            layout,
            page.PageRect,
            body,
            anchorRect,
            anchorRect,
            anchorRect,
            size.Width,
            size.Height,
            clampToBody: false);

        var objectBox = DocumentLayoutGeometryHelper.CreateObjectLayoutBox(
            block.Id,
            block.Id,
            layout,
            page.PageRect,
            body,
            anchorRect,
            anchorRect,
            anchorRect,
            size.Width,
            size.Height,
            state.Metrics.ClampAnchoredObjectsToBody);
        objectBox.AnchorBlockId = layout.Anchor.BlockId;
        objectBox.PageIndex = page.PageIndex;
        layout.Anchor.PageIndex = page.PageIndex;

        if (!RectsApproximatelyEqual(unclamped, objectBox.ObjectRect))
        {
            state.Snapshot.Diagnostics.Add($"Object '{block.Id}' was clamped into page body on page {page.PageNumber}.");
        }

        ResolveObjectOverlap(page, objectBox, body);
        page.Objects.Add(objectBox);
        var zone = DocumentLayoutGeometryHelper.CreateExclusionZone(objectBox, body);
        if (zone is not null)
        {
            page.Exclusions.Add(zone);
        }
    }

    private static DocumentLayoutRect ResolveObjectAnchorRect(
        LayoutState state,
        DocumentBlock block,
        DocumentObjectLayout layout)
    {
        var page = state.CurrentPage;
        var body = page.BodyRect;
        if (layout.Anchor.FixedOnPage)
        {
            return new DocumentLayoutRect
            {
                X = body.X,
                Y = body.Y,
                Width = body.Width,
                Height = state.DefaultLineHeight
            };
        }

        if (!string.IsNullOrWhiteSpace(layout.Anchor.BlockId)
            && state.TryGetParagraphRect(layout.Anchor.BlockId, out var paragraphRect))
        {
            return paragraphRect.Clone();
        }

        return new DocumentLayoutRect
        {
            X = body.X,
            Y = state.CurrentY,
            Width = body.Width,
            Height = state.DefaultLineHeight
        };
    }

    private static void ResolveObjectOverlap(DocumentPageLayoutBox page, DocumentObjectLayoutBox objectBox, DocumentLayoutRect body)
    {
        if (objectBox.AllowOverlap)
        {
            return;
        }

        var layer = GetObjectLayer(objectBox.Layout);
        var guard = 0;
        while (guard++ < 64)
        {
            var overlap = page.Objects
                .Where(existing => !existing.AllowOverlap && GetObjectLayer(existing.Layout) == layer)
                .Where(existing => DocumentLayoutGeometryHelper.Intersects(existing.ObjectRect, objectBox.ObjectRect))
                .OrderBy(existing => existing.ObjectRect.Bottom)
                .LastOrDefault();
            if (overlap is null)
            {
                break;
            }

            var nextY = overlap.ObjectRect.Bottom + 8;
            if (nextY + objectBox.ObjectRect.Height > body.Bottom)
            {
                nextY = Math.Max(body.Y, body.Bottom - objectBox.ObjectRect.Height);
                if (nextY <= objectBox.ObjectRect.Y + 0.01)
                {
                    break;
                }
            }

            objectBox.ObjectRect.Y = nextY;
            objectBox.WrapRect = DocumentLayoutGeometryHelper.ComputeWrapRect(objectBox.ObjectRect, objectBox.Layout.Wrap);
        }
    }

    private static int GetObjectLayer(DocumentObjectLayout layout)
        => layout.Wrap.Mode switch
        {
            DocumentWrapMode.BehindText => 0,
            DocumentWrapMode.InFrontOfText => 30,
            _ => 20
        };

    private static void LayoutInlineImageBlock(
        LayoutState state,
        DocumentBlock block,
        ImageBlockContent image,
        DocumentObjectLayout layout,
        double width,
        double height)
    {
        var lineHeight = Math.Max(height, state.DefaultLineHeight);
        state.EnsureLineFits(lineHeight);

        var page = state.CurrentPage;
        var body = page.BodyRect;
        var x = image.Alignment switch
        {
            DocumentImageAlignment.Start => body.X,
            DocumentImageAlignment.End => body.Right - width,
            _ => body.X + ((body.Width - width) / 2)
        };
        x = Math.Clamp(x, body.X, Math.Max(body.X, body.Right - width));

        var paragraph = new DocumentParagraphLayoutBox
        {
            BlockId = block.Id,
            PageIndex = page.PageIndex,
            Rect = new DocumentLayoutRect
            {
                X = body.X,
                Y = state.CurrentY,
                Width = body.Width,
                Height = lineHeight
            }
        };
        var line = new DocumentLineBox
        {
            BlockId = block.Id,
            PageIndex = page.PageIndex,
            LineIndex = 0,
            Rect = paragraph.Rect.Clone(),
            BaselineY = state.CurrentY + (lineHeight * 0.8),
            AvailableIntervals =
            [
                new DocumentLayoutInterval
                {
                    X = body.X,
                    Width = body.Width
                }
            ]
        };
        paragraph.Lines.Add(line);
        page.Paragraphs.Add(paragraph);
        state.RecordParagraphRect(block.Id, paragraph.Rect);

        var objectRect = new DocumentLayoutRect
        {
            X = x,
            Y = state.CurrentY,
            Width = width,
            Height = height
        };
        page.Objects.Add(new DocumentObjectLayoutBox
        {
            Id = block.Id,
            BlockId = block.Id,
            AnchorBlockId = block.Id,
            PageIndex = page.PageIndex,
            ObjectRect = objectRect,
            WrapRect = objectRect.Clone(),
            Layout = layout,
            ZIndex = layout.Stacking.ZIndex,
            AllowOverlap = layout.Stacking.AllowOverlap
        });

        state.CurrentY += lineHeight + GetParagraphSpacingAfter(block, state.Document.Theme);
    }

    private static void LayoutTextBlock(LayoutState state, DocumentBlock block, IReadOnlyList<InlineContent> inlines)
    {
        var baseStyle = TextStyle.FromDocument(state.Document.Theme, state.Metrics);
        var runs = BuildTextRuns(inlines, baseStyle);
        var lineHeight = GetLineHeight(block, state.Document.Theme, state.Metrics, runs);
        var spacingBefore = Math.Max(0, block.ParagraphProperties.SpacingBefore);
        var spacingAfter = GetParagraphSpacingAfter(block, state.Document.Theme);

        state.CurrentY += spacingBefore;
        state.EnsureLineFits(lineHeight);

        var context = new ParagraphLayoutContext(state, block, lineHeight);
        if (runs.Count == 0)
        {
            context.EnsureWritableLine();
            context.FinalizeCurrentLine(force: true);
            context.AdvanceToParagraphBottom();
            state.RecordParagraphRect(block.Id, context.ParagraphRect);
            state.CurrentY += spacingAfter;
            return;
        }

        foreach (var run in runs)
        {
            for (var offset = 0; offset < run.Text.Length; offset++)
            {
                var ch = run.Text[offset];
                if (ch == '\r')
                {
                    continue;
                }

                if (ch == '\n')
                {
                    context.EnsureWritableLine();
                    context.FinalizeCurrentLine(force: true);
                    context.AdvanceLine();
                    continue;
                }

                var width = context.Measure(run, ch.ToString());
                context.PlaceCharacter(run, offset, ch, width);
            }
        }

        context.FinalizeCurrentLine(force: true);
        context.AdvanceToParagraphBottom();
        state.RecordParagraphRect(block.Id, context.ParagraphRect);
        state.CurrentY += spacingAfter;
    }

    private static void LayoutFallbackBlock(LayoutState state, DocumentBlock block)
    {
        var lineHeight = state.DefaultLineHeight;
        state.EnsureLineFits(lineHeight);
        var page = state.CurrentPage;
        var paragraph = new DocumentParagraphLayoutBox
        {
            BlockId = block.Id,
            PageIndex = page.PageIndex,
            Rect = new DocumentLayoutRect
            {
                X = page.BodyRect.X,
                Y = state.CurrentY,
                Width = page.BodyRect.Width,
                Height = lineHeight
            }
        };
        paragraph.Lines.Add(new DocumentLineBox
        {
            BlockId = block.Id,
            PageIndex = page.PageIndex,
            LineIndex = 0,
            Rect = paragraph.Rect.Clone(),
            BaselineY = state.CurrentY + (lineHeight * 0.8)
        });
        page.Paragraphs.Add(paragraph);
        state.RecordParagraphRect(block.Id, paragraph.Rect);
        state.CurrentY += lineHeight + GetParagraphSpacingAfter(block, state.Document.Theme);
    }

    private static IReadOnlyList<InlineContent>? GetTextInlines(DocumentBlockContent content)
        => content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => null
        };

    private static List<TextRunLayoutSource> BuildTextRuns(IReadOnlyList<InlineContent> inlines, TextStyle baseStyle)
    {
        var runs = new List<TextRunLayoutSource>();
        for (var index = 0; index < inlines.Count; index++)
        {
            var inline = inlines[index];
            var text = GetInlineText(inline);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            runs.Add(new TextRunLayoutSource(index, inline.Id, text, baseStyle.Apply(inline.Marks)));
        }

        return runs;
    }

    private static string GetInlineText(InlineContent inline)
        => inline switch
        {
            TextRun text => text.Text ?? string.Empty,
            TokenRun token => token.DisplayName ?? token.FallbackText ?? token.Key ?? string.Empty,
            DocumentFieldRun field => field.DisplayText ?? field.FallbackText ?? FieldPlaceholder(field.FieldType),
            DocumentNoteReferenceRun note => note.DisplayMarker ?? (note.NoteType == DocumentNoteType.Endnote ? "e" : "1"),
            _ => string.Empty
        };

    private static string FieldPlaceholder(DocumentFieldType type)
        => type switch
        {
            DocumentFieldType.PageNumber => "1",
            DocumentFieldType.PageCount => "1",
            DocumentFieldType.PageXOfY => "1 / 1",
            DocumentFieldType.Date => DateTime.Today.ToShortDateString(),
            DocumentFieldType.DocumentTitle => "Document title",
            DocumentFieldType.Author => "Author",
            DocumentFieldType.LastSaved => DateTime.Today.ToShortDateString(),
            DocumentFieldType.SectionPageNumber => "1",
            DocumentFieldType.SectionPageCount => "1",
            DocumentFieldType.FileName => "Document",
            DocumentFieldType.RevisionNumber => "1",
            _ => string.Empty
        };

    private static double GetLineHeight(
        DocumentBlock block,
        DocumentEditorTheme theme,
        DocumentLayoutRendererMetrics metrics,
        IReadOnlyList<TextRunLayoutSource> runs)
    {
        var maxFont = runs.Count == 0
            ? Math.Max(1, theme.BodyFontSize > 0 ? theme.BodyFontSize : metrics.DefaultFontSize)
            : runs.Max(run => Math.Max(1, run.Style.FontSize));
        var themeLineHeight = theme.BodyLineHeight > 0 ? theme.BodyLineHeight : metrics.DefaultLineHeightMultiplier;
        var lineSpacing = block.ParagraphProperties.LineSpacing > 0 ? block.ParagraphProperties.LineSpacing : 1;
        return maxFont * themeLineHeight * lineSpacing * Math.Max(0.1, metrics.Zoom);
    }

    private static double GetParagraphSpacingAfter(DocumentBlock block, DocumentEditorTheme theme)
        => block.ParagraphProperties.SpacingAfter > 0
            ? block.ParagraphProperties.SpacingAfter
            : Math.Max(0, theme.ParagraphSpacingAfter);

    private static (double Width, double Height) GetImageSize(ImageBlockContent image, DocumentLayoutRendererMetrics metrics)
    {
        var width = image.Layout?.Transform.Width
            ?? image.Size.Width
            ?? image.NaturalSize.Width
            ?? image.Layout?.Transform.NaturalWidth
            ?? metrics.DefaultImageWidth;
        var height = image.Layout?.Transform.Height
            ?? image.Size.Height
            ?? image.NaturalSize.Height
            ?? image.Layout?.Transform.NaturalHeight
            ?? metrics.DefaultImageHeight;
        return (Math.Max(1, width), Math.Max(1, height));
    }

    private static bool RectsApproximatelyEqual(DocumentLayoutRect a, DocumentLayoutRect b)
        => Math.Abs(a.X - b.X) < 0.01
            && Math.Abs(a.Y - b.Y) < 0.01
            && Math.Abs(a.Width - b.Width) < 0.01
            && Math.Abs(a.Height - b.Height) < 0.01;

    private sealed class LayoutState
    {
        private readonly DocumentPageSettings _pageSettings;
        private readonly Dictionary<string, DocumentLayoutRect> _paragraphRects = [];

        public LayoutState(
            DocumentEditorDocument document,
            DocumentPageSettings pageSettings,
            DocumentLayoutRendererMetrics metrics,
            IDocumentTextMeasurer textMeasurer)
        {
            Document = document;
            _pageSettings = pageSettings;
            Metrics = metrics;
            TextMeasurer = textMeasurer;
            Snapshot = new DocumentPageLayoutSnapshot
            {
                DocumentId = document.DocumentId
            };
        }

        public DocumentEditorDocument Document { get; }

        public DocumentLayoutRendererMetrics Metrics { get; }

        public IDocumentTextMeasurer TextMeasurer { get; }

        public DocumentPageLayoutSnapshot Snapshot { get; }

        public DocumentPageLayoutBox CurrentPage => Snapshot.Pages[^1];

        public double CurrentY { get; set; }

        public double DefaultLineHeight
            => Math.Max(1, (Document.Theme.BodyFontSize > 0 ? Document.Theme.BodyFontSize : Metrics.DefaultFontSize)
                * (Document.Theme.BodyLineHeight > 0 ? Document.Theme.BodyLineHeight : Metrics.DefaultLineHeightMultiplier)
                * Math.Max(0.1, Metrics.Zoom));

        public void RecordParagraphRect(string? blockId, DocumentLayoutRect? rect)
        {
            if (string.IsNullOrWhiteSpace(blockId) || rect is null || rect.IsEmpty)
            {
                return;
            }

            _paragraphRects[blockId] = rect.Clone();
        }

        public bool TryGetParagraphRect(string blockId, out DocumentLayoutRect rect)
        {
            if (_paragraphRects.TryGetValue(blockId, out var stored))
            {
                rect = stored;
                return true;
            }

            rect = new DocumentLayoutRect();
            return false;
        }

        public void EnsurePage()
        {
            if (Snapshot.Pages.Count > 0)
            {
                return;
            }

            StartNewPage();
        }

        public void StartNewPage()
        {
            var page = CreatePage(Snapshot.Pages.Count, _pageSettings);
            Snapshot.Pages.Add(page);
            CurrentY = page.BodyRect.Y;
        }

        public void EnsureLineFits(double lineHeight)
        {
            EnsurePage();
            if (CurrentY + lineHeight > CurrentPage.BodyRect.Bottom && CurrentY > CurrentPage.BodyRect.Y)
            {
                StartNewPage();
            }
        }

        private static DocumentPageLayoutBox CreatePage(int pageIndex, DocumentPageSettings settings)
        {
            var width = settings.Landscape ? settings.Size.Height : settings.Size.Width;
            var height = settings.Landscape ? settings.Size.Width : settings.Size.Height;
            var margins = settings.Margins;
            var pageRect = new DocumentLayoutRect
            {
                X = 0,
                Y = 0,
                Width = width,
                Height = height
            };
            var bodyRect = DocumentLayoutRect.FromBounds(
                margins.Left,
                margins.Top,
                Math.Max(margins.Left, width - margins.Right),
                Math.Max(margins.Top, height - margins.Bottom));

            return new DocumentPageLayoutBox
            {
                PageIndex = pageIndex,
                PageNumber = pageIndex + 1,
                PageRect = pageRect,
                BodyRect = bodyRect,
                HeaderRect = new DocumentLayoutRect
                {
                    X = bodyRect.X,
                    Y = Math.Max(0, settings.HeaderDistanceFromTop),
                    Width = bodyRect.Width,
                    Height = Math.Max(0, margins.Top - settings.HeaderDistanceFromTop)
                },
                FooterRect = new DocumentLayoutRect
                {
                    X = bodyRect.X,
                    Y = Math.Min(height, height - margins.Bottom),
                    Width = bodyRect.Width,
                    Height = Math.Max(0, margins.Bottom - settings.FooterDistanceFromBottom)
                }
            };
        }
    }

    private sealed class ParagraphLayoutContext
    {
        private readonly LayoutState _state;
        private readonly DocumentBlock _block;
        private readonly double _lineHeight;
        private readonly List<DocumentTextSegmentBox> _segments = [];
        private DocumentLineBox? _line;
        private DocumentParagraphLayoutBox? _paragraph;
        private List<DocumentLayoutInterval> _intervals = [];
        private int _intervalIndex;
        private double _x;
        private int _lineIndex;
        private double _paragraphBottom;

        public ParagraphLayoutContext(LayoutState state, DocumentBlock block, double lineHeight)
        {
            _state = state;
            _block = block;
            _lineHeight = lineHeight;
        }

        public double Measure(TextRunLayoutSource run, string text)
            => _state.TextMeasurer.Measure(new DocumentTextMeasurementRequest
            {
                Text = text,
                FontFamily = run.Style.FontFamily,
                FontSize = run.Style.FontSize,
                FontWeight = run.Style.FontWeight,
                FontStyle = run.Style.FontStyle,
                LetterSpacing = run.Style.LetterSpacing,
                Zoom = _state.Metrics.Zoom
            }).Width;

        public void PlaceCharacter(TextRunLayoutSource run, int sourceOffset, char ch, double width)
        {
            EnsureWritableLine();
            if (!FitsCurrentInterval(width))
            {
                if (!MoveToNextInterval(width))
                {
                    FinalizeCurrentLine(force: _segments.Count == 0);
                    AdvanceLine();
                    EnsureWritableLine();
                }
            }

            AddCharacter(run, sourceOffset, ch, width);
        }

        public void AdvanceLine()
        {
            _state.CurrentY += _lineHeight;
            _line = null;
            _intervals = [];
            _intervalIndex = 0;
            _segments.Clear();
            _state.EnsureLineFits(_lineHeight);
            if (_paragraph is not null && _paragraph.PageIndex != _state.CurrentPage.PageIndex)
            {
                _paragraph = null;
            }
        }

        public void EnsureWritableLine()
        {
            var guard = 0;
            while (true)
            {
                _state.EnsureLineFits(_lineHeight);
                var page = _state.CurrentPage;
                var bounds = GetLineBounds(page);
                _intervals = DocumentLayoutGeometryHelper.GetAvailableLineIntervals(
                    _state.CurrentY,
                    _lineHeight,
                    page.Exclusions,
                    bounds,
                    _state.Metrics.MinimumLineIntervalWidth).ToList();

                if (_intervals.Count > 0)
                {
                    EnsureParagraphAndLine(page, bounds);
                    return;
                }

                _state.CurrentY += _lineHeight;
                _line = null;
                _paragraph = null;
                _segments.Clear();
                if (++guard > 10000)
                {
                    throw new InvalidOperationException("Unable to find an available line interval for document layout.");
                }
            }
        }

        public void FinalizeCurrentLine(bool force)
        {
            if (_line is null)
            {
                if (!force)
                {
                    return;
                }

                EnsureWritableLine();
            }

            if (_line is null || _paragraph is null)
            {
                return;
            }

            _line.Segments.Clear();
            _line.Segments.AddRange(_segments.Select(segment => segment));
            if (_paragraph.Lines.Count == 0 || _paragraph.Lines[^1] != _line)
            {
                _paragraph.Lines.Add(_line);
            }

            _paragraph.Rect = DocumentLayoutGeometryHelper.Union(_paragraph.Lines.Select(line => line.Rect));
            _paragraphBottom = Math.Max(_paragraphBottom, _line.Rect.Bottom);
            _segments.Clear();
            _line = null;
        }

        public void AdvanceToParagraphBottom()
        {
            if (_paragraphBottom > _state.CurrentY)
            {
                _state.CurrentY = _paragraphBottom;
            }
        }

        public DocumentLayoutRect? ParagraphRect => _paragraph?.Rect;

        private DocumentLayoutRect GetLineBounds(DocumentPageLayoutBox page)
        {
            var props = _block.ParagraphProperties;
            var left = page.BodyRect.X + Math.Max(0, props.LeftIndent);
            var right = page.BodyRect.Right - Math.Max(0, props.RightIndent);
            if (_lineIndex == 0)
            {
                left += props.FirstLineIndent;
            }

            if (right <= left)
            {
                right = left + _state.Metrics.MinimumLineIntervalWidth;
            }

            return new DocumentLayoutRect
            {
                X = left,
                Y = _state.CurrentY,
                Width = right - left,
                Height = _lineHeight
            };
        }

        private void EnsureParagraphAndLine(DocumentPageLayoutBox page, DocumentLayoutRect bounds)
        {
            if (_paragraph is null || _paragraph.PageIndex != page.PageIndex)
            {
                _paragraph = new DocumentParagraphLayoutBox
                {
                    BlockId = _block.Id,
                    PageIndex = page.PageIndex,
                    Rect = bounds.Clone()
                };
                page.Paragraphs.Add(_paragraph);
            }

            if (_line is null)
            {
                _intervalIndex = 0;
                _x = _intervals[0].X;
                _line = new DocumentLineBox
                {
                    BlockId = _block.Id,
                    PageIndex = page.PageIndex,
                    LineIndex = _lineIndex,
                    Rect = bounds.Clone(),
                    BaselineY = _state.CurrentY + (_lineHeight * 0.8),
                    AvailableIntervals = _intervals.Select(interval => new DocumentLayoutInterval
                    {
                        X = interval.X,
                        Width = interval.Width
                    }).ToList()
                };
                _lineIndex++;
            }
        }

        private bool FitsCurrentInterval(double width)
            => _intervals.Count > 0
                && _intervalIndex < _intervals.Count
                && _x + width <= _intervals[_intervalIndex].End + 0.01;

        private bool MoveToNextInterval(double width)
        {
            for (var index = _intervalIndex + 1; index < _intervals.Count; index++)
            {
                if (_intervals[index].Width + 0.01 >= width)
                {
                    _intervalIndex = index;
                    _x = _intervals[index].X;
                    return true;
                }
            }

            return false;
        }

        private void AddCharacter(TextRunLayoutSource run, int sourceOffset, char ch, double width)
        {
            if (_line is null)
            {
                return;
            }

            var segment = _segments.LastOrDefault();
            if (segment is null
                || segment.InlineIndex != run.InlineIndex
                || segment.InlineId != run.InlineId
                || segment.StartOffset + segment.Length != sourceOffset)
            {
                segment = new DocumentTextSegmentBox
                {
                    BlockId = _block.Id,
                    InlineId = run.InlineId,
                    InlineIndex = run.InlineIndex,
                    StartOffset = sourceOffset,
                    Rect = new DocumentLayoutRect
                    {
                        X = _x,
                        Y = _state.CurrentY,
                        Width = 0,
                        Height = _lineHeight
                    }
                };
                _segments.Add(segment);
            }

            segment.Text += ch;
            segment.Length++;
            segment.Rect.Width += width;
            _x += width;
        }
    }

    private sealed record TextRunLayoutSource(int InlineIndex, string? InlineId, string Text, TextStyle Style);

    private sealed record TextStyle(
        string FontFamily,
        double FontSize,
        string FontWeight,
        string FontStyle,
        double LetterSpacing)
    {
        public static TextStyle FromDocument(DocumentEditorTheme theme, DocumentLayoutRendererMetrics metrics)
            => new(
                string.IsNullOrWhiteSpace(theme.BodyFontFamily) ? metrics.DefaultFontFamily : theme.BodyFontFamily,
                theme.BodyFontSize > 0 ? theme.BodyFontSize : metrics.DefaultFontSize,
                "400",
                "normal",
                0);

        public TextStyle Apply(IEnumerable<InlineMark> marks)
        {
            var style = this;
            foreach (var mark in marks)
            {
                style = mark.Type switch
                {
                    InlineMarkType.Bold => style with { FontWeight = "700" },
                    InlineMarkType.Italic => style with { FontStyle = "italic" },
                    InlineMarkType.FontFamily when !string.IsNullOrWhiteSpace(mark.Value) => style with { FontFamily = mark.Value! },
                    InlineMarkType.FontSize when TryParseFontSize(mark.Value, out var size) => style with { FontSize = size },
                    _ => style
                };
            }

            return style;
        }

        private static bool TryParseFontSize(string? value, out double size)
        {
            size = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var cleaned = value.Trim().Replace("pt", string.Empty, StringComparison.OrdinalIgnoreCase);
            return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out size)
                && size > 0;
        }
    }
}
