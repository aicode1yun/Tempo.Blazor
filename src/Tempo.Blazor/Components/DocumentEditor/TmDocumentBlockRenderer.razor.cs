using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Renders one block from the document editor JSON model.</summary>
public partial class TmDocumentBlockRenderer : ComponentBase
{
    private string? _resolvedImageUrl;
    private string? _resolvedImageAssetId;
    private bool _isResolvingImage;

    /// <summary>Document id used by provider-backed image resolution.</summary>
    [Parameter] public string DocumentId { get; set; } = string.Empty;

    /// <summary>Block to render.</summary>
    [Parameter] public DocumentBlock? Block { get; set; }

    /// <summary>Optional resolver for provider-managed image assets.</summary>
    [Parameter] public IDocumentImageUrlResolver? ImageUrlResolver { get; set; }

    /// <summary>Pending provider-backed suggestions to decorate near their block.</summary>
    [Parameter] public IReadOnlyList<DocumentSuggestion> Suggestions { get; set; } = [];

    /// <summary>Raised when a rendered inline comment anchor is selected.</summary>
    [Parameter] public EventCallback<string> OnCommentSelected { get; set; }

    private IEnumerable<DocumentSuggestion> BlockSuggestions => Suggestions
        .Where(suggestion => suggestion.Status == DocumentSuggestionStatus.Pending)
        .Where(suggestion => string.Equals(suggestion.Range.BlockId, Block?.Id, StringComparison.Ordinal));

    private string? ImageUrl
    {
        get
        {
            if (Block?.Content is not ImageBlockContent image)
            {
                return null;
            }

            if (image.Source == DocumentImageSource.Url)
            {
                return IsSafeImageUrl(image.Url) ? image.Url : null;
            }

            if (IsSafeImageUrl(_resolvedImageUrl))
            {
                return _resolvedImageUrl;
            }

            return IsSafeImageUrl(image.Url) ? image.Url : null;
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (Block?.Content is not ImageBlockContent image
            || image.Source == DocumentImageSource.Url)
        {
            _resolvedImageAssetId = null;
            _resolvedImageUrl = null;
            _isResolvingImage = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(image.AssetId) || ImageUrlResolver is null)
        {
            _resolvedImageAssetId = image.AssetId;
            _resolvedImageUrl = null;
            _isResolvingImage = false;
            return;
        }

        if (_resolvedImageAssetId == image.AssetId && !string.IsNullOrWhiteSpace(_resolvedImageUrl))
        {
            return;
        }

        _resolvedImageAssetId = image.AssetId;
        _resolvedImageUrl = null;
        _isResolvingImage = true;

        try
        {
            _resolvedImageUrl = await ImageUrlResolver.ResolveUrlAsync(DocumentId, image.AssetId);
        }
        catch
        {
            _resolvedImageUrl = null;
        }
        finally
        {
            _isResolvingImage = false;
        }
    }

    private RenderFragment RenderHeading(HeadingBlockContent heading) => builder =>
    {
        var level = Math.Clamp(heading.Level, 1, 6);
        var tag = FormattableString.Invariant($"h{level}");
        builder.OpenElement(0, tag);
        builder.AddAttribute(1, "class", $"tm-document-block tm-document-block--heading tm-document-heading tm-document-heading--h{level}");
        var sequence = 2;
        foreach (var inline in heading.Inlines)
        {
            builder.OpenComponent<TmDocumentInlineRenderer>(sequence++);
            builder.AddAttribute(sequence++, nameof(TmDocumentInlineRenderer.Inline), inline);
            builder.AddAttribute(sequence++, nameof(TmDocumentInlineRenderer.OnCommentSelected), OnCommentSelected);
            builder.CloseComponent();
        }

        builder.CloseElement();
    };

    private static string GetListClass(ListBlockContent list)
    {
        return $"tm-document-block tm-document-list tm-document-list--indent-{Math.Clamp(list.IndentLevel, 0, 8)}";
    }

    private static string GetImageClass(ImageBlockContent image)
    {
        var alignment = image.Alignment switch
        {
            DocumentImageAlignment.Start => "start",
            DocumentImageAlignment.End => "end",
            _ => "center"
        };

        var classes = new List<string> { "tm-document-block", "tm-document-image", $"tm-document-image--{alignment}" };
        if (image.FloatingLayout?.Inline == false)
        {
            classes.Add("tm-document-image--floating");
            classes.Add($"tm-document-image--wrap-{ToCssToken(image.FloatingLayout.WrapMode)}");
            classes.Add($"tm-document-image--relative-{ToCssToken(image.FloatingLayout.HorizontalRelativeTo)}");
            classes.Add($"tm-document-image--vrelative-{ToCssToken(image.FloatingLayout.VerticalRelativeTo)}");
        }

        return string.Join(" ", classes);
    }

    private static string GetSuggestionClass(DocumentSuggestion suggestion)
    {
        var kind = suggestion.Type == DocumentSuggestionType.DeleteText ? "delete" : "insert";
        return $"tm-document-suggestion tm-document-suggestion--{kind}";
    }

    private static string GetSuggestionTestId(DocumentSuggestion suggestion)
        => suggestion.Type == DocumentSuggestionType.DeleteText
            ? "document-suggestion-delete"
            : "document-suggestion-insert";

    private string GetSuggestionLabel(DocumentSuggestion suggestion)
        => suggestion.Type == DocumentSuggestionType.DeleteText
            ? Loc["TmDocumentEditor_SuggestionDeleteAria"]
            : Loc["TmDocumentEditor_SuggestionInsertAria"];

    private static string GetSuggestionPreview(DocumentSuggestion suggestion)
        => suggestion.Type == DocumentSuggestionType.DeleteText
            ? suggestion.OriginalText ?? string.Empty
            : suggestion.SuggestedText ?? string.Empty;

    private static string? GetImageFigureStyle(ImageBlockContent image)
    {
        var layout = image.FloatingLayout;
        if (layout?.Inline != false)
        {
            return null;
        }

        var styles = new List<string>
        {
            FormattableString.Invariant($"left: {layout.X:0.##}px"),
            FormattableString.Invariant($"top: {layout.Y:0.##}px")
        };

        if (layout.ZIndex != 0)
        {
            styles.Add(FormattableString.Invariant($"z-index: {layout.ZIndex}"));
        }

        return string.Join("; ", styles);
    }

    private static string? GetImageStyle(ImageBlockContent image)
    {
        var styles = new List<string>();
        if (image.Size.Width is > 0)
        {
            styles.Add(FormattableString.Invariant($"width: {image.Size.Width.Value:0.##}px"));
        }

        if (image.Size.Height is > 0)
        {
            styles.Add(FormattableString.Invariant($"height: {image.Size.Height.Value:0.##}px"));
        }

        return styles.Count == 0 ? null : string.Join("; ", styles);
    }

    private static string? GetTableStyle(TableBlockContent table)
    {
        var styles = new List<string>();
        var layout = table.Layout;
        if (layout.Width is > 0)
        {
            styles.Add(FormattableString.Invariant($"width: {layout.Width.Value:0.##}px"));
        }

        if (!string.IsNullOrWhiteSpace(layout.BackgroundColor))
        {
            styles.Add($"background-color: {layout.BackgroundColor}");
        }

        if (layout.CellPadding is > 0)
        {
            styles.Add(FormattableString.Invariant($"--tm-document-table-cell-padding: {layout.CellPadding.Value:0.##}px"));
        }

        styles.Add(layout.Alignment switch
        {
            TableHorizontalAlignment.Center => "margin-left: auto; margin-right: auto",
            TableHorizontalAlignment.Right => "margin-left: auto; margin-right: 0",
            _ => "margin-left: 0; margin-right: auto"
        });

        return styles.Count == 0 ? null : string.Join("; ", styles);
    }

    private static string? GetTableCellStyle(TableCellContent cell)
    {
        var styles = new List<string>();
        if (cell.Width is > 0)
        {
            styles.Add(FormattableString.Invariant($"width: {cell.Width.Value:0.##}px"));
        }

        if (!string.IsNullOrWhiteSpace(cell.BackgroundColor))
        {
            styles.Add($"background-color: {cell.BackgroundColor}");
        }

        if (cell.Padding is > 0)
        {
            styles.Add(FormattableString.Invariant($"padding: {cell.Padding.Value:0.##}px"));
        }

        styles.Add(cell.VerticalAlignment switch
        {
            TableCellVerticalAlignment.Middle => "vertical-align: middle",
            TableCellVerticalAlignment.Bottom => "vertical-align: bottom",
            _ => "vertical-align: top"
        });

        AddBorder(styles, "top", cell.Borders.Top);
        AddBorder(styles, "right", cell.Borders.Right);
        AddBorder(styles, "bottom", cell.Borders.Bottom);
        AddBorder(styles, "left", cell.Borders.Left);

        return styles.Count == 0 ? null : string.Join("; ", styles);
    }

    private static void AddBorder(List<string> styles, string side, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            styles.Add($"border-{side}: {value}");
        }
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
    {
        return value.ToString().ToLowerInvariant();
    }

    internal static bool IsSafeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (url.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        if (url.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:image/gif;base64,", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }
}
