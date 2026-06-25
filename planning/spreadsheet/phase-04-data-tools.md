# Fáze 4 — Datové nástroje: Odebrat duplicity + Text do sloupců + Speciální vložení

> Stav: ✅ Hotovo (2026-06-05) · Závisí na: Fáze 1 · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Tři datové nástroje:
- **Odebrat duplicity**: z rozsahu odstraní duplicitní řádky podle vybraných sloupců.
- **Text do sloupců** (AdvancedSeparatorDialog): rozdělí text v jednom sloupci do více sloupců dle oddělovače / pevné šířky, s náhledem a typovou detekcí.
- **Speciální vložení**: vložit jen hodnoty / formáty / vzorce / bez ohraničení / transponovat / aritmetické operace.

OnlyOffice reference: `view/RemoveDuplicatesDialog.js`, `view/AdvancedSeparatorDialog.js`, `view/SpecialPasteDialog.js`, `model/clipboard.js`.

---

## ČÁST A — Odebrat duplicity

### 4A.1 Engine
- [x] **(test)** `SpreadsheetDeduplicateTests`: pro rozsah + seznam klíčových sloupců + `HasHeader` vrátí indexy řádků k odstranění (ponechá první výskyt); porovnání respektuje typy (Fáze 1) a volbu citlivosti na velikost.
- [x] Vytvořit `Spreadsheet/Data/SpreadsheetDeduplicate.cs` (Abstractions).
- [x] **(test)** zelené (vč. „žádné duplicity", „vše stejné", prázdné buňky).

### 4A.2 Command
- [x] **(test)** `RemoveDuplicatesCommandTests`: odstraní duplicitní řádky (posun zbývajících), Undo přesně obnoví; vrací počet odstraněných.
- [x] Vytvořit `Commands/RemoveDuplicatesCommand.cs` (atomický `BatchCommand` interně).
- [x] **(test)** zelené.

### 4A.3 Dialog + UI
- [x] **(bUnit, failing)** `TmSpreadsheetRemoveDuplicatesDialog`: seznam sloupců se zaškrtávátky, „Moje data mají záhlaví", Vybrat vše/Zrušit výběr, OK/Zrušit — lokalizováno.
- [x] Vytvořit dialog + lokalizace `TmSpreadsheet_Dedup_*`.
- [x] Tlačítko v záložce **Data**.
- [x] Po dokončení toast/hláška „Odstraněno N duplicit, ponecháno M unikátních" (lokalizováno, plural-aware).
- [x] **(bUnit)** zelené.

### 4A.4 E2E + screenshot
- [x] **(E2E)** označit rozsah → Odebrat duplicity → správný počet řádků zmizí.
- [x] Baseline `dedup-01-dialog.png` + UX sign-off.

---

## ČÁST B — Text do sloupců (AdvancedSeparator)

### 4B.1 Parser rozdělení
- [x] **(test)** `SpreadsheetTextToColumnsTests.Delimited`: rozdělí dle oddělovače (tab/`;`/`,`/mezera/vlastní), s ošetřením uvozovek (text qualifier) a po sobě jdoucích oddělovačů.
- [x] **(test)** `FixedWidth`: rozdělí dle pevných pozic zlomů.
- [x] **(test)** `WithTypeDetection`: výsledné buňky projdou `SpreadsheetValueParser` (Fáze 1), nebo se sloupci vnutí typ Text dle volby.
- [x] Vytvořit `Spreadsheet/Data/SpreadsheetTextToColumns.cs` → `IReadOnlyList<IReadOnlyList<string>> Split(string[] rows, SeparatorOptions)`.
- [x] **(test)** zelené.

### 4B.2 Command
- [x] **(test)** `TextToColumnsCommandTests`: zapíše rozdělené hodnoty do cílových sloupců (s varováním/přepisem dat vpravo), Undo obnoví původní jeden sloupec.
- [x] Vytvořit `Commands/TextToColumnsCommand.cs`.
- [x] **(test)** zelené.

### 4B.3 Průvodce dialog (s náhledem)
- [x] **(bUnit, failing)** `TmSpreadsheetTextToColumnsDialog`: krok 1 typ (oddělovač/pevná šířka), krok 2 oddělovače + náhled tabulky, krok 3 formát sloupců (obecný/text/datum/přeskočit). Náhled se aktualizuje živě.
- [x] Vytvořit vícekrokový dialog `Components/Spreadsheet/Dialogs/TmSpreadsheetTextToColumnsDialog.razor(.cs/.css)` + lokalizace `TmSpreadsheet_TextToColumns_*`.
- [x] **(bUnit)** zelené (přepínání kroků, živý náhled).

### 4B.4 E2E + screenshot
- [x] **(E2E)** sloupec `"Jan;Novák;Praha"` → rozdělí na 3 sloupce.
- [x] Baseline `t2c-01-step2-preview.png` + UX sign-off (čitelnost náhledu, indikace zlomů u pevné šířky).

---

## ČÁST C — Speciální vložení

### 4C.1 Rozšíření clipboardu o obsah s metadaty
- [x] **(test)** `SpreadsheetClipboardTests`: schránka drží hodnoty, vzorce, styly a rozměry rozsahu (ne jen text) — ověřit, že `SpreadsheetClipboard` umí poskytnout jednotlivé složky.
- [x] Rozšířit `SpreadsheetClipboard` o strukturovaný snapshot rozsahu (pokud zatím drží jen text).
- [x] **(test)** zelené.

### 4C.2 PasteSpecial engine + command
- [x] **(test)** `PasteSpecialCommandTests`:
  - `ValuesOnly` (bez vzorců/stylů), `FormulasOnly`, `FormatsOnly`, `ValuesAndFormats`, `WithoutBorders`.
  - `Transpose` (prohodí řádky/sloupce).
  - `Operation` (Add/Subtract/Multiply/Divide vůči cílovým hodnotám).
  - `SkipBlanks`.
  - Každá varianta + Undo.
- [x] Vytvořit `Commands/PasteSpecialCommand.cs` (parametrizovaný `PasteSpecialOptions`).
- [x] **(test)** zelené pro všechny varianty.

### 4C.3 Dialog + UI
- [x] **(bUnit)** `TmSpreadsheetSpecialPasteDialog`: radiové volby (co vložit), zaškrtávátka (transponovat, přeskočit prázdné), operace; lokalizováno.
- [x] Vytvořit dialog + lokalizace `TmSpreadsheet_PasteSpecial_*`.
- [ ] „Paste options" mini-overlay po běžném vložení (jako Excel/OnlyOffice) → rychlá volba (Hodnoty/Formáty/…); volitelné, ale UX plus. *(neimplementováno — volitelný rozšiřující prvek; plnohodnotný dialog Speciální vložení je hotový vč. Ctrl+Shift+V.)*
- [x] Klávesová zkratka `Ctrl+Shift+V`.
- [x] **(bUnit)** zelené.

### 4C.4 E2E + screenshot
- [x] **(E2E)** zkopírovat rozsah se vzorci → Speciální vložení → „jen hodnoty" → cíl má hodnoty, ne vzorce.
- [x] **(E2E)** transpozice prohodí orientaci.
- [x] Baseline `pastespecial-01-dialog.png` + UX sign-off.

---

## Definition of Done (Fáze 4)
- [x] Odebrat duplicity: engine + command (undo) + dialog + E2E.
- [x] Text do sloupců: delimited i fixed-width, živý náhled, typová detekce, undo.
- [x] Speciální vložení: všechny varianty (hodnoty/formáty/vzorce/transpozice/operace/skip blanks), undo, `Ctrl+Shift+V`.
- [x] Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS u všech tří.
- [x] Vše lokalizováno (vč. plural-aware hlášek), žádné placeholdery.
- [x] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 4 na ✅.
