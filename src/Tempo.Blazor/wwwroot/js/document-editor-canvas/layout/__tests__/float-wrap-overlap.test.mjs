import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../pagination.mjs';

// Phase 1 (perf+rendering fix 2026-06-08): reproduce and guard against the
// overlapping-paragraph defect seen on /document-editor with floating wrapped
// images. The invariant: text runs that belong to DIFFERENT paragraph blocks
// must never occupy the same rectangle (they may sit side by side around a
// float, but never on top of each other).

test('paragraphs after a tall floating square image do not overlap each other', () => {
    const layout = layoutCanvasDocument({
        documentId: 'float-wrap-overlap',
        pageSettings: { width: 600, height: 900, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 8 },
        body: {
            blocks: [
                floatingSquareImage('evidence', { width: 220, height: 320, x: 280, y: 0 }),
                paragraph('p1', shortText('First wrapped paragraph beside the floating evidence image.'), 2),
                paragraph('p2', shortText('Second wrapped paragraph continues beside the same image band.'), 3),
                paragraph('p3', shortText('Third wrapped paragraph keeps flowing without colliding.'), 4),
                paragraph('p4', shortText('Fourth paragraph should sit below the image once it is cleared.'), 5),
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    assertNoCrossBlockTextOverlap(layout);
});

test('a paragraph never paints on top of a floating square image exclusion zone', () => {
    const layout = layoutCanvasDocument({
        documentId: 'float-wrap-exclusion',
        pageSettings: { width: 600, height: 900, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 8 },
        body: {
            blocks: [
                floatingSquareImage('evidence', { width: 220, height: 200, x: 280, y: 0 }),
                paragraph('p1', longText(), 2),
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    const image = layout.objectLayouts.find(object => object.blockId === 'evidence');
    assert.ok(image, 'floating image must be laid out');
    const imageBox = image.rect;
    const imageBottom = imageBox.y + imageBox.height;

    for (const rect of textRectsOf(layout, 'p1')) {
        const overlapsHorizontally = rect.x < imageBox.x + imageBox.width && rect.x + rect.width > imageBox.x;
        const overlapsVertically = rect.y < imageBottom && rect.y + rect.height > imageBox.y;
        assert.ok(
            !(overlapsHorizontally && overlapsVertically),
            `text run at (${rect.x.toFixed(1)},${rect.y.toFixed(1)} ${rect.width.toFixed(1)}x${rect.height.toFixed(1)}) overlaps the floating image box (${imageBox.x},${imageBox.y} ${imageBox.width}x${imageBox.height})`);
    }
});

test('stacked floating images taller than their wrapping paragraphs do not pile up (contract-demo pattern)', () => {
    // Mirrors the /document-editor contract-demo: a sequence of
    // [floating image, short paragraph anchored to it] where each image is
    // taller than the paragraph that wraps around it. In Word/OnlyOffice the
    // flow keeps advancing so neither the images nor the paragraphs collide.
    const blocks = [];
    let order = 0;
    for (let i = 0; i < 4; i += 1) {
        const onRight = i % 2 === 0;
        blocks.push(floatingSquareImage(`img-${i}`, {
            width: 180,
            height: 150,
            x: onRight ? 320 : 48,
            // Explicit y:0 (as the C# demo emits) must still flow at the paragraph, not pin to
            // page top. `explicitY ?? cursorY` used to collapse to 0 here ("0 ?? x === 0").
            y: 0,
            order: ++order,
        }));
        blocks.push(paragraph(`para-${i}`, shortText(`Scenario ${i}: a short paragraph that wraps beside the floating image.`), ++order));
    }

    const layout = layoutCanvasDocument({
        documentId: 'stacked-floats',
        pageSettings: { width: 600, height: 1400, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 16 },
        body: { blocks },
    }, { fontMetrics: createDeterministicMetrics() });

    assertNoCrossBlockTextOverlap(layout);
    assertNoFloatingImageOverlap(layout);
});

function assertNoFloatingImageOverlap(layout) {
    const images = (layout.objectLayouts || [])
        .filter(object => (object.object?.isFloating ?? object.isFloating) && shouldReserveSpace(object))
        .map(object => ({ id: object.blockId || object.objectId, pageIndex: Number(object.pageIndex || 0) || 0, ...object.rect }));

    for (let i = 0; i < images.length; i += 1) {
        for (let j = i + 1; j < images.length; j += 1) {
            const a = images[i];
            const b = images[j];
            if (a.pageIndex !== b.pageIndex) {
                continue;
            }

            const overlapX = Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x);
            const overlapY = Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y);
            assert.ok(
                overlapX <= 1 || overlapY <= 1,
                `floating images '${a.id}' and '${b.id}' overlap`
                + ` (a=${a.x.toFixed(1)},${a.y.toFixed(1)} ${a.width.toFixed(1)}x${a.height.toFixed(1)};`
                + ` b=${b.x.toFixed(1)},${b.y.toFixed(1)} ${b.width.toFixed(1)}x${b.height.toFixed(1)})`);
        }
    }
}

function shouldReserveSpace(object) {
    const mode = object.object?.wrapMode || object.wrapMode || 'Inline';
    return mode === 'Square' || mode === 'Tight' || mode === 'Through' || mode === 'TopBottom';
}

test('block flow stays monotonic across floats and empty paragraphs (no backward Y jump)', () => {
    const layout = layoutCanvasDocument({
        documentId: 'monotonic-flow',
        pageSettings: { width: 600, height: 1400, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 10 },
        body: {
            blocks: [
                paragraph('a', 'First paragraph with a couple of lines of descriptive contract text here.', 1),
                paragraph('empty', '', 2),
                floatingSquareImage('float', { width: 180, height: 140, x: 320, y: 0, order: 3 }),
                paragraph('b', 'Second paragraph that wraps beside the floating object on the right side.', 4),
                paragraph('c', 'Third paragraph continues the contract body once the float is cleared.', 5),
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    // Text blocks (excluding the float) must keep a non-decreasing top so nothing jumps back up.
    const textBlocks = (layout.blocks || [])
        .filter(block => block.type !== 'image' && block.rect)
        .sort((left, right) => (left.sequence ?? 0) - (right.sequence ?? 0));
    for (let i = 1; i < textBlocks.length; i += 1) {
        const prev = textBlocks[i - 1];
        const curr = textBlocks[i];
        assert.ok(
            curr.rect.y >= prev.rect.y - 0.5,
            `block '${curr.blockId}' (y=${curr.rect.y.toFixed(1)}) must not start above previous '${prev.blockId}' (y=${prev.rect.y.toFixed(1)})`);
    }

    // The empty paragraph must keep a measurable, in-order slot (not collapse above its predecessor).
    const empty = textBlocks.find(block => block.blockId === 'empty');
    const a = textBlocks.find(block => block.blockId === 'a');
    assert.ok(empty.rect.y >= a.rect.y, 'empty paragraph must not move above its predecessor');

    assertNoCrossBlockTextOverlap(layout);
});

function assertNoCrossBlockTextOverlap(layout) {
    const rects = [];
    for (const block of layout.blocks || []) {
        if (block.type === 'image' || block.type === 'table') {
            continue;
        }

        for (const segment of block.segments || []) {
            if (segment.type === 'space' || segment.type === 'tab' || !segment.rect) {
                continue;
            }

            rects.push({
                blockId: block.blockId,
                pageIndex: Number(block.pageIndex || 0) || 0,
                ...segment.rect,
            });
        }
    }

    for (let i = 0; i < rects.length; i += 1) {
        for (let j = i + 1; j < rects.length; j += 1) {
            const a = rects[i];
            const b = rects[j];
            if (a.blockId === b.blockId || a.pageIndex !== b.pageIndex) {
                continue;
            }

            const overlapX = Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x);
            const overlapY = Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y);
            assert.ok(
                overlapX <= 0.5 || overlapY <= 0.5,
                `text runs from blocks '${a.blockId}' and '${b.blockId}' overlap`
                + ` (a=${a.x.toFixed(1)},${a.y.toFixed(1)} ${a.width.toFixed(1)}x${a.height.toFixed(1)};`
                + ` b=${b.x.toFixed(1)},${b.y.toFixed(1)} ${b.width.toFixed(1)}x${b.height.toFixed(1)})`);
        }
    }
}

function textRectsOf(layout, blockId) {
    const block = (layout.blocks || []).find(item => item.blockId === blockId);
    return (block?.segments || [])
        .filter(segment => segment.type !== 'space' && segment.type !== 'tab' && segment.rect)
        .map(segment => segment.rect);
}

function floatingSquareImage(id, { width, height, x, y, order = 1 }) {
    return {
        id,
        type: 'image',
        order,
        content: {
            type: 'image',
            image: {
                objectId: id,
                url: 'data:image/png;base64,iVBORw0KGgo=',
                layout: {
                    wrapMode: 'Square',
                    transform: { width, height },
                    // Mirrors the C# demo: wrapped images are anchored to the paragraph, so a
                    // zero/absent vertical offset must flow with the text, not pin to page top.
                    position: { x, y, verticalRelativeTo: 'paragraph' },
                },
            },
        },
    };
}

function paragraph(id, text, order) {
    return {
        id,
        type: 'paragraph',
        order,
        paragraphProperties: { alignment: 'left', lineSpacing: 1.1 },
        content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }] },
    };
}

function shortText(text) {
    return text;
}

function longText() {
    return Array.from({ length: 8 }, () => 'This paragraph wraps around the floating evidence image and continues below it once the float is cleared.').join(' ');
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, Array.from(text).reduce((sum, ch) => sum + (/\s/.test(ch) ? fontSize * 0.32 : fontSize * 0.52), 0)),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
