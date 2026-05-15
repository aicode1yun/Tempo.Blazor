namespace Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

/// <summary>Abstract base for inline content nodes.</summary>
public abstract class Inline : DocumentNode
{
    /// <summary>Formatting marks applied to this inline.</summary>
    public List<Mark> Marks { get; init; } = new();
}

/// <summary>Text run with optional formatting marks.</summary>
public class TextRun : Inline
{
    /// <summary>Plain text content.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>Hard line break within a block.</summary>
public class HardBreak : Inline
{
}

/// <summary>Tab character inline.</summary>
public class TabInline : Inline
{
}
