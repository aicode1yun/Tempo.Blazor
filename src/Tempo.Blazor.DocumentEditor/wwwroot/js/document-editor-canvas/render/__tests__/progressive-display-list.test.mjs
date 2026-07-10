import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../display-list.mjs';

// Perf plan N11.2: buildDisplayList passes options.layoutBudget / options.layoutResume through to
// layoutCanvasDocument and surfaces the progressive state (layoutComplete/layoutResume/
// layoutProgress) so the canvas stack can schedule idle continuations. The commands of the final
// continuation must be byte-identical to a single unbudgeted build.

test('buildDisplayList surfaces a partial layout and continues via layoutResume to an identical result', () => {
    const model = buildModel(120);
    const full = buildDisplayList(model, null, { fontMetrics: metrics() });
    assert.equal(full.layoutComplete, true, 'an unbudgeted build reports a complete layout');
    assert.equal(full.layoutResume, null);
    assert.ok(full.pageCount > 4, 'fixture must span several pages');

    let step = buildDisplayList(model, null, { fontMetrics: metrics(), layoutBudget: { maxPages: 2 } });
    assert.equal(step.layoutComplete, false, 'a budgeted build must report the partial layout');
    assert.ok(step.layoutResume, 'a partial build must expose the resume token');
    assert.ok(step.layoutProgress.laidBlockCount < step.layoutProgress.totalBlockCount);
    assert.ok(step.pageCount < full.pageCount, 'partial build must not lay out every page');

    let iterations = 0;
    while (step.layoutComplete === false) {
        step = buildDisplayList(model, null, {
            fontMetrics: metrics(),
            layoutResume: step.layoutResume,
            layoutBudget: { maxPages: step.pageCount + 2 },
        });
        iterations += 1;
        assert.ok(iterations < 200, 'continuation must terminate');
    }

    assert.equal(step.pageCount, full.pageCount, 'final continuation reaches the full page count');
    assert.equal(
        JSON.stringify(step.commands),
        JSON.stringify(full.commands),
        'chunked display list must be byte-identical to the full build');
});

function buildModel(count) {
    return {
        documentId: 'n11-display-list',
        pageSettings: { width: 600, height: 900, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 8 },
        body: {
            blocks: Array.from({ length: count }, (_, index) => ({
                id: `p${index}`,
                type: 'paragraph',
                order: index + 1,
                paragraphProperties: { alignment: 'left', lineSpacing: 1.1 },
                content: {
                    type: 'paragraph',
                    runs: [{
                        id: `p${index}-run`,
                        type: 'text',
                        text: `Paragraph ${index + 1} carries deliberately long deterministic descriptive contract text that wraps across several visual lines to fill pages quickly for the progressive display list tests.`,
                        marks: [],
                    }],
                },
            })),
        },
    };
}

function metrics() {
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
