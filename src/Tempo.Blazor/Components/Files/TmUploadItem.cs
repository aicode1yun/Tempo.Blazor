using Microsoft.AspNetCore.Components.Forms;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Components.Files;

/// <summary>Lifecycle state of a tracked upload shown by <see cref="TmFileUploadProgress"/>.</summary>
public enum TmUploadState
{
    /// <summary>Chunks are being transferred.</summary>
    Uploading,

    /// <summary>Transfer finished; the file is being scanned.</summary>
    Scanning,

    /// <summary>Upload (and scan, if any) finished successfully.</summary>
    Completed,

    /// <summary>Upload failed and can be resumed.</summary>
    Failed,

    /// <summary>Upload was cancelled by the user and can be resumed.</summary>
    Cancelled,

    /// <summary>The uploaded file was blocked by the scan hook.</summary>
    Blocked
}

/// <summary>
/// Observable state for a single file upload: progress, lifecycle, and the context needed to
/// cancel or resume it. Held by the file components and rendered by <see cref="TmFileUploadProgress"/>.
/// </summary>
public sealed class TmUploadItem
{
    /// <summary>Stable id for this upload (used as the render key).</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Display file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Total file size in bytes.</summary>
    public long TotalBytes { get; set; }

    /// <summary>Bytes transferred so far.</summary>
    public long BytesTransferred { get; set; }

    /// <summary>Completion percentage (0–100).</summary>
    public int Percent { get; set; }

    /// <summary>Current lifecycle state.</summary>
    public TmUploadState State { get; set; } = TmUploadState.Uploading;

    /// <summary>Optional detail (e.g. failure or block reason).</summary>
    public string? Message { get; set; }

    /// <summary>True while the upload can be cancelled.</summary>
    public bool CanCancel => State is TmUploadState.Uploading;

    /// <summary>True while the upload can be resumed.</summary>
    public bool CanResume => State is TmUploadState.Failed or TmUploadState.Cancelled;

    /// <summary>True once the upload has reached a terminal, dismissable state.</summary>
    public bool IsFinished => State is TmUploadState.Completed or TmUploadState.Blocked;

    // ── Cancel/resume plumbing (not for consumers) ───────────────
    internal CancellationTokenSource? Cts { get; set; }
    internal int NextChunkIndex { get; set; }
    internal string? SessionId { get; set; }
    internal IBrowserFile? Source { get; set; }
    internal Func<TmUploadItem, Task>? ResumeAction { get; set; }

    internal void Apply(TmUploadProgress progress)
    {
        BytesTransferred = progress.BytesTransferred;
        TotalBytes = progress.TotalBytes;
        Percent = progress.Percent;
        NextChunkIndex = progress.ChunkIndex + 1;
        if (!string.IsNullOrEmpty(progress.UploadSessionId))
        {
            SessionId = progress.UploadSessionId;
        }
    }
}
