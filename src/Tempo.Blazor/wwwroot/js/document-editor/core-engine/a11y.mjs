// Phase R.4.7 — core-engine/a11y.mjs
// Accessibility for the positioned-DOM surface. The big win of rendering real text (vs a
// canvas) is that a screen reader can already read the document; this module adds the
// semantics on top: a document role on the root, heading roles/levels, and an off-screen
// ARIA live region that announces the caret's context (paragraph / heading + text) as it
// moves. Manual NVDA/VoiceOver remains the human gate; the structure here is what the
// automated accessibility-tree checks assert.
//
//   applyEditorAria(root, { label })          → role/document semantics on the render root
//   headingAriaForBlock(modelBlock)           → { role, level } | null (for renderer)
//   describeCaretContext(model, caret, deps)  → a spoken-friendly description string
//   createLiveRegion(doc)                     → { element, announce(text) }

import { blockText } from '../core/text-helpers.mjs';
import { wordRangeAt, prevGraphemeBoundary, nextGraphemeBoundary } from '../layout/grapheme.mjs';

export function applyEditorAria(root, options) {
    if (!root || typeof root.setAttribute !== 'function') return;
    const opts = options || {};
    root.setAttribute('role', 'document');
    root.setAttribute('aria-label', opts.label || 'Rich text document');
    root.setAttribute('aria-roledescription', 'rich text editor');
}

// Heading semantics for a paragraph block carrying a heading level.
export function headingAriaForBlock(modelBlock) {
    const content = modelBlock && modelBlock.content;
    const level = content && (content.headingLevel != null ? content.headingLevel : content.HeadingLevel);
    if (level == null || Number(level) < 1) return null;
    return { role: 'heading', level: Math.max(1, Math.min(6, Number(level) || 1)) };
}

// Describes where the caret is for the live region: "Heading level 2, Results" / the
// paragraph text / "(empty)".
export function describeCaretContext(model, caret, deps) {
    if (!caret) return '';
    const findBlock = deps && deps.findBlock;
    const block = findBlock ? findBlock(model, caret.blockId) : null;
    if (!block) return '';
    const heading = headingAriaForBlock(block);
    const text = blockText(block);
    const prefix = heading ? ('Heading level ' + heading.level + ', ') : '';
    const body = text && text.length ? text : '(empty)';
    return prefix + body;
}

// R.5.20 — finer announcing granularity. 'character' speaks the grapheme just crossed,
// 'word' the word under the caret, anything else falls back to the paragraph context.
export function describeCaretGranular(model, caret, deps, granularity) {
    if (!caret) return '';
    if (granularity !== 'character' && granularity !== 'word') return describeCaretContext(model, caret, deps);
    const findBlock = deps && deps.findBlock;
    const block = findBlock ? findBlock(model, caret.blockId) : null;
    if (!block) return '';
    const text = blockText(block);
    const offset = Math.max(0, Math.min(text.length, Number(caret.offset) || 0));
    if (granularity === 'character') {
        let ch = '';
        if (offset > 0) ch = text.slice(prevGraphemeBoundary(text, offset), offset);      // the grapheme just crossed
        else if (text.length) ch = text.slice(0, nextGraphemeBoundary(text, 0));
        if (ch === ' ') return 'space';
        return ch || '(empty)';
    }
    const range = wordRangeAt(text, offset);
    const word = text.slice(range.start, range.end).trim();
    return word || '(empty)';
}

const LIVE_REGION_CSS = [
    'position:absolute', 'left:-9999px', 'top:0', 'width:1px', 'height:1px',
    'overflow:hidden', 'clip:rect(0 0 0 0)', 'white-space:nowrap',
].join(';');

export function createLiveRegion(doc) {
    const d = doc || globalThis.document;
    const el = d.createElement('div');
    el.className = 'tm-core-live-region';
    el.setAttribute('data-testid', 'core-engine-live-region');
    el.setAttribute('role', 'status');
    el.setAttribute('aria-live', 'polite');
    el.setAttribute('aria-atomic', 'true');
    el.style.cssText = LIVE_REGION_CSS;
    let last = '';
    function announce(text) {
        const t = String(text == null ? '' : text);
        if (t === last) return; // avoid re-announcing identical context (e.g. intra-line caret moves)
        last = t;
        el.textContent = t;
    }
    function destroy() { if (el.parentNode && typeof el.parentNode.removeChild === 'function') el.parentNode.removeChild(el); }
    return { element: el, announce: announce, getText: function () { return el.textContent || ''; }, destroy: destroy };
}
