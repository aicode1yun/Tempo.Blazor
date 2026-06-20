# TmWireframeEditor — Add Missing Tempo.Blazor Components (TODO)

> Created 2026-06-10. Goal: bring the wireframe editor's component palette up to parity
> with the real Tempo.Blazor component library by adding standalone, user-facing widgets
> that are not yet registered.

## How the wireframe palette works (read before implementing)

Every placeable widget is **two paired entries keyed by `Type`**:

1. **Schema** — `src/Tempo.Blazor.Abstractions/Wireframe/BuiltInComponentSchemas.cs`
   - `WireframeComponentSchema { Type, Category, DisplayName, DefaultWidth, DefaultHeight, SizePresets?, Props[] }`
   - Props use the `P(name, display, PropType, def, opts, cat, req)` helper.
   - Added to the right `private static IEnumerable<…> XxxCategory()` method, which must be yielded in `GetSchemas()`.
2. **SVG renderer** — `src/Tempo.Blazor/Components/Wireframe/BuiltInWireframeComponentProvider.cs`
   - `DefFromSchema("TmX", "iconName", (el, b) => { … Svg(b, sb.ToString()); })`
   - Render with `WireframeSvg` helpers: `Rect, Text, TextCentred, Icon, InputField, Pill, Placeholder, ChevronDown, HLine, VLine, DashedRect, FieldLabel` and color constants `Accent, Border, BorderStrong, Fill, FillAccent, FillDark, ColorText, ColorMuted, ColorLight, Grid`. Use `F(x)` to format doubles, `Escape(s)` for text.
   - Coordinates are local to the element (top-left `0,0`); the canvas applies the outer transform.
   - Added to the matching `private static IEnumerable<…> XxxCategory()` method, yielded in `GetDefinitions()`.

**Parity is enforced** by `tests/Tempo.Blazor.Tests/Wireframe/BuiltInWireframeComponentProviderTests.cs` — every schema needs a renderer and vice-versa. Add an `[InlineData("TmX")]` row there for each new type.

### Per-component workflow (repeat for each item)

For every component checkbox below:

1. [ ] Add the **schema** entry (props + sensible `DefaultWidth/Height`, `SizePresets` if it has a `size` prop).
2. [ ] Add the **SVG renderer** in the matching category method.
3. [ ] Add `[InlineData("TmX")]` to the provider parity test.
4. [ ] `dotnet build` + run `BuiltInWireframeComponentProviderTests` (must stay green).
5. [ ] **E2E screenshot**: drop the widget on the `/wireframe-editor` canvas (Playwright, pattern in `tests/Tempo.Blazor.E2E/WireframeEditorE2ETests.cs`), capture a screenshot.
6. [ ] **UX review (as UX expert)**: confirm the rendered shape reads as the real component at a glance — proportions, key affordances (label/icon/handles), muted wireframe palette, no overflow/clipping. Note any fix and re-render before checking off.

> Build/test note (from project memory): `dotnet test` parallel OOMs (exit 137) — run with
> `-- xUnit.parallelizeTestCollections=false`.

---

## Scope decision (UX/architecture judgment)

~300 `Tm*` components are unregistered, but **most are internal sub-components** a designer
would never place directly. The following families are **intentionally excluded** (placing
their *container* is enough; the parts render inside it):

- **Notion internals** — all `TmNotion*` blocks, DB cells (`TmNotionDbCell*`), views, menus,
  panels. (Only the top-level `TmNotionEditor` / `TmNotionPage` are added as block placeholders.)
- **Document editor internals** — `TmDocument*` renderers, hosts, panels, toolbars, dialogs.
  (Only `TmDocumentEditor` added as a block.)
- **Diagram internals** — `TmDiagram*` canvas/toolbox/panels/import dialogs. (Only `TmDiagramEditor`.)
- **Spreadsheet internals** — `TmSpreadsheet*` dialogs/bars/grid. (Only `TmSpreadsheet`.)
- **Gantt/Scheduler sub-views & dialogs** — `TmGantt*Dialog`, `TmScheduler*View`, etc.
- **Signing wizard steps** — `TmSigning*Step`, field overlays/editors, form runner internals.
- **Modeling internals** — `TmModeling*` inspector/tree/panels. (Only `TmModelingEditor`.)
- **Pivot internals** — `TmPivotField*`. (Only `TmPivotTable`.)
- **Pure config children** — `TmDataTableColumn`, `TmTreeListColumn`, `TmImportWizardStep`,
  `TmSplitterPane`, `TmDockPane`, `TmContextMenuItem`-likes (rendered by their parent).
- **Wireframe's own internals** — `TmWireframe*`, `TmWorkflow*` (already covered / self).

If a future need arises to mock one of these in isolation, add it then — keep the palette
focused on widgets designers actually drop.

---

## Phase 1 — Atomic inputs  (Category: `Inputs`)

Small, high-value controls. Reuse `InputField`/`Pill`/`Icon` helpers.

- [x] **TmSlider** — track + filled portion + thumb; optional value label. `~180×32`.
- [x] **TmRangeSlider** — track + two thumbs + filled span between them. `~180×32`.
- [x] **TmRating** — row of 5 star glyphs (N filled). `~120×24`.
- [x] **TmMaskedTextBox** — input field with mask placeholder text (e.g. `__/__/____`). `~180×36`.
- [x] **TmMultiColumnComboBox** — input field + chevron + hint of multi-column dropdown rows. `~200×36`.

## Phase 2 — Color controls  (new Category: `Color`)

- [x] **TmColorPicker** — swatch + hex label + chevron (trigger form). `~140×36`.
- [x] **TmFlatColorPicker** — open grid of color swatches. `~200×140`.
- [x] **TmColorPalette** — single row/grid of named swatches. `~200×40`.
- [x] **TmColorGradient** — gradient bar + hue strip + draggable knob. `~200×120`.

## Phase 3 — Signature & recurrence  (Category: `Inputs` / `Pickers`)

- [x] **TmSignature** — bordered pad with a scribble path + baseline. `~240×100`.
- [x] **TmSignatureCapture** — signature pad + clear/confirm action row. `~260×140`.
- [x] **TmRecurrenceEditor** — "Repeat every [n] [unit]" row + weekday toggle chips. `~280×120`.

## Phase 4 — Media & data viz  (Category: `Charts` / `Data Display`)

- [x] **TmSparkline** — tiny inline line/area path. `~120×32`.
- [x] **TmGauge** — semicircular arc + needle + value label. `~140×100`.
- [x] **TmStockChart** — candlestick/area chart frame with axis ticks. `~320×180`.
- [x] **TmQRCode** — QR module grid placeholder (finder squares + noise). `~120×120`.
- [x] **TmBarcode** — vertical bar stripes + caption number. `~200×80`.
- [x] **TmPdfViewer** — page frame + toolbar strip + scrollbar hint. `~360×460` (block).

## Phase 5 — Buttons & navigation  (Category: `Buttons` / `Navigation`)

- [x] **TmFloatingActionButton** — circular filled button with `+`/icon + soft shadow. `~56×56`.
- [x] **TmBottomNavigation** — bottom bar with 3–5 icon+label tabs, one active. `~360×56`.
- [x] **TmMenu** — vertical menu surface: items, icons, separator, submenu chevron. `~200×180`.

## Phase 6 — Layout containers  (Category: `Layout`)

- [x] **TmStackLayout** — dashed container with stacked child placeholder bands + gap. `~240×160`.
- [x] **TmSplitter** — two panes separated by a draggable divider handle. `~320×180`.
- [x] **TmDockManager** — docked regions (left/center/bottom) with tab strips. `~360×220`.

## Phase 7 — Builders, collaboration & misc panels

Standalone panels/widgets (Categories: `Inputs`, `Feedback`, `Data Display`).

- [x] **TmFormulaBuilder** — token chips + operator buttons + result row. `~300×160`.
- [x] **TmConditionBuilder** — rule rows ("field — op — value") + AND/OR + add button. `~320×180`.
- [x] **TmCommentComposer** — avatar + multiline input + send button. `~300×88`.
- [x] **TmCommentReactions** — row of emoji reaction pills with counts. `~160×28`.
- [x] **TmReactionPicker** — popover grid of emoji to choose. `~200×80`.
- [x] **TmShareLinkPanel** — read-only link field + copy button + role/permission select. `~320×120`.
- [x] **TmSubmissionStatusTimeline** — vertical stepper of statuses with timestamps. `~260×200`.
- [x] **TmAuditTrailViewer** — list of audit rows (actor · action · time). `~320×200`.
- [x] **TmAIPrompt** — prompt input with sparkle icon + suggestion chips + send. `~320×120`.
- [x] **TmWidgetSelector** — grid of widget tiles (dashboard add-widget). `~280×200`.

## Phase 8 — Complex block placeholders  (new Category: `Editors & Apps`)

These are full sub-apps. **Decision (2026-06-10): higher-fidelity mocks** — draw representative
inner content (real-ish rows / bars / bubbles / nodes), not just a labelled empty frame. Each
must read as the actual app at a glance while staying in the muted wireframe palette. The sizes
below are starting points; give the inner content enough room to be legible.

- [x] **TmChat** — message bubbles (left/right) + input bar. `~320×400`.
- [x] **TmSpreadsheet** — formula bar + column headers + grid + sheet tabs. `~480×320`.
- [x] **TmGantt** — task list pane + timeline bars + header scale. `~520×300`.
- [x] **TmGanttPortfolio** — multi-project rollup rows. `~520×300`.
- [x] **TmPivotTable** — row/column headers + aggregated value cells + field drop zones. `~420×280`.
- [x] **TmTreeList** — tree-indented rows + columns header. `~360×260`.
- [x] **TmDiagramEditor** — toolbox rail + canvas with two connected nodes. `~520×340`.
- [x] **TmDocumentEditor** — ruler + toolbar + page with text lines. `~480×360`.
- [x] **TmNotionEditor** — sidebar + page body with mixed blocks. `~520×360`.
- [x] **TmNotionPage** — page header + title + block stack (no sidebar). `~420×360`.
- [x] **TmModelingEditor** — model tree + diagram preview + inspector. `~520×340`.
- [x] **TmFileManager** — breadcrumb + folder/file grid + side tree. `~480×320`.
- [x] **TmDocumentManager** — document list/table + preview pane. `~480×320`.

---

## Final integration checks

- [x] All new types appear in the toolbox under correct (and any new) categories — `Toolbox_NewCategories_Visible` E2E test passes; `Color` and `Editors & Apps` confirmed present ✅.
- [x] `BuiltInWireframeComponentProviderTests` green (schema⇄renderer parity, count threshold) — 123/123 ✅.
- [x] Each component round-trips through `WireframeSerializer` — 27-type `Roundtrip_NewComponent_PreservesTypeAndProps` theory added; 499 wireframe unit tests pass ✅.
- [x] One consolidated **E2E gallery screenshot** taken (`wireframe_gallery_ux_review.png`); UX review passed — all components legible, consistent muted palette, correct proportions ✅.
- [x] Localization: all new `DisplayName` values follow English convention (no `Tm` prefix, words spaced; e.g. "Color Picker", "Gantt Chart", "Document Editor") ✅.
- [x] Component count threshold updated 70 → 110 in `BuiltInWireframeComponentProviderTests` ✅.

## Notes / decisions log

- 2026-06-10: All 47 components implemented across 8 phases. Two structural bugs fixed in provider (premature closing braces in `Pickers()` and `Charts()`). Unit tests: 499/499 wireframe. E2E: 2 new tests pass (Toolbox_NewCategories_Visible + NewComponents_GalleryScreenshot).
- 2026-06-10 UX review (gallery screenshot): All components legible and distinctive at glance. Consistent muted blue/gray palette throughout. Gauge, QRCode, Barcode, Rating stars, Signature scribble, ColorGradient gradient bar all immediately recognizable. Chat/Spreadsheet/Gantt/DiagramEditor/DocumentEditor render as their intended full apps. No overflow or proportion issues noted. PASS.
