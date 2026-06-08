import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { buildDisplayList } from '../../render/display-list.mjs';

test('footnotes and endnotes render from note references on their target pages', () => {
    const model = createNotesModel();
    const display = buildDisplayList(model, { pageSettings: model.pageSettings });

    const footnoteRegions = display.noteRegions.filter(region => region.noteType === 'Footnote');
    const endnoteRegions = display.noteRegions.filter(region => region.noteType === 'Endnote');
    assert.ok(footnoteRegions.length >= 1);
    assert.equal(endnoteRegions.length, 1);
    assert.equal(endnoteRegions[0].pageIndex, display.pageCount - 1);

    const noteCommands = display.commands.filter(command => command.noteType === 'Footnote' || command.noteType === 'Endnote');
    assert.ok(noteCommands.some(command => command.type === 'noteSeparator' && command.noteType === 'Footnote'));
    assert.ok(noteCommands.some(command => command.type === 'noteSeparator' && command.noteType === 'Endnote'));
    assert.ok(noteCommands.some(command => command.type === 'noteMarker' && command.noteId === 'note-footnote' && command.text === '1'));
    assert.ok(noteCommands.some(command => command.type === 'noteMarker' && command.noteId === 'note-endnote' && command.text === 'i'));

    const text = noteCommands.filter(command => command.type === 'textRun' || command.type === 'field').map(command => command.text).join(' ');
    assert.match(text, /Footnote body rendered from the note collection/);
    assert.match(text, /Endnote body rendered on the final page/);
    assert.match(text, /Canvas phase 16 notes/);
});

test('insert footnote and endnote commands create note references and undo cleanly', () => {
    let current = createNotesModel();
    let selection = { anchor: { blockId: 'p1', offset: 12 }, focus: { blockId: 'p1', offset: 12 } };
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

    const initialNoteCount = current.notes.length;
    const footnote = runtime.execCommand('insertFootnote', { blockId: 'p1', offset: 12, text: 'Command footnote body' });
    assert.equal(footnote.handled, true);
    assert.equal(footnote.result.changed, true);
    assert.equal(current.notes.length, initialNoteCount + 1);
    assert.equal(current.notes.at(-1).type, 0);
    assert.equal(current.body.blocks[0].content.runs.some(run => run.type === 'noteReference' && run.noteReference.noteId === footnote.result.noteId), true);

    const endnote = runtime.execCommand('insertEndnote', { blockId: 'p1', offset: 13, text: 'Command endnote body' });
    assert.equal(endnote.result.changed, true);
    assert.equal(current.notes.length, initialNoteCount + 2);
    assert.equal(current.notes.at(-1).type, 1);

    const display = buildDisplayList(current, { pageSettings: current.pageSettings });
    const text = display.commands.filter(command => command.type === 'textRun').map(command => command.text).join(' ');
    assert.match(text, /Command footnote body/);
    assert.match(text, /Command endnote body/);

    runtime.execCommand('undo');
    assert.equal(current.notes.length, initialNoteCount + 1);
    runtime.execCommand('undo');
    assert.equal(current.notes.length, initialNoteCount);
});

test('note numbering settings control inserted note markers and render them in note regions', () => {
    let current = createNotesModel();
    let selection = { anchor: { blockId: 'p1', offset: 12 }, focus: { blockId: 'p1', offset: 12 } };
    const runtime = createCanvasCommandRuntime({
        getModel: () => current,
        getSelection: () => selection,
        history: createHistory(),
        commit(change) {
            current = change.model;
            selection = change.selection || selection;
        },
    });

    const setup = runtime.execCommand('setPageSettings', {
        sectionId: 's1',
        noteNumbering: {
            style: 'lowerRoman',
            startAt: 4,
            restartEachSection: true,
        },
    });
    assert.equal(setup.result.changed, true);
    assert.equal(current.sections[0].properties.noteNumbering.style, 'lowerRoman');
    assert.equal(current.sections[0].properties.noteNumbering.startAt, 4);

    const inserted = runtime.execCommand('insertFootnote', { blockId: 'p1', offset: 12, text: 'Lower roman configured footnote' });
    const insertedNote = current.notes.find(note => note.id === inserted.result.noteId);
    assert.equal(inserted.result.changed, true);
    assert.equal(insertedNote.marker, 'v');
    assert.equal(current.body.blocks[0].content.runs.some(run => run.type === 'noteReference' && run.noteReference.displayMarker === 'v'), true);

    const display = buildDisplayList(current, { pageSettings: current.pageSettings });
    assert.ok(display.commands.some(command =>
        command.type === 'noteMarker'
        && command.noteId === inserted.result.noteId
        && command.text === 'v'));
    assert.ok(display.commands.some(command =>
        command.type === 'textRun'
        && command.noteId === inserted.result.noteId
        && command.text.includes('Lower roman configured footnote')));
});

function createNotesModel() {
    const blocks = [
        paragraph('p1', [
            text('Contract recital '),
            noteReference('fn-ref', 'note-footnote', 0, '1'),
            text(' continues with binding language and supporting references. '),
            noteReference('en-ref', 'note-endnote', 1, 'i'),
        ]),
        ...Array.from({ length: 34 }, (_, index) => paragraph(`p${index + 2}`, [
            text(`Additional body paragraph ${index + 1} keeps pagination deterministic for notes layout coverage.`),
        ])),
    ];

    return {
        documentId: 'notes-test',
        metadata: {
            title: 'Canvas phase 16 notes',
            author: { displayName: 'Tempo Author' },
            modifiedAt: '2026-06-04T09:00:00Z',
        },
        pageSettings: { width: 794, height: 620, marginTop: 72, marginRight: 72, marginBottom: 80, marginLeft: 72, headerDistanceFromTop: 42, footerDistanceFromBottom: 42 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11 },
        sections: [
            {
                id: 's1',
                order: 0,
                pageSettings: { width: 794, height: 620, marginTop: 72, marginRight: 72, marginBottom: 80, marginLeft: 72, headerDistanceFromTop: 42, footerDistanceFromBottom: 42 },
                properties: {},
                blocks,
            },
        ],
        body: { blocks },
        notes: [
            note('note-footnote', 0, '1', ['fn-ref'], [
                text('Footnote body rendered from the note collection. '),
                field('note-title-field', 4),
            ]),
            note('note-endnote', 1, 'i', ['en-ref'], [
                text('Endnote body rendered on the final page.'),
            ]),
        ],
    };
}

function paragraph(id, runs) {
    return {
        id,
        sectionId: 's1',
        type: 'paragraph',
        order: Number(String(id).replace('p', '')) * 10,
        paragraphProperties: { lineSpacing: 1.12, spacingAfter: 8 },
        content: { type: 'paragraph', runs },
    };
}

function note(id, type, marker, referenceIds, runs) {
    return {
        id,
        type,
        sectionId: 's1',
        marker,
        referenceIds,
        blocks: [
            {
                id: `${id}-body`,
                sectionId: 's1',
                type: 'paragraph',
                order: 10,
                paragraphProperties: {},
                content: { type: 'paragraph', runs },
            },
        ],
    };
}

function text(value, id = `text-${value.replace(/[^a-z0-9]+/gi, '-').slice(0, 32)}`) {
    return { id, type: 'text', text: value, marks: [] };
}

function field(id, fieldType) {
    return { id, type: 'field', field: { fieldType, fallbackText: '' }, marks: [] };
}

function noteReference(id, noteId, noteType, displayMarker) {
    return {
        id,
        type: 'noteReference',
        noteReference: {
            noteId,
            noteType,
            displayMarker,
        },
        marks: [],
    };
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
