using System.Net.Http.Json;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Demo.Shared;

namespace Tempo.Blazor.Demo.Services;

public class AttachmentHttpProvider : ITmAttachmentProvider, ITmFileProvider, ITmChunkedFileProvider
{
    private readonly HttpClient _http;

    public AttachmentHttpProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    TmAttachmentProviderCapabilities ITmAttachmentProvider.Capabilities
        => TmAttachmentProviderCapabilities.Read
         | TmAttachmentProviderCapabilities.Add
         | TmAttachmentProviderCapabilities.Remove;

    TmAttachmentProviderCapabilities ITmCapabilityProvider<TmAttachmentProviderCapabilities>.Capabilities
        => TmAttachmentProviderCapabilities.Read
         | TmAttachmentProviderCapabilities.Add
         | TmAttachmentProviderCapabilities.Remove;

    TmFileProviderCapabilities ITmFileProvider.Capabilities
        => TmFileProviderCapabilities.Upload
         | TmFileProviderCapabilities.Resolve
         | TmFileProviderCapabilities.Delete
         | TmFileProviderCapabilities.ChunkUpload;

    TmFileProviderCapabilities ITmCapabilityProvider<TmFileProviderCapabilities>.Capabilities
        => TmFileProviderCapabilities.Upload
         | TmFileProviderCapabilities.Resolve
         | TmFileProviderCapabilities.Delete
         | TmFileProviderCapabilities.ChunkUpload;

    public async Task<IReadOnlyList<TmAttachment>> GetForEntityAsync(
        TmEntityRef entityRef,
        CancellationToken cancellationToken = default)
    {
        var result = await _http.GetFromJsonAsync<List<AttachmentDto>>(
            $"/api/attachments/{Uri.EscapeDataString(entityRef.EntityId)}",
            cancellationToken);

        return result?.Select(dto => ToAttachment(dto, entityRef)).ToList() ?? [];
    }

    public Task<TmAttachment> AddAsync(
        TmAttachment attachment,
        CancellationToken cancellationToken = default)
        => Task.FromResult(attachment);

    public async Task RemoveAsync(
        TmEntityRef entityRef,
        string attachmentId,
        CancellationToken cancellationToken = default)
        => await DeleteAsync(attachmentId, cancellationToken);

    public async Task<TmFileUploadResult> UploadAsync(
        TmFileUploadRequest request,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);

        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);

        return await UploadChunkAsync(
            new TmFileChunk
            {
                FileName = request.FileName,
                ContentType = request.ContentType,
                TotalSizeBytes = request.SizeBytes ?? memory.Length,
                ChunkIndex = 0,
                TotalChunks = 1,
                Data = memory.ToArray(),
                EntityRef = request.EntityRef,
                Purpose = request.Purpose,
                Metadata = request.Metadata
            },
            cancellationToken);
    }

    public async Task<TmFileResolveResult> ResolveAsync(
        TmFileResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _http.GetFromJsonAsync<DownloadUrlResponse>(
            $"/api/attachments/download/{Uri.EscapeDataString(request.AssetId)}",
            cancellationToken);

        return new TmFileResolveResult
        {
            Success = !string.IsNullOrWhiteSpace(result?.Url),
            AssetId = request.AssetId,
            Url = result?.Url,
            ErrorMessage = string.IsNullOrWhiteSpace(result?.Url) ? "Download URL was not returned." : null
        };
    }

    public async Task DeleteAsync(string assetId, CancellationToken cancellationToken = default)
    {
        var response = await _http.DeleteAsync(
            $"/api/attachments/item/{Uri.EscapeDataString(assetId)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TmFileUploadResult> UploadChunkAsync(
        TmFileChunk chunk,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        var body = new
        {
            chunk.FileName,
            chunk.ContentType,
            TotalSize = chunk.TotalSizeBytes,
            chunk.ChunkIndex,
            chunk.TotalChunks,
            Data = Convert.ToBase64String(chunk.Data),
            EntityId = chunk.EntityRef?.EntityId,
            chunk.UploadSessionId
        };

        var response = await _http.PostAsJsonAsync("/api/attachments/chunk", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChunkUploadResponse>(cancellationToken);
        return new TmFileUploadResult
        {
            Success = result?.Completed == true || result is not null,
            IsComplete = result?.Completed == true,
            AssetId = result?.AttachmentId,
            UploadSessionId = result?.AttachmentId ?? chunk.UploadSessionId,
            FileName = chunk.FileName,
            ContentType = chunk.ContentType,
            SizeBytes = chunk.TotalSizeBytes
        };
    }

    private sealed record DownloadUrlResponse(string Url);
    private sealed record ChunkUploadResponse(string? AttachmentId, bool Completed);

    private static TmAttachment ToAttachment(AttachmentDto dto, TmEntityRef entityRef)
        => new()
        {
            Id = dto.Id,
            EntityRef = entityRef,
            AssetId = dto.Id,
            FileName = dto.FileName,
            ContentType = dto.ContentType,
            SizeBytes = dto.FileSizeBytes,
            UploadedAt = dto.UploadedAt,
            UploadedBy = string.IsNullOrWhiteSpace(dto.UploadedByName)
                ? null
                : new TmUserRef { Id = dto.UploadedByName, DisplayName = dto.UploadedByName },
            CanDelete = dto.CanDelete,
            CanDownload = true,
            Purpose = "activity-attachment"
        };
}
