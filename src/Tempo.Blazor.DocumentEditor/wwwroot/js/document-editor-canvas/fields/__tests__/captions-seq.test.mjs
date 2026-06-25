import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';
import { createCaptionBlock, createTableOfFiguresField, renumberCaptions } from '../captions.mjs';
import { createCrossReferenceField, resolveCrossReferenceNavigation } from '../cross-reference.mjs';
import { FIELD_TYPES, updateAllFields } from '../field-engine.mjs';

test('caption SEQ fields renumber after insert and delete', () => {
    const model = baseModel();
    const first = createCaptionBlock({ id: 'caption-a', blockId: 'caption-block-a', kind: 'figure', label: 'Figure', text: 'Architecture' });
    const second = createCaptionBlock({ id: 'caption-b', blockId: 'caption-block-b', kind: 'figure', label: 'Figure', text: 'Runtime' });
    model.body.blocks.push(first, second);

    renumberCaptions(model);
    assert.equal(first.content.caption.numberLabel, 'Figure 1');
    assert.equal(second.content.caption.numberLabel, 'Figure 2');
    assert.equal(first.content.runs[0].field.cachedResult, 'Figure 1');
    assert.equal(second.content.runs[0].field.cachedResult, 'Figure 2');

    model.body.blocks = model.body.blocks.filter(block => block.id !== 'caption-block-a');
    renumberCaptions(model);

    assert.equal(second.content.caption.numberLabel, 'Figure 1');
    assert.equal(second.content.runs[0].field.displayText, 'Figure 1');
});

test('REF fields resolve captions headings bookmarks numbered items and navigation targets', () => {
    const model = baseModel();
    const caption = createCaptionBlock({ id: 'caption-a', blockId: 'caption-block-a', kind: 'figure', label: 'Figure', text: 'Architecture' });
    model.body.blocks.push(caption);
    renumberCaptions(model);
    const refs = [
        createCrossReferenceField({ id: 'heading-target', kind: 'heading' }, { targetId: 'heading-target', referenceFormat: 'text' }),
        createCrossReferenceField({ id: 'bookmark-a', kind: 'bookmark' }, { targetId: 'bookmark-a', referenceFormat: 'text' }),
        createCrossReferenceField({ id: 'numbered-target', kind: 'numberedItem' }, { targetId: 'numbered-target', referenceFormat: 'number' }),
        createCrossReferenceField({ id: 'caption-a', kind: 'caption' }, { targetId: 'caption-a', referenceFormat: 'full' }),
    ];
    model.body.blocks.push({
        id: 'refs',
        type: 'paragraph',
        order: 90,
        content: { type: 'paragraph', runs: refs },
    });

    updateAllFields(model);

    assert.equal(refs[0].field.displayText, 'Reference target heading');
    assert.equal(refs[1].field.displayText, 'Bookmark target');
    assert.equal(refs[2].field.displayText, '1.');
    assert.equal(refs[3].field.displayText, 'Figure 1 Architecture');
    assert.deepEqual(resolveCrossReferenceNavigation(model, refs[3]).selection.anchor, { blockId: 'caption-block-a', offset: 0 });

    model.body.blocks[0].content.runs[0].text = 'Updated reference target';
    updateAllFields(model);

    assert.equal(refs[0].field.displayText, 'Updated reference target');
});

test('table of figures bibliography and citation fields render generated text', () => {
    const model = baseModel();
    model.bibliographySources = [{
        id: 'source-a',
        author: 'Jane Smith',
        title: 'Reliable Canvas Editors',
        container: 'Tempo Review',
        year: 2026,
    }];
    model.body.blocks.push(createCaptionBlock({ id: 'caption-a', blockId: 'caption-block-a', kind: 'figure', label: 'Figure', text: 'Architecture' }));
    model.body.blocks.push({
        id: 'generated',
        type: 'paragraph',
        order: 100,
        content: {
            type: 'paragraph',
            runs: [
                createTableOfFiguresField({ kind: 'figure' }),
                field('bibliography', FIELD_TYPES.bibliography),
                field('citation', FIELD_TYPES.citation, { citationId: 'source-a' }),
            ],
        },
    });
    renumberCaptions(model);
    updateAllFields(model);

    const [tof, bibliography, citation] = model.body.blocks.at(-1).content.runs;
    assert.match(tof.field.displayText, /Figure 1 Architecture\s+1/);
    assert.equal(bibliography.field.displayText, 'Jane Smith (2026). Reliable Canvas Editors. Tempo Review');
    assert.equal(citation.field.displayText, '(Smith, 2026)');
});

test('field commands are undoable through the canvas command runtime', () => {
    let model = baseModel();
    let selection = { anchor: { blockId: 'intro', offset: 0 }, focus: { blockId: 'intro', offset: 0 } };
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const inserted = runtime.execCommand('insertCaption', { kind: 'figure', label: 'Figure', text: 'Runtime proof' });
    assert.equal(inserted.handled, true);
    assert.equal(model.body.blocks.filter(block => block.content?.caption?.id).length, 1);

    const undone = runtime.execCommand('undo');
    assert.equal(undone.handled, true);
    assert.equal(model.body.blocks.filter(block => block.content?.caption?.id).length, 0);
});

function baseModel() {
    return {
        documentId: 'phase-e5-captions',
        body: {
            blocks: [
                {
                    id: 'heading-target',
                    type: 'heading',
                    order: 10,
                    content: { type: 'heading', headingLevel: 1, runs: [text('heading-run', 'Reference target heading')] },
                },
                {
                    id: 'intro',
                    type: 'paragraph',
                    order: 20,
                    content: { type: 'paragraph', runs: [text('intro-run', 'Bookmark target', [{ type: 'bookmark', value: 'bookmark-a' }])] },
                },
                {
                    id: 'numbered-target',
                    type: 'list',
                    order: 30,
                    content: { type: 'list', list: { ordered: true, startNumber: 1 }, runs: [text('numbered-run', 'Numbered target')] },
                },
            ],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function text(id, value, marks = []) {
    return { id, type: 'text', text: value, marks };
}

function field(id, fieldType, overrides = {}) {
    return {
        id,
        type: 'field',
        text: '',
        marks: [],
        field: { fieldType, fallbackText: '', ...overrides },
    };
}
