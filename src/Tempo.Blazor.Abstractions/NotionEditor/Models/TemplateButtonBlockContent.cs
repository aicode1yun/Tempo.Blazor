namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

public class TemplateButtonBlockContent : ITemplateButtonBlockContent
{
    public string Label { get; set; } = string.Empty;
    public IReadOnlyList<IPageBlock> TemplateBlocks { get; set; } = new List<IPageBlock>();
}
