using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Constraints applied while resizing an image object.</summary>
public sealed class ResizeImageObjectConstraints
{
    /// <summary>Minimum rendered image width.</summary>
    public double MinWidth { get; init; } = 24;

    /// <summary>Minimum rendered image height.</summary>
    public double MinHeight { get; init; } = 24;

    /// <summary>Optional maximum rendered image width.</summary>
    public double? MaxWidth { get; init; }

    /// <summary>Optional maximum rendered image height.</summary>
    public double? MaxHeight { get; init; }

    /// <summary>Whether the command should keep the image aspect ratio.</summary>
    public bool PreserveAspectRatio { get; init; }

    /// <summary>Optional height/width aspect ratio. When omitted, the command derives it from the previous transform.</summary>
    public double? AspectRatio { get; init; }
}

/// <summary>Resizes a positioned image object while preserving its anchor, wrap, and stacking metadata.</summary>
public sealed class ResizeImageObjectCommand : IDocumentEditorCommand
{
    private readonly DocumentEditorDocument _document;
    private readonly string _blockId;
    private readonly DocumentObjectTransform _startTransform;
    private readonly DocumentObjectTransform _endTransform;
    private readonly DocumentObjectPosition? _startPosition;
    private readonly DocumentObjectPosition? _endPosition;

    /// <summary>Creates an image object resize command.</summary>
    public ResizeImageObjectCommand(
        DocumentEditorDocument document,
        string blockId,
        DocumentObjectTransform startTransform,
        DocumentObjectTransform endTransform,
        DocumentObjectPosition? startPosition = null,
        DocumentObjectPosition? endPosition = null,
        ResizeImageObjectConstraints? constraints = null,
        string? description = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _blockId = string.IsNullOrWhiteSpace(blockId)
            ? throw new ArgumentException("The image block id is required.", nameof(blockId))
            : blockId;
        _startTransform = DocumentEditorCommandCloner.Clone(startTransform ?? throw new ArgumentNullException(nameof(startTransform)));
        _endTransform = ApplyConstraints(
            endTransform ?? throw new ArgumentNullException(nameof(endTransform)),
            _startTransform,
            constraints ?? new ResizeImageObjectConstraints());
        _startPosition = startPosition is null ? null : DocumentEditorCommandCloner.Clone(startPosition);
        _endPosition = endPosition is null ? null : DocumentEditorCommandCloner.Clone(endPosition);
        Description = string.IsNullOrWhiteSpace(description) ? "Resize image" : description;

        if (FindImageBlock() is null)
        {
            throw new ArgumentException("The image block to resize was not found.", nameof(blockId));
        }
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <summary>Image object id affected by the command.</summary>
    public string ObjectId => _blockId;

    /// <summary>Transform before the resize.</summary>
    public DocumentObjectTransform StartTransform => DocumentEditorCommandCloner.Clone(_startTransform);

    /// <summary>Transform after the resize, including applied constraints.</summary>
    public DocumentObjectTransform EndTransform => DocumentEditorCommandCloner.Clone(_endTransform);

    /// <summary>Optional position before the resize.</summary>
    public DocumentObjectPosition? StartPosition => _startPosition is null ? null : DocumentEditorCommandCloner.Clone(_startPosition);

    /// <summary>Optional position after the resize.</summary>
    public DocumentObjectPosition? EndPosition => _endPosition is null ? null : DocumentEditorCommandCloner.Clone(_endPosition);

    /// <summary>Whether the document layout must be recomputed after applying the command.</summary>
    public bool InvalidatesLayout => true;

    /// <summary>Block ids whose layout depends on this command.</summary>
    public IReadOnlyList<string> InvalidatedBlockIds => [_blockId];

    /// <inheritdoc />
    public Task ExecuteAsync()
    {
        Apply(_endTransform, _endPosition);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UndoAsync()
    {
        Apply(_startTransform, _startPosition);
        return Task.CompletedTask;
    }

    private void Apply(DocumentObjectTransform transform, DocumentObjectPosition? position)
    {
        var block = FindImageBlock();
        if (block?.Content is not ImageBlockContent image)
        {
            return;
        }

        image.Layout ??= DocumentObjectLayout.Inline();
        image.Layout.Transform = DocumentEditorCommandCloner.Clone(transform);
        if (position is not null)
        {
            image.Layout.Position = DocumentEditorCommandCloner.Clone(position);
        }

        image.Size ??= new DocumentImageSize();
        image.Size.Width = transform.Width;
        image.Size.Height = transform.Height;
        image.Size.LockAspectRatio = transform.LockAspectRatio;
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

    private static DocumentObjectTransform ApplyConstraints(
        DocumentObjectTransform requested,
        DocumentObjectTransform reference,
        ResizeImageObjectConstraints constraints)
    {
        var transform = DocumentEditorCommandCloner.Clone(requested);
        var ratio = ResolveAspectRatio(reference, requested, constraints);

        if (constraints.PreserveAspectRatio && ratio > 0)
        {
            if (transform.Width.HasValue)
            {
                transform.Height = transform.Width.Value * ratio;
            }
            else if (transform.Height.HasValue)
            {
                transform.Width = transform.Height.Value / ratio;
            }
        }

        transform.Width = Clamp(transform.Width, constraints.MinWidth, constraints.MaxWidth);
        if (constraints.PreserveAspectRatio && ratio > 0 && transform.Width.HasValue)
        {
            transform.Height = transform.Width.Value * ratio;
        }

        transform.Height = Clamp(transform.Height, constraints.MinHeight, constraints.MaxHeight);
        if (constraints.PreserveAspectRatio && ratio > 0 && transform.Height.HasValue)
        {
            transform.Width = transform.Height.Value / ratio;
            transform.Width = Clamp(transform.Width, constraints.MinWidth, constraints.MaxWidth);
            if (transform.Width.HasValue)
            {
                transform.Height = transform.Width.Value * ratio;
            }
        }

        return transform;
    }

    private static double ResolveAspectRatio(
        DocumentObjectTransform reference,
        DocumentObjectTransform requested,
        ResizeImageObjectConstraints constraints)
    {
        if (constraints.AspectRatio is > 0)
        {
            return constraints.AspectRatio.Value;
        }

        if (reference.Width is > 0 && reference.Height is > 0)
        {
            return reference.Height.Value / reference.Width.Value;
        }

        if (requested.Width is > 0 && requested.Height is > 0)
        {
            return requested.Height.Value / requested.Width.Value;
        }

        return 0;
    }

    private static double? Clamp(double? value, double min, double? max)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var clamped = Math.Max(Math.Max(1, min), value.Value);
        if (max is > 0)
        {
            clamped = Math.Min(max.Value, clamped);
        }

        return clamped;
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
