using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Updates one block content payload and stores before/after snapshots.</summary>
public sealed class UpdateDocumentBlockCommand : IDocumentEditorCommand
{
    private readonly DocumentEditorDocument _document;
    private readonly string _blockId;
    private readonly DocumentBlockContent _before;
    private readonly DocumentBlockContent _after;

    /// <summary>Creates an update block command.</summary>
    public UpdateDocumentBlockCommand(
        DocumentEditorDocument document,
        string blockId,
        DocumentBlockContent before,
        DocumentBlockContent after,
        string? description = null)
    {
        _document = document;
        _blockId = blockId;
        _before = DocumentEditorCommandCloner.CloneContent(before);
        _after = DocumentEditorCommandCloner.CloneContent(after);
        Description = string.IsNullOrWhiteSpace(description) ? "Update block" : description;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task ExecuteAsync()
    {
        Apply(_after);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UndoAsync()
    {
        Apply(_before);
        return Task.CompletedTask;
    }

    private void Apply(DocumentBlockContent content)
    {
        var block = _document.Blocks.FirstOrDefault(item => item.Id == _blockId);
        if (block is null)
        {
            return;
        }

        block.Content = DocumentEditorCommandCloner.CloneContent(content);
    }
}
