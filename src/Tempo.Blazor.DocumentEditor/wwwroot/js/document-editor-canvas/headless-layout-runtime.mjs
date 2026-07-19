// Headless layout runtime glue — the single data-in/data-out seam the server-side JS host
// (Jint in Tempo.Blazor.DocumentFormats) calls. One JSON request in, one JSON result out, no
// host callbacks (text measurement runs entirely inside JS from precomputed Skia advance
// tables — see font-advance-metrics.mjs).
//
// Request shape:
//   {
//     model:          serialized CanvasDocumentModel (camelCase wire shape) — normalized here
//                     via createCanvasDocumentModel, so partial/foreign input is tolerated;
//     fontTables:     advance-table document from TempoFontAdvanceTableExtractor (optional —
//                     without it the deterministic synthetic metrics measure the layout and
//                     diagnostics.fontTablesProvided reports false);
//     reviewDisplayMode: optional tracked-changes print mode (allMarkup/simpleMarkup/...).
//   }
//
// Result shape:
//   {
//     schemaVersion: 1,
//     pageCount,
//     snapshot,      // layout snapshot schema v1 — the DocumentPdfExportRequest.LayoutSnapshotJson contract
//     diagnostics: { fontTablesProvided, unknownFamilies: [...], missingGlyphs: [{family, codePoint}] },
//   }
//
// Fail-closed decisions (unknown font ⇒ error) belong to the C# caller: this layer measures,
// lays out and REPORTS; it never silently swallows a fallback.

import { createCanvasDocumentModel } from './model/canvas-document-model.mjs';
import { buildDisplayList } from './render/display-list.mjs';
import {
    translateDisplayListToLayoutSnapshot,
    collectRedactedRunIds,
} from './render/layout-snapshot-export.mjs';
import { createAdvanceFontMetricsService } from '../document-editor/layout/font-advance-metrics.mjs';

export function generateHeadlessLayoutSnapshot(request = {}) {
    const model = createCanvasDocumentModel(request.model || {});
    const fontTablesProvided = !!request.fontTables;
    const fontMetrics = fontTablesProvided
        ? createAdvanceFontMetricsService(request.fontTables)
        : null;

    const displayList = buildDisplayList(
        model,
        { pageSettings: model.pageSettings },
        {
            fontMetrics: fontMetrics || undefined,
            reviewDisplayMode: request.reviewDisplayMode,
        });

    const snapshot = translateDisplayListToLayoutSnapshot(displayList, {
        revisions: model.revisions,
        reviewDisplayMode: request.reviewDisplayMode,
        redactedRunIds: [...collectRedactedRunIds(model)],
    });

    const advanceDiagnostics = fontMetrics
        ? fontMetrics.getAdvanceDiagnostics()
        : { unknownFamilies: [], missingGlyphs: [] };

    return {
        schemaVersion: 1,
        pageCount: snapshot.pageCount,
        snapshot,
        diagnostics: {
            fontTablesProvided,
            unknownFamilies: advanceDiagnostics.unknownFamilies,
            missingGlyphs: advanceDiagnostics.missingGlyphs,
        },
    };
}

// String-in/string-out variant — the exact function the Jint host invokes.
export function generateHeadlessLayoutSnapshotJson(requestJson) {
    return JSON.stringify(generateHeadlessLayoutSnapshot(JSON.parse(String(requestJson || '{}'))));
}
