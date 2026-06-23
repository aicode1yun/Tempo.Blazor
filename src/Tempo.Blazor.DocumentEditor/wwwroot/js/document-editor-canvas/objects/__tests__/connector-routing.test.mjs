import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../../layout/pagination.mjs';
import { buildDisplayList } from '../../render/display-list.mjs';

test('connector routing binds to source and target shape sites and updates after target move', () => {
    const model = connectorModel();
    const first = connectorCommand(model);
    const target = drawingById(model, 'target-shape');
    target.layout.position.x += 72;
    target.layout.position.y += 24;
    const second = connectorCommand(model);

    assert.equal(first.connector.routing, 'elbow');
    assert.equal(first.connector.points.length, 4);
    assert.equal(first.connector.startConnection.objectId, 'source-shape');
    assert.equal(first.connector.endConnection.objectId, 'target-shape');
    assert.ok(second.connector.end.x > first.connector.end.x);
    assert.ok(second.connector.end.y > first.connector.end.y);
});

function connectorCommand(model) {
    const display = buildDisplayList(model, layoutCanvasDocument(model, { fontMetrics: metrics() }), { fontMetrics: metrics() });
    const command = display.commands.find(item => item.type === 'drawingLine' && item.objectId === 'bound-connector');
    assert.ok(command);
    return command;
}

function connectorModel() {
    return {
        documentId: 'connector-routing-test',
        pageSettings: { width: 720, height: 960, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
        body: {
            blocks: [
                drawingBlock('source-block', 'source-shape', 1, 120, 72, 40, 120, {
                    preset: 'rectangle',
                    fill: { color: '#dbeafe' },
                    stroke: { color: '#2563eb', width: 2 },
                }),
                drawingBlock('target-block', 'target-shape', 1, 120, 72, 300, 150, {
                    preset: 'ellipse',
                    fill: { color: '#fef3c7' },
                    stroke: { color: '#d97706', width: 2 },
                }),
                drawingBlock('connector-block', 'bound-connector', 4, 300, 80, 0, 0, {
                    preset: 'bentConnector',
                    fill: { type: 'none' },
                    stroke: { color: '#16a34a', width: 2, endArrow: 'triangle' },
                    routing: 'elbow',
                    startConnection: { objectId: 'source-shape', site: 'right' },
                    endConnection: { objectId: 'target-shape', site: 'left' },
                }),
            ],
        },
    };
}

function drawingBlock(blockId, objectId, kind, width, height, x, y, shape) {
    return {
        id: blockId,
        type: 'paragraph',
        order: Number(blockId.includes('source') ? 1 : blockId.includes('target') ? 2 : 3),
        content: {
            type: 'paragraph',
            runs: [{
                id: `${objectId}-run`,
                type: 'drawing',
                drawing: {
                    objectId,
                    kind,
                    size: { width, height },
                    naturalSize: { width, height },
                    layout: {
                        kind: 1,
                        anchor: { blockId, offset: 0 },
                        position: { x, y },
                        wrap: { mode: 6 },
                        transform: { width, height },
                        stacking: { zIndex: kind },
                    },
                    shape,
                },
            }],
        },
    };
}

function drawingById(model, objectId) {
    const drawing = model.body.blocks
        .flatMap(block => block.content?.runs || [])
        .find(run => run.drawing?.objectId === objectId)
        ?.drawing;
    assert.ok(drawing);
    return drawing;
}

function metrics() {
    return {
        measureText(text, style = {}) {
            const size = Number(style.fontSize || 16) || 16;
            return { width: String(text || '').length * size * 0.52, height: size * 1.2 };
        },
    };
}
