// Phase D — render/object-aria-html.mjs
// HTML attribute / span builders for object-selection a11y. Factored out of the
// editor's render path so the same accessibility wiring can be unit-tested.
//
// `createRenderObjectSelectionDescriptionAttribute({escapeHtml, activeObjectStatusId})` →
//   `renderObjectSelectionDescriptionAttribute(inst, selected)` — when `selected`
//   is `true`, emits ` aria-describedby="<active-object-status-id>"` so screen
//   readers announce the active-object status text alongside the selected object.
//   Otherwise returns an empty string.
// `createRenderObjectResizeHandleHtml({escapeHtml, objectResizeHandleAriaLabel})` →
//   `renderObjectResizeHandleHtml(inst, handleName, selected)` — emits the resize
//   handle span. When `selected`, it's a focusable `role="button"` with an aria
//   label; otherwise it's `aria-hidden`.
// `createRenderObjectFocusPolicyAttributes({escapeHtml, createObjectFocusPolicy})` →
//   `renderObjectFocusPolicyAttributes(selected)` — emits
//   ` data-object-focus-policy="<policy>" aria-selected="<true|false>"`
//   plus `data-object-selected="true"` when the object is currently selected.
// `renderObjectRotationHandleHtml(selected)` — static rotation-handle span (no
//   dynamic content, so it needs no injected escaper). Focusable `role="button"`
//   with an "Rotate image" label when selected, else `aria-hidden`.

export function renderObjectRotationHandleHtml(selected) {
    const attrs = [
        'class="tm-wysiwyg-object-rotation-handle"',
        'data-testid="document-wysiwyg-object-rotation-handle"',
    ];
    if (selected === true) {
        attrs.push('role="button"');
        attrs.push('tabindex="-1"');
        attrs.push('aria-label="Rotate image"');
    } else {
        attrs.push('aria-hidden="true"');
    }
    return '<span ' + attrs.join(' ') + '></span>';
}

export function createRenderObjectSelectionDescriptionAttribute(options) {
    const opts = options || {};
    if (typeof opts.escapeHtml !== 'function') {
        throw new TypeError(
            'createRenderObjectSelectionDescriptionAttribute requires options.escapeHtml (function)');
    }
    if (typeof opts.activeObjectStatusId !== 'function') {
        throw new TypeError(
            'createRenderObjectSelectionDescriptionAttribute requires options.activeObjectStatusId (function)');
    }
    const { escapeHtml, activeObjectStatusId } = opts;
    return function renderObjectSelectionDescriptionAttribute(inst, selected) {
        return selected === true
            ? ' aria-describedby="' + escapeHtml(activeObjectStatusId(inst)) + '"'
            : '';
    };
}

export function createRenderObjectResizeHandleHtml(options) {
    const opts = options || {};
    if (typeof opts.escapeHtml !== 'function') {
        throw new TypeError(
            'createRenderObjectResizeHandleHtml requires options.escapeHtml (function)');
    }
    if (typeof opts.objectResizeHandleAriaLabel !== 'function') {
        throw new TypeError(
            'createRenderObjectResizeHandleHtml requires options.objectResizeHandleAriaLabel (function)');
    }
    const { escapeHtml, objectResizeHandleAriaLabel } = opts;
    return function renderObjectResizeHandleHtml(inst, handleName, selected) {
        const attrs = [
            'class="tm-wysiwyg-object-resize-handle tm-wysiwyg-object-resize-handle--'
                + escapeHtml(handleName) + '"',
            'data-resize-handle="' + escapeHtml(handleName) + '"',
            'data-testid="document-wysiwyg-object-resize-handle-' + escapeHtml(handleName) + '"',
        ];
        if (selected === true) {
            attrs.push('role="button"');
            attrs.push('tabindex="-1"');
            attrs.push('aria-label="'
                + escapeHtml(objectResizeHandleAriaLabel(inst, handleName)) + '"');
        } else {
            attrs.push('aria-hidden="true"');
        }
        return '<span ' + attrs.join(' ') + '></span>';
    };
}

export function createRenderObjectFocusPolicyAttributes(options) {
    const opts = options || {};
    if (typeof opts.escapeHtml !== 'function') {
        throw new TypeError(
            'createRenderObjectFocusPolicyAttributes requires options.escapeHtml (function)');
    }
    if (typeof opts.createObjectFocusPolicy !== 'function') {
        throw new TypeError(
            'createRenderObjectFocusPolicyAttributes requires options.createObjectFocusPolicy (function)');
    }
    const { escapeHtml, createObjectFocusPolicy } = opts;
    return function renderObjectFocusPolicyAttributes(selected) {
        const policy = createObjectFocusPolicy(selected === true);
        let attributes = ' data-object-focus-policy="' + escapeHtml(policy.focusPolicy)
            + '" aria-selected="' + (policy.selected ? 'true' : 'false') + '"';
        if (policy.selected) attributes += ' data-object-selected="true"';
        return attributes;
    };
}
