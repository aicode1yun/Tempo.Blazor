# Fáze 2 — Najít/nahradit + Stavový řádek (agregace) + Zoom

> Stav: ✅ Hotovo (unit/bUnit + E2E + screenshoty + UX sign-off) · Závisí na: Fáze 0 · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md). Tři menší „rychlé výhry" v jedné fázi.

## Cíl & rozsah
- **Najít/nahradit**: vyhledávání v listu/sešitu, zvýraznění, navigace, nahradit jeden/vše, volby (rozlišovat velikost, celá buňka, hledat ve vzorcích/hodnotách).
- **Stavový řádek — agregace**: pro vybraný rozsah zobrazit Σ, Průměr, Počet, Počet čísel, Min, Max (jako OnlyOffice `Statusbar`).
- **Zoom**: plynulé přiblížení gridu (50–200 %) přes ovládací prvek ve stavovém řádku + Ctrl+kolečko.

OnlyOffice reference: `controller/Search.js`, `controller/Statusbar.js` + `view/Statusbar.js`.

---

## ČÁST A — Stavový řádek (shell + agregace)

### 2A.1 Komponenta stavového řádku
- [x] **(bUnit, failing)** `TmSpreadsheetStatusBarTests.Render_ShowsContainer`: existuje `.tm-spreadsheet-statusbar`.
- [x] Vytvořit `Components/Spreadsheet/TmSpreadsheetStatusBar.razor(.cs/.css)` (scoped CSS), zařadit do `TmSpreadsheet.razor` pod tab bar.
- [x] **(bUnit)** zelený.

### 2A.2 Agregační engine (čistá logika)
- [x] **(test)** `SpreadsheetAggregationTests`: pro sadu hodnot vrátí `Sum/Average/Count/CountNumbers/Min/Max`; ignoruje text a prázdné u číselných agregací; `Count` = neprázdné, `CountNumbers` = jen čísla.
- [x] Vytvořit `Spreadsheet/Data/SpreadsheetAggregation.cs` (Abstractions) — `static SpreadsheetAggregationResult Compute(IEnumerable<object?> values)`.
- [x] **(test)** zelené (vč. okrajových: samé texty → číselné agregace null/skryté; jedna buňka výběru → bez agregace nebo jen hodnota).

### 2A.3 Napojení na výběr
- [x] **(bUnit)** výběr rozsahu s čísly → status bar ukáže lokalizované `Σ`, `Průměr`, `Počet`.
- [x] Předat status baru aktuální výběr (`GetSelectedCellRefs`/`GetSelectionBounds`) a počítat agregaci při změně výběru.
- [x] Lokalizace popisků: `TmSpreadsheet_Status_Sum/Average/Count/CountNumbers/Min/Max` (3 resx).
- [x] **(bUnit)** zelené.

### 2A.4 UX
- [x] Konfigurovatelnost: kliknutím (pravým) na agregaci → kontextové menu pro skrytí/zobrazení jednotlivých agregací.
- [x] **(E2E + screenshot)** `statusbar-01-aggregation.png` + UX sign-off PASS (zarovnání vpravo, oddělovače mezerami, tabular hodnoty, nepřekáží obsahu). E2E `StatusBar_NumericRange_ShowsAggregations` zelený.

---

## ČÁST B — Zoom

### 2B.1 Model & ovládání
- [x] **(bUnit, failing)** status bar obsahuje zoom ovladač `.tm-spreadsheet-statusbar__zoom` (slider/`-`/`+`/procenta).
- [x] Přidat stav `Zoom` (double, 1.0 = 100 %) na úroveň `TmSpreadsheet`/listu; clamp 0.5–2.0.
- [x] **(bUnit)** ovladač zobrazuje aktuální procenta a mění hodnotu.

### 2B.2 Aplikace zoomu na JS canvas
- [x] **(E2E)** změna zoomu → grid překreslí buňky ve změněném měřítku, hlavičky i mřížka konzistentní. E2E `StatusBar_ZoomIn_EnlargesCellsAndUpdatesPercent` ověřuje nárůst šířky buňky na 150 %.
- [x] Zoom aplikován skrze geometrii v C# (`SpreadsheetGridGeometry.Update(..., zoom)` škáluje šířky/výšky → render i hit-test konzistentní) + font scaling v `spreadsheet-canvas.js` (`getRenderZoom`/`Zoom` ve frame) + C# interop v `TmSpreadsheetCanvasGrid` (parametr `Zoom`). _(Pozn.: realizováno geometrií místo literálního `setZoom(factor)` — hlavičkový žlab zůstává nezvětšený.)_
- [x] **(test)** geometrické přepočty (řádek/sloupec → pixely) respektují zoom — unit testy `SpreadsheetRenderingHelperTests.Geometry_Zoom_*`.
- [x] Ctrl+kolečko myši mění zoom (handler v `spreadsheet-canvas.js` → `OnCanvasZoomDelta`), Ctrl+0 reset na 100 %.
- [x] **(E2E + screenshot)** `zoom-01-150.png`, `zoom-02-50.png` + UX sign-off PASS (text ostrý, žádné rozmazání). _(Pozn.: ovladač má krok 10 %, takže 75 % není dosažitelné → baseline zachycuje 50 % spodní mez místo `zoom-02-75`.)_

---

## ČÁST C — Najít / nahradit

### 2C.1 Vyhledávací engine (čistá logika)
- [x] **(test)** `SpreadsheetSearchEngineTests`: najde výskyty v listu dle `SearchOptions { Query, MatchCase, WholeCell, SearchIn(Values|Formulas), Scope(Sheet|Workbook) }`; vrací uspořádaný seznam zásahů (sheet, cellRef, matchStart/Length).
- [x] Vytvořit `Spreadsheet/Data/SpreadsheetSearchEngine.cs` + `SpreadsheetSearchOptions`, `SpreadsheetSearchHit` (Abstractions).
- [x] **(test)** pokrýt: case sensitivity, whole-cell vs. substring, hledání ve vzorci vs. v zobrazené hodnotě, prázdný dotaz.

### 2C.2 Nahrazování (commandy, atomické + undo)
- [x] **(test)** `ReplaceCommandTests`: nahradit jeden výskyt → změní hodnotu buňky, Undo vrátí; nahradit vše → `BatchCommand`, jeden undo krok vrátí všechny.
- [x] Vytvořit `Commands/ReplaceCommand.cs` (a využít `BatchCommand` pro „nahradit vše").
- [x] **(test)** zelené; ošetřit nahrazování ve vzorcích (jen pokud `SearchIn=Formulas`).

### 2C.3 Dialog/panel UI
- [x] **(bUnit, failing)** `TmSpreadsheetFindReplaceDialog` zobrazuje pole hledat/nahradit, přepínače voleb, tlačítka Najít další / Nahradit / Nahradit vše / Zavřít — vše lokalizované.
- [x] Vytvořit `Components/Spreadsheet/Dialogs/TmSpreadsheetFindReplaceDialog.razor(.cs/.css)`.
- [x] Klávesové zkratky: `Ctrl+F` (najít), `Ctrl+H` (nahradit), `Enter`=další, `Esc`=zavřít, `F3`=další. _(Ctrl+F/H přes canvas grid, Enter/Esc/F3/Shift+Enter v dialogu.)_
- [x] Lokalizace klíče `TmSpreadsheet_Find_*` (Title, Query, ReplaceWith, MatchCase, WholeCell, InFormulas, FindNext, Replace, ReplaceAll, NoMatches, ReplacedCount).
- [x] **(bUnit)** zelené.

### 2C.4 Navigace a zvýraznění zásahů
- [x] **(E2E)** `Ctrl+F` → zadat dotaz → `Enter` cykluje mezi zásahy (scroll + výběr aktivní buňky), počítadlo „1 z N". E2E `FindReplace_CtrlF_OpensPanelAndNavigates` zelený.
- [x] Zvýraznění zásahů v canvasu (`spreadsheet-canvas.js` `setSearchHighlights`/`clearSearchHighlights` overlay) + interop (`ApplyEngineSearchHighlightsAsync`). Ověřeno na `find-01-highlight.png`: aktivní zásah oranžově s rámečkem, ostatní žlutě.
- [x] Cross-sheet scope: navigace přepíná listy (`NavigateToCurrentHitAsync` přepne `ActiveSheetIndex`).
- [x] **(E2E + screenshot)** `find-01-highlight.png`, `find-02-replace.png` + UX sign-off PASS (kontrast zvýraznění dobrý, aktivní zásah jasně odlišen, dialog lokalizovaný, fokus na pole hledání). E2E `FindReplace_Replace_ChangesCellValue` zelený.

---

## Definition of Done (Fáze 2)
- [x] Status bar: agregace pro výběr (Σ/Průměr/Počet/Počet čísel/Min/Max), lokalizováno.
- [x] Zoom 50–200 % přes ovladač i Ctrl+kolečko (geometrie+font scaling), ostré vykreslení ověřeno na `zoom-01-150.png`.
- [x] Najít/nahradit: volby, navigace, nahradit jeden/vše (atomicky, undo), cross-sheet.
- [x] Unit + bUnit zelené (571 spreadsheet testů) + E2E 5/5 zelené + 5 screenshot baseline + UX sign-off PASS.
- [x] Žádné hardcoded texty; klávesové zkratky funkční.
- [x] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 2 na ✅.

> **Stav: HOTOVO (2026-06-05).** Logika, modely, commandy, komponenty, wiring, lokalizace (3 resx), unit/bUnit (571 testů) i E2E (`StatusBar_*`, `FindReplace_*` — 5/5 zelené proti WASM demu na https://localhost:7106) a screenshot baseline (`statusbar-01-aggregation`, `zoom-01-150`, `zoom-02-50`, `find-01-highlight`, `find-02-replace`) hotové. UX sign-off PASS. Odchylka: `zoom-02-75` → `zoom-02-50` (ovladač krok 10 %).
