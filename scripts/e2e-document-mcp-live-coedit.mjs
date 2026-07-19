// E2E: live co-editing bridge (plan 3 Fáze 7).
//
// A "human" opens TmDocumentEditor (HTTPS WASM host) on contract-demo while the MCP agent edits
// the same document through the demo API /mcp endpoint. The agent's edit must appear in the open
// editor WITHOUT a reload (SignalR forwarder), proven by the canvas a11y mirror + screenshots.
// Edge case: an MCP edit on a document nobody watches still saves fine (collaboration is
// fail-open, publish just has no listeners).
// Run with the HTTPS demo Api (:5100) and WASM host (:7106) running:
//   node scripts/e2e-document-mcp-live-coedit.mjs
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

import { chromium } from 'playwright-core';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const MCP_URL = process.env.TEMPO_MCP_URL || 'https://localhost:5100/mcp';
const EDITOR_URL = process.env.TEMPO_EDITOR_URL || 'https://localhost:7106/document-editor?documentId=contract-demo';
const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const shotsDir = path.join(repoRoot, 'tests', 'Tempo.Blazor.E2E', '__screenshots__', 'document-mcp-live-coedit');
mkdirSync(shotsDir, { recursive: true });

const assert = (condition, message) => {
    if (!condition) {
        throw new Error(`ASSERT: ${message}`);
    }
};

// ── Minimal MCP client ──────────────────────────────────────────────────────────────────────
let nextId = 1;
let sessionId = null;

async function rpc(method, params) {
    const headers = { 'content-type': 'application/json', accept: 'application/json, text/event-stream' };
    if (sessionId) {
        headers['mcp-session-id'] = sessionId;
    }

    const response = await fetch(MCP_URL, {
        method: 'POST', headers, body: JSON.stringify({ jsonrpc: '2.0', id: nextId++, method, params }),
    });
    sessionId = response.headers.get('mcp-session-id') || sessionId;
    const text = await response.text();
    const contentType = response.headers.get('content-type') || '';
    const payload = contentType.includes('text/event-stream')
        ? JSON.parse(text.split('\n').filter(line => line.startsWith('data:')).pop().slice(5))
        : JSON.parse(text);
    if (payload.error) {
        throw new Error(`${method}: ${JSON.stringify(payload.error)}`);
    }
    return payload.result;
}

async function callTool(name, args) {
    const result = await rpc('tools/call', { name, arguments: args });
    const parsed = JSON.parse(result.content.find(block => block.type === 'text').text);
    if (parsed.success !== true) {
        throw new Error(`${name}: ${JSON.stringify(parsed).slice(0, 500)}`);
    }
    return parsed;
}

await rpc('initialize', {
    protocolVersion: '2025-06-18',
    capabilities: {},
    clientInfo: { name: 'tempo-e2e-live-coedit', version: '1.0.0' },
});
await fetch(MCP_URL, {
    method: 'POST',
    headers: { 'content-type': 'application/json', accept: 'application/json, text/event-stream', 'mcp-session-id': sessionId },
    body: JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized', params: {} }),
});
console.log(`MCP session ${sessionId} ready`);

// ── Edge case first: edit a document nobody watches — must save without any listener ────────
const lonely = await callTool('document_editor_create', { title: 'Nikdo se nedívá' });
const lonelyEdit = await callTool('document_editor_insert_text', {
    documentId: lonely.id, blockId: lonely.firstBlockId, offset: 0, text: 'Uloženo bez posluchačů.',
});
assert(lonelyEdit.concurrencyToken, 'edit without watchers must save');
console.log(`edge case OK: unwatched edit saved (collaborationPublished=${lonelyEdit.collaborationPublished})`);

// ── Human opens the editor ──────────────────────────────────────────────────────────────────
const browser = await chromium.launch({ args: ['--ignore-certificate-errors'] });
try {
    const page = await browser.newPage({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 900 } });
    await page.goto(EDITOR_URL, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.tm-document-editor', { timeout: 120000 });
    // Let the WASM runtime + collaboration join settle.
    await page.waitForTimeout(8000);
    await page.screenshot({ path: path.join(shotsDir, 'editor-before-agent-edit.png'), fullPage: false });
    console.log('editor open, before-screenshot taken');

    // ── Agent edits the same document over MCP ──────────────────────────────────────────────
    const marker = `Živý MCP edit ${Date.now() % 100000}`;
    const edit = await callTool('document_editor_insert_block', {
        documentId: 'contract-demo', blockType: 'paragraph', text: marker, order: -1,
    });
    assert(edit.collaborationPublished === true, 'live edit must report collaborationPublished=true');
    console.log(`agent inserted block ${edit.blockId} ("${marker}")`);

    // ── The open editor must show the edit WITHOUT reload ───────────────────────────────────
    await page.waitForFunction(
        text => document.body.innerText.includes(text) || document.body.textContent.includes(text),
        marker,
        { timeout: 30000 });
    await page.screenshot({ path: path.join(shotsDir, 'editor-after-agent-edit.png'), fullPage: false });
    console.log('live edit visible in the open editor, after-screenshot taken');
} finally {
    await browser.close();
}

console.log('E2E live co-edit PASSED');
