using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for the paragraph layout tree built on top of text measurement.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase6E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_ParagraphLayout_DefinesScopesAndInvalidationDebug()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<ScopeProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const layoutApi = engine.textLayout;
                const paragraph = layoutApi.createParagraphLayoutEngine();
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase6',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'First paragraph' }] } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'Second paragraph' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image', Layout: {} } }
                    ]
                });
                const insert = engine.operations.createOperation(engine.operations.types.InsertText, {
                    target: { blockId: 'p2', offset: 3 },
                    text: '!'
                }, { source: 'test' });
                const image = engine.operations.createOperation(engine.operations.types.UpdateImageLayout, {
                    target: { blockId: 'img1', offset: 0 },
                    layout: { wrap: { mode: 'square' } },
                    affectedParagraphIds: ['p1', 'p2']
                }, { source: 'test' });
                const revision = engine.operations.createOperation(engine.operations.types.AcceptRevision, {
                    revisionId: 'rev1'
                }, { source: 'test' });
                const insertScope = paragraph.inferLayoutScopeFromOperation(insert);
                const imageScope = paragraph.inferLayoutScopeFromOperation(image);
                const revisionScope = paragraph.inferLayoutScopeFromOperation(revision);
                const relayout = paragraph.layoutAfterOperation(model, insert, null, {
                    page: { x: 10, y: 20, width: 420, height: 560 },
                    selection: { blockId: 'p2', offset: 3, isCollapsed: true }
                });
                return {
                    insertScopeKind: String(insertScope.kind || ''),
                    insertScopeBlockId: String(insertScope.blockId || ''),
                    imageScopeKind: String(imageScope.kind || ''),
                    imageScopeIds: imageScope.affectedScopeIds || [],
                    revisionScopeKind: String(revisionScope.kind || ''),
                    relayoutOk: relayout.ok === true,
                    invalidatedScopes: relayout.debug?.invalidatedScopes || [],
                    minimalScopeKind: String(relayout.debug?.minimalScope?.kind || ''),
                    activeParagraphLayout: relayout.activeParagraphLayout === true
                };
            }
            """);

        result.InsertScopeKind.Should().Be("activeParagraph");
        result.InsertScopeBlockId.Should().Be("p2");
        result.ImageScopeKind.Should().Be("pageRegion");
        result.ImageScopeIds.Should().Contain(["img1", "p1", "p2"]);
        result.RevisionScopeKind.Should().Be("wholeDocument");
        result.RelayoutOk.Should().BeTrue();
        result.InvalidatedScopes.Should().Contain("p2");
        result.MinimalScopeKind.Should().Be("activeParagraph");
        result.ActiveParagraphLayout.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_ParagraphLayout_PlainParagraphReturnsGeometryBaselinesAndCaretStops()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<ParagraphGeometryProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const paragraph = engine.textLayout.createParagraphLayoutEngine();
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase6',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta gamma delta epsilon zeta eta theta iota kappa lambda.' }] } }]
                });
                const block = model.indexes.blocks.p1;
                const layout = paragraph.layoutParagraph(block, {
                    page: { x: 100, y: 120, width: 240, height: 600 },
                    minReadableWidth: 80
                });
                const lineRectsInsideParagraph = layout.lines.every(line =>
                    line.rect.x >= layout.rect.x - 0.1
                    && line.rect.x + line.rect.width <= layout.rect.x + layout.rect.width + 0.1
                    && line.rect.y >= layout.rect.y - 0.1
                    && line.rect.y + line.rect.height <= layout.rect.y + layout.rect.height + 0.1);
                const segmentRectsInsideLines = layout.segments.every(segment => {
                    const line = layout.lines.find(item => item.id === segment.lineId);
                    return !!line
                        && segment.rect.y >= line.rect.y - 0.1
                        && segment.rect.y + segment.rect.height <= line.rect.y + line.rect.height + 0.1;
                });
                return {
                    ok: layout.ok === true,
                    blockId: String(layout.blockId || ''),
                    type: String(layout.type || ''),
                    lineCount: layout.lines.length,
                    segmentCount: layout.segments.length,
                    caretStopCount: layout.caretStops.length,
                    baselineCount: layout.baselines.length,
                    rectWidth: Number(layout.rect?.width || 0),
                    rectHeight: Number(layout.rect?.height || 0),
                    lineRectsInsideParagraph,
                    segmentRectsInsideLines,
                    firstCaretBlock: String(layout.caretStops[0]?.blockId || ''),
                    lastCaretOffset: Number(layout.caretStops[layout.caretStops.length - 1]?.offset ?? -1)
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.BlockId.Should().Be("p1");
        result.Type.Should().Be("paragraph");
        result.LineCount.Should().BeGreaterThan(1);
        result.SegmentCount.Should().BeGreaterThan(3);
        result.CaretStopCount.Should().BeGreaterThan(20);
        result.BaselineCount.Should().Be(result.LineCount);
        result.RectWidth.Should().Be(240);
        result.RectHeight.Should().BeGreaterThan(20);
        result.LineRectsInsideParagraph.Should().BeTrue();
        result.SegmentRectsInsideLines.Should().BeTrue();
        result.FirstCaretBlock.Should().Be("p1");
        result.LastCaretOffset.Should().BeGreaterThan(50);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_ParagraphLayout_MarksDecorationsAndFieldRunsKeepStableMetrics()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<MarksProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const paragraph = engine.textLayout.createParagraphLayoutEngine();
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase6',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [
                        { Id: 'plain', Text: 'Plain ' },
                        { Id: 'bold', Text: 'bold text that wraps ', Marks: [{ Type: 'Bold' }] },
                        { Id: 'italic', Text: 'italic ', Marks: [{ Type: 'Italic' }] },
                        { Id: 'under', Text: 'underlined ', Marks: [{ Type: 'Underline' }] },
                        { Id: 'color', Text: 'colored ', Style: { Color: '#ff0000', BackgroundColor: '#ffff00' } },
                        { Id: 'field', Type: 'Field', FieldType: 'PageNumber', Text: '1' }
                    ] } }]
                });
                const block = model.indexes.blocks.p1;
                const layout = paragraph.layoutParagraph(block, {
                    page: { x: 0, y: 0, width: 150, height: 500 }
                });
                const boldSegment = layout.segments.find(segment => segment.runId === 'bold');
                const italicSegment = layout.segments.find(segment => segment.runId === 'italic');
                const underlineSegment = layout.segments.find(segment => segment.runId === 'under');
                const colorSegment = layout.segments.find(segment => segment.runId === 'color');
                const fieldSegment = layout.segments.find(segment => segment.runId === 'field');
                const coloredWidth = colorSegment?.rect?.width || 0;
                const unstyledColorWidth = engine.textLayout.createTextMeasurementService().measureText(colorSegment?.text || '', {
                    fontFamily: 'Arial',
                    fontSize: 16
                }).width;
                return {
                    ok: layout.ok === true,
                    splitBoldAcrossLines: layout.segments.filter(segment => segment.runId === 'bold').length > 1,
                    boldWeight: String(boldSegment?.style?.fontWeight || ''),
                    italicStyle: String(italicSegment?.style?.fontStyle || ''),
                    underlineDecoration: underlineSegment?.decorations?.includes('underline') === true,
                    colorValue: String(colorSegment?.style?.color || colorSegment?.style?.Color || ''),
                    backgroundValue: String(colorSegment?.style?.backgroundColor || colorSegment?.style?.BackgroundColor || ''),
                    fieldKind: String(fieldSegment?.kind || ''),
                    colorWidthDelta: Math.abs(coloredWidth - unstyledColorWidth),
                    lineCount: layout.lines.length
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.SplitBoldAcrossLines.Should().BeTrue();
        result.BoldWeight.Should().Be("700");
        result.ItalicStyle.Should().Be("italic");
        result.UnderlineDecoration.Should().BeTrue();
        result.ColorValue.Should().Be("#ff0000");
        result.BackgroundValue.Should().Be("#ffff00");
        result.FieldKind.Should().Be("field");
        result.ColorWidthDelta.Should().BeLessThan(0.1);
        result.LineCount.Should().BeGreaterThan(1);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_ParagraphLayout_RendersAbsoluteDomWithoutBrowserWrapping()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<AbsoluteRenderProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const paragraph = engine.textLayout.createParagraphLayoutEngine();
                const root = document.createElement('div');
                root.style.position = 'absolute';
                root.style.left = '30px';
                root.style.top = '40px';
                document.body.appendChild(root);
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase6',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Absolute layout should not depend on native browser wrapping.' }] } }]
                });
                const layout = paragraph.layoutParagraph(model.indexes.blocks.p1, {
                    page: { x: 0, y: 0, width: 180, height: 500 }
                });
                paragraph.renderParagraphLayout(root, layout);
                const container = root.querySelector('[data-layout-block-id="layout-p1"]');
                const segments = Array.from(root.querySelectorAll('[data-layout-segment-id]'));
                const rect = container.getBoundingClientRect();
                const firstSegmentRect = segments[0].getBoundingClientRect();
                const noWrappedSegment = segments.every(segment => {
                    const expectedHeight = Number(segment.getAttribute('data-layout-height') || 0);
                    const actual = segment.getBoundingClientRect();
                    return Math.abs(actual.height - expectedHeight) <= 1.5;
                });
                const absoluteSegments = segments.every(segment => getComputedStyle(segment).position === 'absolute');
                const containerPosition = String(container ? getComputedStyle(container).position : '');
                const firstExpected = layout.segments[0].rect;
                root.remove();
                return {
                    rendered: !!container,
                    segmentCount: segments.length,
                    containerPosition,
                    absoluteSegments,
                    noWrappedSegment,
                    rectWidthDelta: Math.abs(rect.width - layout.rect.width),
                    rectHeightDelta: Math.abs(rect.height - layout.rect.height),
                    firstSegmentXDelta: Math.abs(firstSegmentRect.left - 30 - firstExpected.x),
                    firstSegmentYDelta: Math.abs(firstSegmentRect.top - 40 - firstExpected.y)
                };
            }
            """);

        result.Rendered.Should().BeTrue();
        result.SegmentCount.Should().BeGreaterThan(3);
        result.ContainerPosition.Should().Be("absolute");
        result.AbsoluteSegments.Should().BeTrue();
        result.NoWrappedSegment.Should().BeTrue();
        result.RectWidthDelta.Should().BeLessThan(1.5);
        result.RectHeightDelta.Should().BeLessThan(1.5);
        result.FirstSegmentXDelta.Should().BeLessThan(1.5);
        result.FirstSegmentYDelta.Should().BeLessThan(1.5);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_ParagraphLayout_ImmediateRelayoutAndPaginationHandoffStayNonOverlapping()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<HandoffProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const paragraph = engine.textLayout.createParagraphLayoutEngine();
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase6',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Short intro' }] } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'Near page end paragraph.' }] } },
                        { Id: 'p3', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r3', Text: 'Following paragraph must be safely shifted.' }] } }
                    ]
                });
                const pageBox = { x: 0, y: 0, width: 180, height: 92 };
                const previous = paragraph.layoutDocument(model, { page: pageBox });
                const insert = engine.operations.createOperation(engine.operations.types.InsertText, {
                    target: { blockId: 'p2', offset: 24 },
                    text: ' This inserted text is intentionally long enough to make the active paragraph taller before idle pagination catches up.'
                }, { source: 'typing' });
                engine.operations.applyOperation(model, insert);
                const next = paragraph.layoutAfterOperation(model, insert, previous, {
                    page: pageBox,
                    selection: { blockId: 'p2', offset: 24, isCollapsed: true }
                });
                const p2 = next.blocks.find(block => block.blockId === 'p2');
                const p3 = next.blocks.find(block => block.blockId === 'p3');
                const previousP2 = previous.blocks.find(block => block.blockId === 'p2');
                return {
                    ok: next.ok === true,
                    activeParagraphLayout: next.activeParagraphLayout === true,
                    activeBlockId: String(next.activeBlockId || ''),
                    p2HeightIncreased: p2.rect.height > previousP2.rect.height,
                    p3Stale: p3.stale === true,
                    staleFollowingContainsP3: next.staleFollowingBlockIds.includes('p3'),
                    nonOverlapping: p3.rect.y >= p2.rect.y + p2.rect.height,
                    safeOffsetPositive: Number(p3.safeOffsetY || 0) > 0,
                    selectionBlockId: String(next.selection?.blockId || ''),
                    invalidatedIncludesP2: next.debug?.invalidatedScopes?.includes('p2') === true
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.ActiveParagraphLayout.Should().BeTrue();
        result.ActiveBlockId.Should().Be("p2");
        result.P2HeightIncreased.Should().BeTrue();
        result.P3Stale.Should().BeTrue();
        result.StaleFollowingContainsP3.Should().BeTrue();
        result.NonOverlapping.Should().BeTrue();
        result.SafeOffsetPositive.Should().BeTrue();
        result.SelectionBlockId.Should().Be("p2");
        result.InvalidatedIncludesP2.Should().BeTrue();
    }

    public sealed class ScopeProbe
    {
        [JsonPropertyName("insertScopeKind")] public string InsertScopeKind { get; set; } = string.Empty;
        [JsonPropertyName("insertScopeBlockId")] public string InsertScopeBlockId { get; set; } = string.Empty;
        [JsonPropertyName("imageScopeKind")] public string ImageScopeKind { get; set; } = string.Empty;
        [JsonPropertyName("imageScopeIds")] public string[] ImageScopeIds { get; set; } = [];
        [JsonPropertyName("revisionScopeKind")] public string RevisionScopeKind { get; set; } = string.Empty;
        [JsonPropertyName("relayoutOk")] public bool RelayoutOk { get; set; }
        [JsonPropertyName("invalidatedScopes")] public string[] InvalidatedScopes { get; set; } = [];
        [JsonPropertyName("minimalScopeKind")] public string MinimalScopeKind { get; set; } = string.Empty;
        [JsonPropertyName("activeParagraphLayout")] public bool ActiveParagraphLayout { get; set; }
    }

    public sealed class ParagraphGeometryProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("lineCount")] public int LineCount { get; set; }
        [JsonPropertyName("segmentCount")] public int SegmentCount { get; set; }
        [JsonPropertyName("caretStopCount")] public int CaretStopCount { get; set; }
        [JsonPropertyName("baselineCount")] public int BaselineCount { get; set; }
        [JsonPropertyName("rectWidth")] public double RectWidth { get; set; }
        [JsonPropertyName("rectHeight")] public double RectHeight { get; set; }
        [JsonPropertyName("lineRectsInsideParagraph")] public bool LineRectsInsideParagraph { get; set; }
        [JsonPropertyName("segmentRectsInsideLines")] public bool SegmentRectsInsideLines { get; set; }
        [JsonPropertyName("firstCaretBlock")] public string FirstCaretBlock { get; set; } = string.Empty;
        [JsonPropertyName("lastCaretOffset")] public int LastCaretOffset { get; set; }
    }

    public sealed class MarksProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("splitBoldAcrossLines")] public bool SplitBoldAcrossLines { get; set; }
        [JsonPropertyName("boldWeight")] public string BoldWeight { get; set; } = string.Empty;
        [JsonPropertyName("italicStyle")] public string ItalicStyle { get; set; } = string.Empty;
        [JsonPropertyName("underlineDecoration")] public bool UnderlineDecoration { get; set; }
        [JsonPropertyName("colorValue")] public string ColorValue { get; set; } = string.Empty;
        [JsonPropertyName("backgroundValue")] public string BackgroundValue { get; set; } = string.Empty;
        [JsonPropertyName("fieldKind")] public string FieldKind { get; set; } = string.Empty;
        [JsonPropertyName("colorWidthDelta")] public double ColorWidthDelta { get; set; }
        [JsonPropertyName("lineCount")] public int LineCount { get; set; }
    }

    public sealed class AbsoluteRenderProbe
    {
        [JsonPropertyName("rendered")] public bool Rendered { get; set; }
        [JsonPropertyName("segmentCount")] public int SegmentCount { get; set; }
        [JsonPropertyName("containerPosition")] public string ContainerPosition { get; set; } = string.Empty;
        [JsonPropertyName("absoluteSegments")] public bool AbsoluteSegments { get; set; }
        [JsonPropertyName("noWrappedSegment")] public bool NoWrappedSegment { get; set; }
        [JsonPropertyName("rectWidthDelta")] public double RectWidthDelta { get; set; }
        [JsonPropertyName("rectHeightDelta")] public double RectHeightDelta { get; set; }
        [JsonPropertyName("firstSegmentXDelta")] public double FirstSegmentXDelta { get; set; }
        [JsonPropertyName("firstSegmentYDelta")] public double FirstSegmentYDelta { get; set; }
    }

    public sealed class HandoffProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("activeParagraphLayout")] public bool ActiveParagraphLayout { get; set; }
        [JsonPropertyName("activeBlockId")] public string ActiveBlockId { get; set; } = string.Empty;
        [JsonPropertyName("p2HeightIncreased")] public bool P2HeightIncreased { get; set; }
        [JsonPropertyName("p3Stale")] public bool P3Stale { get; set; }
        [JsonPropertyName("staleFollowingContainsP3")] public bool StaleFollowingContainsP3 { get; set; }
        [JsonPropertyName("nonOverlapping")] public bool NonOverlapping { get; set; }
        [JsonPropertyName("safeOffsetPositive")] public bool SafeOffsetPositive { get; set; }
        [JsonPropertyName("selectionBlockId")] public string SelectionBlockId { get; set; } = string.Empty;
        [JsonPropertyName("invalidatedIncludesP2")] public bool InvalidatedIncludesP2 { get; set; }
    }
}
