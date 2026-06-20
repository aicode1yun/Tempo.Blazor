using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Phase B1 transport: the canvas engine's op-log batch (which losslessly carries canvas blocks, tables and
/// images) is relayed verbatim as opaque JSON through <see cref="DocumentOperationBatch.CanvasOperationBatchJson"/>.
/// The strongly-typed <see cref="DocumentOperation.Block"/>/<see cref="DocumentBlock"/> model cannot round-trip
/// a canvas block (different shape, no string-enum), so the host must NOT deserialize the operations — it just
/// pipes the JSON. These tests prove the opaque payload survives the C# serialize/transport round-trip intact.
/// </summary>
public sealed class PhaseBOperationRelayTransportTests
{
    // A realistic canvas op-log batch with an updateBlock carrying a whole table block (the case the typed
    // model cannot represent: nested rows/cells/runs + lowercase string types).
    private const string CanvasOpLogBatchJson =
        """
        {"id":"canvas-local-client-a-1","documentId":"doc-1","protocolVersion":1,"clientId":"client-a","localSequence":1,
         "operations":[{"operationId":"op1","schemaVersion":1,"type":"updateBlock",
           "target":{"blockId":"tbl1","order":0},"metadata":{"clientId":"client-a","source":"test"},"text":null,
           "block":{"id":"tbl1","type":"table","content":{"table":{"border":"single","rows":[{"cells":[
             {"id":"cell1","blocks":[{"id":"cp1","type":"paragraph","content":{"runs":[{"text":"Hi!"}]}}]}]}]}}}}]}
        """;

    [Fact]
    public void CanvasBatchJson_SurvivesOperationBatchRoundTrip()
    {
        var batch = new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            CanvasOperationBatchJson = CanvasOpLogBatchJson,
        };

        var json = JsonSerializer.Serialize(batch, DocumentEditorJson.Options);
        var restored = JsonSerializer.Deserialize<DocumentOperationBatch>(json, DocumentEditorJson.Options);

        restored.Should().NotBeNull();
        restored!.CanvasOperationBatchJson.Should().Be(CanvasOpLogBatchJson);

        // The opaque payload still parses as the canvas batch with the table block content intact.
        using var doc = JsonDocument.Parse(restored.CanvasOperationBatchJson!);
        var op = doc.RootElement.GetProperty("operations")[0];
        op.GetProperty("type").GetString().Should().Be("updateBlock");
        op.GetProperty("block").GetProperty("content").GetProperty("table")
            .GetProperty("rows")[0].GetProperty("cells")[0].GetProperty("blocks")[0]
            .GetProperty("content").GetProperty("runs")[0].GetProperty("text").GetString()
            .Should().Be("Hi!");
    }

    [Fact]
    public void CanvasBatchJson_SurvivesCollaborationBatchTransportRoundTrip()
    {
        // The provider broadcasts a DocumentCollaborationOperationBatch; the inner batch carries the payload.
        var collab = new DocumentCollaborationOperationBatch
        {
            Sequence = 7,
            SessionId = "session-1",
            Batch = new DocumentOperationBatch { DocumentId = "doc-1", CanvasOperationBatchJson = CanvasOpLogBatchJson },
        };

        var json = JsonSerializer.Serialize(collab, DocumentEditorJson.Options);
        var restored = JsonSerializer.Deserialize<DocumentCollaborationOperationBatch>(json, DocumentEditorJson.Options);

        restored.Should().NotBeNull();
        restored!.Sequence.Should().Be(7);
        restored.Batch.CanvasOperationBatchJson.Should().Be(CanvasOpLogBatchJson);
        // No typed operations were needed — the relay is a dumb pipe.
        restored.Batch.Operations.Should().BeEmpty();
    }

    [Fact]
    public void LegacyBatch_WithoutCanvasJson_LeavesPayloadNull()
    {
        // Backward compatibility: existing C#-diff batches don't set the field.
        var batch = new DocumentOperationBatch { DocumentId = "doc-1", Operations = { } };
        var json = JsonSerializer.Serialize(batch, DocumentEditorJson.Options);

        json.Should().NotContain("canvasOperationBatchJson", "null payload must be omitted (WhenWritingNull)");
        JsonSerializer.Deserialize<DocumentOperationBatch>(json, DocumentEditorJson.Options)!
            .CanvasOperationBatchJson.Should().BeNull();
    }
}
