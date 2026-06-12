# TmDocumentEditor Canvas engine - E7: Tvary, textová pole, čáry/konektory, grafy a drawings (detailní TDD + E2E)

Datum založení: 2026-06-04
Nadřazený plán: `planning/tmdocumenteditor-canvas-onlyoffice-inspired-engine-tdd-todo-2026-06-04.md`, fáze **E7**
Stav: dokončeno (model + canvas render + insert/move/resize/rotate/delete + textbox nested editace + chart data boundary + group/ungroup/align/distribute + nested group z-order + save/reload + DOCX smoke + E2E screenshot gates)
Priorita: P1 (rozšířená parita nad rámec legacy; cíl Word / Google Docs / OnlyOffice)

## Proč tento TODO existuje

Master canvas plán má E7 jako jednu fázi, ale tvary, textová pole, čáry/konektory, grafy a seskupení jsou samostatná velká subdoména (v OnlyOffice `common/Drawings` + `word/Editor/GraphicObjects` mají stovky KB). Legacy `TmDocumentEditor` uměl jen obrázky. Aby měl canvas engine kvalitu Word/GDocs/OnlyOffice, potřebuje plnohodnotný drawing layer: vektorové tvary s geometrií, text uvnitř tvarů, čáry a konektory, grafy a skupiny, vše s anchoringem, obtékáním, selection handly, resize/rotací a perzistencí.

## Licence a clean-room pravidla

ONLYOFFICE (`/home/pavel/NetProjects/onlyfficeservergit`) je AGPL. Platí stejná pravidla jako v master plánu:

- [x] Nekopírovat zdrojový kód, názvy interních tříd ani algoritmy z ONLYOFFICE (`common/Drawings/Format/Shape.js`, `Geometry.js`, `CreateGeometry.js`, `ChartSpace.js`, `GroupShape.js`, `CnxShape.js`, `TextBody.js`).
- [x] Geometrii preset tvarů odvodit z veřejné OOXML/DrawingML preset geometry specifikace a vlastních testů, ne z ONLYOFFICE kódu.
- [x] Do PR poznámky napsat: "ONLYOFFICE byl použit pouze jako clean-room architektonická inspirace; kód nebyl kopírován."

## Cílový stav

- Uživatel vloží tvar (obdélník, elipsa, šipka, hvězda…), čáru/konektor, textové pole nebo graf z toolbaru.
- Tvar se vykreslí vektorově na canvas (fill, stroke, efekty), je ostrý na 1x/2x DPR.
- Textové pole má vlastní odstavcový obsah, který prochází stejným layout enginem jako tělo dokumentu.
- Objekt lze vybrat, posunout, změnit velikost (s aspect lockem), otočit; má viditelné handly bez překryvu toolbaru.
- Objekt má anchor (inline / floating) a wrap mód; text dokumentu kolem floating objektu obtéká bez overlapu.
- Skupiny lze seskupit/rozseskupit, měnit z-order, zarovnat a rozmístit.
- Konektory drží spojení na tvary i při jejich posunu.
- Graf se vykreslí z datového modelu; data jdou editovat (nebo přes provider boundary).
- Save/reload a DOCX (DrawingML) roundtrip zachovají všechny objekty.

## Znovupoužití stávající infrastruktury

E7 NENÍ green-field — `wwwroot/js/document-editor/objects/` už má bohatý drawing layer pro obrázky, který se zobecní na tvary:

- [x] `objects/geometry.mjs`, `overlap-geometry.mjs` - geometrie a překryvy.
- [x] `objects/anchored-drawing-layout.mjs`, `anchored-drawing-position.mjs`, `anchor-region.mjs`, `horizontal-position.mjs` - floating anchoring.
- [x] `objects/wrap-modes.mjs`, `wrap-mode-value.mjs` + `layout/text-exclusion*.mjs`, `available-intervals-cache.mjs`, `blocked-intervals.mjs` - obtékání (reuse z fáze D image-wrap práce).
- [x] `objects/image-resize.mjs`, `image-resize-preview.mjs`, `image-move-track.mjs`, `image-move-snap.mjs` - resize/move/snap (zobecnit na drawing-resize/move/rotate).
- [x] `objects/drawing-runs.mjs`, `drawing-kind.mjs`, `drawing-index.mjs`, `drawing-snapshot.mjs` - drawing run model.
- [x] `objects/hit-priority.mjs`, `layer-priority.mjs`, `active-image-target.mjs` - hit-test priorita a aktivní objekt.
- [x] `core-engine/object-overlay.mjs` - overlay vrstva pro handly.
- [x] `CoreEngineModelConverter` (C#) - rozšířit o drawing typy.
- [x] Master plán Faze 15 (Images and drawings) je předpoklad E7; E7 staví nad ní.

## Doporučené nové testovací soubory

```text
tests/Tempo.Blazor.Tests/DocumentEditor/CanvasEngine/Drawings/DrawingModelTests.cs
tests/Tempo.Blazor.Tests/DocumentEditor/CanvasEngine/Drawings/DrawingConverterTests.cs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/objects/__tests__/geometry-preset.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/objects/__tests__/shape-render.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/objects/__tests__/textbox-layout.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/objects/__tests__/connector-routing.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/objects/__tests__/group-transform.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/objects/__tests__/chart-layout.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasShapesE2ETests.cs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasTextBoxE2ETests.cs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasChartE2ETests.cs
```

## Definice hotovo pro každou E7 fázi

Dědí z master plánu + navíc:

- [x] RED test vznikl před implementací (JS unit / C# / E2E dle fáze).
- [x] Každá viditelná změna má screenshot before/after a projde `AssertCanvasNonBlankAsync`, `AssertNoUiOverlapAsync`, `AssertNoTextOverlapAsync`.
- [x] Object handly nepřekrývají toolbar/panel a jsou ostré na 1x/2x DPR.
- [x] Save/reload gate pro každý modelový typ objektu.
- [x] Undo/redo gate pro každý insert/move/resize/rotate/delete command.
- [x] `dotnet build` zelený, existující testy se neoslabují.

## Fáze E7.0: Baseline, flag a rozhodnutí

### E7.0.1 Založit E2E soubory a route
- [x] Vytvořit `DocumentEditorCanvasShapesE2ETests.cs` s helperem `OpenCanvasEngineDocumentAsync` (sdílený z master fáze 2).
- [x] RED E2E: insert-shape command na canvas hostu zatím neexistuje.

### E7.0.2 Rozhodnutí o rozsahu grafů
- [x] Rozhodnout: full chart engine vs. provider-backed chart vs. render-only z dat. Default: model + render + jednoduchý data editor; pokročilé typy (3D, kombo) označit P2.
- [x] Zapsat rozhodnutí do plánu.

### E7.0.3 Akceptace
- [x] RED testy existují, route/flag chybí, rozsah grafů rozhodnut.

## Fáze E7.1: Drawing object model (canvas model)

### E7.1.1 RED model testy
- [x] `DrawingModelTests`: drawing objekt má `kind` (shape/textbox/line/connector/chart/group/image), `geometry`, `fill`, `stroke`, `effects`, `transform` (x,y,w,h,rot,flipH,flipV), `anchor` (inline/floating + position), `wrap`, `zOrder`, `altText`, `name`.
- [x] Group má seznam children s relativním transformem.
- [x] Textbox má `textBody` = blok tree (odstavce/runs) + insets + vertical align + autofit.

### E7.1.2 Implementovat model + normalizace
- [x] Canvas drawing model + factory; validace (clamp velikostí, default fill/stroke).
- [x] Preserve channel pro neznámé DrawingML vlastnosti.

### E7.1.3 Converter round-trip
- [x] `DrawingConverterTests`: `DocumentEditorDocument` <-> canvas model pro shape/textbox/line/connector/group; image už pokrývá fáze 15.
- [x] Chart converter (data + typ) round-trip nebo preserve-channel, dle E7.0.2.

### E7.1.4 Akceptace
- [x] Model + converter zelené, round-trip bez ztráty geometrie/fill/stroke/anchor/text.

## Fáze E7.2: Geometrie preset tvarů (clean-room)

### E7.2.1 RED geometry testy
- [x] `geometry-preset.test.mjs`: preset -> path (sada bodů/segmentů) pro: rectangle, roundRect (s adjust handle), ellipse, triangle, rightTriangle, diamond, pentagon/hexagon, star (5/6), rightArrow/leftArrow/upArrow/downArrow, callout, line, bentConnector.
- [x] Adjust handles (např. roundRect radius, arrow head size) mění path deterministicky.

### E7.2.2 Implementovat geometry generator
- [x] Path generator z preset + adjust values + bounding box; normalizace do 0..1 prostoru škálovaného na transform.
- [x] Stretch guides pro šipky/callouty (clean-room z OOXML preset spec).

### E7.2.3 Akceptace
- [x] Min. 15 presetů deterministicky generuje path; adjust handles fungují; unit zelené.

## Fáze E7.3: Render tvarů na canvasu

### E7.3.1 RED render testy
- [x] `shape-render.test.mjs`: display list pro tvar obsahuje fill path, stroke path, efekt (shadow) jako samostatné kroky; deterministický pro stejný model.

### E7.3.2 Implementovat shape painter
- [x] Canvas paint: fill (solid/gradient/none), stroke (width, dash, color, none), shadow/efekt; respektovat transform (rotace/flip) přes matici.
- [x] Pixel snapping a 1x/2x DPR ostrost.

### E7.3.3 Screenshot E2E + UX
- [x] E2E: vložit obdélník + elipsu + šipku, screenshot, `AssertCanvasNonBlankAsync`, pixel diff v oblasti tvaru.
- [x] UX review: tvary jsou ostré, fill/stroke odpovídá, rotace bez rozmazání.

### E7.3.4 Akceptace
- [x] Tvary se renderují vektorově a ostře; screenshot gate zelený.

## Fáze E7.4: Textová pole - text uvnitř tvaru

### E7.4.1 RED textbox layout testy
- [x] `textbox-layout.test.mjs`: textBody se layoutuje stejným paragraph enginem; respektuje insets, šířku tvaru, vertical align (top/middle/bottom), word wrap; autofit (none/shrink/resize-shape) jako P2 flag.

### E7.4.2 Implementovat textbox layout
- [x] Reuse `paragraph-engine` + `line-breaker` na obsah textboxu v souřadnicích tvaru.
- [x] Caret/selection uvnitř textboxu = nested editing context (reuse hit-test/caret v lokálním prostoru tvaru).

### E7.4.3 Interakce
- [x] Double-click do tvaru/textboxu vstoupí do text editace; Esc vystoupí na object selection.
- [x] Psaní, Enter, formátování uvnitř textboxu prochází stejným command dispatcherem.

### E7.4.4 Screenshot E2E + akceptace
- [x] E2E: text box, napsat víceřádkový text, zarovnat na střed, screenshot bez overlapu.
- [x] Save/reload zachová text + formátování; undo gate.

## Fáze E7.5: Anchoring, pozice a obtékání

### E7.5.1 RED anchor/wrap testy
- [x] Reuse anchor/wrap testy z image-wrap fáze D; rozšířit na obecný drawing (shape/textbox/chart).
- [x] Inline drawing = inline box v řádce; floating = anchor (page/margin/paragraph) + offset.

### E7.5.2 Implementovat
- [x] Napojit drawing na `anchored-drawing-layout` + `wrap-modes` + `text-exclusion`.
- [x] Wrap módy: inline, square, tight (z geometrie path), through, top-bottom, behind/in-front.

### E7.5.3 Screenshot E2E + akceptace
- [x] E2E: floating tvar uprostřed textu, square wrap, `AssertNoTextOverlapAsync`.
- [x] Posun tvaru přepočítá obtékání bez overlapu; undo gate.

## Fáze E7.6: Selection, handly a hit-test

### E7.6.1 RED selection/handle testy
- [x] `object-overlay` kreslí 8 resize handles + rotate handle + (pro adjust) žluté adjust handles; hit-test mapuje pointer -> handle/objekt s prioritou (z-order, layer).
- [x] Konektor má endpoint handly.

### E7.6.2 Implementovat
- [x] Reuse `image-resize` / `image-move-track` infra zobecněně; rotate handle počítá úhel; multi-select (Shift+click, marquee).
- [x] Object selection je oddělená od text selection (master fáze 4/15).

### E7.6.3 Screenshot E2E + akceptace
- [x] E2E: klik na tvar zobrazí handly, screenshot, handly nepřekrývají toolbar.
- [x] UX review: handly ostré, rotate handle viditelný.

## Fáze E7.7: Insert commands + object toolbar/inspector

### E7.7.1 RED command testy
- [x] Command dispatcher: `insertShape(preset)`, `insertTextBox`, `insertLine`, `insertConnector`, `insertChart(type)`; každý undoable.
- [x] Object toolbar/inspector: fill, stroke, wrap, z-order, rotace, alt text, pozice.

### E7.7.2 Implementovat
- [x] Blazor shell: shape gallery dropdown, chart type picker, object inspector panel (reuse `TmDocumentImageInspector` pattern).
- [x] Commandy routují do canvas hostu (master fáze 9 dispatcher).

### E7.7.3 Akceptace
- [x] Insert + inspector fungují, undoable, toolbar state odpovídá vybranému objektu.

## Fáze E7.8: Move, resize, rotate interakce + snapping

### E7.8.1 RED interakční testy
- [x] Drag posune objekt; resize handle mění w/h (Shift = aspect lock); rotate handle otáčí (Shift = 15° krok); snapping na okraje/střed/jiné objekty/grid.

### E7.8.2 Implementovat
- [x] Live preview během dragu (reuse `image-resize-preview`); commit = 1 undo transakce.
- [x] Snap guides render na overlay.

### E7.8.3 Screenshot E2E + akceptace
- [x] E2E real pointer: drag/resize/rotate, screenshot before/after, model transform sedí.
- [x] Undo vrací přesně předchozí transform.

## Fáze E7.9: Skupiny, z-order, zarovnání

### E7.9.1 RED group testy
- [x] `group-transform.test.mjs`: group/ungroup zachová child ids a relativní pozice; transform skupiny se propisuje na children; align left/center/right/top/middle/bottom; distribute horizontal/vertical.
- [x] `group-transform.test.mjs`: z-order bring-to-front/back/forward/backward explicitně pro group wrapper i children.

### E7.9.2 Implementovat + akceptace
- [x] Group wrapper model + child transform; align/distribute commandy.
- [x] Plný nested hit-test/render pro vnořené group hierarchie.
- [x] E2E: seskupit 2 tvary, posunout skupinu, rozseskupit; undo gate; screenshot.

## Fáze E7.10: Čáry a konektory

### E7.10.1 RED connector testy
- [x] `connector-routing.test.mjs`: konektor má start/end (volný bod nebo connection na tvar+site); při posunu napojeného tvaru se přepočítá routing (straight/elbow/curved).

### E7.10.2 Implementovat + akceptace
- [x] Connection sites na tvarech; routing algoritmus (clean-room); endpoint drag.
- [x] E2E: spojit dva tvary konektorem, posunout jeden, konektor drží; screenshot; undo gate.

## Fáze E7.11: Grafy

### E7.11.1 RED chart testy
- [x] `chart-layout.test.mjs`: chart model (typ: bar/column/line/pie/area/scatter; series, categories, values, legenda, osy, titulek) -> layout/display list.

### E7.11.2 Implementovat render + data editor
- [x] Canvas chart render pro základní typy; legenda/osy/labels bez overlapu.
- [x] Data editor (mřížka) nebo provider boundary dle E7.0.2; pokročilé typy P2.

### E7.11.3 Screenshot E2E + akceptace
- [x] E2E: vložit sloupcový graf, upravit data, screenshot vypadá jako graf (ne debug); save/reload; undo gate.

## Fáze E7.12: Save/reload, DOCX (DrawingML) a clipboard

### E7.12.1 RED roundtrip testy
- [x] Save/reload zachová všechny drawing typy + geometrii/fill/stroke/text/anchor/wrap/z-order/rotaci.
- [x] DOCX DrawingML export/import smoke (kde provider podporuje): shape, textbox, group, connector, chart, image.
- [x] Copy/cut/paste objektu (interní fragment + obrázek z clipboardu).

### E7.12.2 Implementovat + akceptace
- [x] Serializace přes provider boundary (reuse `DocumentSerializer`).
- [x] E2E save/reload + DOCX smoke zelené.

## Fáze E7.13: Accessibility, klávesnice a UX galerie

### E7.13.1 A11y + keyboard
- [x] Alt text warning u objektů bez popisu; a11y mirror objekt s rolí/popisem.
- [x] Klávesnice: Tab cyklí objekty, šipky posunují, Alt+šipky resize, Esc deselect, Delete smaže.

### E7.13.2 UX galerie + akceptace
- [x] Screenshot galerie: shape, text box, line/connector, chart, group na desktop/tablet/mobil.
- [x] Agent UX/UI verdikt: vypadá jako Word/OnlyOffice drawing layer, ne jako debug render.
- [x] Zapsat E7 řádky do parity suite (master fáze 24).

## Průběžné poznámky

- 2026-06-05: Implementován první produkční E7 řez: DTO model v Abstractions (`DocumentDrawingShape`, fill/stroke/shadow, textBody, chart, group), canvas converter roundtrip, základní vector painter pro shape/textBox/line/chart, metadata layer pro drawing objekty, undoable insert commandy (`insertShape`, `insertTextBox`, `insertLine`, `insertConnector`, `insertChart`), demo seed `phase-e7-canvas-shapes-drawings`, JS unit testy, C# converter test a E2E save/reload gate. Nehotové zůstává plná toolbar galerie/inspector, nested textbox editace, group/ungroup, connector routing na tvary, resize/move/rotate pointer interakce a DOCX/clipboard drawing smoke.
- 2026-06-05: Doplněn clean-room geometry preset modul `document-editor-canvas/objects/geometry-preset.mjs` a napojen do canvas rendereru pro shape painter. Přibyly JS testy pro 17 presetů včetně adjustů (roundRect radius, arrow head, callout tail), C# `DrawingModelTests` pro drawing metadata/group/textbox/connector/chart JSON roundtrip a zpřísněný E2E screenshot test `DocumentEditorCanvasShapesDrawingsE2ETests`: insert textbox -> selection handly -> synchronizovaný inspector -> save/reload přes reálné demo API. Screenshot artefakty: `00-phasee7-drawings-before.png`, `02-phasee7-textbox-selection-handles.png`, `01-phasee7-drawings-after-reload.png`.
- 2026-06-06: Dodělány ověřené E7 části pro wrapping textboxů, gradient fill rendering, undoable `deleteObject`, keyboard-only ovládání objektů (Tab, arrows, Alt+arrows, Esc, Delete), pointer drag/resize + undo/redo transform gate a deterministický canvas-host E2E režim `disableCollaboration=true`. Ověřeno přes `npm run test:document-editor-modules`, C# `DrawingModelTests` + `CanvasModelConverterTests.PhaseE7_DrawingShapeTextBoxLineAndChartRoundTripThroughCanvasModel` a Playwright `DocumentEditorCanvasShapesDrawingsE2ETests` se screenshoty v `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phasee7-shapes-drawings/2026-06-04/`.
- 2026-06-06: Doplněna produkční rotace drawing objektů: `layout.transform.rotation/flip` se ukládá přes `updateImageLayout`, display list a canvas painter respektují rotaci i flip, overlay má samostatný rotate handle a pointer rotate commit je jedna undoable transakce. Přidány JS unit testy pro rotate handle, rotation/flip roundtrip a painter transform, C# canvas converter roundtrip pro rotation/flip a Playwright screenshot gate `07-phasee7-pointer-rotated.png` v `DocumentEditorCanvasShapesDrawingsE2ETests`.
- 2026-06-06: Doplněn layout a painter grafů pro bar/column/line/area/scatter/pie/donut včetně titulků, os, legendy a labelů; přibyl undoable provider-boundary command `updateChartData`/`setChartData`, samostatné display-list kroky pro shape shadow/fill/stroke, routing konektorů přes connection sites a a11y mirror metadata pro drawing objekty včetně alt-warning. Ověřeno přes focused JS suite 18/18, `CanvasModelConverterTests.PhaseE7_DrawingShapeTextBoxLineAndChartRoundTripThroughCanvasModel`, build E2E projektu a Playwright `DocumentEditorCanvasShapesDrawingsE2ETests` 2/2 se screenshot artefakty.
- 2026-06-06: Dodělány ověřené E7 mezery: textbox layout byl převeden na sdílený `paragraph-engine`/`line-breaker` modul (`textbox-layout.mjs` + `textbox-layout.test.mjs`), přibyly undoable commandy `groupObjects`/`ungroupObjects`/`alignObjects`/`distributeObjects`, group transform posouvá a škáluje child objekty v jedné historii a Playwright má samostatný group/ungroup screenshot gate. Ověřeno přes `npm run test:document-editor-modules` (249/249), build knihovny/demo/API/E2E a `DocumentEditorCanvasShapesDrawingsE2ETests` (3/3) se screenshoty `10-phasee7-grouped.png`, `11-phasee7-group-moved.png`, `12-phasee7-ungrouped.png`.
- 2026-06-06: Doplněny clean-room stretch guides pro šipky/callouty (`buildPresetStretchGuides`) a z-order skupin: `setImageZOrder`/bring-front aplikuje stejný z-index delta na group wrapper i child drawing objekty, včetně undo/redo. Ověřeno přes `geometry-preset.test.mjs`, `group-transform.test.mjs`, `npm run test:document-editor-modules` (251/251), build knihovny/demo/API/E2E a Playwright `DocumentEditorCanvasShapesDrawingsE2ETests` (3/3) se screenshotem `13-phasee7-group-zorder-front.png`.
- 2026-06-07: Doplněny connector endpoint handly a real pointer endpoint drag (`updateConnectorEndpoint`) včetně odpojení taženého endpointu na free-point geometrii, přesný hit-test čar/konektorů před group bounds, obohacení selection layoutu o connector metadata, interní object clipboard pro drawing objekty včetně group child remapu a connector connection remapu, provider save/reload gate pro shape/textbox/line/connector/chart/group a responzivní UX galerie desktop/tablet/mobil. Přidán explicitní E2E pixel-region diff pro vložený rectangle/ellipse/arrow/connector. Ověřeno přes `npm run test:document-editor-modules` (257/257), `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj`, focused Playwright endpoint/clipboard/save (1/1), plný `DocumentEditorCanvasShapesDrawingsE2ETests` (5/5) a focused Playwright insert/pointer/keyboard pixel gate (1/1).
- 2026-06-07: Po restartu bylo zjištěno, že timeouty nested textbox a pointer/keyboard/delete E2E způsobovalo neběžící Demo API na `https://localhost:5100`; po spuštění API prošly focused Playwright testy `PhaseE7_CanvasTextBoxNestedEditingUndoRedoSaveReloadHasScreenshotEvidence` a `PhaseE7_CanvasInsertedShapesPointerKeyboardAndDeleteInteractionsHaveScreenshotEvidence`. Doplněn clean-room PR note soubor `docs/document-editor-canvas-e7-clean-room-pr-note.md`, parity matrix `tests/Tempo.Blazor.E2E/CanvasEngine/ParityCoverageMatrix.cs`, parity audit test, DOCX DrawingML smoke test pro podporované import/preserve hranice, nested group rekurze pro transform/z-order a E2E DPR 2 backing-store screenshot gate `PhaseE7_CanvasDrawingLayerUsesHighDpiBackingStoreForSharpShapesAndHandles`. JS focused E7 suite prošla 49/49; plný Playwright E7 běh po DPR gate prošel 7/7 za 17.4828 minuty.
- E7 staví na master fázi 15 (images/drawings); pořadí: 15 -> E7.1..E7.6 (statické tvary+textbox+anchoring+selection) -> E7.7..E7.10 (insert+interakce+group+connector) -> E7.11 (grafy) -> E7.12..E7.13.
- Grafy jsou nejrizikovější; pokud full engine přeteče rozsah, dodat jako provider-backed render a označit interaktivní data editor jako samostatný follow-up.
- Konektory a group transform mají hodně hraničních případů (rotace + flip + nested) - držet malé fáze a screenshot gate u každé viditelné části.
