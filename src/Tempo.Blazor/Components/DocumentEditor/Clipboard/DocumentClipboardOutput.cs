using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Clipboard;

/// <summary>Result of running clipboard content through the normalizer pipeline.</summary>
public sealed class DocumentClipboardOutput
{
    /// <summary>Normalized document blocks ready to be inserted into the editor.</summary>
    public IReadOnlyList<DocumentBlock> Blocks { get; init; } = [];

    /// <summary>Detected source that produced the output.</summary>
    public DocumentClipboardSource Source { get; init; } = DocumentClipboardSource.Unknown;

    /// <summary>Non-fatal issues encountered during normalization.</summary>
    public IReadOnlyList<DocumentClipboardWarning> Warnings { get; init; } = [];
}
