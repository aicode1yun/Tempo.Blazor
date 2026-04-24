using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Unlocks the selected nodes so they can be edited again.
/// When a group node is unlocked, all its members are unlocked as well.</summary>
public sealed class UnlockNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IEnumerable<string> _nodeIds;
    private readonly Dictionary<string, bool> _beforeStates = [];

    public UnlockNodesCommand(DiagramDocument doc, IEnumerable<string> nodeIds)
    {
        _doc = doc;
        _nodeIds = nodeIds.ToList();
        foreach (var id in _nodeIds)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;

            if (!_beforeStates.ContainsKey(id))
                _beforeStates[id] = node.IsLocked;

            // If node is a group, unlock all members too
            if (node.StencilId == "general.group")
            {
                foreach (var member in _doc.Nodes.Where(n => n.ParentGroupId == id))
                {
                    if (!_beforeStates.ContainsKey(member.Id))
                        _beforeStates[member.Id] = member.IsLocked;
                }
            }
        }
    }

    public string Name => "Unlock nodes";

    public void Execute()
    {
        foreach (var id in _beforeStates.Keys)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is not null) node.IsLocked = false;
        }
    }

    public void Undo()
    {
        foreach (var kvp in _beforeStates)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == kvp.Key);
            if (node is not null) node.IsLocked = kvp.Value;
        }
    }
}
