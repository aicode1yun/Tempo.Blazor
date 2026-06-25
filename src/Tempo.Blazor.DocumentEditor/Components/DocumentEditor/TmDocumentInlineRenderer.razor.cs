using Microsoft.AspNetCore.Components;
using System.Globalization;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Renders a single inline content run from the document editor model.</summary>
public partial class TmDocumentInlineRenderer : ComponentBase
{
    private string? _resolvedDrawingImageUrl;
    private string? _resolvedDrawingAssetId;
    private bool _isResolvingImage;

    /// <summary>Inline content to render.</summary>
    [Parameter] public InlineContent? Inline { get; set; }

    /// <summary>Document id used by provider-backed image resolution.</summary>
    [Parameter] public string DocumentId { get; set; } = string.Empty;

    /// <summary>Optional resolver for provider-managed image assets.</summary>
    [Parameter] public IDocumentImageUrlResolver? ImageUrlResolver { get; set; }

    /// <summary>Raised when a comment anchor inline is selected.</summary>
    [Parameter] public EventCallback<string> OnCommentSelected { get; set; }

    private string? SafeHref => GetSafeHref();

    private string? DrawingImageUrl => GetDrawingImageUrl();

    private string? SafeDrawingLinkUrl => Inline is DocumentDrawingRun drawing && DocumentLinkUtility.IsSafeHref(drawing.LinkUrl)
        ? DocumentLinkUtility.NormalizeHref(drawing.LinkUrl!)
        : null;

    private string? LinkTitle => Inline?.Marks.FirstOrDefault(mark => mark.Type == InlineMarkType.Link)?.Link?.Title;

    private string? TokenKey => Inline is TokenRun token ? token.Key : null;

    private string? TokenType => Inline is TokenRun token ? token.TokenType : null;

    private string? CommentId => Inline?.Marks.FirstOrDefault(mark => mark.Type == InlineMarkType.CommentAnchor)
        ?.CommentAnchor?.CommentId;

    private InlineMark? RevisionMark => Inline?.Marks.FirstOrDefault(mark => mark.Type == InlineMarkType.Revision);

    private string? RevisionId => RevisionMark?.RevisionId;

    private string? InlineTestId => Inline switch
    {
        TokenRun => "document-token-chip",
        DocumentFieldRun => "document-field-chip",
        _ => !string.IsNullOrWhiteSpace(RevisionId)
            ? GetRevisionTestId()
            : string.IsNullOrWhiteSpace(CommentId) ? null : "document-comment-highlight"
    };

    private string? InlineTitle => Inline switch
    {
        TokenRun token => string.IsNullOrWhiteSpace(token.Description) ? token.Key : token.Description,
        DocumentFieldRun run => run.FieldType.ToString(),
        DocumentNoteReferenceRun note => GetNoteReferenceTitle(note),
        _ => LinkTitle
    };

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (Inline is not DocumentDrawingRun drawing
            || drawing.Source == DocumentImageSource.Url)
        {
            _resolvedDrawingAssetId = null;
            _resolvedDrawingImageUrl = null;
            _isResolvingImage = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(drawing.AssetId) || ImageUrlResolver is null)
        {
            _resolvedDrawingAssetId = drawing.AssetId;
            _resolvedDrawingImageUrl = null;
            _isResolvingImage = false;
            return;
        }

        if (string.Equals(_resolvedDrawingAssetId, drawing.AssetId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(_resolvedDrawingImageUrl))
        {
            return;
        }

        _resolvedDrawingAssetId = drawing.AssetId;
        _resolvedDrawingImageUrl = null;
        _isResolvingImage = true;

        try
        {
            _resolvedDrawingImageUrl = await ImageUrlResolver.ResolveUrlAsync(DocumentId, drawing.AssetId);
        }
        catch
        {
            _resolvedDrawingImageUrl = null;
        }
        finally
        {
            _isResolvingImage = false;
        }
    }

    private static string GetNoteReferenceTestId(DocumentNoteReferenceRun note) =>
        note.NoteType == DocumentNoteType.Endnote
            ? "document-wysiwyg-endnote-ref"
            : "document-wysiwyg-footnote-ref";

    private static string GetNoteReferenceTitle(DocumentNoteReferenceRun note) =>
        note.NoteType == DocumentNoteType.Endnote ? "Endnote" : "Footnote";

    private string InlineClass
    {
        get
        {
            var classes = new List<string> { "tm-document-inline" };
            if (Inline is TokenRun)
            {
                classes.Add("tm-document-inline--token");
                if (Inline is TokenRun token && !string.IsNullOrWhiteSpace(token.ColorClass))
                {
                    classes.Add(token.ColorClass!);
                }
            }

            if (Inline is DocumentNoteReferenceRun)
            {
                classes.Add("tm-document-inline--note-reference");
            }

            if (Inline is DocumentFieldRun)
            {
                classes.Add("tm-document-inline--field");
            }

            foreach (var mark in Inline?.Marks ?? [])
            {
                classes.Add(mark.Type switch
                {
                    InlineMarkType.Bold => "tm-document-inline--bold",
                    InlineMarkType.Italic => "tm-document-inline--italic",
                    InlineMarkType.Underline => "tm-document-inline--underline",
                    InlineMarkType.Strikethrough => "tm-document-inline--strikethrough",
                    InlineMarkType.Superscript => "tm-document-inline--superscript",
                    InlineMarkType.Subscript => "tm-document-inline--subscript",
                    InlineMarkType.Link => "tm-document-inline--link",
                    InlineMarkType.CommentAnchor => "tm-document-inline--comment-anchor",
                    InlineMarkType.Revision => GetRevisionClass(mark),
                    InlineMarkType.Highlight => "tm-document-inline--highlight",
                    InlineMarkType.TextColor => "tm-document-inline--text-color",
                    _ => string.Empty
                });
            }

            return string.Join(" ", classes.Where(c => !string.IsNullOrWhiteSpace(c)));
        }
    }

    private string? InlineStyle
    {
        get
        {
            var styles = new List<string>();
            foreach (var mark in Inline?.Marks ?? [])
            {
                if (string.IsNullOrWhiteSpace(mark.Value) || !IsSafeCssColor(mark.Value))
                {
                    continue;
                }

                if (mark.Type == InlineMarkType.Highlight)
                {
                    styles.Add($"background-color: {mark.Value}");
                }
                else if (mark.Type == InlineMarkType.TextColor)
                {
                    styles.Add($"color: {mark.Value}");
                }
            }

            return styles.Count == 0 ? null : string.Join("; ", styles);
        }
    }

    private string GetText()
    {
        return Inline switch
        {
            TextRun text => text.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            DocumentFieldRun field => ResolveFieldDisplayText(field),
            DocumentNoteReferenceRun note => string.IsNullOrWhiteSpace(note.DisplayMarker) ? note.NoteId : note.DisplayMarker!,
            _ => string.Empty
        };
    }

    private string? GetDrawingImageUrl()
    {
        if (Inline is not DocumentDrawingRun drawing || drawing.Kind != DocumentDrawingKind.Image)
        {
            return null;
        }

        if (drawing.Source == DocumentImageSource.Url)
        {
            return TmDocumentBlockRenderer.IsSafeImageUrl(drawing.Url) ? drawing.Url : null;
        }

        if (TmDocumentBlockRenderer.IsSafeImageUrl(_resolvedDrawingImageUrl))
        {
            return _resolvedDrawingImageUrl;
        }

        return TmDocumentBlockRenderer.IsSafeImageUrl(drawing.Url) ? drawing.Url : null;
    }

    private static string GetDrawingClass(DocumentDrawingRun drawing)
    {
        var classes = new List<string>
        {
            "tm-document-inline",
            "tm-document-drawing",
            "tm-document-image"
        };

        var alignment = drawing.Layout.Position.HorizontalAlignment switch
        {
            DocumentImageHorizontalPosition.Left => "start",
            DocumentImageHorizontalPosition.Right => "end",
            _ => "center"
        };
        classes.Add($"tm-document-image--{alignment}");

        if (drawing.Layout.IsInline)
        {
            classes.Add("tm-document-drawing--inline");
        }
        else
        {
            classes.Add("tm-document-drawing--anchored");
            classes.Add("tm-document-image--floating");
            classes.Add($"tm-document-image--wrap-{ToCssToken(drawing.Layout.Wrap.Mode)}");
            classes.Add($"tm-document-image--relative-{ToCssToken(drawing.Layout.Position.HorizontalRelativeTo)}");
            classes.Add($"tm-document-image--vrelative-{ToCssToken(drawing.Layout.Position.VerticalRelativeTo)}");
        }

        return string.Join(" ", classes);
    }

    private static string GetDrawingWrapMode(DocumentDrawingRun drawing)
        => drawing.Layout.Wrap.Mode.ToString();

    private static string GetDrawingAriaLabel(DocumentDrawingRun drawing)
    {
        if (drawing.IsDecorative)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(drawing.AltText)
            ? drawing.Caption ?? string.Empty
            : drawing.AltText!;
    }

    private static string GetDrawingAltText(DocumentDrawingRun drawing)
        => drawing.IsDecorative ? string.Empty : drawing.AltText ?? string.Empty;

    private static string? GetDrawingFigureStyle(DocumentDrawingRun drawing)
    {
        if (drawing.Layout.IsInline)
        {
            return null;
        }

        var styles = new List<string>
        {
            FormattableString.Invariant($"left: {drawing.Layout.Position.X:0.##}px"),
            FormattableString.Invariant($"top: {drawing.Layout.Position.Y:0.##}px")
        };

        if (drawing.Layout.Stacking.ZIndex != 0)
        {
            styles.Add(FormattableString.Invariant($"z-index: {drawing.Layout.Stacking.ZIndex}"));
        }

        return string.Join("; ", styles);
    }

    private static string? GetDrawingImageStyle(DocumentDrawingRun drawing)
    {
        var styles = new List<string>();
        var width = drawing.Layout.Transform.Width ?? drawing.Size.Width;
        var height = drawing.Layout.Transform.Height ?? drawing.Size.Height;
        if (width is > 0)
        {
            styles.Add(FormattableString.Invariant($"width: {width.Value:0.##}px"));
        }

        if (height is > 0)
        {
            styles.Add(FormattableString.Invariant($"height: {height.Value:0.##}px"));
        }

        return styles.Count == 0 ? null : string.Join("; ", styles);
    }

    private static string ToCssToken(DocumentWrapMode value)
    {
        return value switch
        {
            DocumentWrapMode.TopBottom => "top-bottom",
            DocumentWrapMode.BehindText => "behind-text",
            DocumentWrapMode.InFrontOfText => "in-front-of-text",
            _ => value.ToString().ToLowerInvariant()
        };
    }

    private static string ToCssToken(DocumentRelativePosition value)
        => value.ToString().ToLowerInvariant();

    private static string ResolveFieldDisplayText(DocumentFieldRun field)
    {
        if (!string.IsNullOrWhiteSpace(field.DisplayText))
        {
            return field.DisplayText;
        }

        if (!string.IsNullOrWhiteSpace(field.FallbackText))
        {
            return field.FallbackText;
        }

        return field.FieldType switch
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
            DocumentFieldType.FileName => "File name",
            DocumentFieldType.RevisionNumber => "1",
            _ => string.Empty
        };
    }

    private string GetRevisionClass(InlineMark mark)
        => string.Equals(mark.Value, "Deletion", StringComparison.Ordinal)
            ? "tm-document-inline--revision tm-document-inline--revision-delete"
            : "tm-document-inline--revision tm-document-inline--revision-insert";

    private string GetRevisionTestId()
        => string.Equals(RevisionMark?.Value, "Deletion", StringComparison.Ordinal)
            ? "document-revision-delete"
            : "document-revision-insert";

    private string? GetSafeHref()
    {
        var href = Inline?.Marks.FirstOrDefault(mark => mark.Type == InlineMarkType.Link)?.Link?.Href;
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        return DocumentLinkUtility.IsSafeHref(href) ? DocumentLinkUtility.NormalizeHref(href) : null;
    }

    internal static bool IsSafeLinkUrl(string href)
        => DocumentLinkUtility.IsSafeHref(href);

    private static bool IsSafeCssColor(string value)
    {
        if (value.StartsWith("#", StringComparison.Ordinal)
            && (value.Length == 4 || value.Length == 7)
            && value[1..].All(c => Uri.IsHexDigit(c)))
        {
            return true;
        }

        return value.All(char.IsLetter);
    }

    private Task SelectCommentAsync()
    {
        return string.IsNullOrWhiteSpace(CommentId)
            ? Task.CompletedTask
            : OnCommentSelected.InvokeAsync(CommentId);
    }
}
