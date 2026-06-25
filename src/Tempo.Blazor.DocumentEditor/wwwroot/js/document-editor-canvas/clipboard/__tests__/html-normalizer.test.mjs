import assert from 'node:assert/strict';
import test from 'node:test';
import {
    createPlainTextFragment,
    createUrlFragment,
    fragmentToHtml,
    fragmentToPlainText,
    isSingleUrl,
    normalizeClipboardHtml,
    parseInternalFragment,
    serializeInternalFragment,
} from '../html-normalizer.mjs';

test('normalizes Word-like HTML into safe canvas heading and inline marks', () => {
    const fragment = normalizeClipboardHtml(`
        <html><body>
            <script>alert(1)</script>
            <h2 class="MsoTitle">Quarterly <strong>summary</strong></h2>
            <p style="color:#0f766e" onclick="x()">Approved <em>scope</em></p>
            <p><a href="javascript:alert(1)">Unsafe</a><a href="https://example.com/path">Safe link</a></p>
        </body></html>`);

    assert.equal(fragment.source, 'word');
    assert.equal(fragment.blocks[0].type, 'heading');
    assert.equal(fragment.blocks[0].content.headingLevel, 2);
    assert.ok(fragment.blocks[0].content.runs.some(run => run.marks.some(mark => mark.type === 'bold')));
    assert.ok(fragment.blocks[1].content.runs.some(run => run.marks.some(mark => mark.type === 'italic')));
    assert.ok(fragment.blocks[1].content.runs.some(run => run.marks.some(mark => mark.type === 'textColor' && mark.value === '#0f766e')));
    assert.ok(fragment.blocks[2].content.runs.some(run => run.marks.some(mark => mark.type === 'link' && mark.link.href === 'https://example.com/path')));
    assert.doesNotMatch(JSON.stringify({ blocks: fragment.blocks, html: fragment.html }), /javascript:|script|onclick/i);
});

test('normalizes Google Docs-like HTML and keeps highlights and underline', () => {
    const fragment = normalizeClipboardHtml(`
        <b id="docs-internal-guid-123"></b>
        <p><span style="background-color:#fef08a;text-decoration:underline">Highlighted line</span></p>`);

    assert.equal(fragment.source, 'googleDocs');
    assert.equal(fragmentToPlainText(fragment), 'Highlighted line');
    assert.ok(fragment.blocks[0].content.runs[0].marks.some(mark => mark.type === 'highlight' && mark.value === '#fef08a'));
    assert.ok(fragment.blocks[0].content.runs[0].marks.some(mark => mark.type === 'underline'));
});

test('normalizes table clipboard HTML into a canvas table block', () => {
    const fragment = normalizeClipboardHtml(`
        <table class="google-sheets-html-origin">
            <tr><td>Item</td><td>Amount</td></tr>
            <tr><td>Services</td><td>1200</td></tr>
        </table>`);

    assert.equal(fragment.blocks.length, 1);
    assert.equal(fragment.blocks[0].type, 'table');
    assert.equal(fragment.blocks[0].content.table.rows.length, 2);
    assert.equal(fragment.blocks[0].content.table.rows[1].cells[1].blocks[0].content.runs[0].text, '1200');
    assert.equal(fragmentToPlainText(fragment), 'Item\tAmount\nServices\t1200');
});

test('plain text and URL fragments round trip through the internal MIME payload', () => {
    const plain = createPlainTextFragment('First line\nSecond line');
    assert.equal(plain.blocks.length, 2);
    assert.equal(fragmentToPlainText(plain), 'First line\nSecond line');

    const url = createUrlFragment('https://example.com');
    assert.equal(isSingleUrl('https://example.com'), true);
    assert.match(fragmentToHtml(url), /<a href="https:\/\/example\.com">/);

    const parsed = parseInternalFragment(serializeInternalFragment(url));
    assert.equal(parsed.blocks[0].content.runs[0].marks[0].link.href, 'https://example.com');
});
