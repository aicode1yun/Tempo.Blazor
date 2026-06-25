# Fáze 10 — Ochrana (list / sešit + chráněné rozsahy)

> Stav: ☐ Neza­počato · Závisí na: Fáze 0 · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
- **Zámek listu**: zakázat úpravy, s výjimkou povolených akcí (výběr buněk, formátování, řazení, filtr, vkládání řádků…), heslo (volitelné).
- **Zámek sešitu**: zamknout strukturu (přidávání/mazání/přejmenování listů).
- **Chráněné rozsahy**: per-rozsah oprávnění (kdo/heslo smí editovat) i v zamčeném listu.
- **Zámek buňky** (`Locked`/`Hidden` ve stylu) — předpoklad pro zámek listu (zamčené se vynucuje jen na chráněném listu).

> ⚠️ Bezpečnostní rámec: ochrana v tabulkovém editoru je **proti náhodným úpravám**, ne kryptografická bezpečnost (jako Excel). Heslo se ověřuje klientsky; pro reálné zabezpečení musí host řešit autorizaci. Toto **explicitně zdokumentovat** ve veřejném API.

OnlyOffice reference: `model/WorkbookProtection.js`, `model/protectRange.js`, `controller/WBProtection.js`, `view/ProtectDialog.js`, `view/ProtectRangesDlg.js`, `view/ProtectedRangesManagerDlg.js`, `view/ProtectedRangesEditDlg.js`.

---

## ČÁST A — Datový model

### 10A.1 Zámek buňky (style)
- [ ] **(test)** `SpreadsheetCellStyleTests.LockedHidden`: `SpreadsheetCellStyle.Locked` (default true) a `HiddenFormula` (default false).
- [ ] Přidat pole do `SpreadsheetCellStyle` + `Clone()` + (XLSX) protection atributy.
- [ ] **(test)** zelené.

### 10A.2 Ochrana listu
- [ ] **(test)** `SpreadsheetSheetProtectionTests`: `SpreadsheetSheetProtection { bool Enabled, string? PasswordHash, SheetProtectionOptions Allow }` (Allow = selectLocked, selectUnlocked, formatCells, formatCols, formatRows, insertCols, insertRows, insertHyperlinks, deleteCols, deleteRows, sort, autoFilter, pivot, objects, scenarios).
- [ ] Vytvořit modely v `Spreadsheet/Models/`; přidat `SpreadsheetSheet.Protection`.
- [ ] **(test)** zelené.

### 10A.3 Ochrana sešitu + chráněné rozsahy
- [ ] **(test)** `WorkbookProtectionTests`: `SpreadsheetWorkbookProtection { bool LockStructure, bool LockWindows, string? PasswordHash }`.
- [ ] **(test)** `ProtectedRangeTests`: `SpreadsheetProtectedRange { string Title, SpreadsheetRange Range, string? PasswordHash, List<string> AllowedUsers }`.
- [ ] Přidat `SpreadsheetWorkbook.Protection`, `SpreadsheetSheet.ProtectedRanges`.
- [ ] **(test)** zelené.

### 10A.4 Hash hesla
- [ ] **(test)** `PasswordHashTests`: ověření hesla proti hashi; prázdné heslo = bez hesla. Použít standardní XLSX algoritmus (sheetProtection hash) pro round-trip kompatibilitu.
- [ ] Vytvořit `Spreadsheet/Models/SpreadsheetPasswordHash.cs`.
- [ ] **(test)** zelené.

---

## ČÁST B — Engine (vynucení)

### 10B.1 Rozhodování o povolení akce
- [ ] **(test)** `ProtectionPolicyTests`: `CanEditCell(sheet, cellRef, user)` → false na zamčené buňce chráněného listu, true na odemčené nebo v povoleném chráněném rozsahu; `CanSort/CanFilter/CanInsertRow/…` dle `Allow`.
- [ ] Vytvořit `Spreadsheet/Data/SpreadsheetProtectionPolicy.cs`.
- [ ] **(test)** zelené.

### 10B.2 Zapojení do commit/command cesty
- [ ] **(test)** `Commit_OnLockedProtectedCell_Rejected`: zápis se odmítne + lokalizovaná hláška.
- [ ] **(test)** `InsertRow_WhenNotAllowed_Rejected`, `Sort_WhenNotAllowed_Rejected`.
- [ ] Vložit kontrolu politiky před prováděním příslušných commandů (centrálně v `SpreadsheetCommandManager` nebo na vstupních bodech).
- [ ] **(test)** zelené.

---

## ČÁST C — Commandy
- [ ] **(test)** `ProtectSheetCommand` / `UnprotectSheetCommand` (s ověřením hesla), `ProtectWorkbookCommand` / `UnprotectWorkbookCommand`, `AddProtectedRangeCommand` / `EditProtectedRangeCommand` / `DeleteProtectedRangeCommand` — vše Undo.
- [ ] Vytvořit commandy.
- [ ] **(test)** zelené.

---

## ČÁST D — JS canvas rendering
- [ ] Vizuální náznak zamčení (volitelně ikonka/odlišný kurzor) u zamčených buněk na chráněném listu; chráněné rozsahy jemně odlišit.
- [ ] Zablokovat inline editaci zamčených buněk v enginu (na chráněném listu) + hláška.
- [ ] **(E2E)** klik+psaní do zamčené buňky → odmítnuto s hláškou.

---

## ČÁST E — UI

### 10E.1 Dialogy
- [ ] **(bUnit)** `TmSpreadsheetProtectSheetDialog`: heslo (volitelné) + seznam povolených akcí (zaškrtávátka). Lokalizováno.
- [ ] **(bUnit)** `TmSpreadsheetProtectWorkbookDialog`: struktura/okna + heslo.
- [ ] **(bUnit)** `TmSpreadsheetProtectedRangesManagerDialog` + `…EditDialog`: název, rozsah (výběr z gridu), heslo/uživatelé.
- [ ] Vytvořit dialogy + lokalizace `TmSpreadsheet_Protect_*`.
- [ ] Záložka/menu **Revize/Ochrana**: Zamknout list, Zamknout sešit, Povolit úpravy rozsahů…, Zamknout/odemknout buňku.
- [ ] **(bUnit)** zelené.

---

## ČÁST F — E2E, screenshot, XLSX
- [ ] **(E2E)** zamknout list (odemknout vstupní sloupec) → editace odemčených OK, zamčených zakázána; odemknout heslem.
- [ ] **(E2E)** chráněný rozsah s heslem → editace po zadání hesla.
- [ ] Baseline `protect-01-sheet-dialog.png`, `protect-02-ranges.png` + UX sign-off.
- [ ] **(test)** XLSX round-trip `<sheetProtection>`, `<protectedRanges>`, `<workbookProtection>` + cell `Locked/Hidden`.

---

## Definition of Done (Fáze 10)
- [ ] Zámek listu (s výjimkami + heslo), zámek sešitu, chráněné rozsahy (heslo/uživatelé), zámek buňky.
- [ ] Vynucení v commit/command cestě + v JS enginu; lokalizované hlášky.
- [ ] Bezpečnostní omezení zdokumentováno ve veřejném API.
- [ ] Commandy atomické + undo; Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [ ] XLSX round-trip; vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 10 na ✅.
