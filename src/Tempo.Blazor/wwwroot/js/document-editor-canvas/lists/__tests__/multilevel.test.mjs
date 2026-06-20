import assert from 'node:assert/strict';
import test from 'node:test';
import { layoutCanvasDocument } from '../../layout/pagination.mjs';
import { resolveNumberingState } from '../numbering-engine.mjs';

test('legal multilevel numbering uses parent counters in label text', () => {
    const model = {
        documentId: 'e1-legal-multilevel',
        numberingDefinitions: [{
            id: 'legal',
            abstractId: 'legal',
            name: 'Legal',
            levels: [
                { level: 0, format: 'decimal', text: '%1.', startAt: 1, suffix: 'tab', indent: 0, hanging: 24 },
                { level: 1, format: 'decimal', text: '%1.%2.', startAt: 1, suffix: 'tab', indent: 24, hanging: 24 },
                { level: 2, format: 'decimal', text: '%1.%2.%3.', startAt: 1, suffix: 'tab', indent: 48, hanging: 24 },
            ],
        }],
        body: {
            blocks: [
                item('one', 10, 0),
                item('one-one', 20, 1),
                item('one-one-one', 30, 2),
                item('two', 40, 0),
                item('two-one', 50, 1),
            ],
        },
    };

    const state = resolveNumberingState(model, model.body.blocks);
    assert.deepEqual(model.body.blocks.map(block => state.labels.get(block.id)), ['1.', '1.1.', '1.1.1.', '2.', '2.1.']);
});

test('list label layout keeps hanging labels outside wrapped text measure', () => {
    const layout = layoutCanvasDocument({
        documentId: 'e1-list-layout',
        pageSettings: { width: 420, height: 520, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11, paragraphSpacingAfter: 8 },
        numberingDefinitions: [{
            id: 'legal',
            abstractId: 'legal',
            name: 'Legal',
            levels: [
                { level: 0, format: 'decimal', text: '%1.', startAt: 1, suffix: 'tab', indent: 0, hanging: 26 },
                { level: 1, format: 'decimal', text: '%1.%2.', startAt: 1, suffix: 'tab', indent: 30, hanging: 30 },
            ],
        }],
        body: {
            blocks: [
                item('first', 10, 0, 'A top level item establishes the parent counter for the nested label.'),
                item('nested', 20, 1, 'The nested item wraps text while its legal numbering label remains outside the readable text column.'),
            ],
        },
    }, { fontMetrics: createDeterministicMetrics() });

    const nestedLabel = layout.listLabels.find(label => label.blockId === 'nested');
    const nestedBlock = layout.blocks.find(block => block.blockId === 'nested');
    assert.ok(nestedLabel);
    assert.ok(nestedBlock);
    assert.equal(nestedLabel.text, '1.1.');
    const firstSegment = nestedBlock.segments[0];
    assert.ok(nestedLabel.x + nestedLabel.width < firstSegment.rect.x);
});

function item(id, order, level, text = id) {
    return {
        id,
        type: 'list',
        order,
        paragraphProperties: { alignment: 'left' },
        content: {
            type: 'list',
            list: {
                ordered: true,
                indentLevel: level,
                numberingId: 'legal',
                abstractNumberingId: 'legal',
                numberFormat: 'legal',
                startNumber: 1,
            },
            runs: [{ id: `${id}-run`, type: 'text', text, marks: [] }],
        },
    };
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, Array.from(text).reduce((sum, ch) => sum + (/\s/.test(ch) ? fontSize * 0.32 : fontSize * 0.52), 0)),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
