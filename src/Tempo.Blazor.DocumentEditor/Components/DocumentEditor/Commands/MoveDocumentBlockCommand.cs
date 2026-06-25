using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Moves a block within the document block order.</summary>
public sealed class MoveDocumentBlockCommand : IDocumentEditorCommand
{
    private readonly DocumentEditorDocument _document;
    private readonly string _blockId;
    private readonly int _sourceIndex;
    private readonly int _targetIndex;

    /// <summary>Creates a move block command.</summary>
    public MoveDocumentBlockCommand(
        DocumentEditorDocument document,
        string blockId,
        int targetIndex,
        string? description = null)
    {
        _document = document;
        _blockId = blockId;
        _sourceIndex = OrderedBlocks().FindIndex(block => block.Id == blockId);
        if (_sourceIndex < 0)
        {
            throw new ArgumentException("The block to move was not found.", nameof(blockId));
        }

        _targetIndex = Math.Max(0, targetIndex);
        Description = string.IsNullOrWhiteSpace(description) ? "Move block" : description;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task ExecuteAsync()
    {
        var current = OrderedBlocks().FindIndex(block => block.Id == _blockId);
        ApplyMove(current, _targetIndex);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UndoAsync()
    {
        var ordered = OrderedBlocks();
        var current = ordered.FindIndex(block => block.Id == _blockId);
        ApplyMove(current, _sourceIndex);
        return Task.CompletedTask;
    }

    private void ApplyMove(int from, int to)
    {
        var ordered = OrderedBlocks();
        if (from < 0 || from >= ordered.Count)
        {
            return;
        }

        var block = ordered[from];
        ordered.RemoveAt(from);
        ordered.Insert(Math.Clamp(to, 0, ordered.Count), block);
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Order = (index + 1) * 10;
        }

        _document.Blocks = ordered;
    }

    private List<DocumentBlock> OrderedBlocks() => _document.Blocks.OrderBy(block => block.Order).ToList();
}
