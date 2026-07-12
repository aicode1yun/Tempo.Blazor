using FluentAssertions;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Components.Files;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Files;

/// <summary>Reference tests for the dependency-free chunked upload coordinator (256 KB chunks).</summary>
public class TmChunkedUploaderTests
{
    private const int ChunkSize = 256 * 1024;

    private static (Stream stream, byte[] bytes) MakeFile(int size)
    {
        var bytes = new byte[size];
        for (var i = 0; i < size; i++) bytes[i] = (byte)(i % 251);
        return (new MemoryStream(bytes, writable: false), bytes);
    }

    private sealed class RecordingSink
    {
        public List<TmFileChunk> Chunks { get; } = [];
        public string SessionId { get; } = "sess-1";

        public Task<TmFileUploadResult> UploadAsync(TmFileChunk chunk, CancellationToken ct)
        {
            // Copy so later buffer reuse cannot mutate what we recorded.
            Chunks.Add(new TmFileChunk
            {
                FileName = chunk.FileName,
                ContentType = chunk.ContentType,
                TotalSizeBytes = chunk.TotalSizeBytes,
                ChunkIndex = chunk.ChunkIndex,
                TotalChunks = chunk.TotalChunks,
                Data = chunk.Data,
                UploadSessionId = chunk.UploadSessionId
            });
            return Task.FromResult(new TmFileUploadResult
            {
                Success = true,
                IsComplete = chunk.IsLast,
                UploadSessionId = SessionId,
                AssetId = chunk.IsLast ? "asset-1" : null
            });
        }
    }

    [Fact]
    public async Task Upload_SplitsInto256KbChunks_WithSmallerLast()
    {
        var (stream, bytes) = MakeFile(ChunkSize * 2 + 1000);
        var sink = new RecordingSink();

        var result = await TmChunkedUploader.UploadAsync(
            stream,
            new TmChunkedUploadRequest { FileName = "big.bin", TotalSizeBytes = bytes.Length },
            sink.UploadAsync);

        sink.Chunks.Should().HaveCount(3);
        sink.Chunks[0].TotalChunks.Should().Be(3);
        sink.Chunks[0].Data.Length.Should().Be(ChunkSize);
        sink.Chunks[1].Data.Length.Should().Be(ChunkSize);
        sink.Chunks[2].Data.Length.Should().Be(1000);
        sink.Chunks[2].IsLast.Should().BeTrue();
        // Reassembled payload must equal the original file exactly.
        sink.Chunks.SelectMany(c => c.Data).Should().Equal(bytes);
        result.IsComplete.Should().BeTrue();
        result.AssetId.Should().Be("asset-1");
    }

    [Fact]
    public async Task Upload_ReportsProgress_EndingAt100()
    {
        var (stream, bytes) = MakeFile(ChunkSize * 3);
        var sink = new RecordingSink();
        var reports = new List<TmUploadProgress>();

        await TmChunkedUploader.UploadAsync(
            stream,
            new TmChunkedUploadRequest { FileName = "f", TotalSizeBytes = bytes.Length },
            sink.UploadAsync,
            new Progress<TmUploadProgress>(reports.Add));

        // Progress is delivered asynchronously by Progress<T>; drain the sync context.
        await Task.Yield();
        reports.Should().NotBeEmpty();
        reports[^1].Percent.Should().Be(100);
        reports[^1].IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Upload_ThreadsSessionIdForward()
    {
        var (stream, bytes) = MakeFile(ChunkSize * 2);
        var sink = new RecordingSink();

        await TmChunkedUploader.UploadAsync(
            stream,
            new TmChunkedUploadRequest { FileName = "f", TotalSizeBytes = bytes.Length },
            sink.UploadAsync);

        // First chunk has no session yet; the second carries the id returned by the first.
        sink.Chunks[0].UploadSessionId.Should().BeNull();
        sink.Chunks[1].UploadSessionId.Should().Be("sess-1");
    }

    [Fact]
    public async Task Upload_Resume_SkipsAlreadySentChunks()
    {
        var (stream, bytes) = MakeFile(ChunkSize * 3);
        var sink = new RecordingSink();

        await TmChunkedUploader.UploadAsync(
            stream,
            new TmChunkedUploadRequest
            {
                FileName = "f",
                TotalSizeBytes = bytes.Length,
                ResumeFromChunkIndex = 2,
                UploadSessionId = "sess-1"
            },
            sink.UploadAsync);

        // Only the final chunk should be sent, and it must be the correct tail bytes.
        sink.Chunks.Should().ContainSingle();
        sink.Chunks[0].ChunkIndex.Should().Be(2);
        sink.Chunks[0].UploadSessionId.Should().Be("sess-1");
        sink.Chunks[0].Data.Should().Equal(bytes[(ChunkSize * 2)..]);
    }

    [Fact]
    public async Task Upload_EmptyFile_SendsSingleChunk()
    {
        var sink = new RecordingSink();

        var result = await TmChunkedUploader.UploadAsync(
            new MemoryStream([]),
            new TmChunkedUploadRequest { FileName = "empty.txt", TotalSizeBytes = 0 },
            sink.UploadAsync);

        sink.Chunks.Should().ContainSingle();
        sink.Chunks[0].Data.Should().BeEmpty();
        sink.Chunks[0].IsLast.Should().BeTrue();
        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Upload_Cancellation_StopsSending()
    {
        var (stream, bytes) = MakeFile(ChunkSize * 4);
        using var cts = new CancellationTokenSource();
        var count = 0;

        Task<TmFileUploadResult> Sink(TmFileChunk chunk, CancellationToken ct)
        {
            count++;
            if (count == 2) cts.Cancel();
            return Task.FromResult(new TmFileUploadResult { Success = true, IsComplete = chunk.IsLast });
        }

        var act = () => TmChunkedUploader.UploadAsync(
            stream,
            new TmChunkedUploadRequest { FileName = "f", TotalSizeBytes = bytes.Length },
            Sink,
            cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        count.Should().Be(2); // stopped after the cancelling chunk, before chunk 3
    }

    [Fact]
    public async Task Upload_FailedChunk_ShortCircuits()
    {
        var (stream, bytes) = MakeFile(ChunkSize * 3);
        var count = 0;

        Task<TmFileUploadResult> Sink(TmFileChunk chunk, CancellationToken ct)
        {
            count++;
            return Task.FromResult(new TmFileUploadResult
            {
                Success = chunk.ChunkIndex != 1,
                IsComplete = false,
                ErrorMessage = chunk.ChunkIndex == 1 ? "boom" : null
            });
        }

        var result = await TmChunkedUploader.UploadAsync(
            stream,
            new TmChunkedUploadRequest { FileName = "f", TotalSizeBytes = bytes.Length },
            Sink);

        count.Should().Be(2); // chunk 0 ok, chunk 1 fails, chunk 2 never sent
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("boom");
    }

    [Fact]
    public async Task Upload_FailedChunk_DoesNotAdvanceResumeIndex()
    {
        // Regression: a failed chunk must NOT report progress for itself, otherwise the
        // component's NextChunkIndex advances past it and a resume skips the unsent chunk.
        var (stream, bytes) = MakeFile(ChunkSize * 3);
        var reports = new List<TmUploadProgress>();

        Task<TmFileUploadResult> Sink(TmFileChunk chunk, CancellationToken ct)
            => Task.FromResult(new TmFileUploadResult { Success = chunk.ChunkIndex != 1, IsComplete = false });

        await TmChunkedUploader.UploadAsync(
            stream,
            new TmChunkedUploadRequest { FileName = "f", TotalSizeBytes = bytes.Length },
            Sink,
            new Progress<TmUploadProgress>(reports.Add));

        await Task.Yield();
        // Only chunk 0 (acknowledged) is reported; the failed chunk 1 is not.
        reports.Should().OnlyContain(p => p.ChunkIndex == 0);
    }
}
