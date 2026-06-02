// Phase D — render/review-display-mode.mjs
// `normalizeReviewDisplayModeClass(mode)` — maps a review display-mode name (any
//   casing / separators) to the host CSS class: `simple-markup`, `no-markup`,
//   `original`, else `all-markup`.
// `applyReviewDisplayModeClass(root, mode)` — removes all review-mode host classes
//   from `root` and adds the one for `mode`. No-op when `root` has no classList.

const REVIEW_MODE_CLASSES = [
    'tm-wysiwyg-host--review-all-markup',
    'tm-wysiwyg-host--review-simple-markup',
    'tm-wysiwyg-host--review-no-markup',
    'tm-wysiwyg-host--review-original',
];

export function normalizeReviewDisplayModeClass(mode) {
    const normalized = String(mode || 'AllMarkup').replace(/[\s_.:-]+/g, '').toLowerCase();
    if (normalized === 'simplemarkup') return 'tm-wysiwyg-host--review-simple-markup';
    if (normalized === 'nomarkup') return 'tm-wysiwyg-host--review-no-markup';
    if (normalized === 'original') return 'tm-wysiwyg-host--review-original';
    return 'tm-wysiwyg-host--review-all-markup';
}

export function applyReviewDisplayModeClass(root, mode) {
    if (!root || !root.classList) return;
    REVIEW_MODE_CLASSES.forEach(function (className) {
        root.classList.remove(className);
    });
    root.classList.add(normalizeReviewDisplayModeClass(mode));
}
