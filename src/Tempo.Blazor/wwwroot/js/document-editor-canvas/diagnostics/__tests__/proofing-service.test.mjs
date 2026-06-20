import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { createCanvasHistoryController } from '../../history/history-controller.mjs';
import { createCanvasProofingService, mapDiagnosticRects } from '../proofing-service.mjs';

test('mapDiagnosticRects splits a misspelling across wrapped text rects', () => {
    const rects = mapDiagnosticRects(
        { blockId: 'paragraph-1', start: 8, end: 16, word: 'wrnggword' },
        [
            { blockId: 'paragraph-1', start: 0, end: 12, pageIndex: 0, x: 20, y: 40, width: 120, height: 18 },
            { blockId: 'paragraph-1', start: 12, end: 24, pageIndex: 0, x: 20, y: 60, width: 120, height: 18 },
        ]);

    assert.equal(rects.length, 2);
    assert.equal(rects[0].start, 8);
    assert.equal(rects[0].end, 12);
    assert.equal(rects[1].start, 12);
    assert.equal(rects[1].end, 16);
    assert.ok(rects[0].width > 1);
    assert.ok(rects[1].width > 1);
});

test('proofing service finds host-flagged words without mutating the model', () => {
    const model = createProofingModel('The wrngg word stays in the model.');
    const before = JSON.stringify(model);
    const service = createCanvasProofingService({
        flaggedWords: ['wrngg'],
        suggestions: { wrngg: ['wrong'] },
        defaultLanguage: 'en-US',
    });

    const snapshot = service.analyze(model, { incremental: { dirtyBlockIds: [] } });

    assert.equal(snapshot.diagnosticCount, 1);
    assert.equal(snapshot.diagnostics[0].word, 'wrngg');
    assert.deepEqual(snapshot.diagnostics[0].suggestions, ['wrong']);
    assert.equal(JSON.stringify(model), before);
});

test('proofing service reuses word-list checker and suggestion provider boundary', () => {
    const model = createProofingModel('Known wrngg word.', {
        language: 'cs-CZ',
    });
    const suggestionCalls = [];
    const service = createCanvasProofingService({
        knownWords: ['known', 'word'],
        defaultLanguage: 'cs-CZ',
        suggestionProvider: {
            suggest: (word, context) => {
                suggestionCalls.push({ word, language: context.language, blockId: context.blockId });
                return ['wrong'];
            },
        },
    });

    const snapshot = service.analyze(model);

    assert.equal(snapshot.diagnosticCount, 1);
    assert.equal(snapshot.diagnostics[0].word, 'wrngg');
    assert.equal(snapshot.diagnostics[0].language, 'cs-CZ');
    assert.deepEqual(snapshot.diagnostics[0].suggestions, ['wrong']);
    assert.deepEqual(suggestionCalls, [{ word: 'wrngg', language: 'cs-CZ', blockId: 'spell-block' }]);
});

test('proofing service incrementally invalidates only dirty blocks', () => {
    const model = createProofingModel('wrngg in first block.', {
        extraBlocks: [textBlock('second-block', 'erorr in second block.')],
    });
    const checked = [];
    const service = createCanvasProofingService({
        checker: {
            isMisspelled: (word, context) => {
                checked.push(`${context.blockId}:${word}`);
                return ['wrngg', 'erorr'].includes(word.toLocaleLowerCase());
            },
        },
    });

    const first = service.analyze(model);
    assert.equal(first.diagnosticCount, 2);
    assert.ok(checked.some(value => value.startsWith('second-block:')));

    checked.length = 0;
    model.version = 2;
    model.body.blocks[0].content.runs[0].text = 'clean in first block.';
    const second = service.analyze(model, { incremental: { dirtyBlockIds: ['spell-block'] } });

    assert.equal(second.diagnosticCount, 1);
    assert.equal(second.diagnostics[0].blockId, 'second-block');
    assert.deepEqual(checked, ['spell-block:clean', 'spell-block:in', 'spell-block:first', 'spell-block:block']);
});

test('proofing service respects protected state comments revisions and run language', () => {
    const model = createProofingModel('', {
        isProtected: true,
        runs: [
            { id: 'deleted-run', type: 'text', text: 'wrngg ', marks: [{ type: 'revision', revisionId: 'rev-delete', value: 'Deletion' }] },
            { id: 'comment-run', type: 'text', text: 'cmnt ', language: 'cs-CZ', marks: [{ type: 'commentAnchor', commentAnchor: { commentId: 'comment-1' } }] },
            { id: 'revision-run', type: 'text', text: 'insrt ', language: 'cs-CZ', marks: [{ type: 'revision', revisionId: 'rev-insert', value: 'Insertion' }] },
            { id: 'disabled-language-run', type: 'text', text: 'wrngg', language: 'zxx', marks: [] },
        ],
    });
    const service = createCanvasProofingService({
        flaggedWords: ['wrngg', 'cmnt', 'insrt'],
        suggestions: {
            cmnt: ['comment'],
            insrt: ['insert'],
        },
        defaultLanguage: 'en-US',
    });

    const snapshot = service.analyze(model);

    assert.equal(snapshot.diagnosticCount, 2);
    assert.deepEqual(snapshot.diagnostics.map(item => item.word), ['cmnt', 'insrt']);
    assert.deepEqual(snapshot.diagnostics[0].commentIds, ['comment-1']);
    assert.deepEqual(snapshot.diagnostics[1].revisionIds, ['rev-insert']);
    assert.equal(snapshot.diagnostics[0].language, 'cs-CZ');
    assert.equal(snapshot.diagnostics[0].canApplyFix, false);
    assert.equal(snapshot.diagnostics[0].readonlyReason, 'protected');
});

test('replaceRange command applies a spelling suggestion as one undoable transaction', () => {
    let model = createProofingModel('The wrngg word is selected by context menu.');
    let selection = {
        anchor: { blockId: 'spell-block', offset: 4 },
        focus: { blockId: 'spell-block', offset: 9 },
    };
    const history = createCanvasHistoryController();
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history,
        commit: change => {
            model = change.model;
            selection = change.selection;
        },
    });

    const result = runtime.execCommand('replacerange', {
        blockId: 'spell-block',
        start: 4,
        end: 9,
        text: 'wrong',
    });

    assert.equal(result.handled, true);
    assert.equal(result.result.changed, true);
    assert.equal(blockText(model), 'The wrong word is selected by context menu.');
    assert.equal(history.snapshot().undoDepth, 1);

    runtime.execCommand('undo');
    assert.equal(blockText(model), 'The wrngg word is selected by context menu.');
});

function createProofingModel(text, options = {}) {
    const block = textBlock('spell-block', text, options);
    return {
        documentId: 'proofing-test',
        version: Number(options.version || 1) || 1,
        isProtected: options.isProtected === true,
        body: {
            blocks: [
                block,
                ...(options.extraBlocks || []),
            ],
        },
    };
}

function textBlock(id, text, options = {}) {
    return {
        id,
        type: 'paragraph',
        order: Number(options.order || 0) || 0,
        language: options.language || '',
        isProtected: options.isProtected === true,
        content: {
            runs: options.runs || [
                {
                    id: `${id}-run`,
                    type: 'text',
                    text,
                    marks: [],
                },
            ],
        },
    };
}

function blockText(model) {
    return model.body.blocks[0].content.runs.map(run => run.text).join('');
}
