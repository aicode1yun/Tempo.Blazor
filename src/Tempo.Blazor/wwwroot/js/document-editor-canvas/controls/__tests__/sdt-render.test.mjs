import assert from 'node:assert/strict';
import test from 'node:test';
import { buildContentControlRenderState, normalizeContentControlRenderMode } from '../sdt-render.mjs';

test('content control render state separates form fill and design chrome modes', () => {
    const control = {
        controlId: 'customer-name',
        alias: 'Customer',
        kind: 'plainText',
        placeholderText: 'Customer name',
        isRequired: true,
        value: { text: '' },
    };

    const form = buildContentControlRenderState(control, { mode: 'form' });
    const design = buildContentControlRenderState(control, { mode: 'design' });

    assert.equal(normalizeContentControlRenderMode('design-mode'), 'design');
    assert.equal(normalizeContentControlRenderMode('fill'), 'form');
    assert.equal(form.mode, 'form');
    assert.equal(form.text, 'Customer name');
    assert.equal(form.placeholder, true);
    assert.equal(form.showChrome, false);
    assert.equal(form.validation.valid, false);
    assert.equal(form.validation.reason, 'required');
    assert.equal(design.mode, 'design');
    assert.equal(design.showChrome, true);
    assert.equal(design.showTag, true);
    assert.equal(design.tagLabel, 'Customer');
    assert.equal(design.chrome.dash.length, 2);
});
