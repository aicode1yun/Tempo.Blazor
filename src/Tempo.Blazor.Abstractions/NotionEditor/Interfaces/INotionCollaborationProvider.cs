namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

public interface INotionCollaborationProvider
{
    Task JoinPageAsync(string pageId, string userId);
    Task LeavePageAsync(string pageId, string userId);
    IObservable<BlockChange> OnBlockChanged { get; }
    IObservable<CollaboratorCursor> OnCursorMoved { get; }
    Task BroadcastBlockChangeAsync(BlockChange change);
    Task BroadcastCursorAsync(CollaboratorCursor cursor);
    Task<IEnumerable<CollaboratorCursor>> GetActiveCollaboratorsAsync(string pageId);
}
