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
    private const double ImageCaptionGap = 4;
    private const double ObjectOverlapGap = 8;

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

        var orderedBlocks = document.Blocks.OrderBy(block => block.Order).ToList();
        // Floating images anchored to the previous paragraph must register their exclusion before that paragraph is laid out.
        var preAnchoredImagesByBlockId = BuildPreAnchoredImageMap(orderedBlocks);
        var preAnchoredImageIds = preAnchoredImagesByBlockId
            .SelectMany(pair => pair.Value)
            .Select(block => block.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var preLaidImageIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var block in orderedBlocks)
        {
            var pageIndexBefore = state.CurrentPage.PageIndex;
            var currentYBefore = state.CurrentY;

            if (block.Type == DocumentBlockType.PageBreak)
            {
                state.StartNewPage();
                state.RecordBlockDebug(block, pageIndexBefore, currentYBefore);
                continue;
            }

            if (block.Content is ImageBlockContent imageContent)
            {
                if (preLaidImageIds.Contains(block.Id) || preAnchoredImageIds.Contains(block.Id))
                {
                    continue;
                }

                LayoutImageBlock(state, block, imageContent);
                state.RecordBlockDebug(block, pageIndexBefore, currentYBefore);
                continue;
            }

            var inlines = GetTextInlines(block.Content);
            if (inlines is not null)
            {
                LayoutPreAnchoredImagesForBlock(state, block.Id, preAnchoredImagesByBlockId, preLaidImageIds);
                LayoutTextBlock(state, block, inlines);
                state.RecordBlockDebug(block, pageIndexBefore, currentYBefore);
                continue;
            }

            LayoutFallbackBlock(state, block);
            state.RecordBlockDebug(block, pageIndexBefore, currentYBefore);
        }

        ValidateLayoutInvariants(state.Snapshot);
        stopwatch.Stop();
        ApplyPerformanceMetrics(state.Snapshot, stopwatch.Elapsed.TotalMilliseconds, invalidation);
        return state.Snapshot;
    }

    private static Dictionary<string, List<DocumentBlock>> BuildPreAnchoredImageMap(IReadOnlyList<DocumentBlock> blocks)
    {
        var map = new Dictionary<string, List<DocumentBlock>>(StringComparer.Ordinal);
        string? previousTextBlockId = null;

        foreach (var block in blocks)
        {
            if (block.Type == DocumentBlockType.PageBreak)
            {
                previousTextBlockId = null;
                continue;
            }

            if (block.Content is ImageBlockContent image)
            {
                var layout = image.Layout ?? DocumentObjectLayout.Inline();
                if (layout.IsInline || layout.Anchor.FixedOnPage || !LayoutCreatesTextExclusion(layout))
                {
                    continue;
                }

                var anchorBlockId = !string.IsNullOrWhiteSpace(layout.Anchor.BlockId)
                    ? layout.Anchor.BlockId
                    : previousTextBlockId;
                if (string.IsNullOrWhiteSpace(anchorBlockId) || string.Equals(anchorBlockId, block.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!map.TryGetValue(anchorBlockId, out var anchoredBlocks))
                {
                    anchoredBlocks = [];
                    map[anchorBlockId] = anchoredBlocks;
                }

                anchoredBlocks.Add(block);
                continue;
            }

            if (GetTextInlines(block.Content) is not null)
            {
                previousTextBlockId = block.Id;
            }
        }

        return map;
    }

    private static void LayoutPreAnchoredImagesForBlock(
        LayoutState state,
        string blockId,
        IReadOnlyDictionary<string, List<DocumentBlock>> preAnchoredImagesByBlockId,
        HashSet<string> preLaidImageIds)
    {
        if (!preAnchoredImagesByBlockId.TryGetValue(blockId, out var images))
        {
            return;
        }

        foreach (var imageBlock in images)
        {
            if (preLaidImageIds.Contains(imageBlock.Id) || imageBlock.Content is not ImageBlockContent image)
            {
                continue;
            }

            var pageIndexBefore = state.CurrentPage.PageIndex;
            var currentYBefore = state.CurrentY;
            LayoutImageBlock(state, imageBlock, image, blockId);
            state.RecordBlockDebug(imageBlock, pageIndexBefore, currentYBefore);
            preLaidImageIds.Add(imageBlock.Id);
        }
    }

    private static bool LayoutCreatesTextExclusion(DocumentObjectLayout layout)
        => !layout.IsInline
            && layout.Wrap.Mode is not DocumentWrapMode.BehindText
            && layout.Wrap.Mode is not DocumentWrapMode.InFrontOfText;

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

    private static void LayoutImageBlock(LayoutState state, DocumentBlock block, ImageBlockContent image, string? anchorBlockIdOverride = null)
    {
        var layout = CloneObjectLayout(image.Layout ?? DocumentObjectLayout.Inline());
        if (!string.IsNullOrWhiteSpace(anchorBlockIdOverride))
        {
            layout.Anchor.BlockId = anchorBlockIdOverride;
        }

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
        ApplyImageFootprint(objectBox, image, state);

        if (!RectsApproximatelyEqual(unclamped, objectBox.ObjectRect))
        {
            state.Snapshot.Diagnostics.Add($"Object '{block.Id}' was clamped into page body on page {page.PageNumber}.");
        }

        ResolveObjectOverlap(page, objectBox, body);
        page.Objects.Add(objectBox);
        state.RecordObjectDebug(objectBox);
        var zone = DocumentLayoutGeometryHelper.CreateExclusionZone(objectBox, body);
        if (zone is not null)
        {
            page.Exclusions.Add(zone);
        }
    }

    private static DocumentObjectLayout CloneObjectLayout(DocumentObjectLayout layout)
        => new()
        {
            Kind = layout.Kind,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = layout.Anchor.BlockId,
                InlineIndex = layout.Anchor.InlineIndex,
                Offset = layout.Anchor.Offset,
                Region = layout.Anchor.Region,
                PageIndex = layout.Anchor.PageIndex,
                MoveWithText = layout.Anchor.MoveWithText,
                FixedOnPage = layout.Anchor.FixedOnPage,
                LockAnchor = layout.Anchor.LockAnchor
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = layout.Position.HorizontalRelativeTo,
                VerticalRelativeTo = layout.Position.VerticalRelativeTo,
                X = layout.Position.X,
                Y = layout.Position.Y,
                HorizontalAlignment = layout.Position.HorizontalAlignment,
                VerticalAlignment = layout.Position.VerticalAlignment
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = layout.Wrap.Mode,
                DistanceLeft = layout.Wrap.DistanceLeft,
                DistanceRight = layout.Wrap.DistanceRight,
                DistanceTop = layout.Wrap.DistanceTop,
                DistanceBottom = layout.Wrap.DistanceBottom,
                WrapContourPoints = layout.Wrap.WrapContourPoints
                    .Select(point => new DocumentObjectWrapPoint { X = point.X, Y = point.Y })
                    .ToList()
            },
            Transform = new DocumentObjectTransform
            {
                Width = layout.Transform.Width,
                Height = layout.Transform.Height,
                NaturalWidth = layout.Transform.NaturalWidth,
                NaturalHeight = layout.Transform.NaturalHeight,
                LockAspectRatio = layout.Transform.LockAspectRatio,
                Rotation = layout.Transform.Rotation,
                Crop = new DocumentObjectCrop
                {
                    Left = layout.Transform.Crop.Left,
                    Top = layout.Transform.Crop.Top,
                    Right = layout.Transform.Crop.Right,
                    Bottom = layout.Transform.Crop.Bottom
                }
            },
            Stacking = new DocumentObjectStacking
            {
                ZIndex = layout.Stacking.ZIndex,
                AllowOverlap = layout.Stacking.AllowOverlap
            }
        };

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
            var collisionRect = GetObjectCollisionRect(objectBox);
            var overlap = page.Objects
                .Where(existing => !existing.AllowOverlap && GetObjectLayer(existing.Layout) == layer)
                .Where(existing => DocumentLayoutGeometryHelper.Intersects(GetObjectCollisionRect(existing), collisionRect))
                .OrderBy(existing => GetObjectCollisionRect(existing).Bottom)
                .LastOrDefault();
            if (overlap is null)
            {
                break;
            }

            var overlapRect = GetObjectCollisionRect(overlap);
            var nextY = overlapRect.Bottom + ObjectOverlapGap;
            if (nextY + collisionRect.Height > body.Bottom)
            {
                nextY = Math.Max(body.Y, body.Bottom - collisionRect.Height);
                if (nextY <= collisionRect.Y + 0.01)
                {
                    break;
                }
            }

            MoveObjectBoxToY(objectBox, nextY);
        }
    }

    private static DocumentLayoutRect GetObjectCollisionRect(DocumentObjectLayoutBox objectBox)
        => DocumentLayoutGeometryHelper.GetObjectFootprintRect(objectBox);

    private static void MoveObjectBoxToY(DocumentObjectLayoutBox objectBox, double y)
    {
        var delta = y - objectBox.ObjectRect.Y;
        if (Math.Abs(delta) < 0.01)
        {
            return;
        }

        ShiftY(objectBox.ObjectRect, delta);
        ShiftY(objectBox.MediaRect, delta);
        ShiftY(objectBox.CaptionRect, delta);
        ShiftY(objectBox.FootprintRect, delta);
        objectBox.WrapRect = DocumentLayoutGeometryHelper.ComputeWrapRect(GetObjectCollisionRect(objectBox), objectBox.Layout.Wrap);
    }

    private static void ShiftY(DocumentLayoutRect rect, double delta)
    {
        if (!rect.IsEmpty)
        {
            rect.Y += delta;
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
        var captionHeight = EstimateImageCaptionHeight(image, width, state);
        var footprintHeight = height + (captionHeight > 0 ? ImageCaptionGap + captionHeight : 0);
        var lineHeight = Math.Max(footprintHeight, state.DefaultLineHeight);
        var interval = EnsureObjectFitsAvailableInterval(state, lineHeight, width);
        var page = state.CurrentPage;
        var body = page.BodyRect;
        var availableLeft = interval?.X ?? body.X;
        var availableWidth = Math.Max(width, interval?.Width ?? body.Width);
        var x = image.Alignment switch
        {
            DocumentImageAlignment.Start => availableLeft,
            DocumentImageAlignment.End => availableLeft + availableWidth - width,
            _ => availableLeft + ((availableWidth - width) / 2)
        };
        x = Math.Clamp(x, availableLeft, Math.Max(availableLeft, availableLeft + availableWidth - width));

        var paragraph = new DocumentParagraphLayoutBox
        {
            BlockId = block.Id,
            PageIndex = page.PageIndex,
            Rect = new DocumentLayoutRect
            {
                X = interval?.X ?? body.X,
                Y = state.CurrentY,
                Width = interval?.Width ?? body.Width,
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
                    X = interval?.X ?? body.X,
                    Width = interval?.Width ?? body.Width
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
        var objectBox = new DocumentObjectLayoutBox
        {
            Id = block.Id,
            BlockId = block.Id,
            AnchorBlockId = block.Id,
            PageIndex = page.PageIndex,
            ObjectRect = objectRect,
            Layout = layout,
            ZIndex = layout.Stacking.ZIndex,
            AllowOverlap = layout.Stacking.AllowOverlap
        };
        ApplyImageFootprint(objectBox, image, state);
        page.Objects.Add(objectBox);
        state.RecordObjectDebug(objectBox);

        state.CurrentY += lineHeight + GetParagraphSpacingAfter(block, state.Document.Theme);
    }

    private static DocumentLayoutInterval? EnsureObjectFitsAvailableInterval(
        LayoutState state,
        double objectHeight,
        double objectWidth)
    {
        var guard = 0;
        while (guard++ < 10000)
        {
            state.EnsureLineFits(objectHeight);
            var page = state.CurrentPage;
            var body = page.BodyRect;
            var requiredWidth = Math.Min(objectWidth, body.Width);
            var intervals = DocumentLayoutGeometryHelper.GetAvailableLineIntervals(
                    state.CurrentY,
                    objectHeight,
                    page.Exclusions,
                    body,
                    state.Metrics.MinimumLineIntervalWidth)
                .ToList();
            var interval = intervals.FirstOrDefault(candidate => candidate.Width >= requiredWidth - 0.01);
            if (interval is not null)
            {
                return interval;
            }

            if (!AdvanceBelowBlockingExclusion(state, objectHeight, body))
            {
                state.CurrentY += state.DefaultLineHeight;
            }
        }

        throw new InvalidOperationException("Unable to find an available interval for document object layout.");
    }

    private static bool AdvanceBelowBlockingExclusion(
        LayoutState state,
        double objectHeight,
        DocumentLayoutRect bounds)
    {
        var probe = new DocumentLayoutRect
        {
            X = bounds.X,
            Y = state.CurrentY,
            Width = bounds.Width,
            Height = objectHeight
        };
        var nextY = state.CurrentPage.Exclusions
            .Where(zone => zone.BlocksText && DocumentLayoutGeometryHelper.Intersects(probe, zone.Rect))
            .Select(zone => zone.Rect.Bottom)
            .Where(bottom => bottom > state.CurrentY + 0.01)
            .DefaultIfEmpty(double.NaN)
            .Min();
        if (double.IsNaN(nextY))
        {
            return false;
        }

        state.CurrentY = nextY;
        return true;
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
            for (var offset = 0; offset < run.Text.Length;)
            {
                var ch = run.Text[offset];
                if (ch == '\r')
                {
                    offset++;
                    continue;
                }

                if (ch == '\n')
                {
                    context.EnsureWritableLine();
                    context.FinalizeCurrentLine(force: true);
                    context.AdvanceLine();
                    offset++;
                    continue;
                }

                var tokenStart = offset;
                if (char.IsWhiteSpace(ch))
                {
                    while (offset < run.Text.Length
                        && run.Text[offset] is not '\r' and not '\n'
                        && char.IsWhiteSpace(run.Text[offset]))
                    {
                        ch = run.Text[offset];
                        var width = context.Measure(run, ch.ToString());
                        context.PlaceCharacter(run, offset, ch, width);
                        offset++;
                    }

                    continue;
                }

                while (offset < run.Text.Length
                    && run.Text[offset] is not '\r' and not '\n'
                    && !char.IsWhiteSpace(run.Text[offset]))
                {
                    offset++;
                }

                context.PlaceTextUnit(run, tokenStart, run.Text[tokenStart..offset]);
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
        var interval = EnsureObjectFitsAvailableInterval(state, lineHeight, state.CurrentPage.BodyRect.Width);
        var page = state.CurrentPage;
        var paragraphRect = new DocumentLayoutRect
        {
            X = interval?.X ?? page.BodyRect.X,
            Y = state.CurrentY,
            Width = interval?.Width ?? page.BodyRect.Width,
            Height = lineHeight
        };
        var paragraph = new DocumentParagraphLayoutBox
        {
            BlockId = block.Id,
            PageIndex = page.PageIndex,
            Rect = paragraphRect
        };
        paragraph.Lines.Add(new DocumentLineBox
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
                    X = paragraphRect.X,
                    Width = paragraphRect.Width
                }
            ]
        });
        page.Paragraphs.Add(paragraph);
        state.RecordParagraphRect(block.Id, paragraph.Rect);
        state.RecordLineDebug(paragraph.Lines[0], page.Exclusions
            .Where(zone => zone.BlocksText && DocumentLayoutGeometryHelper.Intersects(paragraphRect, zone.Rect))
            .ToList());
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
        var blockOffset = 0;
        for (var index = 0; index < inlines.Count; index++)
        {
            var inline = inlines[index];
            var text = GetInlineText(inline);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            runs.Add(new TextRunLayoutSource(index, inline.Id, text, baseStyle.Apply(inline.Marks), CloneMarks(inline.Marks), blockOffset));
            blockOffset += text.Length;
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

    private static void ApplyImageFootprint(
        DocumentObjectLayoutBox objectBox,
        ImageBlockContent image,
        LayoutState state)
    {
        objectBox.MediaRect = objectBox.ObjectRect.Clone();
        var visibleMediaRect = ComputeConservativeMediaFootprint(objectBox.ObjectRect, objectBox.Layout.Transform.Rotation);
        var captionHeight = EstimateImageCaptionHeight(image, objectBox.ObjectRect.Width, state);
        if (captionHeight > 0)
        {
            objectBox.CaptionRect = new DocumentLayoutRect
            {
                X = objectBox.ObjectRect.X,
                Y = visibleMediaRect.Bottom + ImageCaptionGap,
                Width = objectBox.ObjectRect.Width,
                Height = captionHeight
            };
            objectBox.FootprintRect = DocumentLayoutGeometryHelper.Union(visibleMediaRect, objectBox.CaptionRect);
        }
        else
        {
            objectBox.CaptionRect = new DocumentLayoutRect();
            objectBox.FootprintRect = visibleMediaRect;
        }

        objectBox.WrapRect = DocumentLayoutGeometryHelper.ComputeWrapRect(objectBox.FootprintRect, objectBox.Layout.Wrap);
    }

    private static DocumentLayoutRect ComputeConservativeMediaFootprint(DocumentLayoutRect mediaRect, double rotationDegrees)
    {
        var normalized = Math.Abs(rotationDegrees) % 360;
        if (normalized < 0.01 || Math.Abs(normalized - 180) < 0.01 || Math.Abs(normalized - 360) < 0.01)
        {
            return mediaRect.Clone();
        }

        var radians = normalized * Math.PI / 180;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));
        var width = (mediaRect.Width * cos) + (mediaRect.Height * sin);
        var height = (mediaRect.Width * sin) + (mediaRect.Height * cos);
        return new DocumentLayoutRect
        {
            X = mediaRect.CenterX - (width / 2),
            Y = mediaRect.CenterY - (height / 2),
            Width = width,
            Height = height
        };
    }

    private static double EstimateImageCaptionHeight(
        ImageBlockContent image,
        double availableWidth,
        LayoutState state)
    {
        var caption = image.Caption;
        if (string.IsNullOrWhiteSpace(caption))
        {
            return 0;
        }

        var baseStyle = TextStyle.FromDocument(state.Document.Theme, state.Metrics);
        var captionStyle = baseStyle with
        {
            FontSize = Math.Max(8, baseStyle.FontSize * 0.9)
        };
        var lineHeight = Math.Max(
            state.DefaultLineHeight,
            captionStyle.FontSize
                * (state.Document.Theme.BodyLineHeight > 0
                    ? state.Document.Theme.BodyLineHeight
                    : state.Metrics.DefaultLineHeightMultiplier)
                * Math.Max(0.1, state.Metrics.Zoom));
        var width = Math.Max(1, availableWidth);
        var lines = caption
            .Split('\n')
            .Select(line => string.IsNullOrWhiteSpace(line)
                ? 1
                : Math.Max(1, (int)Math.Ceiling(state.TextMeasurer.Measure(new DocumentTextMeasurementRequest
                {
                    Text = line,
                    FontFamily = captionStyle.FontFamily,
                    FontSize = captionStyle.FontSize,
                    FontWeight = captionStyle.FontWeight,
                    FontStyle = captionStyle.FontStyle,
                    LetterSpacing = captionStyle.LetterSpacing,
                    Zoom = state.Metrics.Zoom
                }).Width / width)))
            .Sum();
        return lines * lineHeight;
    }

    private static bool RectsApproximatelyEqual(DocumentLayoutRect a, DocumentLayoutRect b)
        => Math.Abs(a.X - b.X) < 0.01
            && Math.Abs(a.Y - b.Y) < 0.01
            && Math.Abs(a.Width - b.Width) < 0.01
            && Math.Abs(a.Height - b.Height) < 0.01;

    private static void ValidateLayoutInvariants(DocumentPageLayoutSnapshot snapshot)
    {
        foreach (var page in snapshot.Pages)
        {
            var blockingZones = page.Exclusions.Where(zone => zone.BlocksText).ToList();
            foreach (var paragraph in page.Paragraphs)
            {
                foreach (var line in paragraph.Lines)
                {
                    foreach (var segment in line.Segments)
                    {
                        foreach (var zone in blockingZones.Where(zone => zone.BlockId != segment.BlockId))
                        {
                            if (DocumentLayoutGeometryHelper.Intersects(segment.Rect, zone.Rect))
                            {
                                snapshot.Diagnostics.Add(
                                    $"Paragraph line '{line.Id}' in block '{line.BlockId}' intersects exclusion from object '{zone.BlockId}' on page {page.PageNumber}.");
                            }
                        }
                    }
                }
            }

            foreach (var objectBox in page.Objects.Where(box => box.Layout.IsInline))
            {
                foreach (var zone in blockingZones.Where(zone => zone.BlockId != objectBox.BlockId))
                {
                    if (DocumentLayoutGeometryHelper.Intersects(objectBox.FootprintRect, zone.Rect))
                    {
                        snapshot.Diagnostics.Add(
                            $"Inline object '{objectBox.BlockId}' intersects active exclusion from object '{zone.BlockId}' on page {page.PageNumber}.");
                    }
                }
            }
        }
    }

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

        public void RecordBlockDebug(DocumentBlock block, int pageIndexBefore, double currentYBefore)
        {
            Snapshot.DebugBlockLayouts.Add(new DocumentBlockLayoutDebugInfo
            {
                BlockId = block.Id,
                BlockType = block.Type,
                Order = block.Order,
                PageIndex = pageIndexBefore,
                CurrentYBefore = currentYBefore,
                CurrentYAfter = CurrentY,
                StartY = currentYBefore,
                EndY = CurrentY
            });
        }

        public void RecordObjectDebug(DocumentObjectLayoutBox objectBox)
        {
            Snapshot.DebugObjectLayouts.Add(new DocumentObjectLayoutDebugInfo
            {
                ObjectId = objectBox.Id,
                BlockId = objectBox.BlockId,
                AnchorBlockId = objectBox.AnchorBlockId,
                PageIndex = objectBox.PageIndex,
                MediaRect = objectBox.MediaRect.Clone(),
                CaptionRect = objectBox.CaptionRect.Clone(),
                FootprintRect = objectBox.FootprintRect.Clone(),
                WrapRect = objectBox.WrapRect.Clone(),
                WrapMode = objectBox.Layout.Wrap.Mode,
                AllowOverlap = objectBox.AllowOverlap,
                ZIndex = objectBox.ZIndex
            });
        }

        public void RecordLineDebug(DocumentLineBox line, IReadOnlyList<DocumentExclusionZone> activeExclusions)
        {
            Snapshot.DebugLineLayouts.Add(new DocumentLineLayoutDebugInfo
            {
                LineId = line.Id,
                BlockId = line.BlockId,
                PageIndex = line.PageIndex,
                LineIndex = line.LineIndex,
                LineRect = line.Rect.Clone(),
                AvailableIntervals = line.AvailableIntervals.Select(interval => new DocumentLayoutInterval
                {
                    X = interval.X,
                    Width = interval.Width
                }).ToList(),
                ExclusionRects = activeExclusions.Select(zone => zone.Rect.Clone()).ToList(),
                Segments = line.Segments.Select(segment => new DocumentTextSegmentBox
                {
                    Id = segment.Id,
                    BlockId = segment.BlockId,
                    InlineId = segment.InlineId,
                    InlineIndex = segment.InlineIndex,
                    StartOffset = segment.StartOffset,
                    BlockStartOffset = segment.BlockStartOffset,
                    Length = segment.Length,
                    Text = segment.Text,
                    Marks = CloneMarks(segment.Marks),
                    Rect = segment.Rect.Clone()
                }).ToList()
            });
        }

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
        private List<DocumentExclusionZone> _activeExclusions = [];
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

        public void PlaceTextUnit(TextRunLayoutSource run, int sourceOffset, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            EnsureWritableLine();
            var width = Measure(run, text);
            if (TryPlaceUnbreakableText(run, sourceOffset, text, width))
            {
                return;
            }

            for (var index = 0; index < text.Length; index++)
            {
                var ch = text[index];
                PlaceCharacter(run, sourceOffset + index, ch, Measure(run, ch.ToString()));
            }
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
                _activeExclusions = page.Exclusions
                    .Where(zone => zone.BlocksText && DocumentLayoutGeometryHelper.Intersects(bounds, zone.Rect))
                    .ToList();

                if (_intervals.Count > 0)
                {
                    EnsureParagraphAndLine(page, bounds);
                    return;
                }

                _state.CurrentY += _lineHeight;
                _line = null;
                _paragraph = null;
                _activeExclusions = [];
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
            _state.RecordLineDebug(_line, _activeExclusions);
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
                var firstInterval = _intervals[0];
                _line = new DocumentLineBox
                {
                    BlockId = _block.Id,
                    PageIndex = page.PageIndex,
                    LineIndex = _lineIndex,
                    Rect = new DocumentLayoutRect
                    {
                        X = firstInterval.X,
                        Y = _state.CurrentY,
                        Width = firstInterval.Width,
                        Height = _lineHeight
                    },
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
                    if (_line is not null)
                    {
                        _line.Rect = DocumentLayoutGeometryHelper.Union(
                            _line.Rect,
                            new DocumentLayoutRect
                            {
                                X = _intervals[index].X,
                                Y = _state.CurrentY,
                                Width = _intervals[index].Width,
                                Height = _lineHeight
                            });
                    }

                    return true;
                }
            }

            return false;
        }

        private bool TryPlaceUnbreakableText(TextRunLayoutSource run, int sourceOffset, string text, double width)
        {
            if (FitsCurrentInterval(width))
            {
                AddText(run, sourceOffset, text);
                return true;
            }

            if (MoveToNextInterval(width))
            {
                AddText(run, sourceOffset, text);
                return true;
            }

            if (_segments.Count > 0)
            {
                FinalizeCurrentLine(force: false);
                AdvanceLine();
                EnsureWritableLine();

                if (FitsCurrentInterval(width))
                {
                    AddText(run, sourceOffset, text);
                    return true;
                }

                if (MoveToNextInterval(width))
                {
                    AddText(run, sourceOffset, text);
                    return true;
                }
            }

            return false;
        }

        private void AddText(TextRunLayoutSource run, int sourceOffset, string text)
        {
            for (var index = 0; index < text.Length; index++)
            {
                var ch = text[index];
                AddCharacter(run, sourceOffset + index, ch, Measure(run, ch.ToString()));
            }
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
                    BlockStartOffset = run.BlockStartOffset + sourceOffset,
                    Marks = CloneMarks(run.Marks),
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

    private static List<InlineMark> CloneMarks(IEnumerable<InlineMark>? marks)
        => marks is null
            ? []
            : marks.Select(mark => new InlineMark
            {
                Type = mark.Type,
                Link = mark.Link is null
                    ? null
                    : new LinkMarkData
                    {
                        Href = mark.Link.Href,
                        Title = mark.Link.Title
                    },
                CommentAnchor = mark.CommentAnchor is null
                    ? null
                    : new CommentAnchorMarkData
                    {
                        CommentId = mark.CommentAnchor.CommentId,
                        AnchorId = mark.CommentAnchor.AnchorId
                    },
                RevisionId = mark.RevisionId,
                Value = mark.Value
            }).ToList();

    private sealed record TextRunLayoutSource(
        int InlineIndex,
        string? InlineId,
        string Text,
        TextStyle Style,
        IReadOnlyList<InlineMark> Marks,
        int BlockStartOffset);

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
