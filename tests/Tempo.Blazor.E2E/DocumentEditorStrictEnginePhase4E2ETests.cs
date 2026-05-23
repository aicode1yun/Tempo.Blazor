using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for the new logical selection engine.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase4E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Selection_NormalizesPositionsRangesAndLimitBoundaries()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<SelectionNormalizationProbe>(
            """
            () => {
                const modelApi = window.tmDocumentEditorEngine.model;
                const selection = window.tmDocumentEditorEngine.selection;
                const model = modelApi.importFromCSharpJson({
                    DocumentId: 'phase4',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello' }, { Id: 'r2', Text: ' world' }] } },
                        { Id: 'empty', Type: 'Paragraph', Content: { Inlines: [] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image', Layout: {} } },
                        { Id: 'tbl', Type: 'Table', Content: { Rows: [{ Id: 'row1', Cells: [
                            { Id: 'cell1', Blocks: [{ Id: 'cell-p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'cr1', Text: 'A' }] } }] },
                            { Id: 'cell2', Blocks: [{ Id: 'cell-p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'cr2', Text: 'B' }] } }] }
                        ] }] } }
                    ]
                });
                const pos = selection.normalizePosition(model, {
                    region: 'Body',
                    blockId: 'p1',
                    offset: 99,
                    affinity: 'before',
                    visualHintLineId: 'line-x'
                });
                const missing = selection.normalizePosition(model, { region: 'Body', blockId: 'missing', offset: 10 });
                const empty = selection.normalizePosition(model, { region: 'Body', blockId: 'empty', offset: 5 });
                const image = selection.normalizePosition(model, { region: 'Body', blockId: 'img1', offset: 99, affinity: 'after' });
                const crossCell = selection.normalizeRange(model, {
                    anchor: { region: 'Body', blockId: 'cell-p1', offset: 0 },
                    focus: { region: 'Body', blockId: 'cell-p2', offset: 1 },
                    direction: 'forward'
                });
                const snapshot = selection.createSelectionSnapshot({ position: pos });
                return {
                    positionRegion: pos.region,
                    positionBlock: pos.blockId,
                    positionInline: String(pos.inlineId || ''),
                    positionOffset: Number(pos.offset),
                    positionAffinity: pos.affinity,
                    positionVisualHint: String(pos.visualHintLineId || ''),
                    missingBlock: missing.blockId,
                    emptyOffset: Number(empty.offset),
                    imageOffset: Number(image.offset),
                    imageObjectId: String(image.objectId || ''),
                    crossCellCollapsed: crossCell.isCollapsed === true,
                    crossCellSameLimit: crossCell.anchor.limitId === crossCell.focus.limitId,
                    snapshotBlock: snapshot.blockId,
                    snapshotDirection: snapshot.direction
                };
            }
            """);

        result.PositionRegion.Should().Be("Body");
        result.PositionBlock.Should().Be("p1");
        result.PositionInline.Should().Be("r2");
        result.PositionOffset.Should().Be(11);
        result.PositionAffinity.Should().Be("before");
        result.PositionVisualHint.Should().Be("line-x");
        result.MissingBlock.Should().Be("p1");
        result.EmptyOffset.Should().Be(0);
        result.ImageOffset.Should().Be(1);
        result.ImageObjectId.Should().Be("obj1");
        result.CrossCellCollapsed.Should().BeTrue();
        result.CrossCellSameLimit.Should().BeTrue();
        result.SnapshotBlock.Should().Be("p1");
        result.SnapshotDirection.Should().Be("none");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Selection_DomAdapterRoundtripsAfterAtomicRerender()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<DomAdapterProbe>(
            """
            () => {
                const root = document.createElement('div');
                document.body.appendChild(root);
                const engine = window.tmDocumentEditorEngine;
                const selection = engine.selection;
                const id = engine.create(root, { instanceId: 'phase4-dom', useGoogleDocsEngine: true }, null);
                engine.loadDocument(id, { DocumentId: 'phase4', Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello' }] } }] });
                const before = JSON.stringify(engine.getDocumentSnapshot(id).document);
                let snapshot = engine.getDocumentSnapshot(id);
                let adapter = selection.createSelectionEngine(root, snapshot.document);
                const mapped = adapter.logicalToDomRange({ region: 'Body', blockId: 'p1', offset: 2 });
                const roundtrip = adapter.domRangeToLogical(mapped.range);
                engine.applyCommand(id, engine.operations.types.InsertText, {
                    target: { blockId: 'p1', offset: 5 },
                    text: '!',
                    transactionType: engine.operations.transactionTypes.Typing
                });
                snapshot = engine.getDocumentSnapshot(id);
                adapter = selection.createSelectionEngine(root, snapshot.document);
                const afterRenderMapped = adapter.logicalToDomRange({ region: 'Body', blockId: 'p1', offset: 6 });
                const afterRenderRoundtrip = adapter.domRangeToLogical(afterRenderMapped.range);
                const missing = adapter.logicalToDomRange({ region: 'Body', blockId: 'missing', offset: 0 });
                const after = JSON.stringify(snapshot.document);
                root.remove();
                return {
                    mappedOk: mapped.ok === true,
                    roundtripBlock: String(roundtrip.position?.blockId || ''),
                    roundtripOffset: Number(roundtrip.position?.offset ?? -1),
                    afterRenderOk: afterRenderMapped.ok === true,
                    afterRenderBlock: String(afterRenderRoundtrip.position?.blockId || ''),
                    afterRenderOffset: Number(afterRenderRoundtrip.position?.offset ?? -1),
                    missingCode: String(missing.error?.code || ''),
                    adapterMutatedModel: before === after
                };
            }
            """);

        result.MappedOk.Should().BeTrue();
        result.RoundtripBlock.Should().Be("p1");
        result.RoundtripOffset.Should().Be(2);
        result.AfterRenderOk.Should().BeTrue();
        result.AfterRenderBlock.Should().Be("p1");
        result.AfterRenderOffset.Should().Be(6);
        result.MissingCode.Should().Be("missing-dom-block");
        result.AdapterMutatedModel.Should().BeFalse("the command changes the model, but adapter reads must not be the mutating step");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Selection_CaretHitTestMapperAndKeyboardMovement()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<SelectionGeometryProbe>(
            """
            () => {
                const root = document.createElement('div');
                document.body.appendChild(root);
                const engine = window.tmDocumentEditorEngine;
                const selection = engine.selection;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase4',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta gamma' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image', Layout: {} } }
                    ]
                });
                const layout = {
                    blocks: [
                        { id: 'layout-p1', blockId: 'p1', type: 'paragraph', rect: { x: 100, y: 100, width: 210, height: 48 }, lines: [
                            { id: 'p1-line-1', blockId: 'p1', start: 0, end: 8, rect: { x: 100, y: 100, width: 112, height: 20 }, availableIntervals: [{ x: 100, width: 112, start: 0, end: 8 }] },
                            { id: 'p1-line-2', blockId: 'p1', start: 9, end: 16, rect: { x: 180, y: 124, width: 98, height: 20 }, availableIntervals: [{ x: 180, width: 98, start: 9, end: 16 }] }
                        ], segments: [{ id: 'seg-p1', blockId: 'p1', start: 0, end: 16, rect: { x: 100, y: 100, width: 210, height: 48 } }] },
                        { id: 'layout-img1', blockId: 'img1', type: 'image', objectId: 'obj1', rect: { x: 100, y: 124, width: 64, height: 64 }, lines: [], segments: [] }
                    ],
                    caretStops: [
                        { blockId: 'p1', inlineId: 'r1', offset: 0, rect: { x: 100, y: 100, width: 1, height: 20 }, lineId: 'p1-line-1' },
                        { blockId: 'p1', inlineId: 'r1', offset: 8, rect: { x: 212, y: 100, width: 1, height: 20 }, lineId: 'p1-line-1' },
                        { blockId: 'p1', inlineId: 'r1', offset: 12, rect: { x: 222, y: 124, width: 1, height: 20 }, lineId: 'p1-line-2' },
                        { blockId: 'img1', offset: 0, rect: { x: 100, y: 124, width: 1, height: 64 }, objectBoundary: true },
                        { blockId: 'img1', offset: 1, rect: { x: 164, y: 124, width: 1, height: 64 }, objectBoundary: true }
                    ]
                };
                const left = selection.pointerHitTest(model, layout, 80, 110);
                const right = selection.pointerHitTest(model, layout, 400, 110);
                const secondLine = selection.pointerHitTest(model, layout, 220, 132);
                const objectHit = selection.pointerHitTest(model, layout, 130, 150);
                const caret = selection.caretRectFromLayout(model, layout, { blockId: 'p1', offset: 12 });
                const mapper = selection.createModelLayoutDomMapper(root, model, layout);
                const moveRight = selection.moveSelection(model, layout, { position: { blockId: 'p1', offset: 0 } }, 'ArrowRight', {});
                const ctrlRight = selection.moveSelection(model, layout, { position: { blockId: 'p1', offset: 0 } }, 'ArrowRight', { ctrl: true });
                const shiftRight = selection.moveSelection(model, layout, { position: { blockId: 'p1', offset: 0 } }, 'ArrowRight', { shift: true });
                const home = selection.moveSelection(model, layout, { position: { blockId: 'p1', offset: 8 } }, 'Home', {});
                const end = selection.moveSelection(model, layout, { position: { blockId: 'p1', offset: 0 } }, 'End', {});
                const arrowDown = selection.moveSelection(model, layout, { position: { blockId: 'p1', offset: 4 } }, 'ArrowDown', {});
                const caption = mapper.captionPointToPosition('img1', 3);
                const widget = mapper.widgetHandleToObjectBoundary('img1', 'after');
                const debug = mapper.debugDump();
                root.remove();
                return {
                    leftOffset: Number(left.position?.offset ?? -1),
                    rightOffset: Number(right.position?.offset ?? -1),
                    secondLineType: String(secondLine.type || ''),
                    secondLineId: String(secondLine.lineId || ''),
                    secondLineOffset: Number(secondLine.position?.offset ?? -1),
                    objectType: String(objectHit.type || ''),
                    objectId: String(objectHit.position?.objectId || ''),
                    caretX: Number(caret?.x ?? -1),
                    mapperLayoutBlock: String(mapper.blockIdToLayoutBlockId('p1') || ''),
                    mapperCaretX: Number(mapper.inlineOffsetToCaretRect('r1', 12)?.x ?? -1),
                    moveRightOffset: Number(moveRight.offset),
                    ctrlRightOffset: Number(ctrlRight.offset),
                    shiftCollapsed: shiftRight.isCollapsed === true,
                    homeOffset: Number(home.offset),
                    endOffset: Number(end.offset),
                    arrowDownHint: String(arrowDown.visualHintLineId || ''),
                    captionRegion: String(caption.region || ''),
                    widgetObjectId: String(widget.objectId || ''),
                    debugCaretStops: Number(debug.caretStopCount || 0)
                };
            }
            """);

        result.LeftOffset.Should().Be(0);
        result.RightOffset.Should().Be(8);
        result.SecondLineType.Should().Be("text");
        result.SecondLineId.Should().Be("p1-line-2");
        result.SecondLineOffset.Should().BeGreaterThanOrEqualTo(9);
        result.ObjectType.Should().Be("object");
        result.ObjectId.Should().Be("obj1");
        result.CaretX.Should().Be(222);
        result.MapperLayoutBlock.Should().Be("layout-p1");
        result.MapperCaretX.Should().Be(222);
        result.MoveRightOffset.Should().Be(1);
        result.CtrlRightOffset.Should().Be(6);
        result.ShiftCollapsed.Should().BeFalse();
        result.HomeOffset.Should().Be(0);
        result.EndOffset.Should().Be(16);
        result.ArrowDownHint.Should().Be("next-line");
        result.CaptionRegion.Should().Be("Caption");
        result.WidgetObjectId.Should().Be("obj1");
        result.DebugCaretStops.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Selection_PostFixerKeepsValidObjectAndLimitSelections()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<PostFixerProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase4',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image', Layout: {} } },
                        { Id: 'tbl', Type: 'Table', Content: { Rows: [{ Id: 'row1', Cells: [
                            { Id: 'cell1', Blocks: [{ Id: 'cell-p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'cr1', Text: 'A' }] } }] },
                            { Id: 'cell2', Blocks: [{ Id: 'cell-p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'cr2', Text: 'B' }] } }] }
                        ] }] } }
                    ],
                    HeadersFooters: [{ Id: 'hf1', Region: 'Header', Blocks: [{ Id: 'header-p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'hr1', Text: 'Header' }] } }] }]
                });
                const fixer = engine.selection.createSelectionPostFixer(engine.model.createDefaultSchemaRegistry());
                const image = fixer.fix(model, { position: { region: 'Body', blockId: 'img1', offset: 99, affinity: 'after' } });
                const crossCell = fixer.fix(model, {
                    range: {
                        anchor: { region: 'Body', blockId: 'cell-p1', offset: 0 },
                        focus: { region: 'Body', blockId: 'cell-p2', offset: 1 },
                        direction: 'forward'
                    }
                });
                const header = fixer.fix(model, { position: { region: 'Header', blockId: 'header-p1', offset: 3 } });
                const revisionBoundary = fixer.fix(model, { position: { region: 'Body', blockId: 'p1', offset: 5, affinity: 'after' } });
                return {
                    imageBlock: String(image.blockId || ''),
                    imageObjectId: String(image.objectId || image.focus?.objectId || ''),
                    imageOffset: Number(image.offset),
                    crossCellCollapsed: crossCell.isCollapsed === true,
                    crossCellRejected: crossCell.rejectedCrossLimit === true || crossCell.anchor.limitId === crossCell.focus.limitId,
                    headerRegion: String(header.region || ''),
                    headerBlock: String(header.blockId || ''),
                    revisionAffinity: String(revisionBoundary.affinity || '')
                };
            }
            """);

        result.ImageBlock.Should().Be("img1");
        result.ImageObjectId.Should().Be("obj1");
        result.ImageOffset.Should().Be(1);
        result.CrossCellCollapsed.Should().BeTrue();
        result.CrossCellRejected.Should().BeTrue();
        result.HeaderRegion.Should().Be("Header");
        result.HeaderBlock.Should().Be("header-p1");
        result.RevisionAffinity.Should().Be("after");
    }

    private sealed class SelectionNormalizationProbe
    {
        [JsonPropertyName("positionRegion")] public string PositionRegion { get; set; } = string.Empty;
        [JsonPropertyName("positionBlock")] public string PositionBlock { get; set; } = string.Empty;
        [JsonPropertyName("positionInline")] public string PositionInline { get; set; } = string.Empty;
        [JsonPropertyName("positionOffset")] public int PositionOffset { get; set; }
        [JsonPropertyName("positionAffinity")] public string PositionAffinity { get; set; } = string.Empty;
        [JsonPropertyName("positionVisualHint")] public string PositionVisualHint { get; set; } = string.Empty;
        [JsonPropertyName("missingBlock")] public string MissingBlock { get; set; } = string.Empty;
        [JsonPropertyName("emptyOffset")] public int EmptyOffset { get; set; }
        [JsonPropertyName("imageOffset")] public int ImageOffset { get; set; }
        [JsonPropertyName("imageObjectId")] public string ImageObjectId { get; set; } = string.Empty;
        [JsonPropertyName("crossCellCollapsed")] public bool CrossCellCollapsed { get; set; }
        [JsonPropertyName("crossCellSameLimit")] public bool CrossCellSameLimit { get; set; }
        [JsonPropertyName("snapshotBlock")] public string SnapshotBlock { get; set; } = string.Empty;
        [JsonPropertyName("snapshotDirection")] public string SnapshotDirection { get; set; } = string.Empty;
    }

    private sealed class DomAdapterProbe
    {
        [JsonPropertyName("mappedOk")] public bool MappedOk { get; set; }
        [JsonPropertyName("roundtripBlock")] public string RoundtripBlock { get; set; } = string.Empty;
        [JsonPropertyName("roundtripOffset")] public int RoundtripOffset { get; set; }
        [JsonPropertyName("afterRenderOk")] public bool AfterRenderOk { get; set; }
        [JsonPropertyName("afterRenderBlock")] public string AfterRenderBlock { get; set; } = string.Empty;
        [JsonPropertyName("afterRenderOffset")] public int AfterRenderOffset { get; set; }
        [JsonPropertyName("missingCode")] public string MissingCode { get; set; } = string.Empty;
        [JsonPropertyName("adapterMutatedModel")] public bool AdapterMutatedModel { get; set; }
    }

    private sealed class SelectionGeometryProbe
    {
        [JsonPropertyName("leftOffset")] public int LeftOffset { get; set; }
        [JsonPropertyName("rightOffset")] public int RightOffset { get; set; }
        [JsonPropertyName("secondLineType")] public string SecondLineType { get; set; } = string.Empty;
        [JsonPropertyName("secondLineId")] public string SecondLineId { get; set; } = string.Empty;
        [JsonPropertyName("secondLineOffset")] public int SecondLineOffset { get; set; }
        [JsonPropertyName("objectType")] public string ObjectType { get; set; } = string.Empty;
        [JsonPropertyName("objectId")] public string ObjectId { get; set; } = string.Empty;
        [JsonPropertyName("caretX")] public double CaretX { get; set; }
        [JsonPropertyName("mapperLayoutBlock")] public string MapperLayoutBlock { get; set; } = string.Empty;
        [JsonPropertyName("mapperCaretX")] public double MapperCaretX { get; set; }
        [JsonPropertyName("moveRightOffset")] public int MoveRightOffset { get; set; }
        [JsonPropertyName("ctrlRightOffset")] public int CtrlRightOffset { get; set; }
        [JsonPropertyName("shiftCollapsed")] public bool ShiftCollapsed { get; set; }
        [JsonPropertyName("homeOffset")] public int HomeOffset { get; set; }
        [JsonPropertyName("endOffset")] public int EndOffset { get; set; }
        [JsonPropertyName("arrowDownHint")] public string ArrowDownHint { get; set; } = string.Empty;
        [JsonPropertyName("captionRegion")] public string CaptionRegion { get; set; } = string.Empty;
        [JsonPropertyName("widgetObjectId")] public string WidgetObjectId { get; set; } = string.Empty;
        [JsonPropertyName("debugCaretStops")] public int DebugCaretStops { get; set; }
    }

    private sealed class PostFixerProbe
    {
        [JsonPropertyName("imageBlock")] public string ImageBlock { get; set; } = string.Empty;
        [JsonPropertyName("imageObjectId")] public string ImageObjectId { get; set; } = string.Empty;
        [JsonPropertyName("imageOffset")] public int ImageOffset { get; set; }
        [JsonPropertyName("crossCellCollapsed")] public bool CrossCellCollapsed { get; set; }
        [JsonPropertyName("crossCellRejected")] public bool CrossCellRejected { get; set; }
        [JsonPropertyName("headerRegion")] public string HeaderRegion { get; set; } = string.Empty;
        [JsonPropertyName("headerBlock")] public string HeaderBlock { get; set; } = string.Empty;
        [JsonPropertyName("revisionAffinity")] public string RevisionAffinity { get; set; } = string.Empty;
    }
}
