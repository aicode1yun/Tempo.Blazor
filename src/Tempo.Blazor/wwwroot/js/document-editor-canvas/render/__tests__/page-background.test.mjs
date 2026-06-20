import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../display-list.mjs';

test('display list emits configured page fill, watermark and page border commands', () => {
    const model = {
        documentId: 'phase-e12-page-background',
        pageSettings: { width: 420, height: 560, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        pageBackground: {
            color: '#f8fafc',
            watermark: { enabled: true, kind: 'text', text: 'CONFIDENTIAL', opacity: 0.18, rotation: -32, color: '#64748b' },
            border: { enabled: true, color: '#2563eb', width: 2, margin: 18, alignTo: 'page', dash: [8, 4] },
        },
        body: { blocks: [paragraph('p1', 'Background test')] },
    };

    const display = buildDisplayList(model, { pageSettings: model.pageSettings }, { fontMetrics: metrics() });
    const fill = display.commands.find(command => command.type === 'pageFill');
    const watermark = display.commands.find(command => command.type === 'watermarkText');
    const border = display.commands.find(command => command.type === 'pageBorder');

    assert.equal(fill.fill, '#f8fafc');
    assert.equal(watermark.text, 'CONFIDENTIAL');
    assert.equal(watermark.rotation, -32);
    assert.equal(border.stroke, '#2563eb');
    assert.deepEqual(border.dash, [8, 4]);
    assert.equal(border.x, 18.5);
});

function paragraph(id, text) {
    return {
        id,
        type: 'paragraph',
        order: 1,
        paragraphProperties: {},
        content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
    };
}

function metrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, text.length * fontSize * 0.5),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
