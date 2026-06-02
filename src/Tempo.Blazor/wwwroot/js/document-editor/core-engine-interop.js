// Phase R.4.8 — core-engine-interop.js
// Blazor JS-interop shim for the model-owned core engine. Imported by
// TmDocumentCoreEngineHost via IJSRuntime; it lazy-loads the IIFE bundle
// (_content/Tempo.Blazor/js/document-editor.dist.js → window.tmDocumentEditorModules) and
// wraps coreEngine.createCoreEditor behind small, marshalable functions keyed by a handle
// id (so C# holds an opaque string, not a JS object graph).

const BUNDLE_URL = '_content/Tempo.Blazor/js/document-editor.dist.js';
let bundlePromise = null;

function ensureBundle() {
    if (typeof window !== 'undefined' && window.tmDocumentEditorModules) {
        return Promise.resolve(window.tmDocumentEditorModules);
    }
    if (!bundlePromise) {
        bundlePromise = new Promise(function (resolve, reject) {
            const existing = document.querySelector('script[data-tm-core-engine-bundle]');
            if (existing && window.tmDocumentEditorModules) { resolve(window.tmDocumentEditorModules); return; }
            const script = document.createElement('script');
            script.src = BUNDLE_URL;
            script.setAttribute('data-tm-core-engine-bundle', 'true');
            script.onload = function () { resolve(window.tmDocumentEditorModules); };
            script.onerror = function () { reject(new Error('Failed to load ' + BUNDLE_URL)); };
            document.head.appendChild(script);
        });
    }
    return bundlePromise;
}

const handles = new Map();
const dotnetRefs = new Map(); // R.5.22 — per-handle .NET ref for collab send callbacks
const collabs = new Map();    // R.5.22 — per-handle collab control handle (connectCollab)
let seq = 0;

export async function mount(element, modelJson, optionsJson, dotnetRef) {
    const modules = await ensureBundle();
    if (!modules || !modules.coreEngine || typeof modules.coreEngine.createCoreEditor !== 'function') {
        throw new Error('core engine bundle missing coreEngine.createCoreEditor');
    }
    const model = modelJson ? JSON.parse(modelJson) : { body: { blocks: [] } };
    const options = optionsJson ? JSON.parse(optionsJson) : {};
    const editor = modules.coreEngine.createCoreEditor({
        root: element,
        doc: document,
        model: model,
        pageSettings: options.pageSettings,
        layoutOptions: options.layoutOptions,
        ariaLabel: options.ariaLabel,
        autoFocus: options.autoFocus === true,
        // R.4.8 — notify .NET when an image object is selected/deselected (inspector panel).
        onObjectSelect: dotnetRef ? function (info) {
            try { dotnetRef.invokeMethodAsync('OnCoreObjectSelected', info ? JSON.stringify(info) : null); }
            catch (e) { /* circuit gone */ }
        } : undefined,
        // R.5.3 — notify .NET (debounced) when the model changes, so C# can autosave.
        onChange: dotnetRef ? function () {
            try { dotnetRef.invokeMethodAsync('OnCoreModelChanged'); }
            catch (e) { /* circuit gone */ }
        } : undefined,
        changeDebounceMs: options.changeDebounceMs,
        // R.5.23 — right-click → .NET shows a contextual menu at (x, y) with the engine context.
        onContextMenu: dotnetRef ? function (info, x, y) {
            try { dotnetRef.invokeMethodAsync('OnCoreContextMenu', info ? JSON.stringify(info) : null, Math.round(x || 0), Math.round(y || 0)); }
            catch (e) { /* circuit gone */ }
        } : undefined,
        // R.5.18/R.5.22 — a local text edit produced an operation (for op-log / collab broadcast).
        onOperation: dotnetRef ? function (op) {
            try { dotnetRef.invokeMethodAsync('OnCoreOperation', op ? JSON.stringify(op) : null); }
            catch (e) { /* circuit gone */ }
        } : undefined,
    });
    const id = 'core-editor-' + (++seq);
    handles.set(id, editor);
    if (dotnetRef) dotnetRefs.set(id, dotnetRef);
    return id;
}

// R.5.22 — connect this editor to the SignalR relay. Local ops auto-broadcast through the
// .NET OnCollabSend bridge; inbound server changes are fed back via collabReceiveServerChange.
export function connectCollab(id, clientId) {
    const editor = get(id);
    const ref = dotnetRefs.get(id);
    if (!editor || !ref || typeof editor.connectCollab !== 'function') return false;
    const handle = editor.connectCollab({
        clientId: clientId,
        send: function (msg) { try { ref.invokeMethodAsync('OnCollabSend', JSON.stringify(msg)); } catch (e) { /* circuit gone */ } },
    });
    collabs.set(id, handle);
    return true;
}
export function collabReceiveServerChange(id, changeJson) {
    const c = collabs.get(id);
    if (c && changeJson) { try { c.receiveServerChange(JSON.parse(changeJson)); } catch (e) { /* ignore one bad change */ } }
}
export function collabClientId(id) { const c = collabs.get(id); return c ? c.clientId : null; }

function get(id) { return handles.get(id) || null; }

export function exec(id, command, argJson) {
    const editor = get(id);
    if (!editor) return false;
    const arg = (argJson != null && argJson !== '') ? JSON.parse(argJson) : undefined;
    const result = editor.execCommand(command, arg);
    return result === undefined ? true : !!result;
}
export function query(id, command) { const e = get(id); return e ? !!e.queryCommand(command) : false; }
export function getModelJson(id) { const e = get(id); return e ? JSON.stringify(e.getModel()) : null; }
export function isDirty(id) { const e = get(id); return e ? !!e.isDirty() : false; }
export function markSaved(id) { const e = get(id); if (e) e.markSaved(); }
export function canUndo(id) { const e = get(id); return e ? !!e.canUndo() : false; }
export function canRedo(id) { const e = get(id); return e ? !!e.canRedo() : false; }
export function getFormattingStateJson(id) { const e = get(id); return e ? JSON.stringify(e.getFormattingState()) : null; }
export function getCommentsJson(id) { const e = get(id); return e ? JSON.stringify(e.getComments()) : null; }
export function getSelectedObjectInfoJson(id) { const e = get(id); if (!e) return null; const info = e.getSelectedObjectInfo(); return info ? JSON.stringify(info) : null; }
export function setObjectAltText(id, text) { const e = get(id); return e ? !!e.setObjectAltText(text) : false; }
export function setObjectWrapMode(id, mode) { const e = get(id); return e ? !!e.setObjectWrapMode(mode) : false; }
export function setObjectSize(id, width, height) { const e = get(id); return e ? !!e.setObjectSize(width, height) : false; }
export function setObjectAlignment(id, align) { const e = get(id); return e ? !!e.setObjectAlignment(align) : false; }
export function setObjectCaption(id, text) { const e = get(id); return e ? !!e.setObjectCaption(text) : false; }
export function setObjectPosition(id, x, y) { const e = get(id); return e ? !!e.setObjectPosition(x, y) : false; }
export function bringObjectForward(id) { const e = get(id); return e ? !!e.bringObjectForward() : false; }
export function sendObjectBackward(id) { const e = get(id); return e ? !!e.sendObjectBackward() : false; }
export function getParagraphStyleJson(id) { const e = get(id); return e ? (e.getParagraphStyle() || null) : null; }
// R.5.22 — apply a remote collaborator's text operation (already transformed by the caller).
export function applyRemoteOperation(id, opJson) { const e = get(id); if (!e || !opJson) return false; try { return !!e.applyRemoteOperation(JSON.parse(opJson)); } catch { return false; } }
// R.5.22 — set the collaborators' remote cursors (presence). cursorsJson = [{id,blockId,offset,color,label}].
export function setRemoteCursors(id, cursorsJson) { const e = get(id); if (!e) return 0; try { return e.setRemoteCursors(cursorsJson ? JSON.parse(cursorsJson) : []); } catch { return 0; } }
// R.5.18 — the local operation journal (text ops emitted since mount / last clear).
export function getOperationLogJson(id) { const e = get(id); return e ? JSON.stringify(e.getOperationLog()) : '[]'; }
export function focus(id) { const e = get(id); if (e) e.focus(); }
export function dispose(id) { const e = get(id); if (e) { try { e.destroy(); } catch { /* ignore */ } handles.delete(id); } dotnetRefs.delete(id); collabs.delete(id); }
