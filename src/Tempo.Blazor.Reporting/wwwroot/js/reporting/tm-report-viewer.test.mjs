import assert from 'node:assert/strict';
import test from 'node:test';
import {
    createPageCache,
    normalizeViewerState,
    resolvePageWindow,
    resolveZoomScale,
} from './tm-report-viewer.mjs';

test('normalizeViewerState clamps page and zoom values', () => {
    const state = normalizeViewerState({
        pageNumber: 99,
        pageCount: 4,
        zoomMode: 'Percent',
        zoomPercent: 900,
    });

    assert.equal(state.pageNumber, 4);
    assert.equal(state.zoomPercent, 400);
    assert.deepEqual(state.pageWindow, { start: 2, end: 4 });
});

test('resolvePageWindow keeps page virtualization inside document bounds', () => {
    assert.deepEqual(resolvePageWindow(1, 10, 2), { start: 1, end: 3 });
    assert.deepEqual(resolvePageWindow(5, 10, 2), { start: 3, end: 7 });
    assert.deepEqual(resolvePageWindow(10, 10, 2), { start: 8, end: 10 });
});

test('resolveZoomScale supports fit width, fit page and explicit percent', () => {
    const page = { width: 800, height: 1000 };
    const viewport = { width: 400, height: 500 };

    assert.equal(resolveZoomScale('FitWidth', 100, page, viewport), 0.5);
    assert.equal(resolveZoomScale('FitPage', 100, page, viewport), 0.5);
    assert.equal(resolveZoomScale('Percent', 125, page, viewport), 1.25);
});

test('createPageCache evicts least recently used pages', () => {
    const cache = createPageCache(2);
    cache.set('p1', 1);
    cache.set('p2', 2);
    assert.equal(cache.get('p1'), 1);
    cache.set('p3', 3);

    assert.deepEqual(cache.keys(), ['p1', 'p3']);
    assert.equal(cache.has('p2'), false);
});
