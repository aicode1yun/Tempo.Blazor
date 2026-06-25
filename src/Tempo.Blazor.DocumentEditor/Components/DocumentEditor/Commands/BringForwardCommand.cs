using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Moves one positioned image one z-order step forward.</summary>
public sealed class BringForwardCommand : ImageZOrderCommandBase
{
    /// <summary>Creates an image bring-forward command.</summary>
    public BringForwardCommand(DocumentEditorDocument document, string blockId, string? description = null)
        : base(document, blockId, description ?? "Bring image forward")
    {
    }

    /// <inheritdoc />
    protected override int ResolveAfterZIndex() => BeforeZIndex + 1;
}
