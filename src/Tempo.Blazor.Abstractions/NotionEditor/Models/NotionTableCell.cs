namespace Tempo.Blazor.NotionEditor.Models;

public class NotionTableCell : INotionTableCell
{
    public string Html { get; set; } = string.Empty;
    public int ColSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public string? BackgroundColor { get; set; }
    public bool IsMergeHidden { get; set; }
    public int MergeOriginRow { get; set; } = -1;
    public int MergeOriginColumn { get; set; } = -1;

    public NotionTableCell Clone() => new()
    {
        Html = Html,
        ColSpan = Math.Max(1, ColSpan),
        RowSpan = Math.Max(1, RowSpan),
        BackgroundColor = string.IsNullOrWhiteSpace(BackgroundColor) ? null : BackgroundColor,
        IsMergeHidden = IsMergeHidden,
        MergeOriginRow = MergeOriginRow,
        MergeOriginColumn = MergeOriginColumn
    };
}
