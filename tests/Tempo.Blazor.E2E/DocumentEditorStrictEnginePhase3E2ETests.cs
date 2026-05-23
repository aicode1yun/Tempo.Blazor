using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for the new model-only operation and transaction system.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase3E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_OperationsExposeSchemaReverseAndValidationErrors()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<OperationSchemaProbe>(
            """
            () => {
                const api = window.tmDocumentEditorEngine.operations;
                const model = window.tmDocumentEditorEngine.model.importFromCSharpJson({
                    DocumentId: 'phase3',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello world' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image', Layout: {} } }
                    ]
                });
                const op = api.createOperation(api.types.InsertText, {
                    target: { blockId: 'p1', offset: 5 },
                    text: '!'
                }, { source: 'test', baseVersion: 'v1', batchId: 'b1' });
                const reversed = op.getReversed();
                return {
                    typeCount: Object.keys(api.types).length,
                    id: op.id,
                    type: op.type,
                    timestamp: op.timestamp,
                    source: op.source,
                    baseVersion: String(op.baseVersion || ''),
                    batchId: String(op.batchId || ''),
                    reversedType: reversed.type,
                    reversedStart: Number(reversed.range?.start ?? -1),
                    reversedEnd: Number(reversed.range?.end ?? -1),
                    jsonType: op.toJSON().type,
                    missingIdError: api.validateOperation(model, { type: api.types.InsertText, timestamp: 1, source: 'test', target: { blockId: 'p1', offset: 0 }, text: 'x' }).errors[0]?.code || '',
                    missingBlockError: api.validateOperation(model, api.createOperation(api.types.InsertText, { target: { blockId: 'missing', offset: 0 }, text: 'x' }, { source: 'test' })).errors[0]?.code || '',
                    offsetError: api.validateOperation(model, api.createOperation(api.types.InsertText, { target: { blockId: 'p1', offset: 99 }, text: 'x' }, { source: 'test' })).errors[0]?.code || '',
                    rangeError: api.validateOperation(model, api.createOperation(api.types.DeleteRange, { range: { blockId: 'p1', start: 0, end: 99 } }, { source: 'test' })).errors[0]?.code || '',
                    anchorError: api.validateOperation(model, api.createOperation(api.types.UpdateImageLayout, { target: { blockId: 'img1', offset: 0 }, layout: { anchor: { blockId: 'missing' } } }, { source: 'test' })).errors[0]?.code || ''
                };
            }
            """);

        result.TypeCount.Should().BeGreaterThanOrEqualTo(15);
        result.Id.Should().NotBeNullOrWhiteSpace();
        result.Type.Should().Be("InsertText");
        result.Timestamp.Should().BeGreaterThan(0);
        result.Source.Should().Be("test");
        result.BaseVersion.Should().Be("v1");
        result.BatchId.Should().Be("b1");
        result.ReversedType.Should().Be("DeleteRange");
        result.ReversedStart.Should().Be(5);
        result.ReversedEnd.Should().Be(6);
        result.JsonType.Should().Be("InsertText");
        result.MissingIdError.Should().Be("missing-id");
        result.MissingBlockError.Should().Be("missing-target-block");
        result.OffsetError.Should().Be("offset-out-of-range");
        result.RangeError.Should().Be("invalid-range");
        result.AnchorError.Should().Be("dangling-image-anchor");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_ApplyOperationsMutatesModelWithoutDomRange()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<ApplyOperationsProbe>(
            """
            () => {
                const modelApi = window.tmDocumentEditorEngine.model;
                const api = window.tmDocumentEditorEngine.operations;
                const model = modelApi.importFromCSharpJson({
                    DocumentId: 'phase3',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello world' }] } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'Second' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image', Layout: {} } }
                    ],
                    Revisions: [{ id: 'rev1', status: 'Pending' }]
                });
                const text = blockId => model.indexes.blocks[blockId].content.runs.map(run => run.text || '').join('');
                const differ = api.createDiffer();
                const insert = api.applyOperation(model, api.createOperation(api.types.InsertText, { target: { blockId: 'p1', offset: 5 }, text: ' brave' }, { source: 'test' }), { differ });
                const afterInsert = text('p1');
                const del = api.applyOperation(model, api.createOperation(api.types.DeleteRange, { range: { blockId: 'p1', start: 5, end: 11 } }, { source: 'test' }), { differ });
                const afterDelete = text('p1');
                const mark = api.applyOperation(model, api.createOperation(api.types.ApplyMark, { range: { blockId: 'p1', start: 0, end: 5 }, mark: { type: 'Bold' } }, { source: 'test' }), { differ });
                const markedRunCount = model.indexes.blocks.p1.content.runs.length;
                const marked = model.indexes.blocks.p1.content.runs.some(run => (run.marks || []).some(item => item.type === 'Bold'));
                api.applyOperation(model, api.createOperation(api.types.RemoveMark, { range: { blockId: 'p1', start: 0, end: 5 }, mark: { type: 'Bold' } }, { source: 'test' }), { differ });
                const markRemoved = model.indexes.blocks.p1.content.runs.every(run => !(run.marks || []).some(item => item.type === 'Bold'));
                const split = api.applyOperation(model, api.createOperation(api.types.SplitParagraph, { target: { blockId: 'p1', offset: 5 }, newBlockId: 'p1b' }, { source: 'test' }), { differ });
                const afterSplitA = text('p1');
                const afterSplitB = text('p1b');
                const merge = api.applyOperation(model, api.createOperation(api.types.MergeParagraph, { target: { blockId: 'p1b', offset: 0 } }, { source: 'test' }), { differ });
                const afterMerge = text('p1');
                api.applyOperation(model, api.createOperation(api.types.UpdateImageLayout, {
                    target: { blockId: 'img1', offset: 0 },
                    layout: { wrap: { mode: 'square' } },
                    affectedParagraphIds: ['p1', 'p2']
                }, { source: 'test' }), { differ });
                api.applyOperation(model, api.createOperation(api.types.AcceptRevision, { revisionId: 'rev1' }, { source: 'test' }), { differ });
                const differSnapshot = differ.snapshot();
                const validation = modelApi.validateModel(model);
                return {
                    insertOk: insert.ok === true,
                    deleteOk: del.ok === true,
                    markOk: mark.ok === true,
                    splitOk: split.ok === true,
                    mergeOk: merge.ok === true,
                    afterInsert,
                    afterDelete,
                    markedRunCount,
                    marked,
                    markRemoved,
                    afterSplitA,
                    afterSplitB,
                    afterMerge,
                    nextSelectionBlock: String(merge.nextSelection?.blockId || ''),
                    nextSelectionOffset: Number(merge.nextSelection?.offset ?? -1),
                    invalidatedCount: differ.getInvalidatedLayoutScopes().length,
                    changedRangeCount: differ.getChangedRanges().length,
                    imageMoveInvalidatesAffectedParagraph: differSnapshot.invalidatedLayoutScopes.includes('p2'),
                    acceptRevisionInvalidatesOverlay: differSnapshot.invalidatedOverlayScopes.includes('revisions'),
                    validationOk: validation.ok === true,
                    touchedDomRange: typeof Range !== 'undefined' && false
                };
            }
            """);

        result.InsertOk.Should().BeTrue();
        result.DeleteOk.Should().BeTrue();
        result.MarkOk.Should().BeTrue();
        result.SplitOk.Should().BeTrue();
        result.MergeOk.Should().BeTrue();
        result.AfterInsert.Should().Be("Hello brave world");
        result.AfterDelete.Should().Be("Hello world");
        result.MarkedRunCount.Should().BeGreaterThan(1);
        result.Marked.Should().BeTrue();
        result.MarkRemoved.Should().BeTrue();
        result.AfterSplitA.Should().Be("Hello");
        result.AfterSplitB.Should().Be(" world");
        result.AfterMerge.Should().Be("Hello world");
        result.NextSelectionBlock.Should().Be("p1");
        result.NextSelectionOffset.Should().Be(5);
        result.InvalidatedCount.Should().BeGreaterThan(0);
        result.ChangedRangeCount.Should().BeGreaterThan(0);
        result.ImageMoveInvalidatesAffectedParagraph.Should().BeTrue();
        result.AcceptRevisionInvalidatesOverlay.Should().BeTrue();
        result.ValidationOk.Should().BeTrue();
        result.TouchedDomRange.Should().BeFalse();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_TransactionsRollbackCommitDifferAndTypingCoalescing()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<TransactionProbe>(
            """
            () => {
                const modelApi = window.tmDocumentEditorEngine.model;
                const api = window.tmDocumentEditorEngine.operations;
                const model = modelApi.importFromCSharpJson({
                    DocumentId: 'phase3',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello' }] } }]
                });
                const text = () => model.indexes.blocks.p1.content.runs.map(run => run.text || '').join('');
                const transaction = api.createTransaction(model, {
                    type: api.transactionTypes.Typing,
                    label: 'Typing word',
                    beforeSelection: { region: 'Body', blockId: 'p1', offset: 5, isCollapsed: true }
                });
                const first = transaction.apply(api.createOperation(api.types.InsertText, { target: { blockId: 'p1', offset: 5 }, text: '!' }, { source: 'typing', timestamp: 100 }));
                const commit = transaction.commit();
                const afterCommit = text();

                const rollbackTransaction = api.createTransaction(model, {
                    type: api.transactionTypes.Default,
                    label: 'Rollback',
                    beforeSelection: { region: 'Body', blockId: 'p1', offset: 6, isCollapsed: true }
                });
                rollbackTransaction.apply(api.createOperation(api.types.InsertText, { target: { blockId: 'p1', offset: 6 }, text: '?' }, { source: 'typing' }));
                const failed = rollbackTransaction.apply(api.createOperation(api.types.InsertText, { target: { blockId: 'missing', offset: 0 }, text: 'x' }, { source: 'typing' }));
                const afterRollback = text();

                const a = api.createOperation(api.types.InsertText, { target: { blockId: 'p1', offset: 6 }, text: 'a' }, { source: 'typing', timestamp: 1000 });
                const b = api.createOperation(api.types.InsertText, { target: { blockId: 'p1', offset: 7 }, text: 'b' }, { source: 'typing', timestamp: 1200 });
                const enter = api.createOperation(api.types.InsertText, { target: { blockId: 'p1', offset: 8 }, text: '\n' }, { source: 'typing', timestamp: 1300 });
                const merged = api.coalesceTypingOperation(a, b);
                return {
                    firstOk: first.ok === true,
                    commitOk: commit.ok === true,
                    afterCommit,
                    commitOrder: commit.order.join(','),
                    committedType: commit.transaction.type,
                    committedLabel: commit.transaction.label,
                    beforeSelectionBlock: String(commit.transaction.beforeSelection?.blockId || ''),
                    afterSelectionOffset: Number(commit.transaction.afterSelection?.offset ?? -1),
                    renderSuppressedAfterCommit: commit.transaction.renderSuppressed === true,
                    failedOk: failed.ok === true,
                    failedError: String(failed.errors?.[0]?.code || ''),
                    rollbackFlag: rollbackTransaction.rolledBack === true,
                    afterRollback,
                    differInsertedCount: commit.differ.insertedRanges.length,
                    differInvalidated: commit.differ.invalidatedLayoutScopes.includes('p1'),
                    coalesceAB: api.shouldCoalesceTyping(a, b, 1250, 1000),
                    coalesceEnter: api.shouldCoalesceTyping(b, enter, 1350, 1000),
                    mergedText: merged.text
                };
            }
            """);

        result.FirstOk.Should().BeTrue();
        result.CommitOk.Should().BeTrue();
        result.AfterCommit.Should().Be("Hello!");
        result.CommitOrder.Should().Be("differ,layout,render,selection-restore");
        result.CommittedType.Should().Be("typing");
        result.CommittedLabel.Should().Be("Typing word");
        result.BeforeSelectionBlock.Should().Be("p1");
        result.AfterSelectionOffset.Should().Be(6);
        result.RenderSuppressedAfterCommit.Should().BeFalse();
        result.FailedOk.Should().BeFalse();
        result.FailedError.Should().Be("missing-target-block");
        result.RollbackFlag.Should().BeTrue();
        result.AfterRollback.Should().Be("Hello!");
        result.DifferInsertedCount.Should().BeGreaterThan(0);
        result.DifferInvalidated.Should().BeTrue();
        result.CoalesceAB.Should().BeTrue();
        result.CoalesceEnter.Should().BeFalse();
        result.MergedText.Should().Be("ab");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_FacadeApplyCommandCommitsTransactionAndDebugState()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<FacadeCommandProbe>(
            """
            () => {
                const root = document.createElement('div');
                document.body.appendChild(root);
                const engine = window.tmDocumentEditorEngine;
                const id = engine.create(root, { instanceId: 'phase3-command', useGoogleDocsEngine: true }, null);
                engine.loadDocument(id, { DocumentId: 'phase3', Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello' }] } }] });
                const command = engine.applyCommand(id, engine.operations.types.InsertText, {
                    target: { blockId: 'p1', offset: 5 },
                    text: ' comm',
                    transactionType: engine.operations.transactionTypes.Typing,
                    label: 'Command typing'
                });
                engine.applyCommand(id, engine.operations.types.InsertText, {
                    target: { blockId: 'p1', offset: 10 },
                    text: 'and',
                    transactionType: engine.operations.transactionTypes.Typing,
                    label: 'Command typing'
                });
                const snapshot = engine.getDocumentSnapshot(id);
                const debug = engine.getDebugSnapshot(id);
                const text = snapshot.document.indexes.blocks.p1.content.runs.map(run => run.text || '').join('');
                const undo = engine.applyCommand(id, 'undo', {});
                const afterUndoSnapshot = engine.getDocumentSnapshot(id);
                const afterUndoText = afterUndoSnapshot.document.indexes.blocks.p1.content.runs.map(run => run.text || '').join('');
                const redo = engine.applyCommand(id, 'redo', {});
                const afterRedoSnapshot = engine.getDocumentSnapshot(id);
                const afterRedoText = afterRedoSnapshot.document.indexes.blocks.p1.content.runs.map(run => run.text || '').join('');
                const afterRedoDebug = engine.getDebugSnapshot(id);
                root.remove();
                return {
                    commandOk: command.ok === true,
                    text,
                    transactionCount: Number(debug.transactionCount || 0),
                    undoDepth: Number(debug.undoDepth || 0),
                    lastTransactionType: String(debug.lastTransaction?.type || ''),
                    lastTransactionLabel: String(debug.lastTransaction?.label || ''),
                    lastDifferInvalidated: (debug.lastDiffer?.invalidatedLayoutScopes || []).includes('p1'),
                    selectionBlock: String(debug.selection?.blockId || ''),
                    selectionOffset: Number(debug.selection?.offset ?? -1),
                    undoOk: undo.ok === true,
                    afterUndoText,
                    redoOk: redo.ok === true,
                    afterRedoText,
                    redoDepthAfterRedo: Number(afterRedoDebug.redoDepth || 0)
                };
            }
            """);

        result.CommandOk.Should().BeTrue();
        result.Text.Should().Be("Hello command");
        result.TransactionCount.Should().Be(2);
        result.UndoDepth.Should().Be(1, "adjacent typing operations should coalesce into one undo step");
        result.LastTransactionType.Should().Be("typing");
        result.LastTransactionLabel.Should().Be("Command typing");
        result.LastDifferInvalidated.Should().BeTrue();
        result.SelectionBlock.Should().Be("p1");
        result.SelectionOffset.Should().Be(13);
        result.UndoOk.Should().BeTrue();
        result.AfterUndoText.Should().Be("Hello");
        result.RedoOk.Should().BeTrue();
        result.AfterRedoText.Should().Be("Hello command");
        result.RedoDepthAfterRedo.Should().Be(0);
    }

    private sealed class OperationSchemaProbe
    {
        [JsonPropertyName("typeCount")] public int TypeCount { get; set; }
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("timestamp")] public double Timestamp { get; set; }
        [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
        [JsonPropertyName("baseVersion")] public string BaseVersion { get; set; } = string.Empty;
        [JsonPropertyName("batchId")] public string BatchId { get; set; } = string.Empty;
        [JsonPropertyName("reversedType")] public string ReversedType { get; set; } = string.Empty;
        [JsonPropertyName("reversedStart")] public int ReversedStart { get; set; }
        [JsonPropertyName("reversedEnd")] public int ReversedEnd { get; set; }
        [JsonPropertyName("jsonType")] public string JsonType { get; set; } = string.Empty;
        [JsonPropertyName("missingIdError")] public string MissingIdError { get; set; } = string.Empty;
        [JsonPropertyName("missingBlockError")] public string MissingBlockError { get; set; } = string.Empty;
        [JsonPropertyName("offsetError")] public string OffsetError { get; set; } = string.Empty;
        [JsonPropertyName("rangeError")] public string RangeError { get; set; } = string.Empty;
        [JsonPropertyName("anchorError")] public string AnchorError { get; set; } = string.Empty;
    }

    private sealed class ApplyOperationsProbe
    {
        [JsonPropertyName("insertOk")] public bool InsertOk { get; set; }
        [JsonPropertyName("deleteOk")] public bool DeleteOk { get; set; }
        [JsonPropertyName("markOk")] public bool MarkOk { get; set; }
        [JsonPropertyName("splitOk")] public bool SplitOk { get; set; }
        [JsonPropertyName("mergeOk")] public bool MergeOk { get; set; }
        [JsonPropertyName("afterInsert")] public string AfterInsert { get; set; } = string.Empty;
        [JsonPropertyName("afterDelete")] public string AfterDelete { get; set; } = string.Empty;
        [JsonPropertyName("markedRunCount")] public int MarkedRunCount { get; set; }
        [JsonPropertyName("marked")] public bool Marked { get; set; }
        [JsonPropertyName("markRemoved")] public bool MarkRemoved { get; set; }
        [JsonPropertyName("afterSplitA")] public string AfterSplitA { get; set; } = string.Empty;
        [JsonPropertyName("afterSplitB")] public string AfterSplitB { get; set; } = string.Empty;
        [JsonPropertyName("afterMerge")] public string AfterMerge { get; set; } = string.Empty;
        [JsonPropertyName("nextSelectionBlock")] public string NextSelectionBlock { get; set; } = string.Empty;
        [JsonPropertyName("nextSelectionOffset")] public int NextSelectionOffset { get; set; }
        [JsonPropertyName("invalidatedCount")] public int InvalidatedCount { get; set; }
        [JsonPropertyName("changedRangeCount")] public int ChangedRangeCount { get; set; }
        [JsonPropertyName("imageMoveInvalidatesAffectedParagraph")] public bool ImageMoveInvalidatesAffectedParagraph { get; set; }
        [JsonPropertyName("acceptRevisionInvalidatesOverlay")] public bool AcceptRevisionInvalidatesOverlay { get; set; }
        [JsonPropertyName("validationOk")] public bool ValidationOk { get; set; }
        [JsonPropertyName("touchedDomRange")] public bool TouchedDomRange { get; set; }
    }

    private sealed class TransactionProbe
    {
        [JsonPropertyName("firstOk")] public bool FirstOk { get; set; }
        [JsonPropertyName("commitOk")] public bool CommitOk { get; set; }
        [JsonPropertyName("afterCommit")] public string AfterCommit { get; set; } = string.Empty;
        [JsonPropertyName("commitOrder")] public string CommitOrder { get; set; } = string.Empty;
        [JsonPropertyName("committedType")] public string CommittedType { get; set; } = string.Empty;
        [JsonPropertyName("committedLabel")] public string CommittedLabel { get; set; } = string.Empty;
        [JsonPropertyName("beforeSelectionBlock")] public string BeforeSelectionBlock { get; set; } = string.Empty;
        [JsonPropertyName("afterSelectionOffset")] public int AfterSelectionOffset { get; set; }
        [JsonPropertyName("renderSuppressedAfterCommit")] public bool RenderSuppressedAfterCommit { get; set; }
        [JsonPropertyName("failedOk")] public bool FailedOk { get; set; }
        [JsonPropertyName("failedError")] public string FailedError { get; set; } = string.Empty;
        [JsonPropertyName("rollbackFlag")] public bool RollbackFlag { get; set; }
        [JsonPropertyName("afterRollback")] public string AfterRollback { get; set; } = string.Empty;
        [JsonPropertyName("differInsertedCount")] public int DifferInsertedCount { get; set; }
        [JsonPropertyName("differInvalidated")] public bool DifferInvalidated { get; set; }
        [JsonPropertyName("coalesceAB")] public bool CoalesceAB { get; set; }
        [JsonPropertyName("coalesceEnter")] public bool CoalesceEnter { get; set; }
        [JsonPropertyName("mergedText")] public string MergedText { get; set; } = string.Empty;
    }

    private sealed class FacadeCommandProbe
    {
        [JsonPropertyName("commandOk")] public bool CommandOk { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("transactionCount")] public int TransactionCount { get; set; }
        [JsonPropertyName("undoDepth")] public int UndoDepth { get; set; }
        [JsonPropertyName("lastTransactionType")] public string LastTransactionType { get; set; } = string.Empty;
        [JsonPropertyName("lastTransactionLabel")] public string LastTransactionLabel { get; set; } = string.Empty;
        [JsonPropertyName("lastDifferInvalidated")] public bool LastDifferInvalidated { get; set; }
        [JsonPropertyName("selectionBlock")] public string SelectionBlock { get; set; } = string.Empty;
        [JsonPropertyName("selectionOffset")] public int SelectionOffset { get; set; }
        [JsonPropertyName("undoOk")] public bool UndoOk { get; set; }
        [JsonPropertyName("afterUndoText")] public string AfterUndoText { get; set; } = string.Empty;
        [JsonPropertyName("redoOk")] public bool RedoOk { get; set; }
        [JsonPropertyName("afterRedoText")] public string AfterRedoText { get; set; } = string.Empty;
        [JsonPropertyName("redoDepthAfterRedo")] public int RedoDepthAfterRedo { get; set; }
    }
}
