// Phase 0 (headless document runtime): tests for the embedded headless layout bundle.
//
// The bundle (built by scripts/build-document-editor.mjs, embedded in
// Tempo.Blazor.DocumentFormats) packages the canvas layout chain —
// buildLayoutSnapshotExport → buildDisplayList → layoutCanvasDocument →
// translateDisplayListToLayoutSnapshot plus the injectable font-metrics service —
// so the server can run the SAME layout code the editor paints with (WYSIWYG
// parity by construction). Three gates live here:
//   1. guard  — the bundle must not touch browser globals (document/window/
//               OffscreenCanvas) outside the font-metrics safe fallback;
//   2. smoke  — the bundle loads in plain Node and lays out a model fixture into
//               a deterministic layout snapshot JSON;
//   3. drift  — the committed artifact must match a fresh esbuild of the sources.
import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync, existsSync } from 'node:fs';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';
import {
    BUNDLE_OUTFILE,
    BUNDLE_ENTRY,
    buildHeadlessLayoutBundleText,
    normalizeLineEndings,
} from './build-document-editor.mjs';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const bundlePath = path.join(repoRoot, BUNDLE_OUTFILE);

test('headless layout bundle artifact exists (npm run build:document-editor)', () => {
    assert.ok(existsSync(path.join(repoRoot, BUNDLE_ENTRY)), `bundle entry must exist: ${BUNDLE_ENTRY}`);
    assert.ok(existsSync(bundlePath), `embedded bundle must exist: ${BUNDLE_OUTFILE} — run npm run build:document-editor`);
});

// ── 1. Guard: no active browser-global references outside the font-metrics fallback ─────────────

// Lines belonging to the font-metrics safe fallback (defaultCreateMeasureContext):
// feature-detected, wrapped in try/catch, and falling back to synthetic metrics.
const SAFE_FALLBACK_PATTERNS = [
    /typeof OffscreenCanvas === ["']function["']/,
    /new OffscreenCanvas\(8, 8\)/,
    /globalThis\.document/,
];

// An "active" reference is the bare global being called, dereferenced or indexed —
// `foo.document`/`documentModel` are not matches (lookbehind excludes `.`, `$`, word chars).
const FORBIDDEN_GLOBALS = [
    { name: 'document', pattern: /(?<![\w$.])document\s*[.\[(]/g },
    { name: 'window', pattern: /(?<![\w$.])window\s*[.\[(]/g },
    { name: 'OffscreenCanvas', pattern: /(?<![\w$.])OffscreenCanvas\b/g },
    { name: 'navigator', pattern: /(?<![\w$.])navigator\s*[.\[(]/g },
    { name: 'requestAnimationFrame', pattern: /(?<![\w$.])requestAnimationFrame\s*\(/g },
    { name: 'HTMLCanvasElement', pattern: /(?<![\w$.])HTMLCanvasElement\b/g },
];

test('bundle guard: no browser globals outside the font-metrics safe fallback', () => {
    const text = readFileSync(bundlePath, 'utf8');
    const lines = text.split('\n');
    const violations = [];

    for (const [index, line] of lines.entries()) {
        for (const { name, pattern } of FORBIDDEN_GLOBALS) {
            pattern.lastIndex = 0;
            if (!pattern.test(line)) {
                continue;
            }

            if (SAFE_FALLBACK_PATTERNS.some(safe => safe.test(line))) {
                continue;
            }

            violations.push(`${name} at line ${index + 1}: ${line.trim()}`);
        }
    }

    assert.deepEqual(violations, [],
        `headless bundle must not reference browser globals outside the font-metrics fallback:\n${violations.join('\n')}`);
});

test('bundle guard: browser-only canvas modules are not bundled', () => {
    const text = readFileSync(bundlePath, 'utf8');
    // interop.mjs and the paint/canvas stack are browser-only and must stay out.
    for (const marker of ['createCanvasDocumentEngine', 'replaceChildren', 'addEventListener', 'ResizeObserver']) {
        assert.equal(text.includes(marker), false, `browser-only marker "${marker}" must not appear in the headless bundle`);
    }
});

// ── 2. Smoke: load the bundle in Node, lay out a fixture, deterministic snapshot ────────────────

const bundle = await import(pathToFileURL(bundlePath).href).catch(() => null);

test('bundle exports the headless layout chain API', () => {
    assert.ok(bundle, 'bundle must be importable in plain Node');
    for (const name of [
        'buildLayoutSnapshotExport',
        'translateDisplayListToLayoutSnapshot',
        'collectRedactedRunIds',
        'buildDisplayList',
        'layoutCanvasDocument',
        'createFontMetricsService',
        'normalizePageSettings',
        'parseFontAdvanceTable',
        'createAdvanceFontMetricsService',
    ]) {
        assert.equal(typeof bundle[name], 'function', `bundle must export ${name}`);
    }
});

test('bundle lays out a model fixture into a deterministic snapshot JSON', () => {
    const first = bundle.buildLayoutSnapshotExport(createRenderModel(), createLayout(), { fontMetrics: createDeterministicMetrics() });
    const second = bundle.buildLayoutSnapshotExport(createRenderModel(), createLayout(), { fontMetrics: createDeterministicMetrics() });

    assert.equal(JSON.stringify(first), JSON.stringify(second), 'snapshot must be byte-deterministic across runs');
    assert.equal(first.schemaVersion, 1);
    assert.equal(first.pageCount, 1);
    assert.equal(first.pages[0].width, 794);
    assert.equal(first.pages[0].height, 1123);

    const texts = first.pages[0].commands.filter(command => command.type === 'text');
    assert.ok(texts.length > 0, 'fixture must produce text commands');
    const bold = texts.find(command => command.id === 'bold-run');
    assert.ok(bold, 'bold run must be laid out');
    assert.equal(bold.fontWeight, '700');
    assert.ok(Number.isFinite(bold.x) && Number.isFinite(bold.baseline) && Number.isFinite(bold.width));
});

test('bundle paginates filler content across pages (line breaker + paginator run headless)', () => {
    const model = createRenderModel();
    for (let index = 0; index < 80; index++) {
        model.body.blocks.push({
            id: `filler-${index}`,
            type: 'paragraph',
            order: 10 + index,
            content: {
                type: 'paragraph',
                runs: [{ id: `filler-run-${index}`, type: 'text', text: `Filler paragraph ${index} with enough words to occupy a line.`, marks: [] }],
            },
        });
    }

    const snapshot = bundle.buildLayoutSnapshotExport(model, createLayout(), { fontMetrics: createDeterministicMetrics() });
    assert.ok(snapshot.pageCount > 1, 'filler content must paginate onto multiple pages');
    assert.equal(snapshot.pages.length, snapshot.pageCount);
    assert.ok(snapshot.pages[1].commands.some(command => command.type === 'text'), 'second page must carry text');
});

test('bundle font metrics fall back to synthetic in Node and stay deterministic', () => {
    const service = bundle.createFontMetricsService();
    assert.equal(service.isUsingRealMetrics(), false, 'Node has no canvas — the safe fallback must engage');

    const a = service.measureRun({ text: 'Rozměření', fontFamily: 'Arial', fontSize: 12 });
    const b = service.measureRun({ text: 'Rozměření', fontFamily: 'Arial', fontSize: 12 });
    assert.deepEqual(a, b, 'synthetic metrics must be deterministic');
    assert.ok(a.width > 0 && a.ascent > 0 && a.descent > 0 && a.lineHeight > 0);
});

// ── 3. Drift: committed artifact matches a fresh build of the sources ───────────────────────────

test('drift check: embedded bundle matches a fresh esbuild of the .mjs sources', async () => {
    const fresh = await buildHeadlessLayoutBundleText();
    const committed = readFileSync(bundlePath, 'utf8');
    assert.equal(normalizeLineEndings(committed), normalizeLineEndings(fresh),
        'embedded headless bundle is stale — run npm run build:document-editor and commit the artifact');
});

// ── fixtures (mirrors render/__tests__/layout-snapshot-export.test.mjs) ─────────────────────────

function createLayout() {
    return {
        pages: [{ index: 0, width: 794, height: 1123, body: { x: 96, y: 96, width: 602, height: 931 } }],
    };
}

function createRenderModel() {
    return {
        documentId: 'headless-bundle-smoke',
        theme: {
            bodyFontFamily: 'Aptos, Arial, sans-serif',
            bodyFontSize: 11,
            bodyLineHeight: 1.15,
            paragraphSpacingAfter: 8,
        },
        body: {
            blocks: [
                {
                    id: 'heading-1',
                    type: 'heading',
                    order: 1,
                    content: {
                        type: 'heading',
                        headingLevel: 1,
                        runs: [{ id: 'heading-run', type: 'text', text: 'Headless snapshot', marks: [] }],
                    },
                },
                {
                    id: 'paragraph-1',
                    type: 'paragraph',
                    order: 2,
                    content: {
                        type: 'paragraph',
                        runs: [
                            { id: 'bold-run', type: 'text', text: 'Bold', marks: [{ type: 'bold' }] },
                            { id: 'plain-run', type: 'text', text: ' plain text runs through the real line breaker.', marks: [] },
                        ],
                    },
                },
                {
                    id: 'table-1',
                    type: 'table',
                    order: 3,
                    content: { type: 'table', table: { rows: [{ cells: [] }, { cells: [] }] } },
                },
            ],
        },
    };
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, text.length * fontSize * 0.55),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
