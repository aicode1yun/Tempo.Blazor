using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Abstractions.Interfaces;

/// <summary>
/// Optional hook that scans a freshly uploaded file (virus / content inspection).
/// When supplied to a file component, an upload transitions
/// <see cref="FileScanStatus.Pending"/> → <see cref="FileScanStatus.Clean"/> or
/// <see cref="FileScanStatus.Blocked"/>; blocked files are rendered as unavailable
/// (no download / open). Providers that already scan server-side can instead carry the
/// status on the item/attachment model without supplying this hook.
/// </summary>
public interface IFileScanHook
{
    /// <summary>Scans an uploaded file and returns its resulting status.</summary>
    Task<FileScanResult> ScanAsync(FileScanRequest request, CancellationToken cancellationToken = default);
}
