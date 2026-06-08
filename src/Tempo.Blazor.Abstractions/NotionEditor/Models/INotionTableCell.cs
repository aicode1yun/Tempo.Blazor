namespace Tempo.Blazor.NotionEditor.Models;

public interface INotionTableCell
{
    string Html { get; }
    int ColSpan { get; }
    int RowSpan { get; }
    string? BackgroundColor { get; }
    bool IsMergeHidden { get; }
    int MergeOriginRow { get; }
    int MergeOriginColumn { get; }
}
