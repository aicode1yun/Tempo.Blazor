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
            lastSelectionSnapshot: null,
            lastInputType: null,
            lastInputDataLength: 0,
            lastPatchType: null,
            lastPatchId: null,
            lastPatchTransactionId: null,
            lastPatchAt: null,
            measureCache: new Map(),
            measureStats: { count: 0, cacheHits: 0, invalidations: 0 },
            renderStats: { snapshotApplies: 0, fullRenders: 0, remoteOperations: 0, remoteBatches: 0 },
            virtualPages: [],
            virtualState: null,
            virtualizationScrollTimer: null,
            hasRenderedDocument: false,
            appliedOperationIds: new Set(),
            inlineRevisionPopover: null,
            selectedImageFigure: null,
            imageContextMenu: null,
            miniToolbarVisible: false,
            miniToolbarRequestKey: null,
            imageDragTransaction: null,
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
        inst._handleVirtualScroll = function () { _scheduleVirtualizationRefresh(inst); };
        inst.root.addEventListener('scroll', inst._handleVirtualScroll, { passive: true });
        window.addEventListener('scroll', inst._handleVirtualScroll, { passive: true });
        window.addEventListener('resize', inst._handleVirtualScroll);

        if (inst.readOnly) return;

        inst._handleBeforeInput = function (e) { _onBeforeInput(inst, e); };
        inst._handleInput = function (e) { _onInput(inst, e); };
        inst._handlePaste = function (e) { _onPaste(inst, e); };
        inst._handleCopy = function (e) { _onCopy(inst, e); };
        inst._handleCompositionStart = function () { inst.compositionActive = true; };
        inst._handleCompositionEnd = function (e) {
            inst.compositionActive = false;
            _onInput(inst, e);
            _scheduleRemoteQueueFlush(inst);
        };
        inst._handleSelectionChange = function () { _onSelectionChange(inst); };
        inst._handleKeyDown = function (e) { _onKeyDown(inst, e); };
        inst._handlePointerDown = function (e) { _onFloatingImagePointerDown(inst, e); };
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
        inst.root.addEventListener('compositionend', inst._handleCompositionEnd, true);
        document.addEventListener('selectionchange', inst._handleSelectionChange);
        inst.root.addEventListener('keydown', inst._handleKeyDown, true);
        inst.root.addEventListener('pointerdown', inst._handlePointerDown, true);
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
        if (inst._handleCompositionEnd) {
            inst.root.removeEventListener('compositionend', inst._handleCompositionEnd, true);
        }
        if (inst._handleSelectionChange) {
            document.removeEventListener('selectionchange', inst._handleSelectionChange);
        }
        if (inst._handleKeyDown) {
            inst.root.removeEventListener('keydown', inst._handleKeyDown, true);
        }
        if (inst._handlePointerDown) {
            inst.root.removeEventListener('pointerdown', inst._handlePointerDown, true);
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

        _hideMiniToolbar(inst);

        if (target.closest('.tm-wysiwyg-revision-review')) {
            return;
        }

        var image = target.closest('figure.tm-wysiwyg-image');
        if (image && inst.root.contains(image)) {
            event.preventDefault();
            _selectImageFigure(inst, image);
            _hideInlineRevisionReview(inst);
            return;
        }

        _clearSelectedImage(inst);
        _hideImageContextMenu(inst);

        var revision = target.closest('.tm-wysiwyg-revision[data-revision-id]');
        if (!revision || !inst.root.contains(revision)) {
            _hideInlineRevisionReview(inst);
            return;
        }

        _showInlineRevisionReview(inst, revision);
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
        headerFooter.focus({ preventScroll: true });
        _ensureEditableSelection(inst, headerFooter);
        var snapshot = _captureSelectionSnapshot(inst);
        inst.lastSelectionSnapshot = snapshot;
        inst.pendingSelectionSnapshot = snapshot;
        _flushSelectionNotification(inst);
    }

    function _onRootContextMenu(inst, event) {
        if (!inst || inst.disposed || inst.readOnly) return;
        var target = event.target && event.target.nodeType === Node.ELEMENT_NODE
            ? event.target
            : event.target?.parentElement;
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
            var tablePosition = _placeFloatingElement(event.clientX, event.clientY, 224, 196);
            _invokeDotNet(inst, 'HandleTableContextMenuRequested', {
                ClientX: event.clientX,
                ClientY: event.clientY,
                Left: tablePosition.left,
                Top: tablePosition.top,
                Width: tablePosition.width,
                Height: tablePosition.height,
                ViewportWidth: tablePosition.viewportWidth,
                ViewportHeight: tablePosition.viewportHeight,
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

        event.preventDefault();
        _clearSelectedImage(inst);
        _hideImageContextMenu(inst);
        _hideMiniToolbar(inst, true);
        _ensureContextMenuSelection(inst, target, event);
        var snapshot = _captureSelectionSnapshot(inst);
        if (!snapshot || snapshot.isCollapsed || !_isTextSelectionSnapshot(snapshot)) return;

        inst.lastSelectionSnapshot = snapshot;
        _scheduleSelectionNotification(inst, snapshot);
        var position = _placeFloatingElement(event.clientX, event.clientY, 240, 268);
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

    function _placeFloatingElement(clientX, clientY, width, height) {
        var margin = 8;
        var viewportWidth = window.innerWidth || document.documentElement.clientWidth || 1024;
        var viewportHeight = window.innerHeight || document.documentElement.clientHeight || 768;
        var left = Math.max(margin, Math.min(clientX, viewportWidth - width - margin));
        var top = Math.max(margin, Math.min(clientY, viewportHeight - height - margin));
        return {
            left: left,
            top: top,
            width: width,
            height: height,
            viewportWidth: viewportWidth,
            viewportHeight: viewportHeight
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

        var width = 184;
        var height = 40;
        var margin = 8;
        var viewportWidth = window.innerWidth || document.documentElement.clientWidth || 1024;
        var viewportHeight = window.innerHeight || document.documentElement.clientHeight || 768;
        var left = rect.left + (rect.width / 2) - (width / 2);
        var top = rect.top - height - margin;
        if (top < margin) {
            top = rect.bottom + margin;
        }

        left = Math.max(margin, Math.min(left, viewportWidth - width - margin));
        top = Math.max(margin, Math.min(top, viewportHeight - height - margin));

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

        if (inst.miniToolbarVisible && inst.miniToolbarRequestKey === key) return;
        inst.miniToolbarVisible = true;
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
    }

    function _hideMiniToolbar(inst, force) {
        if (!inst || (!force && !inst.miniToolbarVisible)) return;
        inst.miniToolbarVisible = false;
        inst.miniToolbarRequestKey = null;
        _invokeDotNet(inst, 'HandleMiniToolbarChanged', null);
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

        var block = figure.closest('.tm-wysiwyg-block[data-block-id]');
        if (block) {
            inst.lastSelectionSnapshot = {
                Region: 'Image',
                AnchorBlockId: block.getAttribute('data-block-id') || '',
                FocusBlockId: block.getAttribute('data-block-id') || '',
                AnchorInlineId: '',
                FocusInlineId: '',
                AnchorOffset: 0,
                FocusOffset: 0,
                IsCollapsed: true
            };
        }
    }

    function _clearSelectedImage(inst) {
        if (!inst || !inst.selectedImageFigure) return;
        if (inst.selectedImageFigure.isConnected) {
            inst.selectedImageFigure.classList.remove('tm-wysiwyg-image--selected');
            inst.selectedImageFigure.removeAttribute('aria-selected');
        }
        inst.selectedImageFigure = null;
    }

    function _showImageContextMenu(inst, figure, clientX, clientY) {
        if (!inst || !figure) return;
        _hideImageContextMenu(inst);

        var menu = document.createElement('div');
        menu.className = 'tm-wysiwyg-image-context-menu';
        menu.setAttribute('role', 'menu');
        menu.setAttribute('contenteditable', 'false');
        menu.setAttribute('data-testid', 'document-wysiwyg-image-context-menu');

        var actions = [
            { text: 'Replace image', testId: 'document-wysiwyg-image-replace', action: function () { _replaceSelectedImage(inst); } },
            { text: 'Alt text', testId: 'document-wysiwyg-image-alt-text', action: function () { _editSelectedImageAltText(inst); } },
            { text: 'Caption', testId: 'document-wysiwyg-image-caption', action: function () { _editSelectedImageCaption(inst); } },
            { text: 'Wrap text: Inline', testId: 'document-wysiwyg-image-wrap-inline', action: function () { _setSelectedImageInline(inst); } },
            { text: 'Wrap text: Square', testId: 'document-wysiwyg-image-wrap-square', action: function () { _setSelectedImageWrapMode(inst, { wrapMode: 'Square' }); } },
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
        var rootRect = inst.root.getBoundingClientRect();
        var menuWidth = 220;
        var x = Math.max(8, clientX - rootRect.left + inst.root.scrollLeft);
        var y = Math.max(8, clientY - rootRect.top + inst.root.scrollTop);
        menu.style.left = Math.min(x, Math.max(8, inst.root.clientWidth - menuWidth - 8)) + 'px';
        menu.style.top = y + 'px';
        inst.imageContextMenu = menu;
    }

    function _hideImageContextMenu(inst) {
        if (!inst || !inst.imageContextMenu) return;
        if (inst.imageContextMenu.parentNode) {
            inst.imageContextMenu.parentNode.removeChild(inst.imageContextMenu);
        }
        inst.imageContextMenu = null;
    }

    function _replaceSelectedImage(inst) {
        var figure = _getSelectedImageFigure(inst);
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
        if (assetId) figure.setAttribute('data-image-asset-id', assetId);
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

    // ── Input pipeline ───────────────────────────────────────────────────────

    function _onBeforeInput(inst, event) {
        if (inst.readOnly) {
            event.preventDefault();
            event.stopPropagation();
            return;
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

        inst.pendingNativeInputSelection = selection;
        inst.acceptingNativeInput = true;
        if (inst.nativeInputTimer) {
            clearTimeout(inst.nativeInputTimer);
        }
        inst.nativeInputTimer = setTimeout(function () {
            inst.acceptingNativeInput = false;
            inst.nativeInputTimer = null;
            _scheduleRemoteQueueFlush(inst);
        }, 250);
    }

    function _applyInsertText(inst, data) {
        if (!data) return false;
        inst._applyingOwnPatch = true;
        try {
            var sel = window.getSelection();
            if (!sel || sel.rangeCount === 0) return false;
            var range = sel.getRangeAt(0);
            if (!sel.isCollapsed) {
                range.deleteContents();
            }

            var textNode = range.startContainer;
            var offset = range.startOffset;

            if (textNode.nodeType === Node.TEXT_NODE) {
                var current = textNode.textContent;
                textNode.textContent = current.slice(0, offset) + data + current.slice(offset);
                _setCaret(textNode, offset + data.length);
                return true;
            } else if (textNode.nodeType === Node.ELEMENT_NODE) {
                var inline = textNode.querySelector('[data-inline-id]') || textNode.closest('[data-inline-id]');
                if (inline && inline.firstChild && inline.firstChild.nodeType === Node.TEXT_NODE) {
                    var txt = inline.firstChild;
                    var currentText = txt.textContent;
                    var clampedOffset = Math.max(0, Math.min(offset, currentText.length));
                    txt.textContent = currentText.slice(0, clampedOffset) + data + currentText.slice(clampedOffset);
                    _setCaret(txt, clampedOffset + data.length);
                    return true;
                } else {
                    var newText = document.createTextNode(data);
                    inline = inline || textNode;
                    inline.appendChild(newText);
                    _setCaret(newText, data.length);
                    return true;
                }
            }

            return false;
        } finally {
            inst._applyingOwnPatch = false;
        }
    }

    function _applyDeleteBackward(inst, unit) {
        inst._applyingOwnPatch = true;
        try {
            var sel = window.getSelection();
            if (!sel || sel.rangeCount === 0) return false;
            if (!sel.isCollapsed) {
                sel.deleteFromDocument();
                return true;
            }
            var range = sel.getRangeAt(0);
            var textNode = range.startContainer;
            var offset = range.startOffset;

            if (textNode.nodeType === Node.TEXT_NODE) {
                var current = textNode.textContent;
                var delLen = unit === 'word' ? _wordBoundaryBackward(current, offset) : 1;
                if (offset > 0) {
                    textNode.textContent = current.slice(0, offset - delLen) + current.slice(offset);
                    _setCaret(textNode, offset - delLen);
                    return true;
                }
            }

            return false;
        } finally {
            inst._applyingOwnPatch = false;
        }
    }

    function _applyDeleteForward(inst, unit) {
        inst._applyingOwnPatch = true;
        try {
            var sel = window.getSelection();
            if (!sel || sel.rangeCount === 0) return false;
            if (!sel.isCollapsed) {
                sel.deleteFromDocument();
                return true;
            }
            var range = sel.getRangeAt(0);
            var textNode = range.startContainer;
            var offset = range.startOffset;

            if (textNode.nodeType === Node.TEXT_NODE) {
                var current = textNode.textContent;
                var delLen = unit === 'word' ? _wordBoundaryForward(current, offset) : 1;
                textNode.textContent = current.slice(0, offset) + current.slice(offset + delLen);
                _setCaret(textNode, offset);
                return delLen > 0;
            }

            return false;
        } finally {
            inst._applyingOwnPatch = false;
        }
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

    function _handlePendingTypingBeforeInput(inst, event, inputType, selection) {
        if (inputType !== 'insertText' || !event.data || !_hasPendingTypingMarks(inst)) {
            return false;
        }

        var result = _applyPendingTypingTextToDom(inst, event.data);
        if (!result) {
            return false;
        }

        event.preventDefault();
        _invalidateMeasureCache(inst);
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

        if (!sel.isCollapsed) {
            range.deleteContents();
        }

        var blockId = _createBlockId();
        var inlineId = _createInlineId();
        var newBlock = document.createElement('p');
        newBlock.className = 'tm-wysiwyg-block';
        newBlock.setAttribute('data-block-id', blockId);

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

        newBlock.insertBefore(newInline, newBlock.firstChild);
        if (!newInline.textContent && !newInline.querySelector('br[data-inline-break]')) {
            textNode = textNode.parentNode === newInline ? textNode : document.createTextNode('');
            if (!textNode.parentNode) {
                newInline.appendChild(textNode);
            }
        }

        block.after(newBlock);
        _setCaret(textNode, 0);
        return {
            block: {
                Id: blockId,
                Type: 0,
                Content: {
                    $type: 'paragraph',
                    Inlines: [
                        {
                            $type: 'text',
                            Id: inlineId,
                            Text: textNode.textContent || ''
                        }
                    ]
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
        _setCaret(afterNode, 0);
        return true;
    }

    function _handleStructuralBeforeInput(inst, event, inputType, selection, revisionType) {
        if (inputType !== 'insertParagraph' && inputType !== 'insertLineBreak') {
            return false;
        }

        var result = inputType === 'insertParagraph'
            ? _applyParagraphBreakToDom(inst)
            : (_applySoftBreakToDom(inst) ? { block: null } : null);
        if (!result) return false;

        event.preventDefault();
        _flushPendingInputPatch(inst);
        _invalidateMeasureCache(inst);
        var afterSelection = _captureSelectionSnapshot(inst);
        inst.lastSelectionSnapshot = afterSelection;
        _scheduleSelectionNotification(inst, afterSelection);
        _beginTypingTransaction(inst);
        var patch = {
            type: inputType === 'insertParagraph' ? 'SplitBlock' : 'InsertSoftBreak',
            blockType: inputType === 'insertParagraph' ? 'Paragraph' : null,
            block: result.block,
            selection: selection,
            beforeSelection: selection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        };
        if (revisionType) {
            patch.revisionType = revisionType;
        }

        _dispatchPatch(inst, patch);
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

    function _ensureEditableSelection(inst, target) {
        var sel = window.getSelection();
        if (sel
            && sel.rangeCount > 0
            && _nodeBelongsToRoot(sel.anchorNode, inst.root)
            && (!target || target.contains(sel.anchorNode))) {
            return;
        }

        var editable = target && target.closest ? target.closest('[contenteditable="true"]') : null;
        if (!editable || !inst.root.contains(editable)) {
            editable = inst.root.querySelector('.tm-wysiwyg-page__body[contenteditable="true"]');
        }
        if (!editable) return;

        var textNode = _firstDeepTextNode(editable);
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

    function _onInput(inst, event) {
        if (inst.readOnly) {
            event.stopPropagation();
            return;
        }

        if (inst.compositionActive) return;
        if (_shouldSuppressBrowserInputEvent(inst, event.inputType)) {
            return;
        }
        _invalidateMeasureCache(inst);

        const inputType = event.inputType;
        const data = event.data;
        inst.lastInputType = inputType || null;
        inst.lastInputDataLength = data ? data.length : 0;

        const selection = inst.pendingNativeInputSelection || _captureSelectionSnapshot(inst);
        inst.pendingNativeInputSelection = null;
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

        _dispatchInputPatch(inst, inputType, data, selection, null, null, afterSelection);
    }

    function _handleTrackedBeforeInput(inst, event, inputType, selection) {
        if (inputType === 'insertText') {
            var insertData = event.data || '';
            if (!insertData) return false;

            var insertionRevisionId = _applyTrackedInsertionToDom(inst, insertData);
            if (!insertionRevisionId) return false;

            event.preventDefault();
            _invalidateMeasureCache(inst);
            var afterInsertSelection = _captureSelectionSnapshot(inst);
            inst.lastSelectionSnapshot = afterInsertSelection;
            _scheduleSelectionNotification(inst, afterInsertSelection);
            _dispatchInputPatch(inst, inputType, insertData, selection, insertionRevisionId, 'Insertion', afterInsertSelection);
            return true;
        }

        if (inputType === 'deleteContentBackward'
            || inputType === 'deleteContentForward'
            || inputType === 'deleteWordBackward'
            || inputType === 'deleteWordForward') {
            var deletion = _applyTrackedDeletionToDom(inst, inputType);
            if (!deletion) return false;

            event.preventDefault();
            _invalidateMeasureCache(inst);
            var afterDeleteSelection = _captureSelectionSnapshot(inst);
            inst.lastSelectionSnapshot = afterDeleteSelection;
            _scheduleSelectionNotification(inst, afterDeleteSelection);
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
        var patch = {
            type: _mapInputTypeToPatchType(inputType),
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
            return;
        }

        _flushPendingInputPatch(inst);
        _dispatchPatch(inst, patch);
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
            return false;
        }
        if (inst.suppressInputType !== inputType) return false;

        inst.suppressInputType = null;
        inst.suppressInputUntil = 0;
        return true;
    }

    function _dispatchPatch(inst, patch) {
        _invalidateMeasureCache(inst);
        inst.pendingLocalSnapshotSkips++;
        inst.lastPatchType = patch.type || patch.Type || null;
        inst.lastPatchId = patch.patchId || patch.PatchId || patch.operationId || patch.OperationId || null;
        inst.lastPatchTransactionId = patch.transactionId || patch.TransactionId || null;
        inst.lastPatchAt = new Date().toISOString();
        _invokeDotNet(inst, 'HandlePatchGenerated', patch);
        _scheduleRemoteQueueFlush(inst);
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
        const blocks = html && html.trim()
            ? _parseClipboardHtml(html)
            : _parsePlainTextPaste(plain);

        _insertClipboardBlocks(inst, blocks);
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
                    _insertImageBlock(inst, block, true);
                }
            });
        };
        reader.readAsDataURL(file);
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

    function _insertClipboardBlocks(inst, blocks) {
        if (!blocks || blocks.length === 0) return;
        var insertion = _getInsertionPoint(inst);
        var parent = insertion.parent;
        var after = insertion.after;
        var previousBlockId = after ? after.getAttribute('data-block-id') : (inst.lastSelectionSnapshot && inst.lastSelectionSnapshot.AnchorBlockId);

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
                selection: { AnchorBlockId: previousBlockId || null, IsCollapsed: true },
                protocolVersion: inst.options.protocolVersion || 1
            });
            previousBlockId = block.Id;
        }

        _placeCaretAfterBlock(after);
    }

    function _getInsertionPoint(inst) {
        var sel = window.getSelection();
        var block = null;
        if (sel && sel.rangeCount > 0) {
            var node = sel.anchorNode;
            var el = node && (node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement);
            block = el ? el.closest('.tm-wysiwyg-block[data-block-id]') : null;
        }
        var body = inst.root.querySelector('.tm-wysiwyg-page__body') || inst.root;
        return {
            parent: block && block.parentElement ? block.parentElement : body,
            after: block
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

        if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey) {
            var markPayload = _shortcutMarkPayload(event.key);
            if (markPayload) {
                event.preventDefault();
                event.stopPropagation();
                if (typeof event.stopImmediatePropagation === 'function') {
                    event.stopImmediatePropagation();
                }
                _flushPendingInputPatch(inst);
                _flushSelectionNotification(inst);
                markPayload.selection = _captureSelectionSnapshot(inst);
                _invokeDotNet(inst, 'HandleCommandToggleMark', markPayload);
                return;
            }
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
                }
                return;
            }
        }

        // Handle shortcuts that the browser does not natively support
        // or that we want to intercept for the command stack.
        if ((event.ctrlKey || event.metaKey) && event.key === 'z' && !event.shiftKey) {
            event.preventDefault();
            _flushPendingInputPatch(inst);
            _flushSelectionNotification(inst);
            _invokeDotNet(inst, 'HandleUndoRequested');
            return;
        }
        if ((event.ctrlKey || event.metaKey) && (event.key === 'y' || (event.key === 'z' && event.shiftKey))) {
            event.preventDefault();
            _flushPendingInputPatch(inst);
            _flushSelectionNotification(inst);
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
     * Phase 13: Finds the current table cell from the active selection.
     */
    function _findCurrentTableCell(inst) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return null;
        var node = sel.anchorNode;
        if (!node) return null;
        var el = node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement;
        if (!el) return null;
        return el.closest('td[data-cell-id], th[data-cell-id]');
    }

    /**
     * Phase 13: Finds the next table cell (row-major order).
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
     * Phase 13: Finds the previous table cell (row-major order).
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
     * Phase 13: Focuses a table cell by placing the caret at the start.
     */
    function _focusCell(cell) {
        var sel = window.getSelection();
        if (!sel) return;
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

    // ── Phase 13: Table structural commands ──────────────────────────────────

    /**
     * Inserts a new table (2×2) at the current selection.
     */
    function _insertTable(inst) {
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return;

        var tableBlockId = 'tbl-' + Date.now();
        var rows = [];
        var tableBlock = document.createElement('table');
        tableBlock.className = 'tm-wysiwyg-table tm-wysiwyg-block';
        tableBlock.setAttribute('data-block-id', tableBlockId);
        for (var r = 0; r < 2; r++) {
            var tr = document.createElement('tr');
            var rowCells = [];
            for (var c = 0; c < 2; c++) {
                var cellId = 'tc-' + Date.now() + '-' + r + '-' + c;
                var td = document.createElement('td');
                td.setAttribute('data-cell-id', cellId);
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

        // Notify Blazor about the new table block.
        _dispatchPatch(inst, {
            type: 'InsertBlock',
            blockType: 'Table',
            block: {
                Id: tableBlockId,
                Type: 4,
                Order: 0,
                Content: { $type: 'table', Rows: rows }
            },
            selection: _captureSelectionSnapshot(inst),
            protocolVersion: inst.options.protocolVersion || 1
        });
    }

    /**
     * Phase 13: Finds the parent table element that carries data-block-id.
     */
    function _getTableBlockFromCell(cell) {
        if (!cell) return null;
        var table = cell.closest('table.tm-wysiwyg-block[data-block-id]');
        return table || null;
    }

    function _appendEmptyTableCellParagraph(td, blockId, inlineId) {
        var p = document.createElement('p');
        p.className = 'tm-wysiwyg-block';
        p.setAttribute('data-block-id', blockId || _createBlockId());
        var span = document.createElement('span');
        span.setAttribute('data-inline-id', inlineId || _createInlineId());
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
     * Phase 13: Dispatches an UpdateBlock patch for the given table element.
     */
    function _dispatchTableUpdatePatch(inst, tableEl) {
        if (!tableEl) return;
        var blockId = tableEl.getAttribute('data-block-id');
        if (!blockId) return;
        var content = _serializeTable(tableEl);
        _dispatchPatch(inst, {
            type: 'UpdateBlock',
            block: {
                Id: blockId,
                Type: 4,
                Order: 0,
                Content: content
            },
            selection: _captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot,
            protocolVersion: inst.options.protocolVersion || 1
        });
    }

    function _dispatchImageUpdatePatch(inst, figureEl) {
        if (!figureEl) return;
        var blockEl = figureEl.closest('.tm-wysiwyg-block[data-block-id]');
        if (!blockEl) return;
        var blockId = blockEl.getAttribute('data-block-id');
        if (!blockId) return;
        _dispatchPatch(inst, {
            type: 'UpdateBlock',
            block: {
                Id: blockId,
                Type: 5,
                Order: 0,
                Content: _serializeImage(figureEl)
            },
            selection: _captureSelectionSnapshot(inst),
            protocolVersion: inst.options.protocolVersion || 1
        });
    }

    function _onFloatingImagePointerDown(inst, event) {
        var handle = event.target && event.target.closest && event.target.closest('.tm-wysiwyg-image__resize-handle');
        var figure = handle
            ? handle.closest('figure.tm-wysiwyg-image')
            : event.target && event.target.closest && event.target.closest('figure.tm-wysiwyg-image');
        if (!figure || !inst.root.contains(figure)) return;

        event.preventDefault();
        _selectImageFigure(inst, figure);
        _hideInlineRevisionReview(inst);
        _hideImageContextMenu(inst);

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
        figure.classList.add('tm-wysiwyg-image--dragging');
        figure.setAttribute('data-drag-feedback', handle ? 'resize' : 'move');
        inst.imageDragTransaction = {
            blockId: figure.closest('.tm-wysiwyg-block[data-block-id]')?.getAttribute('data-block-id') || '',
            mode: handle ? 'resize' : 'move'
        };
        if (typeof figure.setPointerCapture === 'function' && event.pointerId != null) {
            try { figure.setPointerCapture(event.pointerId); } catch { }
        }

        function onMove(moveEvent) {
            var dx = moveEvent.clientX - startX;
            var dy = moveEvent.clientY - startY;
            if (handle && img) {
                var maxSize = _getFloatingImageMaxSize(figure);
                img.style.width = Math.min(maxSize.width, Math.max(24, initialWidth + dx)) + 'px';
                img.style.height = Math.min(maxSize.height, Math.max(24, initialHeight + dy)) + 'px';
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
            inst.imageDragTransaction = null;
            if (typeof figure.releasePointerCapture === 'function' && event.pointerId != null) {
                try { figure.releasePointerCapture(event.pointerId); } catch { }
            }
            _dispatchImageUpdatePatch(inst, figure);
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
        inst.imageDragTransaction = {
            blockId: blockEl.getAttribute('data-block-id') || '',
            mode: 'inline-move'
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
                AnchorOffset: 0,
                FocusOffset: 0,
                IsCollapsed: true
            },
            transactionId: inst.imageDragTransaction && inst.imageDragTransaction.blockId
                ? 'image-move-' + inst.imageDragTransaction.blockId
                : null,
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

    /**
     * Inserts a row after the current table cell's row.
     */
    function _insertTableRow(inst) {
        var cell = _findCurrentTableCell(inst);
        if (!cell) return;
        var row = cell.parentElement;
        var table = row.closest('table');
        if (!table) return;
        var cellsPerRow = row.children.length;
        var newRow = document.createElement('tr');
        for (var c = 0; c < cellsPerRow; c++) {
            var td = document.createElement('td');
            td.setAttribute('data-cell-id', 'tc-' + Date.now() + '-' + c);
            _appendEmptyTableCellParagraph(td);
            newRow.appendChild(td);
        }
        row.parentElement.insertBefore(newRow, row.nextSibling);
        _dispatchTableUpdatePatch(inst, _getTableBlockFromCell(cell));
    }

    /**
     * Deletes the current table cell's row.
     */
    function _deleteTableRow(inst) {
        var cell = _findCurrentTableCell(inst);
        if (!cell) return;
        var row = cell.parentElement;
        var table = _getTableBlockFromCell(cell);
        if (row.parentElement.children.length <= 1) {
            // Last row: remove the whole table.
            if (table) {
                var blockId = table.getAttribute('data-block-id');
                table.remove();
                if (blockId) {
                    _dispatchPatch(inst, {
                        type: 'RemoveBlock',
                        selection: { anchorBlockId: blockId },
                        protocolVersion: inst.options.protocolVersion || 1
                    });
                }
            }
        } else {
            row.remove();
            _dispatchTableUpdatePatch(inst, table);
        }
    }

    /**
     * Inserts a column after the current table cell's column.
     */
    function _insertTableColumn(inst) {
        var cell = _findCurrentTableCell(inst);
        if (!cell) return;
        var row = cell.parentElement;
        var table = row.closest('table');
        if (!table) return;
        var cellIndex = Array.from(row.children).indexOf(cell);
        var rows = table.querySelectorAll('tr');
        for (var r = 0; r < rows.length; r++) {
            var td = document.createElement('td');
            td.setAttribute('data-cell-id', 'tc-' + Date.now() + '-' + r);
            _appendEmptyTableCellParagraph(td);
            var targetRow = rows[r];
            var targetCell = targetRow.children[cellIndex];
            if (targetCell) {
                targetRow.insertBefore(td, targetCell.nextSibling);
            } else {
                targetRow.appendChild(td);
            }
        }
        _dispatchTableUpdatePatch(inst, _getTableBlockFromCell(cell));
    }

    /**
     * Deletes the current table cell's column.
     */
    function _deleteTableColumn(inst) {
        var cell = _findCurrentTableCell(inst);
        if (!cell) return;
        var row = cell.parentElement;
        var table = row.closest('table');
        if (!table) return;
        var cellIndex = Array.from(row.children).indexOf(cell);
        var rows = table.querySelectorAll('tr');
        for (var r = 0; r < rows.length; r++) {
            var targetCell = rows[r].children[cellIndex];
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
                        selection: { anchorBlockId: blockId },
                        protocolVersion: inst.options.protocolVersion || 1
                    });
                }
            }
        } else {
            _dispatchTableUpdatePatch(inst, _getTableBlockFromCell(cell));
        }
    }

    /**
     * Merges the current cell with the cell to its right.
     */
    function _mergeTableCells(inst) {
        var cell = _findCurrentTableCell(inst);
        if (!cell) return;
        var row = cell.parentElement;
        var nextCell = cell.nextElementSibling;
        if (!nextCell || nextCell.tagName !== 'TD') return;
        var currentSpan = parseInt(cell.getAttribute('colspan') || '1', 10);
        var nextSpan = parseInt(nextCell.getAttribute('colspan') || '1', 10);
        cell.setAttribute('colspan', currentSpan + nextSpan);
        // Move content from next cell into current cell.
        while (nextCell.firstChild) {
            cell.appendChild(nextCell.firstChild);
        }
        nextCell.remove();
        _dispatchTableUpdatePatch(inst, _getTableBlockFromCell(cell));
    }

    /**
     * Splits a merged cell back into individual cells.
     */
    function _splitTableCell(inst) {
        var cell = _findCurrentTableCell(inst);
        if (!cell) return;
        var span = parseInt(cell.getAttribute('colspan') || '1', 10);
        if (span <= 1) return;
        cell.removeAttribute('colspan');
        var row = cell.parentElement;
        for (var i = 1; i < span; i++) {
            var newCell = document.createElement('td');
            newCell.setAttribute('data-cell-id', 'tc-' + Date.now() + '-' + i);
            var p = document.createElement('p');
            p.className = 'tm-wysiwyg-block';
            p.setAttribute('data-block-id', '');
            p.innerHTML = '<br>';
            newCell.appendChild(p);
            row.insertBefore(newCell, cell.nextSibling);
        }
        _dispatchTableUpdatePatch(inst, _getTableBlockFromCell(cell));
    }

    function _onSelectionChange(inst) {
        if (inst.disposed) return;
        const snapshot = _captureSelectionSnapshot(inst);
        inst.lastSelectionSnapshot = snapshot;
        _scheduleSelectionNotification(inst, snapshot);
        if (snapshot && !snapshot.isCollapsed) {
            _scheduleMiniToolbar(inst, snapshot);
        } else {
            _hideMiniToolbar(inst);
        }
    }

    function _scheduleSelectionNotification(inst, snapshot) {
        inst.pendingSelectionSnapshot = snapshot;
        if (inst.pendingSelectionTimer) return;

        inst.pendingSelectionTimer = setTimeout(function () {
            _flushSelectionNotification(inst);
        }, 80);
    }

    function _flushSelectionNotification(inst) {
        if (inst.pendingSelectionTimer) {
            clearTimeout(inst.pendingSelectionTimer);
            inst.pendingSelectionTimer = null;
        }

        var snapshot = inst.pendingSelectionSnapshot;
        inst.pendingSelectionSnapshot = null;
        if (snapshot !== undefined) {
            _invokeDotNet(inst, 'HandleSelectionChanged', snapshot);
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
            inst.currentTransactionId = null;
            inst.typingTimer = null;
            _invokeDotNet(inst, 'HandleTransactionCommitted');
            _scheduleRemoteQueueFlush(inst);
        }, inst.options.typingBatchMs || 500);
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

        return {
            region: region.region,
            pageIndex: region.pageIndex,
            headerFooterId: region.headerFooterId,
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
    }

    function _resolveSelectionRegion(node, root) {
        var el = node && node.nodeType === Node.ELEMENT_NODE ? node : node && node.parentElement;
        if (!el || !root.contains(el)) {
            return { region: 'Body', pageIndex: null, headerFooterId: null, tableCellPath: null };
        }

        var pageEl = el.closest('.tm-wysiwyg-page[data-page-index]');
        var pageIndex = pageEl ? parseInt(pageEl.getAttribute('data-page-index') || '0', 10) : null;
        if (!Number.isFinite(pageIndex)) pageIndex = null;

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

        var inlineEl = blockEl.querySelector('[data-inline-id="' + _cssEscape(inlineId || '') + '"]');
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
            case 'deleteWordBackward': return 'DeleteWordBackward';
            case 'deleteWordForward': return 'DeleteWordForward';
            case 'formatBold': return 'ToggleMark';
            case 'formatItalic': return 'ToggleMark';
            case 'formatUnderline': return 'ToggleMark';
            default: return 'UnknownInput';
        }
    }

    // ── Render ───────────────────────────────────────────────────────────────

    function _renderDocument(inst) {
        const snapshot = inst.snapshot;
        const doc = snapshot ? (snapshot.document || snapshot.Document) : null;
        if (!doc) return;

        if (inst.renderStats) inst.renderStats.fullRenders++;
        inst._applyingOwnPatch = true;
        inst.root.innerHTML = '';
        inst.root.removeAttribute('contenteditable');
        // Phase 11: enable paginated layout mode on the host root.
        inst.root.classList.add('tm-wysiwyg-host--paginated');

        const blocks = doc.blocks || doc.Blocks || [];
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
        var pages = [{ index: 0, blocks: [], blockIds: [] }];
        for (var i = 0; i < blocks.length; i++) {
            var block = blocks[i];
            var blockType = block.type || block.Type;
            var isPageBreak = blockType === 'PageBreak' || blockType === 6;

            if (isPageBreak) {
                pages.push({ index: pages.length, blocks: [], blockIds: [] });
            } else {
                var page = pages[pages.length - 1];
                page.blocks.push(block);
                var blockId = block.id || block.Id;
                if (blockId) page.blockIds.push(blockId);
            }
        }

        inst.virtualPages = pages;
        inst.virtualSettings = { pageSettings: pageSettings, firstSection: firstSection, hfMap: hfMap, sectionMap: sectionMap };
        _renderVirtualizedPages(inst, false);
    }

    function _renderVirtualizedPages(inst, preserveSelection) {
        if (!inst.virtualPages || inst.virtualPages.length === 0 || !inst.virtualSettings) return;

        var selection = preserveSelection
            ? (_captureSelectionSnapshot(inst) || inst.lastSelectionSnapshot)
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
        inst._applyingOwnPatch = false;

        if (selection) {
            inst.lastSelectionSnapshot = selection;
            _restoreSelection(inst, selection);
        }
    }

    function _renderPageFromData(inst, pageData) {
        var settings = inst.virtualSettings;
        var pageEl = _createPageElement(inst, settings.pageSettings);
        pageEl.setAttribute('data-page-index', pageData.index);
        pageEl.setAttribute('role', 'region');
        pageEl.setAttribute('aria-label', _formatA11yLabel(inst, 'pageLabel', 'PageLabel', 'Page {0}', pageData.index));
        var body = _createBodyElement(inst, pageData.index);
        pageEl.appendChild(body);
        _renderHeaderFooterForPage(inst, pageEl, settings.firstSection, pageData.index, settings.hfMap, settings.sectionMap);

        for (var i = 0; i < pageData.blocks.length; i++) {
            var blockEl = _renderBlock(pageData.blocks[i], inst);
            if (blockEl) body.appendChild(blockEl);
        }

        return pageEl;
    }

    function _createVirtualPagePlaceholder(inst, pageIndex) {
        var pageEl = _createPageElement(inst, inst.virtualSettings.pageSettings);
        pageEl.classList.add('tm-wysiwyg-page--virtual');
        pageEl.setAttribute('data-page-index', pageIndex);
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

        var selectedPage = _findVirtualPageIndexForSelection(inst, selection);
        if (selectedPage >= 0) {
            first = Math.min(first, selectedPage);
            last = Math.max(last, selectedPage);
        }

        return { enabled: true, first: first, last: last, pageExtent: pageExtent, scrollTop: scrollTop };
    }

    function _getVirtualViewport(inst) {
        if (inst.root.scrollHeight > inst.root.clientHeight + 1) {
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
            marginLeft: ptToMm(leftPt)
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
    }

    /**
     * Creates a single A4 page wrapper element.
     */
    function _createPageElement(inst, settings) {
        var page = document.createElement('div');
        page.className = 'tm-wysiwyg-page';
        page.style.width = settings.width;
        page.style.minHeight = settings.height;
        page.style.paddingTop = settings.marginTop;
        page.style.paddingRight = settings.marginRight;
        page.style.paddingBottom = settings.marginBottom;
        page.style.paddingLeft = settings.marginLeft;
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
        body.setAttribute('data-testid', 'document-wysiwyg-body');
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

    /**
     * Phase 12: Renders a header or footer region into a DOM element.
     */
    function _renderHeaderFooterRegion(inst, hf, type, pageIndex) {
        var el = document.createElement('div');
        el.className = 'tm-wysiwyg-page__' + type;
        el.setAttribute('data-hf-id', hf.id || hf.Id || '');
        el.setAttribute('data-hf-type', type);
        el.setAttribute('data-hf-scope', hf.scope || hf.Scope || 'Primary');
        el.setAttribute('data-region', type === 'header' ? 'Header' : 'Footer');
        el.setAttribute('data-placeholder', type === 'header' ? 'Header' : 'Footer');
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

            // Compare scrollHeight (content) against clientHeight (visible).
            if (page.scrollHeight > page.clientHeight + 2) {
                page.classList.add('tm-wysiwyg-page--overflow');
                warning = document.createElement('div');
                warning.className = 'tm-wysiwyg-page__overflow-warning';
                warning.textContent = 'Content overflows page';
                warning.setAttribute('role', 'status');
                warning.setAttribute('aria-live', 'polite');
                page.appendChild(warning);
            }
        }
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
                var li = document.createElement('li');
                _renderInlines(li, content, inst);
                el.appendChild(li);
                break;
            case 'Quote':
            case 3:
                el = document.createElement('blockquote');
                _renderInlines(el, content, inst);
                break;
            case 'Table':
            case 4:
                el = _renderTable(content);
                break;
            case 'Image':
            case 5:
                el = _renderImage(content, inst);
                break;
            case 'PageBreak':
            case 6:
                el = document.createElement('hr');
                el.className = 'tm-wysiwyg-page-break';
                break;
            default:
                el = document.createElement('p');
                _renderInlines(el, content, inst);
                break;
        }

        if (el) {
            el.setAttribute('data-block-id', id || '');
            var order = block.order ?? block.Order;
            if (order != null) el.setAttribute('data-block-order', String(order));
            el.classList.add('tm-wysiwyg-block');
            _applyParagraphProperties(el, block.paragraphProperties || block.ParagraphProperties);
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
                _renderTextRunContent(span, inline.text || inline.Text || '');
                _applyMarks(span, inline.marks || inline.Marks, inst);
                container.appendChild(span);
            } else if (inlineType === 'token' || inlineType === 'TokenRun') {
                var tokenSpan = document.createElement('span');
                tokenSpan.setAttribute('data-inline-id', inlineId || '');
                tokenSpan.setAttribute('data-inline-atomic', 'true');
                tokenSpan.setAttribute('data-token-key', inline.key || inline.Key || '');
                tokenSpan.setAttribute('data-token-type', inline.tokenType || inline.TokenType || '');
                tokenSpan.setAttribute('title', inline.description || inline.Description || inline.key || inline.Key || '');
                tokenSpan.setAttribute('contenteditable', 'false');
                tokenSpan.className = 'tm-wysiwyg-token' + (inline.colorClass || inline.ColorClass ? ' ' + (inline.colorClass || inline.ColorClass) : '');
                tokenSpan.textContent = inline.displayName || inline.DisplayName || inline.key || inline.Key || '';
                container.appendChild(tokenSpan);
            } else if (inlineType === 'noteReference' || inlineType === 'DocumentNoteReferenceRun') {
                var sup = document.createElement('sup');
                sup.setAttribute('data-inline-id', inlineId || '');
                sup.className = 'tm-wysiwyg-note-ref';
                sup.textContent = inline.displayMarker || inline.DisplayMarker || inline.noteId || inline.NoteId || '';
                container.appendChild(sup);
            }
        }
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

    function _renderTable(content) {
        var table = document.createElement('table');
        table.className = 'tm-wysiwyg-table';
        var rows = (content && (content.rows || content.Rows)) || [];
        for (var r = 0; r < rows.length; r++) {
            var tr = document.createElement('tr');
            var cells = (rows[r] && (rows[r].cells || rows[r].Cells)) || [];
            for (var c = 0; c < cells.length; c++) {
                var cell = cells[c];
                var td = document.createElement('td');
                td.setAttribute('data-cell-id', cell.id || cell.Id || '');

                // Phase 13: set colspan and rowspan for merged cells.
                var cSpan = cell.columnSpan || cell.ColumnSpan || 1;
                var rSpan = cell.rowSpan || cell.RowSpan || 1;
                if (cSpan > 1) td.setAttribute('colspan', cSpan);
                if (rSpan > 1) td.setAttribute('rowspan', rSpan);

                var cellBlocks = (cell && (cell.blocks || cell.Blocks)) || [];
                for (var b = 0; b < cellBlocks.length; b++) {
                    var cellBlockEl = _renderBlock(cellBlocks[b], null);
                    if (cellBlockEl) td.appendChild(cellBlockEl);
                }
                // Phase 13: ensure empty cells have an editable paragraph placeholder.
                if (cellBlocks.length === 0) {
                    _appendEmptyTableCellParagraph(td);
                }
                tr.appendChild(td);
            }
            table.appendChild(tr);
        }
        return table;
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
        var size = content && (content.size || content.Size);
        if (size) {
            if (size.width || size.Width) img.style.width = (size.width || size.Width) + 'px';
            if (size.height || size.Height) img.style.height = (size.height || size.Height) + 'px';
        }
        figure.appendChild(img);
        var caption = content && (content.caption || content.Caption);
        if (caption) {
            var figcaption = document.createElement('figcaption');
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
        img.addEventListener('load', function () {
            figure.setAttribute('data-image-load-state', 'loaded');
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

        if (!layout) {
            figure.setAttribute('data-floating-inline', 'true');
            figure.setAttribute('data-wrap-mode', '0');
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

        figure.setAttribute('data-floating-inline', inline === false ? 'false' : 'true');
        figure.setAttribute('data-wrap-mode', String(wrapMode.value));
        figure.setAttribute('data-horizontal-relative-to', String(horizontal.value));
        figure.setAttribute('data-vertical-relative-to', String(vertical.value));
        figure.setAttribute('data-image-x', String(x));
        figure.setAttribute('data-image-y', String(y));
        figure.setAttribute('data-lock-anchor', lockAnchor ? 'true' : 'false');

        if (inline !== false) return;

        figure.classList.add(
            'tm-wysiwyg-image--floating',
            'tm-wysiwyg-image--wrap-' + wrapMode.css,
            'tm-wysiwyg-image--relative-' + horizontal.css,
            'tm-wysiwyg-image--vrelative-' + vertical.css);
        figure.style.left = x + 'px';
        figure.style.top = y + 'px';
        if (z !== 0) figure.style.zIndex = String(z);
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

    function _insertImageBlock(inst, block, dispatchPatch) {
        if (!block) return;

        var blockEl = _renderBlock(block, inst);
        if (!blockEl) return;

        var sel = window.getSelection();
        var anchorBlock = null;
        if (sel && sel.rangeCount > 0) {
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
                selection: inst.lastSelectionSnapshot || _captureSelectionSnapshot(inst),
                protocolVersion: inst.options.protocolVersion || 1
            });
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
        if (skipLocalRender) {
            if (inst.pendingLocalSnapshotSkips > 0) {
                inst.pendingLocalSnapshotSkips--;
            }
            _invokeDotNet(inst, 'HandleSnapshotApplied');
            return;
        }
        _hideInlineRevisionReview(inst);
        _renderDocument(inst);
        inst.hasRenderedDocument = true;
        _applyReviewDisplayMode(inst);
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

    function _applyRemoteOperationCore(inst, operation) {
        var type = operation.type ?? operation.Type;
        if (type === 'InsertText' || type === 0) {
            return _applyRemoteInsertText(inst, operation);
        }
        if (type === 'DeleteText' || type === 1) {
            return _applyRemoteDeleteText(inst, operation);
        }
        if (type === 'AddInlineMark' || type === 'AddMark' || type === 2) {
            return _applyRemoteInlineMark(inst, operation, true);
        }
        if (type === 'RemoveInlineMark' || type === 'RemoveMark' || type === 3) {
            return _applyRemoteInlineMark(inst, operation, false);
        }
        if (type === 'InsertBlock' || type === 4) {
            return _applyRemoteInsertBlock(inst, operation);
        }
        if (type === 'DeleteBlock' || type === 5) {
            return _applyRemoteDeleteBlock(inst, operation);
        }
        if (type === 'MoveBlock' || type === 6) {
            return _applyRemoteMoveBlock(inst, operation);
        }
        if (type === 'SetBlockAttribute' || type === 7) {
            return _applyRemoteSetBlockAttribute(inst, operation);
        }
        if (type === 'UpdateBlock' || type === 8) {
            return _applyRemoteUpdateBlock(inst, operation);
        }
        if (type === 'CreateRevision' || type === 9) {
            return _applyRemoteCreateRevision(inst, operation);
        }
        if (type === 'AcceptRevision' || type === 10) {
            return _applyRemoteReviewRevision(inst, operation, 'Accepted');
        }
        if (type === 'RejectRevision' || type === 11) {
            return _applyRemoteReviewRevision(inst, operation, 'Rejected');
        }

        return false;
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
        var ordered = _sortRemoteBatchOperations(operations);
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
        if (!inline) return false;

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
        if (!inline) return false;

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

    function _updateSnapshotInlineText(inst, target, update) {
        var blockId = target.blockId || target.BlockId || '';
        var inlineId = target.inlineId || target.InlineId || '';
        var inlineIndex = target.inlineIndex ?? target.InlineIndex ?? 0;
        var block = _findSnapshotBlock(inst, blockId);
        var content = block && (block.content || block.Content);
        var inlines = content && (content.inlines || content.Inlines);
        if (!Array.isArray(inlines)) return;

        var inline = null;
        for (var i = 0; i < inlines.length; i++) {
            if ((inlines[i].id || inlines[i].Id) === inlineId) {
                inline = inlines[i];
                break;
            }
        }
        if (!inline) inline = inlines[inlineIndex || 0];
        if (!inline) return;

        var current = inline.text ?? inline.Text ?? '';
        var updated = update(String(current));
        if ('text' in inline) inline.text = updated;
        inline.Text = updated;
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
        _applyFloatingImageLayout(existing, content, inst);
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

        if (start === 0 && end >= text.length) {
            _applyMarks(inline, [mark]);
            return true;
        }

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

    function clearRevisionDecorations(instanceId, revisionId, removeContent) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !inst.root || !revisionId) return;

        _hideInlineRevisionReview(inst);
        var escaped = _cssEscape(String(revisionId));
        var nodes = inst.root.querySelectorAll('[data-revision-id="' + escaped + '"]');
        nodes.forEach(function (node) {
            if (removeContent) {
                node.remove();
                return;
            }

            node.classList.remove('tm-wysiwyg-revision', 'tm-wysiwyg-revision--insert', 'tm-wysiwyg-revision--delete', 'tm-wysiwyg-revision--format');
            node.removeAttribute('data-revision-id');
            node.removeAttribute('data-revision-type');
            if ((node.getAttribute('data-testid') || '').indexOf('document-wysiwyg-revision-') === 0) {
                node.removeAttribute('data-testid');
            }
        });
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

        popover.style.left = Math.max(0, left) + 'px';
        popover.style.top = Math.max(0, top) + 'px';
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
        value = value.toLowerCase();
        if (value === 'simplemarkup' || value === 'simple') return 'SimpleMarkup';
        if (value === 'nomarkup' || value === 'none') return 'NoMarkup';
        return 'AllMarkup';
    }

    function _applyReviewDisplayMode(inst) {
        if (!inst || !inst.root) return;
        var mode = _normalizeReviewDisplayMode(inst.reviewDisplayMode);
        inst.reviewDisplayMode = mode;
        inst.root.classList.remove(
            'tm-wysiwyg-host--review-all-markup',
            'tm-wysiwyg-host--review-simple-markup',
            'tm-wysiwyg-host--review-no-markup');
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
        var body = inst.root.querySelector('.tm-wysiwyg-page__body[contenteditable="true"]');
        (body || inst.root).focus({ preventScroll: true });
    }

    function getSelectionSnapshot(instanceId) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return null;
        return _toPascalSelection(_captureSelectionSnapshot(inst));
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

    /**
     * Executes an editor command from the Blazor ribbon.
     * @param {string} instanceId
     * @param {string} command — e.g. "toggleMark", "insertBlock"
     * @param {Object} payload
     */
    function executeCommand(instanceId, command, payload) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed || inst.readOnly) return;
        _flushPendingInputPatch(inst);
        _flushSelectionNotification(inst);

        switch (command) {
            case 'toggleMark':
                payload = payload || {};
                _executeToggleMarkCommand(inst, payload.markType || payload.MarkType || 'Bold', payload);
                break;
            case 'applyLink':
                payload = payload || {};
                payload.markType = 'Link';
                _executeToggleMarkCommand(inst, 'Link', payload);
                break;
            case 'clearFormatting':
                _executeClearFormattingCommand(inst);
                break;
            case 'setParagraphProperties':
                _executeSetParagraphPropertiesCommand(inst, payload || {});
                break;
            case 'insertBlock':
                payload = payload || {};
                payload.selection = _captureSelectionSnapshot(inst);
                _invokeDotNet(inst, 'HandleCommandInsertBlock', payload);
                break;
            case 'insertImageUrl':
                var block = _createImageBlockFromPayload(payload);
                if (block) {
                    _insertImageBlock(inst, block, true);
                }
                break;
            case 'setImageWrapMode':
                _setSelectedImageWrapMode(inst, payload);
                break;
            // Phase 13: table structural commands.
            case 'insertTable':
                _insertTable(inst);
                break;
            case 'insertTableRow':
                _insertTableRow(inst);
                break;
            case 'deleteTableRow':
                _deleteTableRow(inst);
                break;
            case 'insertTableColumn':
                _insertTableColumn(inst);
                break;
            case 'deleteTableColumn':
                _deleteTableColumn(inst);
                break;
            case 'mergeTableCells':
                _mergeTableCells(inst);
                break;
            case 'splitTableCell':
                _splitTableCell(inst);
                break;
            case 'undo':
                _flushPendingInputPatch(inst);
                _flushSelectionNotification(inst);
                _invokeDotNet(inst, 'HandleUndoRequested');
                break;
            case 'redo':
                _flushPendingInputPatch(inst);
                _flushSelectionNotification(inst);
                _invokeDotNet(inst, 'HandleRedoRequested');
                break;
            default:
                console.warn('tmDocumentWysiwyg: unknown command', command);
        }
    }

    function _executeToggleMarkCommand(inst, markType, payload) {
        var normalizedMark = _normalizeMarkType(markType);
        if (!normalizedMark) return;

        payload = payload || {};
        var explicitSelection = payload.selection || payload.Selection || null;
        if (explicitSelection) {
            _restoreSelection(inst, explicitSelection);
        }
        _ensureEditorSelection(inst);
        var beforeSelection = _captureSelectionSnapshot(inst);
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
        inst.lastSelectionSnapshot = afterSelection;
        _scheduleSelectionNotification(inst, afterSelection);

        if (result && result.collapsed) {
            return;
        }

        _beginTypingTransaction(inst);
        _dispatchPatch(inst, {
            type: normalizedMark === 'Link' || _isValueMark(normalizedMark) ? 'SetMarks' : 'ToggleMark',
            markType: normalizedMark,
            data: normalizedMark === 'Link' || _isValueMark(normalizedMark) ? data : null,
            linkTitle: normalizedMark === 'Link' ? title || null : null,
            selection: beforeSelection,
            beforeSelection: beforeSelection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
    }

    function _executeClearFormattingCommand(inst) {
        _ensureEditorSelection(inst);
        var beforeSelection = _captureSelectionSnapshot(inst);
        var sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || sel.isCollapsed || !inst.root.contains(sel.anchorNode)) {
            _restoreSelection(inst, inst.lastSelectionSnapshot);
            sel = window.getSelection();
            if (!sel || sel.rangeCount === 0 || sel.isCollapsed || !inst.root.contains(sel.anchorNode)) {
                inst.pendingTypingMarks = {};
                return;
            }
        }

        var clearable = ['Bold', 'Italic', 'Underline', 'Strikethrough', 'Superscript', 'Subscript', 'FontFamily', 'FontSize', 'TextColor', 'Highlight'];
        clearable.forEach(function (mark) {
            _applyToggleMarkToDom(inst, mark, '', true);
        });
        var afterSelection = _captureSelectionSnapshot(inst);
        _beginTypingTransaction(inst);
        _dispatchPatch(inst, {
            type: 'ClearFormatting',
            selection: beforeSelection,
            beforeSelection: beforeSelection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
    }

    function _executeSetParagraphPropertiesCommand(inst, payload) {
        _ensureEditorSelection(inst);
        var beforeSelection = _captureSelectionSnapshot(inst);
        if (!beforeSelection) return;

        var patch = payload.paragraphProperties || payload.ParagraphProperties || payload;
        patch = _sanitizeParagraphPropertiesPatch(patch);
        if (!_hasParagraphPropertiesPatch(patch)) return;

        var blocks = _getSelectedBlockElements(inst, beforeSelection);
        if (blocks.length === 0) return;

        blocks.forEach(function (block) {
            _applyParagraphPropertiesPatch(block, patch);
        });

        var afterSelection = _captureSelectionSnapshot(inst) || beforeSelection;
        inst.lastSelectionSnapshot = afterSelection;
        _focusEditorBody(inst);
        _restoreSelection(inst, afterSelection);
        _scheduleSelectionNotification(inst, afterSelection);
        _beginTypingTransaction(inst);
        _dispatchPatch(inst, {
            type: 'SetParagraphProperties',
            paragraphProperties: patch,
            selection: beforeSelection,
            beforeSelection: beforeSelection,
            afterSelection: afterSelection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1
        });
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

        var range = sel.getRangeAt(0);
        var startInfo = _mapNodeToBlockInline(range.startContainer, range.startOffset, inst.root);
        var endInfo = _mapNodeToBlockInline(range.endContainer, range.endOffset, inst.root);
        if (!startInfo || !endInfo || startInfo.blockId !== endInfo.blockId || startInfo.inlineId !== endInfo.inlineId) {
            return _wrapSelectionWithMark(range, markType, data, forceRemove, title);
        }

        var block = inst.root.querySelector('[data-block-id="' + _cssEscape(startInfo.blockId || '') + '"]');
        var inline = block && block.querySelector('[data-inline-id="' + _cssEscape(startInfo.inlineId || '') + '"]');
        if (!inline) return null;

        var start = Math.min(startInfo.offset, endInfo.offset);
        var end = Math.max(startInfo.offset, endInfo.offset);
        var removed = forceRemove || (!_isValueMark(markType) && _rangeHasDomMark(inline, start, end, markType));
        _splitInlineForMark(inline, start, end, markType, data, removed, title);

        return { collapsed: false };
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
        return /^#[0-9a-f]{6}$/i.test(raw) ? raw : '';
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

    function _getElementMarkState(el) {
        var computed = window.getComputedStyle ? window.getComputedStyle(el) : el.style;
        var fontWeight = computed.fontWeight || el.style.fontWeight || '';
        var textDecoration = computed.textDecorationLine || computed.textDecoration || el.style.textDecoration || '';
        return {
            Bold: fontWeight === 'bold' || fontWeight === '700' || parseInt(fontWeight, 10) >= 700,
            Italic: (computed.fontStyle || el.style.fontStyle) === 'italic',
            Underline: textDecoration.indexOf('underline') >= 0,
            Strikethrough: textDecoration.indexOf('line-through') >= 0,
            Link: !!el.closest('a[href], [data-link-href]'),
            FontFamily: el.style.fontFamily || '',
            FontSize: el.style.fontSize || '',
            TextColor: el.style.color || '',
            Highlight: el.style.backgroundColor || ''
        };
    }

    function _getFormattingState(inst) {
        return {
            Bold: _getSelectionMarkState(inst, 'Bold'),
            Italic: _getSelectionMarkState(inst, 'Italic'),
            Underline: _getSelectionMarkState(inst, 'Underline')
        };
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
                    return range.intersectsNode(node) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
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
        var figure = _getSelectedImageFigure(inst);
        if (!figure) return;
        payload = payload || {};
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
        _applyFloatingImageLayout(figure, { FloatingLayout: layout }, inst);
        _ensureImageResizeHandle(figure, inst);
        _dispatchImageUpdatePatch(inst, figure);
    }

    function _getSelectedImageFigure(inst) {
        if (inst && inst.selectedImageFigure && inst.selectedImageFigure.isConnected && inst.root.contains(inst.selectedImageFigure)) {
            return inst.selectedImageFigure;
        }

        var sel = window.getSelection();
        if (sel && sel.rangeCount > 0) {
            var node = sel.anchorNode;
            var el = node && (node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement);
            var selected = el && el.closest && el.closest('figure.tm-wysiwyg-image');
            if (selected && inst.root.contains(selected)) return selected;
        }

        return inst.root.querySelector('figure.tm-wysiwyg-image');
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
            startOffset: start.offset,
            endInlineIndex: Math.max(0, endInlineIndex),
            endOffset: end.offset
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
            Comments: baseDoc.comments || baseDoc.Comments || [],
            Notes: baseDoc.notes || baseDoc.Notes || [],
            HeadersFooters: headersFooters,
            Revisions: baseDoc.revisions || baseDoc.Revisions || [],
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
                content = { $type: 'list', Ordered: false, Inlines: _serializeInlines(blockEl.querySelector('li') || blockEl) };
                break;
            case 'ol':
                type = 2; // List
                content = { $type: 'list', Ordered: true, Inlines: _serializeInlines(blockEl.querySelector('li') || blockEl) };
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

        if (tag === 'sup' && className.indexOf('tm-wysiwyg-note-ref') >= 0) {
            return {
                $type: 'noteReference',
                Id: id,
                NoteId: el.getAttribute('data-note-id') || '',
                DisplayMarker: el.textContent || '',
                NoteType: 'Footnote'
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
        var computed = window.getComputedStyle ? window.getComputedStyle(el) : el.style;

        var fontWeight = computed.fontWeight || style.fontWeight;
        if (fontWeight === 'bold' || fontWeight === '700' || parseInt(fontWeight, 10) >= 700) {
            marks.push({ Type: 0 }); // Bold
        }

        if ((computed.fontStyle || style.fontStyle) === 'italic') {
            marks.push({ Type: 1 }); // Italic
        }

        var textDeco = computed.textDecorationLine || computed.textDecoration || style.textDecoration || '';
        if (textDeco.indexOf('underline') >= 0) {
            marks.push({ Type: 2 }); // Underline
        }
        if (textDeco.indexOf('line-through') >= 0) {
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

        var vAlign = computed.verticalAlign || style.verticalAlign;
        var fSize = computed.fontSize || style.fontSize;
        if (vAlign === 'super' && fSize === 'smaller') {
            marks.push({ Type: 4 }); // Superscript
        }
        if (vAlign === 'sub' && fSize === 'smaller') {
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
                cells.push({
                    Id: cellId,
                    ColumnSpan: parseInt(td.getAttribute('colspan') || '1', 10),
                    RowSpan: parseInt(td.getAttribute('rowspan') || '1', 10),
                    Blocks: _serializeBlocksFromContainer(td)
                });
            }
            rows.push({ Cells: cells });
        }
        return { $type: 'table', Rows: rows };
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
        }
        var source = parseInt(figureEl.getAttribute('data-image-source') || '0', 10);
        var assetId = figureEl.getAttribute('data-image-asset-id') || '';
        var content = {
            $type: 'image',
            Source: source,
            Url: img ? img.src : '',
            AltText: img ? img.alt : '',
            Caption: figcaption ? figcaption.textContent : ''
        };
        if (assetId) content.AssetId = assetId;
        if (Object.keys(size).length > 0) content.Size = size;
        var inline = figureEl.getAttribute('data-floating-inline') !== 'false';
        var wrapMode = parseInt(figureEl.getAttribute('data-wrap-mode') || (inline ? '0' : '1'), 10);
        var horizontal = parseInt(figureEl.getAttribute('data-horizontal-relative-to') || '0', 10);
        var vertical = parseInt(figureEl.getAttribute('data-vertical-relative-to') || '3', 10);
        var x = parseFloat(figureEl.getAttribute('data-image-x') || '0') || 0;
        var y = parseFloat(figureEl.getAttribute('data-image-y') || '0') || 0;
        var z = parseInt(figureEl.style.zIndex || '0', 10) || 0;
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

        return {
            SnapshotApplyCount: inst.renderStats ? inst.renderStats.snapshotApplies : 0,
            FullRenderCount: inst.renderStats ? inst.renderStats.fullRenders : 0,
            RemoteOperationApplyCount: inst.renderStats ? inst.renderStats.remoteOperations : 0,
            RemoteOperationBatchCount: inst.renderStats ? inst.renderStats.remoteBatches : 0,
            MeasureCount: inst.measureStats.count,
            MeasureCacheHits: inst.measureStats.cacheHits,
            MeasureInvalidations: inst.measureStats.invalidations,
            MeasureCacheSize: inst.measureCache.size,
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
            AcceptingNativeInput: !!inst.acceptingNativeInput,
            CurrentTransactionId: inst.currentTransactionId || null,
            PendingTransactionId: pending ? (pending.transactionId || pending.TransactionId || null) : null,
            PendingPatchType: pending ? (pending.type || pending.Type || null) : null,
            QueuedRemoteBatchCount: inst.queuedRemoteBatches ? inst.queuedRemoteBatches.length : 0,
            LastInputType: inst.lastInputType || null,
            LastInputDataLength: inst.lastInputDataLength || 0,
            LastPatchType: inst.lastPatchType || null,
            LastPatchId: inst.lastPatchId || null,
            LastPatchTransactionId: inst.lastPatchTransactionId || null,
            LastPatchAt: inst.lastPatchAt || null,
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
            TableCellPath: selection.tableCellPath || selection.TableCellPath || null
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
        inst.renderStats = { snapshotApplies: 0, fullRenders: 0, remoteOperations: 0, remoteBatches: 0 };
        inst.measureCache.clear();
    }

    function refreshVirtualization(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed || !inst.virtualPages || inst.virtualPages.length === 0) return;
        _renderVirtualizedPages(inst, true);
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
        getSnapshot: getSnapshot,
        focus: focus,
        getSelectionSnapshot: getSelectionSnapshot,
        setTrackChangesEnabled: setTrackChangesEnabled,
        setReviewDisplayMode: setReviewDisplayMode,
        setReadOnly: setReadOnly,
        scrollToRevision: scrollToRevision,
        clearRevisionDecorations: clearRevisionDecorations,
        restoreSelection: restoreSelection,
        getFormattingState: getFormattingState,
        getLinkInfo: getLinkInfo,
        executeCommand: executeCommand,
        measureBlockForDebug: measureBlockForDebug,
        getDebugMetrics: getDebugMetrics,
        getDebugSnapshot: getDebugSnapshot,
        clearDebugMetrics: clearDebugMetrics,
        refreshVirtualization: refreshVirtualization,
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
            }
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
    };
})();

window.tmDocumentEditorWysiwyg = window.tmDocumentWysiwyg;
