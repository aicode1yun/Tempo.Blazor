using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Duplicates selected nodes with a small offset and copies their internal edges.</summary>
public sealed class DuplicateNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IEnumerable<string> _nodeIds;
    private readonly double _offsetX;
    private readonly double _offsetY;
    private readonly CopyNodesCommand _copyCommand;
    private PasteNodesCommand? _pasteCommand;

    public DuplicateNodesCommand(DiagramDocument doc, IEnumerable<string> nodeIds, double offsetX = 20, double offsetY = 20)
    {
        _doc = doc;
        _nodeIds = nodeIds.ToList();
        _offsetX = offsetX;
        _offsetY = offsetY;
        _copyCommand = new CopyNodesCommand(doc, _nodeIds);
    }

    public string Name => "Duplicate nodes";

    public void Execute()
    {
        _copyCommand.Execute();
        var json = CopyNodesCommand.SharedClipboardJson;
        if (!string.IsNullOrWhiteSpace(json))
        {
            _pasteCommand = new PasteNodesCommand(_doc, json, _offsetX, _offsetY);
            _pasteCommand.Execute();
        }
    }

    public void Undo()
    {
        _pasteCommand?.Undo();
    }
}
