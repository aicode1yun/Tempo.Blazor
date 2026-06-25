// Phase D — render/atomic-overlays.mjs
// DOM overlay helpers used by `createAtomicRenderer`. Each function takes a
// `doc` adapter (the global `document` in production, a stub in Node tests) so
// the module remains environment-agnostic.
//
// Pure exports (no injection needed):
//   `createObjectFocusPolicy(selected)` — returns the canonical focus-policy shape.
//
// DOM factory exports (require a `doc` createElement-capable object):
//   `createRenderSelectionOverlay(doc)` → `renderSelectionOverlay(snapshot)`
//   `createRenderRevisionOverlay(doc)` → `renderRevisionOverlay(snapshot)`
//   `createRenderCommentMarkers(doc)` → `renderCommentMarkers(snapshot)`
//   `restoreLogicalSelection(root, selection)` — writes JSON to a data-attribute; no doc needed.
//
// `createApplyObjectFocusPolicyToElement({ applyObjectSelectionAccessibility? })` →
//   `applyObjectFocusPolicyToElement(element, selected, inst)` — applies focus-policy
//   classes/attributes to a DOM element. `applyObjectSelectionAccessibility` is an
//   optional injected inst-scoped helper (aria-describedby + resize-handle labels);
//   it is skipped when not provided.

import { asArray, sortObject } from '../core/helpers.mjs';
import { markOverlayNonText } from './render-helpers.mjs';

// ---------------------------------------------------------------------------
// Pure — no DOM, no injection
// ---------------------------------------------------------------------------

export function createObjectFocusPolicy(selected) {
    return sortObject({
        focusPolicy: 'selection-only',
        isTabStop: false,
        selected: selected === true,
        selectedClass: 'tm-wysiwyg-object--selected',
    });
}

// ---------------------------------------------------------------------------
// DOM overlay factories
// ---------------------------------------------------------------------------

export function createRenderSelectionOverlay(doc) {
    const d = doc || globalThis.document;
    return function renderSelectionOverlay(snapshot) {
        const overlay = markOverlayNonText(d.createElement('div'));
        overlay.setAttribute('data-render-overlay', 'selection');
        overlay.style.position = 'absolute';
        overlay.style.inset = '0';
        overlay.style.pointerEvents = 'none';
        const selection = snapshot && snapshot.selection;
        if (selection && selection.blockId) {
            const marker = markOverlayNonText(d.createElement('span'));
            marker.setAttribute('data-selection-block-id', selection.blockId);
            marker.setAttribute('data-selection-offset', selection.offset || 0);
            overlay.appendChild(marker);
        }
        return overlay;
    };
}

export function createRenderRevisionOverlay(doc) {
    const d = doc || globalThis.document;
    return function renderRevisionOverlay(snapshot) {
        const overlay = markOverlayNonText(d.createElement('div'));
        overlay.setAttribute('data-render-overlay', 'revision');
        overlay.className = 'tm-render-revision-overlay';
        overlay.style.position = 'absolute';
        overlay.style.inset = '0';
        overlay.style.pointerEvents = 'none';
        asArray(snapshot && snapshot.model && snapshot.model.revisions).forEach(function (revision) {
            const id = revision.id || revision.Id;
            if (!id) return;
            const marker = markOverlayNonText(d.createElement('span'));
            const type = revision.type || revision.Type || '';
            marker.className = 'tm-render-revision-marker revision-overlay';
            marker.setAttribute('data-testid', 'document-revision-marker');
            marker.setAttribute('data-revision-id', id);
            marker.setAttribute('data-revision-type', type);
            marker.textContent = '';
            overlay.appendChild(marker);
        });
        return overlay;
    };
}

export function createRenderCommentMarkers(doc) {
    const d = doc || globalThis.document;
    return function renderCommentMarkers(snapshot) {
        const overlay = markOverlayNonText(d.createElement('div'));
        overlay.setAttribute('data-render-overlay', 'comments');
        overlay.style.position = 'absolute';
        overlay.style.inset = '0';
        overlay.style.pointerEvents = 'none';
        asArray(snapshot && snapshot.model && snapshot.model.comments).forEach(function (comment) {
            const id = comment.id || comment.Id;
            if (!id) return;
            const marker = markOverlayNonText(d.createElement('span'));
            marker.className = 'tm-render-comment-marker';
            marker.setAttribute('data-testid', 'document-comment-marker');
            marker.setAttribute('data-comment-id', id);
            marker.textContent = '';
            overlay.appendChild(marker);
        });
        return overlay;
    };
}

// No DOM element created — writes a data attribute on the root for
// selection-restorer logic. Safe to call without a doc adapter.
export function restoreLogicalSelection(root, selection) {
    if (!root) return;
    root.setAttribute('data-logical-selection', JSON.stringify(sortObject(selection || {})));
}

// ---------------------------------------------------------------------------
// applyObjectFocusPolicyToElement factory
// ---------------------------------------------------------------------------

export function createApplyObjectFocusPolicyToElement(options) {
    const opts = options || {};
    // Optional: inst-scoped helper that sets aria-describedby + resize-handle labels.
    const applyObjectSelectionAccessibility = typeof opts.applyObjectSelectionAccessibility === 'function'
        ? opts.applyObjectSelectionAccessibility
        : null;

    return function applyObjectFocusPolicyToElement(element, selected, inst) {
        if (!element) return createObjectFocusPolicy(selected === true);
        const policy = createObjectFocusPolicy(selected === true);
        const objectLayerItem = element.classList
            && typeof element.classList.contains === 'function'
            && element.classList.contains('tm-wysiwyg-object-layer-item');
        if (typeof element.removeAttribute === 'function') element.removeAttribute('tabindex');
        if (typeof element.setAttribute === 'function') {
            element.setAttribute('data-object-focus-policy', policy.focusPolicy);
            element.setAttribute('aria-selected', policy.selected ? 'true' : 'false');
            if (policy.selected) {
                element.setAttribute('data-object-selected', 'true');
            } else if (typeof element.removeAttribute === 'function') {
                element.removeAttribute('data-object-selected');
            }
        }
        if (element.classList && typeof element.classList.toggle === 'function') {
            if (objectLayerItem) {
                element.classList.toggle('tm-wysiwyg-object-layer-item--selected', policy.selected);
                element.classList.remove('tm-wysiwyg-object--selected');
                element.classList.remove('tm-wysiwyg-image--selected');
            } else {
                element.classList.toggle('tm-wysiwyg-object--selected', policy.selected);
                element.classList.toggle('tm-wysiwyg-image--selected', policy.selected);
            }
        }
        if (applyObjectSelectionAccessibility) {
            applyObjectSelectionAccessibility(inst, element, policy.selected);
        }
        return policy;
    };
}
