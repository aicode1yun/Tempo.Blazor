# Server-side operation applier — coverage audit (headless runtime, Fáze 5)

Audit of the canonical document operation model across the three cooperating components:

- **C# applier** — `DocumentOperationApplier` (Tempo.Blazor.Abstractions): applies operations to a
  `DocumentEditorDocument`. The server-side source of truth for MCP tooling (plan C).
- **JS applier** — `document-editor-canvas/collaboration/transform.mjs` `applyOperation`: applies
  remote operation batches to the live canvas model.
- **Resolver** — `DocumentOperationConflictResolver`: deterministically orders/transforms
  concurrent operations before application.

Convergence contract: applying the same batch through the C# applier and the JS applier must
yield the same document CONTENT (block order, text, heading levels, per-character mark ranges) —
run identities may differ, both sides split/merge runs independently. Pinned by the committed
fixture `tests/Tempo.Blazor.Tests/DocumentEditor/TestData/operation-convergence-fixture.json`
(C# side: `DocumentOperationConvergenceFixtureTests`; JS side:
`scripts/operation-convergence.test.mjs`; regenerate with
`TEMPO_REGENERATE_OPERATION_CONVERGENCE_FIXTURE=1`).

## Operation type coverage

| Operation | C# applier | JS applier | Resolver transform | Nested (table cell) targets | Convergence-tested |
|---|---|---|---|---|---|
| `insertText` | ✅ single-run offset insert (`EnsureTextRun`) | ✅ multi-run offset insert | ✅ transforms vs. concurrent inserts/deletes | ✅ both (deep search + `TableCellId` preference, Fáze 5) | ✅ |
| `deleteText` | ✅ single-run range delete | ✅ multi-run range delete | ✅ dedup + shifts | ✅ both (Fáze 5) | ✅ |
| `addInlineMark` / `addMark` | ✅ character-range with run splitting/merging | ✅ character-range with run splitting (Fáze 5 fix — previously whole-run) | ✅ range transform vs. text edits | ✅ both (Fáze 5) | ✅ |
| `removeInlineMark` / `removeMark` | ✅ incl. run merge-back | ✅ range-based | ✅ range transform | ✅ both (Fáze 5) | ✅ |
| `insertBlock` | ✅ body (order-value: append + stable sort) + table cell containers (index, Fáze 5) | ✅ body (order-value, mirrors C#) + cell containers (index); persistence payloads normalized to canvas shape incl. content controls (plan 3 Fáze 5) | ✅ passthrough (id dedup by applier) | ✅ both | ✅ (incl. persistence payloads + cell containers; list/quote payloads — plan 3 Fáze 2; content-control payloads with assembly metadata + nested blocks — plan 3 Fáze 5) |
| `deleteBlock` | ✅ body + nested (Fáze 5) | ✅ deep | ✅ suppresses later ops on deleted block | ✅ both | ✅ |
| `moveBlock` | ✅ body (order-value, moved-block-wins-ties) / nested containers (index, source-container stays without explicit cell id) | ✅ body (order-value, moved-block-wins-ties — mirrors C#) / nested containers (index; source container stays — plan 3 follow-up fix) | ✅ last-write-wins per block | ✅ both (cells + content controls) | ✅ (body order-value + cell index + in-control moves) |
| `setBlockAttribute` | ✅ `headingLevel`, `text`, `paragraphProperties`, `clearFormatting`, `table.cell.text`, `order`, `metadata.title` | ✅ `headingLevel`, `text`, `table.cell.text` (plan 3 follow-up — cell-targeting semantics incl. the deterministic `{cellId}-text` paragraph for empty cells), `content.*` paths | ✅ last-write-wins per (block, attribute) | ✅ both (`table.cell.text` keeps its table-targeting semantics on both runtimes) | ✅ (`headingLevel`, `text`, `table.cell.text` replace + empty-cell create) |
| `updateBlock` | ✅ body + nested in-place (Fáze 5) | ✅ deep replace; persistence payloads normalized to canvas shape incl. content controls (plan 3 Fáze 5) | ✅ last-write-wins per block | ✅ both | ✅ (incl. persistence + content-control payloads) |
| `moveDrawingObject` | ✅ by objectId/inlineId/index across body, headers/footers, nested blocks | ✅ | ✅ last-write-wins per object | ✅ (deep `FindDrawingRun`) | — (layout payload, no text content) |
| `createRevision` | ✅ pending markup incl. formatting revisions | ❌ not in `applyOperation` (revisions reach the canvas via model replace / engine commands) | ✅ passthrough | n/a | — (C#-only in the collab applier) |
| `acceptRevision` | ✅ | ❌ (as above) | ✅ first-decision-wins | n/a | — |
| `rejectRevision` | ✅ | ✅/❌ (as above) | ✅ first-decision-wins | n/a | — |

## Findings from the Fáze 5 audit (and what was done)

1. **C# applier could not address nested table-cell content** — `FindBlock` searched only
   top-level `document.Blocks`, while the JS applier resolves deeply with the table cell id as a
   container preference (`findBlockLocation`/`findContainer`). Fixed: the C# applier now mirrors
   the JS resolution (`FindBlockLocation`/`FindContainerBlocks`) for text, mark, block, attribute
   and update operations (`DocumentOperationApplierNestedTests`, 9 tests). The
   `table.cell.text` attribute keeps its historical semantics (targets the TABLE block, cell id
   points inside it).
2. **JS collab applier applied partial-range marks to whole runs** — `mutateRunsInRange` mutated
   every intersecting run without splitting, so a remote bold over 3 characters bolded the whole
   run. Fixed: runs now split at the range boundaries (head/tail keep formatting, middle keeps
   the run id) — mirrors the C# `ApplyMarkAbsoluteRange` semantics and the real engine commands.
3. **RESOLVED — body-level `moveBlock`/`insertBlock` semantics**: C# modeled the body as
   `Order` values while JS spliced by index — fractional/large order values from C#-produced
   operations landed wrong on the JS side. Both appliers now share ORDER-VALUE semantics for
   the body with deterministic tie-breaks (moved block sorts before equal orders; inserted
   block after equal orders — matching C#'s append + stable sort) and index semantics for
   table-cell containers. Nested moves without an explicit cell id now stay in their source
   cell on the JS side too.
4. **RESOLVED — `insertBlock`/`updateBlock` payload shape**: the JS applier now detects
   persistence-shaped payloads (`content.$type`/`inlines`) and converts + normalizes them into
   canvas blocks (`content.runs`, `headingLevel`, `content.table`) on apply; canvas-shaped
   payloads pass through untouched (free-form block props survive as before). Blocks carried by
   C#-produced operations are fully text-editable on the JS side.
5. **RESOLVED — `setBlockAttribute table.cell.text` now converges** (plan 3 follow-up): the JS
   collab applier implements the cell-targeting semantics (resolve the TABLE block with a
   fallback WITHOUT the cell preference, replace the first cell paragraph's runs, convert a
   non-paragraph first block, or create a paragraph in an empty cell). Both runtimes create the
   empty-cell paragraph with the DETERMINISTIC id `{cellId}-text` — the previous random C# Guid
   was itself a cross-replica divergence. Pinned by the convergence fixture (replace +
   empty-cell create patterns) and unit tests on both sides.
6. **RESOLVED — content-control children are operation-addressable** (plan 3 follow-up): both
   `FindBlockLocation` (C#) and `findBlockLocation`/`walkBlocks` (JS) descend
   `ContentControlBlockContent.Blocks` like table cells (keeping the enclosing cell context for
   the `TableCellId` preference), so text/mark/attribute/update/delete/move operations reach
   template-section content directly. The JS `moveBlock` was also aligned: without an explicit
   cell id a nested block now stays in its SOURCE container (previously it was re-homed to the
   body). Pinned by the convergence fixture (insertText/deleteText/mark range on a conditional
   chain child) and `DocumentOperationApplierContentControlTests`.
7. **Revision operations are C#-only in the collab applier** — the canvas receives revision state
   via model replacement/engine commands, not via `applyOperation`. Server-side revision
   application is fully covered by `DocumentOperationEngineTests`.

## Test map

| Suite | What it pins |
|---|---|
| `DocumentOperationEngineTests` (~60 tests) | Per-type C# applier behavior incl. marks across runs, revisions, tables, drawings |
| `DocumentOperationApplierNestedTests` (9) | Nested table-cell targeting parity with the JS resolution (Fáze 5) |
| `DocumentOperationConflictResolverTests` + `Extended` | Deterministic ordering, OT transforms, content convergence of concurrent orders |
| `DocumentOperationConvergenceFixtureTests` + `scripts/operation-convergence.test.mjs` | C#↔JS content convergence over seeded batches (Fáze 5) |
| `collaboration/__tests__/*` (Node) | JS applier behavior incl. the range-splitting mark fix |
