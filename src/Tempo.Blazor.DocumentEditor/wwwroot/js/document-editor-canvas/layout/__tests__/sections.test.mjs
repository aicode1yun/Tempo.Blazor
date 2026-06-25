import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../../render/display-list.mjs';
import { layoutCanvasDocument } from '../pagination.mjs';

test('section breaks apply next-page, continuous, even, and odd section geometry', () => {
    const layout = layoutCanvasDocument({
        documentId: 'e3-section-breaks',
        pageSettings: pageSettings(420, 520, 48),
        theme: theme(),
        sections: [
            {
                id: 'portrait',
                order: 0,
                pageSettings: pageSettings(420, 520, 48),
                blocks: [
                    paragraph('portrait-intro', 'Portrait section text before a next-page landscape break.', 1, 'portrait'),
                    sectionBreak('break-landscape', 2, 'landscape', 'nextPage', 'portrait'),
                ],
            },
            {
                id: 'landscape',
                order: 1,
                pageSettings: pageSettings(620, 380, 40),
                blocks: [
                    paragraph('landscape-start', 'Landscape section starts on its own wider page.', 3, 'landscape'),
                    sectionBreak('break-continuous', 4, 'continuous', 'continuous', 'landscape'),
                ],
            },
            {
                id: 'continuous',
                order: 2,
                pageSettings: pageSettings(620, 380, 40),
                blocks: [
                    paragraph('continuous-start', 'Continuous section keeps flowing on the current page when geometry is compatible.', 5, 'continuous'),
                    sectionBreak('break-even', 6, 'even', 'evenPage', 'continuous'),
                ],
            },
            {
                id: 'even',
                order: 3,
                pageSettings: pageSettings(500, 520, 44),
                blocks: [
                    paragraph('even-start', 'Even page section starts on an even numbered page.', 7, 'even'),
                    sectionBreak('break-odd', 8, 'odd', 'oddPage', 'even'),
                ],
            },
            {
                id: 'odd',
                order: 4,
                pageSettings: pageSettings(430, 520, 42),
                blocks: [
                    paragraph('odd-start', 'Odd page section starts on an odd numbered page.', 9, 'odd'),
                ],
            },
        ],
    }, { fontMetrics: createDeterministicMetrics() });

    const portrait = fragment(layout, 'portrait-intro');
    const landscape = fragment(layout, 'landscape-start');
    const continuous = fragment(layout, 'continuous-start');
    const even = fragment(layout, 'even-start');
    const odd = fragment(layout, 'odd-start');

    assert.equal(portrait.pageIndex, 0);
    assert.equal(layout.pages[landscape.pageIndex].width, 620);
    assert.equal(layout.pages[landscape.pageIndex].height, 380);
    assert.equal(continuous.pageIndex, landscape.pageIndex);
    assert.equal(even.pageIndex % 2, 1);
    assert.equal(odd.pageIndex % 2, 0);
    assert.equal(layout.pages[odd.pageIndex].sectionId, 'odd');
});

test('line numbering renders with continuous, page, and section restart behavior', () => {
    const layout = layoutCanvasDocument({
        documentId: 'e3-line-numbering',
        pageSettings: pageSettings(380, 190, 36),
        theme: theme(),
        sections: [
            {
                id: 'continuous',
                order: 0,
                pageSettings: pageSettings(380, 190, 36),
                properties: {
                    lineNumbering: { enabled: true, startAt: 3, increment: 2, restart: 'continuous', distanceFromText: 12 },
                },
                blocks: [
                    paragraph('line-a', repeated('Line numbering keeps margin rhythm.', 6), 1, 'continuous'),
                    sectionBreak('break-section', 2, 'section-restart', 'nextPage', 'continuous'),
                ],
            },
            {
                id: 'section-restart',
                order: 1,
                pageSettings: pageSettings(380, 190, 36),
                properties: {
                    lineNumbering: { enabled: true, startAt: 10, increment: 5, restart: 'section', distanceFromText: 12 },
                },
                blocks: [
                    paragraph('line-b', 'The second section restarts its own numbering sequence.', 3, 'section-restart'),
                ],
            },
        ],
    }, { fontMetrics: createDeterministicMetrics() });

    assert.ok(layout.lineNumbers.length >= 4);
    assert.equal(layout.lineNumbers[0].text, '3');
    assert.equal(layout.lineNumbers[1].text, '5');
    const sectionNumber = layout.lineNumbers.find(item => item.sectionId === 'section-restart');
    assert.equal(sectionNumber?.text, '10');
    assert.ok(layout.lineNumbers.every(item => item.x < layout.pages[item.pageIndex].body.x));
});

test('display list exposes column separators and line number commands', () => {
    const display = buildDisplayList({
        documentId: 'e3-display-commands',
        pageSettings: pageSettings(500, 360, 44),
        theme: theme(),
        sections: [
            {
                id: 'columns',
                order: 0,
                pageSettings: pageSettings(500, 360, 44),
                properties: {
                    columns: { count: 2, spacing: 24, separatorLine: true },
                    lineNumbering: { enabled: true, startAt: 1, increment: 1, restart: 'page', distanceFromText: 10 },
                },
                blocks: [
                    paragraph('columns-text', repeated('Two-column text flows with a visible separator.', 10), 1, 'columns'),
                ],
            },
        ],
    }, {}, { fontMetrics: createDeterministicMetrics() });

    assert.ok(display.layout.pages[0].columns.length === 2);
    assert.ok(display.commands.some(command => command.type === 'columnSeparator'));
    assert.ok(display.commands.some(command => command.type === 'lineNumber'));
});

test('balanced columns do not reuse stale overflow page for following landscape section', () => {
    const layout = layoutCanvasDocument({
        documentId: 'e3-balanced-columns-landscape-break',
        pageSettings: pageSettings(500, 360, 44),
        theme: theme(),
        sections: [
            {
                id: 'columns',
                order: 0,
                pageSettings: pageSettings(500, 360, 44),
                properties: {
                    columns: { count: 2, spacing: 24, separatorLine: true, balance: true },
                },
                blocks: [
                    paragraph('columns-story', repeated('Balanced columns should pull a short overflow paragraph back into a calm newspaper layout.', 14), 1, 'columns'),
                    sectionBreak('landscape-break', 2, 'landscape', 1, 'columns'),
                ],
            },
            {
                id: 'landscape',
                order: 1,
                pageSettings: pageSettings(620, 380, 40),
                blocks: [
                    paragraph('landscape-start', 'Landscape section must use the wider page after balanced column flow.', 3, 'landscape'),
                ],
            },
        ],
    }, { fontMetrics: createDeterministicMetrics() });

    assert.ok(layout.pages.some(page => page.sectionId === 'landscape' && page.width > page.height));
});

function fragment(layout, blockId) {
    const result = layout.blocks.find(block => block.blockId === blockId);
    assert.ok(result, `${blockId} fragment exists`);
    return result;
}

function pageSettings(width, height, margin) {
    return {
        width,
        height,
        marginTop: margin,
        marginRight: margin,
        marginBottom: margin,
        marginLeft: margin,
    };
}

function sectionBreak(id, order, nextSectionId, breakType, sectionId) {
    return {
        id,
        type: typeof breakType === 'number' ? 6 : 'pageBreak',
        sectionId,
        order,
        content: {
            type: 'pageBreak',
            pageBreak: { breakType, nextSectionId },
        },
    };
}

function paragraph(id, text, order, sectionId) {
    return {
        id,
        sectionId,
        type: 'paragraph',
        order,
        paragraphProperties: { alignment: 'left', lineSpacing: 1.08 },
        content: {
            type: 'paragraph',
            runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }],
        },
    };
}

function repeated(text, count) {
    return Array.from({ length: count }, () => text).join(' ');
}

function theme() {
    return { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11, paragraphSpacingAfter: 4 };
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, Array.from(text).reduce((sum, ch) => sum + (/\s/.test(ch) ? fontSize * 0.32 : fontSize * 0.5), 0)),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.22),
            };
        },
    };
}
