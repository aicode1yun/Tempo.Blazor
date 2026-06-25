import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../../render/display-list.mjs';

// A signing field placed in a (default) footer renders ON EVERY PAGE: the header/footer layout lays
// out the signingField run as an atomic box per page, all carrying the SAME field uuid. This is the
// source of the field's multiple areas (plan S2.5b/S2.13 — one footer field, N page occurrences).

function modelWithFooterSigningField() {
    const blocks = Array.from({ length: 36 }, (_, index) =>
        paragraph(`p${index + 1}`, `Body line ${index + 1} keeps pagination active across several pages of the contract.`));
    return {
        documentId: 'signing-footer-layout',
        pageSettings: { width: 794, height: 520, marginTop: 64, marginRight: 64, marginBottom: 64, marginLeft: 64, headerDistanceFromTop: 36, footerDistanceFromBottom: 36 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11 },
        sections: [{ id: 's1', order: 0, properties: {}, blocks }],
        body: { blocks },
        headersFooters: [
            {
                id: 'footer-sign',
                type: 1, // footer
                scope: 0, // primary / default → every page
                sectionId: 's1',
                blocks: [
                    {
                        id: 'footer-sign-block',
                        sectionId: 's1',
                        type: 'paragraph',
                        order: 10,
                        paragraphProperties: { alignment: 'center' },
                        content: {
                            type: 'paragraph',
                            runs: [
                                { id: 'foot-initial', type: 'signingField', text: '', marks: [], signingField: { uuid: 'field-footer', fieldType: 'initials', submitterUuid: 'signer', boxWidth: 96, boxHeight: 40 } },
                            ],
                        },
                    },
                ],
            },
        ],
    };
}

function paragraph(id, value) {
    return {
        id,
        sectionId: 's1',
        type: 'paragraph',
        order: Number(id.slice(1)) * 10,
        paragraphProperties: { spacingAfter: 6 },
        content: { type: 'paragraph', runs: [{ id: `${id}-r`, type: 'text', text: value, marks: [] }] },
    };
}

test('a footer signing field lays out as a box on every page with the same field uuid', () => {
    const display = buildDisplayList(modelWithFooterSigningField(), {});
    assert.ok(display.pageCount >= 3, `the body must span multiple pages (got ${display.pageCount})`);

    const signingCommands = display.commands.filter(command => command.type === 'signingField');
    assert.equal(signingCommands.length, display.pageCount, 'the footer field renders once per page');

    const pages = new Set(signingCommands.map(command => command.pageIndex));
    assert.equal(pages.size, display.pageCount, 'one occurrence per distinct page');
    for (const command of signingCommands) {
        assert.equal(command.fieldUuid, 'field-footer', 'every occurrence carries the same field uuid');
        assert.equal(command.fieldType, 'initials');
        assert.ok(command.headerFooterId, 'the command is tagged as a header/footer occurrence');
        assert.equal(command.layer, 'content');
        assert.ok(command.width > 0 && command.height > 0);
    }
});

test('the footer signing box is clamped to the (short) footer region height', () => {
    // Box height (default signature would be tall) must fit the footer region so it does not overflow.
    const model = modelWithFooterSigningField();
    model.headersFooters[0].blocks[0].content.runs[0].signingField.boxHeight = 400; // absurdly tall
    const display = buildDisplayList(model, {});

    const footerFrames = display.commands.filter(command => command.type === 'headerFooterFrame' && command.region === 'Footer');
    const regionHeight = Math.max(...footerFrames.map(frame => Number(frame.height) || 0));
    assert.ok(regionHeight > 0, 'the footer region has a height');

    const signingCommands = display.commands.filter(command => command.type === 'signingField');
    for (const command of signingCommands) {
        assert.ok(command.height <= regionHeight + 1, `box height ${command.height} fits footer region ${regionHeight}`);
    }
});
