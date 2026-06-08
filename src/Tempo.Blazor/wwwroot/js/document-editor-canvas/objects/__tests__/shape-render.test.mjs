import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../../layout/pagination.mjs';
import { buildDisplayList } from '../../render/display-list.mjs';

test('shape render display list exposes metadata plus separate shadow fill and stroke paint steps', () => {
    const model = drawingModel({
        objectId: 'shape-render-shadow',
        kind: 1,
        shape: {
            preset: 'diamond',
            fill: { type: 'linearGradient', color: '#dbeafe', secondaryColor: '#bfdbfe', opacity: 0.9, angle: 45 },
            stroke: { color: '#2563eb', width: 2, dash: 'dash' },
            shadow: { color: 'rgba(15, 23, 42, 0.18)', blur: 6, offsetY: 3 },
        },
    });

    const display = buildDisplayList(model, layoutCanvasDocument(model, { fontMetrics: metrics() }), { fontMetrics: metrics() });
    const commands = display.commands.filter(command => command.objectId === 'shape-render-shadow');

    assert.deepEqual(commands.map(command => command.type), [
        'drawingShape',
        'drawingShapeEffect',
        'drawingShapeFill',
        'drawingShapeStroke',
    ]);
    assert.equal(commands[0].metadataOnly, true);
    assert.equal(commands[1].paintPart, 'effect');
    assert.equal(commands[2].shape.fill.type, 'linearGradient');
    assert.equal(commands[3].shape.stroke.dash, 'dash');
});

function drawingModel(drawing) {
    return {
        documentId: 'shape-render-test',
        pageSettings: { width: 720, height: 960, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
        body: {
            blocks: [{
                id: 'shape-block',
                type: 'paragraph',
                order: 1,
                content: {
                    type: 'paragraph',
                    runs: [{
                        id: `${drawing.objectId}-run`,
                        type: 'drawing',
                        drawing: {
                            objectId: drawing.objectId,
                            kind: drawing.kind,
                            size: { width: 160, height: 96 },
                            naturalSize: { width: 160, height: 96 },
                            layout: {
                                kind: 1,
                                anchor: { blockId: 'shape-block', offset: 0 },
                                position: { x: 36, y: 48 },
                                wrap: { mode: 6 },
                                transform: { width: 160, height: 96 },
                                stacking: { zIndex: 1 },
                            },
                            shape: drawing.shape,
                        },
                    }],
                },
            }],
        },
    };
}

function metrics() {
    return {
        measureText(text, style = {}) {
            const size = Number(style.fontSize || 16) || 16;
            return { width: String(text || '').length * size * 0.52, height: size * 1.2 };
        },
    };
}
