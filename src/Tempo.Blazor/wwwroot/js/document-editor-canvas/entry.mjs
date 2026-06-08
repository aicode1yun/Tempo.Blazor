import { createAccessibilityMirror } from './a11y/accessibility-mirror.mjs';
import { createCanvasLiveRegion } from './a11y/live-region.mjs';
import {
    addCommentToCanvasModel,
    createCanvasCommentOverlay,
    deleteCommentFromCanvasModel,
} from './annotations/comment-overlay.mjs';
import { createCanvasRevisionOverlay, applyReviewDecision, applyReviewDecisionAll, normalizeReviewDisplayMode } from './annotations/revision-render.mjs';
import { createRestrictedEditingRuntime } from './annotations/restricted-editing.mjs';
import { createCanvasClipboardController } from './clipboard/clipboard-controller.mjs';
import { createCanvasOperationLog } from './collaboration/op-log.mjs';
import { createCanvasPresenceOverlay } from './collaboration/presence-overlay.mjs';
import { applyRemoteOperationBatch as applyCanvasRemoteOperationBatch } from './collaboration/transform.mjs';
import { createCanvasCommandRuntime } from './commands/dispatcher.mjs';
import { createCanvasProofingService } from './diagnostics/proofing-service.mjs';
import { createCanvasProofingSquiggleOverlay } from './diagnostics/squiggle-overlay.mjs';
import { createCanvasHistoryController } from './history/history-controller.mjs';
import { createHiddenInputBridge } from './input/hidden-input-bridge.mjs';
import { createCanvasInputController } from './input/input-controller.mjs';
import { createLayoutService } from './layout/page-geometry.mjs';
import { createModelStore } from './model/model-store.mjs';
import { createRecalcInfo } from './perf/recalc-info.mjs';
import { createPerformanceMetrics } from './perf/runtime-metrics.mjs';
import { CANVAS_LAYER_KINDS, createCanvasStack } from './render/canvas-stack.mjs';
import { createCanvasRulerOverlay, syncBlockVisualization } from './render/ruler.mjs';
import { createCanvasSearchOverlay } from './search/search-overlay.mjs';
import { createCanvasSelectionController } from './selection/selection-controller.mjs';
import { createCanvasShortcutManager } from './shortcuts/shortcut-manager.mjs';
import { createPrintDialogResult, createPrintPreviewSnapshot } from './view/print-preview.mjs';

export { CANVAS_LAYER_KINDS };
export { DEFAULT_PAGE_SETUP } from './layout/page-geometry.mjs';

export function createCanvasDocumentEngine(options = {}) {
    return new CanvasDocumentEngine(options);
}

export class CanvasDocumentEngine {
    constructor(options = {}) {
        const host = options.host;
        const doc = options.document || host?.ownerDocument || globalThis.document;
        if (!host || typeof host.appendChild !== 'function') {
            throw new Error('CanvasDocumentEngine requires a host element.');
        }
        if (!doc || typeof doc.createElement !== 'function') {
            throw new Error('CanvasDocumentEngine requires a DOM-like document.');
        }

        this.host = host;
        this.document = doc;
        this.modelStore = createModelStore(options.model);
        this.recalcInfo = createRecalcInfo();
        this.recalcInfo.updateBlockOrder(this.modelStore.getModel());
        this.performanceMetrics = createPerformanceMetrics();
        this.engineOptions = {
            contentControlRenderMode: options.contentControlRenderMode || 'form',
        };
        this.pendingInputRender = null;
        this.inputRenderScheduled = false;
        // Deferred (debounced) proofing analysis + accessibility mirror rebuild — see refreshModelAnalysis.
        this.lastAnalyzedModelVersion = undefined;
        this.modelAnalysisTimer = 0;
        this.forceImmediateAnalysis = false;
        this.pendingTextInputSideEffects = null;
        this.textInputSideEffectsTimer = 0;
        this.trackChangesEnabled = options.trackChanges?.enabled === true;
        this.reviewDisplayMode = normalizeReviewDisplayMode(options.trackChanges?.reviewDisplayMode || options.reviewDisplayMode);
        this.author = options.author || options.trackChanges?.author || null;
        this.operationLog = createCanvasOperationLog({
            documentId: options.model?.documentId,
            clientId: options.collaboration?.clientId,
            model: options.model,
        });
        this.layoutService = createLayoutService({ pageSettings: options.pageSettings });
        this.canvasStack = createCanvasStack({
            document: doc,
            pixelRatioProvider: options.pixelRatioProvider,
            theme: options.theme,
        });
        this.rulerOverlay = createCanvasRulerOverlay({
            document: doc,
            executeCommand: (commandId, argument) => this.execCommand(commandId, argument),
            getModel: () => this.modelStore.getModel(),
            getSelection: () => this.selectionController?.getSelection?.() || null,
            getLayout: () => this.lastLayout,
        });
        this.accessibilityMirror = createAccessibilityMirror({
            document: doc,
            ariaLabel: options.ariaLabel,
        });
        this.liveRegion = createCanvasLiveRegion({
            document: doc,
            ariaLabel: options.accessibility?.liveRegionLabel,
            messages: options.accessibility,
        });
        this.inputBridge = createHiddenInputBridge({
            document: doc,
            ariaLabel: options.inputAriaLabel || options.ariaLabel || '',
            controlsId: 'document-canvas-a11y-mirror',
            describedById: 'document-canvas-live-region',
        });
        this.selectionController = createCanvasSelectionController({
            document: doc,
            canvasStack: this.canvasStack,
            inputBridge: this.inputBridge,
            openLinkAtPosition: position => this.commandRuntime?.openLinkAtPosition(position) === true,
            executeCommand: (commandId, argument) => this.execCommand(commandId, argument),
            onSelectionChanged: state => this.notifySelectionChanged(state),
        });
        this.history = createCanvasHistoryController();
        this.commandRuntime = createCanvasCommandRuntime({
            getModel: () => this.modelStore.getModel(),
            getSelection: () => this.selectionController.getSelection(),
            getLayout: () => this.lastLayout,
            getViewportMetrics: () => this.getViewportMetrics(),
            commit: change => this.commitCommandChange(change),
            history: this.history,
            openUrl: href => this.openUrl(href),
            getTrackChangesState: () => ({ enabled: this.trackChangesEnabled, author: this.author }),
        });
        this.commandDispatcher = this.commandRuntime;
        this.inputController = createCanvasInputController({
            inputBridge: this.inputBridge,
            selectionController: this.selectionController,
            getModel: () => this.modelStore.getModel(),
            commit: change => this.commitInputChange(change),
            afterCommit: input => this.publishInputDiagnostics(input),
            getPendingMarks: () => this.commandRuntime.getPendingMarks(),
            getTrackChangesState: () => ({ enabled: this.trackChangesEnabled, author: this.author }),
            executeCommand: (commandId, argument) => {
                const result = this.execCommand(commandId, argument);
                if (String(commandId || '').replace(/[\s_-]/g, '').toLowerCase() === 'navigatetablecell') {
                    return result.handled === true && result.result?.selectionChanged === true;
                }

                if (/^(increase|decrease)listlevel$/i.test(String(commandId || '').replace(/[\s_-]/g, ''))) {
                    return result.handled === true && result.result?.changed === true;
                }

                if (/^(navigate|next|previous|focus)contentcontrol$/i.test(String(commandId || '').replace(/[\s_-]/g, ''))) {
                    return result.handled === true && result.result?.selectionChanged === true;
                }

                return result.handled === true;
            },
        });
        this.clipboardController = createCanvasClipboardController({
            root: this.canvasStack.root,
            inputBridge: this.inputBridge,
            selectionController: this.selectionController,
            getModel: () => this.modelStore.getModel(),
            commit: change => this.commitClipboardChange(change),
            history: this.history,
            uploadImage: options.uploadImage,
        });
        this.proofingService = createCanvasProofingService({
            ...(options.proofing || {}),
            enabled: options.proofing?.enabled !== false,
        });
        this.proofingOverlay = createCanvasProofingSquiggleOverlay({
            document: doc,
            canvasStack: this.canvasStack,
        });
        this.searchOverlay = createCanvasSearchOverlay({
            document: doc,
            canvasStack: this.canvasStack,
        });
        this.commentOverlay = createCanvasCommentOverlay({ document: doc });
        this.revisionOverlay = createCanvasRevisionOverlay({ document: doc });
        this.restrictedEditing = createRestrictedEditingRuntime();
        this.presenceOverlay = createCanvasPresenceOverlay({ document: doc });
        this.shortcutManager = createCanvasShortcutManager({
            root: this.canvasStack.root,
            input: this.inputBridge.input,
            execCommand: (command, argument) => this.execCommand(command, argument),
            openCommandPalette: () => this.onCommandPaletteRequested?.(),
            focusRibbon: () => this.onRibbonFocusRequested?.(),
            openVersionsPanel: () => this.onVersionsPanelRequested?.(),
        });
        this.interop = createInteropBridge(this);
        this.viewport = null;
        this.lastLayout = null;
        this.lastRender = null;
        this.lastPrintPreview = null;
        this.lastPrintDialog = null;
        this.applyingRemoteBatch = false;
        this.onModelChanged = typeof options.onModelChanged === 'function' ? options.onModelChanged : null;
        this.onContextMenu = typeof options.onContextMenu === 'function' ? options.onContextMenu : null;
        this.onSelectionChange = typeof options.onSelectionChange === 'function' ? options.onSelectionChange : null;
        this.onCommandPaletteRequested = typeof options.onCommandPaletteRequested === 'function' ? options.onCommandPaletteRequested : null;
        this.onRibbonFocusRequested = typeof options.onRibbonFocusRequested === 'function' ? options.onRibbonFocusRequested : null;
        this.onVersionsPanelRequested = typeof options.onVersionsPanelRequested === 'function' ? options.onVersionsPanelRequested : null;
        this.onAnnotationSelected = typeof options.onAnnotationSelected === 'function' ? options.onAnnotationSelected : null;
        this.contextMenuHandler = event => this.handleContextMenu(event);
        this.clickHandler = event => this.handleClick(event);
        this.wheelHandler = event => this.handleWheel(event);
        this.scrollHandler = () => this.handleScroll();
        this.scrollRenderPending = false;
        this.mounted = false;
    }

    mount() {
        if (this.mounted) {
            return this;
        }

        this.canvasStack.mount(this.host);
        this.host.appendChild(this.rulerOverlay.root);
        this.host.appendChild(this.accessibilityMirror.root);
        this.host.appendChild(this.liveRegion.root);
        this.host.appendChild(this.inputBridge.input);
        this.commentOverlay.mount(this.canvasStack);
        this.revisionOverlay.mount(this.canvasStack);
        this.presenceOverlay.mount(this.canvasStack.root);
        this.selectionController.mount();
        this.inputController.mount();
        this.clipboardController.mount();
        this.shortcutManager.mount();
        this.canvasStack.root?.addEventListener?.('contextmenu', this.contextMenuHandler);
        this.canvasStack.root?.addEventListener?.('click', this.clickHandler);
        this.canvasStack.root?.addEventListener?.('wheel', this.wheelHandler, { passive: false });
        this.host?.addEventListener?.('scroll', this.scrollHandler, { passive: true });
        this.document?.defaultView?.addEventListener?.('scroll', this.scrollHandler, { passive: true });
        this.host.setAttribute?.('data-canvas-engine-ready', 'false');
        this.host.setAttribute?.('data-canvas-engine-page-strategy', this.layoutService.pageSurfaceStrategy);
        this.mounted = true;
        return this;
    }

    render(renderOptions = {}) {
        if (!this.mounted) {
            this.mount();
        }

        const startedAt = now();
        const model = this.modelStore.getModel();
        this.recalcInfo.updateBlockOrder(model);
        const viewport = this.readViewport();
        const recalcOptions = this.recalcInfo.immediateRenderOptions(renderOptions);
        const layout = this.layoutService.layout(model, viewport);
        const viewState = this.commandRuntime.getViewState();
        const render = this.canvasStack.render(layout, model, {
            contentControlRenderMode: this.engineOptions.contentControlRenderMode,
            ...renderOptions,
            ...recalcOptions,
            viewState,
            viewport,
        });
        this.selectionController.update(render.selectionLayout, model);
        // Proofing analysis and the accessibility DOM mirror are O(document). Re-position the existing
        // squiggles against the new text (cheap, last analysis) here; the expensive re-analysis and
        // mirror rebuild are deferred to a debounced idle pass so typing stays fast (Phase 6).
        this.proofingOverlay.update(this.proofingService.snapshot(), render);
        this.searchOverlay.update(this.commandRuntime.getSearchState(), render);
        this.restrictedEditing.update(model);
        this.commentOverlay.update(model, render, { selectedCommentId: this.selectedCommentId || '' });
        this.revisionOverlay.update(model, render, {
            selectedRevisionId: this.selectedRevisionId || '',
            reviewMode: this.reviewDisplayMode,
        });
        this.presenceOverlay.update(this.operationLog.cursors(), render, model);
        this.rulerOverlay.update(layout, model, {
            ...viewState,
            selection: this.selectionController.getSelection(),
        });
        syncBlockVisualization(this.canvasStack, render.selectionLayout, model, viewState);
        this.lastLayout = layout;
        this.lastRender = render;
        this.refreshModelAnalysis(model, render);
        this.lastPrintPreview = createPrintPreviewSnapshot(model, layout, render, viewState);
        this.host.setAttribute?.('data-canvas-engine-ready', 'true');
        this.publishPerformanceDiagnostics(this.performanceMetrics.recordRender(now() - startedAt, render), render);
        this.publishFormattingDiagnostics();

        return {
            ok: true,
            layout,
            render,
            architecture: this.getArchitecture(),
        };
    }

    // Refreshes the O(document) proofing analysis + accessibility mirror without blocking typing:
    // the first analysis (and any model replacement, e.g. import/load) runs immediately; incremental
    // edits are coalesced into a single debounced pass that fires once typing pauses.
    refreshModelAnalysis(model, render) {
        const version = this.modelStore.getVersion();
        if (version === this.lastAnalyzedModelVersion && !this.forceImmediateAnalysis) {
            return;
        }

        if (this.forceImmediateAnalysis || this.lastAnalyzedModelVersion === undefined) {
            this.forceImmediateAnalysis = false;
            this.clearModelAnalysisTimer();
            this.runModelAnalysis(model, render);
            return;
        }

        this.scheduleModelAnalysis();
    }

    runModelAnalysis(model = this.modelStore.getModel(), render = this.lastRender) {
        const proofing = this.proofingService.analyze(model, render);
        this.proofingOverlay.update(proofing, render);
        this.accessibilityMirror.update(model);
        this.lastAnalyzedModelVersion = this.modelStore.getVersion();
    }

    scheduleModelAnalysis() {
        this.clearModelAnalysisTimer();
        const view = this.document?.defaultView || globalThis.window || globalThis;
        const run = () => {
            this.modelAnalysisTimer = 0;
            if (this.mounted) {
                this.runModelAnalysis();
            }
        };
        this.modelAnalysisTimer = view.setTimeout ? view.setTimeout(run, 180) : setTimeout(run, 180);
    }

    clearModelAnalysisTimer() {
        if (!this.modelAnalysisTimer) {
            return;
        }

        const view = this.document?.defaultView || globalThis.window || globalThis;
        if (view.clearTimeout) {
            view.clearTimeout(this.modelAnalysisTimer);
        } else {
            clearTimeout(this.modelAnalysisTimer);
        }

        this.modelAnalysisTimer = 0;
    }

    // Paint-only re-render for scroll/zoom: the model and layout are unchanged, so we reuse the
    // cached display list (no document re-layout) and only re-position the viewport-dependent
    // surfaces. The expensive model-only passes — proofing analysis, the accessibility DOM mirror,
    // restricted-editing and ruler — are deliberately skipped because their inputs did not change.
    repaint(repaintOptions = {}) {
        if (!this.mounted) {
            return null;
        }

        const model = this.modelStore.getModel();
        const viewState = this.commandRuntime.getViewState();
        const viewport = this.readViewport();
        const render = this.canvasStack.repaint({ viewState, viewport, ...repaintOptions });
        if (!render) {
            // No cached plan yet (first frame): fall back to a full recalc.
            return this.render({ forceRepaint: false });
        }

        this.selectionController.update(render.selectionLayout, model);
        this.proofingOverlay.update(this.proofingService.snapshot(), render);
        this.searchOverlay.update(this.commandRuntime.getSearchState(), render);
        this.commentOverlay.update(model, render, { selectedCommentId: this.selectedCommentId || '' });
        this.revisionOverlay.update(model, render, {
            selectedRevisionId: this.selectedRevisionId || '',
            reviewMode: this.reviewDisplayMode,
        });
        this.presenceOverlay.update(this.operationLog.cursors(), render, model);
        syncBlockVisualization(this.canvasStack, render.selectionLayout, model, viewState);
        this.lastRender = render;
        this.publishPerformanceDiagnostics(this.performanceMetrics.snapshot(), render);

        return {
            ok: true,
            layout: this.lastLayout,
            render,
            architecture: this.getArchitecture(),
        };
    }

    setModel(model) {
        this.modelStore.setModel(model);
        // A full model replacement (import / document load) should refresh proofing + a11y immediately,
        // not on the typing debounce.
        this.forceImmediateAnalysis = true;
        return this;
    }

    updateOptions(options = {}) {
        this.engineOptions = {
            ...this.engineOptions,
            contentControlRenderMode: options.contentControlRenderMode || this.engineOptions.contentControlRenderMode,
        };
        return this.render({ forceRepaint: true, structural: true });
    }

    setViewport(viewport) {
        this.viewport = viewport || null;
        return this;
    }

    focusInput() {
        this.inputBridge.focus();
        return true;
    }

    getSnapshot() {
        return {
            mounted: this.mounted,
            model: this.modelStore.getModel(),
            modelVersion: this.modelStore.getVersion(),
            layout: this.lastLayout,
            render: this.lastRender,
            selection: this.selectionController.getState(),
            input: this.inputController.getState(),
            clipboard: this.clipboardController.getState(),
            proofing: this.proofingService.snapshot(),
            proofingOverlay: this.proofingOverlay.snapshot(),
            comments: this.commentOverlay.snapshot(),
            revisions: this.revisionOverlay.snapshot(),
            restrictedEditing: this.restrictedEditing.snapshot(),
            collaboration: this.operationLog.snapshot(),
            presence: this.presenceOverlay.snapshot(),
            search: this.commandRuntime.getSearchState(),
            searchOverlay: this.searchOverlay.snapshot(),
            printPreview: this.lastPrintPreview,
            printDialog: this.lastPrintDialog,
            shortcuts: this.shortcutManager.snapshot(),
            formatting: this.commandRuntime.queryCommandState(),
            history: this.history.snapshot(),
            performance: this.performanceMetrics.snapshot(),
            recalc: this.recalcInfo.snapshot(),
            architecture: this.getArchitecture(),
        };
    }

    getArchitecture() {
        return {
            name: 'CanvasDocumentEngine',
            pageSurfaceStrategy: this.layoutService.pageSurfaceStrategy,
            modelAuthority: 'model-store',
            layoutPipeline: ['model-store', 'layout-service', 'display-list', 'canvas-renderer'],
            layerKinds: CANVAS_LAYER_KINDS.slice(),
            accessibilityMirror: 'semantic-dom-mirror',
            liveRegion: 'localized-status-region',
            inputBridge: 'hidden-textarea-beforeinput',
            inputRuntime: 'canvas-input-controller',
            clipboardRuntime: 'canvas-clipboard-controller',
            proofingRuntime: 'canvas-proofing-service',
            proofingOverlay: 'diagnostics-canvas-squiggle-overlay',
            searchRuntime: 'canvas-search-outline-toc-runtime',
            searchOverlay: 'dom-search-highlight-overlay',
            commentOverlay: 'dom-comment-highlight-overlay',
            revisionRuntime: 'canvas-track-changes-review-runtime',
            revisionOverlay: 'dom-revision-highlight-overlay',
            restrictedEditing: 'canvas-protected-region-gate',
            pageVirtualization: 'visible-pages-with-buffer',
            tileCache: 'per-page-content-signature-cache',
            recalcInfo: 'dirty-block-incremental-reconciliation',
            performanceMetrics: 'first-paint-typing-scroll-samples',
            collaborationRuntime: 'serializable-operation-log',
            collaborationMerge: 'deterministic-operation-transform',
            presenceOverlay: 'dom-caret-presence-overlay',
            selectionRuntime: 'canvas-selection-controller',
            shortcutRuntime: 'canvas-shortcut-manager',
            commandPipeline: 'canvas-command-runtime',
            history: 'transaction-history-controller',
            interop: 'marshalable-engine-bridge',
        };
    }

    destroy() {
        this.clipboardController.destroy();
        this.shortcutManager.destroy();
        this.canvasStack.root?.removeEventListener?.('contextmenu', this.contextMenuHandler);
        this.canvasStack.root?.removeEventListener?.('click', this.clickHandler);
        this.canvasStack.root?.removeEventListener?.('wheel', this.wheelHandler);
        this.host?.removeEventListener?.('scroll', this.scrollHandler);
        this.document?.defaultView?.removeEventListener?.('scroll', this.scrollHandler);
        this.pendingInputRender = null;
        this.inputRenderScheduled = false;
        this.clearModelAnalysisTimer();
        this.clearTextInputSideEffectsTimer();
        this.flushPendingTextInputSideEffects();
        this.searchOverlay.destroy();
        this.commentOverlay.destroy();
        this.revisionOverlay.destroy();
        this.presenceOverlay.destroy();
        this.proofingOverlay.destroy();
        this.inputController.destroy();
        this.inputBridge.destroy();
        this.selectionController.destroy();
        this.rulerOverlay.destroy();
        this.canvasStack.destroy();
        if (this.accessibilityMirror.root.parentNode) {
            this.accessibilityMirror.root.parentNode.removeChild(this.accessibilityMirror.root);
        }
        if (this.liveRegion.root.parentNode) {
            this.liveRegion.root.parentNode.removeChild(this.liveRegion.root);
        }
        this.host.removeAttribute?.('data-canvas-engine-ready');
        this.mounted = false;
    }

    commitInputChange(change) {
        const startedAt = now();
        const modelSetStartedAt = now();
        this.modelStore.setModel(change.model, { normalize: false });
        const modelSetMs = now() - modelSetStartedAt;
        const sideEffectsStartedAt = now();
        this.queueTextInputSideEffects(change);
        const sideEffectsMs = now() - sideEffectsStartedAt;
        const recalcStartedAt = now();
        this.recalcInfo.markDirty(change.result?.dirtyBlockIds || change.input?.dirtyBlockIds || [], {
            structural: change.result?.insertedBlockId != null || (change.result?.removedBlockIds || []).length > 0,
        });
        const recalcMs = now() - recalcStartedAt;
        const selectionStartedAt = now();
        if (change.selection) {
            this.selectionController.setSelection(change.selection, { render: false });
        }
        const selectionMs = now() - selectionStartedAt;

        const renderOptions = {
            dirtyBlockIds: change.result?.dirtyBlockIds || change.input?.dirtyBlockIds || [],
            structural: change.result?.insertedBlockId != null || (change.result?.removedBlockIds || []).length > 0,
        };
        const inputLatencyMs = now() - startedAt;
        this.performanceMetrics.recordTypingLatency(inputLatencyMs);
        this.publishPerformanceDiagnostics(this.performanceMetrics.snapshot(), this.lastRender);
        const root = this.canvasStack.root;
        root?.setAttribute?.('data-canvas-input-model-set-ms', String(roundMetric(modelSetMs)));
        root?.setAttribute?.('data-canvas-input-side-effects-ms', String(roundMetric(sideEffectsMs)));
        root?.setAttribute?.('data-canvas-input-recalc-ms', String(roundMetric(recalcMs)));
        root?.setAttribute?.('data-canvas-input-selection-ms', String(roundMetric(selectionMs)));
        root?.setAttribute?.('data-canvas-input-hot-path-ms', String(roundMetric(inputLatencyMs)));
        const render = this.scheduleInputRender(renderOptions);
        this.notifyExternalModelChanged(change);
        return render;
    }

    queueTextInputSideEffects(change) {
        if (change?.input?.compositionPreview === true || change?.result?.changed === false) {
            return;
        }

        const dirtyBlockIds = new Set(this.pendingTextInputSideEffects?.result?.dirtyBlockIds || []);
        for (const id of change.result?.dirtyBlockIds || change.input?.dirtyBlockIds || []) {
            dirtyBlockIds.add(String(id || ''));
        }

        const before = change.result?.autoCorrect === true || change.result?.autoformat === true
            ? change.before
            : this.pendingTextInputSideEffects?.before || change.before;

        this.pendingTextInputSideEffects = {
            ...change,
            before,
            after: {
                model: change.model,
                selection: change.selection,
                formatState: change.formatState || null,
                paragraphState: change.paragraphState || null,
            },
            model: change.model,
            selection: change.selection,
            result: {
                ...(change.result || {}),
                dirtyBlockIds: Array.from(dirtyBlockIds).filter(Boolean),
            },
            input: {
                ...(change.input || {}),
                dirtyBlockIds: Array.from(dirtyBlockIds).filter(Boolean),
            },
        };

        this.scheduleTextInputSideEffectsFlush();
    }

    scheduleTextInputSideEffectsFlush() {
        this.clearTextInputSideEffectsTimer();
        const view = this.document?.defaultView || globalThis.window || globalThis;
        this.textInputSideEffectsTimer = view.setTimeout
            ? view.setTimeout(() => this.flushPendingTextInputSideEffects(), 180)
            : setTimeout(() => this.flushPendingTextInputSideEffects(), 180);
    }

    clearTextInputSideEffectsTimer() {
        if (!this.textInputSideEffectsTimer) {
            return;
        }

        const view = this.document?.defaultView || globalThis.window || globalThis;
        if (view.clearTimeout) {
            view.clearTimeout(this.textInputSideEffectsTimer);
        } else {
            clearTimeout(this.textInputSideEffectsTimer);
        }

        this.textInputSideEffectsTimer = 0;
    }

    flushPendingTextInputSideEffects() {
        this.clearTextInputSideEffectsTimer();
        const pending = this.pendingTextInputSideEffects;
        this.pendingTextInputSideEffects = null;
        if (!pending) {
            return null;
        }
        if (!pending.before?.model || !pending.after?.model) {
            return null;
        }

        this.history.recordTextInput?.(pending);
        return this.recordLocalCollaborationChange(pending.before?.model, pending.after?.model, pending);
    }

    scheduleInputRender(renderOptions) {
        const dirtyBlockIds = new Set(this.pendingInputRender?.dirtyBlockIds || []);
        for (const id of renderOptions?.dirtyBlockIds || []) {
            dirtyBlockIds.add(id);
        }

        this.pendingInputRender = {
            dirtyBlockIds: Array.from(dirtyBlockIds),
            structural: this.pendingInputRender?.structural === true || renderOptions?.structural === true,
        };

        if (this.inputRenderScheduled) {
            return {
                ok: true,
                scheduled: true,
                coalesced: true,
                pending: { ...this.pendingInputRender },
            };
        }

        this.inputRenderScheduled = true;
        scheduleFrame(this.document, () => {
            this.inputRenderScheduled = false;
            const pending = this.pendingInputRender;
            this.pendingInputRender = null;
            if (!this.mounted || !pending) {
                return;
            }

            this.render(pending);
            this.recalcInfo.queueIdleReconciliation(() => this.render({ forceRepaint: false }));
        });

        return {
            ok: true,
            scheduled: true,
            coalesced: false,
            pending: { ...this.pendingInputRender },
        };
    }

    publishInputDiagnostics(input) {
        const root = this.canvasStack.root;
        root?.setAttribute?.('data-canvas-input-revision', String(input.revision || 0));
        root?.setAttribute?.('data-canvas-input-operation', String(input.operation || ''));
        root?.setAttribute?.('data-canvas-input-source', String(input.source || ''));
        root?.setAttribute?.('data-canvas-input-render-duration-ms', String(Math.round((Number(input.durationMs) || 0) * 100) / 100));
        root?.setAttribute?.('data-canvas-input-dirty-block-ids', (input.dirtyBlockIds || []).join(','));
        root?.setAttribute?.('data-canvas-input-composition-preview', String(input.compositionPreview === true));
        const incremental = this.lastRender?.incremental || {};
        root?.setAttribute?.('data-canvas-input-incremental-repaint', String(incremental.enabled === true));
        root?.setAttribute?.('data-canvas-input-repaint-page-indexes', Array.isArray(incremental.repaintPageIndexes) ? incremental.repaintPageIndexes.join(',') : '');
        this.liveRegion.announceSelection(this.selectionController.getSelection());
    }

    commitCommandChange(change) {
        const viewOnlyChange = change?.result?.viewChanged === true || change?.result?.printRequested === true;
        const beforeModel = this.modelStore.getModel();
        if (!viewOnlyChange || change?.result?.changed === true) {
            this.modelStore.setModel(change.model);
            this.recordLocalCollaborationChange(beforeModel, change.model, change);
        }
        this.recalcInfo.markDirty(change.result?.dirtyBlockIds || [], {
            structural: (change.result?.insertedBlockIds || []).length > 0 || (change.result?.removedBlockIds || []).length > 0,
        });
        if (change.selection) {
            this.selectionController.setSelection(change.selection);
        }

        const result = this.render({
            dirtyBlockIds: change.result?.dirtyBlockIds || [],
            structural: (change.result?.insertedBlockIds || []).length > 0 || (change.result?.removedBlockIds || []).length > 0,
        });
        this.publishCommandDiagnostics(change);
        if (change?.command?.printRequested === true || change?.result?.printRequested === true) {
            this.lastPrintDialog = createPrintDialogResult(this.document?.defaultView || globalThis, this.lastPrintPreview);
            this.canvasStack.root?.setAttribute?.('data-canvas-print-dialog-requested', String(this.lastPrintDialog.requested === true));
            this.canvasStack.root?.setAttribute?.('data-canvas-print-dialog-invoked', String(this.lastPrintDialog.invoked === true));
        }
        if (!viewOnlyChange || change?.result?.changed === true) {
            this.notifyExternalModelChanged(change);
        }
        return result;
    }

    commitClipboardChange(change) {
        const beforeModel = this.modelStore.getModel();
        this.modelStore.setModel(change.model);
        this.recordLocalCollaborationChange(beforeModel, change.model, change);
        this.recalcInfo.markDirty(change.result?.dirtyBlockIds || [], {
            structural: (change.result?.insertedBlockIds || []).length > 0 || (change.result?.removedBlockIds || []).length > 0,
        });
        if (change.selection) {
            this.selectionController.setSelection(change.selection);
        }

        const result = this.render({
            dirtyBlockIds: change.result?.dirtyBlockIds || [],
            structural: (change.result?.insertedBlockIds || []).length > 0 || (change.result?.removedBlockIds || []).length > 0,
        });
        this.publishClipboardDiagnostics(change);
        this.notifyExternalModelChanged(change);
        return result;
    }

    publishClipboardDiagnostics(change) {
        const root = this.canvasStack.root;
        if (change?.command) {
            root?.setAttribute?.('data-canvas-command-last', String(change.command.id || ''));
            root?.setAttribute?.('data-canvas-command-changed', String(change.command.changed === true));
        }
        const state = this.clipboardController.getState();
        const debug = state.debug || {};
        root?.setAttribute?.('data-canvas-clipboard-revision', String(debug.revision || state.revision || 0));
        root?.setAttribute?.('data-canvas-clipboard-operation', String(debug.operation || ''));
        root?.setAttribute?.('data-canvas-clipboard-source', String(debug.source || ''));
        root?.setAttribute?.('data-canvas-clipboard-block-count', String(debug.blockCount || 0));
        root?.setAttribute?.('data-canvas-clipboard-warning-count', String((debug.warnings || []).length));
    }

    notifyExternalModelChanged(change) {
        this.onModelChanged?.({
            modelVersion: this.modelStore.getVersion(),
            operation: change?.result?.operation || change?.clipboard?.operation || '',
        });
    }

    recordLocalCollaborationChange(beforeModel, afterModel, change) {
        if (this.applyingRemoteBatch) {
            return null;
        }

        return this.operationLog.recordLocalChange({
            beforeModel,
            afterModel,
            operation: change?.result?.operation || change?.clipboard?.operation || change?.command?.id || '',
            selection: change?.selection || null,
        });
    }

    applyRemoteOperationBatch(batch) {
        const normalized = this.operationLog.appendRemoteBatch(batch);
        const pendingLocalOperations = this.operationLog.snapshot().pendingLocalBatches
            .flatMap(item => Array.isArray(item.operations) ? item.operations : []);
        this.applyingRemoteBatch = true;
        try {
            const result = applyCanvasRemoteOperationBatch(this.modelStore.getModel(), normalized || batch, {
                localOperations: pendingLocalOperations,
            });
            if (result.changed) {
                this.modelStore.setModel(result.model);
                this.recalcInfo.markDirty(affectedBlockIds(batch), {
                    structural: hasStructuralOperation(batch),
                });
                this.render({
                    dirtyBlockIds: affectedBlockIds(batch),
                    structural: hasStructuralOperation(batch),
                });
            }

            return result;
        } finally {
            this.applyingRemoteBatch = false;
        }
    }

    applyRemoteCursor(cursor) {
        this.operationLog.upsertCursor(cursor);
        this.presenceOverlay.update(this.operationLog.cursors(), this.lastRender, this.modelStore.getModel());
        return this.presenceOverlay.snapshot();
    }

    applyRemoteCursors(cursors) {
        this.operationLog.replaceCursors(cursors);
        this.presenceOverlay.update(this.operationLog.cursors(), this.lastRender, this.modelStore.getModel());
        return this.presenceOverlay.snapshot();
    }

    setTrackChangesEnabled(enabled, author = null) {
        this.trackChangesEnabled = enabled === true;
        if (author) {
            this.author = author;
        }

        this.canvasStack.root?.setAttribute?.('data-canvas-track-changes-enabled', String(this.trackChangesEnabled));
        return this.trackChangesEnabled;
    }

    setReviewDisplayMode(mode) {
        this.reviewDisplayMode = normalizeReviewDisplayMode(mode);
        this.render();
        return this.reviewDisplayMode;
    }

    selectComment(commentId) {
        const marker = this.commentOverlay.select(commentId);
        this.selectedCommentId = String(commentId || '');
        if (marker) {
            const offset = Math.max(0, Number(marker.startOffset || 0) || 0);
            this.selectionController.setSelection({
                anchor: { blockId: marker.blockId, offset },
                focus: { blockId: marker.blockId, offset },
            });
            this.scrollMarkerIntoView(marker);
        }

        return marker;
    }

    selectRevision(revisionId) {
        const marker = this.revisionOverlay.select(revisionId);
        this.selectedRevisionId = String(revisionId || '');
        if (marker) {
            this.selectionController.setSelection({
                anchor: { blockId: marker.blockId, offset: 0 },
                focus: { blockId: marker.blockId, offset: 0 },
            });
            this.scrollMarkerIntoView(marker);
        }

        return marker;
    }

    reviewRevision(revisionId, action) {
        const before = this.modelStore.getModel();
        const result = applyReviewDecision(before, revisionId, action);
        if (!result.changed) {
            return { changed: false, revisionId, action };
        }

        this.history.push?.({
            id: `canvas-review-${revisionId}-${Date.now()}`,
            kind: 'revision-review',
            before: { model: before, selection: this.selectionController.getSelection() },
            after: { model: result.model, selection: this.selectionController.getSelection() },
        });
        this.modelStore.setModel(result.model);
        this.recordLocalCollaborationChange(before, result.model, { result: { operation: 'reviewRevision' } });
        this.render({ structural: true });
        this.notifyExternalModelChanged({ result: { operation: 'reviewRevision' } });
        return { changed: true, revisionId, action };
    }

    reviewAllRevisions(action, filter = {}) {
        const before = this.modelStore.getModel();
        const result = applyReviewDecisionAll(before, action, filter);
        if (!result.changed) {
            return { changed: false, revisionIds: [] };
        }

        this.history.push?.({
            id: `canvas-review-all-${Date.now()}`,
            kind: 'revision-review-all',
            before: { model: before, selection: this.selectionController.getSelection() },
            after: { model: result.model, selection: this.selectionController.getSelection() },
        });
        this.modelStore.setModel(result.model);
        this.recordLocalCollaborationChange(before, result.model, { result: { operation: 'reviewAllRevisions' } });
        this.render({ structural: true });
        this.notifyExternalModelChanged({ result: { operation: 'reviewAllRevisions' } });
        return { changed: true, revisionIds: result.revisionIds || [] };
    }

    getOfflineState() {
        return {
            schemaVersion: 1,
            engine: 'CanvasDocumentEngine',
            modelVersion: this.modelStore.getVersion(),
            dirtyEpoch: this.modelStore.getVersion(),
            undoEpoch: this.history.snapshot().revision || this.history.snapshot().undoDepth || 0,
            model: this.modelStore.getModel(),
            collaboration: this.operationLog.snapshot(),
            selection: this.selectionController.getState(),
            history: this.history.snapshot(),
            presence: this.presenceOverlay.snapshot(),
        };
    }

    publishCommandDiagnostics(change) {
        this.publishFormattingDiagnostics(change);
    }

    publishFormattingDiagnostics(change = null) {
        const root = this.canvasStack.root;
        const state = this.commandRuntime.queryCommandState();
        root?.setAttribute?.('data-canvas-command-revision', String(state.revision || 0));
        if (change?.command) {
            root?.setAttribute?.('data-canvas-command-last', String(change.command.id || ''));
            root?.setAttribute?.('data-canvas-command-changed', String(change.command.changed === true));
            root?.setAttribute?.('data-canvas-command-view-changed', String(change.command.viewChanged === true));
        }
        root?.setAttribute?.('data-canvas-view-mode', String(state.view?.viewMode || 'print'));
        root?.setAttribute?.('data-canvas-view-toolbar-hidden', String(state.view?.toolbarHidden === true));
        root?.setAttribute?.('data-canvas-zoom-percent', String(Number(state.view?.zoomPercent || 100) || 100));
        root?.setAttribute?.('data-canvas-zoom-preset', String(state.view?.zoomPreset || 'custom'));
        root?.setAttribute?.('data-canvas-print-preview-active', String(state.view?.printPreview?.active === true));
        root?.setAttribute?.('data-canvas-paragraph-left-indent', String(state.paragraph?.leftIndent ?? 0));
        root?.setAttribute?.('data-canvas-paragraph-right-indent', String(state.paragraph?.rightIndent ?? 0));
        root?.setAttribute?.('data-canvas-paragraph-first-line-indent', String(state.paragraph?.firstLineIndent ?? 0));
        root?.setAttribute?.('data-canvas-paragraph-default-tab-width', String(state.paragraph?.defaultTabWidth ?? 36));
        root?.setAttribute?.('data-canvas-paragraph-tab-stops', JSON.stringify(state.paragraph?.tabStops || []));
        for (const [id, commandState] of Object.entries(state.commands || {})) {
            root?.setAttribute?.(`data-canvas-command-${id}-state`, commandState.state || 'inactive');
            if (commandState.value != null) {
                root?.setAttribute?.(`data-canvas-command-${id}-value`, String(commandState.value));
            } else {
                root?.removeAttribute?.(`data-canvas-command-${id}-value`);
            }
        }
    }

    announceCommand(commandId, result, searchState) {
        const normalized = String(commandId || '').replace(/[\s_-]/g, '').toLowerCase();
        if (['find', 'findnext', 'findprev', 'gotosearchresult'].includes(normalized)) {
            this.liveRegion.announceSearch(searchState);
            return;
        }

        if (normalized === 'save' || result?.result?.saved === true) {
            this.liveRegion.announceSaved();
            return;
        }

        if (result?.result?.mathSlot) {
            this.liveRegion.announceMathSlot(result.result.mathSlot);
        }
    }

    notifySelectionChanged(state) {
        const payload = buildMiniToolbarPayload(this.canvasStack, state);
        this.rulerOverlay?.update?.(this.lastLayout, this.modelStore.getModel(), {
            ...this.commandRuntime.getViewState(),
            selection: this.selectionController.getSelection(),
        });
        this.liveRegion.announceSelection(state);
        this.onSelectionChange?.(payload);
    }

    handleContextMenu(event) {
        const pageElement = findPageElement(event?.target);
        if (!pageElement) {
            return;
        }

        const point = viewportPointToPage(event, pageElement);
        if (!point) {
            return;
        }

        const hit = this.selectionController.hitTestPoint(point.pageIndex, point.x, point.y);
        const selection = this.selectionController.getState();
        const blockHit = blockAtPoint(this.lastRender?.selectionLayout, point.pageIndex, point.x, point.y);
        const diagnostic = hit
            ? this.proofingService.diagnosticAtPosition(hit.blockId, hit.offset) || this.proofingOverlay.diagnosticAtPoint(point.pageIndex, point.x, point.y)
            : this.proofingOverlay.diagnosticAtPoint(point.pageIndex, point.x, point.y);

        if (!hit && !blockHit && !diagnostic) {
            return;
        }

        event.preventDefault?.();
        event.stopPropagation?.();
        if (diagnostic?.blockId) {
            this.selectionController.setSelection({
                anchor: { blockId: diagnostic.blockId, offset: diagnostic.start },
                focus: { blockId: diagnostic.blockId, offset: diagnostic.end },
            });
        }

        const currentSelection = this.selectionController.getState();
        this.onContextMenu?.({
            x: Math.round(Number(event.clientX || 0) || 0),
            y: Math.round(Number(event.clientY || 0) || 0),
            pageIndex: point.pageIndex,
            blockId: diagnostic?.blockId || hit?.blockId || blockHit?.blockId || '',
            offset: diagnostic?.start ?? hit?.offset ?? 0,
            hasSelection: currentSelection.isCollapsed === false || diagnostic != null,
            inTable: String(blockHit?.type || '').toLowerCase() === 'table',
            tableId: String(blockHit?.type || '').toLowerCase() === 'table' ? blockHit.blockId : '',
            cellId: currentSelection.table?.cellId || '',
            imageBlockId: String(blockHit?.type || '').toLowerCase() === 'image' ? blockHit.blockId : '',
            misspelling: diagnostic ? {
                word: diagnostic.word || '',
                start: diagnostic.start || 0,
                end: diagnostic.end || 0,
                blockId: diagnostic.blockId || '',
                suggestions: diagnostic.suggestions || [],
                canApplyFix: diagnostic.canApplyFix !== false,
            } : null,
            selection: toWysiwygSelectionSnapshot(currentSelection),
        });
    }

    handleClick(event) {
        const annotation = closestElement(event?.target, '[data-comment-id], [data-revision-id]');
        if (annotation) {
            const commentId = annotation.getAttribute?.('data-comment-id') || '';
            const revisionId = annotation.getAttribute?.('data-revision-id') || '';
            if (commentId) {
                this.selectComment(commentId);
                this.liveRegion.announceComment(commentId);
                this.onAnnotationSelected?.({ kind: 'comment', id: commentId });
            } else if (revisionId) {
                this.selectRevision(revisionId);
                this.liveRegion.announceRevision(revisionId);
                this.onAnnotationSelected?.({ kind: 'revision', id: revisionId });
            }

            event.preventDefault?.();
            event.stopPropagation?.();
            return;
        }

        const tocEntry = closestElement(event?.target, '[data-canvas-toc-target-block-id]');
        if (!tocEntry) {
            return;
        }

        const targetBlockId = tocEntry.getAttribute?.('data-canvas-toc-target-block-id') || '';
        if (!targetBlockId) {
            return;
        }

        event.preventDefault?.();
        event.stopPropagation?.();
        const result = this.execCommand('gotoHeading', { blockId: targetBlockId });
        this.canvasStack.root?.setAttribute?.('data-canvas-toc-last-target-block-id', targetBlockId);
        this.canvasStack.root?.setAttribute?.('data-canvas-toc-last-navigation', String(result?.result?.selectionChanged === true || result?.selectionChanged === true));
    }

    handleWheel(event) {
        const startedAt = now();
        this.performanceMetrics.recordScrollFrame(0);
        this.canvasStack.root?.setAttribute?.('data-canvas-scroll-frame-count', String(this.performanceMetrics.snapshot().scroll.count));
        if (event?.ctrlKey !== true && event?.metaKey !== true) {
            return;
        }

        event.preventDefault?.();
        this.execCommand('ctrlWheelZoom', {
            deltaY: Number(event.deltaY || 0) || 0,
            scrollAnchor: {
                viewportTop: Number(this.host?.scrollTop || 0) || 0,
            },
        });
        this.performanceMetrics.recordScrollFrame(now() - startedAt);
    }

    handleScroll() {
        if (this.scrollRenderPending || !this.mounted) {
            return;
        }

        this.scrollRenderPending = true;
        const schedule = this.document?.defaultView?.requestAnimationFrame || globalThis.requestAnimationFrame || (callback => setTimeout(callback, 16));
        const startedAt = now();
        schedule(() => {
            this.scrollRenderPending = false;
            this.performanceMetrics.recordScrollFrame(now() - startedAt);
            // Scroll never changes the model/layout — paint visible pages from the cached plan.
            this.repaint({ forceRepaint: false });
        });
    }

    execCommand(commandId, argument = null) {
        this.flushPendingTextInputSideEffects();
        const annotationResult = this.execAnnotationCommand(commandId, argument);
        if (annotationResult) {
            return annotationResult;
        }

        const proofingResult = this.execProofingCommand(commandId, argument);
        if (proofingResult) {
            return proofingResult;
        }

        const result = this.commandRuntime.execCommand(commandId, argument);
        const searchState = this.commandRuntime.getSearchState();
        this.searchOverlay.update(searchState, this.lastRender);
        this.announceCommand(commandId, result, searchState);
        this.scrollActiveSearchIntoView(result);
        return result;
    }

    execAnnotationCommand(commandId, argument = null) {
        const normalized = String(commandId || '').replace(/[\s_-]/g, '').toLowerCase();
        if (normalized === 'trackchanges') {
            return {
                handled: true,
                commandId: normalized,
                result: { changed: false, enabled: this.setTrackChangesEnabled(argument === true || argument?.enabled === true, argument?.author || null) },
                formattingState: this.commandRuntime.queryCommandState(),
                history: this.history.snapshot(),
            };
        }

        if (normalized === 'setreviewdisplaymode' || normalized === 'reviewdisplaymode') {
            return {
                handled: true,
                commandId: normalized,
                result: { changed: false, reviewDisplayMode: this.setReviewDisplayMode(argument?.mode || argument) },
                formattingState: this.commandRuntime.queryCommandState(),
                history: this.history.snapshot(),
            };
        }

        if (normalized === 'gotocomment' || normalized === 'selectcomment') {
            const marker = this.selectComment(argument?.commentId || argument?.id || argument);
            return {
                handled: true,
                commandId: normalized,
                result: {
                    changed: false,
                    selected: marker != null,
                    commentId: marker?.commentId || '',
                    blockId: marker?.blockId || '',
                    offset: Math.max(0, Number(marker?.startOffset || 0) || 0),
                },
                formattingState: this.commandRuntime.queryCommandState(),
                history: this.history.snapshot(),
            };
        }

        if (normalized === 'addcomment' || normalized === 'upsertcomment') {
            const before = this.modelStore.getModel();
            const result = addCommentToCanvasModel(before, this.selectionController.getSelection(), argument || {});
            if (result.changed) {
                this.history.push?.({
                    id: `canvas-comment-add-${result.commentId}-${Date.now()}`,
                    kind: 'comment',
                    before: { model: before, selection: this.selectionController.getSelection() },
                    after: { model: result.model, selection: result.selection },
                });
                this.modelStore.setModel(result.model);
                this.recordLocalCollaborationChange(before, result.model, { result });
                if (result.selection) {
                    this.selectionController.setSelection(result.selection);
                }
                this.selectedCommentId = result.commentId || this.selectedCommentId || '';
                this.render({ dirtyBlockIds: result.dirtyBlockIds || [], structural: false });
                this.publishCommandDiagnostics({ result, command: { id: normalized, changed: true } });
                this.notifyExternalModelChanged({ result });
            }

            return {
                handled: true,
                commandId: normalized,
                result,
                formattingState: this.commandRuntime.queryCommandState(),
                history: this.history.snapshot(),
            };
        }

        if (normalized === 'deletecomment' || normalized === 'removecomment') {
            const before = this.modelStore.getModel();
            const result = deleteCommentFromCanvasModel(before, argument || {});
            if (result.changed) {
                this.history.push?.({
                    id: `canvas-comment-delete-${result.commentId}-${Date.now()}`,
                    kind: 'comment',
                    before: { model: before, selection: this.selectionController.getSelection() },
                    after: { model: result.model, selection: this.selectionController.getSelection() },
                });
                this.modelStore.setModel(result.model);
                this.recordLocalCollaborationChange(before, result.model, { result });
                if (this.selectedCommentId === result.commentId) {
                    this.selectedCommentId = '';
                }
                this.render({ dirtyBlockIds: result.dirtyBlockIds || [], structural: false });
                this.publishCommandDiagnostics({ result, command: { id: normalized, changed: true } });
                this.notifyExternalModelChanged({ result });
            }

            return {
                handled: true,
                commandId: normalized,
                result,
                formattingState: this.commandRuntime.queryCommandState(),
                history: this.history.snapshot(),
            };
        }

        if (normalized === 'gotorevision' || normalized === 'selectrevision') {
            const marker = this.selectRevision(argument?.revisionId || argument?.id || argument);
            return {
                handled: true,
                commandId: normalized,
                result: { changed: false, selected: marker != null, revisionId: marker?.revisionId || '' },
                formattingState: this.commandRuntime.queryCommandState(),
                history: this.history.snapshot(),
            };
        }

        if (normalized === 'acceptrevision' || normalized === 'rejectrevision') {
            const action = normalized === 'acceptrevision' ? 'accepted' : 'rejected';
            const result = this.reviewRevision(argument?.revisionId || argument?.id || argument, action);
            return {
                handled: true,
                commandId: normalized,
                result,
                formattingState: this.commandRuntime.queryCommandState(),
                history: this.history.snapshot(),
            };
        }

        if (normalized === 'acceptallrevisions' || normalized === 'rejectallrevisions') {
            const action = normalized === 'acceptallrevisions' ? 'accepted' : 'rejected';
            const result = this.reviewAllRevisions(action, argument || {});
            return {
                handled: true,
                commandId: normalized,
                result,
                formattingState: this.commandRuntime.queryCommandState(),
                history: this.history.snapshot(),
            };
        }

        return null;
    }

    execProofingCommand(commandId, argument = null) {
        const normalized = String(commandId || '').replace(/[\s_-]/g, '').toLowerCase();
        let snapshot = null;
        if (normalized === 'ignoreonce') {
            snapshot = this.proofingService.ignoreOnce(argument);
        } else if (normalized === 'ignoreall') {
            snapshot = this.proofingService.ignoreAll(argument?.word || argument);
        } else if (normalized === 'addtodictionary') {
            snapshot = this.proofingService.addToDictionary(argument?.word || argument);
        } else {
            return null;
        }

        this.proofingOverlay.update(snapshot, this.lastRender);
        return {
            handled: true,
            commandId: normalized,
            result: {
                changed: false,
                proofingDiagnosticCount: snapshot.diagnosticCount,
            },
            formattingState: this.commandRuntime.queryCommandState(),
            history: this.history.snapshot(),
        };
    }

    queryCommand(commandId = null) {
        return this.commandRuntime.queryCommand(commandId);
    }

    openUrl(href) {
        const url = String(href || '');
        if (!url) {
            return false;
        }

        this.canvasStack.root?.setAttribute?.('data-canvas-last-opened-link', url);
        const view = this.document?.defaultView || globalThis;
        if (typeof view?.open === 'function') {
            view.open(url, '_blank', 'noopener,noreferrer');
            return true;
        }

        return false;
    }

    readViewport() {
        const explicit = this.viewport || {};
        const view = this.document?.defaultView || globalThis;
        const bounds = this.host?.getBoundingClientRect?.() || null;
        const rootBounds = this.canvasStack.root?.getBoundingClientRect?.() || null;
        const hostHasOwnScroll = Number(this.host?.scrollHeight || 0) > Number(this.host?.clientHeight || 0);
        const inferredScrollTop = rootBounds
            ? Math.max(0, -Number(rootBounds.top || 0))
            : Number(this.host?.scrollTop ?? view?.scrollY ?? this.document?.documentElement?.scrollTop ?? 0) || 0;
        return {
            scrollTop: Number(explicit.scrollTop ?? (hostHasOwnScroll ? this.host?.scrollTop : inferredScrollTop) ?? 0) || 0,
            height: Number(explicit.height ?? (hostHasOwnScroll ? this.host?.clientHeight : view?.innerHeight) ?? bounds?.height ?? view?.innerHeight ?? globalThis.innerHeight ?? 900) || 900,
            width: Number(explicit.width ?? this.host?.clientWidth ?? bounds?.width ?? view?.innerWidth ?? globalThis.innerWidth ?? 1280) || 1280,
            overscanPages: Number(explicit.overscanPages ?? 1) || 1,
        };
    }

    publishPerformanceDiagnostics(stats, render) {
        const root = this.canvasStack.root;
        const typing = stats.typing || {};
        const scroll = stats.scroll || {};
        const recalc = this.recalcInfo.snapshot();
        const currentFirstDirtyIndex = Number(recalc.firstDirtyBlockIndex ?? -1);
        const lastFirstDirtyIndex = Number(recalc.lastFirstDirtyBlockIndex ?? -1);
        const diagnosticFirstDirtyIndex = currentFirstDirtyIndex >= 0 ? currentFirstDirtyIndex : lastFirstDirtyIndex;
        const measurementStats = render?.measurementStats || {};
        root?.setAttribute?.('data-canvas-first-paint-ms', String(stats.firstPaintMs || 0));
        root?.setAttribute?.('data-canvas-render-count', String(stats.renderCount || 0));
        root?.setAttribute?.('data-canvas-render-p50-ms', String(stats.renderP50Ms || 0));
        root?.setAttribute?.('data-canvas-render-p95-ms', String(stats.renderP95Ms || 0));
        root?.setAttribute?.('data-canvas-typing-latency-p50-ms', String(typing.p50Ms || 0));
        root?.setAttribute?.('data-canvas-typing-latency-p95-ms', String(typing.p95Ms || 0));
        root?.setAttribute?.('data-canvas-typing-latency-count', String(typing.count || 0));
        root?.setAttribute?.('data-canvas-scroll-p50-ms', String(scroll.p50Ms || 0));
        root?.setAttribute?.('data-canvas-scroll-p95-ms', String(scroll.p95Ms || 0));
        root?.setAttribute?.('data-canvas-scroll-frame-count', String(scroll.count || 0));
        root?.setAttribute?.('data-canvas-tile-cache-entry-count', String(render?.tileCache?.entryCount || 0));
        root?.setAttribute?.('data-canvas-tile-cache-hit-count', String(render?.tileCache?.hits || 0));
        root?.setAttribute?.('data-canvas-measure-cache-size', String(measurementStats.MeasureCacheSize || 0));
        root?.setAttribute?.('data-canvas-measure-cache-eviction-count', String(measurementStats.MeasureEvictions || 0));
        root?.setAttribute?.('data-canvas-recalc-dirty-block-count', String(recalc.dirtyBlockCount || 0));
        root?.setAttribute?.('data-canvas-recalc-first-dirty-block-index', String(diagnosticFirstDirtyIndex));
        root?.setAttribute?.('data-canvas-recalc-last-first-dirty-block-index', String(lastFirstDirtyIndex));
        root?.setAttribute?.('data-canvas-recalc-idle-count', String(recalc.idleReconciliationCount || 0));
    }

    getViewportMetrics() {
        const layoutPage = Array.isArray(this.lastLayout?.pages) && this.lastLayout.pages.length > 0
            ? this.lastLayout.pages[0]
            : null;
        const hostBounds = this.host?.getBoundingClientRect?.() || null;
        return {
            pageWidth: Number(layoutPage?.width || 794) || 794,
            pageHeight: Number(layoutPage?.height || 1123) || 1123,
            viewportWidth: Number(hostBounds?.width || this.document?.defaultView?.innerWidth || globalThis.innerWidth || 1280) || 1280,
            viewportHeight: Number(hostBounds?.height || this.document?.defaultView?.innerHeight || globalThis.innerHeight || 900) || 900,
            pageGap: 24,
            paddingInline: 48,
            paddingBlock: 48,
        };
    }

    scrollActiveSearchIntoView(commandResult) {
        const normalized = String(commandResult?.commandId || '').replace(/[\s_-]/g, '').toLowerCase();
        if (!['find', 'findnext', 'findprev', 'gotosearchresult'].includes(normalized)) {
            return;
        }

        const state = this.commandRuntime.getSearchState();
        const match = state.matches?.[state.activeIndex] || null;
        if (!match) {
            return;
        }

        const rect = firstRectForMatch(this.lastRender?.selectionLayout?.textRects || [], match);
        const page = this.canvasStack.pages.get(String(rect?.pageIndex ?? 0));
        page?.pageElement?.scrollIntoView?.({ block: 'nearest', inline: 'nearest' });
    }

    scrollMarkerIntoView(marker) {
        const page = this.canvasStack.pages.get(String(marker?.pageIndex ?? 0));
        page?.pageElement?.scrollIntoView?.({ block: 'nearest', inline: 'nearest' });
    }
}

function affectedBlockIds(batch) {
    return operationsOf(batch)
        .map(operation => operation?.target?.blockId || operation?.Target?.BlockId)
        .filter(Boolean)
        .map(String);
}

function hasStructuralOperation(batch) {
    return operationsOf(batch).some(operation => {
        const type = String(operation?.type || operation?.Type || '').toLowerCase();
        return type === 'insertblock' || type === 'deleteblock' || type === 'moveblock' || type === 'updateblock';
    });
}

function operationsOf(batch) {
    return Array.isArray(batch?.batch?.operations)
        ? batch.batch.operations
        : Array.isArray(batch?.operations)
            ? batch.operations
            : [];
}

function firstRectForMatch(textRects, match) {
    const blockId = String(match?.blockId || '');
    const start = Number(match?.start || 0) || 0;
    const end = Number(match?.end || 0) || 0;
    return (textRects || []).find(rect =>
        String(rect.blockId || '') === blockId
        && Number(rect.end || 0) > start
        && Number(rect.start || 0) < end) || null;
}

function buildMiniToolbarPayload(canvasStack, selectionState) {
    const selection = toWysiwygSelectionSnapshot(selectionState);
    if (selectionState?.object?.rect) {
        const object = selectionState.object;
        const page = canvasStack?.pages?.get?.(String(object.pageIndex || 0));
        const pageBounds = page?.pageElement?.getBoundingClientRect?.();
        if (pageBounds) {
            const width = 320;
            const height = 44;
            const viewportWidth = Number(globalThis.innerWidth || pageBounds.width || 1024) || 1024;
            const centerX = pageBounds.left + (Number(object.rect.x || 0) || 0) + Math.max(1, Number(object.rect.width || 0) || 0) / 2;
            const top = Math.max(8, pageBounds.top + (Number(object.rect.y || 0) || 0) - height - 10);
            return {
                isVisible: true,
                left: Math.max(8, Math.min(viewportWidth - width - 8, centerX - width / 2)),
                top,
                width,
                height,
                viewportWidth,
                viewportHeight: Number(globalThis.innerHeight || pageBounds.height || 768) || 768,
                reason: 'canvas-object-selection',
                selection,
            };
        }
    }

    if (!selection || selection.isCollapsed) {
        return {
            isVisible: false,
            reason: 'canvas-selection-collapsed',
            selection,
        };
    }

    const rect = selectionState?.boundingRect;
    const page = rect ? canvasStack?.pages?.get?.(String(rect.pageIndex || 0)) : null;
    const pageBounds = page?.pageElement?.getBoundingClientRect?.();
    if (!rect || !pageBounds) {
        return {
            isVisible: false,
            reason: 'canvas-selection-unplaced',
            selection,
        };
    }

    const width = 360;
    const height = 44;
    const viewportWidth = Number(globalThis.innerWidth || pageBounds.width || 1024) || 1024;
    const viewportHeight = Number(globalThis.innerHeight || pageBounds.height || 768) || 768;
    const centerX = pageBounds.left + (Number(rect.x || 0) || 0) + Math.max(1, Number(rect.width || 0) || 0) / 2;
    const top = Math.max(8, pageBounds.top + (Number(rect.y || 0) || 0) - height - 10);
    return {
        isVisible: true,
        left: Math.max(8, Math.min(viewportWidth - width - 8, centerX - width / 2)),
        top,
        width,
        height,
        viewportWidth,
        viewportHeight,
        reason: 'canvas-selection-range',
        selection,
    };
}

function toWysiwygSelectionSnapshot(selectionState) {
    if (!selectionState?.anchor || !selectionState?.focus) {
        return null;
    }

    const anchor = selectionState.anchor;
    const focus = selectionState.focus;
    const inTable = selectionState.table?.inTable === true;
    const object = selectionState.object || null;
    return {
        region: inTable ? 'TableCell' : 'Body',
        selectionMode: object ? 'Object' : 'Text',
        pageIndex: Number(selectionState.pageIndex || 0) || 0,
        anchorBlockId: String(anchor.blockId || ''),
        anchorOffset: Number(anchor.offset || 0) || 0,
        anchorBlockOffset: Number(anchor.offset || 0) || 0,
        focusBlockId: String(focus.blockId || ''),
        focusOffset: Number(focus.offset || 0) || 0,
        focusBlockOffset: Number(focus.offset || 0) || 0,
        isCollapsed: selectionState.isCollapsed !== false,
        direction: 'forward',
        textSelection: {
            region: inTable ? 'TableCell' : 'Body',
            anchorBlockId: String(anchor.blockId || ''),
            anchorOffset: Number(anchor.offset || 0) || 0,
            focusBlockId: String(focus.blockId || ''),
            focusOffset: Number(focus.offset || 0) || 0,
            isCollapsed: selectionState.isCollapsed !== false,
            direction: 'forward',
        },
        selectionToken: `canvas:${anchor.blockId}:${anchor.offset}:${focus.blockId}:${focus.offset}`,
        stableSelectionToken: `canvas:${anchor.blockId}:${anchor.offset}:${focus.blockId}:${focus.offset}`,
        tableId: selectionState.table?.tableId || '',
        cellId: selectionState.table?.cellId || '',
        activeTableId: selectionState.table?.tableId || '',
        activeTableCellId: selectionState.table?.cellId || '',
        rowIndex: Number(selectionState.table?.rowIndex || 0) || 0,
        cellIndex: Number(selectionState.table?.cellIndex || 0) || 0,
        objectSelection: object ? {
            objectId: object.objectId || '',
            blockId: object.blockId || '',
            runId: object.runId || '',
            role: object.role || '',
            wrapMode: object.wrapMode || '',
            width: Number(object.width || object.rect?.width || 0) || 0,
            height: Number(object.height || object.rect?.height || 0) || 0,
        } : null,
    };
}

function findPageElement(target) {
    if (target?.closest) {
        return target.closest('[data-testid="document-canvas-page"]');
    }

    let node = target;
    while (node) {
        if (node.getAttribute?.('data-testid') === 'document-canvas-page') {
            return node;
        }

        node = node.parentNode;
    }

    return null;
}

function closestElement(target, selector) {
    if (target?.closest) {
        return target.closest(selector);
    }

    let node = target;
    while (node) {
        if (typeof node.matches === 'function' && node.matches(selector)) {
            return node;
        }

        node = node.parentNode;
    }

    return null;
}

function viewportPointToPage(event, pageElement) {
    const bounds = pageElement?.getBoundingClientRect?.();
    if (!bounds) {
        return null;
    }

    const scale = Math.max(0.01, Number(pageElement.getAttribute?.('data-canvas-page-zoom-scale') || 1) || 1);
    return {
        pageIndex: Number(pageElement.getAttribute?.('data-page-index') || 0) || 0,
        x: ((Number(event.clientX || 0) || 0) - bounds.left) / scale,
        y: ((Number(event.clientY || 0) || 0) - bounds.top) / scale,
    };
}

function now() {
    return Number(globalThis.performance?.now?.() || Date.now()) || 0;
}

function roundMetric(value) {
    return Math.round((Number(value) || 0) * 100) / 100;
}

function scheduleFrame(document, callback) {
    const view = document?.defaultView || globalThis.window || globalThis;
    if (typeof view?.requestAnimationFrame === 'function') {
        view.requestAnimationFrame(callback);
        return;
    }

    setTimeout(callback, 16);
}

function blockAtPoint(selectionLayout, pageIndex, x, y) {
    for (const block of selectionLayout?.blocks || []) {
        const rect = block?.rect;
        if (!rect || Number(block?.pageIndex || 0) !== Number(pageIndex || 0)) {
            continue;
        }

        const left = Number(rect.x || 0) || 0;
        const top = Number(rect.y || 0) || 0;
        const width = Math.max(1, Number(rect.width || 0) || 0);
        const height = Math.max(1, Number(rect.height || 0) || 0);
        if (x >= left && x <= left + width && y >= top && y <= top + height) {
            return {
                blockId: String(block.blockId || block.id || ''),
                type: String(block.type || ''),
            };
        }
    }

    return null;
}

function createInteropBridge(engine) {
    if (!engine || typeof engine.getSnapshot !== 'function') {
        throw new Error('CanvasDocumentEngine interop bridge requires an engine instance.');
    }

    return {
        ready() {
            return engine.getSnapshot().mounted;
        },
        snapshot() {
            return engine.getSnapshot();
        },
        focus() {
            return engine.focusInput();
        },
        destroy() {
            engine.destroy();
        },
    };
}
