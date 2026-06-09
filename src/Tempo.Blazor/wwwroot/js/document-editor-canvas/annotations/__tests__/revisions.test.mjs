import assert from 'node:assert/strict';
import test from 'node:test';
import { canCreateRestrictedSuggestion, canEditRestrictedSelection } from '../restricted-editing.mjs';
import { applyReviewDecision, applyReviewDecisionAll, buildRevisionMarkers } from '../revision-render.mjs';
import { applyDeletionRevision, applyFormattingRevision } from '../track-changes.mjs';

test('revision render emits insertion deletion and formatting markers with review modes', () => {
    const model = createModel();
    const render = createRender();

    const all = buildRevisionMarkers(model, render, { reviewMode: 'allMarkup' });
    const simple = buildRevisionMarkers(model, render, { reviewMode: 'simpleMarkup' });
    const final = buildRevisionMarkers(model, render, { reviewMode: 'noMarkup' });
    const original = buildRevisionMarkers(model, render, { reviewMode: 'original' });

    assert.deepEqual(all.map(marker => marker.type), ['insertion', 'deletion', 'formatting']);
    assert.equal(simple.every(marker => marker.rect.width <= 4), true);
    assert.equal(final.length, 0);
    assert.deepEqual(original.map(marker => marker.type), ['deletion', 'formatting']);
});

test('accept and reject one revision update model content and pending action', () => {
    const rejectedInsertion = applyReviewDecision(createModel(), 'rev-insert', 'rejected');
    const acceptedDeletion = applyReviewDecision(createModel(), 'rev-delete', 'accepted');

    assert.equal(rejectedInsertion.changed, true);
    assert.equal(blockText(rejectedInsertion.model, 'p1').includes('New'), false);
    assert.equal(rejectedInsertion.model.revisions.find(revision => revision.id === 'rev-insert').action, 'rejected');

    assert.equal(acceptedDeletion.changed, true);
    assert.equal(blockText(acceptedDeletion.model, 'p1').includes('Old'), false);
    assert.equal(acceptedDeletion.model.revisions.find(revision => revision.id === 'rev-delete').action, 'accepted');
});

test('reviewing all revisions renders no markers, even when stale revision anchors remain', () => {
    const reviewed = applyReviewDecisionAll(createModel(), 'rejected', {});

    assert.equal(reviewed.changed, true);
    assert.equal(reviewed.model.revisions.every(revision => String(revision.action).toLowerCase() !== 'pending'), true);
    // A marker must only ever represent a PENDING revision. The display list can still carry revisionAnchor
    // commands for the just-reviewed revisions until the next relayout, so building markers against them must
    // not resurrect a marker for a revision that is no longer pending (Phase17 reject-all marker bug).
    const markers = buildRevisionMarkers(reviewed.model, createRender(), { reviewMode: 'allMarkup' });
    assert.equal(markers.length, 0, `expected no markers after reviewing all revisions, got ${markers.length}`);
});

test('track deletion keeps cross-block text visible with deletion revisions', () => {
    const model = createPlainModel();
    const result = applyDeletionRevision(model, {
        anchor: { blockId: 'a', offset: 2 },
        focus: { blockId: 'b', offset: 3 },
    }, { author: { id: 'u1', displayName: 'Author' } });

    assert.equal(result.changed, true);
    assert.equal(result.revisions.length, 2);
    assert.equal(model.body.blocks[0].content.runs.some(run => hasRevision(run, 'Deletion')), true);
    assert.equal(model.body.blocks[1].content.runs.some(run => hasRevision(run, 'Deletion')), true);
});

test('formatting tracked change creates formatting revision on selected range', () => {
    const model = createPlainModel();
    const result = applyFormattingRevision(model, {
        anchor: { blockId: 'a', offset: 1 },
        focus: { blockId: 'a', offset: 4 },
    }, 'bold');

    assert.equal(result.changed, true);
    assert.equal(model.revisions[0].type, 'Formatting');
    assert.equal(model.body.blocks[0].content.runs.some(run => hasRevision(run, 'Formatting')), true);
});

test('restricted editing allows only configured editable regions in protected documents', () => {
    const model = {
        isProtected: true,
        restrictedMarkers: [{
            id: 'region-1',
            startBlockId: 'a',
            startOffset: 2,
            endBlockId: 'a',
            endOffset: 6,
        }],
    };

    assert.equal(canEditRestrictedSelection(model, {
        anchor: { blockId: 'a', offset: 3 },
        focus: { blockId: 'a', offset: 5 },
    }).allowed, true);
    assert.equal(canEditRestrictedSelection(model, {
        anchor: { blockId: 'a', offset: 0 },
        focus: { blockId: 'a', offset: 5 },
    }).allowed, false);
});

test('restricted editing blocks suggestion provider changes outside editable regions', () => {
    const model = {
        isProtected: true,
        restrictedMarkers: [{
            id: 'region-1',
            startBlockId: 'a',
            startOffset: 2,
            endBlockId: 'a',
            endOffset: 6,
        }],
    };

    const allowed = canCreateRestrictedSuggestion(model, {
        range: { blockId: 'a', startOffset: 3, endOffset: 5 },
        operations: [{ target: { blockId: 'a', offset: 3, length: 2 } }],
    });
    const blocked = canCreateRestrictedSuggestion(model, {
        range: { blockId: 'a', startOffset: 0, endOffset: 5 },
        operations: [{ target: { blockId: 'a', offset: 0, length: 5 } }],
    });

    assert.equal(allowed.allowed, true);
    assert.equal(allowed.markerId, 'region-1');
    assert.equal(blocked.allowed, false);
    assert.equal(blocked.reason, 'outsideEditableRegion');
});

function createModel() {
    return {
        revisions: [
            { id: 'rev-insert', type: 'Insertion', action: 'Pending', range: { blockId: 'p1', startOffset: 0, endOffset: 3 } },
            { id: 'rev-delete', type: 'Deletion', action: 'Pending', range: { blockId: 'p1', startOffset: 3, endOffset: 6 } },
            { id: 'rev-format', type: 'Formatting', action: 'Pending', range: { blockId: 'p1', startOffset: 6, endOffset: 10 } },
        ],
        body: {
            blocks: [{
                id: 'p1',
                type: 'paragraph',
                content: {
                    runs: [
                        { id: 'insert', type: 'text', text: 'New', marks: [{ type: 'revision', revisionId: 'rev-insert', value: 'Insertion' }] },
                        { id: 'delete', type: 'text', text: 'Old', marks: [{ type: 'revision', revisionId: 'rev-delete', value: 'Deletion' }] },
                        { id: 'format', type: 'text', text: 'Bold', marks: [{ type: 'bold' }, { type: 'revision', revisionId: 'rev-format', value: 'Formatting' }] },
                    ],
                },
            }],
        },
    };
}

function createPlainModel() {
    return {
        revisions: [],
        body: {
            blocks: [
                { id: 'a', type: 'paragraph', content: { runs: [{ id: 'a-run', type: 'text', text: 'Alpha', marks: [] }] } },
                { id: 'b', type: 'paragraph', content: { runs: [{ id: 'b-run', type: 'text', text: 'Beta', marks: [] }] } },
            ],
        },
    };
}

function createRender() {
    return {
        displayList: {
            commands: [
                { type: 'revisionAnchor', revisionId: 'rev-insert', blockId: 'p1', pageIndex: 0, x: 100, y: 100, width: 28, height: 18 },
                { type: 'revisionAnchor', revisionId: 'rev-delete', blockId: 'p1', pageIndex: 0, x: 128, y: 100, width: 24, height: 18 },
                { type: 'revisionAnchor', revisionId: 'rev-format', blockId: 'p1', pageIndex: 0, x: 152, y: 100, width: 36, height: 18 },
            ],
        },
    };
}

function blockText(model, blockId) {
    return model.body.blocks.find(block => block.id === blockId).content.runs.map(run => run.text || '').join('');
}

function hasRevision(run, value) {
    return run.marks.some(mark => mark.type === 'revision' && mark.value === value);
}
