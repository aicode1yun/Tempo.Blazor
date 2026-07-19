import assert from 'node:assert/strict';
import test from 'node:test';
import {
    parseFontAdvanceTable,
    createFontAdvanceMeasureContext,
    createAdvanceFontMetricsService,
} from '../font-advance-metrics.mjs';
import { syntheticRunMetrics, normalizeFontMetricStyle } from '../font-metrics.mjs';
import { layoutCanvasDocument } from '../../../document-editor-canvas/layout/pagination.mjs';

// Handcrafted advance table (font units, unitsPerEm 1000):
// 'A'=600, 'B'=650, ' '=250, 'č'=550, 'ř'=560 — bold face is wider, italic face narrower,
// so face resolution shows up in measured widths.
function createTable() {
    return {
        schemaVersion: 1,
        faces: [
            {
                family: 'Test Sans',
                weight: 400,
                style: 'normal',
                unitsPerEm: 1000,
                ascent: 800,
                descent: 200,
                lineGap: 0,
                missingGlyphAdvance: 500,
                advances: { 65: 600, 66: 650, 32: 250, 269: 550, 345: 560 },
            },
            {
                family: 'Test Sans',
                weight: 700,
                style: 'normal',
                unitsPerEm: 1000,
                ascent: 820,
                descent: 210,
                lineGap: 0,
                missingGlyphAdvance: 500,
                advances: { 65: 640, 66: 690, 32: 250, 269: 590, 345: 600 },
            },
            {
                family: 'Test Sans',
                weight: 400,
                style: 'italic',
                unitsPerEm: 1000,
                ascent: 800,
                descent: 200,
                lineGap: 0,
                missingGlyphAdvance: 500,
                advances: { 65: 580, 66: 630, 32: 250, 269: 530, 345: 540 },
            },
            // Family without a 400 face — exercises the nearest-weight fallback step.
            {
                family: 'Step Sans',
                weight: 300,
                style: 'normal',
                unitsPerEm: 1000,
                ascent: 800,
                descent: 200,
                lineGap: 0,
                missingGlyphAdvance: 500,
                advances: { 65: 500 },
            },
            {
                family: 'Step Sans',
                weight: 700,
                style: 'normal',
                unitsPerEm: 1000,
                ascent: 800,
                descent: 200,
                lineGap: 0,
                missingGlyphAdvance: 500,
                advances: { 65: 640 },
            },
        ],
    };
}

test('parseFontAdvanceTable accepts JSON text and objects and indexes faces', () => {
    const fromObject = parseFontAdvanceTable(createTable());
    const fromJson = parseFontAdvanceTable(JSON.stringify(createTable()));

    for (const table of [fromObject, fromJson]) {
        assert.equal(table.faces.length, 5);
        assert.equal(table.faces[0].family, 'Test Sans');
        assert.equal(table.faces[0].advances.get(65), 600);
        assert.equal(table.byFamily.get('test sans').length, 3, 'faces are indexed per lowercased family');
    }
});

test('measureRun sums advances scaled to the font size', () => {
    const service = createAdvanceFontMetricsService(createTable());
    const metrics = service.measureRun({ text: 'AB', fontFamily: 'Test Sans', fontSize: 20 });

    // (600 + 650) * 20 / 1000 = 25
    assert.equal(metrics.width, 25);
    assert.equal(service.isUsingRealMetrics(), true, 'advance tables count as real metrics');
});

test('measureRun measures Czech diacritics from the table', () => {
    const service = createAdvanceFontMetricsService(createTable());
    const metrics = service.measureRun({ text: 'čř č', fontFamily: 'Test Sans', fontSize: 10 });

    // (550 + 560 + 250 + 550) * 10 / 1000 = 19.1
    assert.equal(metrics.width, 19.1);
});

test('measureRun applies letter spacing between glyphs (n−1 gaps)', () => {
    const service = createAdvanceFontMetricsService(createTable());
    const metrics = service.measureRun({ text: 'AB', fontFamily: 'Test Sans', fontSize: 20, letterSpacing: 2 });

    assert.equal(metrics.width, 27, '25 + 1 gap × 2px');
});

test('bold and italic requests resolve their dedicated faces', () => {
    const service = createAdvanceFontMetricsService(createTable());

    const bold = service.measureRun({ text: 'A', fontFamily: 'Test Sans', fontSize: 10, bold: true });
    assert.equal(bold.width, 6.4, 'bold face advance 640 must be used');

    const italic = service.measureRun({ text: 'A', fontFamily: 'Test Sans', fontSize: 10, italic: true });
    assert.equal(italic.width, 5.8, 'italic face advance 580 must be used');
});

// Face resolution mirrors the PDF renderer's ReportPdfFontCatalog exactly (parity with drawing):
// (1) exact family+weight+style, (2) family + weight 400 + style, (3) any style of the family by
// nearest weight, (4) nothing → synthetic fallback with diagnostics.
test('face resolution mirrors the PDF font catalog fallback chain', () => {
    const service = createAdvanceFontMetricsService(createTable());

    const w600 = service.measureRun({ text: 'A', fontFamily: 'Test Sans', fontSize: 10, fontWeight: '600' });
    assert.equal(w600.width, 6, 'weight 600 has no exact face → the regular (400) face of the same style wins');

    const boldItalic = service.measureRun({ text: 'A', fontFamily: 'Test Sans', fontSize: 10, bold: true, italic: true });
    assert.equal(boldItalic.width, 5.8, 'no 700 italic face → the 400 italic face wins (style before weight)');

    const step600 = service.measureRun({ text: 'A', fontFamily: 'Step Sans', fontSize: 10, fontWeight: '600' });
    assert.equal(step600.width, 6.4, 'no exact and no 400 face → nearest weight (700 over 300) wins');
});

test('the first known family in a CSS family list is used', () => {
    const service = createAdvanceFontMetricsService(createTable());
    const metrics = service.measureRun({ text: 'A', fontFamily: '"Missing Font", Test Sans, sans-serif', fontSize: 10 });

    assert.equal(metrics.width, 6);
});

test('unknown family falls back to synthetic metrics and records a diagnostic', () => {
    const service = createAdvanceFontMetricsService(createTable());
    const style = { text: 'AB', fontFamily: 'Nope Sans, serif', fontSize: 20 };

    const metrics = service.measureRun(style);
    const synthetic = syntheticRunMetrics(normalizeFontMetricStyle(style));

    assert.equal(metrics.width, synthetic.width, 'unknown family must measure synthetically');
    const diagnostics = service.getAdvanceDiagnostics();
    assert.deepEqual(diagnostics.unknownFamilies, ['Nope Sans'], 'the cleaned concrete family name is recorded (generics dropped)');
});

test('unknown glyph falls back to synthetic metrics and records the code point', () => {
    const service = createAdvanceFontMetricsService(createTable());
    const style = { text: 'A漢B', fontFamily: 'Test Sans', fontSize: 20 };

    const metrics = service.measureRun(style);
    const synthetic = syntheticRunMetrics(normalizeFontMetricStyle(style));

    assert.equal(metrics.width, synthetic.width, 'a run with an unmapped glyph must measure synthetically');
    const diagnostics = service.getAdvanceDiagnostics();
    assert.equal(diagnostics.missingGlyphs.length, 1);
    assert.deepEqual(diagnostics.missingGlyphs[0], { family: 'Test Sans', codePoint: 0x6f22 });
});

test('vertical metrics scale from font units and drive the line height', () => {
    const service = createAdvanceFontMetricsService(createTable());
    const metrics = service.measureRun({ text: 'A', fontFamily: 'Test Sans', fontSize: 20 });

    assert.equal(metrics.ascent, 16, '800 × 20 / 1000');
    assert.equal(metrics.descent, 4, '200 × 20 / 1000');
    assert.equal(metrics.lineHeight, Math.ceil((16 + 4) * 1.15));
});

test('zoom scales widths and vertical metrics like the browser measurer', () => {
    const service = createAdvanceFontMetricsService(createTable());
    const metrics = service.measureRun({ text: 'A', fontFamily: 'Test Sans', fontSize: 10, zoom: 2 });

    assert.equal(metrics.width, 12, '6px × zoom 2');
    assert.equal(metrics.ascent, 16, 'ascent scales with zoom');
});

test('empty text measures zero width without touching the fallback', () => {
    const service = createAdvanceFontMetricsService(createTable());
    const metrics = service.measureRun({ text: '', fontFamily: 'Test Sans', fontSize: 20 });

    assert.equal(metrics.width, 0);
    assert.deepEqual(service.getAdvanceDiagnostics().unknownFamilies, []);
});

test('the measure context serves the pagination seam as a { measureRun } partial', () => {
    const service = createAdvanceFontMetricsService(createTable());
    const partial = { measureRun: request => service.measureRun(request) };

    const layout = layoutCanvasDocument(
        {
            documentId: 'advance-partial-seam',
            theme: { bodyFontFamily: 'Test Sans', bodyFontSize: 10, bodyLineHeight: 1.2 },
            body: {
                blocks: [{
                    id: 'p1',
                    type: 'paragraph',
                    order: 1,
                    content: { type: 'paragraph', runs: [{ id: 'r1', type: 'text', text: 'AB AB', marks: [] }] },
                }],
            },
        },
        { fontMetrics: partial });

    assert.ok(layout.pages.length >= 1, 'pagination must lay out with the advance-table partial');
    assert.ok(layout.blocks.length > 0, 'the layout must carry the laid-out block');
    const paragraph = layout.blocks.find(block => block.blockId === 'p1' || block.id === 'p1');
    assert.ok(paragraph, 'the paragraph block must be laid out through the advance measurer');
});

test('createFontAdvanceMeasureContext plugs into createFontMetricsService as a canvas context', () => {
    const context = createFontAdvanceMeasureContext(parseFontAdvanceTable(createTable()));

    context.font = '10px Test Sans';
    const measured = context.measureText('AB');
    assert.equal(measured.width, 12.5, '(600+650) × 10 / 1000');
    assert.equal(measured.fontBoundingBoxAscent, 8);
    assert.equal(measured.fontBoundingBoxDescent, 2);
});
