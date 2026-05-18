namespace Tempo.Blazor.Components.DocumentEditor.Clipboard;

/// <summary>Raw clipboard data captured when the user pastes into the editor.</summary>
public sealed class DocumentClipboardInput
{
    /// <summary>HTML content from the clipboard, or <see langword="null"/> if not present.</summary>
    public string? Html { get; init; }

    /// <summary>Plain-text content from the clipboard, or <see langword="null"/> if not present.</summary>
    public string? PlainText { get; init; }

    /// <summary>Detected source application.</summary>
    public DocumentClipboardSource Source { get; init; } = DocumentClipboardSource.Unknown;

    /// <summary>File names or URIs attached to the clipboard event (e.g. image files).</summary>
    public IReadOnlyList<string> Files { get; init; } = [];
}
