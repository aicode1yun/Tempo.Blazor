import assert from 'node:assert/strict';
import test from 'node:test';
import { mathToAccessibleText, normalizeMathRun } from '../math-model.mjs';
import { layoutMathRun } from '../math-layout.mjs';
import { mathContentToMathML, parseMathMLToContent } from '../mathml-adapter.mjs';

test('MathML adapter imports basic structures into the canonical math tree', () => {
    const content = parseMathMLToContent(`
        <math xmlns="http://www.w3.org/1998/Math/MathML" display="block">
            <mfrac><mi>a</mi><mi>b</mi></mfrac>
            <msup><mi>x</mi><mn>2</mn></msup>
            <msub><mi>y</mi><mi>i</mi></msub>
            <msqrt><mi>z</mi></msqrt>
            <mroot><mi>q</mi><mn>3</mn></mroot>
            <mrow><munderover><mo>∑</mo><mrow><mi>i</mi><mo>=</mo><mn>1</mn></mrow><mi>n</mi></munderover><mi>i</mi></mrow>
            <mtable>
                <mtr><mtd><mn>1</mn></mtd><mtd><mn>0</mn></mtd></mtr>
                <mtr><mtd><mn>0</mn></mtd><mtd><mn>1</mn></mtd></mtr>
            </mtable>
        </math>
    `);

    assert.deepEqual(content.elements.map(element => element.type), [
        'fraction',
        'sup',
        'sub',
        'radical',
        'radical',
        'nary',
        'run',
        'matrix',
    ]);
    assert.equal(content.elements[0].numerator.elements[0].text, 'a');
    assert.equal(content.elements[4].degree.elements[0].text, '3');
    assert.equal(content.elements[5].operator, '∑');
    assert.equal(content.elements[7].rows.length, 2);
});

test('MathML adapter exports canonical math content and can import it back', () => {
    const math = normalizeMathRun({
        id: 'mathml-roundtrip',
        math: {
            displayMode: 'display',
            content: {
                elements: [
                    {
                        type: 'fraction',
                        numerator: { elements: [{ type: 'run', text: 'a', style: 'italic' }] },
                        denominator: { elements: [{ type: 'run', text: 'b', style: 'italic' }] },
                    },
                    {
                        type: 'borderBox',
                        content: { elements: [{ type: 'run', text: 'x+y', style: 'italic' }] },
                    },
                ],
            },
        },
    });

    const exported = mathContentToMathML(math.content, { displayMode: math.displayMode });
    const imported = normalizeMathRun({
        id: 'imported',
        math: {
            displayMode: 'display',
            mathML: exported,
        },
    });

    assert.match(exported, /<math /);
    assert.match(exported, /<mfrac>/);
    assert.match(exported, /<menclose notation="box">/);
    assert.equal(imported.content.elements[0].type, 'fraction');
    assert.equal(imported.content.elements[1].type, 'borderBox');
    assert.equal(mathToAccessibleText(imported), '(a)/(b)x+y');
});

test('MathML payload normalizes through math run model and produces deterministic layout', () => {
    const math = normalizeMathRun({
        id: 'mathml-layout',
        math: {
            mathML: '<math><mfrac><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow><mi>c</mi></mfrac></math>',
        },
    });

    const first = layoutMathRun(math, { style: { fontSize: 18 }, metrics: deterministicMetrics() });
    const second = layoutMathRun(math, { style: { fontSize: 18 }, metrics: deterministicMetrics() });

    assert.equal(math.content.elements[0].type, 'fraction');
    assert.equal(mathToAccessibleText(math), '(a+b)/(c)');
    assert.deepEqual(second, first);
    assert.ok(first.width > 20);
    assert.ok(first.height > 20);
});

function deterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, Array.from(text).length * fontSize * 0.52),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: fontSize * 1.2,
            };
        },
    };
}
