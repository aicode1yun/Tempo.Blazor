# E2E Test Findings

## Stencil Layout Gallery

- Screenshot: `tests/Tempo.Blazor.E2E/__baseline__/stencil-layout/stencil-layout-gallery.png`
- Coverage: stack, row, grid, repeat cap, 9-slice at small and large widths, and right-anchor behavior.
- UX/UI review: the gallery uses consistent spacing, visible labels, neutral cards, and distinct fills so each layout behavior is easy to scan and compare.
- Completeness review: stack and row spacing, grid wrapping, repeat clamping, 9-slice corner stability, and right anchoring are all represented in one deterministic viewport.
- Findings: no visual blockers found.

## Stencil Composition Card With Badge

- Screenshot: `tests/Tempo.Blazor.E2E/__baseline__/stencil-composition/card-with-badge.png`
- Coverage: card component embedding a registry-resolved badge component with parent-bound props.
- UX/UI review: the card has clear hierarchy, the status badge is visibly embedded in the card header, and the amount/status content is legible in the captured viewport.
- Completeness review: the scenario proves the renderer can compose child stencil components through `WireframeComponentRegistry`, bind props into the child element, and keep output free of script/foreignObject markup.
- Findings: no visual blockers found.

## Stencil Tempo Form Controls Gallery

- Screenshot: `tests/Tempo.Blazor.E2E/__baseline__/stencil-form-controls/tempo-pack-form-controls-gallery.png`
- Coverage: Tempo stencil-pack Buttons, Inputs, Tags, Pickers, and Dropdowns with representative bound props, variants, checked states, ranges, tags, calendar values, and dropdown labels.
- UX/UI review: the gallery has clear spacing, legible labels, stable control chrome, and the larger calendar/recurrence controls reserve enough vertical room to avoid overlap. The regenerated baseline confirms the radio group label no longer collides with its first item, range values clear the `Window` label, and leading icons no longer overlap select-like placeholders.
- Completeness review: all 41 schema form-control types in Buttons, Inputs, Tags, Pickers, and Dropdowns are pack-backed, render through declarative stencil nodes, and are excluded from fallback output.
- Findings: resolved prior form-control overlap issues in `TmRadioGroup`, `TmRangeSlider`, `TmFilterableDropdown`, and `TmEntityPicker`; no visual blockers found.

## Stencil Tempo Structure And Navigation Gallery

- Screenshot: `tests/Tempo.Blazor.E2E/__baseline__/stencil-structure/tempo-pack-structure-phase10-gallery.png`
- Coverage: all 40 Tempo stencil-pack Data Display, Data Table, Navigation, Layout, and Toolbar components with representative bound titles, labels, active states, table rows/columns, drawer/sidebar/topbar affordances, and toolbar commands.
- UX/UI review: spacing, contrast, and component sizing are stable across the gallery; navigation and toolbar text remains centered after expression normalization; no text overflow or overlap blockers were found.
- Completeness review: all 40 scoped types are pack-backed, render through declarative stencil nodes, and are excluded from fallback output while registry coverage still includes every built-in schema type.
- Findings: no visual blockers found.

## Phase 11 - Feedback/Forms Stencil Pack

- Screenshot: `tests/Tempo.Blazor.E2E/__baseline__/stencil-feedback-forms/gallery.png`
- Coverage: all 30 explicitly listed Feedback, Notifications, Forms, Files, Avatars, Icons, and Color components with representative variant colors, progress fill, notification badge, validation states, form layouts, upload/attachment surfaces, avatars, icon, swatches, and gradient controls.
- UX/UI review: component spacing and contrast are stable in the gallery; alert/modal/dialog, form validation, file drop zone, attachment list, and color controls remain legible with no text overflow, clipping, or overlap blockers found.
- Completeness review: every scoped type is pack-backed, renders through declarative stencil nodes, is excluded from fallback output, and the pack+fallback registry still covers every built-in schema type.
- Findings: no visual blockers found.

## Phase 12 - Complex/Native Stencil Pack

- Screenshot: `tests/Tempo.Blazor.E2E/__baseline__/stencil-complex/gallery.png`
- Coverage: Charts, Workflow, Complex, Data Display native Kanban, and Editors & Apps representatives, including native-hook renderers for Chart, Gauge, StockChart, KanbanBoard, Gantt, Spreadsheet, PivotTable, WorkflowDesignerCanvas, DiagramEditor, DocumentEditor, NotionEditor, and Chat.
- UX/UI review: the gallery keeps the large app surfaces readable in a two-column layout; workflow panels, timelines, selectors, dashboards, document/file surfaces, and native editor previews remain visually distinct without script/foreignObject output.
- Completeness review: every built-in schema type resolves through the Tempo stencil pack, declarative phase-12 types render without fallback, and `BuiltInWireframeComponentProvider.GetDefinitions()` is empty after migration.
- Findings: no visual blockers found.

## Tempo Package Release Sweep

- Screenshots: `tests/Tempo.Blazor.E2E/__baseline__/stencil-*/*.png` and `tests/Tempo.Blazor.E2E/__baseline__/wireframe-server-preview/multipage-home.png`
- Coverage: pack-only registry after removal of the legacy built-in fallback provider; all built-in schema types are resolved through `BuiltInStencilPackProvider`, including the 12 native-hook components.
- UX/UI review: regenerated stencil galleries and server preview remain populated, legible, and free of obvious clipping, overflow, placeholder fallbacks, or blank native surfaces.
- Completeness review: the Wireframe E2E category passed with regenerated baselines; service registration and registry tests verify every `BuiltInComponentSchemas.GetSchemas()` type resolves with positive schema dimensions rather than fallback-sized defaults.
- Findings: no visual blockers found; release baseline is ready for publish approval.

## Plan 20 - Regression, SPEC, And Schema Finalization

- RED baseline: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter StencilFormatSpecInvariantsTests --logger "console;verbosity=normal"` failed 3/10 on July 2, 2026. Failures were expected: missing `docs/SPEC-stencil-format.md`, missing authoring guide, and stale `wireframe-document.schema.json` still describing the legacy root `elements` format instead of current `pages`.
- Targeted GREEN: the same invariant filter passed 10/10 after adding `StencilFormatSpecInvariantsTests`, updating `wireframe-document.schema.json` to the v2 paged model, and writing `docs/SPEC-stencil-format.md` plus `docs/stencil-pack-authoring-guide.md`.
- Final Tempo regression: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter Wireframe` passed 990/990, `dotnet test tests/Tempo.Blazor.Mcp.Tests/Tempo.Blazor.Mcp.Tests.csproj --no-restore` passed 143/143, `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore --filter TestCategory=Wireframe` passed 11/11, and `dotnet build TempoBlazor.slnx --no-restore` completed with 0 warnings/errors.
- Baseline review: `tests/Tempo.Blazor.E2E/__baseline__/stencil-*/*.png` and `tests/Tempo.Blazor.E2E/__baseline__/wireframe-server-preview/multipage-home.png` had no git diff after the final run. Visual review found populated galleries/server-preview, no blank native surfaces, no fallback placeholders, and no text overlap blockers.
- PromptHelper cross-repo regression: `dotnet test PromptHelper.slnx --no-restore` passed 6950 tests with 4 skipped, and `ASPNETCORE_ENVIRONMENT=E2E E2E_INFRA_MODE=containers E2E_UI=1 npx playwright test specs/wireframe-*.spec.ts --reporter=list` passed 34 tests with 3 skipped. PromptHelper app stencil catalog counts and MCP app-scoped component discovery are runtime-derived from uploaded app stencil packs; CodeLibrary reflection remains outside the wireframe render/schema path.
- Findings: no release-blocking visual or functional blockers found.
