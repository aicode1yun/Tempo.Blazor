# Document operation semantics reference (`DocumentOperation`)

What each canonical operation type does to the `DocumentEditorDocument` model, its invariants
and error states. The C# source of truth is `DocumentOperationApplier`
(Tempo.Blazor.Abstractions); the JS collaboration applier
(`document-editor-canvas/collaboration/transform.mjs`) converges on CONTENT for the shared
semantics (see `docs/document-operation-applier-coverage.md`). The drift guard
(`DocumentModelDocumentationDriftTests`) fails when a `DocumentOperationType` member exists in
code without a section here.

## Envelope

```
DocumentOperation
├─ OperationId (unique; resolver ordering tie-breaker)
├─ Type (camelCase on the wire, e.g. "insertText")
├─ Target { BlockId, SectionId, InlineIndex, InlineId, ObjectId, TableCellId, Offset, Length, Order }
├─ Text, Mark, Block, AttributeName, AttributeValueJson
├─ Revision, NewLayout/OldLayout, NewAnchor/OldAnchor
└─ Metadata { LogicalTimestamp, ClientId, AuthorId }  → deterministic resolver ordering
```

Batches (`DocumentOperationBatch`) apply in order; each operation returns
`DocumentOperationValidationResult` (`IsValid` + `Errors`). Applying is IDEMPOTENT-tolerant
where noted (re-inserting an existing block, deleting a missing block).

**Target resolution invariant** — block targets resolve DEEPLY: body blocks first, then
recursively through table cells; `Target.TableCellId`, when set, restricts which container may
match (mirrors JS `findBlockLocation`). Nested containers (table cells) are index-ordered;
the body is `Order`-value ordered.

## Text operations

### `insertText`
Inserts `Text` at `Target.Offset` of the resolved text run (`InlineId`/`InlineIndex`, default
first run; offset clamped to the run length). Errors: target block missing or not text-based.
Invariant: never changes block count or order.

### `deleteText`
Removes `Target.Length` characters (default: `Text.Length`) from `Target.Offset`; length is
clipped to the run end. Errors: block or inline missing. Invariant: text-only change.

## Formatting operations

### `addInlineMark` (alias `addMark`)
With `Offset`+`Length`: applies `Mark` to exactly that character range — runs are split at the
boundaries, compatible neighbours merge back. Without a range: marks the whole resolved inline.
Errors: target or `Mark` missing. Invariant: text content is unchanged; run identities may
change (convergence is content-based).

### `removeInlineMark` (alias `removeMark`)
Inverse of `addInlineMark`; removes the mark type from the range/inline and merges compatible
runs back. Same errors/invariants.

## Block operations

### `insertBlock`
Inserts `Block` (cloned) into the target container — the body (ordered by `Order`, which
`Target.Order` overrides) or a table cell (`Target.TableCellId`; `Target.Order` = list index).
Idempotent: an existing block id in the container is a valid no-op. Errors: `Block` payload or
target cell missing.

### `deleteBlock`
Removes the block (body or nested). Valid even when already gone. The conflict resolver drops
LATER operations targeting a deleted block.

### `moveBlock`
Body: sets `Block.Order = Target.Order` and re-sorts the body. Table cell: index-based move
within the cell's list. Errors: block missing or `Target.Order` null. (Known cross-runtime
nuance: body moves are order-value semantics in C#, index semantics in JS — see the coverage
doc.)

### `updateBlock`
Replaces the whole block payload in place (body keeps the existing `Order` unless
`Target.Order` overrides; nested replaces at the same index). Preserves object content without
degrading it to text. Missing target is a valid no-op.

### `setBlockAttribute`
Attribute-addressed mutations; `AttributeValueJson` carries the payload:

| `AttributeName` | Effect |
|---|---|
| `headingLevel` | Converts the block to a heading (level clamped 1–6), preserving inlines. |
| `text` | Replaces the block's inlines with a single text run. |
| `paragraphProperties` | Patches `ParagraphProperties` (alignment, spacing, indents, …). |
| `clearFormatting` | Removes formatting marks in the given range. |
| `table.cell.text` | Targets the TABLE block; `Target.TableCellId` addresses the cell whose text is replaced. |
| `order` | Sets `Block.Order` and re-sorts the body. |
| `metadata.title` | Document-level: sets `Metadata.Title` (no block target needed). |

Errors: unknown attribute, missing block, missing payload. Concurrent writes resolve
last-write-wins per (block, attribute).

## Object operations

### `moveDrawingObject`
Finds the drawing run by `ObjectId`/`InlineId`/(block, `InlineIndex`) across the body,
headers/footers and nested blocks; replaces its `Layout` with `NewLayout` and applies
`NewAnchor`. Fills `OldLayout`/`OldAnchor` for undo/OT. Errors: payload or drawing missing.
Concurrent moves resolve last-write-wins per object id.

## Revision operations

### `createRevision`
Registers `Revision` as pending (idempotent per id) and applies its pending markup: insertion
revisions insert the text marked with a `revision` mark, deletion revisions mark the range,
formatting revisions record the format patch. Errors: payload missing.

### `acceptRevision`
Materializes the revision: insertions keep their text (revision mark removed), deletions remove
the text, formatting patches apply. First decision wins under concurrency.

### `rejectRevision`
Reverts the revision: insertions are removed, deletions restored, formatting patches discarded.
First decision wins under concurrency.

## Conflict resolution (batch level)

`DocumentOperationConflictResolver.Resolve` orders operations by
`(LogicalTimestamp, ClientId, AuthorId, OperationId)` and transforms them: inserts shift
against prior inserts/deletes, mark ranges shift/clip against text edits (dropped when fully
deleted), duplicate range deletes deduplicate, per-key last-write-wins for moves/attributes/
updates/object moves, first-decision-wins for revision decisions, and operations on deleted
blocks are suppressed.
