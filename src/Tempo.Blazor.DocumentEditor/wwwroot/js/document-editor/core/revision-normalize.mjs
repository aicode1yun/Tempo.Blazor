// Phase D — core/revision-normalize.mjs
// Pure normalizers for revision (tracked-changes) records — the inverse of the
// `exportRevisionType`/`exportRevisionAction` enum mappers in `core/export-types.mjs`.
// These read incoming wire-format values (numeric or string) and produce the
// canonical lowercase-ish name used internally.

import { asText, sortObject } from './helpers.mjs';

// Inbound revision type — handles both numeric ordinals (1=Deletion..6=Table) and
// permissive string aliases. Unknown → 'Insertion'.
export function normalizeRevisionType(value) {
    if (value === 1) return 'Deletion';
    if (value === 2) return 'FormatChange';
    if (value === 3) return 'Move';
    if (value === 4) return 'Structure';
    if (value === 5) return 'Image';
    if (value === 6) return 'Table';
    const raw = String(value || '').replace(/\s+/g, '').toLowerCase();
    if (raw === 'insert' || raw === 'insertion') return 'Insertion';
    if (raw === 'delete' || raw === 'deletion') return 'Deletion';
    if (raw === 'format' || raw === 'formatchange' || raw === 'formatting') return 'FormatChange';
    return value ? String(value) : 'Insertion';
}

// Inbound revision status — Pending=0/default, Accepted=1, Rejected=2.
export function normalizeRevisionStatus(value) {
    if (value === 1) return 'Accepted';
    if (value === 2) return 'Rejected';
    const raw = String(value || '').toLowerCase();
    if (raw.indexOf('accept') >= 0) return 'Accepted';
    if (raw.indexOf('reject') >= 0) return 'Rejected';
    return 'Pending';
}

// Inbound revision range — handles `{start, end}`, `{startOffset, endOffset}`, and
// the Pascal variants. Coerces to non-decreasing `{blockId, start, end}`.
export function normalizeRevisionRange(value) {
    const range = value || {};
    const start = Number(range.start ?? range.Start
        ?? range.startOffset ?? range.StartOffset ?? 0) || 0;
    const end = Number(range.end ?? range.End
        ?? range.endOffset ?? range.EndOffset ?? start) || start;
    return sortObject({
        blockId: asText(range.blockId || range.BlockId
            || range.startBlockId || range.StartBlockId || ''),
        start: Math.min(start, end),
        end: Math.max(start, end),
    });
}
