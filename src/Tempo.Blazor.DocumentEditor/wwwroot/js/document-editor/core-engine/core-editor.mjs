// Phase R.4.8 — core-engine/core-editor.mjs
// The bridge-facing facade over the model-owned render host. It mounts the engine,
// maps toolbar command ids → host APIs, and exposes save/dirty/undo state. Keeping this
// vocabulary here means the command surface stays unit-testable in Node + the browser harness.
//
//   createCoreEditor({ root, doc?, model, pageSettings?, layoutOptions?, ariaLabel?,
//                      measurementService?, autoFocus? }) → editor handle:
//     execCommand(id, arg) — bold/italic/.../align*/heading*/link/textColor/highlight/
//                            insertTable/insertImage/undo/redo/find/replace/trackChanges/comment
//     queryCommand(id)     — boolean state for toggle commands (toolbar pressed-state)
//     getModel() / setModel(model)
//     isDirty() / markSaved()
//     canUndo() / canRedo() / focus() / getHost() / destroy()

import { createRenderHost } from './render-host.mjs';
import { createRelayCollabClient } from './collab-client.mjs';

function normalizeId(id) {
    return String(id == null ? '' : id).toLowerCase().replace(/[^a-z0-9]/g, '');
}

// R.5.23c — build a spell checker from a marshalable word list (C#/JSON friendly):
//   { flagged: [...] }       → those words are misspelled
//   { known: [...] }         → anything NOT in the list is misspelled
//   { suggestions: { word: [...] } } → replacement candidates surfaced in the context menu
export function buildWordListChecker(options) {
    const o = options || {};
    const flagged = new Set((o.flagged || []).map(function (w) { return String(w).toLowerCase(); }));
    const known = o.known ? new Set((o.known || []).map(function (w) { return String(w).toLowerCase(); })) : null;
    const suggestions = o.suggestions || {};
    return {
        isMisspelled: function (word) {
            const w = String(word == null ? '' : word).toLowerCase();
            if (!w) return false;
            if (known) return !known.has(w);
            return flagged.has(w);
        },
        suggest: function (word) {
            const w = String(word == null ? '' : word);
            return suggestions[w] || suggestions[w.toLowerCase()] || [];
        },
    };
}

export function createCoreEditor(options) {
    const opts = options || {};
    const host = createRenderHost({
        doc: opts.doc,
        pageSettings: opts.pageSettings,
        layoutOptions: opts.layoutOptions,
        measurementService: opts.measurementService,
        ariaLabel: opts.ariaLabel,
        onObjectSelect: opts.onObjectSelect, // R.4.8 — JS→.NET object-selection event (inspector)
        onChange: opts.onChange,             // R.5.3 — debounced model-change event (autosave)
        changeDebounceMs: opts.changeDebounceMs,
        onLinkActivate: opts.onLinkActivate, // R.5.4 — Ctrl/Cmd+click hyperlink activation
        onContextMenu: opts.onContextMenu,   // R.5.23 — right-click → contextual menu info
        onOperation: opts.onOperation,       // R.5.18/R.5.22 — emitted text op (op-log + collab)
    });
    if (opts.root) host.mount(opts.root);
    if (opts.model) host.setModel(opts.model);
    let savedVersion = (opts.model && Number(opts.model.version || 0)) || 0;

    if (opts.root && opts.model) {
        host.render();
        host.attachInput();
        if (opts.autoFocus) host.focusInput();
    }

    // Toggle/simple commands (no argument).
    const simple = {
        bold: function () { return host.toggleMark('bold'); },
        italic: function () { return host.toggleMark('italic'); },
        underline: function () { return host.toggleMark('underline'); },
        strikethrough: function () { return host.toggleMark('strikethrough'); },
        strike: function () { return host.toggleMark('strikethrough'); },
        alignleft: function () { return host.setAlignment('left'); },
        aligncenter: function () { return host.setAlignment('center'); },
        alignright: function () { return host.setAlignment('right'); },
        alignjustify: function () { return host.setAlignment('justify'); },
        undo: function () { return host.undo(); },
        redo: function () { return host.redo(); },
        removelink: function () { return host.removeLink(); },
        acceptallrevisions: function () { return host.acceptAllRevisions(); },
        rejectallrevisions: function () { return host.rejectAllRevisions(); },
        addtablerow: function () { return host.addTableRow(); },
        addtablecolumn: function () { return host.addTableColumn(); },
        // R.5.9 advanced table editing
        deletetablerow: function () { return host.deleteTableRow(); },
        deletetablecolumn: function () { return host.deleteTableColumn(); },
        mergecells: function () { return host.mergeCellRight(); },
        mergecellright: function () { return host.mergeCellRight(); },
        splitcell: function () { return host.splitCell(); },
        insertrowabove: function () { return host.insertRowAbove(); },
        insertrowbelow: function () { return host.insertRowBelow(); },
        insertcolumnleft: function () { return host.insertColumnLeft(); },
        insertcolumnright: function () { return host.insertColumnRight(); },
    };
    // Marks/styles that toggle and report pressed-state for the toolbar.
    const toggleMarks = { bold: 1, italic: 1, underline: 1, strikethrough: 1 };

    function execCommand(id, arg) {
        const key = normalizeId(id);
        if (key === 'textcolor' || key === 'forecolor' || key === 'fontcolor') return host.applyMark('textcolor', arg);
        if (key === 'highlight' || key === 'backgroundcolor') return host.applyMark('highlight', arg);
        if (key === 'fontfamily') return host.applyMark('fontfamily', arg);
        if (key === 'fontsize') return host.applyMark('fontsize', arg);
        if (key === 'link' || key === 'hyperlink') return arg ? host.applyLink(arg) : host.removeLink();
        if (/^heading[1-6]$/.test(key)) return host.setParagraphStyle('Heading' + key.slice('heading'.length));
        if (key === 'normal' || key === 'paragraph' || key === 'body') return host.setParagraphStyle('Normal');
        if (key === 'title') return host.setParagraphStyle('Title');
        if (key === 'paragraphstyle' || key === 'style') return host.setParagraphStyle(arg);
        if (key === 'align') return host.setAlignment(arg);
        if (key === 'inserttable') { const o = arg || {}; return host.insertTable({ rows: o.rows || 2, cols: o.cols || o.columns || 2 }); }
        if (key === 'insertimage') return host.insertImage(arg || {});
        if (key === 'inserttext') return false; // text comes through the input surface, not commands
        if (key === 'find') return host.find(arg && arg.query != null ? arg.query : arg, arg && arg.options);
        if (key === 'findnext') return host.findNext();
        if (key === 'findprev' || key === 'findprevious') return host.findPrev();
        if (key === 'replaceall') return host.replaceAll(arg && arg.query, arg && arg.replacement, arg && arg.options);
        if (key === 'replacecurrent' || key === 'replaceone') return host.replaceCurrent(arg && arg.replacement != null ? arg.replacement : arg);
        if (key === 'clearfind') return host.clearFind();
        if (key === 'trackchanges') return host.setTrackChanges(arg == null ? !host.isTrackChanges() : !!arg);
        if (key === 'comment' || key === 'addcomment') { const o = arg || {}; return host.addComment(o.text != null ? o.text : arg, o.author); }
        if (key === 'bookmark' || key === 'addbookmark' || key === 'insertbookmark') { const o = arg || {}; return host.addBookmark(o.name != null ? o.name : arg); }
        if (key === 'gotobookmark' || key === 'navigatebookmark') { const o = arg || {}; return host.goToBookmark(o.name != null ? o.name : arg); }
        // R.5.15 outline / TOC
        if (key === 'inserttoc' || key === 'inserttableofcontents') return host.insertTableOfContents();
        if (key === 'gotoheading' || key === 'navigateheading') { const o = arg || {}; return host.goToHeading(o.blockId != null ? o.blockId : arg); }
        // R.5.11 track-changes depth
        if (key === 'setreviewmode' || key === 'reviewmode') { const o = arg || {}; return host.setReviewMode(o.mode != null ? o.mode : arg); }
        if (key === 'acceptrevision') { const o = arg || {}; return host.acceptRevision(o.id != null ? o.id : arg); }
        if (key === 'rejectrevision') { const o = arg || {}; return host.rejectRevision(o.id != null ? o.id : arg); }
        // R.5.12 comments depth
        if (key === 'gotocomment') { const o = arg || {}; return host.goToComment(o.id != null ? o.id : arg); }
        if (key === 'replycomment' || key === 'commentreply') { const o = arg || {}; return host.replyToComment(o.id, o.text, o.author); }
        // R.5.23 context-menu clipboard (menu clicks aren't clipboard events).
        if (key === 'cut' || key === 'menucut') return host.menuCut();
        if (key === 'copy' || key === 'menucopy') return host.menuCopy();
        if (key === 'paste' || key === 'menupaste') return host.menuPaste();
        if (key === 'replacerange') { const o = arg || {}; return host.replaceRange(o.blockId, o.start, o.end, o.text); }
        if (key === 'setspellcheck' || key === 'spellcheck') {
            if (!arg || arg.enabled === false) return host.setSpellChecker(null);
            return host.setSpellChecker(buildWordListChecker(arg));
        }
        if (key === 'resolvecomment') { const o = arg || {}; return host.resolveComment(o.id != null ? o.id : arg); }
        if (key === 'reopencomment') { const o = arg || {}; return host.reopenComment(o.id != null ? o.id : arg); }
        if (key === 'removecomment' || key === 'deletecomment') { const o = arg || {}; return host.removeComment(o.id != null ? o.id : arg); }
        // R.5.13 header/footer (scope-aware)
        if (key === 'setheaderscope') { const o = arg || {}; return host.setHeader(o.content, o.scope); }
        if (key === 'setfooterscope') { const o = arg || {}; return host.setFooter(o.content, o.scope); }
        // R.5.23 view subsystems
        if (key === 'zoom' || key === 'setzoom') { const o = arg || {}; return host.setZoom(o.factor != null ? o.factor : arg); }
        if (key === 'print') return host.print();
        if (key === 'pagesettings' || key === 'setpagesettings') return host.setPageSettings(arg);
        if (key === 'setheader') return host.setHeader(arg);
        if (key === 'setfooter') return host.setFooter(arg);
        // R.4.8 lists
        if (key === 'bulletlist' || key === 'bulletedlist' || key === 'unorderedlist') return host.toggleList('bullet');
        if (key === 'numberedlist' || key === 'orderedlist') return host.toggleList('ordered');
        if (key === 'indent' || key === 'indentlist') return host.indentList();
        if (key === 'outdent' || key === 'outdentlist') return host.outdentList();
        const fn = simple[key];
        return fn ? fn() : false;
    }

    function queryCommand(id) {
        const key = normalizeId(id);
        if (toggleMarks[key]) return host.isMarkActive(key === 'strike' ? 'strikethrough' : key);
        if (key === 'trackchanges') return host.isTrackChanges();
        if (key === 'bulletlist' || key === 'bulletedlist' || key === 'unorderedlist') return host.activeListType() === 'bullet';
        if (key === 'numberedlist' || key === 'orderedlist') return host.activeListType() === 'ordered';
        return false;
    }

    function currentModel() {
        const snap = host.getSnapshot();
        return (snap && snap.model) || opts.model || null;
    }

    return {
        getHost: function () { return host; },
        execCommand: execCommand,
        queryCommand: queryCommand,
        getModel: currentModel,
        setModel: function (model) { host.setModel(model); savedVersion = model ? Number(model.version || 0) : 0; host.render(); },
        isDirty: function () { const m = currentModel(); return !!m && Number(m.version || 0) !== savedVersion; },
        markSaved: function () { const m = currentModel(); savedVersion = m ? Number(m.version || 0) : savedVersion; },
        canUndo: function () { return host.canUndo(); },
        canRedo: function () { return host.canRedo(); },
        getOutline: function () { return host.getOutline(); },
        getComments: function () { return host.getComments(); },
        getContextAt: function (x, y, target) { return host.getContextAt(x, y, target); }, // R.5.23
        setSpellChecker: function (c) { return host.setSpellChecker(c); },                  // R.5.23c
        applyRemoteOperation: function (op) { return host.applyRemoteOperation(op); },      // R.5.22 collab inbound
        getOperationLog: function () { return host.getOperationLog(); },                    // R.5.18 journal
        setRemoteCursors: function (cursors) { return host.setRemoteCursors(cursors); },    // R.5.22 presence
        // R.5.22 — connect this editor to a relay sequencer (e.g. the SignalR collaboration hub).
        // `send(msg)` ships a local change ({ ops, base, clientId }) to the relay; feed inbound
        // server changes back via the returned `receiveServerChange`. Local edits auto-broadcast.
        connectCollab: function (collabOptions) {
            const co = collabOptions || {};
            const client = createRelayCollabClient({
                clientId: co.clientId,
                send: co.send,
                applyRemote: function (op) { host.applyRemoteOperation(op); },
            });
            host.addOperationListener(function (op) { client.localOperation(op); });
            return {
                receiveServerChange: client.receiveServerChange,
                getState: client.getState,
                clientId: client.clientId,
            };
        },
        getBookmarks: function () { return host.listBookmarks(); }, // R.5.5
        getRevisions: function () { return host.getRevisions(); },   // R.5.11
        getReviewMode: function () { return host.getReviewMode(); }, // R.5.11
        setZoom: function (f) { return host.setZoom(f); },           // R.5.23
        getZoom: function () { return host.getZoom(); },
        setPageSettings: function (s) { return host.setPageSettings(s); },
        print: function () { return host.print(); },
        getFormattingState: function () { return host.getFormattingState(); },
        // R.4.8 image inspector — selected-object snapshot + alt/wrap edits
        getSelectedObjectInfo: function () { return host.getSelectedObjectInfo(); },
        setObjectAltText: function (text) { return host.setSelectedObjectAltText(text); },
        setObjectWrapMode: function (mode) { return host.setSelectedObjectWrapMode(mode); },
        setObjectSize: function (width, height) { return host.resizeSelectedObject(width, height); },
        setObjectAlignment: function (align) { return host.setSelectedObjectAlignment(align); },
        setObjectCaption: function (text) { return host.setSelectedObjectCaption(text); },
        setObjectPosition: function (x, y) { return host.setSelectedObjectPosition(x, y); },
        bringObjectForward: function () { return host.bringSelectedObjectForward(); },
        sendObjectBackward: function () { return host.sendSelectedObjectBackward(); },
        getParagraphStyle: function () { return host.getParagraphStyle(); },
        focus: function () { return host.focusInput(); },
        render: function () { return host.render(); },
        destroy: function () { return host.destroy(); },
    };
}
