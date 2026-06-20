// Phase D — history/revision-engine.mjs
// `createRevisionEngineFactory(deps)` → `createRevisionEngine(model, options)` →
//   tracked-changes engine: insert/delete/format with optional revision recording,
//   coalescing of adjacent insertions, accept/reject, visible-text projection per
//   review mode, and an overlay model. Track-changes resolution, revision type/range
//   normalisation, decorative styling and run lookup are pure imports; the model
//   mutators and revision-list helpers are injected (they come from the engine's
//   already-built run-mutator / revision-list / list-helper factories).
//
// `renderOverlay` touches the DOM (`globalThis.document`) and is only callable in a
// browser; the rest of the surface is environment-agnostic.

import { asArray, asText, clone, stableId, sortObject } from '../core/helpers.mjs';
import { blockText } from '../core/text-helpers.mjs';
import { insertTextRun } from '../core/insert-text-run.mjs';
import { findRunAtOffset } from '../core/run-finders.mjs';
import { normalizeRevisionType, normalizeRevisionRange } from '../core/revision-normalize.mjs';
import { resolveRevisionUserId, resolveTrackChangesState } from './track-changes.mjs';
import { revisionDecorativeStyle } from './revision-decorative.mjs';
import { markOverlayNonText } from '../render/render-helpers.mjs';
import { createSelectionSnapshot } from '../core/selection-snapshot.mjs';

const REQUIRED_DEPS = [
    'findBlock', 'buildIndexes', 'ensureRevisionList', 'addRevision', 'getRevisionById',
    'setRevisionForRange', 'applyRevisionMark', 'clearRevisionFromRuns', 'removeRevisionRuns',
    'updateRevisionStatus', 'removeRangeText', 'splitParagraphPreservingInlineMetadata',
];

export function createRevisionEngineFactory(options) {
    const opts = options || {};
    for (const key of REQUIRED_DEPS) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createRevisionEngineFactory requires options.${key} (function)`);
        }
    }
    const {
        findBlock, buildIndexes, ensureRevisionList, addRevision, getRevisionById,
        setRevisionForRange, applyRevisionMark, clearRevisionFromRuns, removeRevisionRuns,
        updateRevisionStatus, removeRangeText, splitParagraphPreservingInlineMetadata,
    } = opts;

    return function createRevisionEngine(model, engineOptions) {
        const eopts = engineOptions || {};
        ensureRevisionList(model);
        const userId = resolveRevisionUserId(eopts);
        const trackChanges = resolveTrackChangesState(eopts).enabled === true;

        function createRevision(type, range, payload, extra) {
            const normalizedType = normalizeRevisionType(type);
            return addRevision(model, {
                id: (extra && (extra.id || extra.Id)) || 'rev-' + normalizedType.toLowerCase() + '-' + Date.now() + '-' + Math.floor(Math.random() * 100000),
                type: normalizedType,
                author: (extra && (extra.author || extra.Author)) || userId,
                timestamp: (extra && (extra.timestamp || extra.Timestamp)) || Date.now(),
                affectedRange: range,
                payload: payload || {},
                status: 'Pending',
            });
        }

        function coalesceInsertionRevision(selection, text) {
            const offset = Number((selection && selection.offset) || 0);
            const candidate = asArray(model.revisions).slice().reverse().find(function (revision) {
                return revision.type === 'Insertion'
                    && revision.status === 'Pending'
                    && revision.author === userId
                    && revision.affectedRange
                    && revision.affectedRange.blockId === selection.blockId
                    && Number(revision.affectedRange.end || 0) === offset;
            });
            if (!candidate) return null;
            candidate.affectedRange.end = offset + asText(text).length;
            candidate.payload.text = asText(candidate.payload.text) + asText(text);
            buildIndexes(model);
            return candidate;
        }

        function insertText(selection, text) {
            const snapshot = createSelectionSnapshot(selection || {});
            const insertedText = asText(text);
            const block = findBlock(model, snapshot.blockId);
            let revision = null;
            const runId = stableId('inline', block.id + '-revision-insert-' + Date.now() + '-' + Math.floor(Math.random() * 1000));
            if (trackChanges) {
                revision = coalesceInsertionRevision(snapshot, insertedText) || createRevision('Insertion', {
                    blockId: snapshot.blockId,
                    start: snapshot.offset,
                    end: snapshot.offset + insertedText.length,
                }, { text: insertedText });
            }
            insertTextRun(block, snapshot.offset, insertedText, {
                id: runId,
                revisionId: (revision && revision.id) || null,
            });
            buildIndexes(model);
            const insertedRun = asArray(block.content && block.content.runs).find(function (run) { return run.id === runId; }) || { text: insertedText };
            return sortObject({
                ok: true,
                revisionId: (revision && revision.id) || '',
                insertedRun,
                selection: createSelectionSnapshot({ blockId: snapshot.blockId, offset: snapshot.offset + insertedText.length, isCollapsed: true }),
            });
        }

        function deleteRange(range) {
            const normalizedRange = normalizeRevisionRange(range);
            const block = findBlock(model, normalizedRange.blockId);
            const deletedText = blockText(block).slice(normalizedRange.start, normalizedRange.end);
            if (!trackChanges) {
                removeRangeText(model, normalizedRange);
                return sortObject({ ok: true, revisionId: '', deletedText, selection: createSelectionSnapshot({ blockId: normalizedRange.blockId, offset: normalizedRange.start }) });
            }
            const revision = createRevision('Deletion', normalizedRange, { text: deletedText });
            setRevisionForRange(model, revision.id, normalizedRange);
            return sortObject({ ok: true, revisionId: revision.id, deletedText, selection: createSelectionSnapshot({ blockId: normalizedRange.blockId, offset: normalizedRange.start }) });
        }

        function applyFormatChange(range, mark) {
            const normalizedRange = normalizeRevisionRange(range);
            if (!trackChanges) {
                applyRevisionMark(model, normalizedRange, mark);
                return sortObject({ ok: true, revisionId: '', selection: createSelectionSnapshot({ blockId: normalizedRange.blockId, offset: normalizedRange.end }) });
            }
            const revision = createRevision('FormatChange', normalizedRange, { mark: clone(mark || {}), decorativeStyle: { color: '#7c3aed', underline: true } });
            return sortObject({ ok: true, revisionId: revision.id, selection: createSelectionSnapshot({ blockId: normalizedRange.blockId, offset: normalizedRange.end }) });
        }

        function splitParagraph(selection) {
            return splitParagraphPreservingInlineMetadata(model, selection);
        }

        function getVisibleText(blockId, reviewMode) {
            const mode = String(reviewMode || 'showMarkup').toLowerCase();
            const block = findBlock(model, blockId);
            return asArray(block && block.content && block.content.runs).map(function (run) {
                const revisionId = run.revisionId || run.RevisionId || '';
                const revision = revisionId ? getRevisionById(model, revisionId) : null;
                if (revision && revision.type === 'Deletion' && mode === 'final') return '';
                if (revision && revision.type === 'Insertion' && mode === 'original') return '';
                return asText(run.text);
            }).join('');
        }

        function getActualFormattingState(selection) {
            const snapshot = createSelectionSnapshot(selection || {});
            const block = findBlock(model, snapshot.blockId);
            const runInfo = findRunAtOffset(block, snapshot.offset);
            const run = (runInfo && runInfo.run) || {};
            return sortObject({
                marks: clone(run.marks || []),
                style: clone(run.style || {}),
                revisionId: run.revisionId || run.RevisionId || null,
                fromRevisionDecoration: false,
            });
        }

        function createOverlayModel(reviewMode) {
            const mode = reviewMode || 'showMarkup';
            return sortObject({
                mode,
                layer: 'revision',
                zIndex: 12,
                markers: asArray(model.revisions).filter(function (revision) { return revision.status === 'Pending'; }).map(function (revision) {
                    return {
                        revisionId: revision.id,
                        type: revision.type,
                        range: clone(revision.affectedRange),
                        payload: clone(revision.payload),
                        decorativeStyle: revisionDecorativeStyle(revision),
                        source: 'revision-model',
                    };
                }),
            });
        }

        function renderOverlay(root, overlayModel) {
            if (!root) return { ok: false };
            const doc = globalThis.document;
            root.innerHTML = '';
            const overlay = markOverlayNonText(doc.createElement('div'));
            overlay.setAttribute('data-render-overlay', 'revision');
            overlay.className = 'tm-render-revision-overlay';
            overlay.style.position = 'relative';
            overlay.style.zIndex = String((overlayModel && overlayModel.zIndex) || 12);
            asArray(overlayModel && overlayModel.markers).forEach(function (marker) {
                const node = markOverlayNonText(doc.createElement('span'));
                node.className = 'tm-render-revision-marker tm-render-revision-marker--' + String(marker.type || '').toLowerCase();
                node.setAttribute('data-revision-overlay-id', marker.revisionId);
                node.setAttribute('data-revision-type', marker.type);
                node.setAttribute('data-model-block-id', (marker.range && marker.range.blockId) || '');
                node.style.zIndex = String((overlayModel && overlayModel.zIndex) || 12);
                node.textContent = '';
                overlay.appendChild(node);
            });
            root.appendChild(overlay);
            return sortObject({ ok: true, markerCount: asArray(overlayModel && overlayModel.markers).length });
        }

        function createReviewPopover(revisionId) {
            const revision = getRevisionById(model, revisionId);
            const type = revision ? revision.type : '';
            return sortObject({
                revision,
                role: 'dialog',
                ariaModal: false,
                ariaLabel: type ? ('Review ' + type + ' revision') : 'Review revision',
                title: type,
                author: revision ? revision.author : '',
                payload: revision ? revision.payload : null,
                actions: [
                    { id: 'accept', role: 'button', ariaLabel: 'Accept revision' },
                    { id: 'reject', role: 'button', ariaLabel: 'Reject revision' },
                ],
            });
        }

        function createMarkerDiffer(revisionIds) {
            const scopes = asArray(revisionIds).map(asText);
            return sortObject({ invalidatedOverlayScopes: scopes, invalidatedLayoutScopes: [], markerIds: scopes });
        }

        function acceptRevision(revisionId, selection) {
            const revision = getRevisionById(model, revisionId);
            if (!revision) return { ok: false, error: 'missing-revision', selection: createSelectionSnapshot(selection || {}) };
            if (revision.type === 'Insertion') clearRevisionFromRuns(model, revision.id);
            if (revision.type === 'Deletion') removeRevisionRuns(model, revision.id);
            if (revision.type === 'FormatChange') applyRevisionMark(model, revision.affectedRange, (revision.payload && revision.payload.mark) || {});
            updateRevisionStatus(model, revision.id, 'Accepted');
            return sortObject({ ok: true, revisionId: revision.id, status: 'Accepted', selection: createSelectionSnapshot(selection || { blockId: revision.affectedRange.blockId, offset: revision.affectedRange.start }) });
        }

        function rejectRevision(revisionId, selection) {
            const revision = getRevisionById(model, revisionId);
            if (!revision) return { ok: false, error: 'missing-revision', selection: createSelectionSnapshot(selection || {}) };
            if (revision.type === 'Insertion') removeRevisionRuns(model, revision.id);
            if (revision.type === 'Deletion') clearRevisionFromRuns(model, revision.id);
            updateRevisionStatus(model, revision.id, 'Rejected');
            return sortObject({ ok: true, revisionId: revision.id, status: 'Rejected', selection: createSelectionSnapshot(selection || { blockId: revision.affectedRange.blockId, offset: revision.affectedRange.start }) });
        }

        return {
            createRevision,
            insertText,
            deleteRange,
            applyFormatChange,
            splitParagraph,
            getVisibleText,
            getActualFormattingState,
            createOverlayModel,
            renderOverlay,
            createReviewPopover,
            createMarkerDiffer,
            acceptRevision,
            rejectRevision,
        };
    };
}
