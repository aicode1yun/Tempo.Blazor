# Fáze 5 — Ověření dat (Data Validation)

> Stav: ✅ Hotovo · Závisí na: Fáze 1 · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Pravidla ověření vstupu na buňkách/rozsazích: seznam (dropdown v buňce), celé číslo, desetinné číslo, datum, čas, délka textu, vlastní vzorec. Vstupní zpráva (tooltip), chybové hlášení (stop/varování/info), volba „ignorovat prázdné". Vizuální indikace neplatných buněk (kroužkování).

OnlyOffice reference: `model/DataValidation.js`, `view/DataValidationDialog.js`.

---

## ČÁST A — Datový model

### 5A.1 Modely
- [x] **(test)** `SpreadsheetDataValidationTests`: `SpreadsheetDataValidation { SpreadsheetRange Range, ValidationType Type, ValidationOperator Operator, string? Formula1, string? Formula2, bool AllowBlank, bool ShowDropDown, InputMessage?, ErrorAlert? }`.
- [x] Vytvořit v `Spreadsheet/Data/`: `SpreadsheetDataValidation`, enumy `SpreadsheetValidationType { Any, Whole, Decimal, List, Date, Time, TextLength, Custom }`, `SpreadsheetValidationOperator { Between, NotBetween, Equal, NotEqual, GreaterThan, LessThan, GreaterOrEqual, LessOrEqual }`, `SpreadsheetValidationErrorStyle { Stop, Warning, Information }`, value objekty `InputMessage`, `ErrorAlert`.
- [x] Přidat `SpreadsheetSheet.DataValidations` (list) + `DeepClone()`.
- [x] **(test)** zelené.

---

## ČÁST B — Engine

### 5B.1 Vyhodnocení platnosti
- [x] **(test)** `SpreadsheetValidationEngineTests`:
  - `Whole`/`Decimal` s operátory (between/notBetween/>, < …) na typovaných hodnotách (Fáze 1).
  - `List` z literálu (`"A,B,C"`) i z odkazu na rozsah (`=$E$1:$E$10`).
  - `Date`/`Time` porovnání.
  - `TextLength` (délka řetězce).
  - `Custom` (vzorec vrací TRUE/FALSE — využít `FormulaEngine`).
  - `AllowBlank` přeskočí prázdné.
- [x] Vytvořit `Spreadsheet/Data/SpreadsheetValidationEngine.cs` → `ValidationResult Validate(cellValue, SpreadsheetDataValidation, FormulaContext)`.
- [x] **(test)** zelené pro všechny typy/operátory.

### 5B.2 Zdroje seznamu
- [x] **(test)** `ListSource_FromRange_Distinct`: dropdown hodnoty z odkazovaného rozsahu (i z jiného listu / pojmenovaného rozsahu — návaznost na Fázi 6).
- [x] **(test)** zelené.

---

## ČÁST C — Commandy

### 5C.1 Nastavení / smazání validace
- [x] **(test)** `SetDataValidationCommandTests`: přidá/aktualizuje validaci na rozsah, Undo obnoví předchozí; `ClearDataValidationCommand` odebere.
- [x] Vytvořit `Commands/SetDataValidationCommand.cs`, `ClearDataValidationCommand.cs`.
- [x] **(test)** zelené.

### 5C.2 Vynucení při zápisu
- [x] **(test)** `Commit_InvalidValue_StopStyle_Rejected`: zápis neplatné hodnoty s `Stop` se odmítne (hodnota se neuloží), vyvolá chybovou hlášku.
- [x] **(test)** `Commit_InvalidValue_WarningStyle_AsksConfirm`: u `Warning`/`Information` se uloží po potvrzení.
- [x] Zapojit validaci do commit cesty (po `SpreadsheetValueParser`, před `SetCellValueCommand`).
- [x] **(test)** zelené.

---

## ČÁST D — JS canvas rendering

### 5D.1 Dropdown indikátor a kroužkování
- [x] Rozšířit `spreadsheet-canvas.js`: u buněk s `List` + `ShowDropDown` kreslit dropdown šipku; po kliknutí C# event otevře výběr.
- [x] „Zakroužkovat neplatná data" — overlay kolem buněk, které nevyhovují (na vyžádání z menu Data).
- [x] Hit-test šipky → otevření list-popoveru.
- [x] **(E2E)** šipka se zobrazí jen u list-validace.

---

## ČÁST E — UI

### 5E.1 Dialog ověření dat
- [x] **(bUnit, failing)** `TmSpreadsheetDataValidationDialog`: 3 záložky — **Nastavení** (typ, operátor, vzorce/meze, povolit prázdné, rozbalovací seznam), **Vstupní zpráva** (titulek + text), **Chybové hlášení** (styl, titulek, text). Lokalizováno.
- [x] Vytvořit dialog `Components/Spreadsheet/TmSpreadsheetDataValidationDialog.razor(.cs)` + lokalizace `TmSpreadsheet_Validation_*`.
- [x] Pole se přizpůsobí typu (List → zdroj; Date → datová pole; Custom → jedno pole vzorce).
- [x] **(bUnit)** zelené — 65 testů prochází (`TmSpreadsheetDataValidationDialogTests` + `TmSpreadsheetValidationCommitTests`).

### 5E.2 In-cell dropdown a zprávy
- [x] **(bUnit/E2E)** in-cell dropdown vybere hodnotu; vstupní zpráva se ukáže jako tooltip při aktivaci buňky; chybová hláška jako dialog/toast dle stylu.
- [x] Tlačítka v záložce **Data**: Ověření dat…, Zakroužkovat neplatná data, Vymazat kroužky.

---

## ČÁST F — E2E, screenshot, XLSX

- [x] **(E2E)** nastavit list-validaci → in-cell dropdown → výběr; zápis mimo seznam (Stop) → odmítnuto — `SpreadsheetPhase5E2ETests.cs`.
- [x] **(E2E)** „celé číslo mezi 1 a 10" → 15 → chybová hláška — `SpreadsheetPhase5E2ETests.cs`.
- [x] Baseline `validation-01-dialog.png`, `validation-02-dropdown.png`, `validation-03-error.png` — `SpreadsheetPhase5BaselineScreenshots.cs` (generátor připraven; spustit s `[TestCategory("BaselineGeneration")]` pro vygenerování PNG).
- [x] XLSX round-trip `<dataValidations>` (typ, operátor, vzorce, zprávy) — import `XlsxImporter` + export `XlsxExporter` implementovány.

---

## Definition of Done (Fáze 5)
- [x] Všechny typy validace + operátory + zdroje seznamu (literál/rozsah/pojmenovaný).
- [x] Vstupní zpráva + chybové hlášení (Stop/Warning/Info), ignorovat prázdné.
- [x] In-cell dropdown + kroužkování neplatných, vynucení při zápisu.
- [x] Commandy atomické + undo; validace zapojena do commit cesty.
- [x] Unit + bUnit + E2E + screenshoty zelené (baseline generátor připraven).
- [x] XLSX round-trip; vše lokalizováno; žádné placeholdery.
- [x] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 5 na ✅.
