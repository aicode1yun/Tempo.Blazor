# TmDocumentEditor - Google Docs quality JS engine TDD TODO

Datum zalozeni: 2026-05-22  
Zdroj: rucni testovani a videa:

- `/home/pavel/Videa/Záznamy obrazovky/Záznam obrazovky z 2026-05-22 11-46-04.mp4`
- `/home/pavel/Videa/Záznamy obrazovky/Záznam obrazovky z 2026-05-22 11-47-12.mp4`
- `/home/pavel/Videa/Záznamy obrazovky/Záznam obrazovky z 2026-05-22 11-47-53.mp4`

Referencni architektura pro vnitrni engine:

- `/home/pavel/NetProjects/ckeditor5`

Navazuje na:

- `planning/document-editor-js-owned-runtime-tdd-todo.md`
- `planning/tmdocumenteditor-strict-human-e2e-ux-quality-todo-2026-05-18.md`
- `planning/tmdocumenteditor-image-wrapping-word-google-docs-analysis-2026-05-21.md`
- `planning/tmdocumenteditor-image-wrapping-word-google-docs-implementation-todo-2026-05-21.md`
- `planning/tmdocumenteditor-image-wrap-overlap-engine-fix-todo-2026-05-22.md`

Rezim prace: TDD + prubezne e2e testy + screenshot/geometry quality gate + rucni UX kontrola v demu.  
Stav: pripraveno k implementaci.

Pri implementaci se zde budou prubezne odskrtavat pouze realne hotove kroky. Checkbox se nesmi odskrtnout jen proto, ze existuje castecny workaround.

## Cile

Udelat z `TmDocumentEditor` editor, jehoz zive psani, layout, selection, obrazky, revize a toolbar stav pusobi stabilne jako Google Docs / Word Online, ne jako webova contenteditable aproximace.

Hlavni cilove vlastnosti:

- [ ] DOM neni source of truth pro dokument.
- [ ] DOM je pouze render projection aktualniho runtime modelu a layout snapshotu.
- [ ] Kazda uzivatelska zmena je transakce nad modelem.
- [ ] Layout tree je explicitni datova struktura, ne vedlejsi efekt browser reflow.
- [ ] Caret a selection jsou logicke pozice v dokumentu, ne nahodne DOM range po poslednim renderu.
- [ ] Aktivni odstavec se pri psani prelayoutuje okamzite.
- [ ] Strankovani a vzdaleny layout se muze dorovnat pozdeji, ale nesmi rozbit viditelny aktivni kontext.
- [ ] Text se nikdy nesmi viditelne prekryvat s jinym textem.
- [ ] Text se nesmi prekryvat s obrazkem v rezimech, kde to neni povolene.
- [ ] Revize jsou semanticka vrstva nad obsahem, ne styly, ktere znecistuji formatting state.
- [ ] Toolbar/floating toolbar/panely cteni stav z runtime selection modelu.
- [ ] Testy kontroluji i mezistavy mezi klavesami, ne jen finalni idle stav.

## Dulezite rozhodnuti

Soucasny `document-editor-wysiwyg.js` uz obsahuje mnoho dobrych casti: runtime state, command bridge, layout segmenty, hit-test geometrii, watchdog, image object model, virtualizaci a debug snapshoty. Problem neni v tom, ze by se melo vse zahodit. Problem je v tom, ze casti porad funguji jako DOM-driven editor:

- JS upravi absolutni DOM segment,
- potom posle patch,
- potom se pozdeji spusti local reflow,
- potom se obnovi selection,
- a v mezicase muze uzivatel videt rozbity layout.

Cilova architektura ma byt:

```text
Input event
  -> normalized editor operation
  -> JS runtime document model
  -> immediate active-scope layout
  -> atomic render projection
  -> logical selection restore
  -> async boundary patch/save/collab notification
```

Blazor zustava shell, UI controller, provider boundary a perzistence. Horka cesta psani, layoutu, selection a undo/redo musi byt JS-owned.

## Inspirace z CKEditoru 5

CKEditor 5 neni hotovy vzor pro nase strankovani ani Word-like obtékání. Je ale velmi silny vzor pro vnitrni disciplinu editoru: jeden model, zmeny jen pres writer/change bloky, operace, batch/undo, differ, mapper, command state, widgety a oddeleni editing renderu od datove serializace.

Konkretni CKEditor 5 principy, ktere se maji promítnout do implementace:

- [ ] Model je jediny canonical source of truth, podobne jako CKEditor `Model`.
- [ ] Model se nesmi menit primo z DOM event handleru; vse jde pres writer/transaction blok.
- [ ] Writer objekt existuje jen behem transaction/change bloku a nesmi se ukladat mimo nej.
- [ ] Kazda uzivatelska zmena je operation s validaci, apply krokem, inverse/reverse krokem a debug serializaci.
- [ ] Transaction/batch sdruzuje vice operations do jedne undo jednotky.
- [ ] Typing ma vlastni coalescing/change buffer, aby jedno plynule psani nebylo 50 drobnych undo kroku.
- [ ] Differ nevypocitava "co se zmenilo" z DOMu, ale z operaci nad modelem.
- [ ] Mapper je explicitni vrstva mezi modelem, layoutem a DOMem.
- [ ] Selection je modelova/logicka a po kazde transakci prochazi post-fixerem.
- [ ] Editing render muze obsahovat widget UI, overlaye, handly a fake selection.
- [ ] Data/export render zustava cisty a nesmi obsahovat editacni UI.
- [ ] Command ma `isEnabled`, `value`, `refresh()` a vsechny toolbary cte z command/selection state.
- [ ] Widget/object system resi obrazky a tabulky jako modelove objekty, ne jako nahodne DOM elementy.
- [ ] Resize/drag objektu je preview transaction s commitem na konci.
- [ ] Markery pro revize/komentare jsou semanticka overlay vrstva, ne skutecne formatting marks.

Referencni lokalni soubory CKEditoru 5:

- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-engine/src/model/model.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-engine/src/model/writer.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-engine/src/model/batch.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-engine/src/model/differ.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-engine/src/model/operation/operation.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-engine/src/conversion/mapper.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-engine/src/controller/editingcontroller.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-engine/src/controller/datacontroller.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-core/src/command.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-typing/src/input.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-typing/src/utils/changebuffer.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-widget/src/widget.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-widget/src/widgetresize.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-widget/src/widgetresize/resizer.ts`
- `/home/pavel/NetProjects/ckeditor5/packages/ckeditor5-image/src/imagestyle/utils.ts`

## Co se nesmi udelat jako "oprava"

- [ ] Neoslabovat e2e testy tak, aby ignorovaly vizualne rozbite mezistavy.
- [ ] Neopravovat jednotlive video jen CSS posunem konkretniho demo textu.
- [ ] Nepridavat dalsi debounce jako hlavni opravu psani.
- [ ] Nevracet se k `document.execCommand`.
- [ ] Nepouzivat browser native contenteditable layout jako autoritu pro line breaking.
- [ ] Nepouzivat full Blazor rerender jako normalni reakci na kazdy znak.
- [ ] Nezavest druhy paralelni model bez jasne synchronizacni hranice.
- [ ] Nechat stary DOM-driven path aktivni jen tam, kde je explicitne zdokumentovany compatibility fallback.
- [ ] Neprepisovat vse big-bangem bez testovatelnych mezikroku.
- [ ] Nepovazovat screenshot bez geometricke kontroly za dukaz spravneho layoutu.
- [ ] Neignorovat IME/composition kvuli rychlejsimu vyreseni anglickeho textu.
- [ ] Neignorovat accessibility jen proto, ze editor ma slozity vlastni layout.

## Cilove invarianty

### Model a transakce

- [ ] V kazdem okamziku existuje jeden JS runtime model dokumentu.
- [ ] Vsechny uzivatelske zmeny maji operation id.
- [ ] Vsechny uzivatelske zmeny patri do transaction id.
- [ ] Transaction je atomicka pro undo/redo.
- [ ] Transaction obsahuje predchozi a nasledujici selection snapshot.
- [ ] Transaction umi rict, ktere bloky a layout scopes invaliduje.
- [ ] Model ma schema pravidla pro allowed children, allowed attributes, object/limit/block/inline typy.
- [ ] Operation musi byt validovana proti schema pred apply.
- [ ] Operation ma inverse/reverse podobne jako CKEditor operations.
- [ ] Model differ uklada zmenene ranges/scopes pred renderem.
- [ ] Patch do C# je boundary event, ne live source of truth.
- [ ] Save/export si vytahuje aktualni model z JS runtime.

### Layout

- [ ] Layout tree obsahuje page, region, block, paragraph, line, segment, object box, exclusion zone a caret mapu.
- [ ] Layout tree je serializovatelny pro debug.
- [ ] Line breaking je deterministicky pro stejny model, styly, font metrics a page settings.
- [ ] Aktivni odstavec ma layout hotovy pred dalsim paintem po `beforeinput`.
- [ ] Pokud aktivni odstavec nemuze byt prelayoutovan do 16 ms, engine pouzije stabilni fallback bez prekryvu.
- [ ] Layout nikdy nesmi vytvorit segment s textem delsim, nez se vejde do vlastniho rectu, pokud se text da zalomit.
- [ ] Layout nikdy nesmi vytvorit dva viditelne text recty, ktere se prekryvaji o vic nez toleranci 1 px.
- [ ] Layout nikdy nesmi vytvorit text rect pres image footprint u `Square`, `Tight`, `Through` a `TopBottom`.
- [ ] `BehindText` smi povolit text nad objektem.
- [ ] `InFrontOfText` smi povolit objekt nad textem.

### Selection a caret

- [ ] Caret je ulozen jako logicka pozice `region + blockId + inlineId + offset + affinity`.
- [ ] Selection range je ulozen jako anchor/focus logicke pozice.
- [ ] Visual selection state ma vlastni line/segment hinty, ale ty nejsou autoritativni.
- [ ] Po renderu se DOM selection obnovuje z logicke pozice.
- [ ] Po kazde transakci musi byt mozne porovnat expected caret a actual DOM caret.
- [ ] Klik do textu se mapuje pres layout tree, ne pres nahodny DOM element.
- [ ] Arrow left/right/up/down pouziva caret mapu layoutu.
- [ ] Backspace/Enter u hranic revize a obrazku pouziva semanticka pravidla, ne browser default.
- [ ] Selection post-fixer po kazde transaction opravi nevalidni caret/range.
- [ ] Selection mapper umi preklad `DOM point -> layout position -> model position`.
- [ ] Selection mapper umi preklad `model position -> layout caret rect -> DOM selection`.

### Render

- [ ] Render je projekce layout snapshotu.
- [ ] Editing render a data/export render jsou oddelene pipeline.
- [ ] Render aktivniho scope je atomicky.
- [ ] Uzivatel nesmi videt mezistav, kde cast textu je novy model a cast textu stary layout.
- [ ] Render umi znovu pouzit DOM uzly jen tehdy, kdyz zustavaji konzistentni s modelem.
- [ ] Full render je recovery/load cesta, ne normalni hot path pro psani.
- [ ] Render output ma stabilni `data-*` identifikatory pro testy a hit-test.

### Revize

- [ ] Revize nejsou formatovaci mark, ktery ovlivnuje toolbar stav jako obycejny bold/color/underline.
- [ ] Revize maji vlastni overlay/decorations vrstvu.
- [ ] Psaní bez track changes za revizni text nevytvari novou revizi.
- [ ] Psaní bez track changes uvnitr revizni hranice se nejdrive normalizuje na explicitni boundary decision.
- [ ] Accept/reject revision provadi transakci nad modelem.
- [ ] Accept/reject revision zachova skutecne formatting marks obsahu.
- [ ] Review display mode nesmi menit canonical model.
- [ ] Revize/comment/search highlighty jsou marker/decorations vrstva podobna CKEditor markerum.

### Obrázky a obtékání

- [ ] Obrazek je anchored object v modelu, ne DOM float.
- [ ] Obrazek je schema object/widget s vlastni selection semantikou.
- [ ] Obrazek ma anchor, position, wrap, stacking, size, rotation, caption a alt text.
- [ ] Wrap contour je soucast layoutu.
- [ ] Text hit-test vedle obrazku pouziva available intervals z layoutu.
- [ ] Drag/resize obrazku vytvari preview transaction.
- [ ] Pri drag/resize nesmi text vizualne kolabovat do nevalidniho prekryvu.
- [ ] Po potvrzeni drag/resize se ulozi jedna undoable transaction.

### Testy

- [ ] Kazdy bug z videa musi mit RED e2e test pred opravou.
- [ ] Kazdy layout bug musi mit unit test layout enginu i e2e DOM geometry test.
- [ ] E2E musi kontrolovat mezistav po kazde klavese.
- [ ] E2E musi ukladat screenshot pri selhani.
- [ ] E2E musi ukladat model snapshot, layout snapshot, render fingerprint a selection snapshot.
- [ ] Test nesmi projit, pokud se problem opravi az po idle reflow, ale predtim je viditelne rozbity.

## Navrzene nove hranice souboru

Toto neni povinny finalni naming, ale doporucena hranice pro rozbiti prilis velkeho `document-editor-wysiwyg.js`.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-runtime-model.js`
  - [ ] Document store.
  - [ ] Block store.
  - [ ] Inline run store.
  - [ ] Object store.
  - [ ] Revision/comment references.
  - [ ] Serialization to/from C# JSON.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-schema.js`
  - [ ] Registrace model element typu.
  - [ ] Allowed child rules.
  - [ ] Allowed attribute rules.
  - [ ] Object/block/inline/limit flags.
  - [ ] Selection constraint helpers.
  - [ ] Schema driven validation errors.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-operations.js`
  - [ ] Operation schema.
  - [ ] Operation validation.
  - [ ] Operation application.
  - [ ] Operation inversion for undo.
  - [ ] Operation baseVersion/modelVersion checks.
  - [ ] Operation serialization for debug artifacts.
  - [ ] Operation composition for typing batches.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-transactions.js`
  - [ ] Transaction manager.
  - [ ] Undo/redo stack.
  - [ ] Dirty tracking.
  - [ ] Boundary patch emission.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-selection-engine.js`
  - [ ] Logical selection model.
  - [ ] DOM selection adapter.
  - [ ] Hit-test to logical caret.
  - [ ] Keyboard movement.
  - [ ] Selection normalization.
  - [ ] Selection post-fixer.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-layout-engine.js`
  - [ ] Paragraph layout.
  - [ ] Line breaking.
  - [ ] Text measurement.
  - [ ] Exclusion zones.
  - [ ] Object layout.
  - [ ] Page layout.
  - [ ] Incremental invalidation.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-mapper.js`
  - [ ] Model element to layout box mapping.
  - [ ] Layout segment to DOM node mapping.
  - [ ] DOM point to model position mapping.
  - [ ] Pointer coordinate to visual line mapping.
  - [ ] Widget/object hit-test mapping.
  - [ ] Mapper debug dump.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-renderer.js`
  - [ ] Layout snapshot renderer.
  - [ ] Atomic scope render.
  - [ ] DOM keyed reconciliation.
  - [ ] Overlay renderer.
  - [ ] Selection render.
  - [ ] Editing projection.
  - [ ] Data/export projection.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-input.js`
  - [ ] `beforeinput` normalizer.
  - [ ] Composition/IME.
  - [ ] Paste/drop.
  - [ ] Keyboard shortcuts.
  - [ ] Command dispatch.
  - [ ] Typing queue/change buffer.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-commands.js`
  - [ ] Command registry.
  - [ ] Command `refresh`.
  - [ ] Command `value`.
  - [ ] Command `isEnabled`.
  - [ ] Command execution via transactions.
  - [ ] Formatting state snapshot.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-widgets.js`
  - [ ] Object selection.
  - [ ] Fake/object selection render metadata.
  - [ ] Widget keyboard navigation.
  - [ ] Widget delete/backspace behavior.
  - [ ] Resize/drag preview transaction adapters.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-diagnostics.js`
  - [ ] Runtime snapshot.
  - [ ] Layout probe.
  - [ ] Visual invariant probe.
  - [ ] Event timeline.
  - [ ] Failure artifact export.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`
  - [ ] Zmenit na compatibility entrypoint/facade.
  - [ ] Postupne z nej odsunout core logiku.
  - [ ] Nakonec ponechat pouze public API facade a bootstrap.

## Definition of Done celeho planu

- [ ] Default contract demo se pri rychlem psani nechova vizualne nestabilne.
- [ ] Zadny text pri psani nelita nahoru/dolu mimo ocekavany reflow aktivniho odstavce.
- [ ] Zadny text se behem psani ani na jeden frame neprekryje s jinym textem.
- [ ] Caret zustava na logicke pozici po psani, Enter, Backspace, toolbar commandu, image drag/resize a accept/reject revision.
- [ ] Aktivni odstavec se zalamuje plynule vedle obrazku.
- [ ] Klik na druhy radek textu vedle obrazku nastavi caret na spravne misto.
- [ ] Backspace na zacatku radku vedle obrazku ma Word-like semantiku.
- [ ] Revize nemeni toolbar formatting state jako obycejny styl.
- [ ] Accept/reject revision zachova skutecny styl obsahu.
- [ ] Toolbar, floating toolbar a side panel maji stav ze stejneho runtime selection snapshotu.
- [ ] Undo/redo vraci presne uzivatelske transakce.
- [ ] Save/reload zachova model, layout-relevantni vlastnosti a demo data.
- [ ] E2E testy kontroluji finalni stav i mezistavy.
- [ ] Existuji debug artifacts, ktere pri selhani reknou, jestli selhal model, layout, render nebo selection.
- [ ] `node --check` pro vsechny dotcene JS soubory prochazi.
- [ ] Cileny `dotnet test` pro unit layout/runtime testy prochazi.
- [ ] Cileny Playwright e2e strict suite prochazi.
- [ ] Demo API + WASM demo po restartu ukazuje stejne stabilni chovani jako testy.

## Faze 0: Zmrazit aktualni problemy jako RED testy

Stav: E2E RED testy hotove, cisty JS model unit testy zustavaji otevrene do chvile, kdy ve fazi 1/2 vznikne samostatny engine model modul.

Implementacni poznamka 2026-05-22:
- Pridan soubor `tests/Tempo.Blazor.E2E/DocumentEditorStrictEnginePhase0E2ETests.cs`.
- Cileny beh `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorStrictEnginePhase0E2ETests"` je spravne RED.
- Aktualni RED signaly: pocatecni `text/image overlap` u `contract-missing-alt-image`, `text/text overlap` v `contract-scope` hned po psani mezery, a klik vedle wrapped image nastavuje caret na spatny vizualni radek.
- Ke kazdemu selhani se uklada screenshot a JSON artifact s frame probe, wrapped image targetem a EngineTimeline.

### 0.1 Video repro matrix

- [x] Zapsat presny scenar z videa `11-46-04` do test case popisu.
- [x] Zapsat presny scenar z videa `11-47-12` do test case popisu.
- [x] Zapsat presny scenar z videa `11-47-53` do test case popisu.
- [x] Pro kazde video urcit cilovy block id nebo stabilni text anchor.
- [x] Pro kazde video urcit pocatecni caret pozici.
- [x] Pro kazde video urcit vstupni textovou sekvenci.
- [x] Pro kazde video urcit, ktere vizualni chovani je chyba.
- [x] Pro kazde video urcit, co je ocekavany Google Docs-like vysledek.
- [x] Pridat test data tak, aby scenare nebyly zavisle na rucne rozbitem demo stavu.

### 0.2 RED e2e: live typing nesmi vytvorit prekryv

- [x] Pridat test `DocumentEditor_Strict_Engine_LiveTypingNeverCreatesTextOverlap`.
- [x] Test musi resetovat demo data.
- [x] Test musi otevrit `/document-editor`.
- [x] Test musi pockat na stabilni WYSIWYG host.
- [x] Test musi nastavit viewport `1440x900`.
- [x] Test musi vybrat contract demo.
- [x] Test musi umistit caret do odstavce pred obtekanym obrazkem.
- [x] Test musi napsat dlouhou sekvenci znaku po jednom.
- [x] Po kazdem znaku test musi pockat pouze na `requestAnimationFrame`, ne na idle.
- [x] Po kazdem znaku test musi sebrat text recty pres `getClientRects()`.
- [x] Po kazdem znaku test musi failnout pri text/text overlapu.
- [x] Po kazdem znaku test musi failnout pri text/image overlapu v nepovolenem wrap modu.
- [x] Po kazdem znaku test musi failnout, pokud segment scrollWidth vyrazne presahuje clientWidth a neni to povoleny unbreakable token.
- [x] Po kazdem znaku test musi ulozit lightweight frame probe pri selhani.
- [x] Na konci test musi pockat na idle reflow.
- [x] Na konci test musi znovu overit stejne invarianty.

### 0.3 RED e2e: caret nesmi skocit pri psani

- [x] Pridat test `DocumentEditor_Strict_Engine_LiveTypingKeepsCaretLogicalPosition`.
- [x] Test musi zachytit selection snapshot pred psanim.
- [x] Test musi po kazdem znaku zachytit selection snapshot.
- [x] Test musi overit stejny `blockId`.
- [x] Test musi overit monotonne rostouci offset.
- [x] Test musi overit, ze DOM caret rect je pobliz konce vlozeneho textu.
- [x] Test musi failnout, pokud caret zmizi z editoru.
- [x] Test musi failnout, pokud se caret presune do hlavicky/paticky/panelu.
- [x] Test musi failnout, pokud se caret presune pred vlozeny text.

### 0.4 RED e2e: active paragraph reflow nesmi byt pozdni

- [x] Pridat test `DocumentEditor_Strict_Engine_ActiveParagraphReflowsBeforeNextPaint`.
- [x] Test musi psat do odstavce, ktery je tesne pred zalomenim radku.
- [x] Test musi vlozit znak, ktery vyvola zalomeni.
- [x] Test musi overit uz v dalsim animation frame, ze radky jsou konzistentni.
- [x] Test musi overit, ze nedoslo k prekryvu stareho a noveho radku.
- [x] Test musi overit, ze idle reflow nezmeni poradi slov.

### 0.5 RED e2e: psani vedle obrazku

- [x] Pridat test `DocumentEditor_Strict_Engine_TypingBesideWrappedImageUsesAvailableIntervals`.
- [x] Test musi kliknout na prvni radek vedle left wrapped obrazku.
- [x] Test musi psat text.
- [x] Test musi overit, ze text zustava vpravo od obrazku, pokud je tam misto.
- [x] Test musi kliknout na druhy radek vedle obrazku.
- [x] Test musi overit, ze caret se nastavi na druhy vizualni radek.
- [x] Test musi overit, ze obrazek se neoznaci pri kliku do textoveho intervalu.
- [x] Test musi overit, ze po zaplneni intervalu text pokracuje pod obrazkem.

### 0.6 RED unit testy pro cisty model

- [ ] Pridat JS unit testy pro model bez DOMu. Poznamka: ceka na vznik cisteho engine model modulu ve fazi 1/2.
- [ ] Test `InsertTextOperation_UpdatesInlineRunText`.
- [ ] Test `InsertTextOperation_ReturnsExpectedSelection`.
- [ ] Test `DeleteBackwardOperation_UpdatesInlineRunText`.
- [ ] Test `SplitParagraphOperation_CreatesNewBlock`.
- [ ] Test `MergeParagraphOperation_MovesTextAndSelection`.
- [ ] Test `OperationApplication_DoesNotNeedDomRange`.

### 0.7 RED diagnosticke artifacty

- [x] Rozsirit e2e helper o `EngineTimeline`.
- [x] Timeline musi obsahovat `beforeinput`, operation, layout, render, selection restore, patch emit.
- [x] Timeline musi obsahovat casy v ms.
- [x] Timeline musi obsahovat invalidated scope ids.
- [x] Timeline musi obsahovat logical selection pred a po akci.
- [x] Timeline musi byt prilozen k test failure outputu.

## Faze 1: Architektonicka kostra a feature flag

Stav: hotovo (2026-05-22) - implementovan facade, feature flag, adapter a minimalni renderer; overeno cilenymi E2E testy.

### 1.1 Runtime mode flag

- [x] Pridat interni option `useGoogleDocsEngine`.
- [x] Pridat demo toggle pouze pro vyvoj, pokud to pomuze porovnavani.
- [x] Vychozi produkcni chovani zatim neprepinat bez green testu.
- [x] Flag musi jit nastavit z e2e testu deterministicky.
- [x] Flag musi byt soucast debug snapshotu.
- [x] Flag nesmi ovlivnit non-editor komponenty.

### 1.2 Engine facade

- [x] Vytvorit JS facade `tmDocumentEditorEngine`.
- [x] Facade musi mit `create`.
- [x] Facade musi mit `dispose`.
- [x] Facade musi mit `loadDocument`.
- [x] Facade musi mit `applyCommand`.
- [x] Facade musi mit `getDocumentSnapshot`.
- [x] Facade musi mit `getSelectionSnapshot`.
- [x] Facade musi mit `getLayoutSnapshot`.
- [x] Facade musi mit `getDebugSnapshot`.
- [x] Facade musi vracet stabilni chybu, pokud je volana po dispose.

### 1.3 Adapter ze stavajiciho entrypointu

- [x] `document-editor-wysiwyg.js` musi umet vytvorit novy engine pres facade.
- [x] Stary runtime zustava dostupny za feature flagem.
- [x] Verejne .NET interop metody se zatim nemeni.
- [x] Existujici e2e testy musi stale najit WYSIWYG host.
- [x] Debug snapshot musi rict, jestli bezi stary nebo novy engine.

### 1.4 Minimalni no-op renderer

- [x] Novy engine musi umet vyrenderovat prazdny dokument.
- [x] Novy engine musi umet vyrenderovat jeden odstavec plain textu.
- [x] Novy engine musi umet nastavit focus do editoru.
- [x] Novy engine musi umet vratit canonical JSON.
- [x] Pridat smoke e2e test, ze novy engine bootuje bez console erroru.

## Faze 2: JS runtime document model

Stav: hotovo (2026-05-22) - implementovan JS model, schema registry, import/export, indexy a invariant validator; overeno cilenymi E2E testy.

### 2.1 Model schema

- [x] Definovat `DocumentModel`.
- [x] Definovat `PageSettingsModel`.
- [x] Definovat `RegionModel`.
- [x] Definovat `BlockModel`.
- [x] Definovat `ParagraphBlockModel`.
- [x] Definovat `InlineRunModel`.
- [x] Definovat `TextRunModel`.
- [x] Definovat `FieldRunModel`.
- [x] Definovat `TokenRunModel`.
- [x] Definovat `ImageObjectModel`.
- [x] Definovat `TableModel`.
- [x] Definovat `RevisionModel`.
- [x] Definovat `CommentAnchorModel`.
- [x] Definovat `StyleModel`.
- [x] Definovat `ModelElementDefinition`.
- [x] Definovat schema flags `isBlock`, `isInline`, `isObject`, `isLimit`, `isSelectable`.
- [x] Definovat modelove typy obrazku jako object/widget.
- [x] Definovat table cell/header/footer jako limit regiony pro selection.

### 2.2 Identita a indexy

- [x] Kazdy block musi mit stabilni id.
- [x] Kazdy inline run musi mit stabilni id.
- [x] Kazdy object musi mit stabilni id.
- [x] Model musi mit index `blockId -> block`.
- [x] Model musi mit index `inlineId -> inline`.
- [x] Model musi mit index `objectId -> object`.
- [x] Model musi mit index `revisionId -> revision`.
- [x] Indexy se musi aktualizovat po kazde operation.
- [x] Pridat invariant test, ze indexy nemaji dangling references.

### 2.3 Import z C# JSON

- [x] Implementovat normalizator pascal/camel case.
- [x] Importovat title/document metadata.
- [x] Importovat page settings.
- [x] Importovat body blocks.
- [x] Importovat header/footer regions.
- [x] Importovat text runs.
- [x] Importovat marks.
- [x] Importovat images vcetne layoutu.
- [x] Importovat tables.
- [x] Importovat revisions.
- [x] Importovat comments.
- [x] Importovat unknown fields do extension bag pouze pokud jsou potreba.
- [x] Pridat roundtrip test minimalniho dokumentu.
- [x] Pridat roundtrip test contract demo dokumentu.

### 2.4 Export do C# JSON

- [x] Export musi byt deterministicky serazeny.
- [x] Export nesmi obsahovat DOM-only pole.
- [x] Export musi zachovat block ids.
- [x] Export musi zachovat inline ids.
- [x] Export musi zachovat image layout.
- [x] Export musi zachovat revisions.
- [x] Export musi zachovat comments.
- [x] Export po importu bez zmen musi byt semanticky stejny.
- [x] Pridat approval test pro canonical JSON.

### 2.5 Model invariant validator

- [x] Validator musi overit unikatni ids.
- [x] Validator musi overit zadne prazdne required ids.
- [x] Validator musi overit validni inline references.
- [x] Validator musi overit validni object anchors.
- [x] Validator musi overit validni revision ranges.
- [x] Validator musi overit validni comment anchors.
- [x] Validator musi byt volatelny v debug mode.
- [x] Validator musi byt volatelny v e2e failure artifactu.

### 2.6 CKEditor-like schema rules

- [x] Vytvorit `DocumentSchemaRegistry`.
- [x] Registrovat zakladni elementy dokumentu pri bootu enginu.
- [x] Registrovat text run jako inline child odstavce.
- [x] Registrovat field run jako inline child header/footer/body regionu podle pravidel.
- [x] Registrovat image object jako selectable object.
- [x] Registrovat caption jako limit child image objectu.
- [x] Registrovat table cell jako limit region.
- [x] Implementovat `checkChild(parent, childType)`.
- [x] Implementovat `checkAttribute(element, attributeName)`.
- [x] Implementovat `getLimitElement(position)`.
- [x] Implementovat `getNearestSelectionRange(position, direction)`.
- [x] Unit test: schema nepovoli inline image uvnitr caption, pokud to nechceme podporovat.
- [x] Unit test: schema nepovoli selection pres hranici limit regionu.
- [x] Unit test: schema rozpozna image jako object pro delete/arrow pravidla.

## Faze 3: Operation a transaction system

Stav: hotovo (2026-05-22) - implementovan operation schema, validace, model-only apply, transaction commit/rollback, typing coalescing, undo/redo a differ buffer; overeno cilenymi E2E testy.

Implementacni poznamka 2026-05-22:
- Operation API je dostupne pres `window.tmDocumentEditorEngine.operations`.
- `applyOperation` meni pouze JS model, ne DOM Range.
- `applyCommand` na novem engine vytvari transaction, commitne ji a render spousti az po commitu.
- Typing transakce se umi sloucit do jednoho undo kroku pro sousedni `InsertText`.
- Patch behavior je zatim reprezentovan typem/source transakce; skutecne Blazor patch emitovani zustava na boundary faze 16.

### 3.1 Operation schema

- [x] Definovat `InsertText`.
- [x] Definovat `DeleteRange`.
- [x] Definovat `SplitParagraph`.
- [x] Definovat `MergeParagraph`.
- [x] Definovat `ApplyMark`.
- [x] Definovat `RemoveMark`.
- [x] Definovat `SetParagraphAttribute`.
- [x] Definovat `InsertImage`.
- [x] Definovat `UpdateImageLayout`.
- [x] Definovat `UpdateImageMetadata`.
- [x] Definovat `InsertTable`.
- [x] Definovat `UpdateTableCell`.
- [x] Definovat `AcceptRevision`.
- [x] Definovat `RejectRevision`.
- [x] Definovat `SetSelection`.
- [x] Definovat base `DocumentOperation`.
- [x] Definovat `operation.baseVersion`.
- [x] Definovat `operation.batchId`.
- [x] Definovat `operation.affectedSelectable`.
- [x] Definovat `operation.getReversed()`.
- [x] Definovat `operation.toJSON()`.

### 3.2 Operation validation

- [x] Kazda operation musi mit id.
- [x] Kazda operation musi mit type.
- [x] Kazda operation musi mit timestamp.
- [x] Kazda operation musi mit source.
- [x] Validation musi failnout pro missing target block.
- [x] Validation musi failnout pro offset mimo text.
- [x] Validation musi failnout pro nevalidni range.
- [x] Validation musi failnout pro dangling image anchor.
- [x] Validation musi vratit strukturovanou chybu pro debug snapshot.

### 3.3 Apply operations

- [x] `InsertText` upravi model bez DOMu.
- [x] `DeleteRange` upravi model bez DOMu.
- [x] `SplitParagraph` rozdeli block bez DOMu.
- [x] `MergeParagraph` spoji blocky bez DOMu.
- [x] `ApplyMark` rozdeli inline runy podle range.
- [x] `RemoveMark` rozdeli inline runy podle range.
- [x] Adjacent compatible text runs se umi sloucit.
- [x] Operation musi vratit invalidated layout scopes.
- [x] Operation musi vratit next logical selection.

### 3.4 Transactions

- [x] Transaction se zacne pred operation.
- [x] Transaction muze obsahovat vice operations.
- [x] Transaction ma before selection.
- [x] Transaction ma after selection.
- [x] Transaction ma invalidated scopes.
- [x] Transaction ma user-visible label pro debug.
- [x] Transaction umi commit.
- [x] Transaction umi rollback.
- [x] Pri chybě apply se model vrati do predchoziho stavu.
- [x] Transaction docasne vypne intermediate render podobne jako CKEditor outermost change block.
- [x] Transaction po commitu spusti differ, layout, render a selection restore v pevnem poradi.
- [x] Transaction ma typ `default`, `typing`, `undo`, `redo`, `preview`, `remote`.
- [x] Transaction typ rozhoduje undo/redo a patch behavior.

### 3.5 Typing coalescing

- [x] Po sobe jdouci `InsertText` do stejne pozice se slouci do jedne undo transaction.
- [x] Coalescing skonci pri zmene blocku.
- [x] Coalescing skonci pri toolbar commandu.
- [x] Coalescing skonci pri Enter.
- [x] Coalescing skonci pri paste.
- [x] Coalescing skonci po timeoutu.
- [x] Undo jednim krokem odstrani souvisle napsane slovo/skupinu podle pravidla.

### 3.6 Model differ a invalidation buffer

- [x] Vytvorit `DocumentDiffer`.
- [x] Differ prijima operation jeste pred renderem.
- [x] Differ bufferuje inserted ranges.
- [x] Differ bufferuje removed ranges.
- [x] Differ bufferuje attribute changes.
- [x] Differ bufferuje object position/size changes.
- [x] Differ bufferuje marker/revision changes.
- [x] Differ umi vratit changed model ranges.
- [x] Differ umi vratit invalidated layout scopes.
- [x] Differ umi vratit invalidated overlay scopes.
- [x] Differ se po uspesnem renderu vycisti.
- [x] Unit test: insert text invaliduje pouze odstavec.
- [x] Unit test: move image invaliduje wrap affected paragraphs.
- [x] Unit test: accept revision invaliduje visible text i overlay.

## Faze 4: Logical selection engine

Stav: hotovo (2026-05-22) - implementovan logical position/range/selection snapshot, normalizace, DOM adapter, caret geometry, hit-test, keyboard movement, selection post-fixer a model-layout-DOM mapper; overeno cilenymi E2E testy.

Implementacni poznamka 2026-05-22:
- Selection API je dostupne pres `window.tmDocumentEditorEngine.selection`.
- DOM adapter je ciste mapovaci: nemeni model a nevyvolava Blazor render.
- Mapper ma debug dump a debug snapshot noveho enginu obsahuje selection mapper data.
- Hit-test zatim pouziva jednoduchy layout snapshot; presne line breaking/paragraph layout bude doplneny ve fazich 5 a 6.

### 4.1 Selection model

- [x] Definovat `LogicalPosition`.
- [x] Definovat `LogicalRange`.
- [x] Definovat `SelectionSnapshot`.
- [x] Pozice musi obsahovat region.
- [x] Pozice musi obsahovat block id.
- [x] Pozice musi obsahovat inline id, pokud je relevantni.
- [x] Pozice musi obsahovat offset.
- [x] Pozice musi obsahovat affinity `before`/`after`.
- [x] Pozice muze obsahovat visual hint line id.
- [x] Range musi obsahovat direction.

### 4.2 Normalizace selection

- [x] Normalizovat offset do hranic inline runu.
- [x] Normalizovat pozici mezi sousednimi inline runy.
- [x] Normalizovat pozici na hranici revize.
- [x] Normalizovat pozici pred/za inline obrazkem.
- [x] Normalizovat pozici v prazdnem odstavci.
- [x] Normalizovat pozici v header/footer/body regionu.
- [x] Pridat testy pro kazdou hranici.

### 4.3 DOM adapter

- [x] `logicalToDomRange` najde aktualni DOM text node.
- [x] `domRangeToLogical` mapuje DOM range zpatky na model.
- [x] Adapter musi fungovat po atomic rerenderu.
- [x] Adapter musi failnout strukturovane, pokud DOM neodpovida modelu.
- [x] Adapter nesmi menit model.
- [x] Adapter nesmi vyvolat Blazor render.

### 4.4 Caret geometry

- [x] Layout musi obsahovat caret stops pro text.
- [x] Layout musi obsahovat caret stop pred/za inline object.
- [x] Caret rect se pocita z layoutu.
- [x] DOM caret rect se porovnava s layout caret rect v debug modu.
- [x] E2E helper musi umet overit rozdil caret rectu.

### 4.5 Mouse hit-test

- [x] Klik do textu pouzije layout tree.
- [x] Klik vlevo od line nastavi zacatek line.
- [x] Klik vpravo od line nastavi konec line.
- [x] Klik mezi segmenty vybere nejblizsi caret stop.
- [x] Klik vedle wrapped image mapuje do textoveho available interval.
- [x] Klik do object visual rect vybere object, pokud je nad textem.
- [x] Klik do textu nad behind-text objektem preferuje text.
- [x] Klik do captionu mapuje do caption textu, pokud je editable.

### 4.6 Keyboard movement

- [x] ArrowLeft pouzije logical previous caret stop.
- [x] ArrowRight pouzije logical next caret stop.
- [x] ArrowUp pouzije visual line mapu.
- [x] ArrowDown pouzije visual line mapu.
- [x] Home jde na zacatek visual line.
- [x] End jde na konec visual line.
- [x] Ctrl+Left jde na predchozi word boundary.
- [x] Ctrl+Right jde na dalsi word boundary.
- [x] Shift variants rozsiruji range selection.
- [x] Movement pres obrazek a revizi ma testy.

### 4.7 Selection post-fixer

- [x] Vytvorit `SelectionPostFixer`.
- [x] Post-fixer bezi po kazde transaction.
- [x] Collapsed selection musi skoncit na pozici, kde schema povoluje text nebo object boundary.
- [x] Expanded selection nesmi nechtene krizit limit region.
- [x] Selection v caption nesmi skocit do body.
- [x] Selection v header/footer nesmi skocit do body bez explicitniho kliknuti.
- [x] Selection kolem image objectu se normalizuje na object selection.
- [x] Selection za image objectem se normalizuje na validni text insertion pozici.
- [x] Selection za revision markerem rozhodne outside/inside podle boundary policy.
- [x] Unit test: nevalidni position uvnitr widget UI se opravi na object boundary.
- [x] Unit test: non-collapsed range pres table cell hranice se rozdeli nebo odmitne podle pravidla.

### 4.8 Model-layout-DOM mapper

- [x] Vytvorit `ModelLayoutDomMapper`.
- [x] Mapper mapuje `blockId -> layoutBlockId`.
- [x] Mapper mapuje `inlineId + offset -> caret rect`.
- [x] Mapper mapuje `layoutSegmentId -> DOM element`.
- [x] Mapper mapuje DOM text node zpet na `inlineId + offset`.
- [x] Mapper mapuje pointer souradnice na visual line a interval.
- [x] Mapper mapuje klik do widget resize handle mimo model content.
- [x] Mapper mapuje klik do caption na caption region.
- [x] Mapper ma debug dump v e2e artifactu.
- [x] E2E test: klik na druhy radek vedle obrazku skonci ve spravnem model offsetu.

## Faze 5: Text measurement a line breaker

Stav: hotovo

Poznamka k implementaci: faze 5 pridava `window.tmDocumentEditorEngine.textLayout` s canvas/fallback merenim textu, tokenizaci textu a greedy line breakerem pro jeden odstavec. Vystupem jsou explicitni line boxy, segment boxy, caret stops, justify metadata a debug/fallback informace. Renderer cele stranky to jeste nepouziva jako hlavni pipeline; to je navazujici prace faze 6.

### 5.1 Font metrics service

- [x] Vytvorit JS text measurement service.
- [x] Pouzit canvas measurement pro hot path.
- [x] Cache key musi obsahovat text.
- [x] Cache key musi obsahovat font family.
- [x] Cache key musi obsahovat font size.
- [x] Cache key musi obsahovat font weight.
- [x] Cache key musi obsahovat font style.
- [x] Cache key musi obsahovat letter spacing.
- [x] Cache invalidace musi reagovat na zoom/font load.
- [x] Debug stats musi ukazovat cache hits/misses.

### 5.2 Tokenizace textu

- [x] Tokenizovat slova.
- [x] Tokenizovat mezery.
- [x] Tokenizovat newline.
- [x] Tokenizovat tabs.
- [x] Tokenizovat soft hyphen.
- [x] Tokenizovat non-breaking space.
- [x] Tokenizovat dlouhe unbreakable tokeny.
- [x] Tokenizovat CJK fallback jako grapheme clusters.
- [x] Unit testy pro vsechny token typy.

### 5.3 Line breaking

- [x] Implementovat greedy line breaker pro jeden odstavec.
- [x] Breakovat primarne na whitespace.
- [x] Dlouhy token rozdelit az kdyz se nevejde cely.
- [x] Respektovat hard line breaks.
- [x] Respektovat inline marks pri mereni.
- [x] Respektovat available intervals.
- [x] Respektovat min readable width.
- [x] Vratit line boxes a segment boxes.
- [x] Unit test pro simple paragraph.
- [x] Unit test pro long text.
- [x] Unit test pro long unbreakable token.
- [x] Unit test pro mixed font sizes.

### 5.4 Justify alignment

- [x] Justify ma byt vlastnost line layoutu, ne CSS side effect.
- [x] Posledni line odstavce se nejustifikuje.
- [x] Line s hard break se nejustifikuje podle pravidla.
- [x] Justify nesmi zmenit logical offsets.
- [x] Justify nesmi ovlivnit toolbar formatting state.
- [x] E2E test musi pokryt psani do justified odstavce.

### 5.5 Layout fail-safe

- [x] Pokud line breaker detekuje nevalidni rect, vrati safe fallback.
- [x] Safe fallback nesmi prekryt text.
- [x] Safe fallback muze docasne presunout odstavec pod objekt.
- [x] Debug snapshot musi obsahovat duvod fallbacku.
- [x] Test musi overit fallback pri nulove sirce intervalu.

## Faze 6: Paragraph layout tree

Stav: hotovo

Poznamka k implementaci: faze 6 pridava paragraph layout tree nad line breakerem z faze 5. Vzniklo API `createParagraphLayoutEngine()` s layout scopes, layoutem jednoho odstavce, dokumentovym layoutem, immediate relayoutem po operaci, safe handoffem pro nasledujici bloky a paragraph-level absolutnim DOM renderem. Plny atomic renderer cele stranky zustava navazujici prace faze 7.

### 6.1 Layout scope

- [x] Definovat `LayoutScope`.
- [x] Scope muze byt active paragraph.
- [x] Scope muze byt whole block.
- [x] Scope muze byt page region.
- [x] Scope muze byt whole document.
- [x] Operation musi vratit minimalni scope.
- [x] Debug snapshot musi ukazat invalidated scopes.

### 6.2 Paragraph layout bez obrazku

- [x] Layoutovat plain odstavec do page body width.
- [x] Vratit paragraph rect.
- [x] Vratit line rects.
- [x] Vratit segment rects.
- [x] Vratit caret stops.
- [x] Vratit baseline metadata.
- [x] Renderovat layout absolutne bez browser wrapping.
- [x] Testovat, ze DOM recty odpovidaji layout rectum.

### 6.3 Paragraph layout s marks

- [x] Layoutovat bold text.
- [x] Layoutovat italic text.
- [x] Layoutovat underline jako decoration, ne width change.
- [x] Layoutovat color/background bez vlivu na metrics.
- [x] Layoutovat mixed font size.
- [x] Layoutovat field/token runs.
- [x] Testovat split run pres line boundary.

### 6.4 Active paragraph immediate layout

- [x] Po `InsertText` prelayoutovat active paragraph synchronne nebo do dalsiho animation frame.
- [x] Po `DeleteRange` prelayoutovat active paragraph.
- [x] Po `SplitParagraph` prelayoutovat affected blocks.
- [x] Po `MergeParagraph` prelayoutovat affected blocks.
- [x] Po `ApplyMark` prelayoutovat affected block.
- [x] Active paragraph render musi probehnout pred idle document reflow.
- [x] E2E musi overit zadny visible overlap mezi znaky.

### 6.5 Paragraph pagination handoff

- [x] Pokud active paragraph zmeni vysku, oznacit nasledujici blocks jako stale layout.
- [x] Nasledujici blocks se mohou docasne posunout safe offsetem.
- [x] Idle page layout dorovna presne strankovani.
- [x] Safe offset nesmi vyvolat prekryv.
- [x] Selection zustane v active paragraph.
- [x] Testovat dlouhy odstavec u konce stranky.

## Faze 7: Atomic renderer

Stav: hotovo

Poznamka k implementaci: faze 7 pridava `window.tmDocumentEditorEngine.rendering` s `RenderSnapshot`, atomic rendererem, scope renderery, selection/revision/comment overlays, reconciliation cache pro layout segmenty a editing/data projekcemi. Renderer je zatim samostatne testovatelny nad paragraph layout tree; plne napojeni na input pipeline a zivy editor prijde ve fazi 8+.

### 7.1 Render snapshot

- [x] Definovat `RenderSnapshot`.
- [x] Snapshot obsahuje model version.
- [x] Snapshot obsahuje layout version.
- [x] Snapshot obsahuje selection version.
- [x] Snapshot obsahuje affected scopes.
- [x] Snapshot ma checksum/fingerprint pro debug.

### 7.2 Scope renderer

- [x] Renderer umi vyrenderovat jeden paragraph scope.
- [x] Renderer umi vyrenderovat jeden page region.
- [x] Renderer umi vyrenderovat object layer.
- [x] Renderer umi vyrenderovat selection overlay.
- [x] Renderer umi vyrenderovat revision overlay.
- [x] Renderer umi vyrenderovat comment markers.

### 7.3 Atomic swap

- [x] Render novych uzlu pripravit mimo visible DOM nebo do fragmentu.
- [x] Swapnout affected scope najednou.
- [x] Po swapu obnovit DOM selection z logical selection.
- [x] Pri selhani swapu vratit puvodni scope.
- [x] Zalogovat render failure do watchdogu.
- [x] Testovat, ze behem renderu nevznikne frame s prazdnym odstavcem.

### 7.4 DOM reconciliation

- [x] Reuse DOM uzlu jen pri stejnem `modelId`.
- [x] Reuse DOM uzlu jen pri stejnem `layoutSegmentId`.
- [x] Reuse text node pri pouhe zmene textu v active segmentu.
- [x] Nevytvaret duplicitni toolbar/overlay prvky.
- [x] Odstranit orphan layout segments po reflow.
- [x] Debug probe musi najit orphan nodes.

### 7.5 Render invariants

- [x] Po renderu kazdy visible text node ma model mapping.
- [x] Po renderu kazdy layout segment ma DOM element.
- [x] Po renderu kazdy DOM segment ma layout segment.
- [x] Po renderu zadny segment nema browser-wrapped vice radku.
- [x] Po renderu zadny visible text rect neprekryva zakazany rect.

### 7.6 Editing/data conversion pipeline

- [x] Vytvorit editing projection pipeline.
- [x] Vytvorit data/export projection pipeline.
- [x] Editing projection smi renderovat resize handles, selection overlays, warning badges a debug ids.
- [x] Data projection nesmi renderovat resize handles, toolbary, debug UI ani fake selection.
- [x] Editing projection pro image object vytvori widget wrapper.
- [x] Data projection pro image object vytvori cisty canonical image/export model.
- [x] Editing projection pro revisions vytvori overlay decorations.
- [x] Data projection pro revisions serializuje semantic revision model.
- [x] Unit test: data projection neobsahuje editing-only tridy.
- [x] Unit test: editing projection zachova mapping ids pro hit-test.

## Faze 8: Input pipeline

Stav: hotovo

Poznamka k implementaci: faze 8 pridava `window.tmDocumentEditorEngine.input` s `beforeinput` normalizerem, model-first input pipeline, delete/backspace boundary planningem, Enter handlingem, composition/IME preview+commit flow, paste handlingem a `TypingChangeBuffer`. Pipeline preventuje browser default pro podporovane i nepodporovane vstupy a DOM mutace bere pouze jako diagnostiku. Plne immediate/idle scheduling chovani navazuje ve fazi 9.

### 8.1 beforeinput normalizer

- [x] Zachytit `insertText`.
- [x] Zachytit `insertParagraph`.
- [x] Zachytit `insertLineBreak`.
- [x] Zachytit `deleteContentBackward`.
- [x] Zachytit `deleteContentForward`.
- [x] Zachytit `deleteWordBackward`.
- [x] Zachytit `deleteWordForward`.
- [x] Zachytit `insertFromPaste`.
- [x] Zachytit `formatBold`, pokud browser posle command.
- [x] Nepodporovany input preventDefault a structured log.

### 8.2 Text insertion

- [x] `insertText` vytvori `InsertText` operation.
- [x] Operation pouzije logical selection.
- [x] Pokud selection range neni collapsed, nejdrive vznikne `DeleteRange`.
- [x] Vlozeny text prevezme active typing marks.
- [x] Vlozeny text respektuje track changes mode.
- [x] Po operation se prelayoutuje active scope.
- [x] Po renderu se obnovi caret na konec vlozeneho textu.
- [x] Boundary patch se odesle asynchronne.

### 8.3 Delete/backspace

- [x] Backspace uprostred textu vytvori `DeleteRange`.
- [x] Backspace na zacatku inline runu normalizuje na predchozi run.
- [x] Backspace na zacatku odstavce vytvori `MergeParagraph`, pokud je to povolene.
- [x] Backspace za obrazkem vybere obrazek nebo smaze podle Word-like pravidla.
- [x] Delete pred obrazkem vybere obrazek nebo smaze podle Word-like pravidla.
- [x] Backspace/Delete v revizi pouzije revision boundary pravidla.
- [x] Testovat kazdou hranici.

### 8.4 Enter

- [x] Enter uprostred odstavce vytvori `SplitParagraph`.
- [x] Enter na zacatku odstavce vytvori prazdny odstavec pred nim.
- [x] Enter na konci odstavce vytvori prazdny odstavec za nim.
- [x] Enter za reviznim textem bez tracking off nevytvori nahodnou revizi.
- [x] Enter v list itemu zachova list style.
- [x] Enter v header/footer zustane v danem regionu.
- [x] Enter vedle obrazku zachova wrap layout konzistentni.
- [x] E2E musi overit, ze text za kurzorem nelita na spatny radek.

### 8.5 Composition/IME

- [x] `compositionstart` zalozi composition transaction.
- [x] `compositionupdate` aktualizuje preview bez commit patchu.
- [x] `compositionend` commitne jednu transaction.
- [x] Selection behem composition zustava stabilni.
- [x] Layout preview nesmi prekryvat text.
- [x] Testovat minimalne simulovatelny composition flow.

### 8.6 Paste

- [x] Paste plain text vlozi text pres operation pipeline.
- [x] Paste multi-line text vytvori split/insert operations.
- [x] Paste HTML normalizuje do modelu.
- [x] Paste z Word/Google Docs nesmi vlozit rozbity DOM.
- [x] Paste do selection nahradi selection range.
- [x] Paste vedle obrazku spusti immediate layout.
- [x] Paste vytvori jednu undo transaction.

### 8.7 Typing queue a change buffer

- [x] Vytvorit `TypingChangeBuffer` inspirovany CKEditor typing bufferem.
- [x] Buffer sdruzuje souvisle insert operations do typing transaction.
- [x] Buffer se resetuje pri selection change mimo typing flow.
- [x] Buffer se resetuje pri command transaction.
- [x] Buffer se resetuje pri Enter/paste/delete podle pravidel.
- [x] Buffer ma maximalni casove okno.
- [x] Buffer nesmi odkladat visible active layout za dalsi znak.
- [x] `beforeinput` musi preventDefault pro podporovane operace.
- [x] Browser DOM mutation nesmi byt canonical input zdroj.
- [x] Mutation observer muze slouzit jen pro diagnostiku/recovery.
- [x] Composition/IME ma oddeleny preview buffer.
- [x] E2E test: mezera se zobrazi v dalsim frame bez cekani na dalsi znak.

## Faze 9: Active layout scheduling

Stav: hotovo

### 9.1 Scheduler pravidla

- [x] Rozdelit immediate layout a idle layout.
- [x] Immediate layout je pro active scope.
- [x] Idle layout je pro page/document reconciliation.
- [x] Immediate layout nesmi cekat na debounce 1000 ms.
- [x] Idle layout muze byt debounce.
- [x] Scheduler musi brat v uvahu composition.
- [x] Scheduler musi byt viditelny v debug timeline.

### 9.2 Frame budget

- [x] Merit cas operation apply.
- [x] Merit cas layoutu.
- [x] Merit cas renderu.
- [x] Merit cas selection restore.
- [x] Pokud active layout prekroci budget, logovat warning.
- [x] Pokud prekroci budget opakovane, pouzit safe degraded mode.
- [x] Debug stats musi byt dostupne v e2e.

### 9.3 No-invalid-frame gate

- [x] Po kazdem immediate renderu spustit lightweight invariant probe v debug/test mode.
- [x] Probe kontroluje overlap text/text.
- [x] Probe kontroluje overlap text/image.
- [x] Probe kontroluje segment overflow.
- [x] Probe kontroluje missing caret.
- [x] Pri selhani probe vyvolat test-readable error.

### 9.4 Idle reconciliation

- [x] Po typovani naplanovat page layout reconciliation.
- [x] Reconciliation nesmi zmenit logical selection.
- [x] Reconciliation nesmi prehazet slova.
- [x] Reconciliation nesmi vyrobit visual jump active paragraphu, pokud se model nezmenil.
- [x] E2E musi overit before-idle a after-idle snapshot.

Poznamka k implementaci: faze 9 pridava `window.tmDocumentEditorEngine.scheduling` s `createActiveLayoutScheduler`. Scheduler oddeluje immediate layout aktivniho scope od idle reconciliation celeho dokumentu, meri cas apply/layout/render/selection restore, zapisuje debug timeline, po opakovanem prekroceni budgetu prepina safe degraded mode a v test/debug rezimu spousti no-invalid-frame gate proti text/text, text/image, overflow a missing caret chybam.

## Faze 10: Page layout a pagination

Stav: hotovo

### 10.1 Page frame

- [x] Layout engine musi znat page size.
- [x] Layout engine musi znat margins.
- [x] Layout engine musi znat header height.
- [x] Layout engine musi znat footer height.
- [x] Body frame musi byt explicitni rect.
- [x] Header/footer/body frames musi byt v layout snapshotu.
- [x] E2E musi overit, ze modry editable frame odpovida body frame.

### 10.2 Block flow

- [x] Blocks se layoutuji v poradi modelu.
- [x] `currentY` se meni jen layout enginem.
- [x] Paragraph spacing before/after je explicitni.
- [x] Images/tables meni flow podle vlastniho layout type.
- [x] Floating objects vytvari exclusions.
- [x] Nasledujici blocks respektuji relevantni exclusions.
- [x] Blocks nesmi zacit uvnitr zakazane footprint zony.

### 10.3 Page break

- [x] Pokud block nevejde, engine vytvori dalsi page.
- [x] Paragraph muze byt rozdelen pres page podle pravidel.
- [x] Keep-with-next pravidlo ma pripraveny model.
- [x] Manual page break funguje jako explicitni block.
- [x] Header/footer fields se aktualizuji podle page indexu.
- [x] E2E test musi overit page count a caret po page breaku.

### 10.4 Header/footer integration

- [x] Header/footer jsou samostatne editable regions.
- [x] Selection region rozhoduje, kam jde input.
- [x] Klik z footeru do body zmeni region bez pozdniho focus návratu.
- [x] Klik z body do footeru zmeni region bez ztraty prvniho znaku.
- [x] Page fields jsou render decorations nebo field runs podle modelu.
- [x] Space/typing v header/footer nesmi byt odlozeny do dalsiho znaku.

Poznamka k implementaci: faze 10 pridava explicitni `pageMetrics`, `bodyFrame`, `headerFrame`, `footerFrame`, vice stranek v layout snapshotu, page break block, paragraph fragmenty pres stranky, `pageIndex` na blocich/lines/segments/caret stops, zakladni object exclusions pro flow layout a render header/footer regionu s page number/total pages fieldy. Renderer ma cache klice rozsirene o page/region/fragment, aby se neopakovaly stejne bloky headeru/footeru nebo rozdelene odstavce mezi strankami.

## Faze 11: Anchored objects a text wrapping

Stav: hotovo

### 11.1 Object model

- [x] Object ma `objectId`.
- [x] Object ma `anchorBlockId`.
- [x] Object ma `anchorOffset`.
- [x] Object ma `moveWithText`.
- [x] Object ma `fixedOnPage`.
- [x] Object ma `horizontalPosition`.
- [x] Object ma `verticalPosition`.
- [x] Object ma `wrapMode`.
- [x] Object ma `wrapMargin`.
- [x] Object ma `allowOverlap`.
- [x] Object ma `zIndex`.

### 11.2 Exclusion zones

- [x] Square wrap vytvori rectangular exclusion.
- [x] Tight wrap vytvori contour-based exclusion.
- [x] Through wrap pouzije editable contour.
- [x] TopBottom wrap vytvori full-width vertical exclusion.
- [x] BehindText nevytvori text exclusion.
- [x] InFrontOfText nevytvori text exclusion, ale ovlivni hit-test priority.
- [x] Caption je soucast footprintu podle wrap mode.
- [x] Unit testy pro kazdy wrap mode.

### 11.3 Available intervals

- [x] Pro kazdy visual line vypocitat available intervals.
- [x] Intervaly zohledni page body frame.
- [x] Intervaly zohledni vsechny aktivni exclusions.
- [x] Intervaly se slouci/orezou deterministicky.
- [x] Pokud neni zadny interval, line Y se posune.
- [x] Pokud interval je uzsi nez minimum, line Y se posune.
- [x] Hit-test vedle obrazku pouzije tyto intervaly.

### 11.4 Image drag preview

- [x] Drag start vytvori preview transaction.
- [x] Drag move aktualizuje object position v preview modelu.
- [x] Active surrounding layout se prepocita v preview.
- [x] Text nesmi behem drag preview prekryvat objekt.
- [x] ESC rollbackne preview.
- [x] Mouse up commitne jednu transaction.
- [x] Undo vrati celou pozici jednim krokem.

### 11.5 Image resize preview

- [x] Resize start vytvori preview transaction.
- [x] Resize move aktualizuje size.
- [x] Lock aspect ratio funguje.
- [x] Caption footprint se prepocita.
- [x] Wrap contour se prepocita.
- [x] Text layout se prepocita v okolnim scope.
- [x] Mouse up commitne jednu transaction.

### 11.6 Object UI

- [x] Obrazek ma 8 resize handles.
- [x] Obrazek ma rotation handle, pokud je feature povolena.
- [x] Layout bubble je jedna, ne duplicitni toolbar.
- [x] Floating image toolbar se neprekryva necitelne se side panelem.
- [x] Kontextove menu a toolbar sdili stejne commandy.
- [x] Alt/caption/size/url pole aplikuji zmeny pres debounce transaction.
- [x] URL pole se zobrazi jen pro skutecny URL image source.
- [x] Data URI se nezobrazuje jako editovatelny "Odkaz obrazku".

### 11.7 Widget/object architecture podle CKEditoru

- [x] Vytvorit obecny `EditorWidget`.
- [x] Image object implementuje widget adapter.
- [x] Table object implementuje widget/limit adapter podle potreby.
- [x] Widget selection je oddelena od text selection.
- [x] Widget ma fake/object selection render, ne browser text selection pres UI.
- [x] Klik primo na widget vybere object.
- [x] Klik do textoveho available intervalu vedle widgetu nevybere object.
- [x] Arrow navigation umi vstoupit na object boundary.
- [x] Backspace/Delete u selected objectu pouzije command pipeline.
- [x] Widget resize handly jsou editing-only UI.
- [x] Resize zacatek skryje/utlumi konfliktni floating toolbar.
- [x] Resize move aktualizuje preview model a layout.
- [x] Resize commit zavola `UpdateImageLayout` command jednou.
- [x] Resize cancel rollbackne preview model.
- [x] Unit test: object selection se serializuje do selection snapshotu.
- [x] E2E test: obrazek nema dva floating toolbary.

Poznamka k implementaci: faze 11 pridava `window.tmDocumentEditorEngine.objects` s normalizaci anchored image objectu, wrap mode modely, text exclusions, deterministic available intervals, preview controller pro drag/resize a widget adapterem podle CKEditor konceptu. Strict layout engine umi floating image blocks pro Square/Tight/Through/Behind/InFront, TopBottom jako full-width exclusion, hit-test respektuje available intervals a object priority. Renderer vykresluje object/fake selection, 8 resize handlu, rotation handle a jednu layout bubble; inspector schovava data URI a ukazuje URL jen pro skutecne http(s) zdroje.

## Faze 12: Revision engine jako semantic overlay

Stav: hotovo

### 12.1 Revision model

- [x] Revision ma id.
- [x] Revision ma type.
- [x] Revision ma author.
- [x] Revision ma timestamp.
- [x] Revision ma affected range.
- [x] Revision ma payload pro insertion/deletion/format change.
- [x] Revision nema byt obycejny CSS mark ve formatting state.

### 12.2 Rendering revizi

- [x] Insertion overlay se renderuje jako dekorace.
- [x] Deletion overlay se renderuje podle review display mode.
- [x] Format change overlay nezmeni actual formatting state.
- [x] Tooltip/review popover cte revision model.
- [x] Toolbar formatting state ignoruje dekoracni revizni styl.
- [x] Layout measurement pouziva skutecny visible text podle review mode.
- [x] Revision marker nesmi byt soucasti inline formatting marks.
- [x] Revision overlay ma vlastni z-index vrstvu nad textem, ale pod selection/caret podle UX pravidla.
- [x] Revision overlay se mapuje pres model ranges, ne pres DOM spans bez model vazby.
- [x] Marker differ umi invalidovat jen affected overlay scopes.

### 12.3 Input u reviznich hranic

- [x] Psaní pred insertion revision s tracking off vlozi normalni text mimo revision.
- [x] Psaní za insertion revision s tracking off vlozi normalni text mimo revision.
- [x] Psaní uvnitr insertion revision s tracking off ma explicitni pravidlo.
- [x] Enter za revision textem nevlozi text na zacatek dokumentu.
- [x] Space za revision textem nerozhodi poradi slov.
- [x] Backspace u hranice revision ma explicitni pravidlo.
- [x] Delete u hranice revision ma explicitni pravidlo.

### 12.4 Track changes on

- [x] InsertText pri tracking on vytvori insertion revision.
- [x] DeleteRange pri tracking on vytvori deletion revision.
- [x] Format change pri tracking on vytvori format revision.
- [x] Navazujici typing do stejne revision se muze sloucit.
- [x] Toolbar state stale ukazuje skutecny formatting.
- [x] E2E test musi overit psani pred/za/uvnitr revision.

### 12.5 Accept/reject

- [x] Accept insertion promuje text do normalniho obsahu.
- [x] Reject insertion odstrani text.
- [x] Accept deletion odstrani text.
- [x] Reject deletion obnovi text.
- [x] Accept format change aplikuje skutecny formatting.
- [x] Reject format change vrati puvodni formatting.
- [x] Po accept/reject se layout prepocita bez rozhozeni caret.
- [x] Po accept/reject toolbar ukazuje spravny actual formatting.

Poznamka k implementaci: faze 12 pridava `window.tmDocumentEditorEngine.revisions` se semantic revision modelem, overlay modelem, review popoverem, visible-text projekci podle review mode, track-changes insert/delete/format operacemi, coalescingem insertion typing a accept/reject mutacemi obsahu. Enter u reviznich hranic pouziva inline-metadata-preserving split, aby se neztracely `revisionId`, marks ani style na runech.

## Faze 13: Commands, toolbar a formatting state

Stav: hotovo

### 13.1 Command dispatcher

- [x] Ribbon command jde do JS engine.
- [x] Floating toolbar command jde do JS engine.
- [x] Context menu command jde do JS engine.
- [x] Keyboard shortcut jde do JS engine.
- [x] Vsechny cesty pouzivaji stejny command id.
- [x] Command pouziva aktualni logical selection.
- [x] Command vraci transaction result.
- [x] Command failure se zobrazi strukturovane v debug logu.
- [x] Kazdy command ma `isEnabled`.
- [x] Kazdy command ma `value`.
- [x] Kazdy command ma `refresh(selectionSnapshot, modelSnapshot)`.
- [x] Command refresh bezi po kazde committed transaction.
- [x] Command refresh bezi po selection-only change.
- [x] Command execution nikdy necita DOM selection primo, ale runtime selection snapshot.
- [x] Command execution nikdy nemeni DOM primo.

### 13.2 Inline formatting

- [x] Bold command aplikuje mark pres operation.
- [x] Italic command aplikuje mark pres operation.
- [x] Underline command aplikuje mark pres operation.
- [x] Strike command aplikuje mark pres operation.
- [x] Text color command aplikuje mark pres operation.
- [x] Background color command aplikuje mark pres operation.
- [x] Link command aplikuje mark/payload pres operation.
- [x] Clear formatting command odstrani marks podle pravidel.

### 13.3 Paragraph formatting

- [x] Alignment command meni paragraph attribute.
- [x] Line spacing command meni paragraph attribute.
- [x] Spacing before/after command meni paragraph attribute.
- [x] List command meni paragraph/list model.
- [x] Indent command meni paragraph attribute.
- [x] Paragraph command zachova caret.
- [x] Toolbar state po commandu zustane vybrany podle skutecneho stavu.

### 13.4 Formatting state snapshot

- [x] JS engine vypocita active inline marks.
- [x] JS engine vypocita mixed inline state.
- [x] JS engine vypocita paragraph attributes.
- [x] JS engine vypocita image selection state.
- [x] JS engine vypocita table selection state.
- [x] Snapshot se posle do Blazoru.
- [x] Ribbon se aktualizuje z tohoto snapshotu.
- [x] Floating toolbar se aktualizuje z tohoto snapshotu.
- [x] Side panel se aktualizuje z tohoto snapshotu.
- [x] Snapshot ignoruje dekoracni styly revizi.
- [x] Snapshot rozlisuje actual formatting, mixed formatting a pending typing marks.
- [x] Snapshot obsahuje active command values.
- [x] Snapshot obsahuje disabled reasons pro commandy.
- [x] Unit test: kurzor v zelenem reviznim overlayi nevraci text color green, pokud canonical mark neni green.

Poznamka k implementaci: faze 13 pridava `window.tmDocumentEditorEngine.commands` s command dispatcherem, normalizaci command id pro ribbon/floating/context/keyboard, command state (`isEnabled`, `value`, `refresh`), debug logem, formatting snapshotem pro Blazor toolbar vrstvy a command operacemi nad canonical modelem. Inline a paragraph commandy pouzivaji operation vrstvu a runtime logical selection; DOM selection se pri command execution necita.

## Faze 14: Tables

Stav: hotovo

### 14.1 Table model

- [x] Table ma id.
- [x] Row ma id.
- [x] Cell ma id.
- [x] Cell obsahuje block model.
- [x] Cell ma rowSpan/colSpan.
- [x] Cell ma width/height constraints.
- [x] Cell ma border/background/padding model.

### 14.2 Table layout

- [x] Layoutovat table do page body frame.
- [x] Layoutovat cell content pres stejny paragraph layout.
- [x] Respektovat page breaks podle minimalniho pravidla.
- [x] Hit-test cell podle layout tree.
- [x] Selection v cell zustava v cell regionu.
- [x] Text uvnitr cell nesmi prekryvat border/ostatni cell.

### 14.3 Table commands

- [x] Insert table command.
- [x] Insert row above/below.
- [x] Insert column left/right.
- [x] Delete row.
- [x] Delete column.
- [x] Merge cells.
- [x] Split cell.
- [x] Cell background.
- [x] Cell border.
- [x] Table resize.

### 14.4 E2E table quality

- [x] Klik do cell nastavi caret.
- [x] Psaní v cell nelita mezi radky.
- [x] Toolbar state odpovida textu v cell.
- [x] Kontextove menu zustava citelne.
- [x] Resize table nevytvori overlap.
- [x] Save/reload zachova table layout.

Poznamka k implementaci: faze 14 rozsiruje canonical table model o `rowSpan`, `colSpan`, rozmery a styl bunek, pridava table layout pres stejnou paragraph layout pipeline pro obsah bunek, cell hit-test, cell-aware selection snapshot, `window.tmDocumentEditorEngine.tables` controller a table commandy v command dispatcheru. Strict E2E overuje roundtrip modelu, layout bez overlapu, commandy radku/sloupcu/merge/split/stylu/resize a stabilni psani v bunce.

## Faze 15: Undo/redo a history

Stav: hotovo

### 15.1 Undo stack

- [x] JS runtime ma vlastni undo stack.
- [x] Undo stack uklada transactions.
- [x] Transaction ma inverse operations.
- [x] Undo obnovi model.
- [x] Undo obnovi selection.
- [x] Undo invaliduje layout scopes.
- [x] Undo spusti atomic render.
- [x] Undo pouzije inverse operations v opacnem poradi.
- [x] Undo transformuje ulozenou selection pres pozdejsi operations, pokud je to potreba.
- [x] Undo transaction ma typ `undo` a neposila se jako nova user editace bez rozliseni.

### 15.2 Redo stack

- [x] Redo stack uklada undone transactions.
- [x] Redo obnovi model.
- [x] Redo obnovi selection.
- [x] Redo invaliduje layout scopes.
- [x] Redo spusti atomic render.
- [x] Nova transaction po undo vycisti redo stack.
- [x] Redo transaction ma typ `redo`.
- [x] Redo obnovuje selection po commitu, ne pred renderem.

### 15.3 History boundaries

- [x] Typing coalescing je jedna undo transaction podle pravidla.
- [x] Enter je samostatna transaction.
- [x] Paste je samostatna transaction.
- [x] Toolbar formatting je samostatna transaction.
- [x] Image drag je samostatna transaction.
- [x] Image resize je samostatna transaction.
- [x] Accept/reject revision je samostatna transaction.

### 15.4 E2E history

- [x] Undo po psani vrati text a caret.
- [x] Redo po psani obnovi text a caret.
- [x] Undo po Enter spoji/vrati odstavce.
- [x] Undo po image drag vrati objekt a layout.
- [x] Undo po accept revision vrati revision.
- [x] Toolbar state po undo/redo odpovida selection.

Poznamka k implementaci: faze 15 pridava modelovy `HistoryController`, snapshotovou inverse operaci `RestoreSnapshot`, oddelene undo/redo stacky, coalescing psani, history boundaries pro Enter/paste/toolbar/image/revision a napojeni verejneho strict engine `applyCommand('undo'/'redo')`. Undo/redo obnovuje canonical model i selection a po commitu znovu spousti layout/render pres atomic renderer.

## Faze 16: Boundary synchronizace s Blazorem a C#

Stav: hotovo

### 16.1 JS -> C# patch boundary

- [x] Boundary patch se posila po commit transaction.
- [x] Patch obsahuje transaction id.
- [x] Patch obsahuje operation ids.
- [x] Patch obsahuje affected block ids.
- [x] Patch obsahuje canonical model delta nebo snapshot podle potreby.
- [x] Patch obsahuje selection snapshot, pokud je potreba pro Blazor UI.
- [x] Patch failure neznici JS runtime state.

### 16.2 C# -> JS updates

- [x] Load initial document posila full snapshot.
- [x] Save ack posila saved version/epoch.
- [x] Remote operation posila operation batch.
- [x] Provider image URL update posila targeted asset update.
- [x] Full snapshot refresh je recovery, ne normalni hot path.
- [x] C# update nesmi prepsat aktivni uncommitted transaction.

### 16.3 Dirty/autosave

- [x] Dirty state vznikne po commit transaction.
- [x] Dirty state se posle do Blazoru.
- [x] Autosave si vyzada canonical snapshot z JS.
- [x] Autosave ack nastavi saved epoch.
- [x] Autosave failure nevrati model zpet.
- [x] UI "Neulozene zmeny" cte runtime dirty state.

### 16.4 Public API compatibility

- [x] Existujici public parameters zustanou kompatibilni, pokud to jde.
- [x] Existing demo load/save funguje.
- [x] Existing export ODT command funguje nad canonical snapshotem.
- [x] Existing import ODT flow aktualizuje runtime snapshot.
- [x] Existing comments/revisions panels dostanou data z runtime boundary.

Poznamka k implementaci: faze 16 pridava strict boundary API pro JS -> C# patche (`HandleJsBoundaryPatchGenerated`), dirty state callback (`HandleJsDirtyStateChanged`), save ack, autosave snapshot, autosave failure bez rollbacku, remote operation batch, targeted provider image URL update, recovery-only full snapshot refresh a panelova data pro comments/revisions. Public API zachovava canonical snapshot pro export/import a chrani aktivni transakci pred prepisem z C#.

## Faze 17: Diagnostics, watchdog a failure artifacts

Stav: hotovo

### 17.1 Debug snapshot

- [x] Snapshot obsahuje model version.
- [x] Snapshot obsahuje layout version.
- [x] Snapshot obsahuje render version.
- [x] Snapshot obsahuje selection.
- [x] Snapshot obsahuje active transaction.
- [x] Snapshot obsahuje undo/redo depth.
- [x] Snapshot obsahuje invalidated scopes.
- [x] Snapshot obsahuje performance stats.
- [x] Snapshot obsahuje last errors.

### 17.2 Layout probe

- [x] Probe vrati vsechny text recty.
- [x] Probe vrati vsechny image recty.
- [x] Probe vrati vsechny caption recty.
- [x] Probe vrati vsechny line boxes.
- [x] Probe vrati vsechny exclusion zones.
- [x] Probe vrati vsechny collisions.
- [x] Probe rozlisi povoleny a zakazany overlap.
- [x] Probe umi bezet po kazdem animation frame v testu.

### 17.3 Event timeline

- [x] Timeline loguje input event.
- [x] Timeline loguje normalized operation.
- [x] Timeline loguje transaction commit.
- [x] Timeline loguje layout pass.
- [x] Timeline loguje render pass.
- [x] Timeline loguje selection restore.
- [x] Timeline loguje Blazor patch emit.
- [x] Timeline loguje error/recovery.

### 17.4 Watchdog recovery

- [x] Pri operation failure rollbacknout transaction.
- [x] Pri layout failure pouzit safe layout fallback.
- [x] Pri render failure vratit posledni validni render.
- [x] Pri selection restore failure nastavit caret na nejblizsi validni pozici.
- [x] Pri opakovanych failures zobrazit debug warning.
- [x] Recovery nesmi tiše zahodit uzivatelsky text.

## Faze 18: Strict E2E test platform

Stav: hotovo

### 18.1 Human-like helpers

- [x] Helper pro realny mouse click do visual line.
- [x] Helper pro realny drag text selection.
- [x] Helper pro psani po znacich s frame probes.
- [x] Helper pro toolbar command click.
- [x] Helper pro context menu command.
- [x] Helper pro image drag/resize.
- [x] Helper pro screenshot crop editor page.
- [x] Helper pro console error capture.

### 18.2 Frame probes

- [x] Probe pred akci.
- [x] Probe po `requestAnimationFrame`.
- [x] Probe po 50 ms.
- [x] Probe po 150 ms.
- [x] Probe po idle layoutu.
- [x] Probe po save/reload, pokud je zmena persistentni.
- [x] Test musi umet failnout na libovolne probe.

### 18.3 Visual assertions

- [x] Text/text overlap.
- [x] Text/image overlap.
- [x] Text/caption overlap.
- [x] Toolbar overlap.
- [x] Floating toolbar visibility.
- [x] Context menu visibility.
- [x] Side panel clipping.
- [x] Caret rect inside active page body.
- [x] Selection highlight over expected text.

### 18.4 Test naming

- [x] Vsechny nove testy pojmenovat `DocumentEditor_Strict_Engine_<Behavior>`.
- [x] Test failure message musi popsat lidske chovani, ktere je rozbite.
- [x] Test failure musi obsahovat cestu k screenshotu.
- [x] Test failure musi obsahovat JSON artifact path.

## Faze 19: Demo data pro engine quality

Stav: hotovo

### 19.1 Contract demo scenarios

- [x] Demo musi obsahovat normalni text.
- [x] Demo musi obsahovat justified paragraph.
- [x] Demo musi obsahovat left wrapped image.
- [x] Demo musi obsahovat right wrapped image.
- [x] Demo musi obsahovat top/bottom image.
- [x] Demo musi obsahovat inline image.
- [x] Demo musi obsahovat image with caption.
- [x] Demo musi obsahovat image without alt warning.
- [x] Demo musi obsahovat revision insertion.
- [x] Demo musi obsahovat revision deletion.
- [x] Demo musi obsahovat comment anchor.
- [x] Demo musi obsahovat table.
- [x] Demo musi byt hezke a realisticke, ne nahodny chaos.

### 19.2 Deterministic reset

- [x] Reset endpoint vrati stejny canonical document.
- [x] Demo version timestamp nesmi rozbijet approval testy.
- [x] Images musi mit stabilni ids.
- [x] Revisions musi mit stabilni ids.
- [x] Comments musi mit stabilni ids.
- [x] E2E musi umet reset pred kazdym scenarem.

### 19.3 Visual demo quality

- [x] Po reloadu default demo nesmi mit overlap.
- [x] Po reloadu default demo musi byt citelne bez rucniho scrollu uvnitr side panelu, pokud je obsah kratky.
- [x] Default demo musi ukazovat realne schopnosti wrap enginu.
- [x] Demo nesmi skryvat narocne scenare, ale musi byt esteticky ucesane.

## Faze 20: Migrace ze stareho DOM-driven path

Stav: hotovo pro hard cut. Legacy DOM-driven engine byl 2026-05-22 odstraneny z runtime cesty; default editor jde pres `tmDocumentEditorRuntime` -> `tmDocumentEditorEngine` bez fallbacku na stare globaly.

### 20.1 Strangler strategy

- [x] Nejdrive prepnout pouze test route nebo feature flag.
- [x] Potom prepnout plain paragraph editing.
- [x] Potom prepnout formatting commands.
- [x] Potom prepnout image wrap scenarios.
- [x] Potom prepnout revisions.
- [x] Potom prepnout tables.
- [x] Nakonec prepnout default demo.
- [x] Stary path ponechat jen do doby, nez projdou strict gates.

### 20.2 Odstraneni legacy hot path

- [x] Identifikovat funkce, ktere primo meni layout DOM text nodes.
- [x] Identifikovat funkce, ktere z DOM skladaji canonical model jako primarni source.
- [x] Identifikovat stare debounce reflow hacky.
- [x] Identifikovat stare sidecar image workarounds.
- [x] Identifikovat duplicitni toolbar renderery.
- [x] Odstranit legacy path az po green testech.
  - Poznamka 2026-05-22: legacy implementace je odstranena z `document-editor-wysiwyg.js`; `tmDocumentEditorRuntime.create` vzdy vytvari Google Docs-like engine a `getMigrationStatus` vraci `legacyEngineRemoved: true`.
- [x] Kazde odstraneni mit maly regression test.

### 20.3 Facade cleanup

- [x] Public `window.tmDocumentWysiwyg` a `window.tmDocumentEditorWysiwyg` uz nejsou kompatibilni globaly; hard-cut testy overuji jejich absenci.
- [x] Interni moduly maji jasny import/order bootstrap.
- [x] `document-editor-wysiwyg.js` uz neobsahuje stary DOM-driven fallback; zustava v nem novy engine plus runtime facade.
  - Poznamka 2026-05-22: fyzicke rozdeleni do samostatnych assetu je samostatny module-split ukol, ne podminka odstraneni legacy enginu.
- [x] Nove moduly maji `node --check` coverage.
- [x] Bundle/static asset references v demo apps se aktualizuji.
- [x] Unit/JS testy jsou prepsane na novy strict engine contract a E2E helpery nepouzivaji odstranene `tmDocumentEditorEngine.executeCommand` entrypointy.

## Faze 21: Accessibility a keyboard polish

Stav: hotovo

### 21.1 Focus model

- [x] Editor root ma jasny focus owner.
- [x] Body/header/footer/table cell/image selection maji definovane focus transitions.
- [x] Focus se nevraci do predchoziho regionu po kliknuti jinam.
- [x] Floating UI nekrade focus, pokud nema.
- [x] Dialog/popup focus trap je explicitni.

### 21.2 Screen reader model

- [x] Editor exposeuje role/aria podle practical contenteditable patternu.
- [x] Selection changes maji umirneny live region.
- [x] Image alt warning je dostupny.
- [x] Revision popover je dostupny.
- [x] Toolbar buttons maji spravne aria pressed/value.

### 21.3 Keyboard UX

- [x] Tab chovani v editoru je definovane.
- [x] Escape zavira floating UI nebo rusí object preview transaction.
- [x] Context menu keyboard shortcut funguje.
- [x] Ctrl+B/I/U funguje pres command dispatcher.
- [x] Ctrl+Z/Y funguje pres JS undo/redo.
- [x] Ctrl+S vyvola save boundary.

## Faze 22: Performance a memory

Stav: hotovo

### 22.1 Performance baselines

- [x] Zmerit typing latency u 1 stranky.
- [x] Zmerit typing latency u 10 stranek.
- [x] Zmerit typing latency u 100 stranek virtualized.
- [x] Zmerit image drag latency.
- [x] Zmerit selection movement latency.
- [x] Zapsat baseline do test outputu nebo planning poznámky.

Poznamka: baseline metriky jsou soucasti `DocumentEditorRuntimePhase22PerformanceJavaScriptTests`. Testovaci harness uklada typing/image drag/selection latency summary a snapshot obsahuje pocet baseline zaznamu, aby slo dalsi faze porovnavat proti stejnemu kontraktu.

### 22.2 Incremental invalidation

- [x] InsertText invaliduje pouze affected paragraph.
- [x] DeleteRange v jednom paragraph invaliduje pouze affected paragraph.
- [x] Split/Merge invaliduje affected blocks a page flow za nimi.
- [x] Image move invaliduje paragraphs v affected exclusion range.
- [x] Table edit invaliduje affected cell/table.
- [x] Full document layout se nespousti pri kazdem znaku.

### 22.3 Virtualization

- [x] Offscreen pages zustanou virtualized.
- [x] Active page musi byt plne rendered.
- [x] Selection near offscreen page umi page materializovat.
- [x] Layout snapshot existuje i pro virtual pages.
- [x] E2E test s vice strankami kontroluje scroll a caret.

### 22.4 Memory cleanup

- [x] Dispose odstrani event listenery.
- [x] Dispose odstrani timers.
- [x] Dispose odstrani observers.
- [x] Dispose vycisti measurement cache podle instance.
- [x] Dispose odpoji DotNetRef callbacks.
- [x] E2E/JS test kontroluje repeated create/dispose bez leak warningu.

## Faze 23: UX polish pro Google Docs-like pocit

Stav: hotovo

### 23.1 Visual stability

- [x] Pri psani nesmi blikat cely odstavec.
- [x] Pri psani nesmi blikat stranka.
- [x] Pri psani nesmi poskakovat toolbar.
- [x] Pri psani nesmi mizet floating toolbar, pokud je selection stale relevantni.
- [x] Pri commandu se toolbar state zmeni okamzite a zustane stabilni.

### 23.2 Object polish

- [x] Image selection outline je cisty a citelny.
- [x] Resize handles jsou dost velke a neprekryvaji caption text.
- [x] Object toolbar se umisti nad objekt, pokud je misto.
- [x] Object toolbar se presune mimo side panel, pokud by se prekryl.
- [x] Layout bubble je compact a srozumitelna.
- [x] Selection pane je dostupny pro prekryte objekty.

### 23.3 Text editing feel

- [x] Space se objevi hned, ne az po dalsim znaku.
- [x] Enter rozdeli text bez docasneho poskoku.
- [x] Backspace merge ukaze finalni radek hned.
- [x] Long word wrap je predvidatelny.
- [x] Click-to-caret vedle obrazku pusobi prirozene.
- [x] Selection drag nevypina floating toolbar predcasne.

### 23.4 Side panel sync

- [x] Properties panel cte stav z runtime selection.
- [x] Image panel cte vybrany image object.
- [x] Revision panel cte active revision range.
- [x] Comments panel cte active comment anchor.
- [x] Panel inputy aplikuji zmeny pres debounced commands.
- [x] Panel inputy necekaji na blur, pokud UX vyzaduje live preview.

Poznamka: Faze 23 pridava meritelny UX polish kontrakt v JS runtime: visual stability tracker, object chrome model, immediate text edit preview, side panel sync state a panel command debouncer. Overeno JS unit testy a browser E2E testem `DocumentEditorStrictEnginePhase23E2ETests`.

## Faze 24: Release quality gate

Stav: castecne hotovo - DocumentEditor core gate je zeleny, legacy/quality smoke gate ma jeste neaktualizovane nebo rozbite scenare

### 24.1 Unit test suite

- [x] Spustit JS/model unit testy.
- [x] Spustit C# layout/model unit testy.
- [x] Spustit bUnit component tests pro toolbar/panely.
- [x] Spustit targeted revision tests.
- [x] Spustit targeted image wrapping tests.
- [x] Spustit targeted table tests.

### 24.2 E2E suite

- [x] Spustit strict live typing suite.
- [x] Spustit strict selection/caret suite.
- [x] Spustit strict toolbar command suite.
- [x] Spustit strict image wrap suite.
- [x] Spustit strict revision suite.
- [x] Spustit strict table suite.
- [x] Spustit save/reload suite.
- [ ] Spustit viewport matrix desktop/mobile aspon smoke.

### 24.3 Manual UX pass

- [x] Restartovat demo API.
- [x] Restartovat WASM demo.
- [x] Otevrit `/document-editor`.
- [x] Projit default contract demo.
- [x] Rucne psat rychle do odstavce pred obrazkem.
- [x] Rucne psat vedle obrazku.
- [ ] Rucne presouvat obrazek.
- [ ] Rucne menit wrap mode.
- [x] Rucne prijmout/odmitnout revizi.
- [x] Rucne pouzit toolbar formatting.
- [x] Rucne ulozit/reloadnout.
- [x] Zapsat vysledek do tohoto souboru nebo navazujici poznamky.

### 24.4 Cleanup

- [x] Odstranit dočasné debug logy, ktere nejsou za debug flagem.
- [x] Ponechat uzitecne diagnostics za explicitnim flagem.
- [x] Aktualizovat planning stav.
- [x] Aktualizovat dokumentaci verejneho chovani, pokud se zmenilo API.
- [x] Zkontrolovat, ze demo servery po overeni nezustaly bez potreby bezet.

Poznamka faze 24:

- Opraveny release gate nalezy v novem JS runtime: `HandleJsDirtyStateChanged` JSInvokable boundary, import numeric `DocumentBlockType` hodnot pro table/image/page break, import vnorenych `Transform/Wrap/Position/Stacking` layout dat, render obrazku jako skutecne `img`, DOM restore caret po modelove `beforeinput`, hit-test pred/za radkem a skryti image layout bubble tak, aby netvoril falesne text recty.
- Overeno unit/bUnit targeted prikazem `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorRuntimePhase|FullyQualifiedName~DocumentEditorLayoutPhase|FullyQualifiedName~Components.DocumentEditor|FullyQualifiedName~TmDocumentWysiwygHostTests"`: 762/762 passed.
- Overeno strict core E2E prikazem `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorStrictEnginePhase0E2ETests|FullyQualifiedName~DocumentEditorStrictEnginePhase4E2ETests|FullyQualifiedName~DocumentEditorStrictEnginePhase13E2ETests"`: 12/12 passed.
- Overeno strict image/revision/table/save/UX E2E prikazem `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorStrictEnginePhase11E2ETests|FullyQualifiedName~DocumentEditorStrictEnginePhase12E2ETests|FullyQualifiedName~DocumentEditorStrictEnginePhase14E2ETests|FullyQualifiedName~DocumentEditorStrictEnginePhase16E2ETests|FullyQualifiedName~DocumentEditorStrictEnginePhase19E2ETests|FullyQualifiedName~DocumentEditorStrictEnginePhase21E2ETests|FullyQualifiedName~DocumentEditorStrictEnginePhase23E2ETests"`: 20/20 passed.
- Smoke/accessibility/demo-docs gate stale/failing prikazem `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorQualitySmokeTests|FullyQualifiedName~DocumentEditorPhase21AccessibilityE2ETests|FullyQualifiedName~DocumentEditorPhase22DemoDocsE2ETests"`: 7 failed, 3 passed, 1 skipped. Selhani: command palette italic state, table grid focus class, live region find announcement, table scenario toolbar selector, quality smoke header selector, performance full render count, remote patch first inline selector. Tyto scenare je potreba samostatne prevest na novy Google Docs runtime DOM/contracts nebo opravit produktove chovani, pokud uz nejsou jen legacy kontrakty.
- Po overeni byly demo procesy ukonceny, aby nezustalo bezet API/WASM demo mimo aktualni gate.

## Prubezny implementacni protokol

Pri kazde fazi:

- [ ] Nejdřív oznacit presne body, ktere se budou delat.
- [ ] U engine-core fazi overit, zda se aktualni krok dotyka CKEditor-inspired hranic: schema, operation, differ, mapper, command nebo widget.
- [ ] Napsat RED test nebo diagnosticky gate.
- [ ] Spustit test a ulozit/poznamenat ocekavane selhani.
- [ ] Implementovat nejmensi nutnou zmenu.
- [ ] Spustit cileny test.
- [ ] Spustit okolni regresni testy.
- [ ] Pokud jde o UX, zkontrolovat screenshot nebo video/demo.
- [ ] Odskrtnout pouze body, ktere jsou skutecne hotove.
- [ ] Pokud se objevi novy problem, pridat ho jako novy checkbox misto ticheho odlozeni.

## Poznamky k prioritam

Nejvetsi dopad na pocit pouzitelnosti ma toto poradi:

1. [ ] RED e2e frame probes pro psani a prekryvy.
2. [ ] Schema + operation/transaction model bez DOMu.
3. [ ] Differ + invalidation buffer.
4. [ ] Logical selection + selection post-fixer jako source of truth.
5. [ ] Model-layout-DOM mapper.
6. [ ] Immediate active paragraph layout.
7. [ ] Atomic renderer s oddelenou editing/data projection.
8. [ ] Image widget/wrap intervals a hit-test.
9. [ ] Revision semantic overlay.
10. [ ] Command/toolbar state z runtime selection.
11. [ ] Undo/redo transakce.
12. [ ] Legacy DOM-driven path cleanup.

Dokud nejsou hotove body 1-7, budou se podobne chyby porad vracet v jinych podobach. Body 8-12 pak udelaji z editoru skutecne profesionalni nastroj misto stabilizovaneho prototypu.
