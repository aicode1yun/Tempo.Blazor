using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Digest recipients = whoever currently has notifications in the in-memory store.</summary>
public sealed class DemoNotificationRecipientSource : INotificationRecipientSource
{
    private readonly InMemoryNotificationStore _store;

    public DemoNotificationRecipientSource(InMemoryNotificationStore store) => _store = store;

    public Task<IReadOnlyList<TmUserRef>> GetRecipientsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_store.GetKnownRecipients());
}
