using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Tempo.Blazor.Demo.Api.Hubs;
using Tempo.Blazor.Demo.Api.Services;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.DocumentLibrary.Collaboration;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>
/// Tests that <see cref="HubTempoDocumentChangePublisher"/> broadcasts to the correct
/// per-document group with the agreed client event name.
/// </summary>
public sealed class HubTempoDocumentChangePublisherTests
{
    [Fact]
    public async Task PublishAsync_SendsToDocumentGroup_WithRemoteChangedEvent()
    {
        var clientProxy = Substitute.For<IClientProxy>();
        var clients = Substitute.For<IHubClients>();
        var hubContext = Substitute.For<IHubContext<TempoDocumentChangeHub>>();
        hubContext.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(clientProxy);

        var publisher = new HubTempoDocumentChangePublisher(hubContext);
        var id = Guid.NewGuid();
        var change = new TempoDocumentChange
        {
            Kind = TempoDocumentKind.Wireframe,
            DocumentId = id,
            ChangeType = TempoDocumentChangeType.Saved,
            ModifiedAt = DateTime.UtcNow
        };

        await publisher.PublishAsync(change);

        var expectedGroup = SignalRTempoDocumentChangeNotifier.GroupName(TempoDocumentKind.Wireframe, id);
        clients.Received(1).Group(expectedGroup);
        await clientProxy.Received(1).SendCoreAsync(
            SignalRTempoDocumentChangeNotifier.HubMethods.RemoteDocumentChanged,
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], change)),
            Arg.Any<CancellationToken>());
    }
}
