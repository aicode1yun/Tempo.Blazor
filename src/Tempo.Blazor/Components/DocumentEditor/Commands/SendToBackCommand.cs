using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Moves one positioned image below all other image objects.</summary>
public sealed class SendToBackCommand : ImageZOrderCommandBase
{
    /// <summary>Creates an image send-to-back command.</summary>
    public SendToBackCommand(DocumentEditorDocument document, string blockId, string? description = null)
        : base(document, blockId, description ?? "Send image to back")
    {
    }

    /// <inheritdoc />
    protected override int ResolveAfterZIndex()
    {
        var min = GetImageContents()
            .Select(image => image.Layout?.Stacking.ZIndex ?? 0)
            .DefaultIfEmpty(0)
            .Min();
        return Math.Min(BeforeZIndex, min) - 1;
    }
}
