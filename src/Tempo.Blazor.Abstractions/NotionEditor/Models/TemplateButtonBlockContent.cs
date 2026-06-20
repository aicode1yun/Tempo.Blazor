namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

public class TemplateButtonBlockContent : ITemplateButtonBlockContent
{
    public string Label { get; set; } = string.Empty;
    // Concrete type so STJ can deserialize without needing [JsonPolymorphic] on IPageBlock
    public List<PageBlock> TemplateBlocks { get; set; } = [];

    IReadOnlyList<IPageBlock> ITemplateButtonBlockContent.TemplateBlocks => TemplateBlocks;
}
