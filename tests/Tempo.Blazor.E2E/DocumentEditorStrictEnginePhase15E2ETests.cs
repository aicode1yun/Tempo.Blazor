using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for model-owned undo/redo history and transaction boundaries.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase15E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_History_TypingUndoRedoCoalescesAndRestoresSelection()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<HistoryTypingProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase15-typing',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello' }] } }]
                });
                const history = engine.history.createHistoryController(model, {
                    selection: { blockId: 'p1', offset: 5, isCollapsed: true },
                    layoutOptions: { pageWidth: 520, pageHeight: 420, margin: { top: 40, right: 40, bottom: 40, left: 40 } }
                });
                history.commitOperation(engine.operations.createOperation(engine.operations.types.InsertText, {
                    target: { blockId: 'p1', offset: 5 },
                    text: ' '
                }, { source: 'typing', timestamp: 1000 }), { transactionType: engine.operations.transactionTypes.Typing, label: 'Typing' });
                history.commitOperation(engine.operations.createOperation(engine.operations.types.InsertText, {
                    target: { blockId: 'p1', offset: 6 },
                    text: 'world'
                }, { source: 'typing', timestamp: 1100 }), { transactionType: engine.operations.transactionTypes.Typing, label: 'Typing' });
                const afterTyping = model.body.blocks[0].content.runs.map(run => run.text).join('');
                const debugAfterTyping = history.debug();
                const undo = history.undo();
                const afterUndo = model.body.blocks[0].content.runs.map(run => run.text).join('');
                const redo = history.redo();
                const afterRedo = model.body.blocks[0].content.runs.map(run => run.text).join('');
                return {
                    afterTyping,
                    undoDepthAfterTyping: debugAfterTyping.undoDepth,
                    coalesced: history.getUndoStack()[0].transaction.coalesced === true,
                    undoOk: undo.ok,
                    undoTransactionType: undo.transaction.type,
                    undoAppliedSource: undo.appliedOperations[0].source,
                    afterUndo,
                    undoSelectionBlock: undo.selection.blockId,
                    undoSelectionOffset: undo.selection.offset,
                    redoOk: redo.ok,
                    redoTransactionType: redo.transaction.type,
                    redoAppliedSource: redo.appliedOperations[0].source,
                    afterRedo,
                    redoSelectionBlock: redo.selection.blockId,
                    redoSelectionOffset: redo.selection.offset,
                    invalidatedScopes: redo.transaction.invalidatedScopes,
                    renderAdvanced: redo.renderVersion > undo.renderVersion
                };
            }
            """);

        result.AfterTyping.Should().Be("Hello world");
        result.UndoDepthAfterTyping.Should().Be(1);
        result.Coalesced.Should().BeTrue();
        result.UndoOk.Should().BeTrue();
        result.UndoTransactionType.Should().Be("undo");
        result.UndoAppliedSource.Should().Be("undo");
        result.AfterUndo.Should().Be("Hello");
        result.UndoSelectionBlock.Should().Be("p1");
        result.UndoSelectionOffset.Should().Be(5);
        result.RedoOk.Should().BeTrue();
        result.RedoTransactionType.Should().Be("redo");
        result.RedoAppliedSource.Should().Be("redo");
        result.AfterRedo.Should().Be("Hello world");
        result.RedoSelectionBlock.Should().Be("p1");
        result.RedoSelectionOffset.Should().Be(11);
        result.InvalidatedScopes.Should().Contain("p1");
        result.RenderAdvanced.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_History_EnterPasteToolbarHaveSeparateBoundariesAndRedoClears()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<HistoryBoundaryProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase15-boundaries',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta' }] } }]
                });
                const history = engine.history.createHistoryController(model, { selection: { blockId: 'p1', offset: 5, isCollapsed: true } });
                const enter = history.commitOperation(engine.operations.createOperation(engine.operations.types.SplitParagraph, {
                    target: { blockId: 'p1', offset: 5 },
                    newBlockId: 'p2'
                }, { source: 'input' }), { transactionType: 'enter', label: 'Enter' });
                const paste = history.commitOperations([
                    engine.operations.createOperation(engine.operations.types.InsertText, {
                        target: { blockId: 'p2', offset: 0 },
                        text: 'Paste'
                    }, { source: 'paste' }),
                    engine.operations.createOperation(engine.operations.types.InsertText, {
                        target: { blockId: 'p2', offset: 5 },
                        text: ' value'
                    }, { source: 'paste' })
                ], { transactionType: 'paste', label: 'Paste' });
                const toolbar = history.commitOperation(engine.operations.createOperation(engine.operations.types.ApplyMark, {
                    range: { blockId: 'p2', start: 0, end: 5 },
                    mark: { type: 'Bold' }
                }, { source: 'command' }), { transactionType: 'toolbar', label: 'Bold' });
                const beforeUndoDepth = history.debug().undoDepth;
                const undoToolbar = history.undo();
                const redoToolbar = history.redo();
                history.undo();
                const redoDepthBeforeNewEdit = history.debug().redoDepth;
                const newEdit = history.commitOperation(engine.operations.createOperation(engine.operations.types.InsertText, {
                    target: { blockId: 'p1', offset: 5 },
                    text: '!'
                }, { source: 'typing', timestamp: 2000 }), { transactionType: 'typing', label: 'Typing' });
                const redoDepthAfterNewEdit = history.debug().redoDepth;
                const blocksAfterUndoEnter = (() => {
                    history.undo();
                    history.undo();
                    history.undo();
                    return model.body.blocks.map(block => block.id);
                })();
                return {
                    enterOk: enter.ok,
                    pasteOperationCount: paste.transaction.operationCount,
                    toolbarOk: toolbar.ok,
                    beforeUndoDepth,
                    undoToolbarType: undoToolbar.transaction.type,
                    redoToolbarType: redoToolbar.transaction.type,
                    redoDepthBeforeNewEdit,
                    newEditOk: newEdit.ok,
                    redoDepthAfterNewEdit,
                    blocksAfterUndoEnter,
                    textAfterUndoEnter: model.body.blocks[0].content.runs.map(run => run.text).join('')
                };
            }
            """);

        result.EnterOk.Should().BeTrue();
        result.PasteOperationCount.Should().Be(2);
        result.ToolbarOk.Should().BeTrue();
        result.BeforeUndoDepth.Should().Be(3);
        result.UndoToolbarType.Should().Be("undo");
        result.RedoToolbarType.Should().Be("redo");
        result.RedoDepthBeforeNewEdit.Should().Be(1);
        result.NewEditOk.Should().BeTrue();
        result.RedoDepthAfterNewEdit.Should().Be(0);
        result.BlocksAfterUndoEnter.Should().ContainSingle().Which.Should().Be("p1");
        result.TextAfterUndoEnter.Should().Be("Alpha beta");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_History_ImageDragAndRevisionAcceptUndoRestoreModelAndLayout()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<HistoryObjectRevisionProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase15-objects',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [
                            { Id: 'r1', Text: 'Priority ', RevisionId: 'rev1' },
                            { Id: 'r2', Text: 'support' }
                        ] } },
                        { Id: 'img1', Type: 'Image', Content: {
                            ObjectId: 'img-o1',
                            AltText: 'Evidence',
                            Layout: { WrapMode: 'Square', Position: { X: 40, Y: 80 }, Size: { Width: 120, Height: 80 }, Anchor: { BlockId: 'p1', Offset: 0 } }
                        } }
                    ],
                    Revisions: [{
                        Id: 'rev1',
                        Type: 'Insertion',
                        Author: 'u1',
                        Timestamp: 1,
                        AffectedRange: { BlockId: 'p1', Start: 0, End: 9 },
                        Payload: { text: 'Priority ' },
                        Status: 'Pending'
                    }]
                });
                const history = engine.history.createHistoryController(model, { selection: { blockId: 'p1', offset: 9, isCollapsed: true } });
                const imageMove = history.commitOperation(engine.operations.createOperation(engine.operations.types.UpdateImageLayout, {
                    target: { blockId: 'img1', offset: 0 },
                    layout: { WrapMode: 'Square', Position: { X: 180, Y: 130 }, Size: { Width: 120, Height: 80 }, Anchor: { BlockId: 'p1', Offset: 0 } },
                    affectedParagraphIds: ['p1']
                }, { source: 'image-drag' }), { transactionType: 'image-drag', label: 'Move image' });
                const movedX = model.body.blocks[1].content.layout.Position.X;
                const undoImage = history.undo();
                const imageUndoX = model.body.blocks[1].content.layout.Position.X;
                const redoImage = history.redo();
                const imageRedoX = model.body.blocks[1].content.layout.Position.X;
                const resizeBeforeDepth = history.debug().undoDepth;
                const imageResize = history.commitOperation(engine.operations.createOperation(engine.operations.types.UpdateImageLayout, {
                    target: { blockId: 'img1', offset: 0 },
                    layout: { WrapMode: 'Square', Position: { X: 180, Y: 130 }, Size: { Width: 160, Height: 90 }, Anchor: { BlockId: 'p1', Offset: 0 } },
                    affectedParagraphIds: ['p1']
                }, { source: 'image-resize' }), { transactionType: 'image-resize', label: 'Resize image' });
                const resizeAfterDepth = history.debug().undoDepth;
                const resizedWidth = model.body.blocks[1].content.layout.Size.Width;
                const undoResize = history.undo();
                const widthAfterUndoResize = model.body.blocks[1].content.layout.Size.Width;
                const accept = history.commitOperation(engine.operations.createOperation(engine.operations.types.AcceptRevision, {
                    revisionId: 'rev1',
                    selection: { blockId: 'p1', offset: 9, isCollapsed: true }
                }, { source: 'review' }), { transactionType: 'revision', label: 'Accept revision' });
                const revisionAfterAccept = model.revisions.find(revision => revision.id === 'rev1').status;
                const runRevisionAfterAccept = model.body.blocks[0].content.runs[0].revisionId || null;
                const undoAccept = history.undo();
                const revisionAfterUndo = model.revisions.find(revision => revision.id === 'rev1').status;
                const runRevisionAfterUndo = model.body.blocks[0].content.runs[0].revisionId || null;
                return {
                    imageMoveOk: imageMove.ok,
                    movedX,
                    undoImageType: undoImage.transaction.type,
                    imageUndoX,
                    redoImageType: redoImage.transaction.type,
                    imageRedoX,
                    imageInvalidatedP1: redoImage.transaction.invalidatedScopes.includes('p1'),
                    imageResizeOk: imageResize.ok,
                    imageResizeSeparate: resizeAfterDepth === resizeBeforeDepth + 1,
                    resizedWidth,
                    undoResizeType: undoResize.transaction.type,
                    widthAfterUndoResize,
                    acceptOk: accept.ok,
                    revisionAfterAccept,
                    runRevisionAfterAccept,
                    undoAcceptType: undoAccept.transaction.type,
                    revisionAfterUndo,
                    runRevisionAfterUndo,
                    undoAcceptSelectionBlock: undoAccept.selection.blockId,
                    undoAcceptSelectionOffset: undoAccept.selection.offset
                };
            }
            """);

        result.ImageMoveOk.Should().BeTrue();
        result.MovedX.Should().Be(180);
        result.UndoImageType.Should().Be("undo");
        result.ImageUndoX.Should().Be(40);
        result.RedoImageType.Should().Be("redo");
        result.ImageRedoX.Should().Be(180);
        result.ImageInvalidatedP1.Should().BeTrue();
        result.ImageResizeOk.Should().BeTrue();
        result.ImageResizeSeparate.Should().BeTrue();
        result.ResizedWidth.Should().Be(160);
        result.UndoResizeType.Should().Be("undo");
        result.WidthAfterUndoResize.Should().Be(120);
        result.AcceptOk.Should().BeTrue();
        result.RevisionAfterAccept.Should().Be("Accepted");
        result.RunRevisionAfterAccept.Should().BeNull();
        result.UndoAcceptType.Should().Be("undo");
        result.RevisionAfterUndo.Should().Be("Pending");
        result.RunRevisionAfterUndo.Should().Be("rev1");
        result.UndoAcceptSelectionBlock.Should().Be("p1");
        result.UndoAcceptSelectionOffset.Should().Be(9);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_History_PublicEngineUndoRedoUpdatesToolbarState()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<HistoryPublicEngineProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const host = document.createElement('div');
                document.body.appendChild(host);
                const id = engine.create(host, { instanceId: 'phase15-public' });
                engine.loadDocument(id, {
                    DocumentId: 'phase15-public-doc',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta' }] } }]
                });
                const bold = engine.applyCommand(id, 'ApplyMark', {
                    transactionType: 'toolbar',
                    range: { blockId: 'p1', start: 0, end: 5 },
                    mark: { type: 'Bold' }
                });
                const stateAfterBold = engine.commands.collectFormattingState(engine.getDocumentSnapshot(id).document, {
                    blockId: 'p1',
                    anchor: { blockId: 'p1', offset: 0 },
                    focus: { blockId: 'p1', offset: 5 }
                });
                const undo = engine.applyCommand(id, 'undo', {});
                const stateAfterUndo = engine.commands.collectFormattingState(engine.getDocumentSnapshot(id).document, {
                    blockId: 'p1',
                    anchor: { blockId: 'p1', offset: 0 },
                    focus: { blockId: 'p1', offset: 5 }
                });
                const redo = engine.applyCommand(id, 'redo', {});
                const stateAfterRedo = engine.commands.collectFormattingState(engine.getDocumentSnapshot(id).document, {
                    blockId: 'p1',
                    anchor: { blockId: 'p1', offset: 0 },
                    focus: { blockId: 'p1', offset: 5 }
                });
                const debug = engine.getDebugSnapshot(id);
                engine.dispose(id);
                host.remove();
                return {
                    boldOk: bold.ok,
                    stateAfterBold: stateAfterBold.commandValues.bold,
                    undoOk: undo.ok,
                    undoType: undo.transaction.type,
                    stateAfterUndo: stateAfterUndo.commandValues.bold,
                    redoOk: redo.ok,
                    redoType: redo.transaction.type,
                    stateAfterRedo: stateAfterRedo.commandValues.bold,
                    undoDepth: debug.undoDepth,
                    redoDepth: debug.redoDepth,
                    lastCommand: debug.lastTransaction.type
                };
            }
            """);

        result.BoldOk.Should().BeTrue();
        result.StateAfterBold.Should().BeTrue();
        result.UndoOk.Should().BeTrue();
        result.UndoType.Should().Be("undo");
        result.StateAfterUndo.Should().BeFalse();
        result.RedoOk.Should().BeTrue();
        result.RedoType.Should().Be("redo");
        result.StateAfterRedo.Should().BeTrue();
        result.UndoDepth.Should().Be(1);
        result.RedoDepth.Should().Be(0);
        result.LastCommand.Should().Be("redo");
    }

    private sealed class HistoryTypingProbe
    {
        [JsonPropertyName("afterTyping")] public string AfterTyping { get; set; } = "";
        [JsonPropertyName("undoDepthAfterTyping")] public int UndoDepthAfterTyping { get; set; }
        [JsonPropertyName("coalesced")] public bool Coalesced { get; set; }
        [JsonPropertyName("undoOk")] public bool UndoOk { get; set; }
        [JsonPropertyName("undoTransactionType")] public string UndoTransactionType { get; set; } = "";
        [JsonPropertyName("undoAppliedSource")] public string UndoAppliedSource { get; set; } = "";
        [JsonPropertyName("afterUndo")] public string AfterUndo { get; set; } = "";
        [JsonPropertyName("undoSelectionBlock")] public string UndoSelectionBlock { get; set; } = "";
        [JsonPropertyName("undoSelectionOffset")] public int UndoSelectionOffset { get; set; }
        [JsonPropertyName("redoOk")] public bool RedoOk { get; set; }
        [JsonPropertyName("redoTransactionType")] public string RedoTransactionType { get; set; } = "";
        [JsonPropertyName("redoAppliedSource")] public string RedoAppliedSource { get; set; } = "";
        [JsonPropertyName("afterRedo")] public string AfterRedo { get; set; } = "";
        [JsonPropertyName("redoSelectionBlock")] public string RedoSelectionBlock { get; set; } = "";
        [JsonPropertyName("redoSelectionOffset")] public int RedoSelectionOffset { get; set; }
        [JsonPropertyName("invalidatedScopes")] public string[] InvalidatedScopes { get; set; } = [];
        [JsonPropertyName("renderAdvanced")] public bool RenderAdvanced { get; set; }
    }

    private sealed class HistoryBoundaryProbe
    {
        [JsonPropertyName("enterOk")] public bool EnterOk { get; set; }
        [JsonPropertyName("pasteOperationCount")] public int PasteOperationCount { get; set; }
        [JsonPropertyName("toolbarOk")] public bool ToolbarOk { get; set; }
        [JsonPropertyName("beforeUndoDepth")] public int BeforeUndoDepth { get; set; }
        [JsonPropertyName("undoToolbarType")] public string UndoToolbarType { get; set; } = "";
        [JsonPropertyName("redoToolbarType")] public string RedoToolbarType { get; set; } = "";
        [JsonPropertyName("redoDepthBeforeNewEdit")] public int RedoDepthBeforeNewEdit { get; set; }
        [JsonPropertyName("newEditOk")] public bool NewEditOk { get; set; }
        [JsonPropertyName("redoDepthAfterNewEdit")] public int RedoDepthAfterNewEdit { get; set; }
        [JsonPropertyName("blocksAfterUndoEnter")] public string[] BlocksAfterUndoEnter { get; set; } = [];
        [JsonPropertyName("textAfterUndoEnter")] public string TextAfterUndoEnter { get; set; } = "";
    }

    private sealed class HistoryObjectRevisionProbe
    {
        [JsonPropertyName("imageMoveOk")] public bool ImageMoveOk { get; set; }
        [JsonPropertyName("movedX")] public int MovedX { get; set; }
        [JsonPropertyName("undoImageType")] public string UndoImageType { get; set; } = "";
        [JsonPropertyName("imageUndoX")] public int ImageUndoX { get; set; }
        [JsonPropertyName("redoImageType")] public string RedoImageType { get; set; } = "";
        [JsonPropertyName("imageRedoX")] public int ImageRedoX { get; set; }
        [JsonPropertyName("imageInvalidatedP1")] public bool ImageInvalidatedP1 { get; set; }
        [JsonPropertyName("imageResizeOk")] public bool ImageResizeOk { get; set; }
        [JsonPropertyName("imageResizeSeparate")] public bool ImageResizeSeparate { get; set; }
        [JsonPropertyName("resizedWidth")] public int ResizedWidth { get; set; }
        [JsonPropertyName("undoResizeType")] public string UndoResizeType { get; set; } = "";
        [JsonPropertyName("widthAfterUndoResize")] public int WidthAfterUndoResize { get; set; }
        [JsonPropertyName("acceptOk")] public bool AcceptOk { get; set; }
        [JsonPropertyName("revisionAfterAccept")] public string RevisionAfterAccept { get; set; } = "";
        [JsonPropertyName("runRevisionAfterAccept")] public string? RunRevisionAfterAccept { get; set; }
        [JsonPropertyName("undoAcceptType")] public string UndoAcceptType { get; set; } = "";
        [JsonPropertyName("revisionAfterUndo")] public string RevisionAfterUndo { get; set; } = "";
        [JsonPropertyName("runRevisionAfterUndo")] public string? RunRevisionAfterUndo { get; set; }
        [JsonPropertyName("undoAcceptSelectionBlock")] public string UndoAcceptSelectionBlock { get; set; } = "";
        [JsonPropertyName("undoAcceptSelectionOffset")] public int UndoAcceptSelectionOffset { get; set; }
    }

    private sealed class HistoryPublicEngineProbe
    {
        [JsonPropertyName("boldOk")] public bool BoldOk { get; set; }
        [JsonPropertyName("stateAfterBold")] public bool StateAfterBold { get; set; }
        [JsonPropertyName("undoOk")] public bool UndoOk { get; set; }
        [JsonPropertyName("undoType")] public string UndoType { get; set; } = "";
        [JsonPropertyName("stateAfterUndo")] public bool StateAfterUndo { get; set; }
        [JsonPropertyName("redoOk")] public bool RedoOk { get; set; }
        [JsonPropertyName("redoType")] public string RedoType { get; set; } = "";
        [JsonPropertyName("stateAfterRedo")] public bool StateAfterRedo { get; set; }
        [JsonPropertyName("undoDepth")] public int UndoDepth { get; set; }
        [JsonPropertyName("redoDepth")] public int RedoDepth { get; set; }
        [JsonPropertyName("lastCommand")] public string LastCommand { get; set; } = "";
    }
}
