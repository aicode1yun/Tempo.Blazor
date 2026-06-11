import assert from 'node:assert/strict';
import test from 'node:test';
import { paintDisplayList } from '../canvas-renderer.mjs';

test('renderer paints page frame, marked text, and annotations into their layers', () => {
    const layers = new Map([
        ['page-background', createCanvas()],
        ['content', createCanvas()],
        ['objects', createCanvas()],
        ['selection-caret', createCanvas()],
        ['annotations', createCanvas()],
        ['diagnostics', createCanvas()],
    ]);

    const summary = paintDisplayList(layers, {
        commands: [
            { id: 'fill', type: 'pageFill', layer: 'page-background', x: 0, y: 0, width: 794, height: 1123, fill: '#fff' },
            { id: 'body', type: 'bodyArea', layer: 'page-background', x: 96, y: 96, width: 602, height: 931, fill: 'rgba(248,250,252,.28)' },
            { id: 'border', type: 'pageBorder', layer: 'page-background', x: 0.5, y: 0.5, width: 793, height: 1122, stroke: '#cbd5e1' },
            {
                id: 'text',
                type: 'textRun',
                layer: 'content',
                text: 'Styled text',
                x: 96,
                y: 96,
                baseline: 116,
                width: 88,
                height: 24,
                style: {
                    fontFamily: 'Arial',
                    fontSize: 16,
                    fontWeight: '700',
                    fontStyle: 'italic',
                    color: '#1d4ed8',
                    backgroundColor: '#fde68a',
                    decorations: ['underline', 'line-through'],
                },
            },
            { id: 'image', type: 'imageObject', layer: 'objects', x: 96, y: 140, width: 120, height: 80, fill: '#e2e8f0', stroke: '#94a3b8', altText: 'Rendered image' },
            { id: 'comment', type: 'commentAnchor', layer: 'annotations', x: 96, y: 96, width: 88, height: 24 },
        ],
    });

    assert.equal(summary.paintedCommandCount, 6);
    assert.equal(summary.textRunCount, 1);
    assert.equal(layers.get('page-background').context.calls.filter(call => call.name === 'fillRect').length, 2);
    assert.ok(layers.get('content').context.calls.some(call => call.name === 'fillText' && call.args[0] === 'Styled text'));
    assert.ok(layers.get('content').context.calls.some(call => call.name === 'fillRect' && call.args[2] === 88));
    assert.equal(layers.get('content').context.calls.filter(call => call.name === 'lineTo').length, 2);
    assert.ok(layers.get('objects').context.calls.some(call => call.name === 'strokeRect'));
    assert.ok(layers.get('annotations').context.calls.some(call => call.name === 'stroke'));
});

test('renderer leaves diagnostic layer untouched when the display list has no diagnostic commands', () => {
    const layers = new Map([
        ['page-background', createCanvas()],
        ['content', createCanvas()],
        ['objects', createCanvas()],
        ['selection-caret', createCanvas()],
        ['annotations', createCanvas()],
        ['diagnostics', createCanvas()],
    ]);

    const summary = paintDisplayList(layers, {
        commands: [
            { id: 'fill', type: 'pageFill', layer: 'page-background', x: 0, y: 0, width: 100, height: 100, fill: '#fff' },
        ],
    });

    assert.equal(summary.diagnosticCount, 0);
    assert.equal(layers.get('diagnostics').context.calls.length, 0);
});

test('renderer paints math equation command as structured canvas strokes and glyphs', () => {
    const layers = new Map([
        ['page-background', createCanvas()],
        ['content', createCanvas()],
        ['objects', createCanvas()],
        ['selection-caret', createCanvas()],
        ['annotations', createCanvas()],
        ['diagnostics', createCanvas()],
    ]);

    const summary = paintDisplayList(layers, {
        commands: [{
            id: 'math-run-math',
            type: 'mathEquation',
            layer: 'content',
            x: 96,
            y: 96,
            baseline: 120,
            width: 64,
            height: 48,
            style: { fontFamily: 'Cambria Math', fontSize: 18, color: '#111827' },
            mathLayout: {
                type: 'fraction',
                width: 42,
                height: 44,
                ascent: 24,
                descent: 20,
                ruleY: 24,
                ruleWidth: 42,
                children: [
                    {
                        type: 'content',
                        x: 14,
                        y: 0,
                        width: 14,
                        height: 16,
                        ascent: 12,
                        descent: 4,
                        children: [{ type: 'run', x: 0, y: 0, text: 'a', width: 8, ascent: 12, descent: 4, style: { fontFamily: 'Cambria Math', fontSize: 13 } }],
                    },
                    {
                        type: 'content',
                        x: 14,
                        y: 28,
                        width: 14,
                        height: 16,
                        ascent: 12,
                        descent: 4,
                        children: [{ type: 'run', x: 0, y: 0, text: 'b', width: 8, ascent: 12, descent: 4, style: { fontFamily: 'Cambria Math', fontSize: 13 } }],
                    },
                ],
            },
        }],
    });

    const contentCalls = layers.get('content').context.calls;
    assert.equal(summary.mathEquationCount, 1);
    assert.ok(contentCalls.some(call => call.name === 'fillText' && call.args[0] === 'a'));
    assert.ok(contentCalls.some(call => call.name === 'fillText' && call.args[0] === 'b'));
    assert.ok(contentCalls.some(call => call.name === 'stroke'));
});

test('renderer paints advanced math boxes with border primitive and accent glyphs', () => {
    const layers = new Map([
        ['page-background', createCanvas()],
        ['content', createCanvas()],
        ['objects', createCanvas()],
        ['selection-caret', createCanvas()],
        ['annotations', createCanvas()],
        ['diagnostics', createCanvas()],
    ]);

    paintDisplayList(layers, {
        commands: [{
            id: 'math-advanced',
            type: 'mathEquation',
            layer: 'content',
            x: 80,
            y: 80,
            width: 90,
            height: 38,
            style: { fontFamily: 'Cambria Math', fontSize: 18, color: '#111827' },
            mathLayout: {
                type: 'borderBox',
                width: 74,
                height: 32,
                ascent: 22,
                descent: 10,
                children: [{
                    type: 'content',
                    x: 6,
                    y: 4,
                    width: 62,
                    height: 24,
                    ascent: 18,
                    descent: 6,
                    children: [{
                        type: 'accent',
                        x: 0,
                        y: 0,
                        width: 32,
                        height: 24,
                        ascent: 18,
                        descent: 6,
                        children: [
                            { type: 'run', x: 10, y: 0, text: '̂', width: 8, ascent: 8, descent: 2, style: { fontFamily: 'Cambria Math', fontSize: 12 } },
                            { type: 'run', x: 8, y: 8, text: 'x', width: 10, ascent: 12, descent: 4, style: { fontFamily: 'Cambria Math', fontSize: 18 } },
                        ],
                    }],
                }],
            },
        }],
    });

    const contentCalls = layers.get('content').context.calls;
    assert.ok(contentCalls.some(call => call.name === 'strokeRect'));
    assert.ok(contentCalls.some(call => call.name === 'fillText' && call.args[0] === '̂'));
    assert.ok(contentCalls.some(call => call.name === 'fillText' && call.args[0] === 'x'));
});

function createCanvas() {
    return {
        context: new RecordingContext(),
        getContext(type) {
            assert.equal(type, '2d');
            return this.context;
        },
    };
}

class RecordingContext {
    constructor() {
        this.calls = [];
    }

    save() {
        this.calls.push({ name: 'save', args: [] });
    }

    restore() {
        this.calls.push({ name: 'restore', args: [] });
    }

    fillRect(...args) {
        this.calls.push({ name: 'fillRect', args });
    }

    strokeRect(...args) {
        this.calls.push({ name: 'strokeRect', args });
    }

    fillText(...args) {
        this.calls.push({ name: 'fillText', args });
    }

    beginPath(...args) {
        this.calls.push({ name: 'beginPath', args });
    }

    moveTo(...args) {
        this.calls.push({ name: 'moveTo', args });
    }

    lineTo(...args) {
        this.calls.push({ name: 'lineTo', args });
    }

    stroke(...args) {
        this.calls.push({ name: 'stroke', args });
    }

    setLineDash(...args) {
        this.calls.push({ name: 'setLineDash', args });
    }

    drawImage(...args) {
        this.calls.push({ name: 'drawImage', args });
    }

    arc(...args) {
        this.calls.push({ name: 'arc', args });
    }

    fill(...args) {
        this.calls.push({ name: 'fill', args });
    }

    translate(...args) {
        this.calls.push({ name: 'translate', args });
    }

    rotate(...args) {
        this.calls.push({ name: 'rotate', args });
    }

    scale(...args) {
        this.calls.push({ name: 'scale', args });
    }
}

// Layer whose 2d context exposes a `canvas` back-reference with an Image constructor, so paintImageObject's
// resolveCachedImage can build/cache an image. `loaded` controls whether the bitmap reports itself decoded.
test('imageObject applies a rotation transform (translate + rotate about the centre) (P4)', () => {
    const rotated = createImageLayer(false);
    paintDisplayList(new Map([['objects', rotated]]), {
        commands: [{
            id: 'rot-img', type: 'imageObject', layer: 'objects',
            x: 100, y: 100, width: 200, height: 120,
            rotation: 30, fill: '#e2e8f0', stroke: '#94a3b8', altText: 'x',
        }],
    });
    const calls = rotated.context.calls;
    const rotate = calls.find(call => call.name === 'rotate');
    assert.ok(rotate, 'a rotated image issues a canvas rotate()');
    assert.ok(Math.abs(rotate.args[0] - (30 * Math.PI / 180)) < 1e-6, 'rotates by the command angle in radians');
    // Translate to the rect centre (200,160) before rotating, then back.
    assert.ok(calls.some(call => call.name === 'translate' && Math.abs(call.args[0] - 200) < 1e-6 && Math.abs(call.args[1] - 160) < 1e-6),
        'translates to the rect centre before rotating');

    const upright = createImageLayer(false);
    paintDisplayList(new Map([['objects', upright]]), {
        commands: [{ id: 'up-img', type: 'imageObject', layer: 'objects', x: 100, y: 100, width: 200, height: 120, altText: 'x' }],
    });
    assert.equal(upright.context.calls.some(call => call.name === 'rotate'), false, 'an un-rotated image issues no rotate()');
});

function createImageLayer(loaded) {
    const context = new RecordingContext();
    const view = {
        Image: function ImageStub() {
            return { decoding: '', onload: null, set src(_value) {}, complete: loaded === true, naturalWidth: loaded ? 16 : 0 };
        },
    };
    context.canvas = { ownerDocument: { defaultView: view }, __tmCanvasRepaint: null };
    return {
        context,
        getContext(type) {
            assert.equal(type, '2d');
            return this.context;
        },
    };
}

test('imageObject paints a grey placeholder until the bitmap is ready, then the bitmap with no fill beneath', () => {
    const loading = createImageLayer(false);
    paintDisplayList(new Map([['objects', loading]]), {
        commands: [{
            id: 'loading-img', type: 'imageObject', layer: 'objects',
            x: 10, y: 10, width: 50, height: 40,
            url: 'data:image/png;base64,LOADINGPLACEHOLDER', fill: '#e2e8f0', stroke: '#94a3b8', altText: 'x',
        }],
    });
    assert.ok(loading.context.calls.some(call => call.name === 'fillRect'), 'placeholder fill drawn while loading');
    assert.equal(loading.context.calls.some(call => call.name === 'drawImage'), false, 'no bitmap drawn before it is ready');

    const ready = createImageLayer(true);
    paintDisplayList(new Map([['objects', ready]]), {
        commands: [{
            id: 'ready-img', type: 'imageObject', layer: 'objects',
            x: 10, y: 10, width: 50, height: 40,
            url: 'data:image/png;base64,READYBITMAP', fill: '#e2e8f0', stroke: '#94a3b8', altText: 'x',
        }],
    });
    assert.ok(ready.context.calls.some(call => call.name === 'drawImage'), 'bitmap drawn once ready');
    assert.equal(ready.context.calls.some(call => call.name === 'fillRect'), false, 'no grey fill beneath the loaded bitmap');
});
