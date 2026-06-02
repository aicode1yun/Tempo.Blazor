// Phase R.4.2 — core-engine/edit-model.mjs
// Pure model-level text editing primitives for the new engine. These mutate the
// document model in place (preserving run structure → marks/styles survive) and
// return the next logical caret. No DOM, no history wrapping — they are the smallest
// correct edits that the off-screen input surface routes keystrokes into.
//
// Reuses the extracted run mutators (insert-text-run, run-mutators) and merge helper,
// so behaviour matches the rest of the engine.
//
//   applyInsertText(model, caret, text, deps)      → { ok, caret, structural }
//   applyDeleteBackward(model, caret, deps)        → { ok, caret, structural, deletedText? }
//   applyDeleteForward(model, caret, deps)         → { ok, caret, structural, deletedText? }
//   applyInsertParagraph(model, caret, deps)       → { ok, caret, structural, insertedBlockId? }
//   applyReplaceRange(model, blockId, start, end, text, deps) → { ok, caret, structural }
//
// deps: { findBlock, findBlockContainer }. `structural: true` means body.blocks
// changed (caller must rebuild model.indexes).

import { asArray, asText, clone, stableId } from '../core/helpers.mjs';
import { blockText } from '../core/text-helpers.mjs';
import { insertTextRun } from '../core/insert-text-run.mjs';
import { deleteTextRange, splitParagraphRuns } from '../core/run-mutators.mjs';
import { mergeAdjacentTextRuns, plainRuns } from '../core/inline-runs.mjs';

function asParagraph(block) {
    return block && block.type === 'paragraph' ? block : null;
}

function clampOffset(block, offset) {
    const len = blockText(block).length;
    return Math.max(0, Math.min(len, Number(offset || 0) || 0));
}

export function applyInsertText(model, caret, text, deps, attrs) {
    const findBlock = deps.findBlock;
    const block = asParagraph(findBlock(model, caret.blockId));
    const str = asText(text);
    if (!block || !str) return { ok: false, caret };
    const offset = clampOffset(block, caret.offset);
    insertTextRun(block, offset, str, attrs || {}); // attrs.marks → e.g. a tracked-insertion mark
    return { ok: true, structural: false, dirtyBlockIds: [block.id], caret: { blockId: block.id, offset: offset + str.length } };
}

export function applyDeleteBackward(model, caret, deps) {
    const { findBlock, findBlockContainer } = deps;
    const block = asParagraph(findBlock(model, caret.blockId));
    if (!block) return { ok: false, caret };
    const offset = clampOffset(block, caret.offset);
    if (offset > 0) {
        const deletedText = blockText(block).slice(offset - 1, offset);
        deleteTextRange(block, offset - 1, offset);
        return { ok: true, structural: false, dirtyBlockIds: [block.id], deletedText, caret: { blockId: block.id, offset: offset - 1 } };
    }
    // At the start of the block → merge into the previous paragraph (delete the break).
    const container = findBlockContainer(model, block.id);
    if (!container || container.index <= 0) return { ok: false, caret };
    const prev = asParagraph(container.blocks[container.index - 1]);
    if (!prev) return { ok: false, caret };
    const joinOffset = blockText(prev).length;
    prev.content.runs = mergeAdjacentTextRuns(
        asArray(prev.content && prev.content.runs).concat(asArray(block.content && block.content.runs)));
    if (!prev.content.runs.length) prev.content.runs = plainRuns('', prev.id + '-empty');
    container.blocks.splice(container.index, 1);
    return { ok: true, structural: true, dirtyBlockIds: [prev.id], removedBlockIds: [block.id], deletedText: '\n', caret: { blockId: prev.id, offset: joinOffset } };
}

export function applyDeleteForward(model, caret, deps) {
    const { findBlock, findBlockContainer } = deps;
    const block = asParagraph(findBlock(model, caret.blockId));
    if (!block) return { ok: false, caret };
    const offset = clampOffset(block, caret.offset);
    const len = blockText(block).length;
    if (offset < len) {
        const deletedText = blockText(block).slice(offset, offset + 1);
        deleteTextRange(block, offset, offset + 1);
        return { ok: true, structural: false, dirtyBlockIds: [block.id], deletedText, caret: { blockId: block.id, offset } };
    }
    // At the end of the block → pull the next paragraph up (delete the forward break).
    const container = findBlockContainer(model, block.id);
    if (!container || container.index >= container.blocks.length - 1) return { ok: false, caret };
    const next = asParagraph(container.blocks[container.index + 1]);
    if (!next) return { ok: false, caret };
    block.content.runs = mergeAdjacentTextRuns(
        asArray(block.content && block.content.runs).concat(asArray(next.content && next.content.runs)));
    if (!block.content.runs.length) block.content.runs = plainRuns('', block.id + '-empty');
    container.blocks.splice(container.index + 1, 1);
    return { ok: true, structural: true, dirtyBlockIds: [block.id], removedBlockIds: [next.id], deletedText: '\n', caret: { blockId: block.id, offset } };
}

// Replaces the text in [start, end) of a single block with `text`. Used by the IME
// composition flow (R.4.4): each `compositionupdate` removes the previous preview span
// and inserts the new one; `compositionend` replaces the preview with the final string.
// Stays within one paragraph (composition never crosses a block boundary), so it is
// always non-structural.
export function applyReplaceRange(model, blockId, start, end, text, deps) {
    const block = asParagraph(deps.findBlock(model, blockId));
    if (!block) return { ok: false, caret: { blockId, offset: Number(start || 0) || 0 } };
    const s = clampOffset(block, start);
    const e = clampOffset(block, Math.max(s, Number(end || 0) || 0));
    if (e > s) deleteTextRange(block, s, e);
    const str = asText(text);
    if (str) insertTextRun(block, s, str, {});
    if (!asArray(block.content && block.content.runs).length) {
        block.content = block.content || { type: 'paragraph' };
        block.content.runs = plainRuns('', block.id + '-empty');
    }
    return { ok: true, structural: false, dirtyBlockIds: [block.id], caret: { blockId: block.id, offset: s + str.length } };
}

export function applyInsertParagraph(model, caret, deps) {
    const { findBlock, findBlockContainer } = deps;
    const block = asParagraph(findBlock(model, caret.blockId));
    if (!block) return { ok: false, caret };
    const offset = clampOffset(block, caret.offset);
    const split = splitParagraphRuns(block, offset);
    const container = findBlockContainer(model, block.id);
    if (!container) return { ok: false, caret };
    const newBlockId = stableId('block', block.id + '-split');
    // Preserve paragraph-level properties (alignment/spacing/…); replace only runs.
    const newContent = Object.assign({}, clone(block.content || { type: 'paragraph' }), { runs: split.after });
    const newBlock = { id: newBlockId, type: 'paragraph', content: newContent };
    block.content.runs = split.before;
    container.blocks.splice(container.index + 1, 0, newBlock);
    return { ok: true, structural: true, insertedBlockId: newBlockId, dirtyBlockIds: [block.id, newBlockId], caret: { blockId: newBlockId, offset: 0 } };
}
