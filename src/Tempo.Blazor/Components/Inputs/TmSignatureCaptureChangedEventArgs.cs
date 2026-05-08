namespace Tempo.Blazor.Components.Inputs;

/// <summary>Value and metadata emitted by <see cref="TmSignatureCapture"/>.</summary>
/// <param name="Value">Captured signature value.</param>
/// <param name="Mode">Mode that produced the signature.</param>
/// <param name="Reason">Optional signing reason.</param>
/// <param name="RememberSignature">Whether the signer asked to remember the signature.</param>
public sealed record TmSignatureCaptureChangedEventArgs(
    string? Value,
    TmSignatureCaptureMode Mode,
    string? Reason,
    bool RememberSignature);
