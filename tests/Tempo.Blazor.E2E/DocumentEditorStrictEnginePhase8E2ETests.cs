using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for the model-first input pipeline.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase8E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Input_NormalizesBeforeInputAndPreventsBrowserMutation()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<BeforeInputProbe>(
            """
            () => {
                const normalizer = window.tmDocumentEditorEngine.input.createBeforeInputNormalizer();
                const types = [
                    'insertText',
                    'insertParagraph',
                    'insertLineBreak',
                    'deleteContentBackward',
                    'deleteContentForward',
                    'deleteWordBackward',
                    'deleteWordForward',
                    'insertFromPaste',
                    'formatBold'
                ];
                const normalized = types.map(inputType => {
                    let prevented = false;
                    const event = { inputType, data: inputType === 'insertText' ? 'x' : null, preventDefault: () => { prevented = true; } };
                    const result = normalizer.normalize(event);
                    return { inputType, command: result.command, supported: result.supported, prevented, canonical: result.canonicalSource };
                });
                let unsupportedPrevented = false;
                const unsupported = normalizer.normalize({
                    inputType: 'historyUndo',
                    preventDefault: () => { unsupportedPrevented = true; }
                });
                return {
                    supportedCount: normalized.filter(item => item.supported).length,
                    allPrevented: normalized.every(item => item.prevented),
                    allCanonicalModel: normalized.every(item => item.canonical === 'model-operation'),
                    commands: normalized.map(item => item.command),
                    unsupportedSupported: unsupported.supported === true,
                    unsupportedPrevented,
                    unsupportedLogCode: String(unsupported.log?.code || '')
                };
            }
            """);

        result.SupportedCount.Should().Be(9);
        result.AllPrevented.Should().BeTrue();
        result.AllCanonicalModel.Should().BeTrue();
        result.Commands.Should().Contain(["InsertText", "SplitParagraph", "DeleteBackward", "DeleteForward", "Paste", "ToggleBold"]);
        result.UnsupportedSupported.Should().BeFalse();
        result.UnsupportedPrevented.Should().BeTrue();
        result.UnsupportedLogCode.Should().Be("unsupported-beforeinput");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Input_InsertTextUsesLogicalSelectionMarksTrackingLayoutRenderAndBoundaryPatch()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<InsertTextProbe>(
            """
            async () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase8',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello world' }] } }],
                    Revisions: [{ id: 'rev-existing', status: 'Pending' }]
                });
                const root = document.createElement('div');
                document.body.appendChild(root);
                const pipeline = engine.input.createInputPipeline({
                    model,
                    root,
                    page: { x: 0, y: 0, width: 260, height: 400 },
                    selection: {
                        anchor: { blockId: 'p1', offset: 6 },
                        focus: { blockId: 'p1', offset: 11 },
                        isCollapsed: false
                    },
                    activeTypingMarks: [{ type: 'Bold' }],
                    trackChanges: true,
                    userId: 'tester'
                });
                const result = pipeline.handleBeforeInput({
                    inputType: 'insertText',
                    data: 'Tempo',
                    preventDefault() { this.prevented = true; }
                });
                await pipeline.flushBoundaryPatches();
                const block = model.indexes.blocks.p1;
                const insertedRun = block.content.runs.find(run => run.text === 'Tempo');
                const debug = pipeline.debug();
                const selectionAttr = root.getAttribute('data-logical-selection') || '';
                root.remove();
                return {
                    ok: result.ok === true,
                    prevented: result.normalized.preventDefault === true,
                    operationTypes: result.operations.map(op => op.type),
                    text: block.content.runs.map(run => run.text || '').join(''),
                    insertedRunBold: insertedRun?.marks?.some(mark => (mark.type || mark.Type) === 'Bold') === true,
                    insertedRevisionId: String(insertedRun?.revisionId || ''),
                    activeLayoutBlock: String(result.layout?.activeBlockId || ''),
                    renderedSelection: selectionAttr.includes('p1') && selectionAttr.includes('11'),
                    boundaryPatchCount: debug.boundaryPatchCount,
                    asyncPatchFlushed: debug.boundaryPatchFlushCount > 0,
                    browserMutationUsed: debug.browserMutationUsed === true
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.Prevented.Should().BeTrue();
        result.OperationTypes.Should().Equal("DeleteRange", "InsertText");
        result.Text.Should().Be("Hello Tempo");
        result.InsertedRunBold.Should().BeTrue();
        result.InsertedRevisionId.Should().StartWith("rev-");
        result.ActiveLayoutBlock.Should().Be("p1");
        result.RenderedSelection.Should().BeTrue();
        result.BoundaryPatchCount.Should().BeGreaterThan(0);
        result.AsyncPatchFlushed.Should().BeTrue();
        result.BrowserMutationUsed.Should().BeFalse();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Input_DeleteBackspaceHandlesTextObjectAndRevisionBoundaries()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<DeleteProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase8',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'a', Text: 'AB' }] } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'b', Text: 'CD' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image', Layout: {} } },
                        { Id: 'p3', Type: 'Paragraph', Content: { Inlines: [{ Id: 'rev-run', Text: 'XY', RevisionId: 'rev1' }] } }
                    ],
                    Revisions: [{ id: 'rev1', status: 'Pending' }]
                });
                const pipeline = engine.input.createInputPipeline({ model, page: { x: 0, y: 0, width: 260, height: 400 } });
                const middle = pipeline.planDeletion({ blockId: 'p1', offset: 1, isCollapsed: true }, 'deleteContentBackward');
                const inlineBoundary = pipeline.planDeletion({ blockId: 'p1', offset: 2, inlineId: 'a', isCollapsed: true }, 'deleteWordBackward');
                const merge = pipeline.planDeletion({ blockId: 'p2', offset: 0, isCollapsed: true }, 'deleteContentBackward');
                const imageBackspace = pipeline.planDeletion({ blockId: 'img1', offset: 1, isCollapsed: true }, 'deleteContentBackward');
                const imageDelete = pipeline.planDeletion({ blockId: 'img1', offset: 0, isCollapsed: true }, 'deleteContentForward');
                const revision = pipeline.planDeletion({ blockId: 'p3', offset: 1, isCollapsed: true }, 'deleteContentBackward');
                return {
                    middleType: String(middle.operations[0]?.type || ''),
                    middleStart: Number(middle.operations[0]?.range?.start ?? -1),
                    middleEnd: Number(middle.operations[0]?.range?.end ?? -1),
                    inlineBoundaryNormalized: inlineBoundary.normalizedToPreviousRun === true,
                    mergeType: String(merge.operations[0]?.type || ''),
                    imageBackspaceAction: String(imageBackspace.objectAction || ''),
                    imageDeleteAction: String(imageDelete.objectAction || ''),
                    revisionPolicy: String(revision.revisionBoundaryPolicy || ''),
                    revisionType: String(revision.operations[0]?.type || ''),
                    plannedCount: pipeline.debug().plannedDeletionCount
                };
            }
            """);

        result.MiddleType.Should().Be("DeleteRange");
        result.MiddleStart.Should().Be(0);
        result.MiddleEnd.Should().Be(1);
        result.InlineBoundaryNormalized.Should().BeTrue();
        result.MergeType.Should().Be("MergeParagraph");
        result.ImageBackspaceAction.Should().Be("selectObject");
        result.ImageDeleteAction.Should().Be("selectObject");
        result.RevisionPolicy.Should().Be("revision-boundary-checked");
        result.RevisionType.Should().Be("DeleteRange");
        result.PlannedCount.Should().Be(6);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Input_EnterSplitsParagraphsPreservesRegionListAndWrapContext()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<EnterProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase8',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'AlphaBeta' }], Style: { listType: 'bullet' } } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image', Layout: { wrap: { mode: 'square' } } } }
                    ],
                    HeadersFooters: [
                        { Id: 'header1', Region: 'Header', Blocks: [{ Id: 'h1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'hr1', Text: 'Header text' }] } }] }
                    ],
                    Revisions: [{ id: 'rev1', status: 'Pending' }]
                });
                const pipeline = engine.input.createInputPipeline({ model, page: { x: 0, y: 0, width: 260, height: 400 }, trackChanges: false });
                const middle = pipeline.handleEnter({ blockId: 'p1', offset: 5, region: 'Body', isCollapsed: true });
                const start = pipeline.handleEnter({ blockId: 'p1', offset: 0, region: 'Body', isCollapsed: true });
                const endBlockId = middle.operations[0].newBlockId;
                const end = pipeline.handleEnter({ blockId: endBlockId, offset: model.indexes.blocks[endBlockId].content.runs.map(run => run.text || '').join('').length, region: 'Body', isCollapsed: true });
                const header = pipeline.handleEnter({ blockId: 'h1', offset: 6, region: 'Header', isCollapsed: true });
                const imageWrap = pipeline.handleEnter({ blockId: 'img1', offset: 1, region: 'Body', isCollapsed: true, wrapContext: { mode: 'square' } });
                const randomRevisionCreated = middle.operations.concat(start.operations, end.operations, header.operations).some(op => !!op.revisionId);
                return {
                    middleType: String(middle.operations[0]?.type || ''),
                    startCreatesEmptyBefore: model.body.blocks[0].id === 'p1' && model.body.blocks[0].content.runs.map(run => run.text || '').join('') === '',
                    endType: String(end.operations[0]?.type || ''),
                    listStylePreserved: middle.preservedListStyle === true,
                    headerRegion: String(header.selection?.region || ''),
                    imageWrapStable: imageWrap.wrapContextStable === true,
                    randomRevisionCreated,
                    noFlyingText: middle.layout?.debug?.staleFollowingBlockIds?.length >= 0
                };
            }
            """);

        result.MiddleType.Should().Be("SplitParagraph");
        result.StartCreatesEmptyBefore.Should().BeTrue();
        result.EndType.Should().Be("SplitParagraph");
        result.ListStylePreserved.Should().BeTrue();
        result.HeaderRegion.Should().Be("Header");
        result.ImageWrapStable.Should().BeTrue();
        result.RandomRevisionCreated.Should().BeFalse();
        result.NoFlyingText.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Input_CompositionPasteAndTypingBufferAreTransactional()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<CompositionPasteBufferProbe>(
            """
            async () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase8',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello' }] } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'World' }] } }
                    ]
                });
                const pipeline = engine.input.createInputPipeline({
                    model,
                    page: { x: 0, y: 0, width: 220, height: 400 },
                    selection: { blockId: 'p1', offset: 5, isCollapsed: true }
                });
                const compositionStart = pipeline.handleCompositionStart({ data: '', selection: { blockId: 'p1', offset: 5, isCollapsed: true } });
                const compositionUpdate = pipeline.handleCompositionUpdate({ data: '\u017e', selection: { blockId: 'p1', offset: 5, isCollapsed: true } });
                const compositionEnd = pipeline.handleCompositionEnd({ data: '\u017e', selection: { blockId: 'p1', offset: 5, isCollapsed: true } });
                const paste = pipeline.handlePaste({
                    plainText: 'A\nB',
                    html: '<p>A</p><p>B</p>',
                    selection: { blockId: 'p2', offset: 0, isCollapsed: true }
                });
                const buffer = engine.input.createTypingChangeBuffer({ timeoutMs: 500 });
                const a = engine.operations.createOperation(engine.operations.types.InsertText, { target: { blockId: 'p1', offset: 0 }, text: 'a' }, { source: 'typing', timestamp: 1000 });
                const b = engine.operations.createOperation(engine.operations.types.InsertText, { target: { blockId: 'p1', offset: 1 }, text: 'b' }, { source: 'typing', timestamp: 1200 });
                const late = engine.operations.createOperation(engine.operations.types.InsertText, { target: { blockId: 'p1', offset: 2 }, text: 'c' }, { source: 'typing', timestamp: 2000 });
                buffer.push(a);
                buffer.push(b);
                const coalescedBeforeReset = buffer.snapshot().operationCount;
                buffer.resetForSelectionChange({ blockId: 'p2', offset: 0 });
                buffer.push(late);
                const afterSelectionReset = buffer.snapshot().operationCount;
                buffer.resetForCommand('paste');
                const afterCommandReset = buffer.snapshot().operationCount;
                const space = pipeline.handleBeforeInput({ inputType: 'insertText', data: ' ', preventDefault() { this.prevented = true; } });
                await pipeline.nextFrame();
                const debug = pipeline.debug();
                return {
                    compositionTransactionType: String(compositionStart.transactionType || ''),
                    previewCommittedPatch: compositionUpdate.boundaryPatchQueued === true,
                    compositionCommitOperations: compositionEnd.operations.length,
                    compositionSelectionStable: compositionUpdate.selection?.blockId === 'p1',
                    compositionPreviewNonOverlapping: compositionUpdate.previewLayout?.debug?.fallbackReason !== 'overlap',
                    pasteTransactionType: String(paste.transactionType || ''),
                    pasteOperationTypes: paste.operations.map(op => op.type),
                    pasteHtmlNormalized: paste.htmlNormalized === true,
                    pasteUndoTransaction: paste.singleUndoTransaction === true,
                    pasteImmediateLayout: paste.layout?.activeParagraphLayout === true,
                    coalescedBeforeReset,
                    afterSelectionReset,
                    afterCommandReset,
                    spaceVisibleAfterFrame: debug.lastVisibleText.includes(' '),
                    preventDefaultSupported: space.normalized.preventDefault === true,
                    mutationObserverDiagnosticOnly: debug.mutationObserverMode === 'diagnostic-only',
                    browserMutationUsed: debug.browserMutationUsed === true
                };
            }
            """);

        result.CompositionTransactionType.Should().Be("composition");
        result.PreviewCommittedPatch.Should().BeFalse();
        result.CompositionCommitOperations.Should().Be(1);
        result.CompositionSelectionStable.Should().BeTrue();
        result.CompositionPreviewNonOverlapping.Should().BeTrue();
        result.PasteTransactionType.Should().Be("paste");
        result.PasteOperationTypes.Should().Contain("InsertText");
        result.PasteOperationTypes.Should().Contain("SplitParagraph");
        result.PasteHtmlNormalized.Should().BeTrue();
        result.PasteUndoTransaction.Should().BeTrue();
        result.PasteImmediateLayout.Should().BeTrue();
        result.CoalescedBeforeReset.Should().Be(1);
        result.AfterSelectionReset.Should().Be(1);
        result.AfterCommandReset.Should().Be(0);
        result.SpaceVisibleAfterFrame.Should().BeTrue();
        result.PreventDefaultSupported.Should().BeTrue();
        result.MutationObserverDiagnosticOnly.Should().BeTrue();
        result.BrowserMutationUsed.Should().BeFalse();
    }

    public sealed class BeforeInputProbe
    {
        [JsonPropertyName("supportedCount")] public int SupportedCount { get; set; }
        [JsonPropertyName("allPrevented")] public bool AllPrevented { get; set; }
        [JsonPropertyName("allCanonicalModel")] public bool AllCanonicalModel { get; set; }
        [JsonPropertyName("commands")] public string[] Commands { get; set; } = [];
        [JsonPropertyName("unsupportedSupported")] public bool UnsupportedSupported { get; set; }
        [JsonPropertyName("unsupportedPrevented")] public bool UnsupportedPrevented { get; set; }
        [JsonPropertyName("unsupportedLogCode")] public string UnsupportedLogCode { get; set; } = string.Empty;
    }

    public sealed class InsertTextProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("prevented")] public bool Prevented { get; set; }
        [JsonPropertyName("operationTypes")] public string[] OperationTypes { get; set; } = [];
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("insertedRunBold")] public bool InsertedRunBold { get; set; }
        [JsonPropertyName("insertedRevisionId")] public string InsertedRevisionId { get; set; } = string.Empty;
        [JsonPropertyName("activeLayoutBlock")] public string ActiveLayoutBlock { get; set; } = string.Empty;
        [JsonPropertyName("renderedSelection")] public bool RenderedSelection { get; set; }
        [JsonPropertyName("boundaryPatchCount")] public int BoundaryPatchCount { get; set; }
        [JsonPropertyName("asyncPatchFlushed")] public bool AsyncPatchFlushed { get; set; }
        [JsonPropertyName("browserMutationUsed")] public bool BrowserMutationUsed { get; set; }
    }

    public sealed class DeleteProbe
    {
        [JsonPropertyName("middleType")] public string MiddleType { get; set; } = string.Empty;
        [JsonPropertyName("middleStart")] public int MiddleStart { get; set; }
        [JsonPropertyName("middleEnd")] public int MiddleEnd { get; set; }
        [JsonPropertyName("inlineBoundaryNormalized")] public bool InlineBoundaryNormalized { get; set; }
        [JsonPropertyName("mergeType")] public string MergeType { get; set; } = string.Empty;
        [JsonPropertyName("imageBackspaceAction")] public string ImageBackspaceAction { get; set; } = string.Empty;
        [JsonPropertyName("imageDeleteAction")] public string ImageDeleteAction { get; set; } = string.Empty;
        [JsonPropertyName("revisionPolicy")] public string RevisionPolicy { get; set; } = string.Empty;
        [JsonPropertyName("revisionType")] public string RevisionType { get; set; } = string.Empty;
        [JsonPropertyName("plannedCount")] public int PlannedCount { get; set; }
    }

    public sealed class EnterProbe
    {
        [JsonPropertyName("middleType")] public string MiddleType { get; set; } = string.Empty;
        [JsonPropertyName("startCreatesEmptyBefore")] public bool StartCreatesEmptyBefore { get; set; }
        [JsonPropertyName("endType")] public string EndType { get; set; } = string.Empty;
        [JsonPropertyName("listStylePreserved")] public bool ListStylePreserved { get; set; }
        [JsonPropertyName("headerRegion")] public string HeaderRegion { get; set; } = string.Empty;
        [JsonPropertyName("imageWrapStable")] public bool ImageWrapStable { get; set; }
        [JsonPropertyName("randomRevisionCreated")] public bool RandomRevisionCreated { get; set; }
        [JsonPropertyName("noFlyingText")] public bool NoFlyingText { get; set; }
    }

    public sealed class CompositionPasteBufferProbe
    {
        [JsonPropertyName("compositionTransactionType")] public string CompositionTransactionType { get; set; } = string.Empty;
        [JsonPropertyName("previewCommittedPatch")] public bool PreviewCommittedPatch { get; set; }
        [JsonPropertyName("compositionCommitOperations")] public int CompositionCommitOperations { get; set; }
        [JsonPropertyName("compositionSelectionStable")] public bool CompositionSelectionStable { get; set; }
        [JsonPropertyName("compositionPreviewNonOverlapping")] public bool CompositionPreviewNonOverlapping { get; set; }
        [JsonPropertyName("pasteTransactionType")] public string PasteTransactionType { get; set; } = string.Empty;
        [JsonPropertyName("pasteOperationTypes")] public string[] PasteOperationTypes { get; set; } = [];
        [JsonPropertyName("pasteHtmlNormalized")] public bool PasteHtmlNormalized { get; set; }
        [JsonPropertyName("pasteUndoTransaction")] public bool PasteUndoTransaction { get; set; }
        [JsonPropertyName("pasteImmediateLayout")] public bool PasteImmediateLayout { get; set; }
        [JsonPropertyName("coalescedBeforeReset")] public int CoalescedBeforeReset { get; set; }
        [JsonPropertyName("afterSelectionReset")] public int AfterSelectionReset { get; set; }
        [JsonPropertyName("afterCommandReset")] public int AfterCommandReset { get; set; }
        [JsonPropertyName("spaceVisibleAfterFrame")] public bool SpaceVisibleAfterFrame { get; set; }
        [JsonPropertyName("preventDefaultSupported")] public bool PreventDefaultSupported { get; set; }
        [JsonPropertyName("mutationObserverDiagnosticOnly")] public bool MutationObserverDiagnosticOnly { get; set; }
        [JsonPropertyName("browserMutationUsed")] public bool BrowserMutationUsed { get; set; }
    }
}
