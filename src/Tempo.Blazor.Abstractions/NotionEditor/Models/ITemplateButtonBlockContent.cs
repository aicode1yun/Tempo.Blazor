namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

public interface ITemplateButtonBlockContent : IBlockContent
{
    string Label { get; }
    IReadOnlyList<IPageBlock> TemplateBlocks { get; }
}
