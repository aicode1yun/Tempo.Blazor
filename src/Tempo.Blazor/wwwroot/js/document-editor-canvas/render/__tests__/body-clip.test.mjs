import assert from 'node:assert/strict';
import test from 'node:test';
import { paintDisplayList } from '../canvas-renderer.mjs';

// Phase 1.1 (perf+rendering fix 2026-06-08): the optional body clip must wrap the painted commands
// in save()/rect()/clip()/restore() on each touched layer context, and must be a no-op when absent.

test('clipRect applies a save/clip/restore around body commands on each touched context', () => {
    const layers = new Map([
        ['content', recordingCanvas()],
        ['objects', recordingCanvas()],
    ]);

    paintDisplayList(layers, {
        commands: [
            textRun('content', 'r1', 60, 80),
            textRun('content', 'r2', 60, 100),
        ],
    }, { clipRect: { x: 48, y: 48, width: 500, height: 700 } });

    const content = layers.get('content').context.calls.map(call => call.name);
    // Exactly one body clip is established per touched context (paintTextRun does its own
    // save/restore, so only `clip` uniquely identifies the body-clip wrapper).
    assert.equal(content.filter(name => name === 'clip').length, 1, 'one clip per touched context');

    const clipIndex = content.indexOf('clip');
    const firstFill = content.indexOf('fillText');
    assert.ok(clipIndex >= 0 && firstFill > clipIndex, 'text is painted inside the clip');
    // The clip wrapper releases its context after the last paint (a restore follows the last fill).
    assert.ok(content.lastIndexOf('restore') > content.lastIndexOf('fillText'), 'clip is released after painting');

    const rectCall = layers.get('content').context.calls.find(call => call.name === 'rect');
    assert.deepEqual(rectCall.args, [48, 48, 500, 700], 'clip uses the supplied body rect');

    // The objects context was never used, so it is not clipped.
    assert.equal(layers.get('objects').context.calls.length, 0);
});

test('no clipRect leaves painting unclipped (no save/clip/restore)', () => {
    const layers = new Map([['content', recordingCanvas()]]);
    paintDisplayList(layers, { commands: [textRun('content', 'r1', 60, 80)] }, {});
    const names = layers.get('content').context.calls.map(call => call.name);
    assert.equal(names.includes('clip'), false);
    assert.equal(names.includes('rect'), false);
});

function textRun(layer, id, x, y) {
    return {
        id,
        type: 'textRun',
        layer,
        pageIndex: 0,
        text: 'Body text run',
        x,
        y,
        baseline: y + 12,
        width: 120,
        height: 16,
        style: { fontFamily: 'Arial', fontSize: 12, color: '#111' },
    };
}

function recordingCanvas() {
    const context = {
        calls: [],
        save() { this.calls.push({ name: 'save', args: [] }); },
        restore() { this.calls.push({ name: 'restore', args: [] }); },
        beginPath(...args) { this.calls.push({ name: 'beginPath', args }); },
        rect(...args) { this.calls.push({ name: 'rect', args }); },
        clip(...args) { this.calls.push({ name: 'clip', args }); },
        fillText(...args) { this.calls.push({ name: 'fillText', args }); },
        fillRect(...args) { this.calls.push({ name: 'fillRect', args }); },
        strokeRect(...args) { this.calls.push({ name: 'strokeRect', args }); },
        measureText(text) { return { width: String(text).length * 7 }; },
        set fillStyle(_value) {},
        get fillStyle() { return '#000'; },
        set font(_value) {},
        get font() { return '12px Arial'; },
        set textBaseline(_value) {},
        set globalAlpha(_value) {},
        get globalAlpha() { return 1; },
        setTransform() {},
        translate() {},
        scale() {},
        rotate() {},
    };
    return {
        context,
        getContext() { return context; },
    };
}
