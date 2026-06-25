// Phase D — history/revision-run-mutators.mjs
// Revision-aware mutators that operate on paragraph runs (clear/remove a revision
// from runs, remove the text inside a range, apply a revision mark across a range).
//
// `createRevisionRunMutators({...})` injects the helper dependencies so the legacy
// IIFE can keep its inline copies and tests can run with stub findBlock/transformRunsInRange.

import { asArray } from '../core/helpers.mjs';
import {
    normalizeMarks,
    readRevisionIdFromMark,
    readRevisionIdsFromRun,
    updateMarks,
} from '../core/marks.mjs';
import { mergeAdjacentTextRuns } from '../core/inline-runs.mjs';
import { normalizeRevisionRange } from '../core/revision-normalize.mjs';

export function createRevisionRunMutators(options) {
    const opts = options || {};
    const required = ['findBlock', 'transformRunsInRange', 'buildIndexes'];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createRevisionRunMutators requires options.${key} (function)`);
        }
    }
    const { findBlock, transformRunsInRange, buildIndexes } = opts;

    function clearRevisionFromRuns(model, revisionId) {
        asArray(model && model.body && model.body.blocks).forEach(function (block) {
            if (block.type !== 'paragraph') return;
            asArray(block.content && block.content.runs).forEach(function (run) {
                if (run.revisionId === revisionId || run.RevisionId === revisionId) {
                    delete run.revisionId;
                    delete run.RevisionId;
                }
                run.marks = normalizeMarks(asArray(run.marks || run.Marks).filter(function (mark) {
                    return readRevisionIdFromMark(mark) !== revisionId;
                }));
                delete run.Marks;
            });
            block.content.runs = mergeAdjacentTextRuns(block.content.runs);
        });
        buildIndexes(model);
    }

    function removeRevisionRuns(model, revisionId) {
        asArray(model && model.body && model.body.blocks).forEach(function (block) {
            if (block.type !== 'paragraph') return;
            block.content.runs = mergeAdjacentTextRuns(
                asArray(block.content && block.content.runs).filter(function (run) {
                    return readRevisionIdsFromRun(run).indexOf(revisionId) < 0;
                }));
        });
        buildIndexes(model);
    }

    function removeRangeText(model, range) {
        const normalizedRange = normalizeRevisionRange(range);
        const block = findBlock(model, normalizedRange.blockId);
        transformRunsInRange(block, normalizedRange.start, normalizedRange.end, function (run) {
            run.text = '';
            return run;
        });
        buildIndexes(model);
    }

    function applyRevisionMark(model, range, mark) {
        const normalizedRange = normalizeRevisionRange(range);
        const block = findBlock(model, normalizedRange.blockId);
        transformRunsInRange(block, normalizedRange.start, normalizedRange.end, function (run) {
            run.marks = updateMarks(run.marks, mark, false);
            return run;
        });
        buildIndexes(model);
    }

    return Object.freeze({
        clearRevisionFromRuns,
        removeRevisionRuns,
        removeRangeText,
        applyRevisionMark,
    });
}
