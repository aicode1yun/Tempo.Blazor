namespace Tempo.Blazor.DocumentLibrary;

/// <summary>
/// Client-side subscription to document change notifications. A consumer (an open editor,
/// or an embedded block such as a NotionEditor wireframe block) subscribes to the specific
/// documents it displays and refreshes when <see cref="Changed"/> fires for one of them.
/// </summary>
public interface ITempoDocumentChangeNotifier
{
    /// <summary>
    /// Raised when a subscribed document changes. Implementations only deliver changes for
    /// (kind, id) pairs that have been subscribed via <see cref="SubscribeAsync"/>.
    /// </summary>
    event Func<TempoDocumentChange, CancellationToken, Task>? Changed;

    /// <summary>Starts receiving change notifications for the given document.</summary>
    Task SubscribeAsync(
        TempoDocumentKind kind, Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Stops receiving change notifications for the given document.</summary>
    Task UnsubscribeAsync(
        TempoDocumentKind kind, Guid documentId, CancellationToken cancellationToken = default);
}
