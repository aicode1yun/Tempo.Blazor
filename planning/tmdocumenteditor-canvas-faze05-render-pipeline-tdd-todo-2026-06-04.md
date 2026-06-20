# Canvas engine - Faze 5: Canvas render pipeline (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 5** · Stav: hotovo · Priorita: P0

## Proč

Jádro vizuálu: model → layout → **display list** → canvas paint. Tato fáze staví deterministický display list a renderer pro page background, margins a základní text/heading/marks. Text measurement a plné line-breaking řeší Faze 6; tady jde o pipeline, vrstvy a první ostrý paint.

## Cílový stav

- Display list je deterministický pro stejný model (stejné pořadí a souřadnice draw příkazů).
- Renderer kreslí: page background + stín + okraje (margins) + body area; odstavcový text na jedné řádce; nadpisy; základní inline marks (bold/italic/underline/strike/color/highlight).
- Vrstvy: page/content (cache) vs selection/caret vs objects vs annotations vs diagnostics (overlay passy nad cache).
- Debug overlay je volitelný, nikdy default.
- Ostré na devicePixelRatio 1 a 2.

## Clean-room
- [x] Display list i painter vlastní; ONLYOFFICE `CGraphics`/`CPage` jen koncepční inspirace (per-page cache + overlay passy), kód nekopírovat.

## Znovupoužití
- [x] `layout/font-metrics.mjs` pro měření (diff 0.000–0.012px ověřeno).
- [x] `core-engine/render-host.mjs` pipeline (model→paragraph-engine→snapshot→renderer) jako reference; zde se mění render target na canvas.
- [x] `objects/geometry.mjs`, segment-style pro mark styly.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/display-list.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/canvas-renderer.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/layers.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/page-frame.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/__tests__/display-list.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/__tests__/renderer.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasRenderE2ETests.cs
```

## DoD
- [x] Display list deterministický (snapshot test).
- [x] Screenshot ostrý na 1x/2x; `AssertCanvasNonBlankAsync`.
- [x] Žádný debug overlay defaultně.

## Faze 5.1: Display list API a vrstvy

### 5.1.1 RED
- [x] `display-list.test.mjs`: pro daný layout vznikne seznam draw příkazů s typy (textRun, glyphRun, paragraphBox, tableBox, imageBox, field, comment/revision overlay) a vrstvou (content/selection/object/annotation/diagnostic).

### 5.1.2 GREEN + akceptace
- [x] `display-list.mjs` builder; `layers.mjs` definuje pořadí vrstev a kdo patří do cache vs overlay.

## Faze 5.2: Page frame (background, margins, body)

### 5.2.1 RED
- [x] `renderer.test.mjs`: page frame nakreslí pozadí, stín, margin box, body area v korektních souřadnicích pro A4 @ zoom 1.

### 5.2.2 GREEN + screenshot
- [x] `page-frame.mjs` + `canvas-renderer.mjs` kreslí stránku; E2E screenshot prázdné stránky, non-blank + UX (vypadá jako dokument, ne debug).

### 5.2.3 Akceptace
- [x] Stránka ostrá, okraje/stín profesionální.

## Faze 5.3: Paragraph text na jedné řádce

### 5.3.1 RED
- [x] Render jednoho odstavce: text na baseline v body area; pozice z font-metrics; deterministický display list.

### 5.3.2 GREEN + screenshot + akceptace
- [x] FillText přes renderer; E2E screenshot odstavce, pixelová kontrola content vrstvy (text je v body, ne v rohu); UX: správná baseline a padding.

## Faze 5.4: Headings + inline marks

### 5.4.1 RED
- [x] Nadpisy (větší/bold dle stylu) + marks bold/italic/underline/strike/color/highlight render testy.

### 5.4.2 GREEN + screenshot + akceptace
- [x] Mark styly z `segment-style`; underline/strike jako čáry; highlight jako rect pod textem; E2E screenshot, čitelnost.

## Faze 5.5: High-DPI a pixel snapping

### 5.5.1 RED
- [x] Test/skript ověří render @ devicePixelRatio 2 (canvas backing store scale, čáry nerozmazané).

### 5.5.2 GREEN + akceptace fáze 5
- [x] DPR scaling + pixel snapping čar; E2E screenshot 1x i 2x ostrý; debug overlay vypnutý.

## Implementační poznámky

Display-list pipeline je rozdělena do `layers.mjs`, `page-frame.mjs`, `display-list.mjs` a `canvas-renderer.mjs`. Canvas stack teď pro každý render sestaví deterministický seznam příkazů, vyčistí vrstvy a maluje page background/body/margins, text, nadpisy, inline marks, objektové a annotation vrstvy. `TmDocumentCanvasEngineHost` posílá canvas model přes camelCase JSON, aby JS canonical factory dostala reálný provider model, ne prázdný normalizovaný dokument.

Test evidence:

- `node --test src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/__tests__/display-list.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/render/__tests__/renderer.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/entry.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/model/__tests__/model.test.mjs` - 10/10.
- `dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore -v:minimal` - zelený build; existující warningy mimo fázi 5.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore -v:minimal` - zelený build.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorCanvasRenderE2ETests" --no-restore --no-build` - 1/1.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorCanvasHostE2ETests" --no-restore --no-build` - 1/1 regresní smoke.
- Screenshoty: `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase5-render/2026-06-04/desktop-1440x1000/`.

## Poznámky
- Cache/tiling a virtualizace = Faze 22; tady stačí jednoduchý full repaint, ale vrstvy už oddělené, aby overlay (caret/selection) nepřekresloval content (připraví Faze 7).
- Justify/wrap/pagination = Faze 6; v 5.3 text na jedné řádce (bez wrapu).
