import assert from 'node:assert/strict';
import test from 'node:test';
import { drawingTextLayoutHeight, layoutDrawingTextLines } from '../textbox-layout.mjs';

test('textbox layout uses paragraph engine wrapping and deterministic line metrics', () => {
    const lines = layoutDrawingTextLines({
        wrapText: true,
        paragraphs: [{
            text: 'Alpha beta gamma delta epsilon zeta',
            alignment: 'center',
            style: { fontSize: 14, color: '#0f172a' },
        }],
    }, 96, metrics());

    assert.ok(lines.length >= 3);
    assert.equal(lines.every(line => line.alignment === 'center'), true);
    assert.equal(lines.every(line => line.width === 96), true);
    assert.ok(lines[1].y > lines[0].y);
    assert.ok(drawingTextLayoutHeight(lines) >= lines.at(-1).y + lines.at(-1).lineHeight);
});

test('textbox layout honors explicit non-wrapping text while preserving paragraph alignment', () => {
    const lines = layoutDrawingTextLines({
        wrapText: false,
        paragraphs: [{
            text: 'One visual line even when the content is wider than the shape',
            alignment: 'right',
            style: { fontSize: 13, italic: true },
        }],
    }, 72, metrics());

    assert.equal(lines.length, 1);
    assert.equal(lines[0].alignment, 'right');
    assert.equal(lines[0].style.fontStyle, 'italic');
    assert.equal(lines[0].width, 72);
    assert.ok(lines[0].segments.reduce((sum, segment) => sum + segment.width, 0) > 72);
});

function metrics() {
    return {
        measureText(text, style = {}) {
            const size = Number(style.fontSize || 16) || 16;
            return { width: String(text || '').length * size * 0.52, height: size * 1.2 };
        },
    };
}
