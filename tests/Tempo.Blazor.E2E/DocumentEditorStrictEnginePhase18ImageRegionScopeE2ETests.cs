using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict image-region tests for header, footer, and table-cell drawing behavior.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase18ImageRegionScopeE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_ImageRegions_InsertTypeLayoutHitTestAndDragRespectRegionScope()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<Phase18ImageRegionProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const hooks = engine.__testHooks;

                function createDrawingRun(id, objectId, anchorBlockId, region, scopeId, tableId, cellId) {
                    return {
                        $type: 'drawing',
                        Id: id,
                        ObjectId: objectId,
                        Kind: 0,
                        Source: 0,
                        Url: '/' + objectId + '.png',
                        AltText: objectId,
                        Layout: {
                            Kind: 1,
                            Anchor: {
                                BlockId: anchorBlockId,
                                Offset: 0,
                                InlineIndex: 0,
                                Region: region,
                                HeaderFooterId: scopeId || null,
                                TableId: tableId || null,
                                CellId: cellId || null,
                                MoveWithText: true,
                                FixedOnPage: false,
                                LockAnchor: false
                            },
                            Position: {
                                HorizontalRelativeTo: 2,
                                VerticalRelativeTo: 3,
                                HorizontalAlignment: 0,
                                VerticalAlignment: 1,
                                X: 0,
                                Y: 0
                            },
                            Wrap: {
                                Mode: 1,
                                DistanceLeft: 0,
                                DistanceRight: 8,
                                DistanceTop: 0,
                                DistanceBottom: 0
                            },
                            Transform: { Width: 120, Height: 36 },
                            Stacking: { ZIndex: 0, AllowOverlap: false }
                        }
                    };
                }

                function createDocument() {
                    return {
                        DocumentId: 'phase18-e2e-region',
                        Blocks: [
                            { Id: 'body-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'body-r', Text: 'Body paragraph keeps full width while scoped regions wrap locally.' }] } },
                            { Id: 'table-1', Type: 'Table', Content: { Style: { Width: 360 }, Rows: [
                                { Id: 'row-1', Cells: [
                                    { Id: 'cell-1', Width: 360, Blocks: [
                                        { Id: 'cell-p', Type: 'Paragraph', Content: { Inlines: [
                                            createDrawingRun('cell-run', 'cell-square', 'cell-p', 6, null, 'table-1', 'cell-1'),
                                            { Id: 'cell-text', Text: 'Cell text wraps locally beside its square image.' }
                                        ] } }
                                    ] }
                                ] }
                            ] } }
                        ],
                        HeadersFooters: [
                            { Id: 'header-primary', Region: 'Header', Type: 'Header', Scope: 'Primary', Blocks: [
                                { Id: 'header-p', Type: 'Paragraph', Content: { Inlines: [
                                    createDrawingRun('header-run', 'header-square', 'header-p', 1, 'header-primary'),
                                    { Id: 'header-text', Text: 'Header text wraps beside an image.' }
                                ] } }
                            ] },
                            { Id: 'footer-primary', Region: 'Footer', Type: 'Footer', Scope: 'Primary', Blocks: [
                                { Id: 'footer-p', Type: 'Paragraph', Content: { Inlines: [
                                    createDrawingRun('footer-run', 'footer-square', 'footer-p', 2, 'footer-primary'),
                                    { Id: 'footer-text', Text: 'Footer text wraps beside an image.' }
                                ] } }
                            ] }
                        ]
                    };
                }

                function text(block) {
                    return (block?.content?.runs || []).map(run => run.text || '').join('');
                }

                function insertText(model, target, value) {
                    const result = hooks.applyOperation(model, hooks.createOperation('InsertText', { target, text: value }, { source: 'phase18-e2e-typing' }));
                    if (!result || result.ok === false) throw new Error(JSON.stringify(result?.errors || result));
                    return result.nextSelection || {};
                }

                const model = hooks.importFromCSharpJson(createDocument());
                const headerInsert = hooks.applyOperation(model, hooks.createOperation('InsertImage', {
                    target: { blockId: 'header-p', offset: 6, region: 'Header', headerFooterId: 'header-primary' },
                    objectId: 'header-inline',
                    image: { Url: '/header-inline.png', AltText: 'Header inline', Layout: { Kind: 0, Wrap: { Mode: 0 }, Transform: { Width: 48, Height: 24 } } },
                    beforeSelection: { region: 'Header', headerFooterId: 'header-primary', blockId: 'header-p', offset: 6, isCollapsed: true }
                }, { source: 'phase18-e2e-header-insert' }));
                const footerInsert = hooks.applyOperation(model, hooks.createOperation('InsertImage', {
                    target: { blockId: 'footer-p', offset: 6, region: 'Footer', headerFooterId: 'footer-primary' },
                    objectId: 'footer-inline',
                    image: { Url: '/footer-inline.png', AltText: 'Footer inline', Layout: { Kind: 0, Wrap: { Mode: 0 }, Transform: { Width: 48, Height: 24 } } },
                    beforeSelection: { region: 'Footer', headerFooterId: 'footer-primary', blockId: 'footer-p', offset: 6, isCollapsed: true }
                }, { source: 'phase18-e2e-footer-insert' }));
                const cellInsert = hooks.applyOperation(model, hooks.createOperation('InsertImage', {
                    target: { blockId: 'cell-p', offset: 4, region: 'TableCell', tableId: 'table-1', cellId: 'cell-1' },
                    objectId: 'cell-inline',
                    image: { Url: '/cell-inline.png', AltText: 'Cell inline', Layout: { Kind: 0, Wrap: { Mode: 0 }, Transform: { Width: 48, Height: 24 } } },
                    beforeSelection: { region: 'TableCell', tableId: 'table-1', cellId: 'cell-1', activeTableId: 'table-1', activeTableCellId: 'cell-1', blockId: 'cell-p', offset: 4, isCollapsed: true }
                }, { source: 'phase18-e2e-cell-insert' }));

                insertText(model, { blockId: 'header-p', offset: 0, region: 'Header', headerFooterId: 'header-primary' }, 'H ');
                insertText(model, { blockId: 'footer-p', offset: 0, region: 'Footer', headerFooterId: 'footer-primary' }, 'F ');
                insertText(model, { blockId: 'cell-p', offset: 0, region: 'TableCell', tableId: 'table-1', cellId: 'cell-1' }, 'C ');

                const layout = hooks.createParagraphLayoutEngine(null, {
                    width: 640,
                    height: 900,
                    marginLeft: 40,
                    marginRight: 40,
                    marginTop: 40,
                    marginBottom: 40,
                    headerHeight: 80,
                    footerHeight: 80,
                    minReadableWidth: 32
                }).layoutDocument(model, {
                    width: 640,
                    height: 900,
                    marginLeft: 40,
                    marginRight: 40,
                    marginTop: 40,
                    marginBottom: 40,
                    headerHeight: 80,
                    footerHeight: 80,
                    minReadableWidth: 32
                });

                const body = layout.blocks.find(block => block.blockId === 'body-p');
                const headerRegion = layout.headerFooterRegions.find(region => region.region === 'Header' && region.headerFooterId === 'header-primary');
                const footerRegion = layout.headerFooterRegions.find(region => region.region === 'Footer' && region.headerFooterId === 'footer-primary');
                const table = layout.blocks.find(block => block.blockId === 'table-1');
                const cell = table?.cells?.find(item => item.cellId === 'cell-1');
                const cellParagraph = cell?.blockLayouts?.find(block => block.blockId === 'cell-p');
                const headerLine = headerRegion?.blocks?.find(block => block.blockId === 'header-p')?.lines?.[0];
                const cellLine = cellParagraph?.lines?.[0];
                const headerHit = headerLine
                    ? engine.selection.pointerHitTest(model, layout, headerLine.availableIntervals[0].x + 2, headerLine.rect.y + 2)
                    : null;
                const cellHit = cellLine
                    ? engine.selection.pointerHitTest(model, layout, cellLine.availableIntervals[0].x + 2, cellLine.rect.y + 2)
                    : null;
                const nearestCell = engine.objects.findNearestTextPositionForPoint({ model }, 24, 48, {
                    lineBoxes: [{ blockId: 'cell-p', pageIndex: 0, region: 'TableCell', tableId: 'table-1', cellId: 'cell-1', rect: { x: 20, y: 40, width: 220, height: 20 }, referenceRect: { x: 20, y: 40, width: 220, height: 20 }, start: 0, end: 10 }]
                });

                const dragDocument = {
                    DocumentId: 'phase18-e2e-drag',
                    Blocks: [{ Id: 'drag-body-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'drag-body-r', Text: 'Body target paragraph' }] } }],
                    HeadersFooters: [{ Id: 'drag-header', Region: 'Header', Type: 'Header', Scope: 'Primary', Blocks: [
                        { Id: 'drag-header-p', Type: 'Paragraph', Content: { Inlines: [
                            createDrawingRun('drag-header-run', 'header-drag', 'drag-header-p', 1, 'drag-header'),
                            { Id: 'drag-header-text', Text: 'Header source' }
                        ] } }
                    ] }]
                };
                const bodyLine = { blockId: 'drag-body-p', pageIndex: 0, region: 'Body', rect: { x: 20, y: 40, width: 220, height: 20 }, referenceRect: { x: 20, y: 40, width: 220, height: 20 }, start: 0, end: 21 };
                const rejected = engine.objects.createImageMoveTrackHarness({ document: dragDocument, objectId: 'header-drag', blockId: 'drag-header-p', lineBoxes: [bodyLine] });
                const beforeRejected = rejected.begin(0, 0).modelJson;
                rejected.move(40, 48);
                const rejectedState = rejected.up(40, 48);
                const allowed = engine.objects.createImageMoveTrackHarness({ document: dragDocument, objectId: 'header-drag', blockId: 'drag-header-p', lineBoxes: [bodyLine], allowCrossRegionDrop: true });
                allowed.begin(0, 0);
                allowed.move(40, 48);
                const allowedState = allowed.up(40, 48);

                return {
                    headerInsertRegion: hooks.findDrawingRunByObjectId(model, 'header-inline')?.object?.anchorRegion || '',
                    footerInsertRegion: hooks.findDrawingRunByObjectId(model, 'footer-inline')?.object?.anchorRegion || '',
                    cellInsertRegion: hooks.findDrawingRunByObjectId(model, 'cell-inline')?.object?.anchorRegion || '',
                    headerSelectionRegion: headerInsert?.nextSelection?.region || '',
                    footerSelectionRegion: footerInsert?.nextSelection?.region || '',
                    cellSelectionRegion: cellInsert?.nextSelection?.region || '',
                    headerText: text(model.indexes.blocks['header-p']),
                    footerText: text(model.indexes.blocks['footer-p']),
                    cellText: text(model.indexes.blocks['cell-p']),
                    bodyExclusionCount: layout.pages[0].exclusions.length,
                    headerExclusionCount: headerRegion?.exclusions?.length || 0,
                    footerExclusionCount: footerRegion?.exclusions?.length || 0,
                    tableExclusionCount: table?.exclusions?.length || 0,
                    cellExclusionCount: cell?.exclusions?.length || 0,
                    headerHitRegion: headerHit?.position?.region || headerHit?.region || '',
                    cellHitRegion: cellHit?.position?.region || cellHit?.region || '',
                    nearestCellRegion: nearestCell?.region || '',
                    nearestCellAnchorRegion: nearestCell?.anchorRegion || '',
                    rejectedType: rejectedState.commits?.[0]?.type || '',
                    rejectedReason: rejectedState.commits?.[0]?.reason || '',
                    rejectedRestored: rejectedState.modelJson === beforeRejected,
                    allowedType: allowedState.commits?.[0]?.type || '',
                    allowedRegion: allowedState.commits?.[0]?.operation?.newAnchor?.Region ?? allowedState.commits?.[0]?.layout?.Anchor?.Region ?? null
                };
            }
            """);

        result.HeaderInsertRegion.Should().Be("Header");
        result.FooterInsertRegion.Should().Be("Footer");
        result.CellInsertRegion.Should().Be("TableCell");
        result.HeaderSelectionRegion.Should().Be("Header");
        result.FooterSelectionRegion.Should().Be("Footer");
        result.CellSelectionRegion.Should().Be("TableCell");
        result.HeaderText.Should().StartWith("H Header");
        result.FooterText.Should().StartWith("F Footer");
        result.CellText.Should().StartWith("C Cell");
        result.BodyExclusionCount.Should().Be(0);
        result.HeaderExclusionCount.Should().BeGreaterThan(0);
        result.FooterExclusionCount.Should().BeGreaterThan(0);
        result.TableExclusionCount.Should().BeGreaterThan(0);
        result.CellExclusionCount.Should().BeGreaterThan(0);
        result.HeaderHitRegion.Should().Be("Header");
        result.CellHitRegion.Should().Be("TableCell");
        result.NearestCellRegion.Should().Be("TableCell");
        result.NearestCellAnchorRegion.Should().Be("TableCell");
        result.RejectedType.Should().Be("DropRejected");
        result.RejectedReason.Should().Be("cross-region-drop");
        result.RejectedRestored.Should().BeTrue();
        result.AllowedType.Should().Be("MoveDrawingObject");
        result.AllowedRegion.Should().Be(0);
    }

    public sealed class Phase18ImageRegionProbe
    {
        [JsonPropertyName("headerInsertRegion")] public string HeaderInsertRegion { get; set; } = string.Empty;
        [JsonPropertyName("footerInsertRegion")] public string FooterInsertRegion { get; set; } = string.Empty;
        [JsonPropertyName("cellInsertRegion")] public string CellInsertRegion { get; set; } = string.Empty;
        [JsonPropertyName("headerSelectionRegion")] public string HeaderSelectionRegion { get; set; } = string.Empty;
        [JsonPropertyName("footerSelectionRegion")] public string FooterSelectionRegion { get; set; } = string.Empty;
        [JsonPropertyName("cellSelectionRegion")] public string CellSelectionRegion { get; set; } = string.Empty;
        [JsonPropertyName("headerText")] public string HeaderText { get; set; } = string.Empty;
        [JsonPropertyName("footerText")] public string FooterText { get; set; } = string.Empty;
        [JsonPropertyName("cellText")] public string CellText { get; set; } = string.Empty;
        [JsonPropertyName("bodyExclusionCount")] public int BodyExclusionCount { get; set; }
        [JsonPropertyName("headerExclusionCount")] public int HeaderExclusionCount { get; set; }
        [JsonPropertyName("footerExclusionCount")] public int FooterExclusionCount { get; set; }
        [JsonPropertyName("tableExclusionCount")] public int TableExclusionCount { get; set; }
        [JsonPropertyName("cellExclusionCount")] public int CellExclusionCount { get; set; }
        [JsonPropertyName("headerHitRegion")] public string HeaderHitRegion { get; set; } = string.Empty;
        [JsonPropertyName("cellHitRegion")] public string CellHitRegion { get; set; } = string.Empty;
        [JsonPropertyName("nearestCellRegion")] public string NearestCellRegion { get; set; } = string.Empty;
        [JsonPropertyName("nearestCellAnchorRegion")] public string NearestCellAnchorRegion { get; set; } = string.Empty;
        [JsonPropertyName("rejectedType")] public string RejectedType { get; set; } = string.Empty;
        [JsonPropertyName("rejectedReason")] public string RejectedReason { get; set; } = string.Empty;
        [JsonPropertyName("rejectedRestored")] public bool RejectedRestored { get; set; }
        [JsonPropertyName("allowedType")] public string AllowedType { get; set; } = string.Empty;
        [JsonPropertyName("allowedRegion")] public int AllowedRegion { get; set; }
    }
}
