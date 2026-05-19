namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Telemetry detail emitted by the WYSIWYG watchdog during runtime recovery.</summary>
public sealed class WysiwygRuntimeRecoveryDetail
{
    /// <summary>Recovery event name, such as runtimeRecovered or runtimeRecoveryFailed.</summary>
    public string Event { get; set; } = string.Empty;

    /// <summary>Runtime area that raised the failure, such as command, remoteOperation, render, or serialization.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Current watchdog state.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>One-based recovery attempt number.</summary>
    public int Attempt { get; set; }

    /// <summary>Configured recovery attempt limit.</summary>
    public int MaxAttempts { get; set; }

    /// <summary>Backoff delay used for this attempt.</summary>
    public int BackoffMs { get; set; }

    /// <summary>Whether recovery used the last stable snapshot instead of a live snapshot.</summary>
    public bool UsedSnapshotFallback { get; set; }

    /// <summary>Error message captured from the failed runtime operation, if available.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>ISO timestamp reported by the runtime watchdog.</summary>
    public string? Timestamp { get; set; }
}
