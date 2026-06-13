import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../display-list.mjs';
import { paintDisplayList } from '../canvas-renderer.mjs';
import { paintSigningField } from '../signing-field-render.mjs';

// A signing field renders as a role-coloured box with an icon + label (plan S2.7). The role colour
// is resolved by the display list from the engine's signing roles (with a fallback palette); the
// renderer paints the box, border, label and a required marker, plus a focus ring when selected.

function inlineBodyModel() {
    return {
        documentId: 'signing-render',
        body: {
            blocks: [
                {
                    id: 'p1',
                    type: 'paragraph',
                    content: {
                        type: 'paragraph',
                        runs: [
                            { id: 'r1', type: 'text', text: 'Sign: ', marks: [] },
                            { id: 'r2', type: 'signingField', text: '', marks: [], signingField: { uuid: 'f1', fieldType: 'signature', submitterUuid: 'signer', required: true, label: 'Signature' } },
                        ],
                    },
                },
            ],
        },
    };
}

test('the display list emits a signing field command with the role colour resolved', () => {
    const display = buildDisplayList(inlineBodyModel(), {}, {
        signingRoles: [{ uuid: 'signer', color: '#2563eb', name: 'Signer' }],
    });

    const command = display.commands.find(item => item.type === 'signingField');
    assert.ok(command, 'a signing field command is emitted for the body field');
    assert.equal(command.layer, 'content');
    assert.equal(command.fieldUuid, 'f1');
    assert.equal(command.fieldType, 'signature');
    assert.equal(command.required, true);
    assert.equal(command.label, 'Signature');
    assert.equal(command.roleColor, '#2563eb', 'the role colour is resolved from the signing roles');
    assert.ok(command.width > 0 && command.height > 0);
});

test('an unknown role falls back to a deterministic palette colour', () => {
    const display = buildDisplayList(inlineBodyModel(), {}, { signingRoles: [] });
    const command = display.commands.find(item => item.type === 'signingField');

    assert.ok(/^#[0-9a-f]{6}$/i.test(command.roleColor), 'a fallback palette colour is assigned');
});

test('paintSigningField draws the box, border, label and required marker', () => {
    const context = new RecordingContext();
    paintSigningField(context, {
        type: 'signingField', x: 40, y: 50, width: 180, height: 44,
        fieldType: 'signature', label: 'Signature', required: true, roleColor: '#2563eb',
    });

    assert.ok(context.calls.some(call => call.name === 'fillRect'), 'the field box is filled');
    assert.ok(context.calls.some(call => call.name === 'strokeRect'), 'the field box has a border');
    assert.ok(context.calls.some(call => call.name === 'fillText' && /Signature/.test(String(call.args[0]))), 'the label is drawn');
    assert.ok(context.calls.some(call => call.name === 'fillText' && /\*/.test(String(call.args[0]))), 'a required marker is drawn');
});

test('paintSigningField draws a focus ring when the field is selected', () => {
    const plain = new RecordingContext();
    paintSigningField(plain, { type: 'signingField', x: 0, y: 0, width: 120, height: 32, roleColor: '#2563eb' });
    const selected = new RecordingContext();
    paintSigningField(selected, { type: 'signingField', x: 0, y: 0, width: 120, height: 32, roleColor: '#2563eb', selected: true });

    const strokes = ctx => ctx.calls.filter(call => call.name === 'strokeRect').length;
    assert.ok(strokes(selected) > strokes(plain), 'a selected field draws an extra focus ring');
});

test('the signing field command paints through paintDisplayList on the content layer', () => {
    const canvas = makeCanvas();
    const summary = paintDisplayList(new Map([['content', canvas]]), {
        commands: [{ type: 'signingField', layer: 'content', x: 10, y: 10, width: 120, height: 30, fieldType: 'date', label: 'Date', roleColor: '#16a34a' }],
    });

    assert.ok(summary.paintedCommandCount >= 1, 'the signing field command is counted as painted');
    assert.ok(canvas.context.calls.some(call => call.name === 'fillRect'));
});

function makeCanvas() {
    const context = new RecordingContext();
    return { context, getContext: () => context };
}

class RecordingContext {
    constructor() {
        this.calls = [];
        this.fillStyle = '#000';
        this.strokeStyle = '#000';
        this.lineWidth = 1;
        this.font = '';
        this.textBaseline = 'alphabetic';
        this.textAlign = 'left';
        this.globalAlpha = 1;
    }

    save() { this.calls.push({ name: 'save', args: [] }); }
    restore() { this.calls.push({ name: 'restore', args: [] }); }
    fillRect(...args) { this.calls.push({ name: 'fillRect', args }); }
    strokeRect(...args) { this.calls.push({ name: 'strokeRect', args }); }
    fillText(...args) { this.calls.push({ name: 'fillText', args }); }
    measureText(text) { return { width: String(text || '').length * 7 }; }
    beginPath() {} moveTo() {} lineTo() {} rect() {} arc() {} stroke() {} fill() {} clip() {}
    setLineDash() {} translate() {} rotate() {} scale() {} closePath() {} quadraticCurveTo() {} drawImage() {}
}
