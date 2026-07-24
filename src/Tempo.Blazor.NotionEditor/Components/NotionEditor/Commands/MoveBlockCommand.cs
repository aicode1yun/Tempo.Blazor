using Tempo.Blazor.NotionEditor.Commands;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using PageBlock = Tempo.Blazor.NotionEditor.Interfaces.PageBlock;

namespace Tempo.Blazor.Components.NotionEditor.Commands;

/// <summary>
/// Reorders a block within the page by moving it from <paramref name="sourceIndex"/>
/// to <paramref name="targetIndex"/> in the local list and persisting the new order.
/// Undo restores the original order.
/// </summary>
public sealed class MoveBlockCommand : INotionCommand
{
    private readonly INotionEditorBlockService _provider;
    private readonly List<IPageBlock>    _blocks;
    private readonly string              _pageId;
    private readonly int                 _sourceIndex;
    private readonly int                 _targetIndex;

    public MoveBlockCommand(
        INotionEditorBlockService provider,
        List<IPageBlock>     blocks,
        string               pageId,
        int                  sourceIndex,
        int                  targetIndex)
    {
        _provider    = provider;
        _blocks      = blocks;
        _pageId      = pageId;
        _sourceIndex = sourceIndex;
        _targetIndex = targetIndex;
    }

    public string Description => "Move block";

    public async Task ExecuteAsync()
    {
        ApplyMove(_sourceIndex, _targetIndex);
        await PersistOrderAsync();
    }

    public async Task UndoAsync()
    {
        // Reverse the move: the block is now at the normalised target; move it back.
        var normalised = Normalise(_sourceIndex, _targetIndex);
        ApplyMove(normalised, _sourceIndex < _targetIndex ? _sourceIndex : _sourceIndex + 1);
        await PersistOrderAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplyMove(int from, int to)
    {
        if (from < 0 || from >= _blocks.Count) return;
        var block     = _blocks[from];
        var normTo    = Normalise(from, to);
        _blocks.RemoveAt(from);
        _blocks.Insert(Math.Clamp(normTo, 0, _blocks.Count), block);
        RenumberOrder();
    }

    private async Task PersistOrderAsync() =>
        await _provider.ReorderBlocksAsync(_pageId, _blocks.Select(b => b.Id.ToString()));

    private void RenumberOrder()
    {
        for (var i = 0; i < _blocks.Count; i++)
        {
            if (_blocks[i] is PageBlock pb)
                pb.Order = i;
        }
    }

    // Mirrors the normalisation used in TmNotionPage.ReorderBlocksAsync.
    private static int Normalise(int from, int to) =>
        from < to ? to - 1 : to;
}
