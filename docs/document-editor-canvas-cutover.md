# Document Editor Canvas Cutover

Date: 2026-06-07

## Default Engine

`TmDocumentEditor` now defaults to `DocumentEditorRenderEngine.CanvasEnginePreview`. The canvas engine became the default after the phase 24 parity gate covered legacy phases 0-23 and extended phases E1-E12.

```razor
<TmDocumentEditor DocumentId="@documentId"
                  Provider="@provider" />
```

The component still exposes the `RenderEngine` parameter for explicit selection.

## Rollback

Set `RenderEngine` to `DocumentEditorRenderEngine.Legacy` to return one editor instance to the legacy contenteditable engine.

```razor
<TmDocumentEditor DocumentId="@documentId"
                  Provider="@provider"
                  RenderEngine="DocumentEditorRenderEngine.Legacy" />
```

The demo route supports the same rollback from the browser:

```text
/document-editor?renderEngine=legacy
```

The canvas route remains available for targeted smoke and parity runs:

```text
/canvas-engine-host?documentId=phase-24-canvas-parity-seed&showToolbar=true
```

## Compatibility

Legacy and `CoreEnginePreview` remain in the codebase during the phase 26 soak period. Do not remove legacy markup, JavaScript, selectors, provider contracts, or diagnostic tests until phase 26 is explicitly approved.

Provider integrations continue to use the same abstractions:

- `IDocumentEditorProvider`
- `IDocumentImageProvider`
- `IDocumentFormatProvider`
- `IDocumentPdfExportProvider`
- `IDocumentComparisonProvider`
- `IDocumentSuggestionProvider`
- `IDocumentCollaborationProvider`
- `IDocumentOfflineStore`
- `IDocumentSyncProvider`

## Verification

Before declaring a cutover build ready, run:

```bash
dotnet test
dotnet test tests/Tempo.Blazor.E2E/
```

Manual smoke must still cover typing, toolbar formatting, tables, images, comments, revisions, import/export, collaboration, offline recovery, and mobile toolbar overflow.
