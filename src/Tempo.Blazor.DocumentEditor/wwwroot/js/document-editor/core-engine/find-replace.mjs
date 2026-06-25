// Phase R.4.6h-2 — core-engine/find-replace.mjs
// Pure document search over the model (not the DOM): collect every occurrence of a query
// across body paragraphs and table cells, as logical { blockId, start, end } ranges. The
// render-host turns matches into selection/highlight overlays and drives replace. (Native
// browser Ctrl+F still works too, since the new surface renders real text.)
//
//   findMatches(model, query, { caseSensitive?, wholeWord? }) → [{ blockId, start, end, text }]

import { asArray } from '../core/helpers.mjs';
import { blockText } from '../core/text-helpers.mjs';

function isWordChar(ch) { return /[\wÀ-ɏ]/.test(ch || ''); }

function isWholeWord(hay, idx, len) {
    const before = idx > 0 ? hay[idx - 1] : '';
    const after = idx + len < hay.length ? hay[idx + len] : '';
    return !isWordChar(before) && !isWordChar(after);
}

function eachParagraph(model, visit) {
    asArray(model && model.body && model.body.blocks).forEach(function walk(block) {
        if (!block) return;
        if (block.type === 'paragraph') { visit(block); return; }
        if (block.type === 'table') {
            asArray(block.content && block.content.rows).forEach(function (row) {
                asArray(row.cells).forEach(function (cell) {
                    asArray(cell.blocks).forEach(walk);
                });
            });
        }
    });
}

export function findMatches(model, query, options) {
    const opts = options || {};
    const q = String(query == null ? '' : query);
    if (!q) return [];
    if (opts.regex === true) return findRegexMatches(model, q, opts); // R.5.14
    const caseSensitive = opts.caseSensitive === true;
    const needle = caseSensitive ? q : q.toLowerCase();
    const matches = [];
    eachParagraph(model, function (block) {
        const text = blockText(block);
        const hay = caseSensitive ? text : text.toLowerCase();
        let from = 0;
        while (from <= hay.length) {
            const idx = hay.indexOf(needle, from);
            if (idx < 0) break;
            if (!opts.wholeWord || isWholeWord(hay, idx, needle.length)) {
                matches.push({ blockId: block.id, start: idx, end: idx + needle.length, text: text.slice(idx, idx + needle.length) });
            }
            from = idx + Math.max(1, needle.length);
        }
    });
    return matches;
}

// R.5.14 — regular-expression search. Each match carries `groups` (captured sub-matches) so
// replace can honour `$1`..`$9` / `$&` back-references. Invalid patterns yield no matches.
function findRegexMatches(model, pattern, opts) {
    let re;
    try { re = new RegExp(pattern, opts.caseSensitive === true ? 'g' : 'gi'); }
    catch (e) { return []; }
    const matches = [];
    eachParagraph(model, function (block) {
        const text = blockText(block);
        re.lastIndex = 0;
        let m, guard = 0;
        while ((m = re.exec(text)) !== null) {
            if (m[0].length === 0) { re.lastIndex++; continue; } // skip zero-width (avoid infinite loop)
            const start = m.index, end = start + m[0].length;
            if (!opts.wholeWord || isWholeWord(text, start, m[0].length)) {
                matches.push({ blockId: block.id, start: start, end: end, text: m[0], groups: m.slice(1) });
            }
            if (++guard > 100000) break;
        }
    });
    return matches;
}

// Expands `$1`..`$99`, `$&` (whole match) and `$$` (literal $) in a replacement string using a
// match's captured groups. Used for regex replace; plain replace passes the string through.
export function expandReplacement(replacement, matchText, groups) {
    const g = groups || [];
    return String(replacement == null ? '' : replacement).replace(/\$(\$|&|\d{1,2})/g, function (whole, token) {
        if (token === '$') return '$';
        if (token === '&') return String(matchText == null ? '' : matchText);
        const n = parseInt(token, 10);
        return (n >= 1 && g[n - 1] != null) ? String(g[n - 1]) : '';
    });
}
