using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Moves a positioned image object while preserving its anchor, wrap, transform, and stacking metadata.</summary>
public sealed class MoveImageObjectCommand : IDocumentEditorCommand
{
    private readonly DocumentEditorDocument _document;
    private readonly string _blockId;
    private readonly DocumentObjectPosition _startPosition;
    private readonly DocumentObjectPosition _endPosition;

    /// <summary>Creates an image object move command.</summary>
    public MoveImageObjectCommand(
        DocumentEditorDocument document,
        string blockId,
        DocumentObjectPosition startPosition,
        DocumentObjectPosition endPosition,
        string? description = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _blockId = string.IsNullOrWhiteSpace(blockId)
            ? throw new ArgumentException("The image block id is required.", nameof(blockId))
            : blockId;
        _startPosition = DocumentEditorCommandCloner.Clone(startPosition ?? throw new ArgumentNullException(nameof(startPosition)));
        _endPosition = DocumentEditorCommandCloner.Clone(endPosition ?? throw new ArgumentNullException(nameof(endPosition)));
        Description = string.IsNullOrWhiteSpace(description) ? "Move image" : description;

        if (FindImageBlock() is null)
        {
            throw new ArgumentException("The image block to move was not found.", nameof(blockId));
        }
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <summary>Image object id affected by the command.</summary>
    public string ObjectId => _blockId;

    /// <summary>Position before the move.</summary>
    public DocumentObjectPosition StartPosition => DocumentEditorCommandCloner.Clone(_startPosition);

    /// <summary>Position after the move.</summary>
    public DocumentObjectPosition EndPosition => DocumentEditorCommandCloner.Clone(_endPosition);

    /// <summary>Whether the document layout must be recomputed after applying the command.</summary>
    public bool InvalidatesLayout => true;

    /// <summary>Block ids whose layout depends on this command.</summary>
    public IReadOnlyList<string> InvalidatedBlockIds => [_blockId];

    /// <inheritdoc />
    public Task ExecuteAsync()
    {
        Apply(_endPosition);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UndoAsync()
    {
        Apply(_startPosition);
        return Task.CompletedTask;
    }

    private void Apply(DocumentObjectPosition position)
    {
        var block = FindImageBlock();
        if (block?.Content is not ImageBlockContent image)
        {
            return;
        }

        image.Layout ??= DocumentObjectLayout.Inline();
        image.Layout.Position = DocumentEditorCommandCloner.Clone(position);
    }

    private DocumentBlock? FindImageBlock()
    {
        foreach (var block in EnumerateBlocks(_document.Blocks))
        {
            if (string.Equals(block.Id, _blockId, StringComparison.Ordinal)
                && block.Content is ImageBlockContent)
            {
                return block;
            }
        }

        return null;
    }

    private static IEnumerable<DocumentBlock> EnumerateBlocks(IEnumerable<DocumentBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;

            if (block.Content is TableBlockContent table)
            {
                foreach (var row in table.Rows)
                {
                    foreach (var cell in row.Cells)
                    {
                        foreach (var nested in EnumerateBlocks(cell.Blocks))
                        {
                            yield return nested;
                        }
                    }
                }
            }
        }
    }
}
