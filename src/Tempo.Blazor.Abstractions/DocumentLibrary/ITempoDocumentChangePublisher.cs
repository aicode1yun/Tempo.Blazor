namespace Tempo.Blazor.DocumentLibrary;

/// <summary>
/// Server-side (or write-side) publication of document change notifications. Invoked after
/// a document is saved, renamed or deleted — by the store, an MCP write tool, or a save flow.
/// </summary>
public interface ITempoDocumentChangePublisher
{
    /// <summary>Broadcasts a change to all interested subscribers.</summary>
    Task PublishAsync(TempoDocumentChange change, CancellationToken cancellationToken = default);
}
