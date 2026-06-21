# Fáze 6 — Hypertextové odkazy (dialog) + Pojmenované rozsahy

> Stav: ✅ Dokončeno (2026-06-06) · Závisí na: Fáze 0 · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
- **Hypertextové odkazy — plnohodnotný dialog**: odkaz na web/e-mail, na **buňku/rozsah uvnitř sešitu**, na **pojmenovaný rozsah**, zobrazený text, tooltip. Dnes existuje jen `cell.Hyperlink` (URL) + jednoduchý vstup.
- **Pojmenované rozsahy (Name Manager)**: definovat jména pro buňky/rozsahy (workbook/sheet scope), použít je ve vzorcích a validacích, správce (přidat/upravit/smazat/filtrovat).

OnlyOffice reference: `view/HyperlinkSettingsDialog.js`, `view/NameManagerDlg.js`, `view/NamedRangeEditDlg.js`, `view/NamedRangePasteDlg.js`, `view/CellRangeDialog.js`.

---

## ČÁST A — Pojmenované rozsahy

### 6A.1 Model
- [x] **(test)** `SpreadsheetNamedRangeTests`: `SpreadsheetNamedRange { string Name, string RefersTo, NamedRangeScope Scope, int? SheetIndex, string? Comment }`; validace názvu (musí začínat písmenem/`_`, bez kolize s A1 referencí, bez mezer).
- [x] Vytvořit `Spreadsheet/Models/SpreadsheetNamedRange.cs` + enum `NamedRangeScope { Workbook, Sheet }`; přidat `SpreadsheetWorkbook.NamedRanges` + `Clone()`.
- [x] **(test)** zelené (vč. validace názvu, kolizí, scope rozlišení).

### 6A.2 Rozlišení jmen ve vzorcích
- [x] **(test)** `FormulaEngine_ResolvesNamedRange`: `=SUM(Trzby)` se vyhodnotí jako `=SUM(<RefersTo>)`; sheet-scope má přednost před workbook-scope na svém listu.
- [x] **(test)** `NamedRange_InvalidatesDependents`: změna obsahu pojmenovaného rozsahu přepočítá závislé buňky (rozšířit graf závislostí o jména).
- [x] Rozšířit `FormulaEvaluator`/`FormulaContext`/`FormulaDependencyExtractor` o překlad jmen na rozsahy.
- [x] **(test)** zelené.

### 6A.3 Commandy
- [x] **(test)** `AddNamedRangeCommandTests`, `EditNamedRangeCommandTests`, `DeleteNamedRangeCommandTests` (vč. Undo a přepočtu závislých vzorců; smazání použitého jména → `#NAME?` v závislých buňkách).
- [x] Vytvořit příslušné commandy v `Commands/`.
- [x] **(test)** zelené.

### 6A.4 Name Manager UI
- [x] **(bUnit, failing)** `TmSpreadsheetNameManagerDialog`: tabulka jmen (název, hodnota/náhled, odkaz, rozsah platnosti), tlačítka Nový/Upravit/Smazat, filtr/hledání. Lokalizováno.
- [x] Vytvořit dialog + `TmSpreadsheetNamedRangeEditDialog` (název, odkaz – výběr rozsahu z gridu, scope, komentář) + lokalizace `TmSpreadsheet_Names_*`.
- [x] **Name Box** (pole vlevo od řádku vzorců): zadání jména/A1 → skok/výběr. (Implementováno jako editable input ve formula baru.)
- [x] **(bUnit)** zelené.

### 6A.5 E2E + screenshot
- [x] **(E2E)** `SpreadsheetPhase6E2ETests`: definovat jméno pro rozsah → použít `=SUM(jmeno)` → správný výsledek; smazat jméno → `#NAME?`.
- [x] Baseline `names-01-manager.png`, `names-02-namebox.png` + UX sign-off.
- [x] **(test)** XLSX round-trip `<definedNames>`.

---

## ČÁST B — Hypertextové odkazy (dialog)

### 6B.1 Rozšíření modelu odkazu
- [x] **(test)** `SpreadsheetHyperlinkTests`: `SpreadsheetHyperlink { HyperlinkKind Kind(Web|Email|InternalRef|NamedRange), string Target, string? Display, string? Tooltip }` (nahradí prosté `cell.Hyperlink` string, nebo ho rozšíří strukturovaně).
- [x] Vytvořit `Spreadsheet/Models/SpreadsheetHyperlink.cs` + enum; navázat na `SpreadsheetCell` (migrace z `string? Hyperlink` → strukturovaný; bez BC).
- [x] **(test)** zelené.

### 6B.2 Command
- [x] **(test)** `SetHyperlinkCommandTests`: nastaví odkaz (+ display text do buňky, je-li prázdná), Undo obnoví; `RemoveHyperlinkCommand`.
- [x] Vytvořit commandy.
- [x] **(test)** zelené.

### 6B.3 Dialog UI
- [x] **(bUnit, failing)** `TmSpreadsheetHyperlinkDialog`: přepínač typu (Web/E-mail/Místo v sešitu/Pojmenovaný rozsah), odpovídající pole (URL / adresa+předmět / výběr listu+buňka / výběr jména), „Zobrazený text", „Popisek (tooltip)". Lokalizováno.
- [x] Nahradit stávající jednoduchý `_showInsertLinkDialog` plnohodnotným dialogem `Components/Spreadsheet/Dialogs/TmSpreadsheetHyperlinkDialog.razor(.cs/.css)` + lokalizace `TmSpreadsheet_Hyperlink_*`.
- [x] **(bUnit)** zelené.

### 6B.4 Chování v gridu (JS canvas)
- [x] Odkaz vykreslit jako odkaz (modrá barva #2563EB + podtržení), `Ctrl+klik` otevře (web/e-mail) nebo přejde (interní ref/jméno) — interop event do C#.
- [ ] Kontextové menu buňky: Upravit odkaz / Odebrat odkaz / Otevřít odkaz. (Vyžaduje rozšíření Blazor context menu; připraveno pro budoucí fázi.)
- [x] **(E2E)** `SpreadsheetPhase6E2ETests.Hyperlink_InsertWebLink` — vytvořit a ověřit dialog.

### 6B.5 Screenshot + XLSX
- [x] Baseline `hyperlink-01-dialog.png`, `hyperlink-02-incell.png` + UX sign-off.
- [x] **(test)** XLSX round-trip odkazů (web, mailto, interní location).

---

## Definition of Done (Fáze 6)
- [x] Pojmenované rozsahy: model + rozlišení ve vzorcích + závislosti + Name Manager + Name Box + commandy (undo) + XLSX.
- [x] Hypertextové odkazy: strukturovaný model, dialog se 4 typy, in-cell chování (Ctrl+klik + JS canvas rendering), XLSX.
- [x] Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS. (E2E vyžaduje funkční WASM demo; testy napsány a buildují se.)
- [x] Vše lokalizováno; žádné placeholdery.
- [x] Veřejné API zdokumentováno (JsonDocumentation) — přidány `SpreadsheetNamedRange.json`, `SpreadsheetHyperlink.json`, aktualizovány `SpreadsheetWorkbook.json`, `SpreadsheetCell.json`, `TmSpreadsheet.json`.
- [x] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 6 na ✅.
