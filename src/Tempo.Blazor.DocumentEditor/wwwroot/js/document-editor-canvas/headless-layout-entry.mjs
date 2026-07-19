// Headless layout runtime entry (Phase 0, headless document runtime plan).
//
// This is the esbuild entry for the server-side bundle embedded in
// Tempo.Blazor.DocumentFormats (built via `npm run build:document-editor`). It packages
// the exact layout chain the canvas editor paints with —
// buildLayoutSnapshotExport → buildDisplayList → layoutCanvasDocument (pagination,
// line breaker, paragraph engine) → translateDisplayListToLayoutSnapshot — so a JS
// engine on the server (Jint/Node) produces the SAME layout snapshot the browser
// exports: WYSIWYG parity by construction, no C# port of the layout code.
//
// Deliberately excluded: interop.mjs and the paint/canvas stack (browser-only).
// Text measurement is injectable — pass `options.fontMetrics` (a `{ measureRun }`
// partial) or build a service with `createFontMetricsService({ createMeasureContext })`;
// without a canvas context the service falls back to deterministic synthetic metrics.

export {
    buildLayoutSnapshotExport,
    translateDisplayListToLayoutSnapshot,
    collectRedactedRunIds,
} from './render/layout-snapshot-export.mjs';
export { buildDisplayList } from './render/display-list.mjs';
export { layoutCanvasDocument } from './layout/pagination.mjs';
export { DEFAULT_PAGE_SETUP, normalizePageSettings } from './layout/page-geometry.mjs';
export {
    createFontMetricsService,
    normalizeFontMetricStyle,
    fontStringFromStyle,
    syntheticRunMetrics,
    computeFontMetricKey,
} from '../document-editor/layout/font-metrics.mjs';
export {
    parseFontAdvanceTable,
    createFontAdvanceMeasureContext,
    createAdvanceFontMetricsService,
} from '../document-editor/layout/font-advance-metrics.mjs';
