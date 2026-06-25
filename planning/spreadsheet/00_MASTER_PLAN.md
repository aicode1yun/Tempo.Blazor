# TmSpreadsheet → OnlyOffice parity — MASTER PLÁN

> Řídicí dokument pro vývoj komponenty `TmSpreadsheet` směrem k funkční paritě s OnlyOffice Spreadsheet Editorem.
> Vychází z analýzy [`../TmSpreadsheet_OnlyOffice_Analyza.md`](../TmSpreadsheet_OnlyOffice_Analyza.md).
>
> **Tento soubor je závazný pro všechny fáze.** Každý fázový soubor (`phase-NN-*.md`) na něj odkazuje a dědí jeho pravidla, konvence a Definition of Done. Detailní mikro-kroky jsou ve fázových souborech.

---

## 1. Role a způsob práce

Pracuje se v roli **Senior Full-stack Developer + UI/UX expert**. Cílem je **obecná, znovupoužitelná komponenta**, kterou bude konzumovat jiná aplikace — proto:

- Veřejné API (parametry, eventy, modely) navrhujeme jako stabilní kontrakt.
- Žádné předpoklady o hostitelské aplikaci (uživatelé, identita, perzistence) — vše skrze **abstrakce/interfaces** v `Tempo.Blazor.Abstractions`, implementace dodá host nebo demo.
- Vzhled a UX jsou prvotřídní (jsme UI/UX experti), konzistentní se zbytkem knihovny `Tempo.Blazor`.

---

## 2. ⚠️ KRITICKÁ PRAVIDLA (absolutní, neporušovat)

### 2.1 TDD — Test First (Red → Green → Refactor)
```
1. Napiš FAILING test (červený) – MUSÍ selhat před implementací
2. Napiš MINIMÁLNÍ kód pro průchod testu (zelený)
3. Refactor (čistý kód beze změny chování)
4. Opakuj pro další mikro-krok
```
Každý mikro-krok ve fázových souborech je formulován tak, aby šel udělat tímto cyklem. Test je **specifikace** — když selže a logika testu je správná, **opravuje se implementace, nikdy ne test**.

### 2.2 Žádné hardcoded texty — vše z resources
- ❌ ZAKÁZÁNO: `"Uložit"`, `"Filtr"`, `"Komentář"` přímo v C#/Razor.
- ✅ POVINNÉ: `@Loc["TmSpreadsheet_<Oblast>_<Klíč>"]` (globální injekce `Loc` přes `_Imports.razor` — **nikdy znovu neinjektovat** `ITmLocalizer` v code-behind ani razor).
- Klíče se přidávají do **všech** jazykových souborů: `src/Tempo.Blazor/Resources/TmResources.resx` (neutrální/EN), `TmResources.cs.resx` (CZ), `TmResources.fr.resx` (FR).
- Konvence klíčů: `TmSpreadsheet_<Oblast>_<Prvek>` (např. `TmSpreadsheet_Filter_SortAsc`, `TmSpreadsheet_Comment_Resolve`).

### 2.3 Žádné zjednodušené implementace
- ❌ ZAKÁZÁNO: placeholdery, mock data, `// TODO`, `// FIXME`, `// implement later`, „zatím natvrdo".
- ✅ Produkční kód od prvního řádku, plná funkcionalita, reálná data.

### 2.4 Architektonické zásady
- **DTO/modely → `Tempo.Blazor.Abstractions`** (oblast `Spreadsheet/...`). Komponenty a UI logika → `Tempo.Blazor`.
- **Atomické operace / Unit of Work**: každá uživatelská akce = jeden `ISpreadsheetCommand` (nebo `BatchCommand`), který je celý undo/redo-ovatelný jako jeden krok.
- **Scoped CSS**: `*.razor.css` pro každou komponentu (žádné globální styly bez důvodu).
- **Lokalizace** výhradně přes `ITmLocalizer`.
- Veřejné API komentované XML doc komentáři (projekt má `GenerateDocumentationFile`).

### 2.5 Při nejistotě se ptej
Nehádej specifikaci. Když je požadavek nejednoznačný (chování, UX detail, formát XLSX), zeptej se uživatele.

---

## 3. Architektonická rozhodnutí (závazná)

### 3.1 Canvas-only (odstranění DOM enginu)
Komponenta má dnes 3 režimy v `SpreadsheetRenderMode`: `Dom` (`TmSpreadsheetGrid`), `Canvas` a `CanvasJsEngine` (oba přes `TmSpreadsheetCanvasGrid`, přepínač `UseJsEngine`).

**Rozhodnutí:** Jdeme **jen do JS canvas enginu**. Ve Fázi 0:
- Odstranit `TmSpreadsheetGrid.razor/.cs/.css` (DOM) a `spreadsheet.js` (DOM helper, je-li jen pro DOM grid).
- Konsolidovat `TmSpreadsheetCanvasGrid` na `UseJsEngine = true` (odstranit hybridní `UseJsEngine=false` větve).
- Odstranit enum `SpreadsheetRenderMode` a parametr `RenderMode` (žádná zpětná kompatibilita — komponenta je ve vývoji).
- JS engine = `spreadsheet-canvas.js` je jediná vykreslovací vrstva. Veškeré nové funkce (filtry, komentáře, podmíněné formátování…) se kreslí v něm.

### 3.2 Spolupráce: zrcadlit existující `Tempo.Blazor.Collaboration`
Document editor už má hotové **komentáře, revize i co-editaci** přes:
- `Tempo.Blazor.Collaboration` → `SignalRDocumentCollaborationProvider` (model „operation-batch + cursor", hub metody Join/Leave/BroadcastOperationBatch/GetOperationBatches/BroadcastCursor/GetCursors/RemoteOperationBatchReceived/RemoteCursorReceived).
- `IDocumentCollaborationProvider` / `IDocumentCollaborationRealtimeProvider`, `InMemoryDocumentCollaborationProvider`, `DocumentCollaborationSync`, hub v `Tempo.Blazor.Demo.Api/Hubs/`.

**Rozhodnutí:** Pro spreadsheet vytvoříme **analogický** `ISpreadsheetCollaborationProvider` (+ realtime varianta) a serializovatelný **`SpreadsheetOperation`** model, znovupoužijeme SignalR transport (rozšíříme `Tempo.Blazor.Collaboration` o spreadsheet hub/provider). Fáze A1/A2/A3 (15–17) na tom staví.

### 3.3 Evoluce datového modelu (mapa — drží konzistenci napříč fázemi)
Aby fáze nekolidovaly, tady je cílový tvar modelů. Každá fáze přidá jen své části (a napíše k nim testy).

`SpreadsheetCell` (Abstractions/Spreadsheet/Models) – přidá se:
| Pole | Fáze | Účel |
|---|---|---|
| `string? CommentId` | 15 | odkaz na vlákno komentáře |
| `SpreadsheetValidationRef? Validation` | 5 | pravidlo validace (nebo přes rozsah) |

`SpreadsheetSheet` – přidá se:
| Pole | Fáze |
|---|---|
| `SpreadsheetAutoFilter? AutoFilter` | 3 |
| `List<SpreadsheetConditionalFormat> ConditionalFormats` | 7 |
| `List<SpreadsheetDataValidation> DataValidations` | 5 |
| `List<SpreadsheetTable> Tables` | 8 |
| `List<SpreadsheetComment> Comments` | 15 |
| `List<SpreadsheetProtectedRange> ProtectedRanges` | 10 |
| `SpreadsheetSheetProtection? Protection` | 10 |
| `List<SpreadsheetSheetView> Views` | 11 |
| `SpreadsheetHeaderFooter? HeaderFooter` | 12 |
| `SpreadsheetPrintSettings? PrintSettings` | 12 |
| `List<SpreadsheetPivotTable> PivotTables` | 13 |
| `List<SpreadsheetSlicer> Slicers` | 14 |

`SpreadsheetWorkbook` – přidá se:
| Pole | Fáze |
|---|---|
| `List<SpreadsheetNamedRange> NamedRanges` | 6 |
| `SpreadsheetWorkbookProtection? Protection` | 10 |
| `SpreadsheetRevisionLog RevisionLog` | 16 |
| `CalculationOptions Calculation` (iterace) | 9 |

Sdílené nové enginy/služby (Abstractions/Spreadsheet):
- `Format/SpreadsheetValueParser` (Fáze 1) — text → typovaná hodnota + implikovaný formát.
- `Data/SpreadsheetSortEngine`, `Data/SpreadsheetFilterEngine` (Fáze 3).
- `Collaboration/SpreadsheetOperation` (Fáze 17).

---

## 4. Testovací strategie (povinná pro každou fázi)

Testovací pyramida, vše TDD (test first):

1. **Unit testy (čistá logika)** — `Tempo.Blazor.Tests/Components/Spreadsheet/...`
   - Parsery, enginy (sort/filter/conditional/formula), commandy (Execute + **Undo**), XLSX round-trip.
   - bez UI, deterministické, rychlé. FluentAssertions.
2. **Komponentové testy (bUnit)** — `Tempo.Blazor.Tests/Components/Spreadsheet/...`
   - Dědí `LocalizationTestBase`. Ověřují render, interakce, eventy, lokalizované texty (žádné hardcoded).
   - Po canvas-only: assertovat na `.tm-spreadsheet-canvas-grid` a strukturu UI (dialogy, panely, toolbar), ne na DOM grid.
3. **E2E testy (Playwright)** — `Tempo.Blazor.E2E/Spreadsheet*E2ETests.cs`
   - Proti demu na route `/spreadsheet` (+ nové demo stránky/scénáře dle potřeby).
   - Reálné interakce (klik, psaní, drag), ověření chování i přes JS engine.
4. **Screenshot baseline testy** — `__baseline__/spreadsheet/<feature>-NN-*.png`
   - Viz protokol §5.

**Definice „hotové funkce" (Definition of Done)** — zaškrtnout lze, až když:
- [ ] model + unit testy (vč. Undo),
- [ ] command(y) atomické + undo/redo testy,
- [ ] vykreslení v JS canvas enginu,
- [ ] UI (dialog/panel/toolbar) + **plná lokalizace** (3 resx),
- [ ] bUnit testy,
- [ ] E2E testy (Playwright),
- [ ] screenshot baseline + **UX sign-off** (§5),
- [ ] XLSX round-trip (u perzistentních funkcí),
- [ ] JsonDocumentation aktualizována (u změn veřejného API),
- [ ] build zelený (`-f net9.0`, viz §7), všechny testy zelené,
- [ ] checkbox odškrtnut v daném fázovém souboru.

---

## 5. Screenshot + UX protokol (dvojí kontrola)

Každý vizuální přírůstek má screenshot test se **dvěma účely**:

**(a) Regrese — „změnilo se, co se změnit mělo":**
- Před změnou: baseline screenshot stavu.
- Po změně: nový screenshot, diff proti baseline.
- Očekávaná oblast změny se **musí** lišit; zbytek UI se lišit **nesmí** (žádná nechtěná regrese).
- Baseline se aktualizuje vědomě (commit s odůvodněním), ne automaticky.

**(b) UX/UI review (posuzuji jako expert):** ke každému screenshotu projít checklist:
- Vizuální hierarchie a zarovnání (mřížka, odsazení, konzistentní mezery).
- Konzistence s designem `Tempo.Blazor` (barvy, typografie, ikony, poloměry, stíny).
- Stavy: hover / focus / active / disabled / loading / empty / error.
- Přístupnost: kontrast (WCAG AA), focus-ring, klávesová obsluha, ARIA role u dialogů/panelů.
- Responsivita a chování při přetečení (dlouhé texty, mnoho položek).
- Mikrointerakce a plynulost (přechody, žádné „skoky" layoutu).
- Lokalizace: delší překlady (CZ/FR) nerozbíjejí layout.
- Závěr: **PASS / nálezy k opravě** (nálezy = nový červený test/krok, ne „někdy příště").

Pomocné soubory: `Spreadsheet*BaselineScreenshots.cs` (vzor: `DiagramBaselineScreenshots.cs`, `DocumentEditorBaselineScreenshots.cs`).

---

## 6. Konvence a vzory (jak přidat…)

- **Nový command:** `Components/Spreadsheet/Commands/<Name>Command.cs : ISpreadsheetCommand` (Execute + Undo), test `…CommandTests.cs`. Spouští se přes `SpreadsheetCommandManager`.
- **Nový dialog:** `Components/Spreadsheet/Dialogs/TmSpreadsheet<Name>Dialog.razor(.cs/.css)`, lokalizované texty, `OnApply`/`OnClose` eventy; vzor `TmSpreadsheetFormatCellsDialog`.
- **Nový postranní panel:** `Components/Spreadsheet/Panels/…` (komentáře, revize) + scoped CSS.
- **Nová položka toolbaru:** rozšířit `TmSpreadsheetToolbar.razor(.cs)`, ikona přes `TmIcon`, lokalizovaný `title`.
- **Nová funkce vzorce:** `Formula/Functions/SpreadsheetFunctions.cs` + zápis do `FunctionRegistry` + katalog nápovědy `SpreadsheetFormulaFunctionCatalog.cs` + testy v příslušné kategorii (`SpreadsheetFunction<Kategorie>Tests.cs`).
- **Kreslení v JS enginu:** rozšířit `wwwroot/js/spreadsheet-canvas.js` + odpovídající C# interop v `TmSpreadsheetCanvasGrid.razor.cs` (patch metody `ApplyEngine…Async`).
- **Klíče resx:** vždy do 3 souborů; CZ a FR překlad dodat hned (ne odložit).

---

## 7. Build & běh (specifika prostředí)

- Build celého řešení bývá na tomto stroji náročný (OOM/stack overflow). **Stavět cíleně:** `dotnet build -f net9.0` na konkrétní projekt, popř. ověřit kód staticky.
- Testy spouštět cíleně po projektech (`Tempo.Blazor.Tests`, `Tempo.Blazor.E2E`).
- E2E vyžaduje běžící demo; Playwright 1.51, browsery nainstalované.
- Rutinní příkazy (dotnet, netstat, Stop-Process, git) lze spouštět bez doptávání.

---

## 8. Mapa fází (doporučené pořadí dle závislostí)

| # | Fáze | Soubor | Závisí na | Server | Stav |
|---|---|---|---|---|---|
| 0 | Základy & canvas-only | `phase-00-foundation-canvas-only.md` | — | ✅ | ✅ |
| 1 | A5 Rozpoznávání typů buněk | `phase-01-cell-type-detection.md` | 0 | ✅ | ✅ |
| 2 | Najít/nahradit + Stavový řádek + Zoom | `phase-02-find-statusbar-zoom.md` | 0 | ✅ | ✅ |
| 3 | A4 AutoFilter + Řazení | `phase-03-autofilter-sort.md` | 0,1 | ✅ | ✅ |
| 4 | Odebrat duplicity + Text do sloupců + Speciální vložení | `phase-04-data-tools.md` | 1 | ✅ | ✅ |
| 5 | Ověření dat (Data Validation) | `phase-05-data-validation.md` | 1 | ✅ | ✅ |
| 6 | Hypertextové odkazy + Pojmenované rozsahy | `phase-06-hyperlinks-named-ranges.md` | 0 | ✅ | ✅ |
| 7 | Podmíněné formátování | `phase-07-conditional-formatting.md` | 1 | ✅ | ☐ |
| 8 | Formátované tabulky | `phase-08-formatted-tables.md` | 3,7 | ✅ | ☐ |
| 9 | Kompletní vzorce | `phase-09-formulas-complete.md` | 1,6 | ✅ | ☐ |
| 10 | Ochrana (list/sešit + rozsahy) | `phase-10-protection.md` | 0 | ✅ | ☐ |
| 11 | Pojmenované pohledy listu | `phase-11-named-sheet-views.md` | 3 | ✅ | ☐ |
| 12 | Záhlaví/zápatí + Tisk | `phase-12-headerfooter-print.md` | 0 | ✅ | ☐ |
| 13 | Pivot tabulky | `phase-13-pivot-tables.md` | 3,8 | ✅ | ☐ |
| 14 | Slicery | `phase-14-slicers.md` | 8,13 | ✅ | ☐ |
| 15 | A1 Komentáře | `phase-15-comments.md` | 0 | ✅ lokálně / 🖥️ sdílení | ☐ |
| 16 | A2 Revize / sledování změn | `phase-16-track-changes.md` | 15 | ✅ lokálně / 🖥️ sdílení | ☐ |
| 17 | A3 Co-editace | `phase-17-coediting.md` | 15,16 | 🖥️ backend | ☐ |

> **Stav fáze** se zrcadlí i v hlavičce každého fázového souboru. Po dokončení fáze se zde přepne `☐` → `✅`.
>
> **Fáze 0 dokončena** (2026-06-05): canvas-only, DOM engine odstraněn, build + 457 spreadsheet testů zelených, E2E baseline PNG vygenerovány proti `Tempo.Blazor.Demo` (https://localhost:7106), UX sign-off PASS.
>
> **Fáze 2 dokončena** (2026-06-05): stavový řádek (agregace Sum/Average/Count/CountNumbers/Min/Max), zoom 50–200 % (geometrie+font scaling, Ctrl+kolečko, Ctrl+0), najít/nahradit (engine + `ReplaceCommand` undo + dialog + zvýraznění + cross-sheet). 571 spreadsheet unit/bUnit testů + 5 E2E (`StatusBar_*`, `FindReplace_*`) zelených, 5 screenshot baseline (`statusbar-01-aggregation`, `zoom-01-150`, `zoom-02-50`, `find-01-highlight`, `find-02-replace`), UX sign-off PASS.
>
> **Fáze 4 dokončena** (2026-06-05): datové nástroje — **Odebrat duplicity** (engine `SpreadsheetDeduplicate` typově-citlivé porovnání + `RemoveDuplicatesCommand` s kompakcí a undo, dialog se zaškrtávátky sloupců / záhlaví / velikost písmen + plural-aware výsledný banner), **Text do sloupců** (engine `SpreadsheetTextToColumns` delimited s text qualifierem a sbalením oddělovačů + fixed-width, `TextToColumnsCommand` s typovou detekcí přes `SpreadsheetValueParser` a undo, 3-krokový průvodce s živým náhledem), **Speciální vložení** (`SpreadsheetPasteSpecialOptions` + `PasteSpecialCommand`: hodnoty/vzorce/formáty/vše/bez ohraničení, operace +−×÷, transpozice, přeskočit prázdné, vše undo; dialog + `Ctrl+Shift+V` přes JS engine). 60 lokalizačních klíčů × 3 resx. 678 spreadsheet unit/bUnit testů + 4 E2E (`RemoveDuplicates_*`, `TextToColumns_*`, `PasteSpecial_ValuesOnly/Transpose`) proti HTTPS WASM (7106) + HTTPS API (5100), 3 screenshot baseline (`dedup-01-dialog`, `t2c-01-step2-preview`, `pastespecial-01-dialog`), UX sign-off PASS. Opraven JS engine `handleCommandKey` (Ctrl+Shift+V se nyní forwarduje do .NET místo nativního vložení). Zbývá volitelný „paste options" mini-overlay po běžném vložení (neimplementováno, čistě UX rozšíření).
>
> **Fáze 3 dokončena** (2026-06-05): AutoFilter (hodnotový + text/číslo/datum + barevné filtry, AND napříč sloupci, A/NEBO dvě podmínky) a řazení (jedno/víceúrovňové, typové pořadí, prázdné dole, dle barvy, case-sensitive, posun relativních vzorců, hlídání sloučených buněk) — modely v `Spreadsheet/Data/`, enginy `SpreadsheetFilterEngine`/`SpreadsheetSortEngine`, commandy `SetAutoFilter`/`UpdateColumnFilter`/`ClearAutoFilter`/`SortRange` (vše undo), JS canvas filtr tlačítka + hit-test + callback, UI `TmSpreadsheetFilterDropdown` + `TmSpreadsheetCustomFilterDialog` + `TmSpreadsheetSortDialog` + toolbar záložka **Data**, 58 lokalizačních klíčů × 3 resx, XLSX round-trip `<autoFilter>` + skryté řádky. 617 spreadsheet unit/bUnit testů + 5 E2E (`AutoFilter_*`, `Sort_Descending_*`) proti HTTPS WASM+API, 3 screenshot baseline (`filter-01-dropdown`, `filter-02-active`, `sort-01-dialog`), UX sign-off PASS. Opraven latentní bug JS enginu (skryté řádky/sloupce se nesbalovaly — `size || default` a `size <= 0`). Zbývá drobné rozšíření: kontextové menu buňky pro filtr/řazení.

---

## 9. Jak číst fázové soubory

Každý `phase-NN-*.md` má jednotnou strukturu:
1. **Cíl & rozsah** + odkaz na OnlyOffice referenci.
2. **Datový model** (Abstractions) — kroky + testy.
3. **Engine/logika** — kroky + testy.
4. **Command(y)** — kroky + undo testy.
5. **JS canvas rendering** — kroky.
6. **UI (dialog/panel/toolbar)** + lokalizace.
7. **bUnit testy**, **E2E testy**, **screenshot + UX**.
8. **XLSX round-trip** (kde dává smysl).
9. **Definition of Done checklist** (zrcadlí §4).

Každá odrážka `- [ ]` je jeden TDD mikro-krok. Odškrtává se průběžně při implementaci.

---

_Vytvořeno jako podklad pro postupnou TDD implementaci. Fázové soubory se přidávají a udržují aktuální._
