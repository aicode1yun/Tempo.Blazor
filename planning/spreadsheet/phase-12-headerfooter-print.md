# Fáze 12 — Záhlaví/zápatí + Nastavení tisku

> Stav: ☐ Neza­počato · Závisí na: Fáze 0 · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
- **Záhlaví/zápatí**: levá/střední/pravá sekce, pole (číslo stránky, počet stránek, datum, čas, název souboru/listu, cesta), liché/sudé/první stránky, obrázek.
- **Nastavení tisku**: oblast tisku, opakované řádky/sloupce (tisk titulků), orientace, velikost papíru, okraje, měřítko (fit to N stran / %), pořadí stran, mřížka/záhlaví na tisku, zarovnání na střed.
- **Náhled tisku** + export do **PDF** (přes tiskovou pipeline / prohlížeč).

OnlyOffice reference: `model/HeaderFooter.js`, `view/HeaderFooterDialog.js`, `view/PrintSettings.js`, `view/PrintTitlesDialog.js`, `view/PageMarginsDialog.js`, `view/ScaleDialog.js`, `controller/Print.js`.

---

## ČÁST A — Datový model

### 12A.1 Záhlaví/zápatí
- [ ] **(test)** `SpreadsheetHeaderFooterTests`: `SpreadsheetHeaderFooter { HfSection Odd, HfSection? Even, HfSection? First, bool DifferentOddEven, bool DifferentFirst, bool ScaleWithDoc, bool AlignWithMargins }`; `HfSection { string Left, Center, Right }` s podporou polí (`&P`,`&N`,`&D`,`&T`,`&F`,`&A`,`&Z`,`&G` obrázek).
- [ ] Vytvořit modely v `Spreadsheet/Models/`; přidat `SpreadsheetSheet.HeaderFooter`.
- [ ] **(test)** zelené.

### 12A.2 Nastavení tisku
- [ ] **(test)** `SpreadsheetPrintSettingsTests`: `SpreadsheetPrintSettings { SpreadsheetRange? PrintArea (více oblastí), string? RepeatRows, string? RepeatCols, PageOrientation, PaperSize, Margins(L/R/T/B/Header/Footer), ScaleMode(Percent|FitTo), int ScalePercent, int FitWidth, int FitHeight, bool PrintGridlines, bool PrintHeadings, PageOrder, bool CenterH, bool CenterV }`.
- [ ] Vytvořit modely + enumy; přidat `SpreadsheetSheet.PrintSettings`.
- [ ] **(test)** zelené.

---

## ČÁST B — Engine (stránkování & pole)

### 12B.1 Výpočet stránek
- [ ] **(test)** `PaginationTests`: rozdělí oblast tisku na stránky dle velikosti papíru, okrajů, měřítka a opakovaných titulků; `FitTo` přepočítá měřítko.
- [ ] Vytvořit `Spreadsheet/Data/SpreadsheetPaginator.cs` → seznam stránek (rozsahy buněk + pozice).
- [ ] **(test)** zelené (vč. fit-to-width, manuální zlomy stránek — volitelně).

### 12B.2 Vykreslení polí záhlaví/zápatí
- [ ] **(test)** `HeaderFooterFieldsTests`: nahrazení `&P/&N/&D/&T/&F/&A` skutečnými hodnotami pro danou stránku.
- [ ] **(test)** zelené.

---

## ČÁST C — Commandy
- [ ] **(test)** `SetHeaderFooterCommand`, `SetPrintAreaCommand` / `ClearPrintAreaCommand` / `AddToPrintAreaCommand`, `SetPrintSettingsCommand`, `SetPrintTitlesCommand` — vše Undo.
- [ ] Vytvořit commandy.
- [ ] **(test)** zelené.

---

## ČÁST D — UI

### 12D.1 Dialogy
- [ ] **(bUnit)** `TmSpreadsheetHeaderFooterDialog`: tři sekce (L/C/R) pro liché/sudé/první, tlačítka pro vkládání polí, přepínače různé liché/sudé a první. Lokalizováno.
- [ ] **(bUnit)** `TmSpreadsheetPrintSettingsDialog`: oblast tisku, titulky, orientace, papír, okraje, měřítko, mřížka/záhlaví, zarovnání; **náhled**.
- [ ] **(bUnit)** `TmSpreadsheetPageMarginsDialog`, `TmSpreadsheetScaleDialog` (nebo integrovat do PrintSettings).
- [ ] Vytvořit dialogy + lokalizace `TmSpreadsheet_Print_*`, `TmSpreadsheet_HeaderFooter_*`.
- [ ] Záložka **Rozložení/Tisk** (Page Layout): Okraje, Orientace, Velikost, Oblast tisku, Tisk titulků, Měřítko, Záhlaví/zápatí.
- [ ] **(bUnit)** zelené.

### 12D.2 Náhled tisku
- [ ] **(bUnit/E2E)** `TmSpreadsheetPrintPreview`: stránkovaný náhled (canvas), navigace mezi stránkami, zobrazení záhlaví/zápatí, okrajů, zlomů.

---

## ČÁST E — Tisk / PDF export

### 12E.1 Tisková pipeline
- [ ] **(test)** generování tiskové reprezentace (HTML/canvas → tisk) podle stránek; `window.print()` přes JS interop nebo serverový PDF (host abstrakce).
- [ ] **(E2E)** vyvolání náhledu/tisku negeneruje chyby; PDF export (pokud přes prohlížeč) vytvoří soubor.
- [ ] Abstrahovat skutečný PDF výstup do interfacu (host může dodat serverový render); komponenta dodá stránkovaný layout.

---

## ČÁST F — Screenshot + XLSX
- [ ] Baseline `print-01-settings.png`, `print-02-preview.png`, `headerfooter-01-dialog.png` + UX sign-off (čitelnost náhledu, indikace zlomů, ovládání měřítka).
- [ ] **(test)** XLSX round-trip `<headerFooter>`, `<pageSetup>`, `<pageMargins>`, `<printOptions>`, `<definedName _xlnm.Print_Area / Print_Titles>`.

---

## Definition of Done (Fáze 12)
- [ ] Záhlaví/zápatí (sekce, pole, liché/sudé/první, obrázek).
- [ ] Nastavení tisku (oblast, titulky, orientace, papír, okraje, měřítko, mřížka/záhlaví, zarovnání).
- [ ] Stránkování + náhled tisku + tisk/PDF (přes abstrakci hostitele).
- [ ] Commandy atomické + undo; Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [ ] XLSX round-trip; vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 12 na ✅.
