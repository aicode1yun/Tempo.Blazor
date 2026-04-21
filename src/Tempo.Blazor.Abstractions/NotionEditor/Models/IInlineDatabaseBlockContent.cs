namespace Tempo.Blazor.NotionEditor.Models;

public interface IInlineDatabaseBlockContent : IBlockContent
{
    Guid DatabaseId { get; }
    string Title { get; }
    string? IconEmoji { get; }
    IReadOnlyList<IDatabaseField> Fields { get; }
    IReadOnlyList<IDatabaseView> Views { get; }
    Guid ActiveViewId { get; }
}
