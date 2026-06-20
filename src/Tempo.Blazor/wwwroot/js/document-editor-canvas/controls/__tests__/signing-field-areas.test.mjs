import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDisplayList } from '../../render/display-list.mjs';
import { extractSigningFields } from '../signing-field-areas.mjs';

// The bridge core (plan S2.13/S2.14): signing field areas are DERIVED from the layout, never stored.
// Display-list signing field commands are grouped by field uuid -> one field with one normalized 0..1
// area per page occurrence. A body field has 1 area; a header/footer field has N (one per page).

function paragraph(id, value, order = 0) {
    return { id, type: 'paragraph', order, content: { type: 'paragraph', runs: [{ id: `${id}-r`, type: 'text', text: value, marks: [] }] } };
}

function signingRun(id, uuid, fieldType, submitterUuid) {
    return { id, type: 'signingField', text: '', marks: [], signingField: { uuid, fieldType, submitterUuid, boxWidth: 120, boxHeight: 32 } };
}

function multiPageModelWithFooterField() {
    const blocks = [
        { id: 'p1', type: 'paragraph', order: 0, content: { type: 'paragraph', runs: [{ id: 'p1-t', type: 'text', text: 'Sign here: ', marks: [] }, signingRun('p1-f', 'body-field', 'signature', 'signer')] } },
        ...Array.from({ length: 34 }, (_, index) => paragraph(`b${index + 1}`, `Body line ${index + 1} keeps pagination running across multiple pages.`, index + 1)),
    ];
    return {
        documentId: 'signing-areas',
        pageSettings: { width: 794, height: 520, marginTop: 64, marginRight: 64, marginBottom: 64, marginLeft: 64, headerDistanceFromTop: 36, footerDistanceFromBottom: 36 },
        sections: [{ id: 's1', order: 0, properties: {}, blocks }],
        body: { blocks },
        headersFooters: [
            {
                id: 'footer-1', type: 1, scope: 0, sectionId: 's1',
                blocks: [{ id: 'footer-block', type: 'paragraph', paragraphProperties: { alignment: 'center' }, content: { type: 'paragraph', runs: [signingRun('foot-f', 'footer-field', 'initials', 'signer')] } }],
            },
        ],
    };
}

test('a body field yields exactly one normalized area; a footer field yields one per page', () => {
    const display = buildDisplayList(multiPageModelWithFooterField(), {});
    assert.ok(display.pageCount >= 3, `multi-page document expected (got ${display.pageCount})`);

    const fields = extractSigningFields(display);
    const body = fields.find(field => field.uuid === 'body-field');
    const footer = fields.find(field => field.uuid === 'footer-field');

    assert.ok(body && footer, 'both fields are extracted and grouped by uuid');
    assert.equal(fields.length, 2, 'commands are grouped into exactly two fields');

    assert.equal(body.areas.length, 1, 'a body field has a single area');
    assert.equal(footer.areas.length, display.pageCount, 'a footer field has one area per page');

    for (const field of fields) {
        for (const area of field.areas) {
            assert.ok(area.x >= 0 && area.x <= 1, 'x is normalized');
            assert.ok(area.y >= 0 && area.y <= 1, 'y is normalized');
            assert.ok(area.width > 0 && area.width <= 1, 'width is normalized');
            assert.ok(area.height > 0 && area.height <= 1, 'height is normalized');
        }
    }

    const footerPages = footer.areas.map(area => area.page).sort((a, b) => a - b);
    assert.deepEqual(footerPages, Array.from({ length: display.pageCount }, (_, index) => index), 'footer areas cover every page');
    // The footer field renders at the same place on each page.
    const first = footer.areas[0];
    for (const area of footer.areas) {
        assert.ok(Math.abs(area.x - first.x) < 0.001 && Math.abs(area.y - first.y) < 0.001, 'footer area is consistent across pages');
    }

    assert.equal(footer.fieldType, 'initials');
    assert.equal(footer.submitterUuid, 'signer');
});

test('areas track the layout: inserting blocks before a body field moves its area', () => {
    const before = extractSigningFields(buildDisplayList(multiPageModelWithFooterField(), {}))
        .find(field => field.uuid === 'body-field');

    const model = multiPageModelWithFooterField();
    // Insert a tall spacer block AFTER the field's page-0 paragraph so the field flows further down /
    // onward. order -1 would precede p1 (order 0); we want it before by giving it order -1.
    const spacer = paragraph('spacer', Array.from({ length: 60 }, () => 'filler text line wraps to push the field downward').join(' '), -1);
    // body.blocks and sections[0].blocks are the same array reference — insert once.
    model.body.blocks.unshift(spacer);
    const after = extractSigningFields(buildDisplayList(model, {}))
        .find(field => field.uuid === 'body-field');

    assert.ok(after.areas[0].page > before.areas[0].page || after.areas[0].y > before.areas[0].y + 0.001,
        'the field area shifts down/onward after content is inserted before it');
});

test('header/footer scope is honoured: a first-page-only field omits later pages', () => {
    const blocks = Array.from({ length: 34 }, (_, index) => paragraph(`b${index + 1}`, `Body line ${index + 1} fills several pages of content.`));
    const model = {
        documentId: 'signing-areas-scope',
        pageSettings: { width: 794, height: 520, marginTop: 64, marginRight: 64, marginBottom: 64, marginLeft: 64, headerDistanceFromTop: 36, footerDistanceFromBottom: 36 },
        sections: [{ id: 's1', order: 0, properties: { differentFirstPage: true }, blocks }],
        body: { blocks },
        headersFooters: [
            { id: 'header-first', type: 0, scope: 1, sectionId: 's1', blocks: [{ id: 'hf-block', type: 'paragraph', content: { type: 'paragraph', runs: [signingRun('hf-f', 'first-only', 'date', 'signer')] } }] },
        ],
    };
    const display = buildDisplayList(model, {});
    assert.ok(display.pageCount >= 2);

    const field = extractSigningFields(display).find(item => item.uuid === 'first-only');
    assert.ok(field, 'the first-page header field is extracted');
    assert.equal(field.areas.length, 1, 'a first-page-only field renders on a single page');
    assert.equal(field.areas[0].page, 0, 'and only on the first page');
});
