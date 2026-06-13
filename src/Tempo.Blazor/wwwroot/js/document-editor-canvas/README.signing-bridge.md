# Signing bridge — canvas engine modules

Modules added for the TmDocumentEditor ↔ Signing bridge (plán
`planning/tm-documenteditor-signing-bridge-tdd-todo-2026-06-12.md`, fáze S1+S2). All are plain ESM,
loaded directly (no bundle build); Node tests are co-located `*.test.mjs`.

## S1 — page image export

- **`render/page-image-export.mjs`** — flattens a document into one opaque bitmap per page.
  `renderPageToCanvas(model, layout, pageIndex, {scale, createCanvas})` /
  `renderDisplayListPageToCanvas(displayList, pageIndex, opts)` reuse `buildDisplayList` +
  `paintDisplayList`, painting only the printable layers (`page-background`/`content`/`objects`) —
  never the editing chrome (caret, comments, diagnostics). `clampExportScale` keeps scale in 1–3
  (default 2). Canvas factory is injected, so it runs under Node.
- **`interop.mjs`** — `exportPageImages(handle, optionsJson)` / `exportPageImage(handle, pageIndex,
  optionsJson)` reuse the engine's CURRENT display list, so every page (incl. virtualized) exports.

## S2 — inline signing fields

A new run type `signingField` (`{type:'signingField', signingField:{uuid, fieldType, submitterUuid,
required, label, boxWidth, boxHeight, options}}`). Position-agnostic — identical in a body block or a
header/footer block; `uuid` is the key that groups per-page layout occurrences into the field's areas.

- **`controls/signing-field-model.mjs`** — `normalizeSigningFieldRun`, `createSigningFieldRun`,
  `SIGNING_FIELD_TYPES` (mirrors `SigningFieldType` minus heading/strikethrough), per-type default box,
  `resolveSigningRoleColor` + fallback palette.
- **`model/canvas-document-model.mjs`** — `signingField` added to `CANVAS_RUN_TYPES` + `normalizeRun`.
- **Layout (atomic inline box):** body via the shared `document-editor/layout/paragraph-tokenizer.mjs`
  + `paragraph-runs.mjs` + `paragraph-engine.mjs` (additive `signingField` branch — legacy unaffected,
  it never emits the run); header/footer via `layout/header-footer-layout.mjs` (box clamped to the
  region, one command per page).
- **`render/signing-field-render.mjs`** — paints the box (role colour tint + border, icon, label,
  required marker, selected focus ring); wired into `render/canvas-renderer.mjs` (`case 'signingField'`)
  and `render/display-list.mjs` (emits the command + a `roleColor` resolve pass for body + HF).
- **`commands/signing-field-commands.mjs`** — `insertSigningField` / `updateSigningField` /
  `removeSigningField` (undo/redo + collab via the dispatcher). Insert/update/remove operate on EVERY
  block copy sharing the id (the canvas model duplicates body content in `body.blocks` and
  `sections[].blocks`, and the layout renders the section copy).
- **`controls/signing-field-selection.mjs`** — `findSigningFieldAtSelection` → the field at the caret
  (+ `headerFooterId`/`scope`/`repeats`); surfaced in `getSelectionStateJson` for the properties popover.
- **`controls/signing-field-areas.mjs`** — `extractSigningFields(displayList)` groups commands by
  `fieldUuid` → one field with one normalized 0..1 area per page occurrence (body 1, header/footer N;
  scope first/even/odd honoured because the layout only emits where the field actually renders).
  Surfaced as `interop.getSigningFieldsJson`.
- **`signingRoles`** option threaded `setOptions`/mount → `entry.mjs` → `canvas-stack.mjs` →
  `buildDisplayList` for per-role box colours.
