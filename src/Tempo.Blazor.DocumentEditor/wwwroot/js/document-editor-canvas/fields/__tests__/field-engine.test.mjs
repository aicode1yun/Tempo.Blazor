import assert from 'node:assert/strict';
import test from 'node:test';
import { FIELD_TYPES, collectReferenceTargets, updateAllFields } from '../field-engine.mjs';

test('field engine updates instruction text cached result and document context fields', () => {
    const model = {
        documentId: 'phase-e5-fields',
        version: 7,
        metadata: {
            title: 'Generated field contract',
            author: { displayName: 'Ada Lovelace' },
        },
        body: {
            blocks: [
                heading('heading-1', 'Master Services Agreement', 10),
                paragraph('field-block', 20, [
                    field('page', FIELD_TYPES.pageNumber),
                    text('separator-a', ' '),
                    field('pages', FIELD_TYPES.pageCount),
                    text('separator-b', ' '),
                    field('date', FIELD_TYPES.date, { format: 'iso' }),
                    text('separator-c', ' '),
                    field('time', FIELD_TYPES.time),
                    text('separator-d', ' '),
                    field('file', FIELD_TYPES.fileName),
                    text('separator-e', ' '),
                    field('author', FIELD_TYPES.author),
                    text('separator-f', ' '),
                    field('style', FIELD_TYPES.styleRef, { targetId: 'Heading 1' }),
                ]),
            ],
        },
    };

    const result = updateAllFields(model, {
        now: '2026-06-04T09:15:00Z',
        fileName: 'agreement.docx',
        layout: {
            pages: [{ index: 0 }, { index: 1 }, { index: 2 }, { index: 3 }],
            blocks: [{ blockId: 'field-block', pageIndex: 2 }],
        },
    });

    assert.equal(result.changed, true);
    const values = Object.fromEntries(model.body.blocks[1].content.runs
        .filter(run => run.type === 'field')
        .map(run => [run.id, run.field.displayText]));
    assert.equal(values.page, '3');
    assert.equal(values.pages, '4');
    assert.equal(values.date, '2026-06-04');
    assert.match(values.time, /15/);
    assert.equal(values.file, 'agreement.docx');
    assert.equal(values.author, 'Ada Lovelace');
    assert.equal(values.style, 'Master Services Agreement');
    for (const run of model.body.blocks[1].content.runs.filter(item => item.type === 'field')) {
        assert.equal(run.field.cachedResult, run.field.displayText);
        assert.ok(run.field.instrText);
    }
});

test('field engine collects heading bookmark caption and numbered item reference targets', () => {
    const model = {
        body: {
            blocks: [
                heading('heading-target', 'Heading target', 10),
                paragraph('bookmark-target', 20, [
                    { id: 'bookmark-run', type: 'text', text: 'Bookmark target', marks: [{ type: 'bookmark', value: 'bookmark-a' }] },
                ]),
                {
                    id: 'caption-block',
                    type: 'paragraph',
                    order: 30,
                    content: {
                        type: 'paragraph',
                        caption: { id: 'caption-a', kind: 'figure', label: 'Figure', text: 'System view', number: 2, numberLabel: 'Figure 2' },
                        runs: [field('caption-seq', FIELD_TYPES.seq, { targetId: 'caption-a', sequenceId: 'figure', sequenceLabel: 'Figure' }), text('caption-text', ' System view')],
                    },
                },
                {
                    id: 'numbered-target',
                    type: 'list',
                    order: 40,
                    content: {
                        type: 'list',
                        list: { ordered: true, startNumber: 4 },
                        runs: [text('numbered-run', 'Numbered target')],
                    },
                },
            ],
        },
    };

    const targets = collectReferenceTargets(model);

    assert.equal(targets.get('heading-target').kind, 'heading');
    assert.equal(targets.get('bookmark-a').kind, 'bookmark');
    assert.equal(targets.get('caption-a').kind, 'caption');
    assert.equal(targets.get('caption-a').numberLabel, 'Figure 2');
    assert.equal(targets.get('numbered-target').kind, 'numberedItem');
    assert.equal(targets.get('numbered-target').numberLabel, '4.');
});

function heading(id, value, order) {
    return {
        id,
        type: 'heading',
        order,
        content: { type: 'heading', headingLevel: 1, runs: [text(`${id}-run`, value)] },
    };
}

function paragraph(id, order, runs) {
    return {
        id,
        type: 'paragraph',
        order,
        content: { type: 'paragraph', runs },
    };
}

function text(id, value) {
    return { id, type: 'text', text: value, marks: [] };
}

function field(id, fieldType, overrides = {}) {
    return {
        id,
        type: 'field',
        text: '',
        marks: [],
        field: {
            fieldType,
            fallbackText: '',
            ...overrides,
        },
    };
}
