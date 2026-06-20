namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Signer identity information captured in an audit trail.</summary>
public sealed class SigningAuditTrailSigner
{
    /// <summary>Signer display name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Signer email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>IP address observed during signing.</summary>
    public string? IpAddress { get; init; }

    /// <summary>User agent observed during signing.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Signer timezone.</summary>
    public string? TimeZone { get; init; }

    /// <summary>Verification method used for this signer.</summary>
    public string? VerificationMethod { get; init; }
}
