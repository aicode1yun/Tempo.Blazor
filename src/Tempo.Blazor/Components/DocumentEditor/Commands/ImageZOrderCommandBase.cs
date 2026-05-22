using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Base class for undoable image object z-order commands.</summary>
public abstract class ImageZOrderCommandBase : IDocumentEditorCommand
{
    private readonly DocumentEditorDocument _document;
    private readonly string _blockId;
    private readonly int _beforeZIndex;
    private int? _afterZIndex;

    /// <summary>Creates an image z-order command.</summary>
    protected ImageZOrderCommandBase(DocumentEditorDocument document, string blockId, string description)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _blockId = string.IsNullOrWhiteSpace(blockId)
            ? throw new ArgumentException("The image block id is required.", nameof(blockId))
            : blockId;
        Description = string.IsNullOrWhiteSpace(description) ? "Change image order" : description;

        var image = FindImageContent(_blockId)
            ?? throw new ArgumentException("The image block was not found.", nameof(blockId));
        _beforeZIndex = image.Layout?.Stacking.ZIndex ?? 0;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <summary>Image object id affected by the command.</summary>
    public string ObjectId => _blockId;

    /// <summary>Z-index before the command.</summary>
    public int BeforeZIndex => _beforeZIndex;

    /// <summary>Z-index after the command, available after execution.</summary>
    public int? AfterZIndex => _afterZIndex;

    /// <summary>Whether the document layout must be recomputed after applying the command.</summary>
    public bool InvalidatesLayout => true;

    /// <summary>Block ids whose layout depends on this command.</summary>
    public IReadOnlyList<string> InvalidatedBlockIds => [_blockId];

    /// <inheritdoc />
    public Task ExecuteAsync()
    {
        _afterZIndex ??= ResolveAfterZIndex();
        Apply(_afterZIndex.Value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UndoAsync()
    {
        Apply(_beforeZIndex);
        return Task.CompletedTask;
    }

    /// <summary>Resolves the target z-index when the command is first executed.</summary>
    protected abstract int ResolveAfterZIndex();

    /// <summary>Returns all image contents in document order.</summary>
    protected IReadOnlyList<ImageBlockContent> GetImageContents()
        => EnumerateBlocks(_document.Blocks)
            .Select(block => block.Content)
            .OfType<ImageBlockContent>()
            .ToList();

    private void Apply(int zIndex)
    {
        var image = FindImageContent(_blockId);
        if (image is null)
        {
            return;
        }

        image.Layout ??= DocumentObjectLayout.Inline();
        image.Layout.Stacking ??= new DocumentObjectStacking();
        image.Layout.Stacking.ZIndex = zIndex;
    }

    private ImageBlockContent? FindImageContent(string blockId)
    {
        foreach (var block in EnumerateBlocks(_document.Blocks))
        {
            if (string.Equals(block.Id, blockId, StringComparison.Ordinal)
                && block.Content is ImageBlockContent image)
            {
                return image;
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
