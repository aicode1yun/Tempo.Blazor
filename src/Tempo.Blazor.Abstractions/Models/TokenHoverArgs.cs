namespace Tempo.Blazor.Models;

/// <summary>
/// Arguments passed to <c>OnTokenHover</c> when the user hovers over a token chip in TmRichEditor.
/// Contains token metadata and the bounding rect of the chip element in client (viewport) coordinates,
/// so the application can position its own tooltip relative to the chip.
/// </summary>
public record TokenHoverArgs(
    /// <summary>Unique key of the hovered token (e.g. "user.email").</summary>
    string Key,

    /// <summary>Display name of the token (e.g. "User Email").</summary>
    string DisplayName,

    /// <summary>Optional description of the token.</summary>
    string? Description,

    /// <summary>Optional category of the token.</summary>
    string? Category,

    /// <summary>Optional icon (emoji or CSS class).</summary>
    string? Icon,

    /// <summary>Optional CSS color class applied to the chip.</summary>
    string? ColorClass,

    /// <summary>Optional type label (e.g. "Secret", "URL").</summary>
    string? TypeLabel,

    /// <summary>Left edge of the token chip in client (viewport) coordinates (px).</summary>
    double RectLeft,

    /// <summary>Top edge of the token chip in client (viewport) coordinates (px).</summary>
    double RectTop,

    /// <summary>Right edge of the token chip in client (viewport) coordinates (px).</summary>
    double RectRight,

    /// <summary>Bottom edge of the token chip in client (viewport) coordinates (px).</summary>
    double RectBottom,

    /// <summary>Width of the token chip (px).</summary>
    double RectWidth,

    /// <summary>Height of the token chip (px).</summary>
    double RectHeight
);
