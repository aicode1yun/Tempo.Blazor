namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IEmbedBlockContent : IBlockContent
{
    string Url { get; }
    EmbedProvider Provider { get; }
    int? Width { get; }
    int? Height { get; }
    string? Caption { get; }
}
