// Phase 3 (headless document runtime): cross-runtime layout parity.
//
// The committed request fixture (tests/.../TestData/headless-parity-request.json) is the EXACT
// payload the C# Jint host sends to the bundle's generateHeadlessLayoutSnapshotJson seam, and the
// committed snapshot fixture (headless-parity-snapshot-fixture.json) is what Jint produced from
// it. Replaying the same payload through the same bundle in Node (V8) must yield a deeply equal
// snapshot — proving the layout does not depend on the hosting JS engine (number semantics,
// object iteration order, JSON serialization).
import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';
import { BUNDLE_OUTFILE } from './build-document-editor.mjs';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const testData = path.join(repoRoot, 'tests', 'Tempo.Blazor.DocumentFormats.Tests', 'TestData');
const requestJson = readFileSync(path.join(testData, 'headless-parity-request.json'), 'utf8');
const committedSnapshot = JSON.parse(readFileSync(path.join(testData, 'headless-parity-snapshot-fixture.json'), 'utf8'));
const bundle = await import(pathToFileURL(path.join(repoRoot, BUNDLE_OUTFILE)).href);

test('Node (V8) reproduces the Jint-generated 21-page parity snapshot deeply equal', () => {
    const envelope = JSON.parse(bundle.generateHeadlessLayoutSnapshotJson(requestJson));

    assert.equal(envelope.pageCount, committedSnapshot.pageCount, 'page count must match across JS runtimes');
    assert.deepEqual(envelope.diagnostics.unknownFamilies, [], 'the parity document must measure fully from the tables');
    assert.deepEqual(envelope.diagnostics.missingGlyphs, []);
    assert.deepEqual(
        envelope.snapshot,
        committedSnapshot,
        'the layout snapshot must be identical whether the bundle runs in Node or in Jint');
});

test('parity snapshot carries the expected 21-page A4 geometry', () => {
    assert.equal(committedSnapshot.pageCount, 21, 'the parity pair is pinned to the browser fixture page count');
    for (const page of committedSnapshot.pages) {
        assert.ok(Math.abs(page.width - 793.701333) < 0.001);
        assert.ok(Math.abs(page.height - 1122.519333) < 0.001);
    }
});
