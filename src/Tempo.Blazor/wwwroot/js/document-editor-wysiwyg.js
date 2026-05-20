/**
 * Tempo Blazor — WYSIWYG Document Editor JS Engine
 *
 * Owns the live editing surface, input pipeline, selection mapping,
 * DOM patching, and command bridge back to Blazor.
 *
 * Architecture:
 *   - Primární editace: beforeinput, input, paste, composition*, selectionchange
 *   - MutationObserver: guard / fallback only (not primary sync)
 *   - No document.execCommand
 *   - No per-character Blazor re-render
 */
window.tmDocumentWysiwyg = (function () {
    'use strict';

    /** @type {Map<string, WysiwygInstance>} */
    const _instances = new Map();
    let _instanceCounter = 0;

    function _isImageDebugEnabled() {
        try {
            if (window.localStorage && window.localStorage.getItem('tmDocumentEditorImageDebug') === '1') {
                return true;
            }
        } catch {}

        try {
            return new URLSearchParams(window.location.search).get('tmImageDebug') === '1';
        } catch {
            return false;
        }
    }

    function _debugRect(rect) {
        if (!rect) return null;
        return {
            left: Math.round(rect.left),
            top: Math.round(rect.top),
            right: Math.round(rect.right),
            bottom: Math.round(rect.bottom),
            width: Math.round(rect.width),
            height: Math.round(rect.height)
        };
    }

    function _debugElementLabel(element) {
        if (!element) return null;
        var parts = [element.tagName ? element.tagName.toLowerCase() : 'node'];
        var blockId = element.getAttribute && element.getAttribute('data-block-id');
        var inlineId = element.getAttribute && element.getAttribute('data-inline-id');
        var sidecarFor = element.getAttribute && element.getAttribute('data-wrap-sidecar-for');
        if (blockId) parts.push('block=' + blockId);
        if (inlineId) parts.push('inline=' + inlineId);
        if (sidecarFor) parts.push('sidecarFor=' + sidecarFor);
        if (element.className && typeof element.className === 'string') {
            parts.push('.' + element.className.trim().replace(/\s+/g, '.'));
        }
        return parts.join(' ');
    }

    function _debugSelectionSummary() {
        try {
            var sel = window.getSelection && window.getSelection();
            if (!sel || sel.rangeCount === 0) return { present: false };
            var anchor = sel.anchorNode && (sel.anchorNode.nodeType === Node.ELEMENT_NODE ? sel.anchorNode : sel.anchorNode.parentElement);
            var focus = sel.focusNode && (sel.focusNode.nodeType === Node.ELEMENT_NODE ? sel.focusNode : sel.focusNode.parentElement);
            return {
                present: true,
                collapsed: sel.isCollapsed,
                anchor: _debugElementLabel(anchor),
                focus: _debugElementLabel(focus),
                anchorOffset: sel.anchorOffset,
                focusOffset: sel.focusOffset
            };
        } catch (err) {
            return { present: false, error: err && err.message ? err.message : String(err) };
        }
    }

    function _debugImage(inst, eventName, data) {
        if (!_isImageDebugEnabled()) return;
        try {
            var payload = Object.assign({
                instanceId: inst && inst.id,
                selection: _debugSelectionSummary()
            }, data || {});
            console.info('[TmDocumentEditor:image]', eventName, payload);
            try {
                console.info('[TmDocumentEditor:image-json]', JSON.stringify(Object.assign({ eventName: eventName }, payload)));
            } catch {}
        } catch {}
    }

    function _debugImageFigures(inst) {
        if (!inst || !inst.root) return [];
        return Array.from(inst.root.querySelectorAll('figure.tm-wysiwyg-image')).map(function (figure, index) {
            return {
                index: index,
                label: _debugElementLabel(figure),
                inline: figure.getAttribute('data-floating-inline'),
                wrapMode: figure.getAttribute('data-wrap-mode'),
                horizontalPosition: figure.getAttribute('data-horizontal-position'),
                classes: figure.className || '',
                figureRect: _debugRect(figure.getBoundingClientRect()),
                visualRect: _debugRect(_getImagePrimaryVisualRect(figure))
            };
        });
    }

    /**
     * @typedef {Object} WysiwygInstance
     * @property {string} id
     * @property {HTMLElement} root
     * @property {Object} options
     * @property {Object} dotNetRef
     * @property {boolean} readOnly
     * @property {boolean} disposed
     * @property {MutationObserver|null} mutationObserver
     * @property {number|null} typingTimer
     * @property {string|null} currentTransactionId
     */

    /**
     * Creates a new WYSIWYG editor instance.
     * @param {HTMLElement} rootElement
     * @param {Object} options
     * @param {Object} dotNetRef — DotNetObjectReference from Blazor
     * @returns {string} instanceId
     */
    function create(rootElement, options, dotNetRef) {
        if (!rootElement) {
            throw new Error('tmDocumentWysiwyg.create: rootElement is required.');
        }
        if (!dotNetRef) {
            throw new Error('tmDocumentWysiwyg.create: dotNetRef is required.');
        }

        const opts = options || {};
        const instanceId = opts.instanceId || ('tmw-' + (++_instanceCounter));

        // Dispose any existing instance on the same root.
        const existing = Array.from(_instances.values()).find(function (i) { return i.root === rootElement; });
        if (existing) {
            dispose(existing.id);
        }

        /** @type {WysiwygInstance} */
        const inst = {
            id: instanceId,
            root: rootElement,
            options: opts,
            dotNetRef: dotNetRef,
            readOnly: !!opts.readOnly,
            trackChangesEnabled: !!(opts.trackChangesEnabled ?? opts.TrackChangesEnabled),
            reviewDisplayMode: _normalizeReviewDisplayMode(opts.reviewDisplayMode ?? opts.ReviewDisplayMode ?? 'AllMarkup'),
            disposed: false,
            mutationObserver: null,
            typingTimer: null,
            currentTransactionId: null,
            compositionActive: false,
            compositionText: '',
            compositionUpdateCount: 0,
            suppressInputUntil: 0,
            suppressInputType: null,
            acceptingNativeInput: false,
            nativeInputTimer: null,
            pendingNativeInputSelection: null,
            pendingInputPatch: null,
            pendingInputPatchTimer: null,
            pendingInputPatchMaxTimer: null,
            pendingTypingMarks: {},
            pendingSelectionSnapshot: null,
            pendingSelectionTimer: null,
            pendingLocalSnapshotSkips: 0,
            queuedRemoteBatches: [],
            pendingCollaborationTransactions: [],
            remoteCursorLayer: null,
            remoteCursorElements: new Map(),
            markerStore: new Map(),
            lastSelectionSnapshot: null,
            lastBodySelectionSnapshot: null,
            lastTextSelectionSnapshot: null,
            miniToolbarSuppressHideUntil: 0,
            lastInputType: null,
            lastInputDataLength: 0,
            jsOwnedInputCount: 0,
            nativeInputCount: 0,
            lastInputOperationId: null,
            lastPatchType: null,
            lastPatchId: null,
            lastPatchTransactionId: null,
            lastPatchAt: null,
            measureCache: new Map(),
            measureStats: { count: 0, cacheHits: 0, invalidations: 0 },
            renderStats: {
                snapshotApplies: 0,
                fullRenders: 0,
                incrementalOperations: 0,
                remoteOperations: 0,
                remoteBatches: 0,
                lastRenderReason: ''
            },
            inputStats: {
                operationCount: 0,
                longOperationCount: 0,
                totalLatencyMs: 0,
                totalOperationMs: 0,
                maxLatencyMs: 0,
                maxOperationMs: 0,
                lastLatencyMs: 0,
                lastOperationMs: 0,
                lastInputType: '',
                lastEventType: ''
            },
            performanceStats: _createPerformanceStats(),
            runtimeDocument: null,
            runtimeSelection: null,
            renderPlan: null,
            commandTransactionCounter: 0,
            commandOperationCounter: 0,
            commandUndoStack: [],
            commandRedoStack: [],
            lastCommandTransaction: null,
            pendingUndoTransaction: null,
            runtimeUndoEpoch: 0,
            lastUndoState: null,
            isDirty: false,
            dirtyEpoch: 0,
            savedEpoch: 0,
            lastDirtyReason: '',
            lastSavedMarker: null,
            lastSavedAt: null,
            lastDirtyState: null,
            runtimeRevisions: [],
            lastRevisionStateJson: '',
            runtimeComments: [],
            lastCommentStateJson: '',
            commentRailAlignmentFrame: null,
            lastCommittedHtml: '',
            virtualPages: [],
            virtualState: null,
            virtualSelectionSnapshot: null,
            virtualizationScrollTimer: null,
            hasRenderedDocument: false,
            appliedOperationIds: new Set(),
            inlineRevisionPopover: null,
            selectedImageFigure: null,
            imageContextMenu: null,
            miniToolbarVisible: false,
            miniToolbarRequestKey: null,
            imageDragTransaction: null,
            selectedPageBreakId: null,
            showNonPrintingCharacters: false,
        };

        _instances.set(instanceId, inst);

        _attachEventListeners(inst);
        _attachMutationObserver(inst);
        _notifyReady(inst);

        return instanceId;
    }

    /**
     * Disposes an instance and cleans up all resources.
     * @param {string} instanceId
     */
    function dispose(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst) return;

        inst.disposed = true;

        if (inst.typingTimer) {
            clearTimeout(inst.typingTimer);
            inst.typingTimer = null;
        }

        if (inst.nativeInputTimer) {
            clearTimeout(inst.nativeInputTimer);
            inst.nativeInputTimer = null;
        }
        if (inst.pendingInputPatchTimer) {
            clearTimeout(inst.pendingInputPatchTimer);
            inst.pendingInputPatchTimer = null;
        }
        if (inst.pendingInputPatchMaxTimer) {
            clearTimeout(inst.pendingInputPatchMaxTimer);
            inst.pendingInputPatchMaxTimer = null;
        }
        if (inst.pendingSelectionTimer) {
            clearTimeout(inst.pendingSelectionTimer);
            inst.pendingSelectionTimer = null;
        }

        if (inst._remoteQueueFlushTimer) {
            clearTimeout(inst._remoteQueueFlushTimer);
            inst._remoteQueueFlushTimer = null;
        }
        inst.queuedRemoteBatches = [];

        if (inst.virtualizationScrollTimer) {
            clearTimeout(inst.virtualizationScrollTimer);
            inst.virtualizationScrollTimer = null;
        }
        if (inst.commentRailAlignmentFrame && window.cancelAnimationFrame) {
            window.cancelAnimationFrame(inst.commentRailAlignmentFrame);
            inst.commentRailAlignmentFrame = null;
        }

        _hideInlineRevisionReview(inst);
        _hideImageContextMenu(inst);
        _hideMiniToolbar(inst, true);

        _detachEventListeners(inst);

        if (inst.mutationObserver) {
            inst.mutationObserver.disconnect();
            inst.mutationObserver = null;
        }

        _instances.delete(instanceId);
    }

    /**
     * Returns whether an instance exists and is active.
     * @param {string} instanceId
     * @returns {boolean}
     */
    function isAlive(instanceId) {
        const inst = _instances.get(instanceId);
        return !!inst && !inst.disposed;
    }

    // ── Event listeners ──────────────────────────────────────────────────────

    function _attachEventListeners(inst) {
        inst._handleVirtualScroll = function () {
            _updatePageWidthFitZoom(inst);
            _scheduleVirtualizationRefresh(inst);
            _repositionVisibleFloatingLayers(inst);
            _scheduleCommentRailAlignment(inst);
            _notifyPageMetrics(inst);
            _notifyActiveHeading(inst);
        };
        inst.root.addEventListener('scroll', inst._handleVirtualScroll, { passive: true });
        document.addEventListener('scroll', inst._handleVirtualScroll, { passive: true, capture: true });
        window.addEventListener('scroll', inst._handleVirtualScroll, { passive: true });
        window.addEventListener('resize', inst._handleVirtualScroll);

        if (inst.readOnly) return;

        inst._handleBeforeInput = function (e) { _onBeforeInput(inst, e); };
        inst._handleInput = function (e) { _onInput(inst, e); };
        inst._handlePaste = function (e) { _onPaste(inst, e); };
        inst._handleCopy = function (e) { _onCopy(inst, e); };
        inst._handleCompositionStart = function () {
            inst.compositionActive = true;
            inst.compositionText = '';
            inst.compositionUpdateCount = 0;
        };
        inst._handleCompositionUpdate = function (e) {
            inst.compositionText = e && e.data ? String(e.data) : '';
            inst.compositionUpdateCount++;
        };
        inst._handleCompositionEnd = function (e) {
            inst.compositionActive = false;
            inst.compositionText = e && e.data ? String(e.data) : inst.compositionText;
            _onInput(inst, e);
            _scheduleRemoteQueueFlush(inst);
        };
        inst._handleSelectionChange = function () { _onSelectionChange(inst); };
        inst._handleKeyDown = function (e) { _onKeyDown(inst, e); };
        inst._handleDocumentKeyDown = function (e) { _onDocumentKeyDown(inst, e); };
        inst._handleDocumentPointerDown = function (e) { _onDocumentPointerDown(inst, e); };
        inst._handlePointerDown = function (e) { _onFloatingImagePointerDown(inst, e); };
        inst._handleTablePointerDown = function (e) { _onTablePointerDown(inst, e); };
        inst._handlePointerUp = function (e) { _onRootPointerUp(inst, e); };
        inst._handleClick = function (e) { _onRootClick(inst, e); };
        inst._handleDoubleClick = function (e) { _onRootDoubleClick(inst, e); };
        inst._handleContextMenu = function (e) { _onRootContextMenu(inst, e); };
        inst._handleDragOver = function (e) { _onRootDragOver(inst, e); };
        inst._handleDragLeave = function (e) { _onRootDragLeave(inst, e); };
        inst._handleDrop = function (e) { _onRootDrop(inst, e); };

        inst.root.addEventListener('beforeinput', inst._handleBeforeInput, true);
        inst.root.addEventListener('input', inst._handleInput, true);
        inst.root.addEventListener('paste', inst._handlePaste, true);
        inst.root.addEventListener('copy', inst._handleCopy, true);
        inst.root.addEventListener('compositionstart', inst._handleCompositionStart, true);
        inst.root.addEventListener('compositionupdate', inst._handleCompositionUpdate, true);
        inst.root.addEventListener('compositionend', inst._handleCompositionEnd, true);
        document.addEventListener('selectionchange', inst._handleSelectionChange);
        document.addEventListener('keydown', inst._handleDocumentKeyDown, true);
        document.addEventListener('pointerdown', inst._handleDocumentPointerDown, true);
        inst.root.addEventListener('keydown', inst._handleKeyDown, true);
        inst.root.addEventListener('pointerdown', inst._handlePointerDown, true);
        inst.root.addEventListener('pointerdown', inst._handleTablePointerDown, true);
        inst.root.addEventListener('pointerup', inst._handlePointerUp, true);
        inst.root.addEventListener('click', inst._handleClick, true);
        inst.root.addEventListener('dblclick', inst._handleDoubleClick, true);
        inst.root.addEventListener('contextmenu', inst._handleContextMenu, true);
        inst.root.addEventListener('dragover', inst._handleDragOver, true);
        inst.root.addEventListener('dragleave', inst._handleDragLeave, true);
        inst.root.addEventListener('drop', inst._handleDrop, true);
    }

    function _detachEventListeners(inst) {
        if (inst._handleBeforeInput) {
            inst.root.removeEventListener('beforeinput', inst._handleBeforeInput, true);
        }
        if (inst._handleInput) {
            inst.root.removeEventListener('input', inst._handleInput, true);
        }
        if (inst._handlePaste) {
            inst.root.removeEventListener('paste', inst._handlePaste, true);
        }
        if (inst._handleCopy) {
            inst.root.removeEventListener('copy', inst._handleCopy, true);
        }
        if (inst._handleCompositionStart) {
            inst.root.removeEventListener('compositionstart', inst._handleCompositionStart, true);
        }
        if (inst._handleCompositionUpdate) {
            inst.root.removeEventListener('compositionupdate', inst._handleCompositionUpdate, true);
        }
        if (inst._handleCompositionEnd) {
            inst.root.removeEventListener('compositionend', inst._handleCompositionEnd, true);
        }
        if (inst._handleSelectionChange) {
            document.removeEventListener('selectionchange', inst._handleSelectionChange);
        }
        if (inst._handleDocumentKeyDown) {
            document.removeEventListener('keydown', inst._handleDocumentKeyDown, true);
        }
        if (inst._handleDocumentPointerDown) {
            document.removeEventListener('pointerdown', inst._handleDocumentPointerDown, true);
        }
        if (inst._handleKeyDown) {
            inst.root.removeEventListener('keydown', inst._handleKeyDown, true);
        }
        if (inst._handlePointerDown) {
            inst.root.removeEventListener('pointerdown', inst._handlePointerDown, true);
        }
        if (inst._handleTablePointerDown) {
            inst.root.removeEventListener('pointerdown', inst._handleTablePointerDown, true);
        }
        if (inst._handlePointerUp) {
            inst.root.removeEventListener('pointerup', inst._handlePointerUp, true);
        }
        if (inst._handleClick) {
            inst.root.removeEventListener('click', inst._handleClick, true);
        }
        if (inst._handleDoubleClick) {
            inst.root.removeEventListener('dblclick', inst._handleDoubleClick, true);
        }
        if (inst._handleContextMenu) {
            inst.root.removeEventListener('contextmenu', inst._handleContextMenu, true);
        }
        if (inst._handleDragOver) {
            inst.root.removeEventListener('dragover', inst._handleDragOver, true);
        }
        if (inst._handleDragLeave) {
            inst.root.removeEventListener('dragleave', inst._handleDragLeave, true);
        }
        if (inst._handleDrop) {
            inst.root.removeEventListener('drop', inst._handleDrop, true);
        }
        if (inst._handleVirtualScroll) {
            inst.root.removeEventListener('scroll', inst._handleVirtualScroll);
            document.removeEventListener('scroll', inst._handleVirtualScroll, { capture: true });
            window.removeEventListener('scroll', inst._handleVirtualScroll);
            window.removeEventListener('resize', inst._handleVirtualScroll);
        }
    }

    function _onRootClick(inst, event) {
        if (!inst || inst.disposed || inst.readOnly) return;
        var target = event.target && event.target.nodeType === Node.ELEMENT_NODE
            ? event.target
            : event.target?.parentElement;
        if (!target) return;

        if (target.closest('.tm-wysiwyg-image-context-menu')) {
            return;
        }

        if (target.closest('.tm-wysiwyg-revision-review')) {
            return;
        }

        var commentAnchor = target.closest('.tm-document-inline--comment-anchor[data-comment-id]');
        if (commentAnchor && inst.root.contains(commentAnchor)) {
            var commentId = commentAnchor.getAttribute('data-comment-id') || '';
            if (commentId) {
                scrollToComment(inst.id, commentId);
                _invokeDotNet(inst, 'HandleCommentSelected', commentId);
            }
        }

        var pageBreak = target.closest('.tm-wysiwyg-page-break[data-block-id]');
        if (pageBreak && inst.root.contains(pageBreak)) {
            event.preventDefault();
            _selectPageBreak(inst, pageBreak);
            _hideInlineRevisionReview(inst);
            return;
        }

        var imageSideTextBlock = _findWrappedImageSideTextBlockAtPoint(inst, event.clientX, event.clientY);
        if (imageSideTextBlock) {
            event.preventDefault();
            _clearSelectedImage(inst);
            _hideImageContextMenu(inst);
            _hideImageReplaceMenu(inst);
            _focusWrappedImageSideTextBlock(inst, imageSideTextBlock, event.clientX, event.clientY);
            return;
        }

        var image = target.closest('figure.tm-wysiwyg-image');
        if (image && inst.root.contains(image)) {
            if (!_isImageVisualClick(image, event.clientX, event.clientY)) {
                return;
            }

            event.preventDefault();
            _selectImageFigure(inst, image);
            _hideInlineRevisionReview(inst);
            return;
        }

        var tableCell = target.closest('td[data-cell-id], th[data-cell-id]');
        if (tableCell && inst.root.contains(tableCell)) {
            _clearSelectedImage(inst);
            _hideImageContextMenu(inst);
            _hideImageReplaceMenu(inst);
            _markActiveTableCell(tableCell);
            var tableSnapshot = _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot || {};
            var tableCellId = tableCell.getAttribute('data-cell-id') || '';
            tableSnapshot.activeTableCellId = tableCellId;
            tableSnapshot.ActiveTableCellId = tableCellId;
            inst.lastSelectionSnapshot = tableSnapshot;
            inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(tableSnapshot);
            _scheduleSelectionNotification(inst, tableSnapshot);
            _hideInlineRevisionReview(inst);
            return;
        }

        _clearSelectedImage(inst);
        _hideImageContextMenu(inst);
        _hideImageReplaceMenu(inst);

        var headerFooterRegion = target.closest('.tm-wysiwyg-page__header[contenteditable="true"], .tm-wysiwyg-page__footer[contenteditable="true"]');
        if (headerFooterRegion && inst.root.contains(headerFooterRegion)) {
            _rememberBodySelection(inst, _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot);
            _markActivePageRegion(inst, headerFooterRegion);
            window.setTimeout(function () {
                var snapshot = _captureSelectionSnapshot(inst);
                if (snapshot) {
                    inst.lastSelectionSnapshot = snapshot;
                    inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(snapshot);
                    _scheduleSelectionNotification(inst, snapshot);
                }
            }, 0);
            return;
        }

        var bodyRegion = target.closest('.tm-wysiwyg-page__body[contenteditable="true"], .tm-wysiwyg-page__body[contenteditable="false"]');
        if (bodyRegion && inst.root.contains(bodyRegion)) {
            _markActivePageRegion(inst, bodyRegion);
        }

        var revision = target.closest('.tm-wysiwyg-revision[data-revision-id]');
        if (!revision || !inst.root.contains(revision)) {
            _hideInlineRevisionReview(inst);
            return;
        }

        _showInlineRevisionReview(inst, revision);
    }

    function _onDocumentPointerDown(inst, event) {
        if (!inst || inst.disposed || inst.readOnly) return;
        var target = event.target && event.target.nodeType === Node.ELEMENT_NODE
            ? event.target
            : event.target?.parentElement;
        if (!target) return;

        if (target.closest('[data-testid="document-context-clear-formatting"]')) {
            inst.clearFormattingPointerCaptureCount = (inst.clearFormattingPointerCaptureCount || 0) + 1;
            inst.clearFormattingPointerCaptureHandledUntil = Date.now() + 500;
            event.preventDefault();
            _executeClearFormattingCommand(inst, {
                selection: inst.contextMenuSelectionSnapshot || inst.lastSelectionSnapshot,
                fromPointerCapture: true
            });
            return;
        }

        if (inst.root.contains(target)) {
            return;
        }

        if (target.closest('[data-testid="document-toolbar"]')) {
            var toolbarSelection = _captureSelectionSnapshot(inst);
            if (toolbarSelection && _selectionRegionName(toolbarSelection) === 'body') {
                inst.lastSelectionSnapshot = toolbarSelection;
                inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(toolbarSelection);
                _rememberBodySelection(inst, toolbarSelection);
                inst.pendingSelectionSnapshot = toolbarSelection;
                _flushSelectionNotification(inst);
            }
            return;
        }

        if (target.closest('.tm-wysiwyg-image-context-menu, .tm-wysiwyg-image-replace-menu, .tm-wysiwyg-image-selection-toolbar, .tm-document-editor__mini-toolbar')) {
            return;
        }

        _hideImageContextMenu(inst);
        _hideImageReplaceMenu(inst);
        _hideMiniToolbar(inst, true);
    }

    function _onRootPointerUp(inst, event) {
        if (!inst || inst.disposed || inst.readOnly || event.button !== 0) return;

        var refreshSelectionToolbar = function () {
            if (!inst || inst.disposed) return;
            var snapshot = _captureSelectionSnapshot(inst);
            if (!snapshot || snapshot.isCollapsed || !_isTextSelectionSnapshot(snapshot)) {
                _hideMiniToolbar(inst, true);
                return;
            }

            inst.lastSelectionSnapshot = snapshot;
            inst.lastTextSelectionSnapshot = snapshot;
            inst.miniToolbarSuppressHideUntil = Date.now() + 700;
            _scheduleSelectionNotification(inst, snapshot);
            _scheduleMiniToolbar(inst, snapshot);
        };

        window.setTimeout(refreshSelectionToolbar, 0);
        window.setTimeout(refreshSelectionToolbar, 140);
    }

    function _onTablePointerDown(inst, event) {
        if (!inst || inst.disposed || inst.readOnly || event.button !== 0) return;
        var target = event.target && event.target.nodeType === Node.ELEMENT_NODE
            ? event.target
            : event.target?.parentElement;
        var cell = target && target.closest ? target.closest('td[data-cell-id], th[data-cell-id]') : null;
        if (!cell || !inst.root.contains(cell)) return;

        var table = _getTableBlockFromCell(cell);
        if (!table) return;

        _markActiveTableCell(cell);
        var tableSnapshot = _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot || {};
        var cellId = cell.getAttribute('data-cell-id') || '';
        tableSnapshot.activeTableCellId = cellId;
        tableSnapshot.ActiveTableCellId = cellId;
        inst.lastSelectionSnapshot = tableSnapshot;
        inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(tableSnapshot);
        _scheduleSelectionNotification(inst, tableSnapshot);

        var drag = {
            table: table,
            startCell: cell,
            currentCell: cell,
            startX: event.clientX,
            startY: event.clientY,
            started: false
        };
        inst.tableCellRangeDrag = drag;

        function onMove(moveEvent) {
            if (!inst.tableCellRangeDrag || inst.tableCellRangeDrag !== drag) return;
            var nextCell = _getTableCellAtPoint(inst, moveEvent.clientX, moveEvent.clientY);
            if (!nextCell || _getTableBlockFromCell(nextCell) !== table) return;

            var dx = moveEvent.clientX - drag.startX;
            var dy = moveEvent.clientY - drag.startY;
            var movedEnough = Math.sqrt((dx * dx) + (dy * dy)) >= 4;
            if (!drag.started && (!movedEnough || nextCell === drag.startCell)) return;

            drag.started = true;
            drag.currentCell = nextCell;
            moveEvent.preventDefault();
            _markTableCellRange(table, drag.startCell, nextCell);
        }

        function onUp(upEvent) {
            document.removeEventListener('pointermove', onMove, true);
            document.removeEventListener('pointerup', onUp, true);
            if (inst.tableCellRangeDrag === drag) {
                inst.tableCellRangeDrag = null;
            }

            if (!drag.started) return;
            upEvent.preventDefault();
            var finalSnapshot = _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot || {};
            finalSnapshot.activeTableCellId = cellId;
            finalSnapshot.ActiveTableCellId = cellId;
            inst.lastSelectionSnapshot = finalSnapshot;
            inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(finalSnapshot);
            _scheduleSelectionNotification(inst, finalSnapshot);
        }

        document.addEventListener('pointermove', onMove, true);
        document.addEventListener('pointerup', onUp, true);
    }

    function _onRootDoubleClick(inst, event) {
        if (!inst || inst.disposed || inst.readOnly) return;
        var target = event.target && event.target.nodeType === Node.ELEMENT_NODE
            ? event.target
            : event.target?.parentElement;
        if (!target) return;

        var headerFooter = target.closest('.tm-wysiwyg-page__header[contenteditable="true"], .tm-wysiwyg-page__footer[contenteditable="true"]');
        if (!headerFooter || !inst.root.contains(headerFooter)) return;

        event.preventDefault();
        _activatePageRegion(inst, headerFooter);
    }

    function _activatePageRegion(inst, regionEl) {
        if (!inst || !regionEl || !inst.root || !inst.root.contains(regionEl)) return;

        var regionName = regionEl.getAttribute('data-region') || '';
        if (regionName === 'Header' || regionName === 'Footer') {
            _rememberBodySelection(inst, _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot);
        }

        _markActivePageRegion(inst, regionEl);
        regionEl.focus({ preventScroll: true });
        _ensureEditableSelection(inst, regionEl);
        var snapshot = _captureSelectionSnapshot(inst);
        inst.lastSelectionSnapshot = snapshot;
        inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(snapshot);
        inst.pendingSelectionSnapshot = snapshot;
        _flushSelectionNotification(inst);
    }

    function _markActivePageRegion(inst, regionEl) {
        if (!inst || !regionEl || !inst.root || !inst.root.contains(regionEl)) return;

        inst.root.querySelectorAll('.tm-wysiwyg-region--active').forEach(function (active) {
            active.classList.remove('tm-wysiwyg-region--active');
            active.removeAttribute('data-region-active');
        });

        regionEl.classList.add('tm-wysiwyg-region--active');
        regionEl.setAttribute('data-region-active', 'true');
        inst.root.setAttribute('data-active-region', regionEl.getAttribute('data-region') || 'Body');
    }

    function _deactivatePageRegion(inst) {
        if (!inst || !inst.root) return;

        inst.root.querySelectorAll('.tm-wysiwyg-region--active').forEach(function (active) {
            active.classList.remove('tm-wysiwyg-region--active');
            active.removeAttribute('data-region-active');
        });
        inst.root.setAttribute('data-active-region', 'Body');
    }

    function _onRootContextMenu(inst, event) {
        if (!inst || inst.disposed) return;
        var target = event.target && event.target.nodeType === Node.ELEMENT_NODE
            ? event.target
            : event.target?.parentElement;

        if (inst.readOnly) {
            event.preventDefault();
            _clearSelectedImage(inst);
            _hideImageContextMenu(inst);
            _hideMiniToolbar(inst, true);
            _ensureContextMenuSelection(inst, target, event);
            var readOnlySnapshot = _captureSelectionSnapshot(inst);
            if (!readOnlySnapshot || readOnlySnapshot.isCollapsed || !_isTextSelectionSnapshot(readOnlySnapshot)) return;

            inst.lastSelectionSnapshot = readOnlySnapshot;
            inst.contextMenuSelectionSnapshot = _cloneRuntimeJson(readOnlySnapshot);
            inst.virtualSelectionSnapshot = null;
            _scheduleSelectionNotification(inst, readOnlySnapshot);
            var readOnlyPosition = _placeFloatingElement(event.clientX, event.clientY, 260, 380);
            _invokeDotNet(inst, 'HandleTextContextMenuRequested', {
                ClientX: event.clientX,
                ClientY: event.clientY,
                Left: readOnlyPosition.left,
                Top: readOnlyPosition.top,
                Width: readOnlyPosition.width,
                Height: readOnlyPosition.height,
                ViewportWidth: readOnlyPosition.viewportWidth,
                ViewportHeight: readOnlyPosition.viewportHeight,
                Selection: _toPascalSelection(readOnlySnapshot)
            });
            return;
        }

        var tableCell = target && target.closest && target.closest('td[data-cell-id], th[data-cell-id]');
        if (tableCell && inst.root.contains(tableCell)) {
            event.preventDefault();
            _clearSelectedImage(inst);
            _hideImageContextMenu(inst);
            _hideMiniToolbar(inst, true);
            _focusCell(tableCell);
            var tableSnapshot = _captureSelectionSnapshot(inst);
            if (!tableSnapshot) return;

            inst.lastSelectionSnapshot = tableSnapshot;
            _scheduleSelectionNotification(inst, tableSnapshot);
            var tablePosition = _placeFloatingElement(event.clientX, event.clientY, 256, 520);
            _invokeDotNet(inst, 'HandleTableContextMenuRequested', {
                ClientX: event.clientX,
                ClientY: event.clientY,
                Left: tablePosition.left,
                Top: tablePosition.top,
                Width: tablePosition.width,
                Height: tablePosition.height,
                ViewportWidth: tablePosition.viewportWidth,
                ViewportHeight: tablePosition.viewportHeight,
                CellId: tableCell.getAttribute('data-cell-id') || tableSnapshot.activeTableCellId || tableSnapshot.ActiveTableCellId || '',
                Selection: _toPascalSelection(tableSnapshot)
            });
            return;
        }

        var image = target && target.closest && target.closest('figure.tm-wysiwyg-image');
        if (image && inst.root.contains(image)) {
            event.preventDefault();
            _selectImageFigure(inst, image);
            _hideMiniToolbar(inst);
            _showImageContextMenu(inst, image, event.clientX, event.clientY);
            return;
        }

        var revision = target && target.closest && target.closest('.tm-wysiwyg-revision[data-revision-id]');
        if (revision && inst.root.contains(revision)) {
            event.preventDefault();
            _clearSelectedImage(inst);
            _hideImageContextMenu(inst);
            _hideMiniToolbar(inst, true);
            _showInlineRevisionReview(inst, revision);
            return;
        }

        var pageBreak = target && target.closest && target.closest('.tm-wysiwyg-page-break[data-block-id]');
        if (pageBreak && inst.root.contains(pageBreak)) {
            event.preventDefault();
            _selectPageBreak(inst, pageBreak);
            var pageBreakId = pageBreak.getAttribute('data-block-id') || '';
            var pageBreakPosition = _placeFloatingElement(event.clientX, event.clientY, 240, 148);
            _invokeDotNet(inst, 'HandleTextContextMenuRequested', {
                ClientX: event.clientX,
                ClientY: event.clientY,
                Left: pageBreakPosition.left,
                Top: pageBreakPosition.top,
                Width: pageBreakPosition.width,
                Height: pageBreakPosition.height,
                ViewportWidth: pageBreakPosition.viewportWidth,
                ViewportHeight: pageBreakPosition.viewportHeight,
                BlockId: pageBreakId,
                BlockType: 'PageBreak',
                Selection: {
                    Region: 'Body',
                    AnchorBlockId: pageBreakId,
                    FocusBlockId: pageBreakId,
                    AnchorOffset: 0,
                    FocusOffset: 0,
                    IsCollapsed: true
                }
            });
            return;
        }

        event.preventDefault();
        _clearSelectedImage(inst);
        _hideImageContextMenu(inst);
        _hideMiniToolbar(inst, true);
        _ensureContextMenuSelection(inst, target, event);
        var snapshot = _captureSelectionSnapshot(inst);
        if (!snapshot || snapshot.isCollapsed || !_isTextSelectionSnapshot(snapshot)) return;

        inst.lastSelectionSnapshot = snapshot;
        inst.contextMenuSelectionSnapshot = _cloneRuntimeJson(snapshot);
        inst.virtualSelectionSnapshot = null;
        _scheduleSelectionNotification(inst, snapshot);
        var position = _placeFloatingElement(event.clientX, event.clientY, 260, 380);
        _invokeDotNet(inst, 'HandleTextContextMenuRequested', {
            ClientX: event.clientX,
            ClientY: event.clientY,
            Left: position.left,
            Top: position.top,
            Width: position.width,
            Height: position.height,
            ViewportWidth: position.viewportWidth,
            ViewportHeight: position.viewportHeight,
            Selection: _toPascalSelection(snapshot)
        });
    }

    function _openKeyboardContextMenu(inst, event) {
        if (!inst || inst.disposed) return false;

        var point = _getKeyboardContextMenuPoint(inst, event);
        var target = document.elementFromPoint(point.x, point.y)
            || (event && event.target && event.target.nodeType === Node.ELEMENT_NODE ? event.target : null)
            || document.activeElement
            || inst.root;

        _onRootContextMenu(inst, {
            target: target,
            clientX: point.x,
            clientY: point.y,
            preventDefault: function () { },
            stopPropagation: function () { }
        });
        window.setTimeout(function () {
            var menu = document.querySelector('[data-testid="document-text-context-menu"], [data-testid="document-table-context-menu"], .tm-wysiwyg-image-context-menu');
            var item = menu && menu.querySelector('button[role="menuitem"]:not(:disabled), button:not(:disabled)');
            if (item && typeof item.focus === 'function') {
                item.focus({ preventScroll: true });
            }
        }, 0);

        return true;
    }

    function _getKeyboardContextMenuPoint(inst, event) {
        var rect = null;
        var selection = window.getSelection && window.getSelection();
        if (selection && selection.rangeCount > 0) {
            var range = selection.getRangeAt(0);
            var rects = Array.from(range.getClientRects()).filter(function (candidate) {
                return candidate && candidate.width > 0 && candidate.height > 0;
            });
            rect = rects[0] || range.getBoundingClientRect();
        }

        if ((!rect || rect.width <= 0 || rect.height <= 0) && inst.selectedImageFigure) {
            rect = inst.selectedImageFigure.getBoundingClientRect();
        }

        if (!rect || rect.width <= 0 || rect.height <= 0) {
            var target = event && event.target && event.target.nodeType === Node.ELEMENT_NODE
                ? event.target
                : document.activeElement;
            rect = target && target.getBoundingClientRect
                ? target.getBoundingClientRect()
                : inst.root.getBoundingClientRect();
        }

        var x = Math.round(rect.left + Math.min(Math.max(rect.width / 2, 12), Math.max(rect.width - 2, 12)));
        var y = Math.round(rect.top + Math.min(Math.max(rect.height / 2, 12), Math.max(rect.height - 2, 12)));
        x = Math.max(8, Math.min(window.innerWidth - 8, x));
        y = Math.max(8, Math.min(window.innerHeight - 8, y));
        return { x: x, y: y };
    }

    function _ensureContextMenuSelection(inst, target, event) {
        var sel = window.getSelection();
        if (sel && sel.rangeCount > 0 && !sel.isCollapsed && (_nodeBelongsToRoot(sel.anchorNode, inst.root) || _nodeBelongsToRoot(sel.focusNode, inst.root))) {
            return;
        }

        var element = target && target.nodeType === Node.ELEMENT_NODE ? target : target?.parentElement;
        var editable = element && element.closest('[contenteditable="true"], .tm-wysiwyg-block[data-block-id]');
        if (editable && inst.root.contains(editable)) {
            _ensureEditableSelection(inst, editable);
        }
    }

    function _getViewportMetrics(options) {
        options = options || {};
        var doc = typeof document !== 'undefined' ? document : null;
        var docEl = doc && doc.documentElement ? doc.documentElement : null;
        return {
            width: options.viewportWidth || (typeof window !== 'undefined' && window.innerWidth) || (docEl && docEl.clientWidth) || 1024,
            height: options.viewportHeight || (typeof window !== 'undefined' && window.innerHeight) || (docEl && docEl.clientHeight) || 768
        };
    }

    function _normalizeFloatingAnchor(anchor, options) {
        anchor = anchor || {};
        options = options || {};
        var left = Number(anchor.left ?? anchor.x ?? anchor.Left ?? anchor.X ?? 0);
        var top = Number(anchor.top ?? anchor.y ?? anchor.Top ?? anchor.Y ?? 0);
        var width = Number(anchor.width ?? anchor.Width ?? 0);
        var height = Number(anchor.height ?? anchor.Height ?? 0);

        if (options.anchorIsContainerRelative && options.scrollContainerRect) {
            var container = options.scrollContainerRect;
            left = Number(container.left ?? container.Left ?? 0) + left - Number(options.scrollLeft || 0);
            top = Number(container.top ?? container.Top ?? 0) + top - Number(options.scrollTop || 0);
        }

        return {
            left: left,
            top: top,
            width: width,
            height: height,
            right: Number(anchor.right ?? anchor.Right ?? (left + width)),
            bottom: Number(anchor.bottom ?? anchor.Bottom ?? (top + height))
        };
    }

    function _computeFloatingBoundary(options, viewport) {
        options = options || {};
        var margin = Number(options.margin ?? 8);
        var source = options.boundaryRect || (options.constrainToScrollContainer ? options.scrollContainerRect : null);
        if (!source) {
            return {
                left: margin,
                top: margin,
                right: viewport.width - margin,
                bottom: viewport.height - margin,
                viewportWidth: viewport.width,
                viewportHeight: viewport.height
            };
        }

        var left = Number(source.left ?? source.Left ?? 0);
        var top = Number(source.top ?? source.Top ?? 0);
        var right = Number(source.right ?? source.Right ?? (left + Number(source.width ?? source.Width ?? viewport.width)));
        var bottom = Number(source.bottom ?? source.Bottom ?? (top + Number(source.height ?? source.Height ?? viewport.height)));
        return {
            left: Math.max(margin, left + margin),
            top: Math.max(margin, top + margin),
            right: Math.min(viewport.width - margin, right - margin),
            bottom: Math.min(viewport.height - margin, bottom - margin),
            viewportWidth: viewport.width,
            viewportHeight: viewport.height
        };
    }

    function _clamp(value, min, max) {
        if (max < min) return min;
        return Math.max(min, Math.min(value, max));
    }

    function _computeFloatingPosition(anchor, elementSize, options) {
        options = options || {};
        var margin = Number(options.margin ?? 8);
        var viewport = _getViewportMetrics(options);
        var rect = _normalizeFloatingAnchor(anchor, options);
        var boundary = _computeFloatingBoundary(options, viewport);
        var width = Number(elementSize?.width ?? elementSize?.Width ?? options.width ?? 0);
        var height = Number(elementSize?.height ?? elementSize?.Height ?? options.height ?? 0);
        var placement = options.placement || options.preferredPlacement || 'bottom';
        var align = options.align || 'center';

        var left = align === 'start'
            ? rect.left
            : align === 'end'
                ? rect.right - width
                : rect.left + (rect.width / 2) - (width / 2);

        var top = placement === 'top'
            ? rect.top - height - margin
            : rect.bottom + margin;
        var resolvedPlacement = placement;

        if (placement === 'top' && top < boundary.top && rect.bottom + margin + height <= boundary.bottom) {
            top = rect.bottom + margin;
            resolvedPlacement = 'bottom';
        } else if (placement !== 'top' && top + height > boundary.bottom && rect.top - height - margin >= boundary.top) {
            top = rect.top - height - margin;
            resolvedPlacement = 'top';
        }

        left = _clamp(left, boundary.left, boundary.right - width);
        top = _clamp(top, boundary.top, boundary.bottom - height);

        return {
            left: left,
            top: top,
            width: width,
            height: height,
            viewportWidth: viewport.width,
            viewportHeight: viewport.height,
            placement: resolvedPlacement,
            boundaryLeft: boundary.left,
            boundaryTop: boundary.top,
            boundaryRight: boundary.right,
            boundaryBottom: boundary.bottom
        };
    }

    function _placeFloatingElement(clientX, clientY, width, height) {
        var position = _computeFloatingPosition(
            { left: clientX, top: clientY, width: 0, height: 0 },
            { width: width, height: height },
            { placement: 'bottom', align: 'start' });
        return {
            left: position.left,
            top: position.top,
            width: width,
            height: height,
            viewportWidth: position.viewportWidth,
            viewportHeight: position.viewportHeight
        };
    }

    function _isTextSelectionSnapshot(snapshot) {
        if (!snapshot || snapshot.isCollapsed) return false;
        var region = snapshot.region || snapshot.Region || 'Body';
        return region !== 'Image';
    }

    function _scheduleMiniToolbar(inst, snapshot) {
        if (!inst || inst.disposed || inst.readOnly || !_isTextSelectionSnapshot(snapshot)) {
            _hideMiniToolbar(inst);
            return;
        }

        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || sel.isCollapsed || !_nodeBelongsToRoot(sel.anchorNode, inst.root)) {
            _hideMiniToolbar(inst);
            return;
        }

        var range = sel.getRangeAt(0);
        var rect = range.getBoundingClientRect();
        if (!rect || (!rect.width && !rect.height)) {
            var rects = range.getClientRects();
            rect = rects.length > 0 ? rects[0] : null;
        }
        if (!rect) {
            _hideMiniToolbar(inst);
            return;
        }

        var width = 336;
        var height = 40;
        var position = _computeFloatingPosition(rect, { width: width, height: height }, { placement: 'top', align: 'center' });
        var left = position.left;
        var top = position.top;
        var viewportWidth = position.viewportWidth;
        var viewportHeight = position.viewportHeight;

        var key = [
            Math.round(left),
            Math.round(top),
            snapshot.anchorBlockId || '',
            snapshot.anchorInlineId || '',
            snapshot.anchorOffset || 0,
            snapshot.focusBlockId || '',
            snapshot.focusInlineId || '',
            snapshot.focusOffset || 0
        ].join('|');

        if (inst.dismissedMiniToolbarSelectionKey && inst.dismissedMiniToolbarSelectionKey !== key) {
            inst.dismissedMiniToolbarSelectionKey = null;
        }
        if (inst.dismissedMiniToolbarSelectionKey === key) {
            _hideNativeMiniToolbarFallback(inst);
            return;
        }

        if (inst.miniToolbarVisible
            && inst.miniToolbarRequestKey === key
            && document.querySelector('[data-testid="document-mini-toolbar"]')) {
            return;
        }
        inst.miniToolbarVisible = true;
        inst.lastTextSelectionSnapshot = snapshot;
        inst.miniToolbarRequestKey = key;
        _invokeDotNet(inst, 'HandleMiniToolbarChanged', {
            IsVisible: true,
            Left: left,
            Top: top,
            Width: width,
            Height: height,
            ViewportWidth: viewportWidth,
            ViewportHeight: viewportHeight,
            Selection: _toPascalSelection(snapshot)
        });
        window.setTimeout(function () {
            if (!inst || inst.disposed || !inst.miniToolbarVisible || inst.miniToolbarRequestKey !== key) return;
            if (document.querySelector('[data-testid="document-mini-toolbar"]')) return;
            _showNativeMiniToolbarFallback(inst, { left: left, top: top, width: width, height: height }, snapshot, key);
        }, 180);
    }

    function _hideMiniToolbar(inst, force) {
        if (!inst || (!force && !inst.miniToolbarVisible)) return;
        inst.miniToolbarVisible = false;
        inst.miniToolbarRequestKey = null;
        if (force) {
            inst.miniToolbarSuppressHideUntil = 0;
            inst.lastTextSelectionSnapshot = null;
        }
        _hideNativeMiniToolbarFallback(inst);
        _invokeDotNet(inst, 'HandleMiniToolbarChanged', null);
    }

    function _dismissMiniToolbar(inst) {
        if (!inst) return;
        inst.dismissedMiniToolbarSelectionKey = inst.miniToolbarRequestKey || '';
        _hideMiniToolbar(inst, true);
    }

    function _showNativeMiniToolbarFallback(inst, position, snapshot, key) {
        _hideNativeMiniToolbarFallback(inst);
        var toolbar = document.createElement('section');
        toolbar.className = 'tm-document-editor__mini-toolbar tm-document-editor__mini-toolbar--native';
        toolbar.setAttribute('role', 'toolbar');
        toolbar.setAttribute('aria-label', 'Text formatting');
        toolbar.setAttribute('contenteditable', 'false');
        toolbar.setAttribute('data-testid', 'document-mini-toolbar');
        toolbar.style.position = 'fixed';
        toolbar.style.left = Math.round(position.left) + 'px';
        toolbar.style.top = Math.round(position.top) + 'px';
        toolbar.style.zIndex = '1002';

        [
            { label: 'B', title: 'Bold', testId: 'document-mini-bold', command: 'toggleBold' },
            { label: 'I', title: 'Italic', testId: 'document-mini-italic', command: 'toggleItalic' },
            { label: 'U', title: 'Underline', testId: 'document-mini-underline', command: 'toggleUnderline' },
            { label: 'S', title: 'Strikethrough', testId: 'document-mini-strikethrough', command: 'toggleStrikethrough' },
            { label: 'A', title: 'Text color', testId: 'document-mini-text-color', command: 'setTextColor', payload: { Value: '#123456' } },
            { label: 'H', title: 'Highlight', testId: 'document-mini-highlight', command: 'setHighlightColor', payload: { Value: '#fff59d' } },
            { label: 'Link', title: 'Link', testId: 'document-mini-link', command: null },
            { label: 'Comment', title: 'Comment', testId: 'document-mini-comment', command: null },
            { label: 'Clear', title: 'Clear formatting', testId: 'document-mini-clear-formatting', command: 'clearFormatting' }
        ].forEach(function (item) {
            var button = document.createElement('button');
            button.type = 'button';
            button.textContent = item.label;
            button.title = item.title;
            button.setAttribute('data-testid', item.testId);
            button.addEventListener('mousedown', function (event) {
                event.preventDefault();
                event.stopPropagation();
            });
            button.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                if (!item.command) return;
                _restoreSelection(inst, snapshot);
                executeCommand(inst.id, item.command, Object.assign({ Selection: snapshot }, item.payload || {}));
            });
            toolbar.appendChild(button);
        });

        document.body.appendChild(toolbar);
        inst.nativeMiniToolbar = toolbar;
        inst.nativeMiniToolbarKey = key;
        if (inst.nativeMiniToolbarObserver) {
            inst.nativeMiniToolbarObserver.disconnect();
        }
        inst.nativeMiniToolbarObserver = new MutationObserver(function () {
            var blazorToolbar = document.querySelector('.tm-document-editor__floating-root [data-testid="document-mini-toolbar"]');
            if (blazorToolbar) {
                _hideNativeMiniToolbarFallback(inst);
            }
        });
        inst.nativeMiniToolbarObserver.observe(document.body, { childList: true, subtree: true });
    }

    function _hideNativeMiniToolbarFallback(inst) {
        if (!inst) return;
        if (inst.nativeMiniToolbarObserver) {
            inst.nativeMiniToolbarObserver.disconnect();
            inst.nativeMiniToolbarObserver = null;
        }
        if (!inst.nativeMiniToolbar) return;
        if (inst.nativeMiniToolbar.parentNode) {
            inst.nativeMiniToolbar.parentNode.removeChild(inst.nativeMiniToolbar);
        }

        inst.nativeMiniToolbar = null;
        inst.nativeMiniToolbarKey = null;
    }

    function _repositionVisibleFloatingLayers(inst) {
        if (!inst || inst.disposed || !inst.miniToolbarVisible || !inst.lastSelectionSnapshot) return;
        var start = _performanceNow();
        _scheduleMiniToolbar(inst, inst.lastSelectionSnapshot);
        _recordFloatingRepositionMetric(inst, start);
    }

    function _onRootDragOver(inst, event) {
        if (!inst || inst.disposed || inst.readOnly) return;
        var file = _getImageFileFromDataTransfer(event.dataTransfer);
        if (!file) return;
        event.preventDefault();
        inst.root.classList.add('tm-document-wysiwyg-host--image-drop-target');
    }

    function _onRootDragLeave(inst, event) {
        if (!inst || !inst.root) return;
        if (event.relatedTarget && inst.root.contains(event.relatedTarget)) return;
        inst.root.classList.remove('tm-document-wysiwyg-host--image-drop-target');
    }

    function _onRootDrop(inst, event) {
        if (!inst || inst.disposed || inst.readOnly) return;
        var file = _getImageFileFromDataTransfer(event.dataTransfer);
        if (!file) return;

        event.preventDefault();
        inst.root.classList.remove('tm-document-wysiwyg-host--image-drop-target');
        _uploadAndInsertImageFile(inst, file, _captureSelectionSnapshot(inst));
    }

    function _getImageFileFromDataTransfer(dataTransfer) {
        if (!dataTransfer) return null;
        var files = Array.from(dataTransfer.files || []);
        return files.find(function (file) {
            return file && file.type && file.type.indexOf('image/') === 0;
        }) || null;
    }

    function _selectImageFigure(inst, figure) {
        if (!inst || !figure || !inst.root.contains(figure)) return;
        if (inst.selectedImageFigure && inst.selectedImageFigure !== figure) {
            inst.selectedImageFigure.classList.remove('tm-wysiwyg-image--selected');
            inst.selectedImageFigure.removeAttribute('aria-selected');
        }

        inst.selectedImageFigure = figure;
        figure.classList.add('tm-wysiwyg-image--selected');
        figure.setAttribute('aria-selected', 'true');
        figure.setAttribute('tabindex', '0');
        if (typeof figure.focus === 'function') {
            figure.focus({ preventScroll: true });
        }
        var browserSelection = window.getSelection && window.getSelection();
        if (browserSelection) {
            browserSelection.removeAllRanges();
        }

        var block = figure.closest('.tm-wysiwyg-block[data-block-id]');
        if (block) {
            inst.lastSelectionSnapshot = _createImageSelectionSnapshot(figure);
            inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(inst.lastSelectionSnapshot);
            _scheduleSelectionNotification(inst, inst.lastSelectionSnapshot);
        }
        _showImageSelectionToolbar(inst, figure);
    }

    function _createImageSelectionSnapshot(figure) {
        var blockId = figure && figure.closest
            ? (figure.closest('.tm-wysiwyg-block[data-block-id]')?.getAttribute('data-block-id') || figure.getAttribute('data-block-id') || '')
            : '';
        return {
            Region: 'Image',
            AnchorBlockId: blockId,
            FocusBlockId: blockId,
            ActiveImageBlockId: blockId,
            AnchorInlineId: '',
            FocusInlineId: '',
            AnchorOffset: 0,
            FocusOffset: 0,
            IsCollapsed: true
        };
    }

    function _clearSelectedImage(inst) {
        if (!inst || !inst.selectedImageFigure) return;
        _hideImageSelectionToolbar(inst);
        if (inst.selectedImageFigure.isConnected) {
            inst.selectedImageFigure.classList.remove('tm-wysiwyg-image--selected');
            inst.selectedImageFigure.removeAttribute('aria-selected');
        }
        inst.selectedImageFigure = null;
        if (inst.runtimeSelection && (inst.runtimeSelection.activeImageBlockId || inst.runtimeSelection.ActiveImageBlockId)) {
            inst.runtimeSelection = Object.assign({}, inst.runtimeSelection, {
                region: 'Body',
                activeImageBlockId: null,
                ActiveImageBlockId: null
            });
            inst.lastSelectionSnapshot = _createSelectionSnapshotFromRuntimeSelection(inst.runtimeSelection);
        }
    }

    function _positionFloatingElementInRoot(inst, element, x, y, fallbackWidth, fallbackHeight) {
        if (!inst || !inst.root || !element) return;
        var root = inst.root;
        var rootRect = root.getBoundingClientRect();
        var width = element.offsetWidth || fallbackWidth || 220;
        var height = element.offsetHeight || fallbackHeight || 40;
        var minLeft = root.scrollLeft + Math.max(8, 8 - rootRect.left);
        var maxLeft = root.scrollLeft + Math.min(root.clientWidth - width - 8, window.innerWidth - rootRect.left - width - 8);
        var minTop = root.scrollTop + Math.max(8, 8 - rootRect.top);
        var maxTop = root.scrollTop + Math.min(root.clientHeight - height - 8, window.innerHeight - rootRect.top - height - 8);
        if (maxLeft < minLeft) maxLeft = minLeft;
        if (maxTop < minTop) maxTop = minTop;
        element.style.position = 'absolute';
        element.style.left = Math.min(Math.max(x, minLeft), maxLeft) + 'px';
        element.style.top = Math.min(Math.max(y, minTop), maxTop) + 'px';
    }

    function _showImageContextMenu(inst, figure, clientX, clientY) {
        if (!inst || !figure) return;
        _hideImageContextMenu(inst);
        _hideImageSelectionToolbar(inst);

        var menu = document.createElement('div');
        menu.className = 'tm-wysiwyg-image-context-menu';
        menu.setAttribute('role', 'menu');
        menu.setAttribute('aria-label', 'Image context menu');
        menu.setAttribute('contenteditable', 'false');
        menu.setAttribute('data-testid', 'document-wysiwyg-image-context-menu');

        var actions = [
            { text: 'Replace image', testId: 'document-wysiwyg-image-replace', action: function () { _showImageReplaceMenu(inst, figure, clientX, clientY); } },
            { text: 'Alt text', testId: 'document-wysiwyg-image-alt-text', action: function () { _editSelectedImageAltText(inst); } },
            { text: 'Caption', testId: 'document-wysiwyg-image-caption', action: function () { _editSelectedImageCaption(inst); } },
            { text: 'Wrap text: Inline', testId: 'document-wysiwyg-image-wrap-inline', action: function () { _setSelectedImageInline(inst); } },
            { text: 'Wrap text: Square', testId: 'document-wysiwyg-image-wrap-square', action: function () { _setSelectedImageWrapMode(inst, { wrapMode: 'Square' }); } },
            { text: 'Wrap text: Top and bottom', testId: 'document-wysiwyg-image-wrap-top-bottom', action: function () { _setSelectedImageWrapMode(inst, { wrapMode: 'TopBottom' }); } },
            { text: 'Position: Left', testId: 'document-wysiwyg-image-position-left', action: function () { _setSelectedImagePosition(inst, { horizontalPosition: 'Left' }); } },
            { text: 'Position: Right', testId: 'document-wysiwyg-image-position-right', action: function () { _setSelectedImagePosition(inst, { horizontalPosition: 'Right' }); } },
            { text: 'Position: In front of text', testId: 'document-wysiwyg-image-position-front', action: function () { _setSelectedImageWrapMode(inst, { wrapMode: 'InFrontOfText' }); } },
            { text: 'Delete', testId: 'document-wysiwyg-image-delete', action: function () { _deleteSelectedImage(inst); } }
        ];

        actions.forEach(function (item) {
            var button = document.createElement('button');
            button.type = 'button';
            button.textContent = item.text;
            button.setAttribute('role', 'menuitem');
            button.setAttribute('data-testid', item.testId);
            button.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                item.action();
            });
            menu.appendChild(button);
        });

        inst.root.appendChild(menu);
        var figRect = figure.getBoundingClientRect();
        var anchorX = Number.isFinite(clientX) ? clientX : figRect.left + 8;
        var anchorY = Number.isFinite(clientY) ? clientY : figRect.bottom + 8;
        var menuWidth = menu.offsetWidth || menu.getBoundingClientRect().width || 220;
        var menuHeight = menu.offsetHeight || menu.getBoundingClientRect().height || 340;
        var maxLeft = Math.max(8, window.innerWidth - menuWidth - 8);
        var maxTop = Math.max(8, window.innerHeight - menuHeight - 8);
        menu.style.position = 'fixed';
        menu.style.left = Math.min(Math.max(anchorX, 8), maxLeft) + 'px';
        menu.style.top = Math.min(Math.max(anchorY, 8), maxTop) + 'px';
        menu.style.zIndex = '1002';
        inst.imageContextMenu = menu;
    }

    function _hideImageContextMenu(inst) {
        if (!inst || !inst.imageContextMenu) return;
        if (inst.imageContextMenu.parentNode) {
            inst.imageContextMenu.parentNode.removeChild(inst.imageContextMenu);
        }
        inst.imageContextMenu = null;
    }

    function _showImageReplaceMenu(inst, figure, clientX, clientY) {
        figure = figure || _getSelectedImageFigure(inst);
        if (!inst || !figure) return;
        inst.selectedImageFigure = figure;
        _hideImageContextMenu(inst);
        _hideImageReplaceMenu(inst);

        var menu = document.createElement('div');
        menu.className = 'tm-wysiwyg-image-replace-menu';
        menu.setAttribute('role', 'menu');
        menu.setAttribute('contenteditable', 'false');
        menu.setAttribute('data-testid', 'document-wysiwyg-image-replace-menu');

        var hasProviderAssets = !!(inst.options.hasImageAssetOptions ?? inst.options.HasImageAssetOptions);
        var actions = [
            { text: 'Replace from URL', testId: 'document-wysiwyg-image-replace-url', action: function () { _replaceSelectedImageFromUrl(inst); } },
            { text: 'Upload file', testId: 'document-wysiwyg-image-replace-upload', action: function () { _replaceSelectedImageFromUpload(inst, figure); } }
        ];
        if (hasProviderAssets) {
            actions.push({ text: 'Provider asset', testId: 'document-wysiwyg-image-replace-asset', action: function () { _replaceSelectedImageFromAsset(inst, figure); } });
        }

        actions.forEach(function (item) {
            var button = document.createElement('button');
            button.type = 'button';
            button.textContent = item.text;
            button.setAttribute('role', 'menuitem');
            button.setAttribute('data-testid', item.testId);
            button.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                item.action();
                _hideImageReplaceMenu(inst);
            });
            menu.appendChild(button);
        });

        inst.root.appendChild(menu);
        var rootRect = inst.root.getBoundingClientRect();
        var figRect = figure.getBoundingClientRect();
        var x = Number.isFinite(clientX) ? clientX - rootRect.left + inst.root.scrollLeft : figRect.left - rootRect.left + inst.root.scrollLeft;
        var y = Number.isFinite(clientY) ? clientY - rootRect.top + inst.root.scrollTop : figRect.bottom - rootRect.top + inst.root.scrollTop + 8;
        var menuHeight = Math.max(menu.offsetHeight || menu.getBoundingClientRect().height || 120, 120);
        _positionFloatingElementInRoot(inst, menu, x, y, 220, menuHeight);
        inst.imageReplaceMenu = menu;
    }

    function _hideImageReplaceMenu(inst) {
        if (!inst || !inst.imageReplaceMenu) return;
        if (inst.imageReplaceMenu.parentNode) {
            inst.imageReplaceMenu.parentNode.removeChild(inst.imageReplaceMenu);
        }
        inst.imageReplaceMenu = null;
    }

    // Phase 9.1: floating mini-toolbar shown when image is selected.
    function _showImageSelectionToolbar(inst, figure) {
        _hideImageSelectionToolbar(inst);
        if (!inst || !figure) return;

        var toolbar = document.createElement('div');
        toolbar.className = 'tm-wysiwyg-image-selection-toolbar';
        toolbar.setAttribute('role', 'toolbar');
        toolbar.setAttribute('contenteditable', 'false');
        toolbar.setAttribute('data-testid', 'document-wysiwyg-image-selection-toolbar');
        toolbar.setAttribute('aria-label', 'Image tools');

        var buttons = [
            { label: 'Alt text', testId: 'document-wysiwyg-image-toolbar-alt', action: function () { _beginEditImageAltText(inst, figure); } },
            { label: 'Caption', testId: 'document-wysiwyg-image-toolbar-caption', action: function () { _toggleImageCaption(inst); } },
            { label: 'Replace', testId: 'document-wysiwyg-image-toolbar-replace', action: function () { _showImageReplaceMenu(inst, figure); } },
            { label: 'Delete', testId: 'document-wysiwyg-image-toolbar-delete', action: function () { _deleteSelectedImage(inst); } }
        ];

        buttons.forEach(function (btn) {
            var b = document.createElement('button');
            b.type = 'button';
            b.textContent = btn.label;
            b.setAttribute('data-testid', btn.testId);
            b.setAttribute('role', 'button');
            b.addEventListener('mousedown', function (event) {
                event.preventDefault();
                event.stopPropagation();
            });
            b.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                btn.action();
            });
            toolbar.appendChild(b);
        });

        inst.root.appendChild(toolbar);
        var figRect = figure.getBoundingClientRect();
        var rootRect = inst.root.getBoundingClientRect();
        var toolbarHeight = toolbar.offsetHeight || 40;
        var top = figRect.top - rootRect.top + inst.root.scrollTop - toolbarHeight - 8;
        if (top < 8) {
            top = figRect.bottom - rootRect.top + inst.root.scrollTop + 8;
        }

        var left = figRect.left - rootRect.left + inst.root.scrollLeft;
        _positionFloatingElementInRoot(inst, toolbar, left, top, 360, toolbarHeight);
        toolbar.style.zIndex = '1001';
        inst.imageSelectionToolbar = toolbar;
    }

    function _hideImageSelectionToolbar(inst) {
        if (!inst || !inst.imageSelectionToolbar) return;
        if (inst.imageSelectionToolbar.parentNode) {
            inst.imageSelectionToolbar.parentNode.removeChild(inst.imageSelectionToolbar);
        }
        inst.imageSelectionToolbar = null;
    }

    // Phase 9.2: set alt text via command (without window.prompt).
    function _beginEditImageAltText(inst, figure) {
        var img = (figure || _getSelectedImageFigure(inst)) && (figure || _getSelectedImageFigure(inst)).querySelector('img');
        if (!img) return;
        var current = img.alt || '';
        var next = window.prompt('Alt text', current);
        if (next == null) return;
        _setImageAltText(inst, next);
    }

    function _setImageAltText(inst, altText, blockId) {
        var figure = blockId ? _getImageFigureByBlockId(inst, blockId) : _getSelectedImageFigure(inst);
        if (!figure) return;
        var img = figure.querySelector('img');
        if (img) img.alt = altText || '';
        _dispatchImageUpdatePatch(inst, figure);
    }

    // Phase 9.3: toggle figcaption on the selected image.
    function _toggleImageCaption(inst, blockId) {
        var figure = blockId ? _getImageFigureByBlockId(inst, blockId) : _getSelectedImageFigure(inst);
        if (!figure) return;
        var existing = figure.querySelector('figcaption');
        if (existing) {
            existing.remove();
        } else {
            var figcaption = document.createElement('figcaption');
            figcaption.setAttribute('contenteditable', 'true');
            figcaption.setAttribute('data-testid', 'document-wysiwyg-image-caption-text');
            figcaption.textContent = 'Caption';
            var handle = figure.querySelector('.tm-wysiwyg-image__resize-handle');
            if (handle) {
                figure.insertBefore(figcaption, handle);
            } else {
                figure.appendChild(figcaption);
            }
            figcaption.focus();
            var selection = window.getSelection && window.getSelection();
            if (selection) {
                var range = document.createRange();
                range.selectNodeContents(figcaption);
                selection.removeAllRanges();
                selection.addRange(range);
            }
        }
        _ensureImageResizeHandle(figure, inst);
        _dispatchImageUpdatePatch(inst, figure);
    }

    function _setImageCaption(inst, captionText, blockId) {
        var figure = blockId ? _getImageFigureByBlockId(inst, blockId) : _getSelectedImageFigure(inst);
        if (!figure) return;
        var text = captionText == null ? '' : String(captionText);
        var existing = figure.querySelector('figcaption');
        if (text.trim().length > 0) {
            if (!existing) {
                existing = document.createElement('figcaption');
                existing.setAttribute('contenteditable', 'true');
                existing.setAttribute('data-testid', 'document-wysiwyg-image-caption-text');
                var handle = figure.querySelector('.tm-wysiwyg-image__resize-handle');
                if (handle) {
                    figure.insertBefore(existing, handle);
                } else {
                    figure.appendChild(existing);
                }
            }
            existing.textContent = text;
        } else if (existing) {
            existing.remove();
        }
        _ensureImageResizeHandle(figure, inst);
        _dispatchImageUpdatePatch(inst, figure);
    }

    // Phase 9.4: set image URL (replace image source).
    function _setImageUrl(inst, url, blockId) {
        var figure = blockId ? _getImageFigureByBlockId(inst, blockId) : _getSelectedImageFigure(inst);
        if (!figure) return;
        var img = figure.querySelector('img');
        if (!img) return;
        if (_isSafeImageUrl(url)) {
            img.src = url;
            figure.setAttribute('data-image-source', '0');
            figure.removeAttribute('data-image-asset-id');
            _attachImageLoadState(figure, img, url, inst);
        }
        _dispatchImageUpdatePatch(inst, figure);
    }

    // Phase 9.5: set/clear image link URL.
    function _setImageLink(inst, linkUrl, blockId) {
        var figure = blockId ? _getImageFigureByBlockId(inst, blockId) : _getSelectedImageFigure(inst);
        if (!figure) return;
        if (linkUrl) {
            figure.setAttribute('data-image-link', linkUrl);
        } else {
            figure.removeAttribute('data-image-link');
        }
        _dispatchImageUpdatePatch(inst, figure);
    }

    function _replaceSelectedImage(inst) {
        _showImageReplaceMenu(inst, _getSelectedImageFigure(inst));
    }

    function _replaceSelectedImageFromUrl(inst) {
        var figure = _getSelectedImageFigure(inst);
        if (!figure) return;
        var img = figure.querySelector('img');
        var current = img ? (img.getAttribute('src') || '') : '';
        var next = window.prompt('Image URL', current);
        if (next == null) return;
        _setImageUrl(inst, next);
    }

    function _replaceSelectedImageFromUpload(inst, figure) {
        figure = figure || _getSelectedImageFigure(inst);
        if (!figure) return;

        var input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/*';
        input.style.position = 'fixed';
        input.style.left = '-9999px';
        input.addEventListener('change', function () {
            var file = input.files && input.files[0];
            if (!file) {
                input.remove();
                return;
            }

            var block = figure.closest('.tm-wysiwyg-block[data-block-id]');
            var selection = {
                Region: 'Image',
                AnchorBlockId: block ? block.getAttribute('data-block-id') || '' : '',
                FocusBlockId: block ? block.getAttribute('data-block-id') || '' : '',
                AnchorOffset: 0,
                FocusOffset: 0,
                IsCollapsed: true
            };
            _uploadAndReplaceImageFile(inst, figure, file, selection);
            input.remove();
        }, { once: true });
        document.body.appendChild(input);
        input.click();
        _hideImageContextMenu(inst);
    }

    function _replaceSelectedImageFromAsset(inst, figure) {
        figure = figure || _getSelectedImageFigure(inst);
        if (!figure) return;
        _invokeDotNetResult(inst, 'HandleImageAssetRequested').then(function (block) {
            var content = block && (block.content || block.Content);
            if (!content) return;
            _applyImageContentToFigure(figure, content, inst);
            _dispatchImageUpdatePatch(inst, figure);
            _hideImageContextMenu(inst);
            _hideImageReplaceMenu(inst);
        });
    }

    function _editSelectedImageAltText(inst) {
        var figure = _getSelectedImageFigure(inst);
        if (!figure) return;
        var img = figure.querySelector('img');
        var current = img ? (img.alt || '') : '';
        var next = window.prompt('Alt text', current);
        if (next == null) return;
        if (img) img.alt = next;
        _dispatchImageUpdatePatch(inst, figure);
        _hideImageContextMenu(inst);
    }

    function _editSelectedImageCaption(inst) {
        var figure = _getSelectedImageFigure(inst);
        if (!figure) return;
        var caption = figure.querySelector('figcaption');
        var current = caption ? (caption.textContent || '') : '';
        var next = window.prompt('Caption', current);
        if (next == null) return;
        if (!caption && next.trim().length > 0) {
            caption = document.createElement('figcaption');
            figure.insertBefore(caption, figure.querySelector('.tm-wysiwyg-image__resize-handle') || null);
        }
        if (caption) {
            caption.textContent = next;
            if (next.trim().length === 0) caption.remove();
        }
        _ensureImageResizeHandle(figure, inst);
        _dispatchImageUpdatePatch(inst, figure);
        _hideImageContextMenu(inst);
    }

    function _deleteSelectedImage(inst) {
        var figure = _getSelectedImageFigure(inst);
        if (!figure) return;
        var block = figure.closest('.tm-wysiwyg-block[data-block-id]');
        if (!block) return;
        var blockId = block.getAttribute('data-block-id') || '';
        block.remove();
        _clearSelectedImage(inst);
        _hideImageContextMenu(inst);
        _hideImageReplaceMenu(inst);
        _dispatchPatch(inst, {
            type: 'RemoveBlock',
            blockId: blockId,
            selection: {
                Region: 'Body',
                AnchorBlockId: blockId,
                FocusBlockId: blockId,
                AnchorOffset: 0,
                FocusOffset: 0,
                IsCollapsed: true
            },
            protocolVersion: inst.options.protocolVersion || 1
        });
    }

    function _selectPageBreak(inst, pageBreak) {
        if (!inst || !pageBreak) return;
        _clearSelectedImage(inst);
        _hideImageContextMenu(inst);
        _hideMiniToolbar(inst, true);
        if (inst.selectedPageBreakId && inst.root) {
            inst.root.querySelectorAll('.tm-wysiwyg-page-break--selected').forEach(function (el) {
                el.classList.remove('tm-wysiwyg-page-break--selected');
                el.removeAttribute('aria-selected');
            });
        }
        inst.selectedPageBreakId = pageBreak.getAttribute('data-block-id') || null;
        pageBreak.classList.add('tm-wysiwyg-page-break--selected');
        pageBreak.setAttribute('aria-selected', 'true');
        pageBreak.setAttribute('tabindex', '0');
        try { pageBreak.focus({ preventScroll: true }); } catch {}
    }

    function _clearSelectedPageBreak(inst) {
        if (!inst || !inst.root) return;
        inst.root.querySelectorAll('.tm-wysiwyg-page-break--selected').forEach(function (el) {
            el.classList.remove('tm-wysiwyg-page-break--selected');
            el.removeAttribute('aria-selected');
        });
        inst.selectedPageBreakId = null;
    }

    function _deletePageBreak(inst, blockId) {
        if (!inst || !inst.root || inst.readOnly) return false;
        var id = blockId || inst.selectedPageBreakId || '';
        if (!id) return false;
        var selector = '.tm-wysiwyg-page-break[data-block-id="' + _cssEscape(id) + '"]';
        var pageBreak = inst.root.querySelector(selector);
        if (!pageBreak) return false;
        var beforeSelection = {
            Region: 'Body',
            AnchorBlockId: id,
            FocusBlockId: id,
            AnchorOffset: 0,
            FocusOffset: 0,
            IsCollapsed: true
        };
        _beginUndoTransaction(inst, 'deletePageBreak', 'Delete page break', beforeSelection, true);
        pageBreak.remove();
        _clearSelectedPageBreak(inst);
        _dispatchPatch(inst, {
            type: 'RemoveBlock',
            operationId: _nextRuntimeOperationId(inst),
            blockId: id,
            selection: beforeSelection,
            beforeSelection: beforeSelection,
            afterSelection: beforeSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
        _commitCurrentRuntimeTransaction(inst, true);
        _notifyPageMetrics(inst);
        return true;
    }

    function _uploadAndReplaceImageFile(inst, figure, file, selection) {
        var reader = new FileReader();
        reader.onload = function () {
            var result = String(reader.result || '');
            var commaIndex = result.indexOf(',');
            var payload = {
                source: 2,
                fileName: file.name || 'replacement-image',
                contentType: file.type || 'image/png',
                sizeBytes: file.size || 0,
                base64Data: commaIndex >= 0 ? result.slice(commaIndex + 1) : result,
                altText: file.name || 'replacement-image',
                selection: selection
            };

            _invokeDotNetResult(inst, 'HandleImageUploadRequested', payload).then(function (block) {
                var content = block && (block.content || block.Content);
                if (!content) return;
                _applyImageContentToFigure(figure, content, inst);
                _dispatchImageUpdatePatch(inst, figure);
            });
        };
        reader.readAsDataURL(file);
    }

    function _applyImageContentToFigure(figure, content, inst) {
        var img = figure.querySelector('img') || document.createElement('img');
        if (!img.parentNode) figure.insertBefore(img, figure.firstChild);
        var src = (content && (content.url || content.Url)) || '';
        if (_isSafeImageUrl(src)) img.src = src;
        img.alt = (content && (content.altText || content.AltText)) || img.alt || '';
        var source = content && (content.source ?? content.Source);
        var assetId = content && (content.assetId || content.AssetId);
        figure.setAttribute('data-image-source', source == null ? '0' : String(source));
        if (assetId) {
            figure.setAttribute('data-image-asset-id', assetId);
        } else {
            figure.removeAttribute('data-image-asset-id');
        }
        _attachImageLoadState(figure, img, src, inst);
        _ensureImageResizeHandle(figure, inst);
    }

    function _setSelectedImageInline(inst) {
        var figure = _getSelectedImageFigure(inst);
        if (!figure) return;
        var layout = _serializeImage(figure).FloatingLayout || {};
        layout.Inline = true;
        layout.WrapMode = 0;
        layout.X = 0;
        layout.Y = 0;
        layout.ZIndex = 0;
        _applyFloatingImageLayout(figure, { FloatingLayout: layout }, inst);
        _dispatchImageUpdatePatch(inst, figure);
        _hideImageContextMenu(inst);
    }

    function _createWrappedImageSideTextBlockModel(figure) {
        var imageOrder = parseFloat(figure && figure.getAttribute('data-block-order') || '0') || 0;
        var blockId = _createBlockId();
        var inlineId = _createInlineId();
        return {
            Id: blockId,
            Type: 0,
            Order: imageOrder + 0.1,
            ParagraphProperties: {},
            Content: {
                $type: 'paragraph',
                Inlines: [{
                    $type: 'text',
                    Id: inlineId,
                    Text: ''
                }]
            }
        };
    }

    function _isWrappedImageSideTextBlock(block) {
        return !!(block && block.matches && block.matches('p.tm-wysiwyg-block[data-wrap-sidecar-for], p.tm-wysiwyg-image-sidecar-text'));
    }

    function _isTextBlockForWrappedImage(block) {
        if (!block || !block.matches) return false;
        if (_isWrappedImageSideTextBlock(block)) return true;
        return block.matches('p.tm-wysiwyg-block[data-block-id]');
    }

    function _isWrappedImageSideTextLayout(figure) {
        if (!figure) return false;
        var inline = figure.getAttribute('data-floating-inline') !== 'false';
        var wrapMode = parseInt(figure.getAttribute('data-wrap-mode') || (inline ? '0' : '1'), 10);
        var hPos = figure.getAttribute('data-horizontal-position') || '';
        return !inline && wrapMode === 1 && (!hPos || hPos === 'left' || hPos === 'right');
    }

    function _pointInRect(rect, clientX, clientY, padding) {
        if (!rect) return false;
        var pad = padding || 0;
        return clientX >= rect.left - pad
            && clientX <= rect.right + pad
            && clientY >= rect.top - pad
            && clientY <= rect.bottom + pad;
    }

    function _getImagePrimaryVisualRect(figure) {
        if (!figure || !figure.querySelector) return null;
        var img = figure.querySelector('img');
        if (img) return img.getBoundingClientRect();
        return figure.getBoundingClientRect();
    }

    function _isImageVisualClick(figure, clientX, clientY) {
        if (!figure || !figure.querySelectorAll) return false;
        var targets = figure.querySelectorAll('img, figcaption, .tm-wysiwyg-image__resize-handle');
        for (var i = 0; i < targets.length; i++) {
            if (_pointInRect(targets[i].getBoundingClientRect(), clientX, clientY, 4)) {
                return true;
            }
        }

        return targets.length === 0 && _pointInRect(figure.getBoundingClientRect(), clientX, clientY, 4);
    }

    function _createSelectionSnapshotForBlockElement(blockEl, regionOverride) {
        if (!blockEl || !blockEl.getAttribute) return null;
        var blockId = blockEl.getAttribute('data-block-id') || '';
        if (!blockId) return null;

        var inline = blockEl.querySelector && blockEl.querySelector('[data-inline-id]');
        var inlineId = inline ? (inline.getAttribute('data-inline-id') || '') : '';
        var pageEl = blockEl.closest && blockEl.closest('.tm-wysiwyg-page[data-page-index]');
        var pageIndex = pageEl ? parseInt(pageEl.getAttribute('data-page-index') || '0', 10) : null;
        if (!Number.isFinite(pageIndex)) pageIndex = null;

        return {
            region: regionOverride || 'Body',
            pageIndex: pageIndex,
            headerFooterId: null,
            anchorNodeId: inlineId || blockId,
            focusNodeId: inlineId || blockId,
            anchorBlockId: blockId,
            focusBlockId: blockId,
            anchorInlineId: inlineId,
            focusInlineId: inlineId,
            anchorOffset: 0,
            anchorBlockOffset: 0,
            focusOffset: 0,
            focusBlockOffset: 0,
            isCollapsed: true,
            direction: 'forward',
            activeTableCellId: null,
            tableCellPath: null,
            activeImageBlockId: null
        };
    }

    function _ensureWrappedImageSideTextBlock(inst, figure, dispatchPatch, selectionSnapshot) {
        if (!inst || !figure || !_isWrappedImageSideTextLayout(figure)) {
            _debugImage(inst, 'sidecar.ensure.skipped', {
                figure: _debugElementLabel(figure),
                inline: figure && figure.getAttribute('data-floating-inline'),
                wrapMode: figure && figure.getAttribute('data-wrap-mode'),
                horizontalPosition: figure && figure.getAttribute('data-horizontal-position')
            });
            return null;
        }

        var imageId = figure.getAttribute('data-block-id') || '';
        var parent = figure.parentElement;
        if (!parent) {
            _debugImage(inst, 'sidecar.ensure.no-parent', { imageId: imageId });
            return null;
        }

        var next = figure.nextElementSibling;
        if (_isTextBlockForWrappedImage(next)) {
            next.classList.add('tm-wysiwyg-image-sidecar-text');
            if (imageId && !next.getAttribute('data-wrap-sidecar-for')) {
                next.setAttribute('data-wrap-sidecar-for', imageId);
            }
            _debugImage(inst, 'sidecar.ensure.reused', {
                imageId: imageId,
                block: _debugElementLabel(next),
                dispatchPatch: !!dispatchPatch
            });
            return next;
        }

        var block = _createWrappedImageSideTextBlockModel(figure);
        var blockEl = _renderBlock(block, inst);
        if (!blockEl) {
            _debugImage(inst, 'sidecar.ensure.render-failed', { imageId: imageId });
            return null;
        }

        blockEl.classList.add('tm-wysiwyg-image-sidecar-text');
        if (imageId) {
            blockEl.setAttribute('data-wrap-sidecar-for', imageId);
        }

        var inline = blockEl.querySelector('[data-inline-id]');
        _ensureCaretPlaceholder(inline);
        parent.insertBefore(blockEl, figure.nextSibling);

        if (dispatchPatch) {
            var startedSidecarTransaction = false;
            if (!inst.currentTransactionId) {
                inst.currentTransactionId = 'txn-sidecar-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7);
                startedSidecarTransaction = true;
            }
            var patchSelection = selectionSnapshot || _createImageSelectionSnapshot(figure);
            if (!patchSelection) {
                patchSelection = _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot;
            }
            var sideTextSelection = _createSelectionSnapshotForBlockElement(blockEl, 'Body');
            _dispatchPatch(inst, {
                type: 'InsertBlock',
                operationId: _nextRuntimeOperationId(inst),
                blockType: 'Paragraph',
                block: block,
                selection: patchSelection,
                beforeSelection: patchSelection,
                afterSelection: sideTextSelection || patchSelection,
                transactionId: inst.currentTransactionId,
                protocolVersion: inst.options.protocolVersion || 1
            });
            if (startedSidecarTransaction) {
                window.setTimeout(function () {
                    _commitCurrentRuntimeTransaction(inst, true);
                }, 0);
            }
        }

        _debugImage(inst, 'sidecar.ensure.created', {
            imageId: imageId,
            block: _debugElementLabel(blockEl),
            dispatchPatch: !!dispatchPatch
        });
        return blockEl;
    }

    function _findWrappedImageSideTextBlockAtPoint(inst, clientX, clientY) {
        if (!inst || !inst.root) return null;

        var topElement = typeof document.elementFromPoint === 'function'
            ? document.elementFromPoint(clientX, clientY)
            : null;
        var topFigure = topElement && topElement.closest
            ? topElement.closest('figure.tm-wysiwyg-image')
            : null;
        if (topFigure && !inst.root.contains(topFigure)) {
            topFigure = null;
        }

        var figures = Array.from(inst.root.querySelectorAll('figure.tm-wysiwyg-image'));
        _debugImage(inst, 'sidecar.hit-test.start', {
            x: Math.round(clientX),
            y: Math.round(clientY),
            figureCount: figures.length,
            figures: _debugImageFigures(inst)
        });
        for (var i = 0; i < figures.length; i++) {
            var figure = figures[i];
            if (!_isWrappedImageSideTextLayout(figure)) {
                _debugImage(inst, 'sidecar.hit-test.skip-layout', {
                    figure: _debugElementLabel(figure),
                    inline: figure.getAttribute('data-floating-inline'),
                    wrapMode: figure.getAttribute('data-wrap-mode'),
                    horizontalPosition: figure.getAttribute('data-horizontal-position')
                });
                continue;
            }

            var figureRect = figure.getBoundingClientRect();
            var visualRect = _getImagePrimaryVisualRect(figure) || figureRect;
            if (clientY < figureRect.top || clientY > figureRect.bottom) {
                _debugImage(inst, 'sidecar.hit-test.skip-y', {
                    figure: _debugElementLabel(figure),
                    figureRect: _debugRect(figureRect),
                    visualRect: _debugRect(visualRect),
                    x: Math.round(clientX),
                    y: Math.round(clientY)
                });
                continue;
            }

            var hPos = figure.getAttribute('data-horizontal-position') || 'left';
            var isSideArea = hPos === 'right'
                ? clientX < visualRect.left - 4
                : clientX > visualRect.right + 4;
            if (isSideArea) {
                if (topFigure && topFigure !== figure) {
                    _debugImage(inst, 'sidecar.hit-test.skip-visible-image', {
                        figure: _debugElementLabel(figure),
                        topFigure: _debugElementLabel(topFigure),
                        topElement: _debugElementLabel(topElement),
                        figureRect: _debugRect(figureRect),
                        visualRect: _debugRect(visualRect),
                        horizontalPosition: hPos,
                        x: Math.round(clientX),
                        y: Math.round(clientY)
                    });
                    return null;
                }

                _debugImage(inst, 'sidecar.hit-test.match', {
                    figure: _debugElementLabel(figure),
                    figureRect: _debugRect(figureRect),
                    visualRect: _debugRect(visualRect),
                    horizontalPosition: hPos,
                    x: Math.round(clientX),
                    y: Math.round(clientY)
                });
                return _ensureWrappedImageSideTextBlock(inst, figure, true);
            }

            _debugImage(inst, 'sidecar.hit-test.not-side-area', {
                figure: _debugElementLabel(figure),
                figureRect: _debugRect(figureRect),
                visualRect: _debugRect(visualRect),
                horizontalPosition: hPos,
                x: Math.round(clientX),
                y: Math.round(clientY)
            });
        }

        _debugImage(inst, 'sidecar.hit-test.no-match', {
            x: Math.round(clientX),
            y: Math.round(clientY)
        });
        return null;
    }

    function _getCaretRangeFromPoint(doc, clientX, clientY) {
        if (!doc || !Number.isFinite(clientX) || !Number.isFinite(clientY)) return null;
        if (typeof doc.caretRangeFromPoint === 'function') {
            return doc.caretRangeFromPoint(clientX, clientY);
        }

        if (typeof doc.caretPositionFromPoint === 'function') {
            var position = doc.caretPositionFromPoint(clientX, clientY);
            if (!position) return null;
            var range = doc.createRange();
            range.setStart(position.offsetNode, position.offset);
            range.collapse(true);
            return range;
        }

        return null;
    }

    function _trySetCaretFromPointInBlock(block, clientX, clientY) {
        if (!block || !block.ownerDocument) return false;
        var range = _getCaretRangeFromPoint(block.ownerDocument, clientX, clientY);
        if (!range) return false;

        var container = range.startContainer;
        var element = container.nodeType === Node.ELEMENT_NODE ? container : container.parentElement;
        if (!element || !block.contains(element)) {
            _debugImage(null, 'sidecar.caret-from-point.outside-block', {
                block: _debugElementLabel(block),
                rangeElement: _debugElementLabel(element),
                x: Math.round(clientX),
                y: Math.round(clientY)
            });
            return false;
        }

        var sel = block.ownerDocument.defaultView && block.ownerDocument.defaultView.getSelection
            ? block.ownerDocument.defaultView.getSelection()
            : window.getSelection();
        if (!sel) return false;
        var rect = range.getBoundingClientRect ? range.getBoundingClientRect() : null;
        if (rect && rect.width <= 1 && rect.height > 0 && clientX > rect.right + 8) {
            _debugImage(null, 'sidecar.caret-from-point.reject-left-edge', {
                block: _debugElementLabel(block),
                rangeRect: _debugRect(rect),
                x: Math.round(clientX),
                y: Math.round(clientY)
            });
            return false;
        }
        sel.removeAllRanges();
        sel.addRange(range);
        _debugImage(null, 'sidecar.caret-from-point.applied', {
            block: _debugElementLabel(block),
            rangeRect: _debugRect(rect),
            x: Math.round(clientX),
            y: Math.round(clientY)
        });
        return true;
    }

    function _focusWrappedImageSideTextBlock(inst, block, clientX, clientY) {
        if (!block) return;

        var editable = block.closest('[contenteditable="true"]');
        var bodyRegion = block.closest('.tm-wysiwyg-page__body[contenteditable="true"], .tm-wysiwyg-page__body[contenteditable="false"]');
        if (bodyRegion && inst && inst.root && inst.root.contains(bodyRegion)) {
            _markActivePageRegion(inst, bodyRegion);
        }

        if (editable && typeof editable.focus === 'function') {
            editable.focus({ preventScroll: true });
        }

        if (_trySetCaretFromPointInBlock(block, clientX, clientY)) {
            var pointSnapshot = _captureSelectionSnapshot(inst);
            if (pointSnapshot) {
                inst.lastSelectionSnapshot = pointSnapshot;
                inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(pointSnapshot);
                _scheduleSelectionNotification(inst, pointSnapshot);
            }
            _debugImage(inst, 'sidecar.focus.point', {
                block: _debugElementLabel(block),
                snapshot: pointSnapshot
            });
            return;
        }

        var inline = block.querySelector('[data-inline-id]');
        if (inline) {
            _removeCaretPlaceholders(inline);
            if (!inline.firstChild) {
                inline.appendChild(document.createTextNode(''));
            }

            var textNode = Array.from(inline.childNodes).find(function (node) {
                return node.nodeType === Node.TEXT_NODE;
            });
            if (!textNode) {
                textNode = document.createTextNode('');
                inline.insertBefore(textNode, inline.firstChild);
            }

            _setCaret(textNode, textNode.textContent ? textNode.textContent.length : 0);
        } else {
            _placeCaretAfterBlock(block);
        }

        var snapshot = _captureSelectionSnapshot(inst);
        if (snapshot) {
            inst.lastSelectionSnapshot = snapshot;
            inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(snapshot);
            _scheduleSelectionNotification(inst, snapshot);
        }
        _debugImage(inst, 'sidecar.focus.fallback-end', {
            block: _debugElementLabel(block),
            snapshot: snapshot
        });
    }

    // ── Input pipeline ───────────────────────────────────────────────────────

    function _isInsideProtectedEditableRegion(snapshot, markers) {
        var blockId = snapshot.anchorBlockId || snapshot.AnchorBlockId || '';
        var offset = snapshot.anchorOffset != null ? snapshot.anchorOffset : (snapshot.AnchorOffset || 0);
        if (!blockId) return false;
        for (var i = 0; i < markers.length; i++) {
            var m = markers[i];
            var sb = m.startBlockId || m.StartBlockId || '';
            var eb = m.endBlockId || m.EndBlockId || '';
            var so = m.startOffset != null ? m.startOffset : (m.StartOffset || 0);
            var eo = m.endOffset != null ? m.endOffset : (m.EndOffset || 0);
            if (sb === blockId && eb === blockId) {
                if (offset >= so && offset < eo) return true;
            } else if (sb === blockId && offset >= so) {
                return true;
            } else if (eb === blockId && offset < eo) {
                return true;
            }
        }
        return false;
    }

    function _refreshProtectionMarkers(inst) {
        if (!inst || !inst.root) return;
        if (inst.root.classList) {
            if (inst._isProtected && inst.root.classList.add) {
                inst.root.classList.add('tm-wysiwyg--protected');
            } else if (!inst._isProtected && inst.root.classList.remove) {
                inst.root.classList.remove('tm-wysiwyg--protected');
            }
        }
        if (!inst.root.querySelectorAll) return;

        inst.root.querySelectorAll('.tm-wysiwyg-restricted-editable')
            .forEach(function (block) {
                if (block.classList && block.classList.remove) {
                    block.classList.remove('tm-wysiwyg-restricted-editable');
                }
                if (block.removeAttribute) {
                    block.removeAttribute('data-restricted-editable');
                }
            });

        _getRuntimeMarkersByType(inst, 'restrictedRegion').forEach(function (marker) {
            var id = String(marker.id || marker.Id || '');
            var source = String(marker.source || marker.Source || '');
            if (id.indexOf('restricted:') === 0 || source === 'documentProtection') {
                _removeRuntimeMarker(inst, id);
            }
        });

        if (!inst._isProtected || !inst._protectedMarkers || inst._protectedMarkers.length === 0) return;

        inst._protectedMarkers.forEach(function (marker, index) {
            var startBlockId = marker.startBlockId || marker.StartBlockId || '';
            var endBlockId = marker.endBlockId || marker.EndBlockId || startBlockId;
            var markerId = marker.id || marker.Id || ('region-' + index);
            if (startBlockId) {
                _upsertRuntimeMarker(inst, {
                    id: 'restricted:' + markerId,
                    type: 'restrictedRegion',
                    range: {
                        startBlockId: startBlockId,
                        startOffset: marker.startOffset ?? marker.StartOffset ?? 0,
                        endBlockId: endBlockId || startBlockId,
                        endOffset: marker.endOffset ?? marker.EndOffset ?? 0
                    },
                    priority: 40,
                    affectsData: false,
                    source: 'documentProtection',
                    targetId: markerId,
                    label: marker.label || marker.Label || ''
                }, false);
            }
            if (startBlockId) {
                inst.root.querySelectorAll('[data-block-id="' + startBlockId + '"]').forEach(function (block) {
                    if (block.classList && block.classList.add) {
                        block.classList.add('tm-wysiwyg-restricted-editable');
                    }
                    if (block.setAttribute) {
                        block.setAttribute('data-restricted-editable', 'true');
                    }
                });
            }
            if (endBlockId && endBlockId !== startBlockId) {
                inst.root.querySelectorAll('[data-block-id="' + endBlockId + '"]').forEach(function (block) {
                    if (block.classList && block.classList.add) {
                        block.classList.add('tm-wysiwyg-restricted-editable');
                    }
                    if (block.setAttribute) {
                        block.setAttribute('data-restricted-editable', 'true');
                    }
                });
            }
        });
    }

    function _onBeforeInput(inst, event) {
        var measure = _beginInputMeasure(inst, event, 'beforeinput');
        try {
            if (inst.readOnly) {
                event.preventDefault();
                event.stopPropagation();
                return;
            }

            if (inst._isProtected) {
                var protSnap = _captureSelectionSnapshot(inst);
                var protectedMarkers = inst._protectedMarkers || [];
                if (!protSnap || protectedMarkers.length === 0 || !_isInsideProtectedEditableRegion(protSnap, protectedMarkers)) {
                    event.preventDefault();
                    event.stopPropagation();
                    return;
                }
            }

            if (inst.compositionActive) return;
            _hideMiniToolbar(inst);

            var inputType = event.inputType;
            inst.lastInputType = inputType || null;
            inst.lastInputDataLength = event.data ? event.data.length : 0;
            _ensureEditableSelection(inst, event.target);
            var selection = _captureSelectionSnapshot(inst);
            var allowedTypes = [
                'insertText', 'insertParagraph', 'insertLineBreak',
                'deleteContentBackward', 'deleteContentForward', 'deleteWordBackward', 'deleteWordForward',
                'formatBold', 'formatItalic', 'formatUnderline'
            ];

            if (!allowedTypes.includes(inputType)) {
                // Prevent unsupported input types; Blazor will apply them via patches.
                event.preventDefault();
                return;
            }

            if (inst.trackChangesEnabled && _handleTrackedBeforeInput(inst, event, inputType, selection)) {
                return;
            }

            if (_handlePendingTypingBeforeInput(inst, event, inputType, selection)) {
                return;
            }

            if (_handleStructuralBeforeInput(inst, event, inputType, selection, null)) {
                return;
            }

            if (_handleJsOwnedTextBeforeInput(inst, event, inputType, selection)) {
                return;
            }

            event.preventDefault();
        } finally {
            _endInputMeasure(measure);
        }
    }

    function _applyInsertText(inst, data) {
        if (!data) return null;
        inst._applyingOwnPatch = true;
        try {
            var sel = window.getSelection();
            if (!sel || sel.rangeCount === 0) return null;
            var range = sel.getRangeAt(0);
            _removeCaretPlaceholders(inst.root);
            var replacedText = '';
            if (!sel.isCollapsed) {
                replacedText = range.toString();
                range.deleteContents();
            }

            var untrackedRevisionInsert = _applyUntrackedInsertTextOutsideRevision(inst, data, range);
            if (untrackedRevisionInsert) {
                untrackedRevisionInsert.replacedText = replacedText;
                return untrackedRevisionInsert;
            }

            var textNode = range.startContainer;
            var offset = range.startOffset;

            if (textNode.nodeType === Node.TEXT_NODE) {
                var current = textNode.textContent;
                textNode.textContent = current.slice(0, offset) + data + current.slice(offset);
                _setCaret(textNode, offset + data.length);
                return { insertedText: data, replacedText: replacedText };
            } else if (textNode.nodeType === Node.ELEMENT_NODE) {
                var inline = textNode.querySelector('[data-inline-id]') || textNode.closest('[data-inline-id]');
                if (inline && inline.firstChild && inline.firstChild.nodeType === Node.TEXT_NODE) {
                    var txt = inline.firstChild;
                    var currentText = txt.textContent;
                    var clampedOffset = Math.max(0, Math.min(offset, currentText.length));
                    txt.textContent = currentText.slice(0, clampedOffset) + data + currentText.slice(clampedOffset);
                    _setCaret(txt, clampedOffset + data.length);
                    return { insertedText: data, replacedText: replacedText };
                } else {
                    var newText = document.createTextNode(data);
                    inline = inline || textNode;
                    inline.appendChild(newText);
                    _setCaret(newText, data.length);
                    return { insertedText: data, replacedText: replacedText };
                }
            }

            return null;
        } finally {
            inst._applyingOwnPatch = false;
        }
    }

    function _applyUntrackedInsertTextOutsideRevision(inst, data, range) {
        if (!inst || inst.trackChangesEnabled || !data || !range || !range.collapsed) return null;

        var normalized = _normalizeToTextNode(range.startContainer, range.startOffset);
        var revisionInline = _findInlineElement(normalized.node);
        if (!revisionInline || !revisionInline.closest || !revisionInline.closest('[data-revision-id], .tm-wysiwyg-revision')) {
            return null;
        }

        var textNode = normalized.node;
        if (!textNode || textNode.nodeType !== Node.TEXT_NODE || !revisionInline.contains(textNode)) {
            return null;
        }

        var current = textNode.textContent || '';
        var offset = Math.max(0, Math.min(normalized.offset, current.length));
        var beforeText = current.slice(0, offset);
        var afterText = current.slice(offset);
        textNode.textContent = beforeText;

        var insertedInline = _cloneInlineWithoutRevision(revisionInline);
        var inlineId = _createInlineId();
        insertedInline.setAttribute('data-inline-id', inlineId);
        var insertedText = document.createTextNode(data);
        insertedInline.textContent = '';
        insertedInline.appendChild(insertedText);

        if (afterText) {
            var afterInline = revisionInline.cloneNode(false);
            afterInline.setAttribute('data-inline-id', _createInlineId());
            afterInline.textContent = afterText;
            revisionInline.after(insertedInline, afterInline);
        } else {
            revisionInline.after(insertedInline);
        }

        _setCaret(insertedText, data.length);
        return { insertedText: data, insertedInline: true, inlineId: inlineId };
    }

    function _cloneInlineWithoutRevision(sourceInline) {
        var clone = sourceInline.cloneNode(false);
        [
            'tm-wysiwyg-revision',
            'tm-wysiwyg-revision--insert',
            'tm-wysiwyg-revision--delete',
            'tm-wysiwyg-revision--format',
            'tm-wysiwyg-revision--selected',
            'tm-document-inline--revision',
            'tm-document-inline--revision-insert',
            'tm-document-inline--revision-delete'
        ].forEach(function (className) {
            clone.classList.remove(className);
        });
        clone.removeAttribute('data-revision-id');
        clone.removeAttribute('data-revision-type');
        var testId = clone.getAttribute('data-testid') || '';
        if (testId.indexOf('document-wysiwyg-revision-') === 0 || testId.indexOf('document-revision-') === 0) {
            clone.removeAttribute('data-testid');
        }
        return clone;
    }

    function _createCaretPlaceholderBreak() {
        var br = document.createElement('br');
        br.setAttribute('data-caret-placeholder', 'true');
        br.setAttribute('aria-hidden', 'true');
        return br;
    }

    function _isCaretPlaceholderNode(node) {
        return node
            && node.nodeType === Node.ELEMENT_NODE
            && node.tagName
            && node.tagName.toLowerCase() === 'br'
            && node.hasAttribute('data-caret-placeholder');
    }

    function _ensureCaretPlaceholder(inline) {
        if (!inline || inline.querySelector('br[data-caret-placeholder]')) return;
        inline.appendChild(_createCaretPlaceholderBreak());
    }

    function _removeCaretPlaceholders(root) {
        if (!root || !root.querySelectorAll) return;
        root.querySelectorAll('br[data-caret-placeholder]').forEach(function (node) {
            node.remove();
        });
    }

    function _applyDeleteBackward(inst, unit) {
        inst._applyingOwnPatch = true;
        try {
            var sel = window.getSelection();
            if (!sel || sel.rangeCount === 0) return null;
            if (!sel.isCollapsed) {
                var selectedText = sel.toString();
                sel.deleteFromDocument();
                return { deletedText: selectedText, deletedRange: true };
            }
            var range = sel.getRangeAt(0);
            var textNode = range.startContainer;
            var offset = range.startOffset;

            if (textNode.nodeType === Node.TEXT_NODE) {
                var current = textNode.textContent;
                var delLen = unit === 'word' ? _wordBoundaryBackward(current, offset) : 1;
                if (offset > 0) {
                    var deletedText = current.slice(offset - delLen, offset);
                    textNode.textContent = current.slice(0, offset - delLen) + current.slice(offset);
                    _setCaret(textNode, offset - delLen);
                    return { deletedText: deletedText, deletedRange: false };
                }
            }

            return _mergeCurrentBlockWithPrevious(inst, range.startContainer);
        } finally {
            inst._applyingOwnPatch = false;
        }
    }

    function _applyDeleteForward(inst, unit) {
        inst._applyingOwnPatch = true;
        try {
            var sel = window.getSelection();
            if (!sel || sel.rangeCount === 0) return null;
            if (!sel.isCollapsed) {
                var selectedText = sel.toString();
                sel.deleteFromDocument();
                return { deletedText: selectedText, deletedRange: true };
            }
            var range = sel.getRangeAt(0);
            var textNode = range.startContainer;
            var offset = range.startOffset;

            if (textNode.nodeType === Node.TEXT_NODE) {
                var current = textNode.textContent;
                var delLen = unit === 'word' ? _wordBoundaryForward(current, offset) : 1;
                var deletedText = current.slice(offset, offset + delLen);
                textNode.textContent = current.slice(0, offset) + current.slice(offset + delLen);
                _setCaret(textNode, offset);
                return delLen > 0 ? { deletedText: deletedText, deletedRange: false } : null;
            }

            return null;
        } finally {
            inst._applyingOwnPatch = false;
        }
    }

    function _mergeCurrentBlockWithPrevious(inst, node) {
        var block = _closestBlockElement(node);
        if (!block) return null;

        var body = block.parentElement;
        if (!body) return null;

        var siblings = Array.from(body.querySelectorAll(':scope > .tm-wysiwyg-block[data-block-id]'));
        var index = siblings.indexOf(block);
        if (index <= 0) return null;

        var previous = siblings[index - 1];
        if (!previous || previous.matches('figure, table, hr')) return null;

        var caretNode = _lastDeepTextNode(previous) || _firstDeepTextNode(previous);
        var caretOffset = caretNode ? (caretNode.textContent || '').length : 0;
        while (block.firstChild) {
            previous.appendChild(block.firstChild);
        }
        block.remove();

        if (caretNode) {
            _setCaret(caretNode, caretOffset);
        }

        _invalidateMeasureCache(inst);
        return { deletedText: '', deletedRange: false, mergedBlock: true };
    }

    function _wordBoundaryBackward(text, offset) {
        var i = offset - 1;
        while (i >= 0 && /\s/.test(text[i])) i--;
        while (i >= 0 && /\S/.test(text[i])) i--;
        return offset - i - 1;
    }

    function _wordBoundaryForward(text, offset) {
        var i = offset;
        while (i < text.length && /\s/.test(text[i])) i++;
        while (i < text.length && /\S/.test(text[i])) i++;
        return i - offset;
    }

    function _setCaret(node, offset) {
        var sel = window.getSelection();
        if (!sel) return;
        sel.removeAllRanges();
        var range = document.createRange();
        range.setStart(node, offset);
        range.collapse(true);
        sel.addRange(range);
    }

    function _performanceNow() {
        return window.performance && typeof window.performance.now === 'function'
            ? window.performance.now()
            : Date.now();
    }

    function _createPerformanceStats() {
        return {
            markerRenderAttempts: 0,
            markerRenderCount: 0,
            markerRenderSkippedCount: 0,
            markerRenderTotalMs: 0,
            markerRenderMaxMs: 0,
            markerRenderLastMs: 0,
            floatingRepositionCount: 0,
            floatingRepositionTotalMs: 0,
            floatingRepositionMaxMs: 0,
            floatingRepositionLastMs: 0,
            clipboardNormalizationCount: 0,
            clipboardNormalizationTotalMs: 0,
            clipboardNormalizationMaxMs: 0,
            clipboardNormalizationLastMs: 0
        };
    }

    function _ensurePerformanceStats(inst) {
        if (!inst.performanceStats) {
            inst.performanceStats = _createPerformanceStats();
        }

        return inst.performanceStats;
    }

    function _recordMarkerRenderMetric(inst, start, rendered) {
        if (!inst) return;
        var stats = _ensurePerformanceStats(inst);
        var elapsed = Math.max(0, _performanceNow() - start);
        stats.markerRenderAttempts++;
        if (rendered) {
            stats.markerRenderCount++;
        } else {
            stats.markerRenderSkippedCount++;
        }

        stats.markerRenderTotalMs += elapsed;
        stats.markerRenderLastMs = elapsed;
        stats.markerRenderMaxMs = Math.max(stats.markerRenderMaxMs || 0, elapsed);
    }

    function _recordFloatingRepositionMetric(inst, start) {
        if (!inst) return;
        var stats = _ensurePerformanceStats(inst);
        var elapsed = Math.max(0, _performanceNow() - start);
        stats.floatingRepositionCount++;
        stats.floatingRepositionTotalMs += elapsed;
        stats.floatingRepositionLastMs = elapsed;
        stats.floatingRepositionMaxMs = Math.max(stats.floatingRepositionMaxMs || 0, elapsed);
    }

    function _recordClipboardNormalizationMetric(inst, start) {
        if (!inst) return;
        var stats = _ensurePerformanceStats(inst);
        var elapsed = Math.max(0, _performanceNow() - start);
        stats.clipboardNormalizationCount++;
        stats.clipboardNormalizationTotalMs += elapsed;
        stats.clipboardNormalizationLastMs = elapsed;
        stats.clipboardNormalizationMaxMs = Math.max(stats.clipboardNormalizationMaxMs || 0, elapsed);
    }

    function _beginInputMeasure(inst, event, eventType) {
        if (!inst) return null;
        return {
            inst: inst,
            event: event || null,
            eventType: eventType || '',
            start: _performanceNow()
        };
    }

    function _endInputMeasure(measure) {
        if (!measure || !measure.inst) return;
        var inst = measure.inst;
        if (!inst.inputStats) {
            inst.inputStats = {
                operationCount: 0,
                longOperationCount: 0,
                totalLatencyMs: 0,
                totalOperationMs: 0,
                maxLatencyMs: 0,
                maxOperationMs: 0,
                lastLatencyMs: 0,
                lastOperationMs: 0,
                lastInputType: '',
                lastEventType: ''
            };
        }

        var end = _performanceNow();
        var operationMs = Math.max(0, end - measure.start);
        var event = measure.event;
        var latencyMs = operationMs;
        if (event && typeof event.timeStamp === 'number') {
            var candidate = end - event.timeStamp;
            if (Number.isFinite(candidate) && candidate >= 0 && candidate < 60000) {
                latencyMs = candidate;
            }
        }

        var stats = inst.inputStats;
        var threshold = _readNumberOption(inst, 'inputLongTaskThresholdMs', 'InputLongTaskThresholdMs', 24);
        stats.operationCount++;
        stats.totalLatencyMs += latencyMs;
        stats.totalOperationMs += operationMs;
        stats.maxLatencyMs = Math.max(stats.maxLatencyMs || 0, latencyMs);
        stats.maxOperationMs = Math.max(stats.maxOperationMs || 0, operationMs);
        stats.lastLatencyMs = latencyMs;
        stats.lastOperationMs = operationMs;
        stats.lastInputType = inst.lastInputType || (event && event.inputType) || '';
        stats.lastEventType = measure.eventType || (event && event.type) || '';
        if (operationMs > threshold) {
            stats.longOperationCount++;
        }
    }

    function _handleJsOwnedTextBeforeInput(inst, event, inputType, selection) {
        if (inputType === 'insertText') {
            var text = event.data || '';
            if (!text) return false;

            event.preventDefault();
            event.stopPropagation();
            _beginUndoTransaction(inst, 'typing', 'Type text', selection, false);
            var replacingSelection = selection && !selection.isCollapsed && !selection.IsCollapsed;
            var deleteSelection = replacingSelection ? _createDeleteRangePatchFromSelection(inst, selection) : null;
            var insertSelection = deleteSelection
                ? _collapseSelectionSnapshot(selection, deleteSelection.startOffset)
                : selection;
            var result = _applyInsertText(inst, text);
            if (!result) return true;

            _invalidateMeasureCache(inst);
            var afterSelection = _captureSelectionSnapshot(inst);
            inst.lastSelectionSnapshot = afterSelection;
            _scheduleSelectionNotification(inst, afterSelection);
            inst.jsOwnedInputCount++;
            _markIncrementalRender(inst, 'insertText');

            if (deleteSelection) {
                _flushPendingInputPatch(inst);
                _beginTypingTransaction(inst);
                _dispatchPatch(inst, {
                    type: 'DeleteRange',
                    operationId: _nextRuntimeOperationId(inst),
                    data: deleteSelection.deletedText,
                    deleteLength: deleteSelection.deleteLength,
                    selection: deleteSelection.selection,
                    beforeSelection: selection,
                    afterSelection: insertSelection,
                    transactionId: inst.currentTransactionId,
                    protocolVersion: inst.options.protocolVersion || 1
                });
            }

            _dispatchInputPatch(inst, inputType, text, insertSelection, null, null, afterSelection);
            return true;
        }

        if (inputType === 'deleteContentBackward'
            || inputType === 'deleteWordBackward'
            || inputType === 'deleteContentForward'
            || inputType === 'deleteWordForward') {
            event.preventDefault();
            event.stopPropagation();
            _beginUndoTransaction(inst, 'typing', 'Delete', selection, false);
            var backward = inputType === 'deleteContentBackward' || inputType === 'deleteWordBackward';
            var unit = inputType === 'deleteWordBackward' || inputType === 'deleteWordForward' ? 'word' : 'character';
            var deleteRangePatch = selection && !(selection.isCollapsed ?? selection.IsCollapsed ?? true)
                ? _createDeleteRangePatchFromSelection(inst, selection)
                : null;
            var deleteResult = backward
                ? _applyDeleteBackward(inst, unit)
                : _applyDeleteForward(inst, unit);
            if (!deleteResult) return true;

            _invalidateMeasureCache(inst);
            var afterDeleteSelection = _captureSelectionSnapshot(inst);
            inst.lastSelectionSnapshot = afterDeleteSelection;
            _scheduleSelectionNotification(inst, afterDeleteSelection);
            inst.jsOwnedInputCount++;
            _markIncrementalRender(inst, inputType || 'deleteText');

            if (deleteRangePatch) {
                _flushPendingInputPatch(inst);
                _beginTypingTransaction(inst);
                _dispatchPatch(inst, {
                    type: 'DeleteRange',
                    operationId: _nextRuntimeOperationId(inst),
                    data: deleteRangePatch.deletedText || deleteResult.deletedText || '',
                    deleteLength: deleteRangePatch.deleteLength,
                    selection: deleteRangePatch.selection,
                    beforeSelection: selection,
                    afterSelection: afterDeleteSelection,
                    transactionId: inst.currentTransactionId,
                    protocolVersion: inst.options.protocolVersion || 1
                });
                return true;
            }

            _dispatchInputPatch(inst, inputType, deleteResult.deletedText || null, selection, null, null, afterDeleteSelection);
            return true;
        }

        return false;
    }

    function _createDeleteRangePatchFromSelection(inst, selection) {
        if (!selection) return null;
        var anchorBlockId = selection.anchorBlockId || selection.AnchorBlockId || '';
        var focusBlockId = selection.focusBlockId || selection.FocusBlockId || anchorBlockId;
        var anchorInlineId = selection.anchorInlineId || selection.AnchorInlineId || '';
        var focusInlineId = selection.focusInlineId || selection.FocusInlineId || anchorInlineId;
        if (anchorBlockId !== focusBlockId || anchorInlineId !== focusInlineId) {
            return null;
        }

        var anchorOffset = selection.anchorOffset ?? selection.AnchorOffset ?? 0;
        var focusOffset = selection.focusOffset ?? selection.FocusOffset ?? anchorOffset;
        var start = Math.min(anchorOffset, focusOffset);
        var end = Math.max(anchorOffset, focusOffset);
        var anchorBlockOffset = selection.anchorBlockOffset ?? selection.AnchorBlockOffset ?? anchorOffset;
        var focusBlockOffset = selection.focusBlockOffset ?? selection.FocusBlockOffset ?? focusOffset;
        var blockStart = Math.min(anchorBlockOffset, focusBlockOffset);
        if (end <= start) return null;

        var collapsed = _collapseSelectionSnapshot(selection, start);
        collapsed.anchorBlockOffset = blockStart;
        collapsed.AnchorBlockOffset = blockStart;
        collapsed.focusBlockOffset = blockStart;
        collapsed.FocusBlockOffset = blockStart;
        var deletedText = '';
        var sel = window.getSelection();
        if (sel && sel.rangeCount > 0) {
            deletedText = sel.toString();
        }

        return {
            selection: collapsed,
            startOffset: start,
            deleteLength: end - start,
            deletedText: deletedText
        };
    }

    function _collapseSelectionSnapshot(selection, offset) {
        if (!selection) return null;
        var clone = _cloneRuntimeJson(selection);
        var blockId = clone.anchorBlockId || clone.AnchorBlockId || clone.focusBlockId || clone.FocusBlockId || null;
        var inlineId = clone.anchorInlineId || clone.AnchorInlineId || clone.focusInlineId || clone.FocusInlineId || null;
        clone.anchorBlockId = blockId;
        clone.AnchorBlockId = blockId;
        clone.focusBlockId = blockId;
        clone.FocusBlockId = blockId;
        clone.anchorInlineId = inlineId;
        clone.AnchorInlineId = inlineId;
        clone.focusInlineId = inlineId;
        clone.FocusInlineId = inlineId;
        clone.anchorOffset = offset;
        clone.AnchorOffset = offset;
        clone.focusOffset = offset;
        clone.FocusOffset = offset;
        clone.isCollapsed = true;
        clone.IsCollapsed = true;
        clone.direction = 'forward';
        clone.Direction = 'forward';
        return clone;
    }

    function _handlePendingTypingBeforeInput(inst, event, inputType, selection) {
        if (inputType !== 'insertText' || !event.data || !_hasPendingTypingMarks(inst)) {
            return null;
        }

        _beginUndoTransaction(inst, 'typing', 'Type text', selection, false);
        var result = _applyPendingTypingTextToDom(inst, event.data);
        if (!result) {
            return null;
        }

        event.preventDefault();
        _invalidateMeasureCache(inst);
        _markIncrementalRender(inst, 'pendingTypingInsertText');
        var afterSelection = _captureSelectionSnapshot(inst);
        inst.lastSelectionSnapshot = afterSelection;
        _scheduleSelectionNotification(inst, afterSelection);
        _beginTypingTransaction(inst);

        if (result.insertedInline) {
            _flushPendingInputPatch(inst);
            _dispatchPatch(inst, {
                type: 'InsertInline',
                inline: {
                    $type: 'text',
                    Id: result.inlineId,
                    Text: event.data,
                    Marks: _pendingTypingMarksToInlineMarks(inst)
                },
                selection: selection,
                beforeSelection: selection,
                afterSelection: afterSelection,
                transactionId: inst.currentTransactionId,
                protocolVersion: inst.options.protocolVersion || 1
            });
            return true;
        }

        _dispatchInputPatch(inst, inputType, event.data, selection, null, null, afterSelection);
        return true;
    }

    function _applyPendingTypingTextToDom(inst, text) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || !text) return null;
        var range = sel.getRangeAt(0);
        if (!inst.root.contains(range.commonAncestorContainer)) return null;
        if (!sel.isCollapsed) {
            range.deleteContents();
        }

        var normalized = _normalizeToTextNode(range.startContainer, range.startOffset);
        var inline = _findInlineElement(normalized.node);
        if (inline && inline.getAttribute('data-pending-typing') === 'true' && _inlineMatchesPendingTypingMarks(inline, inst)) {
            var pos = _normalizeToTextNode(range.startContainer, range.startOffset);
            if (pos.node && pos.node.nodeType === Node.TEXT_NODE) {
                var current = pos.node.textContent || '';
                var offset = Math.max(0, Math.min(pos.offset, current.length));
                pos.node.textContent = current.slice(0, offset) + text + current.slice(offset);
                _setCaret(pos.node, offset + text.length);
                return { insertedInline: false, inlineId: inline.getAttribute('data-inline-id') || '' };
            }
        }

        inline = inline || _closestBlockElement(range.startContainer)?.querySelector('[data-inline-id]');
        if (!inline) return null;

        var inlineId = _createInlineId();
        var styled = inline.cloneNode(false);
        styled.setAttribute('data-inline-id', inlineId);
        styled.setAttribute('data-pending-typing', 'true');
        _clearInlineFormatting(styled);
        _applyPendingTypingMarksToElement(styled, inst);
        var textNode = document.createTextNode(text);
        styled.appendChild(textNode);

        if (normalized.node && normalized.node.nodeType === Node.TEXT_NODE && inline.contains(normalized.node)) {
            var source = normalized.node.textContent || '';
            var splitOffset = Math.max(0, Math.min(normalized.offset, source.length));
            var afterText = source.slice(splitOffset);
            normalized.node.textContent = source.slice(0, splitOffset);
            if (afterText) {
                var afterInline = inline.cloneNode(false);
                afterInline.setAttribute('data-inline-id', _createInlineId());
                afterInline.textContent = afterText;
                inline.after(afterInline);
            }
            inline.after(styled);
        } else {
            range.insertNode(styled);
        }

        _setCaret(textNode, text.length);
        return { insertedInline: true, inlineId: inlineId };
    }

    function _hasPendingTypingMarks(inst) {
        return !!inst.pendingTypingMarks && Object.keys(inst.pendingTypingMarks).length > 0;
    }

    function _pendingTypingMarksToInlineMarks(inst) {
        return Object.keys(inst.pendingTypingMarks || {}).map(function (key) {
            var value = inst.pendingTypingMarks[key] || {};
            var mark = { Type: _markTypeToNumber(key) };
            if (key === 'Link' && value.href) {
                mark.Link = { Href: value.href, Title: value.title || undefined };
            }
            if (_isValueMark(key) && value.value) {
                mark.Value = value.value;
            }
            return mark;
        });
    }

    function _applyPendingTypingMarksToElement(el, inst) {
        Object.keys(inst.pendingTypingMarks || {}).forEach(function (key) {
            _applyMarkStyle(el, key, inst.pendingTypingMarks[key]);
        });
    }

    function _inlineMatchesPendingTypingMarks(inline, inst) {
        var state = _getElementMarkState(inline);
        var pending = inst.pendingTypingMarks || {};
        var keys = ['Bold', 'Italic', 'Underline', 'Link', 'FontFamily', 'FontSize', 'TextColor', 'Highlight'];
        return keys.every(function (key) {
            if (!pending[key]) {
                return !state[key];
            }
            if (_isValueMark(key)) {
                return state[key] === pending[key].value;
            }
            return !!state[key];
        });
    }

    function _applyTrackedInsertionToDom(inst, data) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || !data) return null;

        var range = sel.getRangeAt(0);
        if (!inst.root.contains(range.commonAncestorContainer)) return null;

        if (!sel.isCollapsed) {
            range.deleteContents();
        }

        var normalized = _normalizeToTextNode(range.startContainer, range.startOffset);
        var textNode = normalized.node;
        var offset = normalized.offset;
        var existingRevision = _closestRevisionElement(textNode, 'Insertion');
        if (existingRevision) {
            var insertionTextNode = textNode.nodeType === Node.TEXT_NODE && existingRevision.contains(textNode)
                ? textNode
                : _firstDeepTextNode(existingRevision);
            if (!insertionTextNode) {
                insertionTextNode = document.createTextNode('');
                existingRevision.appendChild(insertionTextNode);
                offset = 0;
            }

            var current = insertionTextNode.textContent || '';
            var clampedOffset = Math.max(0, Math.min(offset, current.length));
            insertionTextNode.textContent = current.slice(0, clampedOffset) + data + current.slice(clampedOffset);
            _setCaret(insertionTextNode, clampedOffset + data.length);
            return existingRevision.getAttribute('data-revision-id');
        }

        var inline = _findInlineElement(textNode);
        var revisionId = _createRevisionId();
        var revisionSpan = _createRevisionSpan(revisionId, 'Insertion', data, inline);

        if (inline && textNode.nodeType === Node.TEXT_NODE && inline.contains(textNode)) {
            _splitInlineAroundRevision(inline, textNode, offset, offset, revisionSpan);
        } else {
            range.insertNode(revisionSpan);
        }

        _setCaret(_firstDeepTextNode(revisionSpan), data.length);
        return revisionId;
    }

    function _applyTrackedDeletionToDom(inst, inputType) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return null;

        var range = sel.getRangeAt(0);
        if (!inst.root.contains(range.commonAncestorContainer)) return null;

        var normalized = _normalizeToTextNode(range.startContainer, range.startOffset);
        var textNode = normalized.node;
        if (!textNode || textNode.nodeType !== Node.TEXT_NODE) return null;

        var inline = _findInlineElement(textNode);
        if (!inline) return null;

        var text = textNode.textContent || '';
        var start = normalized.offset;
        var end = normalized.offset;

        if (!sel.isCollapsed) {
            var selectedRange = sel.getRangeAt(0);
            if (selectedRange.startContainer !== selectedRange.endContainer || selectedRange.startContainer !== textNode) {
                return null;
            }

            start = Math.min(selectedRange.startOffset, selectedRange.endOffset);
            end = Math.max(selectedRange.startOffset, selectedRange.endOffset);
        } else if (inputType === 'deleteContentBackward' || inputType === 'deleteWordBackward') {
            var backwardLength = inputType === 'deleteWordBackward' ? _wordBoundaryBackward(text, normalized.offset) : 1;
            start = Math.max(0, normalized.offset - backwardLength);
            end = normalized.offset;
        } else {
            var forwardLength = inputType === 'deleteWordForward' ? _wordBoundaryForward(text, normalized.offset) : 1;
            start = normalized.offset;
            end = Math.min(text.length, normalized.offset + forwardLength);
        }

        if (end <= start) return null;

        var deletedText = text.slice(start, end);
        var revisionId = _createRevisionId();
        var revisionSpan = _createRevisionSpan(revisionId, 'Deletion', deletedText, inline);
        _splitInlineAroundRevision(inline, textNode, start, end, revisionSpan);

        var caretNode = _firstDeepTextNode(inline);
        if (caretNode) {
            _setCaret(caretNode, Math.min(start, caretNode.textContent.length));
        }

        return { text: deletedText, revisionId: revisionId };
    }

    function _applyParagraphBreakToDom(inst) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return null;

        var range = sel.getRangeAt(0);
        if (!inst.root.contains(range.commonAncestorContainer)) return null;

        var block = _closestBlockElement(range.startContainer);
        if (!block) return null;

        var blockTag = block.tagName ? block.tagName.toLowerCase() : '';
        if (blockTag === 'ul' || blockTag === 'ol') {
            return _applyListParagraphBreakToDom(inst, block, range, sel);
        }

        if (!sel.isCollapsed) {
            range.deleteContents();
        }

        var blockId = _createBlockId();
        var inlineId = _createInlineId();
        var newBlock = document.createElement('p');
        newBlock.className = 'tm-wysiwyg-block';
        newBlock.setAttribute('data-block-id', blockId);
        newBlock.style.cssText = block.style.cssText || '';

        var normalized = _normalizeToTextNode(range.startContainer, range.startOffset);
        var sourceInline = _findInlineElement(normalized.node);
        var newInline = sourceInline ? sourceInline.cloneNode(false) : document.createElement('span');
        newInline.setAttribute('data-inline-id', inlineId);
        newInline.removeAttribute('data-revision-id');
        newInline.removeAttribute('data-revision-type');
        newInline.removeAttribute('data-testid');

        var textNode = document.createTextNode('');
        if (normalized.node && normalized.node.nodeType === Node.TEXT_NODE && block.contains(normalized.node)) {
            var text = normalized.node.textContent || '';
            var offset = normalized.offset;
            textNode = document.createTextNode(text.slice(offset));
            normalized.node.textContent = text.slice(0, offset);

            while (normalized.node.nextSibling) {
                newInline.appendChild(normalized.node.nextSibling);
            }
        }

        newInline.insertBefore(textNode, newInline.firstChild);
        if (sourceInline && block.contains(sourceInline)) {
            var next = sourceInline.nextSibling;
            while (next) {
                var moved = next;
                next = next.nextSibling;
                newBlock.appendChild(moved);
            }
        }

        var hasMovedFollowingInlines = !!newBlock.firstChild;
        var hasSplitInlineContent = !!(newInline.textContent || newInline.querySelector('br[data-inline-break]'));
        var shouldInsertSplitInline = hasSplitInlineContent || !hasMovedFollowingInlines;
        if (shouldInsertSplitInline) {
            newBlock.insertBefore(newInline, newBlock.firstChild);
        }

        if (shouldInsertSplitInline && !newInline.textContent && !newInline.querySelector('br[data-inline-break]')) {
            textNode = textNode.parentNode === newInline ? textNode : document.createTextNode('');
            if (!textNode.parentNode) {
                newInline.appendChild(textNode);
            }
            _ensureCaretPlaceholder(newInline);
        }

        block.after(newBlock);
        var caretTarget = shouldInsertSplitInline ? textNode : _firstDeepTextNode(newBlock);
        if (caretTarget) {
            _setCaret(caretTarget, 0);
        }
        return {
            block: {
                Id: blockId,
                Type: 0,
                ParagraphProperties: _serializeParagraphProperties(newBlock, null),
                Content: {
                    $type: 'paragraph',
                    Inlines: _serializeInlines(newBlock)
                }
            }
        };
    }

    function _applyListParagraphBreakToDom(inst, block, range, selection) {
        if (!block || !range) return null;
        var blockTag = block.tagName ? block.tagName.toLowerCase() : 'ul';

        if (!selection.isCollapsed) {
            range.deleteContents();
        }

        var currentLi = (range.startContainer.nodeType === Node.ELEMENT_NODE ? range.startContainer : range.startContainer.parentElement)
            ?.closest?.('li');
        if (!currentLi || !block.contains(currentLi)) {
            currentLi = block.querySelector('li');
        }
        if (!currentLi) return null;

        var normalized = _normalizeToTextNode(range.startContainer, range.startOffset);
        var isEmptyItem = (currentLi.textContent || '').trim().length === 0;
        if (isEmptyItem) {
            var paragraph = _convertListBlockToParagraph(block);
            if (!paragraph) return null;
            block.replaceWith(paragraph);
            var textNode = _firstDeepTextNode(paragraph);
            if (!textNode) {
                var inline = paragraph.querySelector('[data-inline-id]') || paragraph.appendChild(document.createElement('span'));
                if (!inline.getAttribute('data-inline-id')) inline.setAttribute('data-inline-id', _createInlineId());
                textNode = inline.appendChild(document.createTextNode(''));
            }
            _setCaret(textNode, Math.min(textNode.textContent.length, 0));
            return {
                block: _serializeBlock(paragraph, _createBlockMap(_resolveRuntimeDocument(inst) || {}), 0),
                updateCurrentBlock: true
            };
        }

        var blockId = _createBlockId();
        var inlineId = _createInlineId();
        var newList = document.createElement(blockTag);
        _copyBlockShell(block, newList);
        newList.setAttribute('data-block-id', blockId);
        _setRuntimeNodeAttributes(newList, blockId, 'block');

        var newLi = document.createElement('li');
        var sourceInline = _findInlineElement(normalized.node);
        var newInline = sourceInline ? sourceInline.cloneNode(false) : document.createElement('span');
        newInline.setAttribute('data-inline-id', inlineId);
        newInline.removeAttribute('data-revision-id');
        newInline.removeAttribute('data-revision-type');
        newInline.removeAttribute('data-testid');

        var textNode = document.createTextNode('');
        if (normalized.node && normalized.node.nodeType === Node.TEXT_NODE && currentLi.contains(normalized.node)) {
            var text = normalized.node.textContent || '';
            var offset = Math.max(0, Math.min(normalized.offset, text.length));
            textNode = document.createTextNode(text.slice(offset));
            normalized.node.textContent = text.slice(0, offset);

            while (normalized.node.nextSibling) {
                newInline.appendChild(normalized.node.nextSibling);
            }
        }

        newInline.insertBefore(textNode, newInline.firstChild);
        if (sourceInline && currentLi.contains(sourceInline)) {
            var next = sourceInline.nextSibling;
            while (next) {
                var moved = next;
                next = next.nextSibling;
                newLi.appendChild(moved);
            }
        }
        var hasMovedFollowingItems = !!newLi.firstChild;
        var hasSplitListInlineContent = !!(newInline.textContent || newInline.querySelector('br[data-inline-break]'));
        var shouldInsertListSplitInline = hasSplitListInlineContent || !hasMovedFollowingItems;
        if (shouldInsertListSplitInline) {
            newLi.insertBefore(newInline, newLi.firstChild);
        }

        if (shouldInsertListSplitInline && !newInline.textContent && !newInline.querySelector('br[data-inline-break]')) {
            textNode = textNode.parentNode === newInline ? textNode : document.createTextNode('');
            if (!textNode.parentNode) {
                newInline.appendChild(textNode);
            }
            _ensureCaretPlaceholder(newInline);
        }

        newList.appendChild(newLi);
        block.after(newList);
        var listCaretTarget = shouldInsertListSplitInline ? textNode : _firstDeepTextNode(newLi);
        if (listCaretTarget) {
            _setCaret(listCaretTarget, 0);
        }
        return {
            block: {
                Id: blockId,
                Type: 2,
                ParagraphProperties: _serializeParagraphProperties(newList, null),
                Content: {
                    $type: 'list',
                    Ordered: blockTag === 'ol',
                    Inlines: _serializeInlines(newLi)
                }
            }
        };
    }

    function _applySoftBreakToDom(inst) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return false;

        var range = sel.getRangeAt(0);
        if (!inst.root.contains(range.commonAncestorContainer)) return false;
        if (!sel.isCollapsed) {
            range.deleteContents();
        }

        var normalized = _normalizeToTextNode(range.startContainer, range.startOffset);
        if (!normalized.node || normalized.node.nodeType !== Node.TEXT_NODE) return false;

        var text = normalized.node.textContent || '';
        var offset = Math.max(0, Math.min(normalized.offset, text.length));
        var afterText = text.slice(offset);
        normalized.node.textContent = text.slice(0, offset);

        var br = document.createElement('br');
        br.setAttribute('data-inline-break', 'true');
        var afterNode = document.createTextNode(afterText);
        normalized.node.parentNode.insertBefore(br, normalized.node.nextSibling);
        normalized.node.parentNode.insertBefore(afterNode, br.nextSibling);
        if (!afterText) {
            normalized.node.parentNode.insertBefore(_createCaretPlaceholderBreak(), afterNode.nextSibling);
        }
        _setCaret(afterNode, 0);
        return true;
    }

    function _handleStructuralBeforeInput(inst, event, inputType, selection, revisionType) {
        if (inputType !== 'insertParagraph' && inputType !== 'insertLineBreak') {
            return null;
        }

        _commitCurrentRuntimeTransaction(inst, true);
        _beginUndoTransaction(
            inst,
            'structural',
            inputType === 'insertParagraph' ? 'Insert paragraph' : 'Insert line break',
            selection,
            true);
        var result = inputType === 'insertParagraph'
            ? _applyParagraphBreakToDom(inst)
            : (_applySoftBreakToDom(inst) ? { block: null } : null);
        if (!result) return false;

        event.preventDefault();
        _flushPendingInputPatch(inst);
        _invalidateMeasureCache(inst);
        _markIncrementalRender(inst, inputType === 'insertParagraph' ? 'splitParagraph' : 'insertSoftBreak');
        var afterSelection = _captureSelectionSnapshot(inst);
        inst.lastSelectionSnapshot = afterSelection;
        _scheduleSelectionNotification(inst, afterSelection);
        _beginTypingTransaction(inst);
        var isCurrentBlockUpdate = inputType === 'insertParagraph' && result.updateCurrentBlock;
        var patch = {
            type: isCurrentBlockUpdate ? 'UpdateBlock' : (inputType === 'insertParagraph' ? 'SplitBlock' : 'InsertSoftBreak'),
            blockType: isCurrentBlockUpdate ? null : (inputType === 'insertParagraph' ? 'Paragraph' : null),
            block: result.block,
            selection: selection,
            beforeSelection: selection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        };
        if (revisionType) {
            var structuralRevisionId = _createRevisionId();
            patch.revisionId = structuralRevisionId;
            patch.revisionType = revisionType;
            _createRuntimeRevision(
                inst,
                structuralRevisionId,
                'Structure',
                inputType === 'insertParagraph' ? 'Paragraph break' : 'Line break',
                selection,
                afterSelection);
        }

        _dispatchPatch(inst, patch);
        _commitCurrentRuntimeTransaction(inst, true);
        return true;
    }

    function _splitInlineAroundRevision(inline, textNode, start, end, revisionSpan) {
        var text = textNode.textContent || '';
        var before = text.slice(0, start);
        var after = text.slice(end);

        textNode.textContent = before;
        inline.after(revisionSpan);

        if (after) {
            var afterSpan = inline.cloneNode(false);
            afterSpan.setAttribute('data-inline-id', _createInlineId());
            afterSpan.textContent = after;
            revisionSpan.after(afterSpan);
        }
    }

    function _createRevisionSpan(revisionId, revisionType, text, sourceInline) {
        var span = document.createElement('span');
        if (sourceInline) {
            span.style.cssText = sourceInline.style.cssText || '';
        }

        span.setAttribute('data-inline-id', 'rev-' + revisionId);
        span.setAttribute('data-revision-id', revisionId);
        span.setAttribute('data-revision-type', revisionType);
        span.setAttribute('data-testid', revisionType === 'Deletion'
            ? 'document-wysiwyg-revision-delete'
            : 'document-wysiwyg-revision-insert');
        span.className = 'tm-wysiwyg-revision '
            + (revisionType === 'Deletion' ? 'tm-wysiwyg-revision--delete' : 'tm-wysiwyg-revision--insert');
        span.textContent = text || '';
        return span;
    }

    function _closestRevisionElement(node, revisionType) {
        var el = node && node.nodeType === Node.ELEMENT_NODE ? node : node?.parentElement;
        if (!el || !el.closest) return null;
        var revision = el.closest('.tm-wysiwyg-revision[data-revision-id]');
        if (!revision) return null;
        return revision.getAttribute('data-revision-type') === revisionType ? revision : null;
    }

    function _findInlineElement(node) {
        var el = node && node.nodeType === Node.ELEMENT_NODE ? node : node?.parentElement;
        return el && el.closest ? el.closest('[data-inline-id]') : null;
    }

    function _closestBlockElement(node) {
        var el = node && node.nodeType === Node.ELEMENT_NODE ? node : node?.parentElement;
        return el && el.closest ? el.closest('.tm-wysiwyg-block[data-block-id]') : null;
    }

    function _createRevisionId() {
        if (window.crypto && typeof window.crypto.randomUUID === 'function') {
            return window.crypto.randomUUID().replace(/-/g, '');
        }

        return 'rev' + Date.now().toString(36) + Math.random().toString(36).slice(2);
    }

    function _createInlineId() {
        return 'inline' + Date.now().toString(36) + Math.random().toString(36).slice(2);
    }

    function _createBlockId() {
        return 'block' + Date.now().toString(36) + Math.random().toString(36).slice(2);
    }

    function _createTableCellId() {
        return 'tc' + Date.now().toString(36) + Math.random().toString(36).slice(2);
    }

    function _ensureEditableSelection(inst, target) {
        var sel = window.getSelection();
        var hasUsableTextSelection = false;
        if (sel
            && sel.rangeCount > 0
            && _nodeBelongsToRoot(sel.anchorNode, inst.root)
            && (!target || target.contains(sel.anchorNode))) {
            var mapped = _mapNodeToBlockInline(sel.anchorNode, sel.anchorOffset, inst.root);
            var region = _resolveSelectionRegion(sel.anchorNode, inst.root);
            hasUsableTextSelection = !!(mapped
                && mapped.blockId
                && mapped.inlineId
                && region
                && region.region !== 'Image');
        }

        if (sel
            && sel.rangeCount > 0
            && _nodeBelongsToRoot(sel.anchorNode, inst.root)
            && (!target || target.contains(sel.anchorNode))
            && hasUsableTextSelection) {
            return;
        }

        var editable = target && target.closest ? target.closest('[contenteditable="true"]') : null;
        if (!editable || !inst.root.contains(editable)) {
            editable = inst.root.querySelector('.tm-wysiwyg-page__body[contenteditable="true"]');
        }
        if (!editable) return;

        var textRoot = _findEditableTextRoot(target, editable) || editable;
        var textNode = _firstDeepTextNode(textRoot);
        if (textNode) {
            _setCaret(textNode, textNode.textContent.length);
            return;
        }

        var block = editable.querySelector('[data-block-id]');
        if (block) {
            var blockRange = document.createRange();
            blockRange.selectNodeContents(block);
            blockRange.collapse(false);
            sel = window.getSelection();
            if (!sel) return;
            sel.removeAllRanges();
            sel.addRange(blockRange);
            return;
        }

        var range = document.createRange();
        range.selectNodeContents(editable);
        range.collapse(false);
        sel = window.getSelection();
        if (!sel) return;
        sel.removeAllRanges();
        sel.addRange(range);
    }

    function _findEditableTextRoot(target, editable) {
        var element = target && target.nodeType === Node.ELEMENT_NODE
            ? target
            : target && target.parentElement;
        var block = element && element.closest
            ? element.closest('.tm-wysiwyg-block[data-block-id]:not(figure):not(table):not(hr)')
            : null;
        if (block && editable.contains(block) && block.querySelector('[data-inline-id]')) {
            return block;
        }

        var blocks = editable.querySelectorAll('.tm-wysiwyg-block[data-block-id]:not(figure):not(table):not(hr)');
        for (var i = 0; i < blocks.length; i++) {
            if (blocks[i].querySelector('[data-inline-id]')) {
                return blocks[i];
            }
        }

        return null;
    }

    function _onInput(inst, event) {
        var measure = _beginInputMeasure(inst, event, 'input');
        try {
            if (inst.readOnly) {
                event.stopPropagation();
                return;
            }

            if (inst.compositionActive) return;
            if (_shouldSuppressBrowserInputEvent(inst, event.inputType)) {
                return;
            }
            _invalidateMeasureCache(inst);
            inst.nativeInputCount++;

            const inputType = event.inputType;
            const data = event.data || (!inputType && event.type === 'compositionend' ? inst.compositionText : null);
            inst.lastInputType = inputType || null;
            inst.lastInputDataLength = data ? data.length : 0;

            if (!inputType && data) {
                var selectionBeforeComposition = inst.lastSelectionSnapshot || _captureSelectionSnapshot(inst);
                var afterCompositionSelection = _captureSelectionSnapshot(inst);
                _markIncrementalRender(inst, 'compositionText');
                _dispatchInputPatch(inst, 'insertText', data, selectionBeforeComposition, null, null, afterCompositionSelection);
                return;
            }

            const selection = inst.pendingNativeInputSelection || _captureSelectionSnapshot(inst);
            inst.pendingNativeInputSelection = null;
            if (!inst.pendingUndoTransaction && data) {
                _beginNativeUndoTransaction(inst, inputType || 'insertText', data, selection);
            }
            if (inst.nativeInputTimer) {
                clearTimeout(inst.nativeInputTimer);
            }
            inst.nativeInputTimer = setTimeout(function () {
                inst.acceptingNativeInput = false;
                inst.nativeInputTimer = null;
                _scheduleRemoteQueueFlush(inst);
            }, 0);

            const afterSelection = _captureSelectionSnapshot(inst);
            inst.lastSelectionSnapshot = afterSelection;
            _scheduleSelectionNotification(inst, afterSelection);

            _markIncrementalRender(inst, inputType || 'nativeInput');
            _dispatchInputPatch(inst, inputType, data, selection, null, null, afterSelection);
        } finally {
            _endInputMeasure(measure);
        }
    }

    function _beginNativeUndoTransaction(inst, inputType, data, afterNativeSelection) {
        if (!inst || inst.pendingUndoTransaction) return;
        var beforeHtml = inst.lastCommittedHtml || inst.root.innerHTML;
        var transactionId = inst.currentTransactionId || ('txn-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7));
        if (!inst.currentTransactionId) {
            inst.currentTransactionId = transactionId;
        }

        inst.pendingUndoTransaction = {
            transactionId: transactionId,
            source: 'native-input',
            description: inputType && inputType.indexOf('delete') === 0 ? 'Delete' : 'Type text',
            beforeHtml: beforeHtml,
            afterHtml: null,
            beforeSelection: _cloneRuntimeJson(inst.lastSelectionSnapshot || afterNativeSelection),
            afterSelection: null,
            operations: [{
                operationId: transactionId + '-native',
                type: inputType || 'insertText',
                data: data || ''
            }],
            inverseOperations: [],
            createdAt: new Date().toISOString(),
            epoch: inst.runtimeUndoEpoch || 0
        };
        _notifyUndoStateChanged(inst);
    }

    function _handleTrackedBeforeInput(inst, event, inputType, selection) {
        if (inputType === 'insertText') {
            var insertData = event.data || '';
            if (!insertData) return false;

            _beginUndoTransaction(inst, 'typing', 'Type text', selection, false);
            var insertionRevisionId = _applyTrackedInsertionToDom(inst, insertData);
            if (!insertionRevisionId) return false;

            event.preventDefault();
            _invalidateMeasureCache(inst);
            _markIncrementalRender(inst, 'trackedInsertText');
            var afterInsertSelection = _captureSelectionSnapshot(inst);
            inst.lastSelectionSnapshot = afterInsertSelection;
            _scheduleSelectionNotification(inst, afterInsertSelection);
            if ((inst.runtimeRevisions || []).some(function (revision) { return revision.Id === insertionRevisionId; })) {
                _appendRuntimeRevisionPayload(inst, insertionRevisionId, insertData);
            } else {
                _createRuntimeRevision(inst, insertionRevisionId, 'Insertion', insertData, selection, afterInsertSelection);
            }
            _dispatchInputPatch(inst, inputType, insertData, selection, insertionRevisionId, 'Insertion', afterInsertSelection);
            return true;
        }

        if (inputType === 'deleteContentBackward'
            || inputType === 'deleteContentForward'
            || inputType === 'deleteWordBackward'
            || inputType === 'deleteWordForward') {
            _beginUndoTransaction(inst, 'typing', 'Delete', selection, false);
            var deletion = _applyTrackedDeletionToDom(inst, inputType);
            if (!deletion) return false;

            event.preventDefault();
            _invalidateMeasureCache(inst);
            _markIncrementalRender(inst, inputType || 'trackedDeleteText');
            var afterDeleteSelection = _captureSelectionSnapshot(inst);
            inst.lastSelectionSnapshot = afterDeleteSelection;
            _scheduleSelectionNotification(inst, afterDeleteSelection);
            _createRuntimeRevision(inst, deletion.revisionId, 'Deletion', deletion.text, selection, afterDeleteSelection);
            _dispatchInputPatch(inst, inputType, deletion.text, selection, deletion.revisionId, 'Deletion', afterDeleteSelection);
            return true;
        }

        if (inputType === 'insertParagraph' || inputType === 'insertLineBreak') {
            return _handleStructuralBeforeInput(inst, event, inputType, selection, inputType === 'insertParagraph' ? 'Structural' : null);
        }

        return false;
    }

    function _dispatchInputPatch(inst, inputType, data, selection, revisionId, revisionType, afterSelection) {
        _beginTypingTransaction(inst);
        var operationId = _nextRuntimeOperationId(inst);
        inst.lastInputOperationId = operationId;
        var patch = {
            type: _mapInputTypeToPatchType(inputType),
            operationId: operationId,
            epoch: inst.runtimeUndoEpoch || 0,
            data: data,
            selection: selection,
            beforeSelection: selection,
            afterSelection: afterSelection || _captureSelectionSnapshot(inst),
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1,
        };
        if (revisionId) {
            patch.revisionId = revisionId;
            patch.revisionType = revisionType || '';
        }

        if (patch.type === 'InsertText' && data) {
            _queueInsertTextPatch(inst, patch);
            _syncAutocompleteTrigger(inst, patch.afterSelection);
            return;
        }

        _flushPendingInputPatch(inst);
        _dispatchPatch(inst, patch);
        _syncAutocompleteTrigger(inst, patch.afterSelection);
    }

    function _queueInsertTextPatch(inst, patch) {
        var pending = inst.pendingInputPatch;
        if (pending && _canMergeInsertTextPatches(pending, patch)) {
            pending.data = (pending.data || '') + (patch.data || '');
            pending.Data = pending.data;
            pending.afterSelection = patch.afterSelection || patch.AfterSelection || pending.afterSelection;
            pending.AfterSelection = pending.afterSelection;
            return _schedulePendingInputPatchFlush(inst);
        }

        _flushPendingInputPatch(inst);
        inst.pendingInputPatch = patch;
        _schedulePendingInputPatchFlush(inst);
    }

    function _schedulePendingInputPatchFlush(inst) {
        if (inst.pendingInputPatchTimer) {
            clearTimeout(inst.pendingInputPatchTimer);
        }

        inst.pendingInputPatchTimer = setTimeout(function () {
            _flushPendingInputPatch(inst);
        }, 90);

        if (!inst.pendingInputPatchMaxTimer) {
            inst.pendingInputPatchMaxTimer = setTimeout(function () {
                _flushPendingInputPatch(inst);
            }, 350);
        }
    }

    function _flushPendingInputPatch(inst) {
        if (inst.pendingInputPatchTimer) {
            clearTimeout(inst.pendingInputPatchTimer);
            inst.pendingInputPatchTimer = null;
        }
        if (inst.pendingInputPatchMaxTimer) {
            clearTimeout(inst.pendingInputPatchMaxTimer);
            inst.pendingInputPatchMaxTimer = null;
        }

        var patch = inst.pendingInputPatch;
        inst.pendingInputPatch = null;
        if (patch) {
            if ((patch.epoch ?? patch.Epoch ?? 0) !== (inst.runtimeUndoEpoch || 0)) {
                return;
            }
            _dispatchPatch(inst, patch);
        }
    }

    function _canMergeInsertTextPatches(pending, next) {
        if (!pending || !next) return false;
        if (pending.type !== 'InsertText' || next.type !== 'InsertText') return false;
        if (pending.transactionId !== next.transactionId) return false;

        var pendingRevisionId = pending.revisionId || pending.RevisionId || '';
        var nextRevisionId = next.revisionId || next.RevisionId || '';
        if (pendingRevisionId || nextRevisionId) {
            return pendingRevisionId === nextRevisionId;
        }

        var pendingSelection = pending.selection || {};
        var nextSelection = next.selection || {};
        var pendingBlockId = pendingSelection.anchorBlockId || pendingSelection.AnchorBlockId || '';
        var nextBlockId = nextSelection.anchorBlockId || nextSelection.AnchorBlockId || '';
        var pendingInlineId = pendingSelection.anchorInlineId || pendingSelection.AnchorInlineId || '';
        var nextInlineId = nextSelection.anchorInlineId || nextSelection.AnchorInlineId || '';
        if (pendingBlockId !== nextBlockId || pendingInlineId !== nextInlineId) return false;

        var pendingOffset = pendingSelection.anchorOffset ?? pendingSelection.AnchorOffset ?? 0;
        var nextOffset = nextSelection.anchorOffset ?? nextSelection.AnchorOffset ?? 0;
        return nextOffset === pendingOffset + (pending.data || '').length;
    }

    function _suppressBrowserInputEvent(inst, inputType) {
        inst.suppressInputType = inputType;
        inst.suppressInputUntil = Date.now() + 100;
    }

    function _shouldSuppressBrowserInputEvent(inst, inputType) {
        if (!inst.suppressInputType) return false;
        if (Date.now() > inst.suppressInputUntil) {
            inst.suppressInputType = null;
            inst.suppressInputUntil = 0;
            return null;
        }
        if (inst.suppressInputType !== inputType) return false;

        inst.suppressInputType = null;
        inst.suppressInputUntil = 0;
        return selection;
    }

    function _dispatchPatch(inst, patch) {
        if (patch && (patch.epoch ?? patch.Epoch) == null) {
            patch.epoch = inst.runtimeUndoEpoch || 0;
        }
        if (patch && (patch.epoch ?? patch.Epoch ?? 0) !== (inst.runtimeUndoEpoch || 0)) {
            return;
        }
        _invalidateMeasureCache(inst);
        _appendUndoOperation(inst, patch);
        _transformRuntimeCommentAnchorsForPatch(inst, patch);
        _transformRuntimeMarkersForPatch(inst, patch);
        inst.pendingLocalSnapshotSkips++;
        inst.lastPatchType = patch.type || patch.Type || null;
        inst.lastPatchId = patch.patchId || patch.PatchId || patch.operationId || patch.OperationId || null;
        inst.lastPatchTransactionId = patch.transactionId || patch.TransactionId || null;
        inst.lastPatchAt = new Date().toISOString();
        _invokeDotNet(inst, 'HandlePatchGenerated', patch);
        _scheduleRemoteQueueFlush(inst);
    }

    // ─── Search marker helpers ────────────────────────────────────────────────

    function _clearSearchMarkers(inst) {
        inst._searchMarkers = [];
        _removeRuntimeMarkersByType(inst, ['search', 'searchActive']);
    }

    function _applySearchMarker(inst, marker) {
        var normalized = _createSearchRuntimeMarker(marker, marker.index || 0);
        _upsertRuntimeMarker(inst, normalized, true);
    }

    function _setSearchMarkers(inst, blockIdsOrMarkers, offsets, lengths) {
        _clearSearchMarkers(inst);
        var markers = _normalizeSearchMarkerInput(blockIdsOrMarkers, offsets, lengths);
        inst._searchMarkers = markers;
        markers.forEach(function (marker, index) {
            marker.index = index;
            _applySearchMarker(inst, marker);
        });
    }

    function _scrollToSearchResult(inst, blockId, offset, length) {
        var targetMarker = null;
        _getRuntimeMarkersByType(inst, 'search').concat(_getRuntimeMarkersByType(inst, 'searchActive')).forEach(function (marker) {
            var range = marker.range || marker.Range || {};
            if (String(range.startBlockId || range.StartBlockId || '') === String(blockId || '')
                && Number(range.startOffset ?? range.StartOffset ?? 0) === Number(offset || 0)
                && Number((range.endOffset ?? range.EndOffset ?? 0) - (range.startOffset ?? range.StartOffset ?? 0)) === Number(length || 0)) {
                targetMarker = marker;
            }
        });

        if (targetMarker) {
            _setActiveSearchMarker(inst, targetMarker.id || targetMarker.Id);
        }

        var mark = inst.root.querySelector('.tm-wysiwyg-search-match--active');
        if (!mark && targetMarker) {
            var targetRange = targetMarker.range || targetMarker.Range || {};
            var targetBlockId = targetRange.startBlockId || targetRange.StartBlockId || blockId || '';
            var pageIndex = _findVirtualPageIndexForBlock(inst, targetBlockId);
            if (pageIndex >= 0) {
                _scrollVirtualPageToIndex(inst, pageIndex);
                _setActiveSearchMarker(inst, targetMarker.id || targetMarker.Id);
                mark = inst.root.querySelector('.tm-wysiwyg-search-match--active');
            }
        }

        if (mark) {
            mark.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        }
    }

    function _setActiveSearchMarker(inst, markerId) {
        if (!inst || !markerId) return;
        var markers = _getRuntimeMarkersByType(inst, 'search').concat(_getRuntimeMarkersByType(inst, 'searchActive'));
        markers.forEach(function (marker) {
            marker.type = String(marker.id || marker.Id) === String(markerId) ? 'searchActive' : 'search';
            marker.Type = marker.type;
        });
        _clearMarkerDecorations(inst, function (marker) {
            var type = _normalizeRuntimeMarkerType(marker.type || marker.Type);
            return type === 'search' || type === 'searchActive';
        });
        markers.forEach(function (marker) { _renderRuntimeMarker(inst, marker); });
    }

    function _normalizeSearchMarkerInput(blockIdsOrMarkers, offsets, lengths) {
        if (Array.isArray(blockIdsOrMarkers) && blockIdsOrMarkers.length > 0 && typeof blockIdsOrMarkers[0] === 'object') {
            return blockIdsOrMarkers.map(function (marker, index) {
                return {
                    id: marker.id || marker.Id || ('search-' + index),
                    blockId: marker.blockId || marker.BlockId || '',
                    offset: marker.offset ?? marker.Offset ?? marker.startOffset ?? marker.StartOffset ?? 0,
                    length: marker.length ?? marker.Length ?? Math.max(0, (marker.endOffset ?? marker.EndOffset ?? 0) - (marker.startOffset ?? marker.StartOffset ?? 0)),
                    active: !!(marker.active ?? marker.Active ?? index === 0)
                };
            });
        }

        var blockIds = Array.isArray(blockIdsOrMarkers) ? blockIdsOrMarkers : [];
        return blockIds.map(function (blockId, index) {
            return {
                id: 'search-' + index,
                blockId: blockId,
                offset: Array.isArray(offsets) ? (offsets[index] || 0) : 0,
                length: Array.isArray(lengths) ? (lengths[index] || 0) : 0,
                active: index === 0
            };
        });
    }

    function _createSearchRuntimeMarker(marker, index) {
        var offset = Number(marker.offset ?? marker.Offset ?? 0);
        var length = Number(marker.length ?? marker.Length ?? 0);
        return {
            id: marker.id || marker.Id || ('search-' + index),
            type: marker.active ? 'searchActive' : 'search',
            range: {
                startBlockId: marker.blockId || marker.BlockId || '',
                startOffset: offset,
                endBlockId: marker.blockId || marker.BlockId || '',
                endOffset: offset + Math.max(0, length)
            },
            affectsData: false,
            priority: marker.active ? 30 : 10,
            source: 'transient',
            label: marker.label || marker.Label || ''
        };
    }

    // ─── Autocomplete trigger helpers ───────────────────────────────────────

    function _detectAutocompleteTriggerText(text, caretOffset) {
        var source = String(text || '');
        var offset = Math.max(0, Math.min(Number(caretOffset || 0), source.length));
        var before = source.slice(0, offset);
        var match = /(^|[\s([{])(\{\{|@|#|\/)([^\s{}@#\/]*)$/.exec(before);
        if (!match) return null;

        var marker = match[2];
        var query = match[3] || '';
        var markerStart = offset - marker.length - query.length;
        var triggerId = marker === '{{'
            ? 'token'
            : marker === '@'
                ? 'mention'
                : marker === '#'
                    ? 'tag'
                    : 'slash';
        var markerType = marker === '{{'
            ? 'tokenQuery'
            : marker === '@'
                ? 'mentionQuery'
                : marker === '#'
                    ? 'tagQuery'
                    : 'slashQuery';

        return {
            triggerId: triggerId,
            marker: marker,
            markerType: markerType,
            query: query,
            startOffset: markerStart,
            endOffset: offset
        };
    }

    function _syncAutocompleteTrigger(inst, selection) {
        if (!inst || inst.readOnly || inst.disposed) return;
        var snapshot = selection || _captureSelectionSnapshot(inst);
        if (!snapshot || snapshot.isCollapsed === false || snapshot.IsCollapsed === false) {
            _closeAutocompleteQuery(inst, true);
            return;
        }

        var blockId = snapshot.anchorBlockId || snapshot.AnchorBlockId || snapshot.focusBlockId || snapshot.FocusBlockId || '';
        var caretOffset = snapshot.anchorBlockOffset ?? snapshot.AnchorBlockOffset ?? snapshot.anchorOffset ?? snapshot.AnchorOffset ?? 0;
        if (!blockId) {
            _closeAutocompleteQuery(inst, true);
            return;
        }

        var block = inst.root && inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        if (!block) {
            _closeAutocompleteQuery(inst, true);
            return;
        }

        var detected = _detectAutocompleteTriggerText(block.textContent || '', caretOffset);
        if (!detected) {
            _closeAutocompleteQuery(inst, true);
            return;
        }

        var key = [detected.triggerId, blockId, detected.startOffset, detected.endOffset, detected.query].join('|');
        if (inst.activeAutocompleteKey === key) return;
        inst.activeAutocompleteKey = key;
        inst.activeAutocomplete = {
            triggerId: detected.triggerId,
            marker: detected.marker,
            query: detected.query,
            blockId: blockId,
            startOffset: detected.startOffset,
            endOffset: detected.endOffset
        };

        _upsertRuntimeMarker(inst, {
            id: 'autocomplete-query',
            type: detected.markerType,
            range: {
                startBlockId: blockId,
                startOffset: detected.startOffset,
                endBlockId: blockId,
                endOffset: detected.endOffset
            },
            affectsData: false,
            priority: 35,
            source: 'autocomplete',
            label: detected.marker
        }, false);

        _invokeDotNet(inst, 'HandleAutocompleteTriggerRequested', {
            TriggerId: detected.triggerId,
            Marker: detected.marker,
            Query: detected.query,
            BlockId: blockId,
            StartOffset: detected.startOffset,
            EndOffset: detected.endOffset
        });
    }

    function _closeAutocompleteQuery(inst, notify) {
        if (!inst) return;
        var hadActive = !!inst.activeAutocompleteKey;
        inst.activeAutocompleteKey = null;
        inst.activeAutocomplete = null;
        _removeRuntimeMarker(inst, 'autocomplete-query');
        if (notify && hadActive) {
            _invokeDotNet(inst, 'HandleAutocompleteClosed');
        }
    }

    function _removeAutocompleteQuery(inst) {
        if (!inst || !inst.activeAutocomplete || !inst.root) {
            _closeAutocompleteQuery(inst, false);
            return null;
        }

        var active = inst.activeAutocomplete;
        var blockId = active.blockId || active.BlockId || '';
        var start = Number(active.startOffset ?? active.StartOffset ?? 0);
        var end = Number(active.endOffset ?? active.EndOffset ?? start);
        var block = blockId ? inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]') : null;
        if (!block || end <= start) {
            _closeAutocompleteQuery(inst, false);
            return null;
        }

        var startPos = _resolveTextPosition(block, start);
        var endPos = _resolveTextPosition(block, end);
        if (!startPos || !endPos) {
            _closeAutocompleteQuery(inst, false);
            return null;
        }

        var startInfo = _mapNodeToBlockInline(startPos.node, startPos.offset, inst.root);
        if (!startInfo) {
            _closeAutocompleteQuery(inst, false);
            return null;
        }

        var removedText = '';
        inst._applyingOwnPatch = true;
        try {
            var range = document.createRange();
            range.setStart(startPos.node, startPos.offset);
            range.setEnd(endPos.node, endPos.offset);
            removedText = range.toString();
            range.deleteContents();
            _setCaret(startPos.node, Math.min(startPos.offset, (startPos.node.textContent || '').length));
        } finally {
            inst._applyingOwnPatch = false;
        }

        var selection = {
            region: 'Body',
            anchorBlockId: blockId,
            focusBlockId: blockId,
            anchorInlineId: startInfo.inlineId,
            focusInlineId: startInfo.inlineId,
            anchorOffset: startInfo.offset,
            focusOffset: startInfo.offset,
            anchorBlockOffset: start,
            focusBlockOffset: start,
            isCollapsed: true,
            direction: 'forward'
        };
        inst.lastSelectionSnapshot = selection;
        _scheduleSelectionNotification(inst, selection);
        _closeAutocompleteQuery(inst, false);

        _dispatchPatch(inst, {
            type: 'DeleteRange',
            operationId: _nextRuntimeOperationId(inst),
            epoch: inst.runtimeUndoEpoch || 0,
            data: removedText,
            deleteLength: Math.max(0, end - start),
            selection: selection,
            beforeSelection: selection,
            afterSelection: selection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });

        return selection;
    }

    function _insertAutocompleteText(inst, text) {
        text = String(text || '');
        if (!inst || inst.disposed || inst.readOnly || !text) return false;
        var selection = _captureSelectionSnapshot(inst);
        if (!selection) return false;

        _beginUndoTransaction(inst, 'autocomplete', 'Insert autocomplete text', selection, false);
        var result = _applyInsertText(inst, text);
        if (!result) return false;

        _invalidateMeasureCache(inst);
        var afterSelection = _captureSelectionSnapshot(inst);
        inst.lastSelectionSnapshot = afterSelection;
        _scheduleSelectionNotification(inst, afterSelection);
        _markIncrementalRender(inst, 'autocompleteText');
        _dispatchInputPatch(inst, 'insertText', text, selection, null, null, afterSelection);
        _flushPendingInputPatch(inst);
        _commitCurrentRuntimeTransaction(inst, true);
        return true;
    }

    function _getSearchReplaceMarkers(inst, payload, all) {
        var markers = _getRuntimeMarkersByType(inst, 'searchActive').concat(_getRuntimeMarkersByType(inst, 'search'));
        if (!all) {
            var markerId = payload && (payload.markerId || payload.MarkerId || payload.id || payload.Id);
            if (markerId) {
                var byId = markers.find(function (marker) {
                    return String(marker.id || marker.Id || '') === String(markerId);
                });
                if (byId) return [byId];
            }

            var active = markers.find(function (marker) {
                return _normalizeRuntimeMarkerType(marker.type || marker.Type) === 'searchActive';
            });
            if (active) return [active];
        }

        var blockId = payload && (payload.blockId || payload.BlockId);
        var offset = payload && (payload.offset ?? payload.Offset);
        var length = payload && (payload.length ?? payload.Length);
        if (!all && blockId != null && offset != null && length != null) {
            return [{
                id: payload.markerId || payload.MarkerId || 'search-payload',
                type: 'searchActive',
                range: {
                    startBlockId: String(blockId),
                    startOffset: Number(offset) || 0,
                    endBlockId: String(blockId),
                    endOffset: (Number(offset) || 0) + (Number(length) || 0)
                }
            }];
        }

        return all ? markers.slice() : [];
    }

    function _resolveTextRangeInBlock(block, startOffset, endOffset) {
        var nodes = _collectTextNodes(block);
        var current = 0;
        var start = null;
        var end = null;

        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            var length = (node.nodeValue || '').length;
            var next = current + length;

            if (!start && startOffset >= current && startOffset <= next) {
                start = { node: node, offset: startOffset - current };
            }

            if (!end && endOffset >= current && endOffset <= next) {
                end = { node: node, offset: endOffset - current };
                break;
            }

            current = next;
        }

        if (!start && nodes.length > 0 && startOffset === current) {
            start = { node: nodes[nodes.length - 1], offset: (nodes[nodes.length - 1].nodeValue || '').length };
        }
        if (!end && start) {
            end = { node: start.node, offset: start.offset };
        }

        return start && end ? { start: start, end: end } : null;
    }

    function _selectInsertedReplacement(node, text) {
        var selection = window.getSelection && window.getSelection();
        if (!selection || !document.createRange) return;
        var range = document.createRange();
        range.setStart(node, 0);
        range.setEnd(node, (text || '').length);
        selection.removeAllRanges();
        selection.addRange(range);
    }

    function _replaceMarkerTextRange(inst, marker, replacement, tracked) {
        var rangeInfo = marker && (marker.range || marker.Range);
        if (!rangeInfo) return false;
        var blockId = rangeInfo.startBlockId || rangeInfo.StartBlockId || '';
        var startOffset = Number(rangeInfo.startOffset ?? rangeInfo.StartOffset ?? 0);
        var endOffset = Number(rangeInfo.endOffset ?? rangeInfo.EndOffset ?? startOffset);
        if (!blockId || endOffset < startOffset) return false;

        var block = inst.root.querySelector('[data-block-id="' + _cssEscape(String(blockId)) + '"]');
        if (!block) return false;

        var resolved = _resolveTextRangeInBlock(block, startOffset, endOffset);
        if (!resolved || !document.createRange) return false;

        var domRange = document.createRange();
        domRange.setStart(resolved.start.node, resolved.start.offset);
        domRange.setEnd(resolved.end.node, resolved.end.offset);
        var originalText = domRange.toString();
        var replacementText = replacement == null ? '' : String(replacement);
        var beforeSelection = {
            anchorBlockId: blockId,
            focusBlockId: blockId,
            anchorOffset: startOffset,
            focusOffset: endOffset,
            isCollapsed: startOffset === endOffset
        };

        var insertedTextNode;
        if (tracked && (originalText || replacementText)) {
            var deletionRevisionId = originalText ? _createRevisionId() : null;
            var insertionRevisionId = replacementText ? _createRevisionId() : null;
            var fragment = document.createDocumentFragment();

            if (deletionRevisionId) {
                fragment.appendChild(_createRevisionSpan(deletionRevisionId, 'Deletion', originalText, resolved.start.node.parentElement));
            }
            if (insertionRevisionId) {
                var insertion = _createRevisionSpan(insertionRevisionId, 'Insertion', replacementText, resolved.start.node.parentElement);
                fragment.appendChild(insertion);
                insertedTextNode = _firstDeepTextNode(insertion) || insertion.firstChild;
            }

            domRange.deleteContents();
            domRange.insertNode(fragment);

            var afterTrackedSelection = {
                anchorBlockId: blockId,
                focusBlockId: blockId,
                anchorOffset: startOffset,
                focusOffset: startOffset + replacementText.length,
                isCollapsed: replacementText.length === 0
            };
            if (deletionRevisionId) {
                _createRuntimeRevision(inst, deletionRevisionId, 'Deletion', originalText, beforeSelection, afterTrackedSelection);
            }
            if (insertionRevisionId) {
                _createRuntimeRevision(inst, insertionRevisionId, 'Insertion', replacementText, beforeSelection, afterTrackedSelection);
            }
        } else {
            domRange.deleteContents();
            insertedTextNode = document.createTextNode(replacementText);
            domRange.insertNode(insertedTextNode);
        }

        if (!insertedTextNode) {
            insertedTextNode = document.createTextNode('');
            block.appendChild(insertedTextNode);
        }
        _selectInsertedReplacement(insertedTextNode, replacementText);
        _invalidateMeasureCache(inst);
        _markIncrementalRender(inst, 'replaceText');
        inst.lastCommittedHtml = inst.root.innerHTML;
        var afterSelection = _captureSelectionSnapshot(inst) || {
            anchorBlockId: blockId,
            focusBlockId: blockId,
            anchorOffset: startOffset,
            focusOffset: startOffset + replacementText.length,
            isCollapsed: replacementText.length === 0
        };
        inst.lastSelectionSnapshot = afterSelection;
        inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(afterSelection);
        _scheduleSelectionNotification(inst, afterSelection);
        _appendUndoOperation(inst, {
            type: 'ReplaceText',
            operationId: _nextRuntimeOperationId(inst),
            blockId: blockId,
            offset: startOffset,
            length: Math.max(0, endOffset - startOffset),
            data: replacementText,
            originalText: originalText,
            selection: beforeSelection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1,
            epoch: inst.runtimeUndoEpoch || 0
        });
        return true;
    }

    function _executeReplaceOneCommand(inst, payload) {
        payload = payload || {};
        var markers = _getSearchReplaceMarkers(inst, payload, false);
        if (markers.length === 0) return false;

        _commitCurrentRuntimeTransaction(inst, true);
        _beginUndoTransaction(inst, 'replace', 'Replace', _captureSelectionSnapshot(inst), true);
        var replaced = _replaceMarkerTextRange(inst, markers[0], payload.replacement ?? payload.Replacement ?? '', !!inst.trackChangesEnabled);
        _clearSearchMarkers(inst);
        _commitCurrentRuntimeTransaction(inst, true);
        return replaced;
    }

    function _executeReplaceAllCommand(inst, payload) {
        payload = payload || {};
        var markers = _getSearchReplaceMarkers(inst, payload, true);
        if (markers.length === 0) return false;

        markers.sort(function (a, b) {
            var ar = a.range || a.Range || {};
            var br = b.range || b.Range || {};
            var ab = String(ar.startBlockId || ar.StartBlockId || '');
            var bb = String(br.startBlockId || br.StartBlockId || '');
            if (ab !== bb) return ab < bb ? 1 : -1;
            return Number(br.startOffset ?? br.StartOffset ?? 0) - Number(ar.startOffset ?? ar.StartOffset ?? 0);
        });

        _commitCurrentRuntimeTransaction(inst, true);
        _beginUndoTransaction(inst, 'replace', 'Replace all', _captureSelectionSnapshot(inst), true);
        var count = 0;
        markers.forEach(function (marker) {
            if (_replaceMarkerTextRange(inst, marker, payload.replacement ?? payload.Replacement ?? '', !!inst.trackChangesEnabled)) {
                count++;
            }
        });
        _clearSearchMarkers(inst);
        _commitCurrentRuntimeTransaction(inst, true);
        return count > 0;
    }

    function _collectTextNodes(el) {
        var result = [];
        var walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT, null);
        var node;
        while ((node = walker.nextNode())) {
            result.push(node);
        }
        return result;
    }

    function _wrapTextRange(textNode, start, end, active) {
        if (start >= end || !textNode.nodeValue) return;
        var text = textNode.nodeValue;
        var before = text.slice(0, start);
        var match = text.slice(start, end);
        var after = text.slice(end);

        var mark = document.createElement('mark');
        mark.className = 'tm-wysiwyg-search-match' + (active ? ' tm-wysiwyg-search-match--active' : '');
        mark.textContent = match;

        var parent = textNode.parentNode;
        if (!parent) return;

        if (before) parent.insertBefore(document.createTextNode(before), textNode);
        parent.insertBefore(mark, textNode);
        if (after) parent.insertBefore(document.createTextNode(after), textNode);
        parent.removeChild(textNode);
    }

    function _ensureRuntimeMarkerStore(inst) {
        if (!inst.markerStore) {
            inst.markerStore = new Map();
        }
        return inst.markerStore;
    }

    function _normalizeRuntimeMarker(marker) {
        if (!marker) return null;
        var id = marker.id || marker.Id || '';
        var range = marker.range || marker.Range || {};
        if (!id) {
            id = 'marker-' + Date.now().toString(36) + Math.random().toString(36).slice(2);
        }

        var startBlockId = range.startBlockId || range.StartBlockId || marker.blockId || marker.BlockId || '';
        var startOffset = Number(range.startOffset ?? range.StartOffset ?? marker.offset ?? marker.Offset ?? 0);
        var endBlockId = range.endBlockId || range.EndBlockId || startBlockId;
        var endOffset = Number(range.endOffset ?? range.EndOffset ?? (startOffset + Number(marker.length ?? marker.Length ?? 0)));
        return {
            id: String(id),
            Id: String(id),
            type: _normalizeRuntimeMarkerType(marker.type || marker.Type || 'search'),
            Type: _normalizeRuntimeMarkerType(marker.type || marker.Type || 'search'),
            range: {
                startBlockId: String(startBlockId || ''),
                StartBlockId: String(startBlockId || ''),
                startInlineId: range.startInlineId || range.StartInlineId || null,
                StartInlineId: range.startInlineId || range.StartInlineId || null,
                startInlineIndex: range.startInlineIndex ?? range.StartInlineIndex ?? null,
                StartInlineIndex: range.startInlineIndex ?? range.StartInlineIndex ?? null,
                startOffset: Math.max(0, startOffset || 0),
                StartOffset: Math.max(0, startOffset || 0),
                endBlockId: String(endBlockId || startBlockId || ''),
                EndBlockId: String(endBlockId || startBlockId || ''),
                endInlineId: range.endInlineId || range.EndInlineId || null,
                EndInlineId: range.endInlineId || range.EndInlineId || null,
                endInlineIndex: range.endInlineIndex ?? range.EndInlineIndex ?? null,
                EndInlineIndex: range.endInlineIndex ?? range.EndInlineIndex ?? null,
                endOffset: Math.max(0, endOffset || 0),
                EndOffset: Math.max(0, endOffset || 0)
            },
            affectsData: !!(marker.affectsData ?? marker.AffectsData),
            AffectsData: !!(marker.affectsData ?? marker.AffectsData),
            priority: Number(marker.priority ?? marker.Priority ?? _defaultRuntimeMarkerPriority(marker.type || marker.Type)),
            Priority: Number(marker.priority ?? marker.Priority ?? _defaultRuntimeMarkerPriority(marker.type || marker.Type)),
            source: marker.source || marker.Source || 'localRuntime',
            Source: marker.source || marker.Source || 'localRuntime',
            targetId: marker.targetId || marker.TargetId || null,
            TargetId: marker.targetId || marker.TargetId || null,
            label: marker.label || marker.Label || '',
            Label: marker.label || marker.Label || '',
            status: marker.status || marker.Status || '',
            Status: marker.status || marker.Status || '',
            metadata: marker.metadata || marker.Metadata || {},
            Metadata: marker.metadata || marker.Metadata || {}
        };
    }

    function _normalizeRuntimeMarkerType(type) {
        var value = String(type || '').replace(/[_\s-]/g, '').toLowerCase();
        switch (value) {
            case 'searchactive': return 'searchActive';
            case 'comment': return 'comment';
            case 'revisioninsertion':
            case 'insertionrevision': return 'revisionInsertion';
            case 'revisiondeletion':
            case 'deletionrevision': return 'revisionDeletion';
            case 'revisionformatting':
            case 'formattingrevision': return 'revisionFormatting';
            case 'remoteselection':
            case 'remotecursor': return 'remoteSelection';
            case 'restrictedregion': return 'restrictedRegion';
            case 'mentionquery': return 'mentionQuery';
            case 'tokenquery': return 'tokenQuery';
            case 'tagquery': return 'tagQuery';
            case 'slashquery': return 'slashQuery';
            default: return 'search';
        }
    }

    function _defaultRuntimeMarkerPriority(type) {
        switch (_normalizeRuntimeMarkerType(type)) {
            case 'revisionDeletion':
            case 'revisionInsertion':
            case 'revisionFormatting': return 80;
            case 'comment': return 60;
            case 'remoteSelection': return 50;
            case 'restrictedRegion': return 40;
            case 'searchActive': return 30;
            case 'search': return 10;
            default: return 0;
        }
    }

    function _upsertRuntimeMarker(inst, marker, render) {
        var normalized = _normalizeRuntimeMarker(marker);
        if (!inst || !normalized) return null;
        _ensureRuntimeMarkerStore(inst).set(normalized.id, normalized);
        if (render !== false) {
            _clearMarkerDecorations(inst, function (candidate) { return String(candidate.id || candidate.Id) === normalized.id; });
            _renderRuntimeMarker(inst, normalized);
        }
        return normalized;
    }

    function _removeRuntimeMarker(inst, markerId) {
        if (!inst || !markerId) return false;
        var store = _ensureRuntimeMarkerStore(inst);
        var removed = store.delete(String(markerId));
        _clearMarkerDecorations(inst, function (marker) { return String(marker.id || marker.Id) === String(markerId); });
        return removed;
    }

    function _removeRuntimeMarkersByType(inst, types) {
        if (!inst) return;
        types = (types || []).map(_normalizeRuntimeMarkerType);
        var store = _ensureRuntimeMarkerStore(inst);
        Array.from(store.values()).forEach(function (marker) {
            if (types.indexOf(_normalizeRuntimeMarkerType(marker.type || marker.Type)) >= 0) {
                store.delete(marker.id || marker.Id);
            }
        });
        _clearMarkerDecorations(inst, function (marker) {
            return types.indexOf(_normalizeRuntimeMarkerType(marker.type || marker.Type)) >= 0;
        });
    }

    function _getRuntimeMarkers(inst) {
        return Array.from(_ensureRuntimeMarkerStore(inst).values()).sort(_compareRuntimeMarkers);
    }

    function _getRuntimeMarkersByType(inst, type) {
        var normalizedType = _normalizeRuntimeMarkerType(type);
        return _getRuntimeMarkers(inst).filter(function (marker) {
            return _normalizeRuntimeMarkerType(marker.type || marker.Type) === normalizedType;
        });
    }

    function _getRuntimeMarkersByBlock(inst, blockId) {
        return _getRuntimeMarkers(inst).filter(function (marker) {
            var range = marker.range || marker.Range || {};
            return String(range.startBlockId || range.StartBlockId || '') === String(blockId || '')
                || String(range.endBlockId || range.EndBlockId || '') === String(blockId || '');
        });
    }

    function _getOverlappingRuntimeMarkers(inst, range) {
        var normalizedRange = _normalizeRuntimeMarker({ id: 'range', range: range }).range;
        return _getRuntimeMarkers(inst).filter(function (marker) {
            return _runtimeMarkerRangesOverlap(marker.range || marker.Range || {}, normalizedRange);
        });
    }

    function _compareRuntimeMarkers(a, b) {
        var ap = Number(a.priority ?? a.Priority ?? 0);
        var bp = Number(b.priority ?? b.Priority ?? 0);
        if (bp !== ap) return bp - ap;
        return String(a.id || a.Id || '').localeCompare(String(b.id || b.Id || ''));
    }

    function _runtimeMarkerRangesOverlap(a, b) {
        var aStartBlock = String(a.startBlockId || a.StartBlockId || '');
        var aEndBlock = String(a.endBlockId || a.EndBlockId || aStartBlock);
        var bStartBlock = String(b.startBlockId || b.StartBlockId || '');
        var bEndBlock = String(b.endBlockId || b.EndBlockId || bStartBlock);
        if (aStartBlock !== aEndBlock || bStartBlock !== bEndBlock || aStartBlock !== bStartBlock) {
            return aStartBlock === bStartBlock || aStartBlock === bEndBlock || aEndBlock === bStartBlock || aEndBlock === bEndBlock;
        }

        var aStart = Number(a.startOffset ?? a.StartOffset ?? 0);
        var aEnd = Number(a.endOffset ?? a.EndOffset ?? aStart);
        var bStart = Number(b.startOffset ?? b.StartOffset ?? 0);
        var bEnd = Number(b.endOffset ?? b.EndOffset ?? bStart);
        return aStart < bEnd && bStart < aEnd;
    }

    function _renderRuntimeMarkers(inst) {
        if (!inst) return;
        _clearMarkerDecorations(inst);
        _getRuntimeMarkers(inst).forEach(function (marker) { _renderRuntimeMarker(inst, marker); });
    }

    function _renderRuntimeMarker(inst, marker) {
        if (!inst || !inst.root || !marker) return false;
        var metricStart = _performanceNow();
        var rendered = false;
        var rangeData = marker.range || marker.Range || {};
        var blockId = rangeData.startBlockId || rangeData.StartBlockId || '';
        var start = Number(rangeData.startOffset ?? rangeData.StartOffset ?? 0);
        var end = Number(rangeData.endOffset ?? rangeData.EndOffset ?? start);
        if (!blockId || end <= start) {
            _recordMarkerRenderMetric(inst, metricStart, false);
            return false;
        }
        var block = inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        if (!block) {
            _recordMarkerRenderMetric(inst, metricStart, false);
            return false;
        }
        if (_isEmbeddedRevisionAlreadyRendered(inst, marker)) {
            _recordMarkerRenderMetric(inst, metricStart, false);
            return false;
        }

        var inlineRange = _resolveRuntimeMarkerInlineRange(block, rangeData);
        var startPos = inlineRange ? inlineRange.start : _resolveTextPosition(block, start);
        var endPos = inlineRange ? inlineRange.end : _resolveTextPosition(block, end);
        if (!startPos || !endPos) {
            _recordMarkerRenderMetric(inst, metricStart, false);
            return false;
        }
        var range = document.createRange();
        try {
            range.setStart(startPos.node, startPos.offset);
            range.setEnd(endPos.node, endPos.offset);
        } catch {
            _recordMarkerRenderMetric(inst, metricStart, false);
            return false;
        }
        if (range.collapsed) {
            _recordMarkerRenderMetric(inst, metricStart, false);
            return false;
        }

        var wrapper = _createRuntimeMarkerElement(marker);
        try {
            wrapper.appendChild(range.extractContents());
            range.insertNode(wrapper);
            rendered = true;
        } catch {
            _recordMarkerRenderMetric(inst, metricStart, false);
            return false;
        }
        _recordMarkerRenderMetric(inst, metricStart, rendered);
        return rendered;
    }

    function _isEmbeddedRevisionAlreadyRendered(inst, marker) {
        if (!inst || !inst.root || !marker) return false;
        var type = _normalizeRuntimeMarkerType(marker.type || marker.Type);
        if (type.indexOf('revision') !== 0) return false;
        var targetId = marker.targetId || marker.TargetId || '';
        if (!targetId) return false;
        return !!inst.root.querySelector('[data-revision-id="' + _cssEscape(targetId) + '"]:not([data-marker-id])');
    }

    function _resolveRuntimeMarkerInlineRange(block, rangeData) {
        if (!block || !rangeData) return null;
        var startInlineIndex = rangeData.startInlineIndex ?? rangeData.StartInlineIndex;
        var endInlineIndex = rangeData.endInlineIndex ?? rangeData.EndInlineIndex ?? startInlineIndex;
        if (startInlineIndex == null || endInlineIndex == null) return null;

        startInlineIndex = Number(startInlineIndex);
        endInlineIndex = Number(endInlineIndex);
        if (!Number.isFinite(startInlineIndex) || !Number.isFinite(endInlineIndex)) return null;

        var inlines = Array.from(block.querySelectorAll(':scope > [data-inline-id]'));
        var startInline = inlines[Math.max(0, Math.floor(startInlineIndex))];
        var endInline = inlines[Math.max(0, Math.floor(endInlineIndex))];
        if (!startInline || !endInline) return null;

        var startOffset = Math.max(0, Number(rangeData.startOffset ?? rangeData.StartOffset ?? 0) || 0);
        var endOffset = Math.max(0, Number(rangeData.endOffset ?? rangeData.EndOffset ?? startOffset) || 0);
        var start = _resolveTextPosition(startInline, startOffset);
        var end = _resolveTextPosition(endInline, endOffset);
        return start && end ? { start: start, end: end } : null;
    }

    function _createRuntimeMarkerElement(marker) {
        var type = _normalizeRuntimeMarkerType(marker.type || marker.Type);
        var el = type === 'search' || type === 'searchActive'
            ? document.createElement('mark')
            : document.createElement('span');
        el.className = _runtimeMarkerClassName(type, marker);
        el.setAttribute('data-marker-id', marker.id || marker.Id || '');
        el.setAttribute('data-marker-type', type);
        el.setAttribute('data-testid', _runtimeMarkerTestId(type));
        if (type === 'comment' && (marker.targetId || marker.TargetId)) {
            el.setAttribute('data-comment-id', marker.targetId || marker.TargetId);
        }
        if (type.indexOf('revision') === 0 && (marker.targetId || marker.TargetId)) {
            el.setAttribute('data-revision-id', marker.targetId || marker.TargetId);
        }
        return el;
    }

    function _runtimeMarkerClassName(type, marker) {
        switch (_normalizeRuntimeMarkerType(type)) {
            case 'searchActive': return 'tm-wysiwyg-marker tm-wysiwyg-marker--search tm-wysiwyg-marker--search-active tm-wysiwyg-search-match tm-wysiwyg-search-match--active';
            case 'search': return 'tm-wysiwyg-marker tm-wysiwyg-marker--search tm-wysiwyg-search-match';
            case 'comment': {
                var status = marker && (marker.status || marker.Status || (marker.metadata || marker.Metadata || {}).status || '');
                return 'tm-wysiwyg-marker tm-wysiwyg-marker--comment tm-document-inline--comment-anchor'
                    + (String(status).toLowerCase() === 'resolved' || status === 1 ? ' tm-document-inline--comment-anchor--resolved' : '');
            }
            case 'revisionInsertion': return 'tm-wysiwyg-marker tm-wysiwyg-marker--revision-insert tm-wysiwyg-revision--insert';
            case 'revisionDeletion': return 'tm-wysiwyg-marker tm-wysiwyg-marker--revision-delete tm-wysiwyg-revision--delete';
            case 'revisionFormatting': return 'tm-wysiwyg-marker tm-wysiwyg-marker--revision-format tm-wysiwyg-revision--format';
            case 'remoteSelection': return 'tm-wysiwyg-marker tm-wysiwyg-marker--remote-selection';
            case 'restrictedRegion': return 'tm-wysiwyg-marker tm-wysiwyg-marker--restricted-region';
            case 'mentionQuery': return 'tm-wysiwyg-marker tm-wysiwyg-marker--mention-query';
            case 'tokenQuery': return 'tm-wysiwyg-marker tm-wysiwyg-marker--token-query';
            case 'tagQuery': return 'tm-wysiwyg-marker tm-wysiwyg-marker--tag-query';
            case 'slashQuery': return 'tm-wysiwyg-marker tm-wysiwyg-marker--slash-query';
            default: return 'tm-wysiwyg-marker';
        }
    }

    function _runtimeMarkerTestId(type) {
        switch (_normalizeRuntimeMarkerType(type)) {
            case 'searchActive': return 'document-search-marker-active';
            case 'search': return 'document-search-marker';
            case 'comment': return 'document-comment-marker';
            case 'revisionInsertion':
            case 'revisionDeletion':
            case 'revisionFormatting': return 'document-revision-marker';
            case 'remoteSelection': return 'document-remote-selection-marker';
            case 'restrictedRegion': return 'document-restricted-region-marker';
            case 'mentionQuery': return 'document-mention-query-marker';
            case 'tokenQuery': return 'document-token-query-marker';
            case 'tagQuery': return 'document-tag-query-marker';
            case 'slashQuery': return 'document-slash-query-marker';
            default: return 'document-marker';
        }
    }

    function _clearMarkerDecorations(inst, predicate) {
        if (!inst || !inst.root) return;
        Array.from(inst.root.querySelectorAll('[data-marker-id]')).forEach(function (node) {
            if (predicate) {
                var marker = {
                    id: node.getAttribute('data-marker-id') || '',
                    type: node.getAttribute('data-marker-type') || ''
                };
                if (!predicate(marker)) return;
            }
            _unwrapElement(node);
        });
    }

    function _transformRuntimeMarkersForPatch(inst, patch) {
        var change = _getTextChangeFromPatch(patch);
        if (!change) return;
        _transformRuntimeMarkersForTextChange(inst, change.blockId, change.offset, change.length, change.isDelete);
    }

    function _transformRuntimeMarkersForTextChange(inst, blockId, offset, length, isDelete) {
        if (!inst || !blockId || !Number.isFinite(offset) || !Number.isFinite(length) || length <= 0) return;
        var changed = false;
        _getRuntimeMarkersByBlock(inst, blockId).forEach(function (marker) {
            var range = marker.range || marker.Range || {};
            var start = Number(range.startOffset ?? range.StartOffset ?? 0);
            var end = Number(range.endOffset ?? range.EndOffset ?? start);
            if (isDelete) {
                var deleteEnd = offset + length;
                if (start >= deleteEnd) {
                    start -= length;
                    end -= length;
                } else if (end > offset) {
                    end = Math.max(offset, end - Math.min(length, Math.max(0, deleteEnd - start)));
                    if (start > offset) start = offset;
                }
            } else {
                if (start >= offset) {
                    start += length;
                    end += length;
                } else if (end >= offset) {
                    end += length;
                }
            }
            range.startOffset = range.StartOffset = Math.max(0, start);
            range.endOffset = range.EndOffset = Math.max(range.startOffset, end);
            changed = true;
        });
        if (changed) {
            _renderRuntimeMarkers(inst);
        }
    }

    function _onPaste(inst, event) {
        if (inst.readOnly) return;
        _invalidateMeasureCache(inst);

        event.preventDefault();

        const clipboardData = event.clipboardData;
        const imageFile = _getClipboardImageFile(clipboardData);
        if (imageFile) {
            _uploadAndInsertImageFile(inst, imageFile, _captureSelectionSnapshot(inst));
            return;
        }

        const html = clipboardData.getData('text/html');
        const plain = clipboardData.getData('text/plain');
        if ((!html || !html.trim()) && plain && !/[\r\n]/.test(plain) && !_isClipboardUrlOnlyText(plain)) {
            if (inst.trackChangesEnabled) {
                var trackedPasteSelection = _captureSelectionSnapshot(inst);
                var trackedPasteEvent = {
                    data: plain,
                    inputType: 'insertText',
                    preventDefault: function () { },
                    stopPropagation: function () { }
                };
                if (_handleTrackedBeforeInput(inst, trackedPasteEvent, 'insertText', trackedPasteSelection)) {
                    return;
                }
            }

            _commitCurrentRuntimeTransaction(inst, true);
            var beforeSelection = _captureSelectionSnapshot(inst);
            _beginUndoTransaction(inst, 'paste', 'Paste', beforeSelection, true);
            var result = _applyInsertText(inst, plain);
            if (result) {
                var afterSelection = _captureSelectionSnapshot(inst);
                inst.lastSelectionSnapshot = afterSelection;
                _scheduleSelectionNotification(inst, afterSelection);
                inst.jsOwnedInputCount++;
                _dispatchInputPatch(inst, 'insertText', plain, beforeSelection, null, null, afterSelection);
                _commitCurrentRuntimeTransaction(inst, true);
            }
            return;
        }

        _insertClipboardBlocksFromPipeline(inst, html, plain);
    }

    function _isClipboardUrlOnlyText(text) {
        var value = String(text || '').trim();
        return /^(https?:\/\/|mailto:|tel:)[^\s]+$/i.test(value);
    }

    function _insertClipboardBlocksFromPipeline(inst, html, plain) {
        var beforeSelection = _captureSelectionSnapshot(inst);
        _commitCurrentRuntimeTransaction(inst, true);
        _beginUndoTransaction(inst, 'paste', 'Paste', beforeSelection, true);

        if (inst.dotNetRef) {
            inst.dotNetRef.invokeMethodAsync('HandleClipboardPasteRequested', html || '', plain || '')
                .then(function (blocksJson) {
                    var normalizationStart = _performanceNow();
                    var blocks = _normalizeInsertionBlocksForSchema(inst, JSON.parse(blocksJson), _getActiveSchemaRegion(inst));
                    _recordClipboardNormalizationMetric(inst, normalizationStart);
                    if (blocks && blocks.length > 0) {
                        _insertClipboardBlocks(inst, blocks, beforeSelection);
                    }
                    _commitCurrentRuntimeTransaction(inst, true);
                })
                .catch(function () {
                    var normalizationStart = _performanceNow();
                    var blocks = html && html.trim()
                        ? _parseClipboardHtml(html)
                        : _parsePlainTextPaste(plain);
                    blocks = _normalizeInsertionBlocksForSchema(inst, blocks, _getActiveSchemaRegion(inst));
                    _recordClipboardNormalizationMetric(inst, normalizationStart);
                    _insertClipboardBlocks(inst, blocks, beforeSelection);
                    _commitCurrentRuntimeTransaction(inst, true);
                });
        } else {
            var normalizationStart = _performanceNow();
            var blocks = html && html.trim()
                ? _parseClipboardHtml(html)
                : _parsePlainTextPaste(plain);
            blocks = _normalizeInsertionBlocksForSchema(inst, blocks, _getActiveSchemaRegion(inst));
            _recordClipboardNormalizationMetric(inst, normalizationStart);
            _insertClipboardBlocks(inst, blocks, beforeSelection);
            _commitCurrentRuntimeTransaction(inst, true);
        }
    }

    function _onCopy(inst, event) {
        if (inst.readOnly) return;
        var payload = _serializeSelectionForClipboard(inst);
        if (!payload) return;

        if (event.clipboardData) {
            event.preventDefault();
            event.clipboardData.setData('text/html', payload.html);
            event.clipboardData.setData('text/plain', payload.plain);
            return;
        }

        _writeClipboardPayload(payload);
    }

    function _getClipboardImageFile(clipboardData) {
        if (!clipboardData) return null;

        var items = Array.from(clipboardData.items || []);
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            if (item.kind === 'file' && item.type && item.type.indexOf('image/') === 0) {
                return item.getAsFile();
            }
        }

        var files = Array.from(clipboardData.files || []);
        for (var f = 0; f < files.length; f++) {
            if (files[f].type && files[f].type.indexOf('image/') === 0) {
                return files[f];
            }
        }

        return null;
    }

    function _uploadAndInsertImageFile(inst, file, selection) {
        if (!file) return;

        var placeholder = _insertImageUploadPlaceholder(inst, file, selection);
        var reader = new FileReader();
        reader.onload = function () {
            var result = String(reader.result || '');
            var commaIndex = result.indexOf(',');
            var base64 = commaIndex >= 0 ? result.slice(commaIndex + 1) : result;
            var payload = {
                source: 2, // Clipboard
                fileName: file.name || 'clipboard-image',
                contentType: file.type || 'image/png',
                sizeBytes: file.size || 0,
                base64Data: base64,
                altText: file.name || 'clipboard-image',
                selection: selection
            };

            _invokeDotNetResult(inst, 'HandleImageUploadRequested', payload).then(function (block) {
                if (block) {
                    if (placeholder && placeholder.parentElement) {
                        placeholder.remove();
                    }
                    _insertImageBlock(inst, block, true);
                } else {
                    _markImageUploadPlaceholderFailed(inst, placeholder, file, selection);
                }
            }).catch(function () {
                _markImageUploadPlaceholderFailed(inst, placeholder, file, selection);
            });
        };
        reader.onerror = function () {
            _markImageUploadPlaceholderFailed(inst, placeholder, file, selection);
        };
        reader.readAsDataURL(file);
    }

    function _insertImageUploadPlaceholder(inst, file, selection) {
        var block = document.createElement('figure');
        block.className = 'tm-wysiwyg-image tm-wysiwyg-image--uploading';
        block.setAttribute('data-testid', 'document-wysiwyg-image-upload-placeholder');
        block.setAttribute('data-image-upload-file', file && file.name ? file.name : 'image');
        block.setAttribute('aria-busy', 'true');
        var status = document.createElement('span');
        status.className = 'tm-wysiwyg-image__upload-status';
        status.textContent = 'Uploading image...';
        block.appendChild(status);

        var sel = window.getSelection();
        var anchorBlock = null;
        if (sel && sel.rangeCount > 0) {
            var node = sel.anchorNode;
            var el = node && (node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement);
            anchorBlock = el ? el.closest('.tm-wysiwyg-block[data-block-id]') : null;
        }
        if (!anchorBlock && selection && (selection.anchorBlockId || selection.AnchorBlockId)) {
            var blockId = selection.anchorBlockId || selection.AnchorBlockId;
            anchorBlock = inst.root.querySelector('[data-block-id="' + String(blockId).replace(/"/g, '\\"') + '"]');
        }

        if (anchorBlock && anchorBlock.parentElement) {
            anchorBlock.parentElement.insertBefore(block, anchorBlock.nextSibling);
        } else {
            var body = inst.root.querySelector('.tm-wysiwyg-page__body') || inst.root;
            body.appendChild(block);
        }
        return block;
    }

    function _markImageUploadPlaceholderFailed(inst, placeholder, file, selection) {
        if (!placeholder || !placeholder.parentElement) return;
        placeholder.classList.remove('tm-wysiwyg-image--uploading');
        placeholder.classList.add('tm-wysiwyg-image--upload-error');
        placeholder.setAttribute('data-testid', 'document-wysiwyg-image-upload-error');
        placeholder.setAttribute('aria-busy', 'false');
        placeholder.textContent = '';
        var status = document.createElement('span');
        status.className = 'tm-wysiwyg-image__upload-status';
        status.textContent = 'Image upload failed.';
        placeholder.appendChild(status);
        var retry = document.createElement('button');
        retry.type = 'button';
        retry.className = 'tm-wysiwyg-image__upload-action';
        retry.setAttribute('data-testid', 'document-wysiwyg-image-upload-retry');
        retry.textContent = (inst.options && inst.options.imageRetryLabel) || 'Retry';
        retry.addEventListener('click', function () {
            placeholder.remove();
            _uploadAndInsertImageFile(inst, file, selection);
        });
        var remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'tm-wysiwyg-image__upload-action';
        remove.setAttribute('data-testid', 'document-wysiwyg-image-upload-remove');
        remove.textContent = 'Remove';
        remove.addEventListener('click', function () { placeholder.remove(); });
        placeholder.appendChild(retry);
        placeholder.appendChild(remove);
    }

    function _parsePlainTextPaste(plain) {
        if (!plain || !String(plain).length) return [];
        return String(plain)
            .replace(/\r\n/g, '\n')
            .replace(/\r/g, '\n')
            .split(/\n+/)
            .map(function (text) { return text.trim(); })
            .filter(function (text) { return text.length > 0; })
            .map(function (text) {
                return _createTextBlock(0, { $type: 'paragraph', Inlines: [_createTextInline(text)] });
            });
    }

    function _parseClipboardHtml(html) {
        var parser = new DOMParser();
        var doc = parser.parseFromString(_extractOfficeHtmlFragment(html), 'text/html');
        doc.querySelectorAll('script,style,iframe,object,embed,meta,link').forEach(function (el) { el.remove(); });
        var root = doc.body;
        var blocks = [];
        Array.from(root.children).forEach(function (child) {
            _appendClipboardElementBlocks(blocks, child);
        });

        if (blocks.length === 0) {
            blocks = _parsePlainTextPaste(root.textContent || '');
        }

        return blocks;
    }

    function _extractOfficeHtmlFragment(html) {
        var startMarker = '<!--StartFragment-->';
        var endMarker = '<!--EndFragment-->';
        var start = html.indexOf(startMarker);
        var end = html.indexOf(endMarker);
        if (start >= 0 && end > start) {
            return html.slice(start + startMarker.length, end);
        }

        return html;
    }

    function _appendClipboardElementBlocks(blocks, element) {
        var tag = element.tagName.toLowerCase();
        if (tag === 'p' || tag === 'div') {
            var headingLevel = _getClipboardHeadingLevel(element);
            if (headingLevel > 0) {
                blocks.push(_createTextBlock(1, { $type: 'heading', Level: headingLevel, Inlines: _readClipboardInlines(element) }));
            } else if (_hasMeaningfulClipboardText(element)) {
                blocks.push(_createTextBlock(0, { $type: 'paragraph', Inlines: _readClipboardInlines(element) }));
            } else {
                Array.from(element.children).forEach(function (child) { _appendClipboardElementBlocks(blocks, child); });
            }
            return;
        }

        if (/^h[1-6]$/.test(tag)) {
            blocks.push(_createTextBlock(1, {
                $type: 'heading',
                Level: parseInt(tag[1], 10),
                Inlines: _readClipboardInlines(element)
            }));
            return;
        }

        if (tag === 'ul' || tag === 'ol') {
            Array.from(element.children)
                .filter(function (child) { return child.tagName && child.tagName.toLowerCase() === 'li'; })
                .forEach(function (li) {
                    blocks.push(_createTextBlock(2, {
                        $type: 'list',
                        Ordered: tag === 'ol',
                        Inlines: _readClipboardInlines(li)
                    }));
                });
            return;
        }

        if (tag === 'blockquote') {
            blocks.push(_createTextBlock(3, { $type: 'quote', Inlines: _readClipboardInlines(element) }));
            return;
        }

        if (tag === 'table') {
            blocks.push(_createTextBlock(4, _readClipboardTable(element)));
            return;
        }

        if (tag === 'img') {
            var src = element.getAttribute('src') || '';
            if (_isSafeImageUrl(src)) {
                blocks.push(_createTextBlock(5, {
                    $type: 'image',
                    Source: 0,
                    Url: src,
                    AltText: element.getAttribute('alt') || ''
                }));
            }
            return;
        }

        Array.from(element.children).forEach(function (child) { _appendClipboardElementBlocks(blocks, child); });
    }

    function _readClipboardTable(table) {
        var rows = [];
        Array.from(table.querySelectorAll('tr')).forEach(function (tr) {
            var cells = [];
            Array.from(tr.children)
                .filter(function (cell) {
                    var tag = cell.tagName && cell.tagName.toLowerCase();
                    return tag === 'td' || tag === 'th';
                })
                .forEach(function (cell) {
                    var cellBlocks = [];
                    Array.from(cell.children).forEach(function (child) {
                        _appendClipboardElementBlocks(cellBlocks, child);
                    });
                    if (cellBlocks.length === 0) {
                        cellBlocks.push(_createTextBlock(0, {
                            $type: 'paragraph',
                            Inlines: [_createTextInline(_normalizeClipboardText(cell.textContent || ''))]
                        }));
                    }
                    cells.push({
                        Id: _newId('cell'),
                        ColumnSpan: Math.max(1, parseInt(cell.getAttribute('colspan') || '1', 10) || 1),
                        RowSpan: Math.max(1, parseInt(cell.getAttribute('rowspan') || '1', 10) || 1),
                        Blocks: cellBlocks
                    });
                });
            if (cells.length > 0) rows.push({ Cells: cells });
        });

        return { $type: 'table', Rows: rows };
    }

    function _readClipboardInlines(element, inheritedMarks) {
        inheritedMarks = inheritedMarks || [];
        var inlines = [];
        Array.from(element.childNodes).forEach(function (node) {
            if (node.nodeType === Node.TEXT_NODE) {
                var text = _normalizeClipboardText(node.textContent || '');
                if (text) inlines.push(_createTextInline(text, inheritedMarks));
                return;
            }

            if (node.nodeType !== Node.ELEMENT_NODE) return;
            var child = node;
            if (child.tagName.toLowerCase() === 'br') {
                inlines.push(_createTextInline('\n', inheritedMarks));
                return;
            }

            if ((child.className || '').indexOf('tm-wysiwyg-field') >= 0 && child.hasAttribute('data-field-type')) {
                inlines.push({
                    $type: 'field',
                    Id: _newId('field'),
                    FieldType: _normalizeDocumentFieldType(child.getAttribute('data-field-type')),
                    Format: child.getAttribute('data-field-format') || undefined,
                    FallbackText: child.getAttribute('data-field-fallback') || child.textContent || undefined,
                    DisplayText: child.textContent || ''
                });
                return;
            }

            var marks = inheritedMarks.slice();
            var mark = _clipboardMarkFromElement(child);
            if (mark) marks.push(mark);
            inlines = inlines.concat(_readClipboardInlines(child, marks));
        });

        return inlines.length > 0 ? inlines : [_createTextInline('')];
    }

    function _clipboardMarkFromElement(element) {
        var tag = element.tagName.toLowerCase();
        var style = element.getAttribute('style') || '';
        if (tag === 'strong' || tag === 'b' || /font-weight\s*:\s*(bold|[7-9]00)/i.test(style)) {
            return { Type: 0 };
        }
        if (tag === 'em' || tag === 'i' || /font-style\s*:\s*italic/i.test(style)) {
            return { Type: 1 };
        }
        if (tag === 'u' || /text-decoration[^;]*underline/i.test(style)) {
            return { Type: 2 };
        }
        if (tag === 's' || tag === 'strike' || tag === 'del' || /text-decoration[^;]*line-through/i.test(style)) {
            return { Type: 3 };
        }
        if (tag === 'a') {
            var href = element.getAttribute('href') || '';
            if (/^(https?:|mailto:)/i.test(href)) return { Type: 6, Link: { Href: href } };
        }
        return null;
    }

    function _getClipboardHeadingLevel(element) {
        var className = element.className || '';
        var style = element.getAttribute('style') || '';
        var text = [className, style].join(' ');
        var match = text.match(/heading\s*([1-6])/i) || text.match(/mso-style-name:\s*['"]?Heading\s*([1-6])/i);
        return match ? parseInt(match[1], 10) : 0;
    }

    function _hasMeaningfulClipboardText(element) {
        return _normalizeClipboardText(element.textContent || '').length > 0;
    }

    function _normalizeClipboardText(text) {
        return String(text || '').replace(/\u00a0/g, ' ').replace(/[ \t\f\v]+/g, ' ').trim();
    }

    function _createTextBlock(type, content) {
        return {
            Id: _newId('paste'),
            Type: type,
            Order: 0,
            Content: content
        };
    }

    function _createTextInline(text, marks) {
        var inline = {
            $type: 'text',
            Id: _newId('inline'),
            Text: text || ''
        };
        if (marks && marks.length > 0) {
            inline.Marks = marks.map(function (mark) {
                return JSON.parse(JSON.stringify(mark));
            });
        }
        return inline;
    }

    function _getActiveSchemaRegion(inst) {
        var snapshot = inst.lastSelectionSnapshot || null;
        if (!snapshot) {
            try {
                snapshot = _captureSelectionSnapshot(inst);
            } catch {
                snapshot = null;
            }
        }

        return _normalizeSchemaRegion(snapshot && (snapshot.Region || snapshot.region));
    }

    function _normalizeSchemaRegion(region) {
        var normalized = String(region || 'Body').toLowerCase();
        switch (normalized) {
            case 'header':
                return 'Header';
            case 'footer':
                return 'Footer';
            case 'tablecell':
            case 'table-cell':
            case 'table_cell':
                return 'TableCell';
            case 'footnote':
                return 'Footnote';
            case 'endnote':
                return 'Endnote';
            case 'caption':
                return 'Caption';
            case 'image':
                return 'Image';
            default:
                return 'Body';
        }
    }

    function _normalizeInsertionBlocksForSchema(inst, blocks, region) {
        region = _normalizeSchemaRegion(region);
        var warnings = [];
        var normalized = [];
        (blocks || []).forEach(function (block) {
            _appendSchemaNormalizedBlock(normalized, block, region, warnings);
        });
        inst.lastInsertionPolicyWarnings = warnings;
        return normalized;
    }

    function _appendSchemaNormalizedBlock(target, block, region, warnings) {
        block = _clonePlainJson(block || {});
        var type = _normalizeBlockTypeNumber(block.Type ?? block.type);
        block.Type = type;
        if (type < 0) {
            target.push(_createTextBlock(0, { $type: 'paragraph', Inlines: [_createTextInline('')] }));
            warnings.push({ code: 'unknown-block-fallback', region: region });
            return;
        }

        if (!_schemaAllowsBlock(type, region)) {
            if (type === 4 && region === 'TableCell') {
                _unwrapTableForSchema(target, block, warnings);
                warnings.push({ code: 'table-unwrapped-in-table-cell', region: region });
                return;
            }

            warnings.push({ code: 'block-rejected-by-schema', region: region, blockType: type });
            return;
        }

        var content = block.Content || block.content || {};
        block.Content = content;
        if (type === 5 && (content.AltText === null || content.AltText === undefined) && (content.altText === null || content.altText === undefined)) {
            content.AltText = '';
            warnings.push({ code: 'image-alt-text-defaulted', region: region });
        }

        if (type === 4) {
            _normalizeTableCellsForSchema(content, warnings);
        }

        target.push(block);
    }

    function _normalizeTableCellsForSchema(tableContent, warnings) {
        var rows = tableContent.Rows || tableContent.rows || [];
        tableContent.Rows = rows;
        rows.forEach(function (row) {
            var cells = row.Cells || row.cells || [];
            row.Cells = cells;
            cells.forEach(function (cell) {
                var cellBlocks = cell.Blocks || cell.blocks || [];
                var normalized = [];
                cellBlocks.forEach(function (child) {
                    _appendSchemaNormalizedBlock(normalized, child, 'TableCell', warnings);
                });
                cell.Blocks = normalized.length > 0
                    ? normalized
                    : [_createTextBlock(0, { $type: 'paragraph', Inlines: [_createTextInline('')] })];
            });
        });
    }

    function _unwrapTableForSchema(target, tableBlock, warnings) {
        var content = tableBlock.Content || tableBlock.content || {};
        var rows = content.Rows || content.rows || [];
        var before = target.length;
        rows.forEach(function (row) {
            (row.Cells || row.cells || []).forEach(function (cell) {
                (cell.Blocks || cell.blocks || []).forEach(function (child) {
                    _appendSchemaNormalizedBlock(target, child, 'TableCell', warnings);
                });
            });
        });
        if (target.length === before) {
            target.push(_createTextBlock(0, { $type: 'paragraph', Inlines: [_createTextInline('')] }));
        }
    }

    function _normalizeBlockTypeNumber(type) {
        if (typeof type === 'number') return type >= 0 && type <= 6 ? type : -1;
        var value = String(type || '').toLowerCase();
        switch (value) {
            case 'paragraph':
                return 0;
            case 'heading':
                return 1;
            case 'list':
                return 2;
            case 'quote':
                return 3;
            case 'table':
                return 4;
            case 'image':
                return 5;
            case 'pagebreak':
            case 'page-break':
                return 6;
            default:
                return -1;
        }
    }

    function _schemaAllowsBlock(type, region) {
        region = _normalizeSchemaRegion(region);
        if (type === 0 || type === 2 || type === 3) {
            return region === 'Body' || region === 'Header' || region === 'Footer' || region === 'TableCell' || region === 'Footnote' || region === 'Endnote';
        }
        if (type === 1 || type === 4 || type === 6) {
            return region === 'Body';
        }
        if (type === 5) {
            return region === 'Body' || region === 'TableCell';
        }
        return false;
    }

    function _schemaAllowsToolbarBlockCommand(type, region) {
        region = _normalizeSchemaRegion(region);
        if (_schemaAllowsBlock(type, region)) {
            return true;
        }

        // A selected image/caption is a block-level anchor. Toolbar block commands insert
        // beside the image in the body instead of nesting inside the image region.
        return region === 'Image' && type === 4;
    }

    function _createPageBreakBlock() {
        return {
            Id: _newId('pagebreak'),
            Type: 6,
            Order: 0,
            Content: { $type: 'pageBreak' }
        };
    }

    function _createEmptyParagraphBlock() {
        return {
            Id: _newId('paragraph'),
            Type: 0,
            Order: 0,
            Content: {
                $type: 'paragraph',
                Inlines: [{
                    $type: 'text',
                    Id: _newId('inline'),
                    Text: ''
                }]
            }
        };
    }

    function _clonePlainJson(value) {
        if (value === null || value === undefined) return value;
        return JSON.parse(JSON.stringify(value));
    }

    function _insertClipboardBlocks(inst, blocks, selectionSnapshot) {
        if (!blocks || blocks.length === 0) return;
        var insertion = _getInsertionPoint(inst, selectionSnapshot);
        var parent = insertion.parent;
        var after = insertion.after;
        var activeCellId = insertion.activeTableCellId || '';
        var region = activeCellId ? 'TableCell' : _normalizeSchemaRegion(selectionSnapshot && (selectionSnapshot.region || selectionSnapshot.Region));
        var previousBlockId = after ? after.getAttribute('data-block-id') : (selectionSnapshot && (selectionSnapshot.anchorBlockId || selectionSnapshot.AnchorBlockId))
            || (inst.lastSelectionSnapshot && (inst.lastSelectionSnapshot.anchorBlockId || inst.lastSelectionSnapshot.AnchorBlockId));
        var beforeSelection = _captureSelectionSnapshot(inst);
        var revisionId = inst.trackChangesEnabled ? _createRevisionId() : null;
        var revisionPayload = revisionId ? _clipboardBlocksRevisionPayload(blocks) : '';

        for (var i = 0; i < blocks.length; i++) {
            var block = blocks[i];
            var blockEl = _renderBlock(block, inst);
            if (!blockEl) continue;
            if (after && after.nextSibling) {
                parent.insertBefore(blockEl, after.nextSibling);
            } else {
                parent.appendChild(blockEl);
            }
            after = blockEl;

            _dispatchPatch(inst, {
                type: 'InsertBlock',
                blockType: _blockTypeName(block.Type),
                block: block,
                selection: {
                    Region: region,
                    AnchorBlockId: previousBlockId || null,
                    FocusBlockId: previousBlockId || null,
                    ActiveTableCellId: activeCellId || null,
                    IsCollapsed: true
                },
                revisionId: revisionId,
                revisionType: revisionId ? 'Insertion' : null,
                protocolVersion: inst.options.protocolVersion || 1
            });
            previousBlockId = block.Id;
        }

        _placeCaretAtEndOfEditableBlock(after);
        var afterPasteSelection = _captureSelectionSnapshot(inst);
        if (afterPasteSelection) {
            inst.lastSelectionSnapshot = afterPasteSelection;
            inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(afterPasteSelection);
            _scheduleSelectionNotification(inst, afterPasteSelection);
        }
        if (revisionId) {
            _createRuntimeRevision(inst, revisionId, 'Insertion', revisionPayload || 'Pasted content', beforeSelection, _captureSelectionSnapshot(inst));
        }
    }

    function _clipboardBlocksRevisionPayload(blocks) {
        var parts = [];
        (blocks || []).forEach(function (block) {
            var text = _clipboardBlockText(block);
            if (text) parts.push(text);
        });
        return parts.join('\n');
    }

    function _clipboardBlockText(block) {
        if (!block) return '';
        var content = block.Content || block.content || {};
        var inlines = content.Inlines || content.inlines;
        if (Array.isArray(inlines)) {
            return inlines.map(function (inline) {
                return inline.Text || inline.text || '';
            }).join('');
        }

        var rows = content.Rows || content.rows;
        if (Array.isArray(rows)) {
            return rows.map(function (row) {
                var cells = row.Cells || row.cells || [];
                return cells.map(function (cell) {
                    return _clipboardBlocksRevisionPayload(cell.Blocks || cell.blocks || []);
                }).filter(Boolean).join('\t');
            }).filter(Boolean).join('\n');
        }

        return '';
    }

    function _getInsertionPoint(inst, selectionSnapshot) {
        var body = inst.root.querySelector('.tm-wysiwyg-page__body') || inst.root;
        var snapshot = selectionSnapshot || inst.lastSelectionSnapshot || null;
        var activeCellId = snapshot && (snapshot.activeTableCellId || snapshot.ActiveTableCellId);
        var anchorBlockId = snapshot && (snapshot.anchorBlockId || snapshot.AnchorBlockId || snapshot.focusBlockId || snapshot.FocusBlockId);
        if (activeCellId) {
            var cell = inst.root.querySelector('td[data-cell-id="' + _cssEscape(activeCellId) + '"], th[data-cell-id="' + _cssEscape(activeCellId) + '"]');
            if (cell) {
                var anchorInCell = anchorBlockId
                    ? cell.querySelector('.tm-wysiwyg-block[data-block-id="' + _cssEscape(anchorBlockId) + '"]')
                    : null;
                var lastCellBlock = anchorInCell || Array.from(cell.querySelectorAll(':scope > .tm-wysiwyg-block[data-block-id]')).pop() || null;
                return {
                    parent: cell,
                    after: lastCellBlock,
                    activeTableCellId: activeCellId
                };
            }
        }

        if (anchorBlockId) {
            var snapshotBlock = inst.root.querySelector('.tm-wysiwyg-block[data-block-id="' + _cssEscape(anchorBlockId) + '"]');
            if (snapshotBlock && snapshotBlock.parentElement) {
                var snapshotCell = snapshotBlock.closest('td[data-cell-id], th[data-cell-id]');
                return {
                    parent: snapshotBlock.parentElement,
                    after: snapshotBlock,
                    activeTableCellId: snapshotCell ? snapshotCell.getAttribute('data-cell-id') || '' : ''
                };
            }
        }

        var sel = window.getSelection();
        var block = null;
        if (sel && sel.rangeCount > 0) {
            var node = sel.anchorNode;
            var el = node && (node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement);
            block = el ? el.closest('.tm-wysiwyg-block[data-block-id]') : null;
        }
        var selectedCell = block ? block.closest('td[data-cell-id], th[data-cell-id]') : null;
        return {
            parent: block && block.parentElement ? block.parentElement : body,
            after: block,
            activeTableCellId: selectedCell ? selectedCell.getAttribute('data-cell-id') || '' : ''
        };
    }

    function _placeCaretAfterBlock(blockEl) {
        if (!blockEl) return;
        var sel = window.getSelection();
        if (!sel) return;
        var range = document.createRange();
        range.setStartAfter(blockEl);
        range.collapse(true);
        sel.removeAllRanges();
        sel.addRange(range);
    }

    function _placeCaretAtEndOfEditableBlock(blockEl) {
        if (!blockEl) return;
        if (blockEl.matches && blockEl.matches('figure.tm-wysiwyg-image, figure.tm-wysiwyg-image-block')) {
            _placeCaretAfterBlock(blockEl);
            return;
        }

        var target = blockEl;
        if (blockEl.matches && blockEl.matches('table.tm-wysiwyg-table')) {
            target = blockEl.querySelector('td[data-cell-id] .tm-wysiwyg-block[data-block-id], th[data-cell-id] .tm-wysiwyg-block[data-block-id]')
                || blockEl.querySelector('td[data-cell-id], th[data-cell-id]')
                || blockEl;
        }

        var textNode = null;
        var walker = document.createTreeWalker(target, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                var parent = node.parentElement;
                if (!parent || parent.closest('[contenteditable="false"], [aria-hidden="true"]')) {
                    return NodeFilter.FILTER_REJECT;
                }
                return NodeFilter.FILTER_ACCEPT;
            }
        });
        while (walker.nextNode()) {
            textNode = walker.currentNode;
        }

        var sel = window.getSelection();
        if (!sel) return;
        var range = document.createRange();
        if (textNode) {
            range.setStart(textNode, textNode.textContent.length);
        } else {
            range.selectNodeContents(target);
            range.collapse(false);
        }
        range.collapse(true);
        sel.removeAllRanges();
        sel.addRange(range);
    }

    function _placeCaretBeforeBlock(blockEl) {
        if (!blockEl) return;
        var sel = window.getSelection();
        if (!sel) return;
        var range = document.createRange();
        range.setStartBefore(blockEl);
        range.collapse(true);
        sel.removeAllRanges();
        sel.addRange(range);
    }

    function _moveCaretFromImageSelection(inst, before) {
        var figure = _getSelectedImageFigure(inst);
        if (!figure) return false;
        var block = figure.closest('.tm-wysiwyg-block[data-block-id]');
        if (!block) return false;

        if (before) {
            _placeCaretBeforeBlock(block);
        } else {
            _placeCaretAfterBlock(block);
        }

        _clearSelectedImage(inst);
        _hideImageContextMenu(inst);
        var snapshot = _captureSelectionSnapshot(inst);
        if (!snapshot) {
            var blockId = block.getAttribute('data-block-id') || '';
            snapshot = {
                region: 'Body',
                anchorNodeId: blockId,
                focusNodeId: blockId,
                anchorBlockId: blockId,
                focusBlockId: blockId,
                anchorInlineId: '',
                focusInlineId: '',
                anchorOffset: 0,
                focusOffset: 0,
                isCollapsed: true,
                direction: 'forward',
                activeImageBlockId: null
            };
            inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(snapshot);
        }

        if (snapshot) {
            inst.lastSelectionSnapshot = snapshot;
            _scheduleSelectionNotification(inst, snapshot);
        }

        return true;
    }

    function _blockTypeName(type) {
        return type === 1 ? 'Heading'
            : type === 2 ? 'List'
                : type === 3 ? 'Quote'
                    : type === 4 ? 'Table'
                        : type === 5 ? 'Image'
                            : type === 6 ? 'PageBreak'
                                : 'Paragraph';
    }

    function _newId(prefix) {
        return prefix + '-' + Date.now() + '-' + Math.random().toString(36).slice(2, 8);
    }

    function _onKeyDown(inst, event) {
        if (inst.readOnly) {
            if (_shouldBlockReadOnlyKey(event)) {
                event.preventDefault();
                event.stopPropagation();
                if (typeof event.stopImmediatePropagation === 'function') {
                    event.stopImmediatePropagation();
                }
            }

            return;
        }

        if (event.key && event.key.length === 1) {
            _hideMiniToolbar(inst);
        }

        if ((event.key === 'Backspace' || event.key === 'Delete') && inst.selectedPageBreakId) {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _deletePageBreak(inst, inst.selectedPageBreakId);
            return;
        }

        if (event.key === 'Escape' && inst.activeAutocompleteKey) {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _closeAutocompleteQuery(inst, true);
            return;
        }

        if (event.key === 'Escape'
            && (inst.root.getAttribute('data-active-region') === 'Header'
                || inst.root.getAttribute('data-active-region') === 'Footer')) {
            var keyTarget = event.target && event.target.nodeType === Node.ELEMENT_NODE
                ? event.target
                : event.target?.parentElement;
            if (keyTarget?.closest?.('[data-testid="document-toolbar"]')) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            closeHeaderFooter(inst.id);
            return;
        }

        if (inst.activeAutocompleteKey
            && (event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Enter')) {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _invokeDotNet(inst, 'HandleAutocompleteKeyRequested', event.key);
            return;
        }

        if (inst.selectedImageFigure) {
            var leaveBeforeImage = (event.shiftKey && event.key === 'Tab')
                || event.key === 'ArrowLeft'
                || event.key === 'ArrowUp';
            var leaveAfterImage = (!event.shiftKey && event.key === 'Tab')
                || event.key === 'ArrowRight'
                || event.key === 'ArrowDown'
                || event.key === 'Escape';
            if (leaveBeforeImage || leaveAfterImage) {
                event.preventDefault();
                event.stopPropagation();
                if (typeof event.stopImmediatePropagation === 'function') {
                    event.stopImmediatePropagation();
                }
                _moveCaretFromImageSelection(inst, leaveBeforeImage);
                return;
            }
        }

        if ((event.shiftKey && event.key === 'F10') || event.key === 'ContextMenu') {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _openKeyboardContextMenu(inst, event);
            return;
        }

        if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey) {
            var markPayload = _shortcutMarkPayload(event.key);
            if (markPayload) {
                event.preventDefault();
                _flushPendingInputPatch(inst);
                _flushSelectionNotification(inst);
                // Let Blazor's command registry handle formatting shortcuts.
                // The JS layer only blocks the browser's native contenteditable command.
                return;
            }
        }

        if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && (event.key || '').toLowerCase() === 'k') {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _flushPendingInputPatch(inst);
            _flushSelectionNotification(inst);
            _invokeDotNet(inst, 'HandleKeyboardCommandRequested', 'link');
            return;
        }

        // Phase 13: Tab navigation between table cells.
        if (event.key === 'Tab') {
            var cell = _findCurrentTableCell(inst);
            if (cell) {
                event.preventDefault();
                var nextCell = event.shiftKey
                    ? _findPreviousTableCell(cell)
                    : _findNextTableCell(cell);
                if (nextCell) {
                    _focusCell(nextCell);
                } else if (!event.shiftKey) {
                    _insertTableRow(inst);
                }
                return;
            }
        }

        if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
            var currentCell = _findCurrentTableCell(inst);
            if (currentCell) {
                event.preventDefault();
                event.stopPropagation();
                _moveCaretAfterTable(inst, currentCell);
                return;
            }
        }

        // Handle shortcuts that the browser does not natively support
        // or that we want to intercept for the command stack.
        if ((event.ctrlKey || event.metaKey) && event.key === 'z' && !event.shiftKey) {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _invokeDotNet(inst, 'HandleUndoRequested');
            return;
        }
        if ((event.ctrlKey || event.metaKey) && (event.key === 'y' || (event.key === 'z' && event.shiftKey))) {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _invokeDotNet(inst, 'HandleRedoRequested');
            return;
        }
        if ((event.ctrlKey || event.metaKey) && event.key === 's') {
            event.preventDefault();
            _flushPendingInputPatch(inst);
            _flushSelectionNotification(inst);
            _invokeDotNet(inst, 'HandleSaveRequested');
            return;
        }

        if (_handleJsOwnedKeyboardEvent(inst, event)) {
            return;
        }
    }

    function _onDocumentKeyDown(inst, event) {
        if (!inst || inst.disposed || inst.readOnly || event.defaultPrevented) return;
        if (event.key === 'Escape' && document.querySelector('.tm-color-picker--open .tm-color-picker-dropdown')) {
            return;
        }

        if (event.key === 'Escape' && inst.miniToolbarVisible) {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _hideMiniToolbar(inst, true);
            _focusEditorBody(inst);
            return;
        }

        if (inst.selectedImageFigure) {
            var leaveBeforeImage = (event.shiftKey && event.key === 'Tab')
                || event.key === 'ArrowLeft'
                || event.key === 'ArrowUp';
            var leaveAfterImage = (!event.shiftKey && event.key === 'Tab')
                || event.key === 'ArrowRight'
                || event.key === 'ArrowDown'
                || event.key === 'Escape';
            if (leaveBeforeImage || leaveAfterImage) {
                event.preventDefault();
                event.stopPropagation();
                if (typeof event.stopImmediatePropagation === 'function') {
                    event.stopImmediatePropagation();
                }
                _moveCaretFromImageSelection(inst, leaveBeforeImage);
                return;
            }
        }

        if ((event.shiftKey && event.key === 'F10') || event.key === 'ContextMenu') {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _openKeyboardContextMenu(inst, event);
            return;
        }

        if (!_hasEditorSelectionOrFocus(inst)) return;
        if (!(event.ctrlKey || event.metaKey) || event.altKey) return;

        var key = (event.key || '').toLowerCase();
        if (key === 'z' && !event.shiftKey) {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _invokeDotNet(inst, 'HandleUndoRequested');
            return;
        }

        if (key === 'y' || (key === 'z' && event.shiftKey)) {
            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === 'function') {
                event.stopImmediatePropagation();
            }
            _invokeDotNet(inst, 'HandleRedoRequested');
        }
    }

    function _handleJsOwnedKeyboardEvent(inst, event) {
        var measure = _beginInputMeasure(inst, event, 'keydown');
        if (!event || event.defaultPrevented || inst.compositionActive) return false;
        if (event.altKey || event.metaKey) return false;

        var inputType = null;
        var data = null;
        if (event.key && event.key.length === 1 && !event.ctrlKey) {
            inputType = 'insertText';
            data = event.key;
        } else if (event.key === 'Backspace') {
            inputType = event.ctrlKey ? 'deleteWordBackward' : 'deleteContentBackward';
        } else if (event.key === 'Delete') {
            inputType = event.ctrlKey ? 'deleteWordForward' : 'deleteContentForward';
        } else if (event.key === 'Enter' && !event.ctrlKey) {
            inputType = event.shiftKey ? 'insertLineBreak' : 'insertParagraph';
        } else {
            return false;
        }

        _ensureEditableSelection(inst, event.target);
        inst.lastInputType = inputType || null;
        inst.lastInputDataLength = data ? data.length : 0;
        var selection = _captureSelectionSnapshot(inst);
        var synthetic = {
            data: data,
            inputType: inputType,
            preventDefault: function () { },
            stopPropagation: function () { }
        };
        var handled = false;

        if (inst.trackChangesEnabled) {
            handled = _handleTrackedBeforeInput(inst, synthetic, inputType, selection);
        }

        if (!handled) {
            handled = _handlePendingTypingBeforeInput(inst, synthetic, inputType, selection)
                || _handleStructuralBeforeInput(inst, synthetic, inputType, selection, null)
                || _handleJsOwnedTextBeforeInput(inst, synthetic, inputType, selection);
        }

        if (!handled) return false;

        event.preventDefault();
        event.stopPropagation();
        if (typeof event.stopImmediatePropagation === 'function') {
            event.stopImmediatePropagation();
        }
        _endInputMeasure(measure);
        return true;
    }

    function _shortcutMarkPayload(key) {
        switch ((key || '').toLowerCase()) {
            case 'b': return { markType: 'Bold' };
            case 'i': return { markType: 'Italic' };
            case 'u': return { markType: 'Underline' };
            case 'k':
                return {
                    markType: 'Link',
                    href: 'https://example.com',
                    data: 'https://example.com'
                };
            default: return null;
        }
    }

    function _shouldBlockReadOnlyKey(event) {
        if (!event) return false;
        if (event.ctrlKey || event.metaKey) {
            var key = (event.key || '').toLowerCase();
            return key === 'b'
                || key === 'i'
                || key === 'u'
                || key === 'k'
                || key === 'z'
                || key === 'y'
                || key === 's';
        }

        if (event.altKey) return false;
        if ((event.key || '').length === 1) return true;
        return event.key === 'Backspace'
            || event.key === 'Delete'
            || event.key === 'Enter';
    }

    /**
     * Phase 12: Finds the current table cell from the active selection.
     */
    function _findCurrentTableCell(inst) {
        var sel = window.getSelection();
        if (sel && sel.rangeCount > 0) {
            var node = sel.anchorNode;
            if (node) {
                var el = node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement;
                var selectedCell = el ? el.closest('td[data-cell-id], th[data-cell-id]') : null;
                if (selectedCell) return selectedCell;
            }
        }

        var snapshot = inst && (inst.lastSelectionSnapshot || inst.runtimeSelection);
        var cellId = snapshot && (snapshot.activeTableCellId || snapshot.ActiveTableCellId);
        if (!cellId || !inst || !inst.root) return null;
        try {
            return inst.root.querySelector('td[data-cell-id="' + CSS.escape(cellId) + '"], th[data-cell-id="' + CSS.escape(cellId) + '"]');
        } catch {
            return null;
        }
    }

    function _applyTableCommandSelectionPayload(inst, payload) {
        if (!inst || !payload) return;
        var cellId = payload.activeTableCellId || payload.ActiveTableCellId || '';
        if (!cellId) return;
        var cell = null;
        try {
            cell = inst.root.querySelector('td[data-cell-id="' + CSS.escape(cellId) + '"], th[data-cell-id="' + CSS.escape(cellId) + '"]');
        } catch {
            cell = null;
        }

        if (!cell) return;
        _focusCell(cell);
        var snapshot = _captureSelectionSnapshot(inst) || {};
        snapshot.activeTableCellId = cellId;
        snapshot.ActiveTableCellId = cellId;
        inst.lastSelectionSnapshot = snapshot;
        inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(snapshot);
    }

    function _findFirstTableCell(inst) {
        if (!inst || !inst.root) return null;
        return inst.root.querySelector('table.tm-wysiwyg-table[data-block-id] td[data-cell-id], table.tm-wysiwyg-table[data-block-id] th[data-cell-id]');
    }

    function _findCurrentOrFallbackTableCell(inst) {
        return _findCurrentTableCell(inst) || _findFirstTableCell(inst);
    }

    /**
     * Phase 12: Finds the next table cell (row-major order).
     */
    function _findNextTableCell(currentCell) {
        var row = currentCell.parentElement;
        if (!row) return null;
        var table = row.closest('table');
        if (!table) return null;
        var allCells = Array.from(table.querySelectorAll('td[data-cell-id], th[data-cell-id]'));
        var idx = allCells.indexOf(currentCell);
        if (idx >= 0 && idx < allCells.length - 1) {
            return allCells[idx + 1];
        }
        return null;
    }

    /**
     * Phase 12: Finds the previous table cell (row-major order).
     */
    function _findPreviousTableCell(currentCell) {
        var row = currentCell.parentElement;
        if (!row) return null;
        var table = row.closest('table');
        if (!table) return null;
        var allCells = Array.from(table.querySelectorAll('td[data-cell-id], th[data-cell-id]'));
        var idx = allCells.indexOf(currentCell);
        if (idx > 0) {
            return allCells[idx - 1];
        }
        return null;
    }

    /**
     * Phase 12: Focuses a table cell by placing the caret at the start.
     */
    function _focusCell(cell) {
        var sel = window.getSelection();
        if (!sel) return;
        _markActiveTableCell(cell);
        sel.removeAllRanges();
        var range = document.createRange();
        var firstText = _firstDeepTextNode(cell);
        if (firstText) {
            range.setStart(firstText, 0);
            range.setEnd(firstText, 0);
        } else {
            range.setStart(cell, 0);
            range.setEnd(cell, 0);
        }
        sel.addRange(range);
        // Trigger selection change notification.
        cell.closest('[contenteditable]')?.dispatchEvent(new Event('selectionchange'));
    }

    function _markActiveTableCell(cell) {
        var table = _getTableBlockFromCell(cell);
        if (!table) return;
        table.querySelectorAll('.tm-wysiwyg-table-cell--active, .tm-wysiwyg-table-cell--range-selected').forEach(function (selectedCell) {
            selectedCell.classList.remove('tm-wysiwyg-table-cell--active', 'tm-wysiwyg-table-cell--range-selected');
            selectedCell.removeAttribute('aria-selected');
        });
        cell.classList.add('tm-wysiwyg-table-cell--active');
        cell.setAttribute('aria-selected', 'true');
        _renderTableHandles(table, cell);
    }

    function _getTableCellAtPoint(inst, clientX, clientY) {
        if (!inst || !inst.root || typeof document.elementFromPoint !== 'function') return null;
        var target = document.elementFromPoint(clientX, clientY);
        var cell = target && target.closest ? target.closest('td[data-cell-id], th[data-cell-id]') : null;
        return cell && inst.root.contains(cell) ? cell : null;
    }

    function _getTableCellCoordinates(table, cell) {
        if (!table || !cell) return null;
        var rows = Array.from(table.querySelectorAll('tr'));
        for (var rowIndex = 0; rowIndex < rows.length; rowIndex++) {
            var cells = Array.from(rows[rowIndex].querySelectorAll('td[data-cell-id], th[data-cell-id]'));
            var colIndex = cells.indexOf(cell);
            if (colIndex >= 0) {
                return { row: rowIndex, col: colIndex };
            }
        }
        return null;
    }

    function _clearTableRangeSelection(table) {
        if (!table) return;
        table.querySelectorAll('.tm-wysiwyg-table-cell--range-selected').forEach(function (selectedCell) {
            selectedCell.classList.remove('tm-wysiwyg-table-cell--range-selected');
            if (!selectedCell.classList.contains('tm-wysiwyg-table-cell--active')) {
                selectedCell.removeAttribute('aria-selected');
            }
        });
    }

    function _markTableCellRange(table, startCell, endCell) {
        if (!table || !startCell || !endCell) return;
        var start = _getTableCellCoordinates(table, startCell);
        var end = _getTableCellCoordinates(table, endCell);
        if (!start || !end) return;

        var minRow = Math.min(start.row, end.row);
        var maxRow = Math.max(start.row, end.row);
        var minCol = Math.min(start.col, end.col);
        var maxCol = Math.max(start.col, end.col);
        _clearTableRangeSelection(table);

        Array.from(table.querySelectorAll('tr')).forEach(function (row, rowIndex) {
            if (rowIndex < minRow || rowIndex > maxRow) return;
            Array.from(row.querySelectorAll('td[data-cell-id], th[data-cell-id]')).forEach(function (cell, colIndex) {
                if (colIndex < minCol || colIndex > maxCol) return;
                cell.classList.add('tm-wysiwyg-table-cell--range-selected');
                cell.setAttribute('aria-selected', 'true');
            });
        });

        startCell.classList.add('tm-wysiwyg-table-cell--active');
        startCell.setAttribute('aria-selected', 'true');
        _renderTableHandles(table, startCell);
    }

    function _renderTableHandles(table, activeCell) {
        if (!table || !activeCell) return;
        _clearTableHandles(table);
        var activeRow = activeCell.parentElement;
        var rows = Array.from(table.querySelectorAll('tr'));
        var rowIndex = rows.indexOf(activeRow);
        var colIndex = Array.from(activeRow.children).indexOf(activeCell);
        if (rowIndex < 0 || colIndex < 0) return;

        var rowHandle = document.createElement('button');
        rowHandle.type = 'button';
        rowHandle.className = 'tm-wysiwyg-table-handle tm-wysiwyg-table-handle--row';
        rowHandle.setAttribute('data-testid', 'document-table-row-handle');
        rowHandle.setAttribute('aria-label', 'Select row');
        rowHandle.addEventListener('click', function (event) {
            event.preventDefault();
            _selectTableRow(table, rowIndex);
        });
        activeRow.insertBefore(rowHandle, activeRow.firstChild);

        var headerRow = rows[0];
        if (headerRow) {
            var colHandle = document.createElement('button');
            colHandle.type = 'button';
            colHandle.className = 'tm-wysiwyg-table-handle tm-wysiwyg-table-handle--column';
            colHandle.setAttribute('data-testid', 'document-table-column-handle');
            colHandle.setAttribute('aria-label', 'Select column');
            colHandle.addEventListener('click', function (event) {
                event.preventDefault();
                _selectTableColumn(table, colIndex);
            });
            headerRow.insertBefore(colHandle, headerRow.children[colIndex] || headerRow.firstChild);
        }
    }

    function _clearTableHandles(table) {
        if (!table) return;
        table.querySelectorAll('.tm-wysiwyg-table-handle').forEach(function (handle) { handle.remove(); });
    }

    function _selectTableRow(table, rowIndex) {
        table.querySelectorAll('.tm-wysiwyg-table-cell--range-selected').forEach(function (cell) {
            cell.classList.remove('tm-wysiwyg-table-cell--range-selected');
        });
        var row = table.querySelectorAll('tr')[rowIndex];
        if (!row) return;
        row.querySelectorAll('td[data-cell-id], th[data-cell-id]').forEach(function (cell) {
            cell.classList.add('tm-wysiwyg-table-cell--range-selected');
        });
    }

    function _selectTableColumn(table, colIndex) {
        table.querySelectorAll('.tm-wysiwyg-table-cell--range-selected').forEach(function (cell) {
            cell.classList.remove('tm-wysiwyg-table-cell--range-selected');
        });
        table.querySelectorAll('tr').forEach(function (row) {
            var cells = Array.from(row.querySelectorAll('td[data-cell-id], th[data-cell-id]'));
            if (cells[colIndex]) cells[colIndex].classList.add('tm-wysiwyg-table-cell--range-selected');
        });
    }

    function _moveCaretAfterTable(inst, cell) {
        var table = _getTableBlockFromCell(cell);
        if (!table || !table.parentNode) return;
        var next = table.nextElementSibling;
        if (!next || !next.classList.contains('tm-wysiwyg-block')) {
            next = document.createElement('p');
            next.className = 'tm-wysiwyg-block';
            var blockId = _createBlockId();
            var inlineId = _createInlineId();
            next.setAttribute('data-block-id', blockId);
            _setRuntimeNodeAttributes(next, blockId, 'block');
            var span = document.createElement('span');
            span.setAttribute('data-inline-id', inlineId);
            _setRuntimeNodeAttributes(span, inlineId, 'inline');
            span.appendChild(document.createTextNode(''));
            next.appendChild(span);
            table.parentNode.insertBefore(next, table.nextSibling);
        }
        var targetText = _firstDeepTextNode(next);
        if (targetText) {
            _setCaret(targetText, 0);
        }
        _scheduleSelectionNotification(inst, _captureSelectionSnapshot(inst));
    }

    function _beginTableTransaction(inst, description) {
        var beforeSelection = _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot;
        _commitCurrentRuntimeTransaction(inst, false);
        return {
            beforeSelection: beforeSelection,
            transaction: _beginUndoTransaction(inst, 'table', description || 'Table edit', beforeSelection, true)
        };
    }

    function _commitTableTransaction(inst, tableEl, description, beforeSelection) {
        var afterSelection = _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot || beforeSelection;
        if (inst.trackChangesEnabled) {
            var revisionId = _createRevisionId();
            _createRuntimeRevision(
                inst,
                revisionId,
                'Table',
                description || 'Table structure',
                beforeSelection,
                afterSelection);
        }

        if (tableEl && tableEl.isConnected) {
            _dispatchTableUpdatePatch(inst, tableEl, beforeSelection, afterSelection);
        }

        _commitCurrentRuntimeTransaction(inst, true);
    }

    // ── Phase 12: Table structural commands ──────────────────────────────────

    /**
     * Inserts a new table (2×2) at the current selection.
     */
    function _insertTable(inst, rowCount, colCount) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return;
        var numRows = (typeof rowCount === 'number' && rowCount > 0) ? rowCount : 2;
        var numCols = (typeof colCount === 'number' && colCount > 0) ? colCount : 2;
        var tx = _beginTableTransaction(inst, 'Insert table');
        var beforeSelection = tx.beforeSelection;

        var tableBlockId = 'tbl-' + Date.now().toString(36) + Math.random().toString(36).slice(2);
        var rows = [];
        var tableBlock = document.createElement('table');
        tableBlock.className = 'tm-wysiwyg-table tm-wysiwyg-block';
        tableBlock.setAttribute('data-block-id', tableBlockId);
        tableBlock.setAttribute('data-table-alignment', 'left');
        _setRuntimeNodeAttributes(tableBlock, tableBlockId, 'block');
        for (var r = 0; r < numRows; r++) {
            var tr = document.createElement('tr');
            var rowCells = [];
            for (var c = 0; c < numCols; c++) {
                var cellId = _createTableCellId();
                var td = document.createElement('td');
                td.setAttribute('data-cell-id', cellId);
                _setRuntimeNodeAttributes(td, cellId, 'table-cell');
                var blockId = _createBlockId();
                var inlineId = _createInlineId();
                _appendEmptyTableCellParagraph(td, blockId, inlineId);
                tr.appendChild(td);
                rowCells.push({
                    Id: cellId,
                    ColumnSpan: 1,
                    RowSpan: 1,
                    Blocks: [_createEmptyTableCellBlockModel(blockId, inlineId)]
                });
            }
            tableBlock.appendChild(tr);
            rows.push({ Cells: rowCells });
        }

        var range = sel.getRangeAt(0);
        var currentBlock = _closestBlockElement(range.startContainer);
        var insertedAsBlock = false;
        if (currentBlock && currentBlock.parentNode) {
            currentBlock.parentNode.insertBefore(tableBlock, currentBlock.nextSibling);
            insertedAsBlock = true;
        }

        if (!insertedAsBlock) {
            range.deleteContents();
            range.insertNode(tableBlock);
        }

        range = document.createRange();
        range.setStartAfter(tableBlock);
        range.setEndAfter(tableBlock);
        sel.removeAllRanges();
        sel.addRange(range);
        var firstCell = tableBlock.querySelector('td[data-cell-id], th[data-cell-id]');
        if (firstCell) {
            _focusCell(firstCell);
        }
        var afterSelection = _captureSelectionSnapshot(inst) || beforeSelection;
        inst.lastSelectionSnapshot = afterSelection;
        inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(afterSelection);
        _scheduleSelectionNotification(inst, afterSelection);

        _dispatchPatch(inst, {
            type: 'InsertBlock',
            operationId: _nextRuntimeOperationId(inst),
            blockType: 'Table',
            block: {
                Id: tableBlockId,
                Type: 4,
                Order: 0,
                Content: {
                    $type: 'table',
                    Rows: rows,
                    Layout: {
                        Alignment: 0,
                        Borders: {}
                    }
                }
            },
            selection: beforeSelection,
            beforeSelection: beforeSelection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
        if (inst.trackChangesEnabled) {
            var revisionId = _createRevisionId();
            _createRuntimeRevision(inst, revisionId, 'Table', 'Insert table', beforeSelection, afterSelection);
        }
        _commitCurrentRuntimeTransaction(inst, true);
    }

    /**
     * Phase 12: Finds the parent table element that carries data-block-id.
     */
    function _getTableBlockFromCell(cell) {
        if (!cell) return null;
        var table = cell.closest('table.tm-wysiwyg-block[data-block-id]');
        return table || null;
    }

    function _appendEmptyTableCellParagraph(td, blockId, inlineId) {
        var p = document.createElement('p');
        p.className = 'tm-wysiwyg-block';
        var actualBlockId = blockId || _createBlockId();
        p.setAttribute('data-block-id', actualBlockId);
        _setRuntimeNodeAttributes(p, actualBlockId, 'block');
        var span = document.createElement('span');
        var actualInlineId = inlineId || _createInlineId();
        span.setAttribute('data-inline-id', actualInlineId);
        _setRuntimeNodeAttributes(span, actualInlineId, 'inline');
        span.appendChild(document.createTextNode(''));
        p.appendChild(span);
        td.appendChild(p);
    }

    function _createEmptyTableCellBlockModel(blockId, inlineId) {
        return {
            Id: blockId || _createBlockId(),
            Type: 0,
            Content: {
                $type: 'paragraph',
                Inlines: [
                    {
                        $type: 'text',
                        Id: inlineId || _createInlineId(),
                        Text: ''
                    }
                ]
            }
        };
    }

    /**
     * Phase 12: Dispatches an UpdateBlock patch for the given table element.
     */
    function _dispatchTableUpdatePatch(inst, tableEl, beforeSelection, afterSelection) {
        if (!tableEl) return;
        var blockId = tableEl.getAttribute('data-block-id');
        if (!blockId) return;
        _markIncrementalRender(inst, 'tableUpdate');
        var content = _serializeTable(tableEl);
        _dispatchPatch(inst, {
            type: 'UpdateBlock',
            operationId: _nextRuntimeOperationId(inst),
            block: {
                Id: blockId,
                Type: 4,
                Order: 0,
                Content: content
            },
            selection: beforeSelection || _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot,
            beforeSelection: beforeSelection || inst.lastSelectionSnapshot,
            afterSelection: afterSelection || _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
    }

    function _dispatchImageUpdatePatch(inst, figureEl) {
        if (!figureEl) return;
        var blockEl = figureEl.closest('.tm-wysiwyg-block[data-block-id]');
        if (!blockEl) return;
        var blockId = blockEl.getAttribute('data-block-id');
        if (!blockId) return;
        _markIncrementalRender(inst, 'imageUpdate');
        var beforeSelection = inst.lastSelectionSnapshot || _captureSelectionSnapshot(inst);
        var afterSelection = _captureSelectionSnapshot(inst) || beforeSelection;
        var revisionId = null;
        if (inst.trackChangesEnabled) {
            revisionId = _createRevisionId();
            _createRuntimeRevision(inst, revisionId, 'Image', 'Image updated', beforeSelection, afterSelection);
        }
        _dispatchPatch(inst, {
            type: 'UpdateBlock',
            operationId: _nextRuntimeOperationId(inst),
            block: {
                Id: blockId,
                Type: 5,
                Order: 0,
                Content: _serializeImage(figureEl)
            },
            selection: afterSelection,
            beforeSelection: beforeSelection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            revisionId: revisionId,
            revisionType: revisionId ? 'Image' : null,
            protocolVersion: inst.options.protocolVersion || 1
        });
    }

    function _onFloatingImagePointerDown(inst, event) {
        _debugImage(inst, 'pointerdown.start', {
            x: Math.round(event.clientX),
            y: Math.round(event.clientY),
            target: _debugElementLabel(event.target && event.target.nodeType === Node.ELEMENT_NODE ? event.target : event.target && event.target.parentElement),
            button: event.button,
            pointerType: event.pointerType || ''
        });
        var sideTextBlock = _findWrappedImageSideTextBlockAtPoint(inst, event.clientX, event.clientY);
        if (sideTextBlock) {
            event.preventDefault();
            _clearSelectedImage(inst);
            _hideImageContextMenu(inst);
            _hideImageReplaceMenu(inst);
            _focusWrappedImageSideTextBlock(inst, sideTextBlock, event.clientX, event.clientY);
            _debugImage(inst, 'pointerdown.sidecar-handled', {
                sideTextBlock: _debugElementLabel(sideTextBlock)
            });
            return;
        }

        var handle = event.target && event.target.closest && event.target.closest('.tm-wysiwyg-image__resize-handle');
        var figure = handle
            ? handle.closest('figure.tm-wysiwyg-image')
            : event.target && event.target.closest && event.target.closest('figure.tm-wysiwyg-image');
        if (!figure || !inst.root.contains(figure)) {
            _debugImage(inst, 'pointerdown.no-image-target', {
                target: _debugElementLabel(event.target && event.target.nodeType === Node.ELEMENT_NODE ? event.target : event.target && event.target.parentElement)
            });
            return;
        }

        if (!handle) {
            if (!_isImageVisualClick(figure, event.clientX, event.clientY)) {
                _debugImage(inst, 'pointerdown.image-nonvisual-click-ignored', {
                    figure: _debugElementLabel(figure),
                    figureRect: _debugRect(figure.getBoundingClientRect()),
                    visualRect: _debugRect(_getImagePrimaryVisualRect(figure)),
                    x: Math.round(event.clientX),
                    y: Math.round(event.clientY)
                });
                return;
            }
        }

        event.preventDefault();
        _selectImageFigure(inst, figure);
        _hideInlineRevisionReview(inst);
        _hideImageContextMenu(inst);
        _debugImage(inst, 'pointerdown.image-selected', {
            figure: _debugElementLabel(figure),
            inline: figure.getAttribute('data-floating-inline'),
            wrapMode: figure.getAttribute('data-wrap-mode'),
            horizontalPosition: figure.getAttribute('data-horizontal-position'),
            handle: !!handle
        });

        var isFloating = figure.classList.contains('tm-wysiwyg-image--floating') ||
            figure.getAttribute('data-floating-inline') === 'false';
        if (!handle && !isFloating) {
            _beginInlineImageMoveDrag(inst, figure, event);
            return;
        }

        var img = figure.querySelector('img');
        var startX = event.clientX;
        var startY = event.clientY;
        var initialX = parseFloat(figure.getAttribute('data-image-x') || '0') || 0;
        var initialY = parseFloat(figure.getAttribute('data-image-y') || '0') || 0;
        var initialWidth = img ? (parseFloat(img.style.width) || img.getBoundingClientRect().width || 120) : 120;
        var initialHeight = img ? (parseFloat(img.style.height) || img.getBoundingClientRect().height || 90) : 90;
        var aspectRatio = initialWidth > 0 && initialHeight > 0 ? initialHeight / initialWidth : 0;
        var blockId = figure.closest('.tm-wysiwyg-block[data-block-id]')?.getAttribute('data-block-id') || '';
        var imageSelection = {
            Region: 'Image',
            AnchorBlockId: blockId,
            FocusBlockId: blockId,
            ActiveImageBlockId: blockId,
            AnchorOffset: 0,
            FocusOffset: 0,
            IsCollapsed: true
        };
        _commitCurrentRuntimeTransaction(inst, true);
        var undoTransaction = _beginUndoTransaction(inst, 'image', handle ? 'Resize image' : 'Move image', imageSelection, true);
        figure.classList.add('tm-wysiwyg-image--dragging');
        figure.setAttribute('data-drag-feedback', handle ? 'resize' : 'move');
        inst.imageDragTransaction = {
            blockId: blockId,
            mode: handle ? 'resize' : 'move',
            transactionId: undoTransaction ? undoTransaction.transactionId : inst.currentTransactionId
        };
        if (typeof figure.setPointerCapture === 'function' && event.pointerId != null) {
            try { figure.setPointerCapture(event.pointerId); } catch { }
        }

        function onMove(moveEvent) {
            var dx = moveEvent.clientX - startX;
            var dy = moveEvent.clientY - startY;
            if (handle && img) {
                var maxSize = _getFloatingImageMaxSize(figure);
                var lockAspectRatio = figure.getAttribute('data-lock-aspect-ratio') !== 'false';
                var nextWidth = Math.min(maxSize.width, Math.max(24, initialWidth + dx));
                var nextHeight = lockAspectRatio && aspectRatio > 0
                    ? nextWidth * aspectRatio
                    : Math.min(maxSize.height, Math.max(24, initialHeight + dy));
                if (nextHeight > maxSize.height) {
                    nextHeight = maxSize.height;
                    if (lockAspectRatio && aspectRatio > 0) {
                        nextWidth = Math.max(24, nextHeight / aspectRatio);
                    }
                }
                img.style.width = Math.round(nextWidth) + 'px';
                img.style.height = Math.round(nextHeight) + 'px';
            } else {
                var next = _clampFloatingImagePosition(figure, initialX + dx, initialY + dy);
                var nextX = next.x;
                var nextY = next.y;
                figure.style.left = nextX + 'px';
                figure.style.top = nextY + 'px';
                figure.setAttribute('data-image-x', String(nextX));
                figure.setAttribute('data-image-y', String(nextY));
            }
        }

        function onUp() {
            document.removeEventListener('pointermove', onMove, true);
            document.removeEventListener('pointerup', onUp, true);
            figure.classList.remove('tm-wysiwyg-image--dragging');
            figure.removeAttribute('data-drag-feedback');
            if (typeof figure.releasePointerCapture === 'function' && event.pointerId != null) {
                try { figure.releasePointerCapture(event.pointerId); } catch { }
            }
            _dispatchImageUpdatePatch(inst, figure);
            _commitCurrentRuntimeTransaction(inst, true);
            inst.imageDragTransaction = null;
        }

        document.addEventListener('pointermove', onMove, true);
        document.addEventListener('pointerup', onUp, true);
    }

    function _beginInlineImageMoveDrag(inst, figure, event) {
        var blockEl = figure.closest('.tm-wysiwyg-block[data-block-id]');
        if (!blockEl) return;

        var startX = event.clientX;
        var startY = event.clientY;
        var dragging = false;
        var lastDrop = null;
        var marker = document.createElement('div');
        marker.className = 'tm-wysiwyg-image-insertion-caret';
        marker.setAttribute('contenteditable', 'false');
        marker.setAttribute('data-testid', 'document-wysiwyg-image-insertion-caret');
        var blockId = blockEl.getAttribute('data-block-id') || '';
        var imageSelection = {
            Region: 'Image',
            AnchorBlockId: blockId,
            FocusBlockId: blockId,
            ActiveImageBlockId: blockId,
            AnchorOffset: 0,
            FocusOffset: 0,
            IsCollapsed: true
        };
        _commitCurrentRuntimeTransaction(inst, true);
        var undoTransaction = _beginUndoTransaction(inst, 'image', 'Move image', imageSelection, true);
        inst.imageDragTransaction = {
            blockId: blockId,
            mode: 'inline-move',
            transactionId: undoTransaction ? undoTransaction.transactionId : inst.currentTransactionId
        };

        function beginDrag() {
            if (dragging) return;
            dragging = true;
            figure.classList.add('tm-wysiwyg-image--dragging');
            figure.setAttribute('data-drag-feedback', 'move');
        }

        function onMove(moveEvent) {
            var dx = moveEvent.clientX - startX;
            var dy = moveEvent.clientY - startY;
            if (!dragging && Math.sqrt((dx * dx) + (dy * dy)) < 4) return;
            beginDrag();
            moveEvent.preventDefault();
            lastDrop = _resolveInlineImageDropTarget(inst, blockEl, moveEvent.clientX, moveEvent.clientY);
            _placeInlineImageDropMarker(marker, lastDrop);
        }

        function onUp(upEvent) {
            document.removeEventListener('pointermove', onMove, true);
            document.removeEventListener('pointerup', onUp, true);
            marker.remove();
            figure.classList.remove('tm-wysiwyg-image--dragging');
            figure.removeAttribute('data-drag-feedback');
            if (typeof figure.releasePointerCapture === 'function' && event.pointerId != null) {
                try { figure.releasePointerCapture(event.pointerId); } catch { }
            }
            if (!dragging || !lastDrop || !lastDrop.body) {
                _commitCurrentRuntimeTransaction(inst, true);
                inst.imageDragTransaction = null;
                return;
            }

            upEvent.preventDefault();
            if (lastDrop.beforeBlock) {
                lastDrop.body.insertBefore(blockEl, lastDrop.beforeBlock);
            } else {
                lastDrop.body.appendChild(blockEl);
            }

            var order = _calculateInlineImageMoveOrder(lastDrop.body, blockEl);
            blockEl.setAttribute('data-block-order', String(order));
            _dispatchImageMovePatch(inst, figure, order);
            _commitCurrentRuntimeTransaction(inst, true);
            inst.imageDragTransaction = null;
        }

        if (typeof figure.setPointerCapture === 'function' && event.pointerId != null) {
            try { figure.setPointerCapture(event.pointerId); } catch { }
        }
        document.addEventListener('pointermove', onMove, true);
        document.addEventListener('pointerup', onUp, true);
    }

    function _resolveInlineImageDropTarget(inst, sourceBlock, clientX, clientY) {
        var element = document.elementFromPoint(clientX, clientY);
        var body = element && element.closest
            ? element.closest('.tm-wysiwyg-page__body')
            : null;
        body = body || sourceBlock.closest('.tm-wysiwyg-page__body') || inst.root.querySelector('.tm-wysiwyg-page__body') || inst.root;
        var blocks = Array.from(body.querySelectorAll(':scope > .tm-wysiwyg-block[data-block-id]'))
            .filter(function (block) { return block !== sourceBlock; });
        var beforeBlock = null;
        for (var i = 0; i < blocks.length; i++) {
            var rect = blocks[i].getBoundingClientRect();
            if (clientY < rect.top + (rect.height / 2)) {
                beforeBlock = blocks[i];
                break;
            }
        }
        return { body: body, beforeBlock: beforeBlock };
    }

    function _placeInlineImageDropMarker(marker, target) {
        if (!target || !target.body) return;
        if (target.beforeBlock) {
            target.body.insertBefore(marker, target.beforeBlock);
        } else {
            target.body.appendChild(marker);
        }
    }

    function _calculateInlineImageMoveOrder(body, blockEl) {
        var blocks = Array.from(body.querySelectorAll(':scope > .tm-wysiwyg-block[data-block-id]'));
        var index = blocks.indexOf(blockEl);
        var previous = index > 0 ? blocks[index - 1] : null;
        var next = index >= 0 && index < blocks.length - 1 ? blocks[index + 1] : null;
        var previousOrder = previous ? (parseFloat(previous.getAttribute('data-block-order') || '0') || 0) : null;
        var nextOrder = next ? (parseFloat(next.getAttribute('data-block-order') || '0') || 0) : null;
        if (previousOrder != null && nextOrder != null && nextOrder > previousOrder) {
            return (previousOrder + nextOrder) / 2;
        }
        if (previousOrder != null) return previousOrder + 10;
        if (nextOrder != null) return nextOrder - 10;
        return 10;
    }

    function _dispatchImageMovePatch(inst, figureEl, order) {
        if (!figureEl) return;
        var blockEl = figureEl.closest('.tm-wysiwyg-block[data-block-id]');
        if (!blockEl) return;
        var blockId = blockEl.getAttribute('data-block-id');
        if (!blockId) return;
        _markIncrementalRender(inst, 'imageMove');
        _dispatchPatch(inst, {
            type: 'MoveBlock',
            block: {
                Id: blockId,
                Type: 5,
                Order: order,
                Content: _serializeImage(figureEl)
            },
            selection: {
                Region: 'Image',
                AnchorBlockId: blockId,
                FocusBlockId: blockId,
                ActiveImageBlockId: blockId,
                AnchorOffset: 0,
                FocusOffset: 0,
                IsCollapsed: true
            },
            transactionId: inst.imageDragTransaction && inst.imageDragTransaction.blockId
                ? inst.imageDragTransaction.transactionId || ('image-move-' + inst.imageDragTransaction.blockId)
                : inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
    }

    function _clampFloatingImagePosition(figure, x, y) {
        var page = figure.closest('.tm-wysiwyg-page');
        if (!page) return { x: x, y: y };
        var maxX = Math.max(0, page.clientWidth - figure.offsetWidth - 8);
        var maxY = Math.max(0, page.clientHeight - figure.offsetHeight - 8);
        return {
            x: Math.max(0, Math.min(x, maxX)),
            y: Math.max(0, Math.min(y, maxY))
        };
    }

    function _getFloatingImageMaxSize(figure) {
        var page = figure.closest('.tm-wysiwyg-page');
        if (!page) return { width: Number.MAX_SAFE_INTEGER, height: Number.MAX_SAFE_INTEGER };
        return {
            width: Math.max(24, page.clientWidth - 16),
            height: Math.max(24, page.clientHeight - 16)
        };
    }

    function _createTableCellElement() {
        var td = document.createElement('td');
        var cellId = _createTableCellId();
        td.setAttribute('data-cell-id', cellId);
        _setRuntimeNodeAttributes(td, cellId, 'table-cell');
        _appendEmptyTableCellParagraph(td);
        return td;
    }

    /**
     * Inserts a row before or after the current table cell's row.
     */
    function _insertTableRow(inst, before) {
        var cell = _findCurrentOrFallbackTableCell(inst);
        if (!cell) return;
        var row = cell.parentElement;
        var table = _getTableBlockFromCell(cell);
        if (!table) return;
        _clearTableHandles(table);
        var tx = _beginTableTransaction(inst, before ? 'Insert table row before' : 'Insert table row after');
        var cellsPerRow = row.children.length;
        var newRow = document.createElement('tr');
        for (var c = 0; c < cellsPerRow; c++) {
            newRow.appendChild(_createTableCellElement());
        }
        row.parentElement.insertBefore(newRow, before ? row : row.nextSibling);
        _focusCell(newRow.querySelector('td[data-cell-id], th[data-cell-id]') || cell);
        _commitTableTransaction(inst, table, before ? 'Insert table row before' : 'Insert table row after', tx.beforeSelection);
    }

    /**
     * Deletes the current table cell's row.
     */
    function _deleteTableRow(inst) {
        var cell = _findCurrentOrFallbackTableCell(inst);
        if (!cell) return;
        var row = cell.parentElement;
        var table = _getTableBlockFromCell(cell);
        if (!row || !table) return;
        _clearTableHandles(table);
        var tx = _beginTableTransaction(inst, 'Delete table row');
        if (row.parentElement.children.length <= 1) {
            // Last row: remove the whole table.
            if (table) {
                var blockId = table.getAttribute('data-block-id');
                table.remove();
                if (blockId) {
                    _dispatchPatch(inst, {
                        type: 'RemoveBlock',
                        operationId: _nextRuntimeOperationId(inst),
                        blockId: blockId,
                        selection: tx.beforeSelection || { anchorBlockId: blockId },
                        beforeSelection: tx.beforeSelection,
                        afterSelection: _captureSelectionSnapshot(inst),
                        transactionId: inst.currentTransactionId,
                        protocolVersion: inst.options.protocolVersion || 1
                    });
                }
                _commitCurrentRuntimeTransaction(inst, true);
            }
        } else {
            var targetCell = row.nextElementSibling?.querySelector('td[data-cell-id], th[data-cell-id]')
                || row.previousElementSibling?.querySelector('td[data-cell-id], th[data-cell-id]');
            row.remove();
            if (targetCell) _focusCell(targetCell);
            _commitTableTransaction(inst, table, 'Delete table row', tx.beforeSelection);
        }
    }

    /**
     * Inserts a column before or after the current table cell's column.
     */
    function _insertTableColumn(inst, before) {
        var cell = _findCurrentOrFallbackTableCell(inst);
        if (!cell) return;
        var row = cell.parentElement;
        var table = _getTableBlockFromCell(cell);
        if (!table) return;
        _clearTableHandles(table);
        var tx = _beginTableTransaction(inst, before ? 'Insert table column before' : 'Insert table column after');
        var cellIndex = Array.from(row.children).indexOf(cell);
        var rows = table.querySelectorAll('tr');
        var firstInserted = null;
        for (var r = 0; r < rows.length; r++) {
            var td = _createTableCellElement();
            if (!firstInserted) firstInserted = td;
            var targetRow = rows[r];
            var targetCell = targetRow.children[cellIndex];
            if (targetCell) {
                targetRow.insertBefore(td, before ? targetCell : targetCell.nextSibling);
            } else {
                targetRow.appendChild(td);
            }
        }
        if (firstInserted) _focusCell(firstInserted);
        _commitTableTransaction(inst, table, before ? 'Insert table column before' : 'Insert table column after', tx.beforeSelection);
    }

    /**
     * Deletes the current table cell's column.
     */
    function _deleteTableColumn(inst) {
        var cell = _findCurrentOrFallbackTableCell(inst);
        if (!cell) return;
        var row = cell.parentElement;
        var table = _getTableBlockFromCell(cell);
        if (!table) return;
        _clearTableHandles(table);
        var tx = _beginTableTransaction(inst, 'Delete table column');
        var cellIndex = Array.from(row.children).indexOf(cell);
        var rows = table.querySelectorAll('tr');
        var focusTarget = null;
        for (var r = 0; r < rows.length; r++) {
            var targetCell = rows[r].children[cellIndex];
            if (!focusTarget) {
                focusTarget = rows[r].children[cellIndex + 1] || rows[r].children[cellIndex - 1] || null;
            }
            if (targetCell) targetCell.remove();
        }
        // If all rows are empty, remove the table.
        if (rows.length > 0 && rows[0].children.length === 0) {
            var tableBlock = _getTableBlockFromCell(cell);
            if (tableBlock) {
                var blockId = tableBlock.getAttribute('data-block-id');
                tableBlock.remove();
                if (blockId) {
                    _dispatchPatch(inst, {
                        type: 'RemoveBlock',
                        operationId: _nextRuntimeOperationId(inst),
                        blockId: blockId,
                        selection: tx.beforeSelection || { anchorBlockId: blockId },
                        beforeSelection: tx.beforeSelection,
                        afterSelection: _captureSelectionSnapshot(inst),
                        transactionId: inst.currentTransactionId,
                        protocolVersion: inst.options.protocolVersion || 1
                    });
                }
                _commitCurrentRuntimeTransaction(inst, true);
            }
        } else {
            if (focusTarget) _focusCell(focusTarget);
            _commitTableTransaction(inst, table, 'Delete table column', tx.beforeSelection);
        }
    }

    function _createEmptyParagraphElement() {
        var blockId = _createBlockId();
        var inlineId = _createInlineId();
        var p = document.createElement('p');
        p.className = 'tm-wysiwyg-block';
        p.setAttribute('data-block-id', blockId);
        _setRuntimeNodeAttributes(p, blockId, 'block');
        var span = document.createElement('span');
        span.setAttribute('data-inline-id', inlineId);
        _setRuntimeNodeAttributes(span, inlineId, 'inline');
        span.appendChild(document.createTextNode(''));
        p.appendChild(span);
        return p;
    }

    function _deleteTable(inst) {
        var cell = _findCurrentOrFallbackTableCell(inst);
        if (!cell) return;
        var table = _getTableBlockFromCell(cell);
        if (!table || !table.parentNode) return;
        var blockId = table.getAttribute('data-block-id') || '';
        if (!blockId) return;

        _clearTableHandles(table);
        var tx = _beginTableTransaction(inst, 'Delete table');
        var parent = table.parentNode;
        var focusBlock = table.nextElementSibling || table.previousElementSibling;
        if (!focusBlock || !focusBlock.classList || !focusBlock.classList.contains('tm-wysiwyg-block')) {
            focusBlock = _createEmptyParagraphElement();
            parent.insertBefore(focusBlock, table.nextSibling);
        }

        table.remove();
        var text = _firstDeepTextNode(focusBlock);
        if (text) {
            _setCaret(text, 0);
        }

        _dispatchPatch(inst, {
            type: 'RemoveBlock',
            operationId: _nextRuntimeOperationId(inst),
            blockId: blockId,
            selection: tx.beforeSelection || { anchorBlockId: blockId },
            beforeSelection: tx.beforeSelection,
            afterSelection: _captureSelectionSnapshot(inst),
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
        _commitCurrentRuntimeTransaction(inst, true);
    }

    /**
     * Merges the current cell with the cell to its right.
     */
    function _mergeTableCells(inst) {
        var cell = _findCurrentTableCell(inst);
        if (!cell) return;
        var row = cell.parentElement;
        var table = _getTableBlockFromCell(cell);
        var selectedCells = table ? Array.from(table.querySelectorAll('.tm-wysiwyg-table-cell--range-selected')) : [];
        var rowSelectedCells = selectedCells.filter(function (selectedCell) {
            return selectedCell.parentElement === row && selectedCell !== cell;
        });
        var cellsToMerge = rowSelectedCells.length > 0
            ? [cell].concat(rowSelectedCells)
            : [cell].concat(cell.nextElementSibling && /^(TD|TH)$/.test(cell.nextElementSibling.tagName) ? [cell.nextElementSibling] : []);
        cellsToMerge = cellsToMerge
            .filter(function (candidate, index, all) { return candidate && all.indexOf(candidate) === index; })
            .sort(function (a, b) { return Array.from(row.children).indexOf(a) - Array.from(row.children).indexOf(b); });
        if (cellsToMerge.length < 2 || cellsToMerge[0] !== cell) return;
        _clearTableHandles(table);
        var tx = _beginTableTransaction(inst, 'Merge table cells');
        var currentSpan = parseInt(cell.getAttribute('colspan') || '1', 10);
        var addedSpan = 0;
        cellsToMerge.slice(1).forEach(function (nextCell) {
            addedSpan += parseInt(nextCell.getAttribute('colspan') || '1', 10);
            while (nextCell.firstChild) {
                cell.appendChild(nextCell.firstChild);
            }
            nextCell.remove();
        });
        cell.setAttribute('colspan', currentSpan + addedSpan);
        _focusCell(cell);
        _commitTableTransaction(inst, table, 'Merge table cells', tx.beforeSelection);
    }

    /**
     * Splits a merged cell back into individual cells.
     */
    function _splitTableCell(inst) {
        var cell = _findCurrentTableCell(inst);
        if (!cell) return;
        var span = parseInt(cell.getAttribute('colspan') || '1', 10);
        if (span <= 1) return;
        var table = _getTableBlockFromCell(cell);
        _clearTableHandles(table);
        var tx = _beginTableTransaction(inst, 'Split table cell');
        cell.removeAttribute('colspan');
        var row = cell.parentElement;
        for (var i = 1; i < span; i++) {
            var newCell = _createTableCellElement();
            row.insertBefore(newCell, cell.nextSibling);
        }
        _focusCell(cell);
        _commitTableTransaction(inst, table, 'Split table cell', tx.beforeSelection);
    }

    /**
     * Phase 8.5: Toggles the first row of the current table between <th> and <td>.
     */
    function _toggleTableHeaderRow(inst) {
        var cell = _findCurrentTableCell(inst);
        var table = cell ? _getTableBlockFromCell(cell) : null;
        if (!table && inst && inst.root) {
            table = inst.root.querySelector('table.tm-wysiwyg-table[data-block-id]');
            cell = table ? table.querySelector('td[data-cell-id], th[data-cell-id]') : null;
        }
        if (!table) return;
        var firstRow = table.querySelector('tr');
        if (!firstRow) return;
        var tx = _beginTableTransaction(inst, 'Toggle header row');
        var cells = Array.from(firstRow.querySelectorAll('td, th'));
        var isHeader = cells.length > 0 && cells[0].tagName === 'TH';
        cells.forEach(function (c) {
            var replacement = document.createElement(isHeader ? 'td' : 'th');
            Array.from(c.attributes).forEach(function (a) {
                replacement.setAttribute(a.name, a.value);
            });
            while (c.firstChild) replacement.appendChild(c.firstChild);
            c.parentNode.replaceChild(replacement, c);
        });
        _focusCell(firstRow.querySelector('td[data-cell-id], th[data-cell-id]') || cell);
        _commitTableTransaction(inst, table, 'Toggle header row', tx.beforeSelection);
    }

    /**
     * Phase 8.5: Sets background colour of the current table cell.
     */
    function _setCellBackgroundColor(inst, color) {
        var cell = _findCurrentTableCell(inst);
        if (!cell) return;
        var table = _getTableBlockFromCell(cell);
        if (!table) return;
        var tx = _beginTableTransaction(inst, 'Set cell background');
        if (color) {
            cell.style.backgroundColor = color;
            cell.setAttribute('data-cell-background', color);
        } else {
            cell.style.backgroundColor = '';
            cell.removeAttribute('data-cell-background');
        }
        _commitTableTransaction(inst, table, 'Set cell background', tx.beforeSelection);
    }

    function _setTableProperties(inst, payload) {
        var cell = _findTableCommandCell(inst, payload);
        if (!cell) return;
        var table = _getTableBlockFromCell(cell);
        if (!table) return;
        var props = payload || {};
        var tx = _beginTableTransaction(inst, 'Set table properties');
        _applyTableLayoutStyle(table, {
            Width: props.width ?? props.Width ?? '',
            Alignment: props.alignment ?? props.Alignment ?? table.getAttribute('data-table-alignment') ?? 'left',
            CellPadding: props.cellPadding ?? props.CellPadding ?? '',
            BackgroundColor: props.backgroundColor ?? props.BackgroundColor ?? '',
            Borders: props.borders ?? props.Borders ?? {}
        });
        _commitTableTransaction(inst, table, 'Set table properties', tx.beforeSelection);
    }

    function _setCellProperties(inst, payload) {
        var cell = _findTableCommandCell(inst, payload);
        if (!cell) return;
        var table = _getTableBlockFromCell(cell);
        if (!table) return;
        var props = payload || {};
        var tx = _beginTableTransaction(inst, 'Set cell properties');
        _applyTableCellStyle(cell, {
            Width: props.width ?? props.Width ?? cell.getAttribute('data-cell-width') ?? '',
            BackgroundColor: props.backgroundColor ?? props.BackgroundColor ?? '',
            VerticalAlignment: props.verticalAlignment ?? props.VerticalAlignment ?? cell.getAttribute('data-cell-vertical-align') ?? 'top',
            Padding: props.padding ?? props.Padding ?? '',
            Borders: props.borders ?? props.Borders ?? {}
        });
        _commitTableTransaction(inst, table, 'Set cell properties', tx.beforeSelection);
    }

    function _resizeTableColumn(inst, payload) {
        var cell = _findTableCommandCell(inst, payload);
        if (!cell) return;
        var table = _getTableBlockFromCell(cell);
        if (!table) return;
        var width = payload && (payload.width ?? payload.Width);
        if (!width) return;
        var tx = _beginTableTransaction(inst, 'Resize table column');
        var colIndex = Array.from(cell.parentElement.querySelectorAll('td[data-cell-id], th[data-cell-id]')).indexOf(cell);
        table.querySelectorAll('tr').forEach(function (row) {
            var target = Array.from(row.querySelectorAll('td[data-cell-id], th[data-cell-id]'))[colIndex];
            if (target) {
                target.style.width = _normalizeCssLength(width);
                target.setAttribute('data-cell-width', String(width));
            }
        });
        _commitTableTransaction(inst, table, 'Resize table column', tx.beforeSelection);
    }

    function _findTableCommandCell(inst, payload) {
        var props = payload || {};
        var explicitCellId = props.cellId || props.CellId || props.activeTableCellId || props.ActiveTableCellId || '';
        if (explicitCellId && inst && inst.root) {
            try {
                var explicitCell = inst.root.querySelector('td[data-cell-id="' + CSS.escape(explicitCellId) + '"], th[data-cell-id="' + CSS.escape(explicitCellId) + '"]');
                if (explicitCell) return explicitCell;
            } catch {
                // Fall back to current browser selection below.
            }
        }

        return _findCurrentOrFallbackTableCell(inst);
    }

    function _onSelectionChange(inst) {
        if (inst.disposed) return;
        const snapshot = _captureSelectionSnapshot(inst);
        if (!snapshot) {
            inst.dismissedMiniToolbarSelectionKey = null;
            if (_shouldKeepMiniToolbarDuringSelectionSettle(inst)) {
                _scheduleMiniToolbar(inst, inst.lastTextSelectionSnapshot);
                _scheduleMiniToolbarSettleHide(inst);
            } else {
                _hideMiniToolbar(inst);
            }
            return;
        }

        inst.lastSelectionSnapshot = snapshot;
        if (snapshot && (snapshot.region === 'Header' || snapshot.region === 'Footer')) {
            var regionSelector = snapshot.region === 'Header' ? '.tm-wysiwyg-page__header' : '.tm-wysiwyg-page__footer';
            var regionId = snapshot.headerFooterId || snapshot.HeaderFooterId || '';
            var regionEl = regionId
                ? inst.root.querySelector(regionSelector + '[data-hf-id="' + _cssEscape(regionId) + '"]')
                : null;
            if (regionEl) {
                _markActivePageRegion(inst, regionEl);
            }
        } else if (snapshot && snapshot.region === 'Body') {
            var bodyRegion = inst.root.querySelector('.tm-wysiwyg-page__body[contenteditable="true"], .tm-wysiwyg-page__body[contenteditable="false"]');
            if (bodyRegion) {
                _markActivePageRegion(inst, bodyRegion);
            }
        }
        _scheduleSelectionNotification(inst, snapshot);
        if (snapshot && !snapshot.isCollapsed) {
            if (inst.miniToolbarSettleHideTimer) {
                window.clearTimeout(inst.miniToolbarSettleHideTimer);
                inst.miniToolbarSettleHideTimer = null;
            }
            inst.lastTextSelectionSnapshot = snapshot;
            inst.miniToolbarSuppressHideUntil = Date.now() + 1200;
            _scheduleMiniToolbar(inst, snapshot);
        } else {
            inst.dismissedMiniToolbarSelectionKey = null;
            if (_shouldKeepMiniToolbarDuringSelectionSettle(inst)) {
                _scheduleMiniToolbar(inst, inst.lastTextSelectionSnapshot);
                _scheduleMiniToolbarSettleHide(inst);
            } else {
                _hideMiniToolbar(inst);
            }
        }
    }

    function _shouldKeepMiniToolbarDuringSelectionSettle(inst) {
        return !!(inst
            && inst.lastTextSelectionSnapshot
            && inst.miniToolbarVisible
            && Date.now() < (inst.miniToolbarSuppressHideUntil || 0));
    }

    function _scheduleMiniToolbarSettleHide(inst) {
        if (!inst || inst.miniToolbarSettleHideTimer) return;
        var delay = Math.max(80, (inst.miniToolbarSuppressHideUntil || 0) - Date.now() + 40);
        inst.miniToolbarSettleHideTimer = window.setTimeout(function () {
            inst.miniToolbarSettleHideTimer = null;
            var current = _captureSelectionSnapshot(inst);
            if (!current || current.isCollapsed) {
                _hideMiniToolbar(inst, true);
            }
        }, delay);
    }

    function _scheduleSelectionNotification(inst, snapshot) {
        _rememberBodySelection(inst, snapshot);
        inst.pendingSelectionSnapshot = snapshot;
        if (inst.pendingSelectionTimer) return;

        inst.pendingSelectionTimer = setTimeout(function () {
            _flushSelectionNotification(inst);
        }, 80);
    }

    function _selectionRegionName(snapshot) {
        return String(snapshot?.region || snapshot?.Region || '').toLowerCase();
    }

    function _rememberBodySelection(inst, snapshot) {
        if (!inst || !snapshot || _selectionRegionName(snapshot) !== 'body') return;
        var blockId = snapshot.anchorBlockId || snapshot.AnchorBlockId || snapshot.focusBlockId || snapshot.FocusBlockId || '';
        if (!blockId) return;
        inst.lastBodySelectionSnapshot = Object.assign({}, snapshot);
    }

    function _flushSelectionNotification(inst) {
        if (inst.pendingSelectionTimer) {
            clearTimeout(inst.pendingSelectionTimer);
            inst.pendingSelectionTimer = null;
        }

        var snapshot = inst.pendingSelectionSnapshot;
        inst.pendingSelectionSnapshot = null;
        if (snapshot !== undefined) {
            _invokeDotNet(inst, 'HandleSelectionChanged', _toPascalSelection(snapshot));
        }
    }

    // ── Transaction batching ─────────────────────────────────────────────────

    function _beginTypingTransaction(inst) {
        if (!inst.currentTransactionId) {
            inst.currentTransactionId = 'txn-' + Date.now() + '-' + Math.random().toString(36).substr(2, 5);
        }
        if (inst.typingTimer) {
            clearTimeout(inst.typingTimer);
        }
        inst.typingTimer = setTimeout(function () {
            _flushPendingInputPatch(inst);
            _flushSelectionNotification(inst);
            _commitUndoTransaction(inst, _captureSelectionSnapshot(inst));
            inst.currentTransactionId = null;
            inst.typingTimer = null;
            _invokeDotNet(inst, 'HandleTransactionCommitted');
            _scheduleRemoteQueueFlush(inst);
        }, inst.options.typingBatchMs || 500);
    }

    function _commitCurrentRuntimeTransaction(inst, notify) {
        if (!inst || inst.disposed) return;
        _flushPendingInputPatch(inst);
        _flushSelectionNotification(inst);
        _commitUndoTransaction(inst, _captureSelectionSnapshot(inst));
        if (inst.typingTimer) {
            clearTimeout(inst.typingTimer);
            inst.typingTimer = null;
        }
        inst.currentTransactionId = null;
        if (notify) {
            _invokeDotNet(inst, 'HandleTransactionCommitted');
        }
    }

    function _beginUndoTransaction(inst, source, description, beforeSelection, forceNew) {
        if (!inst || inst.disposed) return null;
        if (forceNew) {
            _commitUndoTransaction(inst, beforeSelection || _captureSelectionSnapshot(inst));
        }
        if (inst.pendingUndoTransaction) {
            _notifyUndoStateChanged(inst);
            return inst.pendingUndoTransaction;
        }

        var transactionId = inst.currentTransactionId || ('txn-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7));
        if (!inst.currentTransactionId) {
            inst.currentTransactionId = transactionId;
        }

        inst.pendingUndoTransaction = {
            transactionId: transactionId,
            source: source || 'runtime',
            description: description || 'Edit',
            beforeHtml: inst.root.innerHTML,
            afterHtml: null,
            beforeSelection: _cloneRuntimeJson(beforeSelection || _captureSelectionSnapshot(inst)),
            afterSelection: null,
            operations: [],
            inverseOperations: [],
            createdAt: new Date().toISOString(),
            epoch: inst.runtimeUndoEpoch || 0
        };
        _notifyUndoStateChanged(inst);
        return inst.pendingUndoTransaction;
    }

    function _appendUndoOperation(inst, patch) {
        if (!inst || !inst.pendingUndoTransaction || !patch) return;
        inst.pendingUndoTransaction.operations.push(_cloneRuntimeJson(patch));
    }

    function _commitUndoTransaction(inst, afterSelection) {
        if (!inst || !inst.pendingUndoTransaction) return null;
        var transaction = inst.pendingUndoTransaction;
        inst.pendingUndoTransaction = null;
        transaction.afterHtml = inst.root.innerHTML;
        transaction.afterSelection = _cloneRuntimeJson(afterSelection || _captureSelectionSnapshot(inst));
        transaction.inverseOperations = [{
            operationId: transaction.transactionId + '-inverse-restore',
            type: 'RestoreHtmlSnapshot',
            source: transaction.source,
            html: transaction.beforeHtml,
            selection: _cloneRuntimeJson(transaction.beforeSelection),
            epoch: (inst.runtimeUndoEpoch || 0) + 1
        }];
        transaction.operations = transaction.operations && transaction.operations.length > 0
            ? transaction.operations
            : [{
                operationId: transaction.transactionId + '-restore',
                type: 'RestoreHtmlSnapshot',
                source: transaction.source,
                html: transaction.afterHtml,
                selection: _cloneRuntimeJson(transaction.afterSelection),
                epoch: inst.runtimeUndoEpoch || 0
            }];
        transaction.committedAt = new Date().toISOString();

        if (transaction.beforeHtml !== transaction.afterHtml) {
            inst.lastCommandTransaction = transaction;
            inst.commandUndoStack.push(transaction);
            inst.commandRedoStack = [];
            inst.lastCommittedHtml = transaction.afterHtml;
            _rememberPendingCollaborationTransaction(inst, transaction);
            _markRuntimeDirty(inst, transaction.source || 'transaction');
        }

        _notifyUndoStateChanged(inst);
        return transaction;
    }

    function _markRuntimeDirty(inst, reason) {
        if (!inst || inst.disposed) return;
        inst.isDirty = true;
        inst.dirtyEpoch = (inst.dirtyEpoch || 0) + 1;
        inst.lastDirtyReason = reason || 'edit';
        _notifyDirtyStateChanged(inst);
    }

    function _markRuntimeSaved(inst, marker) {
        if (!inst || inst.disposed) return;
        inst.isDirty = false;
        inst.savedEpoch = inst.dirtyEpoch || 0;
        inst.lastSavedMarker = marker || null;
        inst.lastSavedAt = new Date().toISOString();
        _notifyDirtyStateChanged(inst);
    }

    function _getDirtyState(inst) {
        return {
            IsDirty: !!(inst && inst.isDirty),
            DirtyEpoch: inst ? (inst.dirtyEpoch || 0) : 0,
            SavedEpoch: inst ? (inst.savedEpoch || 0) : 0,
            UndoEpoch: inst ? (inst.runtimeUndoEpoch || 0) : 0,
            Reason: inst ? (inst.lastDirtyReason || '') : '',
            LastSavedMarker: inst ? inst.lastSavedMarker : null,
            LastSavedAt: inst ? inst.lastSavedAt : null
        };
    }

    function _notifyDirtyStateChanged(inst) {
        if (!inst || inst.disposed) return;
        var state = _getDirtyState(inst);
        var previous = inst.lastDirtyState || {};
        inst.lastDirtyState = _cloneRuntimeJson(state);
        if (previous.IsDirty === state.IsDirty
            && previous.DirtyEpoch === state.DirtyEpoch
            && previous.SavedEpoch === state.SavedEpoch
            && previous.UndoEpoch === state.UndoEpoch
            && previous.Reason === state.Reason
            && previous.LastSavedMarker === state.LastSavedMarker) {
            return;
        }

        _invokeDotNet(inst, 'HandleDirtyStateChanged', state);
    }

    function _revisionTypeToNumber(type) {
        if (type === 0 || type === '0' || type === 'Insertion') return 0;
        if (type === 1 || type === '1' || type === 'Deletion') return 1;
        if (type === 2 || type === '2' || type === 'Formatting') return 2;
        if (type === 3 || type === '3' || type === 'Move') return 3;
        if (type === 4 || type === '4' || type === 'Structure' || type === 'Structural') return 4;
        if (type === 5 || type === '5' || type === 'Image') return 5;
        if (type === 6 || type === '6' || type === 'Table') return 6;
        return 0;
    }

    function _revisionTypeToName(type) {
        switch (_revisionTypeToNumber(type)) {
            case 1: return 'Deletion';
            case 2: return 'Formatting';
            case 3: return 'Move';
            case 4: return 'Structure';
            case 5: return 'Image';
            case 6: return 'Table';
            default: return 'Insertion';
        }
    }

    function _revisionActionToNumber(action) {
        if (action === 1 || action === '1' || action === 'Accepted') return 1;
        if (action === 2 || action === '2' || action === 'Rejected') return 2;
        return 0;
    }

    function _getRuntimeAuthor(inst) {
        var author = inst && inst.options ? (inst.options.author || inst.options.Author || {}) : {};
        return {
            Id: author.id || author.Id || '',
            DisplayName: author.displayName || author.DisplayName || 'Unknown author'
        };
    }

    function _normalizeRuntimeRevision(revision) {
        if (!revision) return null;
        var id = revision.id || revision.Id || '';
        if (!id) return null;
        var range = revision.range || revision.Range || {};
        var author = revision.author || revision.Author || {};
        return {
            Id: id,
            Type: _revisionTypeToNumber(revision.type ?? revision.Type),
            Range: {
                BlockId: range.blockId || range.BlockId || null,
                SourceBlockId: range.sourceBlockId || range.SourceBlockId || null,
                StartInlineIndex: range.startInlineIndex ?? range.StartInlineIndex ?? null,
                StartOffset: range.startOffset ?? range.StartOffset ?? null,
                EndInlineIndex: range.endInlineIndex ?? range.EndInlineIndex ?? null,
                EndOffset: range.endOffset ?? range.EndOffset ?? null
            },
            Author: {
                Id: author.id || author.Id || '',
                DisplayName: author.displayName || author.DisplayName || 'Unknown author'
            },
            CreatedAt: revision.createdAt || revision.CreatedAt || new Date().toISOString(),
            Action: _revisionActionToNumber(revision.action ?? revision.Action),
            PayloadJson: revision.payloadJson ?? revision.PayloadJson ?? ''
        };
    }

    function _revisionToRuntimeMarker(revision) {
        if (!revision || revision.Action !== 0) return null;
        var range = revision.Range || revision.range || {};
        var blockId = range.BlockId || range.blockId || '';
        if (!blockId) return null;
        var start = Number(range.StartOffset ?? range.startOffset ?? 0);
        var end = Number(range.EndOffset ?? range.endOffset ?? start);
        if (end <= start && revision.PayloadJson) {
            end = start + String(revision.PayloadJson).length;
        }

        var type = 'revisionInsertion';
        if (_revisionTypeToNumber(revision.Type ?? revision.type) === 1) {
            type = 'revisionDeletion';
        } else if (_revisionTypeToNumber(revision.Type ?? revision.type) !== 0) {
            type = 'revisionFormatting';
        }

        return {
            id: 'revision:' + revision.Id,
            type: type,
            range: {
                startBlockId: blockId,
                startInlineIndex: range.StartInlineIndex ?? range.startInlineIndex ?? null,
                startOffset: Math.max(0, start),
                endBlockId: blockId,
                endInlineIndex: range.EndInlineIndex ?? range.endInlineIndex ?? range.StartInlineIndex ?? range.startInlineIndex ?? null,
                endOffset: Math.max(0, end)
            },
            priority: 80,
            affectsData: true,
            source: 'document',
            targetId: revision.Id,
            label: (revision.Author && revision.Author.DisplayName) || ''
        };
    }

    function _syncRuntimeRevisionMarkers(inst) {
        if (!inst) return;
        _getRuntimeMarkersByType(inst, 'revisionInsertion')
            .concat(_getRuntimeMarkersByType(inst, 'revisionDeletion'))
            .concat(_getRuntimeMarkersByType(inst, 'revisionFormatting'))
            .forEach(function (marker) {
                var id = String(marker.id || marker.Id || '');
                if (id.indexOf('revision:') === 0) {
                    _removeRuntimeMarker(inst, id);
                }
            });
        (inst.runtimeRevisions || []).forEach(function (revision) {
            var marker = _revisionToRuntimeMarker(revision);
            if (marker && !_isEmbeddedRevisionAlreadyRendered(inst, marker)) {
                _upsertRuntimeMarker(inst, marker, false);
            }
        });
    }

    function _loadRuntimeRevisionsFromSnapshot(inst, snapshot) {
        var doc = snapshot && (snapshot.document || snapshot.Document) || {};
        var revisions = doc.revisions || doc.Revisions || [];
        inst.runtimeRevisions = revisions.map(_normalizeRuntimeRevision).filter(Boolean);
        inst.lastRevisionStateJson = JSON.stringify(inst.runtimeRevisions);
        _syncRuntimeRevisionMarkers(inst);
    }

    function _upsertRuntimeRevision(inst, revision) {
        if (!inst || !revision) return null;
        var normalized = _normalizeRuntimeRevision(revision);
        if (!normalized) return null;
        var revisions = inst.runtimeRevisions || [];
        var replaced = false;
        for (var i = 0; i < revisions.length; i++) {
            if (revisions[i].Id === normalized.Id) {
                revisions[i] = Object.assign({}, revisions[i], normalized);
                replaced = true;
                break;
            }
        }
        if (!replaced) revisions.push(normalized);
        inst.runtimeRevisions = revisions;
        _syncRuntimeRevisionsToSnapshot(inst);
        _upsertRuntimeMarker(inst, _revisionToRuntimeMarker(normalized), false);
        _notifyRuntimeRevisionsChanged(inst);
        return normalized;
    }

    function _appendRuntimeRevisionPayload(inst, revisionId, text) {
        if (!inst || !revisionId || !text) return;
        var revisions = inst.runtimeRevisions || [];
        for (var i = 0; i < revisions.length; i++) {
            if (revisions[i].Id === revisionId) {
                revisions[i].PayloadJson = (revisions[i].PayloadJson || '') + text;
                _syncRuntimeRevisionsToSnapshot(inst);
                _notifyRuntimeRevisionsChanged(inst);
                return;
            }
        }
    }

    function _createRuntimeRevision(inst, revisionId, type, payload, selection, afterSelection) {
        var start = selection || {};
        var end = afterSelection || selection || {};
        return _upsertRuntimeRevision(inst, {
            Id: revisionId,
            Type: _revisionTypeToNumber(type),
            Range: {
                BlockId: start.anchorBlockId || start.AnchorBlockId || start.focusBlockId || start.FocusBlockId || null,
                StartOffset: start.anchorOffset ?? start.AnchorOffset ?? null,
                EndOffset: end.anchorOffset ?? end.AnchorOffset ?? null
            },
            Author: _getRuntimeAuthor(inst),
            CreatedAt: new Date().toISOString(),
            Action: 0,
            PayloadJson: payload || ''
        });
    }

    function _setRuntimeRevisionAction(inst, revisionId, action) {
        var revisions = inst.runtimeRevisions || [];
        var actionValue = _revisionActionToNumber(action);
        for (var i = 0; i < revisions.length; i++) {
            if (revisions[i].Id === revisionId) {
                revisions[i].Action = actionValue;
                _syncRuntimeRevisionsToSnapshot(inst);
                if (actionValue === 0) {
                    _upsertRuntimeMarker(inst, _revisionToRuntimeMarker(revisions[i]), false);
                } else {
                    _removeRuntimeMarker(inst, 'revision:' + revisionId);
                }
                _notifyRuntimeRevisionsChanged(inst);
                return;
            }
        }
    }

    function _syncRuntimeRevisionsToSnapshot(inst) {
        if (!inst || !inst.snapshot) return;
        var doc = inst.snapshot.document || inst.snapshot.Document;
        if (!doc) return;
        var revisions = (inst.runtimeRevisions || []).map(function (revision) { return _cloneRuntimeJson(revision); });
        doc.Revisions = revisions;
        doc.revisions = revisions;
    }

    function _notifyRuntimeRevisionsChanged(inst) {
        if (!inst || inst.disposed) return;
        var revisions = (inst.runtimeRevisions || []).map(function (revision) { return _cloneRuntimeJson(revision); });
        var json = JSON.stringify(revisions);
        if (json === inst.lastRevisionStateJson) return;
        inst.lastRevisionStateJson = json;
        _invokeDotNet(inst, 'HandleRevisionsChanged', revisions);
    }

    function _commentAnchorTypeToNumber(value) {
        if (typeof value === 'number') return value;
        var raw = String(value || '').toLowerCase();
        if (raw === 'textrange' || raw === 'text-range') return 1;
        if (raw === 'importeddocx') return 2;
        if (raw === 'importedodt') return 3;
        if (raw === 'page') return 4;
        if (raw === 'rendition') return 5;
        return 0;
    }

    function _commentStatusToNumber(value) {
        if (typeof value === 'number') return value;
        return String(value || '').toLowerCase() === 'resolved' ? 1 : 0;
    }

    function _normalizeRuntimeComment(comment) {
        if (!comment) return null;
        var id = comment.id || comment.Id || '';
        if (!id) return null;
        var anchor = comment.anchor || comment.Anchor || {};
        return Object.assign({}, _cloneRuntimeJson(comment), {
            Id: id,
            Anchor: Object.assign({}, _cloneRuntimeJson(anchor), {
                Type: _commentAnchorTypeToNumber(anchor.type ?? anchor.Type),
                BlockId: anchor.blockId || anchor.BlockId || null,
                StartInlineIndex: anchor.startInlineIndex ?? anchor.StartInlineIndex ?? null,
                StartOffset: anchor.startOffset ?? anchor.StartOffset ?? null,
                EndInlineIndex: anchor.endInlineIndex ?? anchor.EndInlineIndex ?? null,
                EndOffset: anchor.endOffset ?? anchor.EndOffset ?? null,
                IsOrphaned: !!(anchor.isOrphaned ?? anchor.IsOrphaned)
            }),
            Status: _commentStatusToNumber(comment.status ?? comment.Status)
        });
    }

    function _loadRuntimeCommentsFromSnapshot(inst, snapshot) {
        var doc = snapshot && (snapshot.document || snapshot.Document) || {};
        var comments = doc.comments || doc.Comments || [];
        inst.runtimeComments = comments.map(_normalizeRuntimeComment).filter(Boolean);
        inst.lastCommentStateJson = JSON.stringify(inst.runtimeComments);
        _syncRuntimeCommentsToSnapshot(inst);
        _syncRuntimeCommentMarkers(inst);
    }

    function _syncRuntimeCommentsToSnapshot(inst) {
        if (!inst || !inst.snapshot) return;
        var doc = inst.snapshot.document || inst.snapshot.Document;
        if (!doc) return;
        var comments = (inst.runtimeComments || []).map(function (comment) { return _cloneRuntimeJson(comment); });
        doc.Comments = comments;
        doc.comments = comments;
    }

    function _commentToRuntimeMarker(comment) {
        if (!comment) return null;
        var anchor = comment.Anchor || comment.anchor || {};
        var blockId = anchor.BlockId || anchor.blockId || '';
        if (!blockId || anchor.IsOrphaned || anchor.isOrphaned) return null;
        var start = Number(anchor.StartOffset ?? anchor.startOffset ?? 0);
        var end = Number(anchor.EndOffset ?? anchor.endOffset ?? start);
        return {
            id: 'comment:' + comment.Id,
            type: 'comment',
            range: {
                startBlockId: blockId,
                startOffset: Math.max(0, start),
                endBlockId: blockId,
                endOffset: Math.max(0, end)
            },
            priority: 60,
            affectsData: true,
            source: 'document',
            targetId: comment.Id,
            label: comment.Title || comment.Text || '',
            status: comment.Status === 1 ? 'resolved' : 'open',
            metadata: {
                status: comment.Status === 1 ? 'resolved' : 'open'
            }
        };
    }

    function _syncRuntimeCommentMarkers(inst) {
        if (!inst) return;
        _getRuntimeMarkersByType(inst, 'comment').forEach(function (marker) {
            var id = String(marker.id || marker.Id || '');
            if (id.indexOf('comment:') === 0) {
                _removeRuntimeMarker(inst, id);
            }
        });
        (inst.runtimeComments || []).forEach(function (comment) {
            _upsertRuntimeMarker(inst, _commentToRuntimeMarker(comment), false);
        });
    }

    function _upsertRuntimeComment(inst, comment, applyDecoration) {
        if (!inst || !comment) return null;
        var normalized = _normalizeRuntimeComment(comment);
        if (!normalized) return null;
        var comments = inst.runtimeComments || [];
        var replaced = false;
        for (var i = 0; i < comments.length; i++) {
            if (comments[i].Id === normalized.Id) {
                comments[i] = Object.assign({}, comments[i], normalized);
                replaced = true;
                break;
            }
        }
        if (!replaced) comments.push(normalized);
        inst.runtimeComments = comments;
        _syncRuntimeCommentsToSnapshot(inst);
        _upsertRuntimeMarker(inst, _commentToRuntimeMarker(normalized), false);
        if (applyDecoration !== false) {
            _applyCommentDecoration(inst, normalized);
        }
        _notifyRuntimeCommentsChanged(inst);
        return normalized;
    }

    function _removeRuntimeComment(inst, commentId) {
        if (!inst || !commentId) return;
        _clearCommentDecorations(inst, commentId);
        _removeRuntimeMarker(inst, 'comment:' + commentId);
        inst.runtimeComments = (inst.runtimeComments || []).filter(function (comment) { return comment.Id !== commentId; });
        _syncRuntimeCommentsToSnapshot(inst);
        _notifyRuntimeCommentsChanged(inst);
    }

    function _notifyRuntimeCommentsChanged(inst) {
        if (!inst || inst.disposed) return;
        var comments = (inst.runtimeComments || []).map(function (comment) { return _cloneRuntimeJson(comment); });
        var json = JSON.stringify(comments);
        if (json === inst.lastCommentStateJson) return;
        inst.lastCommentStateJson = json;
        _invokeDotNet(inst, 'HandleCommentsChanged', comments);
        _scheduleCommentRailAlignment(inst);
    }

    function _scheduleCommentRailAlignment(inst) {
        if (!inst || inst.disposed) return;
        if (inst.commentRailAlignmentFrame) return;
        var schedule = window.requestAnimationFrame || function (callback) { return window.setTimeout(callback, 16); };
        inst.commentRailAlignmentFrame = schedule(function () {
            inst.commentRailAlignmentFrame = null;
            _updateCommentRailAlignment(inst);
        });
    }

    function _findCommentAnchorNode(inst, comment) {
        if (!inst || !inst.root || !comment) return null;
        var id = String(comment.Id || comment.id || '');
        var node = id ? inst.root.querySelector('[data-comment-id="' + _cssEscape(id) + '"]') : null;
        if (node) return node;

        var anchor = comment.Anchor || comment.anchor || {};
        var blockId = anchor.BlockId || anchor.blockId || '';
        return blockId ? inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]') : null;
    }

    function _updateCommentRailAlignment(inst) {
        if (!inst || inst.disposed || !inst.root) return false;
        var editor = inst.root.closest && inst.root.closest('.tm-document-editor');
        var rail = editor ? editor.querySelector('[data-testid="document-comment-rail"]') : document.querySelector('[data-testid="document-comment-rail"]');
        var list = rail && rail.querySelector('[data-testid="document-comment-list"]');
        if (!rail || !list || !list.getBoundingClientRect) return false;

        var listRect = list.getBoundingClientRect();
        var targets = [];
        Array.from(list.querySelectorAll('[data-comment-id]')).forEach(function (thread) {
            thread.style.marginTop = '';
            delete thread.dataset.anchorTop;
        });
        (inst.runtimeComments || []).forEach(function (comment) {
            var id = String(comment.Id || comment.id || '');
            if (!id) return;
            var thread = list.querySelector('[data-comment-id="' + _cssEscape(id) + '"]');
            var anchorNode = _findCommentAnchorNode(inst, comment);
            if (!thread || !anchorNode || !anchorNode.getBoundingClientRect) return;

            var anchorRect = anchorNode.getBoundingClientRect();
            var targetTop = Math.max(0, anchorRect.top - listRect.top + (list.scrollTop || 0));
            thread.style.setProperty('--tm-document-comment-anchor-offset', targetTop.toFixed(1) + 'px');
            thread.dataset.anchorTop = targetTop.toFixed(1);
            targets.push({ thread: thread, top: targetTop });
        });

        targets.sort(function (left, right) { return left.top - right.top; });
        var previousBottom = 0;
        targets.forEach(function (target) {
            var height = target.thread.offsetHeight || target.thread.getBoundingClientRect().height || 0;
            var margin = Math.max(0, target.top - previousBottom);
            target.thread.style.marginTop = margin.toFixed(1) + 'px';
            previousBottom = target.top + height;
        });

        rail.dataset.alignedCommentCount = String(targets.length);
        return targets.length > 0;
    }

    function _renderRuntimeCommentDecorations(inst) {
        if (!inst || !inst.root) return;
        _clearCommentDecorations(inst);
        (inst.runtimeComments || []).forEach(function (comment) {
            _applyCommentDecoration(inst, comment);
        });
        _scheduleCommentRailAlignment(inst);
    }

    function _clearCommentDecorations(inst, commentId) {
        if (!inst || !inst.root) return;
        var selector = '.tm-document-inline--comment-anchor[data-comment-id]';
        if (commentId) {
            selector += '[data-comment-id="' + _cssEscape(String(commentId)) + '"]';
        }

        Array.from(inst.root.querySelectorAll(selector)).forEach(function (node) {
            if (node.hasAttribute && node.hasAttribute('data-inline-id')) {
                node.classList.remove(
                    'tm-document-inline--comment-anchor',
                    'tm-document-inline--comment-anchor--selected',
                    'tm-document-inline--comment-anchor--resolved');
                node.removeAttribute('data-comment-id');
                if ((node.getAttribute('data-testid') || '') === 'document-comment-highlight') {
                    node.removeAttribute('data-testid');
                }
                return;
            }

            _unwrapElement(node);
        });
    }

    function _applyCommentDecoration(inst, comment) {
        if (!inst || !inst.root || !comment) return false;
        var anchor = comment.Anchor || comment.anchor || {};
        if (_commentAnchorTypeToNumber(anchor.Type ?? anchor.type) !== 1 || anchor.IsOrphaned || anchor.isOrphaned) {
            _clearCommentDecorations(inst, comment.Id || comment.id);
            return false;
        }

        var blockId = anchor.BlockId || anchor.blockId || '';
        var start = anchor.StartOffset ?? anchor.startOffset;
        var end = anchor.EndOffset ?? anchor.endOffset;
        if (!blockId || !Number.isFinite(start) || !Number.isFinite(end) || end <= start) return false;

        _clearCommentDecorations(inst, comment.Id);
        var block = inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        if (!block) return false;

        var startPos = _resolveTextPosition(block, start);
        var endPos = _resolveTextPosition(block, end);
        if (!startPos || !endPos) return false;

        var range = document.createRange();
        try {
            range.setStart(startPos.node, startPos.offset);
            range.setEnd(endPos.node, endPos.offset);
        } catch {
            return false;
        }

        if (range.collapsed) return false;

        var wrapper = document.createElement('span');
        wrapper.className = 'tm-document-inline--comment-anchor';
        if (_commentStatusToNumber(comment.Status ?? comment.status) === 1) {
            wrapper.classList.add('tm-document-inline--comment-anchor--resolved');
        }
        wrapper.setAttribute('data-comment-id', comment.Id);
        wrapper.setAttribute('data-testid', 'document-comment-highlight');

        try {
            wrapper.appendChild(range.extractContents());
            range.insertNode(wrapper);
        } catch {
            return false;
        }

        return true;
    }

    function _transformRuntimeCommentAnchorsForTextChange(inst, blockId, offset, length, isDelete) {
        if (!inst || !blockId || !Number.isFinite(offset) || !Number.isFinite(length) || length <= 0) return;
        var changed = false;
        (inst.runtimeComments || []).forEach(function (comment) {
            var anchor = comment.Anchor || comment.anchor || {};
            if (_commentAnchorTypeToNumber(anchor.Type ?? anchor.type) !== 1) return;
            if ((anchor.BlockId || anchor.blockId || '') !== blockId) return;
            if (anchor.IsOrphaned || anchor.isOrphaned) return;

            var start = anchor.StartOffset ?? anchor.startOffset;
            var end = anchor.EndOffset ?? anchor.endOffset;
            if (!Number.isFinite(start) || !Number.isFinite(end)) return;

            if (!isDelete) {
                if (offset <= start) {
                    start += length;
                    end += length;
                    changed = true;
                } else if (offset < end) {
                    end += length;
                    changed = true;
                }
            } else {
                var deleteEnd = offset + length;
                if (deleteEnd <= start) {
                    start -= length;
                    end -= length;
                    changed = true;
                } else if (offset >= end) {
                    return;
                } else {
                    var overlapStart = Math.max(start, offset);
                    var overlapEnd = Math.min(end, deleteEnd);
                    var removedInside = Math.max(0, overlapEnd - overlapStart);
                    if (offset < start) {
                        var shift = Math.min(length, start - offset);
                        start -= shift;
                        end -= shift;
                    }
                    end -= removedInside;
                    if (end <= start) {
                        anchor.IsOrphaned = true;
                        anchor.isOrphaned = true;
                    }
                    changed = true;
                }
            }

            anchor.StartOffset = Math.max(0, start);
            anchor.startOffset = anchor.StartOffset;
            anchor.EndOffset = Math.max(anchor.StartOffset, end);
            anchor.endOffset = anchor.EndOffset;
            comment.Anchor = anchor;
            comment.anchor = anchor;
        });

        if (!changed) return;
        _syncRuntimeCommentsToSnapshot(inst);
        _notifyRuntimeCommentsChanged(inst);
    }

    function _getTextChangeFromPatch(patch) {
        if (!patch) return null;
        var type = patch.type || patch.Type || '';
        var selection = patch.selection || patch.Selection || patch.beforeSelection || patch.BeforeSelection || {};
        var blockId = selection.anchorBlockId || selection.AnchorBlockId || selection.focusBlockId || selection.FocusBlockId || '';
        var offset = selection.anchorBlockOffset ?? selection.AnchorBlockOffset ?? selection.anchorOffset ?? selection.AnchorOffset;
        var data = patch.data ?? patch.Data ?? '';

        if (type === 'InsertText') {
            var text = String(data || '');
            return text ? { blockId: blockId, offset: offset || 0, length: text.length, isDelete: false } : null;
        }

        if (type === 'DeleteRange') {
            var deleteLength = patch.deleteLength ?? patch.DeleteLength ?? String(data || '').length;
            return deleteLength > 0 ? { blockId: blockId, offset: offset || 0, length: deleteLength, isDelete: true } : null;
        }

        if (type === 'DeleteContentBackward' || type === 'DeleteWordBackward') {
            var backwardLength = String(data || '').length || 1;
            return { blockId: blockId, offset: Math.max(0, (offset || 0) - backwardLength), length: backwardLength, isDelete: true };
        }

        if (type === 'DeleteContentForward' || type === 'DeleteWordForward') {
            var forwardLength = String(data || '').length || 1;
            return { blockId: blockId, offset: offset || 0, length: forwardLength, isDelete: true };
        }

        return null;
    }

    function _transformRuntimeCommentAnchorsForPatch(inst, patch) {
        var change = _getTextChangeFromPatch(patch);
        if (!change) return;
        _transformRuntimeCommentAnchorsForTextChange(inst, change.blockId, change.offset, change.length, change.isDelete);
    }

    // ── MutationObserver guard ───────────────────────────────────────────────

    function _attachMutationObserver(inst) {
        if (!inst.options.enableMutationGuard) return;

        inst.mutationObserver = new MutationObserver(function (mutations) {
            if (inst.disposed || inst.readOnly) return;
            if (inst.compositionActive) return;
            if (inst._applyingOwnPatch) return;
            if (inst.acceptingNativeInput) return;

            // Filter out mutations caused by our own explicit DOM changes.
            var relevant = mutations.filter(function (m) {
                return !m.target.closest || !m.target.closest('[data-tm-patch-applied]');
            });

            if (relevant.length === 0) return;

            // Guard: notify Blazor that the DOM diverged from the expected state.
            _invokeDotNet(inst, 'HandleMutationGuardTriggered', relevant.map(function (m) {
                return {
                    type: m.type,
                    target: m.target.nodeName,
                    attributeName: m.attributeName || null,
                };
            }));
        });

        inst.mutationObserver.observe(inst.root, {
            childList: true,
            subtree: true,
            attributes: true,
            characterData: true,
        });
    }

    // ── Selection snapshot ───────────────────────────────────────────────────

    /**
     * Captures the current browser selection and maps it to the document model.
     * Uses anchor/focus (where the user started/ended the selection) rather than
     * range start/end so direction is preserved.
     */
    function _captureSelectionSnapshot(inst) {
        var selectedFigure = _getSelectedImageFigure(inst);
        if (selectedFigure) {
            var activeSelection = window.getSelection();
            if (activeSelection
                && activeSelection.rangeCount > 0
                && inst.root.contains(activeSelection.anchorNode)
                && inst.root.contains(activeSelection.focusNode)
                && !selectedFigure.contains(activeSelection.anchorNode)
                && !selectedFigure.contains(activeSelection.focusNode)) {
                _clearSelectedImage(inst);
                selectedFigure = null;
            }
        }

        if (selectedFigure) {
            var imageBlock = selectedFigure.closest('.tm-wysiwyg-block[data-block-id]');
            var imageBlockId = imageBlock ? (imageBlock.getAttribute('data-block-id') || '') : '';
            if (imageBlockId) {
                var imageSnapshot = {
                    region: 'Image',
                    anchorBlockId: imageBlockId,
                    focusBlockId: imageBlockId,
                    activeImageBlockId: imageBlockId,
                    anchorInlineId: '',
                    focusInlineId: '',
                    anchorOffset: 0,
                    focusOffset: 0,
                    isCollapsed: true
                };
                inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(imageSnapshot);
                return imageSnapshot;
            }
        }

        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) {
            return null;
        }

        var anchor = _mapNodeToBlockInline(sel.anchorNode, sel.anchorOffset, inst.root);
        var focus = _mapNodeToBlockInline(sel.focusNode, sel.focusOffset, inst.root);

        if (!anchor || !focus) {
            return null;
        }

        var direction = _computeSelectionDirection(sel);

        var region = _resolveSelectionRegion(sel.anchorNode, inst.root);
        var activeTableCellId = _findTableCellId(sel.anchorNode);
        var snapshot = {
            region: region.region,
            pageIndex: region.pageIndex,
            headerFooterId: region.headerFooterId,
            anchorNodeId: anchor.inlineId || anchor.blockId || null,
            focusNodeId: focus.inlineId || focus.blockId || null,
            anchorBlockId: anchor.blockId,
            anchorInlineId: anchor.inlineId,
            anchorOffset: anchor.offset,
            anchorBlockOffset: anchor.blockOffset,
            focusBlockId: focus.blockId,
            focusInlineId: focus.inlineId,
            focusOffset: focus.offset,
            focusBlockOffset: focus.blockOffset,
            isCollapsed: sel.isCollapsed,
            direction: direction,
            activeTableCellId: activeTableCellId,
            tableCellPath: region.tableCellPath
        };
        inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(snapshot);
        return snapshot;
    }

    function _createRuntimeSelectionFromSnapshot(snapshot) {
        if (!snapshot) return null;
        var anchorBlockId = snapshot.anchorBlockId || snapshot.AnchorBlockId || null;
        var anchorInlineId = snapshot.anchorInlineId || snapshot.AnchorInlineId || null;
        var focusBlockId = snapshot.focusBlockId || snapshot.FocusBlockId || anchorBlockId;
        var focusInlineId = snapshot.focusInlineId || snapshot.FocusInlineId || anchorInlineId;
        return {
            version: 1,
            region: snapshot.region || snapshot.Region || 'Body',
            pageIndex: snapshot.pageIndex ?? snapshot.PageIndex ?? null,
            headerFooterId: snapshot.headerFooterId || snapshot.HeaderFooterId || null,
            anchorNodeId: snapshot.anchorNodeId || snapshot.AnchorNodeId || anchorInlineId || anchorBlockId,
            focusNodeId: snapshot.focusNodeId || snapshot.FocusNodeId || focusInlineId || focusBlockId,
            anchorBlockId: anchorBlockId,
            anchorInlineId: anchorInlineId,
            anchorOffset: snapshot.anchorOffset ?? snapshot.AnchorOffset ?? 0,
            anchorBlockOffset: snapshot.anchorBlockOffset ?? snapshot.AnchorBlockOffset ?? 0,
            focusBlockId: focusBlockId,
            focusInlineId: focusInlineId,
            focusOffset: snapshot.focusOffset ?? snapshot.FocusOffset ?? snapshot.anchorOffset ?? snapshot.AnchorOffset ?? 0,
            focusBlockOffset: snapshot.focusBlockOffset ?? snapshot.FocusBlockOffset ?? snapshot.anchorBlockOffset ?? snapshot.AnchorBlockOffset ?? 0,
            isCollapsed: snapshot.isCollapsed ?? snapshot.IsCollapsed ?? true,
            direction: snapshot.direction || snapshot.Direction || 'forward',
            activeTableCellId: snapshot.activeTableCellId || snapshot.ActiveTableCellId || null,
            tableCellPath: snapshot.tableCellPath || snapshot.TableCellPath || null,
            activeImageBlockId: snapshot.activeImageBlockId || snapshot.ActiveImageBlockId || null
        };
    }

    function _createSelectionSnapshotFromRuntimeSelection(selection) {
        if (!selection) return null;
        return {
            region: selection.region || selection.Region || 'Body',
            pageIndex: selection.pageIndex ?? selection.PageIndex ?? null,
            headerFooterId: selection.headerFooterId || selection.HeaderFooterId || null,
            anchorNodeId: selection.anchorNodeId || selection.AnchorNodeId || selection.anchorInlineId || selection.AnchorInlineId || selection.anchorBlockId || selection.AnchorBlockId || null,
            focusNodeId: selection.focusNodeId || selection.FocusNodeId || selection.focusInlineId || selection.FocusInlineId || selection.focusBlockId || selection.FocusBlockId || null,
            anchorBlockId: selection.anchorBlockId || selection.AnchorBlockId || null,
            anchorInlineId: selection.anchorInlineId || selection.AnchorInlineId || null,
            anchorOffset: selection.anchorOffset ?? selection.AnchorOffset ?? 0,
            anchorBlockOffset: selection.anchorBlockOffset ?? selection.AnchorBlockOffset ?? 0,
            focusBlockId: selection.focusBlockId || selection.FocusBlockId || selection.anchorBlockId || selection.AnchorBlockId || null,
            focusInlineId: selection.focusInlineId || selection.FocusInlineId || selection.anchorInlineId || selection.AnchorInlineId || null,
            focusOffset: selection.focusOffset ?? selection.FocusOffset ?? selection.anchorOffset ?? selection.AnchorOffset ?? 0,
            focusBlockOffset: selection.focusBlockOffset ?? selection.FocusBlockOffset ?? selection.anchorBlockOffset ?? selection.AnchorBlockOffset ?? 0,
            isCollapsed: selection.isCollapsed ?? selection.IsCollapsed ?? true,
            direction: selection.direction || selection.Direction || 'forward',
            activeTableCellId: selection.activeTableCellId || selection.ActiveTableCellId || null,
            tableCellPath: selection.tableCellPath || selection.TableCellPath || null,
            activeImageBlockId: selection.activeImageBlockId || selection.ActiveImageBlockId || null
        };
    }

    function _collapseSelectionSnapshotToFocus(selection) {
        var snapshot = _createSelectionSnapshotFromRuntimeSelection(selection);
        if (!snapshot) return null;

        var focusBlockId = snapshot.focusBlockId || snapshot.anchorBlockId || null;
        var focusInlineId = snapshot.focusInlineId || snapshot.anchorInlineId || null;
        var focusOffset = snapshot.focusOffset ?? snapshot.anchorOffset ?? 0;
        var focusBlockOffset = snapshot.focusBlockOffset ?? snapshot.anchorBlockOffset ?? 0;
        var focusNodeId = snapshot.focusNodeId || focusInlineId || focusBlockId || null;

        return Object.assign({}, snapshot, {
            anchorNodeId: focusNodeId,
            focusNodeId: focusNodeId,
            anchorBlockId: focusBlockId,
            focusBlockId: focusBlockId,
            anchorInlineId: focusInlineId,
            focusInlineId: focusInlineId,
            anchorOffset: focusOffset,
            focusOffset: focusOffset,
            anchorBlockOffset: focusBlockOffset,
            focusBlockOffset: focusBlockOffset,
            isCollapsed: true,
            direction: 'forward',
            activeImageBlockId: null
        });
    }

    function _resolveSelectionRegion(node, root) {
        var el = node && node.nodeType === Node.ELEMENT_NODE ? node : node && node.parentElement;
        if (!el || !root.contains(el)) {
            return { region: 'Body', pageIndex: null, headerFooterId: null, tableCellPath: null };
        }

        var pageEl = el.closest('.tm-wysiwyg-page[data-page-index]');
        var pageIndex = pageEl ? parseInt(pageEl.getAttribute('data-page-index') || '0', 10) : null;
        if (!Number.isFinite(pageIndex)) pageIndex = null;

        var caption = el.closest('figcaption');
        if (caption) {
            var captionFigure = caption.closest('figure.tm-wysiwyg-image, figure.tm-wysiwyg-image-block, figure[data-image-source]');
            var captionBlock = captionFigure ? captionFigure.closest('[data-block-id]') : null;
            return {
                region: 'Caption',
                pageIndex: pageIndex,
                headerFooterId: null,
                tableCellPath: null,
                activeImageBlockId: captionBlock ? captionBlock.getAttribute('data-block-id') || null : null
            };
        }

        var noteRegion = el.closest('.tm-wysiwyg-page__notes[data-region]');
        if (noteRegion) {
            return {
                region: noteRegion.getAttribute('data-region') || 'Footnote',
                pageIndex: pageIndex,
                headerFooterId: null,
                tableCellPath: null
            };
        }

        var cell = el.closest('td[data-cell-id], th[data-cell-id]');
        if (cell) {
            return {
                region: 'TableCell',
                pageIndex: pageIndex,
                headerFooterId: null,
                tableCellPath: _findTableCellPath(cell)
            };
        }

        var image = el.closest('figure.tm-wysiwyg-image-block, figure[data-image-source]');
        if (image) {
            return {
                region: 'Image',
                pageIndex: pageIndex,
                headerFooterId: null,
                tableCellPath: null
            };
        }

        var header = el.closest('.tm-wysiwyg-page__header[data-hf-id]');
        if (header) {
            return {
                region: 'Header',
                pageIndex: pageIndex,
                headerFooterId: header.getAttribute('data-hf-id') || null,
                tableCellPath: null
            };
        }

        var footer = el.closest('.tm-wysiwyg-page__footer[data-hf-id]');
        if (footer) {
            return {
                region: 'Footer',
                pageIndex: pageIndex,
                headerFooterId: footer.getAttribute('data-hf-id') || null,
                tableCellPath: null
            };
        }

        return { region: 'Body', pageIndex: pageIndex, headerFooterId: null, tableCellPath: null };
    }

    /**
     * Phase 13: Finds the nearest table cell id from a DOM node.
     */
    function _findTableCellId(node) {
        var el = node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement;
        if (!el) return null;
        var cell = el.closest('td[data-cell-id], th[data-cell-id]');
        return cell ? cell.getAttribute('data-cell-id') : null;
    }

    function _findTableCellPath(cell) {
        if (!cell) return null;
        var table = cell.closest('table[data-block-id], .tm-wysiwyg-table[data-block-id]');
        var row = cell.closest('tr');
        var rowIndex = row ? Array.from(row.parentElement ? row.parentElement.children : []).indexOf(row) : -1;
        var cellIndex = row ? Array.from(row.children).indexOf(cell) : -1;
        return [
            table ? table.getAttribute('data-block-id') || '' : '',
            rowIndex >= 0 ? 'row-' + rowIndex : '',
            cell.getAttribute('data-cell-id') || (cellIndex >= 0 ? 'cell-' + cellIndex : '')
        ].filter(Boolean).join('/');
    }

    /**
     * Maps a DOM node and offset to the nearest block/inline identifiers.
     * Normalizes element-node offsets into text-node character offsets.
     */
    function _mapNodeToBlockInline(node, offset, root) {
        var atomicInline = _closestAtomicInlineElement(node, root);
        if (atomicInline) {
            var atomicBlock = atomicInline.closest('[data-block-id]');
            if (!atomicBlock || !root.contains(atomicBlock)) return null;
            var atomicOffset = _resolveAtomicInlineOffset(atomicInline, node, offset);
            return {
                blockId: atomicBlock.getAttribute('data-block-id'),
                inlineId: atomicInline.getAttribute('data-inline-id') || null,
                offset: atomicOffset,
                blockOffset: _absoluteAtomicOffset(atomicBlock, atomicInline, atomicOffset)
            };
        }

        var normalized = _normalizeToTextNode(node, offset);
        var textNode = normalized.node;
        var textOffset = normalized.offset;

        var el = textNode.parentElement;
        if (!el) return null;

        // Walk up to find the nearest semantic block with data-block-id. Revision,
        // link, comment and remote mark wrappers are presentation layers and must
        // not become the logical target when they only decorate an inline.
        var blockEl = el.closest('[data-block-id]');
        if (!blockEl || !root.contains(blockEl)) {
            blockEl = root.querySelector('[data-block-id]');
        }
        if (!blockEl) return null;

        var inlineEl = _findSemanticInlineElement(el, blockEl);
        if (!inlineEl || !blockEl.contains(inlineEl)) {
            inlineEl = blockEl.querySelector('[data-inline-id]');
        }

        var inlineOffset = inlineEl && inlineEl.contains(textNode)
            ? _absoluteTextOffset(inlineEl, textNode, textOffset)
            : textOffset;
        var blockOffset = _absoluteTextOffset(blockEl, textNode, textOffset);

        return {
            blockId: blockEl.getAttribute('data-block-id'),
            inlineId: inlineEl ? inlineEl.getAttribute('data-inline-id') : null,
            offset: inlineOffset,
            blockOffset: blockOffset,
        };
    }

    function _findSemanticInlineElement(element, blockEl) {
        if (!element || !blockEl) return null;

        var inlineEl = element.closest('[data-inline-id]');
        if (!inlineEl || !blockEl.contains(inlineEl)) return null;

        var decoratedParentInline = inlineEl.parentElement && inlineEl.parentElement.closest('[data-inline-id]');
        if (decoratedParentInline && blockEl.contains(decoratedParentInline)) {
            return decoratedParentInline;
        }

        return inlineEl;
    }

    function _closestAtomicInlineElement(node, root) {
        if (!node) return null;
        var el = node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement;
        var atomic = el && el.closest('[data-inline-atomic="true"], .tm-wysiwyg-token');
        return atomic && root && root.contains(atomic) ? atomic : null;
    }

    function _isAtomicInlineElement(node) {
        return node
            && node.nodeType === Node.ELEMENT_NODE
            && (node.getAttribute('data-inline-atomic') === 'true' || node.classList.contains('tm-wysiwyg-token'));
    }

    function _resolveAtomicInlineOffset(atomic, node, offset) {
        if (!atomic) return 0;
        if (node === atomic) {
            return offset <= 0 ? 0 : 1;
        }

        if (node && node.nodeType === Node.TEXT_NODE) {
            return offset <= 0 ? 0 : 1;
        }

        var child = node && node.nodeType === Node.ELEMENT_NODE ? node : null;
        if (child && atomic.contains(child)) {
            return offset <= 0 ? 0 : 1;
        }

        return 1;
    }

    function _atomicBoundaryPosition(atomic, after) {
        var parent = atomic && atomic.parentNode;
        if (!parent) return null;
        var index = Array.prototype.indexOf.call(parent.childNodes, atomic);
        return { node: parent, offset: Math.max(0, index + (after ? 1 : 0)) };
    }

    function _absoluteAtomicOffset(root, targetAtomic, targetOffset) {
        var current = 0;
        var resolved = null;

        function visit(parent) {
            for (var i = 0; i < parent.childNodes.length; i++) {
                var child = parent.childNodes[i];
                if (child === targetAtomic) {
                    resolved = current + (targetOffset > 0 ? 1 : 0);
                    return true;
                }

                if (child.nodeType === Node.TEXT_NODE) {
                    current += child.textContent.length;
                    continue;
                }

                if (_isInlineBreakNode(child) || _isAtomicInlineElement(child)) {
                    current += 1;
                    continue;
                }

                if (child.nodeType === Node.ELEMENT_NODE && visit(child)) {
                    return true;
                }
            }

            return false;
        }

        visit(root);
        return resolved ?? current;
    }

    /**
     * Normalizes a node+offset into a text node + character offset.
     * If the node is an element, it walks to the appropriate text boundary.
     */
    function _normalizeToTextNode(node, offset) {
        if (node.nodeType === Node.TEXT_NODE) {
            return { node: node, offset: Math.max(0, Math.min(offset, node.textContent.length)) };
        }

        // Element node: offset is a child index. Walk to find text.
        if (node.nodeType === Node.ELEMENT_NODE) {
            var child = node.childNodes[offset] || node.lastChild;
            if (_isAtomicInlineElement(child)) {
                return _atomicBoundaryPosition(child, false) || { node: node, offset: offset };
            }
            if (child && child.nodeType === Node.TEXT_NODE) {
                return { node: child, offset: 0 };
            }
            if (_isInlineBreakNode(child)) {
                return _positionAfterInlineBreak(child);
            }
            if (child && child.nodeType === Node.ELEMENT_NODE) {
                var deepest = _firstDeepTextNode(child);
                if (deepest) return { node: deepest, offset: 0 };
            }
            // Fallback: previous sibling's last text node.
            var prev = node.childNodes[offset - 1];
            if (prev) {
                if (_isAtomicInlineElement(prev)) {
                    return _atomicBoundaryPosition(prev, true) || { node: node, offset: offset };
                }
                var last = _lastDeepTextNode(prev);
                if (last) return { node: last, offset: last.textContent.length };
            }
        }

        return { node: node, offset: offset };
    }

    function _firstDeepTextNode(el) {
        if (el.nodeType === Node.TEXT_NODE) return el;
        for (var i = 0; i < el.childNodes.length; i++) {
            var found = _firstDeepTextNode(el.childNodes[i]);
            if (found) return found;
        }
        return null;
    }

    function _lastDeepTextNode(el) {
        if (el.nodeType === Node.TEXT_NODE) return el;
        for (var i = el.childNodes.length - 1; i >= 0; i--) {
            var found = _lastDeepTextNode(el.childNodes[i]);
            if (found) return found;
        }
        return null;
    }

    /**
     * Computes selection direction by comparing anchor and focus positions.
     */
    function _computeSelectionDirection(sel) {
        if (!sel || sel.rangeCount === 0 || sel.isCollapsed) {
            return 'forward';
        }
        var range = document.createRange();
        range.setStart(sel.anchorNode, sel.anchorOffset);
        range.setEnd(sel.focusNode, sel.focusOffset);
        var reversed = range.collapsed;
        range.detach();
        return reversed ? 'backward' : 'forward';
    }

    /**
     * Restores a selection from a snapshot.
     * Falls back to the nearest block boundary if the exact inline is missing.
     */
    function _restoreSelection(inst, snapshot) {
        if (!snapshot) return;
        snapshot = _createSelectionSnapshotFromRuntimeSelection(snapshot);
        var root = inst.root;

        var anchorInfo = _resolveSnapshotPosition(
            root,
            snapshot.anchorBlockId || snapshot.AnchorBlockId,
            snapshot.anchorInlineId || snapshot.AnchorInlineId,
            snapshot.anchorOffset ?? snapshot.AnchorOffset,
            snapshot.anchorBlockOffset ?? snapshot.AnchorBlockOffset,
            snapshot);
        var focusInfo = _resolveSnapshotPosition(
            root,
            snapshot.focusBlockId || snapshot.FocusBlockId,
            snapshot.focusInlineId || snapshot.FocusInlineId,
            snapshot.focusOffset ?? snapshot.FocusOffset,
            snapshot.focusBlockOffset ?? snapshot.FocusBlockOffset,
            snapshot);

        if (!anchorInfo || !focusInfo) return;

        var sel = window.getSelection();
        if (!sel) return;
        sel.removeAllRanges();

        var range = document.createRange();
        range.setStart(anchorInfo.node, anchorInfo.offset);
        range.setEnd(focusInfo.node, focusInfo.offset);
        sel.addRange(range);
        inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(snapshot);
    }

    function _resolveSnapshotPosition(root, blockId, inlineId, offset, blockOffset, snapshot) {
        var regionRoot = _resolveSnapshotRegionRoot(root, snapshot);
        var blockEl = regionRoot ? regionRoot.querySelector('[data-block-id="' + _cssEscape(blockId || '') + '"]') : null;
        if (!blockEl) {
            blockEl = root.querySelector('[data-block-id="' + _cssEscape(blockId || '') + '"]');
        }
        if (!blockEl) {
            // Fallback: first block.
            blockEl = (regionRoot || root).querySelector('[data-block-id]');
        }
        if (!blockEl) return null;

        var inlineSelector = '[data-inline-id="' + _cssEscape(inlineId || '') + '"]';
        var inlineMatches = inlineId ? Array.from(blockEl.querySelectorAll(inlineSelector)) : [];
        if (inlineMatches.length > 1 && Number.isFinite(blockOffset)) {
            var duplicateResolvedByBlockOffset = _resolveTextPosition(blockEl, blockOffset);
            if (duplicateResolvedByBlockOffset) {
                return duplicateResolvedByBlockOffset;
            }
        }

        var inlineEl = inlineMatches[0] || null;
        if (!inlineEl) {
            // Fallback after inline split/merge: resolve the closest logical
            // character offset within the block instead of jumping to the first
            // inline wrapper.
            var fallbackOffset = Number.isFinite(blockOffset) ? blockOffset : (offset || 0);
            var blockPosition = _resolveTextPosition(blockEl, fallbackOffset);
            if (blockPosition) {
                return blockPosition;
            }

            inlineEl = blockEl.querySelector('[data-inline-id]');
        }
        if (!inlineEl) {
            // Fallback: place cursor at the beginning of the block.
            return { node: blockEl, offset: 0 };
        }

        var resolved = _resolveTextPosition(inlineEl, offset || 0);
        if (resolved) {
            return resolved;
        }

        if (Number.isFinite(blockOffset)) {
            var resolvedByBlockOffset = _resolveTextPosition(blockEl, blockOffset);
            if (resolvedByBlockOffset) {
                return resolvedByBlockOffset;
            }
        }

        var textNode = _firstDeepTextNode(inlineEl);
        if (!textNode) {
            return { node: inlineEl, offset: 0 };
        }

        var clampedOffset = Math.max(0, Math.min(offset || 0, textNode.textContent.length));
        return { node: textNode, offset: clampedOffset };
    }

    function _resolveSnapshotRegionRoot(root, snapshot) {
        if (!snapshot) return null;
        var region = snapshot.region || snapshot.Region || 'Body';
        var headerFooterId = snapshot.headerFooterId || snapshot.HeaderFooterId || null;
        if ((region === 'Header' || region === 'Footer') && headerFooterId) {
            var selector = (region === 'Header' ? '.tm-wysiwyg-page__header' : '.tm-wysiwyg-page__footer')
                + '[data-hf-id="' + _cssEscape(headerFooterId) + '"]';
            return root.querySelector(selector);
        }

        if (region === 'Body') {
            return root.querySelector('.tm-wysiwyg-page__body[contenteditable]');
        }

        return null;
    }

    // ── Patch dispatch ───────────────────────────────────────────────────────

    function _mapInputTypeToPatchType(inputType) {
        switch (inputType) {
            case 'insertText': return 'InsertText';
            case 'insertParagraph': return 'SplitBlock';
            case 'insertLineBreak': return 'InsertSoftBreak';
            case 'deleteContentBackward': return 'DeleteContentBackward';
            case 'deleteContentForward': return 'DeleteContentForward';
            case 'deleteWordBackward': return 'DeleteContentBackward';
            case 'deleteWordForward': return 'DeleteContentForward';
            case 'formatBold': return 'ToggleMark';
            case 'formatItalic': return 'ToggleMark';
            case 'formatUnderline': return 'ToggleMark';
            default: return 'UnknownInput';
        }
    }

    // ── Render ───────────────────────────────────────────────────────────────

    function _readModelValue(value, pascalKey, camelKey, fallback) {
        if (value && Object.prototype.hasOwnProperty.call(value, pascalKey)) return value[pascalKey];
        if (value && Object.prototype.hasOwnProperty.call(value, camelKey)) return value[camelKey];
        return fallback;
    }

    function _renderNodeId(prefix, id, fallback) {
        var value = id == null || id === '' ? fallback : id;
        return prefix + '-' + String(value == null ? '0' : value).replace(/[^a-z0-9_-]+/gi, '-');
    }

    function _setRuntimeNodeAttributes(el, nodeId, nodeType) {
        if (!el) return;
        if (nodeId != null && nodeId !== '') {
            el.setAttribute('data-node-id', String(nodeId));
        }
        if (nodeType) {
            el.setAttribute('data-runtime-node-type', nodeType);
        }
    }

    function _createRuntimeDocumentFromSnapshot(snapshot) {
        var document = snapshot ? (snapshot.document || snapshot.Document) : null;
        if (!document) return null;
        return {
            version: 1,
            document: document,
            source: 'canonicalSnapshot'
        };
    }

    function _assignSnapshotDocument(snapshot, documentModel) {
        if (!snapshot || !documentModel) return;
        if (Object.prototype.hasOwnProperty.call(snapshot, 'document') && !Object.prototype.hasOwnProperty.call(snapshot, 'Document')) {
            snapshot.document = documentModel;
        } else {
            snapshot.Document = documentModel;
        }
    }

    function _resolveRuntimeDocument(inst) {
        if (inst && inst.runtimeDocument && inst.runtimeDocument.document) {
            return inst.runtimeDocument.document;
        }

        var snapshot = inst ? inst.snapshot : null;
        return snapshot ? (snapshot.document || snapshot.Document) : null;
    }

    function _blockTypeName(block) {
        var type = block ? (block.type ?? block.Type) : null;
        switch (type) {
            case 'Paragraph':
            case 0: return 'paragraph';
            case 'Heading':
            case 1: return 'heading';
            case 'List':
            case 2: return 'list';
            case 'Quote':
            case 3: return 'quote';
            case 'Table':
            case 4: return 'table';
            case 'Image':
            case 5: return 'image';
            case 'PageBreak':
            case 6: return 'pageBreak';
            default: return String(type || 'paragraph').toLowerCase();
        }
    }

    function _inlineTypeName(inline) {
        var type = inline ? (inline.$type || inline.type || inline.Type) : '';
        if (!type && inline && (inline.noteId || inline.NoteId || inline.noteType !== undefined || inline.NoteType !== undefined)) {
            return 'noteReference';
        }

        if (!type && inline && (inline.fieldType !== undefined || inline.FieldType !== undefined || inline.fallbackText !== undefined || inline.FallbackText !== undefined)) {
            return 'field';
        }

        if (!type && inline && (inline.key || inline.Key || inline.tokenType !== undefined || inline.TokenType !== undefined)) {
            return 'token';
        }

        var normalized = String(type || '').toLowerCase();
        if (normalized === 'textrun') return 'text';
        if (normalized === 'tokenrun') return 'token';
        if (normalized === 'documentfieldrun') return 'field';
        if (normalized === 'documentnotereferencerun') return 'noteReference';
        return normalized || 'text';
    }

    function _createInlineRenderPlan(inline, index) {
        var inlineId = inline ? (inline.id || inline.Id || _renderNodeId('inline', null, index)) : _renderNodeId('inline', null, index);
        return {
            id: inlineId,
            type: _inlineTypeName(inline),
            attributes: {
                'data-inline-id': inlineId,
                'data-node-id': inlineId,
                'data-runtime-node-type': 'inline'
            }
        };
    }

    function _createBlockRenderPlan(block, index) {
        var blockId = block ? (block.id || block.Id || _renderNodeId('block', null, index)) : _renderNodeId('block', null, index);
        var content = block ? (block.content || block.Content || {}) : {};
        var typeName = _blockTypeName(block);
        var plan = {
            id: blockId,
            type: typeName,
            attributes: {
                'data-block-id': blockId,
                'data-node-id': blockId,
                'data-runtime-node-type': 'block'
            },
            inlines: []
        };

        if (typeName === 'table') {
            var rows = (content && (content.rows || content.Rows)) || [];
            plan.rows = rows.map(function (row, rowIndex) {
                var cells = (row && (row.cells || row.Cells)) || [];
                return {
                    id: row ? (row.id || row.Id || _renderNodeId('row', null, rowIndex)) : _renderNodeId('row', null, rowIndex),
                    cells: cells.map(function (cell, cellIndex) {
                        var cellId = cell ? (cell.id || cell.Id || _renderNodeId('cell', null, rowIndex + '-' + cellIndex)) : _renderNodeId('cell', null, rowIndex + '-' + cellIndex);
                        var cellBlocks = (cell && (cell.blocks || cell.Blocks)) || [];
                        return {
                            id: cellId,
                            attributes: {
                                'data-cell-id': cellId,
                                'data-node-id': cellId,
                                'data-runtime-node-type': 'table-cell'
                            },
                            blocks: cellBlocks.map(_createBlockRenderPlan)
                        };
                    })
                };
            });
        } else if (typeName === 'image') {
            plan.image = {
                assetId: content ? (content.assetId || content.AssetId || '') : '',
                url: content ? (content.url || content.Url || '') : ''
            };
        } else {
            var inlines = (content && (content.inlines || content.Inlines)) || [];
            plan.inlines = inlines.map(_createInlineRenderPlan);
        }

        return plan;
    }

    function _createHeaderFooterRenderPlan(headerFooter, index) {
        var id = headerFooter ? (headerFooter.id || headerFooter.Id || _renderNodeId('header-footer', null, index)) : _renderNodeId('header-footer', null, index);
        var blocks = (headerFooter && (headerFooter.blocks || headerFooter.Blocks)) || [];
        return {
            id: id,
            attributes: {
                'data-hf-id': id,
                'data-node-id': id,
                'data-runtime-node-type': 'header-footer'
            },
            blocks: blocks.map(_createBlockRenderPlan)
        };
    }

    function _createRenderPlan(document) {
        var doc = document || {};
        var blocks = _readModelValue(doc, 'Blocks', 'blocks', []) || [];
        var notes = _readModelValue(doc, 'Notes', 'notes', []) || [];
        var pages = [{ index: 0, blocks: [], blockIds: [] }];
        for (var i = 0; i < blocks.length; i++) {
            var block = blocks[i];
            var typeName = _blockTypeName(block);
            if (typeName === 'pageBreak') {
                pages.push({ index: pages.length, blocks: [], blockIds: [] });
                continue;
            }

            var page = pages[pages.length - 1];
            page.blocks.push(block);
            var blockId = block ? (block.id || block.Id) : '';
            if (blockId) page.blockIds.push(blockId);
        }

        var headersFooters = _readModelValue(doc, 'HeadersFooters', 'headersFooters', []) || [];
        return {
            source: 'runtimeDocument',
            documentId: _readModelValue(doc, 'DocumentId', 'documentId', ''),
            pages: pages,
            blockPlans: blocks.map(_createBlockRenderPlan),
            headerFooterPlans: headersFooters.map(_createHeaderFooterRenderPlan),
            notePlans: notes.map(_createNoteRenderPlan)
        };
    }

    function _createNoteRenderPlan(note, index) {
        var id = note ? (note.id || note.Id || _renderNodeId('note', null, index)) : _renderNodeId('note', null, index);
        var blocks = (note && (note.blocks || note.Blocks)) || [];
        return {
            id: id,
            type: _normalizeNoteType(note ? (note.type ?? note.Type ?? note.noteType ?? note.NoteType) : null),
            marker: note ? (note.marker || note.Marker || '') : '',
            blocks: blocks.map(_createBlockRenderPlan)
        };
    }

    function _normalizeNoteType(value) {
        if (value === 1 || value === '1') return 'Endnote';
        var raw = String(value == null ? '' : value).toLowerCase();
        return raw === 'endnote' ? 'Endnote' : 'Footnote';
    }

    function _markFullRender(inst, reason) {
        if (!inst || !inst.renderStats) return;
        inst.renderStats.fullRenders++;
        inst.renderStats.lastRenderReason = reason || 'full-render';
    }

    function _markIncrementalRender(inst, reason) {
        if (!inst || !inst.renderStats) return;
        inst.renderStats.incrementalOperations++;
        inst.renderStats.lastRenderReason = reason || 'incremental-operation';
    }

    function _renderDocument(inst, reason) {
        const doc = _resolveRuntimeDocument(inst);
        if (!doc) return;

        _markFullRender(inst, reason || 'runtime-document');
        inst._applyingOwnPatch = true;
        inst.root.innerHTML = '';
        inst.root.removeAttribute('contenteditable');
        // Phase 11: enable paginated layout mode on the host root.
        inst.root.classList.add('tm-wysiwyg-host--paginated');

        const renderPlan = _createRenderPlan(doc);
        const pageSettings = _normalizePageSettings(doc.pageSettings || doc.PageSettings || {});
        _applyDocumentTheme(inst, doc.theme || doc.Theme || {});
        _applyPageMetrics(inst, pageSettings);

        // Phase 12: build lookup maps for sections and headers/footers.
        var sections = doc.sections || doc.Sections || [];
        var headersFooters = doc.headersFooters || doc.HeadersFooters || [];
        var sectionMap = {};
        for (var si = 0; si < sections.length; si++) {
            var s = sections[si];
            var sid = s.id || s.Id;
            if (sid) sectionMap[sid] = s;
        }
        var hfMap = {};
        for (var hi = 0; hi < headersFooters.length; hi++) {
            var h = headersFooters[hi];
            var hid = h.id || h.Id;
            if (hid) hfMap[hid] = h;
        }

        var firstSection = sections.length > 0 ? sections[0] : null;
        inst.renderPlan = renderPlan;
        inst.virtualPages = renderPlan.pages;
        inst.virtualSettings = { pageSettings: pageSettings, firstSection: firstSection, hfMap: hfMap, sectionMap: sectionMap, notes: doc.notes || doc.Notes || [] };
        _renderVirtualizedPages(inst, false);
    }

    function _executeSyncHeaderFooterLayoutCommand(inst, payload) {
        var documentModel = payload && (payload.document || payload.Document);
        if (!documentModel) return;

        var focusIsOutsideEditor = !(document.activeElement && inst.root && inst.root.contains(document.activeElement));
        var preferBodySelection = !!(payload.preferBodySelectionWhenFocusOutside || payload.PreferBodySelectionWhenFocusOutside);
        var selectionSnapshot = payload.selection || payload.Selection || null;
        if (focusIsOutsideEditor && preferBodySelection && inst.lastBodySelectionSnapshot) {
            selectionSnapshot = inst.lastBodySelectionSnapshot;
        }
        if (selectionSnapshot) {
            inst.lastSelectionSnapshot = selectionSnapshot;
            inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(selectionSnapshot);
        }
        inst.suppressNextRenderSelectionRestore = focusIsOutsideEditor;
        var snapshot = inst.snapshot || { ProtocolVersion: 1 };
        _assignSnapshotDocument(snapshot, documentModel);
        inst.snapshot = snapshot;
        inst.runtimeDocument = _createRuntimeDocumentFromSnapshot(snapshot);
        _loadRuntimeRevisionsFromSnapshot(inst, snapshot);
        _loadRuntimeCommentsFromSnapshot(inst, snapshot);
        _renderDocument(inst, 'header-footer-layout-command');
        inst.hasRenderedDocument = true;
        _applyReviewDisplayMode(inst);
        _renderRuntimeCommentDecorations(inst);
    }

    function _renderVirtualizedPages(inst, preserveSelection) {
        if (!inst.virtualPages || inst.virtualPages.length === 0 || !inst.virtualSettings) return;

        var selection = preserveSelection
            ? (inst.virtualSelectionSnapshot || _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot)
            : inst.lastSelectionSnapshot;

        if (preserveSelection) {
            _syncVirtualPagesFromDom(inst);
        }

        var range = _getVirtualRange(inst, selection);
        inst._applyingOwnPatch = true;
        inst.root.innerHTML = '';
        inst.root.removeAttribute('contenteditable');
        inst.root.setAttribute('role', 'textbox');
        inst.root.setAttribute('aria-multiline', 'true');
        inst.root.setAttribute('aria-readonly', inst.readOnly ? 'true' : 'false');
        inst.root.classList.add('tm-wysiwyg-host--paginated');
        _ensureAccessibilityDescription(inst);

        var rendered = 0;
        for (var i = 0; i < inst.virtualPages.length; i++) {
            var pageData = inst.virtualPages[i];
            var pageEl = (i >= range.first && i <= range.last)
                ? _renderPageFromData(inst, pageData)
                : _createVirtualPagePlaceholder(inst, pageData.index);
            inst.root.appendChild(pageEl);
            if (i >= range.first && i <= range.last) rendered++;
        }

        inst.virtualState = {
            enabled: range.enabled,
            totalPages: inst.virtualPages.length,
            renderedPages: rendered,
            virtualizedPages: inst.virtualPages.length - rendered,
            first: range.first,
            last: range.last,
            pageExtent: range.pageExtent
        };

        if (range.scrollTop > 0 && inst.root.scrollHeight > inst.root.clientHeight) {
            inst.root.scrollTop = Math.min(range.scrollTop, inst.root.scrollHeight - inst.root.clientHeight);
        }

        _checkPageOverflow(inst);
        _refreshDocumentFields(inst);
        _refreshNonPrintingCharacters(inst);
        _notifyPageMetrics(inst);
        _notifyActiveHeading(inst);
        inst._applyingOwnPatch = false;

        if (selection) {
            inst.lastSelectionSnapshot = selection;
            inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(selection);
            if (!inst.suppressNextRenderSelectionRestore && _isVirtualSelectionInRange(inst, selection, range)) {
                inst.virtualSelectionSnapshot = null;
                _restoreSelection(inst, selection);
            } else {
                inst.virtualSelectionSnapshot = selection;
            }
        }
        inst.suppressNextRenderSelectionRestore = false;
    }

    function _renderPageFromData(inst, pageData) {
        var settings = inst.virtualSettings;
        var pageEl = _createPageElement(inst, settings.pageSettings);
        pageEl.setAttribute('data-page-index', pageData.index);
        pageEl.setAttribute('data-node-id', _renderNodeId('page', null, pageData.index));
        pageEl.setAttribute('role', 'region');
        pageEl.setAttribute('aria-label', _formatA11yLabel(inst, 'pageLabel', 'PageLabel', 'Page {0}', pageData.index));
        var body = _createBodyElement(inst, pageData.index);
        pageEl.appendChild(body);
        _renderHeaderFooterForPage(inst, pageEl, settings.firstSection, pageData.index, settings.hfMap, settings.sectionMap);

        for (var i = 0; i < pageData.blocks.length; i++) {
            var blockEl = _renderBlock(pageData.blocks[i], inst);
            if (blockEl) body.appendChild(blockEl);
        }

        if (pageData.blocks.length === 0) {
            body.classList.add('tm-wysiwyg-page__body--empty');
            body.setAttribute('data-placeholder', _readStringOption(inst, 'bodyPlaceholder', 'BodyPlaceholder', 'Start typing'));
        }

        _renderNoteRegionsForPage(inst, pageEl, pageData.index);
        return pageEl;
    }

    function _createVirtualPagePlaceholder(inst, pageIndex) {
        var pageEl = _createPageElement(inst, inst.virtualSettings.pageSettings);
        pageEl.classList.add('tm-wysiwyg-page--virtual');
        pageEl.setAttribute('data-page-index', pageIndex);
        pageEl.setAttribute('data-node-id', _renderNodeId('page', null, pageIndex));
        pageEl.setAttribute('aria-hidden', 'true');
        return pageEl;
    }

    function _getVirtualRange(inst, selection) {
        var total = inst.virtualPages.length;
        var threshold = _readNumberOption(inst, 'virtualizationThreshold', 'VirtualizationThreshold', 20);
        var enabled = total > threshold;
        var pageExtent = _estimatePageExtent(inst);
        if (!enabled) {
            return { enabled: false, first: 0, last: total - 1, pageExtent: pageExtent, scrollTop: 0 };
        }

        var buffer = Math.max(0, Math.floor(_readNumberOption(inst, 'virtualizationBuffer', 'VirtualizationBuffer', 2)));
        var viewport = _getVirtualViewport(inst);
        var scrollTop = viewport.scrollTop;
        var viewportHeight = viewport.height;
        var first = Math.max(0, Math.floor(scrollTop / pageExtent) - buffer);
        var last = Math.min(total - 1, Math.ceil((scrollTop + viewportHeight) / pageExtent) + buffer);

        return { enabled: true, first: first, last: last, pageExtent: pageExtent, scrollTop: scrollTop };
    }

    function _isVirtualSelectionInRange(inst, selection, range) {
        if (!selection || !range || !range.enabled) return true;
        var selectedPage = _findVirtualPageIndexForSelection(inst, selection);
        return selectedPage < 0 || (selectedPage >= range.first && selectedPage <= range.last);
    }

    function _getVirtualViewport(inst) {
        if (Number.isFinite(inst._pendingVirtualScrollTop)) {
            var pendingScrollTop = inst._pendingVirtualScrollTop;
            inst._pendingVirtualScrollTop = null;
            return {
                scrollTop: Math.max(0, pendingScrollTop),
                height: (_usesRootScrollViewport(inst) ? inst.root.clientHeight : window.innerHeight) || inst.root.clientHeight || 900
            };
        }

        if (_usesRootScrollViewport(inst)) {
            return {
                scrollTop: inst.root.scrollTop || 0,
                height: inst.root.clientHeight || window.innerHeight || 900
            };
        }

        var rect = inst.root.getBoundingClientRect();
        var rootTop = rect.top + (window.scrollY || window.pageYOffset || 0);
        return {
            scrollTop: Math.max(0, (window.scrollY || window.pageYOffset || 0) - rootTop),
            height: window.innerHeight || inst.root.clientHeight || 900
        };
    }

    function _usesRootScrollViewport(inst) {
        if (!inst || !inst.root || inst.root.scrollHeight <= inst.root.clientHeight + 1) return false;
        if (typeof getComputedStyle !== 'function') return true;
        var style = getComputedStyle(inst.root);
        var overflowY = style.overflowY || style.overflow || '';
        return /auto|scroll|overlay/i.test(overflowY);
    }

    function _scrollVirtualPageToIndex(inst, pageIndex) {
        if (!inst || !inst.root || !inst.virtualPages || inst.virtualPages.length === 0) return false;
        var index = Math.max(0, Math.min(inst.virtualPages.length - 1, Number(pageIndex) || 0));
        var pageExtent = _estimatePageExtent(inst);
        var targetTop = pageExtent * index;
        var usedRootScroll = false;
        inst._pendingVirtualScrollTop = targetTop;
        if (_usesRootScrollViewport(inst)) {
            inst.root.scrollTop = targetTop;
            usedRootScroll = targetTop <= 0 || Math.abs((inst.root.scrollTop || 0) - targetTop) <= 1;
        }

        if (!usedRootScroll && window.scrollTo && inst.root.getBoundingClientRect) {
            var rect = inst.root.getBoundingClientRect();
            var rootTop = rect.top + (window.scrollY || window.pageYOffset || 0);
            window.scrollTo({ top: rootTop + targetTop, behavior: 'auto' });
        }

        _renderVirtualizedPages(inst, true);
        return true;
    }

    function _estimatePageExtent(inst) {
        var page = inst.root.querySelector('.tm-wysiwyg-page');
        if (page) {
            var rect = page.getBoundingClientRect();
            var gap = _readNumberOption(inst, 'pageGapPx', 'PageGapPx', 24);
            if (rect.height > 0) return rect.height + gap;
        }

        var height = (inst.virtualSettings && inst.virtualSettings.pageSettings && inst.virtualSettings.pageSettings.height) || '297mm';
        var fallback = _cssLengthToPx(height, inst.root) + _readNumberOption(inst, 'pageGapPx', 'PageGapPx', 24);
        return fallback > 0 ? fallback : 1146;
    }

    function _cssLengthToPx(value, root) {
        if (!value) return 0;
        if (typeof value === 'number') return value;
        var match = /^([\d.]+)(mm|pt|px|in|cm)?$/i.exec(String(value).trim());
        if (!match) return 0;
        var n = parseFloat(match[1]);
        var unit = (match[2] || 'px').toLowerCase();
        switch (unit) {
            case 'mm': return n * 96 / 25.4;
            case 'cm': return n * 96 / 2.54;
            case 'in': return n * 96;
            case 'pt': return n * 96 / 72;
            default: return n;
        }
    }

    function _readNumberOption(inst, camelName, pascalName, fallback) {
        var value = inst.options ? (inst.options[camelName] ?? inst.options[pascalName]) : undefined;
        var numberValue = Number(value);
        return Number.isFinite(numberValue) ? numberValue : fallback;
    }

    function _readStringOption(inst, camelName, pascalName, fallback) {
        var value = inst && inst.options ? (inst.options[camelName] ?? inst.options[pascalName]) : undefined;
        return typeof value === 'string' && value.length > 0 ? value : fallback;
    }

    function _formatA11yLabel(inst, camelName, pascalName, fallback, pageIndex) {
        var template = _readStringOption(inst, camelName, pascalName, fallback);
        return template.replace('{0}', String((pageIndex || 0) + 1));
    }

    function _ensureAccessibilityDescription(inst) {
        if (!inst || !inst.root) return;
        var id = inst.id + '-accessibility-help';
        var help = document.getElementById(id);
        if (help && !inst.root.contains(help)) {
            help = null;
        }
        if (!help) {
            help = document.createElement('span');
            help.id = id;
            help.className = 'tm-document-wysiwyg-host__sr-only';
            help.setAttribute('data-testid', 'document-wysiwyg-accessibility-help');
            inst.root.appendChild(help);
        }
        help.textContent = _readStringOption(
            inst,
            'accessibilityHelp',
            'AccessibilityHelp',
            'Editable document surface. Use Tab to move between the ribbon, header, body, footer, and comments.');
        inst.root.setAttribute('aria-describedby', id);
    }

    function _findVirtualPageIndexForSelection(inst, selection) {
        if (!selection) return -1;
        var blockId = selection.anchorBlockId || selection.AnchorBlockId || selection.focusBlockId || selection.FocusBlockId;
        if (!blockId) return -1;
        for (var i = 0; i < inst.virtualPages.length; i++) {
            if ((inst.virtualPages[i].blockIds || []).indexOf(blockId) >= 0) return i;
        }
        return -1;
    }

    function _scheduleVirtualizationRefresh(inst) {
        if (!inst || inst.disposed || !inst.virtualState || !inst.virtualState.enabled) return;
        if (inst.virtualizationScrollTimer) {
            clearTimeout(inst.virtualizationScrollTimer);
        }
        inst.virtualizationScrollTimer = setTimeout(function () {
            inst.virtualizationScrollTimer = null;
            _renderVirtualizedPages(inst, true);
        }, 16);
    }

    function _syncVirtualPagesFromDom(inst) {
        if (!inst.virtualPages || inst.virtualPages.length === 0) return;
        var baseDoc = (inst.snapshot && (inst.snapshot.document || inst.snapshot.Document)) || {};
        var blockMap = _createBlockMap(baseDoc);
        var pages = inst.root.querySelectorAll('.tm-wysiwyg-page[data-page-index]:not(.tm-wysiwyg-page--virtual)');
        for (var i = 0; i < pages.length; i++) {
            var pageEl = pages[i];
            var pageIndex = parseInt(pageEl.getAttribute('data-page-index') || '-1', 10);
            if (pageIndex < 0 || !inst.virtualPages[pageIndex]) continue;
            var body = pageEl.querySelector('.tm-wysiwyg-page__body');
            if (!body) continue;
            var blocks = _serializeBodyBlocks(body, blockMap);
            inst.virtualPages[pageIndex].blocks = blocks;
            inst.virtualPages[pageIndex].blockIds = blocks.map(function (b) { return b.Id || b.id; }).filter(Boolean);
        }
    }

    /**
     * Normalizes PageSettings from the snapshot into CSS-friendly values.
     */
    function _normalizePageSettings(settings) {
        var size = settings.size || settings.Size || {};
        var margins = settings.margins || settings.Margins || {};
        var landscape = !!settings.landscape || !!settings.Landscape;

        var widthPt = size.width || size.Width || 595.276;
        var heightPt = size.height || size.Height || 841.89;
        var topPt = margins.top || margins.Top || 72;
        var rightPt = margins.right || margins.Right || 72;
        var bottomPt = margins.bottom || margins.Bottom || 72;
        var leftPt = margins.left || margins.Left || 72;
        var headerPt = settings.headerDistanceFromTop || settings.HeaderDistanceFromTop || 36;
        var footerPt = settings.footerDistanceFromBottom || settings.FooterDistanceFromBottom || 36;

        // Convert points (1 pt = 1/72 inch) to millimetres (1 inch = 25.4 mm).
        function ptToMm(pt) {
            return (pt * 25.4 / 72).toFixed(2) + 'mm';
        }

        return {
            width: ptToMm(landscape ? heightPt : widthPt),
            height: ptToMm(landscape ? widthPt : heightPt),
            marginTop: ptToMm(topPt),
            marginRight: ptToMm(rightPt),
            marginBottom: ptToMm(bottomPt),
            marginLeft: ptToMm(leftPt),
            headerDistanceFromTop: ptToMm(headerPt),
            footerDistanceFromBottom: ptToMm(footerPt)
        };
    }

    function _applyDocumentTheme(inst, theme) {
        if (!inst || !inst.root) return;
        var fontFamily = theme.bodyFontFamily || theme.BodyFontFamily || 'Aptos, Arial, sans-serif';
        var fontSize = _sanitizeNumber(theme.bodyFontSize ?? theme.BodyFontSize, 11, 6, 96);
        var lineHeight = _sanitizeNumber(theme.bodyLineHeight ?? theme.BodyLineHeight, 1.15, 0.8, 3);
        var spacingAfter = _sanitizeNumber(theme.paragraphSpacingAfter ?? theme.ParagraphSpacingAfter, 8, 0, 144);

        inst.root.style.setProperty('--tm-document-body-font-family', fontFamily);
        inst.root.style.setProperty('--tm-document-body-font-size', fontSize + 'pt');
        inst.root.style.setProperty('--tm-document-body-line-height', String(lineHeight));
        inst.root.style.setProperty('--tm-document-paragraph-spacing-after', spacingAfter + 'pt');
    }

    function _sanitizeNumber(value, fallback, min, max) {
        var number = typeof value === 'number' ? value : parseFloat(value);
        if (!Number.isFinite(number)) {
            number = fallback;
        }

        return Math.max(min, Math.min(max, number));
    }

    function _applyPageMetrics(inst, pageSettings) {
        if (!inst || !inst.root || !pageSettings) return;
        inst.root.style.setProperty('--tm-document-page-width', pageSettings.width);
        inst.root.style.setProperty('--tm-document-page-height', pageSettings.height);
        inst.root.style.setProperty('--tm-document-page-margin-top', pageSettings.marginTop);
        inst.root.style.setProperty('--tm-document-page-margin-right', pageSettings.marginRight);
        inst.root.style.setProperty('--tm-document-page-margin-bottom', pageSettings.marginBottom);
        inst.root.style.setProperty('--tm-document-page-margin-left', pageSettings.marginLeft);
        inst.root.style.setProperty('--tm-document-header-distance-from-top', pageSettings.headerDistanceFromTop);
        inst.root.style.setProperty('--tm-document-footer-distance-from-bottom', pageSettings.footerDistanceFromBottom);
        inst.lastPageSettings = pageSettings;
        _updateRenderedPageMetrics(inst, pageSettings);
        _updatePageWidthFitZoom(inst, pageSettings);
    }

    function _updateRenderedPageMetrics(inst, pageSettings) {
        if (!inst || !inst.root || !pageSettings) return;
        inst.root.querySelectorAll('.tm-wysiwyg-page').forEach(function (page) {
            page.style.width = pageSettings.width;
            page.style.minHeight = pageSettings.height;
            page.style.paddingTop = pageSettings.marginTop;
            page.style.paddingRight = pageSettings.marginRight;
            page.style.paddingBottom = pageSettings.marginBottom;
            page.style.paddingLeft = pageSettings.marginLeft;
            page.style.setProperty('--tm-document-header-distance-from-top', pageSettings.headerDistanceFromTop);
            page.style.setProperty('--tm-document-footer-distance-from-bottom', pageSettings.footerDistanceFromBottom);
        });
    }

    function _updatePageWidthFitZoom(inst, pageSettings) {
        if (!inst || !inst.root) return;
        var settings = pageSettings || inst.lastPageSettings || inst.virtualSettings?.pageSettings || null;
        if (!settings) return;

        if (!inst.root.classList.contains('tm-document-wysiwyg-host--zoom-page-width')) {
            inst.root.style.setProperty('--tm-document-page-fit-zoom', '1');
            return;
        }

        var pageWidthPx = _cssLengthToPx(settings.width);
        var style = getComputedStyle(inst.root);
        var paddingLeft = parseFloat(style.paddingLeft || '0') || 0;
        var paddingRight = parseFloat(style.paddingRight || '0') || 0;
        var availableWidth = Math.max(240, (inst.root.clientWidth || pageWidthPx) - paddingLeft - paddingRight);
        var zoom = pageWidthPx > 0 ? Math.min(1, availableWidth / pageWidthPx) : 1;
        inst.root.style.setProperty('--tm-document-page-fit-zoom', Math.max(0.25, Math.min(1, zoom)).toFixed(4));
    }

    function _cssLengthToPx(value) {
        if (typeof value === 'number') return value;
        var raw = String(value || '').trim().toLowerCase();
        var number = parseFloat(raw);
        if (!Number.isFinite(number) || number <= 0) return 0;
        if (raw.endsWith('mm')) return number * 96 / 25.4;
        if (raw.endsWith('cm')) return number * 96 / 2.54;
        if (raw.endsWith('in')) return number * 96;
        if (raw.endsWith('pt')) return number * 96 / 72;
        return number;
    }

    /**
     * Creates a single A4 page wrapper element.
     */
    function _createPageElement(inst, settings) {
        var page = document.createElement('div');
        page.className = 'tm-wysiwyg-page';
        _setRuntimeNodeAttributes(page, _renderNodeId('page', null, 'pending'), 'page');
        page.style.width = settings.width;
        page.style.minHeight = settings.height;
        page.style.paddingTop = settings.marginTop;
        page.style.paddingRight = settings.marginRight;
        page.style.paddingBottom = settings.marginBottom;
        page.style.paddingLeft = settings.marginLeft;
        page.style.setProperty('--tm-document-header-distance-from-top', settings.headerDistanceFromTop);
        page.style.setProperty('--tm-document-footer-distance-from-bottom', settings.footerDistanceFromBottom);
        return page;
    }

    /**
     * Phase 12: Creates the body content container inside a page.
     */
    function _createBodyElement(inst, pageIndex) {
        var body = document.createElement('div');
        body.className = 'tm-wysiwyg-page__body';
        body.setAttribute('role', 'textbox');
        body.setAttribute('aria-multiline', 'true');
        body.setAttribute('aria-readonly', inst && inst.readOnly ? 'true' : 'false');
        body.setAttribute('aria-label', _formatA11yLabel(inst, 'bodyLabel', 'BodyLabel', 'Document body, page {0}', pageIndex || 0));
        body.setAttribute('tabindex', inst && inst.readOnly ? '-1' : '0');
        body.setAttribute('contenteditable', inst && inst.readOnly ? 'false' : 'true');
        body.setAttribute('data-region', 'Body');
        body.setAttribute('data-testid', 'document-wysiwyg-body');
        _setRuntimeNodeAttributes(body, _renderNodeId('body', null, pageIndex || 0), 'body');
        return body;
    }

    /**
     * Phase 12: Renders header and footer regions for a single page.
     */
    function _renderHeaderFooterForPage(inst, pageEl, section, pageIndex, hfMap, sectionMap) {
        if (!section) return;

        var props = section.properties || section.Properties || {};
        var refs = props.headerFooterReferences || props.HeaderFooterReferences || [];
        var differentFirst = !!props.differentFirstPage || !!props.DifferentFirstPage;
        var differentOddEven = !!props.differentOddAndEvenPages || !!props.DifferentOddAndEvenPages;

        // Resolve header.
        var headerHf = _resolveHeaderFooter('Header', pageIndex, differentFirst, differentOddEven, refs, hfMap, sectionMap);
        if (headerHf) {
            var headerEl = _renderHeaderFooterRegion(inst, headerHf, 'header', pageIndex);
            // Insert header before body.
            var bodyEl = pageEl.querySelector('.tm-wysiwyg-page__body');
            if (bodyEl) {
                pageEl.insertBefore(headerEl, bodyEl);
            } else {
                pageEl.appendChild(headerEl);
            }
        }

        // Resolve footer.
        var footerHf = _resolveHeaderFooter('Footer', pageIndex, differentFirst, differentOddEven, refs, hfMap, sectionMap);
        if (footerHf) {
            var footerEl = _renderHeaderFooterRegion(inst, footerHf, 'footer', pageIndex);
            pageEl.appendChild(footerEl);
        }
    }

    /**
     * Phase 12: Resolves the correct header/footer for a given page index.
     */
    function _resolveHeaderFooter(hfType, pageIndex, differentFirst, differentOddEven, refs, hfMap, sectionMap) {
        // Determine scope based on page index and section settings.
        var scope = 'Primary';
        if (pageIndex === 0 && differentFirst) {
            scope = 'FirstPage';
        } else if (differentOddEven) {
            scope = ((Math.max(0, pageIndex) + 1) % 2 === 0) ? 'EvenPages' : 'OddPages';
        }

        // Look for a matching reference.
        var targetRef = null;
        for (var i = 0; i < refs.length; i++) {
            var ref = refs[i];
            var refType = _normalizeHeaderFooterType(ref.type ?? ref.Type);
            var refScope = _normalizeHeaderFooterScope(ref.scope ?? ref.Scope);
            if (refType === hfType && refScope === scope) {
                targetRef = ref;
                break;
            }
        }

        if (!targetRef) {
            // Fallback to Primary scope for the same type.
            for (var j = 0; j < refs.length; j++) {
                var ref2 = refs[j];
                var ref2Type = _normalizeHeaderFooterType(ref2.type ?? ref2.Type);
                var ref2Scope = _normalizeHeaderFooterScope(ref2.scope ?? ref2.Scope);
                if (ref2Type === hfType && ref2Scope === 'Primary') {
                    targetRef = ref2;
                    break;
                }
            }
        }

        if (!targetRef) return null;

        var hfId = targetRef.headerFooterId || targetRef.HeaderFooterId || '';
        return hfMap[hfId] || null;
    }

    function _normalizeHeaderFooterType(value) {
        if (value === 0 || value === '0') return 'Header';
        if (value === 1 || value === '1') return 'Footer';
        var raw = String(value == null ? '' : value).toLowerCase();
        return raw === 'footer' ? 'Footer' : raw === 'header' ? 'Header' : '';
    }

    function _normalizeHeaderFooterScope(value) {
        if (value === 1 || value === '1') return 'FirstPage';
        if (value === 2 || value === '2') return 'EvenPages';
        if (value === 3 || value === '3') return 'OddPages';
        var raw = String(value == null ? '' : value).toLowerCase();
        if (raw === 'firstpage' || raw === 'first') return 'FirstPage';
        if (raw === 'evenpages' || raw === 'even') return 'EvenPages';
        if (raw === 'oddpages' || raw === 'odd') return 'OddPages';
        return 'Primary';
    }

    function _formatHeaderFooterRegionLabel(type, scope) {
        var normalizedScope = _normalizeHeaderFooterScope(scope);
        var prefix = type === 'header' ? 'Header' : 'Footer';
        switch (normalizedScope) {
            case 'FirstPage':
                return type === 'header' ? 'First page header' : 'First page footer';
            case 'EvenPages':
                return type === 'header' ? 'Even page header' : 'Even page footer';
            case 'OddPages':
                return type === 'header' ? 'Odd page header' : 'Odd page footer';
            default:
                return prefix + ' - Primary';
        }
    }

    /**
     * Phase 12: Renders a header or footer region into a DOM element.
     */
    function _renderHeaderFooterRegion(inst, hf, type, pageIndex) {
        var el = document.createElement('div');
        var headerFooterId = hf.id || hf.Id || '';
        el.className = 'tm-wysiwyg-page__' + type;
        el.setAttribute('data-hf-id', headerFooterId);
        el.setAttribute('data-hf-type', type);
        el.setAttribute('data-hf-scope', hf.scope || hf.Scope || 'Primary');
        el.setAttribute('data-region-label', _formatHeaderFooterRegionLabel(type, hf.scope || hf.Scope || 'Primary'));
        el.setAttribute('data-region', type === 'header' ? 'Header' : 'Footer');
        el.setAttribute(
            'data-placeholder',
            _readStringOption(
                inst,
                type === 'header' ? 'headerPlaceholder' : 'footerPlaceholder',
                type === 'header' ? 'HeaderPlaceholder' : 'FooterPlaceholder',
                type === 'header' ? 'Header' : 'Footer'));
        el.setAttribute('role', 'textbox');
        el.setAttribute('aria-multiline', 'true');
        el.setAttribute('aria-readonly', inst && inst.readOnly ? 'true' : 'false');
        el.setAttribute(
            'aria-label',
            _formatA11yLabel(
                inst,
                type === 'header' ? 'headerLabel' : 'footerLabel',
                type === 'header' ? 'HeaderLabel' : 'FooterLabel',
                type === 'header' ? 'Header, page {0}' : 'Footer, page {0}',
                pageIndex || 0));
        el.setAttribute('tabindex', inst && inst.readOnly ? '-1' : '0');
        el.setAttribute('contenteditable', inst && inst.readOnly ? 'false' : 'true');
        el.setAttribute('data-testid', type === 'header' ? 'document-wysiwyg-header' : 'document-wysiwyg-footer');
        _setRuntimeNodeAttributes(el, headerFooterId || _renderNodeId(type, null, pageIndex || 0), 'header-footer');

        var blocks = hf.blocks || hf.Blocks || [];
        for (var i = 0; i < blocks.length; i++) {
            var blockEl = _renderBlock(blocks[i], inst);
            if (blockEl) {
                el.appendChild(blockEl);
            }
        }

        if (blocks.length === 0 || !(el.textContent || '').trim()) {
            el.classList.add('tm-wysiwyg-page__' + type + '--empty');
        }

        return el;
    }

    function _renderNoteRegionsForPage(inst, pageEl, pageIndex) {
        var notes = (inst.virtualSettings && inst.virtualSettings.notes) || [];
        if (!notes || notes.length === 0 || pageIndex !== 0) return;

        var footnotes = [];
        var endnotes = [];
        for (var i = 0; i < notes.length; i++) {
            var note = notes[i];
            if (_normalizeNoteType(note.type ?? note.Type ?? note.noteType ?? note.NoteType) === 'Endnote') {
                endnotes.push(note);
            } else {
                footnotes.push(note);
            }
        }

        if (footnotes.length > 0) {
            pageEl.appendChild(_renderNoteRegion(inst, footnotes, 'Footnote', pageIndex));
        }

        if (endnotes.length > 0) {
            pageEl.appendChild(_renderNoteRegion(inst, endnotes, 'Endnote', pageIndex));
        }
    }

    function _renderNoteRegion(inst, notes, region, pageIndex) {
        var el = document.createElement('div');
        el.className = 'tm-wysiwyg-page__notes tm-wysiwyg-page__notes--' + region.toLowerCase();
        el.setAttribute('data-region', region);
        el.setAttribute('data-testid', region === 'Endnote' ? 'document-wysiwyg-endnotes' : 'document-wysiwyg-footnotes');
        el.setAttribute('role', 'group');
        el.setAttribute('aria-label', region === 'Endnote' ? 'Endnotes' : 'Footnotes');
        el.setAttribute('tabindex', inst && inst.readOnly ? '-1' : '0');
        el.setAttribute('contenteditable', inst && inst.readOnly ? 'false' : 'true');
        _setRuntimeNodeAttributes(el, _renderNodeId(region.toLowerCase(), null, pageIndex || 0), region.toLowerCase());

        for (var i = 0; i < notes.length; i++) {
            var note = notes[i];
            var item = document.createElement('section');
            var noteId = note.id || note.Id || _renderNodeId('note', null, i);
            item.className = 'tm-wysiwyg-note';
            item.setAttribute('data-note-id', noteId);
            item.setAttribute('data-node-id', noteId);
            item.setAttribute('data-runtime-node-type', 'note');
            var marker = note.marker || note.Marker || String(i + 1);
            if (marker) {
                var markerEl = document.createElement('span');
                markerEl.className = 'tm-wysiwyg-note__marker';
                markerEl.textContent = marker;
                markerEl.setAttribute('contenteditable', 'false');
                item.appendChild(markerEl);
            }

            var blocks = note.blocks || note.Blocks || [];
            for (var bi = 0; bi < blocks.length; bi++) {
                var blockEl = _renderBlock(blocks[bi], inst);
                if (blockEl) item.appendChild(blockEl);
            }

            el.appendChild(item);
        }

        return el;
    }

    /**
     * Phase 11: checks whether any page content overflows its declared page height
     * and adds a non-invasive warning indicator.
     */
    function _checkPageOverflow(inst) {
        var pages = inst.root.querySelectorAll('.tm-wysiwyg-page');
        for (var i = 0; i < pages.length; i++) {
            var page = pages[i];
            if (page.classList.contains('tm-wysiwyg-page--virtual')) continue;

            page.classList.remove('tm-wysiwyg-page--overflow');
            var warning = page.querySelector('.tm-wysiwyg-page__overflow-warning');
            if (warning) {
                warning.remove();
            }

            var blockEls = page.querySelectorAll('.tm-wysiwyg-block[data-block-id]');
            for (var bi = 0; bi < blockEls.length; bi++) {
                _measureBlock(inst, blockEls[bi]);
            }

            var declaredPageHeight = _getDeclaredPageHeight(page);
            var visiblePageHeight = declaredPageHeight || page.clientHeight;
            if (visiblePageHeight > 0 && page.scrollHeight > visiblePageHeight + 2) {
                page.classList.add('tm-wysiwyg-page--overflow');
                warning = document.createElement('div');
                warning.className = 'tm-wysiwyg-page__overflow-warning';
                warning.setAttribute('data-testid', 'document-page-overflow-warning');
                warning.setAttribute('role', 'status');
                warning.setAttribute('aria-live', 'polite');
                var warningText = _readStringOption(inst, 'pageOverflowWarning', 'PageOverflowWarning', 'Content overflows page');
                var insertBreakText = _readStringOption(inst, 'insertPageBreakLabel', 'InsertPageBreakLabel', 'Insert page break');
                var warningLabel = document.createElement('span');
                warningLabel.textContent = warningText;
                warning.appendChild(warningLabel);
                var insertBreakAction = document.createElement('button');
                insertBreakAction.type = 'button';
                insertBreakAction.setAttribute('data-testid', 'document-page-overflow-insert-page-break');
                insertBreakAction.textContent = insertBreakText;
                warning.appendChild(insertBreakAction);
                var action = warning.querySelector('button');
                if (action) {
                    action.addEventListener('click', function (evt) {
                        evt.preventDefault();
                        evt.stopPropagation();
                        executeCommand(inst.id, 'insertPageBreak', {});
                    });
                }
                page.appendChild(warning);
            }
        }
    }

    function _getDeclaredPageHeight(page) {
        if (!page || !page.ownerDocument || !page.ownerDocument.defaultView) return 0;
        var style = page.ownerDocument.defaultView.getComputedStyle(page);
        var minHeight = parseFloat(style.minHeight || '');
        if (Number.isFinite(minHeight) && minHeight > 0) return minHeight;
        var height = parseFloat(style.height || '');
        return Number.isFinite(height) && height > 0 ? height : 0;
    }

    function _buildPageMetrics(inst) {
        if (!inst) {
            return { TotalPages: 0, RenderedPages: 0, VirtualizedPages: 0, ActivePageIndex: 0, Pages: [] };
        }

        var virtualPages = inst.virtualPages || [];
        var domPages = inst.root ? Array.from(inst.root.querySelectorAll('.tm-wysiwyg-page[data-page-index]')) : [];
        var explicitPageBreaks = inst.root ? Array.from(inst.root.querySelectorAll('.tm-wysiwyg-page-break[data-block-id]')) : [];
        var activePage = _getActivePageIndexFromViewport(inst, domPages);
        var pages = virtualPages.length > 0
            ? virtualPages.map(function (page, index) {
                var pageIndex = page.index ?? page.Index ?? index;
                var domPage = domPages.find(function (candidate) {
                    return parseInt(candidate.getAttribute('data-page-index') || '-1', 10) === pageIndex;
                });
                return {
                    PageIndex: pageIndex,
                    PageNumber: pageIndex + 1,
                    Label: _formatA11yLabel(inst, 'pageLabel', 'PageLabel', 'Page {0}', pageIndex),
                    IsVirtual: !!(domPage && domPage.classList.contains('tm-wysiwyg-page--virtual')),
                    HasOverflow: !!(domPage && domPage.classList.contains('tm-wysiwyg-page--overflow')),
                    BlockIds: (page.blockIds || page.BlockIds || [])
                };
            })
            : domPages.map(function (page, index) {
                var pageIndex = parseInt(page.getAttribute('data-page-index') || String(index), 10);
                if (!Number.isFinite(pageIndex)) pageIndex = index;
                return {
                    PageIndex: pageIndex,
                    PageNumber: pageIndex + 1,
                    Label: _formatA11yLabel(inst, 'pageLabel', 'PageLabel', 'Page {0}', pageIndex),
                    IsVirtual: page.classList.contains('tm-wysiwyg-page--virtual'),
                    HasOverflow: page.classList.contains('tm-wysiwyg-page--overflow'),
                    BlockIds: Array.from(page.querySelectorAll('[data-block-id]')).map(function (block) { return block.getAttribute('data-block-id') || ''; }).filter(Boolean)
                };
            });

        var minimumPageCount = Math.max(pages.length, explicitPageBreaks.length + 1);
        while (pages.length < minimumPageCount) {
            var pageIndex = pages.length;
            pages.push({
                PageIndex: pageIndex,
                PageNumber: pageIndex + 1,
                Label: _formatA11yLabel(inst, 'pageLabel', 'PageLabel', 'Page {0}', pageIndex),
                IsVirtual: true,
                HasOverflow: false,
                BlockIds: []
            });
        }

        var renderedPages = pages.filter(function (page) { return !page.IsVirtual; }).length;
        return {
            TotalPages: pages.length,
            RenderedPages: renderedPages,
            VirtualizedPages: pages.length - renderedPages,
            ActivePageIndex: Math.max(0, Math.min(activePage, Math.max(0, pages.length - 1))),
            Pages: pages
        };
    }

    function _getActivePageIndexFromViewport(inst, pages) {
        pages = pages || [];
        if (pages.length === 0) return 0;
        var rootRect = inst.root && inst.root.getBoundingClientRect
            ? inst.root.getBoundingClientRect()
            : { top: 0, height: (typeof window !== 'undefined' && window.innerHeight) || 900 };
        var targetY = rootRect.top + Math.min(Math.max(rootRect.height * 0.25, 80), 220);
        var best = { index: 0, distance: Number.POSITIVE_INFINITY };
        pages.forEach(function (page) {
            if (!page.getBoundingClientRect) return;
            var rect = page.getBoundingClientRect();
            var distance = Math.abs(rect.top - targetY);
            var index = parseInt(page.getAttribute('data-page-index') || '0', 10);
            if (distance < best.distance && Number.isFinite(index)) {
                best = { index: index, distance: distance };
            }
        });
        return best.index;
    }

    function _notifyPageMetrics(inst) {
        if (!inst || !inst.root) return;
        var metrics = _buildPageMetrics(inst);
        var json = JSON.stringify(metrics);
        if (json === inst.lastPageMetricsJson) return;
        inst.lastPageMetricsJson = json;
        _invokeDotNet(inst, 'HandlePageMetricsChanged', metrics);
    }

    function _notifyActiveHeading(inst) {
        if (!inst || !inst.root) return;
        var headings = Array.from(inst.root.querySelectorAll('h1[data-block-id], h2[data-block-id], h3[data-block-id], h4[data-block-id], h5[data-block-id], h6[data-block-id]'));
        var active = _findActiveHeadingBlockId(inst, headings);
        if (active === inst.lastActiveHeadingBlockId) return;
        inst.lastActiveHeadingBlockId = active;
        _invokeDotNet(inst, 'HandleActiveHeadingChanged', active || null);
    }

    function _findActiveHeadingBlockId(inst, headings) {
        if (!headings || headings.length === 0) return null;
        var rootCanScroll = !!(inst && inst.root && inst.root.scrollHeight > inst.root.clientHeight + 2);
        if (rootCanScroll && inst.root.scrollTop >= inst.root.scrollHeight - inst.root.clientHeight - 2) {
            var visibleAtEnd = _findLastVisibleHeading(inst, headings);
            if (visibleAtEnd) return visibleAtEnd;
        }

        var threshold = _getActiveHeadingThreshold(inst);
        var best = null;
        headings.forEach(function (heading) {
            if (!heading.getBoundingClientRect) return;
            var rect = heading.getBoundingClientRect();
            if (rect.top <= threshold) {
                best = heading;
            } else if (!best) {
                best = heading;
            }
        });
        return best ? (best.getAttribute('data-block-id') || null) : null;
    }

    function _findLastVisibleHeading(inst, headings) {
        if (!inst || !inst.root || !inst.root.getBoundingClientRect) return null;
        var rootRect = inst.root.getBoundingClientRect();
        var best = null;
        headings.forEach(function (heading) {
            if (!heading.getBoundingClientRect) return;
            var rect = heading.getBoundingClientRect();
            if (rect.bottom >= rootRect.top && rect.top <= rootRect.bottom) {
                best = heading;
            }
        });
        return best ? (best.getAttribute('data-block-id') || null) : null;
    }

    function _getActiveHeadingThreshold(inst) {
        var viewportHeight = (typeof window !== 'undefined' && window.innerHeight) || 900;
        var viewportThreshold = Math.min(Math.max(viewportHeight * 0.2, 96), 240);
        if (!inst || !inst.root || !inst.root.getBoundingClientRect) return viewportThreshold;

        var rootRect = inst.root.getBoundingClientRect();
        var rootThreshold = Math.min(Math.max(rootRect.height * 0.2, 96), 240);
        var rootCanScroll = inst.root.scrollHeight > inst.root.clientHeight + 2;
        return rootCanScroll ? rootRect.top + rootThreshold : viewportThreshold;
    }

    function _measureBlock(inst, blockEl) {
        if (!inst || !blockEl) return null;

        var key = _getMeasureCacheKey(blockEl);
        var cached = inst.measureCache.get(key);
        if (cached) {
            inst.measureStats.cacheHits++;
            return cached;
        }

        var rect = blockEl.getBoundingClientRect();
        var metrics = {
            width: rect.width,
            height: rect.height,
            scrollWidth: blockEl.scrollWidth || 0,
            scrollHeight: blockEl.scrollHeight || 0
        };

        inst.measureStats.count++;
        inst.measureCache.set(key, metrics);
        if (inst.measureCache.size > 512) {
            inst.measureCache.delete(inst.measureCache.keys().next().value);
        }

        return metrics;
    }

    function _getMeasureCacheKey(blockEl) {
        var computed = window.getComputedStyle ? window.getComputedStyle(blockEl) : blockEl.style;
        var parentWidth = blockEl.parentElement ? blockEl.parentElement.clientWidth : 0;
        return [
            blockEl.getAttribute('data-block-id') || '',
            blockEl.textContent || '',
            blockEl.className || '',
            blockEl.getAttribute('style') || '',
            parentWidth,
            computed.fontFamily || '',
            computed.fontSize || '',
            computed.fontWeight || '',
            computed.lineHeight || '',
            computed.letterSpacing || '',
            computed.whiteSpace || ''
        ].join('|');
    }

    function _invalidateMeasureCache(inst) {
        if (!inst || !inst.measureCache) return;
        if (inst.measureCache.size > 0) {
            inst.measureStats.invalidations++;
        }
        inst.measureCache.clear();
    }

    function _renderBlock(block, inst) {
        var type = block.type || block.Type;
        var id = block.id || block.Id;
        var content = block.content || block.Content;
        var el;

        switch (type) {
            case 'Paragraph':
            case 0:
                el = document.createElement('p');
                _renderInlines(el, content, inst);
                break;
            case 'Heading':
            case 1:
                el = document.createElement('h' + ((content && (content.level || content.Level)) || 1));
                _renderInlines(el, content, inst);
                break;
            case 'List':
            case 2:
                el = document.createElement((content && (content.ordered || content.Ordered)) ? 'ol' : 'ul');
                if (content && (content.startNumber || content.StartNumber) && el.tagName.toLowerCase() === 'ol') {
                    el.setAttribute('start', String(Math.max(1, parseInt(content.startNumber || content.StartNumber, 10) || 1)));
                }
                var li = document.createElement('li');
                _renderInlines(li, content, inst);
                el.appendChild(li);
                if (content && (content.indentLevel ?? content.IndentLevel) != null) {
                    el.style.marginLeft = _sanitizeParagraphPoints((content.indentLevel ?? content.IndentLevel) * 36, 0, 432) + 'pt';
                }
                break;
            case 'Quote':
            case 3:
                el = document.createElement('blockquote');
                _renderInlines(el, content, inst);
                break;
            case 'Table':
            case 4:
                el = _renderTable(content, inst);
                break;
            case 'Image':
            case 5:
                el = _renderImage(content, inst);
                break;
            case 'PageBreak':
            case 6:
                el = document.createElement('hr');
                el.className = 'tm-wysiwyg-page-break';
                el.setAttribute('role', 'separator');
                el.setAttribute('data-testid', 'document-wysiwyg-page-break');
                el.setAttribute('aria-label', 'Page break');
                break;
            default:
                el = document.createElement('p');
                _renderInlines(el, content, inst);
                break;
        }

        if (el) {
            el.setAttribute('data-block-id', id || '');
            var order = block.order ?? block.Order;
            _setRuntimeNodeAttributes(el, id || _renderNodeId('block', null, order ?? 0), 'block');
            if (order != null) el.setAttribute('data-block-order', String(order));
            el.classList.add('tm-wysiwyg-block');
            _applyParagraphProperties(el, block.paragraphProperties || block.ParagraphProperties);
            if ((type === 'List' || type === 2) && content && (content.indentLevel ?? content.IndentLevel) != null) {
                el.style.marginLeft = _sanitizeParagraphPoints((content.indentLevel ?? content.IndentLevel) * 36, 0, 432) + 'pt';
            }
            _appendSuggestionDecorations(el, id, inst);
        }

        return el;
    }

    function _appendSuggestionDecorations(blockEl, blockId, inst) {
        if (!blockEl || !blockId || !inst || !inst.snapshot) return;

        var suggestions = inst.snapshot.suggestions || inst.snapshot.Suggestions || [];
        for (var i = 0; i < suggestions.length; i++) {
            var suggestion = suggestions[i];
            var status = suggestion.status ?? suggestion.Status;
            var range = suggestion.range || suggestion.Range || {};
            var targetBlockId = range.blockId || range.BlockId;
            if (status !== 'Pending' && status !== 0) continue;
            if (targetBlockId !== blockId) continue;

            var type = suggestion.type ?? suggestion.Type;
            var isDelete = type === 'DeleteText' || type === 1;
            var span = document.createElement('span');
            span.className = 'tm-document-suggestion tm-document-suggestion--' + (isDelete ? 'delete' : 'insert');
            span.setAttribute('data-testid', isDelete ? 'document-wysiwyg-suggestion-delete' : 'document-wysiwyg-suggestion-insert');
            span.setAttribute('aria-label', _readStringOption(
                inst,
                isDelete ? 'suggestionDeleteLabel' : 'suggestionInsertLabel',
                isDelete ? 'SuggestionDeleteLabel' : 'SuggestionInsertLabel',
                isDelete ? 'Suggested deletion' : 'Suggested insertion'));
            span.textContent = isDelete
                ? (suggestion.originalText || suggestion.OriginalText || '')
                : (suggestion.suggestedText || suggestion.SuggestedText || '');
            blockEl.appendChild(span);
        }
    }

    function _applyParagraphProperties(blockEl, properties) {
        if (!blockEl || !properties) return;

        var alignment = properties.alignment ?? properties.Alignment;
        var lineSpacing = properties.lineSpacing ?? properties.LineSpacing;
        var spacingBefore = properties.spacingBefore ?? properties.SpacingBefore;
        var spacingAfter = properties.spacingAfter ?? properties.SpacingAfter;
        var leftIndent = properties.leftIndent ?? properties.LeftIndent;
        var rightIndent = properties.rightIndent ?? properties.RightIndent;
        var firstLineIndent = properties.firstLineIndent ?? properties.FirstLineIndent;

        if (alignment != null) {
            blockEl.style.textAlign = _alignmentToCss(alignment);
        }
        if (lineSpacing != null) {
            var line = _sanitizeLineSpacing(lineSpacing);
            if (line != null) blockEl.style.lineHeight = String(line);
        }
        if (spacingBefore != null) {
            blockEl.style.marginTop = _sanitizeParagraphPoints(spacingBefore, 0, 144) + 'pt';
        }
        if (spacingAfter != null) {
            blockEl.style.marginBottom = _sanitizeParagraphPoints(spacingAfter, 0, 144) + 'pt';
        }
        if (leftIndent != null) {
            blockEl.style.marginLeft = _sanitizeParagraphPoints(leftIndent, 0, 432) + 'pt';
        }
        if (rightIndent != null) {
            blockEl.style.marginRight = _sanitizeParagraphPoints(rightIndent, 0, 432) + 'pt';
        }
        if (firstLineIndent != null) {
            blockEl.style.textIndent = _sanitizeParagraphPoints(firstLineIndent, -216, 216) + 'pt';
        }
    }

    function _applyParagraphPropertiesPatch(blockEl, patch) {
        if (!blockEl || !patch) return;

        var current = _serializeParagraphProperties(blockEl, null);
        var next = {
            Alignment: current.Alignment,
            LineSpacing: current.LineSpacing,
            SpacingBefore: current.SpacingBefore,
            SpacingAfter: current.SpacingAfter,
            LeftIndent: current.LeftIndent,
            RightIndent: current.RightIndent,
            FirstLineIndent: current.FirstLineIndent
        };

        var alignment = patch.alignment ?? patch.Alignment;
        var lineSpacing = patch.lineSpacing ?? patch.LineSpacing;
        var spacingBefore = patch.spacingBefore ?? patch.SpacingBefore;
        var spacingAfter = patch.spacingAfter ?? patch.SpacingAfter;
        var leftIndent = patch.leftIndent ?? patch.LeftIndent;
        var rightIndent = patch.rightIndent ?? patch.RightIndent;
        var firstLineIndent = patch.firstLineIndent ?? patch.FirstLineIndent;
        var leftIndentDelta = patch.leftIndentDelta ?? patch.LeftIndentDelta;
        var rightIndentDelta = patch.rightIndentDelta ?? patch.RightIndentDelta;
        var firstLineIndentDelta = patch.firstLineIndentDelta ?? patch.FirstLineIndentDelta;

        if (alignment != null) next.Alignment = _alignmentToNumber(alignment);
        if (lineSpacing != null) next.LineSpacing = _sanitizeLineSpacing(lineSpacing);
        if (spacingBefore != null) next.SpacingBefore = _sanitizeParagraphPoints(spacingBefore, 0, 144);
        if (spacingAfter != null) next.SpacingAfter = _sanitizeParagraphPoints(spacingAfter, 0, 144);
        if (leftIndent != null) next.LeftIndent = _sanitizeParagraphPoints(leftIndent, 0, 432);
        if (rightIndent != null) next.RightIndent = _sanitizeParagraphPoints(rightIndent, 0, 432);
        if (firstLineIndent != null) next.FirstLineIndent = _sanitizeParagraphPoints(firstLineIndent, -216, 216);
        if (leftIndentDelta != null) next.LeftIndent = _sanitizeParagraphPoints(next.LeftIndent + _toNumber(leftIndentDelta, 0), 0, 432);
        if (rightIndentDelta != null) next.RightIndent = _sanitizeParagraphPoints(next.RightIndent + _toNumber(rightIndentDelta, 0), 0, 432);
        if (firstLineIndentDelta != null) next.FirstLineIndent = _sanitizeParagraphPoints(next.FirstLineIndent + _toNumber(firstLineIndentDelta, 0), -216, 216);

        _applyParagraphProperties(blockEl, next);
    }

    function _alignmentToCss(value) {
        switch (_alignmentToNumber(value)) {
            case 1: return 'center';
            case 2: return 'right';
            case 3: return 'justify';
            default: return 'left';
        }
    }

    function _alignmentToNumber(value) {
        if (typeof value === 'number' && Number.isFinite(value)) {
            return Math.max(0, Math.min(3, Math.round(value)));
        }

        switch (String(value || '').trim().toLowerCase()) {
            case 'center': return 1;
            case 'right': return 2;
            case 'justify': return 3;
            default: return 0;
        }
    }

    function _cssAlignmentToNumber(value) {
        switch (String(value || '').trim().toLowerCase()) {
            case 'center': return 1;
            case 'right':
            case 'end': return 2;
            case 'justify': return 3;
            default: return 0;
        }
    }

    function _sanitizeLineSpacing(value) {
        var line = _toNumber(value, 1);
        if (!Number.isFinite(line)) return 1;
        return Math.round(Math.max(0.8, Math.min(3, line)) * 100) / 100;
    }

    function _sanitizeParagraphPoints(value, min, max) {
        var points = _toNumber(value, 0);
        if (!Number.isFinite(points)) points = 0;
        return Math.round(Math.max(min, Math.min(max, points)) * 100) / 100;
    }

    function _toNumber(value, fallback) {
        if (typeof value === 'number') return value;
        var parsed = parseFloat(String(value || '').replace('pt', '').trim());
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    function _renderInlines(container, content, inst) {
        if (!content) return;
        var inlines = content.inlines || content.Inlines || [];

        for (var i = 0; i < inlines.length; i++) {
            var inline = inlines[i];
            var inlineType = inline.$type || inline.type || inline.Type;
            var inlineId = inline.id || inline.Id;

            if (inlineType === 'text' || inlineType === 'TextRun') {
                var span = document.createElement('span');
                span.setAttribute('data-inline-id', inlineId || '');
                _setRuntimeNodeAttributes(span, inlineId || _renderNodeId('inline', null, i), 'inline');
                _renderTextRunContent(span, inline.text || inline.Text || '');
                _applyMarks(span, inline.marks || inline.Marks, inst);
                container.appendChild(span);
                _appendNonPrintingTextOverlay(container, inline.text || inline.Text || '', inst);
            } else if (inlineType === 'token' || inlineType === 'TokenRun') {
                var tokenSpan = document.createElement('span');
                tokenSpan.setAttribute('data-inline-id', inlineId || '');
                _setRuntimeNodeAttributes(tokenSpan, inlineId || _renderNodeId('inline', null, i), 'inline');
                tokenSpan.setAttribute('data-inline-atomic', 'true');
                tokenSpan.setAttribute('data-token-key', inline.key || inline.Key || '');
                tokenSpan.setAttribute('data-token-type', inline.tokenType || inline.TokenType || '');
                tokenSpan.setAttribute('title', inline.description || inline.Description || inline.key || inline.Key || '');
                tokenSpan.setAttribute('contenteditable', 'false');
                tokenSpan.className = 'tm-wysiwyg-token' + (inline.colorClass || inline.ColorClass ? ' ' + (inline.colorClass || inline.ColorClass) : '');
                tokenSpan.textContent = inline.displayName || inline.DisplayName || inline.key || inline.Key || '';
                container.appendChild(tokenSpan);
            } else if (inlineType === 'field' || inlineType === 'DocumentFieldRun') {
                var fieldSpan = document.createElement('span');
                var fieldType = _normalizeDocumentFieldType(inline.fieldType ?? inline.FieldType);
                fieldSpan.setAttribute('data-inline-id', inlineId || '');
                _setRuntimeNodeAttributes(fieldSpan, inlineId || _renderNodeId('inline', null, i), 'inline');
                fieldSpan.setAttribute('data-inline-atomic', 'true');
                fieldSpan.setAttribute('data-field-type', String(fieldType));
                fieldSpan.setAttribute('data-field-format', inline.format || inline.Format || '');
                fieldSpan.setAttribute('data-field-fallback', inline.fallbackText || inline.FallbackText || '');
                fieldSpan.setAttribute('contenteditable', 'false');
                fieldSpan.className = 'tm-wysiwyg-field';
                fieldSpan.textContent = inline.displayText || inline.DisplayText || inline.fallbackText || inline.FallbackText || _fieldFallbackLabel(fieldType);
                container.appendChild(fieldSpan);
            } else if (inlineType === 'noteReference' || inlineType === 'DocumentNoteReferenceRun') {
                var sup = document.createElement('sup');
                var noteId = inline.noteId || inline.NoteId || '';
                var noteType = _normalizeNoteType(inline.noteType ?? inline.NoteType);
                sup.setAttribute('data-inline-id', inlineId || '');
                _setRuntimeNodeAttributes(sup, inlineId || _renderNodeId('inline', null, i), 'inline');
                sup.setAttribute('data-note-id', noteId);
                sup.setAttribute('data-note-type', noteType);
                sup.setAttribute('data-inline-atomic', 'true');
                sup.setAttribute('data-testid', noteType === 'Endnote' ? 'document-wysiwyg-endnote-ref' : 'document-wysiwyg-footnote-ref');
                sup.setAttribute('contenteditable', 'false');
                sup.className = 'tm-wysiwyg-note-ref';
                sup.textContent = inline.displayMarker || inline.DisplayMarker || noteId || '';
                container.appendChild(sup);
            }
        }
    }

    function _normalizeDocumentFieldType(value) {
        if (typeof value === 'number' && Number.isFinite(value)) {
            return Math.max(0, Math.min(10, Math.round(value)));
        }

        var raw = String(value == null ? '' : value).trim().toLowerCase();
        switch (raw) {
            case '0':
            case 'pagenumber':
            case 'page-number':
                return 0;
            case '1':
            case 'pagecount':
            case 'page-count':
                return 1;
            case '2':
            case 'pagexofy':
            case 'page-x-of-y':
                return 2;
            case '3':
            case 'date':
                return 3;
            case '4':
            case 'documenttitle':
            case 'document-title':
                return 4;
            case '5':
            case 'author':
                return 5;
            case '6':
            case 'lastsaved':
            case 'last-saved':
                return 6;
            case '7':
            case 'sectionpagenumber':
            case 'section-page-number':
                return 7;
            case '8':
            case 'sectionpagecount':
            case 'section-page-count':
                return 8;
            case '9':
            case 'filename':
            case 'file-name':
                return 9;
            case '10':
            case 'revisionnumber':
            case 'revision-number':
                return 10;
            default:
                return 0;
        }
    }

    function _fieldFallbackLabel(fieldType) {
        switch (_normalizeDocumentFieldType(fieldType)) {
            case 0: return '1';
            case 1: return '1';
            case 2: return '1 / 1';
            case 3: return new Date().toLocaleDateString();
            case 4: return 'Document title';
            case 5: return 'Author';
            case 6: return new Date().toLocaleDateString();
            case 7: return '1';
            case 8: return '1';
            case 9: return 'File name';
            case 10: return '1';
            default: return '';
        }
    }

    function _refreshDocumentFields(inst) {
        if (!inst || !inst.root) return;
        var pages = Array.prototype.slice.call(inst.root.querySelectorAll('.tm-wysiwyg-page[data-page-index]:not(.tm-wysiwyg-page--virtual)'));
        if (pages.length === 0) return;

        var totalPages = inst.virtualState && inst.virtualState.totalPages ? inst.virtualState.totalPages : (inst.virtualPages ? inst.virtualPages.length : pages.length);
        totalPages = Math.max(1, totalPages || pages.length);
        for (var i = 0; i < pages.length; i++) {
            var page = pages[i];
            var pageIndex = parseInt(page.getAttribute('data-page-index') || String(i), 10);
            if (!Number.isFinite(pageIndex)) pageIndex = i;
            var fields = page.querySelectorAll('.tm-wysiwyg-field[data-field-type]');
            for (var f = 0; f < fields.length; f++) {
                var field = fields[f];
                var value = _resolveDocumentFieldValue(
                    inst,
                    field.getAttribute('data-field-type'),
                    pageIndex,
                    totalPages,
                    field.getAttribute('data-field-format'),
                    field.getAttribute('data-field-fallback'));
                field.textContent = value;
                field.setAttribute('data-field-value', value);
                field.setAttribute('aria-label', value);
            }
        }
    }

    function _resolveDocumentFieldValue(inst, fieldType, pageIndex, totalPages, format, fallback) {
        var doc = _resolveRuntimeDocument(inst) || {};
        var metadata = doc.metadata || doc.Metadata || {};
        switch (_normalizeDocumentFieldType(fieldType)) {
            case 0:
                return String((pageIndex || 0) + 1);
            case 1:
                return String(Math.max(1, totalPages || 1));
            case 2:
                return String((pageIndex || 0) + 1) + ' / ' + String(Math.max(1, totalPages || 1));
            case 3:
                return _formatDocumentFieldDate(new Date(), format);
            case 4:
                return metadata.title || metadata.Title || fallback || '';
            case 5:
                var author = metadata.author || metadata.Author || {};
                return author.displayName || author.DisplayName || fallback || '';
            case 6:
                var modified = metadata.modifiedAt || metadata.ModifiedAt || metadata.createdAt || metadata.CreatedAt || null;
                return _formatDocumentFieldDate(modified ? new Date(modified) : new Date(), format);
            case 7:
                return String((pageIndex || 0) + 1);
            case 8:
                return String(Math.max(1, totalPages || 1));
            case 9:
                return metadata.fileName || metadata.FileName || metadata.title || metadata.Title || fallback || '';
            case 10:
                return String(metadata.revisionNumber || metadata.RevisionNumber || fallback || '1');
            default:
                return fallback || '';
        }
    }

    function _formatDocumentFieldDate(value, format) {
        if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
            value = new Date();
        }

        var rawFormat = String(format || '').trim().toLowerCase();
        if (rawFormat === 'iso' || rawFormat === 'yyyy-mm-dd') {
            return value.toISOString().slice(0, 10);
        }

        return value.toLocaleDateString();
    }

    function _renderTextRunContent(span, text) {
        var value = text || '';
        if (value.length === 0) {
            span.appendChild(document.createTextNode(''));
            return;
        }

        var parts = value.split('\n');
        for (var i = 0; i < parts.length; i++) {
            if (parts[i].length > 0) {
                span.appendChild(document.createTextNode(parts[i]));
            }

            if (i < parts.length - 1) {
                var br = document.createElement('br');
                br.setAttribute('data-inline-break', 'true');
                span.appendChild(br);
            }
        }

        if (!span.firstChild) {
            span.appendChild(document.createTextNode(''));
        }
    }

    function _appendNonPrintingTextOverlay(container, text, inst) {
        if (!inst || !inst.showNonPrintingCharacters) return;
        var marks = _formatNonPrintingText(text || '');
        if (!marks) return;
        var overlay = document.createElement('span');
        overlay.className = 'tm-wysiwyg-nonprinting-text';
        overlay.setAttribute('data-testid', 'document-wysiwyg-nonprinting-text');
        overlay.setAttribute('contenteditable', 'false');
        overlay.setAttribute('aria-hidden', 'true');
        overlay.textContent = marks;
        container.appendChild(overlay);
    }

    function _formatNonPrintingText(text) {
        var value = String(text || '');
        if (!/[ \t\n]/.test(value)) return '';
        return value
            .replace(/ /g, '·')
            .replace(/\t/g, '→')
            .replace(/\n/g, '¶\n');
    }

    function _refreshNonPrintingCharacters(inst) {
        if (!inst || !inst.root) return;
        inst.root.classList.toggle('tm-wysiwyg--show-nonprinting', !!inst.showNonPrintingCharacters);
        inst.root.querySelectorAll('.tm-wysiwyg-nonprinting-text').forEach(function (node) { node.remove(); });
        if (!inst.showNonPrintingCharacters) return;
        inst.root.querySelectorAll('.tm-wysiwyg-block[data-block-id]').forEach(function (block) {
            if (block.classList.contains('tm-wysiwyg-page-break')) return;
            var inlines = Array.from(block.querySelectorAll(':scope > [data-inline-id]'));
            inlines.forEach(function (inline) {
                _appendNonPrintingTextOverlay(block, inline.textContent || '', inst);
            });
            _appendNonPrintingTextOverlay(block, '\n', inst);
        });
    }

    function _applyMarks(el, marks, inst) {
        if (!marks || marks.length === 0) return;
        for (var i = 0; i < marks.length; i++) {
            var mark = marks[i];
            var markType = mark.type ?? mark.Type;
            switch (markType) {
                case 'Bold': case 0: el.style.fontWeight = 'bold'; break;
                case 'Italic': case 1: el.style.fontStyle = 'italic'; break;
                case 'Underline': case 2: el.style.textDecoration = (el.style.textDecoration || '') + ' underline'; break;
                case 'Strikethrough': case 3: el.style.textDecoration = (el.style.textDecoration || '') + ' line-through'; break;
                case 'Superscript': case 4: el.style.verticalAlign = 'super'; el.style.fontSize = 'smaller'; break;
                case 'Subscript': case 5: el.style.verticalAlign = 'sub'; el.style.fontSize = 'smaller'; break;
                case 'Link': case 6:
                    var linkData = mark.link || mark.Link || {};
                    var href = _sanitizeLinkHref(linkData.href || linkData.Href || '');
                    if (!href) break;
                    var title = linkData.title || linkData.Title || '';
                    el.setAttribute('data-link-href', href);
                    if (title) {
                        el.setAttribute('data-link-title', title);
                        el.setAttribute('title', title);
                    }
                    var wrapper = document.createElement('a');
                    wrapper.href = href;
                    if (title) wrapper.title = title;
                    wrapper.style.color = 'var(--tm-color-primary)';
                    wrapper.style.textDecoration = 'underline';
                    wrapper.setAttribute('data-inline-id', el.getAttribute('data-inline-id'));
                    wrapper.setAttribute('data-node-id', el.getAttribute('data-node-id') || el.getAttribute('data-inline-id') || '');
                    wrapper.setAttribute('data-runtime-node-type', 'inline');
                    wrapper.setAttribute('data-link-href', href);
                    if (title) wrapper.setAttribute('data-link-title', title);
                    wrapper.textContent = el.textContent;
                    el.textContent = '';
                    el.appendChild(wrapper);
                    break;
                case 'CommentAnchor': case 'commentAnchor': case 7:
                    var cid = (mark.commentAnchor || mark.CommentAnchor || {}).commentId
                        || (mark.commentAnchor || mark.CommentAnchor || {}).CommentId
                        || '';
                    if (cid) {
                        el.classList.add('tm-document-inline--comment-anchor');
                        el.setAttribute('data-comment-id', cid);
                        el.setAttribute('data-testid', 'document-comment-highlight');
                    }
                    break;
                case 'Revision': case 'revision': case 8:
                    var revisionId = mark.revisionId || mark.RevisionId || '';
                    var revisionType = mark.value || mark.Value || 'Insertion';
                    if (revisionId) {
                        el.classList.add('tm-wysiwyg-revision');
                        if (revisionType === 'Deletion') {
                            el.classList.add('tm-wysiwyg-revision--delete');
                            el.setAttribute('data-testid', 'document-wysiwyg-revision-delete');
                        } else if (revisionType === 'Formatting') {
                            el.classList.add('tm-wysiwyg-revision--format');
                            el.setAttribute('data-testid', 'document-wysiwyg-revision-format');
                        } else {
                            el.classList.add('tm-wysiwyg-revision--insert');
                            el.setAttribute('data-testid', 'document-wysiwyg-revision-insert');
                        }
                        el.setAttribute('data-revision-id', revisionId);
                        el.setAttribute('data-revision-type', revisionType);
                    }
                    break;
                case 'Highlight': case 'highlight': case 9:
                    el.style.backgroundColor = _sanitizeColorValue(mark.value || mark.Value) || '';
                    break;
                case 'TextColor': case 'textColor': case 10:
                    el.style.color = _sanitizeColorValue(mark.value || mark.Value) || '';
                    break;
                case 'FontFamily': case 'fontFamily': case 11:
                    el.style.fontFamily = _sanitizeFontFamilyValue(mark.value || mark.Value, inst) || '';
                    break;
                case 'FontSize': case 'fontSize': case 12:
                    el.style.fontSize = _sanitizeFontSizeValue(mark.value || mark.Value) || '';
                    break;
            }
        }
    }

    function _renderTable(content, inst) {
        var table = document.createElement('table');
        table.className = 'tm-wysiwyg-table';
        _applyTableLayoutStyle(table, content && (content.layout || content.Layout || {}));
        var rows = (content && (content.rows || content.Rows)) || [];
        for (var r = 0; r < rows.length; r++) {
            var tr = document.createElement('tr');
            var cells = (rows[r] && (rows[r].cells || rows[r].Cells)) || [];
            for (var c = 0; c < cells.length; c++) {
                var cell = cells[c];
                var isHeaderCell = !!(cell.isHeader || cell.IsHeader);
                var td = document.createElement(isHeaderCell ? 'th' : 'td');
                var cellId = cell.id || cell.Id || _renderNodeId('cell', null, r + '-' + c);
                td.setAttribute('data-cell-id', cellId);
                _setRuntimeNodeAttributes(td, cellId, 'table-cell');

                // Phase 13: set colspan and rowspan for merged cells.
                var cSpan = cell.columnSpan || cell.ColumnSpan || 1;
                var rSpan = cell.rowSpan || cell.RowSpan || 1;
                if (cSpan > 1) td.setAttribute('colspan', cSpan);
                if (rSpan > 1) td.setAttribute('rowspan', rSpan);
                _applyTableCellStyle(td, cell);

                var cellBlocks = (cell && (cell.blocks || cell.Blocks)) || [];
                for (var b = 0; b < cellBlocks.length; b++) {
                    var cellBlockEl = _renderBlock(cellBlocks[b], inst);
                    if (cellBlockEl) td.appendChild(cellBlockEl);
                }
                // Phase 13: ensure empty cells have an editable paragraph placeholder.
                if (cellBlocks.length === 0) {
                    _appendEmptyTableCellParagraph(td);
                }
                if (!(td.textContent || '').trim()) {
                    td.classList.add('tm-wysiwyg-table-cell--empty');
                    td.setAttribute('data-placeholder', _readStringOption(inst, 'tableCellPlaceholder', 'TableCellPlaceholder', 'Cell'));
                }
                tr.appendChild(td);
            }
            table.appendChild(tr);
        }
        return table;
    }

    function _applyTableCellStyle(td, cell) {
        if (!td || !cell) return;
        var width = cell.width ?? cell.Width ?? null;
        if (width != null && width !== '') {
            td.style.width = _normalizeCssLength(width);
            td.setAttribute('data-cell-width', String(width));
        }

        var background = _sanitizeColorValue(cell.backgroundColor || cell.BackgroundColor || '');
        if (background) {
            td.style.backgroundColor = background;
            td.setAttribute('data-cell-background', background);
        }

        var borders = cell.borders || cell.Borders || {};
        _applyTableCellBorder(td, 'Top', borders.top || borders.Top);
        _applyTableCellBorder(td, 'Right', borders.right || borders.Right);
        _applyTableCellBorder(td, 'Bottom', borders.bottom || borders.Bottom);
        _applyTableCellBorder(td, 'Left', borders.left || borders.Left);

        var verticalAlignment = _normalizeTableVerticalAlignment(cell.verticalAlignment ?? cell.VerticalAlignment ?? '');
        if (verticalAlignment) {
            td.style.verticalAlign = verticalAlignment;
            td.setAttribute('data-cell-vertical-align', verticalAlignment);
        }

        var padding = cell.padding ?? cell.Padding ?? null;
        if (padding != null && padding !== '') {
            td.style.padding = _normalizeCssLength(padding);
            td.setAttribute('data-cell-padding', String(padding));
        }
    }

    function _applyTableLayoutStyle(table, layout) {
        if (!table) return;
        var props = layout || {};
        var width = props.width ?? props.Width ?? null;
        if (width != null && width !== '') {
            table.style.width = _normalizeCssLength(width);
            table.setAttribute('data-table-width', String(width));
        }

        var alignment = _normalizeTableAlignment(props.alignment ?? props.Alignment ?? '');
        if (alignment) {
            table.setAttribute('data-table-alignment', alignment);
            table.style.marginLeft = alignment === 'center' || alignment === 'right' ? 'auto' : '0';
            table.style.marginRight = alignment === 'center' || alignment === 'left' ? 'auto' : '0';
        }

        var background = _sanitizeColorValue(props.backgroundColor || props.BackgroundColor || '');
        if (background) {
            table.style.backgroundColor = background;
            table.setAttribute('data-table-background', background);
        }

        var cellPadding = props.cellPadding ?? props.CellPadding ?? null;
        if (cellPadding != null && cellPadding !== '') {
            table.style.setProperty('--tm-document-table-cell-padding', _normalizeCssLength(cellPadding));
            table.setAttribute('data-table-cell-padding', String(cellPadding));
            table.querySelectorAll('td[data-cell-id], th[data-cell-id]').forEach(function (cell) {
                if (!cell.getAttribute('data-cell-padding')) {
                    cell.style.padding = _normalizeCssLength(cellPadding);
                }
            });
        }

        var borders = props.borders || props.Borders || {};
        _applyTableCellBorder(table, 'Top', borders.top || borders.Top);
        _applyTableCellBorder(table, 'Right', borders.right || borders.Right);
        _applyTableCellBorder(table, 'Bottom', borders.bottom || borders.Bottom);
        _applyTableCellBorder(table, 'Left', borders.left || borders.Left);
    }

    function _normalizeCssLength(value) {
        var text = String(value || '').trim();
        if (!text) return '';
        return /[a-z%]+$/i.test(text) ? text : text + 'px';
    }

    function _normalizeTableAlignment(value) {
        if (value === 1) return 'center';
        if (value === 2) return 'right';
        var text = String(value || '').toLowerCase();
        if (text === 'center' || text === '1') return 'center';
        if (text === 'right' || text === '2') return 'right';
        return 'left';
    }

    function _normalizeTableVerticalAlignment(value) {
        if (value === 1) return 'middle';
        if (value === 2) return 'bottom';
        var text = String(value || '').toLowerCase();
        if (text === 'middle' || text === '1') return 'middle';
        if (text === 'bottom' || text === '2') return 'bottom';
        return 'top';
    }

    function _applyTableCellBorder(td, side, value) {
        if (!td || !value) return;
        var cssValue = String(value);
        td.style['border' + side] = cssValue;
        td.setAttribute('data-cell-border-' + side.toLowerCase(), cssValue);
    }

    function _renderImage(content, inst) {
        var figure = document.createElement('figure');
        figure.className = 'tm-wysiwyg-image';
        var img = document.createElement('img');
        var src = (content && (content.url || content.Url)) || '';
        var alt = (content && (content.altText || content.AltText)) || '';
        if (_isSafeImageUrl(src)) {
            img.src = src;
        }
        img.loading = 'lazy';
        img.decoding = 'async';
        img.alt = alt;
        var source = content && (content.source ?? content.Source);
        var assetId = content && (content.assetId || content.AssetId);
        figure.setAttribute('data-image-source', source == null ? '0' : String(source));
        if (assetId) figure.setAttribute('data-image-asset-id', assetId);
        if (assetId) img.setAttribute('data-node-id', _renderNodeId('asset', assetId, 'image'));
        img.setAttribute('data-runtime-node-type', 'image-asset');
        var size = content && (content.size || content.Size);
        if (size) {
            if (size.width || size.Width) img.style.width = (size.width || size.Width) + 'px';
            if (size.height || size.Height) img.style.height = (size.height || size.Height) + 'px';
            figure.setAttribute('data-lock-aspect-ratio', (size.lockAspectRatio ?? size.LockAspectRatio) === false ? 'false' : 'true');
        }
        var naturalSize = content && (content.naturalSize || content.NaturalSize);
        if (naturalSize) {
            var naturalWidth = naturalSize.width || naturalSize.Width;
            var naturalHeight = naturalSize.height || naturalSize.Height;
            if (naturalWidth) figure.setAttribute('data-image-natural-width', String(naturalWidth));
            if (naturalHeight) figure.setAttribute('data-image-natural-height', String(naturalHeight));
        }
        var linkUrl = content && (content.linkUrl || content.LinkUrl);
        if (linkUrl) {
            figure.setAttribute('data-image-link', linkUrl);
        }
        figure.appendChild(img);
        var caption = content && (content.caption || content.Caption);
        if (caption) {
            var figcaption = document.createElement('figcaption');
            figcaption.setAttribute('contenteditable', 'true');
            figcaption.setAttribute('data-testid', 'document-wysiwyg-image-caption-text');
            figcaption.textContent = caption;
            figure.appendChild(figcaption);
        }
        _attachImageLoadState(figure, img, src, inst);
        _applyFloatingImageLayout(figure, content, inst);
        _ensureImageResizeHandle(figure, inst);
        return figure;
    }

    function _attachImageLoadState(figure, img, src, inst) {
        if (!figure || !img) return;
        var safeSrc = _isSafeImageUrl(src) ? src : '';
        figure.querySelectorAll('.tm-wysiwyg-image__retry').forEach(function (retry) { retry.remove(); });

        if (!safeSrc) {
            figure.setAttribute('data-image-load-state', 'error');
            _ensureImageRetryButton(figure, img, safeSrc, inst);
            return;
        }

        figure.setAttribute('data-image-load-state', img.complete && img.naturalWidth > 0 ? 'loaded' : 'loading');
        if (img.complete && img.naturalWidth > 0) {
            _recordImageNaturalSize(figure, img);
        }
        img.addEventListener('load', function () {
            figure.setAttribute('data-image-load-state', 'loaded');
            _recordImageNaturalSize(figure, img);
            figure.querySelectorAll('.tm-wysiwyg-image__retry').forEach(function (retry) { retry.remove(); });
        }, { once: true });
        img.addEventListener('error', function () {
            figure.setAttribute('data-image-load-state', 'error');
            _ensureImageRetryButton(figure, img, safeSrc, inst);
        }, { once: true });
    }

    function _ensureImageRetryButton(figure, img, src, inst) {
        if (!figure || figure.querySelector('.tm-wysiwyg-image__retry')) return;
        var retry = document.createElement('button');
        retry.type = 'button';
        retry.className = 'tm-wysiwyg-image__retry';
        retry.setAttribute('contenteditable', 'false');
        retry.setAttribute('data-testid', 'document-wysiwyg-image-retry');
        retry.textContent = _readStringOption(inst, 'imageRetryLabel', 'ImageRetryLabel', 'Retry');
        retry.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            if (!src) return;
            figure.setAttribute('data-image-load-state', 'loading');
            retry.remove();
            img.removeAttribute('src');
            _attachImageLoadState(figure, img, src, inst);
            window.setTimeout(function () {
                img.src = _imageRetryUrl(src);
            }, 0);
        });
        figure.appendChild(retry);
    }

    function _recordImageNaturalSize(figure, img) {
        if (!figure || !img || !img.naturalWidth || !img.naturalHeight) return;
        figure.setAttribute('data-image-natural-width', String(img.naturalWidth));
        figure.setAttribute('data-image-natural-height', String(img.naturalHeight));
    }

    function _imageRetryUrl(src) {
        if (!src || src.indexOf('data:') === 0) return src;
        try {
            var url = new URL(src, window.location.origin);
            url.searchParams.set('tmImageRetry', String(Date.now()));
            return url.toString();
        } catch {
            return src;
        }
    }

    function _applyFloatingImageLayout(figure, content, inst) {
        var layout = content && (content.floatingLayout || content.FloatingLayout);
        figure.classList.remove(
            'tm-wysiwyg-image--floating',
            'tm-wysiwyg-image--wrap-inline',
            'tm-wysiwyg-image--wrap-square',
            'tm-wysiwyg-image--wrap-tight',
            'tm-wysiwyg-image--wrap-through',
            'tm-wysiwyg-image--wrap-top-bottom',
            'tm-wysiwyg-image--wrap-behind-text',
            'tm-wysiwyg-image--wrap-in-front-of-text',
            'tm-wysiwyg-image--position-left',
            'tm-wysiwyg-image--position-center',
            'tm-wysiwyg-image--position-right',
            'tm-wysiwyg-image--relative-margin',
            'tm-wysiwyg-image--relative-page',
            'tm-wysiwyg-image--relative-column',
            'tm-wysiwyg-image--relative-character',
            'tm-wysiwyg-image--vrelative-margin',
            'tm-wysiwyg-image--vrelative-page',
            'tm-wysiwyg-image--vrelative-paragraph',
            'tm-wysiwyg-image--vrelative-line');
        figure.style.left = '';
        figure.style.top = '';
        figure.style.zIndex = '';
        figure.style.marginLeft = '';
        figure.style.marginRight = '';
        figure.style.marginTop = '';
        figure.style.marginBottom = '';

        if (!layout) {
            figure.setAttribute('data-floating-inline', 'true');
            figure.setAttribute('data-wrap-mode', '0');
            _debugImage(inst, 'layout.apply.inline-default', {
                figure: _debugElementLabel(figure)
            });
            return;
        }

        var inline = layout.inline ?? layout.Inline;
        var wrapMode = _normalizeWrapMode(layout.wrapMode ?? layout.WrapMode);
        var horizontal = _normalizeRelativePosition(layout.horizontalRelativeTo ?? layout.HorizontalRelativeTo);
        var vertical = _normalizeRelativePosition(layout.verticalRelativeTo ?? layout.VerticalRelativeTo);
        var x = parseFloat(layout.x ?? layout.X ?? 0) || 0;
        var y = parseFloat(layout.y ?? layout.Y ?? 0) || 0;
        var z = parseInt(layout.zIndex ?? layout.ZIndex ?? 0, 10) || 0;
        var lockAnchor = !!(layout.lockAnchor ?? layout.LockAnchor);
        var hPos = _normalizeHorizontalPosition(layout.horizontalPosition ?? layout.HorizontalPosition);
        var distL = parseFloat(layout.distanceLeft ?? layout.DistanceLeft ?? 0) || 0;
        var distR = parseFloat(layout.distanceRight ?? layout.DistanceRight ?? 0) || 0;
        var distT = parseFloat(layout.distanceTop ?? layout.DistanceTop ?? 0) || 0;
        var distB = parseFloat(layout.distanceBottom ?? layout.DistanceBottom ?? 0) || 0;

        figure.setAttribute('data-floating-inline', inline === false ? 'false' : 'true');
        figure.setAttribute('data-wrap-mode', String(wrapMode.value));
        figure.setAttribute('data-horizontal-relative-to', String(horizontal.value));
        figure.setAttribute('data-vertical-relative-to', String(vertical.value));
        figure.setAttribute('data-image-x', String(x));
        figure.setAttribute('data-image-y', String(y));
        figure.setAttribute('data-lock-anchor', lockAnchor ? 'true' : 'false');
        if (hPos) {
            figure.setAttribute('data-horizontal-position', hPos.css);
        } else {
            figure.removeAttribute('data-horizontal-position');
        }

        if (inline !== false) {
            _debugImage(inst, 'layout.apply.inline', {
                figure: _debugElementLabel(figure),
                wrapMode: wrapMode.css,
                horizontalPosition: hPos ? hPos.css : null
            });
            return;
        }

        var positionClass = hPos ? ('tm-wysiwyg-image--position-' + hPos.css) : null;
        figure.classList.add(
            'tm-wysiwyg-image--floating',
            'tm-wysiwyg-image--wrap-' + wrapMode.css,
            'tm-wysiwyg-image--relative-' + horizontal.css,
            'tm-wysiwyg-image--vrelative-' + vertical.css);
        if (positionClass) figure.classList.add(positionClass);
        figure.style.left = x + 'px';
        figure.style.top = y + 'px';
        if (z !== 0) figure.style.zIndex = String(z);
        if (distL > 0) figure.style.marginLeft = distL + 'px';
        if (distR > 0) figure.style.marginRight = distR + 'px';
        if (distT > 0) figure.style.marginTop = distT + 'px';
        if (distB > 0) figure.style.marginBottom = distB + 'px';
        _debugImage(inst, 'layout.apply.floating', {
            figure: _debugElementLabel(figure),
            inline: inline,
            wrapMode: wrapMode.css,
            wrapModeValue: wrapMode.value,
            horizontalPosition: hPos ? hPos.css : null,
            x: x,
            y: y,
            zIndex: z,
            figureRect: _debugRect(figure.getBoundingClientRect()),
            visualRect: _debugRect(_getImagePrimaryVisualRect(figure))
        });
    }

    function _ensureImageResizeHandle(figure, inst) {
        if (!figure.querySelector('.tm-wysiwyg-image__resize-handle')) {
            var handle = document.createElement('span');
            handle.className = 'tm-wysiwyg-image__resize-handle';
            handle.setAttribute('role', 'button');
            handle.setAttribute('tabindex', '0');
            handle.setAttribute('aria-label', _readStringOption(inst, 'imageResizeHandleLabel', 'ImageResizeHandleLabel', 'Resize image'));
            handle.setAttribute('data-testid', 'document-wysiwyg-image-resize-handle');
            figure.appendChild(handle);
        }
    }

    function _normalizeWrapMode(value) {
        var raw = String(value == null ? 'Inline' : value).toLowerCase();
        var byName = {
            inline: { value: 0, css: 'inline' },
            square: { value: 1, css: 'square' },
            tight: { value: 2, css: 'tight' },
            through: { value: 3, css: 'through' },
            topbottom: { value: 4, css: 'top-bottom' },
            topandbottom: { value: 4, css: 'top-bottom' },
            behindtext: { value: 5, css: 'behind-text' },
            infrontoftext: { value: 6, css: 'in-front-of-text' }
        };
        return byName[raw] || Object.values(byName).find(function (item) { return String(item.value) === raw; }) || byName.inline;
    }

    function _normalizeRelativePosition(value) {
        var raw = String(value == null ? 'Page' : value).toLowerCase();
        var byName = {
            page: { value: 0, css: 'page' },
            margin: { value: 1, css: 'margin' },
            column: { value: 2, css: 'column' },
            paragraph: { value: 3, css: 'paragraph' },
            character: { value: 4, css: 'character' },
            line: { value: 5, css: 'line' }
        };
        return byName[raw] || Object.values(byName).find(function (item) { return String(item.value) === raw; }) || byName.page;
    }

    function _normalizeHorizontalPosition(value) {
        if (value == null) return null;
        var raw = String(value).toLowerCase();
        var byName = {
            left: { value: 0, css: 'left' },
            center: { value: 1, css: 'center' },
            right: { value: 2, css: 'right' }
        };
        return byName[raw] || Object.values(byName).find(function (item) { return String(item.value) === raw; }) || null;
    }

    function _isSafeImageUrl(url) {
        if (!url || !String(url).trim()) return false;
        url = String(url).trim();
        if (url.indexOf('/') === 0) return true;
        if (/^data:image\/(png|jpeg|webp|gif);base64,/i.test(url)) return true;
        try {
            var parsed = new URL(url, window.location.origin);
            return parsed.protocol === 'https:' || parsed.protocol === 'http:';
        } catch {
            return false;
        }
    }

    function _createImageBlockFromPayload(payload) {
        payload = payload || {};
        var url = payload.url || payload.Url || '';
        if (!_isSafeImageUrl(url)) return null;

        return {
            Id: 'img-' + Date.now() + '-' + Math.random().toString(36).slice(2, 7),
            Type: 5,
            Order: 0,
            Content: {
                $type: 'image',
                Source: 0,
                Url: url,
                AltText: payload.altText || payload.AltText || '',
                Caption: payload.caption || payload.Caption || ''
            }
        };
    }

    function _insertImageBlock(inst, block, dispatchPatch, selectionOverride) {
        if (!block) return;

        var blockEl = _renderBlock(block, inst);
        if (!blockEl) return;

        var sel = window.getSelection();
        var anchorBlock = null;
        var insertionSelection = selectionOverride || inst.lastSelectionSnapshot || _captureSelectionSnapshot(inst);
        var ownsUndoTransaction = false;
        if (dispatchPatch && !inst.pendingUndoTransaction) {
            _commitCurrentRuntimeTransaction(inst, true);
            _beginUndoTransaction(inst, 'image', 'Insert image', insertionSelection, true);
            ownsUndoTransaction = true;
        }
        var selectionAnchorBlockId = insertionSelection
            ? (insertionSelection.anchorBlockId || insertionSelection.AnchorBlockId || '')
            : '';
        if (selectionAnchorBlockId) {
            anchorBlock = inst.root.querySelector('[data-block-id="' + _cssEscape(selectionAnchorBlockId) + '"]');
        }

        if (!anchorBlock && sel && sel.rangeCount > 0) {
            var node = sel.anchorNode;
            var el = node && (node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement);
            anchorBlock = el ? el.closest('.tm-wysiwyg-block[data-block-id]') : null;
        }

        if (anchorBlock && anchorBlock.parentElement) {
            anchorBlock.parentElement.insertBefore(blockEl, anchorBlock.nextSibling);
        } else {
            var body = inst.root.querySelector('.tm-wysiwyg-page__body') || inst.root;
            body.appendChild(blockEl);
        }

        var range = document.createRange();
        range.setStartAfter(blockEl);
        range.setEndAfter(blockEl);
        if (sel) {
            sel.removeAllRanges();
            sel.addRange(range);
        }

        if (dispatchPatch) {
            _dispatchPatch(inst, {
                type: 'InsertBlock',
                blockType: 'Image',
                block: block,
                selection: insertionSelection,
                protocolVersion: inst.options.protocolVersion || 1
            });
        }

        var insertedFigure = blockEl.matches && blockEl.matches('figure.tm-wysiwyg-image')
            ? blockEl
            : blockEl.querySelector && blockEl.querySelector('figure.tm-wysiwyg-image');
        if (insertedFigure) {
            _selectImageFigure(inst, insertedFigure);
        }

        if (ownsUndoTransaction) {
            _commitUndoTransaction(inst, _captureSelectionSnapshot(inst));
        }
    }

    // ── Command bridge (Blazor → JS) ─────────────────────────────────────────

    /**
     * Applies a snapshot to the DOM.
     * Called by Blazor after loading a document.
     * @param {string} instanceId
     * @param {Object} snapshot
     * @param {boolean=} forceRender
     */
    function applySnapshot(instanceId, snapshot, forceRender) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return;
        if (inst.renderStats) inst.renderStats.snapshotApplies++;
        const editorHasFocus = _hasEditorSelectionOrFocus(inst);
        const hasPendingLocalSnapshot = inst.pendingLocalSnapshotSkips > 0;
        const skipLocalRender = inst.hasRenderedDocument
            && !inst.readOnly
            && !forceRender
            && (editorHasFocus || hasPendingLocalSnapshot);
        inst.snapshot = snapshot;
        inst.runtimeDocument = _createRuntimeDocumentFromSnapshot(snapshot);
        _loadRuntimeRevisionsFromSnapshot(inst, snapshot);
        _loadRuntimeCommentsFromSnapshot(inst, snapshot);
        if (skipLocalRender) {
            if (inst.pendingLocalSnapshotSkips > 0) {
                inst.pendingLocalSnapshotSkips--;
            }
            if (inst.renderStats) inst.renderStats.lastRenderReason = 'snapshot-skip-active-editor';
            _invokeDotNet(inst, 'HandleSnapshotApplied');
            return;
        }
        _hideInlineRevisionReview(inst);
        _renderDocument(inst, forceRender ? 'forced-snapshot' : 'runtime-load');
        inst.hasRenderedDocument = true;
        inst.lastCommittedHtml = inst.root.innerHTML;
        _markRuntimeSaved(inst, 'snapshot-load');
        _applyReviewDisplayMode(inst);
        _renderRuntimeCommentDecorations(inst);
        _invokeDotNet(inst, 'HandleSnapshotApplied');
    }

    function applyRemoteOperation(instanceId, operation) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !inst.root || !operation) return false;

        var operationId = _getRemoteOperationId(operation);
        if (operationId && inst.appliedOperationIds.has(operationId)) {
            return true;
        }

        var applied = _applyRemoteOperationCore(inst, operation);
        if (applied && operationId) {
            inst.appliedOperationIds.add(operationId);
        }
        return applied;
    }

    function _operationTypeKey(type) {
        switch (type) {
            case 'InsertText':
            case 0: return 'insertText';
            case 'DeleteText':
            case 1: return 'deleteText';
            case 'AddInlineMark':
            case 'AddMark':
            case 2: return 'addInlineMark';
            case 'RemoveInlineMark':
            case 'RemoveMark':
            case 3: return 'removeInlineMark';
            case 'InsertBlock':
            case 4: return 'insertBlock';
            case 'DeleteBlock':
            case 5: return 'deleteBlock';
            case 'MoveBlock':
            case 6: return 'moveBlock';
            case 'SetBlockAttribute':
            case 7: return 'setBlockAttribute';
            case 'UpdateBlock':
            case 8: return 'updateBlock';
            case 'CreateRevision':
            case 9: return 'createRevision';
            case 'AcceptRevision':
            case 10: return 'acceptRevision';
            case 'RejectRevision':
            case 11: return 'rejectRevision';
            default: return String(type == null ? 'unknown' : type);
        }
    }

    function _createOperationRendererRegistry() {
        return {
            insertText: function (inst, operation) { return _applyRemoteInsertText(inst, operation); },
            deleteText: function (inst, operation) { return _applyRemoteDeleteText(inst, operation); },
            addInlineMark: function (inst, operation) { return _applyRemoteInlineMark(inst, operation, true); },
            removeInlineMark: function (inst, operation) { return _applyRemoteInlineMark(inst, operation, false); },
            insertBlock: function (inst, operation) { return _applyRemoteInsertBlock(inst, operation); },
            deleteBlock: function (inst, operation) { return _applyRemoteDeleteBlock(inst, operation); },
            moveBlock: function (inst, operation) { return _applyRemoteMoveBlock(inst, operation); },
            setBlockAttribute: function (inst, operation) { return _applyRemoteSetBlockAttribute(inst, operation); },
            updateBlock: function (inst, operation) { return _applyRemoteUpdateBlock(inst, operation); },
            createRevision: function (inst, operation) { return _applyRemoteCreateRevision(inst, operation); },
            acceptRevision: function (inst, operation) { return _applyRemoteReviewRevision(inst, operation, 'Accepted'); },
            rejectRevision: function (inst, operation) { return _applyRemoteReviewRevision(inst, operation, 'Rejected'); }
        };
    }

    function _applyRemoteOperationCore(inst, operation) {
        var key = _operationTypeKey(operation.type ?? operation.Type);
        var renderer = _createOperationRendererRegistry()[key];
        if (!renderer) {
            _renderDocument(inst, 'unsupported-operation:' + key);
            return false;
        }

        var applied = renderer(inst, operation);
        if (applied) {
            _markIncrementalRender(inst, key);
        }
        return applied;
    }

    function _rememberPendingCollaborationTransaction(inst, transaction) {
        if (!inst || !transaction || !Array.isArray(transaction.operations) || transaction.operations.length === 0) {
            return;
        }

        inst.pendingCollaborationTransactions.push({
            transactionId: transaction.transactionId || transaction.TransactionId || '',
            createdAtMs: Date.now(),
            operations: _cloneRuntimeJson(transaction.operations)
        });
        _prunePendingCollaborationTransactions(inst);
    }

    function _prunePendingCollaborationTransactions(inst) {
        if (!inst || !Array.isArray(inst.pendingCollaborationTransactions)) return;
        var cutoff = Date.now() - 30000;
        inst.pendingCollaborationTransactions = inst.pendingCollaborationTransactions
            .filter(function (transaction) { return (transaction.createdAtMs || 0) >= cutoff; })
            .slice(-50);
    }

    function _transformRemoteOperationsAgainstPendingTransactions(inst, operations) {
        if (!inst || !Array.isArray(operations) || operations.length === 0) return operations || [];
        _prunePendingCollaborationTransactions(inst);
        if (!inst.pendingCollaborationTransactions || inst.pendingCollaborationTransactions.length === 0) {
            return operations;
        }

        var transformed = _cloneRuntimeJson(operations);
        var localChanges = [];
        inst.pendingCollaborationTransactions.forEach(function (transaction) {
            (transaction.operations || []).forEach(function (operation) {
                var change = _localRuntimeTextChange(operation);
                if (change) localChanges.push(change);
            });
        });

        if (localChanges.length === 0) return transformed;

        transformed.forEach(function (operation) {
            var target = operation && (operation.target || operation.Target);
            if (!target) return;
            var offset = Number(target.offset ?? target.Offset);
            if (!Number.isFinite(offset)) return;

            localChanges.forEach(function (change) {
                if (!_sameRemoteTextTarget(target, change)) return;
                if (!change.isDelete) {
                    if (offset > change.offset) offset += change.length;
                    return;
                }

                var end = change.offset + change.length;
                if (offset <= change.offset) return;
                offset = offset >= end ? offset - change.length : change.offset;
            });

            if ('offset' in target) target.offset = Math.max(0, offset);
            target.Offset = Math.max(0, offset);
        });

        return transformed;
    }

    function _localRuntimeTextChange(operation) {
        if (!operation) return null;
        var type = operation.type || operation.Type || '';
        var selection = operation.selection || operation.Selection || operation.beforeSelection || operation.BeforeSelection || {};
        if (!selection) return null;

        var blockId = selection.anchorBlockId || selection.AnchorBlockId || '';
        var inlineId = selection.anchorInlineId || selection.AnchorInlineId || '';
        var inlineIndex = selection.anchorInlineIndex ?? selection.AnchorInlineIndex ?? 0;
        var offset = Number(selection.anchorOffset ?? selection.AnchorOffset ?? 0);
        if (!blockId || !Number.isFinite(offset)) return null;

        if (type === 'InsertText') {
            var data = String(operation.data ?? operation.Data ?? '');
            if (!data) return null;
            return { blockId: blockId, inlineId: inlineId, inlineIndex: inlineIndex, offset: offset, length: data.length, isDelete: false };
        }

        if (type === 'DeleteRange' || type === 'DeleteContentForward' || type === 'DeleteContentBackward') {
            var length = Number(operation.deleteLength ?? operation.DeleteLength ?? String(operation.data ?? operation.Data ?? '').length);
            if (!Number.isFinite(length) || length <= 0) return null;
            if (type === 'DeleteContentBackward') offset = Math.max(0, offset - length);
            return { blockId: blockId, inlineId: inlineId, inlineIndex: inlineIndex, offset: offset, length: length, isDelete: true };
        }

        return null;
    }

    function _sameRemoteTextTarget(target, change) {
        var blockId = target.blockId || target.BlockId || '';
        var inlineId = target.inlineId || target.InlineId || '';
        var inlineIndex = target.inlineIndex ?? target.InlineIndex ?? 0;
        return blockId === change.blockId
            && (!inlineId || !change.inlineId || inlineId === change.inlineId)
            && (inlineId || change.inlineId || Number(inlineIndex) === Number(change.inlineIndex || 0));
    }

    function applyRemoteOperations(instanceId, operations) {
        var result = applyRemoteOperationBatch(instanceId, { operations: operations });
        return !!(result && result.success && (result.applied > 0 || result.skipped > 0));
    }

    function applyRemoteOperationBatch(instanceId, batch) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !inst.root || !batch) {
            return {
                success: false,
                applied: 0,
                skipped: 0,
                failedOperationIds: ['instance-unavailable']
            };
        }

        var operations = Array.isArray(batch)
            ? batch
            : (batch.operations || batch.Operations || []);
        if (!Array.isArray(operations)) {
            return {
                success: false,
                applied: 0,
                skipped: 0,
                failedOperationIds: ['invalid-batch']
            };
        }

        if (_isInputTransactionActive(inst)) {
            _queueRemoteOperationBatch(inst, operations);
            _scheduleRemoteQueueFlush(inst, 25);
            return {
                success: true,
                applied: 0,
                skipped: 0,
                queued: operations.length,
                failedOperationIds: []
            };
        }

        return _applyRemoteOperationBatchCore(inst, operations);
    }

    function _applyRemoteOperationBatchCore(inst, operations) {
        var transformedOperations = _transformRemoteOperationsAgainstPendingTransactions(inst, operations);
        var ordered = _sortRemoteBatchOperations(transformedOperations);
        _transformRemoteBatchInsertOffsets(ordered);
        var applied = 0;
        var skipped = 0;
        var failedOperationIds = [];
        for (var i = 0; i < ordered.length; i++) {
            var operation = ordered[i];
            var operationId = _getRemoteOperationId(operation);
            if (operationId && inst.appliedOperationIds.has(operationId)) {
                skipped++;
                continue;
            }

            var success = _applyRemoteOperationCore(inst, operation);
            if (success) {
                applied++;
                if (operationId) inst.appliedOperationIds.add(operationId);
            } else {
                failedOperationIds.push(operationId || ('operation-' + i));
            }
        }

        if (inst.renderStats) {
            inst.renderStats.remoteOperations += applied;
            if (applied > 0 || skipped > 0) inst.renderStats.remoteBatches++;
        }

        return {
            success: failedOperationIds.length === 0,
            applied: applied,
            skipped: skipped,
            failedOperationIds: failedOperationIds
        };
    }

    function _isInputTransactionActive(inst) {
        return !!(inst && !inst.disposed && (
            inst.compositionActive
            || inst.acceptingNativeInput
            || inst.pendingInputPatch
            || inst.currentTransactionId));
    }

    function _queueRemoteOperationBatch(inst, operations) {
        if (!inst || !Array.isArray(operations) || operations.length === 0) return;
        inst.queuedRemoteBatches.push(operations.slice());
    }

    function _scheduleRemoteQueueFlush(inst, delay) {
        if (!inst || inst.disposed || inst._remoteQueueFlushTimer) return;
        inst._remoteQueueFlushTimer = setTimeout(function () {
            inst._remoteQueueFlushTimer = null;
            _flushQueuedRemoteOperationBatches(inst);
        }, delay || 0);
    }

    function _flushQueuedRemoteOperationBatches(inst) {
        if (!inst || inst.disposed) return;
        if (_isInputTransactionActive(inst)) {
            _scheduleRemoteQueueFlush(inst, 25);
            return;
        }
        while (inst.queuedRemoteBatches.length > 0) {
            var operations = inst.queuedRemoteBatches.shift();
            _applyRemoteOperationBatchCore(inst, operations);
        }
    }

    function _getRemoteOperationId(operation) {
        return operation
            ? (operation.operationId || operation.OperationId || operation.id || operation.Id || '')
            : '';
    }

    function _sortRemoteBatchOperations(operations) {
        return operations
            .map(function (operation, index) {
                var order = _getRemoteOperationOrder(operation, null);
                return {
                    operation: operation,
                    index: index,
                    order: order,
                    stableKey: _getRemoteOperationStableSortKey(operation)
                };
            })
            .sort(function (a, b) {
                var aHasOrder = Number.isFinite(a.order);
                var bHasOrder = Number.isFinite(b.order);
                if (aHasOrder && bHasOrder && a.order !== b.order) return a.order - b.order;
                if (aHasOrder !== bHasOrder) return aHasOrder ? -1 : 1;
                if (a.stableKey !== b.stableKey) return a.stableKey < b.stableKey ? -1 : 1;
                return a.index - b.index;
            })
            .map(function (item) { return item.operation; });
    }

    function _getRemoteOperationOrder(operation, fallback) {
        var metadata = operation && (operation.metadata || operation.Metadata || {});
        var target = operation && (operation.target || operation.Target || {});
        var candidates = [
            operation && (operation.sequence ?? operation.Sequence),
            operation && (operation.order ?? operation.Order),
            metadata.sequence ?? metadata.Sequence,
            metadata.order ?? metadata.Order,
            metadata.logicalTimestamp ?? metadata.LogicalTimestamp,
            target.sequence ?? target.Sequence
        ];

        for (var i = 0; i < candidates.length; i++) {
            var value = Number(candidates[i]);
            if (Number.isFinite(value)) return value;
        }

        return fallback == null ? null : fallback;
    }

    function _getRemoteOperationStableSortKey(operation) {
        var target = operation && (operation.target || operation.Target || {});
        var metadata = operation && (operation.metadata || operation.Metadata || {});
        return [
            target.blockId || target.BlockId || '',
            target.inlineId || target.InlineId || '',
            String(target.inlineIndex ?? target.InlineIndex ?? 0).padStart(6, '0'),
            String(target.offset ?? target.Offset ?? 0).padStart(12, '0'),
            _getRemoteOperationId(operation),
            metadata.clientId || metadata.ClientId || '',
            metadata.authorId || metadata.AuthorId || ''
        ].join(':');
    }

    function _transformRemoteBatchInsertOffsets(operations) {
        var priorInserts = [];
        for (var i = 0; i < operations.length; i++) {
            var operation = operations[i];
            if (!_isRemoteInsertTextOperation(operation)) continue;

            var target = operation.target || operation.Target || {};
            var offset = Number(target.offset ?? target.Offset ?? 0);
            if (!Number.isFinite(offset)) offset = 0;
            var originalOffset = offset;

            for (var j = 0; j < priorInserts.length; j++) {
                var prior = priorInserts[j];
                if (prior.key !== _remoteTextTargetKey(target)) continue;
                if (prior.originalOffset <= originalOffset) {
                    offset += prior.length;
                }
            }

            if ('offset' in target) target.offset = offset;
            target.Offset = offset;
            priorInserts.push({
                key: _remoteTextTargetKey(target),
                originalOffset: originalOffset,
                length: String(operation.text ?? operation.Text ?? '').length
            });
        }
    }

    function _isRemoteInsertTextOperation(operation) {
        var type = operation && (operation.type ?? operation.Type);
        return type === 'InsertText' || type === 0;
    }

    function _remoteTextTargetKey(target) {
        return [
            target.blockId || target.BlockId || '',
            target.inlineId || target.InlineId || '',
            target.inlineIndex ?? target.InlineIndex ?? 0
        ].join(':');
    }

    function _applyRemoteInsertText(inst, operation) {
        var target = operation.target || operation.Target || {};
        var text = operation.text ?? operation.Text ?? '';
        if (!text) return false;

        var inline = _findRemoteTargetInline(inst, target);
        if (!inline) {
            return _updateSnapshotInlineText(inst, target, function (current) {
                var offset = Math.max(0, Math.min(target.offset ?? target.Offset ?? current.length, current.length));
                return current.slice(0, offset) + text + current.slice(offset);
            });
        }

        var offset = Math.max(0, Math.min(target.offset ?? target.Offset ?? inline.textContent.length, inline.textContent.length));
        var selection = _captureRemoteSelectionSnapshot(inst);
        var pos = _resolveOrCreateTextPosition(inline, offset);
        if (!pos) return false;

        var range = document.createRange();
        range.setStart(pos.node, pos.offset);
        range.collapse(true);
        range.insertNode(document.createTextNode(text));

        _updateSnapshotInlineText(inst, target, function (current) {
            var clamped = Math.max(0, Math.min(offset, current.length));
            return current.slice(0, clamped) + text + current.slice(clamped);
        });
        _normalizeRemoteInlineDom(inline);
        _restoreRemoteSelectionAfterTextChange(inst, selection, target, offset, text.length, false);
        _invalidateMeasureCache(inst);
        return true;
    }

    function _applyRemoteDeleteText(inst, operation) {
        var target = operation.target || operation.Target || {};
        var inline = _findRemoteTargetInline(inst, target);
        if (!inline) {
            return _updateSnapshotInlineText(inst, target, function (current) {
                var offset = Math.max(0, Math.min(target.offset ?? target.Offset ?? 0, current.length));
                var length = target.length ?? target.Length ?? ((operation.text ?? operation.Text ?? '').length || 0);
                length = Math.max(0, Math.min(length, current.length - offset));
                return current.slice(0, offset) + current.slice(offset + length);
            });
        }

        var text = inline.textContent || '';
        var offset = Math.max(0, Math.min(target.offset ?? target.Offset ?? 0, text.length));
        var length = target.length ?? target.Length ?? ((operation.text ?? operation.Text ?? '').length || 0);
        length = Math.max(0, Math.min(length, text.length - offset));
        if (length <= 0) return false;

        var selection = _captureRemoteSelectionSnapshot(inst);
        var startPos = _resolveTextPosition(inline, offset);
        var endPos = _resolveTextPosition(inline, offset + length);
        if (!startPos || !endPos) return false;

        var range = document.createRange();
        range.setStart(startPos.node, startPos.offset);
        range.setEnd(endPos.node, endPos.offset);
        range.deleteContents();

        _updateSnapshotInlineText(inst, target, function (current) {
            var clamped = Math.max(0, Math.min(offset, current.length));
            var deleteLength = Math.max(0, Math.min(length, current.length - clamped));
            return current.slice(0, clamped) + current.slice(clamped + deleteLength);
        });
        _normalizeRemoteInlineDom(inline);
        _restoreRemoteSelectionAfterTextChange(inst, selection, target, offset, length, true);
        _invalidateMeasureCache(inst);
        return true;
    }

    function _applyRemoteInsertBlock(inst, operation) {
        var block = operation.block || operation.Block;
        if (!block) return false;
        var blockId = block.id || block.Id;
        if (!blockId || inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]')) {
            return false;
        }

        _upsertSnapshotBlock(inst, block);
        var blockEl = _renderBlock(block, inst);
        if (!blockEl) return false;

        var body = _findVisibleBodyForRemoteBlock(inst, block);
        if (!body) return false;

        var order = parseFloat(block.order ?? block.Order ?? Number.MAX_SAFE_INTEGER);
        var inserted = false;
        var siblings = Array.from(body.querySelectorAll(':scope > .tm-wysiwyg-block[data-block-id]'));
        for (var i = 0; i < siblings.length; i++) {
            var siblingOrder = parseFloat(siblings[i].getAttribute('data-block-order') || String((i + 1) * 10));
            if (Number.isFinite(order) && order < siblingOrder) {
                body.insertBefore(blockEl, siblings[i]);
                inserted = true;
                break;
            }
        }
        if (!inserted) body.appendChild(blockEl);
        _invalidateMeasureCache(inst);
        return true;
    }

    function _applyRemoteCreateRevision(inst, operation) {
        var revision = operation.revision || operation.Revision;
        if (!revision) return false;
        _upsertSnapshotRevision(inst, revision);

        var revisionType = revision.type ?? revision.Type;
        if (revisionType === 'Insertion' || revisionType === 0) {
            return _applyRemoteRevisionInsertion(inst, operation, revision);
        }
        if (revisionType === 'Deletion' || revisionType === 1) {
            return _applyRemoteRevisionDeletion(inst, operation, revision);
        }

        return true;
    }

    function _applyRemoteRevisionInsertion(inst, operation, revision) {
        var target = operation.target || operation.Target || {};
        var text = operation.text ?? operation.Text ?? revision.payloadJson ?? revision.PayloadJson ?? '';
        var inline = _findRemoteTargetInline(inst, target);
        if (!inline || !text) return false;

        var revisionId = revision.id || revision.Id || '';
        var existingRevision = inline.closest && inline.closest('[data-revision-id="' + _cssEscape(revisionId) + '"]');
        if (existingRevision) {
            var existingOffset = Math.max(0, Math.min(target.offset ?? target.Offset ?? existingRevision.textContent.length, existingRevision.textContent.length));
            var pos = _resolveTextPosition(existingRevision, existingOffset);
            if (!pos) return false;
            var textNode = document.createTextNode(text);
            var range = document.createRange();
            range.setStart(pos.node, pos.offset);
            range.collapse(true);
            range.insertNode(textNode);
            _invalidateMeasureCache(inst);
            return true;
        }

        var offset = Math.max(0, Math.min(target.offset ?? target.Offset ?? inline.textContent.length, inline.textContent.length));
        var pos = _resolveTextPosition(inline, offset);
        if (!pos) return false;

        var span = _createRemoteRevisionSpan(revision, 'Insertion', text);
        var insertRange = document.createRange();
        insertRange.setStart(pos.node, pos.offset);
        insertRange.collapse(true);
        insertRange.insertNode(span);
        _invalidateMeasureCache(inst);
        return true;
    }

    function _applyRemoteRevisionDeletion(inst, operation, revision) {
        var target = operation.target || operation.Target || {};
        var inline = _findRemoteTargetInline(inst, target);
        if (!inline) return false;

        var text = inline.textContent || '';
        var offset = Math.max(0, Math.min(target.offset ?? target.Offset ?? 0, text.length));
        var length = target.length ?? target.Length ?? ((operation.text ?? operation.Text ?? revision.payloadJson ?? revision.PayloadJson ?? '').length || 0);
        var end = Math.max(offset, Math.min(offset + length, text.length));
        if (end <= offset) return false;

        var startPos = _resolveTextPosition(inline, offset);
        var endPos = _resolveTextPosition(inline, end);
        if (!startPos || !endPos) return false;

        var range = document.createRange();
        range.setStart(startPos.node, startPos.offset);
        range.setEnd(endPos.node, endPos.offset);

        var span = _createRemoteRevisionSpan(revision, 'Deletion', '');
        span.appendChild(range.extractContents());
        range.insertNode(span);
        _invalidateMeasureCache(inst);
        return true;
    }

    function _applyRemoteReviewRevision(inst, operation, action) {
        var revision = operation.revision || operation.Revision || {};
        var metadata = operation.metadata || operation.Metadata || {};
        var revisionId = revision.id || revision.Id || metadata.revisionId || metadata.RevisionId || '';
        if (!revisionId) return false;

        var revisionType = revision.type ?? revision.Type ?? _findSnapshotRevisionType(inst, revisionId);
        var removeContent = (action === 'Rejected' && (revisionType === 'Insertion' || revisionType === 0))
            || (action === 'Accepted' && (revisionType === 'Deletion' || revisionType === 1));
        clearRevisionDecorations(inst.id, revisionId, removeContent);
        _setSnapshotRevisionAction(inst, revisionId, action);
        _invalidateMeasureCache(inst);
        return true;
    }

    function _findRemoteTargetInline(inst, target) {
        var blockId = target.blockId || target.BlockId || '';
        var inlineId = target.inlineId || target.InlineId || '';
        var inlineIndex = target.inlineIndex ?? target.InlineIndex ?? 0;
        if (!blockId) return null;

        var block = inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        if (!block) return null;
        return inlineId
            ? block.querySelector('[data-inline-id="' + _cssEscape(inlineId) + '"]')
            : block.querySelectorAll('[data-inline-id]')[inlineIndex || 0];
    }

    function _resolveOrCreateTextPosition(inline, absoluteOffset) {
        var pos = _resolveTextPosition(inline, absoluteOffset);
        if (pos) return pos;

        var textNode = document.createTextNode('');
        var br = Array.from(inline.childNodes).find(function (node) {
            return node.nodeType === Node.ELEMENT_NODE && node.tagName === 'BR';
        });
        if (br) {
            inline.insertBefore(textNode, br);
        } else {
            inline.appendChild(textNode);
        }
        return { node: textNode, offset: 0 };
    }

    function _captureRemoteSelectionSnapshot(inst) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return null;
        if (!inst.root.contains(sel.anchorNode) || !inst.root.contains(sel.focusNode)) return null;
        return _captureSelectionSnapshot(inst);
    }

    function _restoreRemoteSelectionAfterTextChange(inst, snapshot, target, changeOffset, changeLength, isDelete) {
        if (!snapshot) return;

        var transformed = _transformSelectionForTextChange(snapshot, target, changeOffset, changeLength, isDelete);
        inst.lastSelectionSnapshot = transformed;
        _restoreSelection(inst, transformed);
    }

    function _transformSelectionForTextChange(snapshot, target, changeOffset, changeLength, isDelete) {
        snapshot = snapshot || {};
        target = target || {};
        var blockId = target.blockId || target.BlockId || '';
        var inlineId = target.inlineId || target.InlineId || '';
        var transform = function (offset, pointBlockId, pointInlineId) {
            if (pointBlockId !== blockId || (inlineId && pointInlineId !== inlineId)) {
                return offset;
            }

            offset = offset || 0;
            if (!isDelete) {
                return offset >= changeOffset ? offset + changeLength : offset;
            }

            var changeEnd = changeOffset + changeLength;
            if (offset <= changeOffset) return offset;
            if (offset >= changeEnd) return offset - changeLength;
            return changeOffset;
        };

        var transformed = {
            anchorBlockId: snapshot.anchorBlockId,
            anchorInlineId: snapshot.anchorInlineId,
            anchorOffset: transform(snapshot.anchorOffset, snapshot.anchorBlockId, snapshot.anchorInlineId),
            focusBlockId: snapshot.focusBlockId,
            focusInlineId: snapshot.focusInlineId,
            focusOffset: transform(snapshot.focusOffset, snapshot.focusBlockId, snapshot.focusInlineId),
            isCollapsed: snapshot.isCollapsed,
            direction: snapshot.direction,
            activeTableCellId: snapshot.activeTableCellId
        };
        return transformed;
    }

    function applyRemoteCursor(instanceId, cursor) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !inst.root || !cursor) return false;

        var sessionId = cursor.sessionId || cursor.SessionId || '';
        if (!sessionId) return false;
        var shouldRemove = (cursor.offset ?? cursor.Offset ?? 0) < 0
            || !String(cursor.displayName || cursor.DisplayName || '').trim();
        if (shouldRemove) {
            _removeRemoteCursor(inst, sessionId);
            _removeRuntimeMarker(inst, 'remote:' + sessionId);
            return true;
        }

        var transformed = _transformRemoteCursorAgainstPendingTransactions(inst, cursor);
        var rendered = _renderRemoteCursor(inst, transformed);
        if (rendered) {
            _upsertRuntimeMarker(inst, {
                id: 'remote:' + sessionId,
                type: 'remoteSelection',
                range: {
                    startBlockId: transformed.blockId || transformed.BlockId || '',
                    startOffset: transformed.offset ?? transformed.Offset ?? 0,
                    endBlockId: transformed.blockId || transformed.BlockId || '',
                    endOffset: transformed.offset ?? transformed.Offset ?? 0
                },
                priority: 50,
                affectsData: false,
                source: 'collaboration',
                targetId: sessionId,
                label: transformed.displayName || transformed.DisplayName || ''
            }, false);
        }
        return rendered;
    }

    function _transformRemoteCursorAgainstPendingTransactions(inst, cursor) {
        var target = {
            blockId: cursor.blockId || cursor.BlockId || '',
            inlineIndex: cursor.inlineIndex ?? cursor.InlineIndex ?? 0,
            offset: cursor.offset ?? cursor.Offset ?? 0
        };
        var operation = {
            Target: target
        };
        _transformRemoteOperationsAgainstPendingTransactions(inst, [operation]);
        var updated = _cloneRuntimeJson(cursor);
        if ('offset' in updated) updated.offset = operation.Target.Offset;
        updated.Offset = operation.Target.Offset;
        return updated;
    }

    function _renderRemoteCursor(inst, cursor) {
        var blockId = cursor.blockId || cursor.BlockId || '';
        var inlineIndex = cursor.inlineIndex ?? cursor.InlineIndex ?? 0;
        var offset = cursor.offset ?? cursor.Offset ?? 0;
        var sessionId = cursor.sessionId || cursor.SessionId || '';
        if (!blockId || !sessionId) return false;

        var block = inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        if (!block) return false;
        var inline = block.querySelectorAll('[data-inline-id]')[inlineIndex || 0] || block.querySelector('[data-inline-id]');
        if (!inline) return false;

        var position = _resolveTextPosition(inline, Math.max(0, Math.min(Number(offset) || 0, inline.textContent.length)));
        if (!position) return false;

        var range = document.createRange();
        range.setStart(position.node, position.offset);
        range.collapse(true);
        var rect = _firstNonZeroClientRect(range) || inline.getBoundingClientRect();
        var rootRect = inst.root.getBoundingClientRect();
        var layer = _ensureRemoteCursorLayer(inst);
        var item = inst.remoteCursorElements.get(sessionId);
        if (!item) {
            item = document.createElement('span');
            item.className = 'tm-wysiwyg-remote-cursor';
            item.setAttribute('data-testid', 'document-wysiwyg-remote-cursor');
            item.setAttribute('data-session-id', sessionId);
            item.innerHTML = '<span class="tm-wysiwyg-remote-cursor__bar"></span><span class="tm-wysiwyg-remote-cursor__label"></span>';
            layer.appendChild(item);
            inst.remoteCursorElements.set(sessionId, item);
        }

        var color = cursor.color || cursor.Color || 'var(--tm-color-primary)';
        item.style.setProperty('--tm-wysiwyg-remote-cursor-color', color);
        item.style.left = Math.round(rect.left - rootRect.left + inst.root.scrollLeft) + 'px';
        item.style.top = Math.round(rect.top - rootRect.top + inst.root.scrollTop) + 'px';
        item.querySelector('.tm-wysiwyg-remote-cursor__label').textContent = cursor.displayName || cursor.DisplayName || cursor.clientId || cursor.ClientId || 'Remote';
        return true;
    }

    function _ensureRemoteCursorLayer(inst) {
        if (inst.remoteCursorLayer && inst.remoteCursorLayer.isConnected) {
            return inst.remoteCursorLayer;
        }

        if (getComputedStyle(inst.root).position === 'static') {
            inst.root.style.position = 'relative';
        }

        var layer = document.createElement('div');
        layer.className = 'tm-wysiwyg-remote-cursors';
        layer.setAttribute('aria-hidden', 'true');
        inst.root.appendChild(layer);
        inst.remoteCursorLayer = layer;
        return layer;
    }

    function _removeRemoteCursor(inst, sessionId) {
        var item = inst.remoteCursorElements && inst.remoteCursorElements.get(sessionId);
        if (item) item.remove();
        if (inst.remoteCursorElements) inst.remoteCursorElements.delete(sessionId);
        _removeRuntimeMarker(inst, 'remote:' + sessionId);
    }

    function _firstNonZeroClientRect(range) {
        var rects = Array.from(range.getClientRects ? range.getClientRects() : []);
        return rects.find(function (rect) { return rect.width > 0 || rect.height > 0; }) || null;
    }

    function _updateSnapshotInlineText(inst, target, update) {
        var blockId = target.blockId || target.BlockId || '';
        var inlineId = target.inlineId || target.InlineId || '';
        var inlineIndex = target.inlineIndex ?? target.InlineIndex ?? 0;
        var block = _findSnapshotBlock(inst, blockId);
        var content = block && (block.content || block.Content);
        var inlines = content && (content.inlines || content.Inlines);
        if (!Array.isArray(inlines)) return false;

        var inline = null;
        for (var i = 0; i < inlines.length; i++) {
            if ((inlines[i].id || inlines[i].Id) === inlineId) {
                inline = inlines[i];
                break;
            }
        }
        if (!inline) inline = inlines[inlineIndex || 0];
        if (!inline) return false;

        var current = inline.text ?? inline.Text ?? '';
        var updated = update(String(current));
        if ('text' in inline) inline.text = updated;
        inline.Text = updated;
        return true;
    }

    function _normalizeRemoteInlineDom(inline) {
        if (!inline) return;
        inline.normalize();

        var children = Array.from(inline.childNodes);
        for (var i = 0; i < children.length - 1; i++) {
            var current = children[i];
            var next = children[i + 1];
            if (_canMergeRemoteMarkNodes(current, next)) {
                while (next.firstChild) {
                    current.appendChild(next.firstChild);
                }
                next.remove();
                children.splice(i + 1, 1);
                i--;
            }
        }
    }

    function _canMergeRemoteMarkNodes(left, right) {
        return left
            && right
            && left.nodeType === Node.ELEMENT_NODE
            && right.nodeType === Node.ELEMENT_NODE
            && left.getAttribute('data-remote-mark')
            && left.getAttribute('data-remote-mark') === right.getAttribute('data-remote-mark')
            && left.className === right.className
            && left.getAttribute('style') === right.getAttribute('style');
    }

    function _createRemoteRevisionSpan(revision, fallbackType, text) {
        var revisionId = revision.id || revision.Id || '';
        var revisionType = revision.type ?? revision.Type;
        var typeName = revisionType === 1 ? 'Deletion' : revisionType === 0 ? 'Insertion' : (revisionType || fallbackType || 'Insertion');
        var span = document.createElement('span');
        span.className = 'tm-wysiwyg-revision '
            + (typeName === 'Deletion' ? 'tm-wysiwyg-revision--delete' : typeName === 'Formatting' ? 'tm-wysiwyg-revision--format' : 'tm-wysiwyg-revision--insert');
        span.setAttribute('data-inline-id', 'rev-' + revisionId);
        span.setAttribute('data-revision-id', revisionId);
        span.setAttribute('data-revision-type', typeName);
        span.setAttribute('data-testid', typeName === 'Deletion'
            ? 'document-wysiwyg-revision-delete'
            : typeName === 'Formatting'
                ? 'document-wysiwyg-revision-format'
                : 'document-wysiwyg-revision-insert');
        if (text) span.textContent = text;
        return span;
    }

    function _applyRemoteUpdateBlock(inst, operation) {
        var block = operation.block || operation.Block;
        if (!block) return false;
        var blockId = block.id || block.Id;
        if (!blockId) return false;

        _upsertSnapshotBlock(inst, block);
        var existing = inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        if (!existing) return true;

        if (existing.matches('figure.tm-wysiwyg-image')) {
            _updateImageBlockInPlace(existing, block, inst);
            _invalidateMeasureCache(inst);
            return true;
        }

        var sel = window.getSelection();
        var selectionInside = sel && sel.rangeCount > 0
            && (_nodeBelongsToRoot(sel.anchorNode, existing) || _nodeBelongsToRoot(sel.focusNode, existing));
        var replacement = _renderBlock(block, inst);
        if (!replacement) return false;
        existing.replaceWith(replacement);
        _invalidateMeasureCache(inst);

        if (selectionInside) {
            _restoreSelection(inst, inst.lastSelectionSnapshot || _captureSelectionSnapshot(inst));
        }
        return true;
    }

    function _updateImageBlockInPlace(existing, block, inst) {
        var content = block.content || block.Content || {};
        var image = existing.querySelector('img') || document.createElement('img');
        if (!image.parentNode) existing.prepend(image);

        var src = content.url || content.Url || '';
        if (_isSafeImageUrl(src)) image.src = src;
        image.alt = content.altText || content.AltText || '';

        var size = content.size || content.Size;
        if (size) {
            var width = size.width || size.Width;
            var height = size.height || size.Height;
            image.style.width = width ? width + 'px' : '';
            image.style.height = height ? height + 'px' : '';
        }
        var naturalSize = content.naturalSize || content.NaturalSize;
        if (naturalSize) {
            var naturalWidth = naturalSize.width || naturalSize.Width;
            var naturalHeight = naturalSize.height || naturalSize.Height;
            if (naturalWidth) existing.setAttribute('data-image-natural-width', String(naturalWidth));
            if (naturalHeight) existing.setAttribute('data-image-natural-height', String(naturalHeight));
        }

        var caption = content.caption || content.Caption || '';
        var figcaption = existing.querySelector('figcaption');
        if (caption) {
            if (!figcaption) {
                figcaption = document.createElement('figcaption');
                existing.appendChild(figcaption);
            }
            figcaption.textContent = caption;
        } else if (figcaption) {
            figcaption.remove();
        }

        existing.className = 'tm-wysiwyg-image tm-wysiwyg-block';
        _attachImageLoadState(existing, image, src, inst);
        _applyFloatingImageLayout(existing, content, inst);
        _ensureImageResizeHandle(existing, inst);
    }

    function _applyRemoteMoveBlock(inst, operation) {
        var target = operation.target || operation.Target || {};
        var blockId = target.blockId || target.BlockId || '';
        var order = parseFloat(target.order ?? target.Order ?? NaN);
        if (!blockId || !Number.isFinite(order)) return false;

        var snapshotBlock = _findSnapshotBlock(inst, blockId);
        if (snapshotBlock) {
            snapshotBlock.Order = order;
            snapshotBlock.order = order;
            var blocks = _getSnapshotBlocks(inst);
            if (blocks) _sortSnapshotBlocks(blocks);
        }

        var existing = inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        if (!existing) return true;
        var body = existing.closest('.tm-wysiwyg-page__body') || _findVisibleBodyForRemoteBlock(inst, snapshotBlock) || inst.root;
        existing.setAttribute('data-block-order', String(order));
        var siblings = Array.from(body.querySelectorAll(':scope > .tm-wysiwyg-block[data-block-id]'))
            .filter(function (block) { return block !== existing; });
        var inserted = false;
        for (var i = 0; i < siblings.length; i++) {
            var siblingOrder = parseFloat(siblings[i].getAttribute('data-block-order') || String((i + 1) * 10));
            if (order < siblingOrder) {
                body.insertBefore(existing, siblings[i]);
                inserted = true;
                break;
            }
        }
        if (!inserted) body.appendChild(existing);
        _invalidateMeasureCache(inst);
        return true;
    }

    function _applyRemoteDeleteBlock(inst, operation) {
        var target = operation.target || operation.Target || {};
        var blockId = target.blockId || target.BlockId || '';
        if (!blockId) return false;

        _removeSnapshotBlock(inst, blockId);
        var existing = inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        if (existing) {
            var selection = _captureRemoteSelectionSnapshot(inst);
            var selectionInside = selection
                && (selection.anchorBlockId === blockId || selection.focusBlockId === blockId);
            var nextBlock = existing.nextElementSibling && existing.nextElementSibling.matches('[data-block-id]')
                ? existing.nextElementSibling
                : null;
            var previousBlock = existing.previousElementSibling && existing.previousElementSibling.matches('[data-block-id]')
                ? existing.previousElementSibling
                : null;
            var fallbackBlock = nextBlock || previousBlock;
            existing.remove();
            _invalidateMeasureCache(inst);
            if (selectionInside && fallbackBlock) {
                var fallbackInline = fallbackBlock.querySelector('[data-inline-id]');
                _restoreSelection(inst, {
                    anchorBlockId: fallbackBlock.getAttribute('data-block-id') || '',
                    anchorInlineId: fallbackInline ? fallbackInline.getAttribute('data-inline-id') : null,
                    anchorOffset: 0,
                    focusBlockId: fallbackBlock.getAttribute('data-block-id') || '',
                    focusInlineId: fallbackInline ? fallbackInline.getAttribute('data-inline-id') : null,
                    focusOffset: 0,
                    isCollapsed: true,
                    direction: 'forward'
                });
            }
        }
        return true;
    }

    function _applyRemoteSetBlockAttribute(inst, operation) {
        var name = operation.attributeName || operation.AttributeName || '';
        if (String(name).toLowerCase() === 'table.cell.text') {
            return _applyRemoteTableCellText(inst, operation);
        }
        if (String(name).toLowerCase() === 'headinglevel') {
            return _applyRemoteHeadingLevel(inst, operation);
        }
        return false;
    }

    function _applyRemoteHeadingLevel(inst, operation) {
        var target = operation.target || operation.Target || {};
        var blockId = target.blockId || target.BlockId || '';
        var level = _parseOperationJsonValue(operation.attributeValueJson || operation.AttributeValueJson, 1);
        level = Math.max(1, Math.min(6, parseInt(level, 10) || 1));
        if (!blockId) return false;

        var block = _findSnapshotBlock(inst, blockId);
        if (block) {
            block.Type = 1;
            block.Content = block.Content || block.content || { $type: 'heading', Inlines: [] };
            block.Content.$type = 'heading';
            block.Content.Level = level;
        }

        var existing = inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        if (!existing) return !!block;
        var replacement = document.createElement('h' + level);
        replacement.className = existing.className;
        Array.from(existing.attributes).forEach(function (attr) {
            replacement.setAttribute(attr.name, attr.value);
        });
        replacement.setAttribute('data-block-id', blockId);
        replacement.innerHTML = existing.innerHTML;
        existing.replaceWith(replacement);
        _invalidateMeasureCache(inst);
        return true;
    }

    function _applyRemoteTableCellText(inst, operation) {
        var target = operation.target || operation.Target || {};
        var blockId = target.blockId || target.BlockId || '';
        var cellId = target.tableCellId || target.TableCellId || '';
        var text = _parseOperationJsonValue(operation.attributeValueJson || operation.AttributeValueJson, '');
        if (!blockId || !cellId) return false;

        _setSnapshotTableCellText(inst, blockId, cellId, text);
        var block = inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        var cell = block ? block.querySelector('[data-cell-id="' + _cssEscape(cellId) + '"]') : null;
        if (!cell) return true;

        var paragraph = cell.querySelector('.tm-wysiwyg-block[data-block-id]');
        if (!paragraph) {
            paragraph = document.createElement('p');
            paragraph.className = 'tm-wysiwyg-block';
            paragraph.setAttribute('data-block-id', '');
            cell.appendChild(paragraph);
        }

        paragraph.textContent = '';
        var span = document.createElement('span');
        span.setAttribute('data-inline-id', '');
        span.textContent = text || '';
        paragraph.appendChild(span);
        _invalidateMeasureCache(inst);
        return true;
    }

    function _parseOperationJsonValue(json, fallback) {
        if (json == null || json === '') return fallback;
        if (typeof json !== 'string') return json;
        try {
            return JSON.parse(json);
        } catch {
            return fallback;
        }
    }

    function _getSnapshotDocument(inst) {
        return inst && inst.snapshot
            ? (inst.snapshot.document || inst.snapshot.Document || null)
            : null;
    }

    function _getSnapshotBlocks(inst) {
        var doc = _getSnapshotDocument(inst);
        if (!doc) return null;
        if (!doc.Blocks && doc.blocks) doc.Blocks = doc.blocks;
        if (!doc.blocks && doc.Blocks) doc.blocks = doc.Blocks;
        if (!doc.Blocks) {
            doc.Blocks = [];
            doc.blocks = doc.Blocks;
        }
        return doc.Blocks;
    }

    function _findSnapshotBlock(inst, blockId) {
        var blocks = _getSnapshotBlocks(inst);
        if (!blocks) return null;
        for (var i = 0; i < blocks.length; i++) {
            if ((blocks[i].id || blocks[i].Id) === blockId) return blocks[i];
        }
        return null;
    }

    function _upsertSnapshotBlock(inst, block) {
        var blocks = _getSnapshotBlocks(inst);
        if (!blocks || !block) return;
        var blockId = block.id || block.Id;
        var updated = JSON.parse(JSON.stringify(block));
        for (var i = 0; i < blocks.length; i++) {
            if ((blocks[i].id || blocks[i].Id) === blockId) {
                blocks[i] = updated;
                _sortSnapshotBlocks(blocks);
                return;
            }
        }
        blocks.push(updated);
        _sortSnapshotBlocks(blocks);
    }

    function _getSnapshotRevisions(inst) {
        var doc = _getSnapshotDocument(inst);
        if (!doc) return null;
        if (!doc.Revisions && doc.revisions) doc.Revisions = doc.revisions;
        if (!doc.revisions && doc.Revisions) doc.revisions = doc.Revisions;
        if (!doc.Revisions) {
            doc.Revisions = [];
            doc.revisions = doc.Revisions;
        }
        return doc.Revisions;
    }

    function _upsertSnapshotRevision(inst, revision) {
        var revisions = _getSnapshotRevisions(inst);
        if (!revisions || !revision) return;
        _upsertRuntimeRevision(inst, revision);
        var revisionId = revision.id || revision.Id;
        var updated = JSON.parse(JSON.stringify(revision));
        for (var i = 0; i < revisions.length; i++) {
            if ((revisions[i].id || revisions[i].Id) === revisionId) {
                var existing = revisions[i];
                var existingPayload = existing.payloadJson || existing.PayloadJson || '';
                var nextPayload = updated.payloadJson || updated.PayloadJson || '';
                if (existingPayload && nextPayload && existingPayload !== nextPayload) {
                    updated.PayloadJson = existingPayload + nextPayload;
                }
                revisions[i] = updated;
                return;
            }
        }
        revisions.push(updated);
    }

    function _findSnapshotRevisionType(inst, revisionId) {
        var revisions = _getSnapshotRevisions(inst) || [];
        for (var i = 0; i < revisions.length; i++) {
            if ((revisions[i].id || revisions[i].Id) === revisionId) {
                return revisions[i].type ?? revisions[i].Type;
            }
        }
        return null;
    }

    function _setSnapshotRevisionAction(inst, revisionId, action) {
        var revisions = _getSnapshotRevisions(inst) || [];
        var actionValue = action === 'Accepted' ? 1 : action === 'Rejected' ? 2 : 0;
        _setRuntimeRevisionAction(inst, revisionId, action);
        for (var i = 0; i < revisions.length; i++) {
            if ((revisions[i].id || revisions[i].Id) === revisionId) {
                revisions[i].Action = actionValue;
                revisions[i].action = actionValue;
                return;
            }
        }
    }

    function _removeSnapshotBlock(inst, blockId) {
        var blocks = _getSnapshotBlocks(inst);
        if (!blocks) return;
        for (var i = blocks.length - 1; i >= 0; i--) {
            if ((blocks[i].id || blocks[i].Id) === blockId) {
                blocks.splice(i, 1);
            }
        }
    }

    function _sortSnapshotBlocks(blocks) {
        blocks.sort(function (left, right) {
            return (parseFloat(left.order ?? left.Order ?? 0) || 0)
                - (parseFloat(right.order ?? right.Order ?? 0) || 0);
        });
    }

    function _findVisibleBodyForRemoteBlock(inst, block) {
        var sectionId = block && (block.sectionId || block.SectionId);
        if (sectionId) {
            var bySection = inst.root.querySelector('.tm-wysiwyg-page__body[data-section-id="' + _cssEscape(sectionId) + '"]');
            if (bySection) return bySection;
        }

        return inst.root.querySelector('.tm-wysiwyg-page__body') || inst.root;
    }

    function _setSnapshotTableCellText(inst, blockId, cellId, text) {
        var block = _findSnapshotBlock(inst, blockId);
        var content = block && (block.content || block.Content);
        var rows = (content && (content.rows || content.Rows)) || [];
        for (var r = 0; r < rows.length; r++) {
            var cells = rows[r].cells || rows[r].Cells || [];
            for (var c = 0; c < cells.length; c++) {
                var cell = cells[c];
                if ((cell.id || cell.Id) !== cellId) continue;

                if (!cell.Blocks && cell.blocks) cell.Blocks = cell.blocks;
                if (!cell.blocks && cell.Blocks) cell.blocks = cell.Blocks;
                if (!cell.Blocks) {
                    cell.Blocks = [];
                    cell.blocks = cell.Blocks;
                }

                var paragraph = cell.Blocks[0];
                if (!paragraph) {
                    paragraph = {
                        Id: '',
                        Type: 0,
                        Order: 0,
                        Content: { $type: 'paragraph', Inlines: [] }
                    };
                    cell.Blocks.push(paragraph);
                }

                paragraph.Type = 0;
                paragraph.Content = { $type: 'paragraph', Inlines: [{ $type: 'text', Id: '', Text: text || '' }] };
                return;
            }
        }
    }

    function _applyRemoteInlineMark(inst, operation, add) {
        var target = operation.target || operation.Target || {};
        var mark = operation.mark || operation.Mark;
        var blockId = target.blockId || target.BlockId || '';
        var inlineId = target.inlineId || target.InlineId || '';
        var inlineIndex = target.inlineIndex ?? target.InlineIndex ?? 0;
        if (!blockId || !mark) return false;

        var block = inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]');
        if (!block) return false;

        var inline = inlineId
            ? block.querySelector('[data-inline-id="' + _cssEscape(inlineId) + '"]')
            : block.querySelectorAll('[data-inline-id]')[inlineIndex || 0];
        if (!inline) return false;

        if (add) {
            return _wrapInlineRangeWithRemoteMark(inline, target, mark);
        } else {
            _removeRemoteInlineMark(inline, mark, target);
        }
        return true;
    }

    function _wrapInlineRangeWithRemoteMark(inline, target, mark) {
        var text = inline.textContent || '';
        var offset = target.offset ?? target.Offset ?? 0;
        var length = target.length ?? target.Length ?? text.length;
        var start = Math.max(0, Math.min(offset, text.length));
        var end = Math.max(start, Math.min(start + length, text.length));
        if (end <= start) return false;

        var startPos = _resolveTextPosition(inline, start);
        var endPos = _resolveTextPosition(inline, end);
        if (!startPos || !endPos) return false;

        var range = document.createRange();
        range.setStart(startPos.node, startPos.offset);
        range.setEnd(endPos.node, endPos.offset);

        var wrapper = document.createElement('span');
        wrapper.className = 'tm-wysiwyg-remote-mark';
        wrapper.setAttribute('data-remote-mark', _remoteMarkKey(mark));
        wrapper.appendChild(range.extractContents());
        range.insertNode(wrapper);
        _applyMarks(wrapper, [mark]);
        _normalizeRemoteInlineDom(inline);
        return true;
    }

    function _removeRemoteInlineMark(inline, mark, target) {
        var type = mark.type ?? mark.Type;
        var key = _remoteMarkKey(mark);
        if (target && (target.length ?? target.Length) > 0) {
            _removeRemoteInlineMarkRange(inline, target, key);
        } else {
            inline.querySelectorAll('[data-remote-mark="' + _cssEscape(key) + '"]').forEach(_unwrapElement);
        }
        switch (type) {
            case 'Bold': case 0:
                if (!_hasRemoteMarkWrapper(inline, key)) inline.style.fontWeight = '';
                break;
            case 'Italic': case 1:
                if (!_hasRemoteMarkWrapper(inline, key)) inline.style.fontStyle = '';
                break;
            case 'Underline': case 2:
                if (!_hasRemoteMarkWrapper(inline, key)) inline.style.textDecoration = (inline.style.textDecoration || '').replace('underline', '').trim();
                break;
            case 'Strikethrough': case 3:
                if (!_hasRemoteMarkWrapper(inline, key)) inline.style.textDecoration = (inline.style.textDecoration || '').replace('line-through', '').trim();
                break;
            case 'Link': case 6:
                var link = inline.querySelector('a[data-inline-id]');
                if (link) {
                    inline.textContent = link.textContent || '';
                }
                break;
            case 'CommentAnchor': case 'commentAnchor': case 7:
                inline.classList.remove('tm-document-inline--comment-anchor');
                inline.removeAttribute('data-comment-id');
                break;
            case 'Revision': case 'revision': case 8:
                inline.classList.remove('tm-wysiwyg-revision', 'tm-wysiwyg-revision--delete', 'tm-wysiwyg-revision--insert');
                inline.removeAttribute('data-revision-id');
                inline.removeAttribute('data-revision-type');
                break;
        }
        _normalizeRemoteInlineDom(inline);
    }

    function _removeRemoteInlineMarkRange(inline, target, key) {
        var text = inline.textContent || '';
        var offset = Math.max(0, Math.min(target.offset ?? target.Offset ?? 0, text.length));
        var length = Math.max(0, Math.min(target.length ?? target.Length ?? 0, text.length - offset));
        var end = offset + length;
        if (length <= 0) return;

        inline.querySelectorAll('[data-remote-mark="' + _cssEscape(key) + '"]').forEach(function (wrapper) {
            var wrapperStart = _absoluteTextOffset(inline, wrapper, 0);
            var wrapperText = wrapper.textContent || '';
            var wrapperEnd = wrapperStart + wrapperText.length;
            var overlapStart = Math.max(offset, wrapperStart);
            var overlapEnd = Math.min(end, wrapperEnd);
            if (overlapEnd <= overlapStart) return;

            var before = wrapperText.slice(0, overlapStart - wrapperStart);
            var middle = wrapperText.slice(overlapStart - wrapperStart, overlapEnd - wrapperStart);
            var after = wrapperText.slice(overlapEnd - wrapperStart);
            var fragment = document.createDocumentFragment();
            if (before) {
                var beforeWrapper = wrapper.cloneNode(false);
                beforeWrapper.textContent = before;
                fragment.appendChild(beforeWrapper);
            }
            if (middle) {
                fragment.appendChild(document.createTextNode(middle));
            }
            if (after) {
                var afterWrapper = wrapper.cloneNode(false);
                afterWrapper.textContent = after;
                fragment.appendChild(afterWrapper);
            }
            wrapper.replaceWith(fragment);
        });
    }

    function _absoluteTextOffset(root, node, offset) {
        var current = 0;
        var found = null;

        function visit(parent) {
            for (var i = 0; i < parent.childNodes.length; i++) {
                var child = parent.childNodes[i];
                if (child.nodeType === Node.TEXT_NODE) {
                    if (child === node || (node.contains && node.contains(child))) {
                        found = current + Math.max(0, Math.min(offset || 0, child.textContent.length));
                        return true;
                    }

                    current += child.textContent.length;
                    continue;
                }

                if (_isInlineBreakNode(child)) {
                    if (child === node) {
                        found = current;
                        return true;
                    }

                    current += 1;
                    continue;
                }

                if (child.nodeType === Node.ELEMENT_NODE && visit(child)) {
                    return true;
                }
            }

            return false;
        }

        visit(root);
        return found ?? current;
    }

    function _hasRemoteMarkWrapper(inline, key) {
        return !!inline.querySelector('[data-remote-mark="' + _cssEscape(key) + '"]');
    }

    function _remoteMarkKey(mark) {
        var type = mark.type ?? mark.Type;
        if (typeof type === 'number') return String(type);
        return type || '';
    }

    function _unwrapElement(el) {
        var parent = el.parentNode;
        if (!parent) return;
        while (el.firstChild) {
            parent.insertBefore(el.firstChild, el);
        }
        parent.removeChild(el);
        parent.normalize();
    }

    function _resolveTextPosition(root, absoluteOffset) {
        var current = 0;
        var target = Math.max(0, absoluteOffset || 0);
        var resolved = null;

        function visit(parent) {
            for (var i = 0; i < parent.childNodes.length; i++) {
                var child = parent.childNodes[i];
                if (child.nodeType === Node.TEXT_NODE) {
                    var length = child.textContent.length;
                    if (target <= current + length) {
                        resolved = { node: child, offset: Math.max(0, Math.min(target - current, length)) };
                        return true;
                    }

                    current += length;
                    continue;
                }

                if (_isInlineBreakNode(child)) {
                    if (target <= current + 1) {
                        resolved = _positionAfterInlineBreak(child);
                        return true;
                    }

                    current += 1;
                    continue;
                }

                if (_isCaretPlaceholderNode(child)) {
                    continue;
                }

                if (_isAtomicInlineElement(child)) {
                    if (target <= current) {
                        resolved = _atomicBoundaryPosition(child, false);
                        return true;
                    }

                    if (target <= current + 1) {
                        resolved = _atomicBoundaryPosition(child, true);
                        return true;
                    }

                    current += 1;
                    continue;
                }

                if (child.nodeType === Node.ELEMENT_NODE && visit(child)) {
                    return true;
                }
            }

            return false;
        }

        visit(root);
        return resolved;
    }

    function _isInlineBreakNode(node) {
        return node
            && node.nodeType === Node.ELEMENT_NODE
            && node.tagName
            && node.tagName.toLowerCase() === 'br'
            && node.hasAttribute('data-inline-break');
    }

    function _positionAfterInlineBreak(br) {
        var next = br.nextSibling;
        if (next && next.nodeType === Node.TEXT_NODE) {
            return { node: next, offset: 0 };
        }

        if (next && next.nodeType === Node.ELEMENT_NODE) {
            var first = _firstDeepTextNode(next);
            if (first) {
                return { node: first, offset: 0 };
            }
        }

        var text = document.createTextNode('');
        br.parentNode.insertBefore(text, br.nextSibling);
        return { node: text, offset: 0 };
    }

    function _cssEscape(value) {
        if (window.CSS && typeof window.CSS.escape === 'function') {
            return window.CSS.escape(value);
        }
        return String(value).replace(/"/g, '\\"');
    }

    function _getRuntimeRevision(inst, revisionId) {
        var revisions = inst && inst.runtimeRevisions || [];
        for (var i = 0; i < revisions.length; i++) {
            if (revisions[i].Id === revisionId || revisions[i].id === revisionId) return revisions[i];
        }
        return null;
    }

    function _reviewRevisionCore(inst, revisionId, action, removeContent) {
        if (!inst || inst.disposed || !inst.root || !revisionId) return false;

        _hideInlineRevisionReview(inst);
        _commitCurrentRuntimeTransaction(inst, true);
        var revision = _getRuntimeRevision(inst, revisionId);
        var revisionType = revision ? _revisionTypeToName(revision.Type ?? revision.type) : '';
        var payload = _parseOperationJsonValue(revision && (revision.PayloadJson ?? revision.payloadJson), null);
        _beginUndoTransaction(
            inst,
            'revision-review',
            action === 'Accepted' ? 'Accept revision' : action === 'Rejected' ? 'Reject revision' : 'Review revision',
            _captureSelectionSnapshot(inst),
            true);

        if (revisionType === 'Formatting' && action === 'Rejected' && payload && payload.BeforeHtml) {
            inst.root.innerHTML = payload.BeforeHtml;
            inst.lastCommittedHtml = inst.root.innerHTML;
            _invalidateMeasureCache(inst);
        } else {
            var escaped = _cssEscape(String(revisionId));
            var nodes = inst.root.querySelectorAll('[data-revision-id="' + escaped + '"]');
            nodes.forEach(function (node) {
                if (removeContent) {
                    node.remove();
                    return;
                }

                node.classList.remove(
                    'tm-wysiwyg-revision',
                    'tm-wysiwyg-revision--insert',
                    'tm-wysiwyg-revision--delete',
                    'tm-wysiwyg-revision--format',
                    'tm-document-inline--revision',
                    'tm-document-inline--revision-insert',
                    'tm-document-inline--revision-delete');
                node.removeAttribute('data-revision-id');
                node.removeAttribute('data-revision-type');
                var testId = node.getAttribute('data-testid') || '';
                if (testId.indexOf('document-wysiwyg-revision-') === 0 || testId.indexOf('document-revision-') === 0) {
                    node.removeAttribute('data-testid');
                }
            });
        }

        _setRuntimeRevisionAction(inst, revisionId, action);
        _appendUndoOperation(inst, {
            type: 'ReviewRevision',
            revisionId: revisionId,
            action: action,
            removeContent: !!removeContent,
            selection: _captureSelectionSnapshot(inst),
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
        _commitCurrentRuntimeTransaction(inst, true);
        return true;
    }

    function reviewRevision(instanceId, revisionId, action) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !revisionId) return false;
        var normalizedAction = _revisionActionToNumber(action) === 2 ? 'Rejected' : 'Accepted';
        var revision = _getRuntimeRevision(inst, revisionId);
        var type = revision ? _revisionTypeToName(revision.Type ?? revision.type) : _revisionTypeToName(_findSnapshotRevisionType(inst, revisionId));
        var removeContent = (normalizedAction === 'Rejected' && type === 'Insertion')
            || (normalizedAction === 'Accepted' && type === 'Deletion');
        return _reviewRevisionCore(inst, revisionId, normalizedAction, removeContent);
    }

    function reviewAllRevisions(instanceId, action, payload) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return false;
        var normalizedAction = _revisionActionToNumber(action) === 2 ? 'Rejected' : 'Accepted';
        payload = payload || {};
        var explicitIds = payload.revisionIds || payload.RevisionIds || payload.ids || payload.Ids || null;
        var ids = Array.isArray(explicitIds)
            ? explicitIds.map(function (id) { return String(id || ''); }).filter(Boolean)
            : (inst.runtimeRevisions || [])
                .filter(function (revision) { return _revisionActionToNumber(revision.Action ?? revision.action) === 0; })
                .map(function (revision) { return String(revision.Id || revision.id || ''); })
                .filter(Boolean);

        if (ids.length === 0 && inst.root) {
            var seen = new Set();
            inst.root.querySelectorAll('[data-revision-id]').forEach(function (node) {
                var id = node.getAttribute('data-revision-id') || '';
                if (id) seen.add(id);
            });
            ids = Array.from(seen);
        }

        var reviewed = 0;
        ids.forEach(function (revisionId) {
            var revision = _getRuntimeRevision(inst, revisionId);
            var type = revision ? _revisionTypeToName(revision.Type ?? revision.type) : _revisionTypeToName(_findSnapshotRevisionType(inst, revisionId));
            var removeContent = (normalizedAction === 'Rejected' && type === 'Insertion')
                || (normalizedAction === 'Accepted' && type === 'Deletion');
            if (_reviewRevisionCore(inst, revisionId, normalizedAction, removeContent)) {
                reviewed++;
            }
        });

        return reviewed > 0;
    }

    function clearRevisionDecorations(instanceId, revisionId, removeContent) {
        const inst = _instances.get(instanceId);
        return _reviewRevisionCore(inst, revisionId, removeContent ? 'Accepted' : 'Rejected', !!removeContent);
    }

    function scrollToRevision(instanceId, revisionId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !inst.root || !revisionId) return false;

        var escaped = _cssEscape(String(revisionId));
        var node = inst.root.querySelector('[data-revision-id="' + escaped + '"]');
        if (!node) return false;

        inst.root.querySelectorAll('.tm-wysiwyg-revision--selected').forEach(function (selected) {
            selected.classList.remove('tm-wysiwyg-revision--selected');
        });

        node.classList.add('tm-wysiwyg-revision--selected');
        if (typeof node.scrollIntoView === 'function') {
            node.scrollIntoView({ block: 'center', inline: 'nearest', behavior: 'smooth' });
        }

        window.setTimeout(function () {
            if (node.isConnected) {
                node.classList.remove('tm-wysiwyg-revision--selected');
            }
        }, 2200);

        return true;
    }

    function upsertComment(instanceId, comment) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !comment) return false;
        return !!_upsertRuntimeComment(inst, comment, true);
    }

    function removeComment(instanceId, commentId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !commentId) return false;
        _removeRuntimeComment(inst, commentId);
        return true;
    }

    function scrollToComment(instanceId, commentId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !inst.root || !commentId) return false;

        var escaped = _cssEscape(String(commentId));
        var node = inst.root.querySelector('[data-comment-id="' + escaped + '"]');
        if (!node) {
            var comment = (inst.runtimeComments || []).find(function (candidate) { return candidate.Id === commentId; });
            var anchor = comment && (comment.Anchor || comment.anchor || {});
            var blockId = anchor && (anchor.BlockId || anchor.blockId || '');
            node = blockId ? inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]') : null;
        }

        if (!node) return false;

        inst.root.querySelectorAll('.tm-document-inline--comment-anchor--selected').forEach(function (selected) {
            selected.classList.remove('tm-document-inline--comment-anchor--selected');
        });
        if (node.classList && node.classList.contains('tm-document-inline--comment-anchor')) {
            node.classList.add('tm-document-inline--comment-anchor--selected');
        }

        if (typeof node.scrollIntoView === 'function') {
            node.scrollIntoView({ block: 'center', inline: 'nearest', behavior: 'smooth' });
        }

        return true;
    }

    function _showInlineRevisionReview(inst, revisionNode) {
        if (!inst || !inst.root || !revisionNode) return;
        var revisionId = revisionNode.getAttribute('data-revision-id') || '';
        if (!revisionId) return;

        _hideInlineRevisionReview(inst);

        var popover = document.createElement('div');
        popover.className = 'tm-wysiwyg-revision-review';
        popover.setAttribute('data-testid', 'document-inline-revision-review');
        popover.setAttribute('contenteditable', 'false');
        popover.setAttribute('role', 'group');
        popover.setAttribute('aria-label', 'Review revision');

        var accept = document.createElement('button');
        accept.type = 'button';
        accept.textContent = 'Accept';
        accept.setAttribute('data-testid', 'document-inline-revision-accept');

        var reject = document.createElement('button');
        reject.type = 'button';
        reject.textContent = 'Reject';
        reject.setAttribute('data-testid', 'document-inline-revision-reject');

        accept.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            _requestInlineRevisionReview(inst, revisionId, 'Accepted');
        });
        reject.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            _requestInlineRevisionReview(inst, revisionId, 'Rejected');
        });

        popover.addEventListener('mousedown', function (event) {
            event.preventDefault();
            event.stopPropagation();
        });

        popover.appendChild(accept);
        popover.appendChild(reject);
        inst.root.appendChild(popover);
        inst.inlineRevisionPopover = popover;

        var revisionRect = revisionNode.getBoundingClientRect();
        var rootRect = inst.root.getBoundingClientRect();
        var left = revisionRect.left - rootRect.left + inst.root.scrollLeft;
        var top = revisionRect.top - rootRect.top + inst.root.scrollTop - popover.offsetHeight - 6;
        if (top < inst.root.scrollTop) {
            top = revisionRect.bottom - rootRect.top + inst.root.scrollTop + 6;
        }

        var minLeft = inst.root.scrollLeft + 4;
        var maxLeft = Math.max(minLeft, inst.root.scrollLeft + rootRect.width - popover.offsetWidth - 4);
        var minTop = inst.root.scrollTop + 4;
        var maxTop = Math.max(minTop, inst.root.scrollTop + rootRect.height - popover.offsetHeight - 4);

        popover.style.left = Math.min(Math.max(minLeft, left), maxLeft) + 'px';
        popover.style.top = Math.min(Math.max(minTop, top), maxTop) + 'px';
    }

    function _hideInlineRevisionReview(inst) {
        if (!inst || !inst.inlineRevisionPopover) return;
        inst.inlineRevisionPopover.remove();
        inst.inlineRevisionPopover = null;
    }

    function _requestInlineRevisionReview(inst, revisionId, action) {
        _hideInlineRevisionReview(inst);
        if (!inst || !inst.dotNetRef || !revisionId) return;
        try {
            inst.dotNetRef.invokeMethodAsync('HandleRevisionReviewRequested', revisionId, action).catch(function (err) {
                console.error('tmDocumentWysiwyg.inlineRevisionReview failed:', err);
            });
        } catch (err) {
            console.error('tmDocumentWysiwyg.inlineRevisionReview exception:', err);
        }
    }

    function setTrackChangesEnabled(instanceId, enabled) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return;
        inst.trackChangesEnabled = !!enabled;
    }

    function setReviewDisplayMode(instanceId, mode) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return;
        inst.reviewDisplayMode = _normalizeReviewDisplayMode(mode);
        _applyReviewDisplayMode(inst);
    }

    function _normalizeReviewDisplayMode(mode) {
        var value = String(mode ?? 'AllMarkup');
        if (value === '0') return 'AllMarkup';
        if (value === '1') return 'SimpleMarkup';
        if (value === '2') return 'NoMarkup';
        if (value === '3') return 'Original';
        value = value.toLowerCase();
        if (value === 'simplemarkup' || value === 'simple') return 'SimpleMarkup';
        if (value === 'nomarkup' || value === 'none') return 'NoMarkup';
        if (value === 'original') return 'Original';
        return 'AllMarkup';
    }

    function _applyReviewDisplayMode(inst) {
        if (!inst || !inst.root) return;
        var mode = _normalizeReviewDisplayMode(inst.reviewDisplayMode);
        inst.reviewDisplayMode = mode;
        inst.root.classList.remove(
            'tm-wysiwyg-host--review-all-markup',
            'tm-wysiwyg-host--review-simple-markup',
            'tm-wysiwyg-host--review-no-markup',
            'tm-wysiwyg-host--review-original');
        inst.root.classList.add('tm-wysiwyg-host--review-' + mode.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase());
        inst.root.setAttribute('data-review-display-mode', mode);
    }

    function setReadOnly(instanceId, readOnly) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return;
        inst.readOnly = !!readOnly;
        inst.root.setAttribute('aria-readonly', inst.readOnly ? 'true' : 'false');
    }

    function _hasEditorSelectionOrFocus(inst) {
        if (!inst || !inst.root) return false;
        var active = document.activeElement;
        if (active && (active === inst.root || inst.root.contains(active))) {
            return true;
        }

        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return false;
        return _nodeBelongsToRoot(sel.anchorNode, inst.root) || _nodeBelongsToRoot(sel.focusNode, inst.root);
    }

    function _nodeBelongsToRoot(node, root) {
        if (!node || !root) return false;
        if (node === root) return true;
        var element = node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement;
        return !!element && root.contains(element);
    }

    /**
     * Focuses the editor surface.
     * @param {string} instanceId
     */
    function focus(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return;
        _focusEditorBody(inst);
    }

    function _focusEditorBody(inst) {
        if (!inst || !inst.root) return;
        _deactivatePageRegion(inst);
        var body = inst.root.querySelector('.tm-wysiwyg-page__body[contenteditable="true"]');
        (body || inst.root).focus({ preventScroll: true });
    }

    function closeHeaderFooter(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return false;

        _deactivatePageRegion(inst);
        var bodySelection = inst.lastBodySelectionSnapshot
            || (_selectionRegionName(inst.lastSelectionSnapshot) === 'body'
                ? inst.lastSelectionSnapshot
                : null);

        if (bodySelection) {
            _restoreSelection(inst, bodySelection);
        } else {
            _focusEditorBody(inst);
            var body = inst.root.querySelector('.tm-wysiwyg-page__body[contenteditable="true"]');
            if (body) _ensureEditableSelection(inst, body);
        }

        var snapshot = _captureSelectionSnapshot(inst);
        inst.lastSelectionSnapshot = snapshot;
        _rememberBodySelection(inst, snapshot);
        inst.pendingSelectionSnapshot = snapshot;
        _flushSelectionNotification(inst);
        return true;
    }

    function getSelectionSnapshot(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return null;
        var snapshot = _captureSelectionSnapshot(inst);
        return _toPascalSelection(snapshot);
    }

    function getRuntimeSelection(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return null;
        var snapshot = _captureSelectionSnapshot(inst);
        if (!snapshot && inst.runtimeSelection && (inst.runtimeSelection.activeImageBlockId || inst.runtimeSelection.ActiveImageBlockId) && !_getSelectedImageFigure(inst)) {
            inst.runtimeSelection = Object.assign({}, inst.runtimeSelection, {
                region: 'Body',
                activeImageBlockId: null,
                ActiveImageBlockId: null
            });
        }
        var runtimeSelection = snapshot ? _createRuntimeSelectionFromSnapshot(snapshot) : inst.runtimeSelection;
        return _toPascalSelection(_createSelectionSnapshotFromRuntimeSelection(runtimeSelection));
    }

    /**
     * Restores a selection from a snapshot.
     * Called by Blazor after applying a patch or snapshot.
     * @param {string} instanceId
     * @param {Object} snapshot
     */
    function restoreSelection(instanceId, snapshot) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return;
        _restoreSelection(inst, snapshot);
    }

    /**
     * Returns active/mixed/inactive formatting state for the current selection.
     * @param {string} instanceId
     * @returns {Object|null}
     */
    function getFormattingState(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return null;
        return _getFormattingState(inst);
    }

    function getLastCommandTransaction(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !inst.lastCommandTransaction) return null;
        return _cloneRuntimeJson(inst.lastCommandTransaction);
    }

    function getUndoState(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) {
            return {
                CanUndo: false,
                CanRedo: false,
                UndoDepth: 0,
                RedoDepth: 0,
                NextUndoDescription: null,
                NextRedoDescription: null,
                Epoch: 0
            };
        }

        return _getUndoState(inst);
    }

    function getDirtyState(instanceId) {
        const inst = _instances.get(instanceId);
        return _getDirtyState(inst);
    }

    function markSaved(instanceId, marker) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return false;
        _commitCurrentRuntimeTransaction(inst, true);
        _markRuntimeSaved(inst, marker || null);
        return true;
    }

    function getOfflineState(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return null;

        return JSON.stringify({
            version: 1,
            dirtyState: _getDirtyState(inst),
            undoState: _getUndoState(inst),
            undoStack: _cloneRuntimeJson(inst.commandUndoStack || []),
            redoStack: _cloneRuntimeJson(inst.commandRedoStack || []),
            pendingTransaction: _cloneRuntimeJson(inst.pendingUndoTransaction || null),
            runtimeUndoEpoch: inst.runtimeUndoEpoch || 0,
            lastCommittedHtml: inst.lastCommittedHtml || ''
        });
    }

    function applyOfflineState(instanceId, stateJson) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !stateJson) return false;

        var state;
        try {
            state = typeof stateJson === 'string' ? JSON.parse(stateJson) : stateJson;
        } catch {
            return false;
        }

        inst.commandUndoStack = Array.isArray(state.undoStack) ? _cloneRuntimeJson(state.undoStack) : [];
        inst.commandRedoStack = Array.isArray(state.redoStack) ? _cloneRuntimeJson(state.redoStack) : [];
        inst.pendingUndoTransaction = state.pendingTransaction ? _cloneRuntimeJson(state.pendingTransaction) : null;
        inst.runtimeUndoEpoch = Number(state.runtimeUndoEpoch ?? state.undoState?.Epoch ?? state.dirtyState?.UndoEpoch ?? inst.runtimeUndoEpoch ?? 0);
        if (state.lastCommittedHtml) {
            inst.lastCommittedHtml = String(state.lastCommittedHtml);
        }

        var dirty = state.dirtyState || {};
        inst.isDirty = !!(dirty.IsDirty ?? dirty.isDirty);
        inst.dirtyEpoch = Number(dirty.DirtyEpoch ?? dirty.dirtyEpoch ?? inst.dirtyEpoch ?? 0);
        inst.savedEpoch = Number(dirty.SavedEpoch ?? dirty.savedEpoch ?? inst.savedEpoch ?? 0);
        inst.lastDirtyReason = String(dirty.Reason ?? dirty.reason ?? inst.lastDirtyReason ?? '');
        inst.lastSavedMarker = dirty.LastSavedMarker ?? dirty.lastSavedMarker ?? inst.lastSavedMarker ?? null;
        inst.lastSavedAt = dirty.LastSavedAt ?? dirty.lastSavedAt ?? inst.lastSavedAt ?? null;
        _notifyUndoStateChanged(inst);
        _notifyDirtyStateChanged(inst);
        return true;
    }

    function getDebugUndoStack(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) {
            return {
                Undo: [],
                Redo: [],
                Pending: null,
                LastApply: null
            };
        }

        function summarize(transaction) {
            if (!transaction) return null;
            var operations = Array.isArray(transaction.operations) ? transaction.operations : [];
            var plainBefore = '';
            var plainAfter = '';
            if (transaction.beforeHtml || transaction.afterHtml) {
                var debugContainer = document.createElement('div');
                debugContainer.innerHTML = transaction.beforeHtml || '';
                plainBefore = debugContainer.textContent || '';
                debugContainer.innerHTML = transaction.afterHtml || '';
                plainAfter = debugContainer.textContent || '';
            }
            return {
                TransactionId: transaction.transactionId || '',
                Source: transaction.source || '',
                Description: transaction.description || '',
                OperationCount: operations.length,
                Operations: operations.map(function (operation) {
                    var selection = operation.selection || operation.Selection || operation.beforeSelection || operation.BeforeSelection || {};
                    var afterSelection = operation.afterSelection || operation.AfterSelection || {};
                    return {
                        Type: operation.type || operation.Type || '',
                        Data: operation.data || operation.Data || '',
                        BlockId: selection.anchorBlockId || selection.AnchorBlockId || '',
                        InlineId: selection.anchorInlineId || selection.AnchorInlineId || '',
                        Offset: selection.anchorOffset ?? selection.AnchorOffset ?? null,
                        AfterOffset: afterSelection.anchorOffset ?? afterSelection.AnchorOffset ?? null
                    };
                }),
                BeforeContainsPhase7: transaction.beforeHtml ? transaction.beforeHtml.indexOf('phase7-') >= 0 : false,
                AfterContainsPhase7: transaction.afterHtml ? transaction.afterHtml.indexOf('phase7-') >= 0 : false,
                BeforeText: plainBefore.slice(0, 500),
                AfterText: plainAfter.slice(0, 500)
            };
        }

        return {
            Undo: (inst.commandUndoStack || []).map(summarize),
            Redo: (inst.commandRedoStack || []).map(summarize),
            Pending: summarize(inst.pendingUndoTransaction),
            LastApply: inst.lastUndoApplyResult || null
        };
    }

    function undo(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || inst.readOnly) return false;
        _commitCurrentRuntimeTransaction(inst, true);
        var transaction = inst.commandUndoStack.pop();
        if (!transaction) {
            _notifyUndoStateChanged(inst);
            return false;
        }

        _markTransactionOperationsAsLocallyHandled(inst, transaction);
        inst.runtimeUndoEpoch = (inst.runtimeUndoEpoch || 0) + 1;
        if (!_applyRuntimeTransactionOperations(inst, transaction, false)) {
            _restoreRuntimeTransactionState(inst, transaction, false);
        }
        _syncRuntimeRevisionActionsAfterUndoRedo(inst, transaction, false);
        inst.commandRedoStack.push(transaction);
        inst.lastCommandTransaction = transaction;
        _markRuntimeDirty(inst, 'undo');
        _notifyUndoStateChanged(inst);
        _scheduleRemoteQueueFlush(inst);
        return true;
    }

    function redo(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || inst.readOnly) return false;
        _commitCurrentRuntimeTransaction(inst, true);
        var transaction = inst.commandRedoStack.pop();
        if (!transaction) {
            _notifyUndoStateChanged(inst);
            return false;
        }

        inst.runtimeUndoEpoch = (inst.runtimeUndoEpoch || 0) + 1;
        if (!_applyRuntimeTransactionOperations(inst, transaction, true)) {
            _restoreRuntimeTransactionState(inst, transaction, true);
        }
        _syncRuntimeRevisionActionsAfterUndoRedo(inst, transaction, true);
        inst.commandUndoStack.push(transaction);
        inst.lastCommandTransaction = transaction;
        _markRuntimeDirty(inst, 'redo');
        _notifyUndoStateChanged(inst);
        _scheduleRemoteQueueFlush(inst);
        return true;
    }

    function _syncRuntimeRevisionActionsAfterUndoRedo(inst, transaction, forward) {
        if (!inst || !transaction) return;
        var operations = Array.isArray(transaction.operations) ? transaction.operations : [];
        var changed = false;
        operations.forEach(function (operation) {
            var type = operation.type || operation.Type || '';
            if (type !== 'ReviewRevision') return;
            var revisionId = operation.revisionId || operation.RevisionId || '';
            if (!revisionId) return;
            _setRuntimeRevisionAction(inst, revisionId, forward ? (operation.action || operation.Action || 'Accepted') : 'Pending');
            changed = true;
        });

        if (changed) {
            _syncRuntimeRevisionsToSnapshot(inst);
            _notifyRuntimeRevisionsChanged(inst);
        }
    }

    function _markTransactionOperationsAsLocallyHandled(inst, transaction) {
        if (!inst || !transaction || !inst.appliedOperationIds) return;
        var operations = Array.isArray(transaction.operations) ? transaction.operations : [];
        operations.forEach(function (operation) {
            var operationId = operation.operationId || operation.OperationId || operation.id || operation.Id || '';
            if (operationId) {
                inst.appliedOperationIds.add(operationId);
            }
        });
    }

    function _restoreRuntimeTransactionState(inst, transaction, forward) {
        var html = forward ? transaction.afterHtml : transaction.beforeHtml;
        var selection = forward ? transaction.afterSelection : transaction.beforeSelection;
        if (html == null) return;

        inst._applyingOwnPatch = true;
        try {
            inst.root.innerHTML = html;
            inst.lastCommittedHtml = html;
        } finally {
            inst._applyingOwnPatch = false;
        }

        _invalidateMeasureCache(inst);
        if (inst.renderStats) {
            inst.renderStats.incrementalOperations++;
            inst.renderStats.lastRenderReason = forward ? 'redo' : 'undo';
        }

        _restoreSelection(inst, selection);
        var restoredSelection = _captureSelectionSnapshot(inst) || selection;
        inst.lastSelectionSnapshot = restoredSelection;
        inst.runtimeSelection = restoredSelection ? _createRuntimeSelectionFromSnapshot(restoredSelection) : null;
        _scheduleSelectionNotification(inst, restoredSelection);
    }

    function _applyRuntimeTransactionOperations(inst, transaction, forward) {
        var operations = transaction && transaction.operations;
        if (!Array.isArray(operations) || operations.length === 0) return false;
        if (!operations.every(function (operation) { return (operation.type || operation.Type) === 'InsertText'; })) {
            return false;
        }

        var ordered = forward ? operations : operations.slice().reverse();
        inst._applyingOwnPatch = true;
        try {
            for (var i = 0; i < ordered.length; i++) {
                var operation = ordered[i];
                var text = operation.data || operation.Data || '';
                var selection = operation.selection || operation.Selection || operation.beforeSelection || operation.BeforeSelection;
                if (!text || !selection) return false;
                var ok = forward
                    ? _insertTextAtSnapshot(inst, selection, text)
                    : _deleteInsertedTextForOperation(inst, operation, text.length);
                if (!ok) return false;
            }
        } finally {
            inst._applyingOwnPatch = false;
        }

        var targetSelection = forward ? transaction.afterSelection : transaction.beforeSelection;
        _invalidateMeasureCache(inst);
        if (inst.renderStats) {
            inst.renderStats.incrementalOperations++;
            inst.renderStats.lastRenderReason = forward ? 'redo-ops' : 'undo-ops';
        }
        inst.lastCommittedHtml = inst.root.innerHTML;
        _restoreSelection(inst, targetSelection);
        var restoredSelection = _captureSelectionSnapshot(inst) || targetSelection;
        inst.lastSelectionSnapshot = restoredSelection;
        inst.runtimeSelection = restoredSelection ? _createRuntimeSelectionFromSnapshot(restoredSelection) : null;
        _scheduleSelectionNotification(inst, restoredSelection);
        return true;
    }

    function _findTextNodeForSnapshot(inst, selection) {
        var blockId = selection.anchorBlockId || selection.AnchorBlockId || '';
        var inlineId = selection.anchorInlineId || selection.AnchorInlineId || '';
        var block = blockId ? inst.root.querySelector('[data-block-id="' + CSS.escape(blockId) + '"]') : null;
        if (!block) return null;
        var inline = inlineId ? block.querySelector('[data-inline-id="' + CSS.escape(inlineId) + '"]') : null;
        var node = inline ? _firstDeepTextNode(inline) : _firstDeepTextNode(block);
        return node ? { node: node, block: block } : null;
    }

    function _insertTextAtSnapshot(inst, selection, text) {
        var target = _findTextNodeForSnapshot(inst, selection);
        if (!target || !target.node) return false;
        var offset = Math.max(0, Math.min(selection.anchorOffset ?? selection.AnchorOffset ?? 0, target.node.textContent.length));
        var current = target.node.textContent || '';
        target.node.textContent = current.slice(0, offset) + text + current.slice(offset);
        return true;
    }

    function _deleteTextAtSnapshot(inst, selection, length) {
        var target = _findTextNodeForSnapshot(inst, selection);
        if (!target || !target.node) return false;
        var offset = Math.max(0, Math.min(selection.anchorOffset ?? selection.AnchorOffset ?? 0, target.node.textContent.length));
        var current = target.node.textContent || '';
        target.node.textContent = current.slice(0, offset) + current.slice(offset + length);
        return true;
    }

    function _deleteInsertedTextForOperation(inst, operation, length) {
        var beforeSelection = operation.selection || operation.Selection || operation.beforeSelection || operation.BeforeSelection;
        var afterSelection = operation.afterSelection || operation.AfterSelection || null;
        var selection = beforeSelection;
        var offset = selection ? (selection.anchorOffset ?? selection.AnchorOffset ?? 0) : 0;
        if (afterSelection) {
            var afterOffset = afterSelection.anchorOffset ?? afterSelection.AnchorOffset;
            if (Number.isFinite(afterOffset)) {
                selection = _cloneRuntimeJson(afterSelection);
                offset = Math.max(0, afterOffset - length);
                selection.anchorOffset = offset;
                selection.AnchorOffset = offset;
                selection.focusOffset = offset;
                selection.FocusOffset = offset;
            }
        }

        if (_deleteTextAtSnapshot(inst, selection, length)) {
            return true;
        }

        var text = operation.data || operation.Data || '';
        var walker = document.createTreeWalker(inst.root, NodeFilter.SHOW_TEXT);
        var node;
        while ((node = walker.nextNode())) {
            var current = node.textContent || '';
            var index = current.indexOf(text);
            if (index >= 0) {
                node.textContent = current.slice(0, index) + current.slice(index + text.length);
                return true;
            }
        }

        return false;
    }

    function _getUndoState(inst) {
        var pending = inst.pendingUndoTransaction;
        var undoDepth = (inst.commandUndoStack ? inst.commandUndoStack.length : 0) + (pending ? 1 : 0);
        var redoDepth = inst.commandRedoStack ? inst.commandRedoStack.length : 0;
        var undoItem = pending || (inst.commandUndoStack && inst.commandUndoStack.length > 0
            ? inst.commandUndoStack[inst.commandUndoStack.length - 1]
            : null);
        var redoItem = inst.commandRedoStack && inst.commandRedoStack.length > 0
            ? inst.commandRedoStack[inst.commandRedoStack.length - 1]
            : null;
        return {
            CanUndo: undoDepth > 0,
            CanRedo: redoDepth > 0,
            UndoDepth: undoDepth,
            RedoDepth: redoDepth,
            JsOwnedUndo: true,
            NextUndoDescription: undoItem ? (undoItem.description || undoItem.command || 'Edit') : null,
            NextRedoDescription: redoItem ? (redoItem.description || redoItem.command || 'Edit') : null,
            Epoch: inst.runtimeUndoEpoch || 0,
            PendingTransactionId: pending ? pending.transactionId : null,
            LastTransactionId: inst.lastCommandTransaction ? inst.lastCommandTransaction.transactionId : null
        };
    }

    function _notifyUndoStateChanged(inst) {
        if (!inst || inst.disposed) return;
        var state = _getUndoState(inst);
        _applyUndoStateToToolbarDom(state);
        var previous = inst.lastUndoState || {};
        inst.lastUndoState = _cloneRuntimeJson(state);
        if (previous.CanUndo === state.CanUndo
            && previous.CanRedo === state.CanRedo
            && previous.UndoDepth === state.UndoDepth
            && previous.RedoDepth === state.RedoDepth
            && previous.NextUndoDescription === state.NextUndoDescription
            && previous.NextRedoDescription === state.NextRedoDescription
            && previous.Epoch === state.Epoch) {
            return;
        }

        _invokeDotNet(inst, 'HandleUndoStateChanged', state);
    }

    function _applyUndoStateToToolbarDom(state) {
        var undoButton = document.querySelector('[data-testid="document-undo"]');
        var redoButton = document.querySelector('[data-testid="document-redo"]');
        if (undoButton) {
            undoButton.disabled = !state.CanUndo;
            undoButton.setAttribute('aria-disabled', state.CanUndo ? 'false' : 'true');
            if (state.NextUndoDescription) undoButton.title = 'Undo: ' + state.NextUndoDescription;
        }
        if (redoButton) {
            redoButton.disabled = !state.CanRedo;
            redoButton.setAttribute('aria-disabled', state.CanRedo ? 'false' : 'true');
            if (state.NextRedoDescription) redoButton.title = 'Redo: ' + state.NextRedoDescription;
        }
    }

    /**
     * Executes an editor command from the Blazor ribbon.
     * @param {string} instanceId
     * @param {string} command — e.g. "toggleMark", "insertBlock"
     * @param {Object} payload
     */
    function executeCommand(instanceId, command, payload) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || inst.readOnly) return;
        _commitCurrentRuntimeTransaction(inst, true);
        if (String(command || '').toLowerCase().indexOf('image') >= 0) {
            _debugImage(inst, 'command.execute', {
                command: command,
                payload: payload || null,
                figures: _debugImageFigures(inst)
            });
        }

        switch (command) {
            case 'toggleBold':
                _executeToggleMarkCommand(inst, 'Bold', payload || {}, 'toggleBold');
                break;
            case 'toggleItalic':
                _executeToggleMarkCommand(inst, 'Italic', payload || {}, 'toggleItalic');
                break;
            case 'toggleUnderline':
                _executeToggleMarkCommand(inst, 'Underline', payload || {}, 'toggleUnderline');
                break;
            case 'toggleStrikethrough':
                _executeToggleMarkCommand(inst, 'Strikethrough', payload || {}, 'toggleStrikethrough');
                break;
            case 'toggleMark':
                payload = payload || {};
                _executeToggleMarkCommand(inst, payload.markType || payload.MarkType || 'Bold', payload, 'toggleMark');
                break;
            case 'setFontFamily':
                _executeToggleMarkCommand(inst, 'FontFamily', _withValuePayload(payload), 'setFontFamily');
                break;
            case 'setFontSize':
                _executeToggleMarkCommand(inst, 'FontSize', _withValuePayload(payload), 'setFontSize');
                break;
            case 'setTextColor':
                _executeToggleMarkCommand(inst, 'TextColor', _withValuePayload(payload), 'setTextColor');
                break;
            case 'setHighlightColor':
                _executeToggleMarkCommand(inst, 'Highlight', _withValuePayload(payload), 'setHighlightColor');
                break;
            case 'applyLink':
            case 'insertLink':
                payload = payload || {};
                payload.markType = 'Link';
                _executeToggleMarkCommand(inst, 'Link', payload, 'insertLink');
                break;
            case 'removeLink':
                _executeRemoveLinkCommand(inst, payload || {});
                break;
            case 'replaceOne':
                _executeReplaceOneCommand(inst, payload || {});
                break;
            case 'replaceAll':
                _executeReplaceAllCommand(inst, payload || {});
                break;
            case 'clearFormatting':
                _executeClearFormattingCommand(inst, payload || {});
                break;
            case 'hideMiniToolbar':
                _dismissMiniToolbar(inst);
                break;
            case 'setParagraphProperties':
                _executeSetParagraphPropertiesCommand(inst, payload || {}, 'setParagraphProperties');
                break;
            case 'setParagraphAlignment':
                _executeSetParagraphPropertiesCommand(inst, { Alignment: (payload || {}).alignment ?? (payload || {}).Alignment }, 'setParagraphAlignment');
                break;
            case 'setLineSpacing':
                _executeSetParagraphPropertiesCommand(inst, { LineSpacing: (payload || {}).lineSpacing ?? (payload || {}).LineSpacing }, 'setLineSpacing');
                break;
            case 'increaseIndent':
                _executeSetParagraphPropertiesCommand(inst, { LeftIndentDelta: 36 }, 'increaseIndent');
                break;
            case 'decreaseIndent':
                _executeSetParagraphPropertiesCommand(inst, { LeftIndentDelta: -36 }, 'decreaseIndent');
                break;
            case 'toggleBulletList':
                _executeToggleListCommand(inst, false, payload || {});
                break;
            case 'toggleNumberedList':
                _executeToggleListCommand(inst, true, payload || {});
                break;
            case 'insertBlock':
                payload = payload || {};
                payload.selection = _captureSelectionSnapshot(inst);
                _invokeDotNet(inst, 'HandleCommandInsertBlock', payload);
                break;
            case 'insertImageUrl':
                var block = _createImageBlockFromPayload(payload);
                if (block) {
                    _insertImageBlock(inst, block, true, (payload || {}).selection || (payload || {}).Selection || inst.lastSelectionSnapshot);
                }
                break;
            case 'insertImageBlock': {
                payload = payload || {};
                var imageBlock = payload.block || payload.Block;
                if (imageBlock) {
                    _insertImageBlock(inst, imageBlock, true, payload.selection || payload.Selection || inst.lastSelectionSnapshot);
                }
                break;
            }
            case 'setImageWrapMode':
                _setSelectedImageWrapMode(inst, payload);
                break;
            case 'setImagePosition':
                _setSelectedImagePosition(inst, payload);
                break;
            case 'replaceImage':
                _replaceSelectedImage(inst);
                break;
            case 'setImageSize':
                _setSelectedImageSize(inst, payload);
                break;
            case 'setImageAltText': {
                var altPayload = payload || {};
                var altText = altPayload.altText || altPayload.AltText || altPayload.value || '';
                var altBlockId = altPayload.blockId || altPayload.BlockId || altPayload.imageId || altPayload.ImageId || '';
                _setImageAltText(inst, altText, altBlockId);
                break;
            }
            case 'toggleImageCaption': {
                var captionPayload = payload || {};
                var captionBlockId = captionPayload.blockId || captionPayload.BlockId || captionPayload.imageId || captionPayload.ImageId || '';
                _toggleImageCaption(inst, captionBlockId);
                break;
            }
            case 'setImageCaption': {
                var setCaptionPayload = payload || {};
                var setCaptionBlockId = setCaptionPayload.blockId || setCaptionPayload.BlockId || setCaptionPayload.imageId || setCaptionPayload.ImageId || '';
                var captionText = setCaptionPayload.caption ?? setCaptionPayload.Caption ?? setCaptionPayload.value ?? '';
                _setImageCaption(inst, captionText, setCaptionBlockId);
                break;
            }
            case 'setImageUrl': {
                var urlPayload = payload || {};
                var newUrl = urlPayload.url || urlPayload.Url || urlPayload.value || '';
                var urlBlockId = urlPayload.blockId || urlPayload.BlockId || urlPayload.imageId || urlPayload.ImageId || '';
                _setImageUrl(inst, newUrl, urlBlockId);
                break;
            }
            case 'setImageLink': {
                var linkPayload = payload || {};
                var linkUrl = linkPayload.url || linkPayload.Url || linkPayload.value || '';
                var linkBlockId = linkPayload.blockId || linkPayload.BlockId || linkPayload.imageId || linkPayload.ImageId || '';
                _setImageLink(inst, linkUrl, linkBlockId);
                break;
            }
            case 'syncHeaderFooterLayout':
                _executeSyncHeaderFooterLayoutCommand(inst, payload || {});
                break;
            case 'closeAutocompleteQuery':
                _closeAutocompleteQuery(inst, false);
                break;
            case 'removeAutocompleteQuery':
                return _removeAutocompleteQuery(inst);
            case 'insertAutocompleteText': {
                var autocompleteTextPayload = payload || {};
                return _insertAutocompleteText(inst, autocompleteTextPayload.text || autocompleteTextPayload.Text || '');
            }
            case 'insertPageBreak': {
                var pageBreakPayload = payload || {};
                var pageBreakSelection = pageBreakPayload.selection || pageBreakPayload.Selection || null;
                if (pageBreakSelection) {
                    _restoreSelection(inst, pageBreakSelection);
                }
                _ensureEditorSelection(inst);
                var pageBreakRegion = _getActiveSchemaRegion(inst);
                if (!_schemaAllowsBlock(6, pageBreakRegion)) {
                    inst.lastInsertionPolicyWarnings = [{ code: 'page-break-not-allowed', region: pageBreakRegion, blockType: 6 }];
                    break;
                }

                var pageBreakBeforeSelection = _captureSelectionSnapshot(inst);
                _beginUndoTransaction(inst, 'insertPageBreak', 'Insert page break', pageBreakBeforeSelection, true);
                _insertClipboardBlocks(inst, [_createPageBreakBlock(), _createEmptyParagraphBlock()]);
                _commitCurrentRuntimeTransaction(inst, true);
                _notifyPageMetrics(inst);
                break;
            }
            case 'insertField':
                return _insertDocumentField(inst, payload || {});
            case 'deletePageBreak': {
                var deletePageBreakPayload = payload || {};
                return _deletePageBreak(inst, deletePageBreakPayload.blockId || deletePageBreakPayload.BlockId || inst.selectedPageBreakId || '');
            }
            // Phase 12: table structural commands.
            case 'insertTable': {
                var tp = payload || {};
                var tRows = tp.rows || tp.Rows || 2;
                var tCols = tp.columns || tp.Columns || tp.cols || tp.Cols || 2;
                var tableRegion = _getActiveSchemaRegion(inst);
                if (!_schemaAllowsToolbarBlockCommand(4, tableRegion)) {
                    inst.lastInsertionPolicyWarnings = [{ code: 'table-not-allowed', region: tableRegion, blockType: 4 }];
                    break;
                }
                _insertTable(inst, tRows, tCols);
                break;
            }
            case 'insertTableRowBefore':
                _applyTableCommandSelectionPayload(inst, payload);
                _insertTableRow(inst, true);
                break;
            case 'insertTableRow':
            case 'insertTableRowAfter':
                _applyTableCommandSelectionPayload(inst, payload);
                _insertTableRow(inst);
                break;
            case 'deleteTableRow':
                _applyTableCommandSelectionPayload(inst, payload);
                _deleteTableRow(inst);
                break;
            case 'insertTableColumnBefore':
                _applyTableCommandSelectionPayload(inst, payload);
                _insertTableColumn(inst, true);
                break;
            case 'insertTableColumn':
            case 'insertTableColumnAfter':
                _applyTableCommandSelectionPayload(inst, payload);
                _insertTableColumn(inst);
                break;
            case 'deleteTableColumn':
                _applyTableCommandSelectionPayload(inst, payload);
                _deleteTableColumn(inst);
                break;
            case 'deleteTable':
                _applyTableCommandSelectionPayload(inst, payload);
                _deleteTable(inst);
                break;
            case 'mergeTableCells':
                _applyTableCommandSelectionPayload(inst, payload);
                _mergeTableCells(inst);
                break;
            case 'splitTableCell':
                _applyTableCommandSelectionPayload(inst, payload);
                _splitTableCell(inst);
                break;
            case 'toggleTableHeaderRow':
                _applyTableCommandSelectionPayload(inst, payload);
                _toggleTableHeaderRow(inst);
                break;
            case 'setCellBackgroundColor': {
                var bgPayload = payload || {};
                var bgColor = bgPayload.color || bgPayload.Color || bgPayload.value || bgPayload.Value || '';
                _setCellBackgroundColor(inst, bgColor);
                break;
            }
            case 'tableProperties':
                _markActiveTableCell(_findCurrentTableCell(inst));
                break;
            case 'cellProperties':
                _markActiveTableCell(_findCurrentTableCell(inst));
                break;
            case 'setTableProperties':
                _setTableProperties(inst, payload);
                break;
            case 'setCellProperties':
                _setCellProperties(inst, payload);
                break;
            case 'resizeTableColumn':
                _resizeTableColumn(inst, payload);
                break;
            case 'acceptAllRevisions':
                reviewAllRevisions(instanceId, 'Accepted', payload || {});
                break;
            case 'rejectAllRevisions':
                reviewAllRevisions(instanceId, 'Rejected', payload || {});
                break;
            case 'closeHeaderFooter':
                closeHeaderFooter(instanceId);
                break;
            case 'undo':
                undo(instanceId);
                break;
            case 'redo':
                redo(instanceId);
                break;
            default:
                console.warn('tmDocumentWysiwyg: unknown command', command);
        }
    }

    function _withValuePayload(payload) {
        payload = payload || {};
        var value = payload.value ?? payload.Value ?? payload.data ?? payload.Data ?? '';
        return Object.assign({}, payload, { value: value, Value: value, data: value, Data: value });
    }

    function _insertDocumentField(inst, payload) {
        if (!inst || inst.readOnly) return false;
        _flushPendingInputPatch(inst);
        var explicitSelection = payload.selection || payload.Selection || null;
        if (explicitSelection) {
            _restoreSelection(inst, explicitSelection);
        }
        _ensureEditorSelection(inst);
        var selection = explicitSelection || _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot;
        if (!selection) return false;

        var fieldType = _normalizeDocumentFieldType(payload.fieldType ?? payload.FieldType ?? payload.type ?? payload.Type);
        var fallback = payload.fallbackText || payload.FallbackText || _fieldFallbackLabel(fieldType);
        var field = {
            $type: 'field',
            Id: _createInlineId(),
            FieldType: fieldType,
            Format: payload.format || payload.Format || '',
            FallbackText: fallback,
            DisplayText: fallback
        };

        _beginUndoTransaction(inst, 'insertField', 'Insert field', selection, true);
        _dispatchPatch(inst, {
            type: 'InsertInline',
            operationId: _nextRuntimeOperationId(inst),
            epoch: inst.runtimeUndoEpoch || 0,
            inline: field,
            Inline: field,
            selection: selection,
            Selection: selection,
            beforeSelection: selection,
            BeforeSelection: selection,
            afterSelection: selection,
            AfterSelection: selection,
            transactionId: null,
            TransactionId: null,
            protocolVersion: inst.options.protocolVersion || 1
        });
        _commitCurrentRuntimeTransaction(inst, true);
        return true;
    }

    function _executeToggleMarkCommand(inst, markType, payload, commandName) {
        var normalizedMark = _normalizeMarkType(markType);
        if (!normalizedMark) return;

        payload = payload || {};
        var explicitSelection = payload.selection || payload.Selection || null;
        if (explicitSelection) {
            _restoreSelection(inst, explicitSelection);
        }
        _ensureEditorSelection(inst);
        var beforeSelection = _captureSelectionSnapshot(inst);
        var beforeFormatting = _getFormattingState(inst);
        var beforeHtml = inst.root.innerHTML;
        var data = payload.href || payload.Href || payload.value || payload.Value || payload.data || payload.Data || '';
        var title = payload.title || payload.Title || payload.linkTitle || payload.LinkTitle || '';
        data = _sanitizeMarkData(normalizedMark, data, inst);
        title = String(title || '').trim();
        if (_isValueMark(normalizedMark) && !data) return;
        if (normalizedMark === 'Link' && !data) return;
        var result = _applyToggleMarkToDom(inst, normalizedMark, data, false, title);
        if (!result) {
            return;
        }

        var afterSelection = _captureSelectionSnapshot(inst);
        var afterFormatting = _getFormattingState(inst);
        inst.lastSelectionSnapshot = afterSelection;
        _scheduleSelectionNotification(inst, afterSelection);

        if (result && result.collapsed) {
            return;
        }

        _beginTypingTransaction(inst);
        var operationId = _nextRuntimeOperationId(inst);
        var revisionId = null;
        if (inst.trackChangesEnabled) {
            revisionId = _createRevisionId();
            _decorateFormattingRevision(inst, beforeSelection, revisionId);
            _createRuntimeRevision(
                inst,
                revisionId,
                'Formatting',
                JSON.stringify({
                    MarkType: normalizedMark,
                    NewActive: true,
                    BeforeHtml: beforeHtml,
                    AfterHtml: inst.root.innerHTML
                }),
                beforeSelection,
                afterSelection);
        }

        var patch = {
            type: normalizedMark === 'Link' || _isValueMark(normalizedMark) ? 'SetMarks' : 'ToggleMark',
            operationId: operationId,
            markType: normalizedMark,
            data: normalizedMark === 'Link' || _isValueMark(normalizedMark) ? data : null,
            linkTitle: normalizedMark === 'Link' ? title || null : null,
            selection: beforeSelection,
            beforeSelection: beforeSelection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        };
        if (revisionId) {
            patch.revisionId = revisionId;
            patch.revisionType = 'Formatting';
        }

        _dispatchPatch(inst, patch);
        _recordRuntimeCommandTransaction(inst, commandName || ('toggle' + normalizedMark), payload, operationId, beforeSelection, afterSelection, beforeFormatting, afterFormatting, beforeHtml, inst.root.innerHTML);
    }

    function _decorateFormattingRevision(inst, selection, revisionId) {
        if (!inst || !selection || !revisionId) return;
        var blockId = selection.anchorBlockId || selection.AnchorBlockId || '';
        var inlineId = selection.anchorInlineId || selection.AnchorInlineId || '';
        var block = blockId ? inst.root.querySelector('[data-block-id="' + _cssEscape(blockId) + '"]') : null;
        var inline = block && inlineId ? block.querySelector('[data-inline-id="' + _cssEscape(inlineId) + '"]') : null;
        var target = inline || block;
        if (!target) return;
        target.classList.add('tm-wysiwyg-revision', 'tm-wysiwyg-revision--format');
        target.setAttribute('data-revision-id', revisionId);
        target.setAttribute('data-revision-type', 'Formatting');
        target.setAttribute('data-testid', 'document-wysiwyg-revision-format');
    }

    function _executeRemoveLinkCommand(inst, payload) {
        payload = payload || {};
        var explicitSelection = payload.selection || payload.Selection || null;
        if (explicitSelection) {
            _restoreSelection(inst, explicitSelection);
        }

        _ensureEditorSelection(inst);
        var beforeSelection = _captureSelectionSnapshot(inst);
        var beforeFormatting = _getFormattingState(inst);
        var beforeHtml = inst.root.innerHTML;
        var linkInfo = getLinkInfo(inst.id) || {};
        var data = payload.href || payload.Href || payload.value || payload.Value || payload.data || payload.Data || linkInfo.Href || '';
        var title = payload.title || payload.Title || payload.linkTitle || payload.LinkTitle || linkInfo.Title || '';
        data = _sanitizeMarkData('Link', data, inst);
        if (!data) return;

        var result = _applyToggleMarkToDom(inst, 'Link', data, true, title);
        if (!result) {
            return;
        }

        var afterSelection = _captureSelectionSnapshot(inst);
        var afterFormatting = _getFormattingState(inst);
        inst.lastSelectionSnapshot = afterSelection;
        _scheduleSelectionNotification(inst, afterSelection);
        if (result.collapsed) {
            return;
        }

        _beginTypingTransaction(inst);
        var operationId = _nextRuntimeOperationId(inst);
        _dispatchPatch(inst, {
            type: 'ToggleMark',
            operationId: operationId,
            markType: 'Link',
            data: data,
            linkTitle: title || null,
            selection: beforeSelection,
            beforeSelection: beforeSelection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
        _recordRuntimeCommandTransaction(inst, 'removeLink', payload || {}, operationId, beforeSelection, afterSelection, beforeFormatting, afterFormatting, beforeHtml, inst.root.innerHTML);
    }

    function _executeClearFormattingCommand(inst, payload) {
        payload = payload || {};
        if (!payload.fromPointerCapture && inst.clearFormattingPointerCaptureHandledUntil && inst.clearFormattingPointerCaptureHandledUntil > Date.now()) {
            return;
        }

        inst.clearFormattingCommandCount = (inst.clearFormattingCommandCount || 0) + 1;
        var explicitSelection = payload.selection || payload.Selection || null;
        var contextSelection = _isTextSelectionSnapshot(inst.contextMenuSelectionSnapshot)
            ? inst.contextMenuSelectionSnapshot
            : null;
        var fallbackSelection = _isTextSelectionSnapshot(explicitSelection)
            ? explicitSelection
            : (contextSelection || inst.lastSelectionSnapshot);
        inst.lastClearFormattingFallbackSelection = fallbackSelection ? _cloneRuntimeJson(fallbackSelection) : null;
        if (fallbackSelection) {
            _restoreSelection(inst, fallbackSelection);
        }

        _ensureEditorSelection(inst);
        var beforeSelection = _captureSelectionSnapshot(inst);
        var beforeFormatting = _getFormattingState(inst);
        var beforeHtml = inst.root.innerHTML;
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || sel.isCollapsed || !inst.root.contains(sel.anchorNode)) {
            _restoreSelection(inst, fallbackSelection || inst.lastSelectionSnapshot);
            sel = window.getSelection();
            if (!sel || sel.rangeCount === 0 || sel.isCollapsed || !inst.root.contains(sel.anchorNode)) {
                inst.pendingTypingMarks = {};
                return;
            }
        }

        var range = sel.getRangeAt(0);
        _clearFormattingInDomRange(inst, range);
        var afterSelection = _captureSelectionSnapshot(inst);
        var afterFormatting = _getFormattingState(inst);
        _beginTypingTransaction(inst);
        var operationId = _nextRuntimeOperationId(inst);
        _dispatchPatch(inst, {
            type: 'ClearFormatting',
            operationId: operationId,
            selection: beforeSelection,
            beforeSelection: beforeSelection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
        _recordRuntimeCommandTransaction(inst, 'clearFormatting', payload || {}, operationId, beforeSelection, afterSelection, beforeFormatting, afterFormatting, beforeHtml, inst.root.innerHTML);
    }

    function _clearFormattingInDomRange(inst, range) {
        var originalSelectionSnapshot = _captureSelectionSnapshot(inst);
        var segments = _collectRangeInlineSegments(inst, range);
        if (!segments.length) {
            return;
        }

        segments.forEach(function (segment) {
            if (!segment.inline || !segment.inline.isConnected || segment.end <= segment.start) return;
            _splitInlineForClearFormatting(segment.inline, segment.start, segment.end);
        });

        if (originalSelectionSnapshot) {
            _restoreSelectionByBlockOffsets(inst, originalSelectionSnapshot);
        }
    }

    function _executeSetParagraphPropertiesCommand(inst, payload, commandName) {
        _ensureEditorSelection(inst);
        var beforeSelection = _captureSelectionSnapshot(inst);
        if (!beforeSelection) return;
        var beforeFormatting = _getFormattingState(inst);
        var beforeHtml = inst.root.innerHTML;

        var patch = payload.paragraphProperties || payload.ParagraphProperties || payload;
        patch = _sanitizeParagraphPropertiesPatch(patch);
        if (!_hasParagraphPropertiesPatch(patch)) return;

        var blocks = _getSelectedBlockElements(inst, beforeSelection);
        if (blocks.length === 0) return;

        blocks.forEach(function (block) {
            _applyParagraphPropertiesPatch(block, patch);
        });

        var afterSelection = _collapseSelectionSnapshotToFocus(beforeSelection) || beforeSelection;
        inst.lastSelectionSnapshot = afterSelection;
        _focusEditorBody(inst);
        _restoreSelection(inst, afterSelection);
        _hideMiniToolbar(inst, true);
        _scheduleSelectionNotification(inst, afterSelection);
        _beginTypingTransaction(inst);
        var afterFormatting = _getFormattingState(inst);
        var operationId = _nextRuntimeOperationId(inst);
        _dispatchPatch(inst, {
            type: 'SetParagraphProperties',
            operationId: operationId,
            paragraphProperties: patch,
            selection: beforeSelection,
            beforeSelection: beforeSelection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
        _recordRuntimeCommandTransaction(inst, commandName || 'setParagraphProperties', payload || {}, operationId, beforeSelection, afterSelection, beforeFormatting, afterFormatting, beforeHtml, inst.root.innerHTML);
    }

    function _executeToggleListCommand(inst, ordered, payload) {
        _ensureEditorSelection(inst);
        var beforeSelection = _captureSelectionSnapshot(inst);
        if (!beforeSelection) return;

        var beforeFormatting = _getFormattingState(inst);
        var beforeHtml = inst.root.innerHTML;
        var blocks = _getSelectedBlockElements(inst, beforeSelection);
        if (blocks.length === 0) return;

        var shouldTurnOff = blocks.every(function (block) {
            var tag = block.tagName ? block.tagName.toLowerCase() : '';
            return ordered ? tag === 'ol' : tag === 'ul';
        });
        var updatedBlocks = [];

        blocks.forEach(function (block) {
            var replacement = shouldTurnOff
                ? _convertListBlockToParagraph(block)
                : _convertBlockToList(block, ordered);
            if (!replacement) return;
            block.replaceWith(replacement);
            updatedBlocks.push(replacement);
        });

        if (updatedBlocks.length === 0) return;

        var afterSelection = _collapseSelectionSnapshotToFocus(beforeSelection) || beforeSelection;
        inst.lastSelectionSnapshot = afterSelection;
        _focusEditorBody(inst);
        _restoreSelection(inst, afterSelection);
        _hideMiniToolbar(inst, true);
        _scheduleSelectionNotification(inst, afterSelection);
        _beginTypingTransaction(inst);

        var operationId = _nextRuntimeOperationId(inst);
        var baseDoc = _resolveRuntimeDocument(inst) || {};
        var blockMap = _createBlockMap(baseDoc);
        updatedBlocks.forEach(function (block, index) {
            _dispatchPatch(inst, {
                type: 'UpdateBlock',
                operationId: index === 0 ? operationId : _nextRuntimeOperationId(inst),
                block: _serializeBlock(block, blockMap, index),
                selection: beforeSelection,
                beforeSelection: beforeSelection,
                afterSelection: afterSelection,
                transactionId: inst.currentTransactionId,
                protocolVersion: inst.options.protocolVersion || 1
            });
        });

        var commandName = ordered ? 'toggleNumberedList' : 'toggleBulletList';
        var afterFormatting = _getFormattingState(inst);
        _recordRuntimeCommandTransaction(inst, commandName, payload || {}, operationId, beforeSelection, afterSelection, beforeFormatting, afterFormatting, beforeHtml, inst.root.innerHTML);
    }

    function _convertBlockToList(block, ordered) {
        if (!block) return null;
        var tag = block.tagName ? block.tagName.toLowerCase() : '';
        if ((ordered && tag === 'ol') || (!ordered && tag === 'ul')) {
            return block;
        }

        var list = document.createElement(ordered ? 'ol' : 'ul');
        _copyBlockShell(block, list);
        var li = document.createElement('li');
        var source = tag === 'ul' || tag === 'ol'
            ? (block.querySelector('li') || block)
            : block;
        while (source.firstChild) {
            li.appendChild(source.firstChild);
        }
        if (!li.textContent && !li.querySelector('[data-inline-id]')) {
            var inline = document.createElement('span');
            inline.setAttribute('data-inline-id', _createInlineId());
            inline.appendChild(document.createTextNode(''));
            _ensureCaretPlaceholder(inline);
            li.appendChild(inline);
        }
        list.appendChild(li);
        return list;
    }

    function _convertListBlockToParagraph(block) {
        if (!block) return null;
        var paragraph = document.createElement('p');
        _copyBlockShell(block, paragraph);
        var li = block.querySelector('li') || block;
        while (li.firstChild) {
            paragraph.appendChild(li.firstChild);
        }
        if (!paragraph.textContent && !paragraph.querySelector('[data-inline-id]')) {
            var inline = document.createElement('span');
            inline.setAttribute('data-inline-id', _createInlineId());
            inline.appendChild(document.createTextNode(''));
            _ensureCaretPlaceholder(inline);
            paragraph.appendChild(inline);
        }
        return paragraph;
    }

    function _copyBlockShell(source, target) {
        target.className = source.className || '';
        if (!target.classList.contains('tm-wysiwyg-block')) {
            target.classList.add('tm-wysiwyg-block');
        }
        Array.from(source.attributes || []).forEach(function (attribute) {
            if (attribute.name === 'class' || attribute.name === 'style') return;
            target.setAttribute(attribute.name, attribute.value);
        });
        target.style.cssText = source.style.cssText || '';
    }

    function _nextRuntimeOperationId(inst) {
        inst.commandOperationCounter = (inst.commandOperationCounter || 0) + 1;
        return inst.id + '-op-' + inst.commandOperationCounter;
    }

    function _nextRuntimeTransactionId(inst) {
        inst.commandTransactionCounter = (inst.commandTransactionCounter || 0) + 1;
        return inst.currentTransactionId || (inst.id + '-cmd-' + inst.commandTransactionCounter);
    }

    function _recordRuntimeCommandTransaction(inst, command, payload, operationId, beforeSelection, afterSelection, beforeFormatting, afterFormatting, beforeHtml, afterHtml) {
        var transaction = _createRuntimeCommandTransaction(
            inst,
            command,
            payload,
            operationId,
            beforeSelection,
            afterSelection,
            beforeFormatting,
            afterFormatting,
            beforeHtml,
            afterHtml);

        inst.lastCommandTransaction = transaction;
        inst.commandUndoStack.push(transaction);
        inst.commandRedoStack = [];
        inst.lastCommittedHtml = transaction.afterHtml || inst.root.innerHTML;
        _rememberPendingCollaborationTransaction(inst, transaction);
        _markRuntimeDirty(inst, transaction.source || 'command');
        _notifyUndoStateChanged(inst);
        return transaction;
    }

    function _createRuntimeCommandTransaction(inst, command, payload, operationId, beforeSelection, afterSelection, beforeFormatting, afterFormatting, beforeHtml, afterHtml) {
        var transactionId = _nextRuntimeTransactionId(inst);
        var operation = {
            operationId: operationId || _nextRuntimeOperationId(inst),
            type: 'command',
            command: command,
            payload: _cloneRuntimeJson(payload || {}),
            beforeSelection: _cloneRuntimeJson(beforeSelection),
            afterSelection: _cloneRuntimeJson(afterSelection)
        };
        var inverse = {
            operationId: operation.operationId + '-inverse',
            type: 'inverseCommand',
            command: _getInverseRuntimeCommandName(command),
            inverseOf: operation.operationId,
            payload: {
                originalCommand: command,
                originalPayload: _cloneRuntimeJson(payload || {}),
                beforeFormatting: _cloneRuntimeJson(beforeFormatting),
                afterFormatting: _cloneRuntimeJson(afterFormatting)
            },
            beforeSelection: _cloneRuntimeJson(afterSelection),
            afterSelection: _cloneRuntimeJson(beforeSelection)
        };

        return {
            transactionId: transactionId,
            source: 'ribbon',
            command: command,
            description: _describeRuntimeCommand(command),
            beforeSelection: _cloneRuntimeJson(beforeSelection),
            afterSelection: _cloneRuntimeJson(afterSelection),
            beforeHtml: beforeHtml == null ? null : String(beforeHtml),
            afterHtml: afterHtml == null ? null : String(afterHtml),
            beforeFormatting: _cloneRuntimeJson(beforeFormatting),
            afterFormatting: _cloneRuntimeJson(afterFormatting),
            operations: [operation],
            inverseOperations: [inverse],
            createdAt: new Date().toISOString()
        };
    }

    function _getInverseRuntimeCommandName(command) {
        switch (command) {
            case 'toggleBold':
            case 'toggleItalic':
            case 'toggleUnderline':
            case 'toggleStrikethrough':
            case 'toggleMark':
                return command;
            case 'setFontFamily':
            case 'setFontSize':
            case 'setTextColor':
            case 'setHighlightColor':
            case 'setParagraphAlignment':
            case 'setLineSpacing':
            case 'setParagraphProperties':
            case 'increaseIndent':
            case 'decreaseIndent':
            case 'clearFormatting':
            case 'insertLink':
            case 'removeLink':
                return 'restoreFormatting';
            default:
                return 'restoreRuntimeState';
        }
    }

    function _describeRuntimeCommand(command) {
        switch (command) {
            case 'toggleBold': return 'Bold';
            case 'toggleItalic': return 'Italic';
            case 'toggleUnderline': return 'Underline';
            case 'toggleStrikethrough': return 'Strikethrough';
            case 'setFontFamily': return 'Font family';
            case 'setFontSize': return 'Font size';
            case 'setTextColor': return 'Text color';
            case 'setHighlightColor': return 'Highlight';
            case 'setParagraphAlignment': return 'Paragraph alignment';
            case 'setLineSpacing': return 'Line spacing';
            case 'setParagraphProperties': return 'Paragraph formatting';
            case 'increaseIndent': return 'Increase indent';
            case 'decreaseIndent': return 'Decrease indent';
            case 'clearFormatting': return 'Clear formatting';
            case 'insertLink': return 'Insert link';
            case 'removeLink': return 'Remove link';
            default: return command || 'Edit';
        }
    }

    function _cloneRuntimeJson(value) {
        if (value == null) return value;
        return JSON.parse(JSON.stringify(value));
    }

    function _sanitizeParagraphPropertiesPatch(source) {
        source = source || {};
        var patch = {};

        var alignment = source.alignment ?? source.Alignment;
        if (alignment != null) patch.Alignment = _alignmentToNumber(alignment);

        var lineSpacing = source.lineSpacing ?? source.LineSpacing;
        if (lineSpacing != null) patch.LineSpacing = _sanitizeLineSpacing(lineSpacing);

        var spacingBefore = source.spacingBefore ?? source.SpacingBefore;
        if (spacingBefore != null) patch.SpacingBefore = _sanitizeParagraphPoints(spacingBefore, 0, 144);

        var spacingAfter = source.spacingAfter ?? source.SpacingAfter;
        if (spacingAfter != null) patch.SpacingAfter = _sanitizeParagraphPoints(spacingAfter, 0, 144);

        var leftIndent = source.leftIndent ?? source.LeftIndent;
        if (leftIndent != null) patch.LeftIndent = _sanitizeParagraphPoints(leftIndent, 0, 432);

        var rightIndent = source.rightIndent ?? source.RightIndent;
        if (rightIndent != null) patch.RightIndent = _sanitizeParagraphPoints(rightIndent, 0, 432);

        var firstLineIndent = source.firstLineIndent ?? source.FirstLineIndent;
        if (firstLineIndent != null) patch.FirstLineIndent = _sanitizeParagraphPoints(firstLineIndent, -216, 216);

        var leftIndentDelta = source.leftIndentDelta ?? source.LeftIndentDelta;
        if (leftIndentDelta != null) patch.LeftIndentDelta = _sanitizeParagraphPoints(leftIndentDelta, -432, 432);

        var rightIndentDelta = source.rightIndentDelta ?? source.RightIndentDelta;
        if (rightIndentDelta != null) patch.RightIndentDelta = _sanitizeParagraphPoints(rightIndentDelta, -432, 432);

        var firstLineIndentDelta = source.firstLineIndentDelta ?? source.FirstLineIndentDelta;
        if (firstLineIndentDelta != null) patch.FirstLineIndentDelta = _sanitizeParagraphPoints(firstLineIndentDelta, -216, 216);

        return patch;
    }

    function _hasParagraphPropertiesPatch(patch) {
        return patch && Object.keys(patch).length > 0;
    }

    function _getSelectedBlockElements(inst, selection) {
        var anchorId = selection.anchorBlockId || selection.AnchorBlockId || '';
        var focusId = selection.focusBlockId || selection.FocusBlockId || anchorId;
        if (!anchorId) return [];

        var allBlocks = Array.from(inst.root.querySelectorAll('.tm-wysiwyg-block[data-block-id]'))
            .filter(function (block) {
                return _isParagraphLikeElement(block);
            });
        var anchorIndex = allBlocks.findIndex(function (block) { return block.getAttribute('data-block-id') === anchorId; });
        if (anchorIndex < 0) return [];

        var focusIndex = allBlocks.findIndex(function (block) { return block.getAttribute('data-block-id') === focusId; });
        if (focusIndex < 0) focusIndex = anchorIndex;

        var start = Math.min(anchorIndex, focusIndex);
        var end = Math.max(anchorIndex, focusIndex);
        return allBlocks.slice(start, end + 1);
    }

    function _isParagraphLikeElement(block) {
        if (!block) return false;
        var tag = block.tagName.toLowerCase();
        return tag === 'p'
            || tag === 'blockquote'
            || tag === 'ul'
            || tag === 'ol'
            || /^h[1-6]$/.test(tag);
    }

    function _ensureEditorSelection(inst) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || !inst.root.contains(sel.anchorNode)) {
            _restoreSelection(inst, inst.lastSelectionSnapshot);
        }
    }

    function _applyToggleMarkToDom(inst, markType, data, forceRemove, title) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || !inst.root.contains(sel.anchorNode)) {
            _restoreSelection(inst, inst.lastSelectionSnapshot);
            sel = window.getSelection();
        }
        if (!sel || sel.rangeCount === 0 || !inst.root.contains(sel.anchorNode)) return null;

        if (sel.isCollapsed) {
            if (inst.pendingTypingMarks[markType] && !forceRemove) {
                delete inst.pendingTypingMarks[markType];
            } else if (!forceRemove) {
                inst.pendingTypingMarks[markType] = markType === 'Link'
                    ? { href: data || '', title: title || '' }
                    : _isValueMark(markType) ? { value: data || '' } : {};
            } else {
                delete inst.pendingTypingMarks[markType];
            }
            return { collapsed: true };
        }

        var originalSelectionSnapshot = _captureSelectionSnapshot(inst);
        var range = sel.getRangeAt(0);
        var isToggleMark = !_isValueMark(markType) && _normalizeMarkType(markType) !== 'Link';
        var removeForToggle = forceRemove || (isToggleMark && _getSelectionMarkState(inst, markType) === 1);
        var startInfo = _mapNodeToBlockInline(range.startContainer, range.startOffset, inst.root);
        var endInfo = _mapNodeToBlockInline(range.endContainer, range.endOffset, inst.root);
        var startElement = range.startContainer && range.startContainer.nodeType === Node.ELEMENT_NODE
            ? range.startContainer
            : range.startContainer?.parentElement;
        var endElement = range.endContainer && range.endContainer.nodeType === Node.ELEMENT_NODE
            ? range.endContainer
            : range.endContainer?.parentElement;
        var startBlock = startElement ? startElement.closest('[data-block-id]') : null;
        var endBlock = endElement ? endElement.closest('[data-block-id]') : null;
        var startInlineElement = startBlock ? _findSemanticInlineElement(startElement, startBlock) : null;
        var endInlineElement = endBlock ? _findSemanticInlineElement(endElement, endBlock) : null;
        if (!startInfo
            || !endInfo
            || startInfo.blockId !== endInfo.blockId
            || startInfo.inlineId !== endInfo.inlineId
            || !startInlineElement
            || !endInlineElement
            || startInlineElement !== endInlineElement) {
            var acrossResult = _applyMarkAcrossSelection(inst, range, markType, data, removeForToggle, title);
            if (originalSelectionSnapshot) {
                _restoreSelectionByBlockOffsets(inst, originalSelectionSnapshot);
            }
            return acrossResult;
        }

        var block = inst.root.querySelector('[data-block-id="' + _cssEscape(startInfo.blockId || '') + '"]');
        var inline = block && block.querySelector('[data-inline-id="' + _cssEscape(startInfo.inlineId || '') + '"]');
        if (!inline) return null;

        var start = Math.min(startInfo.offset, endInfo.offset);
        var end = Math.max(startInfo.offset, endInfo.offset);
        var removed = removeForToggle || (isToggleMark && _rangeHasDomMark(inline, start, end, markType));
        _splitInlineForMark(inline, start, end, markType, data, removed, title);
        if (originalSelectionSnapshot) {
            _restoreSelectionByBlockOffsets(inst, originalSelectionSnapshot);
        }

        return { collapsed: false };
    }

    function _restoreSelectionByBlockOffsets(inst, snapshot) {
        if (!snapshot) return;
        _restoreSelection(inst, Object.assign({}, snapshot, {
            anchorNodeId: snapshot.anchorBlockId || snapshot.AnchorBlockId || null,
            focusNodeId: snapshot.focusBlockId || snapshot.FocusBlockId || snapshot.anchorBlockId || snapshot.AnchorBlockId || null,
            anchorInlineId: null,
            focusInlineId: null
        }));
    }

    function _wrapSelectionWithMark(range, markType, data, forceRemove, title) {
        if (forceRemove) {
            return { collapsed: false };
        }

        var wrapper = document.createElement('span');
        _applyMarkStyle(wrapper, markType, markType === 'Link' ? { href: data || '', title: title || '' } : _isValueMark(markType) ? { value: data || '' } : {});
        wrapper.appendChild(range.extractContents());
        range.insertNode(wrapper);
        return { collapsed: false };
    }

    function _applyMarkAcrossSelection(inst, range, markType, data, remove, title) {
        var segments = _collectRangeInlineSegments(inst, range);
        if (!segments.length) {
            return _wrapSelectionWithMark(range, markType, data, remove, title);
        }

        segments.forEach(function (segment) {
            if (!segment.inline || !segment.inline.isConnected || segment.end <= segment.start) return;
            _splitInlineForMark(segment.inline, segment.start, segment.end, markType, data, remove, title);
        });

        return { collapsed: false };
    }

    function _collectRangeInlineSegments(inst, range) {
        var root = inst && inst.root;
        if (!root || !range) return [];

        var walkerRoot = range.commonAncestorContainer.nodeType === Node.ELEMENT_NODE
            ? range.commonAncestorContainer
            : range.commonAncestorContainer.parentElement;
        if (!walkerRoot || !root.contains(walkerRoot)) {
            walkerRoot = root;
        }

        var byInline = new Map();
        var walker = document.createTreeWalker(walkerRoot, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                if (!root.contains(node) || !(node.textContent || '').length) return NodeFilter.FILTER_REJECT;
                try {
                    return range.intersectsNode(node) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                } catch {
                    return NodeFilter.FILTER_REJECT;
                }
            }
        });

        var node;
        while ((node = walker.nextNode())) {
            var element = node.parentElement;
            var block = element && element.closest('[data-block-id]');
            if (!block || !root.contains(block)) continue;
            var inline = _findSemanticInlineElement(element, block);
            if (!inline || !block.contains(inline)) continue;

            var start = 0;
            var end = _textLength(inline);
            if (inline.contains(range.startContainer)) {
                start = _textOffsetWithin(inline, range.startContainer, range.startOffset);
            }
            if (inline.contains(range.endContainer)) {
                end = _textOffsetWithin(inline, range.endContainer, range.endOffset);
            }

            var existing = byInline.get(inline);
            if (existing) {
                existing.start = Math.min(existing.start, start);
                existing.end = Math.max(existing.end, end);
            } else {
                byInline.set(inline, { inline: inline, start: start, end: end });
            }
        }

        return Array.from(byInline.values()).sort(function (a, b) {
            if (a.inline === b.inline) return 0;
            return a.inline.compareDocumentPosition(b.inline) & Node.DOCUMENT_POSITION_PRECEDING ? 1 : -1;
        });
    }

    function _textOffsetWithin(root, node, offset) {
        if (node === root && node.nodeType === Node.ELEMENT_NODE) {
            var current = 0;
            var max = Math.max(0, Math.min(offset || 0, node.childNodes.length));
            for (var i = 0; i < max; i++) {
                current += _textLength(node.childNodes[i]);
            }
            return current;
        }

        return _absoluteTextOffset(root, node, offset);
    }

    function _textLength(node) {
        if (!node) return 0;
        if (node.nodeType === Node.TEXT_NODE) return (node.textContent || '').length;
        if (_isInlineBreakNode(node)) return 1;
        var total = 0;
        for (var i = 0; i < node.childNodes.length; i++) {
            total += _textLength(node.childNodes[i]);
        }
        return total;
    }

    function _splitInlineForMark(inline, start, end, markType, data, remove, title) {
        var text = _serializeInlineText(inline);
        var beforeText = text.slice(0, start);
        var markedText = text.slice(start, end);
        var afterText = text.slice(end);
        var parent = inline.parentNode;
        if (!parent || !markedText) return;

        var fragment = document.createDocumentFragment();
        var beforeInline = null;
        var markedInline = inline.cloneNode(false);
        var afterInline = null;

        if (beforeText) {
            beforeInline = inline.cloneNode(false);
            beforeInline.setAttribute('data-inline-id', _createInlineId());
            _renderTextRunContent(beforeInline, beforeText);
            fragment.appendChild(beforeInline);
        }

        markedInline.setAttribute('data-inline-id', inline.getAttribute('data-inline-id') || _createInlineId());
        _renderTextRunContent(markedInline, markedText);
        if (remove) {
            _removeMarkStyle(markedInline, markType);
        } else {
            _applyMarkStyle(markedInline, markType, markType === 'Link' ? { href: data || '', title: title || '' } : _isValueMark(markType) ? { value: data || '' } : {});
        }
        fragment.appendChild(markedInline);

        if (afterText) {
            afterInline = inline.cloneNode(false);
            afterInline.setAttribute('data-inline-id', _createInlineId());
            _renderTextRunContent(afterInline, afterText);
            fragment.appendChild(afterInline);
        }

        parent.replaceChild(fragment, inline);
        var selectionNode = _firstDeepTextNode(markedInline);
        if (selectionNode) {
            var sel = window.getSelection();
            var range = document.createRange();
            range.setStart(selectionNode, 0);
            range.setEnd(selectionNode, selectionNode.textContent.length);
            sel.removeAllRanges();
            sel.addRange(range);
        }

        return markedInline;
    }

    function _splitInlineForClearFormatting(inline, start, end) {
        var text = _serializeInlineText(inline);
        var beforeText = text.slice(0, start);
        var clearedText = text.slice(start, end);
        var afterText = text.slice(end);
        var parent = inline.parentNode;
        if (!parent || !clearedText) return null;

        var fragment = document.createDocumentFragment();
        var clearedInline = inline.cloneNode(false);

        if (beforeText) {
            var beforeInline = inline.cloneNode(false);
            beforeInline.setAttribute('data-inline-id', _createInlineId());
            _renderTextRunContent(beforeInline, beforeText);
            fragment.appendChild(beforeInline);
        }

        clearedInline.setAttribute('data-inline-id', inline.getAttribute('data-inline-id') || _createInlineId());
        _renderTextRunContent(clearedInline, clearedText);
        _removeAllFormattingStyles(clearedInline);
        fragment.appendChild(clearedInline);

        if (afterText) {
            var afterInline = inline.cloneNode(false);
            afterInline.setAttribute('data-inline-id', _createInlineId());
            _renderTextRunContent(afterInline, afterText);
            fragment.appendChild(afterInline);
        }

        parent.replaceChild(fragment, inline);
        return clearedInline;
    }

    function _rangeHasDomMark(inline, start, end, markType) {
        var state = _getElementMarkState(inline);
        if (state[markType]) return true;
        var pos = _resolveTextPosition(inline, Math.max(start, 0));
        var el = pos && pos.node && pos.node.parentElement;
        return !!(el && _getElementMarkState(el)[markType]);
    }

    function _normalizeMarkType(markType) {
        var value = String(markType || '').toLowerCase();
        if (value === 'bold' || value === '0') return 'Bold';
        if (value === 'italic' || value === '1') return 'Italic';
        if (value === 'underline' || value === '2') return 'Underline';
        if (value === 'strikethrough' || value === 'strike' || value === '3') return 'Strikethrough';
        if (value === 'superscript' || value === '4') return 'Superscript';
        if (value === 'subscript' || value === '5') return 'Subscript';
        if (value === 'link' || value === '6') return 'Link';
        if (value === 'highlight' || value === '9') return 'Highlight';
        if (value === 'textcolor' || value === 'text-color' || value === 'fontcolor' || value === 'font-color' || value === '10') return 'TextColor';
        if (value === 'fontfamily' || value === 'font-family' || value === '11') return 'FontFamily';
        if (value === 'fontsize' || value === 'font-size' || value === '12') return 'FontSize';
        return null;
    }

    function _markTypeToNumber(markType) {
        switch (_normalizeMarkType(markType)) {
            case 'Bold': return 0;
            case 'Italic': return 1;
            case 'Underline': return 2;
            case 'Strikethrough': return 3;
            case 'Superscript': return 4;
            case 'Subscript': return 5;
            case 'Link': return 6;
            case 'Highlight': return 9;
            case 'TextColor': return 10;
            case 'FontFamily': return 11;
            case 'FontSize': return 12;
            default: return 0;
        }
    }

    function _isValueMark(markType) {
        var normalized = _normalizeMarkType(markType);
        return normalized === 'FontFamily'
            || normalized === 'FontSize'
            || normalized === 'TextColor'
            || normalized === 'Highlight';
    }

    function _sanitizeMarkData(markType, data, inst) {
        switch (_normalizeMarkType(markType)) {
            case 'Link': return _sanitizeLinkHref(data);
            case 'FontFamily': return _sanitizeFontFamilyValue(data, inst);
            case 'FontSize': return _sanitizeFontSizeValue(data);
            case 'TextColor':
            case 'Highlight':
                return _sanitizeColorValue(data);
            default:
                return data || '';
        }
    }

    function _sanitizeLinkHref(value) {
        var raw = String(value || '').trim();
        if (!raw) return '';
        if (raw[0] === '/' || raw[0] === '#') return raw;
        try {
            var url = new URL(raw, window.location.origin);
            var protocol = url.protocol.toLowerCase();
            return protocol === 'http:' || protocol === 'https:' || protocol === 'mailto:' || protocol === 'tel:'
                ? raw
                : '';
        } catch {
            return '';
        }
    }

    function _sanitizeFontFamilyValue(value, inst) {
        var raw = String(value || '').trim();
        if (!raw) return '';
        var fonts = (inst && inst.options && (inst.options.fontFamilies || inst.options.FontFamilies)) || [];
        var match = fonts.find(function (font) {
            var css = font.cssFamily || font.CssFamily || '';
            return css && css.toLowerCase() === raw.toLowerCase();
        });
        return match ? (match.cssFamily || match.CssFamily || '') : '';
    }

    function _sanitizeFontSizeValue(value) {
        var raw = String(value || '').trim().toLowerCase().replace('pt', '');
        var size = parseFloat(raw);
        if (!Number.isFinite(size) || size < 6 || size > 96) return '';
        return (Math.round(size * 100) / 100) + 'pt';
    }

    function _sanitizeColorValue(value) {
        var raw = String(value || '').trim();
        if (/^#[0-9a-f]{6}$/i.test(raw)) return raw;

        var match = raw.match(/^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})(?:\s*,\s*(0|1|0?\.\d+))?\s*\)$/i);
        if (!match) return '';

        var red = parseInt(match[1], 10);
        var green = parseInt(match[2], 10);
        var blue = parseInt(match[3], 10);
        if (red > 255 || green > 255 || blue > 255) return '';

        if (match[4] != null) {
            return 'rgba(' + red + ', ' + green + ', ' + blue + ', ' + match[4] + ')';
        }

        return 'rgb(' + red + ', ' + green + ', ' + blue + ')';
    }

    function _applyMarkStyle(el, markType, data) {
        switch (_normalizeMarkType(markType)) {
            case 'Bold':
                el.style.fontWeight = 'bold';
                break;
            case 'Italic':
                el.style.fontStyle = 'italic';
                break;
            case 'Underline':
                el.style.textDecoration = ((el.style.textDecoration || '') + ' underline').trim();
                break;
            case 'Link':
                el.setAttribute('data-link-href', _sanitizeLinkHref(data && data.href));
                if (data && data.title) {
                    el.setAttribute('data-link-title', String(data.title));
                    el.setAttribute('title', String(data.title));
                }
                el.style.textDecoration = ((el.style.textDecoration || '') + ' underline').trim();
                break;
            case 'Strikethrough':
                el.style.textDecoration = ((el.style.textDecoration || '') + ' line-through').trim();
                break;
            case 'Superscript':
                el.style.verticalAlign = 'super';
                el.style.fontSize = 'smaller';
                break;
            case 'Subscript':
                el.style.verticalAlign = 'sub';
                el.style.fontSize = 'smaller';
                break;
            case 'FontFamily':
                el.style.fontFamily = (data && data.value) || data || '';
                break;
            case 'FontSize':
                el.style.fontSize = (data && data.value) || data || '';
                break;
            case 'TextColor':
                el.style.color = (data && data.value) || data || '';
                break;
            case 'Highlight':
                el.style.backgroundColor = (data && data.value) || data || '';
                break;
        }
    }

    function _removeMarkStyle(el, markType) {
        switch (_normalizeMarkType(markType)) {
            case 'Bold':
                el.style.fontWeight = '';
                break;
            case 'Italic':
                el.style.fontStyle = '';
                break;
            case 'Underline':
                el.style.textDecoration = (el.style.textDecoration || '').replace('underline', '').trim();
                break;
            case 'Link':
                el.removeAttribute('data-link-href');
                el.removeAttribute('data-link-title');
                el.removeAttribute('title');
                el.removeAttribute('href');
                el.style.textDecoration = (el.style.textDecoration || '').replace('underline', '').trim();
                Array.from(el.querySelectorAll('a')).forEach(function (link) {
                    var text = document.createTextNode(link.textContent || '');
                    link.parentNode && link.parentNode.replaceChild(text, link);
                });
                break;
            case 'Strikethrough':
                el.style.textDecoration = (el.style.textDecoration || '').replace('line-through', '').trim();
                break;
            case 'Superscript':
            case 'Subscript':
                el.style.verticalAlign = '';
                el.style.fontSize = '';
                break;
            case 'FontFamily':
                el.style.fontFamily = '';
                break;
            case 'FontSize':
                el.style.fontSize = '';
                break;
            case 'TextColor':
                el.style.color = '';
                break;
            case 'Highlight':
                el.style.backgroundColor = '';
                break;
        }
    }

    function _removeAllFormattingStyles(el) {
        ['Bold', 'Italic', 'Underline', 'Strikethrough', 'Superscript', 'Subscript', 'FontFamily', 'FontSize', 'TextColor', 'Highlight', 'Link']
            .forEach(function (markType) {
                _removeMarkStyle(el, markType);
            });
    }

    function _clearInlineFormatting(el) {
        el.style.fontWeight = '';
        el.style.fontStyle = '';
        el.style.textDecoration = '';
        el.style.verticalAlign = '';
        el.style.fontSize = '';
        el.style.fontFamily = '';
        el.style.color = '';
        el.style.backgroundColor = '';
    }

    function _normalizeColorForState(value) {
        if (!value) return '';
        var raw = String(value).trim();
        if (!raw || raw === 'transparent' || raw === 'rgba(0, 0, 0, 0)') return '';
        if (/^#[0-9a-f]{6}$/i.test(raw)) return raw.toLowerCase();
        if (/^#[0-9a-f]{3}$/i.test(raw)) {
            return '#' + raw.slice(1).split('').map(function (part) { return part + part; }).join('').toLowerCase();
        }

        var match = raw.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([.\d]+))?\)$/i);
        if (!match || match[4] === '0') return raw;
        return '#' + [match[1], match[2], match[3]].map(function (part) {
            return Math.max(0, Math.min(255, parseInt(part, 10))).toString(16).padStart(2, '0');
        }).join('');
    }

    function _readNearestInlineStyleValue(el, styleName) {
        var node = el && el.nodeType === Node.ELEMENT_NODE ? el : el?.parentElement;
        while (node && node.nodeType === Node.ELEMENT_NODE) {
            if (node.style && node.style[styleName]) {
                return node.style[styleName];
            }

            if (node.matches && node.matches('.tm-wysiwyg-block, .tm-wysiwyg-page__body')) {
                break;
            }

            node = node.parentElement;
        }

        return '';
    }

    function _hasNearestInlineClass(el, className) {
        var node = el && el.nodeType === Node.ELEMENT_NODE ? el : el?.parentElement;
        while (node && node.nodeType === Node.ELEMENT_NODE) {
            if (node.classList && node.classList.contains(className)) {
                return true;
            }

            if (node.matches && node.matches('.tm-wysiwyg-block, .tm-wysiwyg-page__body')) {
                break;
            }

            node = node.parentElement;
        }

        return false;
    }

    function _readNearestInlineBackground(el) {
        var node = el && el.nodeType === Node.ELEMENT_NODE ? el : el?.parentElement;
        while (node && node.nodeType === Node.ELEMENT_NODE) {
            var value = node.style && node.style.backgroundColor;
            if (value && _normalizeColorForState(value)) {
                return value;
            }

            if (node.matches && node.matches('.tm-wysiwyg-block, .tm-wysiwyg-page__body')) {
                break;
            }

            node = node.parentElement;
        }

        return '';
    }

    function _getElementMarkState(el) {
        var fontWeight = _readNearestInlineStyleValue(el, 'fontWeight') || '';
        var fontStyle = _readNearestInlineStyleValue(el, 'fontStyle') || '';
        var textDecoration = (_readNearestInlineStyleValue(el, 'textDecorationLine') || '')
            + ' ' + (_readNearestInlineStyleValue(el, 'textDecoration') || '');
        var textColor = _readNearestInlineStyleValue(el, 'color') || '';
        var highlight = _readNearestInlineBackground(el);
        var weightValue = parseInt(fontWeight, 10);
        return {
            Bold: _hasNearestInlineClass(el, 'tm-document-inline--bold')
                || fontWeight === 'bold'
                || fontWeight === '700'
                || weightValue >= 700,
            Italic: _hasNearestInlineClass(el, 'tm-document-inline--italic') || fontStyle === 'italic',
            Underline: _hasNearestInlineClass(el, 'tm-document-inline--underline') || textDecoration.indexOf('underline') >= 0,
            Strikethrough: _hasNearestInlineClass(el, 'tm-document-inline--strikethrough') || textDecoration.indexOf('line-through') >= 0,
            Link: !!el.closest('a[href], [data-link-href]'),
            FontFamily: _readNearestInlineStyleValue(el, 'fontFamily') || '',
            FontSize: _readNearestInlineStyleValue(el, 'fontSize') || '',
            TextColor: _normalizeColorForState(textColor),
            Highlight: _normalizeColorForState(highlight)
        };
    }

    function _getFormattingState(inst) {
        var selection = _captureSelectionSnapshot(inst) || _createSelectionSnapshotFromRuntimeSelection(inst.runtimeSelection);
        var paragraphState = _getSelectionParagraphState(inst, selection);
        var inlineState = _getSelectionInlineFormattingState(inst);
        return {
            Bold: _getSelectionMarkState(inst, 'Bold'),
            Italic: _getSelectionMarkState(inst, 'Italic'),
            Underline: _getSelectionMarkState(inst, 'Underline'),
            Strikethrough: _getSelectionMarkState(inst, 'Strikethrough'),
            ParagraphAlignment: paragraphState.alignment,
            ParagraphAlignmentMixed: paragraphState.alignmentMixed,
            FontFamily: inlineState.fontFamily.value,
            FontFamilyMixed: inlineState.fontFamily.mixed,
            FontSize: inlineState.fontSize.value,
            FontSizeMixed: inlineState.fontSize.mixed,
            TextColor: inlineState.textColor.value,
            TextColorMixed: inlineState.textColor.mixed,
            HighlightColor: inlineState.highlightColor.value,
            HighlightColorMixed: inlineState.highlightColor.mixed,
            LineSpacing: paragraphState.lineSpacing,
            LineSpacingMixed: paragraphState.lineSpacingMixed,
            SpacingBefore: paragraphState.spacingBefore,
            SpacingBeforeMixed: paragraphState.spacingBeforeMixed,
            SpacingAfter: paragraphState.spacingAfter,
            SpacingAfterMixed: paragraphState.spacingAfterMixed,
            LeftIndent: paragraphState.leftIndent,
            LeftIndentMixed: paragraphState.leftIndentMixed,
            IsBulletList: paragraphState.isBulletList,
            IsNumberedList: paragraphState.isNumberedList,
            ListMixed: paragraphState.listMixed,
            ActiveRegion: selection ? (selection.region || selection.Region || 'Body') : 'Body',
            CurrentSelection: _toPascalSelection(selection)
        };
    }

    function _getSelectionInlineFormattingState(inst) {
        var elements = _collectSelectionInlineElements(inst);
        return {
            fontFamily: _getMixedInlineValue(elements, function (state) { return state.FontFamily || ''; }),
            fontSize: _getMixedInlineValue(elements, function (state) { return state.FontSize || ''; }),
            textColor: _getMixedInlineValue(elements, function (state) { return state.TextColor || ''; }),
            highlightColor: _getMixedInlineValue(elements, function (state) { return state.Highlight || ''; })
        };
    }

    function _getMixedInlineValue(elements, read) {
        if (!elements || elements.length === 0) {
            return { value: '', mixed: false };
        }

        var first = read(_getElementMarkState(elements[0])) || '';
        var mixed = false;
        for (var i = 1; i < elements.length; i++) {
            var value = read(_getElementMarkState(elements[i])) || '';
            if (value !== first) {
                mixed = true;
                break;
            }
        }
        return { value: first, mixed: mixed };
    }

    function _collectSelectionInlineElements(inst) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || !inst.root.contains(sel.anchorNode)) {
            return [];
        }

        if (sel.isCollapsed) {
            var collapsedEl = sel.anchorNode.nodeType === Node.ELEMENT_NODE ? sel.anchorNode : sel.anchorNode.parentElement;
            return collapsedEl ? [collapsedEl] : [];
        }

        var range = sel.getRangeAt(0);
        var seen = new Set();
        var result = [];
        var walkerRoot = range.commonAncestorContainer.nodeType === Node.ELEMENT_NODE
            ? range.commonAncestorContainer
            : range.commonAncestorContainer.parentElement;
        var walker = document.createTreeWalker(walkerRoot || inst.root, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                if (!inst.root.contains(node)) return NodeFilter.FILTER_REJECT;
                try {
                    return _getSelectedTextLengthInTextNode(range, node) > 0 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                } catch {
                    return NodeFilter.FILTER_REJECT;
                }
            }
        });

        while (walker.nextNode()) {
            var element = walker.currentNode.parentElement;
            if (!element) continue;
            try {
                if (_getSelectedTextLengthInTextNode(range, walker.currentNode) > 0 && !seen.has(element)) {
                    seen.add(element);
                    result.push(element);
                }
            } catch {
                // Ignore detached or browser-internal nodes.
            }
        }
        return result;
    }

    function _getSelectedTextLengthInTextNode(range, node) {
        if (!range || !node || node.nodeType !== Node.TEXT_NODE || !(node.textContent || '').length) {
            return 0;
        }

        try {
            if (!range.intersectsNode(node)) {
                return 0;
            }

            var nodeRange = document.createRange();
            nodeRange.selectNodeContents(node);
            var intersection = range.cloneRange();
            if (intersection.compareBoundaryPoints(Range.START_TO_START, nodeRange) < 0) {
                intersection.setStart(nodeRange.startContainer, nodeRange.startOffset);
            }

            if (intersection.compareBoundaryPoints(Range.END_TO_END, nodeRange) > 0) {
                intersection.setEnd(nodeRange.endContainer, nodeRange.endOffset);
            }

            return intersection.toString().length;
        } catch {
            return 0;
        }
    }

    function _getSelectionParagraphState(inst, selection) {
        var blocks = _collectSelectionBlocks(inst, selection);
        if (blocks.length === 0) {
            return {
                alignment: 0,
                alignmentMixed: false,
                lineSpacing: 1,
                lineSpacingMixed: false,
                spacingBefore: 0,
                spacingBeforeMixed: false,
                spacingAfter: 0,
                spacingAfterMixed: false,
                leftIndent: 0,
                leftIndentMixed: false,
                isBulletList: false,
                isNumberedList: false,
                listMixed: false
            };
        }

        var firstAlignment = _readBlockAlignment(blocks[0]);
        var firstLineSpacing = _readBlockLineSpacing(blocks[0]);
        var firstSpacingBefore = _readBlockSpacingBefore(blocks[0]);
        var firstSpacingAfter = _readBlockSpacingAfter(blocks[0]);
        var firstLeftIndent = _readBlockLeftIndent(blocks[0]);
        var firstListState = _readBlockListState(blocks[0]);
        var alignmentMixed = false;
        var lineSpacingMixed = false;
        var spacingBeforeMixed = false;
        var spacingAfterMixed = false;
        var leftIndentMixed = false;
        var listMixed = false;
        for (var i = 1; i < blocks.length; i++) {
            if (_readBlockAlignment(blocks[i]) !== firstAlignment) alignmentMixed = true;
            if (_readBlockLineSpacing(blocks[i]) !== firstLineSpacing) lineSpacingMixed = true;
            if (_readBlockSpacingBefore(blocks[i]) !== firstSpacingBefore) spacingBeforeMixed = true;
            if (_readBlockSpacingAfter(blocks[i]) !== firstSpacingAfter) spacingAfterMixed = true;
            if (_readBlockLeftIndent(blocks[i]) !== firstLeftIndent) leftIndentMixed = true;
            if (_readBlockListState(blocks[i]) !== firstListState) listMixed = true;
        }

        return {
            alignment: firstAlignment,
            alignmentMixed: alignmentMixed,
            lineSpacing: firstLineSpacing,
            lineSpacingMixed: lineSpacingMixed,
            spacingBefore: firstSpacingBefore,
            spacingBeforeMixed: spacingBeforeMixed,
            spacingAfter: firstSpacingAfter,
            spacingAfterMixed: spacingAfterMixed,
            leftIndent: firstLeftIndent,
            leftIndentMixed: leftIndentMixed,
            isBulletList: firstListState === 'bullet',
            isNumberedList: firstListState === 'numbered',
            listMixed: listMixed
        };
    }

    function _collectSelectionBlocks(inst, selection) {
        if (!inst || !inst.root) return [];
        if (!selection) {
            var first = inst.root.querySelector('[data-block-id]');
            return first ? [first] : [];
        }

        var anchorBlockId = selection.anchorBlockId || selection.AnchorBlockId || '';
        var focusBlockId = selection.focusBlockId || selection.FocusBlockId || anchorBlockId;
        var blocks = Array.from(inst.root.querySelectorAll('[data-block-id]'));
        if (!anchorBlockId) return blocks.length > 0 ? [blocks[0]] : [];
        var start = blocks.findIndex(function (block) { return block.getAttribute('data-block-id') === anchorBlockId; });
        var end = blocks.findIndex(function (block) { return block.getAttribute('data-block-id') === focusBlockId; });
        if (start < 0) return blocks.length > 0 ? [blocks[0]] : [];
        if (end < 0) end = start;
        var low = Math.min(start, end);
        var high = Math.max(start, end);
        return blocks.slice(low, high + 1);
    }

    function _readBlockAlignment(block) {
        if (!block) return 0;
        var style = block.style || {};
        var computed = window.getComputedStyle ? window.getComputedStyle(block) : style;
        return _cssAlignmentToNumber(style.textAlign || computed.textAlign || 'left');
    }

    function _readBlockLineSpacing(block) {
        if (!block) return 1;
        var style = block.style || {};
        var computed = window.getComputedStyle ? window.getComputedStyle(block) : style;
        return _sanitizeLineSpacing(style.lineHeight || computed.lineHeight || 1);
    }

    function _readBlockSpacingBefore(block) {
        if (!block) return 0;
        var style = block.style || {};
        var computed = window.getComputedStyle ? window.getComputedStyle(block) : style;
        return _sanitizeParagraphPoints(_cssLengthToPoints(style.marginTop || computed.marginTop || 0), 0, 144);
    }

    function _readBlockSpacingAfter(block) {
        if (!block) return 0;
        var style = block.style || {};
        var computed = window.getComputedStyle ? window.getComputedStyle(block) : style;
        return _sanitizeParagraphPoints(_cssLengthToPoints(style.marginBottom || computed.marginBottom || 0), 0, 144);
    }

    function _readBlockLeftIndent(block) {
        if (!block) return 0;
        var style = block.style || {};
        var computed = window.getComputedStyle ? window.getComputedStyle(block) : style;
        return _sanitizeParagraphPoints(_cssLengthToPoints(style.marginLeft || computed.marginLeft || 0), 0, 432);
    }

    function _readBlockListState(block) {
        if (!block) return 'none';
        var tag = block.tagName ? block.tagName.toLowerCase() : '';
        if (tag === 'ol') return 'numbered';
        if (tag === 'ul') return 'bullet';
        return 'none';
    }

    function getLinkInfo(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return null;

        _ensureEditorSelection(inst);
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || !inst.root.contains(sel.anchorNode)) {
            return null;
        }

        var node = sel.anchorNode;
        var el = node && node.nodeType === Node.ELEMENT_NODE ? node : node && node.parentElement;
        var linkEl = el && el.closest('a[href], [data-link-href]');
        if (!linkEl && !sel.isCollapsed) {
            var fragment = sel.getRangeAt(0).cloneContents();
            linkEl = fragment.querySelector && fragment.querySelector('a[href], [data-link-href]');
        }

        if (!linkEl) {
            return null;
        }

        var href = _sanitizeLinkHref(linkEl.getAttribute('data-link-href') || linkEl.getAttribute('href') || '');
        if (!href) {
            return null;
        }

        return {
            Href: href,
            Title: linkEl.getAttribute('data-link-title') || linkEl.getAttribute('title') || null
        };
    }

    function _getSelectionMarkState(inst, markType) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || !inst.root.contains(sel.anchorNode)) {
            return 0;
        }

        if (sel.isCollapsed) {
            var el = sel.anchorNode.nodeType === Node.ELEMENT_NODE ? sel.anchorNode : sel.anchorNode.parentElement;
            if (inst.pendingTypingMarks && inst.pendingTypingMarks[markType]) {
                return 1;
            }
            return el && _getElementMarkState(el)[markType] ? 1 : 0;
        }

        var range = sel.getRangeAt(0);
        var textNodes = [];
        var walker = document.createTreeWalker(range.commonAncestorContainer, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                if (!inst.root.contains(node)) return NodeFilter.FILTER_REJECT;
                try {
                    return _getSelectedTextLengthInTextNode(range, node) > 0 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                } catch {
                    return NodeFilter.FILTER_REJECT;
                }
            }
        });
        var node;
        while ((node = walker.nextNode())) {
            if ((node.textContent || '').length > 0) {
                textNodes.push(node);
            }
        }

        if (textNodes.length === 0 && range.commonAncestorContainer.nodeType === Node.TEXT_NODE) {
            textNodes.push(range.commonAncestorContainer);
        }

        var any = false;
        var all = textNodes.length > 0;
        textNodes.forEach(function (textNode) {
            var active = _getElementMarkState(textNode.parentElement)[markType];
            any = any || active;
            all = all && active;
        });

        if (any && all) return 1;
        if (any) return 2;
        return 0;
    }

    function _setSelectedImageWrapMode(inst, payload) {
        payload = payload || {};
        var blockId = payload.blockId || payload.BlockId || payload.imageId || payload.ImageId || '';
        var figure = blockId ? _getImageFigureByBlockId(inst, blockId) : _getSelectedImageFigure(inst);
        if (!figure) {
            _debugImage(inst, 'command.wrap.no-figure', {
                payload: payload,
                figures: _debugImageFigures(inst)
            });
            return;
        }
        _commitCurrentRuntimeTransaction(inst, false);
        var beforeSel = _captureSelectionSnapshot(inst);
        _beginUndoTransaction(inst, 'image', 'Set image wrap', beforeSel, true);
        var wrapMode = _normalizeWrapMode(payload.wrapMode ?? payload.WrapMode ?? 'Square');
        var layout = _serializeImage(figure).FloatingLayout || {
            Inline: false,
            HorizontalRelativeTo: 0,
            VerticalRelativeTo: 3,
            X: parseFloat(figure.getAttribute('data-image-x') || '0') || 0,
            Y: parseFloat(figure.getAttribute('data-image-y') || '0') || 0,
            ZIndex: 0,
            LockAnchor: false
        };
        layout.Inline = false;
        layout.WrapMode = wrapMode.value;
        var hPos = _normalizeHorizontalPosition(layout.horizontalPosition ?? layout.HorizontalPosition);
        if (wrapMode.value === 1 && (!hPos || hPos.css === 'center')) {
            layout.HorizontalPosition = 0;
        }
        _debugImage(inst, 'command.wrap.apply', {
            figure: _debugElementLabel(figure),
            payload: payload,
            wrapMode: wrapMode.css,
            wrapModeValue: wrapMode.value,
            layout: layout
        });
        var imageSelection = _createImageSelectionSnapshot(figure);
        inst.lastSelectionSnapshot = imageSelection;
        inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(imageSelection);
        _applyFloatingImageLayout(figure, { FloatingLayout: layout }, inst);
        _ensureImageResizeHandle(figure, inst);
        _ensureWrappedImageSideTextBlock(inst, figure, true, imageSelection);
        _selectImageFigure(inst, figure);
        _dispatchImageUpdatePatch(inst, figure);
        _commitCurrentRuntimeTransaction(inst, false);
    }

    function _setSelectedImagePosition(inst, payload) {
        payload = payload || {};
        var blockId = payload.blockId || payload.BlockId || payload.imageId || payload.ImageId || '';
        var figure = blockId ? _getImageFigureByBlockId(inst, blockId) : _getSelectedImageFigure(inst);
        if (!figure) {
            _debugImage(inst, 'command.position.no-figure', {
                payload: payload,
                figures: _debugImageFigures(inst)
            });
            return;
        }
        var hPos = _normalizeHorizontalPosition(payload.horizontalPosition ?? payload.HorizontalPosition);
        if (!hPos) {
            _debugImage(inst, 'command.position.invalid-position', {
                figure: _debugElementLabel(figure),
                payload: payload
            });
            return;
        }
        _commitCurrentRuntimeTransaction(inst, false);
        var beforeSel = _captureSelectionSnapshot(inst);
        _beginUndoTransaction(inst, 'image', 'Set image position', beforeSel, true);
        var layout = _serializeImage(figure).FloatingLayout || {
            Inline: false,
            HorizontalRelativeTo: 0,
            VerticalRelativeTo: 3,
            X: parseFloat(figure.getAttribute('data-image-x') || '0') || 0,
            Y: parseFloat(figure.getAttribute('data-image-y') || '0') || 0,
            ZIndex: 0,
            LockAnchor: false
        };
        layout.Inline = false;
        layout.HorizontalPosition = hPos.value;
        var currentWrap = _normalizeWrapMode(layout.wrapMode ?? layout.WrapMode);
        if ((hPos.css === 'left' || hPos.css === 'right') && (currentWrap.value === 0 || currentWrap.value === 4)) {
            layout.WrapMode = 1;
        }
        _debugImage(inst, 'command.position.apply', {
            figure: _debugElementLabel(figure),
            payload: payload,
            horizontalPosition: hPos.css,
            layout: layout
        });
        var imageSelection = _createImageSelectionSnapshot(figure);
        inst.lastSelectionSnapshot = imageSelection;
        inst.runtimeSelection = _createRuntimeSelectionFromSnapshot(imageSelection);
        _applyFloatingImageLayout(figure, { FloatingLayout: layout }, inst);
        _ensureImageResizeHandle(figure, inst);
        _ensureWrappedImageSideTextBlock(inst, figure, true, imageSelection);
        _selectImageFigure(inst, figure);
        _dispatchImageUpdatePatch(inst, figure);
        _commitCurrentRuntimeTransaction(inst, false);
    }

    function _setSelectedImageSize(inst, payload) {
        payload = payload || {};
        var blockId = payload.blockId || payload.BlockId || payload.imageId || payload.ImageId || '';
        var figure = blockId ? _getImageFigureByBlockId(inst, blockId) : _getSelectedImageFigure(inst);
        if (!figure) return;
        var img = figure.querySelector('img');
        if (!img) return;
        var width = payload.width ?? payload.Width;
        var height = payload.height ?? payload.Height;
        var lockAspectRatio = (payload.lockAspectRatio ?? payload.LockAspectRatio) !== false;
        var nextWidth = parseFloat(width);
        var nextHeight = parseFloat(height);
        if (!(nextWidth > 0) && !(nextHeight > 0)) return;

        if (lockAspectRatio && nextWidth > 0 && !(nextHeight > 0)) {
            var naturalW = parseFloat(figure.getAttribute('data-image-natural-width') || img.naturalWidth || '0');
            var naturalH = parseFloat(figure.getAttribute('data-image-natural-height') || img.naturalHeight || '0');
            if (naturalW > 0 && naturalH > 0) nextHeight = Math.round(nextWidth * naturalH / naturalW);
        } else if (lockAspectRatio && nextHeight > 0 && !(nextWidth > 0)) {
            var nW = parseFloat(figure.getAttribute('data-image-natural-width') || img.naturalWidth || '0');
            var nH = parseFloat(figure.getAttribute('data-image-natural-height') || img.naturalHeight || '0');
            if (nW > 0 && nH > 0) nextWidth = Math.round(nextHeight * nW / nH);
        }

        _commitCurrentRuntimeTransaction(inst, false);
        var beforeSel = _captureSelectionSnapshot(inst);
        _beginUndoTransaction(inst, 'image', 'Resize image', beforeSel, true);
        if (nextWidth > 0) img.style.width = Math.round(nextWidth) + 'px';
        if (nextHeight > 0) img.style.height = Math.round(nextHeight) + 'px';
        figure.setAttribute('data-lock-aspect-ratio', lockAspectRatio ? 'true' : 'false');
        _ensureImageResizeHandle(figure, inst);
        _dispatchImageUpdatePatch(inst, figure);
        _commitCurrentRuntimeTransaction(inst, false);
    }

    function _getSelectedImageFigure(inst) {
        if (inst && inst.selectedImageFigure && inst.selectedImageFigure.isConnected && inst.root.contains(inst.selectedImageFigure)) {
            return inst.selectedImageFigure;
        }

        var activeBlockId = inst && inst.lastSelectionSnapshot
            ? (inst.lastSelectionSnapshot.activeImageBlockId || inst.lastSelectionSnapshot.ActiveImageBlockId || inst.lastSelectionSnapshot.anchorBlockId || inst.lastSelectionSnapshot.AnchorBlockId)
            : null;
        var activeFigure = activeBlockId ? _getImageFigureByBlockId(inst, activeBlockId) : null;
        if (activeFigure) {
            inst.selectedImageFigure = activeFigure;
            activeFigure.classList.add('tm-wysiwyg-image--selected');
            activeFigure.setAttribute('aria-selected', 'true');
            return activeFigure;
        }

        var sel = window.getSelection();
        if (sel && sel.rangeCount > 0) {
            var node = sel.anchorNode;
            var el = node && (node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement);
            var selected = el && el.closest && el.closest('figure.tm-wysiwyg-image');
            if (selected && inst.root.contains(selected)) return selected;
        }

        return null;
    }

    function _getImageFigureByBlockId(inst, blockId) {
        if (!inst || !inst.root || !blockId) return null;
        var safeId = String(blockId).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
        var block = inst.root.querySelector('[data-block-id="' + safeId + '"]');
        if (!block) return null;
        return block.matches('figure.tm-wysiwyg-image')
            ? block
            : block.querySelector('figure.tm-wysiwyg-image');
    }

    /**
     * Captures the current text selection as a DocumentCommentAnchor.
     * Returns null if there is no selection, selection is collapsed,
     * or anchor/focus span multiple blocks.
     * @param {string} instanceId
     * @returns {Object|null}
     */
    function captureCommentAnchor(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || inst.readOnly) return null;
        _flushPendingInputPatch(inst);
        _flushSelectionNotification(inst);

        const sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || sel.isCollapsed) return null;

        const anchor = _mapNodeToBlockInline(sel.anchorNode, sel.anchorOffset, inst.root);
        const focus = _mapNodeToBlockInline(sel.focusNode, sel.focusOffset, inst.root);
        if (!anchor || !focus) return null;

        // Only single-block ranges are supported for now.
        if (anchor.blockId !== focus.blockId) return null;

        const blockEl = inst.root.querySelector('[data-block-id="' + anchor.blockId + '"]');
        if (!blockEl) return null;

        const allInlines = Array.from(blockEl.querySelectorAll('[data-inline-id]'));
        const anchorInlineIndex = allInlines.findIndex(function (el) {
            return el.getAttribute('data-inline-id') === anchor.inlineId;
        });
        const focusInlineIndex = allInlines.findIndex(function (el) {
            return el.getAttribute('data-inline-id') === focus.inlineId;
        });

        const direction = _computeSelectionDirection(sel);
        const start = direction === 'forward' ? anchor : focus;
        const end = direction === 'forward' ? focus : anchor;
        const startInlineIndex = direction === 'forward' ? anchorInlineIndex : focusInlineIndex;
        const endInlineIndex = direction === 'forward' ? focusInlineIndex : anchorInlineIndex;

        return {
            type: 1, // TextRange
            blockId: anchor.blockId,
            startInlineIndex: Math.max(0, startInlineIndex),
            startOffset: start.blockOffset ?? start.offset,
            endInlineIndex: Math.max(0, endInlineIndex),
            endOffset: end.blockOffset ?? end.offset
        };
    }

    function _serializeSelectionForClipboard(inst) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || sel.isCollapsed) return null;
        if (!inst.root.contains(sel.anchorNode) || !inst.root.contains(sel.focusNode)) return null;

        var range = sel.getRangeAt(0);
        var container = document.createElement('div');
        container.appendChild(range.cloneContents());

        var plain = sel.toString();
        var html = _sanitizeClipboardHtml(container);
        if (!html || !html.trim()) {
            html = '<p>' + _escapeHtml(plain) + '</p>';
        } else if (!/<(p|h[1-6]|ul|ol|blockquote|table|figure|hr)\b/i.test(html)) {
            html = '<p>' + html + '</p>';
        }

        return {
            html: html,
            plain: plain
        };
    }

    function _sanitizeClipboardHtml(container) {
        var output = [];
        Array.from(container.childNodes).forEach(function (node) {
            var sanitized = _sanitizeClipboardNode(node);
            if (sanitized) output.push(sanitized);
        });
        return output.join('');
    }

    function _sanitizeClipboardNode(node) {
        if (node.nodeType === Node.TEXT_NODE) {
            return _escapeHtml(node.textContent || '');
        }
        if (node.nodeType !== Node.ELEMENT_NODE) {
            return '';
        }

        var el = node;
        var tag = el.tagName.toLowerCase();
        var children = Array.from(el.childNodes).map(_sanitizeClipboardNode).join('');
        if (tag === 'span' && (el.className || '').indexOf('tm-wysiwyg-field') >= 0) {
            return '<span class="tm-wysiwyg-field"'
                + ' data-inline-id="' + _escapeHtmlAttribute(el.getAttribute('data-inline-id') || '') + '"'
                + ' data-field-type="' + _escapeHtmlAttribute(el.getAttribute('data-field-type') || '0') + '"'
                + ' data-field-format="' + _escapeHtmlAttribute(el.getAttribute('data-field-format') || '') + '"'
                + ' data-field-fallback="' + _escapeHtmlAttribute(el.getAttribute('data-field-fallback') || '') + '">'
                + children
                + '</span>';
        }
        if (tag === 'span' && el.style.fontWeight && (el.style.fontWeight === 'bold' || parseInt(el.style.fontWeight, 10) >= 700)) {
            return '<strong>' + children + '</strong>';
        }
        if (tag === 'span' && el.style.fontStyle === 'italic') {
            return '<em>' + children + '</em>';
        }
        if (tag === 'span') {
            return children;
        }
        if (tag === 'b') tag = 'strong';
        if (tag === 'i') tag = 'em';

        var allowed = ['p', 'strong', 'em', 'u', 's', 'sup', 'sub', 'a', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'ul', 'ol', 'li', 'blockquote', 'table', 'tbody', 'thead', 'tr', 'td', 'th', 'figure', 'figcaption', 'img', 'br', 'hr'];
        if (allowed.indexOf(tag) < 0) {
            return children;
        }

        if (tag === 'br' || tag === 'hr') {
            return '<' + tag + '>';
        }

        var attrs = '';
        if (tag === 'a') {
            var href = el.getAttribute('href') || '';
            if (/^(https?:|mailto:)/i.test(href)) attrs += ' href="' + _escapeHtmlAttribute(href) + '"';
        } else if (tag === 'td' || tag === 'th') {
            var colspan = parseInt(el.getAttribute('colspan') || '1', 10);
            var rowspan = parseInt(el.getAttribute('rowspan') || '1', 10);
            if (colspan > 1) attrs += ' colspan="' + colspan + '"';
            if (rowspan > 1) attrs += ' rowspan="' + rowspan + '"';
        } else if (tag === 'img') {
            var src = el.getAttribute('src') || '';
            if (!_isSafeImageUrl(src)) return '';
            attrs += ' src="' + _escapeHtmlAttribute(src) + '"';
            attrs += ' alt="' + _escapeHtmlAttribute(el.getAttribute('alt') || '') + '"';
        }

        return '<' + tag + attrs + '>' + children + '</' + tag + '>';
    }

    function _writeClipboardPayload(payload) {
        if (!payload || !navigator.clipboard) return Promise.resolve(false);
        if (window.ClipboardItem && navigator.clipboard.write) {
            var item = new ClipboardItem({
                'text/html': new Blob([payload.html], { type: 'text/html' }),
                'text/plain': new Blob([payload.plain], { type: 'text/plain' })
            });
            return navigator.clipboard.write([item]).then(function () { return true; }).catch(function () { return false; });
        }

        if (navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(payload.plain).then(function () { return true; }).catch(function () { return false; });
        }

        return Promise.resolve(false);
    }

    function _escapeHtml(value) {
        return String(value || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function _escapeHtmlAttribute(value) {
        return _escapeHtml(value).replace(/"/g, '&quot;');
    }

    // ── DotNet interop helpers ───────────────────────────────────────────────

    function _invokeDotNet(inst, methodName, arg) {
        if (!inst.dotNetRef) return;
        try {
            if (arg !== undefined) {
                inst.dotNetRef.invokeMethodAsync(methodName, arg).catch(function (err) {
                    console.error('tmDocumentWysiwyg.invokeMethodAsync failed:', methodName, err);
                });
            } else {
                inst.dotNetRef.invokeMethodAsync(methodName).catch(function (err) {
                    console.error('tmDocumentWysiwyg.invokeMethodAsync failed:', methodName, err);
                });
            }
        } catch (err) {
            console.error('tmDocumentWysiwyg.invokeMethodAsync exception:', methodName, err);
        }
    }

    function _invokeDotNetResult(inst, methodName, arg) {
        if (!inst.dotNetRef) return Promise.resolve(null);
        try {
            if (arg !== undefined) {
                return inst.dotNetRef.invokeMethodAsync(methodName, arg).catch(function (err) {
                    console.error('tmDocumentWysiwyg.invokeMethodAsync failed:', methodName, err);
                    return null;
                });
            }

            return inst.dotNetRef.invokeMethodAsync(methodName).catch(function (err) {
                console.error('tmDocumentWysiwyg.invokeMethodAsync failed:', methodName, err);
                return null;
            });
        } catch (err) {
            console.error('tmDocumentWysiwyg.invokeMethodAsync exception:', methodName, err);
            return Promise.resolve(null);
        }
    }

    function _notifyReady(inst) {
        _invokeDotNet(inst, 'HandleJsEngineReady', { instanceId: inst.id, protocolVersion: 1 });
    }

    // ── Snapshot serialization (DOM → DocumentEditorDocument) ────────────────

    /**
     * Serializes the current DOM state back into a DocumentEditorDocument snapshot.
     * @param {string} instanceId
     * @returns {string|null} JSON string of WysiwygDocumentSnapshot or null.
     */
    function getSnapshot(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst || !inst.snapshot) return null;
        _flushPendingInputPatch(inst);
        _flushSelectionNotification(inst);

        var baseDoc = inst.snapshot.document || inst.snapshot.Document || {};
        _syncVirtualPagesFromDom(inst);
        var blocks = _serializeAllBodyBlocks(inst, baseDoc);

        // Phase 12: serialize header/footer blocks from region containers.
        var headersFooters = _serializeHeaderFooterRegions(inst, baseDoc);

        var doc = {
            SchemaVersion: baseDoc.schemaVersion || baseDoc.SchemaVersion || 1,
            DocumentId: baseDoc.documentId || baseDoc.DocumentId || '',
            Metadata: baseDoc.metadata || baseDoc.Metadata || {},
            PageSettings: baseDoc.pageSettings || baseDoc.PageSettings || {},
            Sections: baseDoc.sections || baseDoc.Sections || [],
            Blocks: blocks,
            Comments: (inst.runtimeComments && inst.runtimeComments.length > 0)
                ? inst.runtimeComments.map(function (comment) { return _cloneRuntimeJson(comment); })
                : (baseDoc.comments || baseDoc.Comments || []),
            Notes: baseDoc.notes || baseDoc.Notes || [],
            HeadersFooters: headersFooters,
            Revisions: (inst.runtimeRevisions && inst.runtimeRevisions.length > 0)
                ? inst.runtimeRevisions.map(function (revision) { return _cloneRuntimeJson(revision); })
                : (baseDoc.revisions || baseDoc.Revisions || []),
            Assets: baseDoc.assets || baseDoc.Assets || [],
            Anchors: baseDoc.anchors || baseDoc.Anchors || []
        };

        return JSON.stringify({ ProtocolVersion: 1, Document: doc });
    }

    function _createBlockMap(baseDoc) {
        var originalBlocks = baseDoc.blocks || baseDoc.Blocks || [];
        var blockMap = {};
        for (var i = 0; i < originalBlocks.length; i++) {
            var b = originalBlocks[i];
            var bid = b.id || b.Id;
            if (bid) blockMap[bid] = b;
        }
        return blockMap;
    }

    function _serializeAllBodyBlocks(inst, baseDoc) {
        var blockMap = _createBlockMap(baseDoc);
        var blocks = [];

        if (inst.virtualPages && inst.virtualPages.length > 0) {
            var virtualBlocks = [];
            for (var pi = 0; pi < inst.virtualPages.length; pi++) {
                if (pi > 0) {
                    virtualBlocks.push(_createSerializedPageBreak(virtualBlocks.length));
                }

                var pageData = inst.virtualPages[pi];
                for (var bj = 0; bj < pageData.blocks.length; bj++) {
                    var source = pageData.blocks[bj];
                    var cloned = _cloneBlockForSnapshot(source, virtualBlocks.length);
                    if (cloned) virtualBlocks.push(cloned);
                }
            }

            var liveBlocks = _serializeRenderedBodyBlocks(inst, blockMap);
            if (liveBlocks.length > 0) {
                var indexById = new Map();
                for (var vb = 0; vb < virtualBlocks.length; vb++) {
                    var existingId = virtualBlocks[vb].Id || virtualBlocks[vb].id || '';
                    if (existingId) indexById.set(existingId, vb);
                }

                for (var lb = 0; lb < liveBlocks.length; lb++) {
                    var live = liveBlocks[lb];
                    var liveId = live.Id || live.id || '';
                    if (liveId && indexById.has(liveId)) {
                        virtualBlocks[indexById.get(liveId)] = live;
                    } else {
                        virtualBlocks.push(live);
                    }
                }
            }

            return virtualBlocks;
        }

        return _serializeRenderedBodyBlocks(inst, blockMap);
    }

    function _serializeRenderedBodyBlocks(inst, blockMap) {
        var blocks = [];
        var bodyContainers = inst.root.querySelectorAll('.tm-wysiwyg-page__body');
        for (var bi = 0; bi < bodyContainers.length; bi++) {
            if (bi > 0) {
                blocks.push(_createSerializedPageBreak(blocks.length));
            }

            var bodyBlocks = _serializeBodyBlocks(bodyContainers[bi], blockMap);
            for (var i = 0; i < bodyBlocks.length; i++) {
                blocks.push(bodyBlocks[i]);
            }
        }

        return blocks;
    }

    function _serializeBodyBlocks(bodyContainer, blockMap) {
        var blocks = [];
        var bodyBlocks = Array.from(bodyContainer.children)
            .filter(function (child) {
                return child.matches && child.matches('.tm-wysiwyg-block[data-block-id]');
            });
        for (var bj = 0; bj < bodyBlocks.length; bj++) {
            var block = _serializeBlock(bodyBlocks[bj], blockMap, bj);
            if (block) blocks.push(block);
        }
        return blocks;
    }

    function _cloneBlockForSnapshot(block, index) {
        if (!block) return null;
        var cloned = JSON.parse(JSON.stringify(block));
        cloned.Id = cloned.Id || cloned.id || ('block-' + index);
        cloned.Type = cloned.Type ?? cloned.type ?? 0;
        cloned.Order = cloned.Order ?? cloned.order ?? ((index + 1) * 10);
        cloned.Content = cloned.Content || cloned.content || { $type: 'paragraph', Inlines: [] };
        delete cloned.id;
        delete cloned.type;
        delete cloned.order;
        delete cloned.content;
        if (cloned.sectionId && !cloned.SectionId) {
            cloned.SectionId = cloned.sectionId;
            delete cloned.sectionId;
        }
        return cloned;
    }

    function _createSerializedPageBreak(index) {
        return {
            Id: 'page-break-' + index,
            Type: 6,
            Order: (index + 1) * 10,
            Content: { $type: 'pageBreak' }
        };
    }

    function _serializeBlock(blockEl, blockMap, index) {
        var id = blockEl.getAttribute('data-block-id') || '';
        var tag = blockEl.tagName.toLowerCase();
        var type, content;
        var original = blockMap[id];

        switch (tag) {
            case 'p':
                type = 0; // Paragraph
                content = { $type: 'paragraph', Inlines: _serializeInlines(blockEl) };
                break;
            case 'h1': case 'h2': case 'h3': case 'h4': case 'h5': case 'h6':
                type = 1; // Heading
                content = { $type: 'heading', Level: parseInt(tag[1], 10), Inlines: _serializeInlines(blockEl) };
                break;
            case 'ul':
                type = 2; // List
                content = {
                    $type: 'list',
                    Ordered: false,
                    IndentLevel: Math.max(0, Math.round(_readBlockLeftIndent(blockEl) / 36)),
                    Inlines: _serializeInlines(blockEl.querySelector('li') || blockEl)
                };
                break;
            case 'ol':
                type = 2; // List
                content = {
                    $type: 'list',
                    Ordered: true,
                    IndentLevel: Math.max(0, Math.round(_readBlockLeftIndent(blockEl) / 36)),
                    StartNumber: Math.max(1, parseInt(blockEl.getAttribute('start') || '1', 10) || 1),
                    Inlines: _serializeInlines(blockEl.querySelector('li') || blockEl)
                };
                break;
            case 'blockquote':
                type = 3; // Quote
                content = { $type: 'quote', Inlines: _serializeInlines(blockEl) };
                break;
            case 'table':
                type = 4; // Table
                content = _serializeTable(blockEl);
                break;
            case 'figure':
                type = 5; // Image
                content = _serializeImage(blockEl);
                break;
            case 'hr':
                type = 6; // PageBreak
                content = { $type: 'pageBreak' };
                break;
            default:
                type = 0;
                content = { $type: 'paragraph', Inlines: _serializeInlines(blockEl) };
        }

        var order = original ? (original.order || original.Order || (index + 1) * 10) : (index + 1) * 10;
        var sectionId = original ? (original.sectionId || original.SectionId || null) : null;

        var block = {
            Id: id,
            Type: type,
            Order: order,
            ParagraphProperties: _serializeParagraphProperties(blockEl, original),
            Content: content
        };
        if (sectionId) block.SectionId = sectionId;
        return block;
    }

    function _serializeParagraphProperties(blockEl, original) {
        var originalProperties = original
            ? (original.paragraphProperties || original.ParagraphProperties || {})
            : {};
        var style = blockEl.style || {};
        var computed = window.getComputedStyle ? window.getComputedStyle(blockEl) : {};

        return {
            Alignment: style.textAlign
                ? _cssAlignmentToNumber(style.textAlign)
                : _alignmentToNumber(originalProperties.alignment ?? originalProperties.Alignment ?? computed.textAlign ?? 'left'),
            LineSpacing: style.lineHeight
                ? _sanitizeLineSpacing(style.lineHeight)
                : _sanitizeLineSpacing(originalProperties.lineSpacing ?? originalProperties.LineSpacing ?? 1),
            SpacingBefore: style.marginTop
                ? _cssLengthToPoints(style.marginTop)
                : _sanitizeParagraphPoints(originalProperties.spacingBefore ?? originalProperties.SpacingBefore ?? 0, 0, 144),
            SpacingAfter: style.marginBottom
                ? _cssLengthToPoints(style.marginBottom)
                : _sanitizeParagraphPoints(originalProperties.spacingAfter ?? originalProperties.SpacingAfter ?? 0, 0, 144),
            LeftIndent: style.marginLeft
                ? _cssLengthToPoints(style.marginLeft)
                : _sanitizeParagraphPoints(originalProperties.leftIndent ?? originalProperties.LeftIndent ?? 0, 0, 432),
            RightIndent: style.marginRight
                ? _cssLengthToPoints(style.marginRight)
                : _sanitizeParagraphPoints(originalProperties.rightIndent ?? originalProperties.RightIndent ?? 0, 0, 432),
            FirstLineIndent: style.textIndent
                ? _cssLengthToPoints(style.textIndent)
                : _sanitizeParagraphPoints(originalProperties.firstLineIndent ?? originalProperties.FirstLineIndent ?? 0, -216, 216)
        };
    }

    function _cssLengthToPoints(value) {
        var raw = String(value || '').trim().toLowerCase();
        if (!raw || raw === 'normal') return 0;
        var number = parseFloat(raw);
        if (!Number.isFinite(number)) return 0;
        if (raw.endsWith('pt')) return _sanitizeParagraphPoints(number, -432, 432);
        if (raw.endsWith('px')) return _sanitizeParagraphPoints(number * 0.75, -432, 432);
        return _sanitizeParagraphPoints(number, -432, 432);
    }

    function _serializeInlines(container) {
        var inlines = [];
        var nodes = container.childNodes;
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            if (node.nodeType === Node.TEXT_NODE) {
                var text = node.textContent;
                if (text && text.length > 0) {
                    inlines.push({ $type: 'text', Id: '', Text: text });
                }
            } else if (node.nodeType === Node.ELEMENT_NODE) {
                var el = node;
                if (el.hasAttribute('data-inline-id')) {
                    var inline = _serializeInline(el);
                    if (inline) inlines.push(inline);
                } else {
                    var nested = _serializeInlines(el);
                    for (var j = 0; j < nested.length; j++) {
                        inlines.push(nested[j]);
                    }
                }
            }
        }
        return inlines;
    }

    function _serializeInline(el) {
        var id = el.getAttribute('data-inline-id') || '';
        var className = el.className || '';
        var tag = el.tagName.toLowerCase();

        if (className.indexOf('tm-wysiwyg-token') >= 0) {
            return {
                $type: 'token',
                Id: id,
                Key: el.getAttribute('data-token-key') || '',
                TokenType: el.getAttribute('data-token-type') || undefined,
                DisplayName: el.textContent || '',
                ColorClass: className.replace('tm-wysiwyg-token', '').trim() || undefined
            };
        }

        if (className.indexOf('tm-wysiwyg-field') >= 0) {
            return {
                $type: 'field',
                Id: id,
                FieldType: _normalizeDocumentFieldType(el.getAttribute('data-field-type')),
                Format: el.getAttribute('data-field-format') || undefined,
                FallbackText: el.getAttribute('data-field-fallback') || undefined,
                DisplayText: el.textContent || ''
            };
        }

        if (tag === 'sup' && className.indexOf('tm-wysiwyg-note-ref') >= 0) {
            return {
                $type: 'noteReference',
                Id: id,
                NoteId: el.getAttribute('data-note-id') || '',
                DisplayMarker: el.textContent || '',
                NoteType: _normalizeNoteType(el.getAttribute('data-note-type'))
            };
        }

        var marks = _serializeMarks(el);
        var text = _serializeInlineText(el);
        var inline = {
            $type: 'text',
            Id: id,
            Text: text
        };
        if (marks.length > 0) inline.Marks = marks;
        return inline;
    }

    function _serializeInlineText(el) {
        var text = '';

        function visit(node) {
            for (var i = 0; i < node.childNodes.length; i++) {
                var child = node.childNodes[i];
                if (child.nodeType === Node.TEXT_NODE) {
                    text += child.textContent || '';
                    continue;
                }

                if (_isInlineBreakNode(child)) {
                    text += '\n';
                    continue;
                }

                if (_isCaretPlaceholderNode(child)) {
                    continue;
                }

                if (child.nodeType === Node.ELEMENT_NODE) {
                    visit(child);
                }
            }
        }

        visit(el);
        return text;
    }

    function _serializeMarks(el) {
        var marks = [];
        var style = el.style;
        var classList = el.classList;

        var fontWeight = style.fontWeight || '';
        if (classList.contains('tm-document-inline--bold')
            || fontWeight === 'bold'
            || fontWeight === '700'
            || parseInt(fontWeight, 10) >= 700) {
            marks.push({ Type: 0 }); // Bold
        }

        if (classList.contains('tm-document-inline--italic') || style.fontStyle === 'italic') {
            marks.push({ Type: 1 }); // Italic
        }

        var textDeco = (style.textDecorationLine || '') + ' ' + (style.textDecoration || '');
        if (classList.contains('tm-document-inline--underline') || textDeco.indexOf('underline') >= 0) {
            marks.push({ Type: 2 }); // Underline
        }
        if (classList.contains('tm-document-inline--strikethrough') || textDeco.indexOf('line-through') >= 0) {
            marks.push({ Type: 3 }); // Strikethrough
        }

        var background = _cssColorToHex(style.backgroundColor || '');
        if (background) {
            marks.push({ Type: 9, Value: background }); // Highlight
        }

        var color = _cssColorToHex(style.color || '');
        if (color) {
            marks.push({ Type: 10, Value: color }); // TextColor
        }

        if (style.fontFamily) {
            marks.push({ Type: 11, Value: style.fontFamily }); // FontFamily
        }

        if (style.fontSize && /pt$/i.test(style.fontSize)) {
            marks.push({ Type: 12, Value: style.fontSize }); // FontSize
        }

        var vAlign = style.verticalAlign || '';
        var fSize = style.fontSize || '';
        if (classList.contains('tm-document-inline--superscript') || (vAlign === 'super' && fSize === 'smaller')) {
            marks.push({ Type: 4 }); // Superscript
        }
        if (classList.contains('tm-document-inline--subscript') || (vAlign === 'sub' && fSize === 'smaller')) {
            marks.push({ Type: 5 }); // Subscript
        }

        var link = el.querySelector('a');
        var dataHref = _sanitizeLinkHref(el.getAttribute('data-link-href'));
        var linkHref = link ? _sanitizeLinkHref(link.getAttribute('data-link-href') || link.getAttribute('href') || link.href || '') : '';
        var linkTitle = el.getAttribute('data-link-title') || (link ? link.getAttribute('data-link-title') || link.getAttribute('title') : '') || '';
        if (dataHref || linkHref) {
            marks.push({
                Type: 6, // Link
                Link: { Href: dataHref || linkHref, Title: linkTitle || undefined }
            });
        }

        var commentId = el.getAttribute('data-comment-id');
        if (commentId) {
            marks.push({
                Type: 7, // CommentAnchor
                CommentAnchor: { CommentId: commentId }
            });
        }

        var revisionId = el.getAttribute('data-revision-id');
        if (revisionId) {
            marks.push({
                Type: 8, // Revision
                RevisionId: revisionId,
                Value: el.getAttribute('data-revision-type') || 'Insertion'
            });
        }

        return marks;
    }

    function _cssColorToHex(value) {
        var raw = String(value || '').trim();
        if (!raw || raw === 'transparent' || raw === 'rgba(0, 0, 0, 0)') return '';
        if (/^#[0-9a-f]{6}$/i.test(raw)) return raw.toLowerCase();
        var match = raw.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([.\d]+))?\)$/i);
        if (!match || match[4] === '0') return '';
        return '#' + [match[1], match[2], match[3]].map(function (part) {
            return Math.max(0, Math.min(255, parseInt(part, 10))).toString(16).padStart(2, '0');
        }).join('');
    }

    function _serializeTable(tableEl) {
        var rows = [];
        var trEls = tableEl.querySelectorAll('tr');
        for (var r = 0; r < trEls.length; r++) {
            var cells = [];
            var tdEls = trEls[r].querySelectorAll('td, th');
            for (var c = 0; c < tdEls.length; c++) {
                var td = tdEls[c];
                var cellId = td.getAttribute('data-cell-id') || '';
                var serializedCell = {
                    Id: cellId,
                    ColumnSpan: parseInt(td.getAttribute('colspan') || '1', 10),
                    RowSpan: parseInt(td.getAttribute('rowspan') || '1', 10),
                    IsHeader: td.tagName === 'TH',
                    Blocks: _serializeBlocksFromContainer(td)
                };
                var width = td.getAttribute('data-cell-width') || td.style.width || '';
                if (width) {
                    var numericWidth = parseFloat(width);
                    serializedCell.Width = isNaN(numericWidth) ? width : numericWidth;
                }
                var background = td.getAttribute('data-cell-background') || td.style.backgroundColor || '';
                if (background) {
                    serializedCell.BackgroundColor = background;
                }
                var borders = _serializeTableCellBorders(td);
                if (borders) {
                    serializedCell.Borders = borders;
                }
                var verticalAlignment = td.getAttribute('data-cell-vertical-align') || td.style.verticalAlign || '';
                if (verticalAlignment) {
                    serializedCell.VerticalAlignment = _serializeVerticalAlignment(verticalAlignment);
                }
                var padding = td.getAttribute('data-cell-padding') || td.style.padding || '';
                if (padding) {
                    var numericPadding = parseFloat(padding);
                    serializedCell.Padding = isNaN(numericPadding) ? null : numericPadding;
                }
                cells.push(serializedCell);
            }
            rows.push({ Cells: cells });
        }
        return { $type: 'table', Rows: rows, Layout: _serializeTableLayout(tableEl) };
    }

    function _serializeTableLayout(tableEl) {
        var layout = {
            Alignment: _serializeTableAlignment(tableEl.getAttribute('data-table-alignment') || 'left'),
            Borders: {}
        };
        var width = tableEl.getAttribute('data-table-width') || tableEl.style.width || '';
        if (width) {
            var numericWidth = parseFloat(width);
            layout.Width = isNaN(numericWidth) ? null : numericWidth;
        }
        var padding = tableEl.getAttribute('data-table-cell-padding') || '';
        if (padding) {
            var numericPadding = parseFloat(padding);
            layout.CellPadding = isNaN(numericPadding) ? null : numericPadding;
        }
        var background = tableEl.getAttribute('data-table-background') || tableEl.style.backgroundColor || '';
        if (background) {
            layout.BackgroundColor = background;
        }
        var borders = _serializeTableCellBorders(tableEl);
        if (borders) {
            layout.Borders = borders;
        }
        return layout;
    }

    function _serializeTableAlignment(value) {
        var normalized = _normalizeTableAlignment(value);
        return normalized === 'center' ? 1 : normalized === 'right' ? 2 : 0;
    }

    function _serializeVerticalAlignment(value) {
        var normalized = _normalizeTableVerticalAlignment(value);
        return normalized === 'middle' ? 1 : normalized === 'bottom' ? 2 : 0;
    }

    function _serializeTableCellBorders(td) {
        var borders = {};
        var top = td.getAttribute('data-cell-border-top') || td.style.borderTop || '';
        var right = td.getAttribute('data-cell-border-right') || td.style.borderRight || '';
        var bottom = td.getAttribute('data-cell-border-bottom') || td.style.borderBottom || '';
        var left = td.getAttribute('data-cell-border-left') || td.style.borderLeft || '';
        if (top) borders.Top = top;
        if (right) borders.Right = right;
        if (bottom) borders.Bottom = bottom;
        if (left) borders.Left = left;
        return Object.keys(borders).length > 0 ? borders : null;
    }

    function _serializeBlocksFromContainer(container) {
        var blocks = [];
        var blockEls = container.querySelectorAll('.tm-wysiwyg-block[data-block-id]');
        for (var i = 0; i < blockEls.length; i++) {
            var block = _serializeBlock(blockEls[i], {}, i);
            if (block) blocks.push(block);
        }
        return blocks;
    }

    /**
     * Phase 12: Serializes header/footer DOM regions back into the document model.
     * Groups blocks by their header/footer id. Only the first occurrence of each
     * header/footer is serialized (all pages share the same definition).
     */
    function _serializeHeaderFooterRegions(inst, baseDoc) {
        var originalHfs = baseDoc.headersFooters || baseDoc.HeadersFooters || [];
        var hfMap = {};
        for (var i = 0; i < originalHfs.length; i++) {
            var h = originalHfs[i];
            var hid = h.id || h.Id;
            if (hid) hfMap[hid] = h;
        }

        var result = [];
        var seenIds = {};

        var headerEls = inst.root.querySelectorAll('.tm-wysiwyg-page__header');
        for (var hi = 0; hi < headerEls.length; hi++) {
            var el = headerEls[hi];
            var hfId = el.getAttribute('data-hf-id');
            if (!hfId || seenIds[hfId]) continue;
            seenIds[hfId] = true;

            var original = hfMap[hfId] || {};
            var blocks = [];
            var blockEls = el.querySelectorAll('.tm-wysiwyg-block[data-block-id]');
            for (var bj = 0; bj < blockEls.length; bj++) {
                var block = _serializeBlock(blockEls[bj], {}, bj);
                if (block) blocks.push(block);
            }

            result.push({
                Id: hfId,
                Type: original.type || original.Type || 'Header',
                Scope: el.getAttribute('data-hf-scope') || original.scope || original.Scope || 'Primary',
                SectionId: original.sectionId || original.SectionId || null,
                Blocks: blocks
            });
        }

        var footerEls = inst.root.querySelectorAll('.tm-wysiwyg-page__footer');
        for (var fi = 0; fi < footerEls.length; fi++) {
            var fel = footerEls[fi];
            var fId = fel.getAttribute('data-hf-id');
            if (!fId || seenIds[fId]) continue;
            seenIds[fId] = true;

            var fOriginal = hfMap[fId] || {};
            var fBlocks = [];
            var fBlockEls = fel.querySelectorAll('.tm-wysiwyg-block[data-block-id]');
            for (var fk = 0; fk < fBlockEls.length; fk++) {
                var fBlock = _serializeBlock(fBlockEls[fk], {}, fk);
                if (fBlock) fBlocks.push(fBlock);
            }

            result.push({
                Id: fId,
                Type: fOriginal.type || fOriginal.Type || 'Footer',
                Scope: fel.getAttribute('data-hf-scope') || fOriginal.scope || fOriginal.Scope || 'Primary',
                SectionId: fOriginal.sectionId || fOriginal.SectionId || null,
                Blocks: fBlocks
            });
        }

        return result;
    }

    function _serializeImage(figureEl) {
        var img = figureEl.querySelector('img');
        var figcaption = figureEl.querySelector('figcaption');
        var size = {};
        if (img) {
            var w = img.style.width;
            var h = img.style.height;
            if (w) size.Width = parseInt(w, 10);
            if (h) size.Height = parseInt(h, 10);
            size.LockAspectRatio = figureEl.getAttribute('data-lock-aspect-ratio') !== 'false';
        }
        var source = parseInt(figureEl.getAttribute('data-image-source') || '0', 10);
        var assetId = figureEl.getAttribute('data-image-asset-id') || '';
        var linkUrl = figureEl.getAttribute('data-image-link') || '';
        var content = {
            $type: 'image',
            Source: source,
            Url: img ? img.src : '',
            AltText: img ? img.alt : '',
            Caption: figcaption ? figcaption.textContent : ''
        };
        if (assetId) content.AssetId = assetId;
        if (linkUrl) content.LinkUrl = linkUrl;
        if (Object.keys(size).length > 0) content.Size = size;
        var naturalWidth = parseInt(figureEl.getAttribute('data-image-natural-width') || '0', 10) || 0;
        var naturalHeight = parseInt(figureEl.getAttribute('data-image-natural-height') || '0', 10) || 0;
        if (naturalWidth > 0 || naturalHeight > 0) {
            content.NaturalSize = {};
            if (naturalWidth > 0) content.NaturalSize.Width = naturalWidth;
            if (naturalHeight > 0) content.NaturalSize.Height = naturalHeight;
        }
        var inline = figureEl.getAttribute('data-floating-inline') !== 'false';
        var wrapMode = parseInt(figureEl.getAttribute('data-wrap-mode') || (inline ? '0' : '1'), 10);
        var horizontal = parseInt(figureEl.getAttribute('data-horizontal-relative-to') || '0', 10);
        var vertical = parseInt(figureEl.getAttribute('data-vertical-relative-to') || '3', 10);
        var x = parseFloat(figureEl.getAttribute('data-image-x') || '0') || 0;
        var y = parseFloat(figureEl.getAttribute('data-image-y') || '0') || 0;
        var z = parseInt(figureEl.style.zIndex || '0', 10) || 0;
        var hPosAttr = figureEl.getAttribute('data-horizontal-position');
        var hPos = hPosAttr ? _normalizeHorizontalPosition(hPosAttr) : null;
        var distL = parseFloat(figureEl.style.marginLeft) || 0;
        var distR = parseFloat(figureEl.style.marginRight) || 0;
        var distT = parseFloat(figureEl.style.marginTop) || 0;
        var distB = parseFloat(figureEl.style.marginBottom) || 0;
        if (!inline || wrapMode !== 0 || x !== 0 || y !== 0 || z !== 0) {
            content.FloatingLayout = {
                Inline: inline,
                HorizontalRelativeTo: horizontal,
                VerticalRelativeTo: vertical,
                X: x,
                Y: y,
                WrapMode: wrapMode,
                ZIndex: z,
                LockAnchor: figureEl.getAttribute('data-lock-anchor') === 'true'
            };
            if (hPos) content.FloatingLayout.HorizontalPosition = hPos.value;
            if (distL !== 0) content.FloatingLayout.DistanceLeft = distL;
            if (distR !== 0) content.FloatingLayout.DistanceRight = distR;
            if (distT !== 0) content.FloatingLayout.DistanceTop = distT;
            if (distB !== 0) content.FloatingLayout.DistanceBottom = distB;
        }
        return content;
    }

    function measureBlockForDebug(instanceId, target) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return null;

        var blockEl = null;
        if (typeof target === 'string') {
            blockEl = inst.root.querySelector(target);
        } else if (target && target.nodeType === Node.ELEMENT_NODE) {
            blockEl = target;
        } else {
            blockEl = inst.root.querySelector('.tm-wysiwyg-block[data-block-id]');
        }

        return _measureBlock(inst, blockEl);
    }

    function getDebugMetrics(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return null;
        var virtualState = inst.virtualState || {
            enabled: false,
            totalPages: inst.virtualPages ? inst.virtualPages.length : 0,
            renderedPages: inst.root ? inst.root.querySelectorAll('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual)').length : 0,
            virtualizedPages: 0,
            first: 0,
            last: 0,
            pageExtent: 0
        };
        var viewport = _getVirtualViewport(inst);
        var performanceStats = _ensurePerformanceStats(inst);

        return {
            SnapshotApplyCount: inst.renderStats ? inst.renderStats.snapshotApplies : 0,
            FullRenderCount: inst.renderStats ? inst.renderStats.fullRenders : 0,
            IncrementalOperationCount: inst.renderStats ? inst.renderStats.incrementalOperations : 0,
            LastRenderReason: inst.renderStats ? inst.renderStats.lastRenderReason : '',
            InputOperationCount: inst.inputStats ? inst.inputStats.operationCount : 0,
            InputLongOperationCount: inst.inputStats ? inst.inputStats.longOperationCount : 0,
            LastInputLatencyMs: inst.inputStats ? inst.inputStats.lastLatencyMs : 0,
            MaxInputLatencyMs: inst.inputStats ? inst.inputStats.maxLatencyMs : 0,
            AverageInputLatencyMs: inst.inputStats && inst.inputStats.operationCount > 0
                ? inst.inputStats.totalLatencyMs / inst.inputStats.operationCount
                : 0,
            LastInputOperationMs: inst.inputStats ? inst.inputStats.lastOperationMs : 0,
            MaxInputOperationMs: inst.inputStats ? inst.inputStats.maxOperationMs : 0,
            AverageInputOperationMs: inst.inputStats && inst.inputStats.operationCount > 0
                ? inst.inputStats.totalOperationMs / inst.inputStats.operationCount
                : 0,
            LastInputMetricType: inst.inputStats ? inst.inputStats.lastInputType : '',
            LastInputEventType: inst.inputStats ? inst.inputStats.lastEventType : '',
            RemoteOperationApplyCount: inst.renderStats ? inst.renderStats.remoteOperations : 0,
            RemoteOperationBatchCount: inst.renderStats ? inst.renderStats.remoteBatches : 0,
            MeasureCount: inst.measureStats.count,
            MeasureCacheHits: inst.measureStats.cacheHits,
            MeasureInvalidations: inst.measureStats.invalidations,
            MeasureCacheSize: inst.measureCache.size,
            MarkerRenderAttemptCount: performanceStats.markerRenderAttempts || 0,
            MarkerRenderCount: performanceStats.markerRenderCount || 0,
            MarkerRenderSkippedCount: performanceStats.markerRenderSkippedCount || 0,
            LastMarkerRenderMs: performanceStats.markerRenderLastMs || 0,
            MaxMarkerRenderMs: performanceStats.markerRenderMaxMs || 0,
            AverageMarkerRenderMs: performanceStats.markerRenderAttempts > 0
                ? performanceStats.markerRenderTotalMs / performanceStats.markerRenderAttempts
                : 0,
            FloatingRepositionCount: performanceStats.floatingRepositionCount || 0,
            LastFloatingRepositionMs: performanceStats.floatingRepositionLastMs || 0,
            MaxFloatingRepositionMs: performanceStats.floatingRepositionMaxMs || 0,
            AverageFloatingRepositionMs: performanceStats.floatingRepositionCount > 0
                ? performanceStats.floatingRepositionTotalMs / performanceStats.floatingRepositionCount
                : 0,
            ClipboardNormalizationCount: performanceStats.clipboardNormalizationCount || 0,
            LastClipboardNormalizationMs: performanceStats.clipboardNormalizationLastMs || 0,
            MaxClipboardNormalizationMs: performanceStats.clipboardNormalizationMaxMs || 0,
            AverageClipboardNormalizationMs: performanceStats.clipboardNormalizationCount > 0
                ? performanceStats.clipboardNormalizationTotalMs / performanceStats.clipboardNormalizationCount
                : 0,
            VirtualizationEnabled: !!virtualState.enabled,
            TotalPages: virtualState.totalPages || 0,
            RenderedPages: virtualState.renderedPages || 0,
            VirtualizedPages: virtualState.virtualizedPages || 0,
            FirstPage: virtualState.first || 0,
            LastPage: virtualState.last || 0,
            PageExtent: virtualState.pageExtent || 0,
            ScrollTop: viewport.scrollTop || 0,
            RootScrollTop: inst.root.scrollTop || 0,
            RootScrollHeight: inst.root.scrollHeight || 0,
            RootClientHeight: inst.root.clientHeight || 0
        };
    }

    function getDebugSnapshot(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst) {
            return {
                InstanceId: instanceId || '',
                HasInstance: false
            };
        }

        var selection = _captureSelectionSnapshot(inst);
        var lastSelection = inst.lastSelectionSnapshot || null;
        var activeElement = document.activeElement && document.activeElement.nodeType === Node.ELEMENT_NODE
            ? document.activeElement
            : null;
        var selectionNode = null;
        var browserSelection = window.getSelection ? window.getSelection() : null;
        if (browserSelection && browserSelection.rangeCount > 0) {
            selectionNode = browserSelection.anchorNode;
        }

        var selectedElement = selectionNode
            ? (selectionNode.nodeType === Node.ELEMENT_NODE ? selectionNode : selectionNode.parentElement)
            : null;
        var debugElement = selectedElement || activeElement;
        var activeBlock = debugElement ? debugElement.closest('[data-block-id]') : null;
        var activeInline = debugElement ? debugElement.closest('[data-inline-id]') : null;
        var pending = inst.pendingInputPatch || null;

        return {
            InstanceId: inst.id || instanceId || '',
            HasInstance: true,
            IsDisposed: !!inst.disposed,
            IsReadOnly: !!inst.readOnly,
            TrackChangesEnabled: !!inst.trackChangesEnabled,
            CompositionActive: !!inst.compositionActive,
            CompositionText: inst.compositionText || '',
            CompositionUpdateCount: inst.compositionUpdateCount || 0,
            AcceptingNativeInput: !!inst.acceptingNativeInput,
            JsOwnedInputCount: inst.jsOwnedInputCount || 0,
            NativeInputCount: inst.nativeInputCount || 0,
            CurrentTransactionId: inst.currentTransactionId || null,
            PendingTransactionId: pending ? (pending.transactionId || pending.TransactionId || null) : null,
            PendingPatchType: pending ? (pending.type || pending.Type || null) : null,
            UndoDepth: inst.commandUndoStack ? inst.commandUndoStack.length : 0,
            RedoDepth: inst.commandRedoStack ? inst.commandRedoStack.length : 0,
            RuntimeUndoEpoch: inst.runtimeUndoEpoch || 0,
            QueuedRemoteBatchCount: inst.queuedRemoteBatches ? inst.queuedRemoteBatches.length : 0,
            LastInputType: inst.lastInputType || null,
            LastInputDataLength: inst.lastInputDataLength || 0,
            LastInputOperationId: inst.lastInputOperationId || null,
            LastPatchType: inst.lastPatchType || null,
            LastPatchId: inst.lastPatchId || null,
            LastPatchTransactionId: inst.lastPatchTransactionId || null,
            LastPatchAt: inst.lastPatchAt || null,
            ClearFormattingPointerCaptureCount: inst.clearFormattingPointerCaptureCount || 0,
            ClearFormattingCommandCount: inst.clearFormattingCommandCount || 0,
            ContextMenuSelection: _toPascalSelection(inst.contextMenuSelectionSnapshot || null),
            LastClearFormattingFallbackSelection: _toPascalSelection(inst.lastClearFormattingFallbackSelection || null),
            MiniToolbarVisible: !!inst.miniToolbarVisible,
            MiniToolbarRequestKey: inst.miniToolbarRequestKey || null,
            CurrentSelection: _toPascalSelection(selection),
            LastSelection: _toPascalSelection(lastSelection),
            ActiveBlockId: activeBlock ? activeBlock.getAttribute('data-block-id') : null,
            ActiveInlineId: activeInline ? activeInline.getAttribute('data-inline-id') : null,
            ActiveElementTagName: activeElement ? activeElement.tagName.toLowerCase() : null,
            ActiveElementTestId: activeElement ? activeElement.getAttribute('data-testid') : null,
            ActiveElementClasses: activeElement ? (activeElement.className || '') : null,
            ActiveDomPath: debugElement ? _buildDebugDomPath(debugElement, inst.root) : null,
            ActiveTextOffset: selection ? (selection.anchorOffset ?? selection.AnchorOffset ?? 0) : 0,
            RootHasFocus: !!(activeElement && inst.root.contains(activeElement)),
            RenderedBlockCount: inst.root.querySelectorAll('[data-block-id]').length,
            RevisionElementCount: inst.root.querySelectorAll('[data-revision-id], .tm-wysiwyg-revision--insert, .tm-wysiwyg-revision--delete').length,
            ImageElementCount: inst.root.querySelectorAll('figure.tm-wysiwyg-image-block img, figure[data-image-source] img').length,
            BodyTextLength: (inst.root.textContent || '').length
        };
    }

    function _toPascalSelection(selection) {
        if (!selection) return null;
        return {
            Region: selection.region || selection.Region || 'Body',
            PageIndex: selection.pageIndex ?? selection.PageIndex ?? null,
            HeaderFooterId: selection.headerFooterId || selection.HeaderFooterId || null,
            AnchorBlockId: selection.anchorBlockId || selection.AnchorBlockId || null,
            AnchorInlineId: selection.anchorInlineId || selection.AnchorInlineId || null,
            AnchorOffset: selection.anchorOffset ?? selection.AnchorOffset ?? 0,
            AnchorBlockOffset: selection.anchorBlockOffset ?? selection.AnchorBlockOffset ?? 0,
            FocusBlockId: selection.focusBlockId || selection.FocusBlockId || null,
            FocusInlineId: selection.focusInlineId || selection.FocusInlineId || null,
            FocusOffset: selection.focusOffset ?? selection.FocusOffset ?? 0,
            FocusBlockOffset: selection.focusBlockOffset ?? selection.FocusBlockOffset ?? 0,
            IsCollapsed: selection.isCollapsed ?? selection.IsCollapsed ?? true,
            Direction: selection.direction || selection.Direction || 'forward',
            ActiveTableCellId: selection.activeTableCellId || selection.ActiveTableCellId || null,
            TableCellPath: selection.tableCellPath || selection.TableCellPath || null,
            ActiveImageBlockId: selection.activeImageBlockId || selection.ActiveImageBlockId || null,
            AnchorNodeId: selection.anchorNodeId || selection.AnchorNodeId || selection.anchorInlineId || selection.AnchorInlineId || selection.anchorBlockId || selection.AnchorBlockId || null,
            FocusNodeId: selection.focusNodeId || selection.FocusNodeId || selection.focusInlineId || selection.FocusInlineId || selection.focusBlockId || selection.FocusBlockId || null
        };
    }

    function _buildDebugDomPath(element, root) {
        var parts = [];
        var current = element;
        while (current && current.nodeType === Node.ELEMENT_NODE) {
            var part = current.tagName.toLowerCase();
            var testId = current.getAttribute('data-testid');
            var blockId = current.getAttribute('data-block-id');
            var inlineId = current.getAttribute('data-inline-id');
            if (testId) {
                part += '[data-testid="' + testId + '"]';
            } else if (blockId) {
                part += '[data-block-id="' + blockId + '"]';
            } else if (inlineId) {
                part += '[data-inline-id="' + inlineId + '"]';
            } else if (current.classList && current.classList.length > 0) {
                part += '.' + Array.from(current.classList).slice(0, 2).join('.');
            }

            parts.unshift(part);
            if (current === root) break;
            current = current.parentElement;
        }

        return parts.join(' > ');
    }

    function clearDebugMetrics(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return;
        inst.measureStats = { count: 0, cacheHits: 0, invalidations: 0 };
        inst.renderStats = {
            snapshotApplies: 0,
            fullRenders: 0,
            incrementalOperations: 0,
            remoteOperations: 0,
            remoteBatches: 0,
            lastRenderReason: ''
        };
        inst.inputStats = {
            operationCount: 0,
            longOperationCount: 0,
            totalLatencyMs: 0,
            totalOperationMs: 0,
            maxLatencyMs: 0,
            maxOperationMs: 0,
            lastLatencyMs: 0,
            lastOperationMs: 0,
            lastInputType: '',
            lastEventType: ''
        };
        inst.performanceStats = _createPerformanceStats();
        inst.measureCache.clear();
    }

    function refreshVirtualization(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !inst.virtualPages || inst.virtualPages.length === 0) return;
        _renderVirtualizedPages(inst, true);
    }

    function setSearchMarkers(instanceId, blockIdsOrMarkers, offsets, lengths) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return false;
        _setSearchMarkers(inst, blockIdsOrMarkers, offsets, lengths);
        return true;
    }

    function clearSearchMarkers(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return false;
        _clearSearchMarkers(inst);
        return true;
    }

    function scrollToSearchResult(instanceId, blockId, offset, length) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return false;
        _scrollToSearchResult(inst, blockId, offset, length);
        return true;
    }

    function upsertMarker(instanceId, marker) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return null;
        return _upsertRuntimeMarker(inst, marker, true);
    }

    function removeMarker(instanceId, markerId) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return false;
        return _removeRuntimeMarker(inst, markerId);
    }

    function getMarkers(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return [];
        return _getRuntimeMarkers(inst).map(function (marker) { return _cloneRuntimeJson(marker); });
    }

    // ── Public API ───────────────────────────────────────────────────────────

    return {
        create: create,
        dispose: dispose,
        isAlive: isAlive,
        applySnapshot: applySnapshot,
        applyRemoteOperation: applyRemoteOperation,
        applyRemoteOperationBatch: applyRemoteOperationBatch,
        applyRemoteOperations: applyRemoteOperations,
        applyRemoteCursor: applyRemoteCursor,
        getSnapshot: getSnapshot,
        focus: focus,
        closeHeaderFooter: closeHeaderFooter,
        getSelectionSnapshot: getSelectionSnapshot,
        getRuntimeSelection: getRuntimeSelection,
        setTrackChangesEnabled: setTrackChangesEnabled,
        setReviewDisplayMode: setReviewDisplayMode,
        setReadOnly: setReadOnly,
        scrollToRevision: scrollToRevision,
        reviewRevision: reviewRevision,
        clearRevisionDecorations: clearRevisionDecorations,
        upsertComment: upsertComment,
        removeComment: removeComment,
        scrollToComment: scrollToComment,
        restoreSelection: restoreSelection,
        getFormattingState: getFormattingState,
        getLastCommandTransaction: getLastCommandTransaction,
        getUndoState: getUndoState,
        getDebugUndoStack: getDebugUndoStack,
        getDirtyState: getDirtyState,
        markSaved: markSaved,
        getOfflineState: getOfflineState,
        applyOfflineState: applyOfflineState,
        undo: undo,
        redo: redo,
        getLinkInfo: getLinkInfo,
        executeCommand: executeCommand,
        measureBlockForDebug: measureBlockForDebug,
        getDebugMetrics: getDebugMetrics,
        getDebugSnapshot: getDebugSnapshot,
        clearDebugMetrics: clearDebugMetrics,
        refreshVirtualization: refreshVirtualization,
        setSearchMarkers: setSearchMarkers,
        clearSearchMarkers: clearSearchMarkers,
        scrollToSearchResult: scrollToSearchResult,
        upsertMarker: upsertMarker,
        removeMarker: removeMarker,
        getMarkers: getMarkers,
        __testHooks: {
            sortRemoteBatchOperations: function (operations) {
                return _sortRemoteBatchOperations((operations || []).slice());
            },
            transformRemoteBatchInsertOffsets: function (operations) {
                var clone = JSON.parse(JSON.stringify(operations || []));
                _transformRemoteBatchInsertOffsets(clone);
                return clone;
            },
            transformSelectionForTextChange: function (snapshot, target, changeOffset, changeLength, isDelete) {
                return _transformSelectionForTextChange(snapshot, target, changeOffset, changeLength, isDelete);
            },
            createRenderPlan: function (document) {
                return _createRenderPlan(document);
            },
            operationRendererKeys: function () {
                return Object.keys(_createOperationRendererRegistry()).sort();
            },
            createRuntimeSelectionFromSnapshot: function (snapshot) {
                return _createRuntimeSelectionFromSnapshot(snapshot);
            },
            createSelectionSnapshotFromRuntimeSelection: function (selection) {
                return _createSelectionSnapshotFromRuntimeSelection(selection);
            },
            createRuntimeCommandTransaction: function (command, payload, beforeSelection, afterSelection, beforeFormatting, afterFormatting) {
                var inst = {
                    id: 'test',
                    currentTransactionId: 'txn-test',
                    commandOperationCounter: 0,
                    commandTransactionCounter: 0
                };
                return _createRuntimeCommandTransaction(
                    inst,
                    command,
                    payload,
                    _nextRuntimeOperationId(inst),
                    beforeSelection,
                    afterSelection,
                    beforeFormatting,
                    afterFormatting);
            },
            transformRuntimeCommentAnchorsForTextChange: function (comments, blockId, offset, length, isDelete) {
                var inst = {
                    runtimeComments: JSON.parse(JSON.stringify(comments || [])),
                    snapshot: { Document: { Comments: [] } },
                    lastCommentStateJson: JSON.stringify(comments || [])
                };
                _transformRuntimeCommentAnchorsForTextChange(inst, blockId, offset, length, isDelete);
                return inst.runtimeComments;
            },
            normalizeWrapMode: _normalizeWrapMode,
            normalizeHorizontalPosition: _normalizeHorizontalPosition,
            schemaAllowsBlock: _schemaAllowsBlock,
            schemaAllowsToolbarBlockCommand: _schemaAllowsToolbarBlockCommand,
            normalizeInsertionBlocksForSchema: function (blocks, region) {
                var inst = { lastInsertionPolicyWarnings: [] };
                var normalized = _normalizeInsertionBlocksForSchema(inst, blocks || [], region || 'Body');
                return { blocks: normalized, warnings: inst.lastInsertionPolicyWarnings };
            },
            detectAutocompleteTriggerText: _detectAutocompleteTriggerText,
            createMarkerStore: function (markers) {
                var inst = { root: null, markerStore: new Map() };
                (markers || []).forEach(function (marker) { _upsertRuntimeMarker(inst, marker, false); });
                return {
                    all: _getRuntimeMarkers(inst),
                    byType: function (type) { return _getRuntimeMarkersByType(inst, type); },
                    byBlock: function (blockId) { return _getRuntimeMarkersByBlock(inst, blockId); },
                    overlapping: function (range) { return _getOverlappingRuntimeMarkers(inst, range); },
                    upsert: function (marker) {
                        _upsertRuntimeMarker(inst, marker, false);
                        return _getRuntimeMarkers(inst);
                    },
                    remove: function (markerId) { return _removeRuntimeMarker(inst, markerId); },
                    transformText: function (blockId, offset, length, isDelete) {
                        _transformRuntimeMarkersForTextChange(inst, blockId, offset, length, !!isDelete);
                        return _getRuntimeMarkers(inst);
                    },
                    renderClasses: function () {
                        return _getRuntimeMarkers(inst).map(function (marker) {
                            return {
                                id: marker.id,
                                type: marker.type,
                                className: _runtimeMarkerClassName(marker.type, marker),
                                testId: _runtimeMarkerTestId(marker.type)
                            };
                        });
                    }
                };
            },
            computeFloatingPosition: function (anchor, elementSize, options) {
                return _computeFloatingPosition(anchor, elementSize, options);
            },
            createPerformanceMetricsHarness: function () {
                var instanceId = 'phase20-metrics-' + Date.now().toString(36) + Math.random().toString(36).slice(2);
                var root = {
                    scrollTop: 0,
                    scrollHeight: 0,
                    clientHeight: 0,
                    querySelector: function () { return null; },
                    querySelectorAll: function () { return []; },
                    getBoundingClientRect: function () { return { top: 0, left: 0, width: 0, height: 0 }; }
                };
                var inst = {
                    id: instanceId,
                    root: root,
                    disposed: false,
                    measureCache: new Map(),
                    measureStats: { count: 0, cacheHits: 0, invalidations: 0 },
                    renderStats: {
                        snapshotApplies: 0,
                        fullRenders: 0,
                        incrementalOperations: 0,
                        remoteOperations: 0,
                        remoteBatches: 0,
                        lastRenderReason: ''
                    },
                    inputStats: {
                        operationCount: 1,
                        longOperationCount: 0,
                        totalLatencyMs: 12,
                        totalOperationMs: 8,
                        maxLatencyMs: 12,
                        maxOperationMs: 8,
                        lastLatencyMs: 12,
                        lastOperationMs: 8,
                        lastInputType: 'insertText',
                        lastEventType: 'beforeinput'
                    },
                    performanceStats: _createPerformanceStats(),
                    virtualPages: [],
                    virtualState: null
                };
                _instances.set(instanceId, inst);
                return {
                    instanceId: instanceId,
                    recordMarkerRender: function (rendered) { _recordMarkerRenderMetric(inst, _performanceNow(), !!rendered); },
                    recordFloatingReposition: function () { _recordFloatingRepositionMetric(inst, _performanceNow()); },
                    recordClipboardNormalization: function () { _recordClipboardNormalizationMetric(inst, _performanceNow()); },
                    metrics: function () { return getDebugMetrics(instanceId); },
                    clear: function () { clearDebugMetrics(instanceId); },
                    dispose: function () { _instances.delete(instanceId); }
                };
            },
            buildPageMetrics: function (virtualPages, renderedPageIndexes, overflowPageIndexes, activePageIndex) {
                var rendered = new Set(renderedPageIndexes || []);
                var overflow = new Set(overflowPageIndexes || []);
                var inst = {
                    root: null,
                    virtualPages: (virtualPages || []).map(function (page, index) {
                        return {
                            index: page.index ?? page.Index ?? index,
                            blockIds: page.blockIds || page.BlockIds || []
                        };
                    })
                };
                var pages = inst.virtualPages.map(function (page, index) {
                    var pageIndex = page.index ?? index;
                    return {
                        PageIndex: pageIndex,
                        PageNumber: pageIndex + 1,
                        Label: 'Page ' + (pageIndex + 1),
                        IsVirtual: !rendered.has(pageIndex),
                        HasOverflow: overflow.has(pageIndex),
                        BlockIds: page.blockIds || []
                    };
                });
                return {
                    TotalPages: pages.length,
                    RenderedPages: pages.filter(function (page) { return !page.IsVirtual; }).length,
                    VirtualizedPages: pages.filter(function (page) { return page.IsVirtual; }).length,
                    ActivePageIndex: activePageIndex || 0,
                    Pages: pages
                };
            },
            formatNonPrintingText: _formatNonPrintingText,
            findActiveHeadingBlockIdFromRects: function (headings, threshold) {
                var best = null;
                (headings || []).forEach(function (heading) {
                    if (heading.top <= threshold) {
                        best = heading;
                    } else if (!best) {
                        best = heading;
                    }
                });
                return best ? best.id : null;
            },
            _instances: _instances
        },
        insertImageNode: function (instanceId, block, dispatchPatch) {
            var inst = _instances.get(instanceId);
            if (!inst || inst.disposed || inst.readOnly) return;
            _insertImageBlock(inst, block, dispatchPatch !== false);
        },
        copySelection: function (instanceId, writeToClipboard) {
            var inst = _instances.get(instanceId);
            if (!inst || inst.disposed) return null;
            var payload = _serializeSelectionForClipboard(inst);
            if (payload && writeToClipboard !== false) {
                _writeClipboardPayload(payload);
            }
            return payload;
        },
        captureCommentAnchor: captureCommentAnchor,
        upsertComment: upsertComment,
        removeComment: removeComment,
        scrollToComment: scrollToComment,
        reviewAllRevisions: reviewAllRevisions,
        setShowBlocks: function (instanceId, show) {
            var inst = _instances.get(instanceId);
            if (!inst) return;
            if (show) {
                inst.root.classList.add('tm-wysiwyg--show-blocks');
                // Annotate each block with its element type for the CSS label
                inst.root.querySelectorAll('.tm-wysiwyg-block[data-block-id]').forEach(function (block) {
                    var child = block.firstElementChild;
                    if (child) block.setAttribute('data-block-type', child.tagName.toLowerCase());
                });
            } else {
                inst.root.classList.remove('tm-wysiwyg--show-blocks');
            }
        },
        setShowNonPrintingCharacters: function (instanceId, show) {
            var inst = _instances.get(instanceId);
            if (!inst) return;
            inst.showNonPrintingCharacters = !!show;
            _refreshNonPrintingCharacters(inst);
        },
        scrollToBlock: function (instanceId, blockId) {
            var inst = _instances.get(instanceId);
            if (!inst) return;
            var el = inst.root.querySelector('[data-block-id="' + blockId + '"]');
            if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        },
        scrollToPage: function (instanceId, pageIndex) {
            var inst = _instances.get(instanceId);
            if (!inst || !inst.root) return;
            var index = Math.max(0, Number(pageIndex) || 0);
            var page = inst.root.querySelector('.tm-wysiwyg-page[data-page-index="' + index + '"]');
            if ((!page || page.classList.contains('tm-wysiwyg-page--virtual')) && inst.virtualPages && index < inst.virtualPages.length) {
                _scrollVirtualPageToIndex(inst, index);
                page = inst.root.querySelector('.tm-wysiwyg-page[data-page-index="' + index + '"]');
            }
            if (!page && index > 0) {
                page = inst.root.querySelectorAll('.tm-wysiwyg-page-break[data-block-id]')[index - 1] || null;
            }
            if (page) {
                page.scrollIntoView({ behavior: 'smooth', block: 'start' });
                _notifyPageMetrics(inst);
                _notifyActiveHeading(inst);
            }
        },
        getPageMetrics: function (instanceId) {
            var inst = _instances.get(instanceId);
            return _buildPageMetrics(inst);
        },
        setProtectionMode: function (instanceId, isProtected, markers) {
            var inst = _instances.get(instanceId);
            if (!inst) return;
            inst._isProtected = !!isProtected;
            inst._protectedMarkers = markers || [];
            _refreshProtectionMarkers(inst);
        },
        getBodyHtml: function (instanceId) {
            var inst = _instances.get(instanceId);
            if (!inst) return '';
            var body = inst.root.querySelector('.tm-wysiwyg-page__body') || inst.root;
            return body.innerHTML || '';
        },
    };
})();

window.tmDocumentEditorWysiwyg = window.tmDocumentWysiwyg;

window.tmDocumentEditorRuntime = (function () {
    var transactionCallbacks = new Map();
    var selectionCallbacks = new Map();
    var runtimeDocuments = new Map();

    function _engine() {
        return window.tmDocumentEditorWysiwyg || window.tmDocumentWysiwyg || null;
    }

    function _call(methodName, args, fallback) {
        var engine = _engine();
        var method = engine ? engine[methodName] : null;
        if (typeof method !== 'function') {
            if (typeof fallback === 'function') return fallback();
            return undefined;
        }

        return method.apply(engine, args || []);
    }

    function _hasOwn(value, key) {
        return !!value && Object.prototype.hasOwnProperty.call(value, key);
    }

    function _cloneJson(value) {
        if (value === undefined || value === null) return value;
        return JSON.parse(JSON.stringify(value));
    }

    function _readPair(value, pascalKey, camelKey, fallback) {
        if (_hasOwn(value, pascalKey)) return value[pascalKey];
        if (_hasOwn(value, camelKey)) return value[camelKey];
        return fallback;
    }

    function _writePair(value, pascalKey, camelKey, propertyValue) {
        if (!value) return;
        if (_hasOwn(value, camelKey) && !_hasOwn(value, pascalKey)) {
            value[camelKey] = propertyValue;
        } else {
            value[pascalKey] = propertyValue;
        }
    }

    function _ensureArray(value, pascalKey, camelKey) {
        var current = _readPair(value, pascalKey, camelKey, []);
        if (!Array.isArray(current)) current = [];
        _writePair(value, pascalKey, camelKey, current);
        return current;
    }

    function _ensureString(value, pascalKey, camelKey, fallback) {
        var current = _readPair(value, pascalKey, camelKey, fallback || '');
        if (current === undefined || current === null || current === '') current = fallback || '';
        _writePair(value, pascalKey, camelKey, String(current));
        return String(current);
    }

    function _sortObjectDeep(value) {
        if (Array.isArray(value)) {
            return value.map(_sortObjectDeep);
        }

        if (!value || typeof value !== 'object') return value;

        var sorted = {};
        Object.keys(value).sort().forEach(function (key) {
            sorted[key] = _sortObjectDeep(value[key]);
        });
        return sorted;
    }

    function _stableNodeId(prefix, path) {
        return 'rt-' + prefix + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
    }

    function _getSnapshotDocument(snapshot) {
        if (!snapshot) return {};
        return snapshot.Document || snapshot.document || snapshot;
    }

    function _setSnapshotDocument(snapshot, document) {
        if (!snapshot) return;
        if (_hasOwn(snapshot, 'document') && !_hasOwn(snapshot, 'Document')) {
            snapshot.document = document;
        } else {
            snapshot.Document = document;
        }
    }

    function _looksLikeTextInline(inline) {
        if (!inline) return false;
        if (inline.NoteId !== undefined || inline.noteId !== undefined || inline.NoteType !== undefined || inline.noteType !== undefined) return false;
        if (inline.FieldType !== undefined || inline.fieldType !== undefined || inline.FallbackText !== undefined || inline.fallbackText !== undefined) return false;
        if (inline.Key !== undefined || inline.key !== undefined || inline.TokenType !== undefined || inline.tokenType !== undefined) return false;
        var type = String(inline.$type || inline.Type || inline.type || '').toLowerCase();
        return type === 'text' || type === 'textrun' || type.indexOf('text') >= 0 || _hasOwn(inline, 'Text') || _hasOwn(inline, 'text');
    }

    function _readInlineText(inline) {
        return String(_readPair(inline, 'Text', 'text', ''));
    }

    function _writeInlineText(inline, text) {
        _writePair(inline, 'Text', 'text', text);
    }

    function _readInlineMarks(inline) {
        var marks = _readPair(inline, 'Marks', 'marks', []);
        return Array.isArray(marks) ? marks : [];
    }

    function _writeInlineMarks(inline, marks) {
        _writePair(inline, 'Marks', 'marks', marks);
    }

    function _normalizeInline(inline, path) {
        var result = inline ? _cloneJson(inline) : {};
        _ensureString(result, 'Id', 'id', _stableNodeId('inline', path));
        var marks = _readInlineMarks(result).map(function (mark) { return _sortObjectDeep(_cloneJson(mark)); });
        _writeInlineMarks(result, marks);

        if (result.NoteId !== undefined || result.noteId !== undefined || result.NoteType !== undefined || result.noteType !== undefined) {
            if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
                result.$type = 'noteReference';
            }
            return result;
        }

        if (result.FieldType !== undefined || result.fieldType !== undefined || result.FallbackText !== undefined || result.fallbackText !== undefined) {
            if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
                result.$type = 'field';
            }
            return result;
        }

        if (result.Key !== undefined || result.key !== undefined || result.TokenType !== undefined || result.tokenType !== undefined) {
            if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
                result.$type = 'token';
            }
            return result;
        }

        if (_looksLikeTextInline(result)) {
            if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
                result.$type = 'text';
            }
            _writeInlineText(result, _readInlineText(result));
        }

        return result;
    }

    function _inlineMergeKey(inline) {
        var clone = _cloneJson(inline) || {};
        delete clone.Id;
        delete clone.id;
        delete clone.Text;
        delete clone.text;
        return JSON.stringify(_sortObjectDeep(clone));
    }

    function _canMergeInlineRuns(previous, current) {
        return _looksLikeTextInline(previous)
            && _looksLikeTextInline(current)
            && _inlineMergeKey(previous) === _inlineMergeKey(current);
    }

    function _createEmptyTextInline(path) {
        return {
            $type: 'text',
            Id: _stableNodeId('inline', path),
            Marks: [],
            Text: ''
        };
    }

    function _normalizeInlines(inlines, path) {
        var source = Array.isArray(inlines) ? inlines : [];
        var result = [];
        for (var i = 0; i < source.length; i++) {
            var normalized = _normalizeInline(source[i], path + '-' + i);
            var previous = result.length > 0 ? result[result.length - 1] : null;
            if (previous && _canMergeInlineRuns(previous, normalized)) {
                _writeInlineText(previous, _readInlineText(previous) + _readInlineText(normalized));
            } else {
                result.push(normalized);
            }
        }

        if (result.length === 0) {
            result.push(_createEmptyTextInline(path + '-0'));
        }

        return result.map(_sortObjectDeep);
    }

    function _contentKind(content) {
        if (!content) return '';
        var raw = content.$type || content.Type || content.type || '';
        return String(raw).toLowerCase();
    }

    function _looksLikeParagraphContent(content) {
        return !!content
            && (_contentKind(content).indexOf('paragraph') >= 0
                || _hasOwn(content, 'Inlines')
                || _hasOwn(content, 'inlines'));
    }

    function _looksLikeTableContent(content) {
        return !!content
            && (_contentKind(content).indexOf('table') >= 0
                || _hasOwn(content, 'Rows')
                || _hasOwn(content, 'rows'));
    }

    function _normalizeParagraphContent(content, path) {
        var result = content ? _cloneJson(content) : {};
        if (!result.$type && !_hasOwn(result, 'Type') && !_hasOwn(result, 'type')) {
            result.$type = 'paragraph';
        }
        var inlines = _ensureArray(result, 'Inlines', 'inlines');
        _writePair(result, 'Inlines', 'inlines', _normalizeInlines(inlines, path + '-inline'));
        return _sortObjectDeep(result);
    }

    function _normalizeTableContent(content, path) {
        var result = content ? _cloneJson(content) : {};
        var rows = _ensureArray(result, 'Rows', 'rows');
        for (var r = 0; r < rows.length; r++) {
            var row = rows[r] ? _cloneJson(rows[r]) : {};
            _ensureString(row, 'Id', 'id', _stableNodeId('row', path + '-' + r));
            var cells = _ensureArray(row, 'Cells', 'cells');
            for (var c = 0; c < cells.length; c++) {
                var cell = cells[c] ? _cloneJson(cells[c]) : {};
                _ensureString(cell, 'Id', 'id', _stableNodeId('cell', path + '-' + r + '-' + c));
                var blocks = _ensureArray(cell, 'Blocks', 'blocks');
                _writePair(cell, 'Blocks', 'blocks', _normalizeBlocks(blocks, path + '-' + r + '-' + c + '-block'));
                cells[c] = _sortObjectDeep(cell);
            }
            _writePair(row, 'Cells', 'cells', cells);
            rows[r] = _sortObjectDeep(row);
        }
        _writePair(result, 'Rows', 'rows', rows);
        return _sortObjectDeep(result);
    }

    function _normalizeBlockContent(content, path) {
        if (!content) return _normalizeParagraphContent({}, path);
        if (_looksLikeTableContent(content)) return _normalizeTableContent(content, path);
        if (_looksLikeParagraphContent(content)) return _normalizeParagraphContent(content, path);
        return _sortObjectDeep(_cloneJson(content));
    }

    function _normalizeBlock(block, path) {
        var result = block ? _cloneJson(block) : {};
        _ensureString(result, 'Id', 'id', _stableNodeId('block', path));
        var content = _readPair(result, 'Content', 'content', null);
        _writePair(result, 'Content', 'content', _normalizeBlockContent(content, path + '-content'));
        return _sortObjectDeep(result);
    }

    function _normalizeBlocks(blocks, path) {
        var source = Array.isArray(blocks) ? blocks : [];
        return source.map(function (block, index) {
            return _normalizeBlock(block, path + '-' + index);
        });
    }

    function _normalizeHeaderFooter(headerFooter, path) {
        var result = headerFooter ? _cloneJson(headerFooter) : {};
        _ensureString(result, 'Id', 'id', _stableNodeId('header-footer', path));
        var blocks = _ensureArray(result, 'Blocks', 'blocks');
        _writePair(result, 'Blocks', 'blocks', _normalizeBlocks(blocks, path + '-block'));
        return _sortObjectDeep(result);
    }

    function _normalizeDocument(document) {
        var result = document ? _cloneJson(document) : {};
        if (_hasOwn(result, 'document') || _hasOwn(result, 'Document')) {
            result = _getSnapshotDocument(result);
        }

        _writePair(result, 'SchemaVersion', 'schemaVersion', _readPair(result, 'SchemaVersion', 'schemaVersion', 1) || 1);
        _ensureString(result, 'DocumentId', 'documentId', 'document');
        _writePair(result, 'Metadata', 'metadata', _readPair(result, 'Metadata', 'metadata', {}) || {});
        _writePair(result, 'PageSettings', 'pageSettings', _readPair(result, 'PageSettings', 'pageSettings', {}) || {});

        var sections = _ensureArray(result, 'Sections', 'sections');
        _writePair(result, 'Sections', 'sections', sections.map(function (section, index) {
            var normalized = section ? _cloneJson(section) : {};
            _ensureString(normalized, 'Id', 'id', _stableNodeId('section', index));
            return _sortObjectDeep(normalized);
        }));

        var blocks = _ensureArray(result, 'Blocks', 'blocks');
        _writePair(result, 'Blocks', 'blocks', _normalizeBlocks(blocks, 'block'));

        var comments = _ensureArray(result, 'Comments', 'comments');
        _writePair(result, 'Comments', 'comments', comments.map(function (comment) { return _sortObjectDeep(_cloneJson(comment)); }));

        var notes = _ensureArray(result, 'Notes', 'notes');
        _writePair(result, 'Notes', 'notes', notes.map(function (note) { return _sortObjectDeep(_cloneJson(note)); }));

        var headersFooters = _ensureArray(result, 'HeadersFooters', 'headersFooters');
        _writePair(result, 'HeadersFooters', 'headersFooters', headersFooters.map(_normalizeHeaderFooter));

        var revisions = _ensureArray(result, 'Revisions', 'revisions');
        _writePair(result, 'Revisions', 'revisions', revisions.map(function (revision) { return _sortObjectDeep(_cloneJson(revision)); }));

        var assets = _ensureArray(result, 'Assets', 'assets');
        _writePair(result, 'Assets', 'assets', assets.map(function (asset) { return _sortObjectDeep(_cloneJson(asset)); }));

        var anchors = _ensureArray(result, 'Anchors', 'anchors');
        _writePair(result, 'Anchors', 'anchors', anchors.map(function (anchor) { return _sortObjectDeep(_cloneJson(anchor)); }));

        return _sortObjectDeep(result);
    }

    function fromCanonicalDocument(document) {
        return _sortObjectDeep({
            version: 1,
            document: _normalizeDocument(document)
        });
    }

    function toCanonicalDocument(runtimeDocument) {
        if (!runtimeDocument) return _normalizeDocument({});
        var document = _hasOwn(runtimeDocument, 'document') || _hasOwn(runtimeDocument, 'Document')
            ? _getSnapshotDocument(runtimeDocument)
            : runtimeDocument;
        return _normalizeDocument(document);
    }

    function _normalizeSnapshot(snapshot) {
        var result = snapshot ? _cloneJson(snapshot) : {};
        var document = _getSnapshotDocument(result);
        _setSnapshotDocument(result, toCanonicalDocument(document));
        if (!_hasOwn(result, 'ProtocolVersion') && !_hasOwn(result, 'protocolVersion')) {
            result.ProtocolVersion = 1;
        }
        return _sortObjectDeep(result);
    }

    function _storeSnapshotRuntime(instanceId, snapshot) {
        if (!instanceId || !snapshot) return;
        runtimeDocuments.set(instanceId, fromCanonicalDocument(_getSnapshotDocument(snapshot)));
    }

    function _snapshotFromRuntime(instanceId) {
        var runtimeDocument = runtimeDocuments.get(instanceId);
        if (!runtimeDocument) return null;
        return _sortObjectDeep({
            ProtocolVersion: 1,
            Document: toCanonicalDocument(runtimeDocument)
        });
    }

    function _stripRuntimeFields(value) {
        if (Array.isArray(value)) return value.map(_stripRuntimeFields);
        if (!value || typeof value !== 'object') return value;

        var result = {};
        Object.keys(value).sort().forEach(function (key) {
            if (key.indexOf('__runtime') === 0 || key.indexOf('_runtime') === 0) return;
            result[key] = _stripRuntimeFields(value[key]);
        });
        return result;
    }

    function _findFirstDifference(expected, actual, path) {
        if (expected === actual) return null;
        if (typeof expected !== typeof actual) {
            return { path: path || '$', expected: expected, actual: actual };
        }
        if (expected === null || actual === null || typeof expected !== 'object') {
            return { path: path || '$', expected: expected, actual: actual };
        }

        var expectedKeys = Array.isArray(expected) ? expected.map(function (_, index) { return index; }) : Object.keys(expected).sort();
        var actualKeys = Array.isArray(actual) ? actual.map(function (_, index) { return index; }) : Object.keys(actual).sort();
        var keys = Array.from(new Set(expectedKeys.concat(actualKeys))).sort(function (a, b) {
            return String(a).localeCompare(String(b), undefined, { numeric: true });
        });

        for (var i = 0; i < keys.length; i++) {
            var key = keys[i];
            if (!_hasOwn(expected, key) || !_hasOwn(actual, key)) {
                return { path: (path || '$') + '.' + key, expected: expected[key], actual: actual[key] };
            }
            var diff = _findFirstDifference(expected[key], actual[key], (path || '$') + '.' + key);
            if (diff) return diff;
        }

        return null;
    }

    function diffCanonicalDocuments(expected, actual) {
        var left = _stripRuntimeFields(toCanonicalDocument(expected));
        var right = _stripRuntimeFields(toCanonicalDocument(actual));
        var diff = _findFirstDifference(left, right, '$');
        return diff || { equal: true, path: '$', expected: left, actual: right };
    }

    function roundTripCanonicalDocument(document) {
        return toCanonicalDocument(fromCanonicalDocument(document));
    }

    function create(root, options, dotNetRef) {
        return _call('create', [root, options, dotNetRef]);
    }

    function loadDocument(instanceId, snapshot, forceRender) {
        var normalizedSnapshot = _normalizeSnapshot(snapshot);
        _storeSnapshotRuntime(instanceId, normalizedSnapshot);
        return _call('applySnapshot', [instanceId, normalizedSnapshot, forceRender]);
    }

    function getDocument(instanceId) {
        var engineSnapshot = _call('getSnapshot', [instanceId], function () { return null; });
        if (engineSnapshot) {
            try {
                var parsed = typeof engineSnapshot === 'string' ? JSON.parse(engineSnapshot) : engineSnapshot;
                var normalized = _normalizeSnapshot(parsed);
                _storeSnapshotRuntime(instanceId, normalized);
                return typeof engineSnapshot === 'string' ? JSON.stringify(normalized) : normalized;
            } catch {
                return engineSnapshot;
            }
        }

        var runtimeSnapshot = _snapshotFromRuntime(instanceId);
        return runtimeSnapshot ? JSON.stringify(runtimeSnapshot) : null;
    }

    function executeCommand(instanceId, command, payload) {
        return _call('executeCommand', [instanceId, command, payload]);
    }

    function onTransactionCommitted(instanceId, callback) {
        if (!transactionCallbacks.has(instanceId)) {
            transactionCallbacks.set(instanceId, []);
        }
        transactionCallbacks.get(instanceId).push(callback);
        return function () {
            var callbacks = transactionCallbacks.get(instanceId) || [];
            transactionCallbacks.set(instanceId, callbacks.filter(function (item) { return item !== callback; }));
        };
    }

    function onSelectionStateChanged(instanceId, callback) {
        if (!selectionCallbacks.has(instanceId)) {
            selectionCallbacks.set(instanceId, []);
        }
        selectionCallbacks.get(instanceId).push(callback);
        return function () {
            var callbacks = selectionCallbacks.get(instanceId) || [];
            selectionCallbacks.set(instanceId, callbacks.filter(function (item) { return item !== callback; }));
        };
    }

    function dispose(instanceId) {
        transactionCallbacks.delete(instanceId);
        selectionCallbacks.delete(instanceId);
        runtimeDocuments.delete(instanceId);
        return _call('dispose', [instanceId]);
    }

    // Phase 5: public API remains a stable facade; these internal modules are
    // implementation boundaries for tests and refactors, not a public contract.
    var runtimeModules = {
        core: {
            create: create,
            loadDocument: loadDocument,
            getDocument: getDocument,
            executeCommand: executeCommand,
            dispose: dispose,
            call: _call
        },
        selection: {
            onSelectionStateChanged: onSelectionStateChanged,
            restoreSelection: function (instanceId, snapshot) {
                return _call('restoreSelection', [instanceId, snapshot]);
            },
            getRuntimeSelection: function (instanceId) {
                return _call('getRuntimeSelection', [instanceId], function () { return null; });
            },
            getSelectionSnapshot: function (instanceId) {
                return _call('getSelectionSnapshot', [instanceId], function () { return null; });
            }
        },
        rendering: {
            loadDocument: loadDocument,
            applyRemoteOperation: function (instanceId, operation) {
                return _call('applyRemoteOperation', [instanceId, operation]);
            },
            applyRemoteOperationBatch: function (instanceId, batch) {
                return _call('applyRemoteOperationBatch', [instanceId, batch]);
            },
            applyRemoteOperations: function (instanceId, operations) {
                return _call('applyRemoteOperations', [instanceId, operations]);
            },
            applyRemoteCursor: function (instanceId, cursor) {
                return _call('applyRemoteCursor', [instanceId, cursor], function () { return false; });
            },
            getDebugSnapshot: function (instanceId) {
                return _call('getDebugSnapshot', [instanceId], function () { return null; });
            },
            getPageMetrics: function (instanceId) {
                return _call('getPageMetrics', [instanceId], function () { return null; });
            }
        },
        input: {
            focus: function (instanceId) {
                return _call('focus', [instanceId]);
            },
            closeHeaderFooter: function (instanceId) {
                return _call('closeHeaderFooter', [instanceId], function () { return false; });
            }
        },
        formatting: {
            executeCommand: executeCommand,
            getFormattingState: function (instanceId) {
                return _call('getFormattingState', [instanceId], function () { return null; });
            },
            getLastCommandTransaction: function (instanceId) {
                return _call('getLastCommandTransaction', [instanceId], function () { return null; });
            },
            getUndoState: function (instanceId) {
                return _call('getUndoState', [instanceId], function () { return null; });
            },
            getDebugUndoStack: function (instanceId) {
                return _call('getDebugUndoStack', [instanceId], function () { return null; });
            },
            undo: function (instanceId) {
                return _call('undo', [instanceId], function () { return false; });
            },
            redo: function (instanceId) {
                return _call('redo', [instanceId], function () { return false; });
            }
        },
        clipboard: {
            getLinkInfo: function (instanceId) {
                return _call('getLinkInfo', [instanceId], function () { return null; });
            }
        },
        image: {
            executeCommand: executeCommand,
            insertImageNode: function (instanceId, block, dispatchPatch) {
                return _call('insertImageNode', [instanceId, block, dispatchPatch]);
            },
            insertImageUrl: function (instanceId, payload) {
                return executeCommand(instanceId, 'insertImageUrl', payload || {});
            }
        },
        table: {
            executeCommand: executeCommand,
            insertTable: function (instanceId, payload) {
                return executeCommand(instanceId, 'insertTable', payload || {});
            }
        },
        comments: {
            captureCommentAnchor: function (instanceId) {
                return _call('captureCommentAnchor', [instanceId], function () { return null; });
            },
            scrollToComment: function (instanceId, commentId) {
                return _call('scrollToComment', [instanceId, commentId], function () { return false; });
            },
            upsertComment: function (instanceId, comment) {
                return _call('upsertComment', [instanceId, comment], function () { return false; });
            },
            removeComment: function (instanceId, commentId) {
                return _call('removeComment', [instanceId, commentId], function () { return false; });
            }
        },
        revisions: {
            setTrackChangesEnabled: function (instanceId, enabled) {
                return _call('setTrackChangesEnabled', [instanceId, enabled]);
            },
            setReviewDisplayMode: function (instanceId, mode) {
                return _call('setReviewDisplayMode', [instanceId, mode]);
            },
            scrollToRevision: function (instanceId, revisionId) {
                return _call('scrollToRevision', [instanceId, revisionId]);
            },
            reviewRevision: function (instanceId, revisionId, action) {
                return _call('reviewRevision', [instanceId, revisionId, action], function () { return false; });
            },
            reviewAllRevisions: function (instanceId, action, payload) {
                return _call('reviewAllRevisions', [instanceId, action, payload], function () { return false; });
            },
            clearRevisionDecorations: function (instanceId, revisionId, removeContent) {
                return _call('clearRevisionDecorations', [instanceId, revisionId, removeContent]);
            }
        },
        serialization: {
            fromCanonicalDocument: fromCanonicalDocument,
            toCanonicalDocument: toCanonicalDocument,
            roundTripCanonicalDocument: roundTripCanonicalDocument,
            diffCanonicalDocuments: diffCanonicalDocuments,
            normalizeSnapshot: _normalizeSnapshot
        },
        watchdog: {
            getState: function () { return null; }
        }
    };

    function getRuntimeModuleNames() {
        return Object.keys(runtimeModules).sort();
    }

    return {
        create: runtimeModules.core.create,
        loadDocument: runtimeModules.core.loadDocument,
        getDocument: runtimeModules.core.getDocument,
        executeCommand: runtimeModules.core.executeCommand,
        onTransactionCommitted: onTransactionCommitted,
        onSelectionStateChanged: runtimeModules.selection.onSelectionStateChanged,
        dispose: runtimeModules.core.dispose,
        applyRemoteOperation: function (instanceId, operation) {
            return runtimeModules.rendering.applyRemoteOperation(instanceId, operation);
        },
        applyRemoteOperationBatch: function (instanceId, batch) {
            return runtimeModules.rendering.applyRemoteOperationBatch(instanceId, batch);
        },
        applyRemoteOperations: function (instanceId, operations) {
            return runtimeModules.rendering.applyRemoteOperations(instanceId, operations);
        },
        applyRemoteCursor: function (instanceId, cursor) {
            return runtimeModules.rendering.applyRemoteCursor(instanceId, cursor);
        },
        setTrackChangesEnabled: function (instanceId, enabled) {
            return runtimeModules.revisions.setTrackChangesEnabled(instanceId, enabled);
        },
        setReviewDisplayMode: function (instanceId, mode) {
            return runtimeModules.revisions.setReviewDisplayMode(instanceId, mode);
        },
        setReadOnly: function (instanceId, readOnly) {
            return _call('setReadOnly', [instanceId, readOnly]);
        },
        scrollToRevision: function (instanceId, revisionId) {
            return runtimeModules.revisions.scrollToRevision(instanceId, revisionId);
        },
        scrollToComment: function (instanceId, commentId) {
            return runtimeModules.comments.scrollToComment(instanceId, commentId);
        },
        upsertComment: function (instanceId, comment) {
            return runtimeModules.comments.upsertComment(instanceId, comment);
        },
        removeComment: function (instanceId, commentId) {
            return runtimeModules.comments.removeComment(instanceId, commentId);
        },
        reviewRevision: function (instanceId, revisionId, action) {
            return runtimeModules.revisions.reviewRevision(instanceId, revisionId, action);
        },
        reviewAllRevisions: function (instanceId, action, payload) {
            return runtimeModules.revisions.reviewAllRevisions(instanceId, action, payload);
        },
        clearRevisionDecorations: function (instanceId, revisionId, removeContent) {
            return runtimeModules.revisions.clearRevisionDecorations(instanceId, revisionId, removeContent);
        },
        restoreSelection: function (instanceId, snapshot) {
            return runtimeModules.selection.restoreSelection(instanceId, snapshot);
        },
        focus: function (instanceId) {
            return runtimeModules.input.focus(instanceId);
        },
        closeHeaderFooter: function (instanceId) {
            return runtimeModules.input.closeHeaderFooter(instanceId);
        },
        captureCommentAnchor: function (instanceId) {
            return runtimeModules.comments.captureCommentAnchor(instanceId);
        },
        getDebugSnapshot: function (instanceId) {
            return runtimeModules.rendering.getDebugSnapshot(instanceId);
        },
        getPageMetrics: function (instanceId) {
            return runtimeModules.rendering.getPageMetrics(instanceId);
        },
        getFormattingState: function (instanceId) {
            return runtimeModules.formatting.getFormattingState(instanceId);
        },
        getLastCommandTransaction: function (instanceId) {
            return runtimeModules.formatting.getLastCommandTransaction(instanceId);
        },
        getUndoState: function (instanceId) {
            return runtimeModules.formatting.getUndoState(instanceId);
        },
        getDebugUndoStack: function (instanceId) {
            return runtimeModules.formatting.getDebugUndoStack(instanceId);
        },
        getDirtyState: function (instanceId) {
            return _call('getDirtyState', [instanceId], function () { return null; });
        },
        markSaved: function (instanceId, marker) {
            return _call('markSaved', [instanceId, marker], function () { return false; });
        },
        getOfflineState: function (instanceId) {
            return _call('getOfflineState', [instanceId], function () { return null; });
        },
        applyOfflineState: function (instanceId, stateJson) {
            return _call('applyOfflineState', [instanceId, stateJson], function () { return false; });
        },
        undo: function (instanceId) {
            return runtimeModules.formatting.undo(instanceId);
        },
        redo: function (instanceId) {
            return runtimeModules.formatting.redo(instanceId);
        },
        getRuntimeSelection: function (instanceId) {
            return runtimeModules.selection.getRuntimeSelection(instanceId);
        },
        getSelectionSnapshot: function (instanceId) {
            return runtimeModules.selection.getSelectionSnapshot(instanceId);
        },
        getLinkInfo: function (instanceId) {
            return runtimeModules.clipboard.getLinkInfo(instanceId);
        },
        insertImageNode: function (instanceId, block, dispatchPatch) {
            return runtimeModules.image.insertImageNode(instanceId, block, dispatchPatch);
        },
        __internal: {
            version: 1,
            modules: runtimeModules,
            getModuleNames: getRuntimeModuleNames
        },
        __testHooks: {
            fromCanonicalDocument: fromCanonicalDocument,
            toCanonicalDocument: toCanonicalDocument,
            roundTripCanonicalDocument: roundTripCanonicalDocument,
            diffCanonicalDocuments: diffCanonicalDocuments,
            getRuntimeDocument: function (instanceId) {
                return runtimeDocuments.has(instanceId) ? _cloneJson(runtimeDocuments.get(instanceId)) : null;
            },
            createRuntimeCommandTransaction: function (command, payload, beforeSelection, afterSelection, beforeFormatting, afterFormatting) {
                return _engine().__testHooks.createRuntimeCommandTransaction(
                    command,
                    payload,
                    beforeSelection,
                    afterSelection,
                    beforeFormatting,
                    afterFormatting);
            },
            normalizeSnapshot: _normalizeSnapshot,
            normalizeWrapMode: function (value) {
                return _engine().__testHooks.normalizeWrapMode(value);
            },
            normalizeHorizontalPosition: function (value) {
                return _engine().__testHooks.normalizeHorizontalPosition(value);
            }
        }
    };
})();

// Phase 12: Watchdog — wraps tmDocumentEditorRuntime with error recovery
(function () {
    'use strict';

    var runtime = window.tmDocumentEditorRuntime;
    if (!runtime) return;

    var WD_READY = 'ready';
    var WD_RECOVERING = 'recovering';
    var WD_RECOVERED = 'recovered';
    var WD_FAILED = 'failed';
    var WD_DEFAULT_MAX_ATTEMPTS = 3;
    var WD_DEFAULT_BACKOFF_MS = 100;

    var _watchdogContexts = new Map();

    function _wdGet(instanceId) {
        return _watchdogContexts.get(instanceId) || null;
    }

    function _cloneWatchdogJson(value) {
        if (value == null) return value;
        try { return JSON.parse(JSON.stringify(value)); } catch { return value; }
    }

    function _parseWatchdogJson(value) {
        if (value == null || value === '') return null;
        if (typeof value === 'string') {
            try { return JSON.parse(value); } catch { return value; }
        }

        return _cloneWatchdogJson(value);
    }

    function _safeCall(fn, fallback) {
        try {
            var value = fn();
            return value === undefined ? fallback : value;
        } catch {
            return fallback;
        }
    }

    function _watchdogNow() {
        try { return new Date().toISOString(); } catch { return ''; }
    }

    function _notifyDotNet(wd, methodName, detail) {
        if (!wd || !wd.dotNetRef) return;
        try {
            wd.dotNetRef.invokeMethodAsync(methodName, detail || wd.lastRecoveryDetail || null);
        } catch {}
    }

    function _recordWatchdogEvent(wd, eventName, source, error, extra) {
        if (!wd) return null;
        var detail = Object.assign({
            event: eventName || '',
            Event: eventName || '',
            source: source || wd.lastErrorSource || '',
            Source: source || wd.lastErrorSource || '',
            state: wd.state || '',
            State: wd.state || '',
            attempt: wd.attempt || 0,
            Attempt: wd.attempt || 0,
            maxAttempts: wd.maxAttempts || WD_DEFAULT_MAX_ATTEMPTS,
            MaxAttempts: wd.maxAttempts || WD_DEFAULT_MAX_ATTEMPTS,
            backoffMs: wd.currentBackoffMs || 0,
            BackoffMs: wd.currentBackoffMs || 0,
            usedSnapshotFallback: !!wd.usedSnapshotFallback,
            UsedSnapshotFallback: !!wd.usedSnapshotFallback,
            errorMessage: error && error.message ? String(error.message) : (error ? String(error) : ''),
            ErrorMessage: error && error.message ? String(error.message) : (error ? String(error) : ''),
            timestamp: _watchdogNow(),
            Timestamp: _watchdogNow()
        }, extra || {});

        wd.lastRecoveryDetail = detail;
        wd.events.push(detail);
        if (wd.events.length > 20) {
            wd.events = wd.events.slice(wd.events.length - 20);
        }

        return detail;
    }

    function _readMarkers(instanceId) {
        if (typeof runtime.getMarkers === 'function') {
            return _safeCall(function () { return runtime.getMarkers(instanceId); }, []);
        }

        return _safeCall(function () {
            return runtime.__internal.modules.core.call('getMarkers', [instanceId], function () { return []; });
        }, []);
    }

    function _readUploadState(debugSnapshot) {
        var pendingUploads = debugSnapshot && (debugSnapshot.PendingUploads || debugSnapshot.pendingUploads);
        var pendingUploadCount = debugSnapshot
            ? Number(debugSnapshot.PendingUploadCount ?? debugSnapshot.pendingUploadCount ?? (Array.isArray(pendingUploads) ? pendingUploads.length : 0))
            : 0;
        return {
            pendingUploadCount: pendingUploadCount || 0,
            PendingUploadCount: pendingUploadCount || 0,
            pendingUploads: Array.isArray(pendingUploads) ? _cloneWatchdogJson(pendingUploads) : [],
            PendingUploads: Array.isArray(pendingUploads) ? _cloneWatchdogJson(pendingUploads) : []
        };
    }

    function _captureStableSnapshot(instanceId, reason) {
        var debugSnapshot = _safeCall(function () { return runtime.getDebugSnapshot(instanceId); }, null);
        var snapshot = {
            capturedAt: _watchdogNow(),
            CapturedAt: _watchdogNow(),
            reason: reason || '',
            Reason: reason || '',
            document: _parseWatchdogJson(_safeCall(function () { return _origGetDocument(instanceId); }, null)),
            Document: null,
            markers: _cloneWatchdogJson(_readMarkers(instanceId) || []),
            Markers: null,
            selection: _cloneWatchdogJson(
                _safeCall(function () { return runtime.getSelectionSnapshot(instanceId); }, null)
                || _safeCall(function () { return runtime.getRuntimeSelection(instanceId); }, null)),
            Selection: null,
            undoState: _cloneWatchdogJson(_safeCall(function () { return runtime.getUndoState(instanceId); }, null)),
            UndoState: null,
            undoDebug: _cloneWatchdogJson(_safeCall(function () { return runtime.getDebugUndoStack(instanceId); }, null)),
            UndoDebug: null,
            uploadState: _readUploadState(debugSnapshot),
            UploadState: null
        };
        snapshot.Document = snapshot.document;
        snapshot.Markers = snapshot.markers;
        snapshot.Selection = snapshot.selection;
        snapshot.UndoState = snapshot.undoState;
        snapshot.UndoDebug = snapshot.undoDebug;
        snapshot.UploadState = snapshot.uploadState;
        return snapshot;
    }

    function _rememberStableSnapshot(instanceId, wd, reason) {
        if (!wd) return null;
        var snapshot = _captureStableSnapshot(instanceId, reason);
        if (snapshot && snapshot.document) {
            wd.stableSnapshot = snapshot;
        }

        return wd.stableSnapshot;
    }

    function _rememberStableSnapshotFromDocument(instanceId, wd, reason, documentSnapshot) {
        if (!wd) return null;
        var document = _parseWatchdogJson(documentSnapshot);
        if (!document) return wd.stableSnapshot;
        var debugSnapshot = _safeCall(function () { return runtime.getDebugSnapshot(instanceId); }, null);
        var snapshot = {
            capturedAt: _watchdogNow(),
            CapturedAt: _watchdogNow(),
            reason: reason || '',
            Reason: reason || '',
            document: document,
            Document: document,
            markers: _cloneWatchdogJson(_readMarkers(instanceId) || []),
            Markers: null,
            selection: _cloneWatchdogJson(
                _safeCall(function () { return runtime.getSelectionSnapshot(instanceId); }, null)
                || _safeCall(function () { return runtime.getRuntimeSelection(instanceId); }, null)),
            Selection: null,
            undoState: _cloneWatchdogJson(_safeCall(function () { return runtime.getUndoState(instanceId); }, null)),
            UndoState: null,
            undoDebug: _cloneWatchdogJson(_safeCall(function () { return runtime.getDebugUndoStack(instanceId); }, null)),
            UndoDebug: null,
            uploadState: _readUploadState(debugSnapshot),
            UploadState: null
        };
        snapshot.Markers = snapshot.markers;
        snapshot.Selection = snapshot.selection;
        snapshot.UndoState = snapshot.undoState;
        snapshot.UndoDebug = snapshot.undoDebug;
        snapshot.UploadState = snapshot.uploadState;
        wd.stableSnapshot = snapshot;
        return wd.stableSnapshot;
    }

    function _restoreStableSnapshotExtras(instanceId, stableSnapshot) {
        if (!stableSnapshot) return;
        var markers = stableSnapshot.markers || stableSnapshot.Markers || [];
        if (Array.isArray(markers)) {
            markers.forEach(function (marker) {
                _safeCall(function () {
                    if (typeof runtime.upsertMarker === 'function') {
                        return runtime.upsertMarker(instanceId, marker);
                    }

                    return runtime.__internal.modules.core.call('upsertMarker', [instanceId, marker], function () { return null; });
                }, null);
            });
        }

        var selection = stableSnapshot.selection || stableSnapshot.Selection || null;
        if (selection) {
            _safeCall(function () { return runtime.restoreSelection(instanceId, selection); }, null);
        }
    }

    function _captureRecoveryState(instanceId, wd) {
        var snapshot = wd.forceSnapshotFallback
            ? null
            : _parseWatchdogJson(_safeCall(function () { return _origGetDocument(instanceId); }, null));
        var offlineState = _safeCall(function () { return _origGetOfflineState(instanceId); }, null);
        var stableSnapshot = null;
        wd.usedSnapshotFallback = false;

        if (snapshot) {
            stableSnapshot = _captureStableSnapshot(instanceId, 'recovery-live');
            stableSnapshot.document = snapshot;
            stableSnapshot.Document = snapshot;
        } else if (wd.stableSnapshot) {
            stableSnapshot = _cloneWatchdogJson(wd.stableSnapshot);
            snapshot = stableSnapshot.document || stableSnapshot.Document || null;
            wd.usedSnapshotFallback = !!snapshot;
            if (wd.usedSnapshotFallback) {
                _recordWatchdogEvent(wd, 'snapshotFallbackUsed', wd.lastErrorSource, null, { usedSnapshotFallback: true, UsedSnapshotFallback: true });
            }
        }

        return {
            snapshot: snapshot,
            offlineState: offlineState,
            stableSnapshot: stableSnapshot
        };
    }

    function _failRecovery(instanceId, wd, source, error) {
        wd.state = WD_FAILED;
        wd.currentBackoffMs = 0;
        var detail = _recordWatchdogEvent(wd, 'runtimeRecoveryFailed', source, error);
        _notifyDotNet(wd, 'HandleRuntimeRecoveryFailed', detail);
    }

    function _attemptRecovery(instanceId, wd) {
        if (!wd || wd.state !== WD_RECOVERING) return;
        var recoveryState = _captureRecoveryState(instanceId, wd);

        try { _origDispose(instanceId); } catch {}

        try {
            if (wd.forceRecoveryFailure) {
                throw new Error('Forced watchdog recovery failure');
            }

            _origCreate(wd.rootEl, wd.options, wd.dotNetRef);
        } catch (error) {
            if (wd.attempt < wd.maxAttempts) {
                wd.state = WD_READY;
                _scheduleRecovery(instanceId, wd, wd.lastErrorSource || 'unknown', error);
                return;
            }

            _failRecovery(instanceId, wd, wd.lastErrorSource || 'unknown', error);
            return;
        }

        try { if (recoveryState.snapshot) _origLoadDocument(instanceId, recoveryState.snapshot); } catch {}
        try { if (recoveryState.offlineState) _origApplyOfflineState(instanceId, recoveryState.offlineState); } catch {}
        _restoreStableSnapshotExtras(instanceId, recoveryState.stableSnapshot);
        if (recoveryState.stableSnapshot) {
            wd.stableSnapshot = _cloneWatchdogJson(recoveryState.stableSnapshot);
        }

        wd.state = WD_RECOVERED;
        wd.currentBackoffMs = 0;
        var detail = _recordWatchdogEvent(wd, 'runtimeRecovered', wd.lastErrorSource || 'unknown', null);
        _notifyDotNet(wd, 'HandleRuntimeRecovered', detail);
    }

    function _scheduleRecovery(instanceId, wd, source, error) {
        if (!wd || wd.state === WD_RECOVERING) return;
        if (wd.attempt >= wd.maxAttempts) {
            _failRecovery(instanceId, wd, source, error);
            return;
        }

        wd.state = WD_RECOVERING;
        wd.lastErrorSource = source || 'unknown';
        wd.attempt += 1;
        wd.currentBackoffMs = Math.max(0, wd.baseBackoffMs || WD_DEFAULT_BACKOFF_MS) * Math.pow(2, Math.max(0, wd.attempt - 1));
        _recordWatchdogEvent(wd, 'runtimeRecoveryScheduled', source, error);
        setTimeout(function () { _attemptRecovery(instanceId, wd); }, wd.currentBackoffMs);
    }

    var _origCreate = runtime.create;
    var _origLoadDocument = runtime.loadDocument;
    var _origGetDocument = runtime.getDocument;
    var _origGetOfflineState = runtime.getOfflineState;
    var _origApplyOfflineState = runtime.applyOfflineState;
    runtime.create = function (rootEl, options, dotNetRef) {
        var instanceId = options && (options.InstanceId || options.instanceId || '');
        var result = _origCreate.apply(runtime, arguments);
        if (instanceId) {
            var wd = {
                state: WD_READY,
                rootEl: rootEl,
                options: options,
                dotNetRef: dotNetRef || null,
                stableSnapshot: null,
                events: [],
                lastRecoveryDetail: null,
                lastErrorSource: '',
                attempt: 0,
                maxAttempts: Number(options && (options.WatchdogMaxAttempts ?? options.watchdogMaxAttempts) || WD_DEFAULT_MAX_ATTEMPTS),
                baseBackoffMs: Number(options && (options.WatchdogBackoffMs ?? options.watchdogBackoffMs) || WD_DEFAULT_BACKOFF_MS),
                currentBackoffMs: 0,
                usedSnapshotFallback: false,
                forceRecoveryFailure: false,
                forceSnapshotFallback: false
            };
            _watchdogContexts.set(String(instanceId), wd);
        }
        return result;
    };

    var _origDispose = runtime.dispose;
    runtime.dispose = function (instanceId) {
        _watchdogContexts.delete(String(instanceId || ''));
        return _origDispose.apply(runtime, arguments);
    };

    runtime.loadDocument = function (instanceId) {
        try {
            var result = _origLoadDocument.apply(runtime, arguments);
            var wd = _wdGet(String(instanceId || ''));
            if (wd) {
                wd.state = WD_READY;
                wd.attempt = 0;
                _rememberStableSnapshotFromDocument(String(instanceId || ''), wd, 'loadDocument', arguments[1]);
            }

            return result;
        } catch (error) {
            var wd = _wdGet(String(instanceId || ''));
            if (wd && wd.state !== WD_RECOVERING) {
                _scheduleRecovery(String(instanceId || ''), wd, 'render', error);
            }
            return undefined;
        }
    };

    runtime.getDocument = function (instanceId) {
        try {
            return _origGetDocument.apply(runtime, arguments);
        } catch (error) {
            var wd = _wdGet(String(instanceId || ''));
            if (wd && wd.state !== WD_RECOVERING) {
                _scheduleRecovery(String(instanceId || ''), wd, 'serialization', error);
            }
            return wd && wd.stableSnapshot && wd.stableSnapshot.document
                ? JSON.stringify(wd.stableSnapshot.document)
                : null;
        }
    };

    var _origExecuteCommand = runtime.executeCommand;
    runtime.executeCommand = function (instanceId, command, payload) {
        try {
            var result = _origExecuteCommand.apply(runtime, arguments);
            var wd = _wdGet(String(instanceId || ''));
            if (wd) {
                wd.state = WD_READY;
                wd.attempt = 0;
                _rememberStableSnapshot(String(instanceId || ''), wd, 'command');
            }

            return result;
        } catch (error) {
            var wd = _wdGet(String(instanceId || ''));
            if (wd && wd.state !== WD_RECOVERING) {
                _scheduleRecovery(String(instanceId || ''), wd, 'command', error);
            }
            return undefined;
        }
    };

    var _origApplyBatch = runtime.applyRemoteOperationBatch;
    runtime.applyRemoteOperationBatch = function (instanceId, batch) {
        try {
            var result = _origApplyBatch.apply(runtime, arguments);
            var wd = _wdGet(String(instanceId || ''));
            if (wd) {
                wd.state = WD_READY;
                wd.attempt = 0;
                _rememberStableSnapshot(String(instanceId || ''), wd, 'remoteOperation');
            }

            return result;
        } catch (error) {
            var wd = _wdGet(String(instanceId || ''));
            if (wd && wd.state !== WD_RECOVERING) {
                _scheduleRecovery(String(instanceId || ''), wd, 'remoteOperation', error);
            }
            return undefined;
        }
    };

    var _origApplyRemoteOperation = runtime.applyRemoteOperation;
    runtime.applyRemoteOperation = function (instanceId) {
        try {
            var result = _origApplyRemoteOperation.apply(runtime, arguments);
            var wd = _wdGet(String(instanceId || ''));
            if (wd) _rememberStableSnapshot(String(instanceId || ''), wd, 'remoteOperation');
            return result;
        } catch (error) {
            var wd = _wdGet(String(instanceId || ''));
            if (wd && wd.state !== WD_RECOVERING) {
                _scheduleRecovery(String(instanceId || ''), wd, 'remoteOperation', error);
            }
            return undefined;
        }
    };

    runtime.__watchdog = {
        getState: function (instanceId) {
            var wd = _wdGet(String(instanceId || ''));
            return wd ? wd.state : null;
        },
        getStableSnapshot: function (instanceId) {
            var wd = _wdGet(String(instanceId || ''));
            return wd ? _cloneWatchdogJson(wd.stableSnapshot) : null;
        },
        getLastRecoveryDetail: function (instanceId) {
            var wd = _wdGet(String(instanceId || ''));
            return wd ? _cloneWatchdogJson(wd.lastRecoveryDetail) : null;
        },
        getEvents: function (instanceId) {
            var wd = _wdGet(String(instanceId || ''));
            return wd ? _cloneWatchdogJson(wd.events || []) : [];
        },
        configure: function (instanceId, options) {
            var wd = _wdGet(String(instanceId || ''));
            if (!wd) return false;
            if (options && options.maxAttempts != null) wd.maxAttempts = Number(options.maxAttempts) || wd.maxAttempts;
            if (options && options.baseBackoffMs != null) wd.baseBackoffMs = Number(options.baseBackoffMs) || wd.baseBackoffMs;
            if (options && options.forceRecoveryFailure != null) wd.forceRecoveryFailure = !!options.forceRecoveryFailure;
            if (options && options.forceSnapshotFallback != null) wd.forceSnapshotFallback = !!options.forceSnapshotFallback;
            return true;
        },
        simulateCrash: function (instanceId, source, options) {
            var wd = _wdGet(String(instanceId || ''));
            if (!wd) return false;
            if (options) {
                runtime.__watchdog.configure(instanceId, options);
            }
            _scheduleRecovery(String(instanceId || ''), wd, source || 'command', new Error((options && options.message) || 'Simulated watchdog crash'));
            return true;
        }
    };

    if (runtime.__internal && runtime.__internal.modules && runtime.__internal.modules.watchdog) {
        runtime.__internal.modules.watchdog.getState = runtime.__watchdog.getState;
        runtime.__internal.modules.watchdog.getStableSnapshot = runtime.__watchdog.getStableSnapshot;
        runtime.__internal.modules.watchdog.getLastRecoveryDetail = runtime.__watchdog.getLastRecoveryDetail;
        runtime.__internal.modules.watchdog.getEvents = runtime.__watchdog.getEvents;
        runtime.__internal.modules.watchdog.simulateCrash = runtime.__watchdog.simulateCrash;
    }
})();

(function () {
    function _resolveInstanceId(instanceId) {
        if (instanceId) return instanceId;
        var host = document.querySelector('[data-testid="document-wysiwyg-host"][data-instance-id]');
        return host ? (host.getAttribute('data-instance-id') || '') : '';
    }

    function _editor() {
        return window.tmDocumentEditorWysiwyg || window.tmDocumentWysiwyg || null;
    }

    window.tmDocumentWysiwygDebug = {
        getRuntimeState: function (instanceId) {
            var id = _resolveInstanceId(instanceId);
            var editor = _editor();
            var snapshot = editor && editor.getDebugSnapshot
                ? editor.getDebugSnapshot(id)
                : { InstanceId: id, HasInstance: false, Error: 'getDebugSnapshot unavailable' };
            var runtime = window.tmDocumentEditorRuntime;
            var runtimeDocument = runtime && runtime.__testHooks && runtime.__testHooks.getRuntimeDocument
                ? runtime.__testHooks.getRuntimeDocument(id)
                : null;

            return Object.assign({}, snapshot || {}, {
                RuntimeAuthority: 'JsCanonicalBoundary',
                JsOwnedRuntime: true,
                JsOwnedRuntimePhase: 'CanonicalDocumentModel',
                HasRuntimeDocument: !!runtimeDocument,
                RuntimeDocumentId: runtimeDocument && runtimeDocument.document
                    ? (runtimeDocument.document.DocumentId || runtimeDocument.document.documentId || '')
                    : ''
            });
        },
        getRuntimeStateJson: function (instanceId) {
            return JSON.stringify(this.getRuntimeState(instanceId), null, 2);
        },
        getRenderStats: function (instanceId) {
            var id = _resolveInstanceId(instanceId);
            var editor = _editor();
            var metrics = editor && editor.getDebugMetrics ? editor.getDebugMetrics(id) : null;
            if (!metrics) {
                return {
                    InstanceId: id,
                    HasInstance: false
                };
            }

            return {
                InstanceId: id,
                HasInstance: true,
                SnapshotApplyCount: metrics.SnapshotApplyCount || 0,
                FullRenderCount: metrics.FullRenderCount || 0,
                IncrementalOperationCount: metrics.IncrementalOperationCount || 0,
                LastRenderReason: metrics.LastRenderReason || '',
                InputOperationCount: metrics.InputOperationCount || 0,
                InputLongOperationCount: metrics.InputLongOperationCount || 0,
                LastInputLatencyMs: metrics.LastInputLatencyMs || 0,
                MaxInputLatencyMs: metrics.MaxInputLatencyMs || 0,
                AverageInputLatencyMs: metrics.AverageInputLatencyMs || 0,
                LastInputOperationMs: metrics.LastInputOperationMs || 0,
                MaxInputOperationMs: metrics.MaxInputOperationMs || 0,
                AverageInputOperationMs: metrics.AverageInputOperationMs || 0,
                LastInputMetricType: metrics.LastInputMetricType || '',
                LastInputEventType: metrics.LastInputEventType || '',
                RemoteOperationApplyCount: metrics.RemoteOperationApplyCount || 0,
                RemoteOperationBatchCount: metrics.RemoteOperationBatchCount || 0,
                MeasureCount: metrics.MeasureCount || 0,
                MeasureCacheHits: metrics.MeasureCacheHits || 0,
                MeasureInvalidations: metrics.MeasureInvalidations || 0,
                VirtualizationEnabled: !!metrics.VirtualizationEnabled,
                TotalPages: metrics.TotalPages || 0,
                RenderedPages: metrics.RenderedPages || 0,
                VirtualizedPages: metrics.VirtualizedPages || 0
            };
        },
        // ─── Find & Replace marker API ────────────────────────────────────────────

        /// <summary>
        /// Sets search result markers on the editor. Each marker: { blockId, offset, length, active }.
        /// Clears any previously set markers first.
        /// </summary>
        setSearchMarkers: function (instanceId, markers) {
            var inst = _getInstance(instanceId);
            if (!inst) return;
            _clearSearchMarkers(inst);
            if (!markers || markers.length === 0) return;
            inst._searchMarkers = markers;
            markers.forEach(function (marker) {
                _applySearchMarker(inst, marker);
            });
        },

        /// <summary>Removes all search result highlights from the editor.</summary>
        clearSearchMarkers: function (instanceId) {
            var inst = _getInstance(instanceId);
            if (!inst) return;
            _clearSearchMarkers(inst);
        },

        /// <summary>Scrolls to the active search marker (the one with active=true).</summary>
        scrollToSearchResult: function (instanceId, markerIndex) {
            var inst = _getInstance(instanceId);
            if (!inst) return;
            var mark = inst.root.querySelector('.tm-wysiwyg-search-match--active');
            if (!mark) {
                // Try to find by index
                var all = inst.root.querySelectorAll('.tm-wysiwyg-search-match');
                mark = all[markerIndex] || null;
            }
            if (mark) {
                mark.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
            }
        },

        getUndoStack: function (instanceId) {
            var state = this.getRuntimeState(instanceId);
            var id = state.InstanceId || _resolveInstanceId(instanceId);
            var editor = _editor();
            var undoState = editor && editor.getUndoState ? editor.getUndoState(id) : null;
            var debugUndo = editor && editor.getDebugUndoStack ? editor.getDebugUndoStack(id) : null;
            return {
                InstanceId: id,
                HasInstance: !!state.HasInstance,
                JsOwnedUndo: !!(undoState && undoState.JsOwnedUndo),
                CanUndo: !!(undoState && undoState.CanUndo),
                CanRedo: !!(undoState && undoState.CanRedo),
                UndoDepth: undoState ? undoState.UndoDepth : 0,
                RedoDepth: undoState ? undoState.RedoDepth : 0,
                NextUndoDescription: undoState ? undoState.NextUndoDescription : null,
                NextRedoDescription: undoState ? undoState.NextRedoDescription : null,
                Epoch: undoState ? undoState.Epoch : 0,
                Items: debugUndo ? debugUndo.Undo : [],
                RedoItems: debugUndo ? debugUndo.Redo : [],
                PendingItem: debugUndo ? debugUndo.Pending : null,
                LastApply: debugUndo ? debugUndo.LastApply : null,
                CurrentTransactionId: state.CurrentTransactionId || null,
                PendingTransactionId: state.PendingTransactionId || null,
                PendingPatchType: state.PendingPatchType || null
            };
        },

        setImageDebugEnabled: function (enabled) {
            try {
                window.localStorage.setItem('tmDocumentEditorImageDebug', enabled ? '1' : '0');
            } catch {}
            console.info('[TmDocumentEditor:image]', enabled ? 'debug enabled' : 'debug disabled');
        },

        getImageDebugEnabled: function () {
            return _isImageDebugEnabled();
        }
    };
})();
