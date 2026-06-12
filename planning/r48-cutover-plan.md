# R.4.8 — Core-engine cutover plan

Status: **feature-flag seam in place; cutover (default flip + legacy deletion) BLOCKED on the hosted-interop bridge.** (2026-05-29, entry version 219)

## Why the cutover is not done yet

The plan's R.4.8 is *"feature-flag default → core; delete legacy render path + contenteditable body + DOM-readback; full E2E suite green on the new engine; perf ≥ legacy."* Two hard preconditions are not met:

1. **The new core engine has no C# / Blazor interop.** It is a standalone JS render host
   (`core-engine/render-host.mjs`) verified through `core-engine-harness.html` + 18
   Playwright gates (`CoreEngineRenderHostE2ETests`). The shipping component
   (`TmDocumentEditor` → `TmDocumentWysiwygHost`) drives the **legacy** engine through a
   deep C# bridge: model load, **save/snapshot**, **undo/dirty state**, toolbar command
   dispatch, collaboration, offline drafts, audit, PDF/format/comparison providers. None
   of that exists for the core engine.
2. **The full existing E2E suite runs against the legacy component.** The core engine
   cannot pass `DocumentEditor*E2ETests` until it *is* the component's engine (precondition 1).

Deleting legacy now would leave the editor non-functional. So R.4.8 is staged.

## What IS done now (safe, reversible)

- **Feature flag**: `DocumentEditorRenderEngine { Legacy, CoreEnginePreview }` +
  `TmDocumentEditor.RenderEngine` parameter (default `Legacy`).
- **Fail-safe resolution**: `EffectiveRenderEngine` resolves `CoreEnginePreview → Legacy`
  while `CoreEngineHostedInteropReady == false`, so requesting the preview can never break
  the editor. Surfaced as `data-render-engine` / `data-render-engine-requested` on the root.
- Core engine itself is feature-complete + browser-verified standalone (R.4.0–R.4.7:
  layout, input/IME, caret/selection, bidi/grapheme, marks, headings, tables, images+wrap,
  hyperlinks, find/replace, track changes, comments, headers/footers, undo/redo, layout
  cache, accessibility).

## Remaining work to actually cut over (in order)

1. **Core-engine hosted bridge (the big one)** — a `TmDocumentCoreEngineHost` (sibling of
   `TmDocumentWysiwygHost`) that:
   - serializes the C# `DocumentEditorDocument` → the JS model and back (reuse Phase-D
     serialization),
   - mounts `coreEngine.createRenderHost` + `attachInput`,
   - bridges save/snapshot, undo/redo + dirty state, toolbar command dispatch
     (bold/align/style/table/image/link/find/comment/track-changes — all already exist as
     `host.*` APIs), selection, focus.
   - ✅ **Step 1 done (entry version 220, 293 Node + 19 core-engine E2E)**: the JS-side
     bridge facade `coreEngine.createCoreEditor({ root, doc, model, pageSettings, ariaLabel,
     autoFocus })` is built + verified — it mounts the host + input, maps toolbar command
     ids → host APIs (`execCommand`/`queryCommand`: bold/italic/underline/strike, align*,
     heading1-6/title/normal, textColor/highlight/font*, link, insertTable/insertImage,
     find/findNext/replaceAll, trackChanges, comment, setHeader/footer, undo/redo), and
     exposes `getModel/setModel/isDirty/markSaved/canUndo/canRedo/getOutline/getComments/
     focus/destroy`. Node `PhaseR8` + Playwright `R48` (mount→type→dirty, execCommand(bold)
     +queryCommand, markSaved clears dirty, undo). **This is the seam the C# host calls.**
   - ✅ **Step 1b done**: `CoreEngineModelConverter` (C#) — `ToCoreModel(DocumentEditorDocument)`
     → the JS engine model + `FromCoreModel(JsonElement)` back. v1 scope: paragraphs,
     headings (level/style), list/quote-as-paragraph, text runs, marks (bold/italic/
     underline/strikethrough/link/highlight/textColor/fontFamily/fontSize/comment-anchor),
     alignment. Round-trip C# tests `CoreEngineModelConverterTests` (2) green; the produced
     shape is exactly what the engine consumes (same shape as the 19 core-engine E2E +
     `PhaseR8`). **NOTE: tables / images / page-breaks are NOT round-tripped yet — they’d be
     dropped, so the hosted preview must start on docs without them (or extend the converter
     before enabling for rich docs).**
   - ✅ **Step 2 done (entry version 220)**: `TmDocumentCoreEngineHost.razor` (Blazor) +
     `wwwroot/js/document-editor/core-engine-interop.js` (ES-module shim: lazy-loads the
     IIFE bundle, `mount(elementRef, modelJson, optionsJson)` / `exec` / `query` /
     `getModelJson` / `isDirty` / `markSaved` / `canUndo` / `focus` / `dispose`, keyed by
     opaque handle id). Component: `OnAfterRender` imports the shim, `ToCoreModel`→mount;
     `ExecCommandAsync`/`QueryCommandAsync`/`RequestDocumentAsync`(→`FromCoreModel`)/
     `IsDirtyAsync`/`MarkSavedAsync`/`FocusAsync`/`DisposeAsync`. Demo page
     `/core-engine-host` (`CoreEngineHostPage.razor`). **WASM E2E `R49` (live Blazor↔JS):**
     converted C# doc renders as real positioned-DOM (heading role + "Bridge Demo"/"Hello
     world"), real-keyboard typing → DOM "Hello world!", round-trip back to C# →
     "Hello world!" + dirty=true. **The bridge works end-to-end.**
   - ✅ **Step 3a done (entry version 220)**: `TmDocumentEditor` now renders
     `TmDocumentCoreEngineHost` (instead of `TmDocumentWysiwygHost`) when
     `EffectiveRenderEngine == CoreEnginePreview`; `CoreEngineHostedInteropReady` flipped to
     `true` (host renders + edits + saves in-component — readiness now means "renderable
     preview", NOT full parity; default stays `Legacy` so nothing changes unless opted in).
     Demo `/core-engine-editor` (`CoreEngineEditorPage.razor`, in-memory provider). **WASM E2E
     `R50`**: `<TmDocumentEditor RenderEngine="CoreEnginePreview">` → root `data-render-engine=
     CoreEnginePreview`, the core host renders the document, the legacy contenteditable host is
     absent. Component suite unchanged (768 pass / 3 pre-existing fail).
   - ✅ **Step 3b (partial) done (entry version 220)**: the editor's **formatting + undo/redo
     toolbar commands route to the core host** when in preview — `UsingCoreEngine`
     property + `RouteToCoreEngineAsync(command,arg)` short-circuit at the top of
     `ToggleInlineMarkAsync` (bold/italic/underline/strikethrough), `ApplyParagraphAlignmentAsync`
     (align), `UndoAsync`/`RedoAsync`. `SyncCoreEngineStateAsync` pulls `isDirty` + `canUndo`/
     `canRedo` from the host; `EffectiveUndoState` ORs `_coreCanUndo/_coreCanRedo` so the
     toolbar Undo/Redo buttons enable. **WASM E2E `R51`**: select a line → toolbar **Bold**
     button → core segment computed bold; toolbar **Undo** button → reverts (both via the
     core engine). Legacy path unchanged (768 pass / 3 pre-existing fail).
   - ✅ **Step 3b — save wired (entry version 220)**: `SaveCoreAsync` has a `UsingCoreEngine`
     branch — pulls the live model via `_coreHost.RequestDocumentAsync()` → `CreateProviderBoundarySnapshot`
     → provider, and `MarkSavedAsync` + dirty/undo re-sync on success. **WASM E2E `R52`**:
     edit through the core engine → toolbar **Save** → the persisted document carries the
     core edit ("Edited by the core engine.!"). Legacy save path unchanged (768/3).
   - ✅ **Step 3b — font + color routing (entry version 220)**: `ApplyFontFamilyAsync` →
     `fontfamily`, `ApplyFontSizeAsync` → `fontsize`, `ApplyTextColorAsync` → `textcolor`,
     `ApplyHighlightColorAsync` → `highlight` now route to the core host in preview (guard
     reorder so the legacy `_wysiwygHost is null` early-return no longer blocks the core
     path). Node `PhaseR8` extended: `execCommand('textcolor','#ff0000')` + `('fontsize','20pt')`
     apply marks via the facade. Component suite 768/3 (unchanged).
     **Toolbar subset now routed: bold/italic/underline/strikethrough, align, fontFamily,
     fontSize, textColor, highlight, undo/redo, save, track-changes, links (`ApplyLinkAsync`/
     `RemoveLinkAsync` → `link`/`removelink`), tables (`InsertTableAsync` → `insertTable`).**
     Node `PhaseR8` extended: `execCommand('link',…)` applies a link mark + `execCommand(
     'insertTable',{rows,cols})` inserts a 2×3 table block via the facade. Component 768/3.
   - ⏳ **Step 3b (remaining — NOT clean one-line routes)**: images (dialog/upload flow),
     comments (compose dialog), find (separate find bar), lists (core engine has no list
     support yet → would be a no-op), headings/block-style (no simple toolbar callback —
     needs investigation); autosave;
     selection/formatting-state read-back (toolbar pressed-state from the engine);
     collaboration. Then the **full** `DocumentEditor*E2ETests` against the core engine +
     perf parity ≥ legacy on 30/100/500p — note this needs the demo/test harness to opt the
     suite into `CoreEnginePreview`, and many advanced-feature tests will fail until the
     above are wired (it is a parity-measurement pass, not a quick increment).

> **Pre-existing unrelated failures**: 3 `TmDocumentEditorTests` (PDF/Docx export + save
> boundary, `ParagraphBlockContent`→`ImageBlockContent` cast) fail on the clean tree too —
> NOT caused by the cutover work (verified by stashing the R.4.8 changes).
2. **Flip `CoreEngineHostedInteropReady = true`**; render `TmDocumentCoreEngineHost` when
   `EffectiveRenderEngine == CoreEnginePreview`.
3. **Run the full E2E suite against the core engine** (flag on); fix gaps to parity.
4. **Perf baseline** ≥ legacy on 30 / 100 / 500-paragraph docs (the layout cache from
   R.4.6i-2 helps; cold full-doc layout may still need true incremental relayout).
5. **Flip the default** to `CoreEnginePreview` (→ rename to `CoreEngine`); soak.
6. **Delete legacy** — `render(inst)` string path, contenteditable body, `buildLayoutSnapshot`
   DOM-readback, and the legacy-only modules — only after 3–5 are green. **Irreversible;
   requires explicit go-ahead.**

## Rollback

The flag defaults to `Legacy`; until step 5, nothing changes for users. After step 5 the
default can be reverted by setting `RenderEngine="Legacy"` (or flipping the default back)
until step 6 deletes the legacy path.
