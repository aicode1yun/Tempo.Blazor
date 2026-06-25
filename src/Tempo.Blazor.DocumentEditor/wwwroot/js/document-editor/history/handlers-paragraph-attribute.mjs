// Phase D — history/handlers-paragraph-attribute.mjs
// `createParagraphAttributeHandler({findBlock, nextSelectionForOperation})` factory
// → `applySetParagraphAttribute(model, op, differ)` — sets a single paragraph
// attribute (alignment, indent, lineSpacing, ...) on a block, records `previousValue`
// on the op for undo, and emits an `attributeChange` diff entry.

import { normalizeTarget } from '../core/normalize-target.mjs';

export function createParagraphAttributeHandler(options) {
    const opts = options || {};
    const required = ['findBlock', 'nextSelectionForOperation'];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createParagraphAttributeHandler requires options.${key} (function)`);
        }
    }
    const { findBlock, nextSelectionForOperation } = opts;

    return function applySetParagraphAttribute(model, op, differ) {
        const target = normalizeTarget(op.target || op.Target);
        const block = findBlock(model, target.blockId);
        if (!block) {
            return {
                ok: false,
                errors: [{
                    code: 'missing-target-block',
                    path: 'operation.target.blockId',
                    blockId: target.blockId,
                }],
            };
        }
        if (!block.content) block.content = { type: 'paragraph', runs: [] };
        const name = op.attributeName || op.AttributeName;
        op.previousValue = block.content[name];
        block.content[name] = op.value ?? op.Value;
        differ.record({
            attributeChange: {
                blockId: block.id,
                attributeName: name,
                value: block.content[name],
            },
            invalidatedLayoutScopes: [block.id],
        });
        return {
            ok: true,
            invalidatedLayoutScopes: [block.id],
            nextSelection: nextSelectionForOperation(model, op, block.id, target.offset, target),
        };
    };
}
