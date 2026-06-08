import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../dispatcher.mjs';
import { applyParagraphCommand, createParagraphCommandState, queryParagraphCommandState } from '../paragraph-commands.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';

test('paragraph command state tolerates missing history snapshots', () => {
    assert.deepEqual(createParagraphCommandState(null), {
        showRuler: true,
        showBlocks: false,
        showNonPrintingCharacters: false,
    });
    assert.deepEqual(createParagraphCommandState(undefined), {
        showRuler: true,
        showBlocks: false,
        showNonPrintingCharacters: false,
    });
});

test('paragraph commands apply alignment spacing and indent to selected blocks', () => {
    let state = createParagraphCommandState();
    let model = createModel();
    const selection = range('body-1', 'body-1');

    let result = applyParagraphCommand(model, selection, 'align', 'center', state);
    assert.equal(result.changed, true);
    assert.equal(result.model.body.blocks[0].paragraphProperties.alignment, 1);
    model = result.model;
    state = result.state;

    result = applyParagraphCommand(model, selection, 'lineSpacing', 1.5, state);
    assert.equal(result.model.body.blocks[0].paragraphProperties.lineSpacing, 1.5);
    model = result.model;

    result = applyParagraphCommand(model, selection, 'spacingBefore', 12, state);
    assert.equal(result.model.body.blocks[0].paragraphProperties.spacingBefore, 12);
    model = result.model;

    result = applyParagraphCommand(model, selection, 'spacingAfter', 18, state);
    assert.equal(result.model.body.blocks[0].paragraphProperties.spacingAfter, 18);
    model = result.model;

    result = applyParagraphCommand(model, selection, 'increaseIndent', null, state);
    assert.equal(result.model.body.blocks[0].paragraphProperties.leftIndent, 18);

    const queried = queryParagraphCommandState(result.model, selection, state);
    assert.equal(queried.commands.align.value, 'center');
    assert.equal(queried.commands.lineSpacing.value, 1.5);
    assert.equal(queried.commands.spacingBefore.value, 12);
    assert.equal(queried.commands.spacingAfter.value, 18);
});

test('list commands toggle ordered unordered lists and tab nesting changes only list blocks', () => {
    let state = createParagraphCommandState();
    let model = createModel();
    const selection = range('body-2', 'body-2');

    let result = applyParagraphCommand(model, selection, 'bulletList', null, state);
    assert.equal(result.model.body.blocks[1].type, 'list');
    assert.equal(result.model.body.blocks[1].content.list.ordered, false);
    model = result.model;

    result = applyParagraphCommand(model, selection, 'increaseListLevel', null, state);
    assert.equal(result.model.body.blocks[1].content.list.indentLevel, 1);
    model = result.model;

    result = applyParagraphCommand(model, selection, 'decreaseListLevel', null, state);
    assert.equal(result.model.body.blocks[1].content.list.indentLevel, 0);
    model = result.model;

    result = applyParagraphCommand(model, selection, 'numberedList', null, state);
    assert.equal(result.model.body.blocks[1].content.list.ordered, true);

    const queried = queryParagraphCommandState(result.model, selection, state);
    assert.equal(queried.commands.numberedList.state, 'active');
});

test('tab stop commands set move clear and expose paragraph query state', () => {
    let state = createParagraphCommandState();
    let model = createModel();
    const selection = range('body-1', 'body-1');

    let result = applyParagraphCommand(model, selection, 'setTabStop', { position: 180, alignment: 'decimal', leader: 'dots' }, state);
    assert.equal(result.changed, true);
    assert.deepEqual(result.model.body.blocks[0].paragraphProperties.tabStops, [
        { position: 180, alignment: 'decimal', leader: 'dots' },
    ]);
    model = result.model;
    state = result.state;

    result = applyParagraphCommand(model, selection, 'moveTabStop', { fromPosition: 180, position: 220, alignment: 'right', leader: 'underline' }, state);
    assert.deepEqual(result.model.body.blocks[0].paragraphProperties.tabStops, [
        { position: 220, alignment: 'right', leader: 'underline' },
    ]);
    model = result.model;

    result = applyParagraphCommand(model, selection, 'setDefaultTabWidth', 48, state);
    assert.equal(result.model.body.blocks[0].paragraphProperties.defaultTabWidth, 48);
    const queried = queryParagraphCommandState(result.model, selection, state);
    assert.equal(queried.paragraph.defaultTabWidth, 48);
    assert.equal(queried.paragraph.tabStops[0].alignment, 'right');

    result = applyParagraphCommand(result.model, selection, 'clearTabStops', null, state);
    assert.deepEqual(result.model.body.blocks[0].paragraphProperties.tabStops, []);
});

test('tab stop runtime transaction supports undo and redo', () => {
    let model = createModel();
    let selection = range('body-1', 'body-1');
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    assert.equal(runtime.execCommand('setTabStop', { position: 180, alignment: 'decimal', leader: 'dots' }).handled, true);
    assert.deepEqual(model.body.blocks[0].paragraphProperties.tabStops, [
        { position: 180, alignment: 'decimal', leader: 'dots' },
    ]);

    runtime.execCommand('undo');
    assert.deepEqual(model.body.blocks[0].paragraphProperties.tabStops || [], []);

    runtime.execCommand('redo');
    assert.deepEqual(model.body.blocks[0].paragraphProperties.tabStops, [
        { position: 180, alignment: 'decimal', leader: 'dots' },
    ]);
});

test('numbering commands set formats restart values and list styles through runtime history', () => {
    let model = createModel();
    let selection = range('body-2', 'body-2');
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    assert.equal(runtime.execCommand('numberedList').handled, true);
    assert.equal(model.numberingDefinitions.length, 3);
    assert.equal(model.body.blocks[1].content.list.numberingId, 'tm-default-numbered');

    assert.equal(runtime.execCommand('setListFormat', 'legal').handled, true);
    assert.equal(model.body.blocks[1].content.list.numberingId, 'tm-default-legal');
    assert.equal(model.body.blocks[1].content.list.numberFormat, 'legal');

    assert.equal(runtime.execCommand('setNumberingValue', 7).handled, true);
    assert.equal(model.body.blocks[1].content.list.numberingValue, 7);
    assert.equal(model.body.blocks[1].content.list.restartNumbering, true);

    assert.equal(runtime.execCommand('continueNumbering').handled, true);
    assert.equal(model.body.blocks[1].content.list.continueNumbering, true);
    assert.equal(model.body.blocks[1].content.list.numberingValue, undefined);

    assert.equal(runtime.execCommand('defineListStyle', { id: 'contract-clause-list', name: 'Contract Clause List', format: 'legal' }).handled, true);
    assert.equal(model.body.blocks[1].content.list.listStyleId, 'contract-clause-list');
    assert.ok(model.listStyles.some(style => style.id === 'contract-clause-list'));

    runtime.execCommand('undo');
    assert.notEqual(model.body.blocks[1].content.list.listStyleId, 'contract-clause-list');
    runtime.execCommand('redo');
    assert.equal(model.body.blocks[1].content.list.listStyleId, 'contract-clause-list');
});

test('heading block style preserves inline marks and invalidates outline and table of contents revisions', () => {
    const model = createModel();
    const selection = range('body-1', 'body-1');
    const result = applyParagraphCommand(model, selection, 'blockStyle', 'Heading2', createParagraphCommandState());

    const block = result.model.body.blocks[0];
    assert.equal(block.type, 'heading');
    assert.equal(block.content.type, 'heading');
    assert.equal(block.content.headingLevel, 2);
    assert.equal(block.content.styleId, 'heading-2');
    assert.equal(block.content.styleName, 'Heading 2');
    assert.equal(block.content.outlineLevel, 2);
    assert.deepEqual(block.content.runs[0].marks.map(mark => mark.type), ['bold']);
    assert.equal(result.model.outlineRevision, 1);
    assert.equal(result.model.tableOfContentsRevision, 1);
});

test('mixed paragraph selection reports mixed alignment and block style state', () => {
    let model = createModel();
    model.body.blocks[0].paragraphProperties.alignment = 0;
    model.body.blocks[1].paragraphProperties.alignment = 2;
    model.body.blocks[1].type = 'heading';
    model.body.blocks[1].content.type = 'heading';
    model.body.blocks[1].content.headingLevel = 1;
    model.body.blocks[1].content.styleName = 'Heading 1';

    const queried = queryParagraphCommandState(model, range('body-1', 'body-2'), createParagraphCommandState());
    assert.equal(queried.commands.align.state, 'mixed');
    assert.equal(queried.commands.blockStyle.state, 'mixed');
});

test('runtime paragraph transactions support undo redo including heading state', () => {
    let model = createModel();
    let selection = range('body-1', 'body-1');
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    assert.equal(runtime.execCommand('blockStyle', 'Heading1').handled, true);
    assert.equal(model.body.blocks[0].type, 'heading');
    assert.equal(runtime.queryCommand('blockStyle').value, 'Heading 1');

    runtime.execCommand('undo');
    assert.equal(model.body.blocks[0].type, 'paragraph');
    assert.equal(runtime.queryCommand('blockStyle').value, 'Normal');

    runtime.execCommand('redo');
    assert.equal(model.body.blocks[0].type, 'heading');
    assert.equal(runtime.queryCommand('blockStyle').value, 'Heading 1');
});

function createModel() {
    return {
        documentId: 'phase-10-paragraph',
        version: 0,
        outlineRevision: 0,
        tableOfContentsRevision: 0,
        body: {
            blocks: [
                {
                    id: 'body-1',
                    sectionId: 'section-1',
                    type: 'paragraph',
                    order: 10,
                    paragraphProperties: {},
                    content: {
                        type: 'paragraph',
                        runs: [{ id: 'body-1-run', type: 'text', text: 'Important heading', marks: [{ type: 'bold', value: null }] }],
                    },
                },
                {
                    id: 'body-2',
                    sectionId: 'section-1',
                    type: 'paragraph',
                    order: 20,
                    paragraphProperties: {},
                    content: {
                        type: 'paragraph',
                        runs: [{ id: 'body-2-run', type: 'text', text: 'Second paragraph', marks: [] }],
                    },
                },
            ],
        },
        sections: [{
            id: 'section-1',
            blocks: [],
        }],
    };
}

function range(anchorBlockId, focusBlockId) {
    return {
        anchor: { blockId: anchorBlockId, offset: 0 },
        focus: { blockId: focusBlockId, offset: 0 },
    };
}
