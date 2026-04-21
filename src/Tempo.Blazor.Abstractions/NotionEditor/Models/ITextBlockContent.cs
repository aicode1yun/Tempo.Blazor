namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface ITextBlockContent : IBlockContent
{
    string Html { get; }
    IReadOnlyList<Mention> Mentions { get; }
    string? BackgroundColor { get; }
    string? TextColor { get; }
    TextAlignment Alignment { get; }
}
