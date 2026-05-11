using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentOperationConflictResolverTests
{
    [Fact]
    public void Resolve_OrdersConcurrentInsertAtSamePositionDeterministically()
    {
        var operations = new[]
        {
            DocumentOperationEngineTests.InsertText("a", 1, "B", timestamp: 2, clientId: "client-b"),
            DocumentOperationEngineTests.InsertText("a", 1, "A", timestamp: 1, clientId: "client-a")
        };

        var resolved = new DocumentOperationConflictResolver().Resolve(operations);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "xx");
        var result = new DocumentOperationApplier().Apply(document, new DocumentOperationBatch { DocumentId = "doc-1", Operations = resolved.ToList() });

        result.IsValid.Should().BeTrue();
        DocumentOperationEngineTests.TextOf(document, "a").Should().Be("xABx");
    }

    [Fact]
    public void Resolve_DeduplicatesConcurrentDeleteOfSameRange()
    {
        var resolved = new DocumentOperationConflictResolver().Resolve(
        [
            DocumentOperationEngineTests.DeleteText("a", 1, "bc", timestamp: 1, clientId: "client-a"),
            DocumentOperationEngineTests.DeleteText("a", 1, "bc", timestamp: 2, clientId: "client-b")
        ]);

        resolved.Should().ContainSingle(operation => operation.Type == DocumentOperationType.DeleteText);
    }

    [Fact]
    public void Resolve_ShiftsInsertAgainstPriorDelete()
    {
        var resolved = new DocumentOperationConflictResolver().Resolve(
        [
            DocumentOperationEngineTests.DeleteText("a", 1, "bc", timestamp: 1, clientId: "client-a"),
            DocumentOperationEngineTests.InsertText("a", 4, "X", timestamp: 2, clientId: "client-b")
        ]);
        var insert = resolved.Single(operation => operation.Type == DocumentOperationType.InsertText);

        insert.Target.Offset.Should().Be(2);
    }

    [Fact]
    public void Resolve_DropsMarkInsideDeletedRange()
    {
        var mark = new DocumentOperation
        {
            Type = DocumentOperationType.AddMark,
            Target = new DocumentOperationTarget { BlockId = "a", InlineIndex = 0, Offset = 2 },
            Mark = new InlineMark { Type = InlineMarkType.Bold },
            Metadata = DocumentOperationEngineTests.Metadata(2, "client-b")
        };

        var resolved = new DocumentOperationConflictResolver().Resolve(
        [
            DocumentOperationEngineTests.DeleteText("a", 1, "bcd", timestamp: 1, clientId: "client-a"),
            mark
        ]);

        resolved.Should().NotContain(operation => operation.Type == DocumentOperationType.AddMark);
    }

    [Fact]
    public void Resolve_UsesLastWriterForConcurrentBlockRename()
    {
        var resolved = new DocumentOperationConflictResolver().Resolve(
        [
            DocumentOperationEngineTests.SetText("a", "First", timestamp: 1, clientId: "client-a"),
            DocumentOperationEngineTests.SetText("a", "Second", timestamp: 2, clientId: "client-b")
        ]);

        resolved.Should().ContainSingle(operation => operation.Type == DocumentOperationType.SetBlockAttribute);
        resolved[0].AttributeValueJson.Should().Contain("Second");
    }

    [Fact]
    public void Resolve_UsesLastWriterForConcurrentMoveBlock()
    {
        var resolved = new DocumentOperationConflictResolver().Resolve(
        [
            DocumentOperationEngineTests.MoveBlock("a", 10, timestamp: 1, clientId: "client-a"),
            DocumentOperationEngineTests.MoveBlock("a", 20, timestamp: 2, clientId: "client-b")
        ]);

        resolved.Should().ContainSingle(operation => operation.Type == DocumentOperationType.MoveBlock);
        resolved[0].Target.Order.Should().Be(20);
    }
}
