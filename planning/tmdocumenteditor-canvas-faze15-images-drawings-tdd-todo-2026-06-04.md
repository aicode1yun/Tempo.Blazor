# Canvas engine - Faze 15: Images and drawings (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 15** · Stav: ověřeno · Priorita: P1 (předpoklad E7)

## Proč

Obrázky a inline/floating drawings na canvasu: render, insert (URL/upload/provider), selection handles, resize/move, wrap módy, z-order, caption, alt text, inspector. Toto je základ, na kterém E7 staví tvary/grafy/text boxy. Legacy uměl obrázky; canvas to musí dorovnat a překonat.

## Cílový stav

- Standalone image render + inline drawing render.
- Image URL resolver; upload/provider asset insert.
- Selection handles; resize s aspect lockem; move/drag.
- Wrap módy: inline, square, tight/through (kde model podporuje), top-bottom, behind/in-front.
- Z-order; caption; alt text warning; image inspector + floating toolbar.
- Save/reload image layout.

## Clean-room
- [x] Wrap/anchor geometrie vlastní (reuse fáze D); ONLYOFFICE jen inspirace.

## Znovupoužití (velké — existující objects/ infra)
- [ ] `objects/image-insert.mjs`, `image-object.mjs`, `drawing-runs.mjs`, `drawing-kind.mjs`, `image-resize*.mjs`, `image-move-track.mjs`, `image-move-snap.mjs`, `image-preview-controller.mjs`.
- [ ] `objects/anchored-drawing-layout.mjs`, `anchored-drawing-position.mjs`, `anchor-region.mjs`, `horizontal-position.mjs`, `wrap-modes.mjs`, `overlap-geometry.mjs`.
- [ ] `layout/text-exclusion*.mjs`, `available-intervals-cache.mjs`, `blocked-intervals.mjs` (obtékání z fáze D).
- [x] `core-engine/object-overlay.mjs`; C# `TmDocumentImageInspector`, `TmDocumentImageWrapPanel`.
- [x] ImageUrlResolver/ImageProvider/ImageAssetOptions providery.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/objects/image-render.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/objects/object-handles.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/objects/__tests__/image-render.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/objects/__tests__/wrap-layout.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasImageE2ETests.cs
```

## DoD
- [x] Object handles ostré, nepřekrývají toolbar.
- [x] Text obtéká bez overlapu (`AssertNoTextOverlapAsync`).
- [x] Save/reload image layout; undo gate.

## Faze 15.1: Image/inline drawing render

### 15.1.1 RED
- [x] `image-render.test.mjs`: standalone image box + inline drawing run render na správné pozici/velikosti; lazy load přes resolver.

### 15.1.2 GREEN + screenshot + akceptace
- [x] `image-render.mjs` (drawImage přes renderer); E2E render obrázku.

## Faze 15.2: Insert (URL/upload/provider)

### 15.2.1 RED
- [x] Insert image z URL, upload, provider asset na caret pozici; validace (ImageAssetOptions).

### 15.2.2 GREEN + screenshot + akceptace
- [x] Reuse image-insert + providery; E2E insert z URL přes canvas command runtime.

## Faze 15.3: Selection handles + resize/move

### 15.3.1 RED
- [x] 8 resize handles; resize s aspect lockem; move/drag; object selection oddělená od text selection.
- [x] Snap pro pointer move/resize.

### 15.3.2 GREEN + screenshot + akceptace
- [x] `object-handles.mjs`; image-resize/move-track; E2E pointer resize + move; undo gate.
- [x] `image-move-snap.mjs` / snap akceptace pro pointer move/resize.

## Faze 15.4: Wrap módy + obtékání

### 15.4.1 RED
- [x] `wrap-layout.test.mjs`: inline/square/tight/through/top-bottom/behind/in-front; text obtéká floating objekt bez overlapu; posun přepočítá.

### 15.4.2 GREEN + screenshot + akceptace
- [x] Reuse anchored-drawing-layout + wrap-modes + text-exclusion; E2E square wrap, `AssertNoTextOverlapAsync`.

## Faze 15.5: Z-order, caption, alt text, inspector

### 15.5.1 RED
- [x] Z-order bring-to-front/back; caption; alt text warning; image inspector + floating toolbar; save/reload layout.

### 15.5.2 GREEN + screenshot + akceptace fáze 15
- [x] Reuse inspector/wrap panel; E2E caption + alt + z-order UI; save/reload zachová layout.
- [x] Screenshot: handles ostré, text obtéká bez overlapu.

## Implementační poznámky 2026-06-04

- Přidáno canvas image renderování přes `image-render.mjs`, object handles přes `object-handles.mjs`, undoable image commandy a selection overlay pro move/resize.
- Doplněn canvas image command bridge pro URL/upload/provider asset insert, wrap/size/position/anchor/z-order/metadata aliasy a C# image inspector napojený na canvas formatting state.
- `DocumentEditorCanvasImageE2ETests` ověřuje phase 15 seed, render obrázku/drawing runu, 8 handles, image inspector, save/reload, caption v accessibility mirroru, alt warning a lokální object-vs-text wrap invariant.
- Ověření: `node --test` canvas sada 84/84; `DocumentEditorCanvasImageE2ETests` 1/1; `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj --no-restore` prošel s existujícími warningy.
- Ověření 2026-06-05: `DocumentEditorCanvasImageE2ETests` 1/1 prošel proti WASM demo na `https://localhost:7106`; test ukládá screenshoty `00-phase15-images-before.png`, `01-phase15-images-selected.png`, `02-phase15-images-after-reload.png` a manifest do `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase15-images/2026-06-04/desktop-1440x1000/`.
- Ověření 2026-06-06: Doplněn a znovu ověřen skutečný pointer move/drag + resize přes myš, undo/redo gate, save/reload geometrie, `AssertNoTextOverlapAsync`, caption-vs-text overlap gate a screenshoty `00-phase15-images-before.png`, `01-phase15-images-selected.png`, `02-phase15-images-pointer-move-resize.png`, `03-phase15-images-after-reload.png`; `DocumentEditorCanvasImageE2ETests` 1/1 prošel proti WASM demo na `https://localhost:7106`.
- Ověření 2026-06-06: Doplněn produkční snap pro pointer move/resize přes `image-move-snap.mjs`, grid/body/object guides, Alt precision režim a snap guide diagnostika v `selection-controller.mjs`; `node --test` relevantní canvas image/render/layout sada 27/27, `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` a `DocumentEditorCanvasImageE2ETests` 1/1 prošly. Screenshoty a manifest potvrzují move snap `left/top` na grid, resize snap `right` na grid a save/reload geometrii `x=48, y=56, width=224, height=128`.
- Zbývá poctivě dořešit: žádná otevřená funkční položka fáze 15; obecný katalog legacy reuse výše zůstává neodškrtnutý tam, kde nebyl proveden a ověřen 1:1 přenos všech vyjmenovaných modulů.

## Poznámky
- Tvary/text boxy/čáry/grafy/skupiny = E7 (staví přesně na téhle infra).
- Image paste = Faze 11; image v hlavičce/patičce = Faze 16.
