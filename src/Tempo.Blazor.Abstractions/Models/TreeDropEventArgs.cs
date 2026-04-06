using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Models;

/// <summary>
/// Event arguments raised when items are dropped onto a TmTreeView node.
/// </summary>
/// <typeparam name="TKey">The type of the tree node identifier.</typeparam>
public sealed class TreeDropEventArgs<TKey>
{
    /// <summary>The node onto which the items were dropped.</summary>
    public ITreeNode<TKey> TargetNode { get; init; } = default!;

    /// <summary>IDs of the dragged items, as supplied by <see cref="Services.DragDropService"/>.</summary>
    public IReadOnlyList<string> DraggedIds { get; init; } = [];
}
