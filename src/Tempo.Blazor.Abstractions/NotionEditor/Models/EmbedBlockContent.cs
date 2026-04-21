namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class EmbedBlockContent : IEmbedBlockContent
{
    public string Url { get; set; } = string.Empty;
    public EmbedProvider Provider { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Caption { get; set; }
}
