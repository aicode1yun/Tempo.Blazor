using System.Collections.Concurrent;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// In-memory demo implementation of <see cref="INotionFileProvider"/>.
/// Stores uploaded files as base64 data URLs. File size is capped at 10 MB.
/// </summary>
public sealed class DemoNotionFileProvider : INotionFileProvider
{
    private readonly ConcurrentDictionary<string, FileEntry> _files = new();

    public Task<string> UploadFileAsync(Stream content, string fileName, string contentType)
    {
        var id = Guid.NewGuid().ToString("N");
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var dataUrl = $"data:{contentType};base64,{base64}";
        _files[id] = new FileEntry(fileName, contentType, dataUrl, ms.Length);
        return Task.FromResult(id);
    }

    public Task<string> GetFileUrlAsync(string fileId)
    {
        if (_files.TryGetValue(fileId, out var entry))
            return Task.FromResult(entry.DataUrl);
        throw new KeyNotFoundException($"File '{fileId}' not found.");
    }

    public Task DeleteFileAsync(string fileId)
    {
        _files.TryRemove(fileId, out _);
        return Task.CompletedTask;
    }

    public Task<long> GetFileSizeAsync(string fileId)
    {
        if (_files.TryGetValue(fileId, out var entry))
            return Task.FromResult(entry.Size);
        throw new KeyNotFoundException($"File '{fileId}' not found.");
    }

    private sealed record FileEntry(string Name, string ContentType, string DataUrl, long Size);
}
