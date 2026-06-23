import test from 'node:test';
import assert from 'node:assert/strict';
import {
    CANVAS_BLOCK_TYPES,
    CANVAS_RUN_TYPES,
    createCanvasDocumentModel,
} from '../canvas-document-model.mjs';

test('factory creates a section, page settings, empty paragraph, stable ids, and mark arrays', () => {
    const model = createCanvasDocumentModel({ documentId: 'empty-doc' });

    assert.equal(model.schemaVersion, 1);
    assert.equal(model.documentId, 'empty-doc');
    assert.equal(model.sections.length, 1);
    assert.equal(model.sections[0].id, 'section-1');
    assert.equal(model.body.blocks.length, 1);
    assert.equal(model.body.blocks[0].id, 'block-1');
    assert.equal(model.body.blocks[0].type, CANVAS_BLOCK_TYPES.paragraph);
    assert.equal(model.body.blocks[0].content.runs.length, 1);
    assert.equal(model.body.blocks[0].content.runs[0].type, CANVAS_RUN_TYPES.text);
    assert.deepEqual(model.body.blocks[0].content.runs[0].marks, []);
    assert.equal(model.pageSettings.width, 794);
    assert.equal(model.pageSettings.marginTop, 96);
});

test('factory preserves canonical sections, blocks, runs, marks, and preserve channels', () => {
    const model = createCanvasDocumentModel({
        documentId: 'rich-doc',
        version: 7,
        sections: [
            {
                id: 'section-main',
                order: 0,
                blocks: [
                    {
                        id: 'heading-1',
                        type: 'heading',
                        content: {
                            type: 'heading',
                            headingLevel: 2,
                            runs: [
                                {
                                    id: 'run-1',
                                    type: 'text',
                                    text: 'Heading',
                                    marks: [{ type: 'bold' }],
                                    preserve: { sourceJson: '{"id":"run-1"}' },
                                },
                            ],
                        },
                        preserve: { sourceJson: '{"id":"heading-1"}' },
                    },
                ],
            },
        ],
        body: {
            blocks: [
                {
                    id: 'paragraph-1',
                    type: 'paragraph',
                    order: 3,
                    content: {
                        type: 'paragraph',
                        runs: [
                            {
                                id: 'text-1',
                                type: 'text',
                                text: 'Body',
                                marks: [
                                    { type: 'bookmark', value: 'intro' },
                                    { type: 'revision', revisionId: 'rev-1' },
                                ],
                            },
                        ],
                    },
                },
            ],
        },
    });

    assert.equal(model.version, 7);
    assert.equal(model.sections[0].blocks[0].id, 'heading-1');
    assert.equal(model.sections[0].blocks[0].content.headingLevel, 2);
    assert.equal(model.sections[0].blocks[0].content.runs[0].preserve.sourceJson, '{"id":"run-1"}');
    assert.equal(model.body.blocks[0].id, 'paragraph-1');
    assert.equal(model.body.blocks[0].order, 3);
    assert.equal(model.body.blocks[0].content.runs[0].text, 'Body');
    assert.equal(model.body.blocks[0].content.runs[0].marks[0].type, 'bookmark');
    assert.equal(model.body.blocks[0].content.runs[0].marks[0].value, 'intro');
    assert.equal(model.body.blocks[0].content.runs[0].marks[1].revisionId, 'rev-1');
});

test('factory preserves structured math runs during model normalization', () => {
    const model = createCanvasDocumentModel({
        documentId: 'math-doc',
        body: {
            blocks: [
                {
                    id: 'math-p',
                    type: 'paragraph',
                    content: {
                        type: 'paragraph',
                        runs: [
                            {
                                id: 'math-run',
                                type: 'math',
                                math: {
                                    mathId: 'math-1',
                                    displayMode: 'inline',
                                    content: {
                                        elements: [
                                            {
                                                type: 'fraction',
                                                numerator: { elements: [{ type: 'run', text: 'a' }] },
                                                denominator: { elements: [{ type: 'run', text: 'b' }] },
                                            },
                                            {
                                                type: 'matrix',
                                                rows: [
                                                    { cells: [{ elements: [{ type: 'run', text: '1' }] }] },
                                                ],
                                            },
                                        ],
                                    },
                                },
                            },
                        ],
                    },
                },
            ],
        },
    });

    const run = model.body.blocks[0].content.runs[0];

    assert.equal(run.type, CANVAS_RUN_TYPES.math);
    assert.equal(run.math.mathId, 'math-1');
    assert.equal(run.math.content.elements[0].type, 'fraction');
    assert.equal(run.math.content.elements[1].type, 'matrix');
});

test('factory preserves numbering definitions list styles and extended list metadata', () => {
    const model = createCanvasDocumentModel({
        documentId: 'numbering-doc',
        numberingDefinitions: [{ id: 'legal', abstractId: 'legal-abstract', levels: [{ level: 0, format: 'decimal', text: '%1.' }] }],
        listStyles: [{ id: 'legal-style', numberingId: 'legal' }],
        body: {
            blocks: [
                {
                    id: 'clause-1-1',
                    type: 'list',
                    content: {
                        type: 'list',
                        list: {
                            ordered: true,
                            indentLevel: 1,
                            startNumber: 3,
                            numberingId: 'legal',
                            abstractNumberingId: 'legal-abstract',
                            listStyleId: 'legal-style',
                            numberFormat: 'legal',
                            levelText: '%1.%2.',
                            suffix: 'tab',
                            labelIndent: 24,
                            hangingIndent: 18,
                            restartNumbering: true,
                            continueNumbering: false,
                            numberingValue: 3,
                        },
                        runs: [{ id: 'run-1', type: 'text', text: 'Clause', marks: [] }],
                    },
                },
            ],
        },
    });

    assert.equal(model.numberingDefinitions.length, 1);
    assert.equal(model.listStyles.length, 1);
    assert.equal(model.body.blocks[0].content.list.indentLevel, 1);
    assert.equal(model.body.blocks[0].content.list.numberingId, 'legal');
    assert.equal(model.body.blocks[0].content.list.listStyleId, 'legal-style');
    assert.equal(model.body.blocks[0].content.list.levelText, '%1.%2.');
    assert.equal(model.body.blocks[0].content.list.restartNumbering, true);
    assert.equal(model.body.blocks[0].content.list.numberingValue, 3);
});

test('factory preserves document styles with id name inheritance and format bags', () => {
    const model = createCanvasDocumentModel({
        documentId: 'styles-doc',
        styles: [
            {
                id: 'contract-body',
                name: 'Contract Body',
                type: 'paragraph',
                basedOn: 'normal',
                next: 'contract-body',
                isQuickStyle: true,
                isPrimary: false,
                paragraphFormat: { spacingAfter: 12 },
                characterFormat: { fontSize: 10, fontFamily: 'Aptos' },
            },
        ],
    });

    assert.equal(model.styles.length, 1);
    assert.equal(model.styles[0].id, 'contract-body');
    assert.equal(model.styles[0].name, 'Contract Body');
    assert.equal(model.styles[0].basedOn, 'normal');
    assert.equal(model.styles[0].paragraphFormat.spacingAfter, 12);
    assert.equal(model.styles[0].characterFormat.fontFamily, 'Aptos');
});

test('factory preserves document hyphenation and page background options', () => {
    const model = createCanvasDocumentModel({
        documentId: 'phase-e12-options',
        hyphenation: {
            enabled: true,
            mode: 'manual',
            consecutiveLimit: 2,
            minPrefix: 3,
            minSuffix: 3,
            zone: 24,
        },
        pageBackground: {
            color: '#f8fafc',
            watermark: {
                enabled: true,
                kind: 'text',
                text: 'E12',
                color: 'rgba(37, 99, 235, 0.46)',
                opacity: 0.18,
                rotation: -32,
            },
            border: {
                enabled: true,
                color: '#2563eb',
                width: 2,
                margin: 18,
                alignTo: 'page',
                dash: [8, 4],
            },
        },
    });

    assert.equal(model.hyphenation.enabled, true);
    assert.equal(model.hyphenation.mode, 'manual');
    assert.equal(model.pageBackground.color, '#f8fafc');
    assert.equal(model.pageBackground.watermark.text, 'E12');
    assert.equal(model.pageBackground.border.color, '#2563eb');
    assert.deepEqual(model.pageBackground.border.dash, [8, 4]);
});
