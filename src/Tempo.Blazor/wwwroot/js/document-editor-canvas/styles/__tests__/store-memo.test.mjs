import assert from 'node:assert/strict';
import test from 'node:test';
import { deleteStyle, ensureStyleStore, findStyle, renameStyle, upsertStyle } from '../style-store.mjs';

// ensureStyleStore sits on the per-keystroke layout path (resolveStyle -> findStyle per run), so it memoizes
// the normalized store per model+styles identity. These tests pin the memo AND its invalidation rules: the
// in-place mutators must keep findStyle results fresh, and replacing model.styles must rebuild the store.

test('ensureStyleStore returns the identical array on repeat calls (memo hit)', () => {
    const model = { styles: [{ id: 'custom', name: 'Custom', type: 'paragraph' }] };
    const first = ensureStyleStore(model);
    const second = ensureStyleStore(model);
    assert.equal(second, first);
    assert.equal(model.styles, first);
});

test('replacing model.styles invalidates the memo and re-normalizes', () => {
    const model = { styles: [] };
    const first = ensureStyleStore(model);
    assert.ok(first.some(style => style.id === 'normal'));

    model.styles = [{ id: 'fresh', name: 'Fresh', type: 'paragraph' }];
    const second = ensureStyleStore(model);
    assert.notEqual(second, first);
    assert.ok(second.some(style => style.id === 'fresh'));
    assert.ok(second.some(style => style.id === 'normal'), 'built-ins re-merged after replacement');
});

test('findStyle stays fresh across upsert, rename and delete (index invalidation)', () => {
    const model = { styles: [] };
    ensureStyleStore(model);

    assert.equal(findStyle(model, 'my-style'), null);

    // NOTE: the display name must not normalize to the same key as the id (normalizeStyleKey strips
    // spaces/dashes), otherwise the id key keeps resolving it regardless of renames.
    upsertStyle(model, { id: 'my-style', name: 'Pretty Label', type: 'paragraph' });
    assert.equal(findStyle(model, 'my-style')?.id, 'my-style');
    assert.equal(findStyle(model, 'Pretty Label')?.id, 'my-style');

    renameStyle(model, 'my-style', 'Better Name');
    assert.equal(findStyle(model, 'Better Name')?.id, 'my-style');
    assert.equal(findStyle(model, 'Pretty Label'), null, 'old name no longer resolves after rename');

    deleteStyle(model, 'my-style');
    assert.equal(findStyle(model, 'my-style'), null);
    assert.equal(findStyle(model, 'Better Name'), null);
});

test('findStyle honours the type filter and array order (first match wins)', () => {
    const model = { styles: [] };
    ensureStyleStore(model);
    upsertStyle(model, { id: 'dual-table', name: 'Dual', type: 'table' });

    assert.equal(findStyle(model, 'Dual', 'table')?.id, 'dual-table');
    assert.equal(findStyle(model, 'Dual', 'paragraph'), null);
    assert.equal(findStyle(model, 'normal')?.id, 'normal');
});

test('findStyle falls back to normal for an empty key', () => {
    const model = { styles: [] };
    assert.equal(findStyle(model, '')?.id, 'normal');
    assert.equal(findStyle(model, null)?.id, 'normal');
});
