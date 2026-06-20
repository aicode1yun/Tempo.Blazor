namespace Tempo.Blazor.NotionEditor.Models;

public interface ITextAnchorComment : IBlockComment
{
    int StartOffset { get; }
    int EndOffset { get; }
    string HighlightedText { get; }
}
