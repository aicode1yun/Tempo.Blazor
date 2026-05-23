using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for the atomic render snapshot and scope renderer.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase7E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Rendering_CreatesVersionedRenderSnapshotWithFingerprint()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<RenderSnapshotProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const paragraph = engine.textLayout.createParagraphLayoutEngine();
                const rendering = engine.rendering;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase7',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Snapshot text' }] } }]
                });
                const layout = paragraph.layoutDocument(model, { page: { x: 0, y: 0, width: 260, height: 600 } });
                const selection = engine.selection.createSelectionSnapshot({ blockId: 'p1', offset: 4 });
                const snapshot = rendering.createRenderSnapshot(model, layout, selection, {
                    modelVersion: 11,
                    layoutVersion: 22,
                    selectionVersion: 33,
                    affectedScopes: ['p1']
                });
                return {
                    ok: snapshot.ok === true,
                    modelVersion: Number(snapshot.modelVersion || 0),
                    layoutVersion: Number(snapshot.layoutVersion || 0),
                    selectionVersion: Number(snapshot.selectionVersion || 0),
                    affectedScopes: snapshot.affectedScopes || [],
                    checksumLength: String(snapshot.checksum || '').length,
                    fingerprintLength: String(snapshot.fingerprint || '').length,
                    debugHasCounts: Number(snapshot.debug?.blockCount || 0) > 0 && Number(snapshot.debug?.segmentCount || 0) > 0
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.ModelVersion.Should().Be(11);
        result.LayoutVersion.Should().Be(22);
        result.SelectionVersion.Should().Be(33);
        result.AffectedScopes.Should().Contain("p1");
        result.ChecksumLength.Should().BeGreaterThan(8);
        result.FingerprintLength.Should().BeGreaterThan(8);
        result.DebugHasCounts.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Rendering_RendersParagraphPageObjectSelectionRevisionAndComments()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<ScopeRenderProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const paragraph = engine.textLayout.createParagraphLayoutEngine();
                const rendering = engine.rendering;
                const root = document.createElement('div');
                document.body.appendChild(root);
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase7',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [
                            { Id: 'r1', Text: 'Commented revision text', RevisionId: 'rev1', CommentIds: ['c1'] }
                        ] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image alt', Caption: 'Image caption', Layout: { Width: 120, Height: 80 } } }
                    ],
                    Revisions: [{ id: 'rev1', status: 'Pending' }],
                    Comments: [{ id: 'c1', text: 'Needs review' }]
                });
                const layout = paragraph.layoutDocument(model, { page: { x: 10, y: 20, width: 320, height: 600 } });
                const selection = engine.selection.createSelectionSnapshot({ blockId: 'p1', offset: 5 });
                const snapshot = rendering.createRenderSnapshot(model, layout, selection, { affectedScopes: ['p1', 'img1'] });
                const renderer = rendering.createAtomicRenderer();
                const render = renderer.render(root, snapshot, { scope: { kind: 'pageRegion', affectedScopeIds: ['p1', 'img1'] } });
                const paragraphNode = root.querySelector('[data-render-block-id="p1"]');
                const objectLayer = root.querySelector('[data-render-layer="object"]');
                const selectionOverlay = root.querySelector('[data-render-overlay="selection"]');
                const revisionOverlay = root.querySelector('[data-render-overlay="revision"]');
                const commentMarker = root.querySelector('[data-comment-id="c1"]');
                const segmentCount = root.querySelectorAll('[data-layout-segment-id]').length;
                root.remove();
                return {
                    ok: render.ok === true,
                    paragraphRendered: !!paragraphNode,
                    pageRendered: !!root.querySelector('[data-render-page-index="0"]'),
                    objectLayerRendered: !!objectLayer,
                    selectionOverlayRendered: !!selectionOverlay,
                    revisionOverlayRendered: !!revisionOverlay,
                    commentMarkerRendered: !!commentMarker,
                    segmentCount,
                    layerCount: root.querySelectorAll('[data-render-layer]').length
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.ParagraphRendered.Should().BeTrue();
        result.PageRendered.Should().BeTrue();
        result.ObjectLayerRendered.Should().BeTrue();
        result.SelectionOverlayRendered.Should().BeTrue();
        result.RevisionOverlayRendered.Should().BeTrue();
        result.CommentMarkerRendered.Should().BeTrue();
        result.SegmentCount.Should().BeGreaterThan(0);
        result.LayerCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Rendering_AtomicSwapRestoresSelectionAndRollsBackOnFailure()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<AtomicSwapProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const paragraph = engine.textLayout.createParagraphLayoutEngine();
                const rendering = engine.rendering;
                const root = document.createElement('div');
                document.body.appendChild(root);
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase7',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Before render' }] } }]
                });
                const renderer = rendering.createAtomicRenderer();
                let layout = paragraph.layoutDocument(model, { page: { x: 0, y: 0, width: 260, height: 400 } });
                let selection = engine.selection.createSelectionSnapshot({ blockId: 'p1', offset: 6 });
                let snapshot = rendering.createRenderSnapshot(model, layout, selection, { affectedScopes: ['p1'] });
                const first = renderer.render(root, snapshot, { scope: { kind: 'activeParagraph', blockId: 'p1', affectedScopeIds: ['p1'] } });
                const beforeHtml = root.innerHTML;
                const beforeEmptyFrameCount = renderer.debug().emptyFrameCount;
                const textBefore = root.textContent;
                const bad = renderer.render(root, snapshot, { failBeforeSwap: true, scope: { kind: 'activeParagraph', blockId: 'p1', affectedScopeIds: ['p1'] } });
                const afterFailureHtml = root.innerHTML;
                engine.operations.applyOperation(model, engine.operations.createOperation(engine.operations.types.InsertText, {
                    target: { blockId: 'p1', offset: 13 },
                    text: ' updated'
                }, { source: 'test' }));
                layout = paragraph.layoutDocument(model, { page: { x: 0, y: 0, width: 260, height: 400 } });
                selection = engine.selection.createSelectionSnapshot({ blockId: 'p1', offset: 13 });
                snapshot = rendering.createRenderSnapshot(model, layout, selection, { affectedScopes: ['p1'] });
                const second = renderer.render(root, snapshot, { scope: { kind: 'activeParagraph', blockId: 'p1', affectedScopeIds: ['p1'] } });
                const debug = renderer.debug();
                const logicalSelection = root.getAttribute('data-logical-selection');
                const textAfter = root.textContent;
                root.remove();
                return {
                    firstOk: first.ok === true,
                    failureOk: bad.ok === true,
                    failureRolledBack: bad.rolledBack === true,
                    htmlRestored: beforeHtml === afterFailureHtml,
                    secondOk: second.ok === true,
                    logicalSelectionContainsBlock: logicalSelection.includes('p1'),
                    watchdogFailures: debug.watchdogFailures,
                    emptyFrameCountStable: debug.emptyFrameCount === beforeEmptyFrameCount,
                    textBefore,
                    textAfter
                };
            }
            """);

        result.FirstOk.Should().BeTrue();
        result.FailureOk.Should().BeTrue();
        result.FailureRolledBack.Should().BeTrue();
        result.HtmlRestored.Should().BeTrue();
        result.SecondOk.Should().BeTrue();
        result.LogicalSelectionContainsBlock.Should().BeTrue();
        result.WatchdogFailures.Should().BeGreaterThan(0);
        result.EmptyFrameCountStable.Should().BeTrue();
        result.TextBefore.Should().Contain("Before render");
        result.TextAfter.Should().Contain("updated");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Rendering_ReconcilesDomAndReportsInvariants()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<ReconciliationProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const paragraph = engine.textLayout.createParagraphLayoutEngine();
                const rendering = engine.rendering;
                const root = document.createElement('div');
                document.body.appendChild(root);
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase7',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Stable text node' }] } }]
                });
                const renderer = rendering.createAtomicRenderer();
                let layout = paragraph.layoutDocument(model, { page: { x: 0, y: 0, width: 300, height: 400 } });
                let snapshot = rendering.createRenderSnapshot(model, layout, engine.selection.createSelectionSnapshot({ blockId: 'p1', offset: 0 }), { affectedScopes: ['p1'] });
                renderer.render(root, snapshot);
                const firstSegment = root.querySelector('[data-layout-segment-id]');
                const firstTextNode = firstSegment.firstChild;
                engine.operations.applyOperation(model, engine.operations.createOperation(engine.operations.types.InsertText, {
                    target: { blockId: 'p1', offset: 16 },
                    text: '!'
                }, { source: 'test' }));
                layout = paragraph.layoutDocument(model, { page: { x: 0, y: 0, width: 300, height: 400 } });
                snapshot = rendering.createRenderSnapshot(model, layout, engine.selection.createSelectionSnapshot({ blockId: 'p1', offset: 17 }), { affectedScopes: ['p1'] });
                renderer.render(root, snapshot);
                const secondSegment = root.querySelector('[data-layout-segment-id]');
                const secondTextNode = secondSegment.firstChild;
                const invariants = renderer.validateRenderInvariants(root, snapshot, {
                    forbiddenRects: [{ x: 500, y: 500, width: 10, height: 10 }]
                });
                const debug = renderer.debug();
                root.remove();
                return {
                    sameSegmentElement: firstSegment === secondSegment,
                    sameTextNode: firstTextNode === secondTextNode,
                    orphanCount: debug.orphanNodeCount,
                    duplicateToolbarCount: debug.duplicateToolbarCount,
                    invariantsOk: invariants.ok === true,
                    mappedTextNodes: invariants.mappedTextNodes,
                    layoutSegmentCount: invariants.layoutSegmentCount,
                    domSegmentCount: invariants.domSegmentCount,
                    wrappedSegments: invariants.wrappedSegments,
                    forbiddenOverlaps: invariants.forbiddenOverlaps
                };
            }
            """);

        result.SameSegmentElement.Should().BeTrue();
        result.SameTextNode.Should().BeTrue();
        result.OrphanCount.Should().Be(0);
        result.DuplicateToolbarCount.Should().Be(0);
        result.InvariantsOk.Should().BeTrue();
        result.MappedTextNodes.Should().BeGreaterThan(0);
        result.LayoutSegmentCount.Should().Be(result.DomSegmentCount);
        result.WrappedSegments.Should().Be(0);
        result.ForbiddenOverlaps.Should().Be(0);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Rendering_SeparatesEditingAndDataProjection()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<ProjectionProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const rendering = engine.rendering;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase7',
                    Blocks: [
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Image alt', Caption: 'Caption text', Url: 'https://example.test/image.png', Layout: { Width: 120, Height: 80 } } },
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Revision', RevisionId: 'rev1' }] } }
                    ],
                    Revisions: [{ id: 'rev1', status: 'Pending' }]
                });
                const editing = rendering.projectEditing(model);
                const data = rendering.projectData(model);
                const editingJson = JSON.stringify(editing);
                const dataJson = JSON.stringify(data);
                return {
                    editingHasHandles: editingJson.includes('resize-handle'),
                    editingHasDebugIds: editingJson.includes('data-debug-id'),
                    editingHasOverlay: editingJson.includes('revision-overlay'),
                    editingImageIsWidget: editing.blocks.some(block => block.kind === 'imageWidget' && block.mapping?.objectId === 'obj1'),
                    editingMappingIds: editing.blocks.every(block => !!block.mapping?.blockId),
                    dataHasEditingClass: dataJson.includes('resize-handle') || dataJson.includes('revision-overlay') || dataJson.includes('data-debug-id'),
                    dataImageCanonical: data.blocks.some(block => block.type === 'image' && block.objectId === 'obj1' && block.altText === 'Image alt'),
                    dataRevisionCount: data.revisions.length
                };
            }
            """);

        result.EditingHasHandles.Should().BeTrue();
        result.EditingHasDebugIds.Should().BeTrue();
        result.EditingHasOverlay.Should().BeTrue();
        result.EditingImageIsWidget.Should().BeTrue();
        result.EditingMappingIds.Should().BeTrue();
        result.DataHasEditingClass.Should().BeFalse();
        result.DataImageCanonical.Should().BeTrue();
        result.DataRevisionCount.Should().Be(1);
    }

    public sealed class RenderSnapshotProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("modelVersion")] public int ModelVersion { get; set; }
        [JsonPropertyName("layoutVersion")] public int LayoutVersion { get; set; }
        [JsonPropertyName("selectionVersion")] public int SelectionVersion { get; set; }
        [JsonPropertyName("affectedScopes")] public string[] AffectedScopes { get; set; } = [];
        [JsonPropertyName("checksumLength")] public int ChecksumLength { get; set; }
        [JsonPropertyName("fingerprintLength")] public int FingerprintLength { get; set; }
        [JsonPropertyName("debugHasCounts")] public bool DebugHasCounts { get; set; }
    }

    public sealed class ScopeRenderProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("paragraphRendered")] public bool ParagraphRendered { get; set; }
        [JsonPropertyName("pageRendered")] public bool PageRendered { get; set; }
        [JsonPropertyName("objectLayerRendered")] public bool ObjectLayerRendered { get; set; }
        [JsonPropertyName("selectionOverlayRendered")] public bool SelectionOverlayRendered { get; set; }
        [JsonPropertyName("revisionOverlayRendered")] public bool RevisionOverlayRendered { get; set; }
        [JsonPropertyName("commentMarkerRendered")] public bool CommentMarkerRendered { get; set; }
        [JsonPropertyName("segmentCount")] public int SegmentCount { get; set; }
        [JsonPropertyName("layerCount")] public int LayerCount { get; set; }
    }

    public sealed class AtomicSwapProbe
    {
        [JsonPropertyName("firstOk")] public bool FirstOk { get; set; }
        [JsonPropertyName("failureOk")] public bool FailureOk { get; set; }
        [JsonPropertyName("failureRolledBack")] public bool FailureRolledBack { get; set; }
        [JsonPropertyName("htmlRestored")] public bool HtmlRestored { get; set; }
        [JsonPropertyName("secondOk")] public bool SecondOk { get; set; }
        [JsonPropertyName("logicalSelectionContainsBlock")] public bool LogicalSelectionContainsBlock { get; set; }
        [JsonPropertyName("watchdogFailures")] public int WatchdogFailures { get; set; }
        [JsonPropertyName("emptyFrameCountStable")] public bool EmptyFrameCountStable { get; set; }
        [JsonPropertyName("textBefore")] public string TextBefore { get; set; } = string.Empty;
        [JsonPropertyName("textAfter")] public string TextAfter { get; set; } = string.Empty;
    }

    public sealed class ReconciliationProbe
    {
        [JsonPropertyName("sameSegmentElement")] public bool SameSegmentElement { get; set; }
        [JsonPropertyName("sameTextNode")] public bool SameTextNode { get; set; }
        [JsonPropertyName("orphanCount")] public int OrphanCount { get; set; }
        [JsonPropertyName("duplicateToolbarCount")] public int DuplicateToolbarCount { get; set; }
        [JsonPropertyName("invariantsOk")] public bool InvariantsOk { get; set; }
        [JsonPropertyName("mappedTextNodes")] public int MappedTextNodes { get; set; }
        [JsonPropertyName("layoutSegmentCount")] public int LayoutSegmentCount { get; set; }
        [JsonPropertyName("domSegmentCount")] public int DomSegmentCount { get; set; }
        [JsonPropertyName("wrappedSegments")] public int WrappedSegments { get; set; }
        [JsonPropertyName("forbiddenOverlaps")] public int ForbiddenOverlaps { get; set; }
    }

    public sealed class ProjectionProbe
    {
        [JsonPropertyName("editingHasHandles")] public bool EditingHasHandles { get; set; }
        [JsonPropertyName("editingHasDebugIds")] public bool EditingHasDebugIds { get; set; }
        [JsonPropertyName("editingHasOverlay")] public bool EditingHasOverlay { get; set; }
        [JsonPropertyName("editingImageIsWidget")] public bool EditingImageIsWidget { get; set; }
        [JsonPropertyName("editingMappingIds")] public bool EditingMappingIds { get; set; }
        [JsonPropertyName("dataHasEditingClass")] public bool DataHasEditingClass { get; set; }
        [JsonPropertyName("dataImageCanonical")] public bool DataImageCanonical { get; set; }
        [JsonPropertyName("dataRevisionCount")] public int DataRevisionCount { get; set; }
    }
}
