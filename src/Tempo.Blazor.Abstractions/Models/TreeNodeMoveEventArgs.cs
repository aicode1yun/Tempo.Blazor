using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Models;

/// <summary>
/// Event arguments raised when a TmTreeView node is dragged onto another node or the root drop zone.
/// </summary>
/// <typeparam name="TKey">The type of the tree node identifier.</typeparam>
public sealed class TreeNodeMoveEventArgs<TKey>
{
    /// <summary>The node that was dragged.</summary>
    public ITreeNode<TKey> MovedNode { get; init; } = default!;

    /// <summary>
    /// The node onto which <see cref="MovedNode"/> was dropped, becoming its new parent.
    /// <c>null</c> means the node was dropped on the root drop zone and should become a root node.
    /// </summary>
    public ITreeNode<TKey>? NewParent { get; init; }
}
