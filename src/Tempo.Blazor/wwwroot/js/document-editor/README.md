# `document-editor/` JS modules — Phase D

This directory hosts the ES-module port of the legacy monolithic
`../document-editor-wysiwyg.js` (≈27 000 lines). Extraction is incremental: each
module added here removes its inline copy from the monolith once equivalence is
verified.

Build pipeline:

```bash
npm install                       # one-time
npm run build:document-editor     # produces ../document-editor.dist.js
```

## Module layout (target — D2)

| Folder            | Responsibility                                            | Target size  |
|-------------------|-----------------------------------------------------------|--------------|
| `core/`           | helpers, schema registry, model importers/exporters       | ~3 000 lines |
| `history/`        | operation types, command stack, transactions              | ~2 500 lines |
| `layout/`         | paragraphEngine, page metrics, segment generator          | ~5 000 lines |
| `render/`         | atomic renderer, segment patcher                          | ~4 000 lines |
| `input/`          | beforeInput, keydown, composition                         | ~2 500 lines |
| `clipboard/`      | paste pipeline, copy serializer                           | ~1 500 lines |
| `objects/`        | image, table, drawing (lazy-loaded on first object op)    | ~3 000 lines |
| `collaboration/`  | remote ops, CRDT (lazy-loaded on connect)                 | ~2 000 lines |
| `accessibility/`  | announcements, screen-reader help                         | ~500 lines   |
| `runtime/`        | entry point, watchdog, instance manager                   | ~2 000 lines |

## Status (2026-05-27 pass 6)

Migrated:
- `core/helpers.mjs` — generic value helpers (hasOwn, clone, shallowClone, …)
- `core/schema.mjs` — `DocumentSchemaRegistry` + `createDefaultSchemaRegistry`
- `core/text-helpers.mjs` — `blockText`, `isEditableTextBlock`, `clampTextBoundary`, `clampTextRange`, `tableColumnCount`
- `core/model-finders.mjs` — `findBlockContainer`, `findCell`, `findTableInfo*`, `findTableBlockByScan`
- `core/normalize-target.mjs` — `normalizeTarget`, `normalizeRange`, `normalizeTextExclusionColumnIndex`
- `core/marks.mjs` — full mark family: `MarkTypeNames`, `markType/Value/Order/SortKey/Key`, `normalizeMark(s)`, `updateMarks`, `readInlineMarkType`, `readCommentIdFromMark`, `readCommentIdsFromRun`, `readRevisionId*`
- `core/export-types.mjs` — 12 enum mappers for C#-JSON wire format (`exportBlockType`, `exportHeaderFooterType/Scope`, `exportFieldType`, `exportCommentAnchorType/Status/Visibility`, `exportRevisionType/Action/Author`, `exportTextAlignment`, `exportDateTimeOffset`)
- `core/inline-runs.mjs` — complete inline run pipeline (`isDrawingRunSource`, `normalizeDrawingRun`, `importInlineRun`, `exportInlineRun`, `normalizeTextRunForMerge`, `mergeAdjacentTextRuns`, `plainRuns`)
- `history/operation-types.mjs` — `OperationTypes`, `TransactionTypes`, `isTypingLikeTransactionType`
- `history/id-counters.mjs` — `createIdCounters`
- `history/operations.mjs` — `createOperationsModule({ idCounters })` factory + pure classifiers (`supportsOperationHistory`, …)
- `layout/scope-kinds.mjs` — `LayoutScopeKinds`
- `layout/layout-scope.mjs` — `createLayoutScope`, `inferLayoutScopeFromOperation` (per-op-type scope inference)
- `layout/page-metrics.mjs` — `normalizePageBox`, `normalizePageLayoutSettings`, `createPageLayout`, `createPageBreakLayout`, shift helpers (`shiftRectY`, …), field text resolution (`resolveFieldRunText`, `cloneBlockWithResolvedFields`)
- `objects/wrap-modes.mjs` — wrap mode + wrap side enums and normalizers (full legacy aliases) + `wrapSideToValue`
- `objects/drawing-kind.mjs` — `normalizeDrawingKindName`, `exportDrawingKind`
- `runtime/entry.mjs` — bundler entry, re-exports the migrated set

Still pending (large refactor — see plan §6):
- `core/model-import.mjs`, `core/model-export.mjs`, `core/validate.mjs`, `core/indexes.mjs`
- `history/command-stack.mjs`, `history/transactions.mjs`
- `layout/paragraph-engine.mjs`, `layout/segment-generator.mjs`
- `render/atomic-renderer.mjs`, `render/segment-patcher.mjs`
- `input/before-input.mjs`, `input/keydown.mjs`, `input/composition.mjs`
- `clipboard/paste.mjs`, `clipboard/copy.mjs`
- `objects/image.mjs`, `objects/table.mjs`, `objects/drawing.mjs`
- `collaboration/remote-ops.mjs`, `collaboration/crdt.mjs`
- `accessibility/announcements.mjs`
- `runtime/instance-manager.mjs`, `runtime/watchdog.mjs`

The legacy IIFE remains the production source until the extraction reaches a
critical mass; the bundle output (`document-editor.dist.js`) is currently a
verification artifact only.
