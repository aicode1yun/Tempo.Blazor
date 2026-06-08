import { createCanvasDocumentEngine } from './entry.mjs';

const instances = new Map();
let nextInstanceId = 1;

export function createInteropBridge(engine) {
    if (!engine || typeof engine.getSnapshot !== 'function') {
        throw new Error('CanvasDocumentEngine interop bridge requires an engine instance.');
    }

    return {
        ready() {
            return engine.getSnapshot().mounted;
        },
        snapshot() {
            return engine.getSnapshot();
        },
        focus() {
            return engine.focusInput();
        },
        destroy() {
            engine.destroy();
        },
    };
}

export function mount(hostElement, mountElement, modelJson, optionsJson, dotNetRef) {
    const host = resolveElement(hostElement, 'Canvas engine host element is required.');
    const mountHost = resolveElement(mountElement, 'Canvas engine mount element is required.');
    const model = parseJson(modelJson, {});
    const options = parseJson(optionsJson, {});
    const handle = `canvas-document-engine-${nextInstanceId++}`;

    mountHost.replaceChildren();

    let state = null;
    const engine = createCanvasDocumentEngine({
        host: mountHost,
        document: mountHost.ownerDocument,
        model,
        pageSettings: options.pageSettings,
        ariaLabel: options.ariaLabel,
        inputAriaLabel: options.inputAriaLabel,
        accessibility: options.accessibility,
        contentControlRenderMode: options.contentControlRenderMode,
        uploadImage: file => uploadClipboardImage(dotNetRef, model.documentId, file),
        proofing: options.proofing,
        author: options.author,
        trackChanges: options.trackChanges,
        reviewDisplayMode: options.trackChanges?.reviewDisplayMode || options.reviewDisplayMode,
        onContextMenu: payload => notifyDotNet(dotNetRef, 'OnCanvasContextMenuRequested', payload),
        onSelectionChange: payload => notifyDotNet(dotNetRef, 'OnCanvasMiniToolbarChanged', payload),
        onCommandPaletteRequested: () => notifyDotNet(dotNetRef, 'OnCanvasCommandPaletteRequested', {}),
        onRibbonFocusRequested: () => notifyDotNet(dotNetRef, 'OnCanvasRibbonFocusRequested', {}),
        onVersionsPanelRequested: () => notifyDotNet(dotNetRef, 'OnCanvasVersionsPanelRequested', {}),
        onAnnotationSelected: payload => notifyDotNet(dotNetRef, 'OnCanvasAnnotationSelected', payload),
        onModelChanged: () => {
            if (!state) {
                return;
            }

            state.dirty = isDirtyForState(state);
            notifyChanged(state);
        },
    });
    const result = engine.render();
    state = {
        engine,
        host,
        mountHost,
        dotNetRef,
        handle,
        dirty: false,
        savedVersion: engine.getSnapshot().modelVersion,
        options,
    };
    instances.set(handle, state);
    setReadyAttributes(state, true);
    notifyDotNet(dotNetRef, 'OnCanvasEngineReady', buildStateSnapshot(state, result));
    return handle;
}

export function setOptions(handle, optionsJson) {
    const state = getInstance(handle);
    const options = parseJson(optionsJson, {});
    state.options = {
        ...(state.options || {}),
        ...options,
    };
    state.engine.updateOptions(options);
    return JSON.stringify({ ok: true });
}

export function dispose(handle) {
    const state = instances.get(handle);
    if (!state) {
        return;
    }

    state.engine.destroy();
    setReadyAttributes(state, false);
    instances.delete(handle);
}

export function getModelJson(handle) {
    const state = getInstance(handle);
    return JSON.stringify(state.engine.getSnapshot().model);
}

export function getSnapshotJson(handle) {
    const state = getInstance(handle);
    return JSON.stringify(state.engine.getSnapshot());
}

export function setModel(handle, modelJson) {
    const state = getInstance(handle);
    const model = parseJson(modelJson, {});
    state.engine.setModel(model).render();
    state.dirty = true;
    notifyChanged(state);
}

export function replaceModel(handle, modelJson) {
    const state = getInstance(handle);
    const model = parseJson(modelJson, {});
    state.engine.setModel(model).render();
    state.savedVersion = state.engine.getSnapshot().modelVersion;
    state.dirty = false;
}

export function applyRemoteOperationBatch(handle, batchJson) {
    const state = getInstance(handle);
    const batch = parseJson(batchJson, {});
    const result = state.engine.applyRemoteOperationBatch(batch);
    state.dirty = isDirtyForState(state);
    state.host.setAttribute('data-canvas-collaboration-applied-count', String((result.appliedOperationIds || []).length));
    state.host.setAttribute('data-canvas-collaboration-failed-count', String((result.failedOperationIds || []).length));
    notifyChanged(state);
    return JSON.stringify(result);
}

export function applyRemoteCursor(handle, cursorJson) {
    const state = getInstance(handle);
    const cursor = parseJson(cursorJson, {});
    const result = state.engine.applyRemoteCursor(cursor);
    state.host.setAttribute('data-canvas-presence-count', String(result.cursorCount || 0));
    return JSON.stringify(result);
}

export function applyRemoteCursors(handle, cursorsJson) {
    const state = getInstance(handle);
    const cursors = parseJson(cursorsJson, []);
    const result = state.engine.applyRemoteCursors(cursors);
    state.host.setAttribute('data-canvas-presence-count', String(result.cursorCount || 0));
    return JSON.stringify(result);
}

export function getCollaborationStateJson(handle) {
    const state = getInstance(handle);
    return JSON.stringify(state.engine.getSnapshot().collaboration || {});
}

export function getOfflineStateJson(handle) {
    const state = getInstance(handle);
    return JSON.stringify(state.engine.getOfflineState());
}

export function isDirty(handle) {
    const state = getInstance(handle);
    return state.dirty || state.engine.getSnapshot().modelVersion !== state.savedVersion;
}

export function markSaved(handle) {
    const state = getInstance(handle);
    state.savedVersion = state.engine.getSnapshot().modelVersion;
    state.dirty = false;
}

export function focus(handle) {
    const state = getInstance(handle);
    return state.engine.focusInput();
}

export function execCommand(handle, commandId, argumentJson = null) {
    const state = getInstance(handle);
    const argument = parseJson(argumentJson, null);
    const result = state.engine.execCommand(commandId, argument);
    state.dirty = isDirtyForState(state);
    notifyChanged(state);
    return JSON.stringify(result);
}

export function setTrackChangesEnabled(handle, enabled, authorJson = null) {
    const state = getInstance(handle);
    const author = parseJson(authorJson, null);
    const result = state.engine.setTrackChangesEnabled(enabled === true, author);
    return JSON.stringify({ enabled: result });
}

export function setReviewDisplayMode(handle, mode) {
    const state = getInstance(handle);
    const result = state.engine.setReviewDisplayMode(mode);
    return JSON.stringify({ reviewDisplayMode: result });
}

export function selectComment(handle, commentId) {
    const state = getInstance(handle);
    return JSON.stringify(state.engine.selectComment(commentId) || {});
}

export function selectRevision(handle, revisionId) {
    const state = getInstance(handle);
    return JSON.stringify(state.engine.selectRevision(revisionId) || {});
}

export function captureCommentAnchorJson(handle) {
    const state = getInstance(handle);
    const selection = state.engine.getSnapshot().selection || {};
    return JSON.stringify({
        type: selection.isCollapsed === false ? 'textRange' : 'block',
        blockId: selection.anchor?.blockId || selection.focus?.blockId || '',
        startOffset: Number(selection.anchor?.offset || 0) || 0,
        endOffset: Number(selection.focus?.offset || selection.anchor?.offset || 0) || 0,
    });
}

export function queryCommand(handle, commandId = null) {
    const state = getInstance(handle);
    return JSON.stringify(state.engine.queryCommand(commandId));
}

export function getFormattingStateJson(handle) {
    const state = getInstance(handle);
    const formatting = state.engine.getSnapshot().formatting || {};
    const commands = formatting.commands || {};
    return JSON.stringify({
        bold: commands.bold?.active === true,
        boldMixed: commands.bold?.mixed === true,
        italic: commands.italic?.active === true,
        italicMixed: commands.italic?.mixed === true,
        underline: commands.underline?.active === true,
        underlineMixed: commands.underline?.mixed === true,
        strikethrough: commands.strikethrough?.active === true,
        strikethroughMixed: commands.strikethrough?.mixed === true,
        superscript: commands.superscript?.active === true,
        superscriptMixed: commands.superscript?.mixed === true,
        subscript: commands.subscript?.active === true,
        subscriptMixed: commands.subscript?.mixed === true,
        smallCaps: commands.smallcaps?.active === true,
        smallCapsMixed: commands.smallcaps?.mixed === true,
        allCaps: commands.allcaps?.active === true,
        allCapsMixed: commands.allcaps?.mixed === true,
        doubleStrikethrough: commands.doublestrikethrough?.active === true,
        doubleStrikethroughMixed: commands.doublestrikethrough?.mixed === true,
        fontFamily: commands.fontfamily?.value || '',
        fontFamilyMixed: commands.fontfamily?.mixed === true,
        fontSize: commands.fontsize?.value || '',
        fontSizeMixed: commands.fontsize?.mixed === true,
        textColor: commands.textcolor?.value || '',
        textColorMixed: commands.textcolor?.mixed === true,
        highlightColor: commands.highlight?.value || '',
        highlightColorMixed: commands.highlight?.mixed === true,
        alignment: commands.align?.value || formatting.paragraph?.alignment || 'left',
        alignmentMixed: commands.align?.mixed === true || formatting.paragraph?.alignmentMixed === true,
        lineSpacing: Number(commands.lineSpacing?.value ?? formatting.paragraph?.lineSpacing ?? 1) || 1,
        lineSpacingMixed: commands.lineSpacing?.mixed === true || formatting.paragraph?.lineSpacingMixed === true,
        spacingBefore: Number(commands.spacingBefore?.value ?? formatting.paragraph?.spacingBefore ?? 0) || 0,
        spacingBeforeMixed: commands.spacingBefore?.mixed === true || formatting.paragraph?.spacingBeforeMixed === true,
        spacingAfter: Number(commands.spacingAfter?.value ?? formatting.paragraph?.spacingAfter ?? 0) || 0,
        spacingAfterMixed: commands.spacingAfter?.mixed === true || formatting.paragraph?.spacingAfterMixed === true,
        leftIndent: Number(formatting.paragraph?.leftIndent ?? 0) || 0,
        leftIndentMixed: formatting.paragraph?.leftIndentMixed === true,
        bulletList: commands.bulletList?.active === true || formatting.paragraph?.bulletList === true,
        numberedList: commands.numberedList?.active === true || formatting.paragraph?.numberedList === true,
        listMixed: commands.bulletList?.mixed === true || commands.numberedList?.mixed === true || formatting.paragraph?.listMixed === true,
        blockStyle: commands.blockStyle?.value || formatting.paragraph?.blockStyle || 'Normal',
        blockStyleMixed: commands.blockStyle?.mixed === true || formatting.paragraph?.blockStyleMixed === true,
        showRuler: commands.showRuler?.active !== false,
        showBlocks: commands.showBlocks?.active === true,
        showNonPrintingCharacters: commands.toggleNonPrintingCharacters?.active === true,
        viewMode: formatting.view?.viewMode || formatting.view?.mode || 'print',
        zoomPercent: Number(formatting.view?.zoomPercent || formatting.view?.zoom?.percent || 100) || 100,
        zoomPreset: formatting.view?.zoomPreset || formatting.view?.zoom?.preset || 'custom',
        toolbarHidden: formatting.view?.toolbarHidden === true,
        printPreviewActive: formatting.view?.printPreview?.active === true,
        image: formatting.image || null,
    });
}

export function getPrintPreviewStateJson(handle) {
    const state = getInstance(handle);
    const snapshot = state.engine.getSnapshot();
    return JSON.stringify({
        ...(snapshot.printPreview || {}),
        dialog: snapshot.printDialog || null,
    });
}

export function getUndoStateJson(handle) {
    const state = getInstance(handle);
    const history = state.engine.getSnapshot().history || {};
    return JSON.stringify({
        canUndo: (history.undoDepth || 0) > 0,
        canRedo: (history.redoDepth || 0) > 0,
    });
}

export function getSelectionStateJson(handle) {
    const state = getInstance(handle);
    const selection = state.engine.getSnapshot().selection || {};
    return JSON.stringify({
        isCollapsed: selection.isCollapsed !== false,
        pageIndex: Number(selection.pageIndex || 0) || 0,
        anchorBlockId: selection.anchor?.blockId || '',
        anchorOffset: Number(selection.anchor?.offset || 0) || 0,
        focusBlockId: selection.focus?.blockId || '',
        focusOffset: Number(selection.focus?.offset || 0) || 0,
        selectionRectCount: Number(selection.selectionRectCount || 0) || 0,
        inTable: selection.table?.inTable === true,
        tableId: selection.table?.tableId || '',
        cellId: selection.table?.cellId || '',
        rowIndex: Number(selection.table?.rowIndex || 0) || 0,
        cellIndex: Number(selection.table?.cellIndex || 0) || 0,
        objectSelected: selection.object != null,
        objectId: selection.object?.objectId || '',
        objectBlockId: selection.object?.blockId || '',
        objectRunId: selection.object?.runId || '',
        objectWrapMode: selection.object?.wrapMode || '',
    });
}

export function getDiagnosticsJson(handle) {
    const state = getInstance(handle);
    const snapshot = state.engine.getSnapshot();
    return JSON.stringify({
        architectureName: snapshot.architecture?.name || 'CanvasDocumentEngine',
        pageSurfaceStrategy: snapshot.architecture?.pageSurfaceStrategy || 'canvas-per-visible-page',
        pageCount: snapshot.render?.pageCount || snapshot.layout?.pages?.length || 0,
        selectionRectCount: snapshot.selection?.selectionRectCount || 0,
        proofingDiagnosticCount: snapshot.proofing?.diagnosticCount || 0,
        proofingSquiggleCount: snapshot.proofingOverlay?.squiggleCount || 0,
    });
}

export function getRuntimeDebugSnapshotJson(handle) {
    const state = getInstance(handle);
    const snapshot = state.engine.getSnapshot();
    const layoutPages = Array.isArray(snapshot.layout?.pages) ? snapshot.layout.pages : [];
    const displayPages = Array.isArray(snapshot.render?.pages) ? snapshot.render.pages : [];
    const displayListCommandCount = displayPages.reduce(
        (total, page) => total + (Array.isArray(page?.displayList) ? page.displayList.length : 0),
        0);
    return JSON.stringify({
        architecture: snapshot.architecture || {
            name: 'CanvasDocumentEngine',
            pageSurfaceStrategy: 'canvas-per-visible-page',
        },
        model: snapshot.model || null,
        modelVersion: snapshot.modelVersion || 0,
        dirty: isDirtyForState(state),
        savedVersion: state.savedVersion,
        layout: {
            pageCount: layoutPages.length,
            pages: layoutPages,
            blocks: Array.isArray(snapshot.layout?.blocks) ? snapshot.layout.blocks : [],
        },
        render: {
            pageCount: snapshot.render?.pageCount || displayPages.length,
            displayListCommandCount,
            selectionLayout: snapshot.render?.selectionLayout || null,
        },
        selection: snapshot.selection || null,
        formatting: snapshot.formatting || null,
        undo: snapshot.undo || null,
        search: snapshot.search || null,
        printPreview: snapshot.printPreview || null,
        proofing: snapshot.proofing || null,
        proofingOverlay: snapshot.proofingOverlay || null,
        comments: snapshot.comments || null,
        revisions: snapshot.revisions || null,
        restrictedEditing: snapshot.restrictedEditing || null,
        collaboration: snapshot.collaboration || null,
        presence: snapshot.presence || null,
    });
}

export function getSearchStateJson(handle) {
    const state = getInstance(handle);
    const search = state.engine.getSnapshot().search || {};
    return JSON.stringify(search);
}

export function getNavigationStateJson(handle) {
    const state = getInstance(handle);
    const formatting = state.engine.getSnapshot().formatting || {};
    return JSON.stringify(formatting.navigation || { outline: [], bookmarks: [] });
}

export function getClipboardDebugSnapshotJson(handle) {
    const state = getInstance(handle);
    const debug = state.engine.getSnapshot().clipboard?.debug || {};
    return JSON.stringify(debug);
}

export function on(handle, eventName) {
    const state = getInstance(handle);
    return JSON.stringify({
        eventName,
        ready: true,
        dirty: isDirty(handle),
        modelVersion: state.engine.getSnapshot().modelVersion,
    });
}

function getInstance(handle) {
    const state = instances.get(handle);
    if (!state) {
        throw new Error(`Canvas document engine instance '${handle}' does not exist.`);
    }

    return state;
}

function resolveElement(element, message) {
    if (!element || typeof element.appendChild !== 'function') {
        throw new Error(message);
    }

    return element;
}

function parseJson(json, fallback) {
    if (!json || typeof json !== 'string') {
        return fallback;
    }

    return JSON.parse(json);
}

function setReadyAttributes(state, ready) {
    const value = ready ? 'true' : 'false';
    state.host.setAttribute('data-canvas-engine-ready', value);
    state.mountHost.setAttribute('data-canvas-engine-ready', value);
    state.host.setAttribute('data-canvas-engine-interop', 'mounted');
    state.host.setAttribute('data-canvas-engine-handle', state.handle || '');
    state.host.setAttribute('data-canvas-engine-dirty', String(isDirtyForState(state)));
}

function notifyChanged(state) {
    state.host.setAttribute('data-canvas-engine-dirty', String(isDirtyForState(state)));
    notifyDotNet(state.dotNetRef, 'OnCanvasEngineChanged', {
        isDirty: isDirtyForState(state),
        modelVersion: state.engine.getSnapshot().modelVersion,
    });
}

function notifyDotNet(dotNetRef, methodName, payload) {
    if (!dotNetRef || typeof dotNetRef.invokeMethodAsync !== 'function') {
        return;
    }

    dotNetRef.invokeMethodAsync(methodName, JSON.stringify(payload));
}

function buildStateSnapshot(state, result) {
    const snapshot = state.engine.getSnapshot();
    return {
        ready: true,
        isDirty: isDirtyForState(state),
        modelVersion: snapshot.modelVersion,
        architectureName: result?.architecture?.name || snapshot.architecture?.name || 'CanvasDocumentEngine',
        pageSurfaceStrategy: result?.architecture?.pageSurfaceStrategy || snapshot.architecture?.pageSurfaceStrategy || 'canvas-per-visible-page',
        pageCount: snapshot.layout?.pages?.length || 0,
    };
}

async function uploadClipboardImage(dotNetRef, documentId, file) {
    if (!dotNetRef || typeof dotNetRef.invokeMethodAsync !== 'function' || !file) {
        return { success: false, errorMessage: 'Image provider is not available.' };
    }

    const streamReference = globalThis.DotNet?.createJSStreamReference
        ? globalThis.DotNet.createJSStreamReference(file)
        : null;
    if (!streamReference) {
        return { success: false, errorMessage: 'Browser stream reference is not available.' };
    }

    return await dotNetRef.invokeMethodAsync(
        'UploadCanvasClipboardImage',
        {
            documentId: documentId || '',
            fileName: file.name || 'clipboard-image.png',
            contentType: file.type || 'image/png',
            sizeBytes: Number(file.size || 0) || 0,
        },
        streamReference);
}

function isDirtyForState(state) {
    return state.dirty || state.engine.getSnapshot().modelVersion !== state.savedVersion;
}
