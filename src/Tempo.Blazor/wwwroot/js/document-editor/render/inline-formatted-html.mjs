// Phase D — render/inline-formatted-html.mjs
// `createRenderFormattedInlineHtml({markType, markValue, isSafeInlineFontFamily,
//   normalizeInlineFontSize, isSafeInlineCssColor, escapeHtml, renderInlineTextHtml,
//   asArray, asText, unique})` → `renderFormattedInlineHtml(run, chunk, innerHtml?)`
// — emits a `<span class="tm-document-inline …">` wrapping the text chunk with
// classes + inline style derived from the run's marks (bold/italic/underline/
// strike/super/sub/font family/font size/text color/highlight/link). Pre-rendered
// inner HTML may be passed via `innerHtml`; otherwise `renderInlineTextHtml(chunk)`
// is used. When no formatting fires (no marks beyond text), the content HTML is
// returned bare without a wrapper.
//
// Sanitisers are injected so the renderer never inlines unsafe CSS values.

export function createRenderFormattedInlineHtml(deps) {
    const opts = deps || {};
    for (const key of [
        'markType', 'markValue',
        'isSafeInlineFontFamily', 'normalizeInlineFontSize', 'isSafeInlineCssColor',
        'escapeHtml', 'renderInlineTextHtml',
        'asArray', 'asText', 'unique',
    ]) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createRenderFormattedInlineHtml requires options.${key} (function)`);
        }
    }
    const {
        markType, markValue,
        isSafeInlineFontFamily, normalizeInlineFontSize, isSafeInlineCssColor,
        escapeHtml, renderInlineTextHtml,
        asArray, asText, unique,
    } = opts;

    return function renderFormattedInlineHtml(run, chunk, innerHtml) {
        const marks = asArray(run && (run.marks || run.Marks));
        const classes = ['tm-document-inline'];
        const styles = [];
        const textDecoration = [];
        let href = '';
        let linkTitle = '';
        marks.forEach(function (mark) {
            const type = markType(mark);
            const value = markValue(mark);
            if (type === 'bold') {
                classes.push('tm-document-inline--bold');
            } else if (type === 'italic') {
                classes.push('tm-document-inline--italic');
            } else if (type === 'underline') {
                classes.push('tm-document-inline--underline');
                textDecoration.push('underline');
            } else if (type === 'strikethrough' || type === 'strike') {
                classes.push('tm-document-inline--strikethrough');
                textDecoration.push('line-through');
            } else if (type === 'superscript') {
                classes.push('tm-document-inline--superscript');
                styles.push('vertical-align:super', 'font-size:0.8em');
            } else if (type === 'subscript') {
                classes.push('tm-document-inline--subscript');
                styles.push('vertical-align:sub', 'font-size:0.8em');
            } else if (type === 'fontfamily' && isSafeInlineFontFamily(value)) {
                classes.push('tm-document-inline--font-family');
                styles.push('font-family:' + value);
            } else if (type === 'fontsize') {
                const fontSize = normalizeInlineFontSize(value);
                if (fontSize) {
                    classes.push('tm-document-inline--font-size');
                    styles.push('font-size:' + fontSize);
                }
            } else if ((type === 'textcolor' || type === 'fontcolor' || type === 'foregroundcolor')
                && isSafeInlineCssColor(value)) {
                classes.push('tm-document-inline--text-color');
                styles.push('color:' + value);
            } else if ((type === 'highlight' || type === 'backgroundcolor')
                && isSafeInlineCssColor(value)) {
                classes.push('tm-document-inline--highlight');
                styles.push('background-color:' + value);
            } else if (type === 'link') {
                classes.push('tm-document-inline--link');
                href = asText((mark && (mark.href || mark.Href || mark.url || mark.Url || mark.link?.href || mark.Link?.Href)) || value || '');
                linkTitle = asText(mark && (mark.title || mark.Title || mark.link?.title || mark.Link?.Title) || '');
            }
        });
        if (textDecoration.length) {
            styles.push('text-decoration-line:' + unique(textDecoration).join(' '));
        }
        const hasFormatting = classes.length > 1 || styles.length > 0 || href;
        const contentHtml = innerHtml !== undefined ? innerHtml : renderInlineTextHtml(chunk);
        if (!hasFormatting) return contentHtml;
        const inlineId = asText((run && (run.id || run.Id)) || '');
        const attrs = [
            'class="' + classes.join(' ') + '"',
            'data-inline-id="' + escapeHtml(inlineId) + '"',
            'data-node-id="' + escapeHtml(inlineId) + '"',
        ];
        if (styles.length) attrs.push('style="' + escapeHtml(styles.join(';')) + '"');
        if (href) {
            attrs.push('data-href="' + escapeHtml(href) + '"');
            attrs.push('data-link-href="' + escapeHtml(href) + '"');
            attrs.push('role="link"');
        }
        if (linkTitle) attrs.push('title="' + escapeHtml(linkTitle) + '"');
        return '<span ' + attrs.join(' ') + '>' + contentHtml + '</span>';
    };
}
