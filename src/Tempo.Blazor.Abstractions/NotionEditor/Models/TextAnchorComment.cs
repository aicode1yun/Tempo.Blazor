namespace Tempo.Blazor.NotionEditor.Models;

public class TextAnchorComment : BlockComment, ITextAnchorComment
{
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public string HighlightedText { get; set; } = string.Empty;
}
