namespace Tempo.Blazor.Models;

/// <summary>
/// Arguments passed to <c>OnTokenHover</c> when the user hovers over a token chip in TmRichEditor.
/// Contains token metadata and the bounding rect of the chip element in client (viewport) coordinates,
/// so the application can position its own tooltip relative to the chip.
/// </summary>
public sealed class TokenHoverArgs
{
    /// <summary>Unique key of the hovered token (e.g. "user.email").</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Display name of the token (e.g. "User Email").</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Optional description of the token.</summary>
    public string? Description { get; init; }

    /// <summary>Optional category of the token.</summary>
    public string? Category { get; init; }

    /// <summary>Optional icon (emoji or CSS class).</summary>
    public string? Icon { get; init; }

    /// <summary>Optional CSS color class applied to the chip.</summary>
    public string? ColorClass { get; init; }

    /// <summary>Optional type label (e.g. "Secret", "URL").</summary>
    public string? TypeLabel { get; init; }

    /// <summary>Left edge of the token chip in client (viewport) coordinates (px).</summary>
    public double RectLeft { get; init; }

    /// <summary>Top edge of the token chip in client (viewport) coordinates (px).</summary>
    public double RectTop { get; init; }

    /// <summary>Right edge of the token chip in client (viewport) coordinates (px).</summary>
    public double RectRight { get; init; }

    /// <summary>Bottom edge of the token chip in client (viewport) coordinates (px).</summary>
    public double RectBottom { get; init; }

    /// <summary>Width of the token chip (px).</summary>
    public double RectWidth { get; init; }

    /// <summary>Height of the token chip (px).</summary>
    public double RectHeight { get; init; }
}
