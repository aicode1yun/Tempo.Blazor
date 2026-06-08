import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../display-list.mjs';

test('display list is deterministic and separates page, content, objects, annotations, and diagnostics', () => {
    const model = createRenderModel();
    const layout = createLayout();

    const first = buildDisplayList(model, layout, { fontMetrics: createDeterministicMetrics() });
    const second = buildDisplayList(model, layout, { fontMetrics: createDeterministicMetrics() });
    const debug = buildDisplayList(model, layout, { fontMetrics: createDeterministicMetrics(), debug: true });

    assert.equal(JSON.stringify(first), JSON.stringify(second));
    assert.equal(first.pageCount, 1);
    assert.equal(first.diagnosticCount, 0);
    assert.ok(first.textRunCount >= 4);
    assert.equal(debug.diagnosticCount, 1);
    assert.equal(first.commands.filter(command => command.layer === 'diagnostics').length, 0);
    assert.ok(first.commands.some(command => command.type === 'pageFill' && command.layer === 'page-background'));
    assert.ok(first.commands.some(command => command.type === 'bodyArea' && command.layer === 'page-background'));
    assert.ok(first.commands.some(command => command.type === 'marginGuide' && command.layer === 'page-background'));
    assert.ok(first.commands.some(command => command.type === 'paragraphBox' && command.layer === 'content'));
    assert.ok(first.commands.some(command => command.type === 'textRun' && command.layer === 'content'));
    assert.ok(first.commands.some(command => command.type === 'glyphRun' && command.layer === 'content'));
    assert.ok(first.commands.some(command => command.type === 'field' && command.layer === 'content'));
    assert.ok(first.commands.some(command => command.type === 'imageObject' && command.layer === 'objects'));
    assert.ok(first.commands.some(command => command.type === 'tableBox' && command.layer === 'content'));
    assert.ok(first.commands.some(command => command.type === 'commentAnchor' && command.layer === 'annotations'));
    assert.ok(first.commands.some(command => command.type === 'revisionAnchor' && command.layer === 'annotations'));
});

test('text run commands carry basic marks into canvas style', () => {
    const displayList = buildDisplayList(createRenderModel(), createLayout(), { fontMetrics: createDeterministicMetrics() });
    const bold = displayList.commands.find(command => command.id === 'bold-run');
    const italic = displayList.commands.find(command => command.id === 'italic-run');
    const decorated = displayList.commands.find(command => command.id === 'decorated-run');

    assert.equal(bold.style.fontWeight, '700');
    assert.equal(italic.style.fontStyle, 'italic');
    assert.equal(decorated.style.color, '#1d4ed8');
    assert.equal(decorated.style.backgroundColor, '#fde68a');
    assert.deepEqual(decorated.style.decorations, ['underline', 'line-through']);
});

test('display list emits structured math equation commands with deterministic layout', () => {
    const model = createRenderModel();
    model.body.blocks[1].content.runs.splice(2, 0, {
        id: 'math-run',
        type: 'math',
        marks: [],
        math: {
            mathId: 'math-equation',
            displayMode: 0,
            content: {
                elements: [{
                    type: 'fraction',
                    numerator: { elements: [{ type: 'run', text: 'a', style: 'italic' }] },
                    denominator: { elements: [{ type: 'run', text: 'b', style: 'italic' }] },
                }],
            },
        },
    });

    const displayList = buildDisplayList(model, createLayout(), { fontMetrics: createDeterministicMetrics() });
    const math = displayList.commands.find(command => command.type === 'mathEquation');

    assert.equal(displayList.mathEquationCount, 1);
    assert.equal(math.id, 'math-run-math');
    assert.equal(math.mathId, 'math-equation');
    assert.equal(math.text, '(a)/(b)');
    assert.equal(math.layer, 'content');
    assert.ok(math.mathLayout.width > 0);
    assert.equal(displayList.commands.some(command => command.id === 'math-run'), false);
});

test('display list emits form control commands with validation metadata', () => {
    const model = createRenderModel();
    model.body.blocks[1].content.runs.push({
        id: 'customer-name-run',
        type: 'contentControl',
        marks: [],
        contentControl: {
            control: {
                controlId: 'customer-name',
                kind: 'plainText',
                scope: 'inline',
                placeholderText: 'Customer name',
                isRequired: true,
                lockContent: false,
                value: { text: '' },
                items: [],
            },
            runs: [],
        },
    });

    const displayList = buildDisplayList(model, createLayout(), { fontMetrics: createDeterministicMetrics() });
    const control = displayList.commands.find(command => command.type === 'formControl');
    const designDisplayList = buildDisplayList(model, createLayout(), {
        fontMetrics: createDeterministicMetrics(),
        contentControlRenderMode: 'design',
    });
    const designControl = designDisplayList.commands.find(command => command.type === 'formControl');

    assert.equal(displayList.contentControlCount, 1);
    assert.equal(control.id, 'customer-name-run');
    assert.equal(control.controlId, 'customer-name');
    assert.equal(control.text, 'Customer name');
    assert.equal(control.renderMode, 'form');
    assert.equal(control.renderState.showChrome, false);
    assert.equal(control.isPlaceholder, true);
    assert.equal(control.validation.valid, false);
    assert.equal(control.validation.reason, 'required');
    assert.ok(displayList.commands.some(command => command.id === 'customer-name-run-glyphs'));
    assert.equal(designControl.renderMode, 'design');
    assert.equal(designControl.renderState.showChrome, true);
    assert.equal(designControl.renderState.showTag, true);
    assert.equal(designControl.designTag, 'customer-name');
});

function createLayout() {
    return {
        pages: [
            {
                index: 0,
                width: 794,
                height: 1123,
                body: {
                    x: 96,
                    y: 96,
                    width: 602,
                    height: 931,
                },
            },
        ],
    };
}

function createRenderModel() {
    return {
        documentId: 'phase-5-render',
        theme: {
            bodyFontFamily: 'Aptos, Arial, sans-serif',
            bodyFontSize: 11,
            bodyLineHeight: 1.15,
            paragraphSpacingAfter: 8,
        },
        body: {
            blocks: [
                {
                    id: 'heading-1',
                    type: 'heading',
                    order: 1,
                    content: {
                        type: 'heading',
                        headingLevel: 1,
                        runs: [
                            { id: 'heading-run', type: 'text', text: 'Render pipeline', marks: [] },
                        ],
                    },
                },
                {
                    id: 'paragraph-1',
                    type: 'paragraph',
                    order: 2,
                    content: {
                        type: 'paragraph',
                        runs: [
                            { id: 'bold-run', type: 'text', text: 'Bold', marks: [{ type: 'bold' }] },
                            { id: 'italic-run', type: 'text', text: ' italic', marks: [{ type: 'italic' }] },
                            {
                                id: 'decorated-run',
                                type: 'text',
                                text: ' decorated',
                                marks: [
                                    { type: 'underline' },
                                    { type: 'strikethrough' },
                                    { type: 'highlight', value: '#fde68a' },
                                    { type: 'textColor', value: '#1d4ed8' },
                                    { type: 'commentAnchor', commentAnchor: { commentId: 'comment-1' } },
                                    { type: 'revision', revisionId: 'revision-1' },
                                ],
                            },
                            { id: 'field-run', type: 'field', field: { displayText: '1' }, marks: [] },
                        ],
                    },
                },
                {
                    id: 'table-1',
                    type: 'table',
                    order: 3,
                    content: {
                        type: 'table',
                        table: { rows: [{ cells: [] }, { cells: [] }] },
                    },
                },
                {
                    id: 'image-1',
                    type: 'image',
                    order: 4,
                    content: {
                        type: 'image',
                        image: { size: { width: 180, height: 96 } },
                    },
                },
            ],
        },
    };
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, text.length * fontSize * 0.55),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
