import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../../render/display-list.mjs';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';

test('header and footer render per page with first even odd scopes and resolved fields', () => {
    const model = createHeaderFooterModel();
    const display = buildDisplayList(model, { pageSettings: model.pageSettings });
    assert.ok(display.pageCount >= 2);

    const regions = display.commands.filter(command => command.type === 'headerFooterFrame');
    assert.ok(regions.some(command => command.pageIndex === 0 && command.region === 'Header' && command.scope === 'FirstPage'));
    assert.ok(regions.some(command => command.pageIndex === 1 && command.region === 'Header' && command.scope === 'EvenPages'));
    assert.ok(regions.some(command => command.pageIndex === 0 && command.region === 'Footer' && command.scope === 'FirstPage'));
    assert.ok(regions.some(command => command.pageIndex === 1 && command.region === 'Footer' && command.scope === 'EvenPages'));

    const text = display.commands.filter(command => command.type === 'textRun' || command.type === 'field').map(command => command.text).join(' ');
    assert.match(text, /First header/);
    assert.match(text, /Even header/);
    assert.match(text, /Canvas phase 16/);
    assert.match(text, /2026-06-04/);
    assert.match(text, new RegExp(`2\\s*/\\s*${display.pageCount}`));
});

test('field and page setup commands are undoable and mutate the canonical canvas model', () => {
    let current = createHeaderFooterModel();
    let selection = { anchor: { blockId: 'p1', offset: 5 }, focus: { blockId: 'p1', offset: 5 } };
    const history = createHistory();
    const runtime = createCanvasCommandRuntime({
        getModel: () => current,
        getSelection: () => selection,
        history,
        commit(change) {
            current = change.model;
            selection = change.selection || selection;
        },
    });

    const field = runtime.execCommand('insertDateField', { blockId: 'p1', offset: 5, format: 'yyyy-MM-dd' });
    assert.equal(field.handled, true);
    assert.equal(field.result.changed, true);
    assert.equal(current.body.blocks[0].content.runs.some(run => run.type === 'field'), true);

    const setup = runtime.execCommand('setPageSettings', {
        sectionId: 's1',
        pageSettings: {
            size: { name: 'Letter', width: 612, height: 792 },
            margins: { top: 48, right: 54, bottom: 52, left: 54 },
            headerDistanceFromTop: 28,
            footerDistanceFromBottom: 30,
            landscape: true,
        },
        columns: {
            count: 2,
            spacing: 30,
            separatorLine: true,
            balance: true,
            preset: 'two',
            items: [],
        },
        lineNumbering: {
            enabled: true,
            startAt: 7,
            increment: 2,
            distanceFromText: 15,
            restart: 'section',
        },
        noteNumbering: {
            style: 'lowerRoman',
            startAt: 4,
            restartEachSection: true,
        },
    });
    assert.equal(setup.result.changed, true);
    assert.equal(current.pageSettings.landscape, true);
    assert.equal(current.pageSettings.width, 1056);
    assert.equal(current.sections[0].properties.columns.count, 2);
    assert.equal(current.sections[0].properties.columns.separatorLine, true);
    assert.equal(current.sections[0].properties.columns.balance, true);
    assert.equal(current.sections[0].properties.lineNumbering.enabled, true);
    assert.equal(current.sections[0].properties.lineNumbering.restart, 'section');
    assert.equal(current.sections[0].properties.noteNumbering.style, 'lowerRoman');
    assert.equal(current.sections[0].properties.noteNumbering.startAt, 4);
    assert.equal(current.sections[0].properties.noteNumbering.restartEachSection, true);
    assert.equal(history.snapshot().canUndo, true);

    runtime.execCommand('undo');
    assert.equal(current.pageSettings.width, 794);
    assert.equal(current.sections[0].properties.columns, undefined);
    assert.equal(current.sections[0].properties.lineNumbering, undefined);
    assert.equal(current.sections[0].properties.noteNumbering, undefined);
});

test('page setup keeps explicit page breaks as hard pagination boundaries', () => {
    let model = createHeaderFooterModel();
    let selection = { anchor: { blockId: 'p1', offset: 0 }, focus: { blockId: 'p1', offset: 0 } };
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistory(),
        commit(change) {
            model = change.model;
            selection = change.selection || selection;
        },
    });

    const inserted = runtime.execCommand('insertPageBreak', { id: 'manual-page-break', blockId: 'p1' });
    assert.equal(inserted.handled, true);
    assert.equal(inserted.result.changed, true);

    const display = buildDisplayList(model, { pageSettings: model.pageSettings });
    const firstBlockPage = display.commands.find(command => command.blockId === 'p1' && command.type === 'textRun')?.pageIndex;
    const afterBreakPage = display.commands.find(command => command.blockId === 'p2' && command.type === 'textRun')?.pageIndex;
    const breakCommand = display.layout.blocks.find(block => block.blockId === 'manual-page-break');

    assert.equal(firstBlockPage, 0);
    assert.equal(breakCommand.type, 'pageBreak');
    assert.equal(breakCommand.pageIndex, 0);
    assert.equal(afterBreakPage, 1);
});

function createHeaderFooterModel() {
    const blocks = Array.from({ length: 42 }, (_, index) => paragraph(`p${index + 1}`, `Body line ${index + 1} keeps pagination active for header footer field resolution.`));
    return {
        documentId: 'header-footer-test',
        metadata: {
            title: 'Canvas phase 16',
            author: { displayName: 'Tempo Author' },
            modifiedAt: '2026-06-04T08:00:00Z',
        },
        pageSettings: { width: 794, height: 600, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72, headerDistanceFromTop: 42, footerDistanceFromBottom: 42 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11 },
        sections: [
            {
                id: 's1',
                order: 0,
                pageSettings: { width: 794, height: 600, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72, headerDistanceFromTop: 42, footerDistanceFromBottom: 42 },
                properties: {
                    differentFirstPage: true,
                    differentOddAndEvenPages: true,
                    headerFooterReferences: [
                        ref('h-first', 0, 1),
                        ref('h-even', 0, 2),
                        ref('h-odd', 0, 3),
                        ref('f-first', 1, 1),
                        ref('f-even', 1, 2),
                        ref('f-odd', 1, 3),
                    ],
                },
                blocks,
            },
        ],
        body: { blocks },
        headersFooters: [
            headerFooter('h-first', 0, 1, [text('First header')], 'center'),
            headerFooter('h-even', 0, 2, [text('Even header '), field('title', 4)]),
            headerFooter('h-odd', 0, 3, [text('Odd header')], 'right'),
            headerFooter('f-first', 1, 1, [text('First footer')], 'center'),
            headerFooter('f-even', 1, 2, [field('page', 0), text('/'), field('count', 1)], 'center'),
            headerFooter('f-odd', 1, 3, [field('date', 3, 'yyyy-MM-dd')], 'center'),
        ],
    };
}

function paragraph(id, textValue) {
    return {
        id,
        sectionId: 's1',
        type: 'paragraph',
        order: Number(id.slice(1)) * 10,
        paragraphProperties: { lineSpacing: 1.1, spacingAfter: 7 },
        content: { type: 'paragraph', runs: [text(textValue, `${id}-r`)] },
    };
}

function headerFooter(id, type, scope, runs, alignment = 'left') {
    return {
        id,
        type,
        scope,
        sectionId: 's1',
        blocks: [
            {
                id: `${id}-block`,
                sectionId: 's1',
                type: 'paragraph',
                order: 10,
                paragraphProperties: { alignment },
                content: { type: 'paragraph', runs },
            },
        ],
    };
}

function text(value, id = `t-${value}`) {
    return { id, type: 'text', text: value, marks: [] };
}

function field(id, fieldType, format = null) {
    return { id, type: 'field', field: { fieldType, format, fallbackText: '?' }, marks: [] };
}

function ref(headerFooterId, type, scope) {
    return { headerFooterId, type, scope };
}

function createHistory() {
    const undo = [];
    const redo = [];
    return {
        push(transaction) {
            undo.push(transaction);
            redo.length = 0;
        },
        undo() {
            const transaction = undo.pop();
            if (transaction) {
                redo.push(transaction);
            }

            return transaction;
        },
        redo() {
            const transaction = redo.pop();
            if (transaction) {
                undo.push(transaction);
            }

            return transaction;
        },
        snapshot() {
            return { canUndo: undo.length > 0, canRedo: redo.length > 0 };
        },
    };
}
