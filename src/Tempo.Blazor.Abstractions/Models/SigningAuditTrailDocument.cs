namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Document checksum included in an audit trail.</summary>
public sealed class SigningAuditTrailDocument
{
    /// <summary>Document file name.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Document checksum, typically SHA-256.</summary>
    public string Checksum { get; init; } = string.Empty;
}
