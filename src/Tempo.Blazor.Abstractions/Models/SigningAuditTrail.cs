namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Complete provider-agnostic audit trail payload.</summary>
public sealed class SigningAuditTrail
{
    /// <summary>Documents and checksums included in the trail.</summary>
    public IReadOnlyList<SigningAuditTrailDocument> Documents { get; init; } = [];

    /// <summary>Signer identities included in the trail.</summary>
    public IReadOnlyList<SigningAuditTrailSigner> Signers { get; init; } = [];

    /// <summary>Chronological audit events.</summary>
    public IReadOnlyList<SigningAuditTrailEvent> Events { get; init; } = [];

    /// <summary>Optional audit PDF download URL.</summary>
    public string? AuditPdfUrl { get; init; }
}
