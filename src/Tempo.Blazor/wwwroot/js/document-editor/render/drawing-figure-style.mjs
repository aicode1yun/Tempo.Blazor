// Phase D — render/drawing-figure-style.mjs
// `createExplicitObjectLayerRect({rectFromGeometry})` →
//   `explicitObjectLayerRect(object)` — returns the explicit object rect (rect /
//   layoutRect / objectRect) or null when no explicit rect was supplied or the
//   rect is the {0,0,0,0} placeholder.
// `createObjectLayerRectFromObject({rectFromGeometry})` →
//   `objectLayerRectFromObject(object, fallbackWidth, fallbackHeight)` — returns
//   `{x, y, width, height, explicit}` either from the explicit rect or a fallback
//   one anchored at (0,0).
// `createRenderDrawingFigureStyle({objectLayerRectFromObject})` →
//   `renderDrawingFigureStyle(object)` — inline CSS string for an anchored or
//   inline drawing figure. Inline drawings get `display:inline-block` w/ baseline
//   alignment; anchored ones get absolute positioning + max-width:none.
// `estimateInlineDrawingCaptionReserveHeight(object, width)` — heuristic estimate
//   of the height a caption beneath an inline drawing will occupy. Returns 0 when
//   no caption; otherwise `marginTop + lines * lineHeight` with lines capped at 6.
// `createRenderDrawingAnchorReservationStyle({estimateInlineDrawingCaptionReserveHeight})` →
//   `renderDrawingAnchorReservationStyle(object, inline)` — inline CSS string for
//   the drawing anchor reservation span. Inline reservations carry actual size;
//   anchored ones collapse to 0×0 with `visibility:hidden`.
// `createRenderImageFigureClasses({normalizeWrapModeName, asArray})` →
//   `renderImageFigureClasses(selected, object)` — class list string for an image
//   figure based on wrap-mode + horizontal-alignment + selected state.

import { asText } from '../core/helpers.mjs';

export function createExplicitObjectLayerRect(options) {
    const opts = options || {};
    if (typeof opts.rectFromGeometry !== 'function') {
        throw new TypeError(
            'createExplicitObjectLayerRect requires options.rectFromGeometry (function)');
    }
    const { rectFromGeometry } = opts;
    return function explicitObjectLayerRect(object) {
        const source = object && (object.rect || object.Rect
            || object.layoutRect || object.LayoutRect
            || object.objectRect || object.ObjectRect);
        if (!source) return null;
        const rect = rectFromGeometry(source);
        if (rect.width <= 0 && rect.height <= 0 && rect.x === 0 && rect.y === 0) {
            return null;
        }
        return rect;
    };
}

export function createObjectLayerRectFromObject(options) {
    const opts = options || {};
    if (typeof opts.rectFromGeometry !== 'function') {
        throw new TypeError(
            'createObjectLayerRectFromObject requires options.rectFromGeometry (function)');
    }
    const explicit = createExplicitObjectLayerRect(opts);
    return function objectLayerRectFromObject(object, fallbackWidth, fallbackHeight) {
        const width = Math.max(1,
            Number(fallbackWidth || (object && object.width) || 120) || 120);
        const height = Math.max(1,
            Number(fallbackHeight || (object && object.height) || 80) || 80);
        const rect = explicit(object);
        if (!rect) return { x: 0, y: 0, width, height, explicit: false };
        return {
            x: Number(rect.x || 0) || 0,
            y: Number(rect.y || 0) || 0,
            width: Math.max(1, Number(rect.width || width) || width),
            height: Math.max(1, Number(rect.height || height) || height),
            explicit: true,
        };
    };
}

export function createRenderDrawingFigureStyle(options) {
    const opts = options || {};
    if (typeof opts.objectLayerRectFromObject !== 'function') {
        throw new TypeError(
            'createRenderDrawingFigureStyle requires options.objectLayerRectFromObject (function)');
    }
    const { objectLayerRectFromObject } = opts;
    return function renderDrawingFigureStyle(object) {
        const width = Math.max(1, Number((object && object.width) || 120) || 120);
        const height = Math.max(1, Number((object && object.height) || 80) || 80);
        const rect = objectLayerRectFromObject(object, width, height);
        const styles = [
            '--tm-layout-object-width:' + rect.width + 'px',
            '--tm-layout-object-height:' + rect.height + 'px',
            'width:' + rect.width + 'px',
            'min-height:' + rect.height + 'px',
            'height:' + rect.height + 'px',
            'box-sizing:border-box',
        ];
        if (object && object.isInline === true) {
            styles.push('display:inline-block');
            styles.push('vertical-align:baseline');
            styles.push('margin:0');
            return styles.join(';');
        }
        styles.push('position:absolute');
        styles.push('left:' + rect.x + 'px');
        styles.push('top:' + rect.y + 'px');
        styles.push('max-width:none');
        styles.push('margin:0');
        return styles.join(';');
    };
}

export function estimateInlineDrawingCaptionReserveHeight(object, width) {
    const caption = asText((object && object.caption) || '').trim();
    if (!caption) return 0;
    const availableWidth = Math.max(32, Number(width || 0) || 0);
    const fontSize = 12;
    const lineHeight = 15;
    const marginTop = 4;
    const averageCharacterWidth = fontSize * 0.52;
    const charactersPerLine = Math.max(8, Math.floor(availableWidth / averageCharacterWidth));
    const lines = Math.max(1, Math.ceil(caption.length / charactersPerLine));
    return marginTop + Math.min(lines, 6) * lineHeight;
}

export function createRenderDrawingAnchorReservationStyle(options) {
    const opts = options || {};
    if (typeof opts.estimateInlineDrawingCaptionReserveHeight !== 'function') {
        throw new TypeError(
            'createRenderDrawingAnchorReservationStyle requires options.estimateInlineDrawingCaptionReserveHeight (function)');
    }
    const { estimateInlineDrawingCaptionReserveHeight: estimateCaption } = opts;
    return function renderDrawingAnchorReservationStyle(object, inline) {
        const width = Math.max(1, Number((object && object.width) || 120) || 120);
        const height = Math.max(1, Number((object && object.height) || 80) || 80);
        const captionReserveHeight = inline ? estimateCaption(object, width) : 0;
        const reserveHeight = height + captionReserveHeight;
        const styles = [
            '--tm-wysiwyg-drawing-anchor-width:' + width + 'px',
            '--tm-wysiwyg-drawing-anchor-height:' + reserveHeight + 'px',
            '--tm-wysiwyg-drawing-caption-reserve-height:' + captionReserveHeight + 'px',
        ];
        if (inline) {
            styles.push('display:inline-block');
            styles.push('width:' + width + 'px');
            styles.push('height:' + reserveHeight + 'px');
            return styles.join(';');
        }
        styles.push('display:inline-block');
        styles.push('width:0px');
        styles.push('height:0px');
        styles.push('max-width:0px');
        styles.push('max-height:0px');
        styles.push('overflow:hidden');
        styles.push('line-height:0');
        styles.push('vertical-align:baseline');
        styles.push('box-sizing:border-box');
        styles.push('visibility:hidden');
        styles.push('pointer-events:none');
        return styles.join(';');
    };
}
