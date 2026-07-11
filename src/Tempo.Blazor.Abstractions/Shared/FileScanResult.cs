namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Outcome returned by an <see cref="Interfaces.IFileScanHook"/>.</summary>
public sealed class FileScanResult
{
    /// <summary>Resulting scan state.</summary>
    public FileScanStatus Status { get; set; } = FileScanStatus.Clean;

    /// <summary>Human-readable detail (localized by the host), such as why a file was blocked.</summary>
    public string? Message { get; set; }

    /// <summary>Name of the detected threat when <see cref="Status"/> is <see cref="FileScanStatus.Blocked"/>.</summary>
    public string? ThreatName { get; set; }

    /// <summary>Convenience factory for a clean result.</summary>
    public static FileScanResult Clean() => new() { Status = FileScanStatus.Clean };

    /// <summary>Convenience factory for a blocked result.</summary>
    public static FileScanResult BlockedBy(string threatName, string? message = null)
        => new() { Status = FileScanStatus.Blocked, ThreatName = threatName, Message = message };
}
