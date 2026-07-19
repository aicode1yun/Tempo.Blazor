// E2E: agent loop edit → preview over the demo API MCP endpoint (plan 3 Fáze 4).
//
// Drives https://localhost:5100/mcp (streamable HTTP) end-to-end:
//   create → semantic edits → document_render_preview (PNG screenshots) → more edits →
//   preview again → document_render_pdf; plus the empty-document edge case.
// PNG previews land in tests/Tempo.Blazor.E2E/__screenshots__/document-mcp-preview/ for
// UX review. Run with the HTTPS demo Api running:
//   node scripts/e2e-document-mcp-preview.mjs
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const MCP_URL = process.env.TEMPO_MCP_URL || 'https://localhost:5100/mcp';
const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const shotsDir = path.join(repoRoot, 'tests', 'Tempo.Blazor.E2E', '__screenshots__', 'document-mcp-preview');
mkdirSync(shotsDir, { recursive: true });

let nextId = 1;
let sessionId = null;

async function rpc(method, params) {
    const headers = {
        'content-type': 'application/json',
        accept: 'application/json, text/event-stream',
    };
    if (sessionId) {
        headers['mcp-session-id'] = sessionId;
    }

    const response = await fetch(MCP_URL, {
        method: 'POST',
        headers,
        body: JSON.stringify({ jsonrpc: '2.0', id: nextId++, method, params }),
    });

    sessionId = response.headers.get('mcp-session-id') || sessionId;
    if (!response.ok) {
        throw new Error(`${method}: HTTP ${response.status} ${await response.text()}`);
    }

    const contentType = response.headers.get('content-type') || '';
    const text = await response.text();
    let payload;
    if (contentType.includes('text/event-stream')) {
        const dataLine = text.split('\n').filter(line => line.startsWith('data:')).pop();
        payload = JSON.parse(dataLine.slice('data:'.length));
    } else {
        payload = JSON.parse(text);
    }

    if (payload.error) {
        throw new Error(`${method}: ${JSON.stringify(payload.error)}`);
    }

    return payload.result;
}

async function notify(method, params) {
    await fetch(MCP_URL, {
        method: 'POST',
        headers: {
            'content-type': 'application/json',
            accept: 'application/json, text/event-stream',
            'mcp-session-id': sessionId,
        },
        body: JSON.stringify({ jsonrpc: '2.0', method, params }),
    });
}

async function callTool(name, args) {
    const result = await rpc('tools/call', { name, arguments: args });
    const text = result?.content?.find(block => block.type === 'text')?.text;
    if (!text) {
        throw new Error(`${name}: no text content in result ${JSON.stringify(result).slice(0, 400)}`);
    }

    const parsed = JSON.parse(text);
    if (parsed.success !== true) {
        throw new Error(`${name}: tool failure ${text.slice(0, 600)}`);
    }

    return parsed;
}

function savePreviewPages(prefix, preview) {
    const saved = [];
    for (const page of preview.renderedPages) {
        const file = path.join(shotsDir, `${prefix}-page-${page.pageNumber}.png`);
        writeFileSync(file, Buffer.from(page.base64, 'base64'));
        saved.push(`${path.basename(file)} (${page.width}x${page.height})`);
    }
    return saved;
}

const assert = (condition, message) => {
    if (!condition) {
        throw new Error(`ASSERT: ${message}`);
    }
};

// ── MCP handshake ────────────────────────────────────────────────────────────────────────────
await rpc('initialize', {
    protocolVersion: '2025-06-18',
    capabilities: {},
    clientInfo: { name: 'tempo-e2e-document-preview', version: '1.0.0' },
});
await notify('notifications/initialized', {});
console.log(`MCP session ${sessionId} initialized against ${MCP_URL}`);

// ── Agent loop: create → edit → preview ─────────────────────────────────────────────────────
const created = await callTool('document_editor_create', {
    title: 'Nájemní smlouva — MCP E2E',
});
const docId = created.id;
let token = created.concurrencyToken;
console.log(`created document ${docId} (token ${token}, firstBlock ${created.firstBlockId})`);

const heading = await callTool('document_editor_insert_block', {
    documentId: docId, blockType: 'heading', text: 'Nájemní smlouva', order: -1, headingLevel: 1,
    expectedConcurrencyToken: token,
});
token = heading.concurrencyToken;

const intro = await callTool('document_editor_insert_text', {
    documentId: docId, blockId: created.firstBlockId, offset: 0,
    text: 'Pronajímatel a nájemce uzavírají tuto smlouvu o nájmu bytu 2+kk v Praze.',
    expectedConcurrencyToken: token,
});
token = intro.concurrencyToken;

const list1 = await callTool('document_editor_insert_block', {
    documentId: docId, blockType: 'list', text: 'Nájemné: 18 500 Kč měsíčně', ordered: false,
    expectedConcurrencyToken: token,
});
token = list1.concurrencyToken;
const list2 = await callTool('document_editor_insert_block', {
    documentId: docId, blockType: 'list', text: 'Kauce: 37 000 Kč', ordered: false,
    expectedConcurrencyToken: token,
});
token = list2.concurrencyToken;

const preview1 = await callTool('document_render_preview', { documentId: docId, dpi: 144 });
assert(preview1.pageCount >= 1, 'first preview must have at least one page');
assert(preview1.renderedPages[0].base64.length > 1000, 'preview PNG must be non-trivial');
console.log(`preview #1: ${savePreviewPages('edit-loop-1', preview1).join(', ')}`);

// ── Loop iteration 2: bold the rent, add a closing paragraph, preview again ─────────────────
const bold = await callTool('document_editor_format_range', {
    documentId: docId, blockId: list1.blockId, offset: 0, length: 8, mark: 'bold',
    expectedConcurrencyToken: token,
});
token = bold.concurrencyToken;

const closing = await callTool('document_editor_insert_block', {
    documentId: docId, blockType: 'paragraph',
    text: 'Smlouva nabývá účinnosti dnem podpisu obou smluvních stran.',
    expectedConcurrencyToken: token,
});
token = closing.concurrencyToken;

const preview2 = await callTool('document_render_preview', { documentId: docId, dpi: 144 });
assert(preview2.contentDigest !== preview1.contentDigest, 'edits must change the contentDigest');
console.log(`preview #2 (after edits): ${savePreviewPages('edit-loop-2', preview2).join(', ')}`);

// ── Edge case: empty document preview ───────────────────────────────────────────────────────
const emptyDoc = await callTool('document_editor_create', { title: 'Prázdný dokument' });
const emptyPreview = await callTool('document_render_preview', { documentId: emptyDoc.id });
assert(emptyPreview.pageCount === 1, `empty document must render exactly 1 page, got ${emptyPreview.pageCount}`);
console.log(`empty-document preview: ${savePreviewPages('empty-document', emptyPreview).join(', ')}`);

// ── PDF render incl. forensic watermark passthrough ─────────────────────────────────────────
const pdf = await callTool('document_render_pdf', {
    documentId: docId,
    exportOptionsJson: JSON.stringify({
        ForensicWatermark: { UserName: 'mcp-e2e-agent', Timestamp: '2026-07-19T12:00:00+00:00' },
    }),
});
const pdfBytes = Buffer.from(pdf.base64, 'base64');
assert(pdfBytes.subarray(0, 5).toString('ascii') === '%PDF-', 'PDF must start with %PDF-');
assert(String(pdf.forensicTimestamp || '').startsWith('2026-07-19'), 'forensic timestamp must pass through');
writeFileSync(path.join(shotsDir, 'edit-loop-final.pdf'), pdfBytes);
console.log(`pdf: ${pdf.pageCount} page(s), ${pdfBytes.length} bytes, forensic ${pdf.forensicTimestamp}`);

// ── Error-path edge case: unknown font family fails closed ──────────────────────────────────
const badFont = await rpc('tools/call', {
    name: 'document_render_preview',
    arguments: {
        documentJson: JSON.stringify({
            DocumentId: 'e2e-bad-font',
            Theme: { BodyFontFamily: 'Totally Unknown Font' },
            Blocks: [{
                Id: 'p1',
                Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Text: 'Text' }] },
            }],
        }),
    },
});
const badFontPayload = JSON.parse(badFont.content.find(block => block.type === 'text').text);
assert(badFontPayload.success === false, 'unknown font must fail closed');
assert(String(badFontPayload.message).includes('Totally Unknown Font'), 'diagnostics must name the family');
console.log(`fail-closed font diagnostics OK: ${badFontPayload.message.slice(0, 120)}...`);

console.log('E2E document MCP preview loop PASSED');
