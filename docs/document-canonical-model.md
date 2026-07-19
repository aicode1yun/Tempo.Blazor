# Canonical document model reference (`DocumentEditorDocument`)

Reference for the canonical JSON model TmDocumentEditor persists, the headless runtime lays out
and MCP tooling (plan C) manipulates. Source of truth:
`src/Tempo.Blazor.Abstractions/DocumentEditor/Models/*` — this document describes structure AND
semantics; the drift guard (`DocumentModelDocumentationDriftTests`) fails when a block type,
inline type or mark type exists in code without a section here.

## Serialization conventions

Two wire formats exist for the same model:

| Format | Serializer | Casing | Used by |
|---|---|---|---|
| **Persistence** | `DocumentEditorJson.Options` | C# property names (PascalCase), polymorphic `$type` discriminators | providers, save/load, operation batches on the C# side |
| **Canvas wire** | camelCase + camelCase string enums + nulls omitted (`CanvasEngineJsonContext`, `JintDocumentLayoutEngine.CanvasModelWireOptions`) | camelCase | JS engine mount/replaceModel, headless layout requests |

`CanvasDocumentModelConverter.ToCanvasModel` / `FromCanvasModel` convert between the persistence
model and the canvas model (`CanvasDocumentModel` — string-typed blocks with `content.runs`);
the JS side re-normalizes any input through `createCanvasDocumentModel`. The canvas model keeps a
`preserve` channel per node so persistence-only data survives the round trip.

## Top level

```
DocumentEditorDocument
├─ SchemaVersion (int, currently 1)
├─ DocumentId (stable id)
├─ Metadata (title, author, timestamps, status, tags)
├─ PageSettings (points: Size {Name,Width,Height}, Margins, Landscape, header/footer distances)
├─ Theme (BodyFontFamily CSS list, BodyFontSize pt, BodyLineHeight, ParagraphSpacingAfter pt)
├─ Hyphenation, PageBackground (incl. Watermark)
├─ Sections[] (Order, Properties.PageSettings — per-section page setup)
├─ Blocks[] (ordered body content — the main tree)
├─ Comments[], Notes[], HeadersFooters[], Revisions[]
├─ NumberingDefinitions[], ListStyles[], Styles[]
├─ BibliographySources[], Citations[], Assets[], Anchors[]
└─ IsProtected, RestrictedMarkers[]
```

Blocks are ordered by `Order` (double) then `Id`; every block belongs to a section via
`SectionId` (empty → first section).

## Block types (`DocumentBlock.Content`, `$type` discriminators)

| `$type` | Content class | Semantics |
|---|---|---|
| `paragraph` | `ParagraphBlockContent` | Inline run list (`Inlines`); paragraph-level format lives in `DocumentBlock.ParagraphProperties` (alignment, spacing, indents, tabs, borders, shading). |
| `heading` | `HeadingBlockContent` | Like paragraph plus `Level` 1–6; feeds outline/TOC. |
| `list` | `ListBlockContent` | One list item: `Ordered`, `IndentLevel`, `StartNumber`, numbering references (`NumberingId`, `AbstractNumberingId`, `ListStyleId`, `NumberFormat`, `LevelText`, `Suffix`). Consecutive list blocks form the visual list. |
| `quote` | `QuoteBlockContent` | Inline run list rendered as a quotation. |
| `table` | `TableBlockContent` | `Rows[] → Cells[]`; every cell (`TableCellContent`, stable `Id`) hosts a NESTED block list (`Blocks`) — the recursion point of the model. Cell/row properties carry widths, spans, borders, shading. |
| `image` | `ImageBlockContent` | Block-level image (`AssetId`/`Url`, `Size`, `AltText`, caption). Inline/anchored images are `drawing` runs instead. |
| `pageBreak` | `PageBreakBlockContent` | Explicit page break; may carry a section break type. |
| `contentControl` | `ContentControlBlockContent` | Structured document tag: `Control` (type, tag, title, lock state, list items) + nested `Blocks`. Assembly metadata (below) rides on the control. |

## Inline types (`InlineContent`, `$type` discriminators)

| `$type` | Class | Semantics |
|---|---|---|
| `text` | `TextRun` | `Text` + `Marks[]`. The unit of text editing; appliers split/merge runs freely — run identity is NOT content. |
| `token` | `TokenRun` | Assembly token: `Key`, `DisplayName`, optional `Expression` (computed value), `FallbackText`. Resolved by `DocumentAssemblyService` from `DocumentTokenValue`s. |
| `field` | `DocumentFieldRun` | Dynamic field (page number, date, …) with `DisplayText` snapshot. |
| `noteReference` | `DocumentNoteReferenceRun` | Footnote/endnote marker referencing `Notes[]`. |
| `drawing` | `DocumentDrawingRun` | Anchored/inline drawing object: `ObjectId`, `Layout` (anchor, wrap mode, contour, offsets, z-order), shape/image payload, caption, alt text. |
| `math` | `DocumentMathRun` | Structured math content (linearised text + layout tree). |
| `contentControl` | `DocumentContentControlRun` | Inline structured document tag. |
| `signingField` | `DocumentSigningFieldRun` | Signature field placeholder (role, recipient, bounds). |

## Inline marks (`InlineMark.Type`)

`bold`, `italic`, `underline`, `strikethrough`, `superscript`, `subscript`, `smallCaps`,
`allCaps`, `doubleStrikethrough`, `characterSpacing`, `characterScale`, `kerning`, `link`
(`Link.Href`), `commentAnchor` (comment id), `revision` (revision id — tracked-changes
membership), `highlight` (`Value` = color), `textColor`, `fontFamily`, `fontSize`, `bookmark`,
`redaction` (content DESTROYED in print/PDF exports — block characters replace the text).

Value-carrying marks put their payload in `Value` (or `Link`); marks apply to the whole run —
character-precision formatting is expressed by splitting runs.

## Content controls & assembly metadata

Document assembly (server-side, `DocumentAssemblyService.Assemble(template, tokenValues,
options)`) evaluates metadata attached to `contentControl` blocks via `DocumentAssemblyMetadata`:

- **Conditional chains** — `CreateConditionalBlock("if"|"elseif"|"else", expression, chainId)`;
  adjacent controls with the same chain id form an IF/ELSE-IF/ELSE chain; the first branch whose
  expression evaluates truthy against the token values survives, the rest are dropped.
- **Repeating sections** — `CreateRepeatingSection(bindingKey)`; the control's child blocks are
  cloned once per row of `DocumentTokenValue.Rows[bindingKey]`, with row columns exposed as
  token keys inside the clone.
- **Computed tokens** — `TokenRun.Expression` evaluated by `DocumentAssemblyExpression`
  (functions incl. `SUM(rows,'col')`, `COUNT`, `CURRENCY(value,culture,code)`, `TODAY()`,
  `DATEADD(date,days)`, arithmetic and comparisons). `TODAY()` uses the injected clock
  (`DocumentAssemblyOptions.Now`), so assembly is deterministic under test.

## Tokens (`DocumentTokenValue`)

`Key`, `HasValue`, `Value` (raw), `DisplayValue`, `TokenType`, `Rows` (list of column→value
dictionaries for repeating sections/aggregates). `DocumentTokenValue.Resolved(key, value)` and
`Missing(key)` are the canonical factories; missing tokens render the token's `FallbackText`.

## Related references

- Operation semantics: `docs/document-operations-semantics.md`
- Applier coverage & convergence: `docs/document-operation-applier-coverage.md`
- Semantic addressing for MCP tools: `docs/document-mcp-addressing.md`
- Headless runtime API: COMPONENTS.md → „Headless dokumentový runtime“
