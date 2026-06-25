namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Structured anchor that ties a comment thread to a region of an entity.</summary>
public sealed class TmCommentAnchor
{
    /// <summary>Anchor kind.</summary>
    public TmCommentAnchorKind Kind { get; set; } = TmCommentAnchorKind.None;

    /// <summary>Logical block identifier for block or text range anchors.</summary>
    public string? BlockId { get; set; }

    /// <summary>Start inline index for text range anchors.</summary>
    public int? StartInlineIndex { get; set; }

    /// <summary>Start character offset for text range anchors.</summary>
    public int? StartOffset { get; set; }

    /// <summary>End inline index for text range anchors.</summary>
    public int? EndInlineIndex { get; set; }

    /// <summary>End character offset for text range anchors.</summary>
    public int? EndOffset { get; set; }

    /// <summary>One-based page number for page, point, and area anchors.</summary>
    public int? PageNumber { get; set; }

    /// <summary>Normalized horizontal position on a page, from 0 to 1.</summary>
    public double? X { get; set; }

    /// <summary>Normalized vertical position on a page, from 0 to 1.</summary>
    public double? Y { get; set; }

    /// <summary>Normalized area width on a page, from 0 to 1.</summary>
    public double? Width { get; set; }

    /// <summary>Normalized area height on a page, from 0 to 1.</summary>
    public double? Height { get; set; }

    /// <summary>Text captured when the comment was anchored to a range.</summary>
    public string? HighlightedText { get; set; }

    /// <summary>External anchor id from an imported or host-owned format.</summary>
    public string? ExternalAnchorId { get; set; }

    /// <summary>Anchor id in an immutable rendition.</summary>
    public string? RenditionAnchorId { get; set; }

    /// <summary>True when the original anchor can no longer be resolved in the live entity.</summary>
    public bool IsOrphaned { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Creates an empty anchor.</summary>
    public static TmCommentAnchor None()
        => new();

    /// <summary>Creates a block anchor.</summary>
    /// <param name="blockId">Target block identifier.</param>
    public static TmCommentAnchor Block(string blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId))
            throw new ArgumentException("Block id is required.", nameof(blockId));

        return new TmCommentAnchor
        {
            Kind = TmCommentAnchorKind.Block,
            BlockId = blockId.Trim()
        };
    }

    /// <summary>Creates a text range anchor.</summary>
    /// <param name="blockId">Target block identifier.</param>
    /// <param name="startOffset">Start character offset.</param>
    /// <param name="endOffset">End character offset.</param>
    /// <param name="highlightedText">Text captured at creation time.</param>
    public static TmCommentAnchor TextRange(
        string blockId,
        int startOffset,
        int endOffset,
        string? highlightedText = null)
    {
        if (string.IsNullOrWhiteSpace(blockId))
            throw new ArgumentException("Block id is required.", nameof(blockId));
        if (startOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(startOffset), "Start offset cannot be negative.");
        if (endOffset < startOffset)
            throw new ArgumentOutOfRangeException(nameof(endOffset), "End offset cannot be before start offset.");

        return new TmCommentAnchor
        {
            Kind = TmCommentAnchorKind.TextRange,
            BlockId = blockId.Trim(),
            StartOffset = startOffset,
            EndOffset = endOffset,
            HighlightedText = string.IsNullOrWhiteSpace(highlightedText) ? null : highlightedText
        };
    }

    /// <summary>Creates a page anchor.</summary>
    /// <param name="pageNumber">One-based page number.</param>
    public static TmCommentAnchor Page(int pageNumber)
        => new()
        {
            Kind = TmCommentAnchorKind.Page,
            PageNumber = NormalizePageNumber(pageNumber)
        };

    /// <summary>Creates a page point anchor.</summary>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="x">Normalized horizontal position.</param>
    /// <param name="y">Normalized vertical position.</param>
    public static TmCommentAnchor PagePoint(int pageNumber, double x, double y)
        => new()
        {
            Kind = TmCommentAnchorKind.PagePoint,
            PageNumber = NormalizePageNumber(pageNumber),
            X = Clamp01(x),
            Y = Clamp01(y)
        };

    /// <summary>Creates a page area anchor.</summary>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="x">Normalized horizontal position.</param>
    /// <param name="y">Normalized vertical position.</param>
    /// <param name="width">Normalized width.</param>
    /// <param name="height">Normalized height.</param>
    public static TmCommentAnchor PageArea(int pageNumber, double x, double y, double width, double height)
    {
        var normalizedX = Clamp01(x);
        var normalizedY = Clamp01(y);

        return new TmCommentAnchor
        {
            Kind = TmCommentAnchorKind.PageArea,
            PageNumber = NormalizePageNumber(pageNumber),
            X = normalizedX,
            Y = normalizedY,
            Width = Math.Min(Clamp01(width), 1 - normalizedX),
            Height = Math.Min(Clamp01(height), 1 - normalizedY)
        };
    }

    /// <summary>Creates a rendition anchor.</summary>
    /// <param name="renditionAnchorId">Target rendition anchor identifier.</param>
    public static TmCommentAnchor Rendition(string renditionAnchorId)
    {
        if (string.IsNullOrWhiteSpace(renditionAnchorId))
            throw new ArgumentException("Rendition anchor id is required.", nameof(renditionAnchorId));

        return new TmCommentAnchor
        {
            Kind = TmCommentAnchorKind.Rendition,
            RenditionAnchorId = renditionAnchorId.Trim()
        };
    }

    /// <summary>Creates an external anchor.</summary>
    /// <param name="externalAnchorId">Target external anchor identifier.</param>
    public static TmCommentAnchor External(string externalAnchorId)
    {
        if (string.IsNullOrWhiteSpace(externalAnchorId))
            throw new ArgumentException("External anchor id is required.", nameof(externalAnchorId));

        return new TmCommentAnchor
        {
            Kind = TmCommentAnchorKind.External,
            ExternalAnchorId = externalAnchorId.Trim()
        };
    }

    /// <summary>Returns true when the anchor has enough data for its kind.</summary>
    public bool IsValid()
        => Kind switch
        {
            TmCommentAnchorKind.None => true,
            TmCommentAnchorKind.Block => !string.IsNullOrWhiteSpace(BlockId),
            TmCommentAnchorKind.TextRange => !string.IsNullOrWhiteSpace(BlockId)
                && StartOffset >= 0
                && EndOffset >= StartOffset,
            TmCommentAnchorKind.Page => PageNumber >= 1,
            TmCommentAnchorKind.PagePoint => PageNumber >= 1 && IsNormalized(X) && IsNormalized(Y),
            TmCommentAnchorKind.PageArea => PageNumber >= 1
                && IsNormalized(X)
                && IsNormalized(Y)
                && Width > 0
                && Height > 0
                && IsNormalized(Width)
                && IsNormalized(Height),
            TmCommentAnchorKind.Rendition => !string.IsNullOrWhiteSpace(RenditionAnchorId),
            TmCommentAnchorKind.External => !string.IsNullOrWhiteSpace(ExternalAnchorId),
            _ => false
        };

    private static int NormalizePageNumber(int pageNumber)
        => pageNumber < 1 ? 1 : pageNumber;

    private static double Clamp01(double value)
        => double.IsFinite(value) ? Math.Min(Math.Max(value, 0), 1) : 0;

    private static bool IsNormalized(double? value)
        => value.HasValue && double.IsFinite(value.Value) && value.Value >= 0 && value.Value <= 1;
}
