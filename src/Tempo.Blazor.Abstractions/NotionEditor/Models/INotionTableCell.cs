namespace Tempo.Blazor.NotionEditor.Models;

public interface INotionTableCell
{
    string Html { get; }
    int ColSpan { get; }
    int RowSpan { get; }
    string? BackgroundColor { get; }
    string? TextColor { get; }
    NotionTableHorizontalAlignment HorizontalAlignment { get; }
    NotionTableVerticalAlignment VerticalAlignment { get; }
    double? Width { get; }
    NotionTableCellBorders Borders { get; }
    IReadOnlyList<NotionRichTextInline> Inlines { get; }
    bool IsMergeHidden { get; }
    int MergeOriginRow { get; }
    int MergeOriginColumn { get; }
}
