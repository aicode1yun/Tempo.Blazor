/**
 * Tempo Blazor — Notion Editor JS Interop
 *
 * Sections:
 *   26.1  Block lifecycle      — initBlock / destroyBlock / getHtml / setHtml / focus*
 *   26.2  Selection/format     — getSelectionRange / getSelectionRect / applyFormat / …
 *   26.3  Drag & drop          — initDragDrop / destroyDragDrop  (ghost + drop-indicator)
 *   26.4  Slash menu           — getCaretCoords / getTextBeforeCaret
 *   26.5  Keyboard handler     — initKeyboardHandler  (Enter / Backspace / Tab / markdown)
 *   26.6  Inline math          — renderEquation / renderInlineMath  (KaTeX or fallback)
 *   26.7  Clipboard            — handlePaste / copyBlocksToClipboard
 *   26.8  Resize handle        — initResizeHandle / destroyResizeHandle
 *   26.9  Scroll / nav         — scrollToBlock / initSmoothScrollSpy / destroyScrollSpy
 *   30.1  Cover drag           — startCoverDrag
 */
window.tmNotionEditor = (function () {
    'use strict';

    // ── Internal registries ────────────────────────────────────────────────────
    const _blocks          = new WeakMap(); // element → { dotNetRef, listeners: [] }
    const _dragContainers  = new WeakMap();
    const _resizeHandles   = new WeakMap();
    const _scrollSpies     = new WeakMap();
    const _columnResizers  = new WeakMap();

    // ── Slash menu state ───────────────────────────────────────────────────────
    let _slashElement    = null; // contenteditable that triggered the slash menu
    let _slashAnchorNode = null; // text node position just before the '/'
    let _slashAnchorOff  = 0;

    // ── Mention / page-link menu state ─────────────────────────────────────────
    let _mentionElement    = null; // contenteditable that triggered the mention
    let _mentionAnchorNode = null; // text node position just before the trigger
    let _mentionAnchorOff  = 0;
    let _mentionTriggerLen = 1;    // 1 for '@', 2 for '[['

    // ── Shared helpers ─────────────────────────────────────────────────────────

    function _on(el, type, fn, opts) {
        el.addEventListener(type, fn, opts);
        return { el, type, fn, opts };
    }

    function _offAll(list) {
        for (const l of list) l.el.removeEventListener(l.type, l.fn, l.opts);
        list.length = 0;
    }

    function _range() {
        const sel = window.getSelection();
        return sel && sel.rangeCount > 0 ? sel.getRangeAt(0) : null;
    }

    function _applyRange(r) {
        const sel = window.getSelection();
        sel.removeAllRanges();
        sel.addRange(r);
    }

    function _setCursorAtEnd(el) {
        el.focus();
        const r = document.createRange();
        r.selectNodeContents(el);
        r.collapse(false);
        _applyRange(r);
    }

    function _setCursorAtStart(el) {
        el.focus();
        const r = document.createRange();
        r.setStart(el, 0);
        r.collapse(true);
        _applyRange(r);
    }

    function _setCursorAtOffset(el, offset) {
        el.focus();
        const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
        let rem = offset;
        let node;
        while ((node = walker.nextNode())) {
            if (rem <= node.length) {
                const r = document.createRange();
                r.setStart(node, rem);
                r.collapse(true);
                _applyRange(r);
                return;
            }
            rem -= node.length;
        }
        _setCursorAtEnd(el);
    }

    function _isEmpty(el) {
        return !el.textContent.trim() && !el.querySelector('img');
    }

    function _escHtml(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function _detectMarkdownShortcut(text) {
        if (/^# $/.test(text))          return 'heading1';
        if (/^## $/.test(text))         return 'heading2';
        if (/^### $/.test(text))        return 'heading3';
        if (/^[*\-] $/.test(text))      return 'bullet';
        if (/^1\. $/.test(text))        return 'numbered';
        if (/^\[\] $/.test(text))       return 'todo';
        if (/^\[x\] $/.test(text))      return 'todoDone';
        if (/^> $/.test(text))          return 'quote';
        if (/^```$/.test(text.trim()))  return 'code';
        if (/^---$/.test(text.trim()))  return 'divider';
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 26.1 — Block lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    function initBlock(element, dotNetRef) {
        if (!element) return;
        if (_blocks.has(element)) destroyBlock(element);
        _blocks.set(element, { dotNetRef, listeners: [] });
    }

    function destroyBlock(element) {
        if (!element || !_blocks.has(element)) return;
        const s = _blocks.get(element);
        _offAll(s.listeners);
        _blocks.delete(element);
    }

    function getHtml(element) {
        return element ? element.innerHTML : '';
    }

    function setHtml(element, html) {
        if (element) element.innerHTML = html ?? '';
    }

    function focus(element) {
        element?.focus();
    }

    function focusAtEnd(element) {
        if (element) _setCursorAtEnd(element);
    }

    function focusAtStart(element) {
        if (element) _setCursorAtStart(element);
    }

    function focusAtOffset(element, offset) {
        if (element) _setCursorAtOffset(element, offset);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 26.2 — Selection & formatting
    // ═══════════════════════════════════════════════════════════════════════════

    function getSelectionRange() {
        const sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return null;
        const r = sel.getRangeAt(0);
        const anchor = r.startContainer.nodeType === Node.TEXT_NODE
            ? r.startContainer.parentElement
            : r.startContainer;
        const blockElement = anchor?.closest?.('[data-notion-block]') ?? null;
        return {
            blockElement,
            startOffset: r.startOffset,
            endOffset: r.endOffset,
            text: sel.toString()
        };
    }

    function getSelectionRect() {
        const r = _range();
        if (!r) return null;
        const rect = r.getBoundingClientRect();
        return { top: rect.top, left: rect.left, width: rect.width, height: rect.height };
    }

    function applyFormat(command, value) {
        document.execCommand(command, false, value ?? null);
    }

    function queryFormatState(command) {
        return document.queryCommandState(command);
    }

    function insertHtml(html) {
        document.execCommand('insertHTML', false, html);
    }

    function insertLink(url, text) {
        const label = _escHtml(text || url);
        const href  = _escHtml(url);
        document.execCommand('insertHTML', false,
            `<a href="${href}" target="_blank" rel="noopener noreferrer">${label}</a>`);
    }

    function getBlockBoundingRect(blockId) {
        const el = document.querySelector(`[data-block-id="${blockId}"]`);
        if (!el) return null;
        const rect = el.getBoundingClientRect();
        return { top: rect.top, left: rect.left, width: rect.width, height: rect.height };
    }

    function wrapSelectionWithComment(commentId, blockId, dotNetRef, callbackName) {
        const sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || sel.isCollapsed) return;
        const r = sel.getRangeAt(0);
        const mark = document.createElement('mark');
        mark.className = 'tm-notion-comment-highlight';
        mark.dataset.commentId = String(commentId);
        try {
            r.surroundContents(mark);
        } catch {
            const frag = r.extractContents();
            mark.appendChild(frag);
            r.insertNode(mark);
        }
        sel.removeAllRanges();

        if (dotNetRef && callbackName) {
            const blockEl = r.commonAncestorContainer.nodeType === Node.TEXT_NODE
                ? r.commonAncestorContainer.parentElement?.closest('[data-notion-block]')
                : r.commonAncestorContainer.closest?.('[data-notion-block]');
            const actualBlockId = blockId || blockEl?.dataset?.notionBlock || '';
            const text = mark.textContent || '';
            const start = r.startOffset;
            const end = r.endOffset;
            const rect = mark.getBoundingClientRect();
            const top = rect.top + window.scrollY;
            const left = rect.left + window.scrollX;
            dotNetRef.invokeMethodAsync(callbackName, actualBlockId, commentId, text, start, end, top, left)
                .catch(() => {});
        }
    }

    function unwrapCommentHighlight(commentId) {
        const marks = document.querySelectorAll(`mark.tm-notion-comment-highlight[data-comment-id="${commentId}"]`);
        marks.forEach(mark => {
            const parent = mark.parentNode;
            if (!parent) return;
            while (mark.firstChild) {
                parent.insertBefore(mark.firstChild, mark);
            }
            parent.removeChild(mark);
            // Normalize adjacent text nodes
            parent.normalize();
        });
    }

    function setCommentHighlightActive(commentId, isActive) {
        const marks = document.querySelectorAll(`mark.tm-notion-comment-highlight[data-comment-id="${commentId}"]`);
        marks.forEach(mark => {
            mark.classList.toggle('tm-notion-comment-highlight--active', isActive);
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 26.3 — Drag & drop
    // ═══════════════════════════════════════════════════════════════════════════

    function initDragDrop(containerElement, dotNetRef) {
        if (!containerElement) return;
        if (_dragContainers.has(containerElement)) destroyDragDrop(containerElement);

        let dragSrc    = null;
        let dragGhost  = null;
        let indicator  = null;
        let dropTarget = null;

        function _indicator() {
            if (indicator) return indicator;
            indicator = document.createElement('div');
            indicator.className = 'tm-notion-drop-indicator';
            indicator.style.cssText =
                'position:fixed;height:2px;background:var(--tm-primary,#2383e2);border-radius:1px;' +
                'pointer-events:none;z-index:9999;display:none;transition:top .06s;';
            document.body.appendChild(indicator);
            return indicator;
        }

        function _blocks(container) {
            return Array.from(container.querySelectorAll('[data-notion-block]'));
        }

        function _blockAt(y) {
            return _blocks(containerElement).find(b => {
                const r = b.getBoundingClientRect();
                return y >= r.top && y <= r.bottom;
            }) ?? null;
        }

        function _indexOf(b) {
            return _blocks(containerElement).indexOf(b);
        }

        function _onDragStart(e) {
            if (!e.target.closest('[data-notion-drag-handle]')) return;
            const b = e.target.closest('[data-notion-block]');
            if (!b) return;
            dragSrc = b;
            dragSrc.classList.add('tm-notion-dragging');
            dragGhost = b.cloneNode(true);
            Object.assign(dragGhost.style, {
                position: 'fixed', top: '-9999px', left: '-9999px',
                width: b.offsetWidth + 'px', opacity: '0.88',
                pointerEvents: 'none', boxShadow: '0 4px 20px rgba(0,0,0,.22)',
                borderRadius: '4px', background: 'var(--tm-bg, #fff)', zIndex: '10000'
            });
            document.body.appendChild(dragGhost);
            e.dataTransfer.setDragImage(dragGhost, 24, 24);
            e.dataTransfer.effectAllowed = 'move';
        }

        function _onDragOver(e) {
            if (!dragSrc) return; // not a block drag — let diagram/wireframe stencil drops work
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            const b = _blockAt(e.clientY);
            if (!b || b === dragSrc) { _indicator().style.display = 'none'; return; }
            dropTarget = b;
            const rect  = b.getBoundingClientRect();
            const after = e.clientY > rect.top + rect.height / 2;
            const ind   = _indicator();
            const y     = after ? rect.bottom : rect.top;
            Object.assign(ind.style, {
                display: 'block',
                top:   y - 1 + 'px',
                left:  rect.left + 'px',
                width: rect.width + 'px'
            });
        }

        function _onDragLeave(e) {
            if (!containerElement.contains(e.relatedTarget)) {
                _indicator().style.display = 'none';
            }
        }

        function _onDrop(e) {
            if (!dragSrc) return; // not a block drag — let diagram/wireframe stencil drops work
            e.preventDefault();
            _indicator().style.display = 'none';
            if (!dropTarget || dropTarget === dragSrc) return;
            const src = _indexOf(dragSrc);
            const rect = dropTarget.getBoundingClientRect();
            let dst  = _indexOf(dropTarget);
            if (e.clientY > rect.top + rect.height / 2) dst++;
            if (src !== -1 && dst !== -1 && src !== dst) {
                dotNetRef.invokeMethodAsync('OnBlockReordered', src, dst).catch(console.error);
            }
        }

        function _onDragEnd() {
            dragSrc?.classList.remove('tm-notion-dragging');
            dragGhost?.parentNode?.removeChild(dragGhost);
            if (indicator) indicator.style.display = 'none';
            dragSrc = dragGhost = dropTarget = null;
        }

        containerElement.addEventListener('dragstart',  _onDragStart);
        containerElement.addEventListener('dragover',   _onDragOver);
        containerElement.addEventListener('dragleave',  _onDragLeave);
        containerElement.addEventListener('drop',       _onDrop);
        containerElement.addEventListener('dragend',    _onDragEnd);

        _dragContainers.set(containerElement, {
            cleanup() {
                containerElement.removeEventListener('dragstart',  _onDragStart);
                containerElement.removeEventListener('dragover',   _onDragOver);
                containerElement.removeEventListener('dragleave',  _onDragLeave);
                containerElement.removeEventListener('drop',       _onDrop);
                containerElement.removeEventListener('dragend',    _onDragEnd);
                indicator?.parentNode?.removeChild(indicator);
                indicator = null;
            }
        });
    }

    function destroyDragDrop(containerElement) {
        if (!containerElement || !_dragContainers.has(containerElement)) return;
        _dragContainers.get(containerElement).cleanup();
        _dragContainers.delete(containerElement);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 26.4 — Slash menu positioning
    // ═══════════════════════════════════════════════════════════════════════════

    function getCaretCoords() {
        const r = _range();
        if (!r) return { top: 0, left: 0 };
        const c = r.cloneRange();
        c.collapse(true);
        let rect = c.getBoundingClientRect();
        if (rect.width === 0 && rect.height === 0) {
            const span = document.createElement('span');
            span.textContent = '\u200b';
            c.insertNode(span);
            rect = span.getBoundingClientRect();
            span.parentNode?.removeChild(span);
        }
        return {
            top:  rect.bottom + window.scrollY,
            left: rect.left   + window.scrollX
        };
    }

    function getTextBeforeCaret(element) {
        if (!element) return '';
        const r = _range();
        if (!r) return '';
        const pre = document.createRange();
        pre.selectNodeContents(element);
        pre.setEnd(r.startContainer, r.startOffset);
        return pre.toString();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 26.5 — Keyboard handler
    // ═══════════════════════════════════════════════════════════════════════════

    function initKeyboardHandler(element, dotNetRef) {
        if (!element) { console.warn('initKeyboardHandler: element is null'); return; }
        if (!_blocks.has(element)) _blocks.set(element, { dotNetRef, listeners: [] });
        const state = _blocks.get(element);
        state.dotNetRef = dotNetRef;
        _offAll(state.listeners);
        console.log('initKeyboardHandler attached to', element);

        function _htmlAroundCaret() {
            const r = _range();
            if (!r) return { before: element.innerHTML, after: '' };
            function _fragHtml(fr) {
                const d = document.createElement('div');
                d.appendChild(fr);
                return d.innerHTML;
            }
            const beforeR = document.createRange();
            beforeR.selectNodeContents(element);
            beforeR.setEnd(r.startContainer, r.startOffset);
            const afterR = document.createRange();
            afterR.selectNodeContents(element);
            afterR.setStart(r.endContainer, r.endOffset);
            return {
                before: _fragHtml(beforeR.cloneContents()),
                after:  _fragHtml(afterR.cloneContents())
            };
        }

        const onKeyDown = (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                const { before, after } = _htmlAroundCaret();
                dotNetRef.invokeMethodAsync('OnEnterPressed', before, after).catch(console.error);
                return;
            }
            if (e.key === 'Backspace' && _isEmpty(element)) {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('OnBackspaceOnEmpty').catch(console.error);
                return;
            }
            if (e.key === 'Tab') {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('OnTabPressed', e.shiftKey).catch(console.error);
                return;
            }
            if (e.key === 'ArrowUp'   && !e.shiftKey && !e.ctrlKey && !e.metaKey) {
                dotNetRef.invokeMethodAsync('OnArrowUp').catch(console.error);
            }
            if (e.key === 'ArrowDown' && !e.shiftKey && !e.ctrlKey && !e.metaKey) {
                dotNetRef.invokeMethodAsync('OnArrowDown').catch(console.error);
            }
        };

        const onInput = () => {
            const text = element.textContent || '';
            console.log('onInput text:', text);

            const shortcut = _detectMarkdownShortcut(text);
            if (shortcut) {
                element.innerHTML = '';
                dotNetRef.invokeMethodAsync('OnMarkdownShortcut', shortcut).catch(console.error);
                return;
            }

            const lastChar = text[text.length - 1];
            const prevChar = text[text.length - 2];
            const atWordBoundary = !prevChar || prevChar === ' ';

            if (lastChar === '/' && atWordBoundary) {
                // Store the element and anchor position (just before '/')
                _slashElement = element;
                const r = _range();
                if (r) {
                    _slashAnchorNode = r.startContainer;
                    _slashAnchorOff  = Math.max(0, r.startOffset - 1);
                }
                const c = getCaretCoords();
                dotNetRef.invokeMethodAsync('OnSlashTriggered', c.top, c.left).catch(console.error);
            } else if (lastChar === '@' && atWordBoundary) {
                _mentionElement    = element;
                _mentionTriggerLen = 1;
                const r1 = _range();
                if (r1) {
                    _mentionAnchorNode = r1.startContainer;
                    _mentionAnchorOff  = Math.max(0, r1.startOffset - 1);
                }
                const c = getCaretCoords();
                dotNetRef.invokeMethodAsync('OnMentionTriggered', c.top, c.left).catch(console.error);
            } else if (text.endsWith('[[')) {
                _mentionElement    = element;
                _mentionTriggerLen = 2;
                const r2 = _range();
                if (r2) {
                    _mentionAnchorNode = r2.startContainer;
                    _mentionAnchorOff  = Math.max(0, r2.startOffset - 2);
                }
                const c = getCaretCoords();
                dotNetRef.invokeMethodAsync('OnPageLinkTriggered', c.top, c.left).catch(console.error);
            }
        };

        state.listeners.push(
            _on(element, 'keydown', onKeyDown),
            _on(element, 'input',   onInput)
        );
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 26.6 — Inline math (KaTeX with plain-text fallback)
    // ═══════════════════════════════════════════════════════════════════════════

    function renderEquation(element, latex) {
        if (!element) return;
        if (!latex || !latex.trim()) { element.innerHTML = ''; return; }
        if (window.katex) {
            try {
                element.innerHTML = window.katex.renderToString(latex, {
                    displayMode: true,
                    throwOnError: false,
                    output: 'html'
                });
                return;
            } catch { /* fall through */ }
        }
        element.textContent = latex;
        element.dataset.latex = latex;
    }

    function renderInlineMath(element, latex) {
        if (!element) return;
        if (window.katex) {
            try {
                element.innerHTML = window.katex.renderToString(latex, {
                    displayMode: false,
                    throwOnError: false,
                    output: 'html'
                });
                return;
            } catch { /* fall through */ }
        }
        element.textContent = latex;
        element.dataset.latex = latex;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 26.7 — Clipboard
    // ═══════════════════════════════════════════════════════════════════════════

    function handlePaste(element, dotNetRef) {
        if (!element) return;
        if (!_blocks.has(element)) _blocks.set(element, { dotNetRef, listeners: [] });
        const state = _blocks.get(element);

        const onPaste = (e) => {
            e.preventDefault();
            const cd = e.clipboardData || window.clipboardData;

            // Image first
            const imgItem = Array.from(cd.items || []).find(i => i.type.startsWith('image/'));
            if (imgItem) {
                const file = imgItem.getAsFile();
                if (file) {
                    const fr = new FileReader();
                    fr.onload = () => dotNetRef.invokeMethodAsync(
                        'OnImagePasted', fr.result, file.type, file.name || 'pasted-image'
                    ).catch(console.error);
                    fr.readAsDataURL(file);
                    return;
                }
            }

            // HTML
            const html = cd.getData('text/html');
            if (html?.trim()) {
                dotNetRef.invokeMethodAsync('OnHtmlPasted', html).catch(console.error);
                return;
            }

            // Plain text
            const text = cd.getData('text/plain');
            if (text) dotNetRef.invokeMethodAsync('OnTextPasted', text).catch(console.error);
        };

        state.listeners.push(_on(element, 'paste', onPaste));
    }

    function copyBlocksToClipboard(blocksJson) {
        const text = blocksJson ?? '';
        if (navigator.clipboard?.writeText) {
            navigator.clipboard.writeText(text).catch(console.error);
        } else {
            const ta = Object.assign(document.createElement('textarea'), {
                value: text
            });
            Object.assign(ta.style, { position: 'fixed', opacity: '0' });
            document.body.appendChild(ta);
            ta.select();
            document.execCommand('copy');
            document.body.removeChild(ta);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 26.8 — Resize handle
    // ═══════════════════════════════════════════════════════════════════════════

    function initResizeHandle(element, dotNetRef) {
        if (!element) return;
        if (_resizeHandles.has(element)) _resizeHandles.get(element).cleanup();

        const handle = document.createElement('div');
        handle.className = 'tm-notion-resize-handle';
        Object.assign(handle.style, {
            position: 'absolute', right: '0', top: '0', bottom: '0',
            width: '6px', cursor: 'ew-resize', zIndex: '10',
            background: 'transparent', borderRadius: '0 3px 3px 0'
        });

        let isDown = false, startX = 0, startW = 0;
        let _iframeStyles = [];

        const disableIframes = () => {
            _iframeStyles = [];
            element.querySelectorAll('iframe').forEach(iframe => {
                _iframeStyles.push({ el: iframe, original: iframe.style.pointerEvents });
                iframe.style.pointerEvents = 'none';
            });
        };
        const restoreIframes = () => {
            _iframeStyles.forEach(item => { item.el.style.pointerEvents = item.original; });
            _iframeStyles = [];
        };

        const onDown = (e) => {
            e.preventDefault();
            isDown  = true;
            startX  = e.clientX;
            startW  = element.offsetWidth;
            document.body.style.cursor     = 'ew-resize';
            document.body.style.userSelect = 'none';
            disableIframes();
        };
        const onMove = (e) => {
            if (!isDown) return;
            element.style.width = Math.max(80, startW + e.clientX - startX) + 'px';
        };
        const onUp = () => {
            if (!isDown) return;
            isDown = false;
            document.body.style.cursor = document.body.style.userSelect = '';
            restoreIframes();
            dotNetRef.invokeMethodAsync('OnResize', element.offsetWidth, element.offsetHeight)
                     .catch(console.error);
        };

        handle.addEventListener('mousedown', onDown);
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup',   onUp);

        if (getComputedStyle(element).position === 'static') element.style.position = 'relative';
        element.appendChild(handle);

        _resizeHandles.set(element, {
            cleanup() {
                handle.removeEventListener('mousedown', onDown);
                document.removeEventListener('mousemove', onMove);
                document.removeEventListener('mouseup',   onUp);
                restoreIframes();
                handle.parentNode?.removeChild(handle);
            }
        });
    }

    function destroyResizeHandle(element) {
        if (!element || !_resizeHandles.has(element)) return;
        _resizeHandles.get(element).cleanup();
        _resizeHandles.delete(element);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 54.0 — Column width helper
    // ═══════════════════════════════════════════════════════════════════════════

    function setColumnWidth(element, widthPercent) {
        if (!element) return;
        element.style.flexBasis = widthPercent.toFixed(2) + '%';
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 54.1 — Column resize
    // ═══════════════════════════════════════════════════════════════════════════

    function initColumnResize(containerElement, dotNetRef) {
        if (!containerElement) return;
        destroyColumnResize(containerElement);

        const cleanups = [];

        const attachDivider = (divider) => {
            const idx = parseInt(divider.dataset.colDivider, 10);

            const onDown = (e) => {
                e.preventDefault();

                const cols     = Array.from(containerElement.querySelectorAll('[data-col-index]'))
                    .sort((a, b) => parseInt(a.dataset.colIndex) - parseInt(b.dataset.colIndex));
                const leftCol  = cols[idx];
                const rightCol = cols[idx + 1];
                if (!leftCol || !rightCol) return;

                const startX     = e.clientX;
                const totalW     = containerElement.offsetWidth;
                const leftStart  = leftCol.offsetWidth;
                const rightStart = rightCol.offsetWidth;
                const minW       = Math.max(parseFloat(getComputedStyle(leftCol).minWidth) || 120, totalW * 0.1);

                // Disable iframes during drag so they don't steal mouse events
                let _iframeStyles = [];
                const disableIframes = () => {
                    _iframeStyles = [];
                    containerElement.querySelectorAll('iframe').forEach(iframe => {
                        _iframeStyles.push({ el: iframe, original: iframe.style.pointerEvents });
                        iframe.style.pointerEvents = 'none';
                    });
                };
                const restoreIframes = () => {
                    _iframeStyles.forEach(item => { item.el.style.pointerEvents = item.original; });
                    _iframeStyles = [];
                };

                document.body.style.cursor     = 'col-resize';
                document.body.style.userSelect = 'none';
                divider.classList.add('tm-notion-column-list__divider--active');
                disableIframes();

                const onMove = (e2) => {
                    const delta    = e2.clientX - startX;
                    const newLeft  = Math.max(minW, Math.min(leftStart + delta, leftStart + rightStart - minW));
                    const newRight = (leftStart + rightStart) - newLeft;
                    leftCol.style.flexBasis  = (newLeft  / totalW * 100).toFixed(2) + '%';
                    rightCol.style.flexBasis = (newRight / totalW * 100).toFixed(2) + '%';
                };

                const onUp = () => {
                    document.body.style.cursor     = '';
                    document.body.style.userSelect = '';
                    divider.classList.remove('tm-notion-column-list__divider--active');
                    document.removeEventListener('mousemove', onMove);
                    document.removeEventListener('mouseup',   onUp);
                    restoreIframes();

                    const allCols   = Array.from(containerElement.querySelectorAll('[data-col-index]'))
                        .sort((a, b) => parseInt(a.dataset.colIndex) - parseInt(b.dataset.colIndex));
                    const totalWidth = containerElement.offsetWidth;
                    const widths     = allCols.map(c => parseFloat((c.offsetWidth / totalWidth * 100).toFixed(2)));

                    dotNetRef.invokeMethodAsync('OnColumnResized', widths).catch(console.error);
                };

                document.addEventListener('mousemove', onMove);
                document.addEventListener('mouseup',   onUp);
            };

            divider.addEventListener('mousedown', onDown);
            cleanups.push(() => divider.removeEventListener('mousedown', onDown));
        };

        containerElement.querySelectorAll('[data-col-divider]').forEach(attachDivider);

        _columnResizers.set(containerElement, { cleanup() { cleanups.forEach(fn => fn()); } });
    }

    function destroyColumnResize(containerElement) {
        if (!containerElement || !_columnResizers.has(containerElement)) return;
        _columnResizers.get(containerElement).cleanup();
        _columnResizers.delete(containerElement);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 26.9 — Scroll & navigation
    // ═══════════════════════════════════════════════════════════════════════════

    function scrollToBlock(blockId) {
        const el = document.querySelector(`[data-block-id="${CSS.escape(blockId)}"]`);
        el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    // ── 91.1  Collaboration cursor overlays ──────────────────────────────────

    function updateCollabCursors(cursors) {
        // Clear previous markers
        document.querySelectorAll('[data-collab-user]').forEach(el => {
            el.classList.remove('tm-collab-active');
            el.removeAttribute('data-collab-user');
            el.style.removeProperty('--collab-color');
        });
        if (!cursors || !cursors.length) return;
        cursors.forEach(({ blockId, displayName, color }) => {
            const blockEl = document.querySelector(`[data-block-id="${CSS.escape(blockId)}"]`);
            if (!blockEl) return;
            blockEl.classList.add('tm-collab-active');
            blockEl.setAttribute('data-collab-user', displayName);
            blockEl.style.setProperty('--collab-color', color);
        });
    }

    function clearCollabCursors() {
        document.querySelectorAll('[data-collab-user]').forEach(el => {
            el.classList.remove('tm-collab-active');
            el.removeAttribute('data-collab-user');
            el.style.removeProperty('--collab-color');
        });
    }

    function initSmoothScrollSpy(containerElement, dotNetRef) {
        if (!containerElement) return;
        if (_scrollSpies.has(containerElement)) _scrollSpies.get(containerElement).cleanup();

        let ticking    = false;
        let activeId   = null;
        const OFFSET   = 80;

        const onScroll = () => {
            if (ticking) return;
            ticking = true;
            requestAnimationFrame(() => {
                ticking = false;
                const headings = Array.from(
                    containerElement.querySelectorAll('[data-notion-heading][data-block-id]')
                );
                let current = null;
                for (const h of headings) {
                    if (h.getBoundingClientRect().top <= OFFSET) current = h;
                }
                const newId = current?.dataset.blockId ?? null;
                if (newId !== activeId) {
                    activeId = newId;
                    dotNetRef.invokeMethodAsync('OnScrollSpyBlockChanged', newId).catch(console.error);
                }
            });
        };

        const scrollRoot = containerElement.closest('[data-notion-scroll-root]') ?? containerElement;
        scrollRoot.addEventListener('scroll', onScroll, { passive: true });
        window.addEventListener('scroll',    onScroll, { passive: true });

        _scrollSpies.set(containerElement, {
            cleanup() {
                scrollRoot.removeEventListener('scroll', onScroll);
                window.removeEventListener('scroll',    onScroll);
            }
        });
    }

    function destroyScrollSpy(containerElement) {
        if (!containerElement || !_scrollSpies.has(containerElement)) return;
        _scrollSpies.get(containerElement).cleanup();
        _scrollSpies.delete(containerElement);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 30.1 — Cover image drag repositioning
    // ═══════════════════════════════════════════════════════════════════════════

    function startCoverDrag(coverElement, dotNetRef, startClientY, startPositionY) {
        if (!coverElement) return;

        let currentPos = startPositionY ?? 50;

        const onMouseMove = (e) => {
            const rect  = coverElement.getBoundingClientRect();
            const delta = e.clientY - startClientY;
            currentPos  = Math.max(0, Math.min(100, startPositionY - (delta / rect.height * 100)));
            coverElement.style.backgroundPositionY = currentPos.toFixed(1) + '%';
        };

        const onMouseUp = () => {
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup',   onMouseUp);
            document.body.style.cursor = '';
            dotNetRef.invokeMethodAsync('OnCoverDragEnded', currentPos).catch(console.error);
        };

        document.body.style.cursor = 'ns-resize';
        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup',   onMouseUp);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 43.1 — Slash menu helpers
    // ═══════════════════════════════════════════════════════════════════════════

    const SLASH_RECENT_KEY  = 'tm-notion-slash-recent';
    const SLASH_RECENT_MAX  = 5;

    function getRecentSlashItems() {
        try {
            const raw = localStorage.getItem(SLASH_RECENT_KEY);
            return raw ? JSON.parse(raw) : [];
        } catch {
            return [];
        }
    }

    function addRecentSlashItem(blockTypeInt) {
        try {
            const existing = getRecentSlashItems().filter(v => v !== blockTypeInt);
            existing.unshift(blockTypeInt);
            localStorage.setItem(SLASH_RECENT_KEY, JSON.stringify(existing.slice(0, SLASH_RECENT_MAX)));
        } catch { /* private browsing / storage full */ }
    }

    function clearSlashQuery() {
        if (!_slashElement) return;
        try {
            // Build a range from the anchor (before '/') to end of the editable
            const sel   = window.getSelection();
            const range = document.createRange();
            if (_slashAnchorNode && _slashElement.contains(_slashAnchorNode)) {
                range.setStart(_slashAnchorNode, _slashAnchorOff);
            } else {
                range.selectNodeContents(_slashElement);
                range.collapse(false);
                range.setStart(_slashElement, 0);
            }
            // Extend to end of editable content
            const endRange = document.createRange();
            endRange.selectNodeContents(_slashElement);
            range.setEnd(endRange.endContainer, endRange.endOffset);

            sel.removeAllRanges();
            sel.addRange(range);
            document.execCommand('delete');
        } catch { /* ignore edge cases */ }
        _slashElement    = null;
        _slashAnchorNode = null;
        _slashAnchorOff  = 0;
    }

    function refocusSlashElement() {
        if (_slashElement) {
            _setCursorAtEnd(_slashElement);
        }
        _slashElement    = null;
        _slashAnchorNode = null;
        _slashAnchorOff  = 0;
    }

    function insertMentionChip(mentionType, mentionId, displayText) {
        if (!_mentionElement) return;
        try {
            _mentionElement.focus();

            const sel   = window.getSelection();
            const range = document.createRange();
            if (_mentionAnchorNode && _mentionElement.contains(_mentionAnchorNode)) {
                range.setStart(_mentionAnchorNode, _mentionAnchorOff);
            } else {
                range.selectNodeContents(_mentionElement);
                range.setStart(_mentionElement, 0);
            }
            const endRange = document.createRange();
            endRange.selectNodeContents(_mentionElement);
            range.setEnd(endRange.endContainer, endRange.endOffset);

            sel.removeAllRanges();
            sel.addRange(range);
            document.execCommand('delete');

            const chip = document.createElement('span');
            chip.contentEditable = 'false';
            chip.className = 'tm-notion-mention tm-notion-mention--' + mentionType;
            chip.dataset.type = mentionType;
            chip.dataset.id   = String(mentionId);
            chip.textContent  = displayText;

            const curSel   = window.getSelection();
            const curRange = curSel.getRangeAt(0);
            curRange.insertNode(chip);
            curRange.setStartAfter(chip);
            curRange.collapse(true);
            curSel.removeAllRanges();
            curSel.addRange(curRange);

            document.execCommand('insertText', false, ' ');

            // Notify Blazor that content changed so it saves on next blur
            _mentionElement.dispatchEvent(new Event('input', { bubbles: true }));
        } catch { /* ignore edge cases */ }

        _mentionElement    = null;
        _mentionAnchorNode = null;
        _mentionAnchorOff  = 0;
        _mentionTriggerLen = 1;
    }

    function cancelMentionTrigger() {
        if (_mentionElement) {
            _mentionElement.focus();
            _setCursorAtEnd(_mentionElement);
        }
        _mentionElement    = null;
        _mentionAnchorNode = null;
        _mentionAnchorOff  = 0;
        _mentionTriggerLen = 1;
    }

    function adjustSlashMenuPosition(menuEl) {
        if (!menuEl) return;
        const rect   = menuEl.getBoundingClientRect();
        const vw     = window.innerWidth;
        const vh     = window.innerHeight;
        const margin = 8;

        let top  = parseFloat(menuEl.style.top)  || 0;
        let left = parseFloat(menuEl.style.left) || 0;

        // Flip above caret if menu overflows bottom
        if (rect.bottom > vh - margin) {
            top = top - rect.height - 28; // 28 = approx line height
            menuEl.style.top = Math.max(margin, top) + 'px';
        }
        // Clamp right edge
        if (rect.right > vw - margin) {
            left = vw - rect.width - margin;
            menuEl.style.left = Math.max(margin, left) + 'px';
        }
    }

    // 47.1
    function adjustTypeSwitcherPosition(panelEl) {
        if (!panelEl) return;
        const rect   = panelEl.getBoundingClientRect();
        const vw     = window.innerWidth;
        const vh     = window.innerHeight;
        const margin = 8;

        let top  = parseFloat(panelEl.style.top)  || 0;
        let left = parseFloat(panelEl.style.left) || 0;

        if (rect.bottom > vh - margin) {
            top = top - rect.height - 4;
            panelEl.style.top = Math.max(margin, top) + 'px';
        }
        if (rect.right > vw - margin) {
            left = vw - rect.width - margin;
            panelEl.style.left = Math.max(margin, left) + 'px';
        }
    }

    // 46.1
    function getRecentEmojis() {
        try {
            const raw = localStorage.getItem('tm-notion-emoji-recent');
            return raw ? JSON.parse(raw) : [];
        } catch { return []; }
    }

    function addRecentEmoji(emoji) {
        try {
            const recent = getRecentEmojis().filter(e => e !== emoji);
            recent.unshift(emoji);
            localStorage.setItem('tm-notion-emoji-recent', JSON.stringify(recent.slice(0, 16)));
        } catch { }
    }

    function adjustEmojiPickerPosition(pickerEl) {
        if (!pickerEl) return;
        const rect   = pickerEl.getBoundingClientRect();
        const vw     = window.innerWidth;
        const vh     = window.innerHeight;
        const margin = 8;

        let top  = parseFloat(pickerEl.style.top)  || 0;
        let left = parseFloat(pickerEl.style.left) || 0;

        if (rect.bottom > vh - margin) {
            top = top - rect.height - 4;
            pickerEl.style.top = Math.max(margin, top) + 'px';
        }
        if (rect.right > vw - margin) {
            left = vw - rect.width - margin;
            pickerEl.style.left = Math.max(margin, left) + 'px';
        }
    }

    // 45.1
    function adjustColorPickerPosition(pickerEl) {
        if (!pickerEl) return;
        const rect   = pickerEl.getBoundingClientRect();
        const vw     = window.innerWidth;
        const vh     = window.innerHeight;
        const margin = 8;

        let top  = parseFloat(pickerEl.style.top)  || 0;
        let left = parseFloat(pickerEl.style.left) || 0;

        if (rect.bottom > vh - margin) {
            top = top - rect.height - 4;
            pickerEl.style.top = Math.max(margin, top) + 'px';
        }
        if (rect.right > vw - margin) {
            left = vw - rect.width - margin;
            pickerEl.style.left = Math.max(margin, left) + 'px';
        }
    }

    function scrollSlashItemIntoView(listEl, flatIndex) {
        if (!listEl) return;
        const el = listEl.querySelector(`[data-slash-idx="${flatIndex}"]`);
        el?.scrollIntoView({ block: 'nearest' });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 44.1 — Inline formatting toolbar
    // ═══════════════════════════════════════════════════════════════════════════

    const _selectionWatchers = new WeakMap(); // pageEl → { dotNetRef, listeners: [] }

    // Saved selection range for link insertion (focus moves to URL input, losing selection)
    let _savedRange = null;

    function initSelectionWatcher(pageEl, dotNetRef) {
        if (!pageEl) return;
        if (_selectionWatchers.has(pageEl)) destroySelectionWatcher(pageEl);

        const listeners = [];

        function _notify() {
            const sel = window.getSelection();
            if (!sel || sel.isCollapsed || sel.rangeCount === 0) {
                dotNetRef.invokeMethodAsync('OnToolbarSelectionCleared').catch(() => {});
                return;
            }
            const range = sel.getRangeAt(0);
            if (!pageEl.contains(range.commonAncestorContainer)) {
                dotNetRef.invokeMethodAsync('OnToolbarSelectionCleared').catch(() => {});
                return;
            }
            const rect = range.getBoundingClientRect();
            if (rect.width === 0) {
                dotNetRef.invokeMethodAsync('OnToolbarSelectionCleared').catch(() => {});
                return;
            }

            const blockEl = range.commonAncestorContainer.nodeType === Node.TEXT_NODE
                ? range.commonAncestorContainer.parentElement?.closest('[data-notion-block]')
                : range.commonAncestorContainer.closest?.('[data-notion-block]');

            const blockId = blockEl?.dataset?.notionBlock ?? '';
            const isBold          = document.queryCommandState('bold');
            const isItalic        = document.queryCommandState('italic');
            const isUnderline     = document.queryCommandState('underline');
            const isStrikeThrough = document.queryCommandState('strikeThrough');
            const linkEl          = sel.anchorNode?.parentElement?.closest('a');
            const currentHref     = linkEl?.href ?? '';

            // Detect inline code by checking if selection is within a <code> element
            const codeEl   = sel.anchorNode?.parentElement?.closest('code');
            const isCode   = !!codeEl && !codeEl.closest('pre');

            // Toolbar appears just above the selection
            const top  = rect.top + window.scrollY - 40;
            const left = rect.left + window.scrollX + rect.width / 2 - 160;

            dotNetRef.invokeMethodAsync('OnToolbarSelectionChanged',
                top, left, isBold, isItalic, isUnderline, isStrikeThrough, isCode,
                currentHref, blockId
            ).catch(() => {});
        }

        let _timer = 0;
        const onUp = () => { clearTimeout(_timer); _timer = setTimeout(_notify, 10); };

        listeners.push(
            _on(document, 'mouseup',  onUp),
            _on(document, 'keyup',    onUp),
            _on(document, 'selectionchange', onUp)
        );

        _selectionWatchers.set(pageEl, { dotNetRef, listeners });
    }

    function destroySelectionWatcher(pageEl) {
        if (!pageEl) return;
        const state = _selectionWatchers.get(pageEl);
        if (!state) return;
        _offAll(state.listeners);
        _selectionWatchers.delete(pageEl);
    }

    function saveSelection() {
        _savedRange = _range() ? _range().cloneRange() : null;
    }

    function insertLinkOnSavedSelection(url) {
        if (!_savedRange) return;
        _applyRange(_savedRange);
        _savedRange = null;
        const label = _escHtml(window.getSelection()?.toString() || url);
        const href  = _escHtml(url);
        document.execCommand('insertHTML', false,
            `<a href="${href}" target="_blank" rel="noopener noreferrer">${label}</a>`);
    }

    function applyInlineColor(scope, colorValue) {
        if (scope === 'text') {
            if (!colorValue) {
                document.execCommand('removeFormat', false, null);
            } else {
                document.execCommand('foreColor', false, colorValue);
            }
        } else {
            if (!colorValue) {
                document.execCommand('hiliteColor', false, 'transparent');
            } else {
                document.execCommand('hiliteColor', false, colorValue);
            }
        }
    }

    function toggleInlineCode() {
        const sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return;
        const r = sel.getRangeAt(0);
        const codeEl = sel.anchorNode?.parentElement?.closest('code');
        if (codeEl && !codeEl.closest('pre')) {
            // Unwrap: replace <code> with its text content
            const parent = codeEl.parentNode;
            while (codeEl.firstChild) parent.insertBefore(codeEl.firstChild, codeEl);
            parent.removeChild(codeEl);
        } else {
            // Wrap selection in <code>
            const code = document.createElement('code');
            try {
                r.surroundContents(code);
            } catch {
                const frag = r.extractContents();
                code.appendChild(frag);
                r.insertNode(code);
            }
        }
    }

    function insertInlineMath() {
        const sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return;
        const r     = sel.getRangeAt(0);
        const expr  = sel.toString().trim() || 'x';
        const span  = document.createElement('span');
        span.className = 'tm-notion-inline-math';
        span.dataset.expr = expr;
        span.textContent  = expr;
        try {
            r.deleteContents();
            r.insertNode(span);
        } catch { /* ignore */ }
    }

    function adjustInlineToolbarPosition(toolbarEl) {
        if (!toolbarEl) return;
        const rect   = toolbarEl.getBoundingClientRect();
        const vw     = window.innerWidth;
        const margin = 8;
        if (rect.right > vw - margin) {
            const shift = rect.right - (vw - margin);
            toolbarEl.style.left = (parseFloat(toolbarEl.style.left) - shift) + 'px';
        }
        if (rect.left < margin) {
            toolbarEl.style.left = margin + 'px';
        }
    }

    // ── 32.1 Copy block link ───────────────────────────────────────────────────

    function copyBlockLink(fragment) {
        const url = window.location.href.split('#')[0] + fragment;
        navigator.clipboard.writeText(url).catch(() => {});
    }

    // ── 62.1 Copy plain text to clipboard (synced block sync ID) ──────────────

    function copyText(text) {
        if (navigator.clipboard?.writeText) {
            navigator.clipboard.writeText(String(text)).catch(() => {});
        }
    }

    // ── 37.1 Code block keyboard handler ──────────────────────────────────────

    const CODE_TAB_SIZE = 4;

    function _autoResizeTextarea(ta) {
        ta.style.height = 'auto';
        ta.style.height = ta.scrollHeight + 'px';
    }

    function initCodeKeyboardHandler(textarea, dotNetRef) {
        if (!textarea) return;
        if (_blocks.has(textarea)) destroyBlock(textarea);
        _blocks.set(textarea, { dotNetRef, listeners: [] });
        const state = _blocks.get(textarea);

        const onKeyDown = (e) => {
            if (e.key === 'Tab') {
                e.preventDefault();
                const start = textarea.selectionStart;
                const end   = textarea.selectionEnd;
                const val   = textarea.value;

                if (e.shiftKey) {
                    const lineStart = val.lastIndexOf('\n', start - 1) + 1;
                    const spaces    = val.slice(lineStart).match(/^ {1,4}/)?.[0] ?? '';
                    if (spaces.length > 0) {
                        textarea.value = val.slice(0, lineStart) + val.slice(lineStart + spaces.length);
                        textarea.selectionStart = textarea.selectionEnd = Math.max(lineStart, start - spaces.length);
                    }
                } else {
                    const indent = ' '.repeat(CODE_TAB_SIZE);
                    textarea.value = val.slice(0, start) + indent + val.slice(end);
                    textarea.selectionStart = textarea.selectionEnd = start + CODE_TAB_SIZE;
                }
                textarea.dispatchEvent(new Event('input', { bubbles: true }));
                _autoResizeTextarea(textarea);
                return;
            }

            if (e.key === 'Backspace' && !textarea.value.trim()) {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('OnBackspaceOnEmpty').catch(console.error);
                return;
            }
        };

        const onInput = () => _autoResizeTextarea(textarea);

        state.listeners.push(
            _on(textarea, 'keydown', onKeyDown),
            _on(textarea, 'input',   onInput)
        );
    }

    function getCode(textarea) {
        return textarea ? textarea.value : '';
    }

    function setCode(textarea, code) {
        if (!textarea) return;
        textarea.value = code ?? '';
        _autoResizeTextarea(textarea);
    }

    // ── 58.1 Table block — cell keyboard handler & focus ──────────────────────

    function initTableRowKeyboardHandler(rowEl, dotNetRef, columnCount) {
        if (!rowEl) return;
        if (_blocks.has(rowEl)) destroyBlock(rowEl);
        _blocks.set(rowEl, { dotNetRef, listeners: [], columnCount });

        const onKeyDown = (e) => {
            if (e.key !== 'Tab') return;
            const cell = e.target.closest('[data-tm-col]');
            if (!cell) return;
            const colIdx = parseInt(cell.dataset.tmCol, 10);
            const count  = _blocks.get(rowEl)?.columnCount ?? columnCount;
            e.preventDefault();
            if (e.shiftKey) {
                if (colIdx === 0) {
                    dotNetRef.invokeMethodAsync('InvokeShiftTabFromFirstCell').catch(console.error);
                } else {
                    const prev = rowEl.querySelector(`[data-tm-col="${colIdx - 1}"] [contenteditable]`);
                    if (prev) { prev.focus(); _setCursorAtEnd(prev); }
                }
            } else {
                if (colIdx === count - 1) {
                    dotNetRef.invokeMethodAsync('InvokeTabFromLastCell').catch(console.error);
                } else {
                    const next = rowEl.querySelector(`[data-tm-col="${colIdx + 1}"] [contenteditable]`);
                    if (next) { next.focus(); _setCursorAtStart(next); }
                }
            }
        };

        _blocks.get(rowEl).listeners.push(_on(rowEl, 'keydown', onKeyDown));
    }

    function destroyTableRowKeyboardHandler(rowEl) {
        destroyBlock(rowEl);
    }

    function tableFocusCell(tableEl, rowIdx, colIdx) {
        if (!tableEl) return;
        const td = tableEl.querySelector(`[data-tm-row="${rowIdx}"][data-tm-col="${colIdx}"]`);
        const editable = td?.querySelector('[contenteditable]');
        if (editable) { editable.focus(); _setCursorAtEnd(editable); }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 80.1 — Sidebar resize
    // ═══════════════════════════════════════════════════════════════════════════

    const _sidebarResizes = new WeakMap();
    const TM_SIDEBAR_WIDTH_KEY = 'tm-notion-sidebar-width';

    function initSidebarResize(handleEl, dotNetRef, minWidth, maxWidth) {
        if (!handleEl) return;
        if (_sidebarResizes.has(handleEl)) _sidebarResizes.get(handleEl).cleanup();

        const aside = handleEl.closest('.tm-notion-sidebar');
        if (!aside) return;

        const saved = parseInt(localStorage.getItem(TM_SIDEBAR_WIDTH_KEY), 10);
        if (saved >= minWidth && saved <= maxWidth) aside.style.width = saved + 'px';

        let active = false, startX = 0, startW = 0;

        const onDown = (e) => {
            e.preventDefault();
            active  = true;
            startX  = e.clientX;
            startW  = aside.offsetWidth;
            document.body.style.cursor     = 'ew-resize';
            document.body.style.userSelect = 'none';
        };

        const onMove = (e) => {
            if (!active) return;
            const w = Math.min(maxWidth, Math.max(minWidth, startW + e.clientX - startX));
            aside.style.width = w + 'px';
        };

        const onUp = () => {
            if (!active) return;
            active = false;
            document.body.style.cursor     = '';
            document.body.style.userSelect = '';
            const w = aside.offsetWidth;
            localStorage.setItem(TM_SIDEBAR_WIDTH_KEY, w);
            dotNetRef?.invokeMethodAsync('OnSidebarResized', w).catch(() => {});
        };

        handleEl.addEventListener('mousedown', onDown);
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup',   onUp);

        _sidebarResizes.set(handleEl, {
            cleanup() {
                handleEl.removeEventListener('mousedown', onDown);
                document.removeEventListener('mousemove', onMove);
                document.removeEventListener('mouseup',   onUp);
            }
        });
    }

    function destroySidebarResize(handleEl) {
        if (!handleEl || !_sidebarResizes.has(handleEl)) return;
        _sidebarResizes.get(handleEl).cleanup();
        _sidebarResizes.delete(handleEl);
    }

    // ── 87.1 Page Search (Ctrl+P / Cmd+P) ────────────────────────────────────

    let _pageSearchDotNet   = null;
    let _pageSearchListener = null;

    function registerPageSearch(dotNetRef) {
        destroyPageSearch();
        _pageSearchDotNet = dotNetRef;
        _pageSearchListener = function (e) {
            if ((e.ctrlKey || e.metaKey) && e.key === 'p' && !e.shiftKey && !e.altKey) {
                e.preventDefault();
                _pageSearchDotNet.invokeMethodAsync('OpenPageSearch').catch(console.error);
            }
        };
        document.addEventListener('keydown', _pageSearchListener, true);
    }

    function destroyPageSearch() {
        if (_pageSearchListener) {
            document.removeEventListener('keydown', _pageSearchListener, true);
            _pageSearchListener = null;
        }
        _pageSearchDotNet = null;
    }

    // ── 88.1 Page Settings Helpers ────────────────────────────────────────────

    async function downloadFileStream(fileName, contentStreamRef, mimeType) {
        const buf  = await contentStreamRef.arrayBuffer();
        const blob = new Blob([buf], { type: mimeType || 'application/octet-stream' });
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href     = url;
        a.download = fileName;
        a.style.display = 'none';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(() => URL.revokeObjectURL(url), 15000);
    }

    async function copyToClipboard(text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(text);
        } else {
            const el = document.createElement('textarea');
            el.value = text;
            el.style.position = 'fixed';
            el.style.opacity  = '0';
            document.body.appendChild(el);
            el.select();
            document.execCommand('copy');
            document.body.removeChild(el);
        }
    }

    function getPageUrl(pageId) {
        return window.location.origin + window.location.pathname + '#' + pageId;
    }

    // ── Public API ─────────────────────────────────────────────────────────────
    return {
        // 26.1
        initBlock, destroyBlock, getHtml, setHtml,
        focus, focusAtEnd, focusAtStart, focusAtOffset,
        // 26.2
        getSelectionRange, getSelectionRect, applyFormat,
        queryFormatState, insertHtml, insertLink, wrapSelectionWithComment,
        getBlockBoundingRect,
        // 26.3
        initDragDrop, destroyDragDrop,
        // 26.4
        getCaretCoords, getTextBeforeCaret,
        // 26.5
        initKeyboardHandler,
        // 26.6
        renderEquation, renderInlineMath,
        // 26.7
        handlePaste, copyBlocksToClipboard,
        // 26.8
        initResizeHandle, destroyResizeHandle,
        // 54.0
        setColumnWidth,
        // 54.1
        initColumnResize, destroyColumnResize,
        // 26.9
        scrollToBlock, initSmoothScrollSpy, destroyScrollSpy,
        // 91.1
        updateCollabCursors, clearCollabCursors,
        // 30.1
        startCoverDrag,
        // 32.1
        copyBlockLink,
        // 62.1
        copyText,
        // 37.1
        initCodeKeyboardHandler, getCode, setCode,
        // 58.1
        initTableRowKeyboardHandler, destroyTableRowKeyboardHandler, tableFocusCell,
        // 43.1
        getRecentSlashItems, addRecentSlashItem,
        clearSlashQuery, refocusSlashElement,
        insertMentionChip, cancelMentionTrigger,
        adjustSlashMenuPosition, scrollSlashItemIntoView,
        // 44.1
        initSelectionWatcher, destroySelectionWatcher,
        saveSelection, insertLinkOnSavedSelection,
        applyInlineColor, toggleInlineCode,
        insertInlineMath, adjustInlineToolbarPosition,
        // 45.1
        adjustColorPickerPosition,
        // 46.1
        getRecentEmojis, addRecentEmoji, adjustEmojiPickerPosition,
        // 47.1
        adjustTypeSwitcherPosition,
        // 80.1
        initSidebarResize, destroySidebarResize,
        // 87.1
        registerPageSearch, destroyPageSearch,
        // 88.1
        downloadFileStream, copyToClipboard, getPageUrl
    };
})();

// ═══════════════════════════════════════════════════════════════════════════
// 52.5 — PDF block (tmNotionPdf)
//
// Uses PDF.js v5 (ES modules) via dynamic import — no <script> tag needed.
// Requires pdf.min.mjs + pdf.worker.min.mjs in the same directory as this file.
// ═══════════════════════════════════════════════════════════════════════════

window.tmNotionPdf = (function () {
    'use strict';

    // Capture script directory while document.currentScript is still set.
    const _scriptDir = (() => {
        const src = document.currentScript?.src ?? '';
        return src ? src.substring(0, src.lastIndexOf('/') + 1) : '_content/Tempo.Blazor/js/';
    })();

    // canvasEl → { pdfDoc, currentPage, scale, dotNetRef }
    const _docs = new WeakMap();
    let _lib = null;

    async function _ensureLib() {
        if (_lib) return _lib;
        const mod = await import(_scriptDir + 'pdf.min.mjs');
        mod.GlobalWorkerOptions.workerSrc = _scriptDir + 'pdf.worker.min.mjs';
        _lib = mod;
        return _lib;
    }

    function isAvailable() {
        return true;
    }

    async function init(canvasEl, url, dotNetRef) {
        if (!canvasEl || !url) return;
        destroy(canvasEl);
        try {
            const pdfjs  = await _ensureLib();
            const pdfDoc = await pdfjs.getDocument(url).promise;
            _docs.set(canvasEl, { pdfDoc, currentPage: 1, scale: 1.0, dotNetRef });
            await renderPage(canvasEl, 1, 1.0);
            dotNetRef.invokeMethodAsync('OnPdfLoaded', pdfDoc.numPages).catch(console.error);
        } catch (err) {
            dotNetRef.invokeMethodAsync('OnPdfLoadError', String(err?.message ?? err))
                     .catch(console.error);
        }
    }

    async function renderPage(canvasEl, pageNum, scale) {
        const state = _docs.get(canvasEl);
        if (!state) return;
        state.currentPage = pageNum;
        state.scale       = scale;
        try {
            const page     = await state.pdfDoc.getPage(pageNum);
            const viewport = page.getViewport({ scale });
            canvasEl.width  = viewport.width;
            canvasEl.height = viewport.height;
            await page.render({ canvasContext: canvasEl.getContext('2d'), viewport }).promise;
        } catch (err) {
            console.error('tmNotionPdf.renderPage', err);
        }
    }

    function getTotalPages(canvasEl) {
        return _docs.get(canvasEl)?.pdfDoc?.numPages ?? 0;
    }

    async function setScale(canvasEl, scale) {
        const state = _docs.get(canvasEl);
        if (!state) return;
        await renderPage(canvasEl, state.currentPage, scale);
    }

    function destroy(canvasEl) {
        const state = _docs.get(canvasEl);
        if (!state) return;
        try { state.pdfDoc.destroy(); } catch { }
        _docs.delete(canvasEl);
    }

    return { isAvailable, init, renderPage, getTotalPages, setScale, destroy };
})();

// ── Database utilities ────────────────────────────────────────────────────────

window.tmDb = window.tmDb || {};

window.tmDb.downloadFileFromStream = async function (fileName, contentStreamRef) {
    const arrayBuffer = await contentStreamRef.arrayBuffer();
    const blob        = new Blob([arrayBuffer], { type: 'text/csv;charset=utf-8;' });
    const url         = URL.createObjectURL(blob);
    const anchor      = document.createElement('a');
    anchor.href       = url;
    anchor.download   = fileName;
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    setTimeout(() => URL.revokeObjectURL(url), 10000);
};
