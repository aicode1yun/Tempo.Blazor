# Changelog

## 2.3.8 - 2026-07-17

### Fixes

- Increased the dense-line marker spacing threshold from ~9 to ~24 SVG units: at ~9 units the thinned markers still touched each other (12-unit visual diameter), keeping the beaded look on very dense series (e.g. 360 monthly values). Markers now sit clearly ON the line with visible line segments between them.

## 2.3.7 - 2026-07-17

### Dense line series readability (TmChart)

- Line series (both `Line` charts and combo overlays on `Bar` charts) now thin their point markers when values are packed tighter than ~9 SVG units apart — overlapping white-stroked circles previously made a dense line (e.g. 360 monthly values) look dotted. The polyline itself always renders complete; sparse series keep a marker on every value.

## 2.3.6 - 2026-07-17

### Negative values on Bar and Line charts (TmChart)

- `Bar` charts (including combo overlays) and `Line` charts now support negative values through a signed value domain: when any visible value is negative, the Y axis extends below zero with an emphasized zero axis (parity with Area charts), bars grow downward from the zero baseline, and line/overlay points plot below the axis. Charts with only non-negative values render exactly as before (0-based scale). Previously a negative value produced an invalid negative-height bar or a point outside the plot area.

## 2.3.5 - 2026-07-17

### Combo charts (TmChart)

- Added `ChartDataset.RenderAs` (`ChartDatasetRenderAs.Default | Bar | Line`): on a `ChartType.Bar` chart, datasets marked `Line` render as a line overlay (polyline + clickable points) over the bars, centered on each category and sharing the bars' Y scale — bars for periodic flows, lines for cumulative values in one plot. Default keeps the chart's own type, so existing charts are unaffected; on non-Bar charts the override is ignored.
- Bar charts with more than 24 categories now thin their X-axis labels (every n-th label, at most ~12) so dense categorical axes stay readable; charts with up to 24 categories keep every label.

### Fixes

- Fixed `TmLightbox` stacking: the root `.tm-lightbox` used a hardcoded `z-index: 1000`, which painted the close/prev/next buttons underneath sticky chrome such as `TmTopBar` (`--tm-z-sticky` 1020). It now uses the overlay tier (`var(--tm-z-overlay, 1060)`), consistent with `.tm-lightbox-overlay`.

## 2.3.4 - 2026-07-17

### Accent-insensitive filtering (TmFilterableDropdown / TmMultiColumnComboBox)

- Client-side filtering in `TmFilterableDropdown` and `TmMultiColumnComboBox` is now accent-insensitive by default: both the filter term and the item text are normalized to Unicode FormD and combining diacritical marks are stripped before the contains comparison, so e.g. "usti" matches "Ústí nad Labem" and "práha" matches "Praha".
- Added an `AccentInsensitiveFilter` parameter (default `true`) to both components to opt back into accent-sensitive (but still case-insensitive) filtering.
- The change is match-superset only: every item that matched before still matches; accent-mismatched items are newly included. Server-side `DataProvider` filtering is unaffected (the provider owns its own matching).

## 2.3.0-preview.1 - Unreleased

- Added `ButtonVariant.OutlineSecondary`, `ButtonVariant.Warning`, and `ButtonVariant.OutlineWarning` to `TmButton`.
- Added `RowAttributes` to `TmDataTable` for applying row-level HTML attributes consistently across non-virtualized, virtualized, and grouped data rows.
- `DocumentEditorSnapshotCommand` restores the historical defensive-clone contract by default; added an opt-in `assumeOwnership` constructor parameter (default `false`) for callers that hand over dedicated snapshots and want to skip the two O(document) copies. Existing external code compiles and behaves as in 2.0.x.
- `DocumentEditorCommandRegistry.Register` now invalidates the refresh signature gate, so commands registered after the first `RefreshAllAsync` receive their state on the next refresh even when the command context is unchanged.
- TmDocumentEditor toolbar wave: native selects openable by mouse (removed `preventDefault`), 31 missing built-in icons added to `TmIcon` (+ `IconNames` constants and `DocumentToolbarItem.Options`/`DocumentToolbarRenderContext.CommandState` for declarative renderers), ribbon CSS consolidated into `_document-editor-toolbar.css`, 21 additional commands registered in the command registry with a unified enabled/visibility fallback.
- TmDocumentEditor ribbon overflow is now live: a new `toolbar-overflow.mjs` measurement controller (ResizeObserver + scroll + tab-switch mutations) reports off-screen `[data-command]` items through the existing `SetOverflowingAsync` contract, so the More menu finally appears on narrow windows; the toolbar loads the module itself — no host-app setup needed. Fixed the overflow search box opening pre-filled with a literal `_overflowSearchQuery`.
- `DocumentToolbarButtonRenderer` now honors `DocumentToolbarRenderContext.Execute` (click) and `CommandState.IsEnabled` (disabled), matching the toggle/select/color renderers; added the `/document-toolbar-renderers` demo page showcasing the declarative toolbar extension API.
- Added `TmNavigationGuard`, an unsaved-work navigation guard that gates internal router navigation with a `TmDialog` confirmation and arms a browser `beforeunload` prompt for tab close/refresh. Exposes `Suppress()` for post-commit programmatic navigation.
- Added `TmFormActionBar`, a sticky/floating action bar for long forms with `Status`/`PrimaryActions`/`SecondaryActions`/`DangerActions` slots, `Static`/`StickyTop`/`FloatingBottom` positions (`FormActionBarPosition`), and a functional `ShowOnScroll` reveal (real passive scroll listener, not a no-op hook).
- Added `TmScrollSpyNav`, a sectional in-page navigation component with an optional passive-scroll spy (`EnableScrollSpy`), `SideRail`/`Breadcrumb` variants (`ScrollSpyNavVariant`), a minimal generic `ScrollSpyNavItem` record, and an `ItemTemplate` slot for host-supplied enrichment. Active items expose both `aria-current="true"` and `data-active`.
- Added `TmUserPicker<TUser>`, a generic entity/user picker with debounced cancellable search, pointer-down selection, keyboard navigation, and explicit three-state (`TmPickerFetchState.Ok`/`Empty`/`Transient`) fetch rendering so a real search/resolve failure is never shown as a silent "no results". Plain async `SearchProvider`/`ResolveProvider` callers; no built-in retry loop.
- Added a typed `Required` parameter to `TmSelect`, `TmCheckbox`, and `TmRadioGroup`, matching the existing `TmTextInput`/`TmMultiSelect` pattern. When set, it renders the required marker (`tm-input-label-required` label class → asterisk) and sets `aria-required="true"` on the actual control — the `<select>`, the checkbox `<input>`, and the `role="radiogroup"` element respectively — instead of the wrapper, so it is exposed to assistive tech. `AdditionalAttributes` splat behavior is unchanged. Also advertised `required` in the built-in wireframe schemas for Checkbox and Radio Group.
- Extended the visible required marker (`tm-input-label-required` label asterisk) and `aria-required` to the remaining label-owning inputs so all label-bearing field types are consistent: `TmTextInput` and `TmDecimalInput` now add the marker class to their label (both already set `aria-required` on the control); `TmTextArea` and `TmDecimalInput` gained a typed `Required` parameter that drives the marker plus native `required`/`aria-required` on the `<textarea>`/`<input>`; and `TmDatePicker`/`TmDateTimePicker`'s previously-declared-but-unused `Required` is now wired to the marker and `aria-required` on the trigger button. `AdditionalAttributes` splat behavior is unchanged.

## 2.2.0 - 2026-07-06

### Data component chrome and filtering (TmDataTable / TmMultiViewList)

- Added `ShowToolbar` parameter to `TmDataTable` and `TmMultiViewList` to explicitly suppress the built-in toolbar.
- Added `ShowViewManager` parameter to `TmDataTable` and `TmMultiViewList` to control rendering of the saved-views picker independently.
- Added `SearchText` / `SearchTextChanged` two-way binding so the surrounding page can own the search state.
- Added `ToolbarMode` (`DataToolbarMode.Full`, `SearchOnly`, `ActionsOnly`, `ContentOnly`) as a higher-level API for common toolbar presets.
  - `Full` keeps the existing behavior (respects individual `Show*` flags).
  - `SearchOnly` renders only the global search input.
  - `ActionsOnly` renders only chrome actions (column picker / view switcher / view manager).
  - `ContentOnly` hides all toolbar chrome and the external filter builder, leaving a clean data surface for page-owned filtering.
- `ToolbarMode=ContentOnly` and `ShowExternalFilterBuilder=false` prevent duplicate filtering UI when the owning page already provides filters or saved views.
- Empty toolbars are no longer rendered when no control would be visible.

### Migration notes

- Existing code continues to compile and run unchanged; all new parameters have backward-compatible defaults.
- If your page already has its own filter toolbar, switch the data component to `ToolbarMode="DataToolbarMode.ContentOnly"` and bind `Items` to your pre-filtered collection.
- If you want saved views without the inline filter builder, keep `ToolbarMode="DataToolbarMode.Full"` and set `ShowExternalFilterBuilder="false"`.
- See `docs/data-component-chrome-migration.md` for a full migration guide and PromptHelper-specific replacement instructions.

## 2.1.0 - 2026-07-04

- Added the UI role vocabulary model for wireframe authoring, including built-in role synonyms and app-scoped role resolution.
- Added role-aware wireframe authoring through MCP operations and `wireframe_author_document`, with advisory warnings for role gaps, ambiguous matches, enum normalization, off-canvas placement, text overflow, required content, and layout issues.
- Added compact/filterable `wireframe_get_authoring_guide` output with category, type, role, target pack, app scope, skip, and take filters.
- Added container-aware wireframe linting through `isContainer`, so expected containment does not appear as sibling overlap.
- Added `WireframeThumbnailRenderer` in `Tempo.Blazor.Wireframe` and moved the Demo API document-library preview generation onto the package renderer.
- Updated the wireframe document schema and JSON documentation for document version 2.1, element roles, component roles, container metadata, MCP authoring, and thumbnails.
- Bumped published package metadata to `2.1.0` and aligned release workflows to derive manual/CI versions from the core package version.

Release follow-up after review: commit the prepared changes, merge to main, tag `v2.1.0`, push, then verify NuGet.org publication with a clean-project package-install smoke.
