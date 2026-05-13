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
            pendingSelectionSnapshot: null,
            pendingSelectionTimer: null,
            pendingLocalSnapshotSkips: 0,
            lastSelectionSnapshot: null,
            measureCache: new Map(),
            measureStats: { count: 0, cacheHits: 0, invalidations: 0 },
            virtualPages: [],
            virtualState: null,
            virtualizationScrollTimer: null,
            hasRenderedDocument: false,
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

        if (inst.virtualizationScrollTimer) {
            clearTimeout(inst.virtualizationScrollTimer);
            inst.virtualizationScrollTimer = null;
        }

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
        inst._handleCompositionEnd = function (e) { inst.compositionActive = false; _onInput(inst, e); };
        inst._handleSelectionChange = function () { _onSelectionChange(inst); };
        inst._handleKeyDown = function (e) { _onKeyDown(inst, e); };
        inst._handlePointerDown = function (e) { _onFloatingImagePointerDown(inst, e); };

        inst.root.addEventListener('beforeinput', inst._handleBeforeInput, true);
        inst.root.addEventListener('input', inst._handleInput, true);
        inst.root.addEventListener('paste', inst._handlePaste, true);
        inst.root.addEventListener('copy', inst._handleCopy, true);
        inst.root.addEventListener('compositionstart', inst._handleCompositionStart, true);
        inst.root.addEventListener('compositionend', inst._handleCompositionEnd, true);
        document.addEventListener('selectionchange', inst._handleSelectionChange);
        inst.root.addEventListener('keydown', inst._handleKeyDown, true);
        inst.root.addEventListener('pointerdown', inst._handlePointerDown, true);
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
        if (inst._handleVirtualScroll) {
            inst.root.removeEventListener('scroll', inst._handleVirtualScroll);
            window.removeEventListener('scroll', inst._handleVirtualScroll);
            window.removeEventListener('resize', inst._handleVirtualScroll);
        }
    }

    // ── Input pipeline ───────────────────────────────────────────────────────

    function _onBeforeInput(inst, event) {
        if (inst.readOnly || inst.compositionActive) return;

        var inputType = event.inputType;
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

        inst.pendingNativeInputSelection = selection;
        inst.acceptingNativeInput = true;
        if (inst.nativeInputTimer) {
            clearTimeout(inst.nativeInputTimer);
        }
        inst.nativeInputTimer = setTimeout(function () {
            inst.acceptingNativeInput = false;
            inst.nativeInputTimer = null;
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

    function _ensureEditableSelection(inst, target) {
        var sel = window.getSelection();
        if (sel && sel.rangeCount > 0 && _nodeBelongsToRoot(sel.anchorNode, inst.root)) {
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

        var range = document.createRange();
        range.selectNodeContents(editable);
        range.collapse(false);
        sel = window.getSelection();
        if (!sel) return;
        sel.removeAllRanges();
        sel.addRange(range);
    }

    function _onInput(inst, event) {
        if (inst.readOnly || inst.compositionActive) return;
        if (_shouldSuppressBrowserInputEvent(inst, event.inputType)) {
            return;
        }
        _invalidateMeasureCache(inst);

        const inputType = event.inputType;
        const data = event.data;

        const selection = inst.pendingNativeInputSelection || _captureSelectionSnapshot(inst);
        inst.pendingNativeInputSelection = null;
        if (inst.nativeInputTimer) {
            clearTimeout(inst.nativeInputTimer);
        }
        inst.nativeInputTimer = setTimeout(function () {
            inst.acceptingNativeInput = false;
            inst.nativeInputTimer = null;
        }, 0);

        const afterSelection = _captureSelectionSnapshot(inst);
        inst.lastSelectionSnapshot = afterSelection;
        _scheduleSelectionNotification(inst, afterSelection);

        _dispatchInputPatch(inst, inputType, data, selection);
    }

    function _dispatchInputPatch(inst, inputType, data, selection) {
        _beginTypingTransaction(inst);
        var patch = {
            type: _mapInputTypeToPatchType(inputType),
            data: data,
            selection: selection,
            transactionId: inst.currentTransactionId,
            protocolVersion: inst.options.protocolVersion || 1,
        };

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
        _invokeDotNet(inst, 'HandlePatchGenerated', patch);
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
        if (inst.readOnly) return;

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

        var blockId = 'tbl-' + Date.now();
        var rows = [];
        var tableBlock = document.createElement('table');
        tableBlock.className = 'tm-wysiwyg-table tm-wysiwyg-block';
        tableBlock.setAttribute('data-block-id', blockId);
        for (var r = 0; r < 2; r++) {
            var tr = document.createElement('tr');
            var rowCells = [];
            for (var c = 0; c < 2; c++) {
                var cellId = 'tc-' + Date.now() + '-' + r + '-' + c;
                var td = document.createElement('td');
                td.setAttribute('data-cell-id', cellId);
                var p = document.createElement('p');
                p.className = 'tm-wysiwyg-block';
                p.setAttribute('data-block-id', '');
                p.innerHTML = '<br>';
                td.appendChild(p);
                tr.appendChild(td);
                rowCells.push({ Id: cellId, ColumnSpan: 1, RowSpan: 1, Blocks: [] });
            }
            tableBlock.appendChild(tr);
            rows.push({ Cells: rowCells });
        }

        var range = sel.getRangeAt(0);
        range.deleteContents();
        range.insertNode(tableBlock);
        range.setStartAfter(tableBlock);
        range.setEndAfter(tableBlock);
        sel.removeAllRanges();
        sel.addRange(range);

        // Notify Blazor about the new table block.
        _dispatchPatch(inst, {
            type: 'InsertBlock',
            blockType: 'Table',
            block: {
                Id: blockId,
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
            ? handle.closest('.tm-wysiwyg-image--floating')
            : event.target && event.target.closest && event.target.closest('.tm-wysiwyg-image--floating');
        if (!figure || !inst.root.contains(figure)) return;

        event.preventDefault();
        var img = figure.querySelector('img');
        var startX = event.clientX;
        var startY = event.clientY;
        var initialX = parseFloat(figure.getAttribute('data-image-x') || '0') || 0;
        var initialY = parseFloat(figure.getAttribute('data-image-y') || '0') || 0;
        var initialWidth = img ? (parseFloat(img.style.width) || img.getBoundingClientRect().width || 120) : 120;
        var initialHeight = img ? (parseFloat(img.style.height) || img.getBoundingClientRect().height || 90) : 90;
        figure.classList.add('tm-wysiwyg-image--dragging');
        figure.setAttribute('data-drag-feedback', handle ? 'resize' : 'move');

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
            _dispatchImageUpdatePatch(inst, figure);
        }

        document.addEventListener('pointermove', onMove, true);
        document.addEventListener('pointerup', onUp, true);
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
            var p = document.createElement('p');
            p.className = 'tm-wysiwyg-block';
            p.setAttribute('data-block-id', '');
            p.innerHTML = '<br>';
            td.appendChild(p);
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
            var p = document.createElement('p');
            p.className = 'tm-wysiwyg-block';
            p.setAttribute('data-block-id', '');
            p.innerHTML = '<br>';
            td.appendChild(p);
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

        // Phase 13: detect if selection is inside a table cell.
        var activeTableCellId = _findTableCellId(sel.anchorNode);

        return {
            anchorBlockId: anchor.blockId,
            anchorInlineId: anchor.inlineId,
            anchorOffset: anchor.offset,
            focusBlockId: focus.blockId,
            focusInlineId: focus.inlineId,
            focusOffset: focus.offset,
            isCollapsed: sel.isCollapsed,
            direction: direction,
            activeTableCellId: activeTableCellId
        };
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

    /**
     * Maps a DOM node and offset to the nearest block/inline identifiers.
     * Normalizes element-node offsets into text-node character offsets.
     */
    function _mapNodeToBlockInline(node, offset, root) {
        var normalized = _normalizeToTextNode(node, offset);
        var textNode = normalized.node;
        var textOffset = normalized.offset;

        var el = textNode.parentElement;
        if (!el) return null;

        // Walk up to find the nearest block with data-block-id.
        var blockEl = el.closest('[data-block-id]');
        if (!blockEl || !root.contains(blockEl)) {
            blockEl = root.querySelector('[data-block-id]');
        }
        if (!blockEl) return null;

        // Find the nearest inline with data-inline-id.
        var inlineEl = el.closest('[data-inline-id]');
        if (!inlineEl || !blockEl.contains(inlineEl)) {
            inlineEl = blockEl.querySelector('[data-inline-id]');
        }

        return {
            blockId: blockEl.getAttribute('data-block-id'),
            inlineId: inlineEl ? inlineEl.getAttribute('data-inline-id') : null,
            offset: textOffset,
        };
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
            if (child && child.nodeType === Node.TEXT_NODE) {
                return { node: child, offset: 0 };
            }
            if (child && child.nodeType === Node.ELEMENT_NODE) {
                var deepest = _firstDeepTextNode(child);
                if (deepest) return { node: deepest, offset: 0 };
            }
            // Fallback: previous sibling's last text node.
            var prev = node.childNodes[offset - 1];
            if (prev) {
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

        var anchorInfo = _resolveSnapshotPosition(root, snapshot.anchorBlockId, snapshot.anchorInlineId, snapshot.anchorOffset);
        var focusInfo = _resolveSnapshotPosition(root, snapshot.focusBlockId, snapshot.focusInlineId, snapshot.focusOffset);

        if (!anchorInfo || !focusInfo) return;

        var sel = window.getSelection();
        if (!sel) return;
        sel.removeAllRanges();

        var range = document.createRange();
        range.setStart(anchorInfo.node, anchorInfo.offset);
        range.setEnd(focusInfo.node, focusInfo.offset);
        sel.addRange(range);
    }

    function _resolveSnapshotPosition(root, blockId, inlineId, offset) {
        var blockEl = root.querySelector('[data-block-id="' + (blockId || '') + '"]');
        if (!blockEl) {
            // Fallback: first block.
            blockEl = root.querySelector('[data-block-id]');
        }
        if (!blockEl) return null;

        var inlineEl = blockEl.querySelector('[data-inline-id="' + (inlineId || '') + '"]');
        if (!inlineEl) {
            // Fallback: first inline inside the block.
            inlineEl = blockEl.querySelector('[data-inline-id]');
        }
        if (!inlineEl) {
            // Fallback: place cursor at the beginning of the block.
            return { node: blockEl, offset: 0 };
        }

        var textNode = _firstDeepTextNode(inlineEl);
        if (!textNode) {
            return { node: inlineEl, offset: 0 };
        }

        var clampedOffset = Math.max(0, Math.min(offset || 0, textNode.textContent.length));
        return { node: textNode, offset: clampedOffset };
    }

    // ── Patch dispatch ───────────────────────────────────────────────────────

    function _mapInputTypeToPatchType(inputType) {
        switch (inputType) {
            case 'insertText': return 'InsertText';
            case 'insertParagraph': return 'InsertParagraph';
            case 'insertLineBreak': return 'InsertLineBreak';
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

        inst._applyingOwnPatch = true;
        inst.root.innerHTML = '';
        inst.root.removeAttribute('contenteditable');
        // Phase 11: enable paginated layout mode on the host root.
        inst.root.classList.add('tm-wysiwyg-host--paginated');

        const blocks = doc.blocks || doc.Blocks || [];
        const pageSettings = _normalizePageSettings(doc.pageSettings || doc.PageSettings || {});

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
            scope = (pageIndex % 2 === 0) ? 'EvenPages' : 'OddPages';
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
        el.setAttribute('data-testid', type === 'header' ? 'document-wysiwyg-header' : 'document-wysiwyg-footer');

        var blocks = hf.blocks || hf.Blocks || [];
        if (blocks.length === 0) {
            el.classList.add('tm-wysiwyg-page__' + type + '--empty');
        }

        for (var i = 0; i < blocks.length; i++) {
            var blockEl = _renderBlock(blocks[i], inst);
            if (blockEl) {
                el.appendChild(blockEl);
            }
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
                _renderInlines(el, content);
                break;
            case 'Heading':
            case 1:
                el = document.createElement('h' + ((content && (content.level || content.Level)) || 1));
                _renderInlines(el, content);
                break;
            case 'List':
            case 2:
                el = document.createElement((content && (content.ordered || content.Ordered)) ? 'ol' : 'ul');
                var li = document.createElement('li');
                _renderInlines(li, content);
                el.appendChild(li);
                break;
            case 'Quote':
            case 3:
                el = document.createElement('blockquote');
                _renderInlines(el, content);
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
                _renderInlines(el, content);
                break;
        }

        if (el) {
            el.setAttribute('data-block-id', id || '');
            el.classList.add('tm-wysiwyg-block');
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

    function _renderInlines(container, content) {
        if (!content) return;
        var inlines = content.inlines || content.Inlines || [];

        for (var i = 0; i < inlines.length; i++) {
            var inline = inlines[i];
            var inlineType = inline.$type || inline.type || inline.Type;
            var inlineId = inline.id || inline.Id;

            if (inlineType === 'text' || inlineType === 'TextRun') {
                var span = document.createElement('span');
                span.setAttribute('data-inline-id', inlineId || '');
                span.textContent = inline.text || inline.Text || '';
                _applyMarks(span, inline.marks || inline.Marks);
                container.appendChild(span);
            } else if (inlineType === 'token' || inlineType === 'TokenRun') {
                var tokenSpan = document.createElement('span');
                tokenSpan.setAttribute('data-inline-id', inlineId || '');
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

    function _applyMarks(el, marks) {
        if (!marks || marks.length === 0) return;
        for (var i = 0; i < marks.length; i++) {
            var mark = marks[i];
            var markType = mark.type || mark.Type;
            switch (markType) {
                case 'Bold': case 0: el.style.fontWeight = 'bold'; break;
                case 'Italic': case 1: el.style.fontStyle = 'italic'; break;
                case 'Underline': case 2: el.style.textDecoration = (el.style.textDecoration || '') + ' underline'; break;
                case 'Strikethrough': case 3: el.style.textDecoration = (el.style.textDecoration || '') + ' line-through'; break;
                case 'Superscript': case 4: el.style.verticalAlign = 'super'; el.style.fontSize = 'smaller'; break;
                case 'Subscript': case 5: el.style.verticalAlign = 'sub'; el.style.fontSize = 'smaller'; break;
                case 'Link': case 6:
                    var href = (mark.link || mark.Link || {}).href || (mark.link || mark.Link || {}).Href || '#';
                    var wrapper = document.createElement('a');
                    wrapper.href = href;
                    wrapper.style.color = 'var(--tm-color-primary)';
                    wrapper.style.textDecoration = 'underline';
                    wrapper.setAttribute('data-inline-id', el.getAttribute('data-inline-id'));
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
                    var emptyP = document.createElement('p');
                    emptyP.className = 'tm-wysiwyg-block';
                    emptyP.setAttribute('data-block-id', '');
                    emptyP.innerHTML = '<br>';
                    td.appendChild(emptyP);
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
        _applyFloatingImageLayout(figure, content, inst);
        return figure;
    }

    function _applyFloatingImageLayout(figure, content, inst) {
        var layout = content && (content.floatingLayout || content.FloatingLayout);
        if (!layout) return;

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
     */
    function applySnapshot(instanceId, snapshot) {
        const inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return;
        const editorHasFocus = _hasEditorSelectionOrFocus(inst);
        const skipLocalRender = inst.hasRenderedDocument
            && !inst.readOnly
            && editorHasFocus;
        inst.snapshot = snapshot;
        if (skipLocalRender) {
            if (inst.pendingLocalSnapshotSkips > 0) {
                inst.pendingLocalSnapshotSkips--;
            }
            _invokeDotNet(inst, 'HandleSnapshotApplied');
            return;
        }
        _renderDocument(inst);
        inst.hasRenderedDocument = true;
        _invokeDotNet(inst, 'HandleSnapshotApplied');
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
        var body = inst.root.querySelector('.tm-wysiwyg-page__body[contenteditable="true"]');
        (body || inst.root).focus();
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
                payload.selection = _captureSelectionSnapshot(inst);
                _invokeDotNet(inst, 'HandleCommandToggleMark', payload);
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
        _applyFloatingImageLayout(figure, { FloatingLayout: layout });
        _dispatchImageUpdatePatch(inst, figure);
    }

    function _getSelectedImageFigure(inst) {
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
            for (var pi = 0; pi < inst.virtualPages.length; pi++) {
                if (pi > 0) {
                    blocks.push(_createSerializedPageBreak(blocks.length));
                }

                var pageData = inst.virtualPages[pi];
                for (var bj = 0; bj < pageData.blocks.length; bj++) {
                    var source = pageData.blocks[bj];
                    var cloned = _cloneBlockForSnapshot(source, blocks.length);
                    if (cloned) blocks.push(cloned);
                }
            }

            return blocks;
        }

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
        var bodyBlocks = bodyContainer.querySelectorAll('.tm-wysiwyg-block[data-block-id]');
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
            Content: content
        };
        if (sectionId) block.SectionId = sectionId;
        return block;
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
        var text = el.textContent || '';
        var inline = {
            $type: 'text',
            Id: id,
            Text: text
        };
        if (marks.length > 0) inline.Marks = marks;
        return inline;
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

        var vAlign = computed.verticalAlign || style.verticalAlign;
        var fSize = computed.fontSize || style.fontSize;
        if (vAlign === 'super' && fSize === 'smaller') {
            marks.push({ Type: 4 }); // Superscript
        }
        if (vAlign === 'sub' && fSize === 'smaller') {
            marks.push({ Type: 5 }); // Subscript
        }

        var link = el.querySelector('a');
        if (link) {
            marks.push({
                Type: 6, // Link
                Link: { Href: link.getAttribute('href') || link.href || '#' }
            });
        }

        var commentId = el.getAttribute('data-comment-id');
        if (commentId) {
            marks.push({
                Type: 7, // CommentAnchor
                CommentAnchor: { CommentId: commentId }
            });
        }

        return marks;
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

    function clearDebugMetrics(instanceId) {
        var inst = _instances.get(instanceId);
        if (!inst || inst.disposed) return;
        inst.measureStats = { count: 0, cacheHits: 0, invalidations: 0 };
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
        getSnapshot: getSnapshot,
        focus: focus,
        restoreSelection: restoreSelection,
        executeCommand: executeCommand,
        measureBlockForDebug: measureBlockForDebug,
        getDebugMetrics: getDebugMetrics,
        clearDebugMetrics: clearDebugMetrics,
        refreshVirtualization: refreshVirtualization,
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
