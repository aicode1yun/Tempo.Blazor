namespace Tempo.Blazor.Services;

/// <summary>Identifies which component started the current drag operation.</summary>
public enum DragSource
{
    /// <summary>Drag originated from a TmMultiViewList item.</summary>
    MultiViewList,

    /// <summary>Drag originated from a TmTreeView node.</summary>
    TreeView,
}

/// <summary>
/// Scoped service that carries dragged item IDs between sibling components
/// (e.g. TmMultiViewList as drag source and TmTreeView as drop target).
/// </summary>
public class DragDropService
{
    /// <summary>IDs of items currently being dragged. Null when no drag is in progress.</summary>
    public IReadOnlyList<string>? DraggedIds { get; private set; }

    /// <summary>Which component started the current drag. Null when not dragging.</summary>
    public DragSource? Source { get; private set; }

    /// <summary>True while a drag operation is active.</summary>
    public bool IsDragging => DraggedIds is not null;

    /// <summary>Called by the drag source when dragging starts.</summary>
    public void StartDrag(IEnumerable<string> ids, DragSource source = DragSource.MultiViewList)
    {
        DraggedIds = ids.ToList();
        Source = source;
    }

    /// <summary>Called by the drag source when dragging ends (drop or cancel).</summary>
    public void EndDrag()
    {
        DraggedIds = null;
        Source = null;
    }
}
