using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Deletes a block and restores its previous position on undo.</summary>
public sealed class DeleteDocumentBlockCommand : IDocumentEditorCommand
{
    private readonly DocumentEditorDocument _document;
    private readonly DocumentBlock _snapshot;
    private readonly string? _afterBlockId;
    private readonly bool _wasFirst;

    /// <summary>Creates a delete block command.</summary>
    public DeleteDocumentBlockCommand(DocumentEditorDocument document, string blockId, string? description = null)
    {
        _document = document;
        var ordered = document.Blocks.OrderBy(block => block.Order).ToList();
        var index = ordered.FindIndex(block => block.Id == blockId);
        if (index < 0)
        {
            throw new ArgumentException("The block to delete was not found.", nameof(blockId));
        }

        _snapshot = DocumentEditorCommandCloner.CloneBlock(ordered[index]);
        _afterBlockId = index > 0 ? ordered[index - 1].Id : null;
        _wasFirst = index == 0;
        Description = string.IsNullOrWhiteSpace(description) ? "Delete block" : description;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task ExecuteAsync()
    {
        _document.Blocks.RemoveAll(block => block.Id == _snapshot.Id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UndoAsync()
    {
        var insert = new InsertDocumentBlockCommand(_document, _snapshot, _afterBlockId, _wasFirst, Description);
        return insert.ExecuteAsync();
    }
}
