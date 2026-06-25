import assert from 'node:assert/strict';
import test from 'node:test';
import { applyCanvasTextEdit } from '../../input/text-editing.mjs';
import { applyBookmarkToSelection, findBookmark, listBookmarks } from '../bookmarks.mjs';
import { extractCanvasOutline } from '../outline.mjs';
import { formatGeneratedIndexText, insertTableOfContents, updateTableOfContents } from '../toc-generator.mjs';

test('outline extracts heading hierarchy with page coordinates from layout cache', () => {
    const model = outlineModel();
    const layout = {
        blocks: [
            { blockId: 'h1', pageIndex: 0, rect: { y: 72 } },
            { blockId: 'h2', pageIndex: 1, rect: { y: 96 } },
        ],
    };

    const outline = extractCanvasOutline(model, layout);

    assert.equal(outline.length, 2);
    assert.equal(outline[0].text, 'Project Alpha');
    assert.equal(outline[0].pageNumber, 1);
    assert.equal(outline[1].level, 2);
    assert.equal(outline[1].pageNumber, 2);
});

test('table of contents insertion and update are semantic canvas model changes', () => {
    const model = outlineModel();
    const layout = {
        blocks: [
            { blockId: 'h1', pageIndex: 0, rect: { y: 72 } },
            { blockId: 'h2', pageIndex: 1, rect: { y: 96 } },
        ],
    };

    const inserted = insertTableOfContents(model, { focus: { blockId: 'h1', offset: 0 } }, layout);
    assert.equal(inserted.changed, true);
    assert.equal(inserted.entryCount, 2);
    assert.equal(inserted.model.body.blocks[0].content.tableOfContents.isEntry, true);
    assert.equal(inserted.model.body.blocks[0].content.tableOfContents.targetBlockId, 'h1');

    inserted.model.body.blocks.find(block => block.id === 'h2').content.runs[0].text = 'Updated Scope';
    const updated = updateTableOfContents(inserted.model, inserted.selection, layout);
    assert.equal(updated.changed, true);
    assert.equal(updated.entryCount, 2);
    assert.match(updated.model.body.blocks.find(block => block.content?.tableOfContents?.targetBlockId === 'h2').content.runs[0].text, /Updated Scope/);
});

test('generated index formatter is shared by TOC-style generated lists', () => {
    const text = formatGeneratedIndexText([
        { text: 'Figure 1 Architecture', pageNumber: 3 },
        { text: 'Figure 2 Runtime', pageNumber: 5 },
    ]);

    assert.equal(text, 'Figure 1 Architecture\t3\nFigure 2 Runtime\t5');
});

test('bookmarks define, list, target lookup, and survive edits around the marked range', () => {
    const model = outlineModel();
    const marked = applyBookmarkToSelection(model, {
        anchor: { blockId: 'h2', offset: 0 },
        focus: { blockId: 'h2', offset: 'Delivery'.length },
    }, 'delivery-bookmark');

    assert.equal(marked.changed, true);
    assert.equal(listBookmarks(marked.model).length, 1);
    assert.deepEqual(findBookmark(marked.model, 'delivery-bookmark'), {
        name: 'delivery-bookmark',
        blockId: 'h2',
        start: 0,
        end: 'Delivery'.length,
    });

    const edited = applyCanvasTextEdit(marked.model, marked.selection, {
        type: 'replaceRange',
        range: {
            anchor: { blockId: 'h2', offset: 'Delivery Scope'.length },
            focus: { blockId: 'h2', offset: 'Delivery Scope'.length },
        },
        text: ' Updated',
        source: 'phase18-bookmark-survival',
    });
    assert.equal(edited.changed, true);
    assert.equal(findBookmark(edited.model, 'delivery-bookmark')?.blockId, 'h2');
    assert.equal(findBookmark(edited.model, 'delivery-bookmark')?.start, 0);
    assert.equal(findBookmark(edited.model, 'delivery-bookmark')?.end, 'Delivery'.length);
});

function outlineModel() {
    const blocks = [
        heading('h1', 10, 1, 'Project Alpha'),
        paragraph('p1', 20, 'Body text.'),
        heading('h2', 30, 2, 'Delivery Scope'),
    ];
    return {
        documentId: 'phase18-outline',
        body: { blocks },
        sections: [{ id: 's1', order: 0, blocks }],
    };
}

function heading(id, order, level, text) {
    return {
        id,
        sectionId: 's1',
        type: 'heading',
        order,
        content: { type: 'heading', headingLevel: level, outlineLevel: level, runs: [{ id: `${id}-r`, type: 'text', text, marks: [] }] },
    };
}

function paragraph(id, order, text) {
    return {
        id,
        sectionId: 's1',
        type: 'paragraph',
        order,
        content: { type: 'paragraph', runs: [{ id: `${id}-r`, type: 'text', text, marks: [] }] },
    };
}
