# Fáze 11 — Pojmenované pohledy listu

> Stav: ☐ Neza­počato · Závisí na: Fáze 3 (filtr/řazení) · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Uložené „pohledy" listu (jako Excel Sheet Views / OnlyOffice Named Sheet Views): snímek stavu **filtru, řazení, skrytých řádků/sloupců, šířek/výšek, zmrazení, zoomu a aktivního výběru**. Uživatel může mezi pohledy přepínat, vytvořit/duplikovat/přejmenovat/smazat, a mít „výchozí" pohled. Klíčové pro vícero uživatelů (v co-editaci si každý drží svůj pohled — návaznost na Fázi 17).

OnlyOffice reference: `model/NamedSheetViews.js`, `view/ViewManagerDlg.js`, `controller/ViewTab.js`.

---

## ČÁST A — Datový model

### 11A.1 Snímek pohledu
- [ ] **(test)** `SpreadsheetSheetViewTests`: `SpreadsheetSheetView { string Name, bool IsDefault, SheetViewState State }` kde `SheetViewState` drží: `AutoFilter?` (kopie), `SortSpec?`, `HiddenRows`, `HiddenColumns`, `ColumnWidths`, `RowHeights`, `FreezeRow/Col`, `Zoom`, `ActiveCellRef`, `Selection`.
- [ ] Vytvořit modely v `Spreadsheet/Models/`; přidat `SpreadsheetSheet.Views` (list) + `ActiveViewName`.
- [ ] **(test)** zelené.

### 11A.2 Snímání a aplikace stavu
- [ ] **(test)** `CaptureView_FromSheet`: vytvoří `SheetViewState` z aktuálního listu.
- [ ] **(test)** `ApplyView_ToSheet`: nastaví list dle uloženého stavu (filtr/řazení/skrytí/rozměry/freeze/zoom/výběr).
- [ ] Vytvořit `Spreadsheet/Data/SpreadsheetSheetViewService.cs` (Capture/Apply).
- [ ] **(test)** zelené (round-trip Capture→Apply = identita).

---

## ČÁST B — Commandy
- [ ] **(test)** `CreateSheetViewCommand` (z aktuálního stavu), `ApplySheetViewCommand`, `DuplicateSheetViewCommand`, `RenameSheetViewCommand`, `DeleteSheetViewCommand`, `SetDefaultSheetViewCommand` — vše Undo.
- [ ] Vytvořit commandy.
- [ ] **(test)** zelené.

---

## ČÁST C — UI

### 11C.1 View přepínač + správce
- [ ] **(bUnit, failing)** `TmSpreadsheetSheetViewSwitcher`: dropdown se seznamem pohledů + „Nový pohled", indikace aktivního; ve stavovém řádku nebo v záložce **Zobrazení**.
- [ ] **(bUnit)** `TmSpreadsheetViewManagerDialog`: tabulka pohledů (název, výchozí), tlačítka Nový/Duplikovat/Přejmenovat/Smazat/Nastavit výchozí.
- [ ] Vytvořit komponenty + lokalizace `TmSpreadsheet_View_*`.
- [ ] **(bUnit)** zelené.

### 11C.2 Záložka Zobrazení (View)
- [ ] Přidat do toolbaru záložku **Zobrazení**: Pohledy listu (switcher + správce), Zmrazit panely (přesunout sem existující freeze), Mřížka (přesunout), Zoom (sdílí Fázi 2), Záhlaví řádků/sloupců toggle.
- [ ] Lokalizace.

---

## ČÁST D — E2E, screenshot
- [ ] **(E2E)** vytvořit pohled s aktivním filtrem + zmrazením → změnit list → přepnout zpět na pohled → stav obnoven.
- [ ] Baseline `views-01-switcher.png`, `views-02-manager.png` + UX sign-off (srozumitelnost přepínání, indikace neuložených změn pohledu).
- [ ] **(test)** XLSX round-trip (custom sheet views / OnlyOffice formát; pokud XLSX nepokrývá vše, uložit do interního formátu + zdokumentovat).

---

## Definition of Done (Fáze 11)
- [ ] Pohledy ukládají a obnovují filtr/řazení/skrytí/rozměry/freeze/zoom/výběr.
- [ ] Switcher + správce + výchozí pohled; záložka Zobrazení sjednocuje view nástroje.
- [ ] Commandy atomické + undo; Capture↔Apply round-trip.
- [ ] Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [ ] Vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 11 na ✅.
