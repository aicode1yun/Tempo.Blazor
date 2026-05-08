namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Provider-agnostic PDF verification result.</summary>
public sealed class SigningPdfVerificationResult
{
    /// <summary>Current verification status.</summary>
    public SigningPdfVerificationStatus Status { get; init; } = SigningPdfVerificationStatus.Empty;

    /// <summary>Verified file name.</summary>
    public string? FileName { get; init; }

    /// <summary>Document checksum, typically SHA-256.</summary>
    public string? Checksum { get; init; }

    /// <summary>Optional provider message.</summary>
    public string? Message { get; init; }

    /// <summary>Signatures found in the PDF.</summary>
    public IReadOnlyList<SigningPdfSignatureInfo> Signatures { get; init; } = [];
}
