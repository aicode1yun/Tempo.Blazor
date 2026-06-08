import assert from 'node:assert/strict';
import test from 'node:test';
import { createFontMetricsService, fontStringFromStyle, syntheticRunMetrics } from '../../../document-editor/layout/font-metrics.mjs';
import { createCanvasRunDisplayText, createCanvasRunStyle } from '../canvas-text-style.mjs';

test('script marks produce numeric baseline shifts and scaled font sizes', () => {
    const model = baseModel();
    const block = paragraphBlock();
    const normal = createCanvasRunStyle(model, block, textRun('normal', 'x'));
    const superscript = createCanvasRunStyle(model, block, textRun('super', '2', [{ type: 'superscript' }]));
    const subscript = createCanvasRunStyle(model, block, textRun('sub', '2', [{ type: 'subscript' }]));

    assert.ok(superscript.fontSize < normal.fontSize);
    assert.ok(superscript.baselineShift < 0);
    assert.ok(subscript.fontSize < normal.fontSize);
    assert.ok(subscript.baselineShift > 0);
});

test('small caps and all caps keep model text while exposing uppercase display text', () => {
    const smallCaps = textRun('small', 'Canvas caps', [{ type: 'smallCaps' }]);
    const allCaps = textRun('all', 'Canvas caps ß', [{ type: 'allCaps' }]);
    const allCapsStyle = createCanvasRunStyle(baseModel(), paragraphBlock(), allCaps);

    assert.equal(smallCaps.text, 'Canvas caps');
    assert.equal(createCanvasRunDisplayText(smallCaps), 'CANVAS CAPS');
    assert.equal(createCanvasRunDisplayText(allCaps), 'CANVAS CAPS ß');
    assert.equal(allCapsStyle.fontVariantCaps, 'normal');
    assert.equal(allCapsStyle.textTransform, 'uppercase');
    assert.equal(fontStringFromStyle(allCapsStyle).includes('all-small-caps'), false);
});

test('character spacing and scale affect deterministic text metrics', () => {
    const service = createFontMetricsService({ createMeasureContext: () => null });
    const normal = service.measureText('Scale', { fontSize: 16 });
    const expanded = service.measureText('Scale', { fontSize: 16, letterSpacing: 2 });
    const scaled = service.measureText('Scale', { fontSize: 16, characterScale: 1.25 });

    assert.ok(expanded.width > normal.width);
    assert.ok(scaled.width > normal.width);

    const synthetic = syntheticRunMetrics({
        text: 'Scale',
        fontFamily: 'Arial',
        fontSize: 16,
        fontWeight: '400',
        fontStyle: 'normal',
        fontVariantCaps: 'normal',
        kerning: true,
        letterSpacing: 1,
        characterScale: 0.8,
        zoom: 1,
    });
    assert.ok(synthetic.width > 0);
    assert.ok(synthetic.width < expanded.width);
});

function baseModel() {
    return {
        theme: {
            bodyFontFamily: 'Aptos, Arial, sans-serif',
            bodyFontSize: 12,
        },
    };
}

function paragraphBlock() {
    return {
        id: 'char-block',
        type: 'paragraph',
        content: { type: 'paragraph', runs: [] },
        paragraphProperties: {},
    };
}

function textRun(id, text, marks = []) {
    return { id, type: 'text', text, marks };
}
