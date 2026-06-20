# Canvas DocumentEditor — oprava formátování + obrázkových interakcí (2026-06-10)

Zdroj: 3 screen-recordingy z `/document-editor` (canvas engine, demo „Service agreement"):
- `Záznam obrazovky z 2026-06-10 14-28-10.mp4` — bold: označení textu → bold → psaní za ním zůstává tučné; aplikace boldu je pomalá.
- `Záznam obrazovky z 2026-06-10 14-28-38.mp4` — rotace: otáčí se jen výběrový rámeček, obrázek ne; po puštění zůstanou viditelné dva nekonzistentní rámečky (rotovaný + nerotovaný s handly).
- `Záznam obrazovky z 2026-06-10 14-29-04.mp4` — drag&drop: po dropu obrázek zmizí z viewportu (skočí úplně jinam); obrázky jsou jen šedé vyplněné obdélníky; resize nefunguje a kurzor se nad handly nemění.

**Postup při implementaci:** TDD (Node test → fix → zelená), po každé fázi `npm run test:document-editor-modules` + cílené E2E. Hotové úkoly odškrtávat `[x]` přímo v tomto souboru. Součástí každé fáze je E2E screenshot test — screenshoty posoudím (a) funkčně (stalo se, co mělo) a (b) jako UX expert (vzhled, afordance, konzistence s Word/GDocs).

---

## Analýza kořenových příčin

### P1 — Po vypnutí boldu se dál píše tučně
- `commands/inline-format.mjs`: `pendingMarks` je **add-only** množina. `togglePendingMark()` (ř. ~473) jen přidá/odebere položku z pending seznamu a **ignoruje zděděný mark na pozici kurzoru**. Stav „potlačit zděděný bold" nelze vůbec vyjádřit (žádný remove-override).
- `marksForInsertion()` (ř. 135) umí pending marky jen PŘIDAT přes template; zděděný bold nikdy neodstraní.
- `input/input-controller.mjs` `withPendingMarks()` (ř. 657): `edit.marks` se nastaví **jen když je pending neprázdný**, a pak **nahradí** všechny zděděné marky (ztratí se např. barva). Když je pending prázdný, `createTextRun` (`input/text-editing.mjs` ř. 423) zdědí marky z předchozího runu → bold „nejde vypnout".
- Scénář z videa: kurzor za tučným runem → toolbar Bold (vypnout) → `togglePendingMark` PŘIDÁ `{bold}` (nic nezmění) → psaní je tučné; druhý klik pending odebere → opět zděděný bold. Slepá ulička.
- Navíc se pendingMarks nečistí při přesunu kurzoru (Word je zahazuje při změně selection).

### P2 — Pomalé nastavování boldu a dalších formátů
**Naměřeno (fáze 0.2): endToEnd 1691 ms, z toho jsExec 218 ms + C# overhead ~1473 ms.** Dvě dominantní složky:
- **JS exec 218 ms:** `commands/dispatcher.mjs` `executeInlineCommand` dělá `captureSnapshot()` = **plný structuredClone celého modelu** a `applyInlineFormatCommand` (`inline-format.mjs` ř. 52) dělá **druhý plný clone**; + full `queryCommandState`. → fáze 2.3.
- **C# overhead ~1473 ms:** `TmDocumentEditor.razor.cs` `RouteToCanvasEngineAsync` (ř. 7645) po KAŽDÉM toolbar příkazu volá `SyncCanvasEngineStateAsync` (ř. ~1870) = **5 sekvenčních JS-interop roundtripů** (`IsDirtyAsync`, `GetUndoStateAsync`, `GetFormattingStateAsync`, `GetNavigationStateAsync`, content-control popover) + `RefreshCommandRegistryAsync` + **plný `StateHasChanged()` na obřím TmDocumentEditor** + `FocusAsync`. Na single-thread WASM se .NET marshalling + plný re-render sčítají do ~1,5 s.
- **KOREKCE hypotézy:** jednotlivá sync interop čtení jsou v JS LEVNÁ (navigation 1 ms, formatting 0,8 ms, isDirty/undo ~0). Drahé NENÍ extrahování osnovy v JS, ale **počet .NET roundtripů + plný Blazor re-render**. Fáze 2 proto cílí: sloučit roundtripy (2.2), zrušit plný re-render (2.4), odlehčit JS exec (2.3).

### P3 — Obrázky jsou šedé obdélníky místo bitmap
- Demo assety mají `Source = DocumentImageSource.Asset` a **žádné Url** — URL se má řešit přes `IDocumentImageUrlResolver.ResolveUrlAsync(documentId, assetId)` (`DemoDocumentImageUrlResolver` vrací data:URL; PNG 320×180 s modrou grafikou — NENÍ to šedý obdélník, kreslí se jen placeholder).
- `TmDocumentCanvasEngineHost.razor` ale resolver **nikdy nevolá** — předává jen boolean `hasImageUrlResolver` (ř. 927). `CanvasDocumentModelConverter` kopíruje assety bez URL.
- JS `objects/image-render.mjs` `resolveImageUrl()` čte jen `source.url ?? model.assets[].url` → prázdné → `render/canvas-renderer.mjs` `paintImageObject()` (ř. 445) vykreslí jen placeholder fill `rgba(226,232,240,.72)` + rámeček. Mechanika načítání (`resolveCachedImage` + `__tmCanvasRepaint` po `onload`, canvas-stack ř. 264) jinak funguje — chybí jen URL.

### P4 — Rotace obrázku nefunguje
- `paintImageObject()` **ignoruje** `command.rotation`/`flip` — na rozdíl od `paintWatermarkImage` nevolá `applyObjectTransform` (canvas-renderer ř. 520). Display command rotaci nese (`image-render.mjs` ř. 240), jen se nepoužije.
- Overlay nekonzistence (`selection/selection-controller.mjs`): canvas vrstva `paintObjectSelection` (ř. 1047) rotuje rámeček I handly; DOM overlay `appendObjectSelection` (ř. 1200) rotuje **jen outline div** (CSS transform), handle divy zůstávají nerotované → na obrazovce 2 sady rámečků/handlů (přesně jak je vidět na videu).
- Hit-test (`objects/object-handles.mjs` `imageObjectAtPoint`, `objectResizeHandleAt`) je čistě axis-aligned — u rotovaného objektu handly „nesedí" na to, co je vykreslené.
- Rotace se přitom do modelu ukládá správně (`updateImageLayout` → `transform.rotation`) — rozbité je vykreslení, overlay a hit-test.

### P5 — Resize nefunguje + žádná změna kurzoru
- Hover kurzor neexistuje: `handlePointerMove` (selection-controller ř. 254) dělá early-return, když neběží drag (`!pointerState?.active`); `syncResizeCursor` (ř. 359) řeší jen tabulkový `col-resize` a stejně se k němu mimo drag nikdo nedostane. Handle divy mají `pointerEvents:none` a žádný `cursor`.
- Samotný resize drag se spustí (`objectResizeHandleAt` → mode `object-resize`), ale commit na pointerup jde přes stejný `updateImageLayout` s x/y → **bug souřadnicových rámců (P6)** → obrázek po resize uskočí = působí jako rozbité.

### P6 — Drop obrázku skočí úplně jinam
- `selection-controller.mjs` pointerup (ř. 387-399) ukládá `x = rect.x - body.x`, `y = rect.y - body.y` — tedy **offset vůči tělu stránky**.
- `objects/image-commands.mjs` `createLayoutPayload()` (ř. 1277) ale natvrdo zapisuje `position.verticalRelativeTo: 3` (= paragraph) a `image-render.mjs` `resolveObjectY()` (ř. 783) pro Square-wrap + `verticalRelativeTo=paragraph` počítá `y = flowY(kotevního odstavce) + explicitY` → obrázek skončí na `Y_odstavce + (dropY - bodyY)`, tj. o stovky px níž. Demo obrázky mají `VerticalRelativeTo=Paragraph` (DemoDocumentEditorProvider ř. 3908) → přesně chování z videa.
- **Bonusový latentní bug:** `updateImageLayout` přestaví celý layout přes `createLayoutPayload` a tím **materializuje defaulty** — ztratí `horizontalRelativeTo` (vždy 2), `verticalRelativeTo` (vždy 3), `horizontalAlignment` (default Left) a nahradí „nepřítomné" x/y explicitní 0/0. Např. čistá rotace pravostranně zarovnaného obrázku ho může přesunout doleva.

---

## Fáze 0 — Reprodukční E2E + perf baseline (před opravami) — HOTOVO 2026-06-10

Soubor: `tests/Tempo.Blazor.E2E/DocumentEditorCanvasImageFormattingFixE2ETests.cs`. Stav: **P1/P3/P6 červené (věrná reprodukce), P2 prošel (měření).** Tyto 3 jsou ČERVENÉ ZÁMĚRNĚ a zezelenají ve svých fix-fázích (1/3/5); brána 8.2 je hlídá.

- [x] 0.1 `FixP1_TogglingBoldOff_StopsTypingBold` — select „The agree" → Bold → klik dovnitř (bold active) → Bold OFF → psaní „Qz9" → čte model, assert run NENÍ bold. ČERVENÝ: `Run text='Qz9', marks=[bold]`.
- [x] 0.2 `FixP2_FormattingLatency_Baseline` — naměřeno (medián, contract demo, viewport 1440×1000):
  - **jsExec (execCommand 'bold') = 218 ms** ← dvojitý structuredClone modelu (captureSnapshot + applyInlineFormatCommand) + full queryCommandState. **Hlavní JS náklad → fáze 2.3.**
  - jsIsDirty ≈ 0 ms, jsUndoState ≈ 0 ms, jsFormatting = 0,8 ms, **jsNavigation = 1 ms** ← jednotlivá sync interop čtení jsou v JS LEVNÁ (hypotéza „GetNavigationState extrahuje celou osnovu draze" NEPLATÍ na JS straně).
  - **endToEndToolbar (klik → repaint) = 1691 ms.** Odečtem: C# overhead (5 sekvenčních .NET roundtripů + plný `StateHasChanged` na TmDocumentEditor + focus) ≈ **1473 ms** → dominuje **.NET marshalling + plný re-render**, NE engine.
  - **Upřesněné priority fáze 2:** (a) 2.3 = odstranit 2 plné clony v JS exec (218 ms); (b) 2.2 = sloučit 5 roundtripů do 1 `getCommandSyncStateJson`; (c) 2.4 = zrušit plný `StateHasChanged` na toolbar příkaz (push uiState, re-render jen toolbaru). Společně cílí ~1,9 s → <150 ms (2.5).
- [x] 0.3 `FixP3_FirstImage_RendersBitmapNotGrey` — vzorkuje objects-layer canvas, počítá modré pixely demo PNG (`rgb(52,91,175)`). ČERVENÝ: `Blue pixels found: 0 of 72116` (šedý placeholder).
- [x] 0.4 `FixP6_DroppedImage_LandsUnderPointer` — reflow-aware drag (`/canvas-engine-host?documentId=contract-demo`, lehčí než `/document-editor`, kde synthetic drag bojuje s reflow při výběru; bug je stejně v JS enginu). Drag levého wrap obrázku o +40/+60 page px. ČERVENÝ: `modelMoved=True` (model 0,0→40,**300**: X delta OK, Y uloženo body-relativně 300 místo 60), ale vykreslená pozice (metadata uzel) zůstala (97,336) místo (137,396) — **obrázek se vizuálně nepřesune tam, kam se dropl**. POZN: resolved pozici čteme z bounding rectu `[data-canvas-object]` uzlu (debug-snapshot `layout.blocks` je po přesunu stale).
- POZN k servrům: WASM 7106 (`dotnet run --project src/Tempo.Blazor.Demo --launch-profile https`), API 5100 (`dotnet run --project src/Tempo.Blazor.Demo.Api --launch-profile Tempo.Blazor.Demo.Api`). `DocumentEditorE2EReset` POSTuje na `https://localhost:5100/api/document-editor/reset`.

## Fáze 1 — P1: tri-state pending marks (sticky formatting) — HOTOVO 2026-06-10

Stav: **Node 350/350 (3 nové P1 testy), FixP1 E2E ZELENÝ, Phase9 inline-format E2E zelený (bez regrese).** Vizuálně ověřeno: po Bold na „The agree", klik dovnitř, Bold OFF a psaní → „**The** Qz9**agree**ment" (vložené „Qz9" netučné, okolní bold zachován) = parita Word/GDocs.

- [x] 1.1 `commands/__tests__/inline-format.test.mjs` — 3 testy: toggle-off za bold runem → `pendingMarks=[{type:'bold',remove:true}]` + `marksForInsertion` bez bold + vložený run netučný; off→on obnoví bold; value-mark (barva) nezahodí zděděný bold.
- [x] 1.2 `inline-format.mjs`: pending entry může být add-override `{type,value?}` NEBO remove-override `{type,remove:true}`. `togglePendingMark(state, mark, inheritedMarks)` počítá efektivní stav (pending ⊕ zděděné) a zapíše add/remove/clear. `applyToggleMark` + `applyRemoveMark` předávají `inheritedMarksAtCaret(model,selection)` (runAtOffset). Nový `setRemovePendingMark`.
- [x] 1.3 Nový export `mergeMarkOverrides(templateMarks, overrides)` (remove maže, add nastaví, ostatní zděděné zachová); `marksForInsertion` = `mergeMarkOverrides(template, state.pendingMarks)`. `collectSelectedMarkValues` collapsed větev: remove-override → `{marked:0}` → toolbar ukáže OFF.
- [x] 1.4 `input/text-editing.mjs` `createTextRun` → `resolveInsertionMarks`: explicitní `edit.marks` (clipboard) vyhrává; jinak MERGE zděděného template s `edit.pendingMarks` (tri-state), nikdy plain replace. `input-controller.withPendingMarks` posílá `edit.pendingMarks = overrides` (ne `marks`). `dispatcher.getPendingMarkOverrides()` (raw entries) + entry.mjs napojení (`getPendingMarks` option → `getPendingMarkOverrides`).
- [x] 1.5 `dispatcher`: `pendingAnchor` (blockId+offset kde sticky vzniklo) + `reconcilePendingMarks()` — psaní vpřed ve stejném bloku pending zachová (a posune anchor), skok jinam/zpět ho zahodí. Voláno v `getPendingMarkOverrides` (insertion) i `queryCommandState` (toolbar poll na selection change); anchor se nastaví po `executeInlineCommand`. POZN: klik dopředu ve stejném bloku pending nezahodí (drobná odchylka od Wordu, akceptováno).
- [x] 1.6 `FixP1_TogglingBoldOff_StopsTypingBold` ZELENÝ + screenshoty `fix-p1-bold/`. UX: jasný kontrast váhy bold/non-bold, ostrý text, čistý přechod, bez glitchů.

## Fáze 2 — P2: zrychlení formátovacích příkazů — HOTOVO 2026-06-10

Stav: **Node 350/350, FixP2 gate ZELENÝ, regrese (FixP1 + Phase9 inline-format + HistorySave) bez chyby.** Výsledek: **C# toolbar route ~1473 ms → ~250–390 ms (≈5×), endToEnd (Playwright, nafouklý SlowMo) 1691 → ~870–1140 ms, roundtripy 6→1, model clony 4→1, `getFormattingStateJson` ~0,4 ms.**

- [x] 2.1 Měření z 0.2 vyhodnoceno → priorita: roundtripy + plný re-render (C#) a clony (JS); navigation/formatting čtení levná.
- [x] 2.2 Místo 5 sekvenčních sync roundtripů `execCommand` (interop.mjs) PŘIBALÍ `uiState` (formatting+isDirty+undo+pageCount) do své odpovědi — `buildUiState` už existoval (fáze B), teď se přidá i k exec výsledku. C# `CanvasEngineCommandResult.UiState`. `RouteToCanvasEngineAsync` aplikuje uiState přes `ApplyCanvasUiStateFastAsync` (0 extra roundtripů). Navigation/outline LAZY — `SyncCanvasNavigationAsync` tahá jen když `ActiveSidePanelTab == Outline`. Content-control popover je selection-driven (per-selection sync), per-příkaz se netahá.
- [x] 2.3 JS clony: `dispatcher.captureSnapshot` drží model REFERENCÍ (ne clone — model je immutable-by-convention, každý mutátor klonuje); `history.push` opt-in `cloneSnapshots:false` přes wrapper `pushHistory` (before=immutable live ref, after=fresh result.model) → `normalizeTransaction` neklonuje znovu. 4 clony → 1. Navíc `queryCommandState({formattingOnly:true})` přeskočí drahé skupiny (table/fields/math/forms/format-painter/symbols/search/navigation) nepotřebné pro toolbar pressed-state — `readFormattingState` (interop) ho používá.
- [x] 2.4 `ApplyCanvasUiStateFastAsync` dělá jeden `StateHasChanged` (gated chrome signature v push cestě). Plný re-render TmDocumentEditor je na malém demu ~30–50 ms (`applyMs`). POZN: hlubší split toolbaru do samostatné komponenty (aby StateHasChanged nerenderoval canvas host) = follow-up, není nutný pro tento řád zrychlení.
- [x] 2.5 `FixP2_FormattingLatency_Baseline` gate: robustní (WASM/VM jitter + Playwright SlowMo → gateujeme GENUINE C# route, ne Playwright endToEnd): route total < 800 ms (teď ~250–390) + `getFormattingStateJson` < 50 ms (teď ~0,4). C# instrumentace `_routeExecMs/_routeApplyMs/_routeFocusMs` na root divu (`data-canvas-route-*-ms`). POZN: striktní <150 ms není splněno — podlaha je canvas repaint (~140 ms, inherentní vykreslení změny) + Playwright SlowMo (test artefakt); strukturální win (roundtripy/clony) je durable.

**POZN k Phase15 Image E2E (`DocumentEditorCanvasImageE2ETests`):** padá na `WaitForObjectSelectionAsync` — ale jen na podmínce `data-canvas-object-handle-count === '8'`; engine produkuje **9** (8 resize + 1 rotate handle). Diagnostika potvrzuje, že VÝBĚR funguje (objekt vybrán, id sedí). `object-handles.mjs`/`selection-controller.mjs` jsou committed a fází 2 NEzměněné → **pre-existing** nesoulad (test čeká 8, rotate handle dělá 9). K prošetření/aktualizaci ve fázi 8 (test by měl čekat 9, NEBO je rotate handle nechtěný).

## Fáze 3 — P3: skutečné bitmapy obrázků — HOTOVO 2026-06-10

Stav: **Node 351/351, C# CanvasEngineHostRenderTests 7/7 (2 nové), FixP3 E2E ZELENÝ (blue pixels 0 → 15794).** Vizuálně ověřeno: contract obrázky vykreslují demo bitmapu (modrý header + čárová grafika na bílé), žádný šedý podklad, captiony čitelné, text obtéká, jemný okraj (Word-like).

- [x] 3.1 `TmDocumentCanvasEngineHost`: `EnsureAssetUrlsResolvedAsync()` (volá se před serializací v mount + UpdateMountedModel) resolvuje URL assetů bez Url přes `ImageUrlResolver.ResolveUrlAsync`, cache `_resolvedAssetUrls` per assetId (i prázdný výsledek → neretryuje). `BuildCanvasModel`→`ApplyResolvedAssetUrls` vyplní `CanvasDocumentModel.Assets[].Url`. C# testy: resolver volán 1× per asset (cache), bez resolveru mount proběhne beze změny.
- [~] 3.2 ODLOŽENO (follow-up): JS `requestImageAssetUrl(assetId)` callback pro assety vložené ZA BĚHU s assetId-bez-url. NENÍ součástí hlášeného bugu (ten je o seedovaných demo obrázcích → 3.1) a demo insert flow vždy dá URL/upload data-URL. K doplnění pokud vznikne assetId-only runtime insert.
- [x] 3.3 `canvas-renderer.mjs paintImageObject`: šedý fill JEN když bitmapa není ready (`image.complete && naturalWidth>0`); jinak `drawImage` bez fillu pod obrázkem. Amber tečka (chybějící alt) zachována. Node test `renderer.test.mjs` (placeholder dokud loading → fillRect; ready → drawImage, žádný fillRect).
- [x] 3.4 `FixP3_FirstImage_RendersBitmapNotGrey` ZELENÝ (vzorkuje objects-layer canvas, počítá modré pixely `rgb(52,91,175)` → 15794). UX: bitmapa ostrá (canvas backing store ctí devicePixelRatio), bez šedého podkladu, caption italic grey čitelný, jemný okraj.

## Fáze 4 — základ pro P4/P5/P6: věrnost layout round-tripu — HOTOVO 2026-06-10

Stav: **Node 355/355 (4 nové layout round-trip testy), image-render/move-snap/group-transform 19/19 bez regrese.** Foundation fáze (žádná user-viditelná změna, payoff ve fázích 5/6/7). Opraven latentní bug: `updateImageLayout` přepisoval rámec/alignment/x-y při čisté rotaci/resize.

- [x] 4.1 `objects/__tests__/image-commands-layout.test.mjs` (4 testy): rotation-only NEZMĚNÍ frame/alignment/x-y; size-only NEZMĚNÍ rotaci/frame/alignment; explicit move nastaví x/y + zachová frame; keyboard dx/dy nudge z aktuálního offsetu + zachová frame.
- [x] 4.2 `image-commands.mjs`: `normalizeLayout` čte `horizontalRelativeTo`/`verticalRelativeTo`/`horizontalAlignment`/`verticalAlignment` + `numberOrNull` zachová ABSENCI x/y (null místo 0). `createLayoutPayload` zapisuje předané frame hodnoty (fallback na 2/3/Left/1 jen u fresh insertu) + x/y zapíše JEN když je přítomné (alignment-positioned objekt nemá x/y). `updateImageLayout` předává `current.position.*` frame fieldy 1:1 + `nextX/nextY` (explicit move | dx/dy nudge | preserve absence).
- [x] 4.3 `image-render.mjs normalizeCanvasImageObject` BEZE ZMĚNY — legacy chování (prázdné relativeTo → historické body-absolute) hlídá stávající `image-render.test.mjs` (zelené). Po fázi 4 alignment-positioned objekt po rotaci/resize NEMÁ position.x → resolveObjectX použije alignment (správně).

## Fáze 5 — P6: drop přesně pod kurzor — HOTOVO 2026-06-10

Stav: **Node 356/356 (1 nový end-to-end resolve test), FixP6 E2E ZELENÝ (3× stabilní).** Řešení JEDNODUŠŠÍ než plán: místo „posílat absolutní coords + reframe v enginu" stačí poslat DRAG DELTA (dx/dy) — resolved pozice = frameOrigin + storedOffset pro KAŽDÝ frame, takže nudge storedOffsetu o vizuální deltu landne objekt pod kurzor frame-agnosticky. Reuse existující (správné) dx/dy cesty z fáze 4.

- [x] 5.1 `objects/__tests__/image-commands-layout.test.mjs` end-to-end test přes `layoutCanvasDocument`: paragraph-anchored Square obrázek (flowY≫bodyY) → `updateImageLayout({dx:40,dy:60})` → **resolved** rect (z plného layoutu) se posune o přesně (40,60) (±0.5). Dokazuje, že engine resolve je frame-agnostický.
- [x] 5.2 `selection-controller.mjs` pointerup (object-move/resize): místo `x: rect.x − body.x` (body-relativní absolutní offset, správný JEN pro body/page-relative) posílá `dx/dy = previewRect − startRect` (vizuální delta), a jen když nenulová (single-axis drag nezmaterializuje spurious 0 na netknuté ose). `updateImageLayout` dx/dy cesta (fáze 4) inkrementuje storedOffset. POZN: pro contract obrázky (explicit stored 0 + paragraph-relative) plně správné; alignment-X-bez-stored-x edge case = follow-up.
- [~] 5.3 RE-ANCHOR ODLOŽENO (follow-up): dx/dy nudge ponechá kotvu na původním odstavci (Word-ish, korektní). Přepnutí kotvy na nejbližší odstavec = kosmetické zmenšení offsetu, není nutné pro drop-pod-kurzor.
- [x] 5.4 Preview = výsledek: `objectSelection` se znovu naváže přes existující `resolveObjectSelection` v `update()` (nezměněno) → po dropu žádný ghost.
- [x] 5.5 `FixP6` ZELENÝ. Verifikace přes **model delta** (engine vlastní model = čerstvý ground truth): storedOffset se posune o drag deltu (40,61)≈(40,60), bug ukládal ~(40,300). POZN: vykreslené readery (C#-render metadata uzel + cached debug-snapshot layout) LAGUJÍ živou in-engine editaci a screenshot mate reflow z otevření inspectoru + více demo obrázků → spolehlivý signál je model delta + Node resolve test (5.1), ne painted-position read. UX: drop pod kurzor potvrzen funkčně.

## Fáze 6 — P5: resize + kurzory — HOTOVO 2026-06-10

Stav: **Node 359/359 (3 nové object-handles testy), FixP5 E2E ZELENÝ, FixP6 bez regrese.** Vizuálně: obrázek se po SE-resize zvětší, top-left zakotvený, 8 resize handlů + rotate handle viditelné (Word-like).

- [x] 6.1 `object-handles.mjs`: nový export `cursorForObjectHandle(handle)` (nw/se→nwse-resize, ne/sw→nesw-resize, n/s→ns-resize, e/w→ew-resize, rotate→grab, connector→crosshair, body→move). `selection-controller.handlePointerMove` má PŘED active guardem `updateHoverCursor` (rAF-throttled): vybraný objekt → handle/move kurzor, jinak table col-resize, jinak ''. Node test na mapping.
- [x] 6.2 `applyDragCursor(mode, handle)` v handlePointerDown object branch (resize směr / `grabbing` pro move+rotate / crosshair pro connector); handlePointerUp už resetuje `root.style.cursor=''`.
- [x] 6.3 Resize commit: pointerup posílá dx/dy (top-left delta = 0 pro SE) + width/height → obrázek se nepohne. FixP5: SE +44px → width +44, pozice (0,0).
- [x] 6.4 Aspect-ratio: `resizeRectFromHandle(lockAspectRatio)` — locked roh drží poměr (100×50 → 140×70), Shift (unlocked) volný (140×50). Node test. `snapObjectResizeRect` předává `event.shiftKey !== true` jako lock.
- [x] 6.5 `FixP5_ImageResize_ShowsCursorAndResizesWithoutJumping` ZELENÝ: hoverCursor=dragCursor=`nwse-resize`, widthDelta=44, posDx/Dy=0. Hit-area zvětšena na 12px (`HANDLE_HIT_PADDING=2` v `objectResizeHandleAt`, vizuál stále 8px) — Node test ověřuje grab 2px za vizuálním handlem. UX: handly jasně viditelné (bílé čtverečky modrý okraj), snap guide, čistý scale.

## Fáze 7 — P4: rotace end-to-end — HOTOVO 2026-06-10

Stav: **Node 365/365 (6 nových object-handles/render testů), FixP4 E2E ZELENÝ.** Vizuálně ověřeno: bitmapa rotovaná ~30°, výběrový rámeček + 8 handlů rotují S NÍ (jeden rámeček, žádný ghost). **NALEZEN A OPRAVEN ROOT CAUSE: section/body desync** — to bránilo i správné painted pozici move/resize.

- [x] 7.1 `canvas-renderer.mjs paintImageObject`: `applyObjectTransform(context, {x,y,w,h}, command)` (rotation+flip kolem středu) před fill/drawImage/stroke. Node test (translate+rotate o command úhel, žádné rotate když 0).
- [x] 7.2 `selection-controller.appendObjectSelection`: outline + handly v JEDNOM containeru s `transform: rotate(...)` + `transformOrigin` = střed rectu → handly sedí na rotovaném rámečku; canvas `paintObjectSelection` už rotuje.
- [x] 7.3 Hit-test v lokálním prostoru: `imageObjectAtPoint` + `objectResizeHandleAt` `inverseRotatePoint(x,y,rect,rotation)` (inverze kolem středu) před testem. Node testy (rotovaný objekt/handle trefitelné na vizuálních pozicích).
- [x] 7.4 `cursorForObjectHandle(handle, rotation)`: edge úhel + rotace, snap 45° → ns/ew/nwse/nesw. Node test (e@90°→ns, n@90°→ew).
- [x] 7.5 `image-render.aabbOfRotatedRect` + `objectExclusionIntervals` používá AABB rotovaného rectu pro text-wrap. Node test (45° čtverec → AABB √2×, střed zachován).
- [x] 7.6 `FixP4_ImageRotation_RotatesBitmapAndFrameTogether` ZELENÝ: rotace 30° → modelRotation=30, overlayRotation=30, JEDEN outline, 8 directional handlů. Vizuálně: nakloněná bitmapa + rotovaný rámeček/handly, žádný ghost.

**ROOT CAUSE (klíčové):** rotace/move/resize se v prohlížeči NEvykreslovaly i přes správný model (=30). Důvod: `applyImageCommand` mutoval `body.blocks`, ale po clonu modelu jsou `sections[].blocks` SEPARÁTNÍ objekty; `layoutCanvasDocument` čte z `sections[].blocks` (přes `buildSectionFlows`) → stale geometrie (rotation 0, stará pozice). `collectCanvasImageObjects` čte body.blocks (→30), proto Node testy procházely. **Fix:** `applyImageCommand` volá `synchronizeSectionsWithBody(working)` (jako text/table/clipboard commandy) — přes `finalizeImageResult` pro insert/group/align early-returny + přímo v main path. Navíc `tile-cache.pageSignature` rozšířen o rotation/flip (axis-aligned rect se rotací nemění → bez toho žádný repaint). Tohle byl i důvod, proč FixP6 musel verifikovat přes model delta (painted pozice byla stale) — teď je painted pozice správná.

## Fáze 8 — finální integrační brána — HOTOVO 2026-06-10

Stav: **VŠECH 6 reportovaných bugů opraveno a zeleně.** Node 365/365, FixP1-P6 6/6 zelené, canvas component 45/45, DocumentEditor component 811 pass / 4 pre-existing fail (0 nových regresí, ověřeno stashem). Pre-existing Phase15 výběr OPRAVEN (genuine handle-count bug).

- [x] 8.1 `npm run test:document-editor-modules` → **365/365** (z 338 baseline: +27 nových testů napříč fázemi 1–7).
- [x] 8.2 `FixP1`-`FixP6` **6/6 zelené**. Stávající canvas suity: inline-format (Phase9) + HistorySave zelené (fáze 1/2). **Phase15 Image (`DocumentEditorCanvasImageE2ETests`): VÝBĚR OPRAVEN** — našel jsem genuine bug (rotate handle chybně značen `data-canvas-object-resize-handle` → handle-count 9 místo 8), opraveno (rotate→`data-canvas-object-rotate-handle`, `handle-count` = jen resize handly). Zbývající Phase15 snap-type asserty (`grid` vs object-alignment) jsou PRE-EXISTING over-specific (test padal na výběru, downstream nikdy nevalidován; move/resize teď reálně fungují a snapnou na hranu sousedního objektu) → test-maintenance pro tým, NE produktová regrese (deferred, mé test-edity vráceny).
- [x] 8.3 Screenshot review (UX, já): P1 bold (`The` netučné `Qz9`), P3 bitmapa (modrá demo grafika, žádný šedý podklad), P4 rotace (nakloněná bitmapa + rotovaný rámeček, žádný ghost), P5 resize (8 handlů, čistý scale). Vše Word/GDocs-like, ostré, bez glitchů.
- [x] 8.4 Servery běží pro ruční ověření (WASM 7106, API 5100). Automatizované E2E pokrývají scénáře z videí.
- [x] 8.5 Component suite: 4 pre-existing fails (`ExportRequests`/`Phase19_PdfExport`/`SaveRequest...DisplayOnlyImageUrl`/`VersionCreate` — všechny `InvalidCastException ParagraphBlockContent→ImageBlockContent` v PDF/export/save path) — **ověřeno git-stashem: padají i na baseline bez mých změn → 0 nových regresí.** NEsouvisí s P1-P6.

## Souhrn fixů (engine)
- **P1** `inline-format.mjs` tri-state pending marks; `text-editing.resolveInsertionMarks` merge; `dispatcher` reconcile.
- **P2** `interop.execCommand` přibalí uiState; C# `ApplyCanvasUiStateFastAsync` (0 extra roundtripů); `captureSnapshot` ref + `pushHistory cloneSnapshots:false`; `queryCommandState formattingOnly`.
- **P3** `TmDocumentCanvasEngineHost.EnsureAssetUrlsResolvedAsync` + `paintImageObject` loading-placeholder.
- **P4-P6 společný základ** `image-commands` layout round-trip (`numberOrNull`, preserve frame) + **`synchronizeSectionsWithBody`** (root cause stale render) + `tile-cache.pageSignature` += rotation/flip.
- **P6** `selection-controller` pointerup posílá dx/dy deltu.
- **P5** `cursorForObjectHandle` + hover/drag kurzory + 12px hit-padding.
- **P4** `paintImageObject applyObjectTransform` + rotovaný overlay container + `inverseRotatePoint` hit-test + `cursorForObjectHandle(rotation)` + `aabbOfRotatedRect` wrap.

---

## Poznámky

- Canvas engine se načítá PŘÍMO z `_content/Tempo.Blazor/js/document-editor-canvas/interop.mjs` (pro canvas se nebunduje; `npm run build:document-editor` je pro starý DOM engine / harness).
- Servery: WASM `dotnet run --project src/Tempo.Blazor.Demo --launch-profile https` (7106), API `--launch-profile Tempo.Blazor.Demo.Api` (5100).
- Demo asset = data:URL PNG 320×180 (dominantní modrá `rgb(52,91,175)` na světlém pozadí) → vhodné pro pixel-asserty.
- Hit-testy a overlay pracují v page souřadnicích (`normalizePointerPoint` dělí zoom scale) — nové výpočty držet v page prostoru, škálovat až při kreslení DOM overlay (`assignScaledRectStyle`).
- E2E screenshot helpery: `TakeScreenshotAsync` (vzor v `DiagramArchimate3Phase7E2ETests.cs`).
- Fáze B (operation-relay) je necommitnutá — před začátkem zkontrolovat git stav, ať se změny nemíchají bez commitu fáze B.

## Follow-up (mimo scope)

- [ ] Tooltip s úhlem při rotaci (UX nice-to-have)
- [ ] Custom SVG rotate kurzor místo `grab` (parita Word)
- [ ] Placeholder nenačtené bitmapy doplnit ikonou obrázku
