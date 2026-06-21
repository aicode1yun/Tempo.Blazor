# Fáze 14 — Slicery

> Stav: ☐ Neza­počato · Závisí na: Fáze 8 (tabulky), 13 (pivoty) · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Vizuální filtry (slicery) jako plovoucí ovládací prvky vázané na **formátovanou tabulku** nebo **pivot**: tlačítka s hodnotami pole, výběr jedné/více položek, vícesloupcové rozložení, styl, „vymazat filtr", indikace položek bez dat. Změna sliceru aktualizuje filtr cílové tabulky/pivotu.

OnlyOffice reference: `model/Slicer.js`, `view/SlicerSettings.js`, `view/SlicerSettingsAdvanced.js`, `view/SlicerAddDialog.js`.

---

## ČÁST A — Datový model

### 14A.1 Model sliceru
- [ ] **(test)** `SpreadsheetSlicerTests`: `SpreadsheetSlicer { string Name, string Caption, SlicerSource(TableName|PivotName), int FieldIndex, ISet<string> SelectedItems, bool MultiSelect, SlicerLayout (Columns, Width, Height, Position float), SlicerStyle, bool ShowHeader, bool HideItemsWithNoData, SortOrder }`.
- [ ] Vytvořit modely v `Spreadsheet/Models/`; přidat `SpreadsheetSheet.Slicers`.
- [ ] **(test)** zelené.

---

## ČÁST B — Engine

### 14B.1 Položky a aplikace filtru
- [ ] **(test)** `SlicerEngineTests.Items`: vrátí unikátní položky pole z cílové tabulky/pivot cache (řazené, s příznakem „má data").
- [ ] **(test)** `Apply_SelectionFiltersTarget`: výběr položek → aktualizace filtru cílové tabulky (Fáze 3) nebo pivotu (Fáze 13); prázdný výběr = vše.
- [ ] **(test)** `CrossSlicer`: více slicerů na téže tabulce kombinuje filtry (AND mezi poli).
- [ ] Vytvořit `Spreadsheet/Data/SpreadsheetSlicerEngine.cs`.
- [ ] **(test)** zelené.

---

## ČÁST C — Commandy
- [ ] **(test)** `AddSlicerCommand`, `UpdateSlicerSelectionCommand`, `MoveResizeSlicerCommand`, `SetSlicerStyleCommand`, `DeleteSlicerCommand` — vše Undo.
- [ ] Vytvořit commandy.
- [ ] **(test)** zelené.

---

## ČÁST D — JS canvas rendering / overlay
- [ ] Slicer je **plovoucí overlay** nad gridem (jako objekt). Rozhodnout: HTML overlay komponenta pozicovaná nad canvasem (doporučeno pro interaktivitu/přístupnost) vs. kreslení v canvasu.
- [ ] **(bUnit)** `TmSpreadsheetSlicer` komponenta: hlavička (caption + vymazat filtr), seznam tlačítek položek (toggle), vícesloupcové rozložení, scroll; drží se ukotvená při scrollu gridu.
- [ ] Drag (přesun) a resize úchyty; výběr/oddělení od buněk.
- [ ] **(E2E)** klik na položku filtruje tabulku; více položek; vymazat filtr.

---

## ČÁST E — UI
- [ ] **(bUnit)** `TmSpreadsheetInsertSlicerDialog`: výběr pole/polí cílové tabulky/pivotu (zaškrtávátka).
- [ ] **(bUnit)** `TmSpreadsheetSlicerSettings`: caption, sloupce, styl, řazení, skrýt položky bez dat, „vícenásobný výběr".
- [ ] Kontextová záložka **Slicer** (při výběru) + tlačítko „Vložit slicer" (záložka Vložit / Pivot / Tabulka).
- [ ] Lokalizace `TmSpreadsheet_Slicer_*`.
- [ ] **(bUnit)** zelené.

---

## ČÁST F — Screenshot + XLSX
- [ ] Baseline `slicer-01-insert.png`, `slicer-02-active.png`, `slicer-03-multi.png` + UX sign-off (afordance tlačítek, stav vybráno/nevybráno/bez dat, plovoucí chování při scrollu, kontrast).
- [ ] **(test)** XLSX round-trip `<slicer>` / `<slicerCache>` (vazba na tabulku/pivot, výběr).

---

## Definition of Done (Fáze 14)
- [ ] Slicery vázané na tabulku i pivot; výběr jedné/více položek; kombinace více slicerů.
- [ ] Plovoucí overlay s přesunem/resize, ukotvený při scrollu; styl + rozložení sloupců; skrýt položky bez dat.
- [ ] Commandy atomické + undo; Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [ ] XLSX round-trip; vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 14 na ✅.
