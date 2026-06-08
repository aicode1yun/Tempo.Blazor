namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;

public record BlockDiff(string BlockId, BlockDiffType DiffType, IPageBlock? Before, IPageBlock? After)
{
    public int? BeforeOrder => Before?.Order;
    public int? AfterOrder => After?.Order;
}
