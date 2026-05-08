namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Normalized rectangle describing where a signing field appears on a document page.</summary>
public class SigningFieldArea
{
    /// <summary>Stable area identifier.</summary>
    public string Uuid { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document attachment identifier this area belongs to.</summary>
    public string? AttachmentUuid { get; set; }

    /// <summary>Zero-based page index.</summary>
    public int Page { get; set; }

    /// <summary>Normalized horizontal position from the left edge, in the 0..1 page coordinate space.</summary>
    public double X { get; set; }

    /// <summary>Normalized vertical position from the top edge, in the 0..1 page coordinate space.</summary>
    public double Y { get; set; }

    /// <summary>Normalized width in the 0..1 page coordinate space.</summary>
    public double Width { get; set; }

    /// <summary>Normalized height in the 0..1 page coordinate space.</summary>
    public double Height { get; set; }

    /// <summary>Normalized width of a single cell for comb-style fields.</summary>
    public double? CellWidth { get; set; }

    /// <summary>Option identifier represented by this area for radio or multiple-choice fields.</summary>
    public string? OptionUuid { get; set; }
}
