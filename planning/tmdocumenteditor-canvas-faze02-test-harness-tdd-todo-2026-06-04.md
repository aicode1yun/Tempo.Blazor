# Canvas engine - Faze 2: Test harness a screenshot evaluator (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 2** · Stav: hotovo · Priorita: P0 (infra, předchází všem viditelným fázím)

## Proč

Master plán vyžaduje, aby žádná viditelná fáze nebyla bez screenshot gate. Tato fáze postaví sdílenou E2E + screenshot infrastrukturu, na které stojí všechny ostatní fáze (0–26, E1–E12). Bez ní by se gates psaly ad-hoc a nekonzistentně.

## Cílový stav

- Jeden helper otevře canvas engine demo a vrátí page object s přístupem ke canvasu, overlayům, toolbaru a a11y mirroru.
- Screenshot helpery ukládají deterministicky pojmenované obrázky (full / editor / canvas crop / focused control) do strukturovaného adresáře + manifest.json.
- Pixel/rect asserty detekují prázdný canvas, text/UI overlap, neviditelný caret/selection.
- Agent UX/UI review krok je součástí workflow (otevřít after screenshot, zapsat verdikt).

## Clean-room

- [x] Žádný kód ani fixtures z ONLYOFFICE; harness je čistě Playwright/.NET vlastní.

## Doporučené nové soubory

```text
tests/Tempo.Blazor.E2E/CanvasEngine/CanvasEngineTestBase.cs
tests/Tempo.Blazor.E2E/CanvasEngine/CanvasEnginePage.cs
tests/Tempo.Blazor.E2E/CanvasEngine/DocumentEditorCanvasVisualAssert.cs
tests/Tempo.Blazor.E2E/CanvasEngine/CanvasVisualReviewManifest.cs
tests/Tempo.Blazor.E2E/CanvasEngine/CanvasPixelMetrics.cs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasHarnessE2ETests.cs
```

## DoD (dědí z master + navíc)

- [x] RED test selže na prázdném/chybějícím canvasu.
- [x] Helpery jsou použity minimálně jedním zeleným smoke testem.
- [x] `dotnet build` zelený.

## Faze 2.1: OpenCanvasEngineDocumentAsync

### 2.1.1 RED
- [x] `DocumentEditorCanvasHarnessE2ETests`: `OpenCanvasEngineDocumentAsync(seedId)` zatím neexistuje → test nekompiluje/selže.

### 2.1.2 GREEN
- [x] `CanvasEnginePage` page object: navigace na statický canvas harness, čekání na `data-testid="document-canvas-engine-host"` (ready signál z hostu), expose: `Canvas`, `OverlayCanvas`, `Toolbar`, `A11yMirror`, `HiddenInput`.
- [x] `OpenCanvasEngineDocumentAsync(seedId, viewport)` nastaví viewport a načte seed dokument.

### 2.1.3 Akceptace
- [x] Helper otevře demo a vrátí page object; smoke projde.

## Faze 2.2: Screenshot helpery a adresářová struktura

### 2.2.1 RED
- [x] Test očekává `00-before-full.png` … `03-after-editor.png` + `manifest.json` v `TestResults/document-editor-canvas/{class}/{test}/{viewport}/` → soubory vznikají deterministicky.

### 2.2.2 GREEN
- [x] `CaptureFullAsync`, `CaptureEditorAsync`, `CaptureCanvasCropAsync(rect)`, `CaptureControlAsync(selector)`.
- [x] `CanvasVisualReviewManifest`: serializuje test name, viewport, seed id, user actions, expected visible/model changes, screenshot paths, metriky.

### 2.2.3 Akceptace
- [x] Po scénáři vznikne kompletní sada souborů + manifest.

## Faze 2.3: Pixel a rect asserty

### 2.3.1 RED
- [x] Testy pro `AssertCanvasNonBlankAsync`, `AssertTextPixelsChangedAsync`, `AssertCaretVisibleAsync`, `AssertSelectionVisibleAsync`, `AssertNoTextOverlapAsync`, `AssertNoUiOverlapAsync`, `AssertToolbarStateMatchesModelAsync` — RED proti prázdnému/rozbitému renderu.

### 2.3.2 GREEN
- [x] `CanvasPixelMetrics`: čte canvas přes `getImageData` z JS, počítá non-blank ratio, barevnou varianci a bbox změněných pixelů.
- [x] Rect overlap asserty čtou bounding rects z DOM/overlay metadat.
- [x] `AssertCanvasNonBlankAsync` fail na jednobarevném/prázdném canvasu.

### 2.3.3 Akceptace
- [x] Všechny asserty mají RED i GREEN případ; falešně neprochází blank render.

## Faze 2.4: Agent UX/UI review workflow

### 2.4.1
- [x] Definovat krok: po každém screenshot E2E agent otevře after screenshot (`view_image`) a zapíše UX/UI verdikt do manifestu / test outputu.
- [x] `AssertScreenshotLooksIntentionalAsync` implementován jako kontrola existence screenshotů + zápis reviewer notes; dokumentace postupu je v master plánu.

### 2.4.2 Akceptace
- [x] Workflow zapsán; manifest má pole `uxReviewerNotes`.

## Faze 2.5: Multi-viewport matice

### 2.5.1
- [x] Parametrizace viewportů: desktop 1440x1000, notebook 1280x800, tablet 900x1100, mobil 390x844.
- [x] Helper `ForEachViewport` nebo MSTest `DynamicData`.

### 2.5.2 Akceptace fáze 2
- [x] Smoke běží na všech viewportech; žádná další viditelná fáze nesmí být bez screenshot gate (zapsat jako pravidlo).
- [x] RED (blank) selže, GREEN (page canvas) projde.

## Poznámky
- Harness je závislost pro Faze 3+; statická route `canvas-engine-harness.html` zůstává stabilní smoke host a Faze 3 přidá reálný Blazor host na stejný `data-testid` kontrakt.
- Pixel čtení z canvasu řešit přes JS eval (Playwright `EvaluateAsync`), ne přes OS screenshot, kvůli determinismu DPR.

## Implementační evidence

- Helpery: `tests/Tempo.Blazor.E2E/CanvasEngine/CanvasEngineTestBase.cs`, `CanvasEnginePage.cs`, `DocumentEditorCanvasVisualAssert.cs`, `CanvasVisualReviewManifest.cs`, `CanvasPixelMetrics.cs`.
- Smoke a RED/GREEN kontrakt: `tests/Tempo.Blazor.E2E/DocumentEditorCanvasHarnessE2ETests.cs`.
- Host signál a seed query: `src/Tempo.Blazor.Demo/wwwroot/canvas-engine-harness.html`.
- Screenshot evidence: `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/DocumentEditorCanvasHarnessE2ETests/Phase2_OpenCanvasEngineDocument_CapturesScreenshotsManifestAndPassesSmokeGates/{viewport}/`.
- Ověření: `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` a `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorCanvasHarnessE2ETests" --no-restore --no-build`.
