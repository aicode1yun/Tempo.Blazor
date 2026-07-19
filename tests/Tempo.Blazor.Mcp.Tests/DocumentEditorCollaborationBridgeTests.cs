using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Mcp.DocumentEditor;
using Tempo.Blazor.Mcp.Tests.Fixtures;

namespace Tempo.Blazor.Mcp.Tests;

public class DocumentEditorCollaborationBridgeTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void AddTempoDocumentEditorMcpCollaboration_RegistersOptionsAndBridge()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDocumentCollaborationProvider, InMemoryDocumentCollaborationProvider>();
        services.AddTempoDocumentEditorMcpCollaboration(options => options.Enabled = true);

        using var provider = services.BuildServiceProvider();
        provider.GetService<TempoDocumentMcpCollaborationOptions>()!.Enabled.Should().BeTrue();
        provider.GetService<IDocumentEditorMcpCollaborationBridge>().Should().NotBeNull();
    }

    [Fact]
    public async Task PublishAsync_Disabled_DoesNothing()
    {
        var collaboration = new InMemoryDocumentCollaborationProvider();
        var bridge = new TempoDocumentMcpCollaborationBridge(
            new TempoDocumentMcpCollaborationOptions { Enabled = false },
            collaboration);

        var published = await bridge.PublishAsync("doc-1", NewBatch("doc-1"));

        published.Should().BeFalse();
        (await collaboration.GetOperationBatchesAsync("doc-1", 0)).Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_Enabled_BroadcastsBatchAsNamedParticipantWithPresence()
    {
        var collaboration = new InMemoryDocumentCollaborationProvider();
        var options = new TempoDocumentMcpCollaborationOptions
        {
            Enabled = true,
            AgentName = "Agent Smlouva",
            AgentColor = "#00AA55"
        };
        var bridge = new TempoDocumentMcpCollaborationBridge(options, collaboration);

        var published = await bridge.PublishAsync("doc-live", NewBatch("doc-live"));

        published.Should().BeTrue();
        var batches = await collaboration.GetOperationBatchesAsync("doc-live", 0);
        batches.Should().ContainSingle();
        batches[0].Batch.Operations.Should().ContainSingle();

        var cursors = await collaboration.GetCursorsAsync("doc-live");
        cursors.Should().ContainSingle();
        cursors[0].DisplayName.Should().Be("Agent Smlouva");
        cursors[0].Color.Should().Be("#00AA55");
    }

    [Fact]
    public async Task PublishAsync_ReusesTheSessionAcrossPublishes()
    {
        var collaboration = new InMemoryDocumentCollaborationProvider();
        var bridge = new TempoDocumentMcpCollaborationBridge(
            new TempoDocumentMcpCollaborationOptions { Enabled = true },
            collaboration);

        await bridge.PublishAsync("doc-session", NewBatch("doc-session"));
        await bridge.PublishAsync("doc-session", NewBatch("doc-session"));

        var batches = await collaboration.GetOperationBatchesAsync("doc-session", 0);
        batches.Should().HaveCount(2);
        batches.Select(b => b.SessionId).Distinct().Should().ContainSingle("the agent must join once and reuse its session");
    }

    [Fact]
    public async Task PublishAsync_PublishesToBackplaneWithSourceInstanceId()
    {
        var backplane = new InMemoryDocumentCollaborationBackplane();
        DocumentCollaborationBackplaneMessage? received = null;
        await backplane.SubscribeAsync("doc-bp", message =>
        {
            received = message;
            return Task.CompletedTask;
        });

        var options = new TempoDocumentMcpCollaborationOptions { Enabled = true, SourceInstanceId = "mcp-instance-1" };
        var bridge = new TempoDocumentMcpCollaborationBridge(options, provider: null, backplane: backplane);

        var published = await bridge.PublishAsync("doc-bp", NewBatch("doc-bp"));

        published.Should().BeTrue();
        received.Should().NotBeNull();
        received!.SourceInstanceId.Should().Be("mcp-instance-1");
        received.Batch.Should().NotBeNull();
        received.Batch!.Batch.Operations.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishAsync_ProviderThrows_FailsOpen()
    {
        var bridge = new TempoDocumentMcpCollaborationBridge(
            new TempoDocumentMcpCollaborationOptions { Enabled = true },
            new ThrowingCollaborationProvider());

        var published = await bridge.PublishAsync("doc-down", NewBatch("doc-down"));

        published.Should().BeFalse("the backplane being down must never fail the edit");
    }

    [Fact]
    public async Task PublishAsync_InvokesHostCallbacks()
    {
        var collaboration = new InMemoryDocumentCollaborationProvider();
        DocumentCollaborationOperationBatch? callbackBatch = null;
        DocumentCollaborationCursor? callbackCursor = null;
        var options = new TempoDocumentMcpCollaborationOptions
        {
            Enabled = true,
            OperationPublishedCallback = (batch, _) =>
            {
                callbackBatch = batch;
                return Task.CompletedTask;
            },
            CursorPublishedCallback = (cursor, _) =>
            {
                callbackCursor = cursor;
                return Task.CompletedTask;
            }
        };
        var bridge = new TempoDocumentMcpCollaborationBridge(options, collaboration);

        await bridge.PublishAsync("doc-cb", NewBatch("doc-cb"));

        callbackBatch.Should().NotBeNull();
        callbackCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task SemanticTextTool_WithBridge_BroadcastsAppliedOperations()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-tool-collab");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Hello" }] }
        });
        provider.Add(doc);
        var collaboration = new InMemoryDocumentCollaborationProvider();
        var bridge = new TempoDocumentMcpCollaborationBridge(
            new TempoDocumentMcpCollaborationOptions { Enabled = true },
            collaboration);

        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, doc.DocumentId, "p1", 5, " world", collaborationBridge: bridge));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("collaborationPublished").GetBoolean().Should().BeTrue();

        var batches = await collaboration.GetOperationBatchesAsync(doc.DocumentId, 0);
        batches.Should().ContainSingle();
        batches[0].Batch.Operations.Should().ContainSingle()
            .Which.Type.Should().Be(DocumentOperationType.InsertText);
    }

    [Fact]
    public async Task SemanticTextTool_WithoutBridge_ReportsNotPublished()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-tool-nocollab");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Hello" }] }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, doc.DocumentId, "p1", 5, " world"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("collaborationPublished").GetBoolean().Should().BeFalse();
    }

    // ---------------------------------------------------------------- helpers

    private static DocumentOperationBatch NewBatch(string documentId) => new()
    {
        DocumentId = documentId,
        Operations =
        [
            new DocumentOperation
            {
                Type = DocumentOperationType.InsertText,
                Target = new DocumentOperationTarget { BlockId = "p1", InlineIndex = 0, Offset = 0 },
                Text = "x"
            }
        ]
    };

    private sealed class ThrowingCollaborationProvider : IDocumentCollaborationProvider
    {
        public Task<DocumentCollaborationSession> JoinAsync(DocumentCollaborationJoinRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("backplane down");

        public Task LeaveAsync(string sessionId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("backplane down");

        public Task<DocumentCollaborationOperationBatch> BroadcastOperationBatchAsync(string sessionId, DocumentOperationBatch batch, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("backplane down");

        public Task<IReadOnlyList<DocumentCollaborationOperationBatch>> GetOperationBatchesAsync(string documentId, long afterSequence, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("backplane down");

        public Task BroadcastCursorAsync(DocumentCollaborationCursor cursor, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("backplane down");

        public Task<IReadOnlyList<DocumentCollaborationCursor>> GetCursorsAsync(string documentId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("backplane down");
    }
}
