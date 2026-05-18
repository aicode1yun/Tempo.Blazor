using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Components.DocumentEditor.Clipboard;

/// <summary>Developer-only snapshot of the latest clipboard normalization pipeline run.</summary>
public sealed class DocumentClipboardDebugSnapshot
{
    /// <summary>Raw HTML payload received from the browser clipboard.</summary>
    public string RawHtml { get; set; } = string.Empty;

    /// <summary>Raw plain-text payload received from the browser clipboard.</summary>
    public string PlainText { get; set; } = string.Empty;

    /// <summary>Detected clipboard source.</summary>
    public DocumentClipboardSource Source { get; set; } = DocumentClipboardSource.Unknown;

    /// <summary>Target editor region used for insertion policy normalization.</summary>
    public DocumentEditorRegion Region { get; set; } = DocumentEditorRegion.Body;

    /// <summary>Normalized Tempo document blocks as JSON.</summary>
    public string NormalizedJson { get; set; } = string.Empty;

    /// <summary>Non-fatal clipboard pipeline warnings.</summary>
    public IReadOnlyList<DocumentClipboardWarning> Warnings { get; set; } = [];

    /// <summary>UTC timestamp when the snapshot was captured.</summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}
