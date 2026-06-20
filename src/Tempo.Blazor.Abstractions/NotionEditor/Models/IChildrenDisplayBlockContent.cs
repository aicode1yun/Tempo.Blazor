namespace Tempo.Blazor.NotionEditor.Models;

public interface IChildrenDisplayBlockContent : IBlockContent
{
    Guid? RootPageId { get; }
    int Depth { get; }
    bool ShowIcons { get; }
}
