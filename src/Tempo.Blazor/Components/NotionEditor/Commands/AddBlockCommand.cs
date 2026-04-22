using Tempo.Blazor.NotionEditor.Commands;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Commands;

/// <summary>
/// Creates a new block in the provider and inserts it into the local block list.
/// Undo removes the block from both the provider and the local list.
/// </summary>
public sealed class AddBlockCommand : INotionCommand
{
    private readonly INotionBlockProvider _provider;
    private readonly List<IPageBlock>    _blocks;
    private readonly string              _pageId;
    private readonly IPageBlock          _template;     // prototype (Id is pre-generated)
    private readonly string?             _afterBlockId;

    private IPageBlock? _created;   // set after first ExecuteAsync, reused on redo

    public AddBlockCommand(
        INotionBlockProvider provider,
        List<IPageBlock>     blocks,
        string               pageId,
        IPageBlock           template,
        string?              afterBlockId = null)
    {
        _provider     = provider;
        _blocks       = blocks;
        _pageId       = pageId;
        _template     = template;
        _afterBlockId = afterBlockId;
    }

    public string Description => "Add block";

    public async Task ExecuteAsync()
    {
        // On redo the server-side block was already deleted by UndoAsync;
        // re-create it with the same template each time.
        _created = await _provider.CreateBlockAsync(_pageId, _template, _afterBlockId);

        var afterBlock = _afterBlockId is null
            ? null
            : _blocks.FirstOrDefault(b => b.Id.ToString() == _afterBlockId);
        var insertIdx = afterBlock is null
            ? _blocks.Count
            : _blocks.IndexOf(afterBlock) + 1;

        _blocks.Insert(Math.Clamp(insertIdx, 0, _blocks.Count), _created);
    }

    public async Task UndoAsync()
    {
        if (_created is null) return;
        await _provider.DeleteBlockAsync(_created.Id.ToString());
        _blocks.RemoveAll(b => b.Id == _created.Id);
        _created = null;   // cleared so next redo re-creates via provider
    }
}
