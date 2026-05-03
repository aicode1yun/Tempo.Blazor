namespace Tempo.Blazor.Components.DataDisplay;

/// <summary>Supported barcode formats.</summary>
public enum BarcodeFormat
{
    /// <summary>Code 128 linear barcode.</summary>
    Code128,
    /// <summary>Code 39 linear barcode.</summary>
    Code39,
    /// <summary>EAN-13 retail barcode.</summary>
    EAN13,
    /// <summary>UPC barcode.</summary>
    UPC,
    /// <summary>QR code (2D).</summary>
    QR
}
