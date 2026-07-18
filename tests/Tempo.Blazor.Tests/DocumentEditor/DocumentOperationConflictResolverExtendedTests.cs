using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Extended conflict-resolution table: formatting (inline mark ranges transformed against
/// concurrent text edits), object operations (drawing moves, block updates — last write wins),
/// revision decisions (first decision wins), plus property tests — the resolver must be
/// permutation-invariant and applying the resolved sequence must converge.
/// </summary>
public class DocumentOperationConflictResolverExtendedTests
{
    // ── Formatting: mark ranges transform against concurrent text edits ─────────────────────────

    [Fact]
    public void AddMark_AfterConcurrentInsertBeforeRange_ShiftsRight()
    {
        var insert = Op(DocumentOperationType.InsertText, "b1", offset: 0, text: "Hello ", timestamp: 1, client: "a");
        var mark = MarkOp(DocumentOperationType.AddInlineMark, "b1", offset: 10, length: 4, timestamp: 2, client: "b");

        var resolved = new DocumentOperationConflictResolver().Resolve([insert, mark]);

        var resolvedMark = resolved.Single(op => op.Type == DocumentOperationType.AddInlineMark);
        resolvedMark.Target.Offset.Should().Be(16, "the 6-char insert before the range shifts it right");
        resolvedMark.Target.Length.Should().Be(4);
    }

    [Fact]
    public void AddMark_AfterConcurrentDeleteBeforeRange_ShiftsLeft()
    {
        var delete = Op(DocumentOperationType.DeleteText, "b1", offset: 0, length: 5, timestamp: 1, client: "a");
        var mark = MarkOp(DocumentOperationType.AddInlineMark, "b1", offset: 10, length: 4, timestamp: 2, client: "b");

        var resolved = new DocumentOperationConflictResolver().Resolve([delete, mark]);

        var resolvedMark = resolved.Single(op => op.Type == DocumentOperationType.AddInlineMark);
        resolvedMark.Target.Offset.Should().Be(5);
        resolvedMark.Target.Length.Should().Be(4);
    }

    [Fact]
    public void AddMark_DeleteOverlapsRangeTail_TrimsLength()
    {
        var delete = Op(DocumentOperationType.DeleteText, "b1", offset: 12, length: 10, timestamp: 1, client: "a");
        var mark = MarkOp(DocumentOperationType.AddInlineMark, "b1", offset: 10, length: 6, timestamp: 2, client: "b");

        var resolved = new DocumentOperationConflictResolver().Resolve([delete, mark]);

        var resolvedMark = resolved.Single(op => op.Type == DocumentOperationType.AddInlineMark);
        resolvedMark.Target.Offset.Should().Be(10);
        resolvedMark.Target.Length.Should().Be(2, "the deleted tail no longer exists");
    }

    [Fact]
    public void RemoveMark_FullyInsideDeletedRange_IsDropped()
    {
        var delete = Op(DocumentOperationType.DeleteText, "b1", offset: 5, length: 10, timestamp: 1, client: "a");
        var mark = MarkOp(DocumentOperationType.RemoveInlineMark, "b1", offset: 7, length: 3, timestamp: 2, client: "b");

        var resolved = new DocumentOperationConflictResolver().Resolve([delete, mark]);

        resolved.Should().NotContain(op => op.Type == DocumentOperationType.RemoveInlineMark);
    }

    // ── Object operations: last write wins ──────────────────────────────────────────────────────

    [Fact]
    public void MoveDrawingObject_ConcurrentMoves_LastWriteWins()
    {
        var first = ObjectMove("obj-1", timestamp: 1, client: "a");
        var second = ObjectMove("obj-1", timestamp: 2, client: "b");
        var otherObject = ObjectMove("obj-2", timestamp: 1, client: "c");

        var resolved = new DocumentOperationConflictResolver().Resolve([first, second, otherObject]);

        resolved.Where(op => op.Type == DocumentOperationType.MoveDrawingObject && op.Target.ObjectId == "obj-1")
            .Should().ContainSingle().Which.OperationId.Should().Be(second.OperationId);
        resolved.Should().Contain(op => op.Target.ObjectId == "obj-2", "moves of other objects are independent");
    }

    [Fact]
    public void UpdateBlock_ConcurrentUpdates_LastWriteWins()
    {
        var first = Op(DocumentOperationType.UpdateBlock, "b1", timestamp: 1, client: "a");
        var second = Op(DocumentOperationType.UpdateBlock, "b1", timestamp: 2, client: "b");

        var resolved = new DocumentOperationConflictResolver().Resolve([first, second]);

        resolved.Where(op => op.Type == DocumentOperationType.UpdateBlock)
            .Should().ContainSingle().Which.OperationId.Should().Be(second.OperationId);
    }

    // ── Revision decisions: first decision wins ─────────────────────────────────────────────────

    [Fact]
    public void RevisionDecisions_FirstDecisionWins_ConflictDropped()
    {
        var accept = RevisionOp(DocumentOperationType.AcceptRevision, "rev-1", timestamp: 1, client: "a");
        var reject = RevisionOp(DocumentOperationType.RejectRevision, "rev-1", timestamp: 2, client: "b");
        var otherReject = RevisionOp(DocumentOperationType.RejectRevision, "rev-2", timestamp: 1, client: "c");

        var resolved = new DocumentOperationConflictResolver().Resolve([accept, reject, otherReject]);

        resolved.Where(op => op.Revision!.Id == "rev-1")
            .Should().ContainSingle().Which.Type.Should().Be(DocumentOperationType.AcceptRevision);
        resolved.Should().Contain(op => op.Revision!.Id == "rev-2", "decisions on other revisions are independent");
    }

    // ── Property tests ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_IsPermutationInvariant()
    {
        var random = new Random(20260718);
        var operations = GenerateRandomOperations(random, count: 200);
        var resolver = new DocumentOperationConflictResolver();
        var baseline = Serialize(resolver.Resolve(operations));

        for (var shuffle = 0; shuffle < 5; shuffle++)
        {
            var permuted = operations.OrderBy(_ => random.Next()).ToList();
            Serialize(resolver.Resolve(permuted)).Should().Be(
                baseline,
                $"the resolved sequence must not depend on arrival order (shuffle {shuffle})");
        }
    }

    [Fact]
    public void Resolve_AppliedToSameBase_Converges()
    {
        var random = new Random(42);
        var operations = GenerateRandomOperations(random, count: 150);
        var resolver = new DocumentOperationConflictResolver();
        var applier = new DocumentOperationApplier();

        string ApplyPermutation(int seed)
        {
            var document = CreateBaseDocument();
            var permuted = operations.OrderBy(_ => new Random(seed).Next()).ToList();
            foreach (var operation in resolver.Resolve(permuted))
            {
                applier.Apply(document, operation);
            }

            // Empty() and the applier stamp wall-clock times — normalize them so the comparison
            // checks CONTENT convergence, not the clock.
            document.Metadata.CreatedAt = new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero);
            document.Metadata.ModifiedAt = new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero);
            // The applier mints fresh Guid("N") ids when it splits runs — convergence is about
            // CONTENT (text, structure, mark ranges), so synthetic ids are normalized away.
            return System.Text.RegularExpressions.Regex.Replace(
                JsonSerializer.Serialize(document, DocumentEditorJson.Options),
                "[0-9a-f]{32}",
                "id");
        }

        var first = ApplyPermutation(1);
        var second = ApplyPermutation(2);
        var third = ApplyPermutation(3);

        second.Should().Be(first, "every replica must converge to the same document");
        third.Should().Be(first);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static DocumentOperation Op(
        DocumentOperationType type,
        string blockId,
        int? offset = null,
        int? length = null,
        string? text = null,
        long timestamp = 1,
        string client = "a")
        => new()
        {
            OperationId = $"{type}-{blockId}-{timestamp}-{client}-{Guid.NewGuid():N}",
            Type = type,
            Target = new DocumentOperationTarget
            {
                BlockId = blockId,
                InlineIndex = 0,
                Offset = offset,
                Length = length,
            },
            Text = text,
            Metadata = new DocumentOperationMetadata
            {
                LogicalTimestamp = timestamp,
                ClientId = client,
                AuthorId = client,
            },
        };

    private static DocumentOperation MarkOp(
        DocumentOperationType type,
        string blockId,
        int offset,
        int length,
        long timestamp,
        string client)
    {
        var operation = Op(type, blockId, offset, length, timestamp: timestamp, client: client);
        operation.Mark = new InlineMark { Type = InlineMarkType.Bold };
        return operation;
    }

    private static DocumentOperation ObjectMove(string objectId, long timestamp, string client)
    {
        var operation = Op(DocumentOperationType.MoveDrawingObject, "b1", timestamp: timestamp, client: client);
        operation.Target.ObjectId = objectId;
        return operation;
    }

    private static DocumentOperation RevisionOp(DocumentOperationType type, string revisionId, long timestamp, string client)
    {
        var operation = Op(type, "b1", timestamp: timestamp, client: client);
        operation.Revision = new DocumentRevision { Id = revisionId };
        return operation;
    }

    private static List<DocumentOperation> GenerateRandomOperations(Random random, int count)
    {
        var operations = new List<DocumentOperation>();
        string[] blocks = ["b1", "b2", "b3"];
        string[] clients = ["a", "b", "c", "d"];
        for (var i = 0; i < count; i++)
        {
            var block = blocks[random.Next(blocks.Length)];
            var client = clients[random.Next(clients.Length)];
            var timestamp = random.Next(1, 50);
            operations.Add(random.Next(6) switch
            {
                0 => Op(DocumentOperationType.InsertText, block, offset: random.Next(0, 20), text: $"txt{i} ", timestamp: timestamp, client: client),
                1 => Op(DocumentOperationType.DeleteText, block, offset: random.Next(0, 20), length: random.Next(1, 5), timestamp: timestamp, client: client),
                2 => MarkOp(DocumentOperationType.AddInlineMark, block, offset: random.Next(0, 20), length: random.Next(1, 6), timestamp: timestamp, client: client),
                3 => ObjectMove($"obj-{random.Next(3)}", timestamp, client),
                4 => Op(DocumentOperationType.SetBlockAttribute, block, timestamp: timestamp, client: client) is var attr
                    ? Configure(attr, op => { op.AttributeName = "align"; op.AttributeValueJson = $"\"v{i}\""; })
                    : throw new InvalidOperationException(),
                _ => RevisionOp(
                    random.Next(2) == 0 ? DocumentOperationType.AcceptRevision : DocumentOperationType.RejectRevision,
                    $"rev-{random.Next(4)}", timestamp, client),
            });
        }

        return operations;
    }

    private static DocumentOperation Configure(DocumentOperation operation, Action<DocumentOperation> configure)
    {
        configure(operation);
        return operation;
    }

    private static DocumentEditorDocument CreateBaseDocument()
    {
        var document = DocumentEditorDocument.Empty();
        document.DocumentId = "convergence-doc";
        document.Blocks =
        [
            Block("b1"),
            Block("b2"),
            Block("b3"),
        ];
        return document;

        static DocumentBlock Block(string id) => new()
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Id = $"{id}-run", Text = "Základní text odstavce pro konvergenci." }],
            },
        };
    }

    private static string Serialize(IReadOnlyList<DocumentOperation> operations)
        => JsonSerializer.Serialize(operations, DocumentEditorJson.Options);
}
