// Phase D — render/object-overlay-html.mjs
// `buildObjectOverlayStyle(rect)` — absolute-positioned inline CSS string for an
//   object overlay (left/top/width/height from the rect, defaulting to 0).
// `createRenderWysiwygObjectSelectionOverlayHtml({objectLayerRectFromObject,
//   renderObjectResizeHandleHtml, escapeHtml, asText})` →
//   `renderWysiwygObjectSelectionOverlayHtml(inst, entry)` — selection overlay
//   `<div>` for an object entry; when selected, includes the selection box +
//   8 resize handles.
// `createRenderWysiwygObjectGuidesOverlayHtml({objectLayerRectFromObject,
//   escapeHtml, asText, asArray})` →
//   `renderWysiwygObjectGuidesOverlayHtml(inst, entry)` — alignment-guides overlay
//   `<div>` with one `<span>` per `object.alignmentGuides` entry (orientation
//   default `'vertical'`).

export function buildObjectOverlayStyle(rect) {
    return [
        'position:absolute',
        'left:' + (Number(rect.x || 0) || 0) + 'px',
        'top:' + (Number(rect.y || 0) || 0) + 'px',
        'width:' + (Number(rect.width || 0) || 0) + 'px',
        'height:' + (Number(rect.height || 0) || 0) + 'px',
    ].join(';');
}

export function createRenderWysiwygObjectSelectionOverlayHtml(options) {
    const opts = options || {};
    for (const key of [
        'objectLayerRectFromObject', 'renderObjectResizeHandleHtml',
        'escapeHtml', 'asText',
    ]) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createRenderWysiwygObjectSelectionOverlayHtml requires options.${key} (function)`);
        }
    }
    const {
        objectLayerRectFromObject, renderObjectResizeHandleHtml, escapeHtml, asText,
    } = opts;

    return function renderWysiwygObjectSelectionOverlayHtml(inst, entry) {
        const object = (entry && entry.object) || {};
        const width = Math.max(1, Number(object.width || 120) || 120);
        const height = Math.max(1, Number(object.height || 80) || 80);
        const rect = objectLayerRectFromObject(object, width, height);
        const objectId = asText((entry && entry.objectId) || object.objectId || '');
        const anchorBlockId = asText(
            object.anchorBlockId || (entry && entry.blockId) || object.blockId || '');
        const selected = entry && entry.selected === true;
        const classes = ['tm-wysiwyg-object-selection-overlay'];
        if (selected) classes.push('tm-wysiwyg-object-selection-overlay--active');
        const attrs = [
            'class="' + classes.join(' ') + '"',
            'data-testid="document-wysiwyg-object-selection-overlay"',
            'data-object-id="' + escapeHtml(objectId) + '"',
            'data-render-object-id="' + escapeHtml(objectId) + '"',
            'data-block-id="' + escapeHtml(anchorBlockId) + '"',
            'style="' + escapeHtml(buildObjectOverlayStyle(rect)) + '"',
        ];
        const html = ['<div ' + attrs.join(' ') + '>'];
        if (selected) {
            html.push('<span class="tm-wysiwyg-selection-box"'
                + ' data-testid="document-wysiwyg-object-selection-box"'
                + ' aria-hidden="true"></span>');
            ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'].forEach(function (handleName) {
                html.push(renderObjectResizeHandleHtml(inst, handleName, selected));
            });
        }
        html.push('</div>');
        return html.join('');
    };
}

export function createRenderWysiwygObjectGuidesOverlayHtml(options) {
    const opts = options || {};
    for (const key of ['objectLayerRectFromObject', 'escapeHtml', 'asText', 'asArray']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createRenderWysiwygObjectGuidesOverlayHtml requires options.${key} (function)`);
        }
    }
    const { objectLayerRectFromObject, escapeHtml, asText, asArray } = opts;

    return function renderWysiwygObjectGuidesOverlayHtml(inst, entry) {
        const object = (entry && entry.object) || {};
        const width = Math.max(1, Number(object.width || 120) || 120);
        const height = Math.max(1, Number(object.height || 80) || 80);
        const rect = objectLayerRectFromObject(object, width, height);
        const objectId = asText((entry && entry.objectId) || object.objectId || '');
        const guides = asArray(object.alignmentGuides || object.AlignmentGuides);
        const html = ['<div class="tm-wysiwyg-object-guides-overlay"'
            + ' data-testid="document-wysiwyg-object-guides-overlay"'
            + ' data-object-id="' + escapeHtml(objectId) + '"'
            + ' style="' + escapeHtml(buildObjectOverlayStyle(rect)) + '">'];
        guides.forEach(function (guide) {
            const orientation = asText((guide && (guide.orientation || guide.Orientation))
                || 'vertical');
            html.push('<span class="tm-wysiwyg-object-guide tm-wysiwyg-object-guide--'
                + escapeHtml(orientation) + '"'
                + ' data-guide-orientation="' + escapeHtml(orientation) + '"></span>');
        });
        html.push('</div>');
        return html.join('');
    };
}
