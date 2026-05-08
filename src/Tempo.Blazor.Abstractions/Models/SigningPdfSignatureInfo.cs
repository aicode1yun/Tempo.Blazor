namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Signature found in a verified PDF.</summary>
public sealed class SigningPdfSignatureInfo
{
    /// <summary>Signer display name.</summary>
    public string? SignerName { get; init; }

    /// <summary>Signer email address.</summary>
    public string? SignerEmail { get; init; }

    /// <summary>Time when the signature was applied.</summary>
    public DateTimeOffset? SignedAt { get; init; }

    /// <summary>Verification method such as email, SMS, ID check, or KBA.</summary>
    public string? VerificationMethod { get; init; }

    /// <summary>Certificate subject or signing provider identity.</summary>
    public string? CertificateSubject { get; init; }
}
