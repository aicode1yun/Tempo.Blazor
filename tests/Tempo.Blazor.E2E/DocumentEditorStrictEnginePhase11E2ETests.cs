using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for anchored objects, wrapping, widget selection, and image preview transactions.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase11E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_AnchoredObjects_NormalizesFullObjectModelAndWrapExclusions()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<ObjectModelProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase11-object',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Anchor paragraph' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Decorative', Caption: 'Caption text', Layout: {
                            AnchorBlockId: 'p1',
                            AnchorOffset: 6,
                            MoveWithText: true,
                            FixedOnPage: false,
                            HorizontalPosition: { RelativeTo: 'Column', Align: 'Left', Offset: 8 },
                            VerticalPosition: { RelativeTo: 'Paragraph', Offset: 4 },
                            WrapMode: 'Square',
                            WrapMargin: 10,
                            AllowOverlap: false,
                            ZIndex: 7,
                            Width: 90,
                            Height: 50
                        } } }
                    ]
                });
                const image = model.body.blocks.find(block => block.id === 'img1');
                const normalized = engine.objects.normalizeImageObject(image, { anchorBlockId: 'p1' });
                const bodyFrame = { x: 20, y: 30, width: 240, height: 160 };
                const probes = ['Square', 'Tight', 'Through', 'TopBottom', 'BehindText', 'InFrontOfText'].map(mode => {
                    const layout = engine.objects.normalizeImageObject({
                        id: `img-${mode}`,
                        type: 'image',
                        content: { objectId: `obj-${mode}`, caption: 'Cap', layout: { ...image.content.layout, wrapMode: mode, width: 90, height: 50 } }
                    }, { anchorBlockId: 'p1' });
                    return engine.objects.createTextExclusion(layout, bodyFrame);
                });
                return {
                    objectId: normalized.objectId,
                    anchorBlockId: normalized.anchorBlockId,
                    anchorOffset: normalized.anchorOffset,
                    moveWithText: normalized.moveWithText,
                    fixedOnPage: normalized.fixedOnPage,
                    horizontalAlign: normalized.horizontalPosition.align,
                    verticalOffset: normalized.verticalPosition.offset,
                    wrapMode: normalized.wrapMode,
                    wrapMargin: normalized.wrapMargin,
                    allowOverlap: normalized.allowOverlap,
                    zIndex: normalized.zIndex,
                    squareKind: probes[0]?.kind || '',
                    tightKind: probes[1]?.kind || '',
                    throughKind: probes[2]?.kind || '',
                    topBottomKind: probes[3]?.kind || '',
                    behindCreatesExclusion: !!probes[4],
                    inFrontCreatesExclusion: !!probes[5],
                    inFrontHitPriority: engine.objects.hitTestLayerPriority('inFrontOfText', 'InFrontOfText'),
                    captionInFootprint: probes[0]?.captionIncluded === true
                };
            }
            """);

        result.ObjectId.Should().Be("obj1");
        result.AnchorBlockId.Should().Be("p1");
        result.AnchorOffset.Should().Be(6);
        result.MoveWithText.Should().BeTrue();
        result.FixedOnPage.Should().BeFalse();
        result.HorizontalAlign.Should().Be("Left");
        result.VerticalOffset.Should().Be(4);
        result.WrapMode.Should().Be("Square");
        result.WrapMargin.Should().Be(10);
        result.AllowOverlap.Should().BeFalse();
        result.ZIndex.Should().Be(7);
        result.SquareKind.Should().Be("rectangular");
        result.TightKind.Should().Be("contour");
        result.ThroughKind.Should().Be("editableContour");
        result.TopBottomKind.Should().Be("fullWidth");
        result.BehindCreatesExclusion.Should().BeFalse();
        result.InFrontCreatesExclusion.Should().BeFalse();
        result.InFrontHitPriority.Should().BeGreaterThan(20);
        result.CaptionInFootprint.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_AnchoredObjects_ComputesAvailableIntervalsAndHitTestsBesideImageAsText()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<IntervalsProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase11-intervals',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Before' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', Caption: 'Caption', Layout: {
                            AnchorBlockId: 'p1',
                            AnchorOffset: 3,
                            WrapMode: 'Square',
                            WrapMargin: 8,
                            Width: 95,
                            Height: 52,
                            HorizontalPosition: { Align: 'Left', Offset: 0 },
                            VerticalPosition: { Offset: 0 }
                        } } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'Text next to image should wrap beside the object and continue after it deterministically.' }] } }
                    ]
                });
                const layout = engine.textLayout.createParagraphLayoutEngine(null, {
                    page: { x: 0, y: 0, width: 320, height: 240 },
                    margins: { top: 16, right: 20, bottom: 16, left: 20 },
                    headerHeight: 0,
                    footerHeight: 0,
                    blockGap: 6,
                    minReadableWidth: 40
                }).layoutDocument(model);
                const p2 = layout.blocks.find(block => block.blockId === 'p2');
                const firstLine = p2.lines[0];
                const image = layout.objects.find(object => object.blockId === 'img1');
                const hit = engine.selection.pointerHitTest(model, layout, firstLine.availableIntervals[0].x + 4, firstLine.rect.y + 4);
                const imageHit = engine.selection.pointerHitTest(model, layout, image.rect.x + 4, image.rect.y + 4);
                const tooNarrow = engine.objects.getAvailableIntervals(
                    image.rect.y,
                    18,
                    layout.pages[0].bodyFrame,
                    [{ rect: { x: layout.pages[0].bodyFrame.x, y: image.rect.y, width: layout.pages[0].bodyFrame.width - 12, height: 30 }, kind: 'rectangular' }],
                    40
                );
                return {
                    pageCount: layout.pages.length,
                    objectCount: layout.objects.length,
                    exclusionCount: layout.pages[0].exclusions.length,
                    intervalCount: firstLine.availableIntervals.length,
                    intervalStartsAfterImage: firstLine.availableIntervals[0].x >= image.rect.x + image.rect.width + image.wrapMargin - 0.1,
                    lineNotInsideImage: firstLine.rect.x >= image.rect.x + image.rect.width + image.wrapMargin - 0.1,
                    hitType: hit.type,
                    hitBlockId: hit.position?.blockId || '',
                    imageHitType: imageHit.type,
                    tooNarrowMoved: tooNarrow.movedToY > image.rect.y
                };
            }
            """);

        result.PageCount.Should().Be(1);
        result.ObjectCount.Should().Be(1);
        result.ExclusionCount.Should().Be(1);
        result.IntervalCount.Should().BeGreaterThan(0);
        result.IntervalStartsAfterImage.Should().BeTrue();
        result.LineNotInsideImage.Should().BeTrue();
        result.HitType.Should().Be("text");
        result.HitBlockId.Should().Be("p2");
        result.ImageHitType.Should().Be("object");
        result.TooNarrowMoved.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_AnchoredObjects_PreviewDragResizeCommitAndRollbackAsSingleTransactions()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<PreviewProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase11-preview',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Anchor' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image', Layout: {
                            AnchorBlockId: 'p1',
                            AnchorOffset: 1,
                            WrapMode: 'Square',
                            WrapMargin: 6,
                            Width: 80,
                            Height: 40
                        } } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'Paragraph affected by wrapping around the preview image.' }] } }
                    ]
                });
                const controller = engine.objects.createImagePreviewController(model, {
                    page: { x: 0, y: 0, width: 320, height: 240 },
                    margins: { top: 16, right: 20, bottom: 16, left: 20 },
                    blockGap: 6
                });
                const dragStart = controller.startDrag('img1');
                const dragMove = controller.moveDrag({ dx: 24, dy: 12 });
                const dragRollback = controller.cancel();
                const afterRollback = engine.objects.normalizeImageObject(model.body.blocks.find(block => block.id === 'img1'));
                controller.startDrag('img1');
                controller.moveDrag({ dx: 20, dy: 10 });
                const dragCommit = controller.commit();
                controller.startResize('img1', { handle: 'se', lockAspectRatio: true });
                const resizeMove = controller.moveResize({ dx: 40, dy: 10 });
                const resizeCommit = controller.commit();
                const finalLayout = engine.objects.normalizeImageObject(model.body.blocks.find(block => block.id === 'img1'));
                return {
                    dragPreview: dragStart.preview === true && dragMove.preview === true,
                    dragRollbackOk: dragRollback.rolledBack === true,
                    rollbackOffsetX: afterRollback.horizontalPosition.offset,
                    dragCommitOps: dragCommit.operationCount,
                    dragSingleTransaction: dragCommit.singleTransaction === true,
                    resizePreview: resizeMove.preview === true,
                    resizeCommitOps: resizeCommit.operationCount,
                    resizeSingleTransaction: resizeCommit.singleTransaction === true,
                    finalOffsetX: finalLayout.horizontalPosition.offset,
                    finalWidth: finalLayout.width,
                    aspectLocked: Math.abs((finalLayout.width / finalLayout.height) - 2) < 0.05,
                    affectedParagraphs: resizeCommit.affectedParagraphIds
                };
            }
            """);

        result.DragPreview.Should().BeTrue();
        result.DragRollbackOk.Should().BeTrue();
        result.RollbackOffsetX.Should().Be(0);
        result.DragCommitOps.Should().Be(1);
        result.DragSingleTransaction.Should().BeTrue();
        result.ResizePreview.Should().BeTrue();
        result.ResizeCommitOps.Should().Be(1);
        result.ResizeSingleTransaction.Should().BeTrue();
        result.FinalOffsetX.Should().Be(20);
        result.FinalWidth.Should().BeGreaterThan(80);
        result.AspectLocked.Should().BeTrue();
        result.AffectedParagraphs.Should().Contain("p2");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_AnchoredObjects_RendersWidgetUiSelectionAndInspectorRules()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<WidgetProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase11-widget',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Text' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', Url: 'data:image/png;base64,abc', AltText: '', Caption: 'Caption', Layout: { AnchorBlockId: 'p1', WrapMode: 'Square', Width: 90, Height: 50 } } }
                    ]
                });
                const layout = engine.textLayout.createParagraphLayoutEngine(null, {
                    page: { x: 0, y: 0, width: 300, height: 220 },
                    margins: { top: 16, right: 20, bottom: 16, left: 20 },
                    blockGap: 6
                }).layoutDocument(model);
                const root = document.createElement('div');
                document.body.appendChild(root);
                engine.rendering.createAtomicRenderer().render(root, engine.rendering.createRenderSnapshot(model, layout, engine.selection.createSelectionSnapshot({
                    region: 'Body',
                    blockId: 'img1',
                    objectId: 'obj1',
                    isObjectSelection: true
                })));
                const widget = engine.objects.createEditorWidget(model.body.blocks.find(block => block.id === 'img1'));
                const figure = root.querySelector('[data-render-object-id="obj1"]');
                const selection = engine.selection.createSelectionSnapshot({ blockId: 'img1', objectId: 'obj1', isObjectSelection: true });
                const direct = widget.hitTest({ targetRole: 'widget' });
                const beside = widget.hitTest({ targetRole: 'text-interval' });
                const inspector = engine.objects.createImageInspectorState(model.body.blocks.find(block => block.id === 'img1'));
                const duplicateToolbarCount = root.querySelectorAll('[data-testid="document-wysiwyg-image-toolbar"], [data-testid="document-wysiwyg-layout-bubble"]').length;
                const resizeHandles = root.querySelectorAll('[data-testid^="document-wysiwyg-object-resize-handle-"]').length;
                const hasRotation = !!root.querySelector('[data-testid="document-wysiwyg-object-rotation-handle"]');
                const hasFakeSelection = !!root.querySelector('[data-testid="document-wysiwyg-object-selection-box"]');
                root.remove();
                return {
                    widgetKind: widget.kind,
                    adapter: widget.adapter,
                    isObjectSelection: selection.isObjectSelection === true,
                    directHitType: direct.type,
                    besideHitType: beside.type,
                    resizeHandles,
                    hasRotation,
                    hasFakeSelection,
                    duplicateToolbarCount,
                    hasSharedCommand: widget.commands.includes('UpdateImageLayout'),
                    showUrlField: inspector.showUrlField,
                    dataUriHidden: inspector.urlEditable === false,
                    captionValue: inspector.caption,
                    altWarning: inspector.warningBadges.includes('accessibility-warning'),
                    figureSelected: figure?.getAttribute('aria-selected') === 'true'
                };
            }
            """);

        result.WidgetKind.Should().Be("image");
        result.Adapter.Should().Be("EditorWidget");
        result.IsObjectSelection.Should().BeTrue();
        result.DirectHitType.Should().Be("object");
        result.BesideHitType.Should().Be("text");
        result.ResizeHandles.Should().Be(8);
        result.HasRotation.Should().BeTrue();
        result.HasFakeSelection.Should().BeTrue();
        result.DuplicateToolbarCount.Should().BeLessThanOrEqualTo(1);
        result.HasSharedCommand.Should().BeTrue();
        result.ShowUrlField.Should().BeFalse();
        result.DataUriHidden.Should().BeTrue();
        result.CaptionValue.Should().Be("Caption");
        result.AltWarning.Should().BeTrue();
        result.FigureSelected.Should().BeTrue();
    }

    public sealed class ObjectModelProbe
    {
        [JsonPropertyName("objectId")] public string ObjectId { get; set; } = string.Empty;
        [JsonPropertyName("anchorBlockId")] public string AnchorBlockId { get; set; } = string.Empty;
        [JsonPropertyName("anchorOffset")] public int AnchorOffset { get; set; }
        [JsonPropertyName("moveWithText")] public bool MoveWithText { get; set; }
        [JsonPropertyName("fixedOnPage")] public bool FixedOnPage { get; set; }
        [JsonPropertyName("horizontalAlign")] public string HorizontalAlign { get; set; } = string.Empty;
        [JsonPropertyName("verticalOffset")] public double VerticalOffset { get; set; }
        [JsonPropertyName("wrapMode")] public string WrapMode { get; set; } = string.Empty;
        [JsonPropertyName("wrapMargin")] public double WrapMargin { get; set; }
        [JsonPropertyName("allowOverlap")] public bool AllowOverlap { get; set; }
        [JsonPropertyName("zIndex")] public int ZIndex { get; set; }
        [JsonPropertyName("squareKind")] public string SquareKind { get; set; } = string.Empty;
        [JsonPropertyName("tightKind")] public string TightKind { get; set; } = string.Empty;
        [JsonPropertyName("throughKind")] public string ThroughKind { get; set; } = string.Empty;
        [JsonPropertyName("topBottomKind")] public string TopBottomKind { get; set; } = string.Empty;
        [JsonPropertyName("behindCreatesExclusion")] public bool BehindCreatesExclusion { get; set; }
        [JsonPropertyName("inFrontCreatesExclusion")] public bool InFrontCreatesExclusion { get; set; }
        [JsonPropertyName("inFrontHitPriority")] public int InFrontHitPriority { get; set; }
        [JsonPropertyName("captionInFootprint")] public bool CaptionInFootprint { get; set; }
    }

    public sealed class IntervalsProbe
    {
        [JsonPropertyName("pageCount")] public int PageCount { get; set; }
        [JsonPropertyName("objectCount")] public int ObjectCount { get; set; }
        [JsonPropertyName("exclusionCount")] public int ExclusionCount { get; set; }
        [JsonPropertyName("intervalCount")] public int IntervalCount { get; set; }
        [JsonPropertyName("intervalStartsAfterImage")] public bool IntervalStartsAfterImage { get; set; }
        [JsonPropertyName("lineNotInsideImage")] public bool LineNotInsideImage { get; set; }
        [JsonPropertyName("hitType")] public string HitType { get; set; } = string.Empty;
        [JsonPropertyName("hitBlockId")] public string HitBlockId { get; set; } = string.Empty;
        [JsonPropertyName("imageHitType")] public string ImageHitType { get; set; } = string.Empty;
        [JsonPropertyName("tooNarrowMoved")] public bool TooNarrowMoved { get; set; }
    }

    public sealed class PreviewProbe
    {
        [JsonPropertyName("dragPreview")] public bool DragPreview { get; set; }
        [JsonPropertyName("dragRollbackOk")] public bool DragRollbackOk { get; set; }
        [JsonPropertyName("rollbackOffsetX")] public double RollbackOffsetX { get; set; }
        [JsonPropertyName("dragCommitOps")] public int DragCommitOps { get; set; }
        [JsonPropertyName("dragSingleTransaction")] public bool DragSingleTransaction { get; set; }
        [JsonPropertyName("resizePreview")] public bool ResizePreview { get; set; }
        [JsonPropertyName("resizeCommitOps")] public int ResizeCommitOps { get; set; }
        [JsonPropertyName("resizeSingleTransaction")] public bool ResizeSingleTransaction { get; set; }
        [JsonPropertyName("finalOffsetX")] public double FinalOffsetX { get; set; }
        [JsonPropertyName("finalWidth")] public double FinalWidth { get; set; }
        [JsonPropertyName("aspectLocked")] public bool AspectLocked { get; set; }
        [JsonPropertyName("affectedParagraphs")] public string[] AffectedParagraphs { get; set; } = [];
    }

    public sealed class WidgetProbe
    {
        [JsonPropertyName("widgetKind")] public string WidgetKind { get; set; } = string.Empty;
        [JsonPropertyName("adapter")] public string Adapter { get; set; } = string.Empty;
        [JsonPropertyName("isObjectSelection")] public bool IsObjectSelection { get; set; }
        [JsonPropertyName("directHitType")] public string DirectHitType { get; set; } = string.Empty;
        [JsonPropertyName("besideHitType")] public string BesideHitType { get; set; } = string.Empty;
        [JsonPropertyName("resizeHandles")] public int ResizeHandles { get; set; }
        [JsonPropertyName("hasRotation")] public bool HasRotation { get; set; }
        [JsonPropertyName("hasFakeSelection")] public bool HasFakeSelection { get; set; }
        [JsonPropertyName("duplicateToolbarCount")] public int DuplicateToolbarCount { get; set; }
        [JsonPropertyName("hasSharedCommand")] public bool HasSharedCommand { get; set; }
        [JsonPropertyName("showUrlField")] public bool ShowUrlField { get; set; }
        [JsonPropertyName("dataUriHidden")] public bool DataUriHidden { get; set; }
        [JsonPropertyName("captionValue")] public string CaptionValue { get; set; } = string.Empty;
        [JsonPropertyName("altWarning")] public bool AltWarning { get; set; }
        [JsonPropertyName("figureSelected")] public bool FigureSelected { get; set; }
    }
}
