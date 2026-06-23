import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { layoutCanvasDocument } from '../../layout/pagination.mjs';
import { buildDisplayList } from '../../render/display-list.mjs';
import { paintCommand } from '../../render/canvas-renderer.mjs';

test('canvas drawings render shape, text box, line and chart display commands', () => {
    const model = createDrawingModel();
    const layout = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const display = buildDisplayList(model, layout, { fontMetrics: metrics() });

    assert.equal(display.commands.some(command => command.type === 'drawingShape' && command.objectId === 'e7-rounded-shape'), true);
    assert.equal(display.commands.some(command => command.type === 'drawingShapeEffect' && command.objectId === 'e7-rounded-shape'), true);
    assert.equal(display.commands.some(command => command.type === 'drawingShapeFill' && command.objectId === 'e7-rounded-shape'), true);
    assert.equal(display.commands.some(command => command.type === 'drawingShapeStroke' && command.objectId === 'e7-rounded-shape'), true);
    assert.equal(display.commands.some(command => command.type === 'drawingShape' && command.objectId === 'e7-textbox'), true);
    assert.equal(display.commands.some(command => command.type === 'drawingText' && command.text === 'Canvas text box'), true);
    assert.equal(display.commands.some(command => command.type === 'drawingLine' && command.objectId === 'e7-arrow-line'), true);
    assert.equal(display.commands.some(command => command.type === 'drawingChart' && command.objectId === 'e7-bar-chart'), true);
});

test('shape display commands keep deterministic metadata and separate effect fill stroke paint steps', () => {
    const model = createDrawingModel();
    const layout = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const first = buildDisplayList(model, layout, { fontMetrics: metrics() });
    const second = buildDisplayList(model, layout, { fontMetrics: metrics() });
    const commands = first.commands.filter(command => command.objectId === 'e7-rounded-shape');

    assert.deepEqual(
        commands.map(command => [command.type, command.paintPart || '', command.metadataOnly === true]),
        [
            ['drawingShape', '', true],
            ['drawingShapeEffect', 'effect', false],
            ['drawingShapeFill', 'fill', false],
            ['drawingShapeStroke', 'stroke', false],
        ]);
    assert.deepEqual(
        first.commands.filter(command => command.objectId === 'e7-rounded-shape'),
        second.commands.filter(command => command.objectId === 'e7-rounded-shape'));
});

test('chart layout command covers bars, labels and legend without mutating source chart data', () => {
    const model = createDrawingModel();
    const layout = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const display = buildDisplayList(model, layout, { fontMetrics: metrics() });
    const command = display.commands.find(item => item.type === 'drawingChart' && item.objectId === 'e7-bar-chart');

    assert.ok(command);
    assert.equal(command.chart.title, 'Quarterly trend');
    assert.equal(command.chartLayout.type, 'bar');
    assert.equal(command.chartLayout.seriesLayouts[0].bars.length, 3);
    assert.deepEqual(command.chartLayout.categoryLabels.map(label => label.text), ['Q1', 'Q2', 'Q3']);
    assert.equal(command.chartLayout.legendItems[0].name, 'Actual');
    assert.ok(command.chartLayout.plotRect.width > 100);
});

test('drawing text boxes wrap text inside insets and honor middle vertical alignment', () => {
    const model = {
        documentId: 'e7-textbox-layout-test',
        pageSettings: { width: 720, height: 960, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
        body: {
            blocks: [
                paragraph('anchor', 'Text box anchor'),
                drawingBlock('textbox-wrap-block', 'e7-wrapped-textbox', 2, 132, 124, {
                    preset: 'rectangle',
                    fill: { color: '#f8fafc', opacity: 1 },
                    stroke: { color: '#475569', width: 1 },
                }, {
                    insetLeft: 10,
                    insetTop: 10,
                    insetRight: 10,
                    insetBottom: 10,
                    verticalAlignment: 'middle',
                    wrapText: true,
                    paragraphs: [{
                        text: 'Alpha beta gamma delta epsilon zeta eta theta',
                        alignment: 'center',
                        style: { fontSize: 14, color: '#0f172a' },
                    }],
                }),
            ],
        },
    };
    const layout = layoutCanvasDocument(model, { fontMetrics: metrics() });
    const display = buildDisplayList(model, layout, { fontMetrics: metrics() });
    const shape = display.commands.find(command => command.type === 'drawingShape' && command.objectId === 'e7-wrapped-textbox');
    const textCommands = display.commands.filter(command => command.type === 'drawingText' && command.objectId === 'e7-wrapped-textbox');

    assert.ok(shape);
    assert.ok(textCommands.length >= 2);
    assert.equal(textCommands.every(command => command.align === 'center'), true);
    assert.equal(textCommands.every(command => command.width === 112), true);
    assert.ok(textCommands[0].y > shape.y + 10);
    assert.ok(textCommands.at(-1).y + textCommands.at(-1).height <= shape.y + shape.height - 10);
});

test('drawing insert commands create undoable drawing-run blocks with typed payloads', () => {
    let model = {
        documentId: 'e7-command-test',
        body: { blocks: [paragraph('anchor', 'Anchor text')] },
    };
    let selection = { anchor: { blockId: 'anchor', offset: 6 }, focus: { blockId: 'anchor', offset: 6 } };
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    const inserted = runtime.execCommand('insertTextBox', {
        objectId: 'inserted-textbox',
        text: 'Inserted drawing text',
        width: 180,
        height: 72,
        shape: { preset: 'roundRectangle', fill: { color: '#dbeafe' }, stroke: { color: '#2563eb', width: 2 } },
    }).result;

    assert.equal(inserted.changed, true);
    const block = model.body.blocks.find(item => item.id === 'inserted-textbox-paragraph');
    assert.ok(block);
    assert.equal(block.content.runs[0].drawing.kind, 2);
    assert.equal(block.content.runs[0].drawing.textBody.paragraphs[0].text, 'Inserted drawing text');

    const display = buildDisplayList(model, layoutCanvasDocument(model, { fontMetrics: metrics() }), { fontMetrics: metrics() });
    assert.equal(display.commands.some(command => command.type === 'drawingText' && command.objectId === 'inserted-textbox'), true);

    assert.equal(runtime.execCommand('undo').result.changed, true);
    assert.equal(model.body.blocks.some(item => item.id === 'inserted-textbox-paragraph'), false);

    assert.equal(runtime.execCommand('redo').result.changed, true);
    assert.equal(model.body.blocks.some(item => item.id === 'inserted-textbox-paragraph'), true);

    const deleted = runtime.execCommand('deleteObject', { objectId: 'inserted-textbox' }).result;
    assert.equal(deleted.changed, true);
    assert.equal(model.body.blocks.some(item => item.id === 'inserted-textbox-paragraph'), false);
    assert.equal(deleted.dirtyBlockIds.includes('inserted-textbox-paragraph'), true);

    assert.equal(runtime.execCommand('undo').result.changed, true);
    assert.equal(model.body.blocks.some(item => item.id === 'inserted-textbox-paragraph'), true);
});

test('textbox edit commands update nested drawing text with selection and undo history', () => {
    let model = createDrawingModel();
    let selection = {
        anchor: { blockId: 'textbox-block', offset: 0 },
        focus: { blockId: 'textbox-block', offset: 0 },
        object: {
            objectId: 'e7-textbox',
            blockId: 'textbox-block',
            runId: 'e7-textbox-run',
            kind: 'textBox',
        },
    };
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    const activated = runtime.execCommand('activateTextBoxEdit', { objectId: 'e7-textbox', offset: 6 }).result;
    assert.equal(activated.changed, false);
    assert.equal(activated.selectionChanged, true);
    assert.equal(selection.object.textBox.active, true);
    assert.equal(selection.object.textBox.offset, 6);

    const inserted = runtime.execCommand('insertTextBoxText', { text: ' edited' }).result;
    assert.equal(inserted.changed, true);
    assert.equal(drawingById(model, 'e7-textbox').textBody.paragraphs[0].text, 'Canvas edited text box');
    assert.equal(selection.object.textBox.offset, 13);

    const paragraph = runtime.execCommand('insertTextBoxParagraph', {}).result;
    assert.equal(paragraph.changed, true);
    assert.equal(drawingById(model, 'e7-textbox').textBody.paragraphs.length, 2);

    const typedSecondLine = runtime.execCommand('insertTextBoxText', { text: 'Second line' }).result;
    assert.equal(typedSecondLine.changed, true);
    assert.equal(drawingById(model, 'e7-textbox').textBody.paragraphs[1].text, 'Second line text box');

    const alignment = runtime.execCommand('setTextBoxTextAlignment', { alignment: 'right', all: true }).result;
    assert.equal(alignment.changed, true);
    assert.equal(drawingById(model, 'e7-textbox').textBody.paragraphs.every(item => item.alignment === 'right'), true);

    const styled = runtime.execCommand('setTextBoxTextStyle', { style: { italic: true, fontSize: 18 }, all: false }).result;
    assert.equal(styled.changed, true);
    assert.equal(drawingById(model, 'e7-textbox').textBody.paragraphs[1].style.italic, true);
    assert.equal(drawingById(model, 'e7-textbox').textBody.paragraphs[1].style.fontSize, 18);

    assert.equal(runtime.execCommand('undo').result.changed, true);
    assert.equal(drawingById(model, 'e7-textbox').textBody.paragraphs[1].style.italic, false);

    assert.equal(runtime.execCommand('redo').result.changed, true);
    assert.equal(drawingById(model, 'e7-textbox').textBody.paragraphs[1].style.italic, true);
});

test('chart data command updates chart payload through undoable drawing command boundary', () => {
    let model = createDrawingModel();
    let selection = { anchor: { blockId: 'anchor', offset: 0 }, focus: { blockId: 'anchor', offset: 0 } };
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    const result = runtime.execCommand('updateChartData', {
        objectId: 'e7-bar-chart',
        chart: {
            type: 'line',
            title: 'Updated trend',
            categories: ['Jan', 'Feb', 'Mar'],
            series: [{ name: 'Forecast', values: [6, 9, 7], color: '#dc2626' }],
        },
    }).result;

    assert.equal(result.changed, true);
    let chart = drawingById(model, 'e7-bar-chart').chart;
    assert.equal(chart.type, 'line');
    assert.equal(chart.title, 'Updated trend');
    assert.equal(chart.series[0].values[1], 9);

    assert.equal(runtime.execCommand('undo').result.changed, true);
    chart = drawingById(model, 'e7-bar-chart').chart;
    assert.equal(chart.type, 'bar');
    assert.equal(chart.title, 'Quarterly trend');
});

test('connector routing binds endpoints to shape connection sites and recomputes after shape move', () => {
    const model = {
        documentId: 'e7-connector-routing-test',
        pageSettings: { width: 720, height: 960, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
        body: {
            blocks: [
                paragraph('anchor', 'Connector anchor'),
                drawingBlock('source-shape', 'e7-source-shape', 1, 120, 72, {
                    preset: 'rectangle',
                    fill: { color: '#dbeafe' },
                    stroke: { color: '#2563eb', width: 2 },
                }, null, null, { x: 24, y: 120 }),
                drawingBlock('target-shape', 'e7-target-shape', 1, 120, 72, {
                    preset: 'ellipse',
                    fill: { color: '#fef3c7' },
                    stroke: { color: '#d97706', width: 2 },
                }, null, null, { x: 264, y: 160 }),
                drawingBlock('connector', 'e7-connector', 4, 280, 80, {
                    preset: 'bentConnector',
                    fill: { type: 'none' },
                    stroke: { color: '#16a34a', width: 2, endArrow: 'triangle' },
                    routing: 'elbow',
                    startConnection: { objectId: 'e7-source-shape', site: 'right' },
                    endConnection: { objectId: 'e7-target-shape', site: 'left' },
                }, null, null, { x: 0, y: 0 }),
            ],
        },
    };
    const firstDisplay = buildDisplayList(model, layoutCanvasDocument(model, { fontMetrics: metrics() }), { fontMetrics: metrics() });
    const firstConnector = firstDisplay.commands.find(command => command.type === 'drawingLine' && command.objectId === 'e7-connector');

    assert.ok(firstConnector);
    assert.equal(firstConnector.connector.points.length, 4);
    assert.equal(firstConnector.connector.startConnection.objectId, 'e7-source-shape');
    assert.equal(firstConnector.connector.endConnection.objectId, 'e7-target-shape');

    drawingById(model, 'e7-target-shape').layout.position.x += 60;
    const secondDisplay = buildDisplayList(model, layoutCanvasDocument(model, { fontMetrics: metrics() }), { fontMetrics: metrics() });
    const secondConnector = secondDisplay.commands.find(command => command.type === 'drawingLine' && command.objectId === 'e7-connector');

    assert.ok(secondConnector.connector.end.x > firstConnector.connector.end.x);
    assert.ok(secondConnector.connector.points.at(-1).x > firstConnector.connector.points.at(-1).x);
});

test('connector endpoint command detaches the dragged endpoint and persists free-point geometry', () => {
    let model = {
        documentId: 'e7-connector-endpoint-test',
        pageSettings: { width: 720, height: 960, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
        body: {
            blocks: [
                paragraph('anchor', 'Connector endpoint anchor'),
                drawingBlock('source-shape', 'e7-source-shape', 1, 120, 72, {
                    preset: 'rectangle',
                    fill: { color: '#dbeafe' },
                    stroke: { color: '#2563eb', width: 2 },
                }, null, null, { x: 24, y: 120 }),
                drawingBlock('target-shape', 'e7-target-shape', 1, 120, 72, {
                    preset: 'ellipse',
                    fill: { color: '#fef3c7' },
                    stroke: { color: '#d97706', width: 2 },
                }, null, null, { x: 264, y: 160 }),
                drawingBlock('connector', 'e7-connector', 4, 280, 80, {
                    preset: 'bentConnector',
                    fill: { type: 'none' },
                    stroke: { color: '#16a34a', width: 2, endArrow: 'triangle' },
                    routing: 'elbow',
                    startConnection: { objectId: 'e7-source-shape', site: 'right' },
                    endConnection: { objectId: 'e7-target-shape', site: 'left' },
                }, null, null, { x: 0, y: 0 }),
            ],
        },
    };
    let selection = { anchor: { blockId: 'connector', offset: 0 }, focus: { blockId: 'connector', offset: 0 }, object: { objectId: 'e7-connector', blockId: 'connector', runId: 'e7-connector-run' } };
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    const moved = runtime.execCommand('updateConnectorEndpoint', {
        objectId: 'e7-connector',
        endpoint: 'end',
        pageX: 528,
        pageY: 330,
        bodyX: 72,
        bodyY: 72,
        currentStartX: 216,
        currentStartY: 228,
        currentEndX: 336,
        currentEndY: 268,
    }).result;
    assert.equal(moved.changed, true);

    let connector = drawingById(model, 'e7-connector');
    assert.equal(connector.shape.endConnection, null);
    assert.equal(connector.shape.startConnection.objectId, 'e7-source-shape');
    assert.equal(connector.shape.points.length, 2);
    assert.equal(connector.layout.position.x, 144);
    assert.equal(connector.layout.position.y, 156);
    assert.equal(connector.layout.transform.width, 312);
    assert.equal(connector.layout.transform.height, 102);

    let display = buildDisplayList(model, layoutCanvasDocument(model, { fontMetrics: metrics() }), { fontMetrics: metrics() });
    let command = display.commands.find(item => item.type === 'drawingLine' && item.objectId === 'e7-connector');
    assert.equal(command.connector.end.x, 528);
    assert.equal(command.connector.end.y, 330);

    assert.equal(runtime.execCommand('undo').result.changed, true);
    connector = drawingById(model, 'e7-connector');
    assert.equal(connector.shape.endConnection.objectId, 'e7-target-shape');

    assert.equal(runtime.execCommand('redo').result.changed, true);
    display = buildDisplayList(model, layoutCanvasDocument(model, { fontMetrics: metrics() }), { fontMetrics: metrics() });
    command = display.commands.find(item => item.type === 'drawingLine' && item.objectId === 'e7-connector');
    assert.equal(command.connector.end.x, 528);
    assert.equal(command.connector.end.y, 330);
});

test('drawing layout commands persist rotation and flip through display list and undo history', () => {
    let model = createDrawingModel();
    let selection = { anchor: { blockId: 'anchor', offset: 0 }, focus: { blockId: 'anchor', offset: 0 } };
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
        history: createHistory(),
    });

    const updated = runtime.execCommand('updateImageLayout', {
        objectId: 'e7-rounded-shape',
        rotation: 37,
        flipHorizontal: true,
        flipVertical: true,
    }).result;
    assert.equal(updated.changed, true);

    let drawing = model.body.blocks
        .flatMap(block => block.content?.runs || [])
        .find(run => run.drawing?.objectId === 'e7-rounded-shape')
        .drawing;
    assert.equal(drawing.layout.transform.rotation, 37);
    assert.equal(drawing.layout.transform.flip.horizontal, true);
    assert.equal(drawing.layout.transform.flip.vertical, true);

    let display = buildDisplayList(model, layoutCanvasDocument(model, { fontMetrics: metrics() }), { fontMetrics: metrics() });
    let command = display.commands.find(item => item.type === 'drawingShape' && item.objectId === 'e7-rounded-shape');
    assert.equal(command.rotation, 37);
    assert.equal(command.flipHorizontal, true);
    assert.equal(command.flipVertical, true);

    assert.equal(runtime.execCommand('undo').result.changed, true);
    drawing = model.body.blocks
        .flatMap(block => block.content?.runs || [])
        .find(run => run.drawing?.objectId === 'e7-rounded-shape')
        .drawing;
    assert.equal(Number(drawing.layout.transform.rotation ?? 0) || 0, 0);

    assert.equal(runtime.execCommand('redo').result.changed, true);
    display = buildDisplayList(model, layoutCanvasDocument(model, { fontMetrics: metrics() }), { fontMetrics: metrics() });
    command = display.commands.find(item => item.type === 'drawingShape' && item.objectId === 'e7-rounded-shape');
    assert.equal(command.rotation, 37);
});

test('drawing painter handles vector and chart commands without canvas errors', () => {
    const calls = [];
    const context = fakeContext(calls);

    assert.equal(paintCommand(context, {
        type: 'drawingShape',
        x: 10,
        y: 12,
        width: 80,
        height: 44,
        rotation: 12,
        flipHorizontal: true,
        shape: {
            preset: 'diamond',
            fill: { type: 'linearGradient', color: '#dbeafe', secondaryColor: '#bfdbfe', opacity: 0.86, angle: 45 },
            stroke: { color: '#2563eb', width: 2 },
            shadow: { color: 'rgba(15, 23, 42, 0.16)', blur: 4, offsetY: 2 },
        },
    }), true);
    assert.equal(paintCommand(context, {
        type: 'drawingLine',
        x: 10,
        y: 70,
        width: 120,
        height: 24,
        shape: { stroke: { color: '#16a34a', width: 3, endArrow: 'triangle' } },
    }), true);
    assert.equal(paintCommand(context, {
        type: 'drawingChart',
        x: 10,
        y: 110,
        width: 180,
        height: 120,
        chart: { title: 'Trend', categories: ['A', 'B'], series: [{ values: [2, 5], color: '#2563eb' }] },
    }), true);

    assert.equal(calls.includes('fill'), true);
    assert.equal(calls.includes('stroke'), true);
    assert.equal(calls.includes('fillRect'), true);
    assert.equal(calls.includes('createLinearGradient'), true);
    assert.ok(calls.filter(call => call === 'colorStop').length >= 2);
    assert.equal(calls.includes('rotate'), true);
    assert.equal(calls.includes('scale'), true);
});

function createDrawingModel() {
    return {
        documentId: 'e7-drawing-render-test',
        pageSettings: { width: 720, height: 960, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
        body: {
            blocks: [
                paragraph('anchor', 'Drawing objects use the object layout path.'),
                drawingBlock('shape-block', 'e7-rounded-shape', 1, 176, 92, {
                    preset: 'roundRectangle',
                    fill: { color: '#dbeafe', opacity: 1 },
                    stroke: { color: '#2563eb', width: 2 },
                    shadow: { color: 'rgba(15, 23, 42, 0.18)', blur: 6, offsetY: 3 },
                }),
                drawingBlock('textbox-block', 'e7-textbox', 2, 220, 86, {
                    preset: 'rectangle',
                    fill: { color: '#fef3c7', opacity: 1 },
                    stroke: { color: '#d97706', width: 1.5 },
                }, {
                    paragraphs: [{ text: 'Canvas text box', alignment: 'center', style: { fontSize: 16, bold: true } }],
                }),
                drawingBlock('line-block', 'e7-arrow-line', 3, 210, 28, {
                    preset: 'line',
                    fill: { type: 'none', color: '#ffffff' },
                    stroke: { color: '#16a34a', width: 3, endArrow: 'triangle' },
                }),
                drawingBlock('chart-block', 'e7-bar-chart', 5, 300, 180, null, null, {
                    type: 'bar',
                    title: 'Quarterly trend',
                    categories: ['Q1', 'Q2', 'Q3'],
                    series: [{ name: 'Actual', values: [4, 7, 5], color: '#2563eb' }],
                }),
            ],
        },
    };
}

function drawingBlock(blockId, objectId, kind, width, height, shape, textBody = null, chart = null, position = null) {
    const x = Number(position?.x ?? position?.X ?? 0) || 0;
    const y = Number(position?.y ?? position?.Y ?? 0) || 0;
    return {
        id: blockId,
        type: 'paragraph',
        order: Number(blockId.match(/\d+/)?.[0] || 2),
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
                        anchor: { blockId: 'anchor', offset: 0 },
                        position: { x, y },
                        wrap: { mode: kind === 5 ? 4 : 6 },
                        transform: { width, height, lockAspectRatio: false },
                        stacking: { zIndex: kind },
                    },
                    shape,
                    textBody,
                    chart,
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
    assert.ok(drawing, `Expected drawing ${objectId} to exist.`);
    return drawing;
}

function paragraph(id, text) {
    return {
        id,
        type: 'paragraph',
        order: 1,
        content: { type: 'paragraph', runs: [{ id: `${id}-text`, type: 'text', text }] },
    };
}

function metrics() {
    return {
        measureText(text, style = {}) {
            const size = Number(style.fontSize || 16) || 16;
            return { width: String(text || '').length * size * 0.52, height: size * 1.2, actualBoundingBoxAscent: size * 0.8 };
        },
        stats() {
            return { hits: 0, misses: 0, size: 0 };
        },
    };
}

function createHistory() {
    const undoStack = [];
    const redoStack = [];
    return {
        push(entry) {
            undoStack.push(entry);
            redoStack.length = 0;
        },
        undo() {
            const entry = undoStack.pop();
            if (entry) redoStack.push(entry);
            return entry || null;
        },
        redo() {
            const entry = redoStack.pop();
            if (entry) undoStack.push(entry);
            return entry || null;
        },
        snapshot() {
            return { undo: undoStack.length, redo: redoStack.length };
        },
    };
}

function fakeContext(calls) {
    const context = {
        canvas: {},
        save: () => calls.push('save'),
        restore: () => calls.push('restore'),
        beginPath: () => calls.push('beginPath'),
        closePath: () => calls.push('closePath'),
        moveTo: () => calls.push('moveTo'),
        lineTo: () => calls.push('lineTo'),
        quadraticCurveTo: () => calls.push('quadraticCurveTo'),
        ellipse: () => calls.push('ellipse'),
        fill: () => calls.push('fill'),
        stroke: () => calls.push('stroke'),
        fillRect: () => calls.push('fillRect'),
        strokeRect: () => calls.push('strokeRect'),
        fillText: () => calls.push('fillText'),
        setLineDash: () => calls.push('setLineDash'),
        translate: () => calls.push('translate'),
        rotate: () => calls.push('rotate'),
        scale: () => calls.push('scale'),
        createLinearGradient: () => {
            calls.push('createLinearGradient');
            return { addColorStop: () => calls.push('colorStop') };
        },
        measureText: text => ({ width: String(text || '').length * 7 }),
    };
    return context;
}
