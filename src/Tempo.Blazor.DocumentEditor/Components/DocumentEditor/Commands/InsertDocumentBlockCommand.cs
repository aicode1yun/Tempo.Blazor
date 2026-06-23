using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Inserts a document block after another block or at the beginning/end of the document.</summary>
public sealed class InsertDocumentBlockCommand : IDocumentEditorCommand
{
    private readonly DocumentEditorDocument _document;
    private readonly DocumentBlock _blockSnapshot;
    private readonly string? _afterBlockId;
    private readonly bool _insertAtStart;

    /// <summary>Creates an insert block command.</summary>
    public InsertDocumentBlockCommand(
        DocumentEditorDocument document,
        DocumentBlock block,
        string? afterBlockId = null,
        bool insertAtStart = false,
        string? description = null)
    {
        _document = document;
        _blockSnapshot = DocumentEditorCommandCloner.CloneBlock(block);
        _afterBlockId = afterBlockId;
        _insertAtStart = insertAtStart;
        Description = string.IsNullOrWhiteSpace(description) ? "Insert block" : description;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task ExecuteAsync()
    {
        _document.Blocks.RemoveAll(block => block.Id == _blockSnapshot.Id);
        var block = DocumentEditorCommandCloner.CloneBlock(_blockSnapshot);
        var ordered = _document.Blocks.OrderBy(item => item.Order).ToList();
        var index = _insertAtStart
            ? -1
            : string.IsNullOrWhiteSpace(_afterBlockId)
                ? ordered.Count - 1
                : ordered.FindIndex(item => item.Id == _afterBlockId);
        var after = index >= 0 && index < ordered.Count ? ordered[index] : null;
        var nextOrder = index >= 0 && index + 1 < ordered.Count
            ? ordered[index + 1].Order
            : (after?.Order ?? 0) + 20;

        block.Order = after is null
            ? (_insertAtStart && ordered.Count > 0 ? ordered[0].Order / 2 : 10)
            : (after.Order + nextOrder) / 2;
        _document.Blocks.Add(block);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UndoAsync()
    {
        _document.Blocks.RemoveAll(block => block.Id == _blockSnapshot.Id);
        return Task.CompletedTask;
    }
}
