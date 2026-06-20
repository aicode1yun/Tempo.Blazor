import test from 'node:test';
import assert from 'node:assert/strict';
import { layoutCanvasDocument } from '../pagination.mjs';
import { nextTabStop, normalizeTabStops } from '../tab-stops.mjs';
import { buildDisplayList } from '../../render/display-list.mjs';

test('tab stop model normalizes explicit stops and advances to default interval', () => {
    const model = normalizeTabStops({
        defaultTabWidth: 24,
        tabStops: [
            { position: 180, alignment: 'decimal', leader: 'dots' },
            { position: 72, alignment: 'right', leader: 'dash' },
        ],
    });

    assert.equal(model.defaultTabWidth, 24);
    assert.deepEqual(model.tabStops.map(stop => stop.position), [72, 180]);
    assert.equal(nextTabStop(60, model).position, 72);
    assert.equal(nextTabStop(90, model).position, 180);
    assert.equal(nextTabStop(181, model).position, 192);
});

test('left center right decimal and bar tab stops align following content', () => {
    const layout = layoutCanvasDocument(modelWithTabs([
        { position: 120, alignment: 'left', leader: 'none' },
        { position: 220, alignment: 'center', leader: 'dash' },
        { position: 320, alignment: 'right', leader: 'underline' },
        { position: 430, alignment: 'decimal', leader: 'dots' },
        { position: 500, alignment: 'bar', leader: 'none' },
    ], 'A\tLeft\tCenter\tRight\t123.45\tBar'));
    const line = layout.blocks.find(block => block.blockId === 'tabs')?.lines?.[0];
    assert.ok(line, 'Expected a laid out tab paragraph line.');

    const segments = line.segments.filter(segment => segment.type !== 'tab' && segment.type !== 'space');
    const texts = segments.map(segment => segment.text).join('');
    assert.match(texts, /Left/);
    const left = findSegment(line, 'Left');
    const center = findSegment(line, 'Center');
    const right = findSegment(line, 'Right');
    const number = findSegment(line, '123.45');
    const bar = findSegment(line, 'Bar');
    const baseX = line.rect.x;

    assert.ok(Math.abs(left.rect.x - (baseX + 120 * 96 / 72)) < 1.5);
    assert.ok(Math.abs((center.rect.x + center.rect.width / 2) - (baseX + 220 * 96 / 72)) < 1.5);
    assert.ok(Math.abs((right.rect.x + right.rect.width) - (baseX + 320 * 96 / 72)) < 1.5);
    const decimalX = number.rect.x + number.rect.width * ('123'.length / '123.45'.length);
    assert.ok(Math.abs(decimalX - (baseX + 430 * 96 / 72)) < 8);
    assert.ok(bar.rect.x >= baseX + 500 * 96 / 72 - 1);
    assert.ok(line.tabLeaders.some(leader => leader.leader === 'dots'));
    assert.ok(line.tabLeaders.some(leader => leader.leader === 'bar'));
});

test('tab leaders are emitted into the display list for canvas painting', () => {
    const displayList = buildDisplayList(modelWithTabs([
        { position: 240, alignment: 'decimal', leader: 'dots' },
    ], 'Subtotal\t123.45'));
    const leader = displayList.commands.find(command => command.type === 'tabLeader' && command.leader === 'dots');

    assert.ok(leader, 'Expected dotted leader command.');
    assert.equal(leader.blockId, 'tabs');
    assert.ok(leader.width > 100);
});

function modelWithTabs(tabStops, text) {
    return {
        documentId: 'tab-test',
        pageSettings: { width: 794, height: 1123, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
        body: {
            blocks: [{
                id: 'tabs',
                type: 'paragraph',
                order: 1,
                paragraphProperties: {
                    defaultTabWidth: 36,
                    tabStops,
                },
                content: {
                    type: 'paragraph',
                    runs: [{ id: 'run', type: 'text', text, style: { fontSize: 12 } }],
                },
            }],
        },
    };
}

function findSegment(line, text) {
    const segment = line.segments.find(item => String(item.text || '') === text);
    assert.ok(segment, `Expected segment "${text}".`);
    return segment;
}
