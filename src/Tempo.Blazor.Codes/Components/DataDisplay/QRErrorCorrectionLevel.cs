namespace Tempo.Blazor.Components.DataDisplay;

/// <summary>QR code error correction levels.</summary>
public enum QRErrorCorrectionLevel
{
    /// <summary>Recovers 7% of data.</summary>
    L,
    /// <summary>Recovers 15% of data.</summary>
    M,
    /// <summary>Recovers 25% of data.</summary>
    Q,
    /// <summary>Recovers 30% of data.</summary>
    H
}
