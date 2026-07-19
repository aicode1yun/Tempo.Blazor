import { asArray, asText, clone, sortObject } from '../../document-editor/core/helpers.mjs';
import { transformOperation as transformCoreTextOperation } from '../../document-editor/core-engine/operations.mjs';
import { createCanvasDocumentModel } from '../model/canvas-document-model.mjs';

export function applyRemoteOperationBatch(model = {}, remoteBatch = {}, options = {}) {
    let working = createCanvasDocumentModel(clone(model));
    const operations = asArray(remoteBatch?.batch?.operations || remoteBatch?.operations);
    const localOperations = asArray(options.localOperations);
    const appliedOperationIds = [];
    const failedOperationIds = [];
    const conflicts = [];

    for (const original of operations) {
        for (const transformed of asList(transformOperationAgainstLocal(original, localOperations))) {
            const result = applyOperation(working, transformed);
            if (result.applied) {
                working = result.model;
                appliedOperationIds.push(asText(transformed.operationId || transformed.OperationId));
            } else {
                failedOperationIds.push(asText(transformed.operationId || transformed.OperationId));
                conflicts.push({
                    operationId: asText(transformed.operationId || transformed.OperationId),
                    reason: result.reason || 'operationRejected',
                });
            }
        }
    }

    return sortObject({
        success: failedOperationIds.length === 0,
        changed: appliedOperationIds.length > 0,
        model: working,
        appliedOperationIds,
        failedOperationIds,
        conflicts,
    });
}

export function transformOperationAgainstLocal(operation = {}, localOperations = []) {
    let transformed = [clone(operation)];
    for (const local of localOperations) {
        transformed = transformed.flatMap(item => asList(transformPair(item, local)));
    }

    return transformed.length === 1 ? transformed[0] : transformed;
}

export function applyOperation(model = {}, operation = {}) {
    const working = createCanvasDocumentModel(clone(model));
    const type = normalizeType(operation.type || operation.Type);
    if (!type) {
        return { applied: false, model: working, reason: 'unknownOperationType' };
    }

    if (type === 'insertText') {
        return applyTextMutation(working, operation, 'insert');
    }

    if (type === 'deleteText') {
        return applyTextMutation(working, operation, 'delete');
    }

    if (type === 'insertBlock') {
        return applyInsertBlock(working, operation);
    }

    if (type === 'deleteBlock') {
        return applyDeleteBlock(working, operation);
    }

    if (type === 'moveBlock') {
        return applyMoveBlock(working, operation);
    }

    if (type === 'updateBlock') {
        return applyUpdateBlock(working, operation);
    }

    if (type === 'setBlockAttribute') {
        return applySetBlockAttribute(working, operation);
    }

    if (type === 'moveDrawingObject') {
        return applyMoveDrawingObject(working, operation);
    }

    if (type === 'addInlineMark' || type === 'removeInlineMark') {
        return applyInlineMark(working, operation, type === 'addInlineMark');
    }

    return { applied: true, model: working, reason: 'documentLevelOperation' };
}

function transformPair(remote, local) {
    const remoteType = normalizeType(remote.type || remote.Type);
    const localType = normalizeType(local.type || local.Type);
    const remoteTarget = targetOf(remote);
    const localTarget = targetOf(local);
    if (!remoteType || !localType || asText(remoteTarget.blockId) !== asText(localTarget.blockId)) {
        return remote;
    }

    const result = clone(remote);
    const target = targetOf(result);
    let offset = numberOrNull(target.offset);
    if (offset == null) {
        return result;
    }

    const coreRemote = toCoreTextOperation(remote);
    const coreLocal = toCoreTextOperation(local);
    if (coreRemote && coreLocal) {
        return transformCoreTextOperation(coreRemote, coreLocal, compareOperationPriority(remote, local) > 0 ? 'right' : 'left')
            .map((fragment, index) => fromCoreTextOperation(result, fragment, index));
    }

    if (localType === 'insertText') {
        const localOffset = numberOrNull(localTarget.offset) ?? 0;
        const length = asText(local.text ?? local.Text).length;
        if (offset > localOffset || (offset === localOffset && compareOperationPriority(remote, local) > 0)) {
            offset += length;
        }
    }

    if (localType === 'deleteText') {
        const localOffset = numberOrNull(localTarget.offset) ?? 0;
        const length = Math.max(0, Number(localTarget.length ?? localTarget.Length ?? asText(local.text ?? local.Text).length) || 0);
        if (offset > localOffset) {
            offset -= Math.min(length, offset - localOffset);
        }
        if (remoteType === 'deleteText') {
            const remoteLength = Math.max(0, Number(target.length ?? target.Length ?? 0) || 0);
            const overlapStart = Math.max(offset, localOffset);
            const overlapEnd = Math.min(offset + remoteLength, localOffset + length);
            if (overlapEnd > overlapStart) {
                target.length = Math.max(0, remoteLength - (overlapEnd - overlapStart));
            }
        }
    }

    target.offset = Math.max(0, offset);
    result.target = target;
    return result;
}

function toCoreTextOperation(operation) {
    const type = normalizeType(operation?.type || operation?.Type);
    if (type !== 'insertText' && type !== 'deleteText') {
        return null;
    }

    const target = targetOf(operation);
    return {
        type: type === 'insertText' ? 'insert' : 'delete',
        blockId: asText(target.blockId || target.BlockId),
        offset: Math.max(0, Number(target.offset ?? target.Offset ?? 0) || 0),
        text: asText(operation.text ?? operation.Text),
    };
}

function fromCoreTextOperation(template, operation, index) {
    const result = clone(template);
    const target = targetOf(result);
    const type = operation.type === 'insert' ? 'insertText' : 'deleteText';
    result.type = type;
    target.blockId = asText(operation.blockId);
    target.offset = Math.max(0, Number(operation.offset || 0) || 0);
    target.length = asText(operation.text).length;
    result.target = target;
    result.text = asText(operation.text);
    if (index > 0) {
        result.operationId = `${asText(result.operationId || result.OperationId)}:${index + 1}`;
    }

    return result;
}

function compareOperationPriority(left, right) {
    return operationKey(left).localeCompare(operationKey(right));
}

function operationKey(operation) {
    const metadata = operation?.metadata || operation?.Metadata || {};
    return [
        metadata.clientId || metadata.ClientId || '',
        operation.operationId || operation.OperationId || '',
    ].map(asText).join(':');
}

function applyTextMutation(model, operation, mode) {
    const target = targetOf(operation);
    const location = findBlockLocation(model, asText(target.blockId || target.BlockId), asText(target.tableCellId || target.TableCellId));
    if (!location?.block || !Array.isArray(location.block.content?.runs)) {
        return { applied: false, model, reason: 'blockNotFound' };
    }

    const offset = Math.max(0, Number(target.offset ?? target.Offset ?? 0) || 0);
    if (mode === 'insert') {
        insertTextIntoRuns(location.block.content.runs, offset, asText(operation.text ?? operation.Text));
    } else {
        const length = Math.max(0, Number(target.length ?? target.Length ?? asText(operation.text ?? operation.Text).length) || 0);
        deleteTextFromRuns(location.block.content.runs, offset, length);
    }

    refreshModelVersion(model);
    syncSections(model);
    return { applied: true, model };
}

function applyInsertBlock(model, operation) {
    const block = clone(operation.block || operation.Block);
    if (!block || !asText(block.id)) {
        return { applied: false, model, reason: 'blockMissing' };
    }

    const target = targetOf(operation);
    const container = findContainer(model, asText(target.tableCellId || target.TableCellId));
    if (!container) {
        return { applied: false, model, reason: 'containerNotFound' };
    }

    const blocks = ensureBlocks(container);
    const existing = blocks.findIndex(item => asText(item.id) === asText(block.id));
    if (existing >= 0) {
        blocks.splice(existing, 1);
    }

    const order = Math.max(0, Math.min(blocks.length, Number(target.order ?? target.Order ?? blocks.length) || 0));
    blocks.splice(order, 0, block);
    refreshModelVersion(model);
    syncSections(model);
    return { applied: true, model };
}

function applyDeleteBlock(model, operation) {
    const target = targetOf(operation);
    const location = findBlockLocation(model, asText(target.blockId || target.BlockId), asText(target.tableCellId || target.TableCellId));
    if (!location) {
        return { applied: false, model, reason: 'blockNotFound' };
    }

    location.blocks.splice(location.index, 1);
    refreshModelVersion(model);
    syncSections(model);
    return { applied: true, model };
}

function applyMoveBlock(model, operation) {
    const target = targetOf(operation);
    const location = findBlockLocation(model, asText(target.blockId || target.BlockId), asText(target.tableCellId || target.TableCellId));
    if (!location) {
        return { applied: false, model, reason: 'blockNotFound' };
    }

    const [block] = location.blocks.splice(location.index, 1);
    const container = findContainer(model, asText(target.tableCellId || target.TableCellId)) || { blocks: location.blocks };
    const blocks = ensureBlocks(container);
    const order = Math.max(0, Math.min(blocks.length, Number(target.order ?? target.Order ?? blocks.length) || 0));
    blocks.splice(order, 0, block);
    if (isOutlineBlock(block)) {
        refreshOutlineCacheVersions(model);
    }

    refreshModelVersion(model);
    syncSections(model);
    return { applied: true, model };
}

function applyUpdateBlock(model, operation) {
    const block = clone(operation.block || operation.Block);
    const target = targetOf(operation);
    const blockId = asText(target.blockId || target.BlockId || block?.id);
    const location = findBlockLocation(model, blockId, asText(target.tableCellId || target.TableCellId));
    if (!location || !block) {
        return { applied: false, model, reason: location ? 'blockMissing' : 'blockNotFound' };
    }

    location.blocks[location.index] = block;
    refreshModelVersion(model);
    syncSections(model);
    return { applied: true, model };
}

function applySetBlockAttribute(model, operation) {
    const target = targetOf(operation);
    const location = findBlockLocation(model, asText(target.blockId || target.BlockId), asText(target.tableCellId || target.TableCellId));
    if (!location?.block) {
        return { applied: false, model, reason: 'blockNotFound' };
    }

    const name = asText(operation.attributeName || operation.AttributeName);
    const value = parseAttributeValue(operation.attributeValueJson ?? operation.AttributeValueJson);
    if (name === 'headingLevel') {
        location.block.content.headingLevel = value == null ? null : Math.max(1, Number(value) || 1);
        location.block.type = location.block.content.headingLevel ? 'heading' : 'paragraph';
        location.block.content.type = location.block.type;
    } else if (name === 'text') {
        location.block.content.runs = [{ id: 'run-1', type: 'text', text: asText(value), marks: [], preserve: {} }];
    } else if (name.startsWith('content.')) {
        setPath(location.block.content, name.slice('content.'.length), value);
    } else {
        setPath(location.block, name, value);
    }

    refreshModelVersion(model);
    syncSections(model);
    return { applied: true, model };
}

function applyMoveDrawingObject(model, operation) {
    const target = targetOf(operation);
    const layout = clone(operation.newLayout || operation.NewLayout);
    if (!layout) {
        return { applied: false, model, reason: 'layoutMissing' };
    }

    let changed = false;
    walkBlocks(model, block => {
        for (const run of asArray(block?.content?.runs)) {
            const drawing = run?.drawing || run?.Drawing;
            const objectId = asText(drawing?.objectId || drawing?.ObjectId || run?.objectId || run?.ObjectId);
            const runId = asText(run?.id || run?.Id);
            if ((target.objectId && objectId === asText(target.objectId))
                || (target.inlineId && runId === asText(target.inlineId))
                || (target.blockId && asText(block?.id) === asText(target.blockId) && target.inlineIndex === asArray(block?.content?.runs).indexOf(run))) {
                run.drawing = { ...(drawing || {}), layout };
                changed = true;
            }
        }
    });

    if (!changed) {
        return { applied: false, model, reason: 'drawingObjectNotFound' };
    }

    refreshModelVersion(model);
    return { applied: true, model };
}

function applyInlineMark(model, operation, add) {
    const target = targetOf(operation);
    const location = findBlockLocation(model, asText(target.blockId || target.BlockId), asText(target.tableCellId || target.TableCellId));
    if (!location?.block || !Array.isArray(location.block.content?.runs)) {
        return { applied: false, model, reason: 'blockNotFound' };
    }

    const mark = clone(operation.mark || operation.Mark);
    if (!mark || !asText(mark.type)) {
        return { applied: false, model, reason: 'markMissing' };
    }

    const start = Math.max(0, Number(target.offset ?? target.Offset ?? 0) || 0);
    const length = Math.max(0, Number(target.length ?? target.Length ?? 0) || 0);
    mutateRunsInRange(location.block.content.runs, start, length, run => {
        const marks = asArray(run.marks);
        run.marks = add
            ? marks.concat(marks.some(item => asText(item.type) === asText(mark.type)) ? [] : [clone(mark)])
            : marks.filter(item => asText(item.type) !== asText(mark.type));
    });

    refreshModelVersion(model);
    syncSections(model);
    return { applied: true, model };
}

function insertTextIntoRuns(runs, offset, text) {
    if (!text) {
        return;
    }

    const position = locateRunOffset(runs, offset);
    if (!position.run) {
        runs.push({ id: `run-${runs.length + 1}`, type: 'text', text, marks: [], preserve: {} });
        return;
    }

    const current = asText(position.run.text);
    position.run.text = current.slice(0, position.offsetInRun) + text + current.slice(position.offsetInRun);
}

function deleteTextFromRuns(runs, offset, length) {
    if (length <= 0) {
        return;
    }

    let remaining = length;
    let cursor = 0;
    for (const run of runs) {
        const text = asText(run.text);
        const start = cursor;
        const end = cursor + text.length;
        cursor = end;
        if (remaining <= 0 || offset >= end || offset + remaining <= start) {
            continue;
        }

        const localStart = Math.max(0, offset - start);
        const localEnd = Math.min(text.length, offset + remaining - start);
        run.text = text.slice(0, localStart) + text.slice(localEnd);
        remaining -= Math.max(0, localEnd - localStart);
    }
}

// Applies the mutator to EXACTLY the [offset, offset+length) character range: intersecting runs
// are split at the range boundaries first (head/tail keep the original formatting, the middle
// keeps the original run id), so a partial-range mark never leaks onto surrounding characters.
// Mirrors the C# applier's ApplyMarkAbsoluteRange semantics — required for cross-runtime
// operation convergence.
function mutateRunsInRange(runs, offset, length, mutator) {
    if (length <= 0) {
        return;
    }

    const endOffset = offset + length;
    const rebuilt = [];
    let cursor = 0;
    for (const run of runs) {
        const text = asText(run.text);
        const start = cursor;
        const end = cursor + text.length;
        cursor = end;
        if (end <= offset || start >= endOffset || typeof run?.text !== 'string') {
            rebuilt.push(run);
            continue;
        }

        const sliceStart = Math.max(offset, start) - start;
        const sliceEnd = Math.min(endOffset, end) - start;
        if (sliceStart > 0) {
            const head = clone(run);
            head.id = `${asText(run.id) || 'run'}-h`;
            head.text = text.slice(0, sliceStart);
            rebuilt.push(head);
        }

        const middle = clone(run);
        middle.text = text.slice(sliceStart, sliceEnd);
        mutator(middle);
        rebuilt.push(middle);

        if (sliceEnd < text.length) {
            const tail = clone(run);
            tail.id = `${asText(run.id) || 'run'}-t`;
            tail.text = text.slice(sliceEnd);
            rebuilt.push(tail);
        }
    }

    runs.splice(0, runs.length, ...rebuilt);
}

function locateRunOffset(runs, offset) {
    let cursor = 0;
    for (const run of runs) {
        const text = asText(run.text);
        const end = cursor + text.length;
        if (offset <= end) {
            return { run, offsetInRun: Math.max(0, offset - cursor) };
        }

        cursor = end;
    }

    return { run: runs[runs.length - 1] || null, offsetInRun: asText(runs[runs.length - 1]?.text).length };
}

function findBlockLocation(model, blockId, preferredCellId = '') {
    let found = null;
    function visit(blocks, cellId = '') {
        if (found) {
            return;
        }

        asArray(blocks).forEach((block, index) => {
            if (found) {
                return;
            }

            if (asText(block?.id) === blockId && (!preferredCellId || preferredCellId === cellId)) {
                found = { block, blocks, index, cellId };
                return;
            }

            const table = block?.content?.table || block?.content;
            for (const row of asArray(table?.rows)) {
                for (const cell of asArray(row?.cells)) {
                    visit(cell?.blocks, asText(cell?.id || ''));
                }
            }
        });
    }

    visit(model?.body?.blocks);
    return found;
}

function findContainer(model, tableCellId = '') {
    if (!tableCellId) {
        return model?.body || null;
    }

    let found = null;
    walkBlocks(model, block => {
        const table = block?.content?.table || block?.content;
        for (const row of asArray(table?.rows)) {
            for (const cell of asArray(row?.cells)) {
                if (asText(cell?.id) === tableCellId) {
                    found = cell;
                }
            }
        }
    });

    return found;
}

function ensureBlocks(container) {
    if (!Array.isArray(container.blocks)) {
        container.blocks = [];
    }

    return container.blocks;
}

function walkBlocks(model, visitor) {
    function visit(blocks) {
        for (const block of asArray(blocks)) {
            visitor(block);
            const table = block?.content?.table || block?.content;
            for (const row of asArray(table?.rows)) {
                for (const cell of asArray(row?.cells)) {
                    visit(cell?.blocks);
                }
            }
        }
    }

    visit(model?.body?.blocks);
}

function syncSections(model) {
    if (!Array.isArray(model.sections) || model.sections.length === 0) {
        return;
    }

    const blocks = asArray(model.body?.blocks);
    const assigned = new Set();
    for (const section of model.sections) {
        const sectionBlocks = blocks.filter(block => asText(block.sectionId || '') === asText(section.id || ''));
        if (sectionBlocks.length > 0) {
            section.blocks = sectionBlocks.map(clone);
            sectionBlocks.forEach(block => assigned.add(block));
        }
    }

    if (model.sections.length === 1) {
        model.sections[0].blocks = blocks.map(clone);
        return;
    }

    const first = model.sections[0];
    first.blocks = [
        ...asArray(first.blocks),
        ...blocks.filter(block => !assigned.has(block) && !asText(block.sectionId || '')).map(clone),
    ];
}

function refreshModelVersion(model) {
    model.version = Math.max(0, Number(model.version || 0) || 0) + 1;
}

function refreshOutlineCacheVersions(model) {
    model.outlineRevision = Math.max(0, Number(model.outlineRevision || 0) || 0) + 1;
    model.tableOfContentsRevision = Math.max(0, Number(model.tableOfContentsRevision || 0) || 0) + 1;
}

function isOutlineBlock(block) {
    const type = asText(block?.type || block?.content?.type).toLowerCase();
    return type === 'heading' || (Number(block?.content?.outlineLevel || 0) || 0) > 0;
}

function normalizeType(value) {
    const raw = asText(value);
    return raw ? raw.charAt(0).toLowerCase() + raw.slice(1) : '';
}

function asList(value) {
    return Array.isArray(value) ? value : (value == null ? [] : [value]);
}

function targetOf(operation) {
    const target = clone(operation.target || operation.Target || {});
    if (target.BlockId && !target.blockId) target.blockId = target.BlockId;
    if (target.TableCellId && !target.tableCellId) target.tableCellId = target.TableCellId;
    if (target.ObjectId && !target.objectId) target.objectId = target.ObjectId;
    if (target.InlineId && !target.inlineId) target.inlineId = target.InlineId;
    if (target.InlineIndex != null && target.inlineIndex == null) target.inlineIndex = target.InlineIndex;
    if (target.Offset != null && target.offset == null) target.offset = target.Offset;
    if (target.Length != null && target.length == null) target.length = target.Length;
    if (target.Order != null && target.order == null) target.order = target.Order;
    return target;
}

function numberOrNull(value) {
    const number = Number(value);
    return Number.isFinite(number) ? number : null;
}

function parseAttributeValue(value) {
    if (value == null || value === '') {
        return null;
    }

    if (typeof value !== 'string') {
        return value;
    }

    try {
        return JSON.parse(value);
    } catch {
        return value;
    }
}

function setPath(target, path, value) {
    const parts = asText(path).split('.').filter(Boolean);
    if (parts.length === 0) {
        return;
    }

    let current = target;
    for (let index = 0; index < parts.length - 1; index += 1) {
        const part = parts[index];
        if (!current[part] || typeof current[part] !== 'object') {
            current[part] = {};
        }

        current = current[part];
    }

    current[parts[parts.length - 1]] = value;
}
