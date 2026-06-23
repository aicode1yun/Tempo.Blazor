// Phase R.4.6f — core-engine/track-changes.mjs
// Self-contained "track changes" for the model-owned surface. While tracking, inserted
// text carries an `insertion` mark and deleted text is NOT removed — it gets a `deletion`
// mark (so it stays visible, struck through). Accept/reject then resolves every change:
//
//   accept: insertion → keep text (drop mark);  deletion → remove text
//   reject: insertion → remove text;            deletion → keep text (drop mark)
//
// Marks render via mergeTextStyle + decorationsFromMarks (insertion = underline/green,
// deletion = strike/red). Pure — mutates the model in place.
//
//   INSERTION_MARK / DELETION_MARK            → mark type names
//   acceptAllRevisions(model) / rejectAllRevisions(model) → boolean (changed)
//   listRevisions(model)                      → [{ kind, text }]
//   hasRevisions(model)                       → boolean

import { asArray } from '../core/helpers.mjs';
import { markType } from '../core/marks.mjs';
import { normalizeTextRunForMerge, mergeAdjacentTextRuns, plainRuns } from '../core/inline-runs.mjs';

export const INSERTION_MARK = 'insertion';
export const DELETION_MARK = 'deletion';
export const FORMAT_REV_MARK = 'formatrev';        // R.5.11 — a tracked formatting change (run mark)
export const PARAGRAPH_MARK_KEY = 'paragraphMarkRevision'; // R.5.11 — tracked deletion of a paragraph break (on content)

function hasMark(run, type) { return asArray(run.marks).some(function (m) { return markType(m) === type; }); }
function stripMark(run, type) { run.marks = asArray(run.marks).filter(function (m) { return markType(m) !== type; }); return run; }

// R.5.11 — cross-block tracked delete: Backspace at a paragraph start marks the preceding
// paragraph break for deletion (the paragraphs stay separate until accepted, then merge).
function resolveParagraphMarks(model, accept, revisionId) {
    let changed = false;
    const blocks = asArray(model && model.body && model.body.blocks);
    for (let i = 0; i < blocks.length; i++) {
        const b = blocks[i];
        const mark = b && b.content && b.content[PARAGRAPH_MARK_KEY];
        if (!mark) continue;
        if (revisionId != null && String(mark.value ?? mark.Value ?? '') !== String(revisionId)) continue;
        changed = true;
        if (accept) {
            const next = blocks[i + 1];
            if (b.type === 'paragraph' && next && next.type === 'paragraph') {
                b.content.runs = mergeAdjacentTextRuns(asArray(b.content.runs).concat(asArray(next.content && next.content.runs)));
                if (!b.content.runs.length) b.content.runs = plainRuns('', (b.id || 'block') + '-empty');
                blocks.splice(i + 1, 1);
            }
        }
        delete b.content[PARAGRAPH_MARK_KEY];
    }
    return changed;
}

// R.5.11 — format revisions: a `formatrev` mark records that a formatting mark (its `format`)
// was applied while tracking. Accept drops the record (keeps the formatting); reject drops the
// record AND the formatting mark it introduced.
function resolveFormatRevisions(model, accept, revisionId) {
    let changed = false;
    eachParagraph(model, function (block) {
        asArray(block.content && block.content.runs).forEach(function (run) {
            const fr = asArray(run.marks).find(function (m) {
                return markType(m) === FORMAT_REV_MARK && (revisionId == null || String(m.value ?? m.Value ?? '') === String(revisionId));
            });
            if (!fr) return;
            changed = true;
            const fmt = fr.format || fr.Format;
            run.marks = asArray(run.marks).filter(function (m) { return m !== fr; });
            if (!accept && fmt) run.marks = run.marks.filter(function (m) { return markType(m) !== fmt; });
        });
    });
    return changed;
}

function eachParagraph(model, visit) {
    asArray(model && model.body && model.body.blocks).forEach(function walk(block) {
        if (!block) return;
        if (block.type === 'paragraph') { visit(block); return; }
        if (block.type === 'table') {
            asArray(block.content && block.content.rows).forEach(function (row) {
                asArray(row.cells).forEach(function (cell) { asArray(cell.blocks).forEach(walk); });
            });
        }
    });
}

// dropMarkType = the change kind whose text is KEPT (mark cleared); removeMarkType = the
// kind whose text is REMOVED. Shared core of accept/reject. `revisionId` (optional) scopes
// the resolve to a single revision (R.5.11 per-revision accept/reject).
function revisionIdOf(run, type) {
    const mark = asArray(run.marks).find(function (m) { return markType(m) === type; });
    return mark ? String(mark.value ?? mark.Value ?? '') : null;
}
function matchesRevision(run, type, revisionId) {
    if (!hasMark(run, type)) return false;
    if (revisionId == null) return true;
    return revisionIdOf(run, type) === String(revisionId);
}
function resolveRevisions(model, keepMarkType, removeMarkType, revisionId) {
    let changed = false;
    eachParagraph(model, function (block) {
        if (!block.content) return;
        const runs = asArray(block.content.runs);
        const out = [];
        runs.forEach(function (run) {
            if (run.kind !== 'drawing' && matchesRevision(run, removeMarkType, revisionId)) { changed = true; return; } // drop text
            if (matchesRevision(run, keepMarkType, revisionId)) { stripMark(run, keepMarkType); changed = true; }
            out.push(normalizeTextRunForMerge(run));
        });
        block.content.runs = mergeAdjacentTextRuns(out);
        if (!block.content.runs.length) block.content.runs = plainRuns('', (block.id || 'block') + '-empty');
    });
    return changed;
}

// accept/reject also resolve paragraph-mark deletions (merge/keep) + format revisions (R.5.11).
export function acceptAllRevisions(model) {
    const a = resolveRevisions(model, INSERTION_MARK, DELETION_MARK);
    const b = resolveParagraphMarks(model, true, null);
    const c = resolveFormatRevisions(model, true, null);
    return a || b || c;
}
export function rejectAllRevisions(model) {
    const a = resolveRevisions(model, DELETION_MARK, INSERTION_MARK);
    const b = resolveParagraphMarks(model, false, null);
    const c = resolveFormatRevisions(model, false, null);
    return a || b || c;
}
// R.5.11 — accept / reject a SINGLE revision by id (text + paragraph-mark + format).
export function acceptRevision(model, revisionId) {
    const a = resolveRevisions(model, INSERTION_MARK, DELETION_MARK, revisionId);
    const b = resolveParagraphMarks(model, true, revisionId);
    const c = resolveFormatRevisions(model, true, revisionId);
    return a || b || c;
}
export function rejectRevision(model, revisionId) {
    const a = resolveRevisions(model, DELETION_MARK, INSERTION_MARK, revisionId);
    const b = resolveParagraphMarks(model, false, revisionId);
    const c = resolveFormatRevisions(model, false, revisionId);
    return a || b || c;
}

// Revisions grouped by id (+ kind), text concatenated — drives a per-revision review list.
export function listRevisions(model) {
    const byKey = new Map();
    const order = [];
    eachParagraph(model, function (block) {
        asArray(block.content && block.content.runs).forEach(function (run) {
            [INSERTION_MARK, DELETION_MARK].forEach(function (kind) {
                if (!hasMark(run, kind)) return;
                const id = revisionIdOf(run, kind) || '';
                const key = kind + ':' + id;
                if (!byKey.has(key)) { byKey.set(key, { id: id, kind: kind === INSERTION_MARK ? 'insertion' : 'deletion', text: '' }); order.push(key); }
                byKey.get(key).text += (run.text || '');
            });
            // R.5.11 — format revisions.
            const fr = asArray(run.marks).find(function (m) { return markType(m) === FORMAT_REV_MARK; });
            if (fr) {
                const id = String(fr.value ?? fr.Value ?? '');
                const key = 'format:' + id;
                if (!byKey.has(key)) { byKey.set(key, { id: id, kind: 'format', format: fr.format || fr.Format || '', text: '' }); order.push(key); }
                byKey.get(key).text += (run.text || '');
            }
        });
    });
    // R.5.11 — paragraph-mark deletions.
    asArray(model && model.body && model.body.blocks).forEach(function (b) {
        const mark = b && b.content && b.content[PARAGRAPH_MARK_KEY];
        if (!mark) return;
        const id = String(mark.value ?? mark.Value ?? '');
        const key = 'paragraph:' + id;
        if (!byKey.has(key)) { byKey.set(key, { id: id, kind: 'paragraphDeletion', text: '¶' }); order.push(key); }
    });
    return order.map(function (k) { return byKey.get(k); });
}

// R.5.11 — non-destructively applies a review mode to a model CLONE for preview/render:
//   'final'    → as if all changes accepted; 'original' → as if all rejected; 'markup' → unchanged.
export function applyReviewMode(model, mode) {
    if (mode === 'final') { resolveRevisions(model, INSERTION_MARK, DELETION_MARK); resolveParagraphMarks(model, true, null); resolveFormatRevisions(model, true, null); }
    else if (mode === 'original') { resolveRevisions(model, DELETION_MARK, INSERTION_MARK); resolveParagraphMarks(model, false, null); resolveFormatRevisions(model, false, null); }
    return model;
}

export function hasRevisions(model) {
    let found = false;
    eachParagraph(model, function (block) {
        asArray(block.content && block.content.runs).forEach(function (run) {
            if (hasMark(run, INSERTION_MARK) || hasMark(run, DELETION_MARK) || hasMark(run, FORMAT_REV_MARK)) found = true;
        });
    });
    asArray(model && model.body && model.body.blocks).forEach(function (b) {
        if (b && b.content && b.content[PARAGRAPH_MARK_KEY]) found = true;
    });
    return found;
}
