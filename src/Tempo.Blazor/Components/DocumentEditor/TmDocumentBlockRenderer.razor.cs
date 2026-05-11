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

    /// <summary>Raised when a rendered inline comment anchor is selected.</summary>
    [Parameter] public EventCallback<string> OnCommentSelected { get; set; }

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

        return $"tm-document-block tm-document-image tm-document-image--{alignment}";
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
