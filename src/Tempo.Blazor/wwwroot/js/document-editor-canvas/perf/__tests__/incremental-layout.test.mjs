import assert from 'node:assert/strict';
import test from 'node:test';
import { createRecalcInfo, dirtyPagesFromDisplayList } from '../recalc-info.mjs';
import { createPerformanceMetrics } from '../runtime-metrics.mjs';
import { createFontMetricsService } from '../../../document-editor/layout/font-metrics.mjs';

test('recalc info marks dirty paragraphs and starts reconciliation from the first dirty block', () => {
    const recalc = createRecalcInfo({ scheduleIdle: callback => callback() });
    recalc.updateBlockOrder({
        body: {
            blocks: [
                { id: 'p0' },
                { id: 'p1' },
                { id: 'p2' },
                { id: 'p3' },
            ],
        },
    });

    const dirty = recalc.markDirty(['p2', 'p1']);
    const options = recalc.immediateRenderOptions();
    const queued = recalc.queueIdleReconciliation();

    assert.equal(dirty.firstDirtyBlockIndex, 1);
    assert.deepEqual(options.dirtyBlockIds, ['p2', 'p1']);
    assert.equal(options.incremental, true);
    assert.equal(options.firstDirtyBlockIndex, 1);
    assert.equal(queued, true);
    assert.equal(recalc.snapshot().idleReconciliationCount, 1);
    assert.equal(recalc.snapshot().dirtyBlockCount, 0);
    assert.equal(recalc.snapshot().firstDirtyBlockIndex, -1);
    assert.equal(recalc.snapshot().lastFirstDirtyBlockIndex, 1);
});

test('dirty page mapping repaints from the first dirty page only when structural flow changes', () => {
    const displayList = {
        pages: [{ index: 0 }, { index: 1 }, { index: 2 }, { index: 3 }],
        commands: [
            { blockId: 'p0', pageIndex: 0 },
            { blockId: 'p1', pageIndex: 1 },
            { blockId: 'p2', pageIndex: 2 },
            { blockId: 'p3', pageIndex: 3 },
        ],
    };

    assert.deepEqual(dirtyPagesFromDisplayList(displayList, ['p2']), [2]);
    assert.deepEqual(dirtyPagesFromDisplayList(displayList, ['p2'], { structural: true }), [2, 3]);
});

test('performance metrics expose bounded typing p50 and p95 samples', () => {
    let current = 100;
    const metrics = createPerformanceMetrics({ maxSamples: 3, now: () => current });

    metrics.recordRender(12, { mountedPageCount: 1 });
    metrics.recordTypingLatency(6);
    metrics.recordTypingLatency(18);
    metrics.recordTypingLatency(30);
    metrics.recordTypingLatency(42);
    current = 140;
    metrics.recordScrollFrame(9);

    const snapshot = metrics.snapshot();
    assert.equal(snapshot.firstPaintMs, 0);
    assert.equal(snapshot.typing.count, 3);
    assert.equal(snapshot.typing.p50Ms, 30);
    assert.equal(snapshot.typing.p95Ms, 42);
    assert.equal(snapshot.scroll.count, 1);
});

test('font measurement cache enforces bounded LRU entries', () => {
    const service = createFontMetricsService({ cacheLimit: 2, createMeasureContext: () => null });

    service.measureText('Alpha', { fontSize: 16 });
    service.measureText('Beta', { fontSize: 16 });
    service.measureText('Gamma', { fontSize: 16 });

    const stats = service.getStats();
    assert.equal(stats.MeasureCacheSize, 2);
    assert.equal(stats.MeasureEvictions, 1);
});
