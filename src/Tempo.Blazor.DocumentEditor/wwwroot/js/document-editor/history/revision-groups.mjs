// Phase D — history/revision-groups.mjs
// `createRevisionGroupNormaliser({...})` factory → `normalizeRevisionGroups(model, scopeIds)`
// — coalesces adjacent revision runs into single revisions when they share the
// same author/status/type/formatting (using `canMergeAdjacentRevisionRuns`).
// Returns `{ok, merged, removed, scoped, indexesRebuilt}`.

import { asArray, asText, sortObject } from '../core/helpers.mjs';
import { mergeAdjacentTextRuns } from '../core/inline-runs.mjs';
import { readRevisionIdsFromRun } from '../core/marks.mjs';
import { normalizeRevisionRange } from '../core/revision-normalize.mjs';
import {
    setRevisionPayloadText,
    readRevisionStatus,
} from './revision-helpers.mjs';
import { revisionPayloadText } from './track-changes.mjs';
import {
    canMergeAdjacentRevisionRuns,
    replaceRevisionIdOnRun,
} from './revision-merge.mjs';

export function createRevisionGroupNormaliser(options) {
    const opts = options || {};
    if (typeof opts.ensureRevisionList !== 'function') {
        throw new TypeError(
            'createRevisionGroupNormaliser requires options.ensureRevisionList (function)');
    }
    if (typeof opts.buildIndexes !== 'function') {
        throw new TypeError(
            'createRevisionGroupNormaliser requires options.buildIndexes (function)');
    }
    const { ensureRevisionList, buildIndexes } = opts;

    function normalizeRevisionGroups(model, scopeIds) {
        ensureRevisionList(model);
        const revisionsById = {};
        asArray(model && model.revisions).forEach(function (revision) {
            revisionsById[revision.id] = revision;
        });
        const removedIds = new Set();
        let merged = 0;
        const scopes = asArray(scopeIds).map(asText).filter(function (id) {
            return id && id !== 'document' && id !== 'revisions';
        });
        const scoped = scopes.length > 0;
        const scopeLookup = new Set(scopes);

        function mergeRevision(sourceRevision, targetRevision, blockId, start, end, sourceText) {
            if (!sourceRevision || !targetRevision
                || sourceRevision.id === targetRevision.id) return;
            const targetRange = normalizeRevisionRange(
                targetRevision.affectedRange || targetRevision.range || {});
            const sourceRange = normalizeRevisionRange(
                sourceRevision.affectedRange || sourceRevision.range || {});
            const nextRange = sortObject({
                blockId: blockId || targetRange.blockId || sourceRange.blockId,
                start: Math.min(targetRange.start, sourceRange.start, Number(start || 0)),
                end: Math.max(targetRange.end, sourceRange.end, Number(end || 0)),
            });
            targetRevision.affectedRange = nextRange;
            targetRevision.range = nextRange;
            setRevisionPayloadText(targetRevision,
                (revisionPayloadText(targetRevision) || '')
                + (revisionPayloadText(sourceRevision) || asText(sourceText)));
            removedIds.add(sourceRevision.id);
            merged++;
        }

        function scanBlock(block) {
            if (!block || block.type !== 'paragraph') return;
            let cursor = 0;
            let previous = null;
            asArray(block.content && block.content.runs).forEach(function (run) {
                const text = asText(run.text);
                const start = cursor;
                const end = cursor + text.length;
                cursor = end;
                let revisionId = readRevisionIdsFromRun(run)[0] || '';
                let revision = revisionId ? revisionsById[revisionId] : null;
                if (previous && revision
                    && canMergeAdjacentRevisionRuns(
                        previous.revision, revision, previous.run, run, previous.end, start)) {
                    mergeRevision(revision, previous.revision, block.id, previous.start, end, text);
                    replaceRevisionIdOnRun(run, revision.id, previous.revision.id);
                    revision = previous.revision;
                    revisionId = previous.revision.id;
                }
                previous = revision && readRevisionStatus(revision) === 'Pending'
                    ? { revision: revision, run: run, start: start, end: end }
                    : null;
            });
            block.content.runs = mergeAdjacentTextRuns(block.content.runs);
        }

        function scanScopedBlock(block) {
            if (!block) return;
            if (scoped && !scopeLookup.has(block.id)) {
                if (block.type === 'table') {
                    asArray(block.content && block.content.rows).forEach(function (row) {
                        asArray(row.cells).forEach(function (cell) {
                            asArray(cell.blocks).forEach(scanScopedBlock);
                        });
                    });
                }
                return;
            }
            scanBlock(block);
        }

        asArray(model && model.body && model.body.blocks).forEach(scanScopedBlock);
        asArray(model && model.headers).forEach(function (region) {
            asArray(region.blocks).forEach(scanScopedBlock);
        });
        asArray(model && model.footers).forEach(function (region) {
            asArray(region.blocks).forEach(scanScopedBlock);
        });
        if (removedIds.size > 0) {
            model.revisions = asArray(model.revisions).filter(function (revision) {
                return !removedIds.has(revision.id);
            });
        }
        buildIndexes(model);
        return sortObject({
            ok: true,
            merged: merged,
            removed: removedIds.size,
            scoped: scoped,
            indexesRebuilt: true,
        });
    }

    return Object.freeze({ normalizeRevisionGroups });
}
