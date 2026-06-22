namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Optional file provider contract for chunked uploads.</summary>
public interface ITmChunkedFileProvider
{
    /// <summary>Uploads a single chunk in a larger file upload.</summary>
    /// <param name="chunk">Chunk payload and metadata.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmFileUploadResult> UploadChunkAsync(
        TmFileChunk chunk,
        CancellationToken cancellationToken = default);
}
