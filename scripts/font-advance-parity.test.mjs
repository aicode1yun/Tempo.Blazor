// Phase 1 (headless document runtime): JS↔C# text-measurement parity.
//
// The fixture (tests/Tempo.Blazor.DocumentFormats.Tests/TestData/font-advance-parity-fixture.json,
// generated and pinned by TempoFontAdvanceParityFixtureTests) carries the Skia-extracted advance
// table of the committed Dancing Script font plus sample texts (Czech diacritics, letter spacing)
// with expected widths computed by the same double-precision formula the JS measurer uses. This
// test replays the samples through the REAL JS measurer and asserts zero deviation — bit-identical
// widths, no synthetic fallback engaged.
import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import {
    parseFontAdvanceTable,
    createAdvanceFontMetricsService,
} from '../src/Tempo.Blazor.DocumentEditor/wwwroot/js/document-editor/layout/font-advance-metrics.mjs';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const fixturePath = path.join(
    repoRoot, 'tests', 'Tempo.Blazor.DocumentFormats.Tests', 'TestData', 'font-advance-parity-fixture.json');
const fixture = JSON.parse(readFileSync(fixturePath, 'utf8'));

test('parity fixture is present and carries table + samples', () => {
    assert.equal(fixture.schemaVersion, 1);
    assert.ok(Array.isArray(fixture.table?.faces) && fixture.table.faces.length > 0, 'fixture must embed the advance table');
    assert.ok(Array.isArray(fixture.samples) && fixture.samples.length > 0, 'fixture must carry samples');
});

test('JS measurer reproduces the C#-computed widths with zero deviation', () => {
    const service = createAdvanceFontMetricsService(fixture.table);

    for (const sample of fixture.samples) {
        const metrics = service.measureRun({
            text: sample.text,
            fontFamily: sample.fontFamily,
            fontSize: sample.fontSize,
            letterSpacing: sample.letterSpacing,
        });

        assert.equal(
            metrics.width,
            sample.expectedWidth,
            `width for "${sample.text}" must be bit-identical to the C# expectation`);
    }

    const diagnostics = service.getAdvanceDiagnostics();
    assert.deepEqual(diagnostics.unknownFamilies, [], 'no sample may fall back to synthetic metrics');
    assert.deepEqual(diagnostics.missingGlyphs, [], 'every sample glyph must resolve from the table');
});

test('advance sums over the parsed table match the C# unit sums exactly', () => {
    const table = parseFontAdvanceTable(fixture.table);
    const face = table.faces[0];

    for (const sample of fixture.samples) {
        let units = 0;
        for (const ch of sample.text) {
            const advance = face.advances.get(ch.codePointAt(0));
            assert.ok(advance !== undefined, `'${ch}' must exist in the table`);
            units += advance;
        }

        assert.equal(units, sample.expectedUnits, `unit sum for "${sample.text}" must be exact`);
    }
});

test('vertical metrics scale from the fixture face (ascent/descent/lineHeight)', () => {
    const service = createAdvanceFontMetricsService(fixture.table);
    const face = fixture.table.faces[0];
    const fontSize = 16;

    const metrics = service.measureRun({ text: 'Ag', fontFamily: face.family, fontSize });

    assert.equal(metrics.ascent, face.ascent * fontSize / face.unitsPerEm);
    assert.equal(metrics.descent, face.descent * fontSize / face.unitsPerEm);
    assert.equal(metrics.lineHeight, Math.max(1, Math.ceil((metrics.ascent + metrics.descent) * 1.15)));
});
