# Fáze 8 — Formátované tabulky

> Stav: ☐ Neza­počato · Závisí na: Fáze 3 (filtr/řazení), 7 (styly) · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Strukturované tabulky (Excel „Table" / OnlyOffice „Format as table"): pojmenovaná oblast s hlavičkou, styly (pruhované řádky/sloupce, zvýrazněný první/poslední sloupec), integrovaný **autofiltr** (Fáze 3), **řádek souhrnů** (agregace na patě), **auto-rozšíření** při psaní pod/vedle tabulky, **strukturované odkazy** ve vzorcích (`Tabulka1[Sloupec]`).

OnlyOffice reference: `view/TableSettingsAdvanced.js`, `view/TableOptionsDialog.js`, `controller/TableDesignTab.js`, `view/TableDesignTab.js`.

---

## ČÁST A — Datový model

### 8A.1 Model tabulky
- [ ] **(test)** `SpreadsheetTableTests`: `SpreadsheetTable { string Name, SpreadsheetRange Range, bool HeaderRow, bool TotalsRow, TableStyleOptions StyleOptions, string StyleName, List<SpreadsheetTableColumn> Columns }`; `SpreadsheetTableColumn { string Name, TotalsFunction? Totals, string? TotalsCustomFormula }`.
- [ ] Vytvořit modely v `Spreadsheet/Models/`: `SpreadsheetTable`, `SpreadsheetTableColumn`, `TableStyleOptions { FirstColumn, LastColumn, BandedRows, BandedColumns, HeaderRow, TotalsRow }`, enum `TotalsFunction { None, Sum, Average, Count, CountNumbers, Max, Min, StdDev, Var, Custom }`.
- [ ] Přidat `SpreadsheetSheet.Tables` (list) + `Clone()`.
- [ ] **(test)** zelené (vč. validace unikátního názvu tabulky v sešitu).

### 8A.2 Katalog stylů tabulek
- [ ] **(test)** `TableStyleCatalogTests`: vrátí předdefinované styly (light/medium/dark varianty) s definicí barev pro hlavičku, pruhy, total row, border.
- [ ] Vytvořit `Spreadsheet/Models/TableStyleCatalog.cs` (data-driven; barvy z motivu).
- [ ] **(test)** zelené.

---

## ČÁST B — Engine

### 8B.1 Auto-rozšíření
- [ ] **(test)** `TableAutoExpandTests`: zápis do buňky bezprostředně pod/vedle tabulky rozšíří `Range`; nová hlavička dostane jméno sloupce.
- [ ] Vytvořit `Spreadsheet/Data/SpreadsheetTableEngine.cs` (auto-expand, přepočet total row, zajištění unikátních názvů sloupců).
- [ ] **(test)** zelené.

### 8B.2 Totals row
- [ ] **(test)** `TotalsRow_ComputesPerColumn`: každý sloupec dle své `TotalsFunction` (SUBTOTAL-like, respektuje skryté/odfiltrované řádky).
- [ ] **(test)** zelené.

### 8B.3 Strukturované odkazy
- [ ] **(test)** `StructuredReferences`: `=Tabulka1[Trzby]` → rozsah sloupce; `[#Headers]`, `[#Totals]`, `[@Sloupec]` (aktuální řádek).
- [ ] Rozšířit `FormulaParser`/`FormulaEvaluator` o strukturované odkazy (překlad na A1 rozsah dle definice tabulky).
- [ ] **(test)** zelené.

---

## ČÁST C — Commandy
- [ ] **(test)** `CreateTableCommandTests`: z rozsahu vytvoří tabulku (detekce hlavičky), aplikuje styl, zapne filtr; Undo vrátí.
- [ ] **(test)** `ResizeTableCommand`, `ToggleTotalsRowCommand`, `SetTableStyleCommand`, `ConvertTableToRangeCommand`, `RenameTableCommand` — vše Undo.
- [ ] Vytvořit commandy v `Commands/`.
- [ ] **(test)** zelené.

---

## ČÁST D — JS canvas rendering
- [ ] Rozšířit `spreadsheet-canvas.js`: vykreslit styl tabulky (hlavička, pruhované řádky/sloupce, zvýrazněné krajní sloupce, total row, ohraničení) **nad** základním stylem a **pod** podmíněným formátováním (definovat pořadí vrstev: základ → styl tabulky → CF → výběr).
- [ ] Filtr ikony v hlavičce tabulky (sdílí Fázi 3).
- [ ] **(E2E)** tabulka má viditelný styl + filtr + total row.

---

## ČÁST E — UI

### 8E.1 Vytvoření tabulky
- [ ] **(bUnit)** „Formátovat jako tabulku" (toolbar) → galerie stylů → dialog potvrzení rozsahu + „Tabulka má hlavičky".
- [ ] Vytvořit `TmSpreadsheetCreateTableDialog` + galerii stylů (`TmSpreadsheetTableStyleGallery`) + lokalizace `TmSpreadsheet_Table_*`.
- [ ] **(bUnit)** zelené.

### 8E.2 Návrhová záložka (Table Design)
- [ ] **(bUnit)** kontextová záložka **Tabulka** (zobrazí se při výběru v tabulce): název tabulky, zaškrtávátka stylových voleb (řádek hlavičky/souhrnů, pruhy, krajní sloupce), galerie stylů, „Převést na rozsah", „Odebrat duplicity" (sdílí Fázi 4).
- [ ] Vytvořit záložku do toolbaru (podmíněné zobrazení) + lokalizace.
- [ ] **(bUnit)** zelené.

### 8E.3 Totals row UI
- [ ] **(bUnit)** v total row buňce dropdown s funkcí (Součet/Průměr/Počet/…/Vlastní).

---

## ČÁST F — E2E, screenshot, XLSX
- [ ] **(E2E)** vybrat rozsah → Formátovat jako tabulku → styl + filtr; psát pod tabulku → auto-rozšíření; zapnout total row → součty.
- [ ] **(E2E)** strukturovaný odkaz `=SUM(Tabulka1[Trzby])`.
- [ ] Baseline `table-01-styled.png`, `table-02-totals.png`, `table-03-designtab.png`.
- [ ] **UX review:** kontrast hlavičky, čitelnost pruhů, konzistence s motivem, kontextová záložka.
- [ ] **(test)** XLSX round-trip `<table>` (tableColumns, totalsRowFunction, tableStyleInfo, autoFilter).

---

## Definition of Done (Fáze 8)
- [ ] Tabulky: styly + pruhy + krajní sloupce + total row + integrovaný filtr + auto-rozšíření + strukturované odkazy.
- [ ] Návrhová kontextová záložka; galerie stylů; převod na rozsah.
- [ ] Commandy atomické + undo; vrstvení stylů v canvasu definováno.
- [ ] Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [ ] XLSX round-trip; vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 8 na ✅.
