using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates data properties of a single node.</summary>
public sealed class UpdateNodeDataCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _nodeId;
    private readonly Dictionary<string, object> _oldData;
    private readonly Dictionary<string, object> _newData;

    public UpdateNodeDataCommand(
        DiagramDocument doc,
        string nodeId,
        Dictionary<string, object> oldData,
        Dictionary<string, object> newData)
    {
        _doc = doc;
        _nodeId = nodeId;
        _oldData = oldData;
        _newData = newData;
    }

    public string Name => "Update node";

    public void Execute()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null) return;
        node.Data = DeepCopy(_newData);
    }

    public void Undo()
    {
        var node = _doc.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is null) return;
        node.Data = DeepCopy(_oldData);
    }

    private static Dictionary<string, object> DeepCopy(Dictionary<string, object> source)
        => source.ToDictionary(item => item.Key, item => CloneValue(item.Value));

    private static object CloneValue(object value)
        => value switch
        {
            JsonElement element => element.Clone(),
            Dictionary<string, object> dictionary => DeepCopy(dictionary),
            IEnumerable<string> strings => strings.ToList(),
            IEnumerable<object> objects => objects.Select(CloneValue).ToList(),
            _ => value
        };
}
