# Semantic addressing contract for document MCP tools

How agents address blocks and text ranges in a `DocumentEditorDocument` when talking to the
document MCP tools (`document_editor_*`). Consistent with the canonical model
(`docs/document-canonical-model.md`) and the operation semantics
(`docs/document-operations-semantics.md`); the introspection entry point that hands out these
addresses is `document_editor_describe_document`
(`src/Tempo.Blazor.Mcp/DocumentEditor/DocumentEditorDescribeTools.cs`).

## Stable block addresses

Every block carries a stable `Id` (`DocumentBlock.Id`) that survives edits, saves and both wire
formats — **`blockId` is the primary address**. Because blocks nest (table cells and content
controls host their own block lists), an address also names its container:

```
address
├─ container               body | tableCell | contentControl | headerFooter
├─ blockId                 the addressed block
├─ tableBlockId?           table block hosting the cell (container = tableCell)
├─ tableCellId?            TableCellContent.Id hosting the block (container = tableCell)
├─ contentControlBlockId?  contentControl block hosting the block (container = contentControl)
├─ headerFooterId?         DocumentHeaderFooter.Id hosting the block (container = headerFooter)
└─ operationAddressable    whether document_editor_apply_operations can target this block
```

- **Body blocks** (`container: body`) are ordered by `Order` (double) then `Id`;
  `describe_document` returns them in that order. Address = `blockId` alone.
- **Table-cell blocks** (`container: tableCell`) live in `TableCellContent.Blocks`. The cell id
  is stable and unique per document. When targeting such a block in an operation, set
  `Target.BlockId` = the nested block id and `Target.TableCellId` = the cell id — the applier
  resolves body blocks first, then recursively through table cells, and `TableCellId` restricts
  which container may match (see the target-resolution invariant in
  `docs/document-operations-semantics.md`). Cells nested in cells (a table inside a cell) chain
  naturally: the innermost cell id wins.
- **Content-control children** (`container: contentControl`) live in
  `ContentControlBlockContent.Blocks` (template sections: conditional chains, repeating
  sections). They are fully operation-addressable by `blockId` — both appliers
  (`DocumentOperationApplier.FindBlockLocation` and the JS `findBlockLocation`) descend through
  content controls the same way they descend through table cells, keeping the enclosing cell
  context for the `TableCellId` preference. A `moveBlock` without an explicit cell id stays in
  its source container (list-index semantics), matching table-cell children.
- **Header/footer blocks** (`container: headerFooter`) live in
  `DocumentHeaderFooter.Blocks`. These are described but `operationAddressable: false`;
  the only operation that resolves into headers/footers is `moveDrawingObject` (by `ObjectId`).
  Header/footer content is otherwise edited through full-document save.

`operationAddressable` is therefore: `true` for body blocks and blocks reachable from the body
through table cells and content controls (recursively), `false` only for header/footer
subtrees.

## Inline addresses

Inline runs inside a text-like block are addressed by `inlineIndex` (position in the block's
`Inlines` list) or by the optional stable `inlineId` (`InlineContent.Id`, set by editor
runtimes for selection mapping). Run identity is NOT content for text runs — appliers split and
merge `TextRun`s freely — so **agents should prefer plain-text offsets (below) for text and
`inlineIndex`/`inlineId` only for object runs** (tokens, fields, drawings, content controls,
signing fields), which are atomic and keep their identity.

`describe_document` lists non-text runs per block under `objects` with `kind`, `inlineIndex`,
`inlineId` and kind-specific keys (token `key`, field `fieldType`, drawing `objectId`, …).

## Text ranges: offset/length in block plain text

A text range is `{ blockId, offset, length }` measured in the block's **plain text**:

> **Plain text of a block = concatenation of its `TextRun.Text` values, in inline order.**
> Non-text inlines (token, field, noteReference, drawing, math, contentControl, signingField)
> contribute **zero** characters.

This matches `DocumentSearchService` (`document_editor_search_text` returns
`BlockTextOffset`/`Length` in exactly this coordinate space), so search results can be fed back
into semantic edit tools unchanged. Offsets are UTF-16 code-unit indices (C# `string`
semantics), 0-based; `offset + length` never exceeds the block's `textLength` reported by
`describe_document`.

Semantic tools compile a plain-text range into low-level operation targets by walking the
inline list: skip non-text runs, subtract each text run's length from the offset until the
target run is found — yielding the `(InlineIndex, Offset)` pair the operation envelope uses.
A range spanning several text runs compiles into per-run operations (or a range mark
operation, which splits runs at the boundaries itself). The implementations live in
`DocumentEditorSemanticTextTools` (`document_editor_insert_text` / `replace_text` /
`delete_text` / `format_range` / `set_heading` / `set_paragraph_properties`). Two details of
that compilation:

- An insert offset that falls on a boundary between text runs binds to the EARLIEST run
  (append at its end); when a non-text inline sits on that boundary, the text lands before it.
- The applier's mark-range coordinates count token/note-reference display text
  (`GetInlineText` space), so `format_range` converts plain-text boundaries into that space —
  a range whose plain-text characters surround a token therefore marks the token as well,
  while tokens sitting exactly on a range boundary stay unmarked.

Blocks without inline text (`image`, `pageBreak`, `table`, `contentControl` wrappers) have
plain text `""`. `code` blocks expose their verbatim `Code` string as plain text.

## Tokens and content controls

- **Tokens** (`TokenRun`) are addressed by `key` (document-wide semantics: every occurrence of
  the same key resolves to the same value at assembly time). `describe_document` aggregates
  them: `{ key, displayName, tokenType, expression, fallbackText, occurrences: [{ blockId,
  inlineIndex }] }`.
- **Content controls** are addressed by `controlId` (`DocumentContentControl.ControlId`).
  Block-scope controls also carry their `blockId`; inline controls carry the owning `blockId` +
  `inlineIndex`. Assembly metadata (`tmAssembly:*` keys — branch/expression/group for
  conditional chains, bind for repeating sections) is surfaced as `assembly: { branch,
  expression, group, bind }`.

## Optimistic concurrency

`describe_document` returns two coordination values:

| Value | Source | Use |
|---|---|---|
| `concurrencyToken` | `IDocumentEditorProvider.LoadAsync` | **Authoritative** write guard — pass as `expectedConcurrencyToken` to save/apply tools; a stale token yields `error: "conflict"`. |
| `contentDigest` | SHA-256 (lowercase hex) of the normalized persistence JSON (`DocumentEditorJson.Serialize`) | Content fingerprint — detects whether content actually differs across loads/instances even when provider tokens differ (e.g. after a no-op save or across replicas). Never a substitute for `concurrencyToken` in writes. |

## Related references

- Canonical model: `docs/document-canonical-model.md`
- Operation semantics & target resolution: `docs/document-operations-semantics.md`
- Applier convergence coverage: `docs/document-operation-applier-coverage.md`
