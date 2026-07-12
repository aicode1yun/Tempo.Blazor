namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Anti-virus / content scan state of an uploaded file.</summary>
public enum FileScanStatus
{
    /// <summary>The file has not been submitted for scanning.</summary>
    NotScanned = 0,

    /// <summary>A scan is in progress; the file should be treated as not-yet-available.</summary>
    Pending = 1,

    /// <summary>The scan completed and the file is safe to access.</summary>
    Clean = 2,

    /// <summary>The scan flagged the file; it must not be downloaded or opened.</summary>
    Blocked = 3,

    /// <summary>The scan could not be completed (infrastructure error).</summary>
    Failed = 4
}
