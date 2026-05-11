using FluentAssertions;
using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentOperationEngineTests
{
    [Fact]
    public void OperationLog_AppendsBatchesInOrder()
    {
        var log = new DocumentOperationLog();

        log.Append(Batch("doc-1", InsertBlock("a", 0, "Alpha"))).IsValid.Should().BeTrue();
        log.Append(Batch("doc-1", InsertBlock("b", 1, "Beta"))).IsValid.Should().BeTrue();

        log.Batches.SelectMany(batch => batch.Operations).Select(operation => operation.Target.BlockId)
            .Should().Equal("a", "b");
    }

    [Fact]
    public void OperationApplier_ReplaysOperationsOnEmptyDocument()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        var log = new DocumentOperationLog();
        log.Append(Batch("doc-1", InsertBlock("a", 0, "Alpha")));
        log.Append(Batch("doc-1", InsertText("a", 5, " text")));

        var result = log.Replay(document);

        result.IsValid.Should().BeTrue();
        TextOf(document, "a").Should().Be("Alpha text");
    }

    [Fact]
    public void OperationApplier_ReplaysOperationsOnExistingDocument()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        var applier = new DocumentOperationApplier();

        var result = applier.Apply(document, Batch("doc-1", DeleteText("a", 2, "ph"), InsertText("a", 2, "ZZ")));

        result.IsValid.Should().BeTrue();
        TextOf(document, "a").Should().Be("AlZZa");
    }

    [Fact]
    public void OperationLog_ReplayIsIdempotentByOperationId()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        var operation = InsertText("a", 5, "!");
        var log = new DocumentOperationLog();

        log.Append(Batch("doc-1", operation));
        log.Append(Batch("doc-1", operation));
        log.Replay(document);

        TextOf(document, "a").Should().Be("Alpha!");
    }

    [Fact]
    public void OperationLog_RejectsUnknownSchemaVersion()
    {
        var operation = InsertText("a", 0, "x");
        operation.SchemaVersion = 999;

        var result = new DocumentOperationLog().Append(Batch("doc-1", operation));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("Unsupported operation schema version", StringComparison.Ordinal));
    }

    private static DocumentOperationBatch Batch(string documentId, params DocumentOperation[] operations)
    {
        return new DocumentOperationBatch
        {
            DocumentId = documentId,
            Operations = operations.ToList()
        };
    }

    internal static DocumentEditorDocument CreateDocument(string documentId, string blockId, string text)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = blockId,
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Text = text }]
                }
            }
        ];
        return document;
    }

    internal static DocumentOperation InsertBlock(string blockId, double order, string text)
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.InsertBlock,
            Target = new DocumentOperationTarget { BlockId = blockId, Order = order },
            Block = CreateDocument("doc-1", blockId, text).Blocks[0],
            Metadata = Metadata(1, "client-a")
        };
    }

    internal static DocumentOperation InsertText(string blockId, int offset, string text, long timestamp = 1, string clientId = "client-a")
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.InsertText,
            Target = new DocumentOperationTarget { BlockId = blockId, InlineIndex = 0, Offset = offset },
            Text = text,
            Metadata = Metadata(timestamp, clientId)
        };
    }

    internal static DocumentOperation DeleteText(string blockId, int offset, string text, long timestamp = 1, string clientId = "client-a")
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.DeleteText,
            Target = new DocumentOperationTarget { BlockId = blockId, InlineIndex = 0, Offset = offset },
            Text = text,
            Metadata = Metadata(timestamp, clientId)
        };
    }

    internal static DocumentOperation SetText(string blockId, string text, long timestamp = 1, string clientId = "client-a")
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget { BlockId = blockId },
            AttributeName = "text",
            AttributeValueJson = JsonSerializer.Serialize(text, DocumentEditorJson.Options),
            Metadata = Metadata(timestamp, clientId)
        };
    }

    internal static DocumentOperation MoveBlock(string blockId, double order, long timestamp = 1, string clientId = "client-a")
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.MoveBlock,
            Target = new DocumentOperationTarget { BlockId = blockId, Order = order },
            Metadata = Metadata(timestamp, clientId)
        };
    }

    internal static DocumentOperationMetadata Metadata(long timestamp, string clientId)
    {
        return new DocumentOperationMetadata
        {
            AuthorId = clientId,
            ClientId = clientId,
            LogicalTimestamp = timestamp
        };
    }

    internal static string TextOf(DocumentEditorDocument document, string blockId)
    {
        return document.Blocks
            .First(block => block.Id == blockId)
            .Content.Should()
            .BeOfType<ParagraphBlockContent>()
            .Subject
            .Inlines
            .OfType<TextRun>()
            .Single()
            .Text;
    }
}
