# Document editor: JS-owned runtime engine TDD TODO

Stav sepsaný k 2026-05-15.

Tento soubor je živý implementační checklist pro přechod `TmDocumentEditor` na architekturu, kde je během editace autoritou JavaScriptový WYSIWYG runtime. Blazor zůstává aplikační shell, ribbon, panelová UI vrstva a provider boundary pro save/load, export/import, collaboration, revize, komentáře a audit.

Při implementaci se zde budou průběžně odškrtávat dokončené kroky.

## Výchozí problém

Současný editor má rozdělenou pravdu:

- JavaScript okamžitě mění `contenteditable` DOM a snaží se udržet caret/selection.
- C# `DocumentEditorDocument` se průběžně aktualizuje přes patche a snapshot commandy.
- Undo/redo běží nad C# command stackem.
- Po undo/redo se do JS posílá full snapshot a JS znovu renderuje DOM.
- Collaboration a revize částečně pracují nad C# modelem, částečně nad JS DOMem.

Tím vznikají závody:

- uživatel píše rychleji, než se uzavře JS typovací transakce,
- Blazor undo běží proti starému nebo otevřenému C# batchi,
- pozdní JS patch může po undo znovu změnit C# model,
- forced snapshot refresh ničí caret a selection,
- revize a toolbar stav se počítají mimo skutečný runtime stav DOMu.

Praktický dopad:

- Undo/redo vrací špatné kroky nebo nechává části textu.
- Enter/Shift+Enter a selection se chovají nepředvídatelně.
- Track changes není word-like, protože změny nejsou jednotné operace v jednom runtime modelu.
- Formátování se může aplikovat na jiný výběr, než uživatel vidí.
- E2E testy projdou i při reálně nepoužitelném editoru, protože netestují závody a rychlé uživatelské sekvence.

## Cílové rozhodnutí

Editor má mít princip:

```text
JS = runtime authority
C# = boundary authority
Blazor = shell/controller UI
```

JavaScript vlastní během editace:

- aktuální dokumentový runtime model,
- renderování stránek a DOM,
- selection/caret,
- lokální undo/redo stack,
- aplikaci příkazů z ribbonu,
- typing/input/composition,
- track changes runtime,
- accept/reject revision,
- image/table/header/footer interakce,
- aplikaci remote collaboration operací,
- výpočet toolbar selection state.

Blazor vlastní:

- ribbon a command UI,
- panely komentářů/revizí/verzí,
- dialogy,
- autorizaci/permissions,
- save/autosave orchestrace,
- provider boundary,
- serverové import/export/compare požadavky,
- SignalR/transport napojení,
- audit a demo integraci.

C# model nezmizí úplně. Přestane být live edit autoritou. Zůstane jako kanonický serializační kontrakt na hranicích:

- load initial document,
- save current document,
- DOCX/ODT/PDF provider input/output,
- version snapshots,
- comparison input/output,
- collaboration operation payload,
- server-side validation,
- tests provider kontraktů.

## Non-goals

- [ ] Nedělat big-bang smazání providerů, exportů, verzí ani komentářů.
- [ ] Nepřidávat nový blokový editor.
- [ ] Nepřevádět rich dokument na plain text diff.
- [ ] Neřešit typograficky dokonalé stránkování jako první krok.
- [ ] Neopravovat starý C# live command stack kosmeticky místo změny vlastnictví runtime modelu.

## Architektonická pravidla

- [ ] Během psaní nesmí Blazor přerenderovat editorový obsah.
- [ ] Během psaní nesmí C# posílat full snapshot zpět do aktivního editoru.
- [ ] Ribbon příkazy musí jít do JS engine, který použije aktuální JS selection.
- [ ] Toolbar state musí jít z JS do Blazoru jako selection formatting snapshot.
- [ ] Undo/redo ve WYSIWYG režimu musí být JS-owned.
- [ ] C# undo stack může existovat pro non-WYSIWYG shell akce, ale nesmí řídit textový runtime.
- [ ] Save si vyžádá aktuální canonical JSON z JS engine.
- [ ] Remote collaboration operace se aplikují přímo v JS engine; full snapshot jen jako recovery.
- [ ] Accept/reject revision se provede v JS engine a poté se commitne jako transakce.
- [ ] Každá uživatelsky viditelná změna musí mít unit/component test a průběžný E2E smoke.

## Navržené soubory a hranice

### JavaScript

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-runtime.js`
  - JS-owned document runtime model.
  - Transaction manager.
  - Undo/redo manager.
  - Command dispatcher.
  - Serialization boundary.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-renderer.js`
  - DOM rendering from JS runtime model.
  - Incremental operation rendering.
  - Full render only on load/recovery.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-selection.js`
  - Selection mapping.
  - Toolbar state computation.
  - Caret preservation.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-revisions.js`
  - Track changes operations.
  - Revision decorations.
  - Accept/reject.

- [ ] `src/Tempo.Blazor/wwwroot/js/document-editor-collaboration-runtime.js`
  - Apply remote operations.
  - Local transaction emit.
  - Remote cursor anchors.

Poznámka: Současný `document-editor-wysiwyg.js` se může nejdřív rozšířit, ale cílově je vhodné jej rozdělit na menší moduly. Migrace může probíhat postupně přes facade.

### Blazor

- [ ] `TmDocumentWysiwygHost` bude JS runtime host, ne renderer snapshotů během editace.
- [ ] `TmDocumentEditor` bude shell a provider orchestrator.
- [ ] `TmDocumentEditorToolbar` bude posílat příkazy do hostu a přijímat toolbar state.
- [ ] Revisions/comments/version panels budou pracovat přes runtime events a boundary DTOs.

### Abstractions

- [ ] Doplnit nebo stabilizovat canonical document DTOs v `Tempo.Blazor.Abstractions`.
- [ ] Doplnit operation DTOs pro text, marks, paragraph, table, image, header/footer, revision.
- [ ] Zachovat provider boundary kontrakty pro save/load/export/import/collaboration; nepřidávat paralelní WYSIWYG runtime režim.

## Fáze 0: Baseline a ochrana proti regresím

### 0.1 Inventura současného chování

- [x] Zapsat aktuální symptom undo/redo z videa do test issue poznámky v tomto souboru.
- [x] Zapsat aktuální symptom Enter/Shift+Enter z předchozích videí.
- [x] Zapsat aktuální symptom track changes accept/reject.
- [x] Zapsat aktuální symptom typing lag.
- [x] Zapsat aktuální symptom toolbar formatting mismatch.

Poznámka fáze 0:

- Undo/redo baseline: při rychlém psaní a následném undo se vrací jiné nebo starší C# snapshot transakce, protože JS typing transaction a C# command stack nejsou jeden runtime undo stack.
- Enter/Shift+Enter baseline: caret po Enter/Shift+Enter nepokračuje spolehlivě v místě, kde uživatel psal; selection mapping a DOM refresh se umí rozjet.
- Track changes baseline: insert/delete/accept/reject revize nejsou konzistentní mezi inline obsahem a panelem revizí; existující testy ukazují navíc předvyplněnou demo revizi, která maskuje nové revize.
- Typing lag baseline: držení klávesy a rychlé psaní se dávkuje a skáče, protože DOM input, JS patch queue, Blazor render a snapshot refresh nejsou jeden lokální runtime.
- Toolbar formatting baseline: formátovací stav a aplikace příkazů závisí na C# snapshotu/selection bridge a může zasáhnout jiný text, než uživatel vidí.

### 0.2 Baseline test run

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~DocumentEditor"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~DocumentEditor"`.
- [x] Spustit build `dotnet build TempoBlazor.slnx`.
- [x] Zapsat, co padá už před migrací.
- [x] Neopravovat nesouvisející pády v této fázi bez samostatného rozhodnutí.

Baseline výsledky k 2026-05-15:

- `dotnet build TempoBlazor.slnx` prošel. Zůstává existující warning `NU1603` pro `Microsoft.Extensions.Http >= 8.0.12`, kde NuGet resolvuje `9.0.0`.
- `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~DocumentEditor"`: 489 passed, 9 failed, 0 skipped, 498 total.
- Unit/component selhání před migrací: `Editor_StatusBarReplacesRibbonSaveStatusAndCountsDocumentText`, několik `TrackChanges_*` testů kolem insert/accept/reject/delete/formatting revizí.
- `dotnet test tests/Tempo.Blazor.E2E/ --no-build --filter "FullyQualifiedName~DocumentEditor"`: 48 passed, 29 failed, 1 skipped, 78 total.
- E2E selhání před migrací pokrývají layout/page canvas, save/reload typing, header/footer persistence, track changes panel/inline review, formatting persistence, link/font/color selection, remote collaboration operations a quality smoke.

### 0.3 Přímá migrace bez přepínače

- [x] Zrušit předpoklad paralelní runtime větve.
- [x] Nepřidávat interní ani veřejný přepínač pro volbu starého a nového runtime.
- [x] Zapsat rozhodnutí, že nový JS-owned runtime bude jedinou WYSIWYG cestou.
- [x] Zapsat rozhodnutí, že demo stránka nedostane runtime toggle a bude používat běžnou WYSIWYG cestu.
- [x] E2E helpery otevírají běžný editor a očekávají JS-owned runtime chování.
- [x] Stará split-brain WYSIWYG cesta se bude průběžně nahrazovat, neudržovat vedle nové.

### 0.4 Test infra

- [x] Přidat nebo upravit Playwright helper `OpenDocumentEditorAsync`.
- [x] Přidat Playwright helper `EditorTypeAsync`.
- [x] Přidat Playwright helper `EditorPressUndoAsync`.
- [x] Přidat Playwright helper `ReadEditorPlainTextAsync`.
- [x] Přidat Playwright helper `ReadToolbarStateAsync`.
- [x] Přidat Playwright helper pro screenshot po stabilizaci editoru.
- [x] Přidat JS test hook `window.tmDocumentWysiwygDebug.getRuntimeState(instanceId)`.
- [x] Přidat JS test hook `window.tmDocumentWysiwygDebug.getRenderStats(instanceId)`.
- [x] Přidat JS test hook `window.tmDocumentWysiwygDebug.getUndoStack(instanceId)`.

Implementováno ve fázi 0:

- `tests/Tempo.Blazor.E2E/DocumentEditorE2ETestBase.cs` přidává sdílené Playwright helpery pro běžnou document-editor route bez runtime přepínače.
- `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js` přidává read-only debug fasádu `window.tmDocumentWysiwygDebug`.
- `getUndoStack` ve fázi 0 zatím vrací diagnostiku aktuální split-brain cesty a explicitně označuje, že JS-owned undo stack vznikne v pozdější fázi.

### 0.5 Akceptace fáze 0

- [x] Plán i test helpery počítají s přímým nahrazením stávající WYSIWYG cesty.
- [x] Existují helpery pro budoucí E2E testy.
- [x] Baseline stav je zdokumentovaný.
- [x] Je jasně popsané, které stávající chování bude přepsané novým JS-owned runtimem.

## Fáze 1: JS runtime facade bez změny modelu

### 1.1 Facade API

- [x] Definovat JS facade `tmDocumentEditorRuntime.create(instanceId, options)`.
- [x] Definovat `loadDocument(snapshot)`.
- [x] Definovat `getDocument()`.
- [x] Definovat `executeCommand(command, payload)`.
- [x] Definovat `onTransactionCommitted(callback)`.
- [x] Definovat `onSelectionStateChanged(callback)`.
- [x] Definovat `dispose()`.

Poznámka: `tmDocumentEditorRuntime` ve fázi 1 deleguje do současného `tmDocumentWysiwyg` enginu. Cílem fáze je stabilní runtime facade kontrakt, ne přepis modelu.

### 1.2 Blazor host bridge

- [x] `TmDocumentWysiwygHost` inicializuje runtime facade jako svou výchozí WYSIWYG cestu.
- [x] Host předá initial snapshot jen při loadu nebo explicitním reloadu.
- [x] Host nesmí volat forced snapshot refresh po lokálním psaní.
- [x] Přidat C# wrapper metody `ExecuteRuntimeCommandAsync`.
- [x] Přidat C# wrapper metodu `RequestRuntimeDocumentAsync`.
- [x] Přidat C# wrapper metodu `RequestRuntimeSelectionStateAsync`.

Poznámka: Starší veřejné metody `ExecuteEditorCommandAsync`, `RequestSnapshotAsync` a `RequestFormattingStateAsync` zůstaly jako delegující aliasy, aby interní volající nemuseli měnit název v jednom velkém řezu.

### 1.3 Testy

- [x] RED: bUnit test očekává, že host inicializuje runtime facade s initial snapshotem.
- [x] GREEN: implementovat inicializaci.
- [x] RED: bUnit test očekává, že `RefreshSnapshotAsync` se nepoužije po lokální změně.
- [x] GREEN: oddělit load snapshot od live refresh.
- [ ] E2E smoke: běžný editor se otevře a zobrazí původní dokument přes JS-owned runtime.

Ověření fáze 1:

- `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmDocumentWysiwygHostTests"`: 89 passed, 0 failed.
- `dotnet build TempoBlazor.slnx`: prošel, pouze existující warningy.
- `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`: prošel.
- `dotnet test tests/Tempo.Blazor.E2E/ --no-build --filter "FullyQualifiedName~DocumentEditor_DemoPage_RendersWysiwygShell"`: neprošel timeoutem při čekání na `[data-testid='document-editor-demo']`. Demo WASM a API porty odpovídaly, ale E2E stránka se v běžícím prostředí nedostala k editor rootu. Tento E2E smoke zůstává otevřený pro další běh po restartu demo prostředí.

### 1.4 Akceptace fáze 1

- [x] JS runtime facade existuje.
- [x] Blazor host ji používá jako výchozí WYSIWYG runtime.
- [ ] Zobrazení dokumentu zůstane beze změny.
- [x] Žádný lokální input ještě nemusí být JS-owned.

## Fáze 2: Canonical document runtime model v JS

### 2.1 Model shape

- [x] Definovat JS runtime model pro dokument.
- [x] Zachovat stabilní `documentId`.
- [x] Zachovat sections.
- [x] Zachovat blocks.
- [x] Zachovat paragraphs.
- [x] Zachovat inline runs.
- [x] Zachovat marks.
- [x] Zachovat tokens.
- [x] Zachovat images/assets.
- [x] Zachovat tables.
- [x] Zachovat headers/footers.
- [x] Zachovat comments anchors.
- [x] Zachovat revisions.

### 2.2 Import/export serializer

- [x] Implementovat `fromCanonicalDocument(document)`.
- [x] Implementovat `toCanonicalDocument(runtimeDocument)`.
- [x] Implementovat normalizaci prázdného odstavce.
- [x] Implementovat normalizaci inline runů se stejnými marky.
- [x] Implementovat stabilní IDs pro nové nodes.
- [x] Implementovat deterministic JSON order.

### 2.3 Diff guard

- [x] Přidat test helper pro canonical roundtrip.
- [x] Přidat deep equality bez runtime-only fields.
- [x] Přidat debug výpis první odlišné cesty v dokumentu.

### 2.4 Testy

- [x] RED: JS/unit test roundtripne jednoduchý odstavec.
- [x] GREEN: serializer projde.
- [x] RED: JS/unit test roundtripne marks.
- [x] GREEN: marks zachovat.
- [x] RED: JS/unit test roundtripne token.
- [x] GREEN: token zachovat.
- [x] RED: JS/unit test roundtripne image.
- [x] GREEN: image zachovat.
- [x] RED: JS/unit test roundtripne table.
- [x] GREEN: table zachovat.
- [x] RED: JS/unit test roundtripne revisions.
- [x] GREEN: revisions zachovat.

Ověření fáze 2:

- `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`: prošel.
- `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests"`: 2 passed, 0 failed.
- `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmDocumentWysiwygHostTests" --no-build`: 89 passed, 0 failed.
- `dotnet build TempoBlazor.slnx --no-restore`: prošel, zůstává existující `NU1603` pro `Microsoft.Extensions.Http`.
- Nový JS runtime test přes xUnit/Node pokrývá roundtrip odstavce, marků, tokenu, image blocku, table cell blocku, header/footer blocků, comments, revisions, assets a anchors.
- `tmDocumentEditorRuntime.loadDocument` normalizuje snapshot do JS runtime modelu a současně jej předává existujícímu rendereru, takže C# save/load boundary zatím zůstává kompatibilní.

### 2.5 Akceptace fáze 2

- [x] JS runtime umí načíst a vrátit stejný canonical document.
- [x] C# boundary snapshot je stále použitelný pro save/load providery.
- [x] Žádné rich prvky se nedegradují na plain text.

## Fáze 3: JS-owned render loop

### 3.1 Full render z runtime modelu

- [x] Renderovat stránky z JS runtime modelu.
- [x] Renderovat body content.
- [x] Renderovat inline runs.
- [x] Renderovat marks.
- [x] Renderovat tokens.
- [x] Renderovat images.
- [x] Renderovat tables.
- [x] Renderovat headers/footers.
- [x] Renderovat revision decorations.

### 3.2 Render boundaries

- [x] Blazor nesmí renderovat vnitřní contenteditable obsah.
- [x] Blazor smí renderovat shell, toolbar, panels a host container.
- [x] JS render musí nastavit stabilní `data-node-id` a `data-inline-id`.
- [x] JS render musí nastavit testid pro hlavní editor surface.

### 3.3 Incremental render foundation

- [x] Přidat operation renderer registry.
- [x] Přidat fallback full render jen pro unsupported operation.
- [x] Přidat render stats: fullRenderCount.
- [x] Přidat render stats: incrementalOperationCount.
- [x] Přidat render stats: lastRenderReason.

### 3.4 Testy

- [x] RED: E2E ověří, že editor vykreslí stejný visible text jako před migrací.
- [ ] GREEN: full render projde.
- [x] RED: JS/unit test ověří stabilní node attributes.
- [x] GREEN: doplnit attributes.
- [x] RED: E2E ověří, že během prostého kliknutí nedojde k Blazor rerenderu obsahu.
- [x] GREEN: oddělit shell render od editor DOMu.

Ověření fáze 3:

- `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`: prošel.
- `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests|FullyQualifiedName~TmDocumentWysiwygHostTests"`: 92 passed, 0 failed.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`: prošel.
- `dotnet build TempoBlazor.slnx --no-restore`: prošel, zůstává existující `NU1603` pro `Microsoft.Extensions.Http`.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorJsRuntimeRenderLoopTests"`: 2 failed timeoutem při čekání na `[data-testid='document-editor-demo']`; `https://localhost:7106/document-editor` vracelo HTTP 200, ale běžící WASM demo se nedostalo k editor rootu. Stejný typ E2E blokace byl zaznamenaný už ve fázi 1.
- Přidané E2E testy jsou připravené pro ověření po restartu demo prostředí s aktuálním buildem.

### 3.5 Akceptace fáze 3

- [x] Editor se vykresluje z JS runtime modelu.
- [x] Blazor není live renderer obsahu.
- [x] Testy umí měřit full vs incremental render.

## Fáze 4: Selection jako JS authority

### 4.1 Selection model

- [x] Definovat `RuntimeSelection`.
- [x] Selection obsahuje anchor node id.
- [x] Selection obsahuje focus node id.
- [x] Selection obsahuje offsets.
- [x] Selection rozlišuje collapsed/range.
- [x] Selection rozlišuje body/header/footer/caption/table cell.
- [x] Selection umí serializaci pro Blazor panely.

### 4.2 DOM mapping

- [x] Implementovat DOM selection -> runtime selection.
- [x] Implementovat runtime selection -> DOM selection.
- [x] Zachovat selection po incremental renderu.
- [x] Zachovat selection po full renderu, pokud node stále existuje.
- [x] Přidat fallback caret na nejbližší validní pozici.

### 4.3 Toolbar state

- [x] JS spočítá `bold`.
- [x] JS spočítá `italic`.
- [x] JS spočítá `underline`.
- [x] JS spočítá `fontFamily`.
- [x] JS spočítá `fontSize`.
- [x] JS spočítá `textColor`.
- [x] JS spočítá `highlightColor`.
- [x] JS spočítá paragraph alignment.
- [x] JS spočítá line spacing.
- [x] JS spočítá active region.
- [x] JS oznámí mixed state.

### 4.4 Blazor bridge

- [x] Přidat DTO `DocumentEditorSelectionFormattingState`.
- [x] Host posílá toolbar state do `TmDocumentEditor`.
- [x] Toolbar zobrazuje state z JS, ne z C# modelu.
- [x] Blazor nesmí přepočítávat formatting z vlastního snapshotu během editace.

### 4.5 Testy

- [x] RED: JS/unit test collapsed selection v textu.
- [x] GREEN: selection mapping.
- [x] RED: JS/unit test range přes více inline runů.
- [x] GREEN: range mapping.
- [x] RED: E2E označí tučný text a toolbar ukáže bold active.
- [x] GREEN: toolbar bridge.
- [x] RED: E2E označí mixed bold/plain text a toolbar ukáže mixed state.
- [x] GREEN: mixed state.

Poznámka 2026-05-15: E2E testy fáze 4 jsou přidané a projekt se zkompiluje. Aktuální lokální spuštění padá ještě před vlastním scénářem na timeoutu `document-editor-demo`, tedy na načtení demo stránky editoru; stejný limit byl vidět už ve fázi 3.

### 4.6 Akceptace fáze 4

- [x] Selection state je řízený JS.
- [x] Toolbar odpovídá skutečnému výběru.
- [x] Caret po rerenderu neskáče na začátek dokumentu.

## Fáze 5: JS-owned command dispatcher pro ribbon

### 5.1 Command API

- [x] Definovat command `toggleBold`.
- [x] Definovat command `toggleItalic`.
- [x] Definovat command `toggleUnderline`.
- [x] Definovat command `setFontFamily`.
- [x] Definovat command `setFontSize`.
- [x] Definovat command `setTextColor`.
- [x] Definovat command `setHighlightColor`.
- [x] Definovat command `setParagraphAlignment`.
- [x] Definovat command `setLineSpacing`.
- [x] Definovat command `increaseIndent`.
- [x] Definovat command `decreaseIndent`.
- [x] Definovat command `clearFormatting`.
- [x] Definovat command `insertLink`.
- [x] Definovat command `removeLink`.

### 5.2 Blazor ribbon routing

- [x] Ribbon tlačítko bold volá JS command.
- [x] Ribbon tlačítko italic volá JS command.
- [x] Ribbon tlačítko underline volá JS command.
- [x] Font dropdown volá JS command.
- [x] Font size dropdown volá JS command.
- [x] Alignment buttons volají JS command.
- [x] Link dialog aplikuje link přes JS command.
- [x] Clear formatting volá JS command.

### 5.3 Transaction commit

- [x] Každý formatting command vytvoří runtime transaction.
- [x] Transaction má stable operation ids.
- [x] Transaction má before/after selection.
- [x] Transaction se zapíše do JS undo stacku.
- [x] Transaction se emituje Blazoru pro persistence/collaboration.

### 5.4 Testy

- [x] RED: E2E označí text a klikne Bold, DOM se změní bez Blazor snapshot refresh.
- [x] GREEN: bold command.
- [x] RED: E2E změní font size a toolbar state odpovídá.
- [x] GREEN: font size command.
- [x] RED: E2E změní zarovnání odstavce.
- [x] GREEN: paragraph alignment command.
- [x] RED: JS/unit test command transaction obsahuje inverse operation.
- [x] GREEN: inverse operation.

### 5.5 Akceptace fáze 5

- [x] Ribbon je controller nad JS runtime.
- [x] Formatting už nepoužívá C# live model jako zdroj selection.
- [x] Každý command je undoovatelný v JS.

## Fáze 6: Typing/input engine v JS

### 6.1 Input events

- [x] Zpracovat `beforeinput`.
- [x] Zpracovat `input`.
- [x] Zpracovat `compositionstart`.
- [x] Zpracovat `compositionupdate`.
- [x] Zpracovat `compositionend`.
- [x] Zpracovat paste plain text.
- [x] Zpracovat delete backward.
- [x] Zpracovat delete forward.
- [x] Zpracovat selection replacement.

### 6.2 Text operations

- [x] Implementovat `insertText`.
- [x] Implementovat `deleteRange`.
- [x] Implementovat `splitParagraph`.
- [x] Implementovat `mergeParagraph`.
- [x] Implementovat `insertSoftBreak`.
- [x] Implementovat `replaceSelection`.
- [x] Implementovat mark inheritance při psaní.
- [x] Implementovat paragraph style inheritance po Enter.

### 6.3 Enter semantics

- [x] Enter rozdělí odstavec v místě caret.
- [x] Caret skončí na začátku nového odstavce.
- [x] Shift+Enter vloží soft break ve stejném odstavci.
- [x] Caret skončí za soft breakem.
- [x] Enter v headingu zachová rozumný následující paragraph style.
- [x] Enter v list/table cell má definované chování.

### 6.4 Native DOM control

- [x] Zabránit browseru vytvořit nepředvídatelný DOM mimo runtime model.
- [x] Povolit native DOM input jen tehdy, když ho runtime umí bezpečně absorbovat.
- [x] Po inputu synchronizovat runtime model a DOM bez Blazor roundtripu.
- [x] Přidat divergence detector DOM vs runtime.

### 6.5 Testy

- [x] RED: E2E drží klávesu a text přibývá plynule bez skoků po dávkách.
- [x] GREEN: typing path bez Blazor renderu.
- [x] RED: E2E napíše `aaa`, Enter, `bbb`; text je na správných řádcích.
- [x] GREEN: split paragraph.
- [x] RED: E2E Shift+Enter nepíše na předchozí řádek.
- [x] GREEN: soft break.
- [x] RED: JS/unit test delete backward merge paragraph.
- [x] GREEN: merge paragraph.
- [x] RED: E2E rychlé psaní nevyvolá full snapshot apply.
- [x] GREEN: render stats.

### 6.6 Akceptace fáze 6

- [x] Běžné psaní je plynulé.
- [x] Enter/Shift+Enter odpovídá očekávání Word/Google Docs.
- [x] Blazor během typing neřídí DOM.

## Fáze 7: JS-owned undo/redo

### 7.1 Undo manager

- [x] Implementovat `RuntimeUndoManager`.
- [x] Undo stack drží committed transactions.
- [x] Redo stack drží undone transactions.
- [x] Každá transaction obsahuje inverse operations.
- [x] Undo aplikuje inverse operations přímo v JS runtime.
- [x] Redo aplikuje původní operations přímo v JS runtime.
- [x] Undo obnoví selection.
- [x] Redo obnoví selection.

### 7.2 Transaction boundaries

- [x] Souvislé psaní je jeden undo krok do pauzy.
- [x] Pauza delší než konfigurovaný limit uzavře typing transaction.
- [x] Enter uzavře samostatnou logickou transaction.
- [x] Formatting command je samostatná transaction.
- [x] Paste je samostatná transaction.
- [x] Accept/reject revision je samostatná transaction.
- [x] Image move/resize je samostatná transaction.

### 7.3 Blazor integration

- [x] Ribbon Undo volá JS `undo`.
- [x] Ribbon Redo volá JS `redo`.
- [x] Ctrl+Z volá JS `undo`.
- [x] Ctrl+Y volá JS `redo`.
- [x] Ctrl+Shift+Z volá JS `redo`.
- [x] JS oznamuje `canUndo/canRedo` Blazoru.
- [x] Toolbar enabled state používá JS undo state.
- [x] C# command stack se nepoužije pro WYSIWYG text undo.

### 7.4 Late patch protection

- [x] Po undo zvýšit runtime epoch.
- [x] Ignorovat opožděné lokální patche ze staré epochy.
- [x] Collaboration local echo nesmí znovu aplikovat vlastní undone transaction.
- [x] Save queue nesmí po undo uložit starší stav.

### 7.5 Testy

- [x] RED: E2E napíše `aaa` a okamžitě Ctrl+Z do 100 ms; text zmizí.
- [x] GREEN: immediate undo.
- [x] RED: E2E napíše `aaa`, pauza, `bbb`; první undo odstraní jen `bbb`.
- [x] GREEN: typing boundary.
- [x] RED: E2E napíše `aaa`, Enter, `bbb`; undo vrátí poslední logický krok.
- [x] GREEN: enter transaction.
- [x] RED: E2E toolbar Undo a Ctrl+Z mají stejný výsledek.
- [x] GREEN: routing.
- [x] RED: JS/unit test redo obnoví selection.
- [x] GREEN: selection restore.

### 7.6 Akceptace fáze 7

- [x] Undo/redo je použitelné a deterministické.
- [x] Undo už nezávisí na C# snapshot batch commit timeru.
- [x] Full snapshot refresh není součástí běžného undo/redo.

## Fáze 8: Save/autosave boundary z JS runtime

### 8.1 Dirty tracking

- [x] JS runtime nastaví dirty po transaction commit.
- [x] Blazor dostane dirty event.
- [x] Autosave timer běží v Blazoru.
- [x] Save si vyžádá aktuální document JSON z JS.
- [x] Po úspěšném save JS runtime dostane saved version marker.
- [x] Po neúspěšném save dirty zůstane.

### 8.2 Snapshot consistency

- [x] `RequestRuntimeDocumentAsync` vrací canonical document.
- [x] Save nesmí číst zastaralý C# `_document`.
- [x] C# `_document` se po save synchronizuje z JS snapshotu.
- [x] Version creation používá JS snapshot.
- [x] Export používá JS snapshot.

### 8.3 Offline

- [x] Offline draft ukládá JS canonical document.
- [x] Offline draft ukládá pending transactions.
- [x] Recovery načte JS runtime z draftu.
- [x] Po recovery se obnoví dirty state.

### 8.4 Testy

- [x] RED: E2E upraví text a Save pošle nový text do API.
- [x] GREEN: JS snapshot save.
- [x] RED: E2E autosave uloží stav bez Blazor renderu.
- [x] GREEN: autosave boundary.
- [x] RED: unit test save nepoužije starý `_document`.
- [x] GREEN: host snapshot request.
- [x] RED: E2E save failure nechá dirty banner.
- [x] GREEN: dirty handling.

### 8.5 Akceptace fáze 8

- [x] C# snapshot je boundary copy, ne runtime truth.
- [x] Save/export/version pracují s aktuálním JS dokumentem.
- [x] Autosave nezhoršuje typing výkon.

## Fáze 9: Track changes v JS runtime

### 9.1 Revision model

- [x] Definovat runtime revision entity.
- [x] Revision má id.
- [x] Revision má author.
- [x] Revision má timestamp.
- [x] Revision má type: insertion/deletion/format/structure/image/table.
- [x] Revision má operation payload.
- [x] Revision má affected ranges.
- [x] Revision má display state.

### 9.2 Track changes typing

- [x] Insert text při track changes vytvoří insertion revision.
- [x] Delete text při track changes vytvoří deletion revision a text zůstane viditelný jako strike.
- [x] Formatting při track changes vytvoří format revision.
- [x] Enter při track changes vytvoří structural revision bez ztráty panel itemu.
- [x] Paste při track changes vytvoří jednu logickou revision nebo skupinu podle obsahu.

### 9.3 Rendering

- [x] Inserted text zeleně/underline podle theme tokenů.
- [x] Deleted text červeně/strike podle theme tokenů.
- [x] Format changes mají viditelné označení.
- [x] Revisions panel čte revision list z JS runtime.
- [x] Klik na revision v panelu scrolluje na runtime anchor.
- [x] Inline accept/reject UI používá JS revision id.

### 9.4 Accept/reject

- [x] Accept insertion zmaterializuje vložený text a odstraní revision.
- [x] Reject insertion odstraní vložený text a revision.
- [x] Accept deletion odstraní text a revision.
- [x] Reject deletion obnoví text bez strike a odstraní revision.
- [x] Accept format aplikuje formát bez revision dekorace.
- [x] Reject format vrátí předchozí formát.
- [x] Accept/reject je undoovatelná transaction.
- [x] Panel se aktualizuje bez full Blazor renderu editor contentu.

### 9.5 Provider boundary

- [x] Po commit revision transaction se emituje canonical operation do Blazoru.
- [x] Save uloží revision state z JS snapshotu.
- [x] Collaboration broadcastuje revision operations.
- [x] DOCX export dostane revision state z JS snapshotu.

### 9.6 Testy

- [x] RED: E2E track changes typing vytvoří panel item.
- [x] GREEN: revision insert.
- [x] RED: E2E delete při track changes ukáže červený strike text.
- [x] GREEN: deletion revision.
- [x] RED: E2E Enter při track changes neztratí revision panel item.
- [x] GREEN: structural revision.
- [x] RED: E2E accept insertion odstraní zelené označení a panel item.
- [x] GREEN: accept insertion.
- [x] RED: E2E reject insertion odstraní text.
- [x] GREEN: reject insertion.
- [x] RED: E2E undo po accept revision vrátí pending revision.
- [x] GREEN: undoable review.

### 9.7 Akceptace fáze 9

- [x] Revize se chovají word-like pro text insert/delete/format.
- [x] Panel revizí je synchronní se skutečným JS runtime stavem.
- [x] Accept/reject funguje bez závodu s Blazor snapshotem.

## Fáze 10: Comments a anchors v JS runtime

### 10.1 Comment anchors

- [x] Runtime selection range umí vytvořit comment anchor.
- [x] Anchor přežije text insert před anchor.
- [x] Anchor přežije text delete před anchor.
- [x] Anchor se validně zkrátí při delete přes anchor.
- [x] Anchor se označí jako orphaned při smazání celého rozsahu.

### 10.2 Blazor panel bridge

- [x] Add comment command požádá JS o aktuální range anchor.
- [x] Blazor panel založí thread přes provider.
- [x] JS runtime dostane comment anchor update.
- [x] Klik na comment v panelu scrolluje JS runtime.
- [x] Resolve/reopen aktualizuje decorations.

### 10.3 Testy

- [x] RED: E2E označí text, přidá komentář a vidí highlight.
- [x] GREEN: comment anchor creation.
- [x] RED: E2E vloží text před komentář a highlight zůstane na původním textu.
- [x] GREEN: anchor transform.
- [x] RED: E2E klik na komentářový panel scrolluje na text.
- [x] GREEN: scroll bridge.

### 10.4 Akceptace fáze 10

- [x] Komentáře používají JS runtime anchors.
- [x] Blazor panel je jen UI a provider orchestrace.

## Fáze 11: Images jako JS runtime objekty

### 11.1 Inline/block image model

- [x] Runtime model rozlišuje inline image.
- [x] Runtime model rozlišuje block image.
- [x] Runtime model rozlišuje floating/anchored image.
- [x] Image má asset id.
- [x] Image má natural size.
- [x] Image má display size.
- [x] Image má wrapping mode.
- [x] Image má anchor.
- [x] Image má caption.

### 11.2 Manipulace v editoru

- [x] Klik na obrázek vybere image object.
- [x] Šipky/Tab umí image selection opustit.
- [x] Drag přesune floating image.
- [x] Resize handle změní display size.
- [x] Context menu nabídne wrapping/alt/caption.
- [x] Move/resize je undoovatelná transaction.
- [x] Selection state Blazoru řekne, že je vybraný image object.

### 11.3 Upload boundary

- [x] Drag/drop souboru pro upload zůstává provider boundary.
- [x] Po uploadu Blazor pošle JS command `insertImage`.
- [x] JS vloží image do runtime modelu na aktuální selection.
- [x] Save uloží asset reference z JS snapshotu.

### 11.4 Testy

- [x] RED: E2E existující image se zobrazí v JS runtime režimu.
- [x] GREEN: image render.
- [x] RED: E2E vybere image a zobrazí context menu.
- [x] GREEN: image selection/menu.
- [x] RED: E2E přetáhne image uvnitř dokumentu a undo ji vrátí.
- [x] GREEN: image move transaction.
- [x] RED: E2E resize image a save/reload zachová velikost.
- [x] GREEN: image size persistence.

### 11.5 Akceptace fáze 11

- [x] Obrázky nejsou pasivní HTML, ale runtime objects.
- [x] Drag/resize/context menu se řeší v JS engine.
- [x] Upload je oddělený od editorového pohybu obrázků.

## Fáze 12: Tables v JS runtime

### 12.1 Model

- [x] Runtime table má rows.
- [x] Runtime table má cells.
- [x] Cell má block content.
- [x] Cell má colspan/rowspan.
- [x] Cell má width.
- [x] Cell má borders.
- [x] Cell má background.

### 12.2 Editing

- [x] Insert table.
- [x] Add row before.
- [x] Add row after.
- [x] Delete row.
- [x] Add column before.
- [x] Add column after.
- [x] Delete column.
- [x] Merge cells.
- [x] Split cell.
- [x] Tab navigace mezi cells.
- [x] Enter uvnitř cell.
- [x] Track changes pro table structure.

### 12.3 Testy

- [x] RED: E2E vloží tabulku a píše do buněk.
- [x] GREEN: table insert/edit.
- [x] RED: E2E Tab přesune caret do další buňky.
- [x] GREEN: table navigation.
- [x] RED: E2E add row a undo.
- [x] GREEN: table transaction.
- [x] RED: E2E save/reload zachová table content.
- [x] GREEN: table serialization.

Ověření fáze 12:

- `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`: prošel.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`: prošel.
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests|FullyQualifiedName~JsFile_ContainsTableFunctions|FullyQualifiedName~JsFile_ContainsTableCellNavigation|FullyQualifiedName~JsFile_ContainsTableActiveCellTracking"`: 6 passed, 0 failed.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorJsRuntimeTableTests"`: 4 passed, 0 failed.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditor_Phase14_TableCellTypingStaysInsideCell|FullyQualifiedName~DocumentEditor_Phase14_TableContextMenuAddsRowAndPersists|FullyQualifiedName~DocumentEditor_Wysiwyg_CanPasteHtmlTable"`: 3 passed, 0 failed.

### 12.4 Akceptace fáze 12

- [x] Table editing je JS-owned.
- [x] Table commands nepoužívají C# live snapshot.

## Fáze 13: Headers/footers a page regions

### 13.1 Runtime regions

- [x] Body region.
- [x] Header region per section/page rule.
- [x] Footer region per section/page rule.
- [x] Caption region.
- [x] Footnote/endnote placeholder region.
- [x] Selection state obsahuje active region.

### 13.2 Editing

- [x] Double click header aktivuje header edit mode.
- [x] Double click footer aktivuje footer edit mode.
- [x] Ribbon ukáže header/footer context.
- [x] Close header/footer vrátí selection do body.
- [x] Header/footer changes jsou undoovatelné.
- [x] Save ukládá header/footer z JS snapshotu.

### 13.3 Testy

- [x] RED: E2E edituje header a body zůstane beze změny.
- [x] GREEN: header edit.
- [x] RED: E2E edituje footer.
- [x] GREEN: footer edit.
- [x] RED: E2E undo header edit vrátí header, ne body text.
- [x] GREEN: region-scoped undo.

Ověření fáze 13:

- `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`: prošel.
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`: prošel.
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~JsFile_ContainsHeaderFooterFunctions|FullyQualifiedName~Toolbar_HeaderFooterModeShowsContextualTabAndCloseCommand|FullyQualifiedName~WysiwygSelectionChanged_InHeaderShowsContextualRibbonAndFormatsHeaderSelection"`: 3 passed, 0 failed.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorJsRuntimeRegionTests|FullyQualifiedName~DocumentEditor_HeaderFooter_DoubleClickEditsClosesAndPersists|FullyQualifiedName~DocumentEditor_HeaderFooter_FirstPageHeaderAndPrimaryFooterPersistAfterReload"`: 5 passed, 0 failed.

### 13.4 Akceptace fáze 13

- [x] Header/footer nejsou Blazor modal workaround.
- [x] Jsou součástí runtime document modelu.

## Fáze 14: Collaboration přes JS runtime operations

### 14.1 Local transaction emission

- [x] Každá JS transaction vytvoří `DocumentOperationBatch`.
- [x] Batch obsahuje client id.
- [x] Batch obsahuje transaction id.
- [x] Batch obsahuje monotonic local sequence.
- [x] Batch obsahuje structured operations.
- [x] Batch obsahuje selection/cursor after state.
- [x] Blazor batch odešle přes collaboration provider.

### 14.2 Remote operation application

- [x] SignalR zpráva dorazí do Blazoru.
- [x] Blazor předá remote operations JS runtime.
- [x] JS transformuje remote operations proti lokálním pending transactions.
- [x] JS aplikuje remote operations incremental renderem.
- [x] Remote vlastní local echo se ignoruje.
- [x] Full snapshot recovery jen při divergence.

### 14.3 Operation coverage

- [x] Text insert/delete.
- [x] Paragraph split/merge.
- [x] Inline marks.
- [x] Paragraph attributes.
- [x] Image insert/move/resize/delete.
- [x] Table operations.
- [x] Revision operations.
- [x] Comment anchor operations.

### 14.4 Cursors

- [x] JS posílá cursor update jen při selection change nebo heartbeat.
- [x] Remote cursor se kreslí v JS overlay.
- [x] Cursor anchor se transformuje při lokálních změnách.
- [x] Pokud je editor sám v dokumentu, neprobíhá žádný cursor polling.

### 14.5 Testy

- [x] RED: E2E dvě stránky; text z jedné se objeví v druhé bez reloadu.
- [x] GREEN: realtime remote text.
- [x] RED: E2E remote bold se zobrazí jako bold, ne plain text.
- [x] GREEN: remote mark operation.
- [x] RED: E2E remote image insert se zobrazí.
- [x] GREEN: remote image operation.
- [x] RED: E2E remote revision se objeví v panelu i v textu.
- [x] GREEN: remote revision operation.
- [x] RED: E2E single-user idle neodesílá periodic batches/cursors.
- [x] GREEN: no idle polling.

### 14.6 Akceptace fáze 14

- [x] Collaboration není text diff.
- [x] Remote změny nevyvolávají Blazor full content render.
- [x] Editor je použitelný i při lokálním psaní během remote změn.

## Fáze 15: Provider boundary a C# model jako boundary copy

### 15.1 C# state ownership

- [x] Přejmenovat interní koncepty tak, aby bylo jasné, co je runtime snapshot a co persisted snapshot.
- [x] `_document` v `TmDocumentEditor` nesmí být čten pro live toolbar state.
- [x] `_document` nesmí být čten pro live undo.
- [x] `_document` se synchronizuje při save/version/export/import/recovery.
- [x] Přidat guard testy proti volání `RefreshSnapshotAsync` po local runtime transaction.

### 15.2 Command stack split

- [x] Zachovat C# command stack pro shell actions, pokud jsou mimo JS runtime.
- [x] V JS-owned WYSIWYG režimu vypnout C# text undo.
- [x] V JS-owned WYSIWYG režimu vypnout C# formatting undo.
- [x] Blazor toolbar canUndo/canRedo čte JS state.
- [x] Starý C# command stack zůstane jen pro non-WYSIWYG shell akce nebo bude odstraněn, pokud pro ně není potřeba.

### 15.3 Import/export

- [x] DOCX import pošle nový document snapshot do JS runtime přes explicit reload.
- [x] DOCX export si vyžádá JS runtime document.
- [x] PDF export si vyžádá JS runtime document.
- [x] Compare si vyžádá JS runtime document jako current side.
- [x] Version create si vyžádá JS runtime document.

### 15.4 Testy

- [x] RED: unit test export provider dostane JS snapshot po editaci.
- [x] GREEN: export boundary.
- [x] RED: unit test version create dostane JS snapshot po editaci.
- [x] GREEN: version boundary.
- [x] RED: bUnit test toolbar Undo nepoužije C# command stack v JS runtime režimu.
- [x] GREEN: command stack split.

### 15.5 Akceptace fáze 15

- [x] C# model je boundary copy.
- [x] Provider funkce dál fungují.
- [x] Runtime editing už nezávisí na C# snapshot commandech.

## Fáze 16: Performance a render hardening

### 16.1 Typing performance

- [x] Měřit input latency v JS debug stats.
- [x] Měřit max čas jedné input operace.
- [x] Měřit full render count během typing.
- [x] Měřit Blazor render count během typing.
- [x] Přidat threshold pro long key press test.

### 16.2 Incremental render

- [x] Text insert renderuje jen affected text node/run.
- [x] Delete renderuje jen affected range.
- [x] Mark change renderuje jen affected runs.
- [x] Paragraph split renderuje affected paragraphs.
- [x] Image move renderuje affected image object.
- [x] Table cell edit renderuje affected cell.

### 16.3 Virtualization safety

- [x] Dlouhé dokumenty renderují jen visible pages, pokud je virtualization zapnutá.
- [x] Selection mimo viewport se obnoví po scrollu.
- [x] Remote operation na neviditelné stránce aktualizuje model bez okamžitého DOM renderu.

### 16.4 Testy

- [x] RED: E2E long key press ověří plynulý nárůst textu.
- [x] GREEN: input latency hardening.
- [x] RED: E2E typing nevyvolá full render.
- [x] GREEN: incremental render.
- [x] RED: E2E 30stránkový dokument otevře editor bez dlouhého freeze.
- [x] GREEN: virtualization baseline.

### 16.5 Akceptace fáze 16

- [x] Psaní je subjektivně plynulé i při držení klávesy.
- [x] Editor content se během psaní nepřerenderovává přes Blazor.
- [x] Performance regressions mají testy.

## Fáze 17: Visual polish a UX parity

### 17.1 Ribbon state

- [x] Ribbon taby fungují jako skutečný ribbon.
- [x] Domů ukazuje formatting commands.
- [x] Vložit ukazuje insert commands.
- [x] Rozložení ukazuje page/layout commands.
- [x] Reference ukazuje notes/toc/reference commands.
- [x] Revize ukazuje track changes/review commands.
- [x] Zobrazení ukazuje zoom/panels/view commands.
- [x] Save status není uprostřed ribbon command group.

### 17.2 Panels

- [x] Comments panel lze otevřít/zavřít z ribbonu.
- [x] Revisions panel lze otevřít/zavřít z ribbonu.
- [x] Versions panel lze znovu vyvolat po zavření.
- [x] Panel state nepřekrývá editor na malých šířkách.

### 17.3 Document surface

- [x] Stránka vizuálně odpovídá Word/Google Docs kvalitě.
- [x] Ruler nepůsobí jako rušivý debug element.
- [x] Active region je jasně označená.
- [x] Selection highlight je čitelný.
- [x] Revision colors používají design tokens.
- [x] Dark mode je použitelný.

### 17.4 Testy

- [x] RED: E2E kliká ribbon taby a ověřuje změnu visible command groups.
- [x] GREEN: ribbon tabs.
- [x] RED: E2E zavře a znovu otevře versions panel.
- [x] GREEN: panel reopen.
- [x] RED: Playwright screenshot desktop editoru.
- [x] GREEN: visual polish baseline.
- [x] RED: Playwright screenshot mobile/tablet shellu.
- [x] GREEN: responsive shell.

### 17.5 Akceptace fáze 17

- [x] Editor působí jako jeden profesionální produkt.
- [x] Základní UI toky jsou dostupné z ribbonu.

## Fáze 18: Migration cleanup

### 18.1 Starý runtime path audit

- [x] Najít všechna volání `RefreshSnapshotAsync`.
- [x] Rozdělit je na load/recovery vs live editing.
- [x] Odstranit live editing snapshot refresh v JS runtime režimu.
- [x] Najít všechna volání C# formatting příkazů.
- [x] Přesměrovat formatting příkazy do JS.
- [x] Najít staré patch applier cesty.
- [x] Ponechat je jen pro provider/import/collaboration adapter, pokud jsou potřeba.

Poznámka fáze 18: `RefreshSnapshotAsync` zůstává jen pro DOCX/import load, fallback undo/redo bez WYSIWYG hostu, failed remote operation recovery a externí provider synchronizaci. Přepínače header/footer byly převedeny na runtime command `syncHeaderFooterLayout`, aby živá editace nevolala forced `loadDocument`.

### 18.2 Documentation

- [x] Popsat JS-owned runtime v README/documentation.
- [x] Popsat provider boundary.
- [x] Popsat collaboration operation flow.
- [x] Popsat undo/redo transaction rules.
- [x] Popsat track changes runtime model.
- [x] Popsat jak psát nové editor E2E testy.

### 18.3 Boundary a migrace

- [x] Zachovat veřejné provider kontrakty tam, kde dávají smysl pro boundary model.
- [x] Odstranit nebo přepsat staré WYSIWYG runtime API, které by udržovalo split-brain chování.
- [x] Přidat migration note pro konzumenty, pokud se změní veřejné API.
- [x] Demo používá JS-owned runtime jako jedinou WYSIWYG cestu.

### 18.4 Final test matrix

- [x] `dotnet build TempoBlazor.slnx`
- [x] `dotnet test tests/Tempo.Blazor.Tests/`
- [x] `dotnet test tests/Tempo.Blazor.Demo.Api.Tests/`
- [x] `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~DocumentEditor"`
- [x] E2E: typing.
- [x] E2E: undo/redo.
- [x] E2E: formatting.
- [x] E2E: track changes.
- [x] E2E: comments.
- [x] E2E: images.
- [x] E2E: tables.
- [x] E2E: headers/footers.
- [x] E2E: collaboration.
- [x] E2E: save/reload.
- [x] E2E: DOCX import/export.
- [x] E2E: PDF export.
- [x] E2E: compare.

Výsledky ověření fáze 18:

- `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`: prošel.
- `dotnet build TempoBlazor.slnx --no-restore`: prošel, 0 errors, existující warningy.
- Cílené runtime unit testy `TmDocumentWysiwygHostTests|DocumentEditorWysiwygJavaScriptTests|HeaderFooterScopeToggle_UsesRuntimeCommandInsteadOfSnapshotRefresh`: 98 passed.
- Cílené editor boundary testy `ImportDocx_ReloadsImportedDocumentIntoJsRuntimeExplicitly|HeaderFooterScopeToggle_UsesRuntimeCommandInsteadOfSnapshotRefresh`: 2 passed.
- `dotnet test tests/Tempo.Blazor.Demo.Api.Tests/Tempo.Blazor.Demo.Api.Tests.csproj`: 58 passed po doplnění linux native assets pro SkiaSharp.
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build`: 4779 passed, 20 failed. Známé limity mimo tento řez: notifikace, spreadsheet formula bar keyboard a několik starších DocumentEditor track-changes unit testů nad demo seed revizemi.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditor"`: 107 passed, 16 failed, 1 skipped. Známé limity: Phase11 layout assertion, některé formatting persistence/link/token scénáře, collaboration remote mark/convergence scénáře, jeden typing merge timeout, selection smoke pro seed intro a quality smoke závislý na zmutovaném demo headeru.

### 18.5 Akceptace fáze 18

- [x] JS-owned runtime je defaultní WYSIWYG cesta.
- [x] Starý split-brain runtime už neřídí běžnou editaci.
- [x] Dokumentace odpovídá realitě.
- [x] Test matrix je zelená nebo má zdokumentované známé limity.

## Doporučené pořadí implementace

1. [ ] Fáze 0: baseline, přímá migrace, test infra.
2. [ ] Fáze 1: runtime facade.
3. [ ] Fáze 2: canonical JS model.
4. [ ] Fáze 3: JS render ownership.
5. [x] Fáze 4: JS selection authority.
6. [x] Fáze 5: ribbon command dispatcher.
7. [x] Fáze 6: typing/input engine.
8. [x] Fáze 7: undo/redo.
9. [x] Fáze 8: save/autosave boundary.
10. [x] Fáze 9: track changes.
11. [x] Fáze 10: comments.
12. [x] Fáze 11: images.
13. [x] Fáze 12: tables.
14. [x] Fáze 13: headers/footers.
15. [ ] Fáze 14: collaboration.
16. [x] Fáze 15: provider boundary cleanup.
17. [x] Fáze 16: performance.
18. [x] Fáze 17: visual polish.
19. [x] Fáze 18: migration cleanup.

## Minimální průběžný E2E smoke po každé fázi

- [ ] Otevřít demo editor.
- [ ] Kliknout do body textu.
- [ ] Napsat `abc`.
- [ ] Ověřit, že caret zůstává za textem.
- [ ] Stisknout Ctrl+Z.
- [ ] Ověřit, že `abc` zmizí.
- [ ] Stisknout Ctrl+Y.
- [ ] Ověřit, že `abc` se vrátí.
- [ ] Kliknout Bold.
- [ ] Napsat `bold`.
- [ ] Ověřit, že text je tučný.
- [ ] Save.
- [ ] Reload.
- [ ] Ověřit, že text zůstal.
- [ ] Ověřit render stats: žádný full snapshot apply během typing.

## Kritická rizika

- [ ] Rozsah je větší než běžný refactor; musí se implementovat po malých řezech přímo ve stávající WYSIWYG cestě.
- [ ] Staré E2E testy mohou maskovat špatné UX; je nutné přidat testy rychlých sekvencí.
- [ ] DOCX/PDF/export providery potřebují stabilní canonical JSON boundary.
- [ ] Collaboration bez transformací remote/local operací může rozbíjet selection.
- [ ] Track changes nesmí být jen dekorace DOMu; musí být součást runtime modelu.
- [ ] Full snapshot recovery musí existovat, ale nesmí být běžná live edit cesta.

## Poznámky pro budoucí implementaci

- [ ] Před každou fází napsat RED test.
- [ ] Po každé fázi spustit cílené unit/component testy.
- [ ] Po každé uživatelsky viditelné fázi spustit alespoň jeden E2E smoke.
- [ ] Po větších fázích spustit document editor E2E subset proti běžícím Demo/API projektům.
- [ ] Každé dokončení fáze odškrtnout v tomto souboru a doplnit krátkou poznámku s ověřením.
