# Fáze 13 — Pivot tabulky

> Stav: ☐ Neza­počato · Závisí na: Fáze 3 (filtr), 8 (tabulky) · Server: ✅ klientsky · Náročnost: 🔴 XL
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Kontingenční tabulky: zdroj dat (rozsah/tabulka), oblasti **Filtry / Sloupce / Řádky / Hodnoty**, agregace (Sum/Count/Average/Max/Min/Product/StdDev/Var/% z celku…), seskupování (datum→rok/měsíc, číselné koše), rozbalování/sbalování, mezisoučty a celkové součty, řazení a filtry polí, počítaná pole/položky, styl, **GETPIVOTDATA**. Velký samostatný subsystém — implementovat v pod-iteracích.

OnlyOffice reference: `model/PivotTables.js`, `view/PivotTable.js`, `view/CreatePivotDialog.js`, `view/PivotSettings(.Advanced).js`, `view/PivotGroupDialog.js`, `view/PivotShowDetailDialog.js`, `view/FieldSettingsDialog.js`, `view/ValueFieldSettingsDialog.js`, `view/PivotCalculatedItemsDialog.js`, `view/PivotInsertCalculatedItemDialog.js`, `controller/PivotTable.js`.

---

## ČÁST A — Datový model (13.1)

### 13A.1 Definice pivotu
- [ ] **(test)** `SpreadsheetPivotTableTests`: `SpreadsheetPivotTable { string Name, PivotSource Source, SpreadsheetRange TargetAnchor, List<PivotField> Fields, List<int> Filters, List<int> Rows, List<int> Columns, List<PivotValueField> Values, PivotLayout Layout, PivotStyleOptions Style }`.
- [ ] `PivotField { string SourceName, string Caption, PivotFieldSettings (subtotaly, řazení, filtr položek, sbalení) }`; `PivotValueField { int FieldIndex, PivotAggregation, ShowAs(None|PercentOfTotal|PercentOfColumn|RunningTotal|…), string NumberFormat }`.
- [ ] Vytvořit modely v `Spreadsheet/Models/Pivot/`; přidat `SpreadsheetSheet.PivotTables`.
- [ ] **(test)** zelené.

### 13A.2 Cache zdroje
- [ ] **(test)** `PivotCacheTests`: ze zdrojového rozsahu/tabulky vytvoří cache (sloupce, unikátní hodnoty, typy). Aktualizace cache při změně zdroje (refresh).
- [ ] Vytvořit `Spreadsheet/Data/Pivot/PivotCache.cs`.
- [ ] **(test)** zelené.

---

## ČÁST B — Výpočetní engine (13.2)

### 13B.1 Agregace a layout
- [ ] **(test)** `PivotEngineTests.SingleRowSingleValue`: řádkové pole + Sum hodnoty → správné skupiny a součty.
- [ ] **(test)** `RowAndColumn`: křížová tabulka.
- [ ] **(test)** `MultipleValues`, `MultipleRowFields` (vnořené), `Subtotals`, `GrandTotals`.
- [ ] **(test)** `Filters` (page area), `ItemFilter` (skrytí položek pole).
- [ ] **(test)** `ShowAs_PercentOfTotal/Column/Row`, `RunningTotal`.
- [ ] **(test)** agregace: Sum/Count/CountNums/Average/Max/Min/Product/StdDev(P)/Var(P).
- [ ] Vytvořit `Spreadsheet/Data/Pivot/PivotEngine.cs` → `PivotResult Compute(definition, cache)` (matice buněk + meta o rozbalení).
- [ ] **(test)** zelené.

### 13B.2 Seskupování
- [ ] **(test)** `Grouping_Date` (rok/čtvrtletí/měsíc/den), `Grouping_Numeric` (koše po N), `Grouping_Manual`.
- [ ] **(test)** zelené.

### 13B.3 Počítaná pole/položky
- [ ] **(test)** `CalculatedField` (vzorec nad poli), `CalculatedItem` (vzorec nad položkami).
- [ ] **(test)** zelené.

---

## ČÁST C — Renderování do listu (13.3)
- [ ] **(test)** `PivotRenderTests`: `PivotResult` se zapíše do buněk od `TargetAnchor` (hlavičky, popisky, hodnoty, mezisoučty/součty, styl); oblast pivotu je „spravovaná" (uživatel needituje hodnoty ručně).
- [ ] Vytvořit `Commands/RefreshPivotCommand`, `CreatePivotCommand`, `UpdatePivotLayoutCommand`, `DeletePivotCommand` — Undo.
- [ ] JS canvas: vykreslit rozbalovací/sbalovací tlačítka (+/−) u skupin, hit-test → toggle.
- [ ] **(E2E)** pivot se vykreslí, +/− sbalí skupinu.

---

## ČÁST D — UI (13.4)

### 13D.1 Vytvoření pivotu
- [ ] **(bUnit)** `TmSpreadsheetCreatePivotDialog`: zdroj (rozsah/tabulka), umístění (nový list / existující buňka).
- [ ] Vytvořit dialog + lokalizace `TmSpreadsheet_Pivot_*`.

### 13D.2 Panel polí (Field List)
- [ ] **(bUnit)** `TmSpreadsheetPivotFieldsPanel`: seznam polí + 4 zóny (Filtry/Sloupce/Řádky/Hodnoty) s drag-and-drop, kontextové menu pole (nastavení, odebrat, přesun).
- [ ] Vytvořit panel + drag-drop (sdílet s existující DnD infrastrukturou knihovny, pokud je) + lokalizace.
- [ ] **(bUnit)** zelené.

### 13D.3 Dialogy nastavení
- [ ] **(bUnit)** `FieldSettingsDialog` (subtotaly, layout, řazení, filtr), `ValueFieldSettingsDialog` (agregace, ShowAs, formát, název), `PivotGroupDialog`, počítaná pole/položky dialogy.
- [ ] Kontextová záložka **Pivot** (analýza + návrh): Obnovit, Změnit zdroj, Pole, Seskupit, Styl, Možnosti.
- [ ] **(bUnit)** zelené.

---

## ČÁST E — GETPIVOTDATA, screenshot, XLSX
- [ ] **(test)** `GETPIVOTDATA` funkce (návaznost na Fázi 9) čte hodnoty z pivotu.
- [ ] Baseline `pivot-01-create.png`, `pivot-02-fields.png`, `pivot-03-result.png`, `pivot-04-grouping.png` + UX sign-off (přehlednost zón, drag-drop afordance, čitelnost výsledku).
- [ ] **(test)** XLSX round-trip `<pivotTableDefinition>` + `<pivotCacheDefinition>`/`records` (alespoň základní definice + cache; složité ShowAs/calculated dle možností).

---

## Definition of Done (Fáze 13)
- [ ] Pivot: zdroj+cache, oblasti filtr/řádky/sloupce/hodnoty, agregace, ShowAs, seskupování (datum/číslo/ruční), mezisoučty/součty, filtry polí, počítaná pole/položky.
- [ ] Field list s drag-drop + dialogy nastavení; kontextová záložka; +/− rozbalování v canvasu.
- [ ] GETPIVOTDATA; refresh při změně zdroje; spravovaná oblast pivotu.
- [ ] Commandy atomické + undo; Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [ ] XLSX round-trip; vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 13 na ✅.
