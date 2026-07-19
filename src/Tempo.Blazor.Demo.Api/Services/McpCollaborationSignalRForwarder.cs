using Microsoft.AspNetCore.SignalR;
using Tempo.Blazor.Demo.Api.Hubs;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Mcp.DocumentEditor;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Forwards MCP-agent collaboration publishes to the SignalR document groups: the MCP bridge
/// stores batches/cursors in the shared collaboration store, and this forwarder pushes the same
/// payloads to connected TmDocumentEditor sessions (the agent has no SignalR connection of its
/// own, so it cannot use the hub's OthersInGroup path).
/// </summary>
public sealed class McpCollaborationSignalRForwarder
{
    /// <summary>Wires the MCP collaboration callbacks onto the SignalR hub context.</summary>
    public McpCollaborationSignalRForwarder(
        TempoDocumentMcpCollaborationOptions options,
        IHubContext<DocumentEditorCollaborationHub> hub)
    {
        options.OperationPublishedCallback = (broadcast, cancellationToken) => hub.Clients
            .Group(DocumentEditorCollaborationHub.DocumentGroup(broadcast.Batch.DocumentId))
            .SendAsync(
                SignalRDocumentCollaborationProvider.HubMethods.RemoteOperationBatchReceived,
                broadcast,
                cancellationToken);

        options.CursorPublishedCallback = (cursor, cancellationToken) => hub.Clients
            .Group(DocumentEditorCollaborationHub.DocumentGroup(cursor.DocumentId))
            .SendAsync(
                SignalRDocumentCollaborationProvider.HubMethods.RemoteCursorReceived,
                cursor,
                cancellationToken);
    }
}
