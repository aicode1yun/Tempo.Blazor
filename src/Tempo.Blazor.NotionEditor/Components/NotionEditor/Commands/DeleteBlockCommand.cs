using Tempo.Blazor.NotionEditor.Commands;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Commands;

/// <summary>
/// Removes a block and everything nested under it, and puts the whole subtree back on undo.
/// The blocks are restored with the ids they had — re-creating them would mint new ones, and a
/// restored table would no longer own the rows that still point at its old id.
/// </summary>
public sealed class DeleteBlockCommand : INotionCommand
{
    private readonly INotionBlockProvider _provider;
    private readonly List<IPageBlock>     _blocks;
    private readonly IPageBlock           _snapshot;
    private readonly IPageBlock[]         _descendants;

    /// <param name="descendants">
    /// The block's subtree, captured before the deletion. Empty for a block that has no children.
    /// </param>
    public DeleteBlockCommand(
        INotionBlockProvider     provider,
        List<IPageBlock>         blocks,
        string                   pageId,
        IPageBlock               block,
        IEnumerable<IPageBlock>? descendants = null)
    {
        _provider    = provider;
        _blocks      = blocks;
        _snapshot    = block;
        _descendants = descendants?.ToArray() ?? [];
        PageId       = pageId;
    }

    /// <summary>The page the deleted block belongs to.</summary>
    public string PageId { get; }

    public string Description => "Delete block";

    public async Task ExecuteAsync()
    {
        await _provider.DeleteBlockAsync(_snapshot.Id.ToString());
        _blocks.RemoveAll(block => block.Id == _snapshot.Id);
    }

    public async Task UndoAsync()
    {
        await _provider.RestoreBlocksAsync([_snapshot, .. _descendants]);

        // The page's list holds the top level only; a container reloads its own children.
        if (_snapshot.ParentBlockId is not null || _blocks.Any(block => block.Id == _snapshot.Id)) return;

        var insertIdx = _blocks.FindIndex(block => block.Order > _snapshot.Order);
        _blocks.Insert(insertIdx < 0 ? _blocks.Count : insertIdx, _snapshot);
    }
}
