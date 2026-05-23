using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for active layout scheduling and invalid-frame gates.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase9E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Scheduling_SeparatesImmediateAndIdleWithDebugTimeline()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<SchedulerRulesProbe>(
            """
            async () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase9',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha' }] } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'Beta' }] } }
                    ]
                });
                const root = document.createElement('div');
                document.body.appendChild(root);
                const scheduler = engine.scheduling.createActiveLayoutScheduler({
                    page: { x: 0, y: 0, width: 260, height: 400 },
                    idleDebounceMs: 250,
                    testMode: true
                });
                const op = engine.operations.createOperation(engine.operations.types.InsertText, {
                    target: { blockId: 'p1', offset: 5 },
                    text: '!'
                }, { source: 'typing' });
                const immediate = scheduler.runImmediate({
                    model,
                    operation: op,
                    root,
                    selection: { blockId: 'p1', offset: 5, isCollapsed: true }
                });
                const idleScheduled = scheduler.scheduleIdleReconciliation({
                    model,
                    root,
                    selection: immediate.selection,
                    reason: 'typing'
                });
                const beforeIdle = scheduler.debug();
                const idle = await scheduler.flushIdle();
                const afterIdle = scheduler.debug();
                root.remove();
                return {
                    immediateOk: immediate.ok === true,
                    immediateKind: String(immediate.kind || ''),
                    immediateScope: String(immediate.layout?.debug?.minimalScope?.kind || ''),
                    idleScheduled: idleScheduled.scheduled === true,
                    idleDebounced: idleScheduled.debounceMs === 250,
                    idleKind: String(idle.kind || ''),
                    timelineKinds: afterIdle.timeline.map(item => item.kind),
                    hasCompositionAwareness: afterIdle.compositionAware === true,
                    immediateBeforeIdleCount: beforeIdle.idleRunCount === 0,
                    idleAfterFlushCount: afterIdle.idleRunCount,
                    activeBlockId: String(immediate.layout?.activeBlockId || '')
                };
            }
            """);

        result.ImmediateOk.Should().BeTrue();
        result.ImmediateKind.Should().Be("immediate");
        result.ImmediateScope.Should().Be("activeParagraph");
        result.IdleScheduled.Should().BeTrue();
        result.IdleDebounced.Should().BeTrue();
        result.IdleKind.Should().Be("idle");
        result.TimelineKinds.Should().Contain(["immediate", "idle-scheduled", "idle"]);
        result.HasCompositionAwareness.Should().BeTrue();
        result.ImmediateBeforeIdleCount.Should().BeTrue();
        result.IdleAfterFlushCount.Should().Be(1);
        result.ActiveBlockId.Should().Be("p1");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Scheduling_MeasuresFrameBudgetAndEntersSafeDegradedMode()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<FrameBudgetProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase9',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Budget' }] } }]
                });
                const root = document.createElement('div');
                document.body.appendChild(root);
                const scheduler = engine.scheduling.createActiveLayoutScheduler({
                    page: { x: 0, y: 0, width: 260, height: 400 },
                    frameBudgetMs: 1,
                    repeatedBudgetWarningThreshold: 2,
                    testMode: true
                });
                const first = scheduler.runImmediate({
                    model,
                    operation: engine.operations.createOperation(engine.operations.types.InsertText, { target: { blockId: 'p1', offset: 6 }, text: '1' }, { source: 'typing' }),
                    root,
                    selection: { blockId: 'p1', offset: 6, isCollapsed: true },
                    simulatedDurations: { operationApply: 2, layout: 2, render: 2, selectionRestore: 2 }
                });
                const second = scheduler.runImmediate({
                    model,
                    operation: engine.operations.createOperation(engine.operations.types.InsertText, { target: { blockId: 'p1', offset: 7 }, text: '2' }, { source: 'typing' }),
                    root,
                    selection: first.selection,
                    simulatedDurations: { operationApply: 2, layout: 2, render: 2, selectionRestore: 2 }
                });
                const stats = scheduler.debug().stats;
                root.remove();
                return {
                    firstWarning: first.budgetWarning === true,
                    secondWarning: second.budgetWarning === true,
                    operationApplyMs: stats.lastOperationApplyMs,
                    layoutMs: stats.lastLayoutMs,
                    renderMs: stats.lastRenderMs,
                    selectionRestoreMs: stats.lastSelectionRestoreMs,
                    warningCount: stats.budgetWarningCount,
                    safeDegradedMode: stats.safeDegradedMode === true,
                    statsAvailable: typeof stats.lastTotalMs === 'number' && stats.lastTotalMs > 0
                };
            }
            """);

        result.FirstWarning.Should().BeTrue();
        result.SecondWarning.Should().BeTrue();
        result.OperationApplyMs.Should().BeGreaterThanOrEqualTo(2);
        result.LayoutMs.Should().BeGreaterThanOrEqualTo(2);
        result.RenderMs.Should().BeGreaterThanOrEqualTo(2);
        result.SelectionRestoreMs.Should().BeGreaterThanOrEqualTo(2);
        result.WarningCount.Should().BeGreaterThanOrEqualTo(2);
        result.SafeDegradedMode.Should().BeTrue();
        result.StatsAvailable.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Scheduling_NoInvalidFrameGateDetectsReadableFailures()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<InvalidFrameProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const scheduler = engine.scheduling.createActiveLayoutScheduler({ testMode: true });
                const root = document.createElement('div');
                root.style.position = 'absolute';
                root.style.left = '0px';
                root.style.top = '0px';
                document.body.appendChild(root);
                root.innerHTML = `
                    <div data-render-layer="text">
                        <span data-layout-segment-id="s1" data-model-block-id="p1" data-layout-height="20" style="position:absolute;left:0px;top:0px;width:100px;height:20px;line-height:20px;display:block;overflow:hidden">A</span>
                        <span data-layout-segment-id="s2" data-model-block-id="p1" data-layout-height="20" style="position:absolute;left:50px;top:0px;width:100px;height:20px;line-height:20px;display:block;overflow:hidden">B</span>
                    </div>
                    <figure data-render-object-id="obj1" style="position:absolute;left:40px;top:0px;width:40px;height:20px"></figure>
                `;
                const snapshot = {
                    layout: {
                        caretStops: [],
                        blocks: [{ blockId: 'p1', segments: [
                            { id: 's1', rect: { x: 0, y: 0, width: 100, height: 20 } },
                            { id: 's2', rect: { x: 50, y: 0, width: 100, height: 20 } }
                        ] }]
                    },
                    selection: { blockId: 'p1', offset: 1 }
                };
                const probe = scheduler.probeNoInvalidFrame(root, snapshot, { throwOnFailure: false });
                let thrown = '';
                try {
                    scheduler.probeNoInvalidFrame(root, snapshot, { throwOnFailure: true });
                } catch (error) {
                    thrown = String(error.message || error);
                }
                root.remove();
                return {
                    ok: probe.ok === true,
                    textTextOverlaps: probe.textTextOverlaps,
                    textImageOverlaps: probe.textImageOverlaps,
                    segmentOverflows: probe.segmentOverflows,
                    missingCaret: probe.missingCaret === true,
                    thrown
                };
            }
            """);

        result.Ok.Should().BeFalse();
        result.TextTextOverlaps.Should().BeGreaterThan(0);
        result.TextImageOverlaps.Should().BeGreaterThan(0);
        result.SegmentOverflows.Should().Be(0);
        result.MissingCaret.Should().BeTrue();
        result.Thrown.Should().Contain("invalid-frame");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Scheduling_IdleReconciliationKeepsSelectionWordsAndActiveParagraphStable()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<IdleReconciliationProbe>(
            """
            async () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase9',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta' }] } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'Gamma delta' }] } }
                    ]
                });
                const root = document.createElement('div');
                document.body.appendChild(root);
                const scheduler = engine.scheduling.createActiveLayoutScheduler({
                    page: { x: 0, y: 0, width: 260, height: 400 },
                    idleDebounceMs: 50,
                    testMode: true
                });
                const op = engine.operations.createOperation(engine.operations.types.InsertText, {
                    target: { blockId: 'p1', offset: 10 },
                    text: '!'
                }, { source: 'typing' });
                const immediate = scheduler.runImmediate({
                    model,
                    operation: op,
                    root,
                    selection: { blockId: 'p1', offset: 10, isCollapsed: true }
                });
                const beforeIdle = scheduler.snapshotForIdle(model, immediate.layout, immediate.selection);
                scheduler.scheduleIdleReconciliation({ model, root, selection: immediate.selection, reason: 'typing' });
                const idle = await scheduler.flushIdle();
                const afterIdle = scheduler.snapshotForIdle(model, idle.layout, idle.selection);
                const beforeActive = beforeIdle.blocks.find(block => block.blockId === 'p1');
                const afterActive = afterIdle.blocks.find(block => block.blockId === 'p1');
                root.remove();
                return {
                    idleOk: idle.ok === true,
                    selectionStable: JSON.stringify(beforeIdle.selection) === JSON.stringify(afterIdle.selection),
                    wordOrderStable: beforeIdle.text === afterIdle.text,
                    activeParagraphJump: Math.abs((beforeActive?.rect?.y || 0) - (afterActive?.rect?.y || 0)),
                    beforeIdleText: beforeIdle.text,
                    afterIdleText: afterIdle.text,
                    timelineKinds: scheduler.debug().timeline.map(item => item.kind)
                };
            }
            """);

        result.IdleOk.Should().BeTrue();
        result.SelectionStable.Should().BeTrue();
        result.WordOrderStable.Should().BeTrue();
        result.ActiveParagraphJump.Should().BeLessThan(0.1);
        result.BeforeIdleText.Should().Be("Alpha beta!|Gamma delta");
        result.AfterIdleText.Should().Be("Alpha beta!|Gamma delta");
        result.TimelineKinds.Should().Contain("idle");
    }

    public sealed class SchedulerRulesProbe
    {
        [JsonPropertyName("immediateOk")] public bool ImmediateOk { get; set; }
        [JsonPropertyName("immediateKind")] public string ImmediateKind { get; set; } = string.Empty;
        [JsonPropertyName("immediateScope")] public string ImmediateScope { get; set; } = string.Empty;
        [JsonPropertyName("idleScheduled")] public bool IdleScheduled { get; set; }
        [JsonPropertyName("idleDebounced")] public bool IdleDebounced { get; set; }
        [JsonPropertyName("idleKind")] public string IdleKind { get; set; } = string.Empty;
        [JsonPropertyName("timelineKinds")] public string[] TimelineKinds { get; set; } = [];
        [JsonPropertyName("hasCompositionAwareness")] public bool HasCompositionAwareness { get; set; }
        [JsonPropertyName("immediateBeforeIdleCount")] public bool ImmediateBeforeIdleCount { get; set; }
        [JsonPropertyName("idleAfterFlushCount")] public int IdleAfterFlushCount { get; set; }
        [JsonPropertyName("activeBlockId")] public string ActiveBlockId { get; set; } = string.Empty;
    }

    public sealed class FrameBudgetProbe
    {
        [JsonPropertyName("firstWarning")] public bool FirstWarning { get; set; }
        [JsonPropertyName("secondWarning")] public bool SecondWarning { get; set; }
        [JsonPropertyName("operationApplyMs")] public double OperationApplyMs { get; set; }
        [JsonPropertyName("layoutMs")] public double LayoutMs { get; set; }
        [JsonPropertyName("renderMs")] public double RenderMs { get; set; }
        [JsonPropertyName("selectionRestoreMs")] public double SelectionRestoreMs { get; set; }
        [JsonPropertyName("warningCount")] public int WarningCount { get; set; }
        [JsonPropertyName("safeDegradedMode")] public bool SafeDegradedMode { get; set; }
        [JsonPropertyName("statsAvailable")] public bool StatsAvailable { get; set; }
    }

    public sealed class InvalidFrameProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("textTextOverlaps")] public int TextTextOverlaps { get; set; }
        [JsonPropertyName("textImageOverlaps")] public int TextImageOverlaps { get; set; }
        [JsonPropertyName("segmentOverflows")] public int SegmentOverflows { get; set; }
        [JsonPropertyName("missingCaret")] public bool MissingCaret { get; set; }
        [JsonPropertyName("thrown")] public string Thrown { get; set; } = string.Empty;
    }

    public sealed class IdleReconciliationProbe
    {
        [JsonPropertyName("idleOk")] public bool IdleOk { get; set; }
        [JsonPropertyName("selectionStable")] public bool SelectionStable { get; set; }
        [JsonPropertyName("wordOrderStable")] public bool WordOrderStable { get; set; }
        [JsonPropertyName("activeParagraphJump")] public double ActiveParagraphJump { get; set; }
        [JsonPropertyName("beforeIdleText")] public string BeforeIdleText { get; set; } = string.Empty;
        [JsonPropertyName("afterIdleText")] public string AfterIdleText { get; set; } = string.Empty;
        [JsonPropertyName("timelineKinds")] public string[] TimelineKinds { get; set; } = [];
    }
}
