import { extractCanvasOutline } from './outline.mjs';

export function createTableOfContentsBlocks(model, layout = null, options = {}) {
    const levels = Math.max(1, Math.min(9, Number(options.levels || 3) || 3));
    const outline = extractCanvasOutline(model, layout).filter(item => Number(item.level || 1) <= levels);
    const tocId = String(options.tocId || `toc-${Date.now().toString(36)}`);
    const baseOrder = Number(options.order ?? firstBodyOrder(model)) || 0;
    return outline.map((item, index) => tocEntryBlock(tocId, item, baseOrder + index + 1, index));
}

export function insertTableOfContents(model, selection, layout = null, options = {}) {
    const working = clone(model);
    const blocks = bodyBlocks(working);
    const insertIndex = insertionIndex(blocks, selection);
    const order = orderBetween(blocks[insertIndex - 1], blocks[insertIndex]);
    const entries = createTableOfContentsBlocks(working, layout, { ...options, order });
    if (entries.length === 0) {
        return { changed: false, model, selection, dirtyBlockIds: [] };
    }

    blocks.splice(insertIndex, 0, ...entries);
    working.version = Number(working.version || 0) + 1;
    working.tableOfContentsRevision = Number(working.tableOfContentsRevision || 0) + 1;
    synchronizeSections(working);
    return {
        changed: true,
        model: working,
        selection: selectionForBlock(entries[0]),
        dirtyBlockIds: entries.map(block => block.id),
        insertedBlockIds: entries.map(block => block.id),
        operation: 'insertTableOfContents',
        entryCount: entries.length,
    };
}

export function updateTableOfContents(model, selection, layout = null, options = {}) {
    const working = clone(model);
    const blocks = bodyBlocks(working);
    const existing = blocks.filter(isTocBlock);
    if (existing.length === 0) {
        return insertTableOfContents(model, selection, layout, options);
    }

    const tocId = String(existing[0]?.content?.tableOfContents?.tocId || options.tocId || 'toc');
    const firstIndex = blocks.findIndex(isTocBlock);
    const firstOrder = Number(existing[0]?.order || 0) || orderBetween(blocks[firstIndex - 1], blocks[firstIndex]);
    const levels = Math.max(1, Math.min(9, Number(existing[0]?.content?.tableOfContents?.levels || options.levels || 3) || 3));
    const nextEntries = createTableOfContentsBlocks(working, layout, { tocId, levels, order: firstOrder - 1 });
    if (nextEntries.length === 0) {
        return { changed: false, model, selection, dirtyBlockIds: [] };
    }

    for (let index = blocks.length - 1; index >= 0; index--) {
        if (isTocBlock(blocks[index])) {
            blocks.splice(index, 1);
        }
    }

    blocks.splice(firstIndex, 0, ...nextEntries);
    working.version = Number(working.version || 0) + 1;
    working.tableOfContentsRevision = Number(working.tableOfContentsRevision || 0) + 1;
    synchronizeSections(working);
    return {
        changed: true,
        model: working,
        selection: selectionForBlock(nextEntries[0]),
        dirtyBlockIds: nextEntries.map(block => block.id),
        insertedBlockIds: nextEntries.map(block => block.id),
        operation: 'updateTableOfContents',
        entryCount: nextEntries.length,
    };
}

export function isTocBlock(block) {
    return block?.content?.tableOfContents?.isEntry === true;
}

function tocEntryBlock(tocId, item, order, index) {
    const segments = createGeneratedIndexEntrySegments(item, { leader: ' ....... ' });
    return {
        id: `${tocId}-entry-${index + 1}-${item.blockId}`,
        sectionId: null,
        type: 'paragraph',
        order,
        paragraphProperties: {
            leftIndent: Math.max(0, Number(item.level || 1) - 1) * 18,
            spacingAfter: 2,
        },
        content: {
            type: 'paragraph',
            tableOfContents: {
                tocId,
                isEntry: true,
                targetBlockId: item.blockId,
                level: item.level,
                text: item.text,
                pageNumber: item.pageNumber,
                pageIndex: item.pageIndex,
                y: item.y,
                levels: 3,
            },
            runs: [
                { id: `${tocId}-${index + 1}-text`, type: 'text', text: segments.text, marks: [] },
                { id: `${tocId}-${index + 1}-leader`, type: 'text', text: segments.leader, marks: [] },
                { id: `${tocId}-${index + 1}-page`, type: 'text', text: segments.pageText, marks: [] },
            ],
        },
        preserve: {},
    };
}

export function createGeneratedIndexEntrySegments(item, options = {}) {
    const level = Math.max(1, Number(item?.level || 1) || 1);
    const text = String(item?.text || '').trim();
    const pageText = String(Math.max(1, Number(item?.pageNumber ?? item?.page ?? 1) || 1));
    return {
        text: `${'  '.repeat(Math.max(0, level - 1))}${text}`,
        leader: String(options.leader ?? ' ....... '),
        pageText,
    };
}

export function formatGeneratedIndexText(entries, options = {}) {
    const separator = String(options.separator ?? '\t');
    return (Array.isArray(entries) ? entries : [])
        .map(entry => createGeneratedIndexEntrySegments(entry, { leader: '' }))
        .filter(entry => entry.text.length > 0)
        .map(entry => `${entry.text}${separator}${entry.pageText}`)
        .join('\n');
}

function bodyBlocks(model) {
    model.body ||= {};
    if (!Array.isArray(model.body.blocks) || model.body.blocks.length === 0) {
        const sectionBlocks = Array.isArray(model.sections)
            ? model.sections.flatMap(section => Array.isArray(section?.blocks) ? section.blocks : [])
            : [];
        model.body.blocks = sectionBlocks.length > 0
            ? sectionBlocks.slice().sort(compareBlocks)
            : [];
    }

    return model.body.blocks;
}

function insertionIndex(blocks, selection) {
    const blockId = String(selection?.focus?.blockId || selection?.anchor?.blockId || '');
    const index = blocks.findIndex(block => String(block.id || '') === blockId);
    return index >= 0 ? index : 0;
}

function firstBodyOrder(model) {
    const first = bodyBlocks(model)[0];
    return Number(first?.order || 0) || 0;
}

function orderBetween(before, after) {
    if (!before && !after) {
        return 10;
    }

    if (!before) {
        return (Number(after?.order || 0) || 0) - 10;
    }

    if (!after) {
        return (Number(before?.order || 0) || 0) + 10;
    }

    return ((Number(before.order || 0) || 0) + (Number(after.order || 0) || 0)) / 2;
}

function selectionForBlock(block) {
    return {
        anchor: { blockId: block.id, offset: 0 },
        focus: { blockId: block.id, offset: 0 },
    };
}

function synchronizeSections(model) {
    if (!Array.isArray(model.sections) || model.sections.length === 0) {
        return;
    }

    const blocks = bodyBlocks(model);
    const defaultSectionId = model.sections[0]?.id || null;
    for (const block of blocks) {
        block.sectionId ||= defaultSectionId;
    }

    for (const section of model.sections) {
        const sectionId = String(section?.id || '');
        const sectionBlocks = blocks.filter(block => String(block?.sectionId || '') === sectionId);
        section.blocks = sectionBlocks.length > 0 || sectionId
            ? sectionBlocks
            : blocks;
    }
}

function compareBlocks(left, right) {
    const order = (Number(left?.order) || 0) - (Number(right?.order) || 0);
    return order !== 0 ? order : String(left?.id || '').localeCompare(String(right?.id || ''));
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
