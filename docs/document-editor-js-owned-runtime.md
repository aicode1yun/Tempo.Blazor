# Document Editor JS-Owned Runtime

`TmDocumentEditor` now treats the JavaScript WYSIWYG runtime as the authority for live editing. Blazor owns the product shell: ribbon, panels, provider calls, save/import/export orchestration, localization, and status UI. The editable document DOM, selection, input transactions, formatting commands, image/table interactions, comments decorations, and revision decorations are owned by `tmDocumentEditorRuntime`.

## Runtime Boundary

- Blazor sends an initial snapshot through `tmDocumentEditorRuntime.loadDocument` when a document is loaded.
- During live editing, Blazor sends commands such as `toggleBold`, `setFontFamily`, `setParagraphAlignment`, `insertTable`, `insertImageUrl`, `reviewRevision`, and `syncHeaderFooterLayout` through `executeCommand` or dedicated runtime facade methods.
- The runtime emits granular patches and transaction state back to Blazor. Blazor mirrors those changes into the C# document model for provider save, export, collaboration, suggestion panels, and audit UI.
- `RefreshSnapshotAsync` is reserved for load/import/version restore, external provider synchronization, and recovery after a failed granular remote operation. It must not be used as the normal response to local typing, formatting, undo/redo, comments, track changes, table, image, or header/footer commands.

The older `tmDocumentEditorWysiwyg` / `tmDocumentWysiwyg` globals are no longer part of the runtime path. New component code must call `tmDocumentEditorRuntime`.

Side panels that need live editor state should read through `tmDocumentEditorRuntime.getSidePanelSyncState(instanceId)`. The method returns a selection-driven projection for the properties panel, image tools, revision panel, and comment anchors so Blazor controls can update from the current runtime selection without forcing a document snapshot reload.

## Provider Boundary

Provider contracts in `Tempo.Blazor.Abstractions` remain the public boundary for host applications:

- `IDocumentEditorProvider` persists the canonical `DocumentEditorDocument`.
- `IDocumentFormatProvider` handles DOCX and other server-side import/export.
- `IDocumentPdfExportProvider` handles server-side PDF generation.
- `IDocumentCollaborationProvider` exchanges structured operation batches.
- Font, rendition, token, comparison, and suggestion providers stay outside the DOM runtime.

The provider boundary exchanges structured document models and operations, not rendered HTML.

## Collaboration Flow

Local edits are committed in the JS runtime, emitted as patches, mapped into `DocumentOperation` values, and sent to the collaboration provider as append-only batches. Remote batches arrive through the realtime provider path and are applied directly to the JS runtime with `applyRemoteOperationBatch`.

Successful remote operations do not force a Blazor re-render of the editable surface. If a remote operation cannot be applied granularly, the editor records a recovery message and reloads a synchronized snapshot so the DOM and provider model converge.

## Undo And Redo Rules

- Typing is grouped into runtime transactions.
- Formatting commands and structural commands create runtime undo items with before/after selection state.
- Undo/redo calls go to `tmDocumentEditorRuntime.undo` and `tmDocumentEditorRuntime.redo` while the WYSIWYG host is active.
- The C# command stack is only a fallback for non-runtime paths and provider-side recovery.
- A snapshot load resets the runtime saved marker; a local transaction marks the runtime dirty.

## Track Changes Model

Track changes is evaluated in the JS runtime before rendering. Insertions, deletions, and formatting changes produce pending revision decorations in the DOM and matching revision records in the mirrored C# model. Accept/reject actions are commands against the runtime; Blazor updates panels and provider state after the runtime confirms the review action.

Deletion revisions must remain visible as red struck text until accepted or rejected. Insertions must remain visible as pending inserted text. The review panel is a projection of the same runtime-backed revision model, not a separate editor surface.

## Writing Document Editor E2E Tests

Prefer testing the regular demo editor route rather than a synthetic fixture. New tests should:

- Open the page through `OpenDocumentEditorPageAsync`.
- Wait for `[data-testid='document-wysiwyg-host']` and `WaitForWysiwygBodyAsync`.
- Interact with the real ribbon buttons, document DOM, side panel, and provider controls.
- Assert JS-owned behavior with `window.tmDocumentEditorRuntime` and `window.tmDocumentEditorDebug` only for diagnostics or runtime invariants.
- Avoid asserting raw Blazor render counts for the editable surface; assert DOM results, selection, undo state, dirty state, provider payloads, and absence of unexpected snapshot reloads.
- Save debug artifacts when layout, selection, or visual regressions fail.

Representative coverage should include typing, undo/redo, formatting, track changes, comments, images, tables, headers/footers, collaboration, save/reload, DOCX import/export, PDF export, and document comparison.

## Migration Notes

There is no `UseJsOwnedRuntime` switch. The WYSIWYG editor always uses the JS-owned runtime path.

Consumers that only use public provider contracts do not need to change their code. Integrations that called internal WYSIWYG JS APIs directly should move to `tmDocumentEditorRuntime`; direct dependence on C# snapshot refreshes during live editing is no longer supported.
