import assert from 'node:assert/strict';
import test from 'node:test';
import { imageDisplayCommands, layoutCanvasImageObject, objectExclusionIntervals } from '../image-render.mjs';

// B4 (UX fix 2026-06-11): a long image caption must wrap to the image width (it used to be a single
// un-clipped line overflowing to the right), and the wrapped caption's full height must be excluded from the
// surrounding text flow so the body text never collides with it.

const fontMetrics = {
    measureRun({ text, fontSize }) {
        return { width: String(text || '').length * (Number(fontSize) || 12) * 0.5, ascent: 9, descent: 3, lineHeight: 15 };
    },
};

function imageObject(caption, width = 148) {
    return {
        id: 'img-block-image',
        blockId: 'img-block',
        objectId: 'img-1',
        runId: '',
        role: 'imageBlock',
        caption,
        width,
        height: 84,
        x: 72,
        wrapMode: 'Square',
        isFloating: true,
        distanceTop: 0,
        distanceBottom: 0,
        distanceLeft: 8,
        distanceRight: 8,
    };
}

const page = { index: 0, body: { x: 72, y: 72, width: 468, height: 680 } };

test('a long caption wraps to multiple lines and grows the caption rect height', () => {
    const longCaption = 'Evidence preview loaded from the demo image provider for the contract agreement appendix';
    const layout = layoutCanvasImageObject(imageObject(longCaption), { page, y: 100, sequence: 0, fontMetrics });

    assert.ok(layout.captionLines.length >= 2, `caption must wrap to >=2 lines, got ${layout.captionLines.length}`);
    assert.equal(layout.captionLines.join(' '), longCaption.replace(/\s+/g, ' '), 'wrapped lines must preserve the caption text');
    assert.ok(layout.captionRect.height > 22, `caption rect must grow beyond one line (was ${layout.captionRect.height})`);
    assert.equal(layout.captionRect.height, layout.captionLines.length * 15 + 7);
});

test('caption display commands stay within the caption rect width (no horizontal overflow)', () => {
    const layout = layoutCanvasImageObject(imageObject('Evidence preview loaded from the demo image provider appendix detail'), { page, y: 100, sequence: 0, fontMetrics });
    const commands = imageDisplayCommands(layout, 0);
    const captionCommands = commands.filter(command => command.type === 'imageCaption');

    assert.ok(captionCommands.length >= 2, 'a wrapped caption emits one command per line');
    const right = layout.captionRect.x + layout.captionRect.width;
    for (const command of captionCommands) {
        assert.ok(command.x >= layout.captionRect.x - 0.5, 'caption line starts at the caption rect left');
        const measured = fontMetrics.measureRun({ text: command.text, fontSize: 12 }).width;
        assert.ok(command.x + measured <= right + 0.5, `caption line "${command.text}" must not overflow the rect width`);
    }
});

test('the wrapped caption is excluded from the text flow for its full height', () => {
    const longCaption = 'Evidence preview loaded from the demo image provider for the contract appendix detail line';
    const layout = layoutCanvasImageObject(imageObject(longCaption), { page, y: 100, sequence: 0, fontMetrics });

    // A text row inside the caption band (below the image, within the caption height) must be excluded beside
    // the image+caption column, i.e. the available interval must not start at the image's left edge.
    const captionMidY = layout.captionRect.y + layout.captionRect.height / 2;
    const intervals = objectExclusionIntervals([layout], page, captionMidY, 15);
    const startsAtImageLeft = intervals.some(interval => Math.abs(interval.x - layout.rect.x) < 1);
    assert.ok(!startsAtImageLeft, 'body text must not flow into the image/caption column within the caption band');

    // Below the full caption, the row is free again (interval spans the body width).
    const belowCaptionY = layout.captionRect.y + layout.captionRect.height + 30;
    const freeIntervals = objectExclusionIntervals([layout], page, belowCaptionY, 15);
    assert.ok(freeIntervals.some(interval => interval.width >= page.body.width - 1), 'rows below the caption span the full body width');
});
