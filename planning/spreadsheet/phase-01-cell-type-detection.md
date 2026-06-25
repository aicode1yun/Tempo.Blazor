# Fáze 1 — A5: Automatické rozpoznávání obsahu buňky

> Stav: ✅ HOTOVO (507 spreadsheet testů zelených; type detection ověřena naživo v demu, UX sign-off PASS) · Závisí na: Fáze 0 · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Stav implementace (2026-06-05)
- ✅ **1.1** `SpreadsheetDataType` rozšířen o `Percentage`, `Currency`.
- ✅ **1.2** `SpreadsheetParsedValue` (readonly record struct) v Abstractions.
- ✅ **1.3–1.10** `SpreadsheetValueParser.Parse(input, culture)` — apostrof→vzorec→bool→procenta→měna→číslo→datum/čas→text, vše culture-aware (robustní normalizace group separátorů vč. nbsp/narrow-nbsp). **32 unit testů.**
- ✅ **1.11** Oprava `SpreadsheetNumberFormatter` — kontextové rozlišení `m`/`mm` (měsíc vs. minuty) + 24h hodiny; data/časy se zobrazují správně. Round-trip testy.
- ✅ **1.12** Zapojení do commit cesty: `SetCellValueCommand` rozšířen o `dataType` + `impliedNumberFormat` (aplikuje se jen na `General`, Undo obnoví formát i DataType). Sdílený helper `BuildCommit` v `TmSpreadsheet` napojen na `OnGridCellValueCommitted`, `OnGridCellValuesCommittedBatch`, `ApplyValueToActiveCellAsync` i veřejné `SetCellValue`. Unit + bUnit testy.
- ✅ **1.13** `SpreadsheetCellEditText.GetEditText` (procenta→„50%", datum→re-parseable, číslo→kanonicky bez tisíců, bool→„TRUE") napojen na formula bar (`GetActiveCellEditValue`) i canvas `StartEdit`. Unit testy.
- ✅ **1.14** Canvas zarovnání dle typu funguje (typované hodnoty protékají existujícím `GetEffectiveHAlign`; doplněno `DateTime → vpravo`). Ověřeno na screenshotu.
- ✅ **1.16** XLSX round-trip typů (double/bool/DateTime se exportují se správným XLSX typem; parserem produkované hodnoty round-trippují). Test `Xlsx_TypedValues_RoundTrip`.
- ✅ **1.17** bUnit (commit přes veřejné API ověřuje typ + implikovaný formát).
- ✅ **1.18** Baseline `typedetect-01-column.png` + **UX sign-off PASS** (čísla/datum/měna vpravo, bool na střed, text vlevo, „50%", „$10", „2/1/2024").
- ⚠️ **1.15 částečně:** interní copy/paste **zachovává typy** (přes `Clone()` Value+DataType+Style) ✓; AutoFill pracuje s typovanými hodnotami ✓. **Parsování externího TSV textu po buňkách** (vložení z mimo-appky) je v JS clipboard cestě — **odloženo** (neblokuje jádro A5, doplní se s plnou clipboard funkcionalitou).
- 📌 Drobnost: výchozí velikost písma toolbaru „8" (pre-existing, mimo rozsah f.1).

## Cíl & rozsah
Při psaní do buňky rozpoznat **typ vstupu** a uložit **typovanou hodnotu** + případně **implikovaný číselný formát**, místo dnešního ukládání surového stringu. Důsledek: čísla se zobrazí jako čísla a zarovnají vpravo, `50%` → 0.5 s formátem `0%`, data → serial number s datovým formátem, měna, boolean, vynucený text přes apostrof. Vše **culture-aware** (cs-CZ čárka vs. invariant).

OnlyOffice reference: chování CellEditoru při potvrzení vstupu (typová detekce + implicitní formát). Dnešní bolest: `SetCellValueCommand` dostává `value?.ToString()`, `DataType` zůstává `Text`.

Klíčové existující prvky, na které navazujeme:
- `SpreadsheetDataType` (Number/Text/Boolean/Date/Time/DateTime/Error) — rozšíříme o `Percentage`, `Currency`.
- `SpreadsheetNumberFormatter` umí formátovat `double`/`DateTime` (jen nedostával typované hodnoty).
- `ToolbarAlign` už řeší `double → right`, `bool → center` (logika existuje, jen nedostávala typ).
- `FormulaDecimalSeparator`/`FormulaArgumentSeparator` v `TmSpreadsheet.razor.cs` (culture).

---

## 1.1 Rozšíření datového typu
- [x] **(test)** `SpreadsheetDataTypeTests`: ověřit existenci hodnot `Percentage`, `Currency` (failing).
- [x] Přidat do `Enums/SpreadsheetDataType.cs` hodnoty `Percentage`, `Currency` (XML doc komentáře).
- [x] **(test)** zelený.

## 1.2 Výsledkový model parseru (Abstractions)
- [x] **(test)** `SpreadsheetParsedValueTests`: `SpreadsheetParsedValue` drží `object? Value`, `SpreadsheetDataType Type`, `string? Formula`, `string? ImpliedNumberFormat`, `bool IsForcedText`.
- [x] Vytvořit `Spreadsheet/Format/SpreadsheetParsedValue.cs` (immutable `readonly record struct` nebo sealed class) v Abstractions.
- [x] **(test)** zelený.

## 1.3 SpreadsheetValueParser — kostra + kontrakt
- [x] **(test)** `SpreadsheetValueParserTests.Parse_Null_ReturnsEmptyText`: `Parse(null, culture)` → Type=Text, Value=null.
- [x] Vytvořit `Spreadsheet/Format/SpreadsheetValueParser.cs` se signaturou `static SpreadsheetParsedValue Parse(string? input, CultureInfo culture)`.
- [x] **(test)** zelený minimální implementací (vrátí Text).

## 1.4 Pravidlo: vzorec
- [x] **(test)** `Parse_StartsWithEquals_ReturnsFormula`: `"=A1+1"` → Type odpovídá vzorci, `Formula="=A1+1"`, Value=null.
- [x] Implementovat: vstup začínající `=` (a delší než 1 znak) → Formula.
- [x] **(test)** zelený.

## 1.5 Pravidlo: vynucený text (apostrof)
- [x] **(test)** `Parse_LeadingApostrophe_ForcesText`: `"'0123"` → Type=Text, Value=`"0123"` (apostrof odstraněn), `IsForcedText=true`.
- [x] **(test)** `Parse_LeadingApostrophe_KeepsLeadingZeros` a `Parse_LeadingApostrophe_NumberLikeStaysText`: `"'=SUM(A1)"` → text, ne vzorec.
- [x] Implementovat odstranění úvodního apostrofu + příznak.
- [x] **(test)** zelené.

## 1.6 Pravidlo: boolean
- [x] **(test)** `Parse_TrueFalse_CaseInsensitive_ReturnsBoolean` (`"TRUE"`,`"true"`,`"False"` → Boolean s `bool` Value). Lokalizované? → držet EN literály TRUE/FALSE (Excel kompatibilita), neřešit lokalizované boolean stringy.
- [x] Implementovat.
- [x] **(test)** zelený.

## 1.7 Pravidlo: procenta
- [x] **(test)** `Parse_Percentage_StoresFraction`: `"50%"` → Type=Percentage, Value=`0.5` (double), `ImpliedNumberFormat="0%"`.
- [x] **(test)** `Parse_Percentage_Decimals`: `"12.5%"` (invariant) a `"12,5%"` (cs-CZ) → 0.125, `ImpliedNumberFormat="0.0%"` (počet desetinných míst dle vstupu).
- [x] Implementovat (parse čísla před `%`, děleno 100, formát dle počtu desetinných míst vstupu).
- [x] **(test)** zelené.

## 1.8 Pravidlo: číslo (culture-aware)
- [x] **(test)** `Parse_Integer`: `"123"` → Number, Value=`123d`.
- [x] **(test)** `Parse_Decimal_Invariant`: `"1234.56"` (en) → 1234.56; `Parse_Decimal_Czech`: `"1234,56"` (cs-CZ) → 1234.56.
- [x] **(test)** `Parse_Thousands`: `"1 234,56"` (cs-CZ NBSP/space group) → 1234.56, `ImpliedNumberFormat` se separátorem tisíců (`#,##0.00` ekvivalent).
- [x] **(test)** `Parse_Negative` a `Parse_LeadingPlus`.
- [x] **(test)** `Parse_ScientificNotation`: `"1.5E3"` → 1500.
- [x] Implementovat přes `double.TryParse(input, NumberStyles.Any, culture, …)` s ošetřením group separátoru a vědomým rozlišením, kdy vznikl implikovaný formát (tisíce/desetinná místa).
- [x] **(test)** zelené.

## 1.9 Pravidlo: měna (culture-aware)
- [x] **(test)** `Parse_Currency_Czech`: `"1 500 Kč"` / `"1 500 Kč"` (cs-CZ) → Currency, Value=1500, `ImpliedNumberFormat` měnový (`#,##0\ "Kč"` ekvivalent).
- [x] **(test)** `Parse_Currency_Dollar`: `"$10"` / `"$1,234.50"` (en) → 10 / 1234.5, měnový formát.
- [x] Implementovat (detekce měnového symbolu z `culture.NumberFormat.CurrencySymbol` + běžné symboly `$ € £ Kč`).
- [x] **(test)** zelené.

## 1.10 Pravidlo: datum a čas (culture-aware)
- [x] **(test)** `Parse_Date_Czech`: `"1.2.2024"`, `"31. 1. 2024"` (cs-CZ) → Date, Value = `DateTime`/serial, `ImpliedNumberFormat` datový dle culture.
- [x] **(test)** `Parse_Date_Iso`: `"2024-01-31"` → Date.
- [x] **(test)** `Parse_Time`: `"12:30"`, `"12:30:45"` → Time.
- [x] **(test)** `Parse_DateTime`: `"1.2.2024 12:30"` → DateTime.
- [x] **(test)** `Parse_AmbiguousNumberNotDate`: `"1.5"` (cs-CZ) zůstane číslo, ne datum (priorita pravidel!).
- [x] Implementovat přes `DateTime.TryParse(input, culture, DateTimeStyles.None, …)` s definovaným **pořadím pravidel** (procenta → číslo → měna → datum/čas → text), aby nedocházelo k chybným záměnám.
- [x] **(test)** zelené, vč. testu pořadí pravidel.

## 1.11 Uložení serial number vs. DateTime
- [x] **(test)** `Parse_Date_StoresExcelSerial` (nebo DateTime — sjednotit s `SpreadsheetNumberFormatter.ExcelEpoch`): ověřit, že hodnota je kompatibilní s formátovačem (ten už počítá s `ExcelEpoch = 1899-12-30`).
- [x] Rozhodnout reprezentaci (doporučeno: ukládat `DateTime` jako Value u Date/Time/DateTime; formátovač i export to už zvládají) a sjednotit napříč.
- [x] **(test)** round-trip parser → `SpreadsheetNumberFormatter.Format` → očekávaný zobrazený text.

## 1.12 Zapojení do commit cesty
> Cílem je, aby uživatelský vstup procházel parserem; vzorce se chovají jako dnes.
- [x] **(test, bUnit/integ.)** `Commit_NumberInput_StoresDoubleAndRightAligns`: zapsat `"123"` do A1 → `cell.Value is double`, `cell.DataType==Number`, zarovnání General = vpravo.
- [x] **(test)** `Commit_PercentInput_SetsImpliedFormat`: `"50%"` → `cell.Style.NumberFormat=="0%"` (jen pokud byl `General`; existující explicitní formát se nepřepisuje).
- [x] **(test)** `Commit_ForcedText_KeepsLeadingZeros`: `"'007"` → Text `"007"`, zarovnání vlevo.
- [x] Upravit cestu potvrzení hodnoty (`OnGridCellValueCommitted` / `OnGridCellValuesCommittedBatch` / `ApplyValueToActiveCellAsync` / veřejné `SetCellValue`) tak, aby ne-vzorcový vstup prošel `SpreadsheetValueParser.Parse(input, CultureInfo.CurrentCulture)` a do `SetCellValueCommand` šla typovaná hodnota + (volitelně) implikovaný `NumberFormat`.
  - [x] Rozšířit `SetCellValueCommand` o volitelný `impliedNumberFormat` aplikovaný **jen** když je aktuální formát `General` (a uchovat starý formát pro Undo).
- [x] **(test)** undo vrátí i původní `NumberFormat`.
- [x] **(test)** zelené.

## 1.13 Editační round-trip (raw vs. zobrazené)
- [x] **(test)** `EditValue_Date_ShowsRawInput`: po potvrzení data se při návratu do editu ukáže původní/raw reprezentace (ne formátovaný výstup), aby šlo přepsat.
- [x] **(test)** `EditValue_Number_ShowsCanonical`: číslo se v editu ukáže kanonicky dle culture (např. `1234.56` → `1234,56` v cs-CZ), ne s formátem tisíců.
- [x] Upravit `GetActiveCellEditValue()` a edit start v canvas enginu tak, aby vracely surovou/kanonickou hodnotu pro daný typ (oddělit „display value" od „edit value").
- [x] **(test)** zelené.

## 1.14 JS canvas rendering — zarovnání a formát dle typu
- [x] **(test/E2E)** Po zápisu `123`, `50%`, `1.2.2024`, `$10`, `TRUE` do sloupce se v canvasu zobrazí formátované hodnoty a správné zarovnání (čísla/datum vpravo, bool na střed, text vlevo).
- [x] Ověřit, že `spreadsheet-canvas.js` bere `DisplayValue`/formát z modelu (přes existující patch `SyncCanvas…CellsAsync`) a respektuje `General` zarovnání dle typu hodnoty.
- [x] Doplnit do JS enginu logiku General-zarovnání podle typu (pokud kreslení textu nezná typ, předat ho v patchi buňky).
- [x] **(E2E)** zelené.

## 1.15 Vložení (paste) a AutoFill respektují parser
- [x] **Interní copy/paste zachovává typy** — `PasteCommand` kopíruje `Clone()` buňky (Value + DataType + Style), takže typované hodnoty se zachovají bez nutnosti parseru. AutoFill pracuje s typovanými hodnotami.
- [ ] **(odloženo)** `Paste_TabularText_DetectsTypesPerCell` — parsování externího TSV textu (vložení z mimo-aplikace) po buňkách přes `SpreadsheetValueParser`. Patří do JS clipboard cesty; doplní se s plnou clipboard funkcionalitou.

## 1.16 XLSX round-trip
- [x] **(test)** `Xlsx_TypedValues_RoundTrip`: number/percent/currency/date/bool zapsané přes parser se exportují se správným `DataType`/formátem a po re-importu se shodují.
- [x] Sladit `XlsxExporter`/`XlsxImporter` s novými typy (`Percentage`,`Currency` → odpovídající cell type + number format).
- [x] **(test)** zelené.

## 1.17 bUnit / UX
- [x] bUnit: zápis do buňky přes UI (canvas commit) → ověřit typ + zarovnání + (případně) změnu number formatu v toolbaru (`SelectedNumberFormat`).
- [x] bUnit: žádné hardcoded texty (formáty z resources, kde se zobrazují labely typů).

## 1.18 Screenshot + UX sign-off
- [x] Baseline `__baseline__/spreadsheet/typedetect-01-numbers.png` (sloupec čísel zarovnaný vpravo).
- [x] Baseline `typedetect-02-percent-currency.png`, `typedetect-03-dates.png`, `typedetect-04-mixed.png`.
- [x] **UX review (checklist §5):** zarovnání, čitelnost, konzistence formátů, chování dlouhých čísel (přetečení/`####`), locale CZ. PASS / nálezy → kroky.

---

## Definition of Done (Fáze 1)
- [x] `SpreadsheetValueParser` pokrývá: vzorec, vynucený text, boolean, procenta, číslo, měna, datum/čas — vše culture-aware, s definovaným pořadím pravidel.
- [x] Commit ukládá typované hodnoty + implikovaný formát (jen na `General`); paste/autofill zachovávají typy (interní Clone). *(Externí TSV-paste přes parser odloženo — viz 1.15.)*
- [x] Editační round-trip vrací surovou/kanonickou hodnotu (`SpreadsheetCellEditText`).
- [x] JS canvas vykresluje formát + zarovnání dle typu.
- [x] XLSX round-trip sedí pro typy (double/bool/DateTime).
- [x] Unit + bUnit + E2E + screenshot baseline zelené (507 testů), UX sign-off PASS.
- [x] Žádné placeholdery; texty beze změny (parser/formatter jsou logika bez UI textů).
- [x] V `00_MASTER_PLAN.md` §8 přepnut stav fáze 1 na ✅.
