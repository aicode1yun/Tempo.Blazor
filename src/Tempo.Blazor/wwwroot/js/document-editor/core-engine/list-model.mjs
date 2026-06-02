// Phase R.4.8 (lists) — core-engine/list-model.mjs
// Bullet + ordered (numbered) lists for the model-owned engine. In the core model EVERY
// editable block is a `paragraph` (the converter folds headings/lists/quotes into
// paragraphs; the paragraph engine only lays out `type:'paragraph'`). So a list ITEM is a
// paragraph that carries list properties on its content: `listType` ('bullet'|'ordered')
// and a 0-based nesting `level`. Keeping the block a paragraph means text layout, caret,
// and editing all work unchanged — and Enter (applyInsertParagraph clones `content`)
// continues the list automatically.
//
// Numbering is COMPUTED from document order (consecutive same-level ordered items), so
// inserting / deleting / re-leveling items renumbers automatically — no stored ordinals.
//
//   isListBlock(block)                     → content carries a list type
//   listTypeOf(block) / listLevelOf(block) → 'bullet'|'ordered' | 0..MAX_LIST_LEVEL
//   toggleListType(block, type)            → set / clear / switch the list type (block stays a paragraph)
//   setListLevel(block, level)             → clamp 0..MAX (returns whether it changed)
//   changeListLevel(block, delta)          → +1 / -1 (Tab / Shift+Tab); -1 below 0 → plain paragraph
//   listMarkerText(block, ordinal)         → marker glyph/label for one block
//   computeListMarkers(blocks)             → Map blockId → marker string ('•','◦','1.','a.','i.',…)

import { asArray } from '../core/helpers.mjs';

export const MAX_LIST_LEVEL = 8;
const BULLET_GLYPHS = ['•', '◦', '▪'];

function rawListType(block) {
    return block && block.content && (block.content.listType || block.content.ListType);
}

export function isListBlock(block) {
    const t = String(rawListType(block) || '').toLowerCase();
    return t === 'bullet' || t === 'ordered' || t === 'numbered';
}

// A block whose runs can carry a list marker. In the core model these are all paragraphs.
function isTextBlock(block) {
    return !!block && (block.type === 'paragraph' || block.type === 'heading' || block.type === 'list');
}

function ensureContent(block) {
    if (!block.content) block.content = { type: 'paragraph', runs: [] };
    return block.content;
}

export function listTypeOf(block) {
    if (!isListBlock(block)) return null;
    const t = String(rawListType(block)).toLowerCase();
    return (t === 'ordered' || t === 'numbered') ? 'ordered' : 'bullet';
}

export function listLevelOf(block) {
    const raw = Number((block && block.content && (block.content.level ?? block.content.Level)) || 0) || 0;
    return Math.max(0, Math.min(MAX_LIST_LEVEL, raw | 0));
}

function clearListProps(block) {
    const c = ensureContent(block);
    delete c.listType; delete c.ListType;
    delete c.level; delete c.Level;
}

export function toggleListType(block, type) {
    if (!isTextBlock(block)) return false;
    const want = (type === 'ordered' || type === 'numbered') ? 'ordered' : 'bullet';
    if (isListBlock(block) && listTypeOf(block) === want) {
        // Toggling the active list type OFF returns the item to a plain paragraph.
        clearListProps(block);
        return true;
    }
    const c = ensureContent(block);
    const prevLevel = isListBlock(block) ? listLevelOf(block) : 0;
    c.listType = want;
    c.level = prevLevel;
    return true;
}

export function setListLevel(block, level) {
    if (!isListBlock(block)) return false;
    const next = Math.max(0, Math.min(MAX_LIST_LEVEL, Number(level) | 0));
    if (listLevelOf(block) === next) return false;
    ensureContent(block).level = next;
    return true;
}

export function changeListLevel(block, delta) {
    if (!isListBlock(block)) return false;
    const next = listLevelOf(block) + (delta > 0 ? 1 : -1);
    if (next < 0) {
        // Outdenting below level 0 leaves the list entirely (Word/GDocs behaviour).
        clearListProps(block);
        return true;
    }
    return setListLevel(block, Math.min(MAX_LIST_LEVEL, next));
}

// --- marker / numbering ---------------------------------------------------------------

function toAlpha(n) {
    let s = '';
    let x = n | 0;
    while (x > 0) { const r = (x - 1) % 26; s = String.fromCharCode(97 + r) + s; x = Math.floor((x - 1) / 26); }
    return s || 'a';
}

const ROMAN = [[1000, 'm'], [900, 'cm'], [500, 'd'], [400, 'cd'], [100, 'c'], [90, 'xc'],
    [50, 'l'], [40, 'xl'], [10, 'x'], [9, 'ix'], [5, 'v'], [4, 'iv'], [1, 'i']];
function toRoman(n) {
    let x = n | 0; let s = '';
    for (let i = 0; i < ROMAN.length && x > 0; i++) { while (x >= ROMAN[i][0]) { s += ROMAN[i][1]; x -= ROMAN[i][0]; } }
    return s || 'i';
}

function orderedLabel(level, ordinal) {
    const kind = level % 3; // 0: 1. 2. 3.  1: a. b. c.  2: i. ii. iii.
    if (kind === 1) return toAlpha(ordinal) + '.';
    if (kind === 2) return toRoman(ordinal) + '.';
    return String(ordinal) + '.';
}

function bulletGlyph(level) { return BULLET_GLYPHS[level % BULLET_GLYPHS.length]; }

export function listMarkerText(block, ordinal) {
    if (!isListBlock(block)) return '';
    const level = listLevelOf(block);
    return listTypeOf(block) === 'ordered' ? orderedLabel(level, ordinal || 1) : bulletGlyph(level);
}

export function computeListMarkers(blocks) {
    const markers = new Map();
    const counters = []; // counters[level] = last ordinal used at that nesting level
    asArray(blocks).forEach(function (block) {
        if (!isListBlock(block)) { counters.length = 0; return; }
        const level = listLevelOf(block);
        // Returning to a shallower level resets every deeper counter (so nested runs restart).
        if (counters.length > level + 1) counters.length = level + 1;
        if (listTypeOf(block) === 'ordered') {
            counters[level] = (counters[level] || 0) + 1;
            markers.set(block.id, orderedLabel(level, counters[level]));
        } else {
            // A bullet breaks an ordered run at its level — a following ordered item restarts.
            counters[level] = 0;
            markers.set(block.id, bulletGlyph(level));
        }
    });
    return markers;
}
