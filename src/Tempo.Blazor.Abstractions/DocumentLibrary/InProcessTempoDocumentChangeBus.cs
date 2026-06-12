using System.Collections.Concurrent;

namespace Tempo.Blazor.DocumentLibrary;

/// <summary>
/// In-process implementation of the document change channel: writers call
/// <see cref="PublishAsync"/> and subscribers receive <see cref="Changed"/> for documents
/// they registered via <see cref="SubscribeAsync"/>. Suitable for single-process scenarios
/// and as the in-process transport behind a SignalR adapter.
/// </summary>
public sealed class InProcessTempoDocumentChangeBus
    : ITempoDocumentChangeNotifier, ITempoDocumentChangePublisher
{
    private readonly ConcurrentDictionary<(TempoDocumentKind Kind, Guid Id), byte> _subscriptions = new();

    /// <inheritdoc />
    public event Func<TempoDocumentChange, CancellationToken, Task>? Changed;

    /// <inheritdoc />
    public Task SubscribeAsync(
        TempoDocumentKind kind, Guid documentId, CancellationToken cancellationToken = default)
    {
        _subscriptions[(kind, documentId)] = 0;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(
        TempoDocumentKind kind, Guid documentId, CancellationToken cancellationToken = default)
    {
        _subscriptions.TryRemove((kind, documentId), out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        TempoDocumentChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (!_subscriptions.ContainsKey((change.Kind, change.DocumentId)))
        {
            return;
        }

        var handler = Changed;
        if (handler is not null)
        {
            await handler(change, cancellationToken).ConfigureAwait(false);
        }
    }
}
