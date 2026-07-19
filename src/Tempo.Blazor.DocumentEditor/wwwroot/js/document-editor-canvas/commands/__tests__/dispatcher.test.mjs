import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';
import { mathToAccessibleText } from '../../math/math-model.mjs';

test('execCommand and queryCommand use one dispatcher boundary for toolbar and interop callers', () => {
    let model = createModel();
    let selection = range(6, 12);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const toolbarPointerDownSelectionToken = JSON.stringify(selection);
    const toolbarResult = runtime.execCommand('bold', { source: 'toolbar' });
    const interopState = runtime.queryCommand('bold');

    assert.equal(toolbarResult.handled, true);
    assert.equal(interopState.state, 'active');
    assert.equal(JSON.stringify(selection), toolbarPointerDownSelectionToken);
    assert.ok(model.body.blocks[0].content.runs.some(run => run.marks?.some(mark => mark.type === 'bold')));
});

test('unknown commands are not handled but custom registered commands still work', () => {
    const runtime = createCanvasCommandRuntime({
        getModel: () => createModel(),
        getSelection: () => range(0, 0),
        history: createHistoryStore(),
        commit() { },
    });

    assert.equal(runtime.execCommand('missing').handled, false);
    runtime.register('probe', payload => ({ value: payload.value }));
    const result = runtime.execute('probe', { value: 42 });
    assert.equal(result.handled, true);
    assert.deepEqual(result.result, { value: 42 });
});

test('setSelection command moves the caret without mutating model or history', () => {
    let model = createModel();
    let selection = range(0, 0);
    const history = createHistoryStore();
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const result = runtime.execCommand('setSelection', { blockId: 'dispatcher-body', start: 5, end: 5 });

    assert.equal(result.handled, true);
    assert.equal(result.result.changed, false);
    assert.equal(result.result.selectionChanged, true);
    assert.deepEqual(selection, range(5, 5));
    assert.equal(model.body.blocks[0].content.runs[0].text, 'Hello canvas world');
    assert.equal(history.snapshot().undoDepth, 0);
});

test('inline formatting commands each create undoable redoable runtime transactions', () => {
    const commands = [
        ['bold', null, 'bold'],
        ['italic', null, 'italic'],
        ['underline', null, 'underline'],
        ['strikethrough', null, 'strikethrough'],
        ['fontfamily', 'Georgia, serif', 'fontfamily'],
        ['fontsize', '24pt', 'fontsize'],
        ['textcolor', '#123456', 'textcolor'],
        ['highlight', '#fff59d', 'highlight'],
        ['link', 'https://example.test/canvas', 'link'],
    ];

    for (const [commandId, argument, stateCommandId] of commands) {
        let model = createModel();
        let selection = range(0, 5);
        const runtime = createCanvasCommandRuntime({
            getModel: () => model,
            getSelection: () => selection,
            history: createHistoryStore(),
            commit(change) {
                model = change.model;
                selection = change.selection;
            },
        });

        const applied = runtime.execCommand(commandId, argument);
        assert.equal(applied.handled, true, `${commandId} must be handled`);
        assert.equal(runtime.queryCommand(stateCommandId).state, 'active', `${commandId} must become active`);

        const undone = runtime.execCommand('undo');
        assert.equal(undone.handled, true, `${commandId} undo must be handled`);
        assert.equal(runtime.queryCommand(stateCommandId).state, 'inactive', `${commandId} must undo to inactive`);

        const redone = runtime.execCommand('redo');
        assert.equal(redone.handled, true, `${commandId} redo must be handled`);
        assert.equal(runtime.queryCommand(stateCommandId).state, 'active', `${commandId} must redo to active`);
    }
});

test('differentFirstPage and differentOddEven set or toggle the section flag with undo/redo', () => {
    const cases = [
        ['differentFirstPage', 'differentFirstPage'],
        ['differentOddEven', 'differentOddAndEvenPages'],
    ];

    for (const [commandId, sectionKey] of cases) {
        let model = createModel();
        let selection = range(0, 0);
        const runtime = createCanvasCommandRuntime({
            getModel: () => model,
            getSelection: () => selection,
            history: createHistoryStore(),
            commit(change) {
                model = change.model;
                selection = change.selection ?? selection;
            },
        });

        // The C# ribbon checkbox sends the TARGET state ({enabled}) so C# and engine
        // cannot diverge — set-mode must be idempotent, not a blind toggle.
        assert.equal(runtime.execCommand(commandId, { enabled: true }).handled, true, `${commandId} must be handled`);
        assert.equal(model.sections[0].properties[sectionKey], true, `${commandId} {enabled:true} must set the flag`);
        assert.equal(runtime.queryCommand(commandId).state, 'active', `${commandId} state must report active`);

        runtime.execCommand(commandId, { enabled: true });
        assert.equal(model.sections[0].properties[sectionKey], true, `${commandId} {enabled:true} must be idempotent`);

        runtime.execCommand(commandId, { enabled: false });
        assert.equal(model.sections[0].properties[sectionKey], false, `${commandId} {enabled:false} must clear the flag`);

        // No payload keeps the legacy toggle semantics (togglefirstpageheaderfooter aliases).
        runtime.execCommand(commandId);
        assert.equal(model.sections[0].properties[sectionKey], true, `${commandId} without payload must toggle on`);

        const undone = runtime.execCommand('undo');
        assert.equal(undone.handled, true, `${commandId} undo must be handled`);
        assert.equal(model.sections[0].properties[sectionKey], false, `${commandId} undo must restore the previous flag`);

        const redone = runtime.execCommand('redo');
        assert.equal(redone.handled, true, `${commandId} redo must be handled`);
        assert.equal(model.sections[0].properties[sectionKey], true, `${commandId} redo must reapply the flag`);
    }
});

test('link remove and ctrl click open use the command runtime link state', () => {
    let model = createModel();
    let selection = range(0, 5);
    const opened = [];
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        openUrl: href => opened.push(href),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    runtime.execCommand('link', 'https://example.test/open');
    assert.equal(runtime.queryCommand('link').value, 'https://example.test/open');
    assert.equal(runtime.openLinkAtPosition({ blockId: 'dispatcher-body', offset: 2 }), true);
    assert.deepEqual(opened, ['https://example.test/open']);

    runtime.execCommand('removelink');
    assert.equal(runtime.queryCommand('link').state, 'inactive');
});

test('math equation command inserts structured runs through undoable runtime transactions', () => {
    let model = createModel();
    let selection = range(6, 6);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const inserted = runtime.execCommand('insertEquation', {
        id: 'inserted-math-run',
        mathId: 'inserted-math',
        linear: 'x^2',
    });

    assert.equal(inserted.handled, true);
    assert.equal(inserted.result.changed, true);
    const mathRun = model.body.blocks[0].content.runs.find(run => run.id === 'inserted-math-run');
    assert.equal(mathRun.type, 'math');
    assert.equal(mathRun.math.mathId, 'inserted-math');
    assert.equal(mathRun.math.displayMode, 0);
    assert.equal(mathRun.math.content.elements[0].type, 'sup');
    assert.equal(runtime.queryCommand('insertEquation').disabled, false);

    const undone = runtime.execCommand('undo');
    assert.equal(undone.result.changed, true);
    assert.equal(model.body.blocks[0].content.runs.some(run => run.id === 'inserted-math-run'), false);

    const redone = runtime.execCommand('redo');
    assert.equal(redone.result.changed, true);
    assert.equal(model.body.blocks[0].content.runs.some(run => run.id === 'inserted-math-run'), true);
});

test('advanced math commands insert symbols, limits, accents and border boxes as structured runs', () => {
    let model = createModel();
    let selection = range(0, 0);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const commands = [
        ['insertMathSymbol', { id: 'math-symbol-run', mathId: 'math-symbol', symbol: '\\alpha' }, 'run'],
        ['insertLinearMath', { id: 'math-sub-run', mathId: 'math-sub', linear: 'x_i' }, 'sub'],
        ['insertLimit', { id: 'math-limit-run', mathId: 'math-limit', lowerText: 'x→0', text: 'f(x)' }, 'limit'],
        ['insertAccent', { id: 'math-accent-run', mathId: 'math-accent', accent: '̂', baseText: 'x' }, 'accent'],
        ['insertBorderBox', { id: 'math-box-run', mathId: 'math-box', text: 'x+y' }, 'borderBox'],
    ];

    for (const [commandId, payload, expectedType] of commands) {
        const result = runtime.execCommand(commandId, payload);
        assert.equal(result.handled, true, commandId);
        assert.equal(result.result.changed, true, commandId);
        const run = model.body.blocks[0].content.runs.find(item => item.id === payload.id);
        assert.equal(run.type, 'math', commandId);
        assert.equal(run.math.content.elements[0].type, expectedType, commandId);
    }

    assert.equal(runtime.queryCommand('insertMathSymbol').disabled, false);
    runtime.execCommand('undo');
    assert.equal(model.body.blocks[0].content.runs.some(run => run.id === 'math-box-run'), false);
    runtime.execCommand('redo');
    assert.equal(model.body.blocks[0].content.runs.some(run => run.id === 'math-box-run'), true);
});

test('math template commands create empty editable slots and activate the first slot', () => {
    const commands = [
        ['insertFraction', { id: 'template-fraction-run', mathId: 'template-fraction' }, 'fraction', 'numerator', ['elements', 0, 'numerator']],
        ['insertRadical', { id: 'template-radical-run', mathId: 'template-radical' }, 'radical', 'radicand', ['elements', 0, 'radicand']],
        ['insertSuperscript', { id: 'template-sup-run', mathId: 'template-sup' }, 'sup', 'base', ['elements', 0, 'base']],
        ['insertSubscript', { id: 'template-sub-run', mathId: 'template-sub' }, 'sub', 'base', ['elements', 0, 'base']],
        ['insertNary', { id: 'template-nary-run', mathId: 'template-nary', operator: 'sum' }, 'nary', 'lower limit', ['elements', 0, 'lowerLimit']],
        ['insertDelimiter', { id: 'template-delimiter-run', mathId: 'template-delimiter', open: '[', close: ']' }, 'delimiter', 'content', ['elements', 0, 'content']],
        ['insertMatrix', { id: 'template-matrix-run', mathId: 'template-matrix', rows: 2, columns: 2, values: ['', '', '', ''] }, 'matrix', 'cell 1, 1', ['elements', 0, 'rows', 0, 'cells', 0]],
    ];

    for (const [commandId, payload, expectedType, expectedSlotName, expectedSlotPath] of commands) {
        let model = createModel();
        let selection = range(0, 0);
        const runtime = createCanvasCommandRuntime({
            getModel: () => model,
            getSelection: () => selection,
            history: createHistoryStore(),
            commit(change) {
                model = change.model;
                selection = change.selection;
            },
        });

        const result = runtime.execCommand(commandId, payload);
        assert.equal(result.handled, true, commandId);
        assert.equal(result.result.changed, true, commandId);
        assert.equal(result.result.mathSlot.slotName, expectedSlotName, commandId);
        assert.deepEqual(result.result.mathSlot.slotPath, expectedSlotPath, commandId);
        assert.equal(selection.math.slotName, expectedSlotName, commandId);

        const run = model.body.blocks[0].content.runs.find(item => item.id === payload.id);
        assert.equal(run.math.content.elements[0].type, expectedType, commandId);
        const typed = runtime.execCommand('insertMathSlotText', { text: 'q' });
        assert.equal(typed.result.changed, true, commandId);
        const editedRun = model.body.blocks[0].content.runs.find(item => item.id === payload.id);
        assert.match(mathToAccessibleText(editedRun.math.content), /q/, commandId);
    }
});

test('math symbol command inserts greek operator arrow and relation symbols through undoable transactions', () => {
    const symbols = [
        ['\\alpha', 'α'],
        ['\\beta', 'β'],
        ['\\gamma', 'γ'],
        ['\\Delta', 'Δ'],
        ['\\theta', 'θ'],
        ['\\lambda', 'λ'],
        ['\\int', '∫'],
        ['\\infty', '∞'],
        ['±', '±'],
        ['→', '→'],
        ['≤', '≤'],
        ['≥', '≥'],
        ['≠', '≠'],
    ];

    let model = createModel();
    let selection = range(0, 0);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    for (const [symbol, expectedText] of symbols) {
        const runId = `symbol-${expectedText.codePointAt(0).toString(16)}-run`;
        const result = runtime.execCommand('insertMathSymbol', {
            id: runId,
            mathId: `symbol-${expectedText.codePointAt(0).toString(16)}`,
            symbol,
        });
        assert.equal(result.handled, true, symbol);
        assert.equal(result.result.changed, true, symbol);
        assert.equal(mathToAccessibleText(model.body.blocks[0].content.runs.find(run => run.id === runId).math.content), expectedText, symbol);
    }

    const lastRunId = model.body.blocks[0].content.runs.findLast(run => run.type === 'math').id;
    runtime.execCommand('undo');
    assert.equal(model.body.blocks[0].content.runs.some(run => run.id === lastRunId), false);
    runtime.execCommand('redo');
    assert.equal(model.body.blocks[0].content.runs.some(run => run.id === lastRunId), true);
});

test('math display mode command toggles inline and display equations through undoable transactions', () => {
    let model = createModelWithMath();
    let selection = range(7, 7);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    runtime.execCommand('activateMathSlot', {
        mathId: 'dispatcher-math',
        slotPath: ['elements', 0, 'numerator'],
        offset: 1,
    });

    const display = runtime.execCommand('setMathDisplayMode', {
        mathId: 'dispatcher-math',
        displayMode: 'display',
    });
    assert.equal(display.handled, true);
    assert.equal(display.result.changed, true);
    assert.equal(model.body.blocks[0].content.runs.find(run => run.id === 'dispatcher-math-run').math.displayMode, 1);
    assert.equal(runtime.queryCommand('setMathDisplayMode').disabled, false);

    const undo = runtime.execCommand('undo');
    assert.equal(undo.result.changed, true);
    assert.equal(model.body.blocks[0].content.runs.find(run => run.id === 'dispatcher-math-run').math.displayMode, 0);

    const redo = runtime.execCommand('redo');
    assert.equal(redo.result.changed, true);
    assert.equal(model.body.blocks[0].content.runs.find(run => run.id === 'dispatcher-math-run').math.displayMode, 1);
});

test('math cross-slot selection records a structural range without mutating the equation tree', () => {
    let model = createModelWithMath();
    let selection = range(7, 7);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const selected = runtime.execCommand('selectMathSlotRange', {
        mathId: 'dispatcher-math',
        anchorSlotPath: ['elements', 0, 'numerator'],
        focusSlotPath: ['elements', 0, 'denominator'],
    });

    assert.equal(selected.handled, true);
    assert.equal(selected.result.changed, false);
    assert.equal(selected.result.selectionChanged, true);
    assert.equal(selection.math.structuralRange, true);
    assert.deepEqual(selection.math.structuralPath, ['elements', 0]);
    assert.deepEqual(selection.math.selectedSlotPaths, [
        ['elements', 0, 'numerator'],
        ['elements', 0, 'denominator'],
    ]);
    assert.equal(currentMathText(model), '(a)/(b)1001');
});

test('math slot deactivation exits equation editing and announces the exit without history mutation', () => {
    let model = createModelWithMath();
    let selection = range(7, 7);
    const history = createHistoryStore();
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    runtime.execCommand('activateMathSlot', {
        mathId: 'dispatcher-math',
        slotPath: ['elements', 0, 'numerator'],
        offset: 1,
    });

    const exited = runtime.execCommand('deactivateMathSlot');
    assert.equal(exited.handled, true);
    assert.equal(exited.result.changed, false);
    assert.equal(exited.result.viewChanged, true);
    assert.equal(exited.result.mathSlot.exit, true);
    assert.equal(selection.math, undefined);
    assert.equal(history.snapshot().undoDepth, 0);
});

test('math equation command imports MathML payload through undoable runtime transactions', () => {
    let model = createModel();
    let selection = range(0, 0);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const inserted = runtime.execCommand('insertEquation', {
        id: 'mathml-run',
        mathId: 'mathml-equation',
        mathML: '<math><mtable><mtr><mtd><mn>1</mn></mtd><mtd><mn>0</mn></mtd></mtr><mtr><mtd><mn>0</mn></mtd><mtd><mn>1</mn></mtd></mtr></mtable></math>',
    });

    assert.equal(inserted.handled, true);
    assert.equal(inserted.result.changed, true);
    const mathRun = model.body.blocks[0].content.runs.find(run => run.id === 'mathml-run');
    assert.equal(mathRun.type, 'math');
    assert.equal(mathRun.math.mathId, 'mathml-equation');
    assert.equal(mathRun.math.content.elements[0].type, 'matrix');
    assert.equal(mathRun.math.content.elements[0].rows.length, 2);
    assert.match(mathRun.math.mathML, /<mtable>/);

    runtime.execCommand('undo');
    assert.equal(model.body.blocks[0].content.runs.some(run => run.id === 'mathml-run'), false);
    runtime.execCommand('redo');
    assert.equal(model.body.blocks[0].content.runs.some(run => run.id === 'mathml-run'), true);
});

test('math slot edit commands update equation slots through undoable runtime transactions', () => {
    let model = createModelWithMath();
    let selection = range(7, 7);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const activated = runtime.execCommand('activateMathSlot', {
        mathId: 'dispatcher-math',
        slotPath: ['elements', 0, 'numerator'],
        offset: 1,
    });
    assert.equal(activated.handled, true);
    assert.equal(activated.result.selectionChanged, true);
    assert.equal(selection.math.slotName, 'numerator');

    const inserted = runtime.execCommand('insertMathSlotText', {
        text: '+c',
    });
    assert.equal(inserted.handled, true);
    assert.equal(inserted.result.changed, true);
    assert.equal(inserted.result.mathSlot.slotName, 'numerator');
    assert.equal(inserted.result.announcement, 'numerator');
    assert.equal(currentMathText(model), '(a+c)/(b)1001');

    const symbol = runtime.execCommand('insertMathSymbol', {
        symbol: '\\alpha',
    });
    assert.equal(symbol.handled, true);
    assert.equal(symbol.result.changed, true);
    assert.equal(currentMathText(model), '(a+cα)/(b)1001');

    const next = runtime.execCommand('moveMathSlot', { direction: 'next' });
    assert.equal(next.handled, true);
    assert.equal(next.result.selectionChanged, true);
    assert.equal(selection.math.slotName, 'denominator');

    runtime.execCommand('insertMathSlotText', { text: '+d', offset: 1 });
    assert.equal(currentMathText(model), '(a+cα)/(b+d)1001');

    const deleted = runtime.execCommand('deleteMathSlotBackward', { offset: 3 });
    assert.equal(deleted.result.changed, true);
    assert.equal(currentMathText(model), '(a+cα)/(b+)1001');

    const undo = runtime.execCommand('undo');
    assert.equal(undo.result.changed, true);
    assert.equal(currentMathText(model), '(a+cα)/(b+d)1001');

    const redo = runtime.execCommand('redo');
    assert.equal(redo.result.changed, true);
    assert.equal(currentMathText(model), '(a+cα)/(b+)1001');

    const template = runtime.execCommand('insertLinearMath', {
        linear: 'm/n',
    });
    assert.equal(template.handled, true);
    assert.equal(template.result.changed, true);
    assert.equal(model.body.blocks[0].content.runs.find(run => run.id === 'dispatcher-math-run').math.content.elements[0].denominator.elements[0].type, 'fraction');
});

test('math matrix edit commands add rows and columns without flattening the matrix', () => {
    let model = createModelWithMath();
    let selection = range(7, 7);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const row = runtime.execCommand('addMathMatrixRow', {
        mathId: 'dispatcher-math',
        matrixPath: ['elements', 1],
        afterRowIndex: 0,
        values: ['r', 's'],
    });
    assert.equal(row.handled, true);
    assert.equal(row.result.changed, true);

    const column = runtime.execCommand('addMathMatrixColumn', {
        mathId: 'dispatcher-math',
        matrixPath: ['elements', 1],
        afterColumnIndex: 0,
        values: ['u', 'v', 'w'],
    });
    assert.equal(column.handled, true);
    assert.equal(column.result.changed, true);

    const matrix = model.body.blocks[0].content.runs.find(run => run.id === 'dispatcher-math-run').math.content.elements[1];
    assert.equal(matrix.rows.length, 3);
    assert.equal(matrix.rows[0].cells.length, 3);
    assert.equal(matrix.rows[1].cells[1].elements[0].text, 'v');

    runtime.execCommand('undo');
    assert.equal(model.body.blocks[0].content.runs.find(run => run.id === 'dispatcher-math-run').math.content.elements[1].rows[0].cells.length, 2);
    runtime.execCommand('redo');
    assert.equal(model.body.blocks[0].content.runs.find(run => run.id === 'dispatcher-math-run').math.content.elements[1].rows[0].cells.length, 3);
});

test('math boundary delete unwraps structures through undoable dispatcher transactions', () => {
    let model = createModelWithMath();
    let selection = range(7, 7);
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    runtime.execCommand('activateMathSlot', {
        mathId: 'dispatcher-math',
        slotPath: ['elements', 0, 'numerator'],
        offset: 0,
    });

    const deleted = runtime.execCommand('deleteMathSlotBackward', { offset: 0 });
    assert.equal(deleted.handled, true);
    assert.equal(deleted.result.changed, true);
    assert.equal(currentMathText(model), 'a1001');
    assert.equal(selection.math.slotName, 'equation');

    const undo = runtime.execCommand('undo');
    assert.equal(undo.result.changed, true);
    assert.equal(currentMathText(model), '(a)/(b)1001');

    const redo = runtime.execCommand('redo');
    assert.equal(redo.result.changed, true);
    assert.equal(currentMathText(model), 'a1001');
});

function createModel() {
    return {
        documentId: 'phase-9-dispatcher',
        version: 0,
        body: {
            blocks: [{
                id: 'dispatcher-body',
                sectionId: 'section-1',
                type: 'paragraph',
                order: 10,
                paragraphProperties: {},
                content: {
                    type: 'paragraph',
                    runs: [{ id: 'dispatcher-body-run', type: 'text', text: 'Hello canvas world', marks: [] }],
                },
            }],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function createModelWithMath() {
    const model = createModel();
    model.body.blocks[0].content.runs = [
        { id: 'dispatcher-text-before', type: 'text', text: 'Before ', marks: [] },
        {
            id: 'dispatcher-math-run',
            type: 'math',
            text: '',
            marks: [],
            math: {
                mathId: 'dispatcher-math',
                displayMode: 0,
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
                                { cells: [{ elements: [{ type: 'run', text: '1' }] }, { elements: [{ type: 'run', text: '0' }] }] },
                                { cells: [{ elements: [{ type: 'run', text: '0' }] }, { elements: [{ type: 'run', text: '1' }] }] },
                            ],
                        },
                    ],
                },
            },
        },
        { id: 'dispatcher-text-after', type: 'text', text: ' after', marks: [] },
    ];
    return model;
}

function currentMathText(model) {
    const run = model.body.blocks[0].content.runs.find(item => item.id === 'dispatcher-math-run');
    const fraction = run.math.content.elements[0];
    return `${mathToAccessibleText({ elements: [fraction] })}${run.math.content.elements[1].rows.map(row => row.cells.map(cell => mathToAccessibleText(cell)).join('')).join('')}`;
}

function range(start, end) {
    return {
        anchor: { blockId: 'dispatcher-body', offset: start },
        focus: { blockId: 'dispatcher-body', offset: end },
    };
}
