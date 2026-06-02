// Phase D — render/atomic-renderer.mjs
// `createAtomicRendererFactory(factoryDeps)` → `createAtomicRenderer(options)` —
//   the Phase B incremental DOM diff renderer.
//
// Factory deps (all required):
//   `findBlock(model, blockId)` — index-based block lookup for renderObjectScope.
//   `applySegmentStyleToElement(el, style, decorations)` — apply CSS/decorations.
//   `applyObjectFocusPolicyToElement(el, selected, inst)` — focus-policy DOM mutator.
//   `renderSelectionOverlay(snapshot)` → overlay element.
//   `renderRevisionOverlay(snapshot)` → overlay element.
//   `renderCommentMarkers(snapshot)` → overlay element.
//   `restoreLogicalSelection(root, selection)` — writes data-attribute.
//   `doc` (optional, default globalThis.document) — DOM adapter for Node testability.
//
// Pure imports (no injection):
//   `flattenLayoutSegments` from render/render-snapshot.mjs
//   `scopeIncludesBlock`, `rectsOverlap`, `domRectToRect`, `markOverlayNonText`
//     from render/render-helpers.mjs
//
// B1: per-block fingerprint skip via `container.__tmFingerprint` (FNV-1a hash).
// B2: per-segment Map diff (`existingById`) — `insertBefore` keeps order, no `replaceChildren`.
// B3: `validateRenderInvariants` is off by default; opt-in via `opts.diagnostics = true`
//     or `renderer.setDiagnostics(true)`. DOM measurements (getBoundingClientRect) only
//     when `useDomMeasurements: true`.
// B4: `localizeLayoutBlock` uses shallow per-property copy instead of JSON clone.

import { asArray, sortObject } from '../core/helpers.mjs';
import { flattenLayoutSegments } from './render-snapshot.mjs';
import {
    scopeIncludesBlock, rectsOverlap, domRectToRect, markOverlayNonText,
} from './render-helpers.mjs';
import { applySegmentStyleToElement } from '../layout/segment-style.mjs';

const REQUIRED_FACTORY_DEPS = [
    'findBlock',
    'applyObjectFocusPolicyToElement',
    'renderSelectionOverlay',
    'renderRevisionOverlay',
    'renderCommentMarkers',
    'restoreLogicalSelection',
];

export function createAtomicRendererFactory(factoryDeps) {
    const fdeps = factoryDeps || {};
    for (const key of REQUIRED_FACTORY_DEPS) {
        if (typeof fdeps[key] !== 'function') {
            throw new TypeError(`createAtomicRendererFactory requires factoryDeps.${key} (function)`);
        }
    }
    const {
        findBlock,
        applyObjectFocusPolicyToElement,
        renderSelectionOverlay,
        renderRevisionOverlay,
        renderCommentMarkers,
        restoreLogicalSelection,
    } = fdeps;
    // applySegmentStyleToElement can be overridden (e.g. in tests) but has a pure default.
    const applySegStyle = typeof fdeps.applySegmentStyleToElement === 'function'
        ? fdeps.applySegmentStyleToElement
        : applySegmentStyleToElement;

    return function createAtomicRenderer(options) {
        const opts = options || {};
        const doc = fdeps.doc || opts.doc || globalThis.document;
        // Legacy parity defaults to native contenteditable header/footer regions. The
        // model-owned engine (Phase R) sets this false — all input flows through the
        // off-screen input surface, so no DOM node should be natively editable.
        const contentEditableRegions = opts.contentEditableRegions !== false;
        const segmentCache = new Map();
        const blockCache = new Map();
        // B1.4 — track which segment-cache keys each block container owns, so a removed
        // block can release its segment nodes from segmentCache (not just blockCache).
        const blockSegmentKeys = new Map();
        const watchdog = [];
        let emptyFrameCount = 0;
        let lastSnapshot = null;
        let diagnosticsEnabled = !!(opts.diagnostics === true || opts.runtimeDiagnostics === true);
        // B1 counters
        let paragraphFingerprintHits = 0;
        let paragraphFingerprintMisses = 0;
        // B2 counter
        let segmentPatchCount = 0;
        // B1.4 counters
        let blockEvictionCount = 0;
        let segmentEvictionCount = 0;

        // ----------------------------------------------------------------
        // B1 — FNV-1a fingerprint for paragraph layout blocks
        // ----------------------------------------------------------------
        function fingerprintHash(value) {
            let h = 2166136261;
            const s = String(value || '');
            for (let i = 0; i < s.length; i++) {
                h ^= s.charCodeAt(i);
                h = (h * 16777619) >>> 0;
            }
            return h.toString(36);
        }

        function computeParagraphFingerprint(blockLayout) {
            if (!blockLayout) return '';
            const rect = blockLayout.rect || {};
            const lm = blockLayout.listMarker;
            const parts = [
                blockLayout.blockId || '',
                rect.x | 0, rect.y | 0, rect.width | 0, rect.height | 0,
                // R.4.8 — list marker text + position participate in the skip fingerprint.
                lm ? (lm.text || '') : '', lm ? (lm.localX | 0) : '', lm ? (lm.localY | 0) : '',
            ];
            const segs = asArray(blockLayout.segments);
            for (let i = 0; i < segs.length; i++) {
                const seg = segs[i];
                const sr = seg.rect || {};
                const style = seg.style || {};
                parts.push(
                    seg.id || '',
                    sr.x | 0, sr.y | 0, sr.width | 0, sr.height | 0,
                    seg.text || '',
                    style.fontFamily || '', style.fontSize || '',
                    style.fontWeight || '', style.fontStyle || '',
                    style.color || '', style.backgroundColor || '',
                    style.textDecoration || '',
                );
                const decorations = asArray(seg.decorations);
                for (let d = 0; d < decorations.length; d++) parts.push(decorations[d]);
                // R.5.5 — marks drive DOM attributes (data-href / data-bookmark / role) that carry no
                // visual style, so they must participate in the skip fingerprint (else a bookmark added
                // to already-rendered text would be skipped).
                const marks = asArray(seg.marks);
                for (let mi = 0; mi < marks.length; mi++) {
                    const mk = marks[mi];
                    parts.push('m:' + String((mk && (mk.type ?? mk.Type)) || '') + '=' + String((mk && (mk.value ?? mk.Value)) || ''));
                }
            }
            return fingerprintHash(parts.join('|'));
        }

        // ----------------------------------------------------------------
        // B4 — shallow coord-shifting (no JSON clone)
        // ----------------------------------------------------------------
        function shiftRect(rect, dx, dy) {
            if (!rect) return { x: dx, y: dy, width: 0, height: 0 };
            return { x: (rect.x || 0) + dx, y: (rect.y || 0) + dy, width: rect.width || 0, height: rect.height || 0 };
        }

        function localizeLayoutBlock(blockLayout, dx, dy) {
            if (!blockLayout) return blockLayout;
            const c = {};
            for (const key in blockLayout) {
                if (Object.prototype.hasOwnProperty.call(blockLayout, key)) c[key] = blockLayout[key];
            }
            c.rect = shiftRect(blockLayout.rect, dx, dy);
            c.lines = asArray(blockLayout.lines).map(function (line) {
                if (!line) return line;
                const lc = {};
                for (const k in line) { if (Object.prototype.hasOwnProperty.call(line, k)) lc[k] = line[k]; }
                lc.rect = shiftRect(line.rect, dx, dy);
                lc.baseline = (line.baseline || 0) + dy;
                if (line.availableIntervals) {
                    lc.availableIntervals = asArray(line.availableIntervals).map(function (iv) {
                        return Object.assign({}, iv, { x: (iv.x || 0) + dx, y: (iv.y || 0) + dy });
                    });
                }
                return lc;
            });
            c.segments = asArray(blockLayout.segments).map(function (seg) {
                if (!seg) return seg;
                const sc = {};
                for (const sk in seg) { if (Object.prototype.hasOwnProperty.call(seg, sk)) sc[sk] = seg[sk]; }
                sc.rect = shiftRect(seg.rect, dx, dy);
                if (seg.objectRect) sc.objectRect = shiftRect(seg.objectRect, dx, dy);
                return sc;
            });
            if (blockLayout.inlineObjects) {
                c.inlineObjects = asArray(blockLayout.inlineObjects).map(function (obj) {
                    if (!obj) return obj;
                    const oc = {};
                    for (const ok in obj) { if (Object.prototype.hasOwnProperty.call(obj, ok)) oc[ok] = obj[ok]; }
                    oc.rect = shiftRect(obj.rect, dx, dy);
                    return oc;
                });
            }
            if (blockLayout.caretStops) {
                c.caretStops = asArray(blockLayout.caretStops).map(function (stop) {
                    if (!stop) return stop;
                    const stc = {};
                    for (const stk in stop) { if (Object.prototype.hasOwnProperty.call(stop, stk)) stc[stk] = stop[stk]; }
                    stc.rect = shiftRect(stop.rect, dx, dy);
                    return stc;
                });
            }
            // R.4.6c — table geometry (cells/rows) must shift with the block too.
            if (blockLayout.cells) {
                c.cells = asArray(blockLayout.cells).map(function (cellLayout) {
                    if (!cellLayout) return cellLayout;
                    return Object.assign({}, cellLayout, { rect: shiftRect(cellLayout.rect, dx, dy) });
                });
            }
            if (blockLayout.rows) {
                c.rows = asArray(blockLayout.rows).map(function (rowLayout) {
                    if (!rowLayout || !rowLayout.rect) return rowLayout;
                    return Object.assign({}, rowLayout, { rect: shiftRect(rowLayout.rect, dx, dy) });
                });
            }
            return c;
        }

        function localizeLayoutBlockToFrame(blockLayout, frame) {
            return localizeLayoutBlock(blockLayout, -(frame && frame.x || 0), -(frame && frame.y || 0));
        }

        function localizeLayoutBlockToPage(blockLayout, page) {
            return localizeLayoutBlock(blockLayout, -(page && page.rect && page.rect.x || 0), -(page && page.rect && page.rect.y || 0));
        }

        // ----------------------------------------------------------------
        // B3 — invariant validation (off by default)
        // ----------------------------------------------------------------
        function validateRenderInvariants(root, snapshot, invOptions) {
            const layoutSegments = flattenLayoutSegments(snapshot && snapshot.layout);
            const layoutIds = new Set(layoutSegments.map(function (seg) { return seg.id; }));
            const domSegments = Array.from(root.querySelectorAll('[data-layout-segment-id]'));
            const orphanNodes = domSegments.filter(function (node) {
                return !layoutIds.has(node.getAttribute('data-layout-segment-id'));
            });
            const mappedTextNodes = domSegments.filter(function (node) {
                return !!node.getAttribute('data-model-block-id')
                    && node.firstChild && node.firstChild.nodeType === 3;
            }).length;
            const useDom = invOptions && invOptions.useDomMeasurements === true;
            const wrappedSegments = useDom
                ? domSegments.filter(function (node) {
                    const expected = Number(node.getAttribute('data-layout-height') || 0);
                    const rect = node.getBoundingClientRect();
                    return expected > 0 && rect.height > expected + 1.5;
                }).length
                : 0;
            let forbiddenOverlaps = 0;
            if (useDom) {
                asArray(invOptions && invOptions.forbiddenRects).forEach(function (forbidden) {
                    domSegments.forEach(function (node) {
                        if (rectsOverlap(domRectToRect(node.getBoundingClientRect()), forbidden)) forbiddenOverlaps++;
                    });
                });
            }
            return sortObject({
                ok: orphanNodes.length === 0 && mappedTextNodes === domSegments.length
                    && layoutSegments.length === domSegments.length
                    && wrappedSegments === 0 && forbiddenOverlaps === 0,
                mappedTextNodes,
                layoutSegmentCount: layoutSegments.length,
                domSegmentCount: domSegments.length,
                orphanNodeCount: orphanNodes.length,
                wrappedSegments,
                forbiddenOverlaps,
                usedDomMeasurements: useDom,
            });
        }

        function updateDebugOrphans(root, snapshot) {
            return validateRenderInvariants(root, snapshot, {}).orphanNodeCount;
        }

        // ----------------------------------------------------------------
        // Segment rendering (B2 — reuse + in-place text update)
        // ----------------------------------------------------------------
        function computeSegmentKey(segment, blockLayout) {
            return [
                segment.id,
                segment.region || blockLayout.region || 'Body',
                segment.headerFooterId || blockLayout.headerFooterId || '',
                segment.pageIndex ?? blockLayout.pageIndex ?? '',
                blockLayout.fragmentIndex ?? '',
            ].join(':');
        }

        function renderSegment(snapshot, segment, blockLayout) {
            const key = computeSegmentKey(segment, blockLayout);
            let span = segmentCache.get(key);
            if (!span) {
                span = doc.createElement('span');
                span.appendChild(doc.createTextNode(''));
                segmentCache.set(key, span);
            }
            span.className = 'tm-render-segment';
            span.setAttribute('data-layout-segment-id', segment.id);
            span.setAttribute('data-model-block-id', segment.blockId || blockLayout.blockId);
            span.setAttribute('data-model-run-id', segment.runId || '');
            span.setAttribute('data-model-start', segment.start);
            span.setAttribute('data-model-end', segment.end);
            span.setAttribute('data-layout-height', segment.rect.height);
            span.style.position = 'absolute';
            span.style.left = (segment.rect.x - blockLayout.rect.x) + 'px';
            span.style.top = (segment.rect.y - blockLayout.rect.y) + 'px';
            span.style.width = segment.rect.width + 'px';
            span.style.height = segment.rect.height + 'px';
            span.style.lineHeight = segment.rect.height + 'px';
            span.style.whiteSpace = 'pre';
            span.style.overflow = 'hidden';
            span.style.display = 'block';
            // R.4.5 — bidi: an RTL segment is isolated + direction:rtl so the browser
            // shapes Arabic joining and orders glyphs right-to-left inside the box. The
            // engine has already placed the segment box in visual order.
            if (segment.direction === 'rtl') {
                span.setAttribute('dir', 'rtl');
                span.style.direction = 'rtl';
                span.style.unicodeBidi = 'isolate';
            } else {
                // Reset (a cached span may have been RTL before); leave default otherwise.
                if (typeof span.removeAttribute === 'function') span.removeAttribute('dir');
                span.style.direction = '';
                span.style.unicodeBidi = '';
            }
            applySegStyle(span, segment.style || {}, segment.decorations || []);
            // R.4.6h — hyperlink: surface the href + link role (real DOM → also enables
            // native middle-click / context "open link" + screen-reader link semantics).
            const linkMark = asArray(segment.marks).find(function (m) {
                const tp = String((m && (m.type ?? m.Type)) || '').toLowerCase();
                return tp === 'link' || tp === 'hyperlink';
            });
            if (linkMark) {
                const href = linkMark.value ?? linkMark.Value ?? linkMark.href ?? linkMark.Href ?? '';
                span.setAttribute('data-href', String(href));
                span.setAttribute('role', 'link');
                span.style.cursor = 'pointer';
            } else if (typeof span.removeAttribute === 'function') {
                span.removeAttribute('data-href');
            }
            // R.5.5 — bookmark anchor: surface the name so navigation can scroll to it.
            const bookmarkMark = asArray(segment.marks).find(function (m) {
                return String((m && (m.type ?? m.Type)) || '').toLowerCase() === 'bookmark';
            });
            if (bookmarkMark && typeof span.setAttribute === 'function') {
                span.setAttribute('data-bookmark', String(bookmarkMark.value ?? bookmarkMark.Value ?? ''));
            } else if (typeof span.removeAttribute === 'function') {
                span.removeAttribute('data-bookmark');
            }
            if (!span.firstChild) span.appendChild(doc.createTextNode(''));
            if (span.firstChild.nodeValue !== (segment.text || '')) span.firstChild.nodeValue = segment.text || '';
            return span;
        }

        // ----------------------------------------------------------------
        // B1 — paragraph scope with fingerprint skip + B2 segment diff
        // ----------------------------------------------------------------
        function computeBlockKey(blockLayout) {
            return [
                blockLayout.blockId,
                blockLayout.region || 'Body',
                blockLayout.headerFooterId || '',
                blockLayout.pageIndex ?? '',
                blockLayout.fragmentIndex ?? '',
            ].join(':');
        }

        function renderParagraphScope(snapshot, blockLayout) {
            const key = computeBlockKey(blockLayout);
            let container = blockCache.get(key);
            const firstRender = !container;
            if (firstRender) {
                container = doc.createElement('div');
                blockCache.set(key, container);
            }

            // B1 fingerprint skip
            const nextFingerprint = computeParagraphFingerprint(blockLayout);
            if (!firstRender && container.__tmFingerprint === nextFingerprint) {
                paragraphFingerprintHits++;
                return container;
            }
            paragraphFingerprintMisses++;

            container.className = 'tm-render-paragraph';
            container.setAttribute('data-render-block-id', blockLayout.blockId);
            container.setAttribute('data-model-id', blockLayout.blockId);
            // R.4.7 — heading semantics for screen readers (role=heading + aria-level).
            const modelPara = findBlock(snapshot && snapshot.model, blockLayout.blockId);
            const headingLevel = modelPara && modelPara.content
                && (modelPara.content.headingLevel != null ? modelPara.content.headingLevel : modelPara.content.HeadingLevel);
            if (headingLevel != null && Number(headingLevel) >= 1) {
                container.setAttribute('role', 'heading');
                container.setAttribute('aria-level', String(Math.max(1, Math.min(6, Number(headingLevel) || 1))));
            } else if (typeof container.removeAttribute === 'function') {
                container.removeAttribute('role');
                container.removeAttribute('aria-level');
            }
            container.style.position = 'absolute';
            container.style.left = blockLayout.rect.x + 'px';
            container.style.top = blockLayout.rect.y + 'px';
            container.style.width = blockLayout.rect.width + 'px';
            container.style.height = blockLayout.rect.height + 'px';
            container.style.whiteSpace = 'pre';
            container.style.overflow = 'visible';

            // B2 — per-segment diff.
            // R.5.20 — reading order for assistive tech: segments are laid out in VISUAL order
            // (bidi reorders RTL runs), but absolute positioning means DOM order is free. Append
            // them in LOGICAL order (by model start) so a screen reader reads bidi/RTL text in
            // reading order; the visual layout is unchanged (each box keeps its own left/top).
            const nextSegments = asArray(blockLayout.segments).slice()
                .sort(function (a, b) { return (Number(a.start) || 0) - (Number(b.start) || 0); });
            const existingById = new Map();
            let child = container.firstChild;
            while (child) {
                const next = child.nextSibling;
                const sid = child.getAttribute && child.getAttribute('data-layout-segment-id');
                if (sid) existingById.set(sid, child);
                child = next;
            }
            const reused = new Set();
            const segKeys = new Set();
            let anchor = null;
            for (let i = 0; i < nextSegments.length; i++) {
                const seg = nextSegments[i];
                const node = renderSegment(snapshot, seg, blockLayout);
                segKeys.add(computeSegmentKey(seg, blockLayout));
                segmentPatchCount++;
                if (existingById.has(seg.id)) reused.add(seg.id);
                const expectedNext = anchor ? anchor.nextSibling : container.firstChild;
                if (expectedNext !== node) container.insertBefore(node, expectedNext);
                anchor = node;
            }
            existingById.forEach(function (oldNode, sid) {
                if (!reused.has(sid) && oldNode.parentNode === container) container.removeChild(oldNode);
            });

            // R.4.8 — hanging list marker (bullet / number) drawn in the gutter. Non-editable
            // (no segment id), so it is invisible to the segment diff + caret/hit-test.
            renderListMarker(container, blockLayout.listMarker);

            // B1.4 — remember the segment-cache keys this block owns, so a later
            // removal can release them from segmentCache too.
            blockSegmentKeys.set(key, segKeys);
            container.__tmFingerprint = nextFingerprint;
            return container;
        }

        function renderListMarker(container, marker) {
            let el = null;
            let child = container.firstChild;
            while (child) {
                if (child.getAttribute && child.getAttribute('data-list-marker') != null) { el = child; break; }
                child = child.nextSibling;
            }
            if (!marker) { if (el && el.parentNode === container) container.removeChild(el); return; }
            if (!el) {
                el = doc.createElement('span');
                el.setAttribute('data-list-marker', '');
                el.setAttribute('aria-hidden', 'true');
                el.className = 'tm-render-list-marker';
                container.insertBefore(el, container.firstChild);
            }
            if (el.textContent !== marker.text) el.textContent = marker.text;
            el.style.position = 'absolute';
            el.style.left = (marker.localX | 0) + 'px';
            el.style.top = (marker.localY | 0) + 'px';
            if (marker.height) el.style.height = (marker.height | 0) + 'px';
            el.style.pointerEvents = 'none';
            el.style.userSelect = 'none';
            el.style.whiteSpace = 'pre';
        }

        // ----------------------------------------------------------------
        // Object block scope
        // ----------------------------------------------------------------
        function renderObjectScope(snapshot, blockLayout) {
            const modelBlock = findBlock(snapshot && snapshot.model, blockLayout.blockId);
            const node = doc.createElement('figure');
            const selected = snapshot && snapshot.selection
                && (snapshot.selection.objectId === (blockLayout.objectId || blockLayout.blockId)
                    || (snapshot.selection.blockId === blockLayout.blockId && snapshot.selection.isObjectSelection === true));
            node.className = 'tm-render-object tm-render-image-widget'
                + (selected ? ' tm-wysiwyg-image--selected tm-wysiwyg-object--selected' : '');
            node.setAttribute('data-render-block-id', blockLayout.blockId);
            node.setAttribute('data-render-object-id', blockLayout.objectId || blockLayout.blockId);
            node.setAttribute('data-model-id', blockLayout.blockId);
            node.setAttribute('data-wrap-mode', blockLayout.wrapMode || (blockLayout.object && blockLayout.object.wrapMode) || '');
            node.setAttribute('data-anchor-block-id', (blockLayout.object && blockLayout.object.anchorBlockId) || '');
            const objectLabel = (modelBlock && modelBlock.content && (modelBlock.content.altText || modelBlock.content.caption)) || 'Image';
            node.setAttribute('role', 'figure');
            node.setAttribute('aria-label', objectLabel);
            applyObjectFocusPolicyToElement(node, selected);
            if (modelBlock && modelBlock.content && !modelBlock.content.altText) {
                node.setAttribute('aria-describedby', 'tm-render-image-alt-warning-' + blockLayout.blockId);
            }
            node.style.position = 'absolute';
            node.style.left = blockLayout.rect.x + 'px';
            node.style.top = blockLayout.rect.y + 'px';
            node.style.width = Math.min(
                blockLayout.rect.width,
                Number((modelBlock && modelBlock.content && modelBlock.content.layout
                    && (modelBlock.content.layout.width || modelBlock.content.layout.Width)) || 120)
            ) + 'px';
            node.style.height = blockLayout.rect.height + 'px';
            node.style.zIndex = String(blockLayout.zIndex || (blockLayout.object && blockLayout.object.zIndex) || 0);
            const label = doc.createElement('figcaption');
            label.textContent = objectLabel;
            node.appendChild(label);
            if (modelBlock && modelBlock.content && !modelBlock.content.altText) {
                const warning = doc.createElement('span');
                warning.id = 'tm-render-image-alt-warning-' + blockLayout.blockId;
                warning.className = 'tm-document-wysiwyg-host__sr-only';
                warning.setAttribute('data-testid', 'document-wysiwyg-image-alt-warning');
                warning.setAttribute('role', 'status');
                warning.setAttribute('aria-live', 'polite');
                warning.textContent = 'Image is missing alternative text.';
                node.appendChild(warning);
            }
            if (selected) {
                const selectionBox = doc.createElement('span');
                selectionBox.className = 'tm-wysiwyg-selection-box';
                selectionBox.setAttribute('data-testid', 'document-wysiwyg-object-selection-box');
                node.appendChild(selectionBox);
            }
            ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'].forEach(function (handleName) {
                const handle = doc.createElement('span');
                handle.className = 'tm-wysiwyg-object-resize-handle tm-wysiwyg-object-resize-handle--' + handleName;
                handle.setAttribute('data-resize-handle', handleName);
                handle.setAttribute('data-testid', 'document-wysiwyg-object-resize-handle-' + handleName);
                node.appendChild(handle);
            });
            const rotation = doc.createElement('span');
            rotation.className = 'tm-wysiwyg-object-rotation-handle';
            rotation.setAttribute('data-testid', 'document-wysiwyg-object-rotation-handle');
            node.appendChild(rotation);
            const bubble = doc.createElement('span');
            bubble.className = 'tm-wysiwyg-layout-bubble';
            bubble.setAttribute('data-testid', 'document-wysiwyg-layout-bubble');
            bubble.textContent = '';
            node.appendChild(bubble);
            return node;
        }

        // ----------------------------------------------------------------
        // Table scope (R.4.6c): cell borders + cell text segments. Cells/segments come
        // pre-positioned (page-local after localizeLayoutBlock), so segments render the
        // same way as paragraph segments and cell borders use the localized cell rects.
        // ----------------------------------------------------------------
        function renderTableScope(snapshot, blockLayout) {
            const container = doc.createElement('div');
            container.className = 'tm-render-table';
            container.setAttribute('data-render-block-id', blockLayout.blockId);
            container.setAttribute('data-render-table-id', blockLayout.blockId);
            container.style.position = 'absolute';
            container.style.left = blockLayout.rect.x + 'px';
            container.style.top = blockLayout.rect.y + 'px';
            container.style.width = blockLayout.rect.width + 'px';
            container.style.height = blockLayout.rect.height + 'px';
            asArray(blockLayout.cells).forEach(function (cellLayout) {
                const cr = cellLayout.rect || {};
                const cell = doc.createElement('div');
                cell.className = 'tm-render-table-cell';
                cell.setAttribute('data-render-cell-id', cellLayout.cellId || cellLayout.id || '');
                cell.style.position = 'absolute';
                cell.style.left = ((cr.x || 0) - blockLayout.rect.x) + 'px';
                cell.style.top = ((cr.y || 0) - blockLayout.rect.y) + 'px';
                cell.style.width = (cr.width || 0) + 'px';
                cell.style.height = (cr.height || 0) + 'px';
                cell.style.border = '1px solid #c8c8c8';
                cell.style.boxSizing = 'border-box';
                container.appendChild(cell);
            });
            asArray(blockLayout.segments).forEach(function (seg) {
                container.appendChild(renderSegment(snapshot, seg, blockLayout));
            });
            return container;
        }

        // ----------------------------------------------------------------
        // Frame / page region builders
        // ----------------------------------------------------------------
        function renderFrameNode(frameName, regionName, frame, page) {
            const node = doc.createElement('div');
            node.className = 'tm-render-' + frameName + '-frame';
            node.setAttribute('data-render-frame', frameName);
            node.setAttribute('data-render-region-name', regionName);
            node.style.position = 'absolute';
            node.style.left = ((frame && frame.x || 0) - (page && page.rect && page.rect.x || 0)) + 'px';
            node.style.top = ((frame && frame.y || 0) - (page && page.rect && page.rect.y || 0)) + 'px';
            node.style.width = (frame && frame.width || 0) + 'px';
            node.style.height = (frame && frame.height || 0) + 'px';
            if (frameName === 'body') {
                node.style.outline = '1px solid rgba(37, 99, 235, 0.5)';
            }
            return node;
        }

        function renderHeaderFooterRegion(snapshot, page, pageIndex, regionName) {
            const regionLayout = asArray(
                snapshot && snapshot.layout && snapshot.layout.headerFooterRegions
            ).find(function (region) {
                return region.region === regionName && Number(region.pageIndex || 0) === pageIndex;
            });
            const frame = regionName === 'Header' ? page.headerFrame : page.footerFrame;
            const node = renderFrameNode(
                regionName === 'Header' ? 'header-content' : 'footer-content',
                regionName, frame, page
            );
            node.className = regionName === 'Header' ? 'tm-render-header-region' : 'tm-render-footer-region';
            node.setAttribute('data-testid', regionName === 'Header' ? 'document-page-header' : 'document-page-footer');
            node.setAttribute('data-render-region', regionName);
            node.setAttribute('data-render-page-index', pageIndex);
            node.setAttribute('data-hf-id', (regionLayout && regionLayout.headerFooterId) || '');
            if (contentEditableRegions) node.setAttribute('contenteditable', 'true');
            node.setAttribute('role', 'textbox');
            node.setAttribute('aria-multiline', 'true');
            node.setAttribute('aria-label', regionName + ', page ' + (pageIndex + 1));
            node.setAttribute('tabindex', '0');
            asArray(regionLayout && regionLayout.blocks).forEach(function (blockLayout) {
                if (blockLayout.type === 'paragraph') {
                    node.appendChild(renderParagraphScope(snapshot, localizeLayoutBlockToFrame(blockLayout, frame)));
                } else {
                    node.appendChild(renderObjectScope(snapshot, localizeLayoutBlockToFrame(blockLayout, frame)));
                }
            });
            return node;
        }

        function renderPageRegion(snapshot, page, pageIndex, scope) {
            const pageNode = doc.createElement('section');
            pageNode.className = 'tm-render-page';
            pageNode.setAttribute('data-render-page-index', pageIndex);
            pageNode.style.position = 'relative';
            pageNode.style.width = (page.rect && page.rect.width || 640) + 'px';
            pageNode.style.minHeight = (page.rect && page.rect.height || 900) + 'px';
            pageNode.style.height = (page.rect && page.rect.height || 900) + 'px';
            pageNode.appendChild(renderFrameNode('header', 'Header', page.headerFrame, page));
            const bodyFrame = renderFrameNode('body', 'Body', page.bodyFrame, page);
            const textLayer = doc.createElement('div');
            textLayer.setAttribute('data-render-layer', 'text');
            textLayer.style.position = 'absolute';
            textLayer.style.inset = '0';
            const objectLayer = doc.createElement('div');
            objectLayer.setAttribute('data-render-layer', 'object');
            objectLayer.style.position = 'absolute';
            objectLayer.style.inset = '0';
            asArray(snapshot && snapshot.layout && snapshot.layout.blocks).forEach(function (blockLayout) {
                if (Number(blockLayout.pageIndex || 0) !== pageIndex) return;
                if (!scopeIncludesBlock(scope, blockLayout.blockId)) return;
                const pageBlock = localizeLayoutBlockToPage(blockLayout, page);
                if (blockLayout.type === 'paragraph') {
                    textLayer.appendChild(renderParagraphScope(snapshot, pageBlock));
                } else if (blockLayout.type === 'table') {
                    textLayer.appendChild(renderTableScope(snapshot, pageBlock));
                } else if (blockLayout.type !== 'pageBreak') {
                    objectLayer.appendChild(renderObjectScope(snapshot, pageBlock));
                }
            });
            pageNode.appendChild(bodyFrame);
            pageNode.appendChild(textLayer);
            pageNode.appendChild(objectLayer);
            pageNode.appendChild(renderHeaderFooterRegion(snapshot, page, pageIndex, 'Header'));
            pageNode.appendChild(renderHeaderFooterRegion(snapshot, page, pageIndex, 'Footer'));
            pageNode.appendChild(renderFrameNode('footer', 'Footer', page.footerFrame, page));
            pageNode.appendChild(renderSelectionOverlay(snapshot));
            pageNode.appendChild(renderRevisionOverlay(snapshot));
            pageNode.appendChild(renderCommentMarkers(snapshot));
            return pageNode;
        }

        function renderSnapshotFragment(snapshot, renderOptions) {
            const scope = renderOptions && renderOptions.scope || null;
            const host = doc.createElement('div');
            host.className = 'tm-render-snapshot';
            host.setAttribute('data-render-snapshot', (snapshot && snapshot.fingerprint) || '');
            host.setAttribute('data-model-version', (snapshot && snapshot.modelVersion) || 0);
            host.setAttribute('data-layout-version', (snapshot && snapshot.layoutVersion) || 0);
            host.setAttribute('data-selection-version', (snapshot && snapshot.selectionVersion) || 0);
            const layout = (snapshot && snapshot.layout) || {};
            asArray(layout.pages).forEach(function (page, pageIndex) {
                host.appendChild(renderPageRegion(snapshot, page, pageIndex, scope));
            });
            if (!asArray(layout.pages).length) {
                host.appendChild(renderPageRegion(snapshot, { pageNumber: 1, rect: { x: 0, y: 0, width: 640, height: 900 } }, 0, scope));
            }
            return host;
        }

        // ----------------------------------------------------------------
        // B1.4 — cache pruning (block + segment eviction)
        // ----------------------------------------------------------------
        // Collects the set of block-cache keys that are still present in the snapshot.
        // Uses the full `snapshot.layout.blocks` (+ header/footer region blocks), NOT
        // "which blocks were painted this pass" — so scoped/partial renders don't evict
        // valid cached blocks that are simply outside the current scope.
        function collectValidBlockKeys(snapshot) {
            const valid = new Set();
            asArray(snapshot && snapshot.layout && snapshot.layout.blocks).forEach(function (blockLayout) {
                if (blockLayout && blockLayout.type === 'paragraph') valid.add(computeBlockKey(blockLayout));
            });
            asArray(snapshot && snapshot.layout && snapshot.layout.headerFooterRegions).forEach(function (region) {
                asArray(region && region.blocks).forEach(function (blockLayout) {
                    if (blockLayout && blockLayout.type === 'paragraph') valid.add(computeBlockKey(blockLayout));
                });
            });
            return valid;
        }

        // Evicts cached block containers (and their owned segment nodes) that are no
        // longer present in the snapshot — prevents unbounded cache growth (B1.4).
        function pruneCaches(snapshot) {
            const valid = collectValidBlockKeys(snapshot);
            blockCache.forEach(function (_container, key) {
                if (valid.has(key)) return;
                blockCache.delete(key);
                blockEvictionCount++;
                const segKeys = blockSegmentKeys.get(key);
                if (segKeys) {
                    segKeys.forEach(function (segKey) {
                        if (segmentCache.delete(segKey)) segmentEvictionCount++;
                    });
                    blockSegmentKeys.delete(key);
                }
            });
        }

        // ----------------------------------------------------------------
        // Main render entry point
        // ----------------------------------------------------------------
        function render(root, snapshot, renderOptions) {
            const renderOpts = renderOptions || {};
            const beforeHtml = root ? root.innerHTML : '';
            const allowDiagnostics = diagnosticsEnabled || renderOpts.diagnostics === true;
            try {
                if (!root) throw new Error('render root is required');
                const fragment = doc.createDocumentFragment();
                const nextTree = renderSnapshotFragment(snapshot, renderOpts);
                fragment.appendChild(nextTree);
                if (renderOpts.failBeforeSwap) throw new Error('forced render failure before atomic swap');
                root.replaceChildren(fragment);
                restoreLogicalSelection(root, snapshot && snapshot.selection);
                // B1.4 — evict cached containers/segments for blocks no longer present.
                pruneCaches(snapshot);
                lastSnapshot = snapshot;
                const text = root.textContent || '';
                if (!text && flattenLayoutSegments(snapshot && snapshot.layout).length > 0) emptyFrameCount++;
                const invariants = allowDiagnostics
                    ? validateRenderInvariants(root, snapshot, renderOpts)
                    : { ok: true, mappedTextNodes: 0, layoutSegmentCount: 0, domSegmentCount: 0, orphanNodeCount: 0, wrappedSegments: 0, forbiddenOverlaps: 0 };
                if (allowDiagnostics) updateDebugOrphans(root, snapshot);
                return sortObject({ ok: true, rolledBack: false, invariants, snapshotFingerprint: (snapshot && snapshot.fingerprint) || '' });
            } catch (error) {
                if (root) root.innerHTML = beforeHtml;
                watchdog.push({ message: String((error && error.message) || error), at: Date.now() });
                return sortObject({ ok: true, rolledBack: true, error: String((error && error.message) || error), watchdogFailures: watchdog.length });
            }
        }

        // ----------------------------------------------------------------
        // Debug / diagnostics API
        // ----------------------------------------------------------------
        function debug() {
            return sortObject({
                watchdogFailures: watchdog.length,
                emptyFrameCount,
                orphanNodeCount: 0,
                duplicateToolbarCount: 0,
                cachedSegmentCount: segmentCache.size,
                cachedBlockCount: blockCache.size,
                paragraphFingerprintHits,
                paragraphFingerprintMisses,
                segmentPatchCount,
                blockEvictionCount,
                segmentEvictionCount,
                diagnosticsEnabled,
            });
        }

        function resetDebugCounters() {
            paragraphFingerprintHits = 0;
            paragraphFingerprintMisses = 0;
            segmentPatchCount = 0;
            emptyFrameCount = 0;
            blockEvictionCount = 0;
            segmentEvictionCount = 0;
        }

        function setDiagnostics(enabled) {
            diagnosticsEnabled = !!enabled;
        }

        // R.4.9.3b-2 — update ONLY the given paragraph blocks' DOM containers in place (they are
        // already attached from a prior full render), without rebuilding the page fragment or
        // calling replaceChildren. O(dirty). Returns { ok: false } if any block is not already
        // rendered (e.g. virtualized out / never painted) → the caller falls back to a full render.
        function patchBlocks(root, snapshot, blockIds) {
            if (!root || !snapshot || !snapshot.layout) return { ok: false, reason: 'no-snapshot' };
            const blocks = asArray(snapshot.layout.blocks);
            const pages = asArray(snapshot.layout.pages);
            const ids = asArray(blockIds);
            if (!ids.length) return { ok: false, reason: 'no-ids' };
            for (let n = 0; n < ids.length; n++) {
                const id = ids[n];
                let bl = null;
                for (let k = 0; k < blocks.length; k++) { if (blocks[k] && blocks[k].blockId === id) { bl = blocks[k]; break; } }
                if (!bl || bl.type !== 'paragraph') return { ok: false, reason: 'not-simple-paragraph' };
                const page = pages[Number(bl.pageIndex) || 0];
                if (!page || !page.rect) return { ok: false, reason: 'no-page' };
                const container = renderParagraphScope(snapshot, localizeLayoutBlockToPage(bl, page));
                if (!container || !container.parentNode) return { ok: false, reason: 'not-attached' };
            }
            lastSnapshot = snapshot;
            return { ok: true, patched: ids.length };
        }

        return {
            render,
            patchBlocks,
            renderParagraphScope,
            renderPageRegion,
            renderObjectScope,
            renderSelectionOverlay,
            renderRevisionOverlay,
            renderCommentMarkers,
            validateRenderInvariants,
            debug,
            resetDebugCounters,
            setDiagnostics,
        };
    };
}
