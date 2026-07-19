# Document MCP tool catalog (`Tempo.Blazor.Mcp`)

Catalog of every MCP tool the document editor exposes (`TempoDocumentEditorMcp.ToolTypes`).
Semantic tools compile into canonical operations (`docs/document-operations-semantics.md`) and
apply them through `DocumentOperationApplier` + provider save — the same convergence-tested path
as `document_editor_apply_operations`. Addressing follows
`docs/document-mcp-addressing.md`; the model reference is `docs/document-canonical-model.md`.
The drift guard (`DocumentMcpToolsDocumentationDriftTests`) fails when a registered tool has no
`### \`name\`` section here (or a section documents a tool that no longer exists).

## Addressing & concurrency in one paragraph

Blocks are addressed by stable `blockId` (+ `tableCellId` for table-cell-nested blocks); text
ranges are `offset`/`length` in the block's **plain text** (text runs only — identical to
`document_editor_search_text` coordinates). Every read tool returns `concurrencyToken`
(authoritative write guard — pass as `expectedConcurrencyToken`) and `contentDigest` (SHA-256
content fingerprint); every write returns the new token + digest, so an agent can chain edits
without re-reading. Blocks inside content controls and headers/footers are described but not
operation-addressable (`operationAddressable: false`).

## The agent loop

```
document_editor_create / document_editor_import (markdown je primární autoring)
→ document_editor_describe_document          (adresy bloků, textLength, tokeny)
→ sémantické edity (insert_text, format_range, insert_block, insert_token, …)
→ document_render_preview                    (vizuální kontrola PNG)
→ document_editor_export("markdown")         (textová verifikace)
→ document_render_pdf                        (finální výstup)
```

DI setup: `AddTempoDocumentEditorMcpRendering(...)` (font catalog + preview limits, required by
the render/assemble tools), optionally `AddTempoDocumentEditorMcpCollaboration(o => o.Enabled =
true)` for the live co-editing bridge; the host must register `IDocumentEditorProvider`.

## Introspection & reading

### `document_editor_describe_document`
Structured overview for agents: every block with its address (container, blockId, tableCellId,
operationAddressable), type, truncated plain text + `textLength`, tables with cell ids, inline
objects (tokens/fields/drawings…), aggregated tokens, content controls incl. `tmAssembly:*`
metadata, headers/footers, statistics, `concurrencyToken` + `contentDigest`.

### `document_template_describe`
Template view: aggregated tokens (key, expression, fallback, occurrences), conditional
IF/ELSEIF/ELSE chains (group, ordered branches with expressions) and repeating sections
(bindKey, row template block count).

### `document_editor_get_document`
Full typed document JSON + optional raw snapshot + `concurrencyToken`.

### `document_editor_get_json`
Raw JSON snapshot only.

### `document_editor_get_outline`
Heading outline (levels + text) for navigation.

### `document_editor_get_versions`
Saved versions (id, kind, label, author, snapshot hash) for diff/restore flows.

### `document_editor_search_text`
Text/regex search over body, headers/footers and comments; results carry
`blockId` + `blockTextOffset`/`length` in the same plain-text space the semantic edit tools use.

### `document_editor_validate_document`
Validates a full document JSON (schema, blocks, tables, images, comments, revisions).

## Semantic text edits

### `document_editor_insert_text`
Insert text at a plain-text offset. Boundary offsets bind to the earliest run; empty blocks get
a fresh run.

### `document_editor_replace_text`
Replace a plain-text range (`length=0` behaves as insert). Compiled as per-run deleteText
segments + insertText at the range start.

### `document_editor_delete_text`
Delete a plain-text range; tokens/fields inside the range are preserved (they occupy no plain
text).

### `document_editor_format_range`
Add/remove one of 17 formatting marks (bold…link) on a plain-text range; runs split at the
boundaries, tokens fully inside the range are marked too. Value-carrying marks require `value`.

### `document_editor_set_heading`
Convert a text block to a heading (level 1-6), preserving inlines.

### `document_editor_set_paragraph_properties`
Patch alignment/lineSpacing/spacing/indents of a text block; only supplied values change.

## Blocks & tables

### `document_editor_insert_block`
New paragraph/heading/list/quote in the body (order-value semantics, fractional insert) or a
table cell (index semantics). Returns the new `blockId`; explicit duplicate ids are rejected.

### `document_editor_delete_block`
Delete a body or table-cell block (empty cells get a placeholder paragraph via the post-fixer).

### `document_editor_move_block`
Body: order-value with moved-block-wins-ties (order 0 puts it first). Table cell: target index.

### `document_editor_update_block`
Whole-block replace with a full persistence `DocumentBlock` JSON payload (id forced to the
addressed block).

### `document_editor_set_table_cell_text`
Replace a table cell's text (first paragraph gets a single run; created when the cell is
empty). Targets the TABLE block id + `tableCellId`.

## Authoring: create, import, export

### `document_editor_create`
New empty document with one addressable paragraph (`firstBlockId`), title, landscape, optional
`pageSettingsJson`.

### `document_editor_import`
Markdown/HTML (text) or DOCX/ODT (base64) → new document, or content replacement of an existing
one under the concurrency token. Markdown is the primary agent authoring path: import rough
content, refine with semantic edits.

### `document_editor_export`
Markdown/HTML text (agent verification channel) or DOCX/ODT base64 packages, paired with
`concurrencyToken` + `contentDigest`.

## Templates & assembly

### `document_editor_insert_token`
Insert a `TokenRun` (key, displayName, tokenType, fallbackText, optional computed `expression` —
SUM/COUNT/CURRENCY/TODAY/DATEADD…) at a plain-text offset; the key is validated against the
host `IDocumentTokenValueProvider` when registered (`validateKey=false` skips).

### `document_editor_wrap_conditional`
Wrap top-level body blocks into an IF/ELSEIF/ELSE content-control chain
(`branchesJson`), or update the branch/expression of an existing conditional control
(`existingControlBlockId`). At assembly the first truthy branch survives.

### `document_editor_insert_repeating_section`
Repeating section bound to a collection token (`bindKey`); row template as `rowText` (single
paragraph) or `rowBlocksJson` (full block payloads with item tokens).

### `document_assemble_render`
Template + `tokenValuesJson` (scalars, `{value, displayValue}` objects, `{rows: [...]}`
collections) → assembled PNG previews or PDF (IF/ELSE evaluation, repeat expansion, computed
expressions over the injected clock). `includeLayoutText` returns the laid-out text for
verification.

## Visual feedback

### `document_render_preview`
Per-page base64 PNG previews (page selection `'1,3-5'`, dpi 24-600, `maxPages` cap). Fails
closed with a font-catalog diagnostic when the configured fonts cannot measure the document.

### `document_render_pdf`
WYSIWYG PDF with `DocumentPdfExportOptions` passthrough (page setup, review display mode,
comments/suggestions toggles, forensic watermark).

## Versions, diff & redline

### `document_editor_save_document`
Save a full normalized document JSON snapshot under the concurrency token.

### `document_editor_replace_document`
Alias of save for clients that distinguish replace from save.

### `document_editor_restore_version`
Restore a saved version snapshot back onto the document.

### `document_editor_diff_versions`
Two saved versions — or a version vs. the CURRENT state — as a structured diff: summary
counters, added/removed/changed blocks with word-level diff segments, `redlineAvailable` flag.

### `document_editor_export_redline`
The diff as a redline: DOCX with real `w:ins`/`w:del` tracked changes, or PDF rendered with
review markup. Identical versions refuse the export.

## Low level

### `document_editor_apply_operations`
Raw `DocumentOperationBatch` (or an operations array) under the concurrency token — the escape
hatch the semantic tools compile into. Prefer the semantic tools.

## Live co-editing bridge (opt-in)

Not a tool — a DI option: `AddTempoDocumentEditorMcpCollaboration(o => o.Enabled = true)` makes
every semantic write also publish its operation batch to the host collaboration stream
(`IDocumentCollaborationProvider` join as a named participant with presence name+color,
`IDocumentCollaborationBackplane` envelope with `SourceInstanceId` against echo). Humans with
the document open see agent edits live; publishing is FAIL-OPEN (a broken backplane logs and
never fails the edit). Write responses report `collaborationPublished`.

## Related references

- Addressing contract: `docs/document-mcp-addressing.md`
- Canonical model: `docs/document-canonical-model.md`
- Operation semantics: `docs/document-operations-semantics.md`
- Applier convergence: `docs/document-operation-applier-coverage.md`
- E2E agent loops: `scripts/e2e-document-mcp-preview.mjs`, `scripts/e2e-document-mcp-live-coedit.mjs`
