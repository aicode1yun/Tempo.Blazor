namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Text format used by a comment entry body.</summary>
public enum TmCommentBodyFormat
{
    /// <summary>Plain text that should be HTML-encoded by renderers.</summary>
    PlainText,

    /// <summary>Host-sanitized HTML. The host application remains responsible for sanitization.</summary>
    Html,

    /// <summary>Markdown text rendered by the consuming application.</summary>
    Markdown
}
