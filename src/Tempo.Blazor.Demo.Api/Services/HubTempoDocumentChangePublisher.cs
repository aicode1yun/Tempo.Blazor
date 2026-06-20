using Microsoft.AspNetCore.SignalR;
using Tempo.Blazor.Demo.Api.Hubs;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.DocumentLibrary.Collaboration;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Server-side <see cref="ITempoDocumentChangePublisher"/> that broadcasts document changes to the
/// matching <see cref="TempoDocumentChangeHub"/> group, so subscribed clients refresh live.
/// </summary>
public sealed class HubTempoDocumentChangePublisher : ITempoDocumentChangePublisher
{
    private readonly IHubContext<TempoDocumentChangeHub> _hub;

    public HubTempoDocumentChangePublisher(IHubContext<TempoDocumentChangeHub> hub) => _hub = hub;

    public Task PublishAsync(TempoDocumentChange change, CancellationToken cancellationToken = default)
        => _hub.Clients
            .Group(SignalRTempoDocumentChangeNotifier.GroupName(change.Kind, change.DocumentId))
            .SendAsync(
                SignalRTempoDocumentChangeNotifier.HubMethods.RemoteDocumentChanged,
                change,
                cancellationToken);
}
