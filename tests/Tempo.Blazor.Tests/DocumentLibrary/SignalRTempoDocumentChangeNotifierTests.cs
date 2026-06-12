using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.DocumentLibrary.Collaboration;

namespace Tempo.Blazor.Tests.DocumentLibrary;

/// <summary>
/// Tests for <see cref="SignalRTempoDocumentChangeNotifier"/> in its in-process transport mode
/// (the SignalR wire is exercised by the E2E suite).
/// </summary>
public class SignalRTempoDocumentChangeNotifierTests
{
    private static TempoDocumentChange Change(TempoDocumentKind kind, Guid id) => new()
    {
        Kind = kind,
        DocumentId = id,
        ChangeType = TempoDocumentChangeType.Saved,
        ModifiedAt = DateTime.UtcNow
    };

    [Fact]
    public void GroupName_IsStableAndKindSpecific()
    {
        var id = Guid.NewGuid();

        SignalRTempoDocumentChangeNotifier.GroupName(TempoDocumentKind.Wireframe, id)
            .Should().Be($"doclib:Wireframe:{id}");
        SignalRTempoDocumentChangeNotifier.GroupName(TempoDocumentKind.Diagram, id)
            .Should().NotBe(SignalRTempoDocumentChangeNotifier.GroupName(TempoDocumentKind.Wireframe, id));
    }

    [Fact]
    public async Task ForwardsChange_FromTransport_ForSubscribedDocument()
    {
        var bus = new InProcessTempoDocumentChangeBus();
        await using var notifier = new SignalRTempoDocumentChangeNotifier(bus);
        var received = new List<TempoDocumentChange>();
        notifier.Changed += (c, _) => { received.Add(c); return Task.CompletedTask; };

        var id = Guid.NewGuid();
        await notifier.SubscribeAsync(TempoDocumentKind.Wireframe, id);
        await bus.PublishAsync(Change(TempoDocumentKind.Wireframe, id));

        received.Should().ContainSingle().Which.DocumentId.Should().Be(id);
    }

    [Fact]
    public async Task DoesNotForward_AfterUnsubscribe()
    {
        var bus = new InProcessTempoDocumentChangeBus();
        await using var notifier = new SignalRTempoDocumentChangeNotifier(bus);
        var received = new List<TempoDocumentChange>();
        notifier.Changed += (c, _) => { received.Add(c); return Task.CompletedTask; };

        var id = Guid.NewGuid();
        await notifier.SubscribeAsync(TempoDocumentKind.Wireframe, id);
        await notifier.UnsubscribeAsync(TempoDocumentKind.Wireframe, id);
        await bus.PublishAsync(Change(TempoDocumentKind.Wireframe, id));

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task RefCounts_KeepSubscriptionUntilLastUnsubscribe()
    {
        var bus = new InProcessTempoDocumentChangeBus();
        await using var notifier = new SignalRTempoDocumentChangeNotifier(bus);
        var received = new List<TempoDocumentChange>();
        notifier.Changed += (c, _) => { received.Add(c); return Task.CompletedTask; };

        var id = Guid.NewGuid();
        await notifier.SubscribeAsync(TempoDocumentKind.Wireframe, id);
        await notifier.SubscribeAsync(TempoDocumentKind.Wireframe, id); // second consumer
        await notifier.UnsubscribeAsync(TempoDocumentKind.Wireframe, id); // one leaves

        await bus.PublishAsync(Change(TempoDocumentKind.Wireframe, id));

        received.Should().ContainSingle(); // still delivered because one consumer remains
    }
}
