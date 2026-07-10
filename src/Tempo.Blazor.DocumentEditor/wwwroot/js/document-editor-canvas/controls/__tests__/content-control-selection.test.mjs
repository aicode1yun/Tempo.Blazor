import assert from 'node:assert/strict';
import test from 'node:test';
import { findContentControlAtSelection } from '../content-control-selection.mjs';

// Phase N2 (canvas perf 2026-07-10): the content-control popover state must come from the engine's
// selection state — an O(focused block) walk — instead of a full-document marshal into C#. The
// locator mirrors the semantics of the removed C# FindCanvasContentControlAtSelection: the caret is
// "on" the control when focusOffset falls inside the control's display-text span (or exactly on a
// zero-length control), and only date/comboBox/dropDown/picture kinds open the popover.

function dateControlRun(overrides = {}) {
    return {
        id: 'run-cc',
        type: 'contentControl',
        contentControl: {
            control: {
                controlId: 'cc-date-1',
                kind: 'date',
                alias: 'Delivery date',
                tag: 'delivery',
                isRequired: true,
                lockContent: false,
                placeholderText: 'Pick a date',
                value: { dateIso: '2026-07-10', text: '' },
                items: [],
                ...overrides,
            },
            runs: [],
        },
    };
}

function model(blocks) {
    return { body: { blocks } };
}

function paragraph(id, runs) {
    return { id, type: 'paragraph', content: { type: 'paragraph', runs } };
}

const textRun = (id, text) => ({ id, type: 'text', text, marks: [] });

test('caret inside a date control display text returns the popover payload', () => {
    const blocks = [paragraph('p1', [textRun('r1', 'Date: '), dateControlRun()])];
    // 'Date: ' has length 6; display text '2026-07-10' spans offsets 6..16.
    const found = findContentControlAtSelection(model(blocks), { focus: { blockId: 'p1', offset: 8 } });

    assert.ok(found, 'control under the caret must be found');
    assert.equal(found.controlId, 'cc-date-1');
    assert.equal(found.kind, 'date');
    assert.equal(found.title, 'Delivery date', 'title prefers alias');
    assert.equal(found.isRequired, true);
    assert.equal(found.lockContent, false);
    assert.equal(found.dateIso, '2026-07-10');
});

test('caret outside the control span returns null', () => {
    const blocks = [paragraph('p1', [textRun('r1', 'Date: '), dateControlRun(), textRun('r2', ' end')])];
    assert.equal(findContentControlAtSelection(model(blocks), { focus: { blockId: 'p1', offset: 2 } }), null);
    assert.equal(findContentControlAtSelection(model(blocks), { focus: { blockId: 'p1', offset: 18 } }), null);
});

test('caret in a different block returns null', () => {
    const blocks = [
        paragraph('p1', [dateControlRun()]),
        paragraph('p2', [textRun('r1', 'plain')]),
    ];
    assert.equal(findContentControlAtSelection(model(blocks), { focus: { blockId: 'p2', offset: 1 } }), null);
});

test('dropdown control returns selectedValue and items with display texts', () => {
    const run = {
        id: 'run-dd',
        type: 'contentControl',
        contentControl: {
            control: {
                controlId: 'cc-dd-1',
                kind: 'dropDown',
                tag: 'country',
                value: { selectedValue: 'cz' },
                items: [
                    { value: 'cz', displayText: 'Czechia' },
                    { value: 'sk', displayText: 'Slovakia' },
                ],
            },
            runs: [],
        },
    };
    // Display text is the selected item's displayText: 'Czechia' (offsets 0..7).
    const found = findContentControlAtSelection(model([paragraph('p1', [run])]), { focus: { blockId: 'p1', offset: 3 } });

    assert.ok(found);
    assert.equal(found.kind, 'dropDown');
    assert.equal(found.title, 'country', 'title falls back to tag when alias is empty');
    assert.equal(found.selectedValue, 'cz');
    assert.deepEqual(found.items, [
        { value: 'cz', displayText: 'Czechia' },
        { value: 'sk', displayText: 'Slovakia' },
    ]);
});

test('picture control with empty display text matches exactly at its zero-length position', () => {
    const run = {
        id: 'run-pic',
        type: 'contentControl',
        contentControl: {
            control: { controlId: 'cc-pic-1', kind: 'picture', value: {}, items: [] },
            runs: [],
        },
    };
    const blocks = [paragraph('p1', [textRun('r1', 'ab'), run])];
    const found = findContentControlAtSelection(model(blocks), { focus: { blockId: 'p1', offset: 2 } });

    assert.ok(found, 'zero-length control at the caret offset must match');
    assert.equal(found.kind, 'picture');
    assert.equal(found.title, 'cc-pic-1', 'title falls back to controlId');
    assert.equal(found.assetId, '');
});

test('non-popover kinds (plainText, checkbox) are ignored', () => {
    const plain = {
        id: 'run-plain',
        type: 'contentControl',
        contentControl: {
            control: { controlId: 'cc-txt', kind: 'plainText', value: { text: 'hello' }, items: [] },
            runs: [],
        },
    };
    const found = findContentControlAtSelection(model([paragraph('p1', [plain])]), { focus: { blockId: 'p1', offset: 2 } });
    assert.equal(found, null);
});

test('control nested inside a table cell block is found', () => {
    const table = {
        id: 'tbl',
        type: 'table',
        content: {
            type: 'table',
            table: {
                rows: [{
                    id: 'r1',
                    cells: [{ id: 'c1', blocks: [paragraph('cell-p', [dateControlRun()])] }],
                }],
            },
        },
    };
    const found = findContentControlAtSelection(model([table]), { focus: { blockId: 'cell-p', offset: 0 } });
    assert.ok(found);
    assert.equal(found.controlId, 'cc-date-1');
});

test('selection without focus falls back to anchor; missing block ids return null', () => {
    const blocks = [paragraph('p1', [dateControlRun()])];
    const viaAnchor = findContentControlAtSelection(model(blocks), { anchor: { blockId: 'p1', offset: 1 } });
    assert.ok(viaAnchor, 'anchor is used when focus is absent');
    assert.equal(findContentControlAtSelection(model(blocks), {}), null);
    assert.equal(findContentControlAtSelection(null, { focus: { blockId: 'p1', offset: 0 } }), null);
});
