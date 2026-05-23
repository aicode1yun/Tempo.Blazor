using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 17 runtime watchdog recovery and diagnostics.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase17WatchdogE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase17_CommandCrash_RecoversAndPreservesTextAndSelection()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase17-command-{DateTimeOffset.UtcNow:HHmmssfff}";

        await EditorTypeAsync(page, $" {marker}");
        var before = await PlaceCaretAtMarkerEndAsync(page, marker);
        Assert.IsFalse(string.IsNullOrWhiteSpace(before.BlockId), "The test must capture a real pre-crash caret block.");
        Assert.IsTrue(await SimulateWatchdogCrashAsync(page, "command"));

        await Assertions.Expect(page.GetByTestId("document-runtime-message"))
            .ToContainTextAsync("command error", new() { Timeout = 10000 });
        await Assertions.Expect(page.GetByTestId("document-wysiwyg-host"))
            .ToContainTextAsync(marker, new() { Timeout = 10000 });

        var state = await ReadWatchdogStateAsync(page);
        Assert.AreEqual("recovered", state.State);
        Assert.AreEqual("command", state.LastDetail.Source);
        Assert.AreEqual("runtimeRecovered", state.LastDetail.Event);

        var after = await ReadSelectionSnapshotAsync(page);
        Assert.AreEqual(before.BlockId, after.BlockId, "Recovery should restore the selected block.");
        Assert.IsTrue(after.Offset >= before.Offset, "Recovery should restore the caret near the original text position.");
    }

    [TestMethod]
    public async Task Phase17_RemoteOperationCrash_UsesStableSnapshotFallback()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase17-remote-{DateTimeOffset.UtcNow:HHmmssfff}";

        await EditorTypeAsync(page, $" {marker}");
        Assert.IsTrue(await RefreshStableSnapshotAsync(page), "Stable snapshot refresh must succeed before forcing fallback.");
        Assert.IsTrue(await SimulateWatchdogCrashAsync(page, "remoteOperation", forceSnapshotFallback: true));

        await Assertions.Expect(page.GetByTestId("document-runtime-message"))
            .ToContainTextAsync("remote operation error", new() { Timeout = 10000 });
        await Assertions.Expect(page.GetByTestId("document-runtime-message"))
            .ToContainTextAsync("last stable snapshot", new() { Timeout = 10000 });
        await Assertions.Expect(page.GetByTestId("document-wysiwyg-host"))
            .ToContainTextAsync(marker, new() { Timeout = 10000 });

        var state = await ReadWatchdogStateAsync(page);
        Assert.AreEqual("recovered", state.State);
        Assert.AreEqual("remoteOperation", state.LastDetail.Source);
        Assert.IsTrue(state.LastDetail.UsedSnapshotFallback);
        Assert.IsTrue(state.Events.Any(item => item.Event == "snapshotFallbackUsed"), "Watchdog telemetry should record snapshot fallback.");
    }

    [TestMethod]
    public async Task Phase17_MarkersSurviveRecovery()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        Assert.IsTrue(await AddPhase17MarkersAsync(page), "The test must add comment and revision markers to the runtime store.");
        Assert.IsTrue(await SimulateWatchdogCrashAsync(page, "command"));

        await Assertions.Expect(page.GetByTestId("document-runtime-message"))
            .ToContainTextAsync("command error", new() { Timeout = 10000 });

        var markers = await ReadPhase17MarkerStateAsync(page);
        Assert.IsTrue(markers.HasCommentMarker, "Comment markers should be restored after runtime recovery.");
        Assert.IsTrue(markers.HasRevisionMarker, "Revision markers should be restored after runtime recovery.");
    }

    [TestMethod]
    public async Task Phase17_RecoveryFailure_ShowsFailedStateAndTelemetry()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        Assert.IsTrue(await SimulateWatchdogCrashAsync(
            page,
            "command",
            forceRecoveryFailure: true,
            maxAttempts: 1,
            baseBackoffMs: 1));

        await Assertions.Expect(page.GetByTestId("document-runtime-message"))
            .ToContainTextAsync("Editor recovery failed", new() { Timeout = 10000 });

        var state = await ReadWatchdogStateAsync(page);
        Assert.AreEqual("failed", state.State);
        Assert.AreEqual("runtimeRecoveryFailed", state.LastDetail.Event);
        Assert.AreEqual("command", state.LastDetail.Source);
    }

    private static Task<bool> SimulateWatchdogCrashAsync(
        IPage page,
        string source,
        bool forceSnapshotFallback = false,
        bool forceRecoveryFailure = false,
        int maxAttempts = 3,
        int baseBackoffMs = 1)
    {
        return page.EvaluateAsync<bool>(
            """
            args => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                if (!instanceId || !window.tmDocumentEditorRuntime?.__watchdog) return false;
                return window.tmDocumentEditorRuntime.__watchdog.simulateCrash(instanceId, args.source, {
                    forceSnapshotFallback: !!args.forceSnapshotFallback,
                    forceRecoveryFailure: !!args.forceRecoveryFailure,
                    maxAttempts: args.maxAttempts,
                    baseBackoffMs: args.baseBackoffMs,
                    message: `Phase 17 ${args.source} crash`
                });
            }
            """,
            new
            {
                source,
                forceSnapshotFallback,
                forceRecoveryFailure,
                maxAttempts,
                baseBackoffMs
            });
    }

    private static Task<bool> RefreshStableSnapshotAsync(IPage page)
    {
        return page.EvaluateAsync<bool>(
            """
            () => {
                const runtime = window.tmDocumentEditorRuntime;
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                if (!runtime || !instanceId) return false;
                const raw = runtime.getDocument(instanceId);
                if (!raw) return false;
                runtime.loadDocument(instanceId, JSON.parse(raw));
                const snapshot = runtime.__watchdog?.getStableSnapshot?.(instanceId);
                return !!snapshot?.document || !!snapshot?.Document;
            }
            """);
    }

    private static Task<WatchdogRuntimeState> ReadWatchdogStateAsync(IPage page)
    {
        return page.EvaluateAsync<WatchdogRuntimeState>(
            """
            () => {
                const runtime = window.tmDocumentEditorRuntime;
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return {
                    state: runtime?.__watchdog?.getState?.(instanceId) || '',
                    lastDetail: runtime?.__watchdog?.getLastRecoveryDetail?.(instanceId) || {},
                    events: runtime?.__watchdog?.getEvents?.(instanceId) || []
                };
            }
            """);
    }

    private static Task<SelectionState> PlaceCaretAtMarkerEndAsync(IPage page, string marker)
    {
        return page.EvaluateAsync<SelectionState>(
            """
            marker => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const blocks = Array.from(host?.querySelectorAll('.tm-wysiwyg-block[data-block-id]') || []);
                const block = blocks.find(item => (item.innerText || item.textContent || '').includes(marker));
                if (!block) return { blockId: '', offset: 0 };
                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                let node = null;
                let baseOffset = 0;
                let textNode;
                while ((textNode = walker.nextNode())) {
                    const text = textNode.textContent || '';
                    const index = text.indexOf(marker);
                    if (index >= 0) {
                        node = textNode;
                        baseOffset += index + marker.length;
                        break;
                    }
                    baseOffset += text.length;
                }

                if (!node) return { blockId: '', offset: 0 };
                const localOffset = Math.min((node.textContent || '').length, (node.textContent || '').indexOf(marker) + marker.length);
                const range = document.createRange();
                range.setStart(node, localOffset);
                range.collapse(true);
                block.closest('[contenteditable="true"]')?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);

                const snapshot = window.tmDocumentEditorRuntime?.getSelectionSnapshot?.(instanceId)
                    || window.tmDocumentEditorRuntime?.getRuntimeSelection?.(instanceId)
                    || {};
                return {
                    blockId: snapshot.BlockId || snapshot.blockId || snapshot.AnchorBlockId || snapshot.anchorBlockId || block.getAttribute('data-block-id') || '',
                    offset: Number(snapshot.Offset ?? snapshot.offset ?? snapshot.AnchorOffset ?? snapshot.anchorOffset ?? baseOffset)
                };
            }
            """,
            marker);
    }

    private static Task<SelectionState> ReadSelectionSnapshotAsync(IPage page)
    {
        return page.EvaluateAsync<SelectionState>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const snapshot = window.tmDocumentEditorRuntime?.getSelectionSnapshot?.(instanceId)
                    || window.tmDocumentEditorRuntime?.getRuntimeSelection?.(instanceId)
                    || {};
                return {
                    blockId: snapshot.BlockId || snapshot.blockId || snapshot.AnchorBlockId || snapshot.anchorBlockId || '',
                    offset: Number(snapshot.Offset ?? snapshot.offset ?? snapshot.AnchorOffset ?? snapshot.anchorOffset ?? 0)
                };
            }
            """);
    }

    private static Task<bool> AddPhase17MarkersAsync(IPage page)
    {
        return page.EvaluateAsync<bool>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const block = host?.querySelector('.tm-wysiwyg-block[data-block-id]');
                const blockId = block?.getAttribute('data-block-id') || '';
                if (!instanceId || !blockId || !window.tmDocumentEditorEngine?.upsertMarker) return false;

                window.tmDocumentEditorEngine.upsertMarker(instanceId, {
                    id: 'phase17-comment-marker',
                    type: 'comment',
                    range: { startBlockId: blockId, startOffset: 0, endBlockId: blockId, endOffset: 6 },
                    priority: 60,
                    affectsData: true,
                    targetId: 'phase17-comment-marker'
                });
                window.tmDocumentEditorEngine.upsertMarker(instanceId, {
                    id: 'phase17-revision-marker',
                    type: 'revisionInsertion',
                    range: { startBlockId: blockId, startOffset: 1, endBlockId: blockId, endOffset: 7 },
                    priority: 80,
                    affectsData: true,
                    targetId: 'phase17-revision-marker'
                });
                return true;
            }
            """);
    }

    private static Task<MarkerState> ReadPhase17MarkerStateAsync(IPage page)
    {
        return page.EvaluateAsync<MarkerState>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const markers = window.tmDocumentEditorEngine?.getMarkers?.(instanceId) || [];
                const typeOf = marker => marker.type || marker.Type || '';
                return {
                    hasCommentMarker: markers.some(marker => marker.id === 'phase17-comment-marker' || marker.Id === 'phase17-comment-marker'),
                    hasRevisionMarker: markers.some(marker => marker.id === 'phase17-revision-marker' || marker.Id === 'phase17-revision-marker' || String(typeOf(marker)).startsWith('revision'))
                };
            }
            """);
    }

    private sealed class WatchdogRuntimeState
    {
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("lastDetail")]
        public WatchdogRecoveryDetail LastDetail { get; set; } = new();

        [JsonPropertyName("events")]
        public WatchdogRecoveryDetail[] Events { get; set; } = [];
    }

    private sealed class WatchdogRecoveryDetail
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("usedSnapshotFallback")]
        public bool UsedSnapshotFallback { get; set; }
    }

    private sealed class SelectionState
    {
        [JsonPropertyName("blockId")]
        public string BlockId { get; set; } = string.Empty;

        [JsonPropertyName("offset")]
        public int Offset { get; set; }
    }

    private sealed class MarkerState
    {
        [JsonPropertyName("hasCommentMarker")]
        public bool HasCommentMarker { get; set; }

        [JsonPropertyName("hasRevisionMarker")]
        public bool HasRevisionMarker { get; set; }
    }
}
