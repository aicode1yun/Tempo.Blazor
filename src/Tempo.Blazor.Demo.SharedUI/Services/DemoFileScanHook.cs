using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// Demo virus scanner: blocks any file whose name hints at malware (contains "virus",
/// "eicar", or "malware"); everything else is clean. Illustrates the
/// upload → Pending → Clean/Blocked flow of <see cref="IFileScanHook"/>.
/// </summary>
public sealed class DemoFileScanHook : IFileScanHook
{
    private static readonly string[] Signatures = ["virus", "eicar", "malware"];

    public Task<FileScanResult> ScanAsync(FileScanRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.FileName ?? string.Empty;
        var hit = Signatures.FirstOrDefault(s => name.Contains(s, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(hit is null
            ? FileScanResult.Clean()
            : FileScanResult.BlockedBy($"Demo.{hit}.Test", "Blocked by the demo virus scanner."));
    }
}
