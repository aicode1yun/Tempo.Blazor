import { applyInlineFormatCommand, createInlineFormatState } from '../commands/inline-format.mjs';
import { applyCanvasTextEdit, canvasBlockText, normalizeCanvasSelection } from '../input/text-editing.mjs';
import {
    createPlainTextFragment,
    createUrlFragment,
    fragmentToHtml,
    fragmentToPlainText,
    INTERNAL_CLIPBOARD_MIME,
    isSingleUrl,
    normalizeClipboardHtml,
    normalizeFragment,
    parseInternalFragment,
    serializeInternalFragment,
} from './html-normalizer.mjs';

const IMAGE_CONTENT_TYPES = new Set(['image/png', 'image/jpeg', 'image/webp', 'image/gif']);

export function createCanvasClipboardController(options = {}) {
    const input = options.inputBridge?.input || options.input || null;
    const root = options.root || null;
    const getModel = requiredFunction(options.getModel, 'Canvas clipboard controller requires getModel.');
    const selectionController = options.selectionController || null;
    const getSelection = () => selectionController?.getSelection?.() || null;
    const setSelection = selection => selectionController?.setSelection?.(selection);
    const commit = requiredFunction(options.commit, 'Canvas clipboard controller requires commit.');
    const history = options.history || null;
    const uploadImage = typeof options.uploadImage === 'function' ? options.uploadImage : null;
    const clock = typeof options.now === 'function' ? options.now : () => performance.now();

    let mounted = false;
    let revision = 0;
    let debugSnapshot = createDebugSnapshot();

    function mount() {
        if (mounted) {
            return api;
        }

        input?.addEventListener?.('copy', onCopy);
        input?.addEventListener?.('cut', onCut);
        input?.addEventListener?.('paste', onPaste);
        root?.addEventListener?.('copy', onCopy);
        root?.addEventListener?.('cut', onCut);
        root?.addEventListener?.('paste', onPaste);
        mounted = true;
        return api;
    }

    function destroy() {
        input?.removeEventListener?.('copy', onCopy);
        input?.removeEventListener?.('cut', onCut);
        input?.removeEventListener?.('paste', onPaste);
        root?.removeEventListener?.('copy', onCopy);
        root?.removeEventListener?.('cut', onCut);
        root?.removeEventListener?.('paste', onPaste);
        mounted = false;
    }

    function onCopy(event) {
        const result = copy(event?.clipboardData || null);
        if (result.handled) {
            event?.preventDefault?.();
        }
    }

    function onCut(event) {
        const result = cut(event?.clipboardData || null);
        if (result.handled) {
            event?.preventDefault?.();
        }
    }

    function onPaste(event) {
        event?.preventDefault?.();
        void pasteFromClipboardData(event?.clipboardData || null);
    }

    function copy(dataTransfer = null) {
        const fragment = selectedObjectFragment(getModel(), getSelection()) || selectedFragment(getModel(), getSelection());
        if (!fragmentHasContent(fragment)) {
            return { handled: false, operation: 'copy' };
        }

        writeFragmentToClipboard(dataTransfer, fragment);
        captureDebug({ operation: 'copy', fragment });
        return { handled: true, operation: 'copy', fragment };
    }

    function cut(dataTransfer = null) {
        const before = captureSnapshot();
        const objectFragment = selectedObjectFragment(before.model, before.selection);
        const fragment = objectFragment || selectedFragment(before.model, before.selection);
        if (!fragmentHasContent(fragment)) {
            return { handled: false, operation: 'cut' };
        }

        writeFragmentToClipboard(dataTransfer, fragment);
        return finishCut(before, fragment, objectFragment);
    }

    // The delete half of a cut (shared by the clipboard-event path and the programmatic system-clipboard
    // path): removes the selected object or text range and commits a single undoable change.
    function finishCut(before, fragment, objectFragment) {
        if (objectFragment) {
            const result = removeSelectedObject(before.model, before.selection);
            if (!result.changed) {
                return { handled: false, operation: 'cut' };
            }

            const change = commitClipboardChange('cut-object', before, result.model, result.selection, {
                changed: true,
                operation: 'cut-object',
                dirtyBlockIds: result.dirtyBlockIds || [],
                removedBlockIds: result.removedBlockIds || [],
            });
            captureDebug({ operation: 'cut-object', fragment });
            return { handled: true, operation: 'cut-object', fragment, change };
        }

        const result = applyCanvasTextEdit(before.model, before.selection, {
            type: 'replaceRange',
            range: before.selection,
            text: '',
        });
        if (!result.changed) {
            return { handled: false, operation: 'cut' };
        }

        const change = commitClipboardChange('cut', before, result.model, result.selection, {
            changed: true,
            operation: 'cut',
            dirtyBlockIds: result.dirtyBlockIds || [],
            removedBlockIds: result.removedBlockIds || [],
        });
        captureDebug({ operation: 'cut', fragment });
        return { handled: true, operation: 'cut', fragment, change };
    }

    // Resolve the browser clipboard object (programmatic copy/paste from a context menu has no clipboard event).
    function systemClipboard() {
        const view = root?.ownerDocument?.defaultView || input?.ownerDocument?.defaultView || globalThis;
        return { view, clip: view?.navigator?.clipboard || null };
    }

    async function writeFragmentToSystemClipboard(fragment) {
        const { view, clip } = systemClipboard();
        if (!clip) {
            return false;
        }

        const normalized = normalizeFragment(fragment);
        const html = fragmentToHtml(normalized);
        const text = fragmentToPlainText(normalized);
        try {
            if (typeof clip.write === 'function' && typeof view.ClipboardItem === 'function') {
                const item = new view.ClipboardItem({
                    'text/html': new view.Blob([html], { type: 'text/html' }),
                    'text/plain': new view.Blob([text], { type: 'text/plain' }),
                });
                await clip.write([item]);
                return true;
            }

            if (typeof clip.writeText === 'function') {
                await clip.writeText(text);
                return true;
            }
        } catch {
            // permission denied / unsupported — fall through to a writeText attempt, then report failure.
            try {
                if (typeof clip.writeText === 'function') {
                    await clip.writeText(text);
                    return true;
                }
            } catch {
                return false;
            }
        }

        return false;
    }

    // Programmatic copy to the system clipboard (context-menu "Copy" — no clipboard event to hook).
    async function copyToSystemClipboard() {
        const fragment = selectedObjectFragment(getModel(), getSelection()) || selectedFragment(getModel(), getSelection());
        if (!fragmentHasContent(fragment)) {
            return { handled: false, operation: 'copy' };
        }

        const wrote = await writeFragmentToSystemClipboard(fragment);
        captureDebug({ operation: 'copy', fragment });
        return { handled: wrote, operation: 'copy', fragment, reason: wrote ? undefined : 'permission' };
    }

    // Programmatic cut to the system clipboard (context-menu "Cut").
    async function cutToSystemClipboard() {
        const before = captureSnapshot();
        const objectFragment = selectedObjectFragment(before.model, before.selection);
        const fragment = objectFragment || selectedFragment(before.model, before.selection);
        if (!fragmentHasContent(fragment)) {
            return { handled: false, operation: 'cut' };
        }

        const wrote = await writeFragmentToSystemClipboard(fragment);
        if (!wrote) {
            return { handled: false, operation: 'cut', reason: 'permission' };
        }

        return finishCut(before, fragment, objectFragment);
    }

    // Programmatic paste from the system clipboard (context-menu "Paste" — async Clipboard API, user gesture).
    async function pasteFromSystemClipboard() {
        const { clip } = systemClipboard();
        if (!clip) {
            return { handled: false, operation: 'paste', reason: 'unsupported' };
        }

        try {
            if (typeof clip.read === 'function') {
                const items = await clip.read();
                for (const item of items) {
                    if (item.types?.includes?.('text/html')) {
                        const html = await (await item.getType('text/html')).text();
                        const plain = item.types.includes('text/plain') ? await (await item.getType('text/plain')).text() : '';
                        return pasteFragment(normalizeClipboardHtml(html, plain), 'paste-html');
                    }
                }
                for (const item of items) {
                    if (item.types?.includes?.('text/plain')) {
                        const text = await (await item.getType('text/plain')).text();
                        return pasteFragment(createPlainTextFragment(text), 'paste-plain');
                    }
                }
            }

            if (typeof clip.readText === 'function') {
                const text = await clip.readText();
                if (text) {
                    return pasteFragment(createPlainTextFragment(text), 'paste-plain');
                }
            }

            return { handled: false, operation: 'paste' };
        } catch {
            return { handled: false, operation: 'paste', reason: 'permission' };
        }
    }

    async function pasteFromClipboardData(dataTransfer = null) {
        const imageFiles = filesFromClipboard(dataTransfer).filter(file => IMAGE_CONTENT_TYPES.has(String(file.type || '').toLowerCase()));
        if (imageFiles.length > 0) {
            const imageResult = await pasteImages(imageFiles);
            if (imageResult.handled) {
                return imageResult;
            }
        }

        const internalText = readClipboardData(dataTransfer, INTERNAL_CLIPBOARD_MIME);
        const internalFragment = internalText ? parseInternalFragment(internalText) : null;
        if (internalFragment?.blocks?.length) {
            return pasteFragment(internalFragment, 'paste-internal');
        }

        const html = readClipboardData(dataTransfer, 'text/html');
        const plainText = readClipboardData(dataTransfer, 'text/plain');
        if (html) {
            const fragment = normalizeClipboardHtml(html, plainText);
            return pasteFragment(fragment, 'paste-html');
        }

        if (plainText) {
            if (isSingleUrl(plainText)) {
                return pasteUrl(plainText);
            }

            return pasteFragment(createPlainTextFragment(plainText), 'paste-plain');
        }

        return { handled: false, operation: 'paste' };
    }

    function pasteFragment(fragment, operation = 'paste') {
        const before = captureSnapshot();
        const normalized = normalizeFragment(fragment);
        const result = insertFragment(before.model, before.selection, normalized);
        if (!result.changed) {
            return { handled: false, operation };
        }

        const change = commitClipboardChange(operation, before, result.model, result.selection, {
            changed: true,
            operation,
            dirtyBlockIds: result.dirtyBlockIds || allBlockIds(result.model),
            insertedBlockIds: result.insertedBlockIds || [],
            removedBlockIds: result.removedBlockIds || [],
        });
        captureDebug({ operation, fragment: normalized });
        return { handled: true, operation, fragment: normalized, change };
    }

    function pasteUrl(url) {
        const before = captureSnapshot();
        const selection = normalizeCanvasSelection(before.selection, before.model);
        if (!selection) {
            return { handled: false, operation: 'paste-url' };
        }

        if (!isCollapsed(selection)) {
            const result = applyInlineFormatCommand(before.model, selection, 'link', { href: String(url || '').trim() }, createInlineFormatState());
            if (result.changed) {
                const change = commitClipboardChange('paste-url-link', before, result.model, result.selection, {
                    changed: true,
                    operation: 'paste-url-link',
                    dirtyBlockIds: result.dirtyBlockIds || [],
                });
                const fragment = createUrlFragment(url, selectedPlainText(before.model, selection));
                captureDebug({ operation: 'paste-url-link', fragment });
                return { handled: true, operation: 'paste-url-link', fragment, change };
            }
        }

        return pasteFragment(createUrlFragment(url), 'paste-url');
    }

    async function pasteImages(files) {
        if (!uploadImage) {
            captureDebug({
                operation: 'paste-image',
                fragment: null,
                warnings: ['image-provider-unavailable'],
            });
            return { handled: false, operation: 'paste-image', reason: 'image-provider-unavailable' };
        }

        const blocks = [];
        for (const file of files) {
            const upload = await uploadImage(file);
            if (!upload?.success || (!upload.assetId && !upload.url)) {
                continue;
            }

            const assetId = String(upload.assetId || '');
            const url = String(upload.url || '');
            const blockId = `clipboard-image-${assetId || revision + blocks.length + 1}`;
            blocks.push({
                id: blockId,
                sectionId: null,
                type: 'image',
                order: (blocks.length + 1) * 10,
                paragraphProperties: {},
                content: {
                    type: 'image',
                    runs: [],
                    image: {
                        source: assetId ? 1 : 0,
                        url,
                        assetId: assetId || null,
                        altText: upload.fileName || file.name || '',
                        isDecorative: false,
                        caption: '',
                        size: { width: Number(upload.width || 240) || 240, height: Number(upload.height || 160) || 160 },
                        naturalSize: { width: Number(upload.width || 240) || 240, height: Number(upload.height || 160) || 160 },
                        alignment: 1,
                        layout: { kind: 0 },
                        linkUrl: null,
                    },
                },
                preserve: {},
            });
        }

        if (blocks.length === 0) {
            return { handled: false, operation: 'paste-image', reason: 'upload-failed' };
        }

        return pasteFragment({ schemaVersion: 1, source: 'image', blocks }, 'paste-image');
    }

    function commitClipboardChange(operation, before, model, selection, result) {
        revision += 1;
        const after = { model: clone(model), selection: clone(selection), formatState: createInlineFormatState(), paragraphState: {} };
        history?.push?.({
            id: `canvas-clipboard-${revision}`,
            kind: 'clipboard',
            commandId: operation,
            before,
            after,
        });
        const change = {
            model,
            selection,
            result,
            command: {
                id: operation,
                changed: true,
                revision,
            },
            clipboard: {
                operation,
                revision,
            },
        };
        const render = commit(change);
        return { render, revision };
    }

    function captureSnapshot() {
        return {
            model: clone(getModel()),
            selection: clone(getSelection()),
            formatState: createInlineFormatState(),
            paragraphState: {},
        };
    }

    function captureDebug(input) {
        debugSnapshot = createDebugSnapshot(input);
        publishDiagnostics();
    }

    function publishDiagnostics() {
        root?.setAttribute?.('data-canvas-clipboard-revision', String(debugSnapshot.revision || 0));
        root?.setAttribute?.('data-canvas-clipboard-operation', debugSnapshot.operation || '');
        root?.setAttribute?.('data-canvas-clipboard-source', debugSnapshot.source || '');
        root?.setAttribute?.('data-canvas-clipboard-plain-text', debugSnapshot.plainText || '');
        root?.setAttribute?.('data-canvas-clipboard-html-length', String((debugSnapshot.rawHtml || '').length));
        root?.setAttribute?.('data-canvas-clipboard-block-count', String(debugSnapshot.blockCount || 0));
        root?.setAttribute?.('data-canvas-clipboard-warning-count', String((debugSnapshot.warnings || []).length));
    }

    function createDebugSnapshot(input = {}) {
        const fragment = input.fragment ? normalizeFragment(input.fragment) : null;
        return {
            revision: revision + (input.operation ? 1 : 0),
            operation: input.operation || '',
            rawHtml: fragment?.rawHtml || '',
            plainText: fragment ? fragmentToPlainText(fragment) : '',
            source: fragment?.source || 'unknown',
            normalizedJson: fragment ? JSON.stringify({ ...fragment, rawHtml: '' }, null, 2) : '',
            warnings: input.warnings || fragment?.warnings || [],
            blockCount: fragment?.blocks?.length || 0,
            capturedAt: new Date().toISOString(),
        };
    }

    const api = {
        mount,
        destroy,
        copy,
        cut,
        copyToSystemClipboard,
        cutToSystemClipboard,
        pasteFromSystemClipboard,
        pasteFromClipboardData,
        pasteFragment,
        pasteUrl,
        getState() {
            return {
                mounted,
                revision,
                debug: clone(debugSnapshot),
            };
        },
        getDebugSnapshot() {
            return clone(debugSnapshot);
        },
    };

    return api;
}

function selectedFragment(model, selection) {
    const range = orderedSelection(normalizeCanvasSelection(selection, model), model);
    if (!range || isCollapsed(range)) {
        return null;
    }

    const blocks = bodyBlocks(model);
    const startIndex = blocks.findIndex(block => block.id === range.anchor.blockId);
    const endIndex = blocks.findIndex(block => block.id === range.focus.blockId);
    if (startIndex < 0 || endIndex < 0) {
        return null;
    }

    const selected = [];
    for (let index = startIndex; index <= endIndex; index += 1) {
        const block = clone(blocks[index]);
        if (!isEditableBlock(block)) {
            selected.push(block);
            continue;
        }

        const start = block.id === range.anchor.blockId ? range.anchor.offset : 0;
        const end = block.id === range.focus.blockId ? range.focus.offset : canvasBlockText(model, block.id).length;
        block.content.runs = sliceRuns(block, start, end);
        selected.push(block);
    }

    return normalizeFragment({
        schemaVersion: 1,
        source: 'internal',
        blocks: selected,
    });
}

function selectedObjectFragment(model, selection) {
    const target = findSelectedObjectTarget(model, selection);
    if (!target) {
        return null;
    }

    if (target.role === 'imageBlock') {
        return normalizeFragment({
            schemaVersion: 1,
            source: 'internal-object',
            blocks: [clone(target.block)],
        });
    }

    const blocks = [cloneDrawingTargetBlock(target)];
    if (drawingKindName(target.source?.kind ?? target.source?.Kind) === 'group') {
        const childTargets = drawingTargetsByObjectIds(model, groupChildObjectIds(target.source));
        for (const child of childTargets) {
            blocks.push(cloneDrawingTargetBlock(child));
        }
    }

    return normalizeFragment({
        schemaVersion: 1,
        source: 'internal-object',
        blocks,
    });
}

function cloneDrawingTargetBlock(target) {
    const block = clone(target.block);
    block.content = block.content && typeof block.content === 'object'
        ? block.content
        : { type: block.type || 'paragraph', runs: [] };
    block.content.runs = [clone(target.run)];
    return block;
}

function drawingTargetsByObjectIds(model, objectIds) {
    const wanted = new Set((objectIds || []).map(id => String(id || '')).filter(Boolean));
    if (wanted.size === 0) {
        return [];
    }

    const targets = [];
    for (const block of bodyBlocks(model)) {
        for (const run of block?.content?.runs || []) {
            if (String(run?.type || '').toLowerCase() !== 'drawing' || !run.drawing) {
                continue;
            }

            const objectId = drawingObjectId(run.drawing);
            if (wanted.has(objectId)) {
                targets.push({ block, run, source: run.drawing, role: 'drawingRun' });
            }
        }
    }

    return targets;
}

function groupChildObjectIds(source) {
    const group = source?.group || source?.Group || {};
    const ids = group.childObjectIds ?? group.ChildObjectIds ?? [];
    return Array.isArray(ids) ? ids.map(id => String(id || '')).filter(Boolean) : [];
}

function drawingKindName(value) {
    if (typeof value === 'number') {
        return ['image', 'shape', 'textBox', 'line', 'connector', 'chart', 'group'][Math.max(0, Math.min(6, Math.trunc(value)))] || 'image';
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'textbox' || normalized === 'textbody') return 'textBox';
    if (normalized === 'line') return 'line';
    if (normalized === 'connector') return 'connector';
    if (normalized === 'chart') return 'chart';
    if (normalized === 'group') return 'group';
    if (normalized === 'image') return 'image';
    return 'shape';
}

function drawingObjectId(drawing) {
    return String(drawing?.objectId ?? drawing?.ObjectId ?? '');
}

function fragmentHasContent(fragment) {
    const normalized = fragment ? normalizeFragment(fragment) : null;
    return !!normalized
        && normalized.blocks.length > 0
        && (String(normalized.plainText || '').length > 0
            || normalized.blocks.some(block => block.type === 'image'
                || block.type === 'table'
                || (block.content?.runs || []).some(run => String(run?.type || '').toLowerCase() === 'drawing' && run.drawing)));
}

function removeSelectedObject(model, selection) {
    const working = clone(model || {});
    ensureBodyBlocks(working);
    const target = findSelectedObjectTarget(working, selection);
    if (!target) {
        return { changed: false, model: working, selection };
    }

    const blocks = bodyBlocks(working);
    const blockIndex = blocks.findIndex(block => block === target.block || String(block?.id || '') === String(target.block?.id || ''));
    if (blockIndex < 0) {
        return { changed: false, model: working, selection };
    }

    const dirtyBlockIds = [String(target.block?.id || '')].filter(Boolean);
    const removedBlockIds = [];
    if (target.role === 'imageBlock') {
        removedBlockIds.push(String(target.block.id || ''));
        blocks.splice(blockIndex, 1);
    } else {
        const runs = Array.isArray(target.block?.content?.runs) ? target.block.content.runs : [];
        const runIndex = runs.findIndex(run => run === target.run || String(run?.id || '') === String(target.run?.id || ''));
        if (runIndex < 0) {
            return { changed: false, model: working, selection };
        }

        runs.splice(runIndex, 1);
        if (runs.length === 0) {
            removedBlockIds.push(String(target.block.id || ''));
            blocks.splice(blockIndex, 1);
        }
    }

    working.version = Number(model?.version || 0) + 1;
    normalizeBlockOrder(blocks);
    synchronizeSectionsWithBody(working);
    return {
        changed: true,
        model: working,
        selection: nearestTextSelection(working, blockIndex),
        dirtyBlockIds,
        removedBlockIds: removedBlockIds.filter(Boolean),
    };
}

function insertFragment(model, selection, fragment) {
    const working = clone(model || {});
    ensureBodyBlocks(working);
    let range = normalizeCanvasSelection(selection, working);
    if (!range) {
        return { changed: false, model: working, selection: null };
    }

    const removed = [];
    if (!isCollapsed(range)) {
        const deleted = applyCanvasTextEdit(working, range, { type: 'replaceRange', range, text: '' });
        working.body = deleted.model.body;
        working.sections = deleted.model.sections;
        working.version = Number(model?.version || 0) || 0;
        range = deleted.selection;
        removed.push(...(deleted.removedBlockIds || []));
    }

    const blocks = normalizeFragment(fragment).blocks.map(clone);
    if (blocks.length === 0) {
        return { changed: false, model: working, selection: range };
    }
    const objectIdMap = createDrawingObjectIdMap(blocks, working);

    const targetIndex = bodyBlocks(working).findIndex(block => block.id === range.focus.blockId);
    if (targetIndex < 0) {
        return { changed: false, model: working, selection: range };
    }

    const target = working.body.blocks[targetIndex];
    const insertedBlockIds = [];
    let selectionAfter;
    if (blocks.length === 1 && isEditableBlock(blocks[0]) && isEditableBlock(target)) {
        const offset = clampOffset(target, range.focus.offset);
        const split = splitRunsAtOffset(target, offset);
        const pastedRuns = blocks[0].content.runs.map((run, index) => remapRun(run, target.id, index, working, objectIdMap));
        target.content.runs = compactRuns([...split.before, ...pastedRuns, ...split.after], target.id);
        const caret = { blockId: target.id, offset: offset + fragmentToPlainText({ blocks }).length };
        selectionAfter = collapsed(caret);
    } else {
        const offset = isEditableBlock(target) ? clampOffset(target, range.focus.offset) : 0;
        const split = isEditableBlock(target) ? splitRunsAtOffset(target, offset) : { before: [], after: [] };
        const prefix = clone(target);
        const suffix = clone(target);
        prefix.content.runs = compactRuns(split.before, prefix.id);
        suffix.id = uniqueBlockId(working, `${target.id}-after-paste`);
        suffix.content.runs = compactRuns(split.after, suffix.id);
        const prepared = blocks.map((block, index) => remapBlock(block, working, index, objectIdMap));
        insertedBlockIds.push(...prepared.map(block => block.id));
        working.body.blocks.splice(targetIndex, 1, prefix, ...prepared, suffix);
        selectionAfter = collapsed({ blockId: suffix.id, offset: 0 });
    }

    working.version = Number(model?.version || 0) + 1;
    normalizeBlockOrder(working.body.blocks);
    synchronizeSectionsWithBody(working);
    return {
        changed: true,
        model: working,
        selection: selectionAfter,
        insertedBlockIds,
        removedBlockIds: removed,
        dirtyBlockIds: allBlockIds(working),
    };
}

function writeFragmentToClipboard(dataTransfer, fragment) {
    if (!dataTransfer || typeof dataTransfer.setData !== 'function') {
        return false;
    }

    const normalized = normalizeFragment(fragment);
    dataTransfer.setData(INTERNAL_CLIPBOARD_MIME, serializeInternalFragment(normalized));
    dataTransfer.setData('text/plain', fragmentToPlainText(normalized));
    dataTransfer.setData('text/html', fragmentToHtml(normalized));
    return true;
}

function readClipboardData(dataTransfer, type) {
    if (!dataTransfer || typeof dataTransfer.getData !== 'function') {
        return '';
    }

    try {
        return String(dataTransfer.getData(type) || '');
    } catch {
        return '';
    }
}

function filesFromClipboard(dataTransfer) {
    const items = Array.from(dataTransfer?.items || []);
    const files = items
        .filter(item => String(item.kind || '').toLowerCase() === 'file')
        .map(item => item.getAsFile?.())
        .filter(Boolean);
    return files.length > 0 ? files : Array.from(dataTransfer?.files || []);
}

function selectedPlainText(model, selection) {
    const fragment = selectedFragment(model, selection);
    return fragment ? fragmentToPlainText(fragment) : '';
}

function bodyBlocks(model) {
    ensureBodyBlocks(model);
    return model.body.blocks;
}

function ensureBodyBlocks(model) {
    if (!model.body || typeof model.body !== 'object') {
        model.body = { blocks: [] };
    }

    if (!Array.isArray(model.body.blocks)) {
        model.body.blocks = [];
    }
}

function orderedSelection(selection, model) {
    if (!selection) {
        return null;
    }

    const blocks = bodyBlocks(model);
    const anchorIndex = blocks.findIndex(block => block.id === selection.anchor?.blockId);
    const focusIndex = blocks.findIndex(block => block.id === selection.focus?.blockId);
    if (anchorIndex < 0 || focusIndex < 0) {
        return null;
    }

    if (anchorIndex < focusIndex || (anchorIndex === focusIndex && Number(selection.anchor.offset || 0) <= Number(selection.focus.offset || 0))) {
        return selection;
    }

    return { anchor: selection.focus, focus: selection.anchor };
}

function isCollapsed(selection) {
    return selection?.anchor?.blockId === selection?.focus?.blockId
        && Number(selection?.anchor?.offset || 0) === Number(selection?.focus?.offset || 0);
}

function collapsed(position) {
    return { anchor: clone(position), focus: clone(position) };
}

function isEditableBlock(block) {
    return ['paragraph', 'heading', 'list', 'quote'].includes(String(block?.type || block?.content?.type || ''));
}

function sliceRuns(block, start, end) {
    const before = splitRunsAtOffset(block, start).after;
    const temp = clone(block);
    temp.content.runs = before;
    return compactRuns(splitRunsAtOffset(temp, end - start).before, block.id);
}

function splitRunsAtOffset(block, offset) {
    const before = [];
    const after = [];
    let cursor = 0;
    const target = clampOffset(block, offset);
    for (const run of runsOrEmpty(block)) {
        const text = String(run?.text || '');
        const length = runClipboardLength(run);
        const start = cursor;
        const end = cursor + length;
        cursor = end;
        if (String(run?.type || '').toLowerCase() !== 'text') {
            if (end <= target) {
                before.push(clone(run));
            } else {
                after.push(clone(run));
            }
            continue;
        }

        if (end <= target) {
            before.push(clone(run));
        } else if (start >= target) {
            after.push(clone(run));
        } else {
            const local = Math.max(0, Math.min(text.length, target - start));
            if (local > 0) before.push({ ...clone(run), text: text.slice(0, local) });
            if (local < text.length) after.push({ ...clone(run), text: text.slice(local) });
        }
    }

    return { before, after };
}

function runsOrEmpty(block) {
    if (!block.content || typeof block.content !== 'object') {
        block.content = { type: block.type || 'paragraph', runs: [] };
    }

    if (!Array.isArray(block.content.runs)) {
        block.content.runs = [];
    }

    return block.content.runs;
}

function compactRuns(runs, blockId) {
    const compacted = [];
    for (const run of runs.map(clone)) {
        if (String(run?.type || 'text') === 'text' && String(run.text || '').length === 0) {
            continue;
        }

        const previous = compacted.at(-1);
        if (previous
            && String(previous.type || 'text') === 'text'
            && String(run.type || 'text') === 'text'
            && JSON.stringify(previous.marks || []) === JSON.stringify(run.marks || [])) {
            previous.text = `${previous.text || ''}${run.text || ''}`;
        } else {
            compacted.push(run);
        }
    }

    return compacted.length > 0 ? compacted : [{
        id: `${blockId || 'block'}-empty-run`,
        type: 'text',
        text: '',
        marks: [],
        field: null,
        token: null,
        noteReference: null,
        drawing: null,
        preserve: {},
    }];
}

function clampOffset(block, offset) {
    return Math.max(0, Math.min(runsOrEmpty(block).reduce((sum, run) => sum + runClipboardLength(run), 0), Number(offset || 0) || 0));
}

function remapBlock(block, model, index, objectIdMap = new Map()) {
    const copy = clone(block);
    copy.id = uniqueBlockId(model, `${block.id || 'clipboard-block'}-${index + 1}`);
    if (copy.content?.runs) {
        copy.content.runs = copy.content.runs.map((run, runIndex) => remapRun(run, copy.id, runIndex, model, objectIdMap));
    }

    return copy;
}

function remapRun(run, blockId, index, model, objectIdMap = new Map()) {
    const copy = {
        ...clone(run),
        id: `${blockId}-paste-run-${index + 1}`,
    };
    if (String(copy.type || '').toLowerCase() === 'drawing' && copy.drawing) {
        const originalObjectId = String(copy.drawing.objectId || copy.drawing.ObjectId || '');
        copy.drawing = {
            ...copy.drawing,
            objectId: objectIdMap.get(originalObjectId)
                || uniqueObjectId(model, `${originalObjectId || 'clipboard-drawing'}-copy`),
        };
        remapDrawingReferences(copy.drawing, objectIdMap);
    }

    return copy;
}

function createDrawingObjectIdMap(blocks, model) {
    const objectIdMap = new Map();
    const reserved = new Set();
    for (const block of blocks || []) {
        for (const run of block?.content?.runs || []) {
            if (String(run?.type || '').toLowerCase() !== 'drawing' || !run.drawing) {
                continue;
            }

            const objectId = drawingObjectId(run.drawing);
            if (!objectId || objectIdMap.has(objectId)) {
                continue;
            }

            objectIdMap.set(objectId, uniqueObjectId(model, `${objectId}-copy`, reserved));
        }
    }

    return objectIdMap;
}

function remapDrawingReferences(drawing, objectIdMap) {
    const group = drawing.group || drawing.Group;
    if (group && typeof group === 'object') {
        const ids = group.childObjectIds ?? group.ChildObjectIds;
        if (Array.isArray(ids)) {
            group.childObjectIds = ids.map(id => objectIdMap.get(String(id || '')) || id);
        }

        drawing.group = group;
    }

    const metadata = drawing.metadata || drawing.Metadata;
    if (metadata && typeof metadata === 'object') {
        const groupId = String(metadata.groupId ?? metadata.GroupId ?? '');
        if (groupId && objectIdMap.has(groupId)) {
            metadata.groupId = objectIdMap.get(groupId);
        }

        drawing.metadata = metadata;
    }

    const shape = drawing.shape || drawing.Shape;
    if (shape && typeof shape === 'object') {
        remapConnection(shape.startConnection || shape.StartConnection, objectIdMap);
        remapConnection(shape.endConnection || shape.EndConnection, objectIdMap);
        drawing.shape = shape;
    }
}

function remapConnection(connection, objectIdMap) {
    if (!connection || typeof connection !== 'object') {
        return;
    }

    const objectId = String(connection.objectId ?? connection.ObjectId ?? '');
    if (objectId && objectIdMap.has(objectId)) {
        connection.objectId = objectIdMap.get(objectId);
    }
}

function runClipboardLength(run) {
    return String(run?.type || '').toLowerCase() === 'text'
        ? String(run?.text || '').length
        : 1;
}

function uniqueBlockId(model, base) {
    const existing = new Set(bodyBlocks(model).map(block => String(block.id || '')));
    let id = String(base || 'clipboard-block').replace(/[^a-zA-Z0-9_-]/g, '-');
    if (!existing.has(id)) {
        return id;
    }

    let index = 2;
    while (existing.has(`${id}-${index}`)) {
        index += 1;
    }

    return `${id}-${index}`;
}

function uniqueObjectId(model, base, reserved = null) {
    const existing = new Set();
    for (const block of bodyBlocks(model)) {
        if (String(block?.type || block?.content?.type || '').toLowerCase() === 'image') {
            existing.add(String(block.content?.image?.objectId ?? block.content?.image?.ObjectId ?? block.id ?? ''));
        }

        for (const run of block?.content?.runs || []) {
            if (String(run?.type || '').toLowerCase() === 'drawing' && run.drawing) {
                existing.add(String(run.drawing.objectId ?? run.drawing.ObjectId ?? run.id ?? ''));
            }
        }
    }
    for (const id of reserved || []) {
        existing.add(String(id || ''));
    }

    const seed = String(base || 'clipboard-object').replace(/[^a-zA-Z0-9_-]/g, '-');
    if (!existing.has(seed)) {
        reserved?.add?.(seed);
        return seed;
    }

    let index = 2;
    while (existing.has(`${seed}-${index}`)) {
        index += 1;
    }

    const next = `${seed}-${index}`;
    reserved?.add?.(next);
    return next;
}

function findSelectedObjectTarget(model, selection) {
    const objectId = String(selection?.object?.objectId || selection?.objectId || '');
    const blockId = String(selection?.object?.blockId || selection?.focus?.blockId || '');
    const runId = String(selection?.object?.runId || '');
    if (!objectId && !blockId && !runId) {
        return null;
    }

    for (const block of bodyBlocks(model)) {
        if (String(block?.type || block?.content?.type || '').toLowerCase() === 'image' && block.content?.image) {
            const candidateObjectId = String(block.content.image.objectId ?? block.content.image.ObjectId ?? block.id ?? '');
            if ((objectId && candidateObjectId === objectId) || (blockId && String(block.id || '') === blockId)) {
                return { block, source: block.content.image, role: 'imageBlock' };
            }
        }

        for (const run of block?.content?.runs || []) {
            if (String(run?.type || '').toLowerCase() !== 'drawing' || !run.drawing) {
                continue;
            }

            const candidateObjectId = String(run.drawing.objectId ?? run.drawing.ObjectId ?? run.id ?? '');
            if ((objectId && candidateObjectId === objectId)
                || (runId && String(run.id || '') === runId)
                || (blockId && String(block.id || '') === blockId)) {
                return { block, run, source: run.drawing, role: 'drawingRun' };
            }
        }
    }

    return null;
}

function nearestTextSelection(model, preferredIndex) {
    const blocks = bodyBlocks(model);
    if (blocks.length === 0) {
        const empty = { blockId: '', offset: 0 };
        return { anchor: empty, focus: empty };
    }

    const start = Math.max(0, Math.min(blocks.length - 1, Number(preferredIndex || 0) || 0));
    const indexes = [];
    for (let index = start; index < blocks.length; index += 1) {
        indexes.push(index);
    }

    for (let index = start - 1; index >= 0; index -= 1) {
        indexes.push(index);
    }

    for (const index of indexes) {
        const block = blocks[index];
        if (!isEditableBlock(block)) {
            continue;
        }

        const position = {
            blockId: String(block.id || ''),
            offset: clampOffset(block, 0),
        };
        return { anchor: position, focus: position };
    }

    const fallback = { blockId: String(blocks[start]?.id || ''), offset: 0 };
    return { anchor: fallback, focus: fallback };
}

function normalizeBlockOrder(blocks) {
    blocks.forEach((block, index) => {
        block.order = (index + 1) * 10;
    });
}

function synchronizeSectionsWithBody(model) {
    if (!Array.isArray(model.sections) || model.sections.length === 0) {
        return;
    }

    const blocks = bodyBlocks(model);
    for (const section of model.sections) {
        const sectionId = String(section?.id || '');
        section.blocks = blocks.filter(block => String(block.sectionId || '') === sectionId);
    }

    if (model.sections[0].blocks.length === 0) {
        model.sections[0].blocks = blocks;
    }
}

function allBlockIds(model) {
    return bodyBlocks(model).map(block => String(block.id || '')).filter(Boolean);
}

function requiredFunction(value, message) {
    if (typeof value !== 'function') {
        throw new Error(message);
    }

    return value;
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
