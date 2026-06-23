namespace Tempo.Blazor.Components.DocumentEditor.Clipboard;

/// <summary>Non-fatal issue encountered while normalizing clipboard content.</summary>
public sealed class DocumentClipboardWarning
{
    /// <summary>Machine-readable warning code, e.g. "stripped-element" or "unsupported-style".</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable description of what was changed or removed.</summary>
    public string Message { get; init; } = string.Empty;
}
