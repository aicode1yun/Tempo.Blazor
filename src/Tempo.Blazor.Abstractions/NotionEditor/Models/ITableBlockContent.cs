using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.NotionEditor.Models;

public interface ITableBlockContent : IBlockContent
{
    bool HasHeaderRow { get; }
    bool HasHeaderColumn { get; }
    bool HasColumnHeader { get; }
    bool HasRowHeader { get; }
    int ColumnCount { get; }

    /// <summary>Per-column horizontal alignment, indexed by column. Empty means no explicit alignment.</summary>
    IReadOnlyList<TableColumnAlignment> ColumnAlignments => [];
}
