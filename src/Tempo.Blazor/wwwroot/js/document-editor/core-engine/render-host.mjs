// Phase R.4.1 — core-engine/render-host.mjs
// First end-to-end assembly of the new model-owned engine: it wires together the
// extracted modules into a working pipeline:
//
//     model → paragraph-engine.layoutDocument (real font metrics)
//           → createRenderSnapshot (model + layout + selection)
//           → atomic-renderer.render (positioned-DOM, B1/B2 diff)
//
// This is the foundation the off-screen input (R.4.2) and caret/selection overlay
// (R.4.3) will build on. It does NOT use the legacy contenteditable surface or
// DOM-readback layout — layout is computed headlessly, the DOM is pure output.
//
// `createRenderHost(options?)` → host:
//   - `mount(rootElement)` — attaches the render root.
//   - `setModel(model)` — sets the document model (rebuilds indexes on render).
//   - `setSelection(selection)` — sets the logical selection used by the next render.
//   - `setViewport({ scrollTop, height, overscanPages? })` — enables page
//        virtualization: only blocks on visible (±overscan) pages are painted; page
//        frames keep full height so scroll geometry is preserved. Pass null to paint all.
//   - `render()` — runs the pipeline; returns `{ ok, layout, snapshot, renderResult }`.
//   - `getLayout()`, `getSnapshot()`, `getRenderer()`, `getEngine()`.
//   - `destroy()`.
//
// Options:
//   `doc` — DOM adapter (default globalThis.document); inject a stub in Node tests.
//   `pageSettings` — page geometry override passed to the layout engine.
//   `layoutOptions` — extra layout options (width/margins/etc).
//   `measurementService` — override the font-metrics service (else engine default).

import { normalizeImageObject } from '../objects/image-object.mjs';
import { createIndexBuilder, findBlockByIndex } from '../core/indexes.mjs';
import { findBlockContainer } from '../core/model-finders.mjs';
import { createParagraphLayoutEngineFactory } from '../layout/paragraph-engine.mjs';
import { createPageLayout } from '../layout/page-metrics.mjs';
import { createRenderSnapshot } from '../render/render-snapshot.mjs';
import { createAtomicRendererFactory } from '../render/atomic-renderer.mjs';
import {
    createRenderSelectionOverlay,
    createRenderRevisionOverlay,
    createRenderCommentMarkers,
    restoreLogicalSelection,
    createApplyObjectFocusPolicyToElement,
} from '../render/atomic-overlays.mjs';
import { asArray } from '../core/helpers.mjs';
import { createInputSurface } from './input-surface.mjs';
import {
    applyInsertText,
    applyDeleteBackward,
    applyDeleteForward,
    applyInsertParagraph,
    applyReplaceRange,
} from './edit-model.mjs';
import { hitTestPoint, caretStopAt } from './hit-test.mjs';
import { serializeRange, parseClipboard, orderRange, rangeIsCollapsed, INTERNAL_MIME } from './clipboard.mjs';
import { moveCaretByKey, createCaretElement } from './caret.mjs';
import { wordRangeAt } from '../layout/grapheme.mjs';
import { selectionRectsForRange, createSelectionRectElement, createCompositionUnderlineElement, createFindHighlightElement, createSpellUnderlineElement, createRemoteCaretElement } from './selection-overlay.mjs';
import { findMatches, expandReplacement } from './find-replace.mjs';
import { acceptAllRevisions, rejectAllRevisions, acceptRevision, rejectRevision, applyReviewMode, listRevisions, hasRevisions, INSERTION_MARK, DELETION_MARK, FORMAT_REV_MARK, PARAGRAPH_MARK_KEY } from './track-changes.mjs';
import { addCommentMarkToRange, stripCommentMark, commentAnchorText, commentIdsInRange } from './comments.mjs';
import { setRegion as setHeaderFooterRegion, clearRegion as clearHeaderFooterRegion } from './header-footer.mjs';
import { applyEditorAria, describeCaretContext, describeCaretGranular, createLiveRegion } from './a11y.mjs';
import { formattingStateForBlockRange } from './format-state.mjs';
import { applyBidiToLayout } from './bidi-line.mjs';
import { applyListLayout } from './list-layout.mjs';
import { blockText } from '../core/text-helpers.mjs';
import { deleteTextRange } from '../core/run-mutators.mjs';
import { mergeAdjacentTextRuns, plainRuns } from '../core/inline-runs.mjs';
import { applyMarkToBlockRange, blockRangeHasMark, setParagraphProperty, firstMarkValueInRange } from './edit-format.mjs';
import { createObjectElement, objectHitTest, resizeRectByHandle } from './object-overlay.mjs';
import { insertDrawingRunAtTextOffset } from '../objects/image-insert.mjs';
import { stableId, clone } from '../core/helpers.mjs';
import { createUndoStack } from './undo-stack.mjs';
import { invertOperation } from './operations.mjs';
import { applyParagraphStyle, getDocumentOutline, paragraphStyleName, buildStyleRegistry, defineStyle as defineModelStyle } from './paragraph-styles.mjs';
import { isListBlock, listTypeOf, toggleListType, changeListLevel } from './list-model.mjs';
import { createTableModel, firstCellParagraphId, insertTableAfterBlock, addTableRow, addTableColumn, findTableContaining,
    locateCell, adjacentCellParagraphId, cellFirstParagraphId, deleteTableRow, deleteTableColumn, mergeCellRight, splitCellHorizontal,
    mergeCellDown, splitCellVertical, cellRangeIds, setColumnWidth } from './edit-table.mjs';

export function createRenderHost(options) {
    const opts = options || {};
    const doc = opts.doc || globalThis.document;

    // --- model index + findBlock -------------------------------------------------
    const { buildIndexes } = createIndexBuilder({ normalizeImageObject });
    function findBlock(model, blockId) {
        if (!model || !blockId) return null;
        if (!model.indexes || !model.indexes.blocks) buildIndexes(model);
        return findBlockByIndex(model, blockId);
    }

    // --- layout engine -----------------------------------------------------------
    const engineFactory = createParagraphLayoutEngineFactory({ findBlock });
    const engine = engineFactory(opts.measurementService || null, opts.layoutOptions || {});

    // --- atomic renderer ---------------------------------------------------------
    const applyObjectFocusPolicyToElement = createApplyObjectFocusPolicyToElement({});
    const rendererFactory = createAtomicRendererFactory({
        findBlock,
        applyObjectFocusPolicyToElement,
        renderSelectionOverlay: createRenderSelectionOverlay(doc),
        renderRevisionOverlay: createRenderRevisionOverlay(doc),
        renderCommentMarkers: createRenderCommentMarkers(doc),
        restoreLogicalSelection,
        doc,
    });
    // Model-owned engine: no native contenteditable anywhere — input flows through the
    // off-screen input surface (R.4.2), so header/footer regions are NOT editable DOM.
    const renderer = rendererFactory({ diagnostics: opts.diagnostics === true, doc, contentEditableRegions: false });

    // --- host state --------------------------------------------------------------
    let root = null;
    let model = null;
    let selection = null;
    let viewport = null;
    let lastLayout = null;
    let lastSnapshot = null;
    let reviewMode = 'markup'; // R.5.11 — 'markup' | 'final' | 'original'
    let pageSettings = opts.pageSettings || null; // R.5.23 — mutable (runtime page-settings changes)
    let zoom = 1;                                 // R.5.23 — view zoom factor
    // R.5.17 — first-paint budget: lay out only the first N blocks for an instant first paint,
    // then complete the full layout on idle. Opt-in (0 = off → always full layout).
    const firstPaintMaxBlocks = Number(opts.firstPaintMaxBlocks) > 0 ? Number(opts.firstPaintMaxBlocks) : 0;
    let firstPaintPending = firstPaintMaxBlocks > 0;
    let fullLayoutTimer = null;
    function scheduleFullLayout() {
        if (fullLayoutTimer != null) return;
        const schedule = (typeof requestIdleCallback === 'function')
            ? function (fn) { return requestIdleCallback(fn, { timeout: 200 }); }
            : function (fn) { return setTimeout(fn, 0); };
        fullLayoutTimer = schedule(function () {
            fullLayoutTimer = null;
            if (!firstPaintPending) return;
            firstPaintPending = false;
            layoutCache = null; // force a full recompute now that we have idle time
            if (root && model) render();
        });
    }
    // R.5.3 — debounced model-change notifier (drives C# autosave). Fires `opts.onChange`
    // once the model has been idle for `changeDebounceMs` after an edit (≈ autosave debounce),
    // not on scroll/viewport renders (gated on model.version).
    let onChangeTimer = null;
    let lastChangeVersion = 0;
    function scheduleChangeNotify() {
        if (typeof opts.onChange !== 'function' || !model) return;
        if (Number(model.version || 0) === lastChangeVersion) return; // no model change (scroll/viewport render)
        if (onChangeTimer) { try { clearTimeout(onChangeTimer); } catch (e) { /* */ } }
        const delay = Number(opts.changeDebounceMs) > 0 ? Number(opts.changeDebounceMs) : 800;
        onChangeTimer = setTimeout(function () {
            onChangeTimer = null;
            lastChangeVersion = Number((model && model.version) || 0);
            try { opts.onChange(); } catch (e) { /* circuit gone */ }
        }, delay);
    }
    // R.4.6i-2 — layout cache: the (post-bidi) document layout is reused across renders
    // while the model + layout inputs are unchanged, so scroll / viewport / selection
    // re-renders don't re-run the (expensive) full-document layout. Invalidated by every
    // edit (model.version bumps) and by setModel / undo-redo (model swap).
    let layoutCache = null;
    let layoutComputeCount = 0;
    // R.4.9.1 — per-render timing breakdown (profiling the per-keystroke cost).
    const __now = (typeof performance !== 'undefined' && performance.now) ? function () { return performance.now(); } : function () { return Date.now(); };
    let renderTimings = {};
    let lastEditDirty = null; // R.4.9.2 — { blockIds, removedBlockIds, insertedBlockId, structural }
    let lastIncrementalBail = null; // R.4.9.3 — why the incremental path fell back (diagnostics)
    // R.4.2 — logical caret { blockId, offset } the input surface edits at.
    let caret = null;
    let inputSurface = null;
    // R.4.3 — selection anchor (null = collapsed caret) + overlay DOM elements.
    let anchor = null;
    let caretView = null;
    let selectionEls = [];
    let pointerHandler = null;
    let contextMenuHandler = null; // R.5.23 — right-click → onContextMenu callback
    let spellChecker = null;       // R.5.23c — { isMisspelled(word), suggest(word) } | null
    // R.4.4 — active IME composition: { blockId, start, text } while composing, else null.
    // `text` is the current preview string occupying [start, start+text.length).
    let composition = null;
    // R.4.6d — floating-image overlay state: selected object id, painted figure els,
    // and the in-flight pointer drag (resize/move) descriptor.
    let selectedObjectId = null;
    let objectEls = [];
    let objectDrag = null;
    // R.4.6h-2 — find/replace state: { query, opts, matches, index } | null + painted els.
    let findState = null;
    let findEls = [];
    let spellEls = []; // R.5.23c — painted red wavy underlines for misspelled words
    let opLog = [];    // R.5.18/R.5.22 — journal of emitted text operations (op-log undo + collab)
    const opListeners = []; // R.5.22 — collab adapters subscribed to local ops
    let remoteCursors = []; // R.5.22 — [{ id, blockId, offset, color, label }] collaborator carets
    let remoteCursorEls = [];
    // R.4.6f — track changes: when on, inserts get an insertion mark + deletes mark text
    // as deletion (kept, struck through) rather than removing it.
    let trackChanges = false;
    let revisionSeq = 0;
    // R.4.6g — comments: metadata lives in model.comments; anchored via `comment` marks.
    let commentSeq = 0;
    // R.4.7 — accessibility: off-screen ARIA live region announcing caret context.
    let liveRegion = null;
    // R.4.6i — snapshot undo/redo (coalesces typing/delete/drag runs into single steps).
    const history = createUndoStack({ clone: clone, limit: 200 });

    function mount(rootElement) {
        root = rootElement || null;
        if (root) applyEditorAria(root, { label: opts.ariaLabel }); // R.4.7
        return host;
    }

    // R.4.7 — announce the caret's context (heading / paragraph text) to the live region.
    let announceGranularity = 'paragraph'; // R.5.20 — 'paragraph' | 'word' | 'character'
    function announceCaret() {
        if (liveRegion && caret && model) liveRegion.announce(describeCaretGranular(model, caret, { findBlock: findBlock }, announceGranularity));
    }
    function setAnnounceGranularity(mode) {
        announceGranularity = (mode === 'word' || mode === 'character') ? mode : 'paragraph';
        return announceGranularity;
    }

    function setModel(nextModel) {
        model = nextModel || null;
        if (model) {
            // Force index rebuild on next render.
            model.indexes = null;
        }
        lastChangeVersion = Number((model && model.version) || 0); // loading a doc is not a "change"
        firstPaintPending = firstPaintMaxBlocks > 0; // R.5.17 — re-enable the budget for the new doc's first paint
        history.clear(); // a new document starts with empty undo/redo
        layoutCache = null; // different document → drop the cached layout
        return host;
    }

    function setSelection(nextSelection) {
        selection = nextSelection || null;
        if (nextSelection && nextSelection.blockId) {
            caret = { blockId: nextSelection.blockId, offset: Number(nextSelection.offset || 0) || 0 };
            anchor = null; // collapse
            announceCaret(); // R.4.7
        }
        return host;
    }

    function getCaret() { return caret ? { blockId: caret.blockId, offset: caret.offset } : null; }
    function getSelectionRange() {
        if (!caret) return null;
        return { anchor: anchor ? { blockId: anchor.blockId, offset: anchor.offset } : { blockId: caret.blockId, offset: caret.offset }, focus: { blockId: caret.blockId, offset: caret.offset } };
    }

    function setViewport(nextViewport) {
        viewport = nextViewport || null;
        return host;
    }

    // Page virtualization: returns a Set of visible page indices (±overscan) given the
    // viewport, or null when no viewport is set (→ paint all pages).
    function visiblePageIndices(layout) {
        if (!viewport) return null;
        const pages = asArray(layout && layout.pages);
        if (!pages.length) return null;
        const top = Number(viewport.scrollTop || 0) || 0;
        const height = Number(viewport.height || 0) || 0;
        const bottom = top + height;
        const overscan = Number(viewport.overscanPages ?? 1) || 0;
        const visible = new Set();
        pages.forEach(function (page, index) {
            const rect = page.rect || {};
            const pTop = Number(rect.y || 0) || 0;
            const pBottom = pTop + (Number(rect.height || 0) || 0);
            if (pBottom >= top && pTop <= bottom) visible.add(index);
        });
        // Overscan: include neighbouring pages so scrolling reveals pre-painted content.
        const expanded = new Set(visible);
        visible.forEach(function (index) {
            for (let d = 1; d <= overscan; d++) {
                if (index - d >= 0) expanded.add(index - d);
                if (index + d < pages.length) expanded.add(index + d);
            }
        });
        return expanded;
    }

    // Builds the layout actually handed to the renderer. With virtualization on, blocks
    // on non-visible pages are dropped (page frames stay, preserving scroll height).
    function viewLayout(layout) {
        const visible = visiblePageIndices(layout);
        if (!visible) return layout;
        const blocks = asArray(layout.blocks).filter(function (block) {
            return visible.has(Number(block.pageIndex || 0) || 0);
        });
        return Object.assign({}, layout, { blocks, virtualized: true, visiblePageIndices: Array.from(visible).sort((a, b) => a - b) });
    }

    // Document layout signature — anything that changes the laid-out result. model.version
    // bumps on every edit; page/layout options are static per host but included for safety.
    function layoutSignature() {
        return [
            (model && (model.version != null ? model.version : 0)),
            pageSettings ? JSON.stringify(pageSettings) : '',
            opts.layoutOptions ? JSON.stringify(opts.layoutOptions) : '',
            reviewMode, // R.5.11 — review mode changes the laid-out content (final/original)
            (firstPaintPending && firstPaintMaxBlocks > 0) ? ('fp' + firstPaintMaxBlocks) : 'full', // R.5.17
        ].join('|');
    }
    // Returns the (post-bidi) full-document layout, reusing the cache when nothing that
    // affects layout changed. Only a cache miss runs engine.layoutDocument + the bidi pass.
    function computeLayout() {
        const sig = layoutSignature();
        if (layoutCache && layoutCache.signature === sig) { renderTimings.layoutCacheHit = true; return layoutCache.layout; }
        renderTimings.layoutCacheHit = false;
        const tA = __now();
        // R.5.11 — in a non-markup review mode, lay out a filtered CLONE (final = accepted view,
        // original = rejected view) without mutating the live model.
        const layoutModel = (reviewMode === 'markup' || !model) ? model : applyReviewMode(clone(model), reviewMode);
        const budget = (firstPaintPending && firstPaintMaxBlocks > 0) ? { maxBlocks: firstPaintMaxBlocks } : {};
        // R.5.23 fix — pageSettings must be spread FLAT (the engine reads width/height/marginTop…
        // directly, not a nested `pageSettings` key), so page geometry actually applies + can change.
        const layout = engine.layoutDocument(layoutModel, Object.assign(
            {}, opts.layoutOptions || {}, pageSettings || {}, budget));
        const tB = __now();
        // R.4.5 — reorder each line into visual (bidi) order before render + caret use.
        applyBidiToLayout(layout);
        const tC = __now();
        // R.4.8 (lists) — indent list items + attach hanging markers (post-layout, like bidi).
        applyListLayout(layout, model);
        const tD = __now();
        renderTimings.layoutDocumentMs = tB - tA;
        renderTimings.bidiMs = tC - tB;
        renderTimings.listMs = tD - tC;
        layoutComputeCount += 1;
        layoutCache = { signature: sig, layout: layout };
        return layout;
    }

    // R.4.9.4 — translate a block-layout's vertical geometry by `dy` (Y-reflow). Content is
    // unchanged; only positions move. (listMarker is block-relative, so it is NOT shifted.)
    function shiftBlockLayoutY(bl, dy) {
        if (!bl || !dy) return;
        if (bl.rect) bl.rect.y = (Number(bl.rect.y) || 0) + dy;
        asArray(bl.lines).forEach(function (line) {
            if (line && line.rect) line.rect.y = (Number(line.rect.y) || 0) + dy;
            if (line && typeof line.baseline === 'number') line.baseline += dy;
        });
        asArray(bl.segments).forEach(function (seg) { if (seg && seg.rect) seg.rect.y = (Number(seg.rect.y) || 0) + dy; });
        asArray(bl.caretStops).forEach(function (stop) { if (stop && stop.rect) stop.rect.y = (Number(stop.rect.y) || 0) + dy; });
        asArray(bl.baselines).forEach(function (b) { if (b && typeof b.y === 'number') b.y += dy; });
    }

    // R.4.9.10 — re-flow + re-paginate the blocks AFTER `startIndex` (whose dirty block height just
    // changed), reusing each cached block's line structure (content unchanged → only y/page move).
    // Replicates layoutDocument's greedy block-level pagination (page-stacked geometry from
    // lastLayout.pageMetrics). Returns the list of moved block ids, or false to fall back to a full
    // render (cases this can't reproduce: a block taller than a page would split; tables/multi-page).
    function repaginateFrom(startIndex, fresh) {
        const pageMetrics = lastLayout && lastLayout.pageMetrics;
        if (!pageMetrics) return false;
        const blocks = asArray(lastLayout.blocks);
        let pages = asArray(lastLayout.pages).slice();
        const bodyHeight = (pages[0] && pages[0].bodyFrame && Number(pages[0].bodyFrame.height)) || 0;
        if (!bodyHeight) return false;
        // Pre-check (NO mutation): every following block must be a simple single-page paragraph that
        // fits a page. A continuation fragment (fragmentIndex > 0 — NOTE 0 is the normal first/only
        // fragment) or a too-tall block means real layout could split it → fall back cleanly. This
        // guarantees the mutation pass below cannot bail half-way (no partial corruption).
        for (let k = startIndex + 1; k < blocks.length; k++) {
            const bl = blocks[k];
            if (!bl) continue;
            if (bl.type !== 'paragraph' || (Number(bl.fragmentIndex) || 0) > 0) return false;
            if ((Number(bl.rect && bl.rect.height) || 0) > bodyHeight + 0.5) return false;
        }
        const blockGap = Number(pageMetrics.blockGap) || 0;
        const ensurePage = function (idx) { while (pages.length <= idx) pages.push(createPageLayout(pages.length, pageMetrics)); return pages[idx]; };
        const bodyBottom = function (p) { return (Number(p.bodyFrame.y) || 0) + (Number(p.bodyFrame.height) || 0); };
        let pageIndex = Number(fresh.pageIndex) || 0;
        let currentY = (Number(fresh.rect.y) || 0) + (Number(fresh.rect.height) || 0) + blockGap;
        const touched = [];
        for (let k = startIndex + 1; k < blocks.length; k++) {
            const bl = blocks[k];
            if (!bl) continue;
            const h = Number(bl.rect && bl.rect.height) || 0;
            let page = ensurePage(pageIndex);
            if (currentY + h > bodyBottom(page) + 0.5) { pageIndex++; page = ensurePage(pageIndex); currentY = Number(page.bodyFrame.y) || 0; }
            const newY = currentY;
            const oldY = Number(bl.rect && bl.rect.y) || 0;
            const oldPage = Number(bl.pageIndex) || 0;
            if (Math.abs(newY - oldY) > 0.5 || oldPage !== pageIndex) {
                shiftBlockLayoutY(bl, newY - oldY);
                bl.pageIndex = pageIndex;
                touched.push(bl.blockId);
            }
            currentY = newY + h + blockGap;
        }
        // Trim trailing pages that no longer hold any block (a shrink can free whole pages).
        if (pages.length > pageIndex + 1) pages = pages.slice(0, pageIndex + 1);
        // Rebuild page → block membership + totalPages.
        pages.forEach(function (p) { p.blockIds = []; p.totalPages = pages.length; });
        blocks.forEach(function (bl) { const p = pages[Number(bl.pageIndex) || 0]; if (p && p.blockIds.indexOf(bl.blockId) < 0) p.blockIds.push(bl.blockId); });
        lastLayout.pages = pages;
        return touched;
    }

    // R.4.9.3 — re-layout ONE paragraph block in place (engine.layoutParagraph), reusing the
    // cached layout for every other block. Returns true if it patched `lastLayout` incrementally,
    // false if the caller must fall back to a full layout. v1 covers the dominant case: a
    // non-structural edit whose block height is UNCHANGED (typing a char that doesn't wrap a line)
    // → nothing else moves. Height change / structural / objects / multi-page block → fall back.
    function relayoutDirtyBlock(blockId) {
        const bail = function (reason) { lastIncrementalBail = reason; return false; };
        if (!lastLayout || !model) return bail('no-layout');
        const blocks = asArray(lastLayout.blocks);
        let i = -1;
        for (let k = 0; k < blocks.length; k++) { if (blocks[k] && blocks[k].blockId === blockId) { i = k; break; } }
        if (i < 0) return bail('block-not-in-layout');
        const cached = blocks[i];
        if (!cached || cached.type !== 'paragraph' || !cached.rect) return bail('cached-not-paragraph:' + (cached && cached.type));
        // Multiple layout entries for one block id = the block is split across pages → full render.
        let fragCount = 0;
        for (let k = 0; k < blocks.length; k++) { if (blocks[k] && blocks[k].blockId === blockId) fragCount++; }
        if (fragCount > 1) return bail('multi-page-block:' + fragCount);
        // A block that owns / anchors a floating object affects surrounding layout → full render.
        if (asArray(lastLayout.objects).some(function (o) { return o && (o.anchorBlockId === blockId || o.blockId === blockId); })) return bail('has-object');
        const block = findBlock(model, blockId);
        if (!block || block.type !== 'paragraph') return bail('model-block-not-paragraph:' + (block && block.type));
        let fresh;
        try { fresh = engine.layoutParagraph(block, { x: cached.rect.x, y: cached.rect.y, width: cached.rect.width }); }
        catch (e) { return bail('layoutParagraph-threw:' + (e && e.message)); }
        if (!fresh || fresh.type !== 'paragraph' || !fresh.rect) return bail('fresh-invalid:' + (fresh && fresh.type));
        // bidi + list for just the re-laid-out block (scoped mini-layout).
        applyBidiToLayout({ blocks: [fresh] });
        applyListLayout({ blocks: [fresh] }, model);
        const delta = (Number(fresh.rect.height) || 0) - (Number(cached.rect.height) || 0);
        fresh.pageIndex = cached.pageIndex;
        blocks[i] = fresh;
        const touched = [blockId];

        // R.4.9.4 / R.4.9.10 — the block's height changed (a line wrapped / unwrapped): re-flow +
        // re-paginate the following blocks from the dirty block downward, reusing their cached line
        // structure (content unchanged → only y/page move). Same-page shifts AND cross-page
        // repagination are handled. Falls back to a full render for cases this can't reproduce
        // (block taller than a page → split, tables). The golden tests (R70 same-page, R71
        // cross-page) guard that this matches a full layout exactly.
        if (Math.abs(delta) > 0.5) {
            const moved = repaginateFrom(i, fresh);
            if (moved === false) { blocks[i] = cached; return bail('repaginate-failed'); }
            moved.forEach(function (id) { touched.push(id); });
        }

        // rebuild the global caret-stop aggregate (cheap O(N) concat).
        lastLayout.caretStops = blocks.reduce(function (acc, b) { return acc.concat(asArray(b && b.caretStops)); }, []);
        // the incrementally-patched layout is identical to a full layout for this version → keep it cached.
        layoutCache = { signature: layoutSignature(), layout: lastLayout };
        return touched;
    }

    // R.4.9.6 — incremental render path: patch the dirty block's layout + re-render. The atomic
    // renderer already diffs per block/segment (B1/B2), so only the dirty block's DOM updates.
    function renderIncremental(blockId) {
        if (!root || !model || !lastLayout) return false;
        const touched = relayoutDirtyBlock(blockId);
        if (!touched) return false;
        const t0 = __now();
        renderTimings = { incremental: true };
        const ra = __now();
        const rendered = viewLayout(lastLayout);
        const rb = __now();
        const snapshot = createRenderSnapshot(model, rendered, selection, { cheap: true, dirtyBlockId: blockId });
        lastSnapshot = snapshot;
        const rc = __now();
        // R.4.9.3b-2 — patch only the dirty block's DOM in place; fall back to a full render if it
        // isn't already painted (virtualized / first paint).
        let renderResult = (typeof renderer.patchBlocks === 'function') ? renderer.patchBlocks(root, snapshot, touched) : { ok: false };
        if (!renderResult || renderResult.ok === false) {
            renderTimings.patchFallback = true;
            renderResult = renderer.render(root, snapshot);
        }
        const rd = __now();
        paintObjects();
        paintOverlays();
        const re = __now();
        renderTimings.viewLayoutMs = rb - ra;
        renderTimings.snapshotMs = rc - rb;
        renderTimings.rendererMs = rd - rc;
        renderTimings.overlaysMs = re - rd;
        renderTimings.totalMs = re - t0;
        scheduleChangeNotify();
        return renderResult.ok !== false;
    }

    function render() {
        if (!root) return { ok: false, error: 'render-host: not mounted' };
        if (!model) return { ok: false, error: 'render-host: no model' };
        const t0 = __now();
        renderTimings = {};
        if (!model.indexes || !model.indexes.blocks) buildIndexes(model);
        const layout = computeLayout();
        const t1 = __now();
        lastLayout = layout;
        const rendered = viewLayout(layout);
        const t2 = __now();
        const snapshot = createRenderSnapshot(model, rendered, selection);
        lastSnapshot = snapshot;
        const t3 = __now();
        const renderResult = renderer.render(root, snapshot);
        const t4 = __now();
        // render() rebuilds page sections (replaceChildren), so the app-drawn overlays
        // (floating images, caret, selection) are wiped — repaint them on the fresh DOM.
        paintObjects();
        paintOverlays();
        const t5 = __now();
        renderTimings.computeLayoutMs = t1 - t0;
        renderTimings.viewLayoutMs = t2 - t1;
        renderTimings.snapshotMs = t3 - t2;
        renderTimings.rendererMs = t4 - t3;
        renderTimings.overlaysMs = t5 - t4;
        renderTimings.totalMs = t5 - t0;
        scheduleChangeNotify();
        // R.5.17 — a budgeted first paint left blocks unlaid; finish the full layout on idle.
        if (firstPaintPending) { if (layout && layout.complete === false) scheduleFullLayout(); else firstPaintPending = false; }
        return { ok: renderResult.ok !== false, layout, snapshot, renderResult };
    }

    // --- R.4.3 caret + selection overlay painting --------------------------------
    function pageRect(pageIndex) {
        const page = asArray(lastLayout && lastLayout.pages)[pageIndex];
        return (page && page.rect) || { x: 0, y: 0 };
    }
    function pageSectionFor(pageIndex) {
        if (!root || typeof root.querySelector !== 'function') return null;
        return root.querySelector('.tm-render-page[data-render-page-index="' + pageIndex + '"]')
            || root.querySelector('[data-render-page-index="' + pageIndex + '"]');
    }
    // Document coords → page-local coords for the page that owns the caret stop.
    function toPageLocal(docRect, pageIndex) {
        const pr = pageRect(pageIndex);
        return {
            x: (Number(docRect.x || 0) || 0) - (Number(pr.x || 0) || 0),
            y: (Number(docRect.y || 0) || 0) - (Number(pr.y || 0) || 0),
            width: docRect.width,
            height: docRect.height,
        };
    }
    function clearSelectionEls() {
        selectionEls.forEach(function (el) { if (el.parentNode) el.parentNode.removeChild(el); });
        selectionEls = [];
    }
    function clearSpellEls() {
        spellEls.forEach(function (el) { if (el.parentNode) el.parentNode.removeChild(el); });
        spellEls = [];
    }
    // R.5.23c — paint red wavy underlines under misspelled words in the laid-out paragraphs.
    function paintMisspellings() {
        clearSpellEls();
        if (!spellChecker || !lastLayout) return;
        const wordRe = /[\p{L}\p{M}'’]+/gu;
        const visit = function (block) {
            if (!block || block.type !== 'paragraph') return;
            const text = blockText(block);
            if (!text) return;
            let m;
            wordRe.lastIndex = 0;
            while ((m = wordRe.exec(text))) {
                if (!spellChecker.isMisspelled(m[0])) continue;
                const a = { blockId: block.id, offset: m.index };
                const f = { blockId: block.id, offset: m.index + m[0].length };
                selectionRectsForRange(lastLayout, a, f).forEach(function (entry) {
                    const section = pageSectionFor(entry.pageIndex);
                    if (!section) return;
                    const el = createSpellUnderlineElement({ doc, rect: toPageLocal(entry.rect, entry.pageIndex) });
                    section.appendChild(el);
                    spellEls.push(el);
                });
            }
        };
        asArray(model && model.body && model.body.blocks).forEach(function walk(block) {
            if (!block) return;
            if (block.type === 'paragraph') visit(block);
            else if (block.type === 'table') {
                asArray(block.content && block.content.rows).forEach(function (row) {
                    asArray(row.cells).forEach(function (cell) { asArray(cell.blocks).forEach(walk); });
                });
            }
        });
    }
    function clearFindEls() {
        findEls.forEach(function (el) { if (el.parentNode) el.parentNode.removeChild(el); });
        findEls = [];
    }
    function clearRemoteCursorEls() {
        remoteCursorEls.forEach(function (el) { if (el.parentNode) el.parentNode.removeChild(el); });
        remoteCursorEls = [];
    }
    // R.5.22 — paint each collaborator's caret (colored bar + name flag) at its model position.
    function paintRemoteCursors() {
        clearRemoteCursorEls();
        if (!lastLayout || !remoteCursors.length) return;
        remoteCursors.forEach(function (rc) {
            if (!rc || !rc.blockId) return;
            const stop = caretStopAt(lastLayout, { blockId: rc.blockId, offset: Number(rc.offset) || 0 });
            if (!stop || !stop.rect) return;
            const section = pageSectionFor(Number(stop.pageIndex || 0) || 0);
            if (!section) return;
            const el = createRemoteCaretElement({ doc, rect: toPageLocal(stop.rect, Number(stop.pageIndex || 0) || 0), color: rc.color, label: rc.label, id: rc.id });
            section.appendChild(el);
            remoteCursorEls.push(el);
        });
    }
    // R.5.22 — set the collaborators present in the document (presence + remote cursors).
    function setRemoteCursors(cursors) {
        remoteCursors = asArray(cursors).filter(function (c) { return c && c.blockId; });
        paintOverlays();
        return remoteCursors.length;
    }

    function paintOverlays() {
        if (!root || !lastLayout) return;
        clearSelectionEls();
        clearFindEls();
        paintMisspellings(); // R.5.23c — red wavy underlines for misspelled words
        paintRemoteCursors(); // R.5.22 — collaborator carets
        // R.4.6h-2 — find/replace match highlights (under the selection/caret).
        if (findState && asArray(findState.matches).length) {
            findState.matches.forEach(function (match, i) {
                const a = { blockId: match.blockId, offset: match.start };
                const f = { blockId: match.blockId, offset: match.end };
                selectionRectsForRange(lastLayout, a, f).forEach(function (entry) {
                    const section = pageSectionFor(entry.pageIndex);
                    if (!section) return;
                    const el = createFindHighlightElement({ doc, rect: toPageLocal(entry.rect, entry.pageIndex), current: i === findState.index });
                    section.appendChild(el);
                    findEls.push(el);
                });
            });
        }
        // Selection rectangles (under the caret).
        const range = getSelectionRange();
        if (range && anchor) {
            selectionRectsForRange(lastLayout, range.anchor, range.focus).forEach(function (entry) {
                const section = pageSectionFor(entry.pageIndex);
                if (!section) return;
                const local = toPageLocal(entry.rect, entry.pageIndex);
                const el = createSelectionRectElement({ doc, rect: local });
                section.appendChild(el);
                selectionEls.push(el);
            });
        }
        // R.5.9 — table cell-range selection highlight.
        if (cellSelection && cellSelection.ids && cellSelection.ids.length) {
            allTableCellLayouts().forEach(function (cl) {
                if (cellSelection.ids.indexOf(cl.cellId) < 0 || !cl.rect) return;
                const pageIndex = Number(cl.pageIndex || 0) || 0;
                const section = pageSectionFor(pageIndex);
                if (!section) return;
                const el = createSelectionRectElement({ doc, rect: toPageLocal(cl.rect, pageIndex) });
                el.setAttribute('data-cell-selection', cl.cellId);
                section.appendChild(el);
                selectionEls.push(el);
            });
        }
        // R.4.4 — IME composition (pre-edit) underline under the in-progress text.
        if (composition && composition.text) {
            const a = { blockId: composition.blockId, offset: composition.start };
            const f = { blockId: composition.blockId, offset: composition.start + composition.text.length };
            selectionRectsForRange(lastLayout, a, f).forEach(function (entry) {
                const section = pageSectionFor(entry.pageIndex);
                if (!section) return;
                const local = toPageLocal(entry.rect, entry.pageIndex);
                const el = createCompositionUnderlineElement({ doc, rect: local });
                section.appendChild(el);
                selectionEls.push(el); // transient — cleared on next paint with the rest
            });
        }
        // Blinking caret at the focus position.
        const stop = caret ? caretStopAt(lastLayout, caret) : null;
        if (stop && stop.rect) {
            if (!caretView) caretView = createCaretElement({ doc });
            const section = pageSectionFor(Number(stop.pageIndex || 0) || 0);
            if (section) {
                caretView.place(toPageLocal(stop.rect, Number(stop.pageIndex || 0) || 0));
                section.appendChild(caretView.element);
            } else {
                caretView.hide();
            }
        } else if (caretView) {
            caretView.hide();
        }
    }

    // --- R.4.6d floating images ---------------------------------------------------
    function clearObjectEls() {
        objectEls.forEach(function (el) { if (el.parentNode) el.parentNode.removeChild(el); });
        objectEls = [];
    }
    // Paints every floating object from layout.objects as a positioned figure (page-local)
    // with a real <img> + (when selected) resize handles.
    function paintObjects() {
        if (!root || !lastLayout) return;
        clearObjectEls();
        asArray(lastLayout.objects).forEach(function (object) {
            const pageIndex = Number(object.pageIndex || 0) || 0;
            const section = pageSectionFor(pageIndex);
            if (!section) return;
            // The layout object is geometry-only; resolve the image src/alt from the model
            // drawing run so the overlay paints a real <img>.
            const src = findDrawingRunByObjectId(object.objectId || object.id);
            const run = src && src.run;
            const runLayout = (run && (run.layout || run.Layout)) || {};
            const paintObject = run
                ? Object.assign({}, object, {
                    url: object.url || run.url || run.src || runLayout.url || '',
                    altText: object.altText || runLayout.altText || run.altText || '',
                    caption: object.caption || runLayout.caption || '',
                    zIndex: (object.zIndex != null && object.zIndex !== 0) ? object.zIndex : (Number(runLayout.zIndex || 0) || 0),
                })
                : object;
            const el = createObjectElement({
                doc,
                object: paintObject,
                rect: toPageLocal(paintObject.rect || {}, pageIndex),
                selected: (paintObject.objectId || paintObject.id) === selectedObjectId,
            });
            section.appendChild(el);
            objectEls.push(el);
        });
    }

    function objectLayoutById(objectId) {
        return asArray(lastLayout && lastLayout.objects).find(function (o) {
            return (o.objectId || o.id) === objectId;
        }) || null;
    }
    // Finds the drawing run carrying `objectId` and its owning block in the model.
    function findDrawingRunByObjectId(objectId) {
        let found = null;
        asArray(model && model.body && model.body.blocks).forEach(function (block) {
            if (found || !block.content) return;
            asArray(block.content.runs).forEach(function (run) {
                if (found) return;
                if (run && run.kind === 'drawing' && (run.objectId === objectId || run.id === objectId)) {
                    found = { block: block, run: run };
                }
            });
        });
        return found;
    }

    // Inserts an image at the caret. `wrapMode: 'inline'` (default) flows in the text;
    // a floating mode ('square'/'tight'/'topAndBottom'/'through') creates a text
    // exclusion so body text wraps around it.
    function insertImage(opts) {
        if (!model || !caret) return null;
        const o = opts || {};
        const block = findBlock(model, caret.blockId);
        if (!block || block.type !== 'paragraph') return null;
        const objectId = o.objectId || stableId('obj', (block.id || 'b') + '-' + Date.now());
        const wrapMode = o.wrapMode || 'inline';
        const drawingRun = {
            kind: 'drawing',
            id: o.runId || (objectId + '-run'),
            objectId: objectId,
            url: o.url || o.src || '',
            layout: {
                wrapMode: wrapMode,
                width: Math.max(1, Number(o.width || 120) || 120),
                height: Math.max(1, Number(o.height || 90) || 90),
                altText: o.alt || o.altText || '',
                horizontalPosition: o.horizontalPosition || { align: 'Left', offset: 0, relativeTo: 'Page' },
                verticalPosition: o.verticalPosition || { align: 'Top', offset: 0, relativeTo: 'Page' },
            },
        };
        recordHistory(null);
        const res = insertDrawingRunAtTextOffset(block, caret.offset, drawingRun, {});
        model.version = Number(model.version || 0) + 1;
        model.indexes = null;
        selectedObjectId = objectId;
        render();
        return { objectId: objectId, runId: (res && res.runId) || drawingRun.id };
    }

    function selectObject(objectId) {
        selectedObjectId = objectId || null;
        paintObjects(); // repaint so handles appear/disappear without a full re-layout
        notifyObjectSelection();
        return host;
    }
    function clearObjectSelection() { if (selectedObjectId) { selectedObjectId = null; paintObjects(); notifyObjectSelection(); } }

    // R.4.8 — a snapshot of the selected image object (for the host inspector). null = none.
    function selectedObjectInfo() {
        if (!selectedObjectId) return null;
        const hit = findDrawingRunByObjectId(selectedObjectId);
        if (!hit) return null;
        const l = (hit.run.layout || hit.run.Layout || {});
        return {
            objectId: selectedObjectId,
            url: hit.run.url || hit.run.src || l.url || '',
            wrapMode: l.wrapMode || l.WrapMode || 'inline',
            altText: l.altText || l.AltText || '',
            caption: l.caption || l.Caption || '',
            width: Math.max(1, Number(l.width || l.Width || 0) || 0),
            height: Math.max(1, Number(l.height || l.Height || 0) || 0),
            x: Number((l.horizontalPosition && l.horizontalPosition.offset) || 0) || 0,
            y: Number((l.verticalPosition && l.verticalPosition.offset) || 0) || 0,
        };
    }
    // Pushes the current object selection to the host (JS→.NET) so the inspector can react.
    function notifyObjectSelection() {
        if (typeof opts.onObjectSelect === 'function') {
            try { opts.onObjectSelect(selectedObjectInfo()); } catch (e) { /* host detached */ }
        }
    }
    // Inspector edits on the selected object (alt text / wrap mode); size = resizeSelectedObject.
    function setSelectedObjectAltText(text) {
        const hit = selectedObjectId && findDrawingRunByObjectId(selectedObjectId);
        if (!hit) return false;
        recordHistory(null);
        if (!hit.run.layout) hit.run.layout = {};
        hit.run.layout.altText = String(text == null ? '' : text);
        model.version = Number(model.version || 0) + 1;
        render();
        notifyObjectSelection();
        return true;
    }
    function setSelectedObjectWrapMode(mode) {
        const hit = selectedObjectId && findDrawingRunByObjectId(selectedObjectId);
        if (!hit) return false;
        recordHistory(null);
        if (!hit.run.layout) hit.run.layout = {};
        hit.run.layout.wrapMode = String(mode || 'inline');
        model.version = Number(model.version || 0) + 1;
        model.indexes = null;
        render();
        notifyObjectSelection();
        return true;
    }
    // R.4.8 inspector — horizontal alignment of a floating object (Left/Center/Right).
    function setSelectedObjectAlignment(align) {
        const hit = selectedObjectId && findDrawingRunByObjectId(selectedObjectId);
        if (!hit) return false;
        recordHistory(null);
        const layout = hit.run.layout || (hit.run.layout = {});
        const hp = layout.horizontalPosition || (layout.horizontalPosition = { align: 'Left', offset: 0, relativeTo: 'Page' });
        hp.align = String(align || 'Left');
        hp.offset = 0; // an explicit alignment overrides any drag offset
        model.version = Number(model.version || 0) + 1;
        model.indexes = null;
        render();
        notifyObjectSelection();
        return true;
    }
    // R.4.8 inspector — caption text (rendered as a <figcaption> below the image).
    function setSelectedObjectCaption(text) {
        const hit = selectedObjectId && findDrawingRunByObjectId(selectedObjectId);
        if (!hit) return false;
        recordHistory(null);
        if (!hit.run.layout) hit.run.layout = {};
        hit.run.layout.caption = String(text == null ? '' : text);
        model.version = Number(model.version || 0) + 1;
        render();
        notifyObjectSelection();
        return true;
    }
    // R.4.8 inspector — absolute position (x/y offset) of a floating object.
    function setSelectedObjectPosition(x, y) {
        const hit = selectedObjectId && findDrawingRunByObjectId(selectedObjectId);
        if (!hit) return false;
        recordHistory(null);
        const layout = hit.run.layout || (hit.run.layout = {});
        const hp = layout.horizontalPosition || (layout.horizontalPosition = { align: 'Left', offset: 0, relativeTo: 'Page' });
        const vp = layout.verticalPosition || (layout.verticalPosition = { align: 'Top', offset: 0, relativeTo: 'Page' });
        hp.offset = Number(x) || 0; hp.align = null; // an explicit offset overrides alignment
        vp.offset = Number(y) || 0; vp.align = null;
        model.version = Number(model.version || 0) + 1;
        model.indexes = null;
        render();
        notifyObjectSelection();
        return true;
    }
    // R.4.8 inspector — z-order (bring forward / send backward) via layout.zIndex.
    function nudgeSelectedObjectZ(delta) {
        const hit = selectedObjectId && findDrawingRunByObjectId(selectedObjectId);
        if (!hit) return false;
        recordHistory(null);
        const layout = hit.run.layout || (hit.run.layout = {});
        const cur = Number(layout.zIndex || 0) || 5; // 5 = the renderer's default object z base
        layout.zIndex = Math.max(0, cur + (delta > 0 ? 1 : -1));
        model.version = Number(model.version || 0) + 1;
        render();
        notifyObjectSelection();
        return true;
    }

    function resizeSelectedObject(width, height) {
        if (!selectedObjectId) return false;
        const hit = findDrawingRunByObjectId(selectedObjectId);
        if (!hit) return false;
        recordHistory('resize');
        if (!hit.run.layout) hit.run.layout = {};
        if (width != null) hit.run.layout.width = Math.max(1, Number(width) || 1);
        if (height != null) hit.run.layout.height = Math.max(1, Number(height) || 1);
        model.version = Number(model.version || 0) + 1;
        render();
        if (!objectDrag) notifyObjectSelection(); // panel-driven resize → refresh inspector (skip per-move drag spam)
        return true;
    }

    // Nudges a floating object by (dx,dy) layout px via its position offsets.
    function moveSelectedObject(dx, dy) {
        if (!selectedObjectId) return false;
        const hit = findDrawingRunByObjectId(selectedObjectId);
        if (!hit) return false;
        recordHistory('move');
        const layout = hit.run.layout || (hit.run.layout = {});
        const hp = layout.horizontalPosition || (layout.horizontalPosition = { align: 'Left', offset: 0, relativeTo: 'Page' });
        const vp = layout.verticalPosition || (layout.verticalPosition = { align: 'Top', offset: 0, relativeTo: 'Page' });
        hp.offset = Number(hp.offset || 0) + Number(dx || 0);
        vp.offset = Number(vp.offset || 0) + Number(dy || 0);
        // An explicit offset overrides alignment.
        if (dx) hp.align = null;
        if (dy) vp.align = null;
        model.version = Number(model.version || 0) + 1;
        render();
        return true;
    }

    // --- R.4.2 editing -----------------------------------------------------------
    const editDeps = { findBlock, findBlockContainer };

    // Applies an edit-model result: advances the caret, bumps the model version,
    // invalidates indexes after structural changes, and re-renders.
    function commitEdit(result) {
        if (!result || result.ok === false) return false;
        if (result.caret) {
            caret = { blockId: result.caret.blockId, offset: result.caret.offset };
            anchor = null; // edits collapse the selection
            selection = { region: 'Body', blockId: caret.blockId, offset: caret.offset, isCollapsed: true };
        }
        model.version = Number(model.version || 0) + 1;
        if (result.structural) model.indexes = null;
        // R.4.9.2 — record which blocks this edit touched (for the incremental render path).
        // A non-structural edit (typing / in-block delete) dirties exactly one block → the
        // common fast path; structural edits (split/merge) fall back to full layout for now.
        lastEditDirty = {
            blockIds: asArray(result.dirtyBlockIds).slice(),
            removedBlockIds: asArray(result.removedBlockIds).slice(),
            insertedBlockId: result.insertedBlockId || null,
            structural: result.structural === true,
        };
        // R.4.9.6 — fast path: a non-structural edit touching exactly one paragraph → re-layout
        // just that block + re-render (O(1)); anything else falls back to a full render.
        const dirty = asArray(result.dirtyBlockIds);
        if (result.structural || dirty.length !== 1 || !renderIncremental(dirty[0])) {
            render();
        }
        announceCaret();
        return true;
    }

    // R.4.3 — caret navigation (no model change → repaint overlays only, no re-layout).
    function moveCaret(key, shiftKey) {
        if (!caret || !lastLayout) return;
        clearPendingMarks(); // R.5.8 — moving the caret discards a pending format
        if (shiftKey) { if (!anchor) anchor = { blockId: caret.blockId, offset: caret.offset }; }
        else { anchor = null; }
        history.breakCoalescing(); // a caret move ends a typing/delete run → next edit is a new undo step
        // Pass the block's text so ArrowLeft/Right step by grapheme cluster (R.4.5); pageLines so
        // PageUp/PageDown jump a viewport's worth of lines (R.5.7).
        const block = model ? findBlock(model, caret.blockId) : null;
        const moveOpts = { text: block ? blockText(block) : null, pageLines: viewportPageLines() };
        caret = moveCaretByKey(lastLayout, caret, key, moveOpts);
        selection = { region: 'Body', blockId: caret.blockId, offset: caret.offset, isCollapsed: !anchor };
        paintOverlays();
        announceCaret();
        if (key === 'PageUp' || key === 'PageDown') scrollCaretIntoView(); // R.5.7
    }

    // R.5.7 — how many text lines a PageUp/PageDown should jump (≈ a viewport height).
    function viewportPageLines() {
        const vh = (viewport && Number(viewport.height)) || (root && root.clientHeight) || 0;
        const lineH = approxLineHeight();
        if (vh > 0 && lineH > 0) return Math.max(1, Math.floor(vh / lineH) - 1);
        return 12;
    }
    function approxLineHeight() {
        const page = asArray(lastLayout && lastLayout.pages)[0];
        const lines = asArray(page && page.lines);
        for (let i = 0; i < lines.length; i++) { const h = Number(lines[i].rect && lines[i].rect.height); if (h > 0) return h; }
        return 18;
    }
    function scrollCaretIntoView() {
        try { if (caretView && caretView.element && typeof caretView.element.scrollIntoView === 'function') caretView.element.scrollIntoView({ block: 'nearest' }); } catch (e) { /* no scroll in Node */ }
    }

    // --- R.4.4 IME composition ---------------------------------------------------
    // The composing string lives in the model as ordinary preview text occupying
    // [start, start+text.length) so it flows through the real layout (wrapping, caret
    // geometry, line height come for free) and is underlined by paintOverlays(). Each
    // update replaces the previous preview span; `end` swaps it for the final string as
    // one committed edit. No history/undo entry yet — the new surface routes via direct
    // model mutation (history integration is a later milestone).
    function compositionStart() {
        if (!caret) return;
        recordHistory(null); // one undo step for the whole composition (pre-composition state)
        anchor = null; // a composition always begins from a collapsed caret
        composition = { blockId: caret.blockId, start: caret.offset, text: '' };
    }
    function applyComposingText(str) {
        // Apple/quirk: an update (or end) can arrive before `compositionstart` — start
        // lazily at the caret so the first preview is not dropped.
        if (!composition) {
            if (!caret) return null;
            composition = { blockId: caret.blockId, start: caret.offset, text: '' };
        }
        const res = applyReplaceRange(
            model, composition.blockId, composition.start, composition.start + composition.text.length, str, editDeps);
        if (res && res.ok) composition.text = str;
        return res;
    }
    function compositionUpdate(data) {
        const str = String(data == null ? '' : data);
        const res = applyComposingText(str);
        if (res && res.ok) commitEdit(res); // re-layout; paintOverlays draws the underline
    }
    function compositionEnd(data) {
        const str = String(data == null ? '' : data);
        if (!composition) {
            // No active composition (quirk) → treat as a plain insert.
            if (caret && str) commitEdit(applyInsertText(model, caret, str, editDeps));
            return;
        }
        const start = composition.start;
        const prevLen = composition.text.length;
        const blockId = composition.blockId;
        composition = null; // clear BEFORE render so the underline is not painted
        const res = applyReplaceRange(model, blockId, start, start + prevLen, str, editDeps);
        if (res && res.ok) commitEdit(res);
    }

    // --- R.4.6i undo/redo --------------------------------------------------------
    function currentState() {
        return { model: model, caret: caret, anchor: anchor, selection: selection, selectedObjectId: selectedObjectId };
    }
    // Snapshot the PRE-edit state. Call before a mutation. `coalesceKey` collapses a run
    // of same-kind edits (typing/delete/resize/move) into one undo step.
    function recordHistory(coalesceKey) {
        if (model) history.record(currentState(), coalesceKey);
    }
    function restoreState(snap) {
        if (!snap || !snap.model) return;
        layoutCache = null; // model object swapped → cached layout no longer applies
        model = snap.model;
        model.indexes = null; // rebuilt on render
        caret = snap.caret ? { blockId: snap.caret.blockId, offset: snap.caret.offset } : null;
        anchor = snap.anchor ? { blockId: snap.anchor.blockId, offset: snap.anchor.offset } : null;
        selection = snap.selection || null;
        selectedObjectId = snap.selectedObjectId || null;
        render();
    }
    // R.5.18 — true when there's no active selection (op-log undo only applies to single-caret edits).
    function caretSelectionCollapsed() {
        return !anchor || (caret && anchor.blockId === caret.blockId && anchor.offset === caret.offset);
    }
    // R.5.18 — lightweight caret/selection state for an operation-log entry (no model clone).
    function snapState() {
        return {
            caret: caret ? { blockId: caret.blockId, offset: caret.offset } : null,
            anchor: anchor ? { blockId: anchor.blockId, offset: anchor.offset } : null,
            selection: selection ? Object.assign({}, selection) : null,
        };
    }
    function restoreCaretState(s) {
        if (!s) return;
        caret = s.caret ? { blockId: s.caret.blockId, offset: s.caret.offset } : null;
        anchor = s.anchor ? { blockId: s.anchor.blockId, offset: s.anchor.offset } : null;
        selection = s.selection || (caret ? { region: 'Body', blockId: caret.blockId, offset: caret.offset, isCollapsed: !anchor } : null);
    }
    // R.5.18 — apply a single text op (insert/delete) to the model during op-log undo/redo.
    function applyOpToModel(op) {
        if (!op || !op.blockId) return;
        if (op.type === 'insert') {
            applyReplaceRange(model, op.blockId, Number(op.offset) || 0, Number(op.offset) || 0, String(op.text == null ? '' : op.text), editDeps);
        } else if (op.type === 'delete') {
            const start = Number(op.offset) || 0;
            applyReplaceRange(model, op.blockId, start, start + String(op.text == null ? '' : op.text).length, '', editDeps);
        }
    }
    // R.5.18 — record an operation-log undo step for a plain text edit (op already applied).
    function recordOpEdit(op, inverse, key, before, after) {
        if (!op || !inverse) return;
        history.recordOps({ undo: [inverse], redo: [op], before: before, after: after }, key);
    }
    function undo() {
        const entry = history.undo(currentState());
        if (!entry) return false;
        if (entry.kind === 'ops') {
            entry.undo.forEach(applyOpToModel);
            model.version = Number(model.version || 0) + 1; model.indexes = null;
            restoreCaretState(entry.before);
            render();
        } else {
            restoreState(entry);
        }
        return true;
    }
    function redo() {
        const entry = history.redo(currentState());
        if (!entry) return false;
        if (entry.kind === 'ops') {
            entry.redo.forEach(applyOpToModel);
            model.version = Number(model.version || 0) + 1; model.indexes = null;
            restoreCaretState(entry.after);
            render();
        } else {
            restoreState(entry);
        }
        return true;
    }

    // --- R.4.6f track changes ----------------------------------------------------
    let trackRevId = null;
    function setTrackChanges(on) {
        trackChanges = !!on;
        if (trackChanges && !trackRevId) trackRevId = 'rev-' + (++revisionSeq);
        if (!trackChanges) trackRevId = null;
        return host;
    }
    function trackedInsertAttrs() {
        return trackChanges ? { marks: [{ type: INSERTION_MARK, value: trackRevId }] } : null;
    }

    // R.5.8 — pending format. `pendingMarks` (type → mark) holds the inline marks the next typed
    // character should carry. Seeded from the marks already active at the collapsed caret, then
    // toggled, so the toolbar reflects reality and a toggle-then-type round-trips correctly.
    let pendingMarks = null;
    const PENDING_BOOLEAN_MARKS = ['bold', 'italic', 'underline', 'strikethrough'];
    function caretActiveBooleanMarks() {
        const block = caret && findBlock(model, caret.blockId);
        if (!block) return {};
        const fmt = formattingStateForBlockRange(block, caret.offset, caret.offset);
        const out = {};
        PENDING_BOOLEAN_MARKS.forEach(function (k) { if (fmt[k]) out[k] = { type: k }; });
        return out;
    }
    function togglePendingMark(type, opts) {
        if (!caret) return false;
        if (!pendingMarks) pendingMarks = caretActiveBooleanMarks();
        if (opts.mode === 'remove') { delete pendingMarks[type]; }
        else if (opts.value != null) { pendingMarks[type] = { type: type, value: opts.value }; }
        else if (pendingMarks[type]) { delete pendingMarks[type]; }
        else { pendingMarks[type] = { type: type }; }
        return true;
    }
    function clearPendingMarks() { pendingMarks = null; }
    function insertAttrsWithPending() {
        const tracked = trackedInsertAttrs();
        if (!pendingMarks) return tracked;
        const pend = Object.keys(pendingMarks).map(function (k) { return pendingMarks[k]; });
        const marks = (tracked && tracked.marks ? tracked.marks.slice() : []).concat(pend);
        return Object.assign({}, tracked || {}, { marks: marks });
    }
    // Backspace while tracking: mark the preceding char as deleted (keep it, struck
    // through) and step the caret behind it. (Cross-block tracked delete deferred.)
    function trackedDeleteBackward() {
        if (!caret) return;
        const block = findBlock(model, caret.blockId);
        const offset = caret.offset;
        if (!block) return;
        if (offset <= 0) {
            // R.5.11 — cross-block tracked delete: mark the preceding paragraph break for deletion
            // (paragraphs stay separate until accepted, then merge). Caret moves to the prev block end.
            const container = findBlockContainer(model, block.id);
            if (!container || container.index <= 0) return;
            const prev = container.blocks[container.index - 1];
            if (!prev || prev.type !== 'paragraph' || !prev.content) return;
            recordHistory('delete');
            prev.content[PARAGRAPH_MARK_KEY] = { type: DELETION_MARK, value: trackRevId };
            caret = { blockId: prev.id, offset: blockText(prev).length };
            anchor = null;
            selection = { region: 'Body', blockId: prev.id, offset: caret.offset, isCollapsed: true };
            model.version = Number(model.version || 0) + 1;
            render();
            return;
        }
        recordHistory('delete');
        applyMarkToBlockRange(block, offset - 1, offset, DELETION_MARK, { value: trackRevId });
        caret = { blockId: block.id, offset: offset - 1 };
        anchor = null;
        selection = { region: 'Body', blockId: block.id, offset: caret.offset, isCollapsed: true };
        model.version = Number(model.version || 0) + 1;
        render();
    }
    function trackedDeleteForward() {
        if (!caret) return;
        const block = findBlock(model, caret.blockId);
        const offset = caret.offset;
        const len = block ? blockText(block).length : 0;
        if (!block || offset >= len) return;
        recordHistory('delete');
        applyMarkToBlockRange(block, offset, offset + 1, DELETION_MARK, { value: trackRevId });
        model.version = Number(model.version || 0) + 1;
        render();
    }
    function acceptAll() {
        if (!model) return false;
        recordHistory(null);
        const changed = acceptAllRevisions(model);
        if (changed) { model.version = Number(model.version || 0) + 1; model.indexes = null; render(); }
        return changed;
    }
    function rejectAll() {
        if (!model) return false;
        recordHistory(null);
        const changed = rejectAllRevisions(model);
        if (changed) { model.version = Number(model.version || 0) + 1; model.indexes = null; render(); }
        return changed;
    }
    // R.5.11 — accept / reject one revision by id.
    function acceptOne(revisionId) {
        if (!model) return false;
        recordHistory(null);
        const changed = acceptRevision(model, revisionId);
        if (changed) { model.version = Number(model.version || 0) + 1; model.indexes = null; render(); }
        return changed;
    }
    function rejectOne(revisionId) {
        if (!model) return false;
        recordHistory(null);
        const changed = rejectRevision(model, revisionId);
        if (changed) { model.version = Number(model.version || 0) + 1; model.indexes = null; render(); }
        return changed;
    }
    // R.5.11 — switch the review display (markup / final / original). Non-destructive.
    function setReviewMode(mode) {
        const next = (mode === 'final' || mode === 'original') ? mode : 'markup';
        if (next === reviewMode) return reviewMode;
        reviewMode = next;
        layoutCache = null; // the laid-out content differs per mode
        render();
        return reviewMode;
    }
    function getReviewMode() { return reviewMode; }

    // ----- R.5.2 clipboard ------------------------------------------------------------
    function blockTextLength(blockId) { const b = findBlock(model, blockId); return b ? blockText(b).length : 0; }

    // Deletes the current non-collapsed selection, leaving the caret at its start. Handles a
    // single block (reuse applyReplaceRange) and a contiguous same-container multi-block range.
    function deleteSelectedRange() {
        const range = getSelectionRange();
        if (!model || !range || rangeIsCollapsed(range)) return false;
        const ordered = orderRange(model, range);
        if (ordered.start.blockId === ordered.end.blockId) {
            const res = applyReplaceRange(model, ordered.start.blockId, ordered.start.offset, ordered.end.offset, '', editDeps);
            caret = res.caret; anchor = null; model.indexes = null;
            return true;
        }
        const startBlock = findBlock(model, ordered.start.blockId);
        const endBlock = findBlock(model, ordered.end.blockId);
        const container = startBlock && findBlockContainer(model, startBlock.id);
        const endContainer = endBlock && findBlockContainer(model, endBlock.id);
        if (!startBlock || !endBlock || !container || !endContainer || container.blocks !== endContainer.blocks
            || startBlock.type !== 'paragraph' || endBlock.type !== 'paragraph') return false;
        deleteTextRange(startBlock, ordered.start.offset, blockText(startBlock).length);
        deleteTextRange(endBlock, 0, ordered.end.offset);
        startBlock.content.runs = mergeAdjacentTextRuns(
            asArray(startBlock.content && startBlock.content.runs).concat(asArray(endBlock.content && endBlock.content.runs)));
        if (!startBlock.content.runs.length) startBlock.content.runs = plainRuns('', startBlock.id + '-empty');
        container.blocks.splice(container.index + 1, endContainer.index - container.index); // drop start+1 .. end
        caret = { blockId: startBlock.id, offset: ordered.start.offset }; anchor = null; model.indexes = null;
        return true;
    }

    // Inserts clipboard "lines" (array of [{text, marks}]) at the caret: the first line flows
    // into the current paragraph; each subsequent line starts a new paragraph (split).
    function insertLines(lines) {
        if (!caret || !Array.isArray(lines) || !lines.length) return false;
        for (let li = 0; li < lines.length; li++) {
            if (li > 0) { const para = applyInsertParagraph(model, caret, editDeps); caret = para.caret; }
            const runs = lines[li] || [];
            for (let ri = 0; ri < runs.length; ri++) {
                const run = runs[ri];
                if (!run || typeof run.text !== 'string' || run.text.length === 0) continue;
                const res = applyInsertText(model, caret, run.text, editDeps, { marks: Array.isArray(run.marks) ? run.marks : [] });
                caret = res.caret;
            }
        }
        return true;
    }

    function copySelectionToClipboard(clipboardData) {
        if (!model || !clipboardData) return false;
        const range = getSelectionRange();
        if (rangeIsCollapsed(range)) return false;
        const ser = serializeRange(model, orderRange(model, range));
        try { clipboardData.setData('text/plain', ser.text); } catch (e) { /* */ }
        try { clipboardData.setData('text/html', ser.html); } catch (e) { /* */ }
        try { clipboardData.setData(INTERNAL_MIME, ser.internal); } catch (e) { /* some browsers reject custom types */ }
        return true;
    }

    function cutSelectionToClipboard(clipboardData) {
        if (!copySelectionToClipboard(clipboardData)) return false;
        recordHistory(null);
        deleteSelectedRange();
        model.version = Number(model.version || 0) + 1;
        selection = { region: 'Body', blockId: caret.blockId, offset: caret.offset, isCollapsed: true };
        render();
        return true;
    }

    function pasteFromClipboard(clipboardData, plain) {
        if (!model || !caret || !clipboardData) return false;
        const lines = parseClipboard(function (mime) { return clipboardData.getData(mime); }, { plain: !!plain }, doc);
        if (!lines || !lines.length) return false;
        recordHistory(null);
        if (!rangeIsCollapsed(getSelectionRange())) deleteSelectedRange();
        insertLines(lines);
        model.version = Number(model.version || 0) + 1; model.indexes = null;
        anchor = null;
        selection = { region: 'Body', blockId: caret.blockId, offset: caret.offset, isCollapsed: true };
        render();
        return true;
    }

    // R.5.23 — context-menu clipboard. Menu clicks aren't clipboard events, so route through
    // navigator.clipboard while reusing the same serialize/paste logic via a synthetic
    // clipboardData object.
    function menuCopy() {
        const cap = {};
        const cd = { setData: function (m, v) { cap[m] = v; }, getData: function (m) { return cap[m] || ''; } };
        if (!copySelectionToClipboard(cd)) return false;
        const nav = doc && doc.defaultView && doc.defaultView.navigator;
        if (nav && nav.clipboard && nav.clipboard.writeText && cap['text/plain'] != null) {
            try { nav.clipboard.writeText(cap['text/plain']); } catch (e) { /* permissions */ }
        }
        return true;
    }
    function menuCut() {
        if (!menuCopy()) return false;
        recordHistory(null);
        deleteSelectedRange();
        model.version = Number(model.version || 0) + 1;
        selection = { region: 'Body', blockId: caret.blockId, offset: caret.offset, isCollapsed: true };
        render();
        return true;
    }
    function menuPaste() {
        const nav = doc && doc.defaultView && doc.defaultView.navigator;
        if (!nav || !nav.clipboard || !nav.clipboard.readText) return false;
        return nav.clipboard.readText().then(function (text) {
            if (!text) return false;
            const cd = { getData: function (m) { return m === 'text/plain' ? text : ''; } };
            return pasteFromClipboard(cd, true);
        }).catch(function () { return false; });
    }

    // R.5.18/R.5.22 — record a text operation in the journal and notify the host (collab).
    function emitOp(op) {
        if (!op) return;
        opLog.push(op);
        if (typeof opts.onOperation === 'function') { try { opts.onOperation(op); } catch (e) { /* host gone */ } }
        opListeners.forEach(function (l) { try { l(op); } catch (e) { /* */ } });
    }
    // R.5.22 — apply a remote text operation (insert/delete) to the model and re-render. The
    // caller is responsible for transforming it past un-acked local ops first (see operations.mjs).
    function applyRemoteOperation(op) {
        if (!model || !op || !op.blockId) return false;
        if (op.type === 'insert') {
            const res = applyReplaceRange(model, op.blockId, Number(op.offset) || 0, Number(op.offset) || 0, String(op.text == null ? '' : op.text), editDeps);
            if (!(res && res.ok)) return false;
        } else if (op.type === 'delete') {
            const len = String(op.text == null ? '' : op.text).length;
            const start = Number(op.offset) || 0;
            const res = applyReplaceRange(model, op.blockId, start, start + len, '', editDeps);
            if (!(res && res.ok)) return false;
        } else {
            return false;
        }
        model.version = Number(model.version || 0) + 1; model.indexes = null;
        // Keep the caret valid if it sat after the edit point in the same block.
        if (caret && caret.blockId === op.blockId) {
            const delta = op.type === 'insert' ? String(op.text || '').length : -String(op.text || '').length;
            if ((Number(op.offset) || 0) <= caret.offset) caret = { blockId: caret.blockId, offset: Math.max(0, caret.offset + delta) };
        }
        render();
        return true;
    }

    // R.5.23c — replace [start,end) in a block with new text (used by spell-suggestion picks).
    function replaceRange(blockId, start, end, text) {
        if (!model || !blockId) return false;
        recordHistory(null);
        const res = applyReplaceRange(model, blockId, Number(start) || 0, Number(end) || 0, String(text == null ? '' : text), editDeps);
        if (!(res && res.ok)) return false;
        model.version = Number(model.version || 0) + 1;
        caret = res.caret || caret;
        anchor = null;
        selection = { region: 'Body', blockId: caret.blockId, offset: caret.offset, isCollapsed: true };
        render();
        return true;
    }

    // ----- R.5.4 hyperlink activation --------------------------------------------------
    function activateLink(href) {
        if (typeof opts.onLinkActivate === 'function') { try { opts.onLinkActivate(String(href)); return; } catch (e) { /* fall through to window.open */ } }
        try { (globalThis.window || globalThis).open(String(href), '_blank', 'noopener'); } catch (e) { /* no window (Node) */ }
    }

    // ----- R.5.5 bookmarks (named anchors via a `bookmark` value-mark on a range) -------
    function scanBookmarks() {
        const out = [];
        asArray(model && model.body && model.body.blocks).forEach(function (block) {
            let off = 0;
            asArray(block.content && block.content.runs).forEach(function (run) {
                const t = typeof run.text === 'string' ? run.text : '';
                asArray(run.marks).forEach(function (m) {
                    if (m && String(m.type || '').toLowerCase() === 'bookmark' && m.value != null) {
                        out.push({ name: String(m.value), blockId: block.id, offset: off });
                    }
                });
                off += t.length;
            });
        });
        return out;
    }
    function listBookmarks() {
        const all = scanBookmarks();
        return all.filter(function (b, i) { return all.findIndex(function (x) { return x.name === b.name; }) === i; });
    }
    function addBookmark(name) {
        if (!model || !caret || name == null || String(name) === '') return false;
        const range = getSelectionRange();
        let blockId, s, e;
        if (!rangeIsCollapsed(range)) {
            const o = orderRange(model, range);
            blockId = o.start.blockId;
            s = o.start.offset;
            e = (o.end.blockId === o.start.blockId) ? o.end.offset : blockTextLength(blockId);
        } else {
            blockId = caret.blockId;
            const len = blockTextLength(blockId);
            if (len === 0) return false; // a mark needs ≥1 character to anchor (empty-block point bookmark = follow-up)
            if (caret.offset < len) { s = caret.offset; e = caret.offset + 1; }
            else { s = Math.max(0, len - 1); e = len; }
        }
        const block = findBlock(model, blockId);
        if (!block) return false;
        recordHistory(null);
        applyMarkToBlockRange(block, s, e, 'bookmark', { mode: 'add', value: String(name) });
        model.version = Number(model.version || 0) + 1;
        render();
        return true;
    }
    function goToBookmark(name) {
        const hit = scanBookmarks().find(function (b) { return b.name === String(name); });
        if (!hit) return false;
        caret = { blockId: hit.blockId, offset: hit.offset };
        anchor = null;
        selection = { region: 'Body', blockId: hit.blockId, offset: hit.offset, isCollapsed: true };
        render();
        try { if (caretView && caretView.element && typeof caretView.element.scrollIntoView === 'function') caretView.element.scrollIntoView({ block: 'center' }); } catch (e) { /* no scroll in Node */ }
        announceCaret();
        return true;
    }

    // R.5.15 — outline navigation: jump the caret to a heading block (by id) + scroll.
    function goToHeading(blockId) {
        const block = findBlock(model, blockId);
        if (!block) return false;
        caret = { blockId: blockId, offset: 0 };
        anchor = null;
        selection = { region: 'Body', blockId: blockId, offset: 0, isCollapsed: true };
        render();
        try { if (caretView && caretView.element && typeof caretView.element.scrollIntoView === 'function') caretView.element.scrollIntoView({ block: 'center' }); } catch (e) { /* */ }
        announceCaret();
        return true;
    }

    // R.5.15 — generate a Table of Contents from the document outline, inserted after the caret.
    // Each entry is a paragraph indented by heading level and tagged with its target block id so a
    // click navigates to the heading. Returns the number of entries (0 = no headings).
    function insertTableOfContents() {
        if (!model || !caret) return 0;
        const outline = getDocumentOutline(model);
        if (!outline.length) return 0;
        recordHistory(null);
        const container = findBlockContainer(model, caret.blockId);
        const blocks = (container && container.blocks) || (model.body && model.body.blocks);
        const insertAt = container ? container.index + 1 : blocks.length;
        if (!Array.isArray(blocks)) return 0;
        const tocBlocks = outline.map(function (h, i) {
            const id = stableId('toc', caret.blockId + '-' + i);
            const indent = new Array(Math.max(0, (Number(h.level) || 1) - 1) + 1).join('  '); // em-space per level
            return {
                id: id,
                type: 'paragraph',
                content: { type: 'paragraph', toc: true, tocTargetBlockId: h.blockId, runs: [{ id: id + '-r', kind: 'text', text: indent + h.text }] },
            };
        });
        Array.prototype.splice.apply(blocks, [insertAt, 0].concat(tocBlocks));
        model.version = Number(model.version || 0) + 1; model.indexes = null;
        render();
        return tocBlocks.length;
    }

    const intents = {
        insertText: function (text) {
            if (!caret) return;
            const at = { blockId: caret.blockId, offset: caret.offset };
            const str = String(text == null ? '' : text);
            // R.5.18 — op-log undo for a collapsed-caret type; snapshot when a selection is replaced.
            if (caretSelectionCollapsed()) {
                const before = snapState();
                commitEdit(applyInsertText(model, caret, text, editDeps, insertAttrsWithPending()));
                clearPendingMarks();
                const op = { type: 'insert', blockId: at.blockId, offset: at.offset, text: str };
                recordOpEdit(op, invertOperation(op), 'type', before, snapState());
                emitOp(op);
            } else {
                recordHistory('type');
                commitEdit(applyInsertText(model, caret, text, editDeps, insertAttrsWithPending()));
                clearPendingMarks();
                emitOp({ type: 'insert', blockId: at.blockId, offset: at.offset, text: str });
            }
        },
        copy: function (cd) { return copySelectionToClipboard(cd); },
        cut: function (cd) { return cutSelectionToClipboard(cd); },
        paste: function (cd, plain) { return pasteFromClipboard(cd, plain); },
        deleteBackward: function () {
            if (!caret) return;
            if (trackChanges) { trackedDeleteBackward(); return; }
            const b = findBlock(model, caret.blockId);
            // Op-log undo only for a collapsed same-block single-char delete; a merge at offset 0
            // or a selection delete is structural → snapshot (correct for every case).
            const removed = (caretSelectionCollapsed() && caret.offset > 0 && b) ? blockText(b).slice(caret.offset - 1, caret.offset) : null;
            if (removed != null) {
                const before = snapState();
                const op = { type: 'delete', blockId: caret.blockId, offset: caret.offset - 1, text: removed };
                commitEdit(applyDeleteBackward(model, caret, editDeps));
                recordOpEdit(op, invertOperation(op), 'delete', before, snapState());
                emitOp(op);
            } else {
                recordHistory('delete'); commitEdit(applyDeleteBackward(model, caret, editDeps));
            }
        },
        deleteForward: function () {
            if (!caret) return;
            if (trackChanges) { trackedDeleteForward(); return; }
            const b = findBlock(model, caret.blockId);
            const txt = b ? blockText(b) : '';
            const removed = (caretSelectionCollapsed() && b && caret.offset < txt.length) ? txt.slice(caret.offset, caret.offset + 1) : null;
            if (removed != null) {
                const before = snapState();
                const op = { type: 'delete', blockId: caret.blockId, offset: caret.offset, text: removed };
                commitEdit(applyDeleteForward(model, caret, editDeps));
                recordOpEdit(op, invertOperation(op), 'delete', before, snapState());
                emitOp(op);
            } else {
                recordHistory('delete'); commitEdit(applyDeleteForward(model, caret, editDeps));
            }
        },
        insertParagraph: function () {
            if (!caret) return;
            const cb = findBlock(model, caret.blockId);
            if (cb && isListBlock(cb) && blockText(cb).length === 0) {
                // R.4.8 — Enter on an EMPTY list item exits/outdents the list (no new item),
                // matching Word / Google Docs.
                recordHistory(null);
                changeListLevel(cb, -1);
                model.version = Number(model.version || 0) + 1; model.indexes = null; render();
                return;
            }
            recordHistory(null);
            commitEdit(applyInsertParagraph(model, caret, editDeps));
        },
        // R.4.8 — Tab / Shift+Tab nesting; returns whether the caret was in a list (so the
        // input surface only swallows Tab when it actually changed a list item).
        indentList: function () { return changeListIndent(1); },
        outdentList: function () { return changeListIndent(-1); },
        // R.5.9 — Tab in a table cell navigates cells; otherwise it nests/un-nests a list item.
        tabKey: function (info) {
            const shift = !!(info && info.shiftKey);
            if (caretCell()) return tableTab(shift);
            return shift ? changeListIndent(-1) : changeListIndent(1);
        },
        caretMove: function (info) { moveCaret(info && info.key, !!(info && info.shiftKey)); },
        compositionStart,
        compositionUpdate,
        compositionEnd,
        undo: undo,
        redo: redo,
    };

    // R.4.3 — pointer hit-test: client coords → layout coords (via the rendered page
    // section's client rect) → nearest caret stop.
    function closestPageSection(node) {
        let cur = node;
        while (cur && cur !== root) {
            if (typeof cur.getAttribute === 'function' && cur.getAttribute('data-render-page-index') !== null) return cur;
            cur = cur.parentNode;
        }
        return null;
    }
    // Client coords → model position (no caret mutation). Shared by caret placement and the
    // R.5.6 word/paragraph/drag selection gestures.
    // Client coords → layout coords ({x, y, pageIndex}); R.5.23 divides by the zoom factor.
    function layoutPointFromClient(clientX, clientY, target) {
        if (!lastLayout) return null;
        const section = closestPageSection(target) || pageSectionFor(0);
        if (!section || typeof section.getBoundingClientRect !== 'function') return null;
        const pageIndex = Number(section.getAttribute('data-render-page-index') || 0) || 0;
        const sectionRect = section.getBoundingClientRect();
        const pr = pageRect(pageIndex);
        return { x: (clientX - sectionRect.left) / zoom + (Number(pr.x || 0) || 0), y: (clientY - sectionRect.top) / zoom + (Number(pr.y || 0) || 0), pageIndex: pageIndex };
    }
    function posFromClient(clientX, clientY, target) {
        const lp = layoutPointFromClient(clientX, clientY, target);
        return lp ? hitTestPoint(lastLayout, lp.x, lp.y) : null;
    }

    // R.5.23 — view zoom (CSS transform on the render root; hit-testing divides by it).
    function setZoom(factor) {
        zoom = Math.max(0.25, Math.min(4, Number(factor) || 1));
        if (root) { root.style.transformOrigin = 'top left'; root.style.transform = (zoom === 1) ? '' : ('scale(' + zoom + ')'); }
        return zoom;
    }
    // R.5.23 — change page geometry (margins / size / orientation) at runtime → re-layout.
    function setPageSettings(settings) {
        pageSettings = settings || null;
        layoutCache = null;
        if (model) model.indexes = null;
        if (root && model) render();
        return pageSettings;
    }
    // R.5.23 — print: hide the editing overlays (caret/selection/surface) then window.print().
    const PRINT_STYLE_ID = 'tm-core-print-style';
    function ensurePrintStyle() {
        if (!doc || !doc.head || typeof doc.getElementById !== 'function' || doc.getElementById(PRINT_STYLE_ID)) return;
        const style = doc.createElement('style');
        style.id = PRINT_STYLE_ID;
        style.textContent = '@media print{.tm-core-caret,.tm-core-selection-rect,.tm-core-find-highlight,'
            + '.tm-core-input-surface,.tm-core-live-region,[data-resize-handle]{display:none!important}}';
        try { doc.head.appendChild(style); } catch (e) { /* */ }
    }
    function printDocument() {
        ensurePrintStyle();
        try { (globalThis.window || globalThis).print(); return true; } catch (e) { return false; }
    }

    function placeCaretFromClient(clientX, clientY, shiftKey, target) {
        const pos = posFromClient(clientX, clientY, target);
        if (!pos) return false;
        clearPendingMarks(); // R.5.8 — clicking elsewhere discards a pending format
        if (shiftKey) { if (!anchor) anchor = caret ? { blockId: caret.blockId, offset: caret.offset } : null; }
        else { anchor = null; }
        caret = { blockId: pos.blockId, offset: pos.offset };
        selection = { region: 'Body', blockId: caret.blockId, offset: caret.offset, isCollapsed: !anchor };
        paintOverlays();
        announceCaret();
        return true;
    }

    // R.5.6 — double-click selects the word under the pointer.
    function selectWordAt(clientX, clientY, target) {
        const pos = posFromClient(clientX, clientY, target);
        const block = pos && findBlock(model, pos.blockId);
        if (!block) return false;
        const range = wordRangeAt(blockText(block), pos.offset);
        anchor = { blockId: pos.blockId, offset: range.start };
        caret = { blockId: pos.blockId, offset: range.end };
        selection = { region: 'Body', blockId: pos.blockId, offset: range.end, isCollapsed: range.start === range.end };
        paintOverlays(); announceCaret();
        return true;
    }

    // R.5.6 — triple-click selects the whole paragraph.
    function selectParagraphAt(clientX, clientY, target) {
        const pos = posFromClient(clientX, clientY, target);
        const block = pos && findBlock(model, pos.blockId);
        if (!block) return false;
        const len = blockText(block).length;
        anchor = { blockId: pos.blockId, offset: 0 };
        caret = { blockId: pos.blockId, offset: len };
        selection = { region: 'Body', blockId: pos.blockId, offset: len, isCollapsed: len === 0 };
        paintOverlays(); announceCaret();
        return true;
    }

    // R.5.6 — drag-select: a mousedown on text begins a drag that extends the selection on
    // each doc-level mousemove until mouseup.
    let textDragging = false;
    function onTextDragMove(e) {
        if (!textDragging) return;
        // R.5.9 — dragging across table cells selects a cell range instead of text.
        const anchorCell = textDragAnchorBlockId && locateCell(model, textDragAnchorBlockId);
        if (anchorCell) {
            const pos = posFromClient(e.clientX, e.clientY, e.target);
            const overCell = pos && locateCell(model, pos.blockId);
            if (overCell && overCell.table === anchorCell.table && overCell.cell !== anchorCell.cell) {
                selectCellRange(textDragAnchorBlockId, pos.blockId);
                if (typeof e.preventDefault === 'function') e.preventDefault();
                return;
            }
            if (cellSelection) clearCellSelection();
        }
        placeCaretFromClient(e.clientX, e.clientY, true, e.target);
        if (typeof e.preventDefault === 'function') e.preventDefault();
    }
    function onTextDragEnd() {
        if (!textDragging) return;
        textDragging = false;
        if (doc && typeof doc.removeEventListener === 'function') {
            doc.removeEventListener('mousemove', onTextDragMove);
            doc.removeEventListener('mouseup', onTextDragEnd);
        }
    }
    let textDragAnchorBlockId = null; // R.5.9 — caret block at drag start (cell-range anchor)
    function startTextDrag() {
        textDragging = true;
        textDragAnchorBlockId = caret ? caret.blockId : null;
        if (doc && typeof doc.addEventListener === 'function') {
            doc.addEventListener('mousemove', onTextDragMove);
            doc.addEventListener('mouseup', onTextDragEnd);
        }
    }

    // --- R.5.23b text drag-and-drop ----------------------------------------------------
    // mousedown INSIDE an existing selection starts a potential move; dragging past a small
    // threshold relocates the selected text to the drop caret on mouseup (Word/GDocs gesture).
    let textMove = null;   // { startX, startY, moved, target, drop }
    let dropCaretEl = null;
    function isPosInOrderedRange(pos, ordered) {
        if (!pos || !ordered) return false;
        const order = asArray(lastLayout && lastLayout.blocks).map(function (b) { return b.blockId; });
        const pi = order.indexOf(pos.blockId);
        const si = order.indexOf(ordered.start.blockId);
        const ei = order.indexOf(ordered.end.blockId);
        if (pi < 0 || si < 0 || ei < 0) return false;
        const afterStart = (pi > si) || (pi === si && pos.offset >= ordered.start.offset);
        const beforeEnd = (pi < ei) || (pi === ei && pos.offset <= ordered.end.offset);
        return afterStart && beforeEnd;
    }
    function moveSelectionTo(dropBlockId, dropOffset) {
        const range = getSelectionRange();
        if (!model || !range || rangeIsCollapsed(range)) return false;
        const ordered = orderRange(model, range);
        if (isPosInOrderedRange({ blockId: dropBlockId, offset: dropOffset }, ordered)) return false; // drop inside source → no-op
        const ser = serializeRange(model, ordered);
        const lines = parseClipboard(function (mime) {
            return mime === INTERNAL_MIME ? ser.internal : (mime === 'text/plain' ? ser.text : (mime === 'text/html' ? ser.html : ''));
        }, {}, doc);
        recordHistory(null);
        const sameBlock = ordered.start.blockId === ordered.end.blockId;
        let tgtBlock = dropBlockId;
        let tgtOffset = dropOffset;
        if (sameBlock) {
            if (dropBlockId === ordered.start.blockId && dropOffset > ordered.end.offset) {
                tgtOffset = dropOffset - (ordered.end.offset - ordered.start.offset);
            }
        } else if (dropBlockId === ordered.end.blockId) {
            // the end block merges into the start block on delete — remap the drop into it.
            tgtBlock = ordered.start.blockId;
            tgtOffset = ordered.start.offset + Math.max(0, dropOffset - ordered.end.offset);
        }
        deleteSelectedRange();
        caret = { blockId: tgtBlock, offset: tgtOffset };
        anchor = null;
        selection = { region: 'Body', blockId: caret.blockId, offset: caret.offset, isCollapsed: true };
        if (lines && lines.length) insertLines(lines);
        model.version = Number(model.version || 0) + 1; model.indexes = null;
        render();
        return true;
    }
    function showDropCaret(pos) {
        if (!pos) { hideDropCaret(); return; }
        const stop = caretStopAt(lastLayout, pos);
        const section = stop && stop.rect ? pageSectionFor(Number(stop.pageIndex || 0) || 0) : null;
        if (!section) { hideDropCaret(); return; }
        if (!dropCaretEl) {
            dropCaretEl = doc.createElement('div');
            dropCaretEl.className = 'tm-core-drop-caret';
            dropCaretEl.setAttribute('data-testid', 'core-engine-drop-caret');
            dropCaretEl.setAttribute('aria-hidden', 'true');
            dropCaretEl.style.position = 'absolute';
            dropCaretEl.style.width = '2px';
            dropCaretEl.style.background = 'rgba(37, 99, 235, 0.95)';
            dropCaretEl.style.pointerEvents = 'none';
            dropCaretEl.style.zIndex = '30';
        }
        const local = toPageLocal(stop.rect, Number(stop.pageIndex || 0) || 0);
        dropCaretEl.style.left = (Number(local.x) || 0) + 'px';
        dropCaretEl.style.top = (Number(local.y) || 0) + 'px';
        dropCaretEl.style.height = Math.max(2, Number(local.height) || 16) + 'px';
        section.appendChild(dropCaretEl);
    }
    function hideDropCaret() { if (dropCaretEl && dropCaretEl.parentNode) dropCaretEl.parentNode.removeChild(dropCaretEl); }
    function onTextMoveMove(e) {
        if (!textMove) return;
        const dx = (e.clientX || 0) - textMove.startX;
        const dy = (e.clientY || 0) - textMove.startY;
        if (!textMove.moved && (dx * dx + dy * dy) > 16) textMove.moved = true;
        if (textMove.moved) {
            textMove.drop = posFromClient(e.clientX, e.clientY, e.target);
            showDropCaret(textMove.drop);
            if (typeof e.preventDefault === 'function') e.preventDefault();
        }
    }
    function onTextMoveEnd(e) {
        if (!textMove) return;
        if (doc && typeof doc.removeEventListener === 'function') {
            doc.removeEventListener('mousemove', onTextMoveMove);
            doc.removeEventListener('mouseup', onTextMoveEnd);
        }
        const tm = textMove; textMove = null; hideDropCaret();
        if (tm.moved) {
            const pos = tm.drop || (e && posFromClient(e.clientX, e.clientY, e.target));
            if (pos) moveSelectionTo(pos.blockId, pos.offset);
        } else {
            placeCaretFromClient(tm.startX, tm.startY, false, tm.target); // plain click inside selection → collapse
            paintOverlays();
        }
        focusInput();
    }
    function startTextMove(e, hitPos) {
        textMove = { startX: e.clientX, startY: e.clientY, moved: false, target: e.target, hitPos: hitPos, drop: null };
        if (doc && typeof doc.addEventListener === 'function') {
            doc.addEventListener('mousemove', onTextMoveMove);
            doc.addEventListener('mouseup', onTextMoveEnd);
        }
    }
    function cancelTextMove() {
        if (!textMove) return;
        if (doc && typeof doc.removeEventListener === 'function') {
            doc.removeEventListener('mousemove', onTextMoveMove);
            doc.removeEventListener('mouseup', onTextMoveEnd);
        }
        textMove = null; hideDropCaret();
    }

    // --- R.4.6 inline marks + paragraph formatting --------------------------------
    // Resolves the current selection into an ordered list of { block, start, end } per
    // block (start→end in document order), plus whether it is collapsed.
    function orderedSelectionBlocks() {
        const range = getSelectionRange();
        if (!range || !model) return null;
        const order = asArray(lastLayout && lastLayout.blocks).map(function (b) { return b.blockId; });
        const aIdx = order.indexOf(range.anchor.blockId);
        const fIdx = order.indexOf(range.focus.blockId);
        const anchorFirst = (aIdx < fIdx) || (aIdx === fIdx && range.anchor.offset <= range.focus.offset);
        const startB = anchorFirst ? range.anchor.blockId : range.focus.blockId;
        const startO = anchorFirst ? range.anchor.offset : range.focus.offset;
        const endB = anchorFirst ? range.focus.blockId : range.anchor.blockId;
        const endO = anchorFirst ? range.focus.offset : range.anchor.offset;
        const si = order.indexOf(startB); const ei = order.indexOf(endB);
        const out = [];
        for (let i = si; i <= ei && i >= 0; i++) {
            const block = findBlock(model, order[i]);
            if (!block) continue;
            const len = blockText(block).length;
            const s = (i === si) ? startO : 0;
            const e = (i === ei) ? endO : len;
            out.push({ block: block, start: s, end: e });
        }
        return { blocks: out, collapsed: (startB === endB && startO === endO) };
    }

    // Toggle/apply an inline mark across the selection. Boolean marks (bold/italic/
    // underline/strikethrough) toggle as a group: if every covered block-range already
    // has the mark it is removed, else added. Value marks (textcolor/highlight/font…)
    // are set when `options.value` is supplied. Collapsed selection → no-op (a future
    // milestone adds "pending format" for the next typed character).
    function applyMarkToSelection(type, options) {
        if (!model || !lastLayout) return false;
        const sel = orderedSelectionBlocks();
        const opts = options || {};
        const t = String(type || '').toLowerCase();
        if (!sel || sel.collapsed || !sel.blocks.length) {
            // R.5.8 — collapsed selection: stash a pending mark for the next typed character
            // (toolbar reflects it; insertText applies + clears it).
            return togglePendingMark(t, opts);
        }
        let mode = opts.mode;
        if (!mode && opts.value == null) {
            const allHave = sel.blocks.every(function (b) { return blockRangeHasMark(b.block, b.start, b.end, t); });
            mode = allHave ? 'remove' : 'add';
        }
        recordHistory(null);
        // R.5.11 — when tracking, a formatting change records a `formatrev` mark so it can be
        // accepted/rejected (skip link/comment/bookmark anchors and removals).
        const tracksFormat = trackChanges && mode !== 'remove' && t !== 'link' && t !== 'comment' && t !== 'bookmark';
        sel.blocks.forEach(function (b) {
            applyMarkToBlockRange(b.block, b.start, b.end, t, { mode: mode, value: opts.value });
            if (tracksFormat) applyMarkToBlockRange(b.block, b.start, b.end, FORMAT_REV_MARK, { value: trackRevId, markExtra: { format: t } });
        });
        model.version = Number(model.version || 0) + 1;
        render(); // marks only change run styling, not text → caret/anchor offsets stay valid
        return true;
    }

    // R.4.6h — hyperlinks (a value mark carrying the href on the selected range).
    function applyLink(href) { return applyMarkToSelection('link', { value: String(href == null ? '' : href) }); }
    function removeLink() { return applyMarkToSelection('link', { mode: 'remove' }); }
    function getLinkHref() {
        if (!model) return null;
        const sel = orderedSelectionBlocks();
        if (sel && sel.blocks.length) {
            for (let i = 0; i < sel.blocks.length; i++) {
                const v = firstMarkValueInRange(sel.blocks[i].block, sel.blocks[i].start, sel.blocks[i].end, 'link');
                if (v != null) return v;
            }
            return null;
        }
        if (caret) { const b = findBlock(model, caret.blockId); if (b) return firstMarkValueInRange(b, caret.offset, caret.offset, 'link'); }
        return null;
    }

    // --- R.4.6h-2 find / replace -------------------------------------------------
    function selectMatch(match) {
        if (!match) return;
        anchor = { blockId: match.blockId, offset: match.start };
        caret = { blockId: match.blockId, offset: match.end };
        selection = { region: 'Body', blockId: match.blockId, offset: match.end, isCollapsed: false };
    }
    function find(query, options) {
        if (!model) return 0;
        const matches = findMatches(model, query, options || {});
        findState = { query: String(query == null ? '' : query), opts: options || {}, matches: matches, index: matches.length ? 0 : -1 };
        if (matches.length) selectMatch(matches[0]);
        render();
        return matches.length;
    }
    function findStep(delta) {
        if (!findState || !findState.matches.length) return -1;
        const n = findState.matches.length;
        findState.index = ((findState.index + delta) % n + n) % n;
        selectMatch(findState.matches[findState.index]);
        render();
        return findState.index;
    }
    function clearFind() { findState = null; paintOverlays(); }
    function replaceCurrent(replacement) {
        if (!findState || findState.index < 0) return false;
        const m = findState.matches[findState.index];
        recordHistory(null);
        const repl = (findState.opts && findState.opts.regex) ? expandReplacement(replacement, m.text, m.groups) : String(replacement == null ? '' : replacement);
        const res = applyReplaceRange(model, m.blockId, m.start, m.end, repl, editDeps);
        if (!(res && res.ok)) return false;
        model.version = Number(model.version || 0) + 1;
        // Re-find (offsets shifted) and keep the index near the replaced spot.
        const prevIndex = findState.index;
        findState.matches = findMatches(model, findState.query, findState.opts);
        findState.index = findState.matches.length ? Math.min(prevIndex, findState.matches.length - 1) : -1;
        if (findState.index >= 0) selectMatch(findState.matches[findState.index]);
        render();
        return true;
    }
    function replaceAll(query, replacement, options) {
        if (!model) return 0;
        const matches = findMatches(model, query, options || {});
        if (!matches.length) return 0;
        recordHistory(null);
        const repl = String(replacement == null ? '' : replacement);
        // Apply right-to-left within each block so earlier offsets stay valid.
        const ordered = matches.slice().sort(function (a, b) {
            return a.blockId === b.blockId ? (b.start - a.start) : (a.blockId < b.blockId ? 1 : -1);
        });
        const useRegex = !!(options && options.regex);
        ordered.forEach(function (m) {
            const r = useRegex ? expandReplacement(repl, m.text, m.groups) : repl;
            applyReplaceRange(model, m.blockId, m.start, m.end, r, editDeps);
        });
        model.version = Number(model.version || 0) + 1;
        findState = null;
        render();
        return matches.length;
    }
    function getFindState() {
        if (!findState) return null;
        return { query: findState.query, count: findState.matches.length, index: findState.index };
    }

    // --- R.4.6e headers / footers + fields ---------------------------------------
    function setHeader(content, scope) {
        if (!model) return false;
        recordHistory(null);
        setHeaderFooterRegion(model, 'header', content, scope);
        model.version = Number(model.version || 0) + 1; model.indexes = null; render();
        return true;
    }
    function setFooter(content, scope) {
        if (!model) return false;
        recordHistory(null);
        setHeaderFooterRegion(model, 'footer', content, scope);
        model.version = Number(model.version || 0) + 1; model.indexes = null; render();
        return true;
    }
    function clearHeader() { if (!model) return; recordHistory(null); clearHeaderFooterRegion(model, 'header'); model.version = Number(model.version || 0) + 1; model.indexes = null; render(); }
    function clearFooter() { if (!model) return; recordHistory(null); clearHeaderFooterRegion(model, 'footer'); model.version = Number(model.version || 0) + 1; model.indexes = null; render(); }

    // --- R.4.6g comments ---------------------------------------------------------
    function addComment(text, author) {
        if (!model) return null;
        const sel = orderedSelectionBlocks();
        if (!sel || sel.collapsed || !sel.blocks.length) return null;
        recordHistory(null);
        const id = 'cmt-' + (++commentSeq);
        if (!Array.isArray(model.comments)) model.comments = [];
        model.comments.push({ id: id, author: author || 'You', text: String(text == null ? '' : text), resolved: false, createdAt: Date.now() });
        sel.blocks.forEach(function (b) { addCommentMarkToRange(b.block, b.start, b.end, id); });
        model.version = Number(model.version || 0) + 1;
        render();
        return id;
    }
    function getComments() {
        return asArray(model && model.comments).map(function (c) {
            const pos = commentAnchorPosition(c.id);
            return Object.assign({}, c, {
                anchorText: commentAnchorText(model, c.id),
                replies: asArray(c.replies),                       // R.5.12 threads
                anchorBlockId: pos ? pos.blockId : null,           // R.5.12 rail positioning
                anchorOffset: pos ? pos.offset : null,
            });
        });
    }
    // R.5.12 — the {blockId, offset} of a comment's first anchor mark (for navigation + rail).
    function commentAnchorPosition(commentId) {
        let result = null;
        asArray(model && model.body && model.body.blocks).forEach(function walk(block) {
            if (result || !block) return;
            if (block.type === 'paragraph') {
                let off = 0;
                asArray(block.content && block.content.runs).forEach(function (run) {
                    const t = typeof run.text === 'string' ? run.text : '';
                    if (!result && asArray(run.marks).some(function (m) {
                        return String((m && m.type) || '').toLowerCase() === 'comment' && String(m.value ?? m.Value ?? '') === String(commentId);
                    })) { result = { blockId: block.id, offset: off }; }
                    off += t.length;
                });
            } else if (block.type === 'table') {
                asArray(block.content && block.content.rows).forEach(function (row) { asArray(row.cells).forEach(function (cell) { asArray(cell.blocks).forEach(walk); }); });
            }
        });
        return result;
    }
    // R.5.12 — reply to a comment (appends to its thread).
    function replyToComment(id, text, author) {
        const c = asArray(model && model.comments).find(function (x) { return x.id === id; });
        if (!c) return false;
        recordHistory(null);
        if (!Array.isArray(c.replies)) c.replies = [];
        c.replies.push({ author: author || 'You', text: String(text == null ? '' : text), createdAt: Date.now() });
        model.version = Number(model.version || 0) + 1;
        return true;
    }
    // R.5.12 — navigate to a comment's anchor (caret + scroll).
    function goToComment(id) {
        const pos = commentAnchorPosition(id);
        if (!pos) return false;
        caret = { blockId: pos.blockId, offset: pos.offset };
        anchor = null;
        selection = { region: 'Body', blockId: pos.blockId, offset: pos.offset, isCollapsed: true };
        render();
        try { if (caretView && caretView.element && typeof caretView.element.scrollIntoView === 'function') caretView.element.scrollIntoView({ block: 'center' }); } catch (e) { /* */ }
        announceCaret();
        return true;
    }
    function resolveComment(id) {
        const c = asArray(model && model.comments).find(function (x) { return x.id === id; });
        if (!c) return false;
        recordHistory(null);
        c.resolved = true;
        stripCommentMark(model, id); // anchor highlight clears; record kept as resolved
        model.version = Number(model.version || 0) + 1;
        render();
        return true;
    }
    // R.5.12 — reopen a resolved comment (the thread re-opens in the rail; the original anchor
    // highlight was stripped on resolve and is not restored).
    function reopenComment(id) {
        const c = asArray(model && model.comments).find(function (x) { return x.id === id; });
        if (!c) return false;
        recordHistory(null);
        c.resolved = false;
        model.version = Number(model.version || 0) + 1;
        render();
        return true;
    }
    function removeComment(id) {
        if (!model) return false;
        recordHistory(null);
        model.comments = asArray(model.comments).filter(function (x) { return x.id !== id; });
        stripCommentMark(model, id);
        model.version = Number(model.version || 0) + 1;
        render();
        return true;
    }
    function getCommentIdsAtCaret() {
        if (!caret || !model) return [];
        const b = findBlock(model, caret.blockId);
        return b ? commentIdsInRange(b, caret.offset, caret.offset) : [];
    }

    // R.5.23c — a misspelled word covering `offset` in `block`, or null. Real detection is
    // wired by setSpellChecker(); without one this returns null (no squiggles, no suggestions).
    function misspellingAt(block, offset) {
        if (!spellChecker || !block) return null;
        const text = blockText(block);
        const re = /[\p{L}\p{M}'’]+/gu;
        let m;
        while ((m = re.exec(text))) {
            const start = m.index;
            const end = start + m[0].length;
            if (offset >= start && offset <= end && spellChecker.isMisspelled(m[0])) {
                return { word: m[0], start, end, blockId: block.id, suggestions: spellChecker.suggest ? spellChecker.suggest(m[0]) : [] };
            }
        }
        return null;
    }

    // R.5.23 — the id of the table whose cells contain `blockId` (or null). Used by the
    // context menu to surface table-specific actions when the pointer is inside a table.
    function tableIdContaining(blockId) {
        if (!blockId) return null;
        let found = null;
        asArray(model && model.body && model.body.blocks).forEach(function (b) {
            if (found || !b || b.type !== 'table') return;
            asArray(b.content && b.content.rows).forEach(function (row) {
                asArray(row.cells).forEach(function (cell) {
                    asArray(cell.blocks).forEach(function (cb) { if (cb && cb.id === blockId) found = b.id; });
                });
            });
        });
        return found;
    }

    // R.5.23 — describe what's under a pointer so the host (C#) can show a contextual menu:
    // selection presence, hyperlink href, image object id, comment ids, table containment,
    // misspelling (R.5.23c) and the resolved model position.
    function getContextAt(clientX, clientY, target) {
        const sel = orderedSelectionBlocks();
        const hasSelection = !!(sel && !sel.collapsed && sel.blocks.length);
        const pos = posFromClient(clientX, clientY, target);
        const block = pos && findBlock(model, pos.blockId);
        let link = null;
        let commentIds = [];
        if (block && pos) {
            link = firstMarkValueInRange(block, pos.offset, pos.offset + 1, 'link');
            if (link == null && pos.offset > 0) link = firstMarkValueInRange(block, pos.offset - 1, pos.offset, 'link');
            commentIds = commentIdsInRange(block, pos.offset, pos.offset) || [];
        }
        let objectId = null;
        const objEl = target ? closestWithAttr(target, 'data-object-id') : null;
        if (objEl) objectId = objEl.getAttribute('data-object-id');
        if (!objectId && selectedObjectId) objectId = selectedObjectId;
        const tableId = pos ? tableIdContaining(pos.blockId) : null;
        const misspelling = (block && pos) ? misspellingAt(block, pos.offset) : null;
        return {
            blockId: pos ? pos.blockId : null,
            offset: pos ? pos.offset : null,
            hasSelection,
            link: link || null,
            objectId: objectId || null,
            commentIds,
            inTable: !!tableId,
            tableId: tableId || null,
            misspelling: misspelling || null,
        };
    }

    // R.4.8 — formatting state for the hosted toolbar (bold/italic/underline/strike/align).
    function getFormattingState() {
        const empty = { bold: false, italic: false, underline: false, strikethrough: false, link: false, alignment: 'left', listType: null, bulletList: false, numberedList: false };
        if (!caret || !model) return empty;
        const block = findBlock(model, caret.blockId);
        if (!block) return empty;
        let start = caret.offset;
        let end = caret.offset;
        if (anchor && anchor.blockId === caret.blockId) {
            start = Math.min(anchor.offset, caret.offset);
            end = Math.max(anchor.offset, caret.offset);
        }
        const state = formattingStateForBlockRange(block, start, end);
        // R.5.8 — a pending format (set on a collapsed caret) overrides the toolbar pressed-state.
        if (pendingMarks && start === end) {
            PENDING_BOOLEAN_MARKS.forEach(function (k) { state[k] = !!pendingMarks[k]; });
        }
        // R.4.8 — selection-level list state for the toolbar (bullet / numbered pressed).
        const lt = activeListType();
        state.listType = lt;
        state.bulletList = lt === 'bullet';
        state.numberedList = lt === 'ordered';
        // R.4.8 — paragraph (block) style for the headings dropdown read-back.
        state.paragraphStyle = paragraphStyleName(block);
        return state;
    }

    function isMarkActive(type) {
        const sel = orderedSelectionBlocks();
        if (!sel || sel.collapsed || !sel.blocks.length) return false;
        const t = String(type || '').toLowerCase();
        return sel.blocks.every(function (b) { return blockRangeHasMark(b.block, b.start, b.end, t); });
    }

    // Paragraphs the selection touches (or the caret's paragraph when collapsed).
    function selectionParagraphs() {
        if (!model) return [];
        const sel = orderedSelectionBlocks();
        let blocks = sel && sel.blocks.length ? sel.blocks.map(function (b) { return b.block; }) : [];
        if (!blocks.length && caret) { const b = findBlock(model, caret.blockId); if (b) blocks = [b]; }
        return blocks.filter(function (b) { return b && b.type === 'paragraph'; });
    }

    // Paragraph-level property over every paragraph the selection touches (or the caret's
    // paragraph when collapsed) — alignment / lineSpacing / indentLeft / etc.
    function setParagraphPropertyOnSelection(key, value) {
        const blocks = selectionParagraphs();
        if (!blocks.length) return false;
        recordHistory(null);
        let changed = false;
        blocks.forEach(function (b) { if (setParagraphProperty(b, key, value)) changed = true; });
        if (changed) { model.version = Number(model.version || 0) + 1; render(); }
        return changed;
    }

    // R.4.6b — apply a named paragraph style (Normal / Title / Heading 1–6).
    function setParagraphStyleOnSelection(styleName) {
        const blocks = selectionParagraphs();
        if (!blocks.length) return false;
        recordHistory(null);
        const registry = buildStyleRegistry(model); // R.5.15 — resolve through user styles + inheritance
        let changed = false;
        blocks.forEach(function (b) { if (applyParagraphStyle(b, styleName, registry)) changed = true; });
        if (changed) { model.version = Number(model.version || 0) + 1; render(); }
        return changed;
    }
    // R.5.15 — define/update a named style (with optional basedOn) + re-resolve every paragraph
    // using a style so the change (and inherited changes) propagate immediately.
    function defineStyle(name, def) {
        if (!model || !name) return false;
        recordHistory(null);
        defineModelStyle(model, name, def);
        const registry = buildStyleRegistry(model);
        asArray(model.body && model.body.blocks).forEach(function (b) {
            if (b && b.type === 'paragraph' && b.content && (b.content.styleName || b.content.StyleName)) {
                applyParagraphStyle(b, paragraphStyleName(b), registry);
            }
        });
        model.version = Number(model.version || 0) + 1; render();
        return true;
    }
    function getStyles() { return buildStyleRegistry(model); }

    // --- R.4.8 lists (bullet / numbered) -----------------------------------------
    // Text blocks (paragraph / heading / list) touched by the selection — the unit a list
    // command operates on (works for a collapsed caret too: the caret's own block).
    function selectionTextBlocks() {
        if (!model) return [];
        const sel = orderedSelectionBlocks();
        let blocks = sel && sel.blocks.length ? sel.blocks.map(function (b) { return b.block; }) : [];
        if (!blocks.length && caret) { const b = findBlock(model, caret.blockId); if (b) blocks = [b]; }
        return blocks.filter(function (b) { return b && (b.type === 'paragraph' || b.type === 'heading' || b.type === 'list'); });
    }

    // Toggle a list type over the selection: if every touched block is already this type it
    // turns OFF (→ paragraph); otherwise every block becomes this type (mixed → all on).
    function toggleList(type) {
        const blocks = selectionTextBlocks();
        if (!blocks.length) return false;
        const want = (type === 'ordered' || type === 'numbered') ? 'ordered' : 'bullet';
        const allWanted = blocks.every(function (b) { return isListBlock(b) && listTypeOf(b) === want; });
        recordHistory(null);
        blocks.forEach(function (b) {
            const isWanted = isListBlock(b) && listTypeOf(b) === want;
            if (allWanted) toggleListType(b, want);        // all already this type → toggle OFF
            else if (!isWanted) toggleListType(b, want);   // turn ON / switch type
            // else: already this type inside a mixed selection → leave on
        });
        model.version = Number(model.version || 0) + 1;
        model.indexes = null;
        render();
        return true;
    }

    // Tab / Shift+Tab nesting. delta>0 indents, delta<0 outdents (−1 below level 0 → paragraph).
    function changeListIndent(delta) {
        const blocks = selectionTextBlocks().filter(isListBlock);
        if (!blocks.length) return false;
        recordHistory(null);
        let changed = false;
        blocks.forEach(function (b) { if (changeListLevel(b, delta)) changed = true; });
        if (!changed) return false;
        model.version = Number(model.version || 0) + 1;
        model.indexes = null;
        render();
        return true;
    }

    // 'bullet' | 'ordered' | null (null = none or mixed) — for toolbar pressed state.
    function activeListType() {
        const blocks = selectionTextBlocks();
        if (!blocks.length) return null;
        let type = isListBlock(blocks[0]) ? listTypeOf(blocks[0]) : null;
        for (let i = 1; i < blocks.length; i++) {
            const t = isListBlock(blocks[i]) ? listTypeOf(blocks[i]) : null;
            if (t !== type) return null;
        }
        return type;
    }

    // --- R.4.6d object pointer drag (resize / move) ------------------------------
    function closestWithAttr(node, attr) {
        let cur = node;
        while (cur && cur !== root) {
            if (typeof cur.getAttribute === 'function' && cur.getAttribute(attr) !== null) return cur;
            cur = cur.parentNode;
        }
        return null;
    }
    function startObjectDrag(kind, handle, e) {
        const obj = objectLayoutById(selectedObjectId);
        if (!obj) return;
        objectDrag = {
            kind: kind, handle: handle,
            startX: e.clientX, startY: e.clientY, lastX: e.clientX, lastY: e.clientY,
            startRect: { width: Number(obj.rect.width) || 1, height: Number(obj.rect.height) || 1 },
        };
        if (doc && typeof doc.addEventListener === 'function') {
            doc.addEventListener('mousemove', onObjectDragMove);
            doc.addEventListener('mouseup', onObjectDragEnd);
        }
    }
    function onObjectDragMove(e) {
        if (!objectDrag) return;
        if (objectDrag.kind === 'resize') {
            const dx = e.clientX - objectDrag.startX;
            const dy = e.clientY - objectDrag.startY;
            const size = resizeRectByHandle(objectDrag.startRect, objectDrag.handle, dx, dy, {});
            resizeSelectedObject(size.width, size.height);
        } else { // move — apply the incremental delta
            const ddx = e.clientX - objectDrag.lastX;
            const ddy = e.clientY - objectDrag.lastY;
            objectDrag.lastX = e.clientX; objectDrag.lastY = e.clientY;
            if (ddx || ddy) moveSelectedObject(ddx, ddy);
        }
        if (typeof e.preventDefault === 'function') e.preventDefault();
    }
    function onObjectDragEnd() {
        objectDrag = null;
        history.breakCoalescing(); // end of a resize/move drag → next drag is a new undo step
        if (doc && typeof doc.removeEventListener === 'function') {
            doc.removeEventListener('mousemove', onObjectDragMove);
            doc.removeEventListener('mouseup', onObjectDragEnd);
        }
        notifyObjectSelection(); // refresh the inspector with the dragged size/position
    }

    // R.5.9 — table cell layouts are nested inside each table BLOCK layout, not flattened onto
    // lastLayout — collect them for hit-testing + the cell-selection highlight.
    function allTableCellLayouts() {
        const out = [];
        asArray(lastLayout && lastLayout.blocks).forEach(function (bl) {
            if (bl && bl.type === 'table') asArray(bl.cells).forEach(function (cl) { out.push(cl); });
        });
        return out;
    }
    // R.5.9 — column resize: drag a cell's right border to widen/narrow its column.
    let colResize = null;
    function columnBorderHit(layoutX, layoutY) {
        let hit = null;
        allTableCellLayouts().forEach(function (cl) {
            if (!cl.rect) return;
            const right = cl.rect.x + cl.rect.width;
            if (Math.abs(layoutX - right) <= 5 && layoutY >= cl.rect.y && layoutY <= cl.rect.y + cl.rect.height) {
                hit = { tableId: cl.tableId, columnIndex: Number(cl.columnIndex || 0) + Math.max(1, Number(cl.colSpan || 1)) - 1, startWidth: cl.rect.width };
            }
        });
        return hit;
    }
    function startColResizeDrag(hit, e) {
        colResize = { tableId: hit.tableId, columnIndex: hit.columnIndex, startX: e.clientX, startWidth: hit.startWidth };
        if (doc && typeof doc.addEventListener === 'function') { doc.addEventListener('mousemove', onColResizeMove); doc.addEventListener('mouseup', onColResizeEnd); }
    }
    function onColResizeMove(e) {
        if (!colResize) return;
        setColumnWidth(model, colResize.tableId, colResize.columnIndex, colResize.startWidth + (e.clientX - colResize.startX) / zoom);
        model.version = Number(model.version || 0) + 1; model.indexes = null; render();
        if (typeof e.preventDefault === 'function') e.preventDefault();
    }
    function onColResizeEnd() {
        if (!colResize) return;
        colResize = null; history.breakCoalescing();
        if (doc && typeof doc.removeEventListener === 'function') { doc.removeEventListener('mousemove', onColResizeMove); doc.removeEventListener('mouseup', onColResizeEnd); }
    }

    // --- R.4.6c tables -----------------------------------------------------------
    // Inserts an R×C table after the caret's block and drops the caret into the first cell.
    function insertTable(opts) {
        if (!model || !caret) return null;
        const o = opts || {};
        recordHistory(null);
        const table = createTableModel(o.rows || 2, o.cols || 2);
        insertTableAfterBlock(model, caret.blockId, table, { findBlockContainer });
        model.version = Number(model.version || 0) + 1;
        model.indexes = null;
        const cellPara = firstCellParagraphId(table);
        if (cellPara) {
            caret = { blockId: cellPara, offset: 0 };
            anchor = null;
            selection = { region: 'Body', blockId: cellPara, offset: 0, isCollapsed: true };
        }
        render();
        return { tableId: table.id };
    }
    function currentTableId() {
        if (!caret || !model) return null;
        const t = findTableContaining(model, caret.blockId);
        return t ? t.id : null;
    }
    function addRowToCurrentTable(atIndex) {
        const tid = currentTableId();
        if (!tid) return false;
        recordHistory(null);
        const r = addTableRow(model, tid, atIndex, { findBlock });
        if (r.ok) { model.version = Number(model.version || 0) + 1; model.indexes = null; render(); }
        return r.ok;
    }
    function addColumnToCurrentTable(atIndex) {
        const tid = currentTableId();
        if (!tid) return false;
        recordHistory(null);
        const r = addTableColumn(model, tid, atIndex, { findBlock });
        if (r.ok) { model.version = Number(model.version || 0) + 1; model.indexes = null; render(); }
        return r.ok;
    }
    function tableInfo() {
        const tid = currentTableId();
        if (!tid) return null;
        const t = findBlock(model, tid);
        const rows = asArray(t && t.content && t.content.rows);
        return { tableId: tid, rows: rows.length, cols: rows[0] ? asArray(rows[0].cells).length : 0 };
    }

    // R.5.9 — caret is inside a table cell?
    function caretCell() { return (model && caret) ? locateCell(model, caret.blockId) : null; }
    function placeCaretInCellParagraph(blockId) {
        if (!blockId) return false;
        caret = { blockId: blockId, offset: 0 };
        anchor = null;
        selection = { region: 'Body', blockId: blockId, offset: 0, isCollapsed: true };
        return true;
    }

    // R.5.9 — Tab / Shift+Tab moves between cells; Tab past the last cell appends a row.
    function tableTab(shift) {
        const loc = caretCell();
        if (!loc) return false;
        let targetParaId = adjacentCellParagraphId(model, caret.blockId, shift ? -1 : 1);
        if (!targetParaId && !shift) {
            recordHistory(null);
            addTableRow(model, loc.table.id, null, { findBlock });
            model.version = Number(model.version || 0) + 1; model.indexes = null;
            const rows = asArray(loc.table.content.rows);
            targetParaId = cellFirstParagraphId(asArray(rows[rows.length - 1].cells)[0]);
            placeCaretInCellParagraph(targetParaId);
            render();
            return true;
        }
        if (!targetParaId) return false;
        placeCaretInCellParagraph(targetParaId);
        paintOverlays(); announceCaret();
        return true;
    }

    // R.5.9 — structural cell/row/column edits operating on the caret's cell.
    function runTableEdit(fn) {
        if (!caretCell()) return false;
        recordHistory(null);
        const res = fn(model, caret.blockId);
        if (!res || !res.ok) return false;
        model.version = Number(model.version || 0) + 1; model.indexes = null;
        if (res.caretBlockId) placeCaretInCellParagraph(res.caretBlockId);
        render();
        return true;
    }
    function deleteCurrentTableRow() { return runTableEdit(deleteTableRow); }
    function deleteCurrentTableColumn() { return runTableEdit(deleteTableColumn); }
    function mergeCurrentCellRight() { return runTableEdit(mergeCellRight); }
    function splitCurrentCell() { return runTableEdit(splitCellHorizontal); }
    function mergeCurrentCellDown() { return runTableEdit(mergeCellDown); }          // R.5.9 vertical merge
    function splitCurrentCellVertical() { return runTableEdit(splitCellVertical); }

    // R.5.9 — cell-range selection (rectangular block of table cells).
    let cellSelection = null; // { tableId, ids: Set, anchorBlockId }
    function selectCellRange(fromBlockId, toBlockId) {
        const ids = cellRangeIds(model, fromBlockId, toBlockId);
        if (!ids.length) { clearCellSelection(); return false; }
        const loc = locateCell(model, fromBlockId);
        cellSelection = { tableId: loc ? loc.table.id : null, ids: ids, anchorBlockId: fromBlockId };
        anchor = null; // a cell-range supersedes the text selection
        paintOverlays();
        return true;
    }
    function clearCellSelection() { if (cellSelection) { cellSelection = null; paintOverlays(); } }
    function cellSelectionBlockIds() {
        if (!cellSelection) return [];
        const out = [];
        cellSelection.ids.forEach(function (cellId) {
            asArray(model && model.body && model.body.blocks).forEach(function (tbl) {
                if (tbl.type !== 'table') return;
                asArray(tbl.content && tbl.content.rows).forEach(function (row) {
                    asArray(row.cells).forEach(function (cell) { if (cell.id === cellId) asArray(cell.blocks).forEach(function (b) { out.push(b.id); }); });
                });
            });
        });
        return out;
    }
    // Apply a mark / clear text across every paragraph in the selected cells.
    function formatCellSelection(type, value) {
        if (!cellSelection) return false;
        recordHistory(null);
        cellSelectionBlockIds().forEach(function (blockId) {
            const b = findBlock(model, blockId);
            if (b && b.type === 'paragraph') applyMarkToBlockRange(b, 0, blockText(b).length, String(type).toLowerCase(), { mode: 'add', value: value });
        });
        model.version = Number(model.version || 0) + 1; render();
        return true;
    }
    function clearCellSelectionContent() {
        if (!cellSelection) return false;
        recordHistory(null);
        cellSelectionBlockIds().forEach(function (blockId) {
            const b = findBlock(model, blockId);
            if (b && b.type === 'paragraph') { b.content.runs = plainRuns('', blockId + '-empty'); }
        });
        model.version = Number(model.version || 0) + 1; model.indexes = null; render();
        return true;
    }
    function insertRowRelative(below) {
        const loc = caretCell(); if (!loc) return false;
        return runTableEditAtIndex(addTableRow, loc.rowIndex + (below ? 1 : 0));
    }
    function insertColumnRelative(right) {
        const loc = caretCell(); if (!loc) return false;
        return runTableEditAtIndex(addTableColumn, loc.cellIndex + (right ? 1 : 0));
    }
    function runTableEditAtIndex(fn, atIndex) {
        const loc = caretCell(); if (!loc) return false;
        recordHistory(null);
        const res = fn(model, loc.table.id, atIndex, { findBlock });
        if (!res || !res.ok) return false;
        model.version = Number(model.version || 0) + 1; model.indexes = null; render();
        return true;
    }

    // Creates + mounts the off-screen input surface and wires keystrokes → model.
    // IMPORTANT: the surface must live OUTSIDE the render root — `render()` calls
    // `root.replaceChildren(...)`, which would otherwise detach the capture element
    // and drop keyboard focus. Mount it as a sibling (root.parentNode) or in <body>.
    function attachInput() {
        if (inputSurface) return host;
        inputSurface = createInputSurface({ doc, handlers: intents, ariaLabel: opts.ariaLabel });
        const mountTarget = (root && root.parentNode) || (doc && doc.body) || root;
        if (mountTarget) inputSurface.mount(mountTarget);
        // R.4.7 — mount the ARIA live region as a sibling (survives root.replaceChildren).
        if (!liveRegion) liveRegion = createLiveRegion(doc);
        if (mountTarget && typeof mountTarget.appendChild === 'function') mountTarget.appendChild(liveRegion.element);
        announceCaret();
        // Pointer → caret: mousedown places the caret and keeps focus on the off-screen
        // surface. `preventDefault()` is essential — a plain mousedown on the rendered
        // (non-focusable) text would otherwise move focus to <body> AFTER our focus call,
        // breaking subsequent keyboard input. (R.4.3)
        if (root && typeof root.addEventListener === 'function') {
            pointerHandler = function (e) {
                // R.5.23 — ignore non-primary buttons here; the right-button mousedown must NOT
                // collapse the selection (the contextmenu handler reads it + places the caret).
                if (e && e.button != null && e.button !== 0) return;
                // (0) R.5.4 — Ctrl/Cmd+click on a hyperlink opens it (Word / Google Docs gesture).
                if (e.ctrlKey || e.metaKey) {
                    const linkEl = closestWithAttr(e.target, 'data-href');
                    if (linkEl) {
                        const href = linkEl.getAttribute('data-href');
                        if (href) {
                            if (typeof e.preventDefault === 'function') e.preventDefault();
                            activateLink(href);
                            return;
                        }
                    }
                }
                // (0.5) R.5.13 — click a header/footer region → place the caret in it (editable).
                const hfRegionEl = closestWithAttr(e.target, 'data-render-region');
                const hfRegion = hfRegionEl && hfRegionEl.getAttribute('data-render-region');
                if (hfRegion === 'Header' || hfRegion === 'Footer') {
                    const hfBlockEl = closestWithAttr(e.target, 'data-render-block-id');
                    const hfBlockId = hfBlockEl && hfBlockEl.getAttribute('data-render-block-id');
                    const hfBlock = hfBlockId && findBlock(model, hfBlockId);
                    if (hfBlock) {
                        if (selectedObjectId) clearObjectSelection();
                        caret = { blockId: hfBlockId, offset: blockText(hfBlock).length };
                        anchor = null;
                        selection = { region: hfRegion, blockId: hfBlockId, offset: caret.offset, isCollapsed: true };
                        if (typeof e.preventDefault === 'function') e.preventDefault();
                        focusInput(); paintOverlays(); announceCaret();
                        return;
                    }
                }
                // (1) Resize handle on the selected object → start a resize drag.
                const handleEl = closestWithAttr(e.target, 'data-resize-handle');
                if (handleEl && selectedObjectId) {
                    startObjectDrag('resize', handleEl.getAttribute('data-resize-handle'), e);
                    if (typeof e.preventDefault === 'function') e.preventDefault();
                    return;
                }
                // (2) Object body → select it and start a move drag.
                const objEl = closestWithAttr(e.target, 'data-object-id');
                if (objEl) {
                    selectObject(objEl.getAttribute('data-object-id'));
                    startObjectDrag('move', null, e);
                    if (typeof e.preventDefault === 'function') e.preventDefault();
                    focusInput();
                    return;
                }
                // (2.5) R.5.9 — near a table column border → start a column resize drag.
                const lp = layoutPointFromClient(e.clientX, e.clientY, e.target);
                if (lp) {
                    const border = columnBorderHit(lp.x, lp.y);
                    if (border) { startColResizeDrag(border, e); if (typeof e.preventDefault === 'function') e.preventDefault(); return; }
                }
                // (3) Text → clear object + cell selection; place caret / select word / select paragraph.
                if (selectedObjectId) clearObjectSelection();
                if (cellSelection) clearCellSelection();
                // R.5.15 — click on a Table-of-Contents entry navigates to its heading.
                const hitPos = posFromClient(e.clientX, e.clientY, e.target);
                const hitBlock = hitPos && findBlock(model, hitPos.blockId);
                if (hitBlock && hitBlock.content && hitBlock.content.toc && hitBlock.content.tocTargetBlockId) {
                    if (typeof e.preventDefault === 'function') e.preventDefault();
                    goToHeading(hitBlock.content.tocTargetBlockId);
                    focusInput();
                    return;
                }
                const clickCount = Number(e.detail) || 1;
                if (clickCount >= 3) { // triple-click → paragraph
                    if (selectParagraphAt(e.clientX, e.clientY, e.target)) { if (typeof e.preventDefault === 'function') e.preventDefault(); focusInput(); }
                    return;
                }
                if (clickCount === 2) { // double-click → word
                    if (selectWordAt(e.clientX, e.clientY, e.target)) { if (typeof e.preventDefault === 'function') e.preventDefault(); focusInput(); }
                    return;
                }
                // (3.5) R.5.23b — mousedown inside an existing selection → potential text move.
                const curRange = getSelectionRange();
                if (!e.shiftKey && anchor && curRange && !rangeIsCollapsed(curRange)
                    && hitPos && isPosInOrderedRange(hitPos, orderRange(model, curRange))) {
                    startTextMove(e, hitPos);
                    if (typeof e.preventDefault === 'function') e.preventDefault();
                    focusInput();
                    return;
                }
                if (placeCaretFromClient(e.clientX, e.clientY, !!e.shiftKey, e.target)) {
                    if (typeof e.preventDefault === 'function') e.preventDefault();
                    focusInput();
                    startTextDrag(); // mousedown begins a drag-select (extends until mouseup)
                }
            };
            root.addEventListener('mousedown', pointerHandler);
            // R.5.23 — right-click → place caret (unless inside a selection) + emit context.
            contextMenuHandler = function (e) {
                if (typeof opts.onContextMenu !== 'function') return; // no host menu wired → native menu
                const sel = orderedSelectionBlocks();
                const hasSelection = !!(sel && !sel.collapsed && sel.blocks.length);
                if (!hasSelection) {
                    // Mirror browsers: a right-click outside any selection moves the caret first.
                    if (selectedObjectId) {
                        const objEl = closestWithAttr(e.target, 'data-object-id');
                        if (!objEl) clearObjectSelection();
                    }
                    placeCaretFromClient(e.clientX, e.clientY, false, e.target);
                    focusInput();
                }
                const info = getContextAt(e.clientX, e.clientY, e.target);
                if (typeof e.preventDefault === 'function') e.preventDefault();
                try { opts.onContextMenu(info, e.clientX, e.clientY); } catch (err) { /* host gone */ }
            };
            root.addEventListener('contextmenu', contextMenuHandler);
        }
        return host;
    }

    // R.5.23c — install a spell checker ({ isMisspelled(word), suggest(word)? }); null disables.
    function setSpellChecker(checker) {
        spellChecker = (checker && typeof checker.isMisspelled === 'function') ? checker : null;
        render();
        return host;
    }

    function focusInput() {
        if (inputSurface) inputSurface.focus();
        return host;
    }

    function destroy() {
        if (root && pointerHandler && typeof root.removeEventListener === 'function') {
            root.removeEventListener('mousedown', pointerHandler);
            if (contextMenuHandler) root.removeEventListener('contextmenu', contextMenuHandler);
        }
        pointerHandler = null;
        contextMenuHandler = null;
        onObjectDragEnd(); // detach any in-flight drag listeners
        onTextDragEnd();   // detach any in-flight text drag-select listeners
        cancelTextMove();  // R.5.23b — detach any in-flight text move-drag
        onColResizeEnd();  // detach any in-flight column-resize listeners
        history.clear();
        clearObjectEls();
        clearFindEls();
        clearSpellEls();
        clearRemoteCursorEls();
        findState = null;
        clearSelectionEls();
        if (caretView) { caretView.destroy(); caretView = null; }
        if (liveRegion) { liveRegion.destroy(); liveRegion = null; }
        if (inputSurface) { inputSurface.destroy(); inputSurface = null; }
        if (root && typeof root.replaceChildren === 'function') {
            try { root.replaceChildren(); } catch { /* ignore */ }
        }
        root = null;
        model = null;
        selection = null;
        viewport = null;
        lastLayout = null;
        lastSnapshot = null;
        caret = null;
        anchor = null;
        composition = null;
        selectedObjectId = null;
    }

    const host = {
        mount,
        setModel,
        setSelection,
        getCaret,
        getSelectionRange,
        setViewport,
        render,
        attachInput,
        focusInput,
        moveCaret,
        placeCaretFromClient,
        // R.4.6 formatting
        toggleMark: function (type) { return applyMarkToSelection(type); },
        applyMark: function (type, value) { return applyMarkToSelection(type, { value: value }); },
        isMarkActive,
        getFormattingState,
        // R.4.6h hyperlinks
        applyLink,
        removeLink,
        getLinkHref,
        activateLink,
        // R.5.2 clipboard
        copyToClipboard: copySelectionToClipboard,
        cutToClipboard: cutSelectionToClipboard,
        pasteFromClipboard: pasteFromClipboard,
        deleteSelectedRange,
        // R.5.5 bookmarks
        addBookmark,
        goToBookmark,
        listBookmarks,
        // R.5.15 outline / TOC + named-style registry
        goToHeading,
        insertTableOfContents,
        defineStyle,
        getStyles,
        // R.4.6h-2 find / replace
        find,
        findNext: function () { return findStep(1); },
        findPrev: function () { return findStep(-1); },
        clearFind,
        replaceCurrent,
        replaceAll,
        getFindState,
        // R.4.6f track changes
        setTrackChanges,
        isTrackChanges: function () { return trackChanges; },
        acceptAllRevisions: acceptAll,
        rejectAllRevisions: rejectAll,
        acceptRevision: acceptOne,
        rejectRevision: rejectOne,
        setReviewMode,
        getReviewMode,
        getRevisions: function () { return listRevisions(model); },
        hasRevisions: function () { return hasRevisions(model); },
        // R.4.6g comments
        addComment,
        getComments,
        resolveComment,
        reopenComment,
        removeComment,
        getCommentIdsAtCaret,
        replyToComment,
        goToComment,
        // R.5.23 context menu + spellcheck + menu clipboard
        getContextAt,
        setSpellChecker,
        menuCopy,
        menuCut,
        menuPaste,
        replaceRange,
        // R.5.18/R.5.22 operation journal + collaboration
        applyRemoteOperation,
        getOperationLog: function () { return opLog.slice(); },
        clearOperationLog: function () { opLog = []; },
        addOperationListener: function (fn) { if (typeof fn === 'function') opListeners.push(fn); },
        setRemoteCursors,
        getRemoteCursors: function () { return remoteCursors.slice(); },
        misspellingAt: function (blockId, offset) { const b = findBlock(model, blockId); return b ? misspellingAt(b, offset) : null; },
        // R.4.6e headers / footers
        setHeader,
        setFooter,
        clearHeader,
        clearFooter,
        setAlignment: function (value) { return setParagraphPropertyOnSelection('alignment', value); },
        setParagraphProperty: function (key, value) { return setParagraphPropertyOnSelection(key, value); },
        // R.4.6b headings + styles + outline
        setParagraphStyle: function (styleName) { return setParagraphStyleOnSelection(styleName); },
        getParagraphStyle: function () { const b = selectionParagraphs()[0]; return b ? paragraphStyleName(b) : null; },
        getOutline: function () { return getDocumentOutline(model); },
        // R.4.8 lists (bullet / numbered)
        toggleList: function (type) { return toggleList(type); },
        indentList: function () { return changeListIndent(1); },
        outdentList: function () { return changeListIndent(-1); },
        activeListType: function () { return activeListType(); },
        // R.4.6c tables
        insertTable,
        addTableRow: function (atIndex) { return addRowToCurrentTable(atIndex); },
        addTableColumn: function (atIndex) { return addColumnToCurrentTable(atIndex); },
        getTableInfo: tableInfo,
        // R.5.9 advanced table editing
        tableTab,
        deleteTableRow: deleteCurrentTableRow,
        deleteTableColumn: deleteCurrentTableColumn,
        mergeCellRight: mergeCurrentCellRight,
        splitCell: splitCurrentCell,
        mergeCellDown: mergeCurrentCellDown,
        splitCellVertical: splitCurrentCellVertical,
        selectCellRange,
        clearCellSelection,
        getCellSelection: function () { return cellSelection ? cellSelection.ids.slice() : []; },
        formatCellSelection,
        clearCellSelectionContent,
        setColumnWidth: function (columnIndex, width) { const loc = caretCell(); if (!loc) return false; const r = setColumnWidth(model, loc.table.id, columnIndex, width); if (r.ok) { model.version = Number(model.version || 0) + 1; model.indexes = null; render(); } return r.ok; },
        insertRowAbove: function () { return insertRowRelative(false); },
        insertRowBelow: function () { return insertRowRelative(true); },
        insertColumnLeft: function () { return insertColumnRelative(false); },
        insertColumnRight: function () { return insertColumnRelative(true); },
        // R.4.6d floating images
        insertImage,
        selectObject,
        clearObjectSelection,
        resizeSelectedObject,
        moveSelectedObject,
        getSelectedObjectId: function () { return selectedObjectId; },
        // R.4.8 image inspector — selected-object snapshot + alt/wrap/size edits
        getSelectedObjectInfo: selectedObjectInfo,
        setSelectedObjectAltText: function (text) { return setSelectedObjectAltText(text); },
        setSelectedObjectWrapMode: function (mode) { return setSelectedObjectWrapMode(mode); },
        setSelectedObjectAlignment: function (align) { return setSelectedObjectAlignment(align); },
        setSelectedObjectCaption: function (text) { return setSelectedObjectCaption(text); },
        setSelectedObjectPosition: function (x, y) { return setSelectedObjectPosition(x, y); },
        bringSelectedObjectForward: function () { return nudgeSelectedObjectZ(1); },
        sendSelectedObjectBackward: function () { return nudgeSelectedObjectZ(-1); },
        getObjects: function () { return asArray(lastLayout && lastLayout.objects).slice(); },
        getObjectElements: function () { return objectEls.slice(); },
        // R.4.6i undo/redo
        undo,
        redo,
        canUndo: function () { return history.canUndo(); },
        canRedo: function () { return history.canRedo(); },
        getHistoryDepth: function () { return history.depth(); },
        getInputSurface: function () { return inputSurface; },
        getLastRenderTimings: function () { return Object.assign({}, renderTimings); }, // R.4.9.1 profiling
        getLastEditDirty: function () { return lastEditDirty ? Object.assign({}, lastEditDirty) : null; }, // R.4.9.2
        getLastIncrementalBail: function () { return lastIncrementalBail; }, // R.4.9.3 diagnostics
        getCaretElement: function () { return caretView ? caretView.element : null; },
        getSelectionElements: function () { return selectionEls.slice(); },
        // R.5.20 accessibility — announcing granularity + live-region read-back
        setAnnounceGranularity,
        getAnnounceGranularity: function () { return announceGranularity; },
        getLiveRegionText: function () { return liveRegion ? liveRegion.getText() : ''; },
        // R.5.23 view subsystems — zoom / runtime page settings / print
        setZoom,
        getZoom: function () { return zoom; },
        setPageSettings,
        getPageSettings: function () { return pageSettings; },
        print: printDocument,
        isComposing: function () { return composition != null; },
        getComposition: function () { return composition ? { blockId: composition.blockId, start: composition.start, text: composition.text } : null; },
        getLayout: function () { return lastLayout; },
        getSnapshot: function () { return lastSnapshot; },
        getLayoutComputeCount: function () { return layoutComputeCount; }, // R.4.6i-2 cache telemetry
        // R.4.7 accessibility
        getLiveRegionElement: function () { return liveRegion ? liveRegion.element : null; },
        getLiveRegionText: function () { return liveRegion ? liveRegion.getText() : ''; },
        getRenderer: function () { return renderer; },
        getEngine: function () { return engine; },
        destroy,
    };
    return host;
}
