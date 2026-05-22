using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Moves one positioned image above all other image objects.</summary>
public sealed class BringToFrontCommand : ImageZOrderCommandBase
{
    /// <summary>Creates an image bring-to-front command.</summary>
    public BringToFrontCommand(DocumentEditorDocument document, string blockId, string? description = null)
        : base(document, blockId, description ?? "Bring image to front")
    {
    }

    /// <inheritdoc />
    protected override int ResolveAfterZIndex()
    {
        var max = GetImageContents()
            .Select(image => image.Layout?.Stacking.ZIndex ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(BeforeZIndex, max) + 1;
    }
}
