namespace Tempo.Blazor.NotionEditor.Models;

public sealed class ChildrenDisplayBlockContent : IChildrenDisplayBlockContent
{
    private int _depth;

    public Guid? RootPageId { get; set; }

    public int Depth
    {
        get => _depth;
        set => _depth = value is >= 0 and <= 10 ? value : 0;
    }

    public bool ShowIcons { get; set; } = true;
}
