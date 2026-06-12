using Tempo.Blazor.DocumentLibrary;

namespace Tempo.Blazor.Tests.DocumentLibrary;

/// <summary>
/// Tests for <see cref="InProcessTempoDocumentChangeBus"/> — the in-process implementation
/// of the document change channel that links a <see cref="ITempoDocumentChangePublisher"/>
/// (writers) to <see cref="ITempoDocumentChangeNotifier"/> subscribers (open editors/blocks).
/// </summary>
public class InProcessTempoDocumentChangeBusTests
{
    private static TempoDocumentChange Change(TempoDocumentKind kind, Guid id,
        TempoDocumentChangeType type = TempoDocumentChangeType.Saved)
        => new()
        {
            Kind = kind,
            DocumentId = id,
            ChangeType = type,
            ModifiedAt = DateTime.UtcNow,
            Origin = "tester"
        };

    [Fact]
    public async Task Publish_RaisesChanged_ForMatchingSubscriber()
    {
        var bus = new InProcessTempoDocumentChangeBus();
        var id = Guid.NewGuid();
        var received = new List<TempoDocumentChange>();
        bus.Changed += (c, _) => { received.Add(c); return Task.CompletedTask; };

        await bus.SubscribeAsync(TempoDocumentKind.Wireframe, id);
        await bus.PublishAsync(Change(TempoDocumentKind.Wireframe, id));

        received.Should().ContainSingle().Which.DocumentId.Should().Be(id);
    }

    [Fact]
    public async Task Publish_DoesNotRaise_ForDifferentDocument()
    {
        var bus = new InProcessTempoDocumentChangeBus();
        var received = new List<TempoDocumentChange>();
        bus.Changed += (c, _) => { received.Add(c); return Task.CompletedTask; };

        await bus.SubscribeAsync(TempoDocumentKind.Wireframe, Guid.NewGuid());
        await bus.PublishAsync(Change(TempoDocumentKind.Wireframe, Guid.NewGuid()));

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_DoesNotRaise_ForDifferentKind_SameId()
    {
        var bus = new InProcessTempoDocumentChangeBus();
        var id = Guid.NewGuid();
        var received = new List<TempoDocumentChange>();
        bus.Changed += (c, _) => { received.Add(c); return Task.CompletedTask; };

        await bus.SubscribeAsync(TempoDocumentKind.Wireframe, id);
        await bus.PublishAsync(Change(TempoDocumentKind.Diagram, id));

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Unsubscribe_StopsDelivery()
    {
        var bus = new InProcessTempoDocumentChangeBus();
        var id = Guid.NewGuid();
        var received = new List<TempoDocumentChange>();
        bus.Changed += (c, _) => { received.Add(c); return Task.CompletedTask; };

        await bus.SubscribeAsync(TempoDocumentKind.Wireframe, id);
        await bus.UnsubscribeAsync(TempoDocumentKind.Wireframe, id);
        await bus.PublishAsync(Change(TempoDocumentKind.Wireframe, id));

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_DeliversDeleteChangeType()
    {
        var bus = new InProcessTempoDocumentChangeBus();
        var id = Guid.NewGuid();
        TempoDocumentChange? got = null;
        bus.Changed += (c, _) => { got = c; return Task.CompletedTask; };

        await bus.SubscribeAsync(TempoDocumentKind.Spreadsheet, id);
        await bus.PublishAsync(Change(TempoDocumentKind.Spreadsheet, id, TempoDocumentChangeType.Deleted));

        got.Should().NotBeNull();
        got!.ChangeType.Should().Be(TempoDocumentChangeType.Deleted);
    }
}
