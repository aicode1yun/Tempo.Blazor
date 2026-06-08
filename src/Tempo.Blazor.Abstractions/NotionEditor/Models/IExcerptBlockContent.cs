namespace Tempo.Blazor.NotionEditor.Models;

public interface IExcerptBlockContent : IBlockContent
{
    string? Html { get; }
}
