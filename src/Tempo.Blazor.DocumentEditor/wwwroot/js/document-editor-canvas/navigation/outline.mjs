import { createCanvasRunText, orderedCanvasBlocks } from '../layout/canvas-text-style.mjs';

export function extractCanvasOutline(model, layout = null) {
    const byBlockId = layoutBlockMap(layout);
    return orderedCanvasBlocks(model)
        .filter(isHeadingBlock)
        .map((block, index) => {
            const item = byBlockId.get(String(block.id || '')) || {};
            const pageIndex = Number(item.pageIndex || 0) || 0;
            return {
                index,
                blockId: String(block.id || ''),
                level: headingLevel(block),
                text: blockText(block),
                pageIndex,
                pageNumber: pageIndex + 1,
                y: Number(item.y ?? item.rect?.y ?? 0) || 0,
            };
        })
        .filter(item => item.blockId && item.text);
}

export function findOutlineTarget(outline, blockId) {
    const id = String(blockId || '');
    return (outline || []).find(item => String(item.blockId || '') === id) || null;
}

function isHeadingBlock(block) {
    const type = String(block?.type || block?.content?.type || '').toLowerCase();
    return type === 'heading' || Number(block?.content?.outlineLevel || 0) > 0;
}

function headingLevel(block) {
    return Math.max(1, Math.min(9, Number(block?.content?.headingLevel || block?.content?.outlineLevel || 1) || 1));
}

function blockText(block) {
    return (block?.content?.runs || []).map(createCanvasRunText).join('').replace(/\s+/g, ' ').trim();
}

function layoutBlockMap(layout) {
    const map = new Map();
    for (const block of layout?.blocks || []) {
        const blockId = String(block?.blockId || '');
        if (!blockId || map.has(blockId)) {
            continue;
        }

        const firstLine = Array.isArray(block.lines) ? block.lines[0] : null;
        map.set(blockId, {
            pageIndex: Number(block.pageIndex ?? firstLine?.pageIndex ?? 0) || 0,
            y: Number(block.y ?? block.rect?.y ?? firstLine?.rect?.y ?? 0) || 0,
        });
    }

    return map;
}
