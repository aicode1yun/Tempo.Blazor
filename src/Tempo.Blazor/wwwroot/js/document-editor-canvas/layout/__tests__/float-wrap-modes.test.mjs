import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../pagination.mjs';

// Phase 1.4b (perf+rendering fix 2026-06-08): lock in the per-wrap-mode placement and exclusion
// behavior so the paragraph-anchored float fix (resolveObjectY) keeps Word/OnlyOffice semantics.

const PAGE = { width: 600, height: 1200, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 };
const BODY_X = 48;
const BODY_WIDTH = 600 - 48 - 48; // 504

test('Square wrap: text wraps beside the float and clears below it', () => {
    const layout = layoutDoc([
        wrapImage('img', 'Square', { width: 200, height: 90, x: 280, y: 0 }),
        paragraph('p', longText(), 2),
    ]);
    const image = imageRect(layout, 'img');
    const lines = blockLines(layout, 'p');

    const besideLines = lines.filter(line => line.rect.y < image.y + image.height - 1);
    assert.ok(besideLines.length > 0, 'expected at least one line beside the float');
    for (const line of besideLines) {
        assert.ok(line.rect.width < BODY_WIDTH - 1, `beside line should be narrowed, got ${line.rect.width}`);
    }
    const belowLines = lines.filter(line => line.rect.y >= image.y + image.height);
    assert.ok(belowLines.length > 0, 'expected text to continue at full width below the float');
    assert.ok(belowLines.some(line => line.rect.width > BODY_WIDTH - 40), 'a below line should reclaim full width');
});

test('TopBottom wrap: reserves the full band, text starts below the image at full width', () => {
    const layout = layoutDoc([
        wrapImage('img', 'TopBottom', { width: 200, height: 110, x: 0, y: 0 }),
        paragraph('p', longText(), 2),
    ]);
    const image = imageRect(layout, 'img');
    const lines = blockLines(layout, 'p');

    assert.ok(lines.length > 0);
    for (const line of lines) {
        assert.ok(line.rect.y >= image.y + image.height - 1, `TopBottom text must not sit beside the image (line y=${line.rect.y}, image bottom=${image.y + image.height})`);
    }
    assert.ok(lines[0].rect.width > BODY_WIDTH - 40, 'first line below a TopBottom image should be full width');
});

test('InFrontOfText wrap: does not exclude text (text keeps full width)', () => {
    const layout = layoutDoc([
        wrapImage('img', 'InFrontOfText', { width: 200, height: 90, x: 60, y: 0, verticalRelativeTo: 'page' }),
        paragraph('p', longText(), 2),
    ]);
    const lines = blockLines(layout, 'p');
    assert.ok(lines.length > 0);
    // The last line of a paragraph is naturally short; assert the first (full) line is not narrowed.
    assert.ok(lines[0].rect.width > BODY_WIDTH - 40, 'in-front objects must not narrow the text');
    assert.ok(lines[0].rect.x <= BODY_X + 1, 'in-front objects must not shift text to the right');
});

test('BehindText wrap: does not exclude text (text keeps full width)', () => {
    const layout = layoutDoc([
        wrapImage('img', 'BehindText', { width: 200, height: 90, x: 60, y: 0, verticalRelativeTo: 'page' }),
        paragraph('p', longText(), 2),
    ]);
    const lines = blockLines(layout, 'p');
    assert.ok(lines.length > 0);
    assert.ok(lines[0].rect.width > BODY_WIDTH - 40, 'behind-text objects must not narrow the text');
    assert.ok(lines[0].rect.x <= BODY_X + 1, 'behind-text objects must not shift text to the right');
});

test('Inline image participates in the text flow and advances the cursor', () => {
    const layout = layoutDoc([
        paragraph('before', shortText('Paragraph before the inline image.'), 1),
        wrapImage('img', 'Inline', { width: 160, height: 80, x: 0, y: 0 }),
        paragraph('after', shortText('Paragraph after the inline image.'), 3),
    ]);
    const before = blockRect(layout, 'before');
    const image = imageRect(layout, 'img');
    const after = blockRect(layout, 'after');

    assert.ok(image.y >= before.y + before.height - 1, 'inline image should flow below the preceding paragraph');
    assert.ok(after.y >= image.y + image.height - 1, 'inline image should push following text below it');
});

test('captioned square image: caption sits below the image and reserves its footprint', () => {
    const image = wrapImage('img', 'Square', { width: 200, height: 90, x: 0, y: 0 });
    image.content.image.caption = 'Exhibit caption for the wrapped evidence image.';
    const layout = layoutDoc([
        image,
        paragraph('p', longText(), 2),
    ]);

    const object = (layout.objectLayouts || []).find(item => item.blockId === 'img');
    assert.ok(object, 'image must be laid out');
    assert.ok(object.captionRect, 'a captioned image must produce a caption rect');
    assert.ok(
        object.captionRect.y >= object.rect.y + object.rect.height - 0.5,
        `caption (y=${object.captionRect.y}) must sit below the image (bottom=${object.rect.y + object.rect.height})`);

    // Text that clears below the float must clear the caption too (footprint includes the caption).
    const captionBottom = object.captionRect.y + object.captionRect.height;
    const belowLines = blockLines(layout, 'p').filter(line => line.rect.x <= BODY_X + 1 && line.rect.width > BODY_WIDTH - 40);
    assert.ok(belowLines.length > 0, 'expected full-width lines below the captioned float');
    for (const line of belowLines) {
        assert.ok(line.rect.y >= captionBottom - 0.5, `full-width line (y=${line.rect.y}) must clear the caption (bottom=${captionBottom})`);
    }
});

function layoutDoc(blocks) {
    return layoutCanvasDocument({
        documentId: 'float-wrap-modes',
        pageSettings: PAGE,
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 10 },
        body: { blocks },
    }, { fontMetrics: createDeterministicMetrics() });
}

function imageRect(layout, blockId) {
    const object = (layout.objectLayouts || []).find(item => item.blockId === blockId);
    assert.ok(object, `image ${blockId} must be laid out`);
    return object.rect;
}

function blockLines(layout, blockId) {
    const block = (layout.blocks || []).find(item => item.blockId === blockId);
    assert.ok(block, `block ${blockId} must be laid out`);
    return (block.lines || []).filter(line => line.rect && (line.segments || []).some(segment => segment.type !== 'space'));
}

function blockRect(layout, blockId) {
    const block = (layout.blocks || []).find(item => item.blockId === blockId);
    assert.ok(block, `block ${blockId} must be laid out`);
    return block.rect;
}

function wrapImage(id, mode, { width, height, x, y, verticalRelativeTo = 'paragraph' }) {
    return {
        id,
        type: 'image',
        order: Number(id.match(/\d+/)?.[0] || 2),
        content: {
            type: 'image',
            image: {
                objectId: id,
                url: 'data:image/png;base64,iVBORw0KGgo=',
                layout: {
                    kind: mode === 'Inline' ? 0 : 1,
                    wrapMode: mode,
                    transform: { width, height },
                    position: { x, y, verticalRelativeTo },
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
    return Array.from({ length: 7 }, () => 'This paragraph contains enough words to wrap across several lines around the floating object and below it.').join(' ');
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
