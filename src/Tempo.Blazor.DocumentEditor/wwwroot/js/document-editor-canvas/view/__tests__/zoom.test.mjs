import test from 'node:test';
import assert from 'node:assert/strict';
import {
    applyZoomCommand,
    computePresetScale,
    normalizeZoomState,
    percentToScale,
    scaleToPercent,
    ZOOM_PRESETS,
} from '../zoom-controller.mjs';

test('zoom controller normalizes percent and clamps the production zoom range', () => {
    assert.deepEqual(normalizeZoomState({ percent: 125 }), {
        preset: ZOOM_PRESETS.CUSTOM,
        scale: 1.25,
        percent: 125,
    });
    assert.equal(percentToScale(10), 0.25);
    assert.equal(percentToScale(600), 4);
    assert.equal(scaleToPercent(1.375), 138);
});

test('zoom controller computes fit width, fit page and multiple pages from viewport metrics', () => {
    const metrics = {
        pageWidth: 800,
        pageHeight: 1000,
        viewportWidth: 1200,
        viewportHeight: 900,
        pageGap: 24,
        paddingInline: 48,
        paddingBlock: 48,
    };

    assert.equal(computePresetScale(ZOOM_PRESETS.FIT_WIDTH, metrics), 1.44);
    assert.equal(computePresetScale(ZOOM_PRESETS.FIT_PAGE, metrics), 0.852);
    assert.equal(computePresetScale(ZOOM_PRESETS.MULTIPLE_PAGES, metrics), 0.709);
});

test('zoom commands expose changed view state without mutating document content', () => {
    const metrics = {
        pageWidth: 794,
        pageHeight: 1123,
        viewportWidth: 1440,
        viewportHeight: 1000,
    };

    const fitWidth = applyZoomCommand(normalizeZoomState(), 'fitWidth', null, metrics);
    assert.equal(fitWidth.handled, true);
    assert.equal(fitWidth.changed, true);
    assert.equal(fitWidth.state.preset, ZOOM_PRESETS.FIT_WIDTH);
    assert.ok(fitWidth.state.percent > 100);

    const custom = applyZoomCommand(fitWidth.state, 'setZoom', { percent: 80 }, metrics);
    assert.equal(custom.state.preset, ZOOM_PRESETS.CUSTOM);
    assert.equal(custom.state.scale, 0.8);

    const wheel = applyZoomCommand(custom.state, 'ctrlWheelZoom', { deltaY: -100 }, metrics);
    assert.equal(wheel.state.preset, ZOOM_PRESETS.CUSTOM);
    assert.equal(wheel.state.percent, 88);
});
