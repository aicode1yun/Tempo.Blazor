import assert from 'node:assert/strict';
import test from 'node:test';
import { createMathContentFromLinear, mathToAccessibleText, normalizeMathRun } from '../math-model.mjs';
import { layoutMathRun } from '../math-layout.mjs';

test('math model normalizes linear input into accessible structured content', () => {
    const fraction = normalizeMathRun({
        id: 'math-1',
        math: {
            displayMode: 1,
            content: createMathContentFromLinear('a/b'),
        },
    });
    const superscript = normalizeMathRun({
        id: 'math-2',
        math: {
            content: createMathContentFromLinear('x^2'),
        },
    });

    assert.equal(fraction.displayMode, 'display');
    assert.equal(fraction.content.elements[0].type, 'fraction');
    assert.equal(mathToAccessibleText(fraction), '(a)/(b)');
    assert.equal(superscript.content.elements[0].type, 'sup');
    assert.equal(mathToAccessibleText(superscript), 'x^2');

    const subscript = normalizeMathRun({
        id: 'math-3',
        math: {
            content: createMathContentFromLinear('x_i'),
        },
    });
    const symbol = normalizeMathRun({
        id: 'math-4',
        math: {
            content: createMathContentFromLinear('\\alpha + \\infty'),
        },
    });

    assert.equal(subscript.content.elements[0].type, 'sub');
    assert.equal(mathToAccessibleText(subscript), 'x_i');
    assert.equal(mathToAccessibleText(symbol), 'α + ∞');
});

test('math layout handles fraction, radical, n-ary, superscript and matrix boxes deterministically', () => {
    const math = normalizeMathRun({
        id: 'math-rich',
        math: {
            displayMode: 'display',
            content: {
                elements: [
                    {
                        type: 'fraction',
                        numerator: createMathContentFromLinear('a+b'),
                        denominator: createMathContentFromLinear('c'),
                    },
                    {
                        type: 'sup',
                        base: createMathContentFromLinear('x'),
                        superscript: createMathContentFromLinear('2'),
                    },
                    {
                        type: 'radical',
                        radicand: createMathContentFromLinear('y'),
                    },
                    {
                        type: 'nary',
                        operator: '∑',
                        lowerLimit: createMathContentFromLinear('i=1'),
                        upperLimit: createMathContentFromLinear('n'),
                        base: createMathContentFromLinear('i'),
                    },
                    {
                        type: 'matrix',
                        rows: [
                            { cells: [createMathContentFromLinear('1'), createMathContentFromLinear('0')] },
                            { cells: [createMathContentFromLinear('0'), createMathContentFromLinear('1')] },
                        ],
                    },
                ],
            },
        },
    });

    const first = layoutMathRun(math, { style: { fontSize: 18 }, metrics: deterministicMetrics() });
    const second = layoutMathRun(math, { style: { fontSize: 18 }, metrics: deterministicMetrics() });

    assert.deepEqual(second, first);
    assert.equal(first.type, 'content');
    assert.ok(first.width > 120);
    assert.ok(first.height > 40);
    assert.equal(first.children[0].type, 'fraction');
    assert.equal(first.children[1].type, 'sup');
    assert.equal(first.children[2].type, 'radical');
    assert.equal(first.children[3].type, 'nary');
    assert.equal(first.children[4].type, 'matrix');
    assert.ok(first.children[3].children.length >= 3);
});

test('math layout handles pre-scripts, accent, group char, limit, function and border box elements', () => {
    const math = normalizeMathRun({
        id: 'math-advanced',
        math: {
            displayMode: 'display',
            content: {
                elements: [
                    {
                        type: 'preSubSup',
                        subscript: createMathContentFromLinear('i'),
                        superscript: createMathContentFromLinear('j'),
                        base: createMathContentFromLinear('T'),
                    },
                    {
                        type: 'accent',
                        accent: '̂',
                        base: createMathContentFromLinear('x'),
                    },
                    {
                        type: 'groupChar',
                        position: 'under',
                        base: createMathContentFromLinear('a+b'),
                    },
                    {
                        type: 'limit',
                        base: { elements: [{ type: 'run', text: 'lim', style: 'normal' }] },
                        lowerLimit: createMathContentFromLinear('x→0'),
                        content: createMathContentFromLinear('f(x)'),
                    },
                    {
                        type: 'function',
                        functionName: createMathContentFromLinear('sin'),
                        base: createMathContentFromLinear('θ'),
                    },
                    {
                        type: 'borderBox',
                        content: createMathContentFromLinear('x+y'),
                    },
                ],
            },
        },
    });

    const layout = layoutMathRun(math, { style: { fontSize: 20 }, metrics: deterministicMetrics() });

    assert.equal(mathToAccessibleText(math), '_i^jTx ̂undergroup(a+b)lim_x→0^ f(x)sin(θ)x+y');
    assert.deepEqual(layout.children.map(child => child.type), ['preSubSup', 'accent', 'groupChar', 'limit', 'function', 'borderBox']);
    assert.ok(layout.width > 150);
    assert.ok(layout.children[0].children.at(-1).x > 0, 'pre-script base is positioned after the script stack');
    assert.ok(layout.children[1].ascent > layout.children[1].children[1].ascent, 'accent contributes to ascent');
    assert.ok(layout.children[2].descent > layout.children[2].children[0].descent, 'under group char contributes to descent');
    assert.ok(layout.children[3].children.length >= 3, 'limit stacks lower limit and expression');
    assert.ok(layout.children[5].width > layout.children[5].children[0].width, 'border box adds padding');
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
