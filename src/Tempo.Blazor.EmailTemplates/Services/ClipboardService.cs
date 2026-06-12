using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Services;

/// <summary>
/// In-memory, block-level clipboard for the editor. Copy stores an independent snapshot; paste returns
/// a fresh copy with new identifiers each time, so repeated pastes never collide.
/// </summary>
public sealed class ClipboardService
{
    private EmailBlockBase? _clipboard;

    /// <summary>Gets whether a block is available to paste.</summary>
    public bool CanPaste => _clipboard is not null;

    /// <summary>Stores an independent copy of the block on the clipboard.</summary>
    public void Copy(EmailBlockBase block) => _clipboard = block.Clone();

    /// <summary>Returns a fresh copy (with new ids) of the clipboard block, or <see langword="null"/> if empty.</summary>
    public EmailBlockBase? Paste() => _clipboard?.CloneWithNewIds();

    /// <summary>Clears the clipboard.</summary>
    public void Clear() => _clipboard = null;
}
