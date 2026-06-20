namespace Tempo.Blazor.Abstractions.Models;

/// <summary>PDF signature verification state.</summary>
public enum SigningPdfVerificationStatus
{
    /// <summary>No file has been selected or verified.</summary>
    Empty,

    /// <summary>Verification is running.</summary>
    Loading,

    /// <summary>The document checksum and signatures were verified.</summary>
    Verified,

    /// <summary>The checksum was not found in the audit store.</summary>
    ChecksumNotFound,

    /// <summary>The uploaded file is not a valid PDF.</summary>
    MalformedPdf,

    /// <summary>Verification failed for another reason.</summary>
    Error
}
