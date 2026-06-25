// Phase D — objects/editor-widget.mjs
// `createEditorWidgetFactory({normalizeImageObject})` factory →
//   `createEditorWidget(block)` — builds an EditorWidget adapter wrapping an
//   image/table block. Carries the supported command list, a hit-test that returns
//   `{type:'text', objectId:null}` for `targetRole === 'text-interval'` and a
//   `{type:'object', objectId, blockId}` shape otherwise. `kind` is `'table'` for
//   table blocks and `'image'` for everything else (drawings included).
// `createImageInspectorStateFactory({normalizeImageObject})` factory →
//   `createImageInspectorState(block)` — UI state for the image inspector panel.
//   Reveals the URL field only for `http(s)` URLs and emits `accessibility-warning`
//   when altText is missing.

import { asText, sortObject } from '../core/helpers.mjs';

export function createEditorWidgetFactory(options) {
    const opts = options || {};
    if (typeof opts.normalizeImageObject !== 'function') {
        throw new TypeError(
            'createEditorWidgetFactory requires options.normalizeImageObject (function)');
    }
    const { normalizeImageObject } = opts;

    return function createEditorWidget(block) {
        const object = normalizeImageObject(block);
        return sortObject({
            adapter: 'EditorWidget',
            kind: block && block.type === 'table' ? 'table' : 'image',
            blockId: object.blockId,
            objectId: object.objectId,
            commands: ['UpdateImageLayout', 'UpdateImageMetadata', 'DeleteObject', 'ReplaceImage'],
            selectionKind: 'object',
            fakeSelection: true,
            hitTest(input) {
                const role = (input && (input.targetRole || input.TargetRole)) || '';
                return role === 'text-interval'
                    ? { type: 'text', objectId: null }
                    : { type: 'object', objectId: object.objectId, blockId: object.blockId };
            },
        });
    };
}

export function createImageInspectorStateFactory(options) {
    const opts = options || {};
    if (typeof opts.normalizeImageObject !== 'function') {
        throw new TypeError(
            'createImageInspectorStateFactory requires options.normalizeImageObject (function)');
    }
    const { normalizeImageObject } = opts;

    return function createImageInspectorState(block) {
        const object = normalizeImageObject(block);
        const url = asText(object.url || '');
        const isHttpUrl = /^https?:\/\//i.test(url);
        return sortObject({
            altText: object.altText,
            caption: object.caption,
            width: object.width,
            height: object.height,
            wrapMode: object.wrapMode,
            showUrlField: isHttpUrl,
            urlEditable: isHttpUrl,
            url: isHttpUrl ? url : '',
            warningBadges: object.altText ? [] : ['accessibility-warning'],
        });
    };
}
