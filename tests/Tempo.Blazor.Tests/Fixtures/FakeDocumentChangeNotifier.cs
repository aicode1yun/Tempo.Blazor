using System.Collections.Concurrent;
using Tempo.Blazor.DocumentLibrary;

namespace Tempo.Blazor.Tests.Fixtures;

/// <summary>
/// Test fake for <see cref="ITempoDocumentChangeNotifier"/> that records subscriptions and lets a
/// test raise a change to simulate a remote edit.
/// </summary>
public sealed class FakeDocumentChangeNotifier : ITempoDocumentChangeNotifier
{
    private readonly ConcurrentDictionary<(TempoDocumentKind, Guid), int> _subscribed = new();

    public event Func<TempoDocumentChange, CancellationToken, Task>? Changed;

    public IReadOnlyCollection<(TempoDocumentKind Kind, Guid Id)> Subscriptions
        => _subscribed.Keys.ToList();

    public bool IsSubscribed(TempoDocumentKind kind, Guid id) => _subscribed.ContainsKey((kind, id));

    public Task SubscribeAsync(TempoDocumentKind kind, Guid documentId, CancellationToken ct = default)
    {
        _subscribed[(kind, documentId)] = 1;
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(TempoDocumentKind kind, Guid documentId, CancellationToken ct = default)
    {
        _subscribed.TryRemove((kind, documentId), out _);
        return Task.CompletedTask;
    }

    /// <summary>Raises <see cref="Changed"/> for all handlers, as a remote edit would.</summary>
    public async Task RaiseAsync(TempoDocumentChange change)
    {
        var handler = Changed;
        if (handler is not null)
        {
            await handler(change, CancellationToken.None);
        }
    }
}
