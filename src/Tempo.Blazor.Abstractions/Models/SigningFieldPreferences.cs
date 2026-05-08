namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Display and behavior preferences for a signing field.</summary>
public class SigningFieldPreferences
{
    /// <summary>Field text or stroke color.</summary>
    public string? Color { get; set; }

    /// <summary>Text alignment, such as left, center, or right.</summary>
    public string? Align { get; set; }

    /// <summary>Field format, such as date format, number format, or signature capture mode.</summary>
    public string? Format { get; set; }

    /// <summary>Font family used when rendering text into a document.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Font size used when rendering text into a document.</summary>
    public double? FontSize { get; set; }

    /// <summary>Whether a signature identifier should be shown near the signature.</summary>
    public bool? WithSignatureId { get; set; }

    /// <summary>Whether generated stamp fields should include a logo.</summary>
    public bool? WithLogo { get; set; }

    /// <summary>Field UUID containing the signing reason for a signature field.</summary>
    public string? ReasonFieldUuid { get; set; }

    /// <summary>Formula expression used for computed number or payment fields.</summary>
    public string? Formula { get; set; }

    /// <summary>Currency code for payment fields.</summary>
    public string? Currency { get; set; }

    /// <summary>Static price for payment fields.</summary>
    public decimal? Price { get; set; }

    /// <summary>Provider-specific external price identifier.</summary>
    public string? PriceId { get; set; }

    /// <summary>Provider-specific payment link identifier.</summary>
    public string? PaymentLinkId { get; set; }

    /// <summary>Additional provider or application-specific settings.</summary>
    public Dictionary<string, object?> AdditionalSettings { get; set; } = [];
}
