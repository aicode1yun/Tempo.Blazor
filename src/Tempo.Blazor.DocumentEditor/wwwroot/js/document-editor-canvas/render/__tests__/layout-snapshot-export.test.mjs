import assert from 'node:assert/strict';
import test from 'node:test';
import { buildLayoutSnapshotExport, translateDisplayListToLayoutSnapshot } from '../layout-snapshot-export.mjs';

const PNG_DATA_URI = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

test('layout snapshot export mirrors page geometry and is deterministic', () => {
    const first = buildLayoutSnapshotExport(createRenderModel(), createLayout(), { fontMetrics: createDeterministicMetrics() });
    const second = buildLayoutSnapshotExport(createRenderModel(), createLayout(), { fontMetrics: createDeterministicMetrics() });

    assert.equal(JSON.stringify(first), JSON.stringify(second), 'export must be deterministic');
    assert.equal(first.schemaVersion, 1);
    assert.equal(first.pageCount, 1);
    assert.equal(first.pages.length, 1);
    assert.equal(first.pages[0].index, 0);
    assert.equal(first.pages[0].width, 794);
    assert.equal(first.pages[0].height, 1123);
    assert.ok(first.pages[0].commands.length > 0, 'page must carry print commands');
});

test('text runs become text commands with typography and positions', () => {
    const snapshot = buildLayoutSnapshotExport(createRenderModel(), createLayout(), { fontMetrics: createDeterministicMetrics() });
    const commands = snapshot.pages[0].commands;

    const bold = commands.find(command => command.id === 'bold-run');
    assert.ok(bold, 'bold text run must be exported');
    assert.equal(bold.type, 'text');
    assert.equal(bold.text, 'Bold');
    assert.equal(bold.fontWeight, '700');
    assert.ok(Number.isFinite(bold.x) && bold.x > 0);
    assert.ok(Number.isFinite(bold.baseline) && bold.baseline > 0);
    assert.ok(Number.isFinite(bold.fontSize) && bold.fontSize > 0);
    assert.ok(bold.fontFamily.length > 0, 'font family must be carried');

    const decorated = commands.find(command => command.id === 'decorated-run');
    assert.equal(decorated.underline, true);
    assert.equal(decorated.strikeThrough, true);
    assert.equal(decorated.highlight, '#fde68a');
    assert.equal(decorated.fill, '#1d4ed8');

    const field = commands.find(command => command.id === 'field-run-field' || (command.type === 'text' && command.text === '1'));
    assert.ok(field, 'field runs must be exported as text');
});

test('tables become rect commands and screen-only chrome is excluded', () => {
    const snapshot = buildLayoutSnapshotExport(createRenderModel(), createLayout(), { fontMetrics: createDeterministicMetrics() });
    const commands = snapshot.pages[0].commands;

    assert.ok(commands.some(command => command.type === 'rect' && command.sourceType === 'tableBox'), 'table box must be exported as rect');

    for (const excluded of ['marginGuide', 'bodyArea', 'pageFill', 'pageBorder', 'paragraphBox', 'commentAnchor', 'revisionAnchor', 'diagnosticOverlay']) {
        assert.ok(!commands.some(command => command.sourceType === excluded), `${excluded} is screen chrome and must not print`);
    }
});

test('images with a source become image commands; sourceless images fall back to a bordered rect', () => {
    const model = createRenderModel();
    model.body.blocks.push({
        id: 'image-2',
        type: 'image',
        order: 5,
        content: {
            type: 'image',
            image: { url: PNG_DATA_URI, size: { width: 120, height: 60 } },
        },
    });

    const snapshot = buildLayoutSnapshotExport(model, createLayout(), { fontMetrics: createDeterministicMetrics() });
    const commands = snapshot.pages.flatMap(page => page.commands);

    const image = commands.find(command => command.type === 'image');
    assert.ok(image, 'data-URI image must be exported as image command');
    assert.equal(image.source, PNG_DATA_URI);
    assert.ok(image.width > 0 && image.height > 0);

    assert.ok(
        commands.some(command => command.type === 'rect' && command.sourceType === 'imageObject'),
        'the fixture image without a source must fall back to a visible rect');
});

test('multi-page documents export one page entry per laid-out page', () => {
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

    const snapshot = buildLayoutSnapshotExport(model, createLayout(), { fontMetrics: createDeterministicMetrics() });

    assert.ok(snapshot.pageCount > 1, 'filler content must paginate');
    assert.equal(snapshot.pages.length, snapshot.pageCount);
    assert.ok(snapshot.pages.every(page => page.width === 794 && page.height === 1123));
    assert.ok(snapshot.pages[1].commands.some(command => command.type === 'text'), 'second page must carry text');
});

test('text commands without an explicit font size derive it from the line height, not a fixed default', () => {
    const snapshot = translateDisplayListToLayoutSnapshot({
        pages: [{ index: 0, width: 794, height: 1123 }],
        commands: [
            {
                id: 'caption-line-2',
                type: 'imageCaption',
                pageIndex: 0,
                x: 100,
                baseline: 200,
                width: 120,
                height: 14,
                text: 'save and reload',
                style: { color: '#475569', fontStyle: 'italic' },
            },
        ],
    });

    const caption = snapshot.pages[0].commands.find(command => command.id === 'caption-line-2');
    assert.ok(caption, 'caption line must be exported');
    assert.ok(
        caption.fontSize > 10 && caption.fontSize < 12.5,
        `font size must derive from the 14px line height (~11.2px), got ${caption.fontSize}`);
});

// ── Watermark print translation ──────────────────────────────────────────────────────────────────

test('watermarkText prints centered under rotation with opacity-combined color and no stretch', () => {
    const snapshot = translateDisplayListToLayoutSnapshot({
        pages: [{ index: 0, width: 794, height: 1123 }],
        commands: [{
            id: 'page-0-watermark',
            type: 'watermarkText',
            pageIndex: 0,
            text: 'CONFIDENTIAL',
            x: 397,
            y: 561.5,
            width: 619,
            height: 96,
            baseline: 0,
            rotation: -32,
            opacity: 0.5,
            style: { fontFamily: 'Aptos, Arial, sans-serif', fontSize: 84, fontWeight: 700, textColor: '#475569', textAlign: 'center' },
        }],
    });

    const watermark = snapshot.pages[0].commands.find(command => command.sourceType === 'watermarkText');
    assert.ok(watermark, 'watermark must print');
    assert.equal(watermark.type, 'text');
    assert.equal(watermark.text, 'CONFIDENTIAL');
    assert.equal(watermark.rotation, -32);
    assert.ok(!watermark.width, 'no width → the PDF backend must not stretch the glyphs');
    // #475569 at opacity 0.5 → rgba with combined alpha.
    assert.equal(watermark.fill, 'rgba(71, 85, 105, 0.5)');
    // Centered under rotation: the left-baseline start point sits left of and below the page centre
    // for a negative rotation (x < cx, baseline > cy).
    assert.ok(watermark.x < 397, `start x must sit left of centre (got ${watermark.x})`);
    assert.ok(watermark.baseline > 561.5, `baseline must sit below centre for −32° (got ${watermark.baseline})`);
    assert.equal(watermark.fontSize, 84);
    assert.equal(watermark.fontWeight, '700');
});

test('watermarkText combines rgba colors with the command opacity', () => {
    const snapshot = translateDisplayListToLayoutSnapshot({
        pages: [{ index: 0, width: 794, height: 1123 }],
        commands: [{
            id: 'page-0-watermark',
            type: 'watermarkText',
            pageIndex: 0,
            text: 'E12',
            x: 397,
            y: 561.5,
            width: 619,
            height: 96,
            rotation: -32,
            opacity: 0.18,
            style: { fontSize: 84, textColor: 'rgba(37, 99, 235, 0.46)' },
        }],
    });

    const watermark = snapshot.pages[0].commands.find(command => command.sourceType === 'watermarkText');
    // 0.46 × 0.18 ≈ 0.083 — both alphas multiply.
    const alpha = Number(watermark.fill.match(/rgba\(37, 99, 235, ([0-9.]+)\)/)[1]);
    assert.ok(Math.abs(alpha - 0.0828) < 0.001, `combined alpha must be ≈0.0828 (got ${watermark.fill})`);
});

test('watermarkImage prints as image via its imageUrl', () => {
    const snapshot = translateDisplayListToLayoutSnapshot({
        pages: [{ index: 0, width: 794, height: 1123 }],
        commands: [{
            id: 'page-0-watermark-image',
            type: 'watermarkImage',
            pageIndex: 0,
            imageUrl: 'data:image/png;base64,AAA=',
            x: 200,
            y: 300,
            width: 300,
            height: 200,
            opacity: 0.2,
        }],
    });

    const image = snapshot.pages[0].commands.find(command => command.type === 'image');
    assert.ok(image, 'image watermark must print');
    assert.equal(image.source, 'data:image/png;base64,AAA=');
});

// ── Redline (tracked changes) print styling ──────────────────────────────────────────────────────

function createRedlineDisplayList() {
    return {
        pages: [{ index: 0, width: 794, height: 1123 }],
        commands: [
            textRunCommand('run-del', 'r1', 'smazaný text', 96, 120),
            { id: 'run-del-revision', type: 'revisionAnchor', pageIndex: 0, runId: 'r1', revisionId: 'rev-del', x: 96, width: 90, height: 16 },
            textRunCommand('run-ins', 'r2', 'vložený text', 200, 120),
            { id: 'run-ins-revision', type: 'revisionAnchor', pageIndex: 0, runId: 'r2', revisionId: 'rev-ins', x: 200, width: 90, height: 16 },
            textRunCommand('run-plain', 'r3', 'beze změny', 300, 160),
        ],
    };
}

function textRunCommand(id, runId, text, x, baseline) {
    return {
        id,
        type: 'textRun',
        pageIndex: 0,
        runId,
        text,
        x,
        baseline,
        width: 90,
        height: 16,
        style: { fontFamily: 'Arial', fontSize: 12, color: '#111827' },
    };
}

const REDLINE_OPTIONS = {
    reviewDisplayMode: 'allMarkup',
    revisions: [
        { id: 'rev-del', type: 1, author: { displayName: 'Jana' } },
        { id: 'rev-ins', type: 'insertion', author: { displayName: 'Jana' } },
    ],
};

test('revision-marked runs print as redline: deletions struck through red, insertions underlined blue', () => {
    const snapshot = translateDisplayListToLayoutSnapshot(createRedlineDisplayList(), REDLINE_OPTIONS);
    const commands = snapshot.pages[0].commands;

    const deleted = commands.find(command => command.id === 'run-del');
    assert.equal(deleted.strikeThrough, true, 'deletion must be struck through');
    assert.equal(deleted.fill, '#dc2626', 'deletion prints red');

    const inserted = commands.find(command => command.id === 'run-ins');
    assert.equal(inserted.underline, true, 'insertion must be underlined');
    assert.equal(inserted.fill, '#1d4ed8', 'insertion prints blue');

    const plain = commands.find(command => command.id === 'run-plain');
    assert.ok(!plain.underline && !plain.strikeThrough, 'unmarked text keeps its style');
    assert.equal(plain.fill, '#111827');
});

test('redline pages carry margin change bars and author notes for each revision', () => {
    const snapshot = translateDisplayListToLayoutSnapshotWithLayout();

    function translateDisplayListToLayoutSnapshotWithLayout() {
        return translateDisplayListToLayoutSnapshot(createRedlineDisplayList(), REDLINE_OPTIONS);
    }

    const commands = snapshot.pages[0].commands;
    const bars = commands.filter(command => command.sourceType === 'revisionBar');
    assert.equal(bars.length, 2, 'one change bar per revised run');
    assert.ok(bars.every(bar => bar.type === 'line' && bar.x < 96), 'bars sit in the left margin');
    assert.ok(bars.some(bar => bar.stroke === '#dc2626') && bars.some(bar => bar.stroke === '#1d4ed8'));

    const notes = commands.filter(command => command.sourceType === 'revisionNote');
    assert.equal(notes.length, 2, 'one margin note per revision');
    assert.ok(notes.some(note => note.text === '− Jana'), 'deletion note carries minus + author');
    assert.ok(notes.some(note => note.text === '+ Jana'), 'insertion note carries plus + author');
    assert.ok(notes.every(note => note.x > 700), 'notes sit in the right margin');
});

test('redline styling is applied only in markup review modes', () => {
    const snapshot = translateDisplayListToLayoutSnapshot(createRedlineDisplayList(), {
        ...REDLINE_OPTIONS,
        reviewDisplayMode: 'noMarkup',
    });
    const commands = snapshot.pages[0].commands;

    const deleted = commands.find(command => command.id === 'run-del');
    assert.ok(!deleted.strikeThrough, 'noMarkup prints final text without redline decoration');
    assert.equal(commands.filter(command => command.sourceType === 'revisionBar').length, 0);
});

function createLayout() {
    return {
        pages: [
            {
                index: 0,
                width: 794,
                height: 1123,
                body: { x: 96, y: 96, width: 602, height: 931 },
            },
        ],
    };
}

function createRenderModel() {
    return {
        documentId: 'layout-snapshot-export',
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
                        runs: [{ id: 'heading-run', type: 'text', text: 'Snapshot export', marks: [] }],
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
                            { id: 'italic-run', type: 'text', text: ' italic', marks: [{ type: 'italic' }] },
                            {
                                id: 'decorated-run',
                                type: 'text',
                                text: ' decorated',
                                marks: [
                                    { type: 'underline' },
                                    { type: 'strikethrough' },
                                    { type: 'highlight', value: '#fde68a' },
                                    { type: 'textColor', value: '#1d4ed8' },
                                ],
                            },
                            { id: 'field-run', type: 'field', field: { displayText: '1' }, marks: [] },
                        ],
                    },
                },
                {
                    id: 'table-1',
                    type: 'table',
                    order: 3,
                    content: {
                        type: 'table',
                        table: { rows: [{ cells: [] }, { cells: [] }] },
                    },
                },
                {
                    id: 'image-1',
                    type: 'image',
                    order: 4,
                    content: {
                        type: 'image',
                        image: { size: { width: 180, height: 96 } },
                    },
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

// ── Redaction (real content removal in the print snapshot) ──────────────────────────────────────

import { collectRedactedRunIds } from '../layout-snapshot-export.mjs';

test('collectRedactedRunIds finds redaction-marked runs in body, tables and content controls', () => {
    const model = {
        body: {
            blocks: [
                {
                    id: 'p1',
                    type: 'paragraph',
                    content: {
                        runs: [
                            { id: 'r-plain', text: 'viditelný' },
                            { id: 'r-secret', text: 'tajný', marks: [{ type: 'redaction' }] },
                        ],
                    },
                },
                {
                    id: 't1',
                    type: 'table',
                    content: {
                        table: {
                            rows: [{
                                cells: [{
                                    blocks: [{
                                        id: 'cell-p',
                                        type: 'paragraph',
                                        content: { runs: [{ id: 'r-cell-secret', text: 'buňka', marks: [{ type: 'Redaction' }] }] },
                                    }],
                                }],
                            }],
                        },
                    },
                },
            ],
        },
    };

    const ids = collectRedactedRunIds(model);
    assert.deepEqual([...ids].sort(), ['r-cell-secret', 'r-secret']);
});

test('redacted runs print as block characters — the original text never reaches the snapshot', () => {
    const displayList = {
        pages: [{ index: 0, width: 794, height: 1123 }],
        commands: [
            textRunCommand('run-open', 'r1', 'Číslo účtu: ', 96, 120),
            textRunCommand('run-secret', 'r2', '123456789/0100', 200, 120),
            textRunCommand('run-close', 'r3', ' je tajné.', 330, 120),
        ],
    };

    const snapshot = translateDisplayListToLayoutSnapshot(displayList, {
        redactedRunIds: ['r2'],
    });

    const commands = snapshot.pages[0].commands;
    const secret = commands.find(command => command.id === 'run-secret');
    assert.equal(secret.text, '█'.repeat('123456789/0100'.length));
    assert.equal(JSON.stringify(snapshot).includes('123456789'), false, 'original characters must not exist anywhere in the snapshot');
    assert.equal(commands.find(command => command.id === 'run-open').text, 'Číslo účtu: ');
});
