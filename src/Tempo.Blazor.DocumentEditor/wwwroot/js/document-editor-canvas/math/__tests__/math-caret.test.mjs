import assert from 'node:assert/strict';
import test from 'node:test';
import {
    addMathMatrixColumn,
    addMathMatrixRow,
    collectMathSlots,
    deleteTextInMathSlot,
    insertTextInMathSlot,
    mathCaretRectForSlot,
    mathSlotAtPoint,
    mathSlotRectForSlot,
    moveMathSlot,
    replaceContentInMathSlot,
} from '../math-caret.mjs';
import { createMathContentFromLinear, mathToAccessibleText, normalizeMathRun } from '../math-model.mjs';
import { layoutMathRun } from '../math-layout.mjs';

test('math caret model exposes deterministic slots for fractions, scripts, radicals and matrices', () => {
    const math = richMath();
    const slots = collectMathSlots(math);
    const slotKeys = slots.map(slot => `${slot.slotName}:${slot.path.join('.')}`);

    assert.deepEqual(slotKeys.slice(0, 8), [
        'numerator:elements.0.numerator',
        'denominator:elements.0.denominator',
        'base:elements.1.base',
        'superscript:elements.1.superscript',
        'radicand:elements.2.radicand',
        'cell 1, 1:elements.3.rows.0.cells.0',
        'cell 1, 2:elements.3.rows.0.cells.1',
        'cell 2, 1:elements.3.rows.1.cells.0',
    ]);
    assert.equal(slots.at(-1).slotName, 'cell 2, 2');
});

test('math slot editing inserts and deletes text without replacing sibling structures', () => {
    const math = richMath();

    const numerator = insertTextInMathSlot(math, ['elements', 0, 'numerator'], '1', { offset: 1 });
    assert.equal(mathToAccessibleText(numerator.math), '(a1)/(b)x^2sqrt(y)[1,0;0,1]');
    assert.equal(mathToAccessibleText(math), '(a)/(b)x^2sqrt(y)[1,0;0,1]');

    const deleted = deleteTextInMathSlot(numerator.math, ['elements', 0, 'numerator'], { offset: 2, direction: 'backward' });
    assert.equal(mathToAccessibleText(deleted.math), '(a)/(b)x^2sqrt(y)[1,0;0,1]');
    assert.equal(deleted.offset, 1);

    const denominator = insertTextInMathSlot(deleted.math, ['elements', 0, 'denominator'], '+c', { offset: 1 });
    assert.equal(mathToAccessibleText(denominator.math), '(a)/(b+c)x^2sqrt(y)[1,0;0,1]');
    assert.equal(denominator.slot.slotName, 'denominator');
});

test('math boundary delete unwraps the parent structure while preserving the active slot content', () => {
    const math = richMath();
    const backward = deleteTextInMathSlot(math, ['elements', 0, 'numerator'], { offset: 0, direction: 'backward' });

    assert.equal(backward.changed, true);
    assert.equal(backward.math.content.elements[0].type, 'run');
    assert.equal(backward.math.content.elements[0].text, 'a');
    assert.equal(mathToAccessibleText(backward.math), 'ax^2sqrt(y)[1,0;0,1]');
    assert.deepEqual(backward.slot.path, []);

    const forward = deleteTextInMathSlot(richMath(), ['elements', 0, 'denominator'], { offset: 1, direction: 'forward' });
    assert.equal(forward.changed, true);
    assert.equal(forward.math.content.elements[0].type, 'run');
    assert.equal(forward.math.content.elements[0].text, 'b');
    assert.equal(mathToAccessibleText(forward.math), 'bx^2sqrt(y)[1,0;0,1]');
});

test('math slot navigation and caret rectangles are stable across layout recalculation', () => {
    const math = richMath();
    const slots = collectMathSlots(math);
    const next = moveMathSlot(math, slots[0].path, 'next');
    const previous = moveMathSlot(math, next.slot.path, 'previous');
    const layout = layoutMathRun(math, { style: { fontSize: 18 }, metrics: deterministicMetrics() });
    const rect = mathCaretRectForSlot(layout, next.slot.path, 1);

    assert.equal(next.slot.slotName, 'denominator');
    assert.deepEqual(previous.slot.path, slots[0].path);
    assert.equal(Number.isFinite(rect.x), true);
    assert.equal(Number.isFinite(rect.y), true);
    assert.ok(rect.height >= 8);
});

test('math slot hit-testing resolves a visual point to the expected structured slot', () => {
    const math = richMath();
    const layout = layoutMathRun(math, { style: { fontSize: 18 }, metrics: deterministicMetrics() });
    const rect = mathSlotRectForSlot(layout, ['elements', 0, 'denominator']);
    const hit = mathSlotAtPoint(layout, rect.x + rect.width / 2, rect.y + rect.height / 2);

    assert.equal(hit.slotName, 'denominator');
    assert.deepEqual(hit.path, ['elements', 0, 'denominator']);
    assert.equal(Number.isFinite(hit.offset), true);
});

test('nary display math maps base and limit slot rectangles to their own visual boxes', () => {
    const math = normalizeMathRun({
        id: 'math-nary-run',
        math: {
            mathId: 'math-nary',
            displayMode: 'display',
            content: {
                elements: [{
                    type: 'nary',
                    operator: '∑',
                    lowerLimit: createMathContentFromLinear('i=1'),
                    upperLimit: createMathContentFromLinear('n'),
                    base: createMathContentFromLinear('i'),
                    limitsAboveBelow: true,
                }],
            },
        },
    });
    const layout = layoutMathRun(math, { style: { fontSize: 18 }, metrics: deterministicMetrics() });
    const baseRect = mathSlotRectForSlot(layout, ['elements', 0, 'base']);
    const upperRect = mathSlotRectForSlot(layout, ['elements', 0, 'upperLimit']);
    const lowerRect = mathSlotRectForSlot(layout, ['elements', 0, 'lowerLimit']);
    const baseHit = mathSlotAtPoint(layout, baseRect.x + baseRect.width / 2, baseRect.y + baseRect.height / 2);
    const upperHit = mathSlotAtPoint(layout, upperRect.x + upperRect.width / 2, upperRect.y + upperRect.height / 2);
    const lowerHit = mathSlotAtPoint(layout, lowerRect.x + lowerRect.width / 2, lowerRect.y + lowerRect.height / 2);

    assert.equal(baseHit.slotName, 'expression');
    assert.deepEqual(baseHit.path, ['elements', 0, 'base']);
    assert.equal(upperHit.slotName, 'upper limit');
    assert.deepEqual(upperHit.path, ['elements', 0, 'upperLimit']);
    assert.equal(lowerHit.slotName, 'lower limit');
    assert.deepEqual(lowerHit.path, ['elements', 0, 'lowerLimit']);
    assert.ok(baseRect.x > upperRect.x, 'base expression is laid out to the right of the stacked limits');
});

test('nary inline hit-testing prefers the expression when limits overlap the base box', () => {
    const math = normalizeMathRun({
        id: 'math-inline-nary-run',
        math: {
            mathId: 'math-inline-nary',
            displayMode: 'inline',
            content: createMathContentFromLinear('\\sum'),
        },
    });
    const layout = layoutMathRun(math, { style: { fontSize: 18 }, metrics: deterministicMetrics() });
    const baseRect = mathSlotRectForSlot(layout, ['elements', 0, 'base']);
    const hit = mathSlotAtPoint(layout, baseRect.x + baseRect.width / 2, baseRect.y + baseRect.height / 2, { hitSlop: 4 });

    assert.equal(hit.slotName, 'expression');
    assert.deepEqual(hit.path, ['elements', 0, 'base']);
});

test('math slot replacement commits linear templates without flattening the containing equation', () => {
    const math = richMath();
    const replaced = replaceContentInMathSlot(math, ['elements', 0, 'numerator'], createMathContentFromLinear('p/q'));

    assert.equal(replaced.changed, true);
    assert.equal(replaced.math.content.elements[0].type, 'fraction');
    assert.equal(replaced.math.content.elements[0].numerator.elements[0].type, 'fraction');
    assert.equal(replaced.math.content.elements[0].denominator.elements[0].text, 'b');
});

test('matrix slot commands add rows and columns while preserving existing cell content', () => {
    const math = richMath();
    const withRow = addMathMatrixRow(math, ['elements', 3], { afterRowIndex: 0, values: ['r', 's'] });
    const withColumn = addMathMatrixColumn(withRow.math, ['elements', 3], { afterColumnIndex: 0, values: ['u', 'v', 'w'] });
    const matrix = withColumn.math.content.elements[3];

    assert.equal(matrix.rows.length, 3);
    assert.equal(matrix.rows[0].cells.length, 3);
    assert.equal(mathToAccessibleText(matrix.rows[0].cells[0]), '1');
    assert.equal(mathToAccessibleText(matrix.rows[0].cells[1]), 'u');
    assert.equal(mathToAccessibleText(matrix.rows[1].cells[0]), 'r');
    assert.equal(mathToAccessibleText(matrix.rows[1].cells[1]), 'v');
});

function richMath() {
    return normalizeMathRun({
        id: 'math-caret-run',
        math: {
            mathId: 'math-caret',
            content: {
                elements: [
                    {
                        type: 'fraction',
                        numerator: createMathContentFromLinear('a'),
                        denominator: createMathContentFromLinear('b'),
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
}

function deterministicMetrics() {
    return {
        measureText: (text, style) => ({ width: String(text || '').length * (Number(style?.fontSize) || 18) * 0.5 }),
    };
}
