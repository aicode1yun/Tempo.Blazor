// Phase D — layout/scope-kinds.mjs
// Layout scope enum used by the rendering pipeline to decide how much of the document
// must be re-laid-out after a given operation (single paragraph, page region, whole doc).

export const LayoutScopeKinds = Object.freeze({
    ActiveParagraph: 'activeParagraph',
    WholeBlock: 'wholeBlock',
    PageRegion: 'pageRegion',
    WholeDocument: 'wholeDocument',
});
