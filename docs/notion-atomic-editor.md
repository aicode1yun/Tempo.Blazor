# Atomic Notion editor authoring

`TmNotionEditor` can use `INotionAggregateProvider` as a canonical, optimistic
persistence boundary for logical changes that span multiple blocks. This is
required for editable tables and structured multi-block paste.

```razor
<TmNotionEditor DataProvider="@pages"
                BlockProvider="@blocks"
                AggregateProvider="@aggregates"
                InitialPageId="@pageId" />
```

The editor loads one complete `NotionPageSnapshot` when it opens a page. A
table row/column change, merge/split, cell edit, sort, row reorder, undo, redo,
or structured paste is applied to a clone, validated with
`NotionAggregateValidator`, and sent through exactly one
`INotionAggregateProvider.SaveAsync` request. The MCP authoring tools use the
same validator.

## Conflict behavior

Saves use the loaded snapshot's concurrency token. If the provider reports a
conflict, the editor keeps the complete local candidate visible and prevents
another mutation from being layered over it. The user can:

- reload the current server aggregate and discard the local candidate; or
- load the current server aggregate, reapply the retained logical mutation,
  validate it again, and save once against the fresh token.

Provider and validation failures are shown in the editor. Persistence
exceptions are not treated as successful saves.

## Canonical table rendering

Canonical table cells support structured inline marks and links, background
and text colors, horizontal and vertical alignment, row/column spans, preferred
cell or column widths, and per-side borders. Dynamic HTML and CSS values are
sanitized or normalized before rendering. `DisplayHtml` is a transient,
non-serialized view derived from structured inlines; canonical `Html` remains
separate.

When `AggregateProvider` is omitted, canonical tables still render, but their
aggregate-only authoring controls are hidden to avoid falling back to partial
sequential writes.
