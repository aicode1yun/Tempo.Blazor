using Microsoft.AspNetCore.Components.Forms;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Components.Files;

/// <summary>
/// Streams a file to a chunk sink (typically a provider's <c>UploadChunkAsync</c>) in fixed-size
/// chunks, reporting progress and honouring cancellation and resume. Kept free of any Blazor or
/// browser dependency so it is fully unit-testable against an ordinary <see cref="Stream"/>.
/// </summary>
public static class TmChunkedUploader
{
    /// <summary>Delegate matching a provider's chunk-upload entry point.</summary>
    public delegate Task<TmFileUploadResult> ChunkSink(TmFileChunk chunk, CancellationToken cancellationToken);

    /// <summary>
    /// Reads <paramref name="source"/> and sends it to <paramref name="sink"/> chunk by chunk.
    /// Threads the provider-assigned session id forward, reports one <see cref="TmUploadProgress"/>
    /// per acknowledged chunk, and stops early if a chunk fails or is cancelled.
    /// </summary>
    /// <returns>The result of the final chunk (or the failing chunk).</returns>
    public static async Task<TmFileUploadResult> UploadAsync(
        Stream source,
        TmChunkedUploadRequest request,
        ChunkSink sink,
        IProgress<TmUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        var chunkSize = request.ChunkSizeBytes > 0 ? request.ChunkSizeBytes : TmChunkedUploadRequest.DefaultChunkSizeBytes;
        var totalSize = Math.Max(0, request.TotalSizeBytes);
        var totalChunks = totalSize == 0 ? 1 : (int)((totalSize + chunkSize - 1) / chunkSize);

        var startIndex = Math.Clamp(request.ResumeFromChunkIndex, 0, totalChunks - 1);
        if (startIndex > 0)
        {
            await SkipAsync(source, (long)startIndex * chunkSize, cancellationToken).ConfigureAwait(false);
        }

        var sessionId = request.UploadSessionId;
        long bytesTransferred = (long)startIndex * chunkSize;
        var buffer = new byte[chunkSize];
        TmFileUploadResult result = new() { Success = true, IsComplete = false };

        for (var index = startIndex; index < totalChunks; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var read = await FillAsync(source, buffer, cancellationToken).ConfigureAwait(false);
            var data = read == buffer.Length ? buffer : buffer[..read];

            var chunk = new TmFileChunk
            {
                FileName = request.FileName,
                ContentType = request.ContentType,
                TotalSizeBytes = totalSize,
                ChunkIndex = index,
                TotalChunks = totalChunks,
                Data = data.ToArray(),
                UploadSessionId = sessionId,
                EntityRef = request.EntityRef,
                Purpose = request.Purpose,
                Metadata = request.Metadata
            };

            result = await sink(chunk, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(result.UploadSessionId))
            {
                sessionId = result.UploadSessionId;
            }

            // Only advance progress (and therefore the resume index) for an *acknowledged*
            // chunk. A failed chunk must not move NextChunkIndex past itself, otherwise a
            // resume would skip the unsent chunk and corrupt the reassembled file.
            if (!result.Success)
            {
                return result;
            }

            bytesTransferred = Math.Min(totalSize == 0 ? read : totalSize, bytesTransferred + read);

            progress?.Report(new TmUploadProgress
            {
                FileName = request.FileName,
                BytesTransferred = bytesTransferred,
                TotalBytes = totalSize,
                ChunkIndex = index,
                TotalChunks = totalChunks,
                UploadSessionId = sessionId,
                IsComplete = chunk.IsLast
            });
        }

        return result;
    }

    /// <summary>
    /// Chunk-uploads an <see cref="IBrowserFile"/> to a chunk provider, opening the browser stream
    /// with <paramref name="maxAllowedSize"/> and honouring resume state on the <paramref name="request"/>.
    /// </summary>
    public static async Task<TmFileUploadResult> UploadBrowserFileAsync(
        IBrowserFile file,
        ChunkSink sink,
        long maxAllowedSize,
        TmChunkedUploadRequest request,
        IProgress<TmUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(request);

        request.FileName = string.IsNullOrEmpty(request.FileName) ? file.Name : request.FileName;
        request.ContentType ??= file.ContentType;
        if (request.TotalSizeBytes <= 0) request.TotalSizeBytes = file.Size;

        await using var stream = file.OpenReadStream(maxAllowedSize, cancellationToken);
        return await UploadAsync(stream, request, sink, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads until <paramref name="buffer"/> is full or the stream ends.</summary>
    private static async Task<int> FillAsync(Stream source, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await source.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            offset += read;
        }
        return offset;
    }

    /// <summary>Advances a (possibly non-seekable) stream by <paramref name="count"/> bytes.</summary>
    private static async Task SkipAsync(Stream source, long count, CancellationToken cancellationToken)
    {
        if (count <= 0) return;
        if (source.CanSeek)
        {
            source.Seek(count, SeekOrigin.Current);
            return;
        }

        var scratch = new byte[Math.Min(count, 81920)];
        var remaining = count;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(remaining, scratch.Length);
            var read = await source.ReadAsync(scratch.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            remaining -= read;
        }
    }
}
