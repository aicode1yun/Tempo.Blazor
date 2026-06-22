using System.Collections.Concurrent;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// In-memory demo implementation of <see cref="ITmFileProvider"/>.
/// Stores uploaded files as base64 data URLs. File size is capped at 10 MB.
/// </summary>
public sealed class DemoNotionFileProvider : ITmFileProvider
{
    private readonly ConcurrentDictionary<string, FileEntry> _files = new();

    public TmFileProviderCapabilities Capabilities
        => TmFileProviderCapabilities.Upload
         | TmFileProviderCapabilities.Resolve
         | TmFileProviderCapabilities.Delete;

    public async Task<TmFileUploadResult> UploadAsync(
        TmFileUploadRequest request,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);

        var id = Guid.NewGuid().ToString("N");
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType;
        var dataUrl = $"data:{contentType};base64,{base64}";
        _files[id] = new FileEntry(request.FileName, contentType, dataUrl, ms.Length);

        return new TmFileUploadResult
        {
            Success = true,
            IsComplete = true,
            AssetId = id,
            Url = dataUrl,
            FileName = request.FileName,
            ContentType = contentType,
            SizeBytes = ms.Length
        };
    }

    public Task<TmFileResolveResult> ResolveAsync(
        TmFileResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_files.TryGetValue(request.AssetId, out var entry))
        {
            return Task.FromResult(new TmFileResolveResult
            {
                Success = false,
                AssetId = request.AssetId,
                ErrorMessage = $"File '{request.AssetId}' not found."
            });
        }

        return Task.FromResult(new TmFileResolveResult
        {
            Success = true,
            AssetId = request.AssetId,
            Url = entry.DataUrl,
            FileName = entry.Name,
            ContentType = entry.ContentType,
            SizeBytes = entry.Size
        });
    }

    public Task DeleteAsync(string assetId, CancellationToken cancellationToken = default)
    {
        _files.TryRemove(assetId, out _);
        return Task.CompletedTask;
    }

    private sealed record FileEntry(string Name, string ContentType, string DataUrl, long Size);
}
