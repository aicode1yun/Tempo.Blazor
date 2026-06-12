# TmDocumentEditor Canvas engine - E8: Matematika / rovnice (detailní TDD + E2E)

Datum založení: 2026-06-04
Nadřazený plán: `planning/tmdocumenteditor-canvas-onlyoffice-inspired-engine-tdd-todo-2026-06-04.md`, fáze **E8**
Stav: produkční E8 řez dokončen pro clean-room canvas equation editor: model/layout/render, slotové editace, strukturální selection, keyboard/IME, equation dropdown galerie, samostatný Math ribbon tab, symboly, inline/display režim, undo/redo, save/reload, DOCX OMML smoke, a11y mirror/live region, parity řádek a Playwright screenshot suite.
Priorita: P1 (rozšířená parita nad rámec legacy; cíl Word / Google Docs / OnlyOffice)

## Proč tento TODO existuje

Editor rovnic je samostatná velká subdoména (v OnlyOffice `word/Math/` + `word/Editor/Math.js` + `MathChanges.js` mají ~200 KB; vlastní layout/typesetting engine). Legacy `TmDocumentEditor` rovnice neuměl. Word, Google Docs i OnlyOffice mají plný equation editor (struktury zlomek/odmocnina/index/suma/matice, šablony, symbolová paleta, navigace mezi sloty). Bez něj nelze tvrdit „kvalita jako Word/GDocs/OnlyOffice".

## Licence a clean-room pravidla

ONLYOFFICE (`/home/pavel/NetProjects/onlyfficeservergit`) je AGPL.

- [x] Nekopírovat zdrojový kód, názvy interních tříd ani algoritmy z `word/Math/*` (`fraction.js`, `radical.js`, `degree.js`, `nary.js`, `matrix.js`, `accent.js`, `limit.js`, `mathContent.js`, `operators.js`, `LaTeXParser.js`, `UnicodeParser.js`, `math-ml.js`).
- [x] Math layout (script shift, fraction gap, radical, nary limity) odvodit z veřejných standardů (OMML/MathML, principy TeX/Unicode math, OpenType MATH tabulka kde dostupná) a vlastních testů, ne z ONLYOFFICE kódu.
- [x] Do PR poznámky napsat: "ONLYOFFICE byl použit pouze jako clean-room architektonická inspirace; kód nebyl kopírován."

## Cílový stav

- Uživatel vloží inline nebo display rovnici z toolbaru (Equation gallery + symbol paleta).
- Rovnice se vykreslí na canvas jako profesionální matematická sazba: správné baseline, velikosti indexů, mezery operátorů, roztažitelné závorky/odmocniny.
- Uživatel edituje rovnici: klikne dovnitř, navigace mezi sloty (šipky/Tab), vloží šablonu (zlomek, odmocnina, index, suma, matice), píše do slotů.
- Backspace/Delete v math slotech mažou struktury logicky.
- Caret a selection fungují uvnitř rovnice.
- Lineární vstup (`a/b`, `x^2`, `\alpha`) se převede na strukturu (basic build-up; pokročilý auto-buildup P2).
- Save/reload a DOCX (OMML) roundtrip zachovají rovnice.
- A11y: rovnice má mluvený popis / MathML mirror.

## Znovupoužití stávající infrastruktury

- [x] `layout/font-metrics.mjs` - measureText pro math glyphy; rozšířit o math scale levely (display / text / script / scriptscript).
- [x] `input/input-controller.mjs` - skrytý input bridge + IME/beforeinput routing pro psaní do math slotů.
- [x] `selection/selection-controller.mjs` - caret/selection overlay pro aktivní math slot (`document-canvas-math-caret`, `document-canvas-math-selection-rect`).
- [x] `core-engine/operations.mjs`, `undo-stack.mjs` - math edity jako transakce.
- [x] `CoreEngineModelConverter` (C#) - rozšířit o math objekt.
- [x] Math objekt je inline run (master fáze 4 model parity „drawing run"-like), layoutuje se uvnitř řádky jako jeden box.

## Doporučené nové testovací soubory

```text
tests/Tempo.Blazor.Tests/DocumentEditor/CanvasEngine/Math/MathModelTests.cs
tests/Tempo.Blazor.Tests/DocumentEditor/CanvasEngine/Math/MathConverterTests.cs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/math/__tests__/math-tree.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/math/__tests__/math-layout.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/math/__tests__/math-render.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/math/__tests__/math-caret.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/math/__tests__/math-input.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/math/__tests__/linear-parser.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasMathE2ETests.cs
```

## Definice hotovo pro každou E8 fázi

Dědí z master plánu + navíc:

- [x] RED test vznikl před implementací (JS unit / C# / E2E).
- [x] Layout testy ověřují metriky (baseline, výšky, mezery) deterministicky, ne jen "nespadlo".
- [x] Každá viditelná změna má screenshot before/after, `AssertCanvasNonBlankAsync`, `AssertNoTextOverlapAsync`.
- [x] Save/reload gate pro podporovaný E8 math element rozsah; DOCX OMML smoke kde provider podporuje.
- [x] Undo/redo gate pro podporované E8 math edit commandy; view-only selection/exit commandy nemutují historii záměrně.
- [x] `dotnet build` zelený, existující testy se neoslabují.

## Fáze E8.0: Baseline, flag a rozhodnutí

### E8.0.1 Založit E2E a route
- [x] `DocumentEditorCanvasMathE2ETests.cs` + `OpenCanvasEngineDocumentAsync`.
- [x] RED E2E: insert-equation command na canvas hostu neexistuje.

### E8.0.2 Rozhodnutí o reprezentaci
- [x] Interní math strom = OMML-like (clean-room) jako kanonická forma; MathML a LaTeX jako import/export adaptéry.
- [x] Rozsah scriptů: display/text/script/scriptscript (4 úrovně), default mapping velikostí.
- [x] Zapsat rozhodnutí do plánu.

### E8.0.3 Akceptace
- [x] RED existuje, reprezentace rozhodnuta.

## Fáze E8.1: Math model strom (OMML-like)

### E8.1.1 RED model testy
- [x] `math-tree.test.mjs` + `MathModelTests`: math objekt = `mathPara` (inline|display) -> `mathContent` (sekvence elementů). Element typy:
  - [x] `run` / `text` (math run s glyfy, normal/italic math style),
  - [x] `fraction` (num, den; bar/skewed/linear/no-bar),
  - [x] `radical` (degree?, radicand),
  - [x] `sup` / `sub` / `subSup` (base + indexy),
  - [x] `preSubSup` (indexy vlevo),
  - [x] `nary` (operátor sum/prod/integral, lowLimit, uppLimit, base),
  - [x] `delimiter` (open/close char, separators, content slots),
  - [x] `function` (fName, base),
  - [x] `accent` (char nad bází),
  - [x] `bar` (over/under),
  - [x] `groupChar` (brace nad/pod),
  - [x] `limit` (lim nad/pod),
  - [x] `matrix` (rows x cols, sloty, zarovnání),
  - [x] `box` / `borderBox`.

### E8.1.2 Implementovat model + factory
- [x] Immutable-ish math tree + factory pro každý typ; sloty drží `mathContent`.
- [x] Preserve channel pro neznámé OMML elementy.

### E8.1.3 Akceptace
- [x] Všechny element typy se postaví, mají sloty, projdou validací; unit zelené.
- [x] E8.1 první řez: `DocumentMathRun`/`DocumentMathContent`/`DocumentMathElement`, canvas `CanvasMathRun`, JS `math-model.mjs`, model normalizace zachovávající `type: "math"`, unit testy zelené pro podporovanou sadu.

## Fáze E8.2: Converter a formátové adaptéry

### E8.2.1 RED converter testy
- [x] `MathConverterTests`: `DocumentEditorDocument` math run <-> canvas math tree round-trip pro podporovanou E8 první sadu (fraction, radical, sup/sub, nary, matrix).
- [x] MathML import/export adaptér (basic: fraction, sup/sub, radical, nary, matrix).
- [x] OMML mapping pro DOCX provider boundary (smoke).

### E8.2.2 Implementovat + akceptace
- [x] Convertery + adaptéry; preserve-channel pro neznámé.
- [x] Round-trip bez ztráty struktury.
- [x] E8.2 první řez: `CanvasDocumentModelConverter` převádí math run tam/zpět, `DocumentEditorJson` serializace zachová polymorfní `DocumentMathRun`, demo provider seed se načte do canvas modelu.

## Fáze E8.3: Math layout engine (typesetting)

### E8.3.1 RED layout testy
- [x] `math-layout.test.mjs`: pro podporovanou E8 první sadu deterministické metriky boxu (width, ascent, descent, baseline) a pozice slotů:
  - [x] fraction: šířka = max(num,den), bar gap, num/den vertikální posun, axis alignment,
  - [x] sup: script shift up + scale; sub: shift down; subSup: oba bez kolize,
  - [x] radical: výška = radicand + clearance, znaménko škáluje, degree vlevo nahoře,
  - [x] nary: limity nad/pod (display) vs vpravo (inline) dle nary-lim nastavení,
  - [x] matrix: zarovnání sloupců/řádků, mezery buněk, baseline řádku,
  - [x] delimiter: roztažitelné závorky podle výšky obsahu,
  - [x] accent/bar/groupChar/limit: pozice nad/pod bázi.
- [x] Math scale levely: vnořený index/zlomek zmenšuje font (text->script->scriptscript).

### E8.3.2 Implementovat layout
- [x] Rekurzivní layout: každý element spočítá svůj box z boxů slotů; reuse `font-metrics` s math scale.
- [x] Axis/baseline model (matematická osa), operator spacing třídy (clean-room z veřejné math sazby).
- [x] Inline math box se vrací jako jeden inline box do paragraph enginu (master fáze 6).

### E8.3.3 Akceptace
- [x] Layout metriky deterministické a stabilní; vnořené struktury bez kolizí; unit zelené.

## Fáze E8.4: Render rovnic na canvasu

### E8.4.1 RED render testy
- [x] `math-render.test.mjs`: display list rovnice = glyph runs + bar/line primitiva (fraction bar, radical, bar) + roztažitelné závorky; deterministický.

### E8.4.2 Implementovat math painter
- [x] Canvas paint: math glyphy přes font-metrics, fraction/radical/bar čáry, roztažitelné delimitery (assembled glyphs nebo vektorové), italic math default pro proměnné.
- [x] 1x/2x DPR ostrost, pixel snapping čar.

### E8.4.3 Screenshot E2E + UX
- [x] E2E: vložit zlomek `a/b`, odmocninu, `x^2`, sumu s limity, matici 2x2; screenshot.
- [x] UX review: rovnice vypadá jako profesionální sazba pro podporovaný E8 rozsah, ne jako prostý text.

### E8.4.4 Akceptace
- [x] Render ostrý a korektní; screenshot gate zelený.

## Fáze E8.5: Caret a selection uvnitř rovnice

### E8.5.1 RED caret testy
- [x] `math-caret.test.mjs`: math position model = cesta (element path + slot + offset). Hit-test point -> math pozice; pozice -> caret rect. Navigace šipkami přechází mezi sloty logicky (vstup do zlomku zhora/zdola, výstup ven), Home/End v rámci slotu.
- [x] E8.5 první řez runtime: `math-caret.test.mjs` kryje slot path model (`elements.*`), path -> caret rect, navigaci next/previous mezi sloty a stabilitu po přepočtu layoutu.
- [x] `math-caret.test.mjs`: hit-test bodu do slotu a slot replacement pro lineární šablonu bez rozbití okolní rovnice.
- [x] `math-caret.test.mjs`: nary hit-test preferuje výrazový slot při inline překryvu limitů a base boxu.
- [x] Selection uvnitř math slotu = rozsah; cross-slot selection vybírá celé pod-struktury (ne částečně rozbité).
- [x] Selection uvnitř aktivního math slotu má runtime rozsah a vizuální overlay.

### E8.5.2 Implementovat
- [x] Math caret/selection nad canvas selection overlay v math souřadnicích.
- [x] Caret blikání a viditelnost uvnitř rovnice.

### E8.5.3 Screenshot E2E + akceptace
- [x] E2E: klik do čitatele zlomku, šipkou dolů do jmenovatele, Shift+šipka vybere; caret viditelný.
- [x] Selection nerozbije strukturu.

## Fáze E8.6: Vstup - šablony, sloty a editace

### E8.6.1 RED input testy
- [x] `math-input.test.mjs`/dispatcher runtime coverage: vložení šablony na caret (fraction/radical/sup/sub/nary/matrix/delimiter) vloží strukturu s prázdnými sloty a umístí caret do prvního slotu.
- [x] Psaní textu do slotu; Enter v matici přidá řádek (nebo dle kontextu); Tab mezi sloty.
- [x] Backspace na začátku slotu / Delete maže strukturu logicky (např. smazání zlomku zachová obsah čitatele dle pravidla).
- [x] E8.6 runtime RED: dispatcher testy kryjí `activateMathSlot`, `moveMathSlot`, `insertMathSlotText`, `deleteMathSlotBackward`, `addMathMatrixRow`, `addMathMatrixColumn` včetně undo/redo.
- [x] `input/__tests__/insert-delete.test.mjs`: active math slot routuje `beforeinput`, `keydown`, Backspace/Delete, Tab, Enter v matici a composition/IME přes math commands.
- [x] `math-caret.test.mjs` + dispatcher test: strukturální Backspace/Delete unwrapne parent strukturu a undo/redo ji vrací.

### E8.6.2 Implementovat
- [x] Math edit commandy (insert template, insert text, delete in slot, add matrix row/col) jako transakce přes `operations`/`undo-stack`.
- [x] Napojení `input-surface` (keyboard/IME) na aktivní math slot.
- [x] E8.6 první řez: insertFraction/insertRadical/insertSuperscript/insertSubscript/insertMatrix vloží strukturovaný math run jako undoable runtime transakci.
- [x] Aktivní slot přijímá symboly a šablony jako replacement, pokud command nemá explicitní block/offset target.

### E8.6.3 Screenshot E2E + akceptace
- [x] E2E real keyboard: vložit zlomek, napsat čitatel/jmenovatel, vložit sumu, upravit; undo vrací krok po kroku.
- [x] E2E real keyboard: klik do čitatele/jmenovatele, psaní `+c`/`+d`, Backspace, undo/redo a screenshot gate.
- [x] Save/reload zachová editovanou rovnici.

## Fáze E8.7: Symbolová paleta a lineární vstup

### E8.7.1 RED symbol/linear testy
- [x] Symbol paleta: vložení math symbolu (řecká písmena, operátory, šipky, relace) na caret.
- [x] Toolbar symboly první řez: `alpha`, `beta`, `pi`, `infinity`, `plusminus`, `integral` se vkládají přes produkční canvas command do rovnice / aktivního slotu.
- [x] Toolbar/command symboly druhý řez: `gamma`, `Delta`, `theta`, `lambda`, `rightArrow`, `lessEqual`, `greaterEqual`, `notEqual` mají produkční command coverage + localized toolbar položky.
- [x] `linear-parser.test.mjs`: basic build-up `a/b`->fraction, `x^2`->sup, `x_i`->sub, `sqrt(x)`->radical, `\alpha`->α, `\sum`->nary operátor. Pokročilý auto-buildup (mezery, závorky) = P2.

### E8.7.2 Implementovat + akceptace
- [x] Symbol paleta UI (Blazor shell, math tab) + insert command.
- [x] Equation dropdown má symbolovou skupinu + insert command bez hardcoded textů.
- [x] Lineární parser pro základní vzory; undoable.
- [x] E8.7 první řez: equation toolbar vkládá `\alpha` přes produkční canvas command a toolbar undo/redo.
- [x] E2E: napsat `\alpha` + mezera -> α, `a/b` -> zlomek.

## Fáze E8.8: Insert command a equation toolbar

### E8.8.1 RED command testy
- [x] `insertEquation(inline|display)` command; toolbar Equation gallery (běžné rovnice: kvadratická, binomická, atd.) + math tab (struktury, symboly, scripts, nary, matrix, accent).
- [x] Inline vs display přepínání; zarovnání display rovnice.
- [x] E8.8 první řez: `insertEquation`/`insertMath` command existuje, podporuje inline/display payload a routuje přes canvas dispatcher.
- [x] Command/runtime routuje toolbar presety `quadratic`, `product`, `limit`, `accent`, `borderBox`, symboly a aktivní math slot replacement.

### E8.8.2 Implementovat + akceptace
- [x] Blazor shell: equation gallery dropdown + math tab; commandy routují do canvas hostu (master fáze 9/13).
- [x] E8.8 první řez UI: Insert ribbon equation dropdown routuje fraction/radical/scripts/sum/matrix/limit/accent/borderBox/alpha do canvas hostu.
- [x] E8.8 druhý řez UI: Insert ribbon equation dropdown routuje quadratic/product/beta/pi/infinity/plus-minus/integral do canvas hostu.
- [x] E2E: vložit z gallery kvadratickou rovnici, upravit koeficient; screenshot.
- [x] E2E: vložit z gallery kvadratickou rovnici a další pokročilé presety; screenshot.

## Fáze E8.9: Undo/redo, save/reload, DOCX OMML

### E8.9.1 RED roundtrip testy
- [x] Undo/redo pro insert template, insert symbol, delete struktury, add matrix row.
- [x] Undo/redo gate pro toolbar symbol, active-slot text/symbol/template replacement a slot deletion první řez.
- [x] Save/reload zachová celý math strom včetně scriptů a matic.
- [x] DOCX OMML export/import smoke (fraction, sup/sub, radical, nary, matrix).

### E8.9.2 Implementovat + akceptace
- [x] Serializace přes provider boundary.
- [x] Roundtrip zelený; undo gate zelený.
- [x] OMML adaptér z E8.2.

## Fáze E8.10: Accessibility a UX galerie

### E8.10.1 A11y
- [x] A11y mirror: rovnice má `role` + textový/MathML popis pro screen readery (např. "zlomek a lomeno b").
- [x] Live region ohlásí vstup/výstup z rovnice a aktivní slot.
- [x] E8.10 runtime první řez: live region ohlašuje aktivní math slot z command runtime (`activateMathSlot`/`moveMathSlot`) lokalizovanou hláškou.
- [x] Canvas root vystavuje aktivní math slot atributy pro asistivní runtime a E2E (`data-canvas-math-slot-*`).

### E8.10.2 UX galerie + akceptace
- [x] Screenshot galerie: inline rovnice v textu, display zlomek, suma s limity, matice, odmocnina; desktop/tablet/mobil.
- [x] E8.10 první řez: desktop screenshot gate pro otevřenou equation gallery + vložené limit/accent/borderBox/alpha struktury.
- [x] E8.10 responsive slot-editing screenshot gate: desktop/tablet/mobil screenshoty pro editovaný zlomek + matici (`responsive-slot-editing`), bez překrytí side panelem.
- [x] E8.10 toolbar gallery screenshot gate: quadratic/product/limit/alpha/gamma/arrow/relation presety s `AssertCanvasNonBlankAsync` a `AssertNoTextOverlapAsync`.
- [x] Agent UX/UI verdikt: sazba je profesionální pro podporovaný E8 rozsah, baseline sedí, indexy nečitelně malé nejsou.
- [x] Zapsat E8 řádky do parity suite (master fáze 24).

## Doporučené rozdělení (pokud fáze přeteče)

- E8.1-E8.2 = model + converter (E8a),
- E8.3-E8.4 = layout + render (E8b),
- E8.5-E8.7 = caret + input + symboly (E8c),
- E8.8-E8.10 = toolbar + perzistence + a11y (E8d).

## Průběžné poznámky

- 2026-06-05: Implementován první produkční řez E8: Abstractions DTO/model (`DocumentMathRun`, `DocumentMathContent`, `DocumentMathElement`, `CanvasMathRun`), converter + `DocumentEditorJson` roundtrip, JS math model/layout/render, canvas model normalizace pro `type: "math"`, undoable insert commands (`insertEquation`, `insertFraction`, `insertRadical`, `insertSuperscript`, `insertSubscript`, `insertMatrix`), basic linear parser a E2E seed/insert/save-reload screenshot gate. Nehotové zůstává slotové math caret/selection, editace uvnitř slotů, Blazor toolbar galerie/symbol palette UI, MathML/OMML import-export a plná a11y math mirror role.
- 2026-06-06: Doplněn E8d první řez: JS model/layout/render podporuje `preSubSup`, `accent`, `groupChar`, `limit`, `function`, `box`/`borderBox`; lineární parser pokrývá subscript, `\prod`, `\lim`, rozšířené symboly; Blazor Insert ribbon má equation dropdown pro struktury a alfa symbol bez hardcoded textů; canvas a11y mirror vytváří `role="math"` uzly s textovým popisem. Přidán E2E screenshot test `PhaseE8_EquationToolbarGalleryInsertsAdvancedMathAndAccessibleMirror` včetně toolbar undo/redo pro symbol a `AssertCanvasNonBlankAsync`/`AssertNoTextOverlapAsync`.
- 2026-06-06: Doplněn MathML adaptér `math/mathml-adapter.mjs` pro basic import/export (fraction, sup/sub/subSup, radical/root, nary, delimiter, matrix, accent/bar/borderBox), napojený do `normalizeMathRun` a produkčního `insertEquation` commandu pro payload bez předpočítaného `content`. DOCX provider boundary smoke nyní zachovává `MathML` i `OmmlXml` přes round-trip. E2E `PhaseE8_CanvasMathEquationsRenderInsertAndPersist` vkládá lineární `a/b`, symbol `\alpha` a MathML matici přes runtime, pořizuje screenshot `02-phasee8-runtime-linear-symbol-mathml.png`, ukládá a ověřuje reload. Ověřeno přes `npm run test:document-editor-modules` (239/239), cílený DOCX test a Playwright E8 (2/2).
- 2026-06-06: Doplněn slot command/runtime první řez: `math/math-caret.mjs` zavádí slot path model, slot navigation, slot text insert/delete, matrix row/column editaci a path -> caret rect výpočet. Canvas command runtime má `activateMathSlot`, `moveMathSlot`, `insertMathSlotText`, `deleteMathSlotBackward`/`Forward`, `addMathMatrixRow`, `addMathMatrixColumn`; edity jdou přes historii/undo-redo a při editaci zneplatní zastaralé `mathML`/`ommlXml`, protože zdrojem pravdy je normalizovaný math strom. Live region ohlašuje aktivní slot lokalizovaně (`TmDocumentEditor_CanvasMathSlotAnnouncement`). Přidán E2E `PhaseE8_MathSlotEditingCommandsUndoRedoLiveRegionAndResponsiveScreenshots` s desktop/tablet/mobile screenshoty v `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phasee8-math-equations/2026-06-04/responsive-slot-editing/`. Ověřeno přes `npm run test:document-editor-modules` (245/245), `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj --no-restore`, `dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore`, `dotnet build src/Tempo.Blazor.Demo.Api/Tempo.Blazor.Demo.Api.csproj --no-restore`, `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` a Playwright E8 suite (3/3).
- 2026-06-07: Doplněn E8c/E8d druhý řez: klikací hit-test math slotů přes reálný canvas layout, viditelný blikající math caret, lokální selection overlay, keyboard/IME routing do aktivních slotů, slot replacement pro symboly/lineární šablony, `\alpha ` a `a/b ` build-up uvnitř prázdných slotů, rozšířená equation gallery (`quadratic`, `product`, `beta`, `pi`, `infinity`, `plusminus`, `integral`) a root atributy pro aktivní slot. E2E nově kliká do reálného čitatele/jmenovatele z layout snapshotu, píše přes keyboard, ověřuje ArrowDown/Shift+Arrow/End/Backspace/undo-redo/save-reload a pořizuje desktop/tablet/mobile screenshoty. Ověřeno přes `node --test` cílené math/command/input testy (23/23), `npm run test:document-editor-modules` (260/260), `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`, `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorCanvasMathEquationsE2ETests"` (3/3) a `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentDocxFormatTests"` (28/28).
- 2026-06-07: Dotažen třetí řez E8 slotů: strukturální Backspace/Delete na hraně slotu unwrapne parent math strukturu se zachováním aktivního slot contentu, dispatcher má undo/redo gate pro strukturální delete a E2E rozšířilo real-keyboard cestu o vložení sumy, klik do expression slotu, editaci `+k`, undo/redo a save/reload. Opraven nary hit-test pro inline sumu, kde upper limit a base sdílí rect; tie-breaker nyní preferuje konkrétnější/pozdější slot a screenshot E2E před responsive snímky zavírá side panel. Neodškrtávám plný UX/UI verdikt ani kompletní screenshot galerii, protože na tablet/mobile je print page při 100% zoomu stále vodorovně ořezaná a plná Word-level sazba ještě není doložená. Ověřeno: `npm run test:document-editor-modules` (69/69), `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`, `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorCanvasMathEquationsE2ETests"` (3/3), `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentDocxFormatTests"` (28/28).
- 2026-06-07: Finální E8 dodělávka: doplněny commandy `insertNary`, `insertDelimiter`, `setMathDisplayMode`, `selectMathSlotRange` a `deactivateMathSlot`; selection controller kreslí strukturální math selection overlay a live region lokalizovaně hlásí opuštění rovnice. Save/reload E2E nyní ověřuje nary/delimiter/display mode a parity matrix obsahuje E8 řádek s command/provider/interaction/screenshot coverage. Clean-room PR poznámka je v `docs/document-editor-canvas-e8-clean-room-pr-note.md`. Ověřeno: cílený `node --test` (38/38), `npm run test:document-editor-modules` (272/272), `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`, `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorCanvasMathEquationsE2ETests"` (3/3, 7.8023 min), `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorCanvasParityCoverageMatrixTests"` (2/2) a `dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentDocxFormatTests"` (28/28).
- 2026-06-07: Doplněn samostatný Math ribbon tab v Blazor toolbar shellu, napojený na stejnou produkční equation gallery a lokalizovaný přes EN/CS/FR resource klíče. Built-in toolbar metadata obsahuje `DocumentToolbarTab.Math` a bUnit test ověřuje render math karty, otevření galerie i command callback. E2E toolbar galerie nyní pořizuje i přímý screenshot otevřené Math tab equation galerie (`00-phasee8-math-tab-equation-gallery-open.png`). Ověřeno: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentEditorToolbarDeclarativeMigrationTests"` (11/11) a po rebuild/restart demo hostů `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorCanvasMathEquationsE2ETests"` (3/3, 6.7492 min).
- Math layout je nejnáročnější část; držet metriky deterministické a testovat každý element typ zvlášť před skládáním.
- Roztažitelné závorky a radical znaménko: bez OpenType MATH tabulky použít vektorové kreslení / skládání glyphů; ověřit na 2x DPR.
- Lineární auto-buildup (Word „math autocorrect") je velký; v E8.7 dodat jen explicitní vzory, plný build-up jako samostatný follow-up.
- E8 závisí na master fázích 4 (model), 6 (paragraph layout - math inline box), 8 (input), 9 (dispatcher); naplánovat E8 až po nich.
