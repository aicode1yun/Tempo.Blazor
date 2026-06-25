// Phase R.5.22 — core-engine/collab-client.mjs
// Client-side OT control for realtime collaboration (ot.js / Google-Wave "Jupiter" style).
// The SERVER is the sequencer + relay (it assigns each change a monotonic revision and is the
// single source of truth for order). Each client keeps at most ONE change "in flight"
// (`outstanding`) plus a local `buffer` of edits made while waiting for the ack — this
// one-at-a-time discipline is what keeps the transform sound without composing ops.
//
// A "change" on the wire is an ARRAY of text operations (see operations.mjs). The control:
//   • local edit  → if nothing outstanding, send it; else queue into buffer.
//   • server ack  → outstanding acknowledged; promote buffer to outstanding and send it.
//   • server op   → transform it past outstanding+buffer, APPLY it locally, rebase the buffers.
//
//   createCollabClient({ host, send, applyRemote?, revision?, clientId? }) → {
//     localOperation(op)         // engine emitted a local op (host already applied it)
//     receiveServerChange(msg)   // { ops, revision, clientId } broadcast from the server
//     receiveAck(revision)       // server acknowledged our outstanding change
//     getState()                 // { revision, outstanding, buffer, clientId }
//   }
//
// Convergence: with the server enforcing a total order and every client running this control,
// all replicas reach identical text (TP1 of operations.mjs.transformOperation, exercised by a
// full multi-client simulation in PhaseR15).

import { transformOperation } from './operations.mjs';

function asList(v) { return Array.isArray(v) ? v.slice() : (v == null ? [] : [v]); }

// Transform a remote change (op list) past a local change (op list). Returns both rebased:
//   remote' valid AFTER local applied, local' valid AFTER remote' applied.
// `serverWins` decides offset ties (the server-ordered change has priority).
export function transformChange(remoteOps, localOps, serverWins) {
    let remote = asList(remoteOps);
    let local = asList(localOps);
    const remotePriority = serverWins ? 'right' : 'left';
    const localPriority = serverWins ? 'left' : 'right';
    const newRemote = [];
    // Fold each remote op past the whole local list, accumulating the rebased local list.
    remote.forEach(function (rOp) {
        let curRemote = [rOp];
        const nextLocal = [];
        local.forEach(function (lOp) {
            // transform this (possibly split) remote fragment past lOp, and lOp past it.
            const grownRemote = [];
            let lFrags = [lOp];
            curRemote.forEach(function (rf) {
                transformOperation(rf, lOp, remotePriority).forEach(function (x) { grownRemote.push(x); });
            });
            // rebase lOp past ALL current remote fragments (sequentially).
            curRemote.forEach(function (rf) {
                const grown = [];
                lFrags.forEach(function (lf) { transformOperation(lf, rf, localPriority).forEach(function (x) { grown.push(x); }); });
                lFrags = grown;
            });
            curRemote = grownRemote;
            lFrags.forEach(function (lf) { nextLocal.push(lf); });
        });
        curRemote.forEach(function (x) { newRemote.push(x); });
        local = nextLocal;
    });
    return { remote: newRemote, local: local };
}

// R.5.22 — client for a PURE-RELAY sequencer (the existing DocumentEditorCollaborationHub:
// it assigns each change a total-order `sequence` and relays, but does NOT transform). Each
// change on the wire carries `base` (= the sender's last-applied sequence). The client brings
// an incoming change "to head" by transforming it past the committed ops the sender hadn't
// seen (committed[base+1 .. sequence-1]), then past its own un-acked ops. Convergence relies on
// the total order + a transform that holds TP1/TP2 for these text ops (verified by PhaseR16).
export function createRelayCollabClient(options) {
    const opts = options || {};
    const host = opts.host || null;
    const send = typeof opts.send === 'function' ? opts.send : function () {};
    const applyRemote = typeof opts.applyRemote === 'function'
        ? opts.applyRemote
        : function (op) { if (host && typeof host.applyRemoteOperation === 'function') host.applyRemoteOperation(op); };
    const clientId = opts.clientId || ('c-' + Math.random().toString(36).slice(2, 8));

    let serverSeq = Number(opts.revision) || 0;
    const committed = {};            // sequence → head-relative committed op list
    let outstanding = null;          // { ops, base } sent, awaiting its relay-back (ack)
    let buffer = [];                 // local ops made while outstanding

    function toHead(opsIn, base, sequence) {
        let inc = opsIn;
        for (let s = base + 1; s < sequence; s++) {
            // committed[s] is earlier in the total order → the incoming op YIELDS to it (serverWins).
            if (committed[s]) inc = transformChange(inc, committed[s], true).remote;
        }
        return inc;
    }
    function sendOutstanding() { if (outstanding && outstanding.ops.length) send({ kind: 'op', ops: outstanding.ops, base: outstanding.base, clientId: clientId }); }

    function localOperation(op) {
        if (!op) return;
        if (outstanding === null) { outstanding = { ops: [op], base: serverSeq }; sendOutstanding(); }
        else buffer.push(op);
    }
    function receiveServerChange(msg) {
        const m = msg || {};
        const seq = Number(m.sequence) || (serverSeq + 1);
        const headForm = toHead(asList(m.ops), Number(m.base) || 0, seq); // canonical, client-independent
        committed[seq] = headForm;
        serverSeq = seq;
        if (m.clientId === clientId) {
            // our outstanding committed → promote buffer; nothing to apply (already applied at base).
            if (buffer.length) { outstanding = { ops: buffer, base: serverSeq }; buffer = []; sendOutstanding(); }
            else outstanding = null;
            return;
        }
        // remote: rebase the head form past our un-acked ops (which commit later → they yield).
        let inc = headForm;
        if (outstanding && outstanding.ops.length) { const t = transformChange(inc, outstanding.ops, false); inc = t.remote; outstanding.ops = t.local; }
        if (buffer.length) { const t = transformChange(inc, buffer, false); inc = t.remote; buffer = t.local; }
        inc.forEach(function (op) { applyRemote(op); });
    }
    return {
        localOperation: localOperation,
        receiveServerChange: receiveServerChange,
        getState: function () { return { serverSeq: serverSeq, outstanding: outstanding, buffer: buffer.slice(), clientId: clientId }; },
        clientId: clientId,
    };
}

export function createCollabClient(options) {
    const opts = options || {};
    const host = opts.host || null;
    const send = typeof opts.send === 'function' ? opts.send : function () {};
    const applyRemote = typeof opts.applyRemote === 'function'
        ? opts.applyRemote
        : function (op) { if (host && typeof host.applyRemoteOperation === 'function') host.applyRemoteOperation(op); };

    let revision = Number(opts.revision) || 0; // last server revision we have applied/acked
    let outstanding = null;                     // op list sent, awaiting ack (or null)
    let buffer = [];                            // local ops made while outstanding
    const clientId = opts.clientId || ('c-' + Math.random().toString(36).slice(2, 8));

    function sendOutstanding() {
        if (outstanding && outstanding.length) {
            send({ kind: 'op', ops: outstanding, revision: revision, clientId: clientId });
        }
    }

    function localOperation(op) {
        if (!op) return;
        if (outstanding === null) {
            outstanding = [op];
            sendOutstanding();
        } else {
            buffer.push(op);
        }
    }

    function receiveAck(serverRevision) {
        revision = Number(serverRevision) || (revision + 1);
        if (buffer.length) {
            outstanding = buffer;
            buffer = [];
            sendOutstanding();
        } else {
            outstanding = null;
        }
    }

    function receiveServerChange(msg) {
        const m = msg || {};
        if (m.clientId === clientId) { receiveAck(m.revision); return; } // our own change came back = ack
        let remote = asList(m.ops);
        // Rebase the incoming change past our outstanding, then past our buffer; apply the result.
        // The server-committed remote op is ordered BEFORE our un-acked ops (the server commits
        // ours later, transformed to yield), so remote WINS offset ties here (serverWins=false →
        // remotePriority 'left' / our ops 'right'), keeping client + server consistent.
        if (outstanding && outstanding.length) {
            const t = transformChange(remote, outstanding, false);
            remote = t.remote;
            outstanding = t.local;
        }
        if (buffer.length) {
            const t = transformChange(remote, buffer, false);
            remote = t.remote;
            buffer = t.local;
        }
        remote.forEach(function (op) { applyRemote(op); });
        revision = Number(m.revision) || (revision + 1);
    }

    return {
        localOperation: localOperation,
        receiveServerChange: receiveServerChange,
        receiveAck: receiveAck,
        getState: function () { return { revision: revision, outstanding: outstanding ? outstanding.slice() : null, buffer: buffer.slice(), clientId: clientId }; },
        clientId: clientId,
    };
}
