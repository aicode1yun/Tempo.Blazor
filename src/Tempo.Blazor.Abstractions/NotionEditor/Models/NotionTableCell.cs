namespace Tempo.Blazor.NotionEditor.Models;

public class NotionTableCell : INotionTableCell
{
    public string Html { get; set; } = string.Empty;

    /// <summary>
    /// Sanitized presentation HTML derived from structured inlines. It is never serialized back
    /// into the canonical aggregate.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? DisplayHtml { get; set; }
    public int ColSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public NotionTableHorizontalAlignment HorizontalAlignment { get; set; } =
        NotionTableHorizontalAlignment.Left;
    public NotionTableVerticalAlignment VerticalAlignment { get; set; } =
        NotionTableVerticalAlignment.Top;
    public double? Width { get; set; }
    public NotionTableCellBorders Borders { get; set; } = new();
    public IReadOnlyList<NotionRichTextInline> Inlines { get; set; } = [];
    public bool IsMergeHidden { get; set; }
    public int MergeOriginRow { get; set; } = -1;
    public int MergeOriginColumn { get; set; } = -1;

    public NotionTableCell Clone() => new()
    {
        Html = Html,
        DisplayHtml = DisplayHtml,
        ColSpan = Math.Max(1, ColSpan),
        RowSpan = Math.Max(1, RowSpan),
        BackgroundColor = string.IsNullOrWhiteSpace(BackgroundColor) ? null : BackgroundColor,
        TextColor = string.IsNullOrWhiteSpace(TextColor) ? null : TextColor,
        HorizontalAlignment = HorizontalAlignment,
        VerticalAlignment = VerticalAlignment,
        Width = Width,
        Borders = new NotionTableCellBorders
        {
            Top = CloneBorder(Borders.Top),
            Right = CloneBorder(Borders.Right),
            Bottom = CloneBorder(Borders.Bottom),
            Left = CloneBorder(Borders.Left)
        },
        Inlines = Inlines.Select(inline => new NotionRichTextInline
        {
            Text = inline.Text,
            Href = inline.Href,
            Bold = inline.Bold,
            Italic = inline.Italic,
            Underline = inline.Underline,
            Strikethrough = inline.Strikethrough,
            Code = inline.Code,
            TextColor = inline.TextColor,
            BackgroundColor = inline.BackgroundColor
        }).ToList(),
        IsMergeHidden = IsMergeHidden,
        MergeOriginRow = MergeOriginRow,
        MergeOriginColumn = MergeOriginColumn
    };

    private static NotionTableBorder? CloneBorder(NotionTableBorder? border)
        => border is null
            ? null
            : new NotionTableBorder
            {
                Style = border.Style,
                Color = border.Color,
                Width = border.Width
            };
}
