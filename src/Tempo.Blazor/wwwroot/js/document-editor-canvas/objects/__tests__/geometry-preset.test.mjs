import assert from 'node:assert/strict';
import test from 'node:test';
import {
    applyPresetGeometryPath,
    buildPresetGeometryPath,
    buildPresetStretchGuides,
    normalizePresetName,
} from '../geometry-preset.mjs';

const BOX = Object.freeze({ x: 10, y: 20, width: 160, height: 90 });

test('preset geometry generator supports core E7 shape families deterministically', () => {
    const presets = [
        'rectangle',
        'roundRectangle',
        'ellipse',
        'triangle',
        'rightTriangle',
        'diamond',
        'pentagon',
        'hexagon',
        'star5',
        'star6',
        'rightArrow',
        'leftArrow',
        'upArrow',
        'downArrow',
        'callout',
        'line',
        'bentConnector',
    ];

    for (const preset of presets) {
        const first = buildPresetGeometryPath(preset, BOX);
        const second = buildPresetGeometryPath(preset, BOX);
        assert.deepEqual(second, first, `${preset} path must be deterministic`);
        assert.ok(first.length >= 1, `${preset} should produce at least one command`);
        assert.equal(typeof first[0].command, 'string', `${preset} should produce command objects`);
    }

    assert.equal(normalizePresetName('Round Rectangle'), 'roundrectangle');
    assert.equal(normalizePresetName('right-arrow'), 'rightarrow');
});

test('adjust values mutate round rectangle radius and arrow head geometry', () => {
    const roundSmall = buildPresetGeometryPath('roundRectangle', BOX, { radius: 0.08 });
    const roundLarge = buildPresetGeometryPath('roundRectangle', BOX, { radius: 0.34 });
    assert.notDeepEqual(roundLarge, roundSmall);
    assert.ok(roundLarge[0].x > roundSmall[0].x);

    const arrowNarrow = buildPresetGeometryPath('rightArrow', BOX, { arrowHead: 0.22 });
    const arrowWide = buildPresetGeometryPath('rightArrow', BOX, { arrowHead: 0.58 });
    assert.notDeepEqual(arrowWide, arrowNarrow);
    assert.ok(arrowWide[1].x < arrowNarrow[1].x);

    const calloutDefault = buildPresetGeometryPath('callout', BOX);
    const calloutShifted = buildPresetGeometryPath('callout', BOX, { tailX: 0.32, tailY: 1.18 });
    assert.notDeepEqual(calloutShifted, calloutDefault);
});

test('preset path can be replayed against a canvas context', () => {
    const calls = [];
    const context = {
        beginPath: () => calls.push('beginPath'),
        moveTo: () => calls.push('moveTo'),
        lineTo: () => calls.push('lineTo'),
        quadraticCurveTo: () => calls.push('quadraticCurveTo'),
        ellipse: () => calls.push('ellipse'),
        closePath: () => calls.push('closePath'),
    };

    assert.equal(applyPresetGeometryPath(context, buildPresetGeometryPath('ellipse', BOX)), true);
    assert.equal(applyPresetGeometryPath(context, buildPresetGeometryPath('roundRectangle', BOX)), true);
    assert.equal(calls.includes('ellipse'), true);
    assert.equal(calls.includes('quadraticCurveTo'), true);
    assert.equal(calls.includes('closePath'), true);
});

test('preset stretch guides expose clean-room arrow shaft and callout text regions', () => {
    const rightArrowNarrow = buildPresetStretchGuides('rightArrow', BOX, { arrowHead: 0.22 });
    const rightArrowWide = buildPresetStretchGuides('rightArrow', BOX, { arrowHead: 0.58 });
    const narrowBoundary = rightArrowNarrow.find(guide => guide.name === 'headBoundary');
    const wideBoundary = rightArrowWide.find(guide => guide.name === 'headBoundary');
    const shaftRect = rightArrowWide.find(guide => guide.name === 'shaftRect');

    assert.equal(narrowBoundary.axis, 'x');
    assert.ok(wideBoundary.position < narrowBoundary.position);
    assert.equal(shaftRect.axis, 'rect');
    assert.ok(shaftRect.width > 20);
    assert.ok(shaftRect.height > 20);

    const calloutDefault = buildPresetStretchGuides('callout', BOX);
    const calloutShifted = buildPresetStretchGuides('callout', BOX, { tailX: 0.32, tailY: 1.18 });
    const textRect = calloutDefault.find(guide => guide.name === 'textRect');
    const defaultTailX = calloutDefault.find(guide => guide.name === 'tailPointX');
    const shiftedTailX = calloutShifted.find(guide => guide.name === 'tailPointX');

    assert.deepEqual(textRect, { name: 'textRect', axis: 'rect', x: 10, y: 20, width: 160, height: 70.2 });
    assert.ok(shiftedTailX.position < defaultTailX.position);
    assert.notDeepEqual(calloutShifted, calloutDefault);
});
