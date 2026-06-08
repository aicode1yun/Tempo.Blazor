import assert from 'node:assert/strict';
import test from 'node:test';
import { createAccessibilityMirror, readingOrderBlocks } from '../accessibility-mirror.mjs';
import { createCanvasLiveRegion } from '../live-region.mjs';

test('accessibility mirror renders logical reading order, headings, bidi, tables, comments and revisions', () => {
    const document = createFakeDocument();
    const mirror = createAccessibilityMirror({ document, ariaLabel: 'Canvas surface' });
    const model = {
        comments: [{ id: 'comment-1' }],
        revisions: [{ id: 'revision-1' }],
        body: {
            blocks: [
                paragraph('p2', 2, 'Second paragraph'),
                {
                    id: 'heading',
                    type: 'heading',
                    order: 1,
                    content: {
                        type: 'heading',
                        headingLevel: 2,
                        runs: [{ id: 'h-run', type: 'text', text: 'Logical heading', marks: [] }],
                    },
                },
                {
                    id: 'rtl',
                    type: 'paragraph',
                    order: 3,
                    direction: 'rtl',
                    content: {
                        runs: [{
                            id: 'rtl-run',
                            type: 'text',
                            text: 'שלום',
                            marks: [
                                { type: 'commentAnchor', commentAnchor: { commentId: 'comment-1' } },
                                { type: 'revision', revisionId: 'revision-1', value: 'Insertion' },
                            ],
                        }],
                    },
                },
                {
                    id: 'table',
                    type: 'table',
                    order: 4,
                    content: {
                        table: {
                            rows: [{
                                id: 'row-1',
                                cells: [{
                                    id: 'cell-1',
                                    isHeader: true,
                                    blocks: [paragraph('cell-p', 1, 'Header cell')],
                                }],
                            }],
                        },
                    },
                },
            ],
        },
    };

    mirror.update(model);

    assert.equal(mirror.root.getAttribute('role'), 'document');
    assert.equal(mirror.root.getAttribute('id'), 'document-canvas-a11y-mirror');
    assert.equal(mirror.root.getAttribute('aria-label'), 'Canvas surface');
    assert.equal(mirror.root.getAttribute('data-canvas-a11y-comment-count'), '1');
    assert.equal(mirror.root.getAttribute('data-canvas-a11y-revision-count'), '1');
    assert.deepEqual(mirror.root.children.map(child => child.getAttribute('data-block-id')), ['heading', 'p2', 'rtl', 'table']);

    const heading = findOne(mirror.root, node => node.getAttribute('data-block-id') === 'heading');
    assert.equal(heading.tagName, 'H2');
    assert.equal(heading.getAttribute('role'), 'heading');
    assert.equal(heading.getAttribute('aria-level'), '2');

    const rtl = findOne(mirror.root, node => node.getAttribute('data-block-id') === 'rtl');
    assert.equal(rtl.getAttribute('dir'), 'rtl');
    assert.equal(rtl.getAttribute('data-canvas-a11y-comment-ids'), 'comment-1');
    assert.equal(rtl.getAttribute('data-canvas-a11y-revision-ids'), 'revision-1');
    const revisionRun = findOne(rtl, node => node.getAttribute('data-canvas-a11y-revision-id') === 'revision-1');
    assert.equal(revisionRun.getAttribute('data-canvas-a11y-revision-kind'), 'insertion');

    const table = findOne(mirror.root, node => node.getAttribute('data-canvas-a11y-table') === 'true');
    assert.equal(table.getAttribute('role'), 'table');
    assert.equal(table.getAttribute('aria-rowcount'), '1');
    assert.equal(table.getAttribute('aria-colcount'), '1');
    assert.equal(findOne(table, node => node.getAttribute('data-cell-id') === 'cell-1').getAttribute('role'), 'columnheader');
    assert.equal(textOf(table), 'Header cell');
});

test('readingOrderBlocks sorts ordered blocks without reordering unordered models', () => {
    const unordered = {
        body: {
            blocks: [
                paragraph('first', undefined, 'First'),
                paragraph('second', undefined, 'Second'),
            ],
        },
    };
    const ordered = {
        body: {
            blocks: [
                paragraph('b', 20, 'B'),
                paragraph('a', 10, 'A'),
            ],
        },
    };

    assert.deepEqual(readingOrderBlocks(unordered).map(block => block.id), ['first', 'second']);
    assert.deepEqual(readingOrderBlocks(ordered).map(block => block.id), ['a', 'b']);
});

test('accessibility mirror renders nested content control blocks in reading order', () => {
    const document = createFakeDocument();
    const mirror = createAccessibilityMirror({ document });
    const model = {
        body: {
            blocks: [
                paragraph('before', 1, 'Before'),
                {
                    id: 'addresses',
                    type: 'contentControl',
                    order: 2,
                    content: {
                        type: 'contentControl',
                        contentControl: {
                            control: {
                                controlId: 'canvas-form-addresses',
                                kind: 'repeatingSection',
                            },
                            blocks: [
                                paragraph('address-1', 1, 'Billing address: Prague'),
                                paragraph('address-2', 2, 'Shipping address: 1 Infinite Loop'),
                            ],
                        },
                    },
                },
                paragraph('after', 3, 'After'),
            ],
        },
    };

    mirror.update(model);

    const group = findOne(mirror.root, node => node.getAttribute('data-canvas-a11y-content-control') === 'true');
    assert.equal(group.getAttribute('role'), 'group');
    assert.equal(group.getAttribute('data-control-id'), 'canvas-form-addresses');
    assert.equal(group.getAttribute('data-control-kind'), 'repeatingsection');
    assert.equal(textOf(mirror.root), 'BeforeBilling address: PragueShipping address: 1 Infinite LoopAfter');
});

test('accessibility mirror exposes drawing objects with role, kind and alt warning metadata', () => {
    const document = createFakeDocument();
    const mirror = createAccessibilityMirror({ document });
    const model = {
        body: {
            blocks: [
                {
                    id: 'drawing-p',
                    type: 'paragraph',
                    order: 1,
                    content: {
                        runs: [
                            {
                                id: 'chart-run',
                                type: 'drawing',
                                drawing: {
                                    objectId: 'chart-1',
                                    kind: 5,
                                    altText: '',
                                    caption: 'Quarterly chart',
                                },
                            },
                            {
                                id: 'decorative-run',
                                type: 'drawing',
                                drawing: {
                                    objectId: 'shape-1',
                                    kind: 'shape',
                                    isDecorative: true,
                                },
                            },
                        ],
                    },
                },
            ],
        },
    };

    mirror.update(model);

    const chart = findOne(mirror.root, node => node.getAttribute('data-run-id') === 'chart-run');
    assert.equal(chart.getAttribute('role'), 'img');
    assert.equal(chart.getAttribute('data-canvas-a11y-drawing'), 'true');
    assert.equal(chart.getAttribute('data-drawing-kind'), 'chart');
    assert.equal(chart.getAttribute('aria-label'), 'Quarterly chart');
    assert.equal(chart.getAttribute('data-canvas-a11y-alt-warning'), 'true');

    const decorative = findOne(mirror.root, node => node.getAttribute('data-run-id') === 'decorative-run');
    assert.equal(decorative.getAttribute('data-drawing-kind'), 'shape');
    assert.equal(decorative.getAttribute('aria-label'), '');
    assert.equal(decorative.getAttribute('data-canvas-a11y-alt-warning'), 'false');
});

test('canvas live region announces localized caret, find, comment, revision, save and math slot messages', () => {
    const document = createFakeDocument();
    const live = createCanvasLiveRegion({
        document,
        ariaLabel: 'Canvas status',
        messages: {
            caretAnnouncement: 'Caret {0}:{1}',
            searchResultAnnouncement: 'Match {0} of {1}',
            searchNoResultsAnnouncement: 'No results for {0}',
            commentAnnouncement: 'Comment {0}',
            revisionAnnouncement: 'Revision {0}',
            saveAnnouncement: 'Saved',
            mathSlotAnnouncement: 'Equation slot {0}, offset {1}',
            mathExitAnnouncement: 'Exited equation editing',
        },
    });

    assert.equal(live.root.getAttribute('role'), 'status');
    assert.equal(live.root.getAttribute('id'), 'document-canvas-live-region');
    assert.equal(live.root.getAttribute('aria-live'), 'polite');
    live.announceSelection({ focus: { blockId: 'p1', offset: 7 } });
    assert.equal(live.root.textContent, 'Caret p1:7');
    live.announceSearch({ query: 'alpha', activeIndex: 1, matches: [{}, {}, {}] });
    assert.equal(live.root.textContent, 'Match 2 of 3');
    live.announceSearch({ query: 'omega', matches: [] });
    assert.equal(live.root.textContent, 'No results for omega');
    live.announceComment('comment-1');
    assert.equal(live.root.textContent, 'Comment comment-1');
    live.announceRevision('revision-1');
    assert.equal(live.root.textContent, 'Revision revision-1');
    live.announceSaved();
    assert.equal(live.root.textContent, 'Saved');
    live.announceMathSlot({ mathId: 'math-1', slotName: 'numerator', offset: 2 });
    assert.equal(live.root.textContent, 'Equation slot numerator, offset 2');
    assert.equal(live.root.getAttribute('data-canvas-live-kind'), 'math');
    live.announceMathSlot({ mathId: 'math-1', slotName: 'equation', exit: true });
    assert.equal(live.root.textContent, 'Exited equation editing');
    assert.equal(live.root.getAttribute('data-canvas-live-exit'), 'true');
});

function paragraph(id, order, text) {
    return {
        id,
        type: 'paragraph',
        order,
        content: { runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
    };
}

function createFakeDocument() {
    return {
        createElement(tagName) {
            return new FakeElement(String(tagName).toUpperCase());
        },
        createTextNode(text) {
            return new FakeTextNode(String(text || ''));
        },
    };
}

class FakeElement {
    constructor(tagName) {
        this.tagName = tagName;
        this.children = [];
        this.attributes = new Map();
        this.style = {};
        this.parentNode = null;
        this.className = '';
        this._textContent = '';
    }

    appendChild(child) {
        child.parentNode = this;
        this.children.push(child);
        return child;
    }

    replaceChildren(...children) {
        this.children = [];
        this._textContent = '';
        for (const child of children) {
            this.appendChild(child);
        }
    }

    setAttribute(name, value) {
        this.attributes.set(String(name), String(value));
    }

    getAttribute(name) {
        return this.attributes.get(String(name)) ?? null;
    }

    set textContent(value) {
        this.children = [];
        this._textContent = String(value || '');
    }

    get textContent() {
        return `${this._textContent}${this.children.map(child => child.textContent || '').join('')}`;
    }
}

class FakeTextNode {
    constructor(text) {
        this.textContent = text;
        this.children = [];
        this.parentNode = null;
    }

    getAttribute() {
        return null;
    }
}

function findOne(root, predicate) {
    const result = findAll(root, predicate)[0];
    assert.ok(result, 'Expected a matching fake DOM node.');
    return result;
}

function findAll(root, predicate) {
    const results = [];
    visit(root);
    return results;

    function visit(node) {
        if (predicate(node)) {
            results.push(node);
        }

        for (const child of node.children || []) {
            visit(child);
        }
    }
}

function textOf(node) {
    return node.textContent || '';
}
