using Tempo.Blazor.NotionEditor.Commands;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Commands;

/// <summary>
/// Applies a change to page-level metadata (title, icon, cover, flags).
/// Both <paramref name="before"/> and <paramref name="after"/> must be
/// complete snapshots — the command swaps between them on Execute / Undo.
/// </summary>
public sealed class UpdatePageCommand : INotionCommand
{
    private readonly INotionDataProvider   _provider;
    private readonly Action<INotionPage>   _applyToLocal; // updates the caller's reference in-place
    private readonly INotionPage           _before;
    private readonly INotionPage           _after;

    /// <param name="provider">Data provider for persisting page changes.</param>
    /// <param name="applyToLocal">
    /// Delegate that receives the new page snapshot and replaces the caller's
    /// local reference (e.g. <c>p => _currentPage = p</c>).
    /// </param>
    /// <param name="before">Snapshot of the page state before this change.</param>
    /// <param name="after">Snapshot of the page state after this change.</param>
    public UpdatePageCommand(
        INotionDataProvider provider,
        Action<INotionPage> applyToLocal,
        INotionPage         before,
        INotionPage         after)
    {
        _provider     = provider;
        _applyToLocal = applyToLocal;
        _before       = before;
        _after        = after;
    }

    public string Description => "Update page";

    public Task ExecuteAsync() => ApplyAsync(_after);
    public Task UndoAsync()    => ApplyAsync(_before);

    private async Task ApplyAsync(INotionPage snapshot)
    {
        await _provider.UpdatePageAsync(snapshot);
        _applyToLocal(snapshot);
    }
}
