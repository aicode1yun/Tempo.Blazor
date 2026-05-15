namespace Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

/// <summary>Represents a text selection within the document.</summary>
public class DocumentSelection
{
    /// <summary>Selection anchor (where selection started).</summary>
    public DocumentPosition Anchor { get; set; } = new();

    /// <summary>Selection focus (where cursor currently is).</summary>
    public DocumentPosition Focus { get; set; } = new();

    /// <summary>True when anchor and focus are at the same position.</summary>
    public bool IsCollapsed => Anchor.BlockId == Focus.BlockId
        && Anchor.InlineIndex == Focus.InlineIndex
        && Anchor.TextOffset == Focus.TextOffset;

    /// <summary>True when focus is after anchor (or same position).</summary>
    public bool IsForward => IsPositionBeforeOrEqual(Anchor, Focus);

    /// <summary>The start position of the selection (min of anchor/focus).</summary>
    public DocumentPosition Start => IsForward ? Anchor : Focus;

    /// <summary>The end position of the selection (max of anchor/focus).</summary>
    public DocumentPosition End => IsForward ? Focus : Anchor;

    private static bool IsPositionBeforeOrEqual(DocumentPosition a, DocumentPosition b)
    {
        if (a.BlockId != b.BlockId)
            return string.Compare(a.BlockId, b.BlockId, StringComparison.Ordinal) <= 0;
        if (a.InlineIndex != b.InlineIndex)
            return a.InlineIndex <= b.InlineIndex;
        return a.TextOffset <= b.TextOffset;
    }
}

/// <summary>A specific position within the document tree.</summary>
public class DocumentPosition
{
    /// <summary>Block identifier.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Index of the inline within the block.</summary>
    public int InlineIndex { get; set; } = 0;

    /// <summary>Character offset within the inline text.</summary>
    public int TextOffset { get; set; } = 0;
}
