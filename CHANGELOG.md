# Changelog

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
