using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Moves one positioned image one z-order step backward.</summary>
public sealed class SendBackwardCommand : ImageZOrderCommandBase
{
    /// <summary>Creates an image send-backward command.</summary>
    public SendBackwardCommand(DocumentEditorDocument document, string blockId, string? description = null)
        : base(document, blockId, description ?? "Send image backward")
    {
    }

    /// <inheritdoc />
    protected override int ResolveAfterZIndex() => BeforeZIndex - 1;
}
