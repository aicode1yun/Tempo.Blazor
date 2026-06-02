// Phase D — render/image-figure-classes.mjs
// `createRenderImageFigureClasses({normalizeWrapModeName})` →
//   `renderImageFigureClasses(selected, object)` — class list string for an
//   image figure derived from:
//     • wrap-mode → `tm-wysiwyg-image--wrap-<mode>` (top-bottom / behind-text /
//       in-front-of-text are emitted with hyphenated suffixes; other modes
//       lowercase verbatim).
//     • `object.horizontalPosition.align` → position class
//       (right/end → position-right; center/middle → position-center; else
//       position-left).
//     • `selected` → adds both `tm-wysiwyg-image--selected` and
//       `tm-wysiwyg-object--selected`.

export function createRenderImageFigureClasses(options) {
    const opts = options || {};
    if (typeof opts.normalizeWrapModeName !== 'function') {
        throw new TypeError(
            'createRenderImageFigureClasses requires options.normalizeWrapModeName (function)');
    }
    const { normalizeWrapModeName } = opts;
    return function renderImageFigureClasses(selected, object) {
        const mode = normalizeWrapModeName(object && object.wrapMode);
        const align = String(
            (object && object.horizontalPosition && object.horizontalPosition.align)
            || 'Left').toLowerCase();
        const modeClass = mode === 'TopBottom'
            ? 'top-bottom'
            : (mode === 'BehindText'
                ? 'behind-text'
                : (mode === 'InFrontOfText'
                    ? 'in-front-of-text'
                    : mode.toLowerCase()));
        const classes = [
            'tm-wysiwyg-block',
            'tm-wysiwyg-image',
            'tm-wysiwyg-image--wrap-' + modeClass,
        ];
        classes.push(
            align === 'right' || align === 'end'
                ? 'tm-wysiwyg-image--position-right'
                : (align === 'center' || align === 'middle'
                    ? 'tm-wysiwyg-image--position-center'
                    : 'tm-wysiwyg-image--position-left'));
        if (selected) {
            classes.push('tm-wysiwyg-image--selected');
            classes.push('tm-wysiwyg-object--selected');
        }
        return classes.join(' ');
    };
}
