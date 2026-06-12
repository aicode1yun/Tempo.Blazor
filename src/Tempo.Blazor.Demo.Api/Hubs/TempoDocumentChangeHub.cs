using Microsoft.AspNetCore.SignalR;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.DocumentLibrary.Collaboration;

namespace Tempo.Blazor.Demo.Api.Hubs;

/// <summary>
/// SignalR hub for live document-library change notifications. Clients join per-document groups
/// and receive <c>RemoteDocumentChanged</c> when a document they display is saved/renamed/deleted.
/// </summary>
public sealed class TempoDocumentChangeHub : Hub
{
    /// <summary>Subscribes the connection to changes for one document.</summary>
    public Task JoinDocument(TempoDocumentKind kind, Guid documentId)
        => Groups.AddToGroupAsync(
            Context.ConnectionId, SignalRTempoDocumentChangeNotifier.GroupName(kind, documentId));

    /// <summary>Unsubscribes the connection from changes for one document.</summary>
    public Task LeaveDocument(TempoDocumentKind kind, Guid documentId)
        => Groups.RemoveFromGroupAsync(
            Context.ConnectionId, SignalRTempoDocumentChangeNotifier.GroupName(kind, documentId));
}
