namespace Tempo.Blazor.NotionEditor.Models;

public interface IExcerptIncludeBlockContent : IBlockContent
{
    Guid? SourcePageId { get; }
}
