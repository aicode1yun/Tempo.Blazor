import test from 'node:test';
import assert from 'node:assert/strict';
import {
    applyCanvasViewCommand,
    CANVAS_VIEW_MODES,
    createCanvasViewState,
    queryCanvasViewCommandState,
    viewPresentation,
} from '../view-modes.mjs';
import { createPrintPreviewSnapshot } from '../print-preview.mjs';

test('view mode commands preserve scroll anchor and expose reading presentation', () => {
    const initial = createCanvasViewState({
        scrollAnchor: {
            pageIndex: 1,
            blockId: 'intro',
            offset: 7,
            viewportTop: 240,
        },
    });

    const result = applyCanvasViewCommand(initial, 'readingMode');
    assert.equal(result.handled, true);
    assert.equal(result.viewChanged, true);
    assert.equal(result.state.viewMode, CANVAS_VIEW_MODES.READING);
    assert.deepEqual(result.state.scrollAnchor, initial.scrollAnchor);

    const state = queryCanvasViewCommandState(result.state);
    assert.equal(state.commands.readingMode.active, true);
    assert.equal(state.view.toolbarHidden, true);
    assert.equal(viewPresentation(result.state).toolbarHidden, true);
});

test('view mode commands route zoom and print preview through the same state object', () => {
    let state = createCanvasViewState();
    const fit = applyCanvasViewCommand(state, 'fitWidth', null, {
        pageWidth: 800,
        pageHeight: 1000,
        viewportWidth: 1200,
        viewportHeight: 900,
    });
    state = fit.state;

    assert.equal(fit.viewChanged, true);
    assert.equal(state.zoom.preset, 'fitWidth');
    assert.equal(state.zoom.percent, 144);

    const preview = applyCanvasViewCommand(state, 'openPrintPreview');
    assert.equal(preview.state.printPreview.active, true);
    assert.equal(queryCanvasViewCommandState(preview.state).view.printPreview.active, true);
});

test('print preview snapshot is generated from rendered canvas display list', () => {
    const snapshot = createPrintPreviewSnapshot(
        { documentId: 'phase-e11-canvas-viewmodes-print' },
        { pages: [{ index: 0, width: 794, height: 1123 }] },
        {
            displayList: {
                pages: [{ index: 0, width: 794, height: 1123 }],
                commands: [
                    { type: 'pageBackground', layer: 'page-background' },
                    { type: 'textRun', layer: 'content', text: 'Print preview' },
                    { type: 'diagnostic', layer: 'diagnostics' },
                ],
            },
        },
        {
            viewMode: 'print',
            zoom: { percent: 100 },
            printPreview: { active: true },
        },
    );

    assert.equal(snapshot.active, true);
    assert.equal(snapshot.documentId, 'phase-e11-canvas-viewmodes-print');
    assert.equal(snapshot.pageCount, 1);
    assert.equal(snapshot.textRunCount, 1);
    assert.equal(snapshot.printableCommandCount, 2);
    assert.equal(snapshot.isBlank, false);
});
