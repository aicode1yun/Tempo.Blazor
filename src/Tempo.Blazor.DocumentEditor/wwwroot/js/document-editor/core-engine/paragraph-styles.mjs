// Phase R.4.6b — core-engine/paragraph-styles.mjs
// Named paragraph styles (Normal / Title / Heading 1–6) + document outline. The layout
// engine already reads a paragraph's base style from `content.style` (font size/weight),
// so a "heading" is simply a paragraph whose named style sets a larger base style and an
// outline level. This module owns the registry, applies a style to a block, and derives
// the heading outline used for navigation / a table of contents.
//
//   DEFAULT_PARAGRAPH_STYLES                  → the built-in registry
//   applyParagraphStyle(block, name, reg?)    → sets content.styleName + base style + headingLevel
//   paragraphStyleName(block)                 → the block's current style name ('Normal' default)
//   getDocumentOutline(model)                 → [{ blockId, level, text, styleName }] for headings

import { asArray } from '../core/helpers.mjs';
import { blockText } from '../core/text-helpers.mjs';

export const DEFAULT_PARAGRAPH_STYLES = {
    Normal: { label: 'Normal', outlineLevel: null, style: { fontSize: 16, fontWeight: '400' } },
    Title: { label: 'Title', outlineLevel: 0, style: { fontSize: 36, fontWeight: '700' } },
    Heading1: { label: 'Heading 1', outlineLevel: 1, style: { fontSize: 32, fontWeight: '700' } },
    Heading2: { label: 'Heading 2', outlineLevel: 2, style: { fontSize: 26, fontWeight: '700' } },
    Heading3: { label: 'Heading 3', outlineLevel: 3, style: { fontSize: 22, fontWeight: '700' } },
    Heading4: { label: 'Heading 4', outlineLevel: 4, style: { fontSize: 18, fontWeight: '700' } },
    Heading5: { label: 'Heading 5', outlineLevel: 5, style: { fontSize: 16, fontWeight: '700' } },
    Heading6: { label: 'Heading 6', outlineLevel: 6, style: { fontSize: 14, fontWeight: '700' } },
};

export function paragraphStyleName(block) {
    return (block && block.content && (block.content.styleName || block.content.StyleName)) || 'Normal';
}

// R.5.15 — the effective registry = user-defined `model.styles` layered over the built-ins.
export function buildStyleRegistry(model) {
    const userStyles = (model && (model.styles || model.Styles)) || {};
    return Object.assign({}, DEFAULT_PARAGRAPH_STYLES, userStyles);
}

// R.5.15 — resolve a style's effective properties by walking its `basedOn` inheritance chain
// (root → derived; the derived style's own properties win). Cycles are guarded.
export function resolveStyle(name, registry) {
    const reg = registry || DEFAULT_PARAGRAPH_STYLES;
    const chain = [];
    const seen = {};
    let cur = reg[name] ? name : 'Normal';
    while (cur && reg[cur] && !seen[cur]) { seen[cur] = true; chain.push(reg[cur]); cur = reg[cur].basedOn || reg[cur].BasedOn; }
    let style = {}; let outlineLevel = null; let label = name;
    for (let i = chain.length - 1; i >= 0; i--) {
        style = Object.assign(style, chain[i].style || chain[i].Style || {});
        if (chain[i].outlineLevel !== undefined) outlineLevel = chain[i].outlineLevel;
        if (chain[i].label) label = chain[i].label;
    }
    return { style: style, outlineLevel: outlineLevel, label: label };
}

// R.5.15 — define / update a named style (optionally `basedOn` another) on the model.
export function defineStyle(model, name, def) {
    if (!model || !name) return false;
    if (!model.styles) model.styles = {};
    model.styles[name] = Object.assign({}, def || {});
    return true;
}

export function applyParagraphStyle(block, styleName, registry) {
    if (!block || block.type !== 'paragraph') return false;
    const reg = registry || DEFAULT_PARAGRAPH_STYLES;
    const name = reg[styleName] ? styleName : 'Normal';
    const resolved = resolveStyle(name, reg); // R.5.15 — resolve through the basedOn chain
    if (!block.content) block.content = { type: 'paragraph', runs: [] };
    block.content.styleName = name;
    // The named style provides the paragraph BASE style (run-level marks still win at
    // render time via mergeTextStyle).
    block.content.style = Object.assign({}, resolved.style);
    if (resolved.outlineLevel == null || resolved.outlineLevel < 1) delete block.content.headingLevel;
    else block.content.headingLevel = resolved.outlineLevel;
    return true;
}

export function getDocumentOutline(model) {
    const out = [];
    asArray(model && model.body && model.body.blocks).forEach(function (b) {
        if (!b || b.type !== 'paragraph' || !b.content) return;
        const level = b.content.headingLevel ?? b.content.HeadingLevel;
        if (level != null && Number(level) >= 1) {
            out.push({
                blockId: b.id,
                level: Number(level) || 1,
                text: blockText(b),
                styleName: paragraphStyleName(b),
            });
        }
    });
    return out;
}
