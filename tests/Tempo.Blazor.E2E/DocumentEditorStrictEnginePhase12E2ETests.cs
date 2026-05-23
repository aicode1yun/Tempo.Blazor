using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for semantic revision overlays and tracked-change behavior.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase12E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Revisions_ModelIsSemanticAndToolbarIgnoresDecorations()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<RevisionModelProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase12-model',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [
                        { Id: 'r1', Text: 'Actual text', Marks: [{ type: 'Italic' }], Style: { color: '#111111' } }
                    ] } }]
                });
                const revisions = engine.revisions.createRevisionEngine(model, { userId: 'u1' });
                const revision = revisions.createRevision('Insertion', {
                    blockId: 'p1',
                    start: 6,
                    end: 10
                }, {
                    text: ' semantic',
                    decorativeStyle: { color: '#008000', underline: true }
                });
                const formatting = revisions.getActualFormattingState({ blockId: 'p1', offset: 7 });
                const overlay = revisions.createOverlayModel();
                return {
                    id: revision.id,
                    type: revision.type,
                    author: revision.author,
                    hasTimestamp: typeof revision.timestamp === 'number' && revision.timestamp > 0,
                    rangeBlockId: revision.affectedRange.blockId,
                    rangeStart: revision.affectedRange.start,
                    payloadText: revision.payload.text,
                    revisionNotInMarks: !formatting.marks.some(mark => String(mark.type || mark.Type).toLowerCase().includes('revision')),
                    actualItalic: formatting.marks.some(mark => String(mark.type || mark.Type).toLowerCase() === 'italic'),
                    actualColor: formatting.style.color,
                    decorativeColorIgnored: formatting.style.color !== '#008000',
                    overlayReadsRevisionModel: overlay.markers.some(marker => marker.revisionId === revision.id && marker.range.blockId === 'p1')
                };
            }
            """);

        result.Id.Should().NotBeNullOrWhiteSpace();
        result.Type.Should().Be("Insertion");
        result.Author.Should().Be("u1");
        result.HasTimestamp.Should().BeTrue();
        result.RangeBlockId.Should().Be("p1");
        result.RangeStart.Should().Be(6);
        result.PayloadText.Should().Be(" semantic");
        result.RevisionNotInMarks.Should().BeTrue();
        result.ActualItalic.Should().BeTrue();
        result.ActualColor.Should().Be("#111111");
        result.DecorativeColorIgnored.Should().BeTrue();
        result.OverlayReadsRevisionModel.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Revisions_RenderOverlayAndVisibleTextRespectReviewMode()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<RevisionOverlayProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase12-overlay',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [
                        { Id: 'r1', Text: 'Alpha ' },
                        { Id: 'r2', Text: 'deleted', RevisionId: 'rev-del' },
                        { Id: 'r3', Text: ' omega' }
                    ] } }],
                    Revisions: [{
                        Id: 'rev-del',
                        Type: 'Deletion',
                        Author: 'u1',
                        Timestamp: 1,
                        AffectedRange: { BlockId: 'p1', Start: 6, End: 13 },
                        Payload: { text: 'deleted' },
                        Status: 'Pending'
                    }]
                });
                const revisions = engine.revisions.createRevisionEngine(model);
                const visibleShowMarkup = revisions.getVisibleText('p1', 'showMarkup');
                const visibleFinal = revisions.getVisibleText('p1', 'final');
                const overlayModel = revisions.createOverlayModel('showMarkup');
                const root = document.createElement('div');
                document.body.appendChild(root);
                revisions.renderOverlay(root, overlayModel);
                const marker = root.querySelector('[data-revision-overlay-id="rev-del"]');
                const popover = revisions.createReviewPopover('rev-del');
                root.remove();
                return {
                    visibleShowMarkup,
                    visibleFinal,
                    markerRendered: !!marker,
                    markerType: marker?.getAttribute('data-revision-type') || '',
                    overlayZIndex: marker ? Number(marker.style.zIndex || 0) : 0,
                    mappedBlockId: overlayModel.markers[0]?.range?.blockId || '',
                    popoverType: popover.revision.type,
                    markerDifferScopes: revisions.createMarkerDiffer(['rev-del']).invalidatedOverlayScopes
                };
            }
            """);

        result.VisibleShowMarkup.Should().Be("Alpha deleted omega");
        result.VisibleFinal.Should().Be("Alpha  omega");
        result.MarkerRendered.Should().BeTrue();
        result.MarkerType.Should().Be("Deletion");
        result.OverlayZIndex.Should().BeGreaterThan(1);
        result.MappedBlockId.Should().Be("p1");
        result.PopoverType.Should().Be("Deletion");
        result.MarkerDifferScopes.Should().Contain("rev-del");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Revisions_InputBoundariesDoNotJoinRevisionWhenTrackingOff()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<RevisionBoundaryProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase12-boundary',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [
                        { Id: 'r1', Text: 'Alpha ' },
                        { Id: 'r2', Text: 'revision', RevisionId: 'rev-ins' },
                        { Id: 'r3', Text: ' omega' }
                    ] } }],
                    Revisions: [{
                        Id: 'rev-ins',
                        Type: 'Insertion',
                        Author: 'u1',
                        Timestamp: 1,
                        AffectedRange: { BlockId: 'p1', Start: 6, End: 14 },
                        Payload: { text: 'revision' },
                        Status: 'Pending'
                    }]
                });
                const revisions = engine.revisions.createRevisionEngine(model, { trackChanges: false });
                const before = revisions.insertText({ blockId: 'p1', offset: 6, isCollapsed: true }, 'plain ');
                const after = revisions.insertText({ blockId: 'p1', offset: 20, isCollapsed: true }, ' tail');
                const space = revisions.insertText({ blockId: 'p1', offset: 25, isCollapsed: true }, ' ');
                const enter = revisions.splitParagraph({ blockId: 'p1', offset: 26, isCollapsed: true });
                const text = model.body.blocks.map(block => block.content.runs.map(run => run.text).join('')).join('|');
                const normalRuns = model.body.blocks.flatMap(block => block.content.runs).filter(run => !run.revisionId).map(run => run.text).join('');
                const revisionRunCount = model.body.blocks.flatMap(block => block.content.runs).filter(run => run.revisionId === 'rev-ins').length;
                return {
                    beforeRevisionId: before.insertedRun.revisionId || '',
                    afterRevisionId: after.insertedRun.revisionId || '',
                    spaceRevisionId: space.insertedRun.revisionId || '',
                    enterBlockId: enter.selection.blockId,
                    enterNotAtDocumentStart: enter.selection.blockId !== 'p1' || enter.selection.offset !== 0,
                    wordOrder: text,
                    normalRuns,
                    revisionRunCount
                };
            }
            """);

        result.BeforeRevisionId.Should().BeEmpty();
        result.AfterRevisionId.Should().BeEmpty();
        result.SpaceRevisionId.Should().BeEmpty();
        result.EnterBlockId.Should().NotBe("p1");
        result.EnterNotAtDocumentStart.Should().BeTrue();
        result.WordOrder.Should().Contain("plain");
        result.WordOrder.Should().Contain("revision");
        result.WordOrder.Should().Contain("tail");
        result.RevisionRunCount.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Revisions_TrackChangesCreatesInsertionDeletionAndFormatRevisions()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<TrackChangesProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase12-track',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta gamma' }] } }]
                });
                const revisions = engine.revisions.createRevisionEngine(model, { trackChanges: true, userId: 'u2' });
                const insert1 = revisions.insertText({ blockId: 'p1', offset: 5, isCollapsed: true }, ' one');
                const insert2 = revisions.insertText(insert1.selection, ' two');
                const deletion = revisions.deleteRange({ blockId: 'p1', start: 0, end: 5 });
                const format = revisions.applyFormatChange({ blockId: 'p1', start: 6, end: 10 }, { type: 'Bold' });
                const formatting = revisions.getActualFormattingState({ blockId: 'p1', offset: 7 });
                const types = model.revisions.map(revision => revision.type || revision.Type);
                return {
                    insertionRevisionId: insert1.revisionId,
                    coalescedInsertion: insert2.revisionId === insert1.revisionId,
                    deletionRevisionId: deletion.revisionId,
                    formatRevisionId: format.revisionId,
                    types,
                    deletionTextStillVisible: revisions.getVisibleText('p1', 'showMarkup').includes('Alpha'),
                    finalTextHidesDeletion: !revisions.getVisibleText('p1', 'final').startsWith('Alpha'),
                    actualBold: formatting.marks.some(mark => String(mark.type || mark.Type).toLowerCase() === 'bold'),
                    toolbarIgnoresFormatRevisionDecoration: formatting.fromRevisionDecoration === false
                };
            }
            """);

        result.InsertionRevisionId.Should().NotBeNullOrWhiteSpace();
        result.CoalescedInsertion.Should().BeTrue();
        result.DeletionRevisionId.Should().NotBeNullOrWhiteSpace();
        result.FormatRevisionId.Should().NotBeNullOrWhiteSpace();
        result.Types.Should().Contain(["Insertion", "Deletion", "FormatChange"]);
        result.DeletionTextStillVisible.Should().BeTrue();
        result.FinalTextHidesDeletion.Should().BeTrue();
        result.ActualBold.Should().BeFalse();
        result.ToolbarIgnoresFormatRevisionDecoration.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Revisions_AcceptRejectMutatesSemanticContentAndKeepsCaretStable()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<AcceptRejectProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase12-accept',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta gamma' }] } }]
                });
                const revisions = engine.revisions.createRevisionEngine(model, { trackChanges: true, userId: 'u3' });
                const insertion = revisions.insertText({ blockId: 'p1', offset: 5, isCollapsed: true }, ' accepted');
                const deletion = revisions.deleteRange({ blockId: 'p1', start: 0, end: 5 });
                const format = revisions.applyFormatChange({ blockId: 'p1', start: 6, end: 10 }, { type: 'Underline' });
                const acceptInsertion = revisions.acceptRevision(insertion.revisionId, { blockId: 'p1', offset: 14, isCollapsed: true });
                const rejectDeletion = revisions.rejectRevision(deletion.revisionId, acceptInsertion.selection);
                const acceptFormat = revisions.acceptRevision(format.revisionId, rejectDeletion.selection);
                const formatting = revisions.getActualFormattingState({ blockId: 'p1', offset: 7 });
                const layout = engine.textLayout.createParagraphLayoutEngine().layoutDocument(model, { selection: acceptFormat.selection });
                return {
                    text: model.body.blocks[0].content.runs.map(run => run.text).join(''),
                    insertionRunStillRevision: model.body.blocks[0].content.runs.some(run => run.revisionId === insertion.revisionId),
                    deletionRejected: !model.body.blocks[0].content.runs.some(run => run.revisionId === deletion.revisionId),
                    formatAccepted: formatting.marks.some(mark => String(mark.type || mark.Type).toLowerCase() === 'underline'),
                    caretBlock: acceptFormat.selection.blockId,
                    caretOffset: acceptFormat.selection.offset,
                    layoutOk: layout.ok === true,
                    toolbarUnderline: formatting.marks.some(mark => String(mark.type || mark.Type).toLowerCase() === 'underline')
                };
            }
            """);

        result.Text.Should().Contain("accepted");
        result.Text.Should().Contain("Alpha");
        result.InsertionRunStillRevision.Should().BeFalse();
        result.DeletionRejected.Should().BeTrue();
        result.FormatAccepted.Should().BeTrue();
        result.CaretBlock.Should().Be("p1");
        result.CaretOffset.Should().BeGreaterThan(0);
        result.LayoutOk.Should().BeTrue();
        result.ToolbarUnderline.Should().BeTrue();
    }

    public sealed class RevisionModelProbe
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("author")] public string Author { get; set; } = string.Empty;
        [JsonPropertyName("hasTimestamp")] public bool HasTimestamp { get; set; }
        [JsonPropertyName("rangeBlockId")] public string RangeBlockId { get; set; } = string.Empty;
        [JsonPropertyName("rangeStart")] public int RangeStart { get; set; }
        [JsonPropertyName("payloadText")] public string PayloadText { get; set; } = string.Empty;
        [JsonPropertyName("revisionNotInMarks")] public bool RevisionNotInMarks { get; set; }
        [JsonPropertyName("actualItalic")] public bool ActualItalic { get; set; }
        [JsonPropertyName("actualColor")] public string ActualColor { get; set; } = string.Empty;
        [JsonPropertyName("decorativeColorIgnored")] public bool DecorativeColorIgnored { get; set; }
        [JsonPropertyName("overlayReadsRevisionModel")] public bool OverlayReadsRevisionModel { get; set; }
    }

    public sealed class RevisionOverlayProbe
    {
        [JsonPropertyName("visibleShowMarkup")] public string VisibleShowMarkup { get; set; } = string.Empty;
        [JsonPropertyName("visibleFinal")] public string VisibleFinal { get; set; } = string.Empty;
        [JsonPropertyName("markerRendered")] public bool MarkerRendered { get; set; }
        [JsonPropertyName("markerType")] public string MarkerType { get; set; } = string.Empty;
        [JsonPropertyName("overlayZIndex")] public int OverlayZIndex { get; set; }
        [JsonPropertyName("mappedBlockId")] public string MappedBlockId { get; set; } = string.Empty;
        [JsonPropertyName("popoverType")] public string PopoverType { get; set; } = string.Empty;
        [JsonPropertyName("markerDifferScopes")] public string[] MarkerDifferScopes { get; set; } = [];
    }

    public sealed class RevisionBoundaryProbe
    {
        [JsonPropertyName("beforeRevisionId")] public string BeforeRevisionId { get; set; } = string.Empty;
        [JsonPropertyName("afterRevisionId")] public string AfterRevisionId { get; set; } = string.Empty;
        [JsonPropertyName("spaceRevisionId")] public string SpaceRevisionId { get; set; } = string.Empty;
        [JsonPropertyName("enterBlockId")] public string EnterBlockId { get; set; } = string.Empty;
        [JsonPropertyName("enterNotAtDocumentStart")] public bool EnterNotAtDocumentStart { get; set; }
        [JsonPropertyName("wordOrder")] public string WordOrder { get; set; } = string.Empty;
        [JsonPropertyName("normalRuns")] public string NormalRuns { get; set; } = string.Empty;
        [JsonPropertyName("revisionRunCount")] public int RevisionRunCount { get; set; }
    }

    public sealed class TrackChangesProbe
    {
        [JsonPropertyName("insertionRevisionId")] public string InsertionRevisionId { get; set; } = string.Empty;
        [JsonPropertyName("coalescedInsertion")] public bool CoalescedInsertion { get; set; }
        [JsonPropertyName("deletionRevisionId")] public string DeletionRevisionId { get; set; } = string.Empty;
        [JsonPropertyName("formatRevisionId")] public string FormatRevisionId { get; set; } = string.Empty;
        [JsonPropertyName("types")] public string[] Types { get; set; } = [];
        [JsonPropertyName("deletionTextStillVisible")] public bool DeletionTextStillVisible { get; set; }
        [JsonPropertyName("finalTextHidesDeletion")] public bool FinalTextHidesDeletion { get; set; }
        [JsonPropertyName("actualBold")] public bool ActualBold { get; set; }
        [JsonPropertyName("toolbarIgnoresFormatRevisionDecoration")] public bool ToolbarIgnoresFormatRevisionDecoration { get; set; }
    }

    public sealed class AcceptRejectProbe
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("insertionRunStillRevision")] public bool InsertionRunStillRevision { get; set; }
        [JsonPropertyName("deletionRejected")] public bool DeletionRejected { get; set; }
        [JsonPropertyName("formatAccepted")] public bool FormatAccepted { get; set; }
        [JsonPropertyName("caretBlock")] public string CaretBlock { get; set; } = string.Empty;
        [JsonPropertyName("caretOffset")] public int CaretOffset { get; set; }
        [JsonPropertyName("layoutOk")] public bool LayoutOk { get; set; }
        [JsonPropertyName("toolbarUnderline")] public bool ToolbarUnderline { get; set; }
    }
}
