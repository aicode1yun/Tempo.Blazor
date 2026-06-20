# Canvas engine - Faze 3: Blazor host a render flag (detailni TDD + E2E)

Datum: 2026-06-04 · Nadrazeny: master canvas plan, **Faze 3** · Stav: hotovo · Priorita: P0

## Proč

Canvas engine potřebuje Blazor host komponentu a feature flag, aby šel zapnout vedle Legacy a CoreEnginePreview, aniž by cokoli rozbil. Host je shell: canvas stack + a11y mirror + hidden input bridge + interop. Tato fáze zavádí produkční host lifecycle a explicitní preview render větev; další fáze rozšíří plný modelový převod a editaci.

## Cílový stav

- `DocumentEditorRenderEngine.CanvasEnginePreview` existuje; `Resolve` ho povolí jen při explicitním opt-in a default editoru zůstává beze změny.
- `TmDocumentCanvasEngineHost` renderuje canvas vrstvy, mirror root, hidden input; má `data-testid="document-canvas-engine-host"` a ready signál.
- `TmDocumentEditor` při `CanvasEnginePreview` renderuje canvas host místo legacy/core hostu.
- Dispose lifecycle korektně uvolní JS engine.

## Clean-room
- [x] Host i flag jsou vlastní; žádný ONLYOFFICE kód.

## Znovupoužití
- [x] Vzor `TmDocumentCoreEngineHost.razor` + ES interop lifecycle pro mount/dispose.
- [x] `EffectiveRenderEngine` / `Resolve` guard pattern z R.4.8.

## Doporučené nové soubory

```text
src/Tempo.Blazor/Components/DocumentEditor/TmDocumentCanvasEngineHost.razor(.cs)
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/interop.mjs
src/Tempo.Blazor.Demo.SharedUI/Pages/CanvasEngineHostPage.razor   (demo route /canvas-engine-host)
tests/Tempo.Blazor.Tests/DocumentEditor/CanvasEngine/CanvasEngineHostRenderTests.cs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasHostE2ETests.cs
```

## DoD
- [x] RED bUnit: flag renderuje canvas host, ne legacy/core.
- [x] Dispose nezůstává bez JS cleanupu (ověřeno dispose volání).
- [x] `dotnet build` zelený; default zůstává beze změny (nikdo neopt-in → nic se nemění).

## Faze 3.1: Enum flag + Resolve guard

### 3.1.1 RED
- [x] Test očekává `DocumentEditorRenderEngine.CanvasEnginePreview` a explicitní opt-in chování bez změny defaultu → RED.

### 3.1.2 GREEN
- [x] Přidat enum hodnotu; rozšířit `Resolve` o explicitní preview větev.
- [x] 2–3 C# testy na resolve chování.

### 3.1.3 Akceptace
- [x] Flag existuje, default zůstává beze změny, testy zelené.

## Faze 3.2: TmDocumentCanvasEngineHost lešení

### 3.2.1 RED
- [x] `CanvasEngineHostRenderTests` (bUnit): host vyrenderuje `data-testid="document-canvas-engine-host"`, canvas element(y), a11y mirror root (`role=document`), hidden input → RED (host neexistuje).

### 3.2.2 GREEN
- [x] Host markup: page/canvas stack kontejner, overlay canvas, `aria` mirror root, skrytý `<textarea>`/input mimo canvas.
- [x] Parametry: `Document` (`DocumentEditorDocument`), `ReadOnly`, `Permissions`, image/object providers.
- [x] OnAfterRenderAsync: lazy-load `interop.mjs`, `mount(...)`, uložit JS handle.

### 3.2.3 Akceptace
- [x] Host se vyrenderuje s testid + mirror + input; bUnit zelené.

## Faze 3.3: Interop ready/changed/state surface

### 3.3.1 RED
- [x] Test očekává, že host po mountu vyvolá ready callback a expose `changed`, `formattingState`, `undoState`, `selectionState`, `diagnostics` → RED.

### 3.3.2 GREEN
- [x] `interop.mjs`: `mount`, `dispose`, `getModelJson`, `setModel`, `isDirty`, `markSaved`, `focus`, `on(event)` (ready/changed/selection/formatting/undo).
- [x] DotNetObjectReference callback bridge (ready → host nastaví `_ready`).

### 3.3.3 Akceptace
- [x] Ready signál dorazí; state eventy vrací deterministický marshalovatelný stav; smoke zelený.

## Faze 3.4: Napojení na TmDocumentEditor + demo route

### 3.4.1 RED
- [x] bUnit: `<TmDocumentEditor RenderEngine="CanvasEnginePreview">` renderuje canvas host, ne legacy/core → RED.
- [x] E2E: demo route `/canvas-engine-host` ukáže prázdný/seed dokument na canvasu → RED.

### 3.4.2 GREEN
- [x] `TmDocumentEditor` větev pro `EffectiveRenderEngine == CanvasEnginePreview` → render `TmDocumentCanvasEngineHost`.
- [x] Demo `CanvasEngineHostPage.razor`.

### 3.4.3 Screenshot E2E + akceptace
- [x] E2E (harness z Faze 2): host renderuje, `AssertCanvasNonBlankAsync` (prázdná A4 stránka = ne blank, má okraje/pozadí).
- [x] UX review: vypadá jako dokumentová plocha.

## Faze 3.5: Dispose lifecycle

### 3.5.1 RED
- [x] Test: po Dispose komponenty se zavolá JS `dispose` a uvolní handle → RED.

### 3.5.2 GREEN + akceptace
- [x] `IAsyncDisposable` na hostu volá `dispose`; idempotentní; component suite beze změny.

## Evidence

- `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore` - zeleny build.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - zeleny build.
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~CanvasEngineHostRenderTests|FullyQualifiedName~DocumentEditorRenderEngineFlagTests" --no-restore --no-build` - 8/8.
- `node --test src/Tempo.Blazor/wwwroot/js/document-editor-canvas/entry.test.mjs` - 4/4.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorCanvasHostE2ETests" --no-restore --no-build` - 1/1.

Screenshoty:

```text
tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase3-host/2026-06-04/desktop-1440x1000/
  00-phase3-full.png
  01-phase3-editor.png
  02-phase3-canvas-host.png
  03-phase3-canvas-page.png
  manifest.json
```

UX verdikt: `03-phase3-canvas-page.png` je čistá dokumentová plocha bez demo navigace, debug prvků a nežádoucích překryvů.
