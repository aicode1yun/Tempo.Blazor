using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Normalized anchor kind for a document comment thread.</summary>
public enum DocumentCommentAnchorKind
{
    /// <summary>A point on a document page.</summary>
    Point,

    /// <summary>A rectangular area on a document page.</summary>
    Area,

    /// <summary>The whole document page.</summary>
    Page,

    /// <summary>A selected run of text on a document page, described by one or more highlight rectangles.</summary>
    TextRange
}

/// <summary>Normalized rectangle on a document page, expressed as fractions of the page size.</summary>
public sealed class DocumentCommentRect
{
    /// <summary>Normalized horizontal position in the page, from 0 to 1.</summary>
    public double X { get; set; }

    /// <summary>Normalized vertical position in the page, from 0 to 1.</summary>
    public double Y { get; set; }

    /// <summary>Normalized width in the page, from 0 to 1.</summary>
    public double Width { get; set; }

    /// <summary>Normalized height in the page, from 0 to 1.</summary>
    public double Height { get; set; }

    /// <summary>Creates a normalized rectangle, clamping every component into the page bounds.</summary>
    /// <param name="x">Normalized horizontal position.</param>
    /// <param name="y">Normalized vertical position.</param>
    /// <param name="width">Normalized width.</param>
    /// <param name="height">Normalized height.</param>
    public static DocumentCommentRect Create(double x, double y, double width, double height)
    {
        var normalizedX = Clamp01(x);
        var normalizedY = Clamp01(y);
        return new DocumentCommentRect
        {
            X = normalizedX,
            Y = normalizedY,
            Width = Math.Min(Clamp01(width), 1 - normalizedX),
            Height = Math.Min(Clamp01(height), 1 - normalizedY)
        };
    }

    /// <summary>Returns true when every component is a finite fraction inside the page bounds and the size is positive.</summary>
    public bool IsValid
        => IsNormalized(X) && IsNormalized(Y) && Width > 0 && Height > 0
           && IsNormalized(Width) && IsNormalized(Height);

    private static double Clamp01(double value)
        => double.IsFinite(value) ? Math.Min(Math.Max(value, 0), 1) : 0;

    private static bool IsNormalized(double value)
        => double.IsFinite(value) && value is >= 0 and <= 1;
}

/// <summary>Status of a document comment thread.</summary>
public enum DocumentCommentThreadStatus
{
    /// <summary>The thread still needs attention.</summary>
    Open,

    /// <summary>The thread has been resolved.</summary>
    Resolved
}

/// <summary>Document comment display mode.</summary>
public enum DocumentCommentMode
{
    /// <summary>Comments are visible but new anchors are not created by page clicks.</summary>
    Browse,

    /// <summary>Page clicks or drags create a new comment draft.</summary>
    Comment
}

/// <summary>Filter applied to the document comment thread panel.</summary>
public enum DocumentCommentThreadFilter
{
    /// <summary>Show open comment threads.</summary>
    Open,

    /// <summary>Show resolved comment threads.</summary>
    Resolved,

    /// <summary>Show comment threads mentioning the current user.</summary>
    Mentions
}

/// <summary>Normalized document page anchor for a comment thread.</summary>
public sealed class DocumentCommentAnchor
{
    /// <summary>One-based page number where the comment is anchored.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>Anchor kind.</summary>
    public DocumentCommentAnchorKind Kind { get; set; } = DocumentCommentAnchorKind.Point;

    /// <summary>Normalized horizontal position in the page, from 0 to 1.</summary>
    public double X { get; set; }

    /// <summary>Normalized vertical position in the page, from 0 to 1.</summary>
    public double Y { get; set; }

    /// <summary>Normalized width for area anchors, from 0 to 1.</summary>
    public double Width { get; set; }

    /// <summary>Normalized height for area anchors, from 0 to 1.</summary>
    public double Height { get; set; }

    /// <summary>Highlight rectangles for text range anchors. Empty for other anchor kinds.</summary>
    public List<DocumentCommentRect> Rects { get; set; } = [];

    /// <summary>Text captured when the anchor was created from a text selection, when available.</summary>
    public string? HighlightedText { get; set; }

    /// <summary>Creates a point anchor.</summary>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="x">Normalized horizontal position.</param>
    /// <param name="y">Normalized vertical position.</param>
    public static DocumentCommentAnchor Point(int pageNumber, double x, double y)
    {
        return new DocumentCommentAnchor
        {
            PageNumber = pageNumber,
            Kind = DocumentCommentAnchorKind.Point,
            X = Clamp01(x),
            Y = Clamp01(y)
        };
    }

    /// <summary>Creates an area anchor.</summary>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="x">Normalized horizontal position.</param>
    /// <param name="y">Normalized vertical position.</param>
    /// <param name="width">Normalized width.</param>
    /// <param name="height">Normalized height.</param>
    public static DocumentCommentAnchor Area(int pageNumber, double x, double y, double width, double height)
    {
        var normalizedX = Clamp01(x);
        var normalizedY = Clamp01(y);
        return new DocumentCommentAnchor
        {
            PageNumber = pageNumber,
            Kind = DocumentCommentAnchorKind.Area,
            X = normalizedX,
            Y = normalizedY,
            Width = Math.Min(Clamp01(width), 1 - normalizedX),
            Height = Math.Min(Clamp01(height), 1 - normalizedY)
        };
    }

    /// <summary>Creates a page-level anchor.</summary>
    /// <param name="pageNumber">One-based page number.</param>
    public static DocumentCommentAnchor Page(int pageNumber)
    {
        return new DocumentCommentAnchor
        {
            PageNumber = pageNumber,
            Kind = DocumentCommentAnchorKind.Page
        };
    }

    /// <summary>Creates a text range anchor from one or more normalized highlight rectangles.</summary>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="rects">Highlight rectangles covering the selected text.</param>
    /// <param name="highlightedText">Text captured at creation time, when available.</param>
    public static DocumentCommentAnchor TextRange(
        int pageNumber,
        IEnumerable<DocumentCommentRect> rects,
        string? highlightedText = null)
    {
        ArgumentNullException.ThrowIfNull(rects);

        var normalized = rects
            .Select(rect => DocumentCommentRect.Create(rect.X, rect.Y, rect.Width, rect.Height))
            .Where(rect => rect.IsValid)
            .ToList();

        // The point/area coordinates track the union bounding box so existing marker
        // positioning keeps working for text range anchors.
        double minX = 0, minY = 0, width = 0, height = 0;
        if (normalized.Count > 0)
        {
            minX = normalized.Min(rect => rect.X);
            minY = normalized.Min(rect => rect.Y);
            var maxX = normalized.Max(rect => rect.X + rect.Width);
            var maxY = normalized.Max(rect => rect.Y + rect.Height);
            width = Math.Min(maxX - minX, 1 - minX);
            height = Math.Min(maxY - minY, 1 - minY);
        }

        return new DocumentCommentAnchor
        {
            PageNumber = pageNumber < 1 ? 1 : pageNumber,
            Kind = DocumentCommentAnchorKind.TextRange,
            X = minX,
            Y = minY,
            Width = width,
            Height = height,
            Rects = normalized,
            HighlightedText = string.IsNullOrWhiteSpace(highlightedText) ? null : highlightedText
        };
    }

    private static double Clamp01(double value)
    {
        return double.IsFinite(value) ? Math.Min(Math.Max(value, 0), 1) : 0;
    }
}

/// <summary>User available for document comment mentions.</summary>
public sealed class DocumentCommentUser
{
    /// <summary>Stable user identifier.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Displayed user name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional email address.</summary>
    public string? Email { get; set; }

    /// <summary>Optional avatar image URL.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Optional role (e.g. reviewer, approver) used for role-based annotation colors.</summary>
    public string? Role { get; set; }
}

/// <summary>Mention metadata stored with a document comment.</summary>
public sealed class DocumentCommentMention
{
    /// <summary>Stable user identifier.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Displayed mention text.</summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>Reaction metadata stored with a document comment.</summary>
public sealed class DocumentCommentReaction
{
    /// <summary>Emoji or compact reaction value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>User ids that applied this reaction.</summary>
    public List<string> UserIds { get; set; } = [];
}

/// <summary>Single comment entry inside a document comment thread.</summary>
public sealed class DocumentComment
{
    /// <summary>Stable comment identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Author user identifier.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>Displayed author name.</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>Optional author avatar image URL.</summary>
    public string? AuthorAvatarUrl { get; set; }

    /// <summary>Plain text comment body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last edit timestamp, when edited.</summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>Users mentioned by this comment.</summary>
    public List<DocumentCommentMention> Mentions { get; set; } = [];

    /// <summary>Reactions applied to this comment.</summary>
    public List<DocumentCommentReaction> Reactions { get; set; } = [];

    /// <summary>Whether the current viewer may edit this comment.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Whether the current viewer may delete this comment.</summary>
    public bool CanDelete { get; set; }
}

/// <summary>Document comment thread anchored to a page location.</summary>
public sealed class DocumentCommentThread
{
    /// <summary>Stable thread identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Thread anchor on the document page.</summary>
    public DocumentCommentAnchor Anchor { get; set; } = DocumentCommentAnchor.Page(1);

    /// <summary>Thread status.</summary>
    public DocumentCommentThreadStatus Status { get; set; } = DocumentCommentThreadStatus.Open;

    /// <summary>Comments in the thread.</summary>
    public List<DocumentComment> Comments { get; set; } = [];

    /// <summary>User id that resolved the thread, when resolved.</summary>
    public string? ResolvedByUserId { get; set; }

    /// <summary>Displayed name of the user that resolved the thread.</summary>
    public string? ResolvedByName { get; set; }

    /// <summary>Timestamp when the thread was resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Annotation kind. Default is <see cref="DocumentAnnotationKind.Comment"/> for existing threads.</summary>
    public DocumentAnnotationKind Kind { get; set; } = DocumentAnnotationKind.Comment;

    /// <summary>Explicit display color (CSS value). When null the color is resolved per author/role.</summary>
    public string? Color { get; set; }

    /// <summary>Stamp text for <see cref="DocumentAnnotationKind.Stamp"/> annotations.</summary>
    public string? StampText { get; set; }

    /// <summary>Freehand strokes for <see cref="DocumentAnnotationKind.Drawing"/> annotations.</summary>
    public List<DocumentInkStroke> InkStrokes { get; set; } = [];
}

/// <summary>Maps document-viewer comment models to the shared comment contract.</summary>
public static class DocumentViewerCommentBridge
{
    /// <summary>Entity type used by document-viewer comments in shared comment providers.</summary>
    public const string EntityType = "document-viewer-document";

    /// <summary>Creates a shared entity reference for a document-viewer document.</summary>
    /// <param name="documentId">Stable document identifier.</param>
    public static TmEntityRef Entity(string documentId)
        => TmEntityRef.Create(EntityType, documentId);

    /// <summary>Converts a document-viewer comment thread to a shared comment thread.</summary>
    /// <param name="thread">Document-viewer comment thread.</param>
    /// <param name="documentId">Stable document identifier.</param>
    public static TmCommentThread ToTmCommentThread(DocumentCommentThread thread, string documentId)
    {
        ArgumentNullException.ThrowIfNull(thread);

        var threadId = string.IsNullOrWhiteSpace(thread.Id) ? Guid.NewGuid().ToString("N") : thread.Id;
        var entries = thread.Comments
            .Select(comment => ToTmCommentEntry(comment, threadId))
            .ToList();

        var anchor = ToTmCommentAnchor(thread.Anchor);
        ApplyAnnotationMetadata(anchor, thread);

        return new TmCommentThread
        {
            Id = threadId,
            EntityRef = Entity(documentId),
            Anchor = anchor,
            Status = ToTmStatus(thread.Status),
            CreatedAt = entries.Count == 0 ? DateTimeOffset.UtcNow : entries.Min(entry => entry.CreatedAt),
            UpdatedAt = GetUpdatedAt(entries, thread.ResolvedAt),
            ResolvedAt = thread.ResolvedAt,
            ResolvedBy = ToNullableTmUserRef(thread.ResolvedByUserId, thread.ResolvedByName),
            Entries = entries
        };
    }

    /// <summary>Converts a shared comment thread to a document-viewer comment thread.</summary>
    /// <param name="thread">Shared comment thread.</param>
    public static DocumentCommentThread ToDocumentCommentThread(TmCommentThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        var result = new DocumentCommentThread
        {
            Id = thread.Id,
            Anchor = ToDocumentCommentAnchor(thread.Anchor),
            Status = ToDocumentStatus(thread.Status),
            Comments = thread.Entries.Select(ToDocumentComment).ToList(),
            ResolvedByUserId = thread.ResolvedBy?.Id,
            ResolvedByName = thread.ResolvedBy?.DisplayName,
            ResolvedAt = thread.ResolvedAt
        };

        ReadAnnotationMetadata(thread.Anchor, result);
        return result;
    }

    /// <summary>Converts a document-viewer comment entry to a shared comment entry.</summary>
    /// <param name="comment">Document-viewer comment entry.</param>
    /// <param name="threadId">Parent thread identifier.</param>
    public static TmCommentEntry ToTmCommentEntry(DocumentComment comment, string threadId)
    {
        ArgumentNullException.ThrowIfNull(comment);

        return new TmCommentEntry
        {
            Id = string.IsNullOrWhiteSpace(comment.Id) ? Guid.NewGuid().ToString("N") : comment.Id,
            ThreadId = threadId,
            Author = ToTmUserRef(comment.AuthorId, comment.AuthorName, comment.AuthorAvatarUrl),
            Body = comment.Body,
            BodyFormat = TmCommentBodyFormat.PlainText,
            CreatedAt = comment.CreatedAt == default ? DateTimeOffset.UtcNow : comment.CreatedAt,
            EditedAt = comment.EditedAt,
            Mentions = comment.Mentions.Select(ToTmMention).ToList(),
            Reactions = comment.Reactions.Select(ToTmReaction).ToList(),
            CanEdit = comment.CanEdit,
            CanDelete = comment.CanDelete
        };
    }

    /// <summary>Converts a shared comment entry to a document-viewer comment entry.</summary>
    /// <param name="entry">Shared comment entry.</param>
    public static DocumentComment ToDocumentComment(TmCommentEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new DocumentComment
        {
            Id = entry.Id,
            AuthorId = entry.Author.Id,
            AuthorName = string.IsNullOrWhiteSpace(entry.Author.DisplayName) ? entry.Author.Id : entry.Author.DisplayName,
            AuthorAvatarUrl = entry.Author.AvatarUrl,
            Body = entry.Body,
            CreatedAt = entry.CreatedAt == default ? DateTimeOffset.UtcNow : entry.CreatedAt,
            EditedAt = entry.EditedAt,
            Mentions = entry.Mentions.Select(ToDocumentMention).ToList(),
            Reactions = entry.Reactions.Select(ToDocumentReaction).ToList(),
            CanEdit = entry.CanEdit,
            CanDelete = entry.CanDelete
        };
    }

    /// <summary>Metadata key marking a shared page-area anchor that originated from a text range selection.</summary>
    private const string TextRangeMetadataKind = "pdfAnchorKind";

    /// <summary>Metadata key holding the encoded highlight rectangles of a text range anchor.</summary>
    private const string TextRangeMetadataRects = "pdfTextRangeRects";

    private const string TextRangeKindValue = "TextRange";

    /// <summary>Metadata key holding the annotation kind of an annotator thread.</summary>
    private const string AnnotationKindMetadata = "pdfAnnotationKind";

    /// <summary>Metadata key holding the explicit annotation color.</summary>
    private const string AnnotationColorMetadata = "pdfAnnotationColor";

    /// <summary>Metadata key holding the stamp text of a stamp annotation.</summary>
    private const string AnnotationStampTextMetadata = "pdfStampText";

    /// <summary>Metadata key holding the encoded ink strokes of a drawing annotation.</summary>
    private const string AnnotationInkStrokesMetadata = "pdfInkStrokes";

    private static void ApplyAnnotationMetadata(TmCommentAnchor anchor, DocumentCommentThread thread)
    {
        var hasAnnotationData = thread.Kind != DocumentAnnotationKind.Comment
            || !string.IsNullOrEmpty(thread.Color)
            || !string.IsNullOrEmpty(thread.StampText)
            || thread.InkStrokes.Count > 0;
        if (!hasAnnotationData)
        {
            return;
        }

        anchor.Metadata ??= new Dictionary<string, object>();
        if (thread.Kind != DocumentAnnotationKind.Comment)
        {
            anchor.Metadata[AnnotationKindMetadata] = thread.Kind.ToString();
        }

        if (!string.IsNullOrEmpty(thread.Color))
        {
            anchor.Metadata[AnnotationColorMetadata] = thread.Color!;
        }

        if (!string.IsNullOrEmpty(thread.StampText))
        {
            anchor.Metadata[AnnotationStampTextMetadata] = thread.StampText!;
        }

        if (thread.InkStrokes.Count > 0)
        {
            anchor.Metadata[AnnotationInkStrokesMetadata] = EncodeInkStrokes(thread.InkStrokes);
        }
    }

    private static void ReadAnnotationMetadata(TmCommentAnchor? anchor, DocumentCommentThread thread)
    {
        if (anchor?.Metadata is null)
        {
            return;
        }

        var kindText = ConvertToString(anchor.Metadata.GetValueOrDefault(AnnotationKindMetadata));
        if (!string.IsNullOrEmpty(kindText) && Enum.TryParse<DocumentAnnotationKind>(kindText, out var kind))
        {
            thread.Kind = kind;
        }

        var color = ConvertToString(anchor.Metadata.GetValueOrDefault(AnnotationColorMetadata));
        if (!string.IsNullOrEmpty(color))
        {
            thread.Color = color;
        }

        var stampText = ConvertToString(anchor.Metadata.GetValueOrDefault(AnnotationStampTextMetadata));
        if (!string.IsNullOrEmpty(stampText))
        {
            thread.StampText = stampText;
        }

        var strokes = DecodeInkStrokes(anchor.Metadata.GetValueOrDefault(AnnotationInkStrokesMetadata));
        if (strokes.Count > 0)
        {
            thread.InkStrokes = strokes;
        }
    }

    /// <summary>Encodes ink strokes into a culture-invariant, JSON-round-trip-safe string.
    /// Format: one stroke per ';'-separated segment as "thickness|x,y x,y …".</summary>
    /// <param name="strokes">Strokes to encode.</param>
    public static string EncodeInkStrokes(IEnumerable<DocumentInkStroke> strokes)
        => string.Join(";", strokes.Select(stroke => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{stroke.Thickness:0.######}|{string.Join(" ", stroke.Points.Select(point => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{point.X:0.######},{point.Y:0.######}")))}")));

    /// <summary>Decodes ink strokes previously produced by <see cref="EncodeInkStrokes"/>.</summary>
    /// <param name="encoded">Encoded stroke string.</param>
    public static List<DocumentInkStroke> DecodeInkStrokes(object? encoded)
    {
        var text = ConvertToString(encoded);
        var result = new List<DocumentInkStroke>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (var segment in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('|');
            if (parts.Length != 2
                || !double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var thickness))
            {
                continue;
            }

            var points = new List<DocumentInkPoint>();
            foreach (var pair in parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var xy = pair.Split(',');
                if (xy.Length == 2
                    && double.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                    && double.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
                {
                    points.Add(DocumentInkPoint.Create(x, y));
                }
            }

            if (points.Count >= 2)
            {
                result.Add(new DocumentInkStroke { Points = points, Thickness = thickness > 0 ? thickness : 0.004 });
            }
        }

        return result;
    }

    private static TmCommentAnchor ToTmCommentAnchor(DocumentCommentAnchor anchor)
    {
        switch (anchor.Kind)
        {
            case DocumentCommentAnchorKind.TextRange:
                var textRange = TmCommentAnchor.PageArea(
                    anchor.PageNumber,
                    anchor.X,
                    anchor.Y,
                    anchor.Width,
                    anchor.Height);
                textRange.HighlightedText = anchor.HighlightedText;
                textRange.Metadata = new Dictionary<string, object>
                {
                    [TextRangeMetadataKind] = TextRangeKindValue,
                    [TextRangeMetadataRects] = EncodeRects(anchor.Rects)
                };
                return textRange;
            case DocumentCommentAnchorKind.Area:
                return TmCommentAnchor.PageArea(
                    anchor.PageNumber,
                    anchor.X,
                    anchor.Y,
                    anchor.Width,
                    anchor.Height);
            case DocumentCommentAnchorKind.Page:
                return TmCommentAnchor.Page(anchor.PageNumber);
            default:
                return TmCommentAnchor.PagePoint(anchor.PageNumber, anchor.X, anchor.Y);
        }
    }

    private static DocumentCommentAnchor ToDocumentCommentAnchor(TmCommentAnchor? anchor)
    {
        switch (anchor?.Kind)
        {
            case TmCommentAnchorKind.PageArea when IsTextRange(anchor):
                var rects = DecodeRects(anchor.Metadata?.GetValueOrDefault(TextRangeMetadataRects));
                return rects.Count > 0
                    ? DocumentCommentAnchor.TextRange(anchor.PageNumber ?? 1, rects, anchor.HighlightedText)
                    : DocumentCommentAnchor.Area(
                        anchor.PageNumber ?? 1,
                        anchor.X ?? 0,
                        anchor.Y ?? 0,
                        anchor.Width ?? 0,
                        anchor.Height ?? 0);
            case TmCommentAnchorKind.PageArea:
                return DocumentCommentAnchor.Area(
                    anchor.PageNumber ?? 1,
                    anchor.X ?? 0,
                    anchor.Y ?? 0,
                    anchor.Width ?? 0,
                    anchor.Height ?? 0);
            case TmCommentAnchorKind.Page:
                return DocumentCommentAnchor.Page(anchor.PageNumber ?? 1);
            case TmCommentAnchorKind.PagePoint:
                return DocumentCommentAnchor.Point(
                    anchor.PageNumber ?? 1,
                    anchor.X ?? 0,
                    anchor.Y ?? 0);
            default:
                return DocumentCommentAnchor.Page(anchor?.PageNumber ?? 1);
        }
    }

    private static bool IsTextRange(TmCommentAnchor anchor)
        => anchor.Metadata is not null
           && anchor.Metadata.TryGetValue(TextRangeMetadataKind, out var kind)
           && string.Equals(ConvertToString(kind), TextRangeKindValue, StringComparison.Ordinal);

    /// <summary>Encodes normalized rectangles into a culture-invariant, JSON-round-trip-safe string.</summary>
    public static string EncodeRects(IEnumerable<DocumentCommentRect> rects)
        => string.Join(";", rects.Select(rect => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{rect.X:0.######},{rect.Y:0.######},{rect.Width:0.######},{rect.Height:0.######}")));

    /// <summary>Decodes rectangles previously produced by <see cref="EncodeRects"/>.</summary>
    public static List<DocumentCommentRect> DecodeRects(object? encoded)
    {
        var text = ConvertToString(encoded);
        var result = new List<DocumentCommentRect>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var components = part.Split(',');
            if (components.Length == 4
                && double.TryParse(components[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                && double.TryParse(components[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
                && double.TryParse(components[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)
                && double.TryParse(components[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h))
            {
                var rect = DocumentCommentRect.Create(x, y, w, h);
                if (rect.IsValid)
                {
                    result.Add(rect);
                }
            }
        }

        return result;
    }

    private static string? ConvertToString(object? value)
        => value switch
        {
            null => null,
            string s => s,
            _ => value.ToString()
        };

    private static TmCommentMention ToTmMention(DocumentCommentMention mention)
    {
        var user = ToTmUserRef(mention.UserId, mention.DisplayName, null);
        return new TmCommentMention
        {
            User = user,
            DisplayText = string.IsNullOrWhiteSpace(mention.DisplayName) ? user.Id : mention.DisplayName
        };
    }

    private static DocumentCommentMention ToDocumentMention(TmCommentMention mention)
    {
        var displayName = string.IsNullOrWhiteSpace(mention.DisplayText)
            ? mention.User.DisplayName
            : mention.DisplayText;

        return new DocumentCommentMention
        {
            UserId = mention.User.Id,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? mention.User.Id : displayName
        };
    }

    private static TmCommentReaction ToTmReaction(DocumentCommentReaction reaction)
        => new()
        {
            Value = reaction.Value,
            UserIds = reaction.UserIds.ToList()
        };

    private static DocumentCommentReaction ToDocumentReaction(TmCommentReaction reaction)
        => new()
        {
            Value = reaction.Value,
            UserIds = reaction.UserIds.ToList()
        };

    private static TmUserRef ToTmUserRef(string? userId, string? displayName, string? avatarUrl)
    {
        var normalizedId = userId ?? string.Empty;
        return new TmUserRef
        {
            Id = normalizedId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedId : displayName,
            AvatarUrl = avatarUrl
        };
    }

    private static TmUserRef? ToNullableTmUserRef(string? userId, string? displayName)
        => string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(displayName)
            ? null
            : ToTmUserRef(userId, displayName, null);

    private static TmCommentThreadStatus ToTmStatus(DocumentCommentThreadStatus status)
        => status == DocumentCommentThreadStatus.Resolved
            ? TmCommentThreadStatus.Resolved
            : TmCommentThreadStatus.Open;

    private static DocumentCommentThreadStatus ToDocumentStatus(TmCommentThreadStatus status)
        => status == TmCommentThreadStatus.Resolved
            ? DocumentCommentThreadStatus.Resolved
            : DocumentCommentThreadStatus.Open;

    private static DateTimeOffset? GetUpdatedAt(IReadOnlyCollection<TmCommentEntry> entries, DateTimeOffset? resolvedAt)
    {
        DateTimeOffset? updatedAt = entries.Count == 0
            ? null
            : entries.Max(entry => entry.EditedAt ?? entry.CreatedAt);

        return resolvedAt.HasValue && (!updatedAt.HasValue || resolvedAt.Value > updatedAt.Value)
            ? resolvedAt
            : updatedAt;
    }
}

/// <summary>Payload emitted when a new document comment thread is requested.</summary>
public sealed class DocumentCommentThreadCreateRequest
{
    /// <summary>Anchor requested for the new thread.</summary>
    public DocumentCommentAnchor Anchor { get; set; } = DocumentCommentAnchor.Page(1);

    /// <summary>Plain text body for the first comment.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Mentions detected in the body.</summary>
    public List<DocumentCommentMention> Mentions { get; set; } = [];

    /// <summary>Annotation kind for the new thread. Default is <see cref="DocumentAnnotationKind.Comment"/>.</summary>
    public DocumentAnnotationKind Kind { get; set; } = DocumentAnnotationKind.Comment;

    /// <summary>Explicit display color (CSS value) for the new thread, when any.</summary>
    public string? Color { get; set; }

    /// <summary>Stamp text for <see cref="DocumentAnnotationKind.Stamp"/> annotations.</summary>
    public string? StampText { get; set; }

    /// <summary>Freehand strokes for <see cref="DocumentAnnotationKind.Drawing"/> annotations.</summary>
    public List<DocumentInkStroke> InkStrokes { get; set; } = [];
}

/// <summary>Payload emitted when a reply is requested.</summary>
public sealed class DocumentCommentReplyRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Plain text reply body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Mentions detected in the body.</summary>
    public List<DocumentCommentMention> Mentions { get; set; } = [];
}

/// <summary>Payload emitted for thread status changes.</summary>
public sealed class DocumentCommentThreadStatusRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;
}

/// <summary>Payload emitted when a comment edit is requested.</summary>
public sealed class DocumentCommentEditRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Target comment identifier.</summary>
    public string CommentId { get; set; } = string.Empty;

    /// <summary>Updated plain text body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Mentions detected in the body.</summary>
    public List<DocumentCommentMention> Mentions { get; set; } = [];
}

/// <summary>Payload emitted when a comment delete is requested.</summary>
public sealed class DocumentCommentDeleteRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Target comment identifier.</summary>
    public string CommentId { get; set; } = string.Empty;
}

/// <summary>Payload emitted when a comment reaction is toggled.</summary>
public sealed class DocumentCommentReactionToggleRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Target comment identifier.</summary>
    public string CommentId { get; set; } = string.Empty;

    /// <summary>Emoji or compact reaction value.</summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>Payload emitted when a comment selection changes.</summary>
public sealed class DocumentCommentSelectionChangeRequest
{
    /// <summary>Selected thread identifier, or null when selection is cleared.</summary>
    public string? ThreadId { get; set; }
}

/// <summary>Helper methods for document comment models.</summary>
public static class DocumentCommentHelper
{
    /// <summary>Returns true when an anchor is usable by the viewer.</summary>
    /// <param name="anchor">Anchor to validate.</param>
    public static bool IsValidAnchor(DocumentCommentAnchor? anchor)
    {
        if (anchor is null || anchor.PageNumber < 1)
        {
            return false;
        }

        if (!IsNormalized(anchor.X) || !IsNormalized(anchor.Y))
        {
            return false;
        }

        if (anchor.Kind == DocumentCommentAnchorKind.TextRange)
        {
            return anchor.Rects.Count > 0 && anchor.Rects.All(rect => rect.IsValid);
        }

        return anchor.Kind != DocumentCommentAnchorKind.Area
            || (anchor.Width > 0 && anchor.Height > 0 && IsNormalized(anchor.Width) && IsNormalized(anchor.Height));
    }

    /// <summary>Counts open threads.</summary>
    /// <param name="threads">Threads to inspect.</param>
    public static int CountOpenThreads(IEnumerable<DocumentCommentThread>? threads)
    {
        return threads?.Count(thread => thread.Status == DocumentCommentThreadStatus.Open) ?? 0;
    }

    /// <summary>Counts threads where any comment mentions a given user.</summary>
    /// <param name="threads">Threads to inspect.</param>
    /// <param name="userId">Stable user identifier.</param>
    public static int CountMentionedThreads(IEnumerable<DocumentCommentThread>? threads, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || threads is null)
        {
            return 0;
        }

        return threads.Count(thread => thread.Comments.Any(comment =>
            comment.Mentions.Any(mention => string.Equals(mention.UserId, userId, StringComparison.Ordinal))));
    }

    /// <summary>Returns true when a thread mentions a given user.</summary>
    /// <param name="thread">Thread to inspect.</param>
    /// <param name="userId">Stable user identifier.</param>
    public static bool MentionsUser(DocumentCommentThread? thread, string? userId)
    {
        return !string.IsNullOrWhiteSpace(userId)
            && thread?.Comments.Any(comment =>
                comment.Mentions.Any(mention => string.Equals(mention.UserId, userId, StringComparison.Ordinal))) == true;
    }

    private static bool IsNormalized(double value)
    {
        return double.IsFinite(value) && value >= 0 && value <= 1;
    }
}
