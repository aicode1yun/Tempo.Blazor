using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for diagnostics, layout probes, event timeline, and watchdog recovery.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase17E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Diagnostics_DebugSnapshotContainsVersionsStateStatsAndTimeline()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<DebugSnapshotProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const host = document.createElement('div');
                document.body.appendChild(host);
                const calls = [];
                const dotNet = { invokeMethodAsync(method, payload) { calls.push({ method, payload }); return Promise.resolve(true); } };
                const id = engine.create(host, { instanceId: 'phase17-debug' }, dotNet);
                engine.loadDocument(id, {
                    DocumentId: 'phase17-debug-doc',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha' }] } }]
                });
                engine.applyCommand(id, 'InsertText', {
                    transactionType: 'typing',
                    target: { blockId: 'p1', offset: 5 },
                    text: ' beta'
                });
                const debug = engine.getDebugSnapshot(id);
                const kinds = (debug.timeline || []).map(item => item.kind);
                const text = engine.getDocumentSnapshot(id).document.body.blocks[0].content.runs.map(run => run.text).join('');
                engine.dispose(id);
                host.remove();
                return {
                    text,
                    modelVersion: debug.modelVersion,
                    layoutVersion: debug.layoutVersion,
                    renderVersion: debug.renderVersion,
                    selectionVersion: debug.selectionVersion,
                    hasSelection: !!debug.selection?.blockId,
                    activeTransactionIsNull: debug.activeTransaction === null,
                    undoDepth: debug.undoDepth,
                    redoDepth: debug.redoDepth,
                    invalidatedScopes: debug.invalidatedScopes || [],
                    layoutPassCount: Number(debug.performanceStats?.layoutPassCount || 0),
                    renderPassCount: Number(debug.performanceStats?.renderPassCount || 0),
                    lastErrorsIsArray: Array.isArray(debug.lastErrors),
                    hasInputEvent: kinds.includes('input-event'),
                    hasNormalizedOperation: kinds.includes('normalized-operation'),
                    hasTransactionCommit: kinds.includes('transaction-commit'),
                    hasLayoutPass: kinds.includes('layout-pass'),
                    hasRenderPass: kinds.includes('render-pass'),
                    hasSelectionRestore: kinds.includes('selection-restore'),
                    hasBlazorPatchEmit: kinds.includes('blazor-patch-emit')
                };
            }
            """);

        result.Text.Should().Be("Alpha beta");
        result.ModelVersion.Should().BeGreaterThan(0);
        result.LayoutVersion.Should().BeGreaterThan(0);
        result.RenderVersion.Should().BeGreaterThan(0);
        result.SelectionVersion.Should().BeGreaterThan(0);
        result.HasSelection.Should().BeTrue();
        result.ActiveTransactionIsNull.Should().BeTrue();
        result.UndoDepth.Should().BeGreaterThan(0);
        result.RedoDepth.Should().Be(0);
        result.InvalidatedScopes.Should().Contain("p1");
        result.LayoutPassCount.Should().BeGreaterThan(0);
        result.RenderPassCount.Should().BeGreaterThan(0);
        result.LastErrorsIsArray.Should().BeTrue();
        result.HasInputEvent.Should().BeTrue();
        result.HasNormalizedOperation.Should().BeTrue();
        result.HasTransactionCommit.Should().BeTrue();
        result.HasLayoutPass.Should().BeTrue();
        result.HasRenderPass.Should().BeTrue();
        result.HasSelectionRestore.Should().BeTrue();
        result.HasBlazorPatchEmit.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Diagnostics_LayoutProbeReportsRectsExclusionsCollisionsAndAnimationFrames()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<LayoutProbeResult>(
            """
            async () => {
                const engine = window.tmDocumentEditorEngine;
                const host = document.createElement('div');
                document.body.appendChild(host);
                const id = engine.create(host, { instanceId: 'phase17-probe' });
                engine.loadDocument(id, {
                    DocumentId: 'phase17-probe-doc',
                    Blocks: [
                        {
                            Id: 'img-front',
                            Type: 'Image',
                            Content: {
                                Id: 'obj-front',
                                AltText: 'Layered image',
                                Caption: 'Layered caption',
                                Layout: {
                                    Width: 160,
                                    Height: 90,
                                    WrapMode: 'InFrontOfText',
                                    HorizontalPosition: { Align: 'Left', Offset: 0 },
                                    VerticalPosition: { Align: 'Top', Offset: 0 }
                                }
                            }
                        },
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Text intentionally shares the same visual band as the front image.' }] } },
                        {
                            Id: 'img-square',
                            Type: 'Image',
                            Content: {
                                Id: 'obj-square',
                                AltText: 'Square image',
                                Caption: 'Square caption',
                                Layout: {
                                    Width: 120,
                                    Height: 70,
                                    WrapMode: 'Square',
                                    WrapMargin: 8,
                                    HorizontalPosition: { Align: 'Left', Offset: 0 },
                                    VerticalPosition: { Align: 'Top', Offset: 0 }
                                }
                            }
                        }
                    ]
                });
                const probe = engine.getLayoutProbe(id);
                const frames = await engine.runFrameProbe(id, 2);
                engine.dispose(id);
                host.remove();
                return {
                    textRectCount: probe.textRects.length,
                    imageRectCount: probe.imageRects.length,
                    captionRectCount: probe.captionRects.length,
                    lineBoxCount: probe.lineBoxes.length,
                    exclusionZoneCount: probe.exclusionZones.length,
                    collisionCount: probe.collisions.length,
                    allowedCollisionCount: probe.collisions.filter(item => item.allowed === true).length,
                    collisionAllowedIsBoolean: probe.collisions.every(item => typeof item.allowed === 'boolean'),
                    frameCount: frames.frameCount,
                    frameHasProbeData: frames.frames.every(frame => frame.textRects.length > 0 && frame.imageRects.length > 0)
                };
            }
            """);

        result.TextRectCount.Should().BeGreaterThan(0);
        result.ImageRectCount.Should().BeGreaterThanOrEqualTo(2);
        result.CaptionRectCount.Should().BeGreaterThanOrEqualTo(2);
        result.LineBoxCount.Should().BeGreaterThan(0);
        result.ExclusionZoneCount.Should().BeGreaterThan(0);
        result.CollisionCount.Should().BeGreaterThan(0);
        result.AllowedCollisionCount.Should().BeGreaterThan(0);
        result.CollisionAllowedIsBoolean.Should().BeTrue();
        result.FrameCount.Should().Be(2);
        result.FrameHasProbeData.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Diagnostics_WatchdogRecoversFailuresWithoutDroppingUserText()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<WatchdogRecoveryProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const host = document.createElement('div');
                document.body.appendChild(host);
                const id = engine.create(host, { instanceId: 'phase17-watchdog' });
                engine.loadDocument(id, {
                    DocumentId: 'phase17-watchdog-doc',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Important user text' }] } }]
                });
                const before = engine.getDocumentSnapshot(id).document.body.blocks[0].content.runs.map(run => run.text).join('');
                const operation = engine.simulateWatchdogFailure(id, 'operation');
                const layout = engine.simulateWatchdogFailure(id, 'layout');
                const render = engine.simulateWatchdogFailure(id, 'render');
                const selection = engine.simulateWatchdogFailure(id, 'selection');
                const after = engine.getDocumentSnapshot(id).document.body.blocks[0].content.runs.map(run => run.text).join('');
                const debug = engine.getDebugSnapshot(id);
                const kinds = (debug.timeline || []).map(item => item.kind);
                engine.dispose(id);
                host.remove();
                return {
                    before,
                    after,
                    operationRecovered: operation.recovered,
                    operationTextPreserved: operation.textPreserved,
                    layoutRecovered: layout.recovered,
                    layoutTextPreserved: layout.textPreserved,
                    renderRecovered: render.recovered,
                    renderTextPreserved: render.textPreserved,
                    selectionRecovered: selection.recovered,
                    selectionTextPreserved: selection.textPreserved,
                    watchdogFailures: debug.watchdogFailures,
                    debugWarningVisible: debug.debugWarningVisible,
                    lastErrorCount: debug.lastErrors.length,
                    hasErrorRecoveryTimeline: kinds.includes('error-recovery'),
                    selectionBlock: debug.selection?.blockId || ''
                };
            }
            """);

        result.Before.Should().Be("Important user text");
        result.After.Should().Be("Important user text");
        result.OperationRecovered.Should().BeTrue();
        result.OperationTextPreserved.Should().BeTrue();
        result.LayoutRecovered.Should().BeTrue();
        result.LayoutTextPreserved.Should().BeTrue();
        result.RenderRecovered.Should().BeTrue();
        result.RenderTextPreserved.Should().BeTrue();
        result.SelectionRecovered.Should().BeTrue();
        result.SelectionTextPreserved.Should().BeTrue();
        result.WatchdogFailures.Should().BeGreaterThanOrEqualTo(4);
        result.DebugWarningVisible.Should().BeTrue();
        result.LastErrorCount.Should().BeGreaterThanOrEqualTo(4);
        result.HasErrorRecoveryTimeline.Should().BeTrue();
        result.SelectionBlock.Should().Be("p1");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Diagnostics_FailureArtifactContainsDebugProbeTimelineAndCanonicalDocument()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<FailureArtifactProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const host = document.createElement('div');
                document.body.appendChild(host);
                const id = engine.create(host, { instanceId: 'phase17-artifact' });
                engine.loadDocument(id, {
                    DocumentId: 'phase17-artifact-doc',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Artifact text' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Id: 'obj1', AltText: 'Artifact image', Caption: 'Artifact caption', Layout: { Width: 110, Height: 70, WrapMode: 'Square' } } }
                    ]
                });
                engine.simulateWatchdogFailure(id, 'render');
                const artifact = engine.exportFailureArtifact(id, 'phase17-test');
                const text = artifact.document.body.blocks[0].content.runs.map(run => run.text).join('');
                engine.dispose(id);
                host.remove();
                return {
                    ok: artifact.ok,
                    reason: artifact.reason,
                    hasDebugSnapshot: !!artifact.debugSnapshot?.modelVersion,
                    hasLayoutProbe: artifact.layoutProbe?.imageRects?.length > 0,
                    timelineCount: artifact.timeline?.length || 0,
                    recoveryFailures: artifact.recovery?.watchdogFailures || 0,
                    hasCSharpDocument: !!artifact.csharpDocument?.Blocks,
                    text
                };
            }
            """);

        result.Ok.Should().BeTrue();
        result.Reason.Should().Be("phase17-test");
        result.HasDebugSnapshot.Should().BeTrue();
        result.HasLayoutProbe.Should().BeTrue();
        result.TimelineCount.Should().BeGreaterThan(0);
        result.RecoveryFailures.Should().BeGreaterThan(0);
        result.HasCSharpDocument.Should().BeTrue();
        result.Text.Should().Be("Artifact text");
    }

    public sealed class DebugSnapshotProbe
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("modelVersion")] public int ModelVersion { get; set; }
        [JsonPropertyName("layoutVersion")] public int LayoutVersion { get; set; }
        [JsonPropertyName("renderVersion")] public int RenderVersion { get; set; }
        [JsonPropertyName("selectionVersion")] public int SelectionVersion { get; set; }
        [JsonPropertyName("hasSelection")] public bool HasSelection { get; set; }
        [JsonPropertyName("activeTransactionIsNull")] public bool ActiveTransactionIsNull { get; set; }
        [JsonPropertyName("undoDepth")] public int UndoDepth { get; set; }
        [JsonPropertyName("redoDepth")] public int RedoDepth { get; set; }
        [JsonPropertyName("invalidatedScopes")] public string[] InvalidatedScopes { get; set; } = [];
        [JsonPropertyName("layoutPassCount")] public int LayoutPassCount { get; set; }
        [JsonPropertyName("renderPassCount")] public int RenderPassCount { get; set; }
        [JsonPropertyName("lastErrorsIsArray")] public bool LastErrorsIsArray { get; set; }
        [JsonPropertyName("hasInputEvent")] public bool HasInputEvent { get; set; }
        [JsonPropertyName("hasNormalizedOperation")] public bool HasNormalizedOperation { get; set; }
        [JsonPropertyName("hasTransactionCommit")] public bool HasTransactionCommit { get; set; }
        [JsonPropertyName("hasLayoutPass")] public bool HasLayoutPass { get; set; }
        [JsonPropertyName("hasRenderPass")] public bool HasRenderPass { get; set; }
        [JsonPropertyName("hasSelectionRestore")] public bool HasSelectionRestore { get; set; }
        [JsonPropertyName("hasBlazorPatchEmit")] public bool HasBlazorPatchEmit { get; set; }
    }

    public sealed class LayoutProbeResult
    {
        [JsonPropertyName("textRectCount")] public int TextRectCount { get; set; }
        [JsonPropertyName("imageRectCount")] public int ImageRectCount { get; set; }
        [JsonPropertyName("captionRectCount")] public int CaptionRectCount { get; set; }
        [JsonPropertyName("lineBoxCount")] public int LineBoxCount { get; set; }
        [JsonPropertyName("exclusionZoneCount")] public int ExclusionZoneCount { get; set; }
        [JsonPropertyName("collisionCount")] public int CollisionCount { get; set; }
        [JsonPropertyName("allowedCollisionCount")] public int AllowedCollisionCount { get; set; }
        [JsonPropertyName("collisionAllowedIsBoolean")] public bool CollisionAllowedIsBoolean { get; set; }
        [JsonPropertyName("frameCount")] public int FrameCount { get; set; }
        [JsonPropertyName("frameHasProbeData")] public bool FrameHasProbeData { get; set; }
    }

    public sealed class WatchdogRecoveryProbe
    {
        [JsonPropertyName("before")] public string Before { get; set; } = string.Empty;
        [JsonPropertyName("after")] public string After { get; set; } = string.Empty;
        [JsonPropertyName("operationRecovered")] public bool OperationRecovered { get; set; }
        [JsonPropertyName("operationTextPreserved")] public bool OperationTextPreserved { get; set; }
        [JsonPropertyName("layoutRecovered")] public bool LayoutRecovered { get; set; }
        [JsonPropertyName("layoutTextPreserved")] public bool LayoutTextPreserved { get; set; }
        [JsonPropertyName("renderRecovered")] public bool RenderRecovered { get; set; }
        [JsonPropertyName("renderTextPreserved")] public bool RenderTextPreserved { get; set; }
        [JsonPropertyName("selectionRecovered")] public bool SelectionRecovered { get; set; }
        [JsonPropertyName("selectionTextPreserved")] public bool SelectionTextPreserved { get; set; }
        [JsonPropertyName("watchdogFailures")] public int WatchdogFailures { get; set; }
        [JsonPropertyName("debugWarningVisible")] public bool DebugWarningVisible { get; set; }
        [JsonPropertyName("lastErrorCount")] public int LastErrorCount { get; set; }
        [JsonPropertyName("hasErrorRecoveryTimeline")] public bool HasErrorRecoveryTimeline { get; set; }
        [JsonPropertyName("selectionBlock")] public string SelectionBlock { get; set; } = string.Empty;
    }

    public sealed class FailureArtifactProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
        [JsonPropertyName("hasDebugSnapshot")] public bool HasDebugSnapshot { get; set; }
        [JsonPropertyName("hasLayoutProbe")] public bool HasLayoutProbe { get; set; }
        [JsonPropertyName("timelineCount")] public int TimelineCount { get; set; }
        [JsonPropertyName("recoveryFailures")] public int RecoveryFailures { get; set; }
        [JsonPropertyName("hasCSharpDocument")] public bool HasCSharpDocument { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }
}
