# Fáze 3 — A4: AutoFilter + Řazení

> Stav: ✅ Dokončeno (2026-06-05) · Závisí na: Fáze 0, 1 · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).
>
> **Shrnutí dokončení:** modely (`Spreadsheet/Data/`: `SpreadsheetAutoFilter`, `SpreadsheetColumnFilter`,
> `SpreadsheetFilterCriteria/Condition`, `SpreadsheetColorFilter`, enumy; `SpreadsheetSortSpec`/`SortLevel`),
> enginy (`SpreadsheetFilterEngine` – distinct/values/text/number/date/color/AND+OR, `SpreadsheetSortEngine`
> – jedno/víceúrovňové, typové pořadí, prázdné dole, dle barvy, case-sensitive), commandy
> (`SetAutoFilterCommand`, `UpdateColumnFilterCommand`, `ClearAutoFilterCommand`, `SortRangeCommand` – vše
> s přesným undo, sort posouvá relativní vzorce a hlídá sloučené buňky), JS canvas (filtr tlačítka v hlavičce
> + hit-test + `.NET` callback `CanvasFilterButtonClicked`), UI (`TmSpreadsheetFilterDropdown`,
> `TmSpreadsheetCustomFilterDialog`, `TmSpreadsheetSortDialog`, toolbar záložka **Data**) plně lokalizováno
> (58 klíčů do 3 resx + mock), XLSX round-trip `<autoFilter>` + skryté řádky. 617 unit/bUnit spreadsheet testů,
> 5 E2E (`AutoFilter_*`, `Sort_Descending_*`) proti HTTPS WASM+API, 3 screenshot baseline
> (`filter-01-dropdown`, `filter-02-active`, `sort-01-dialog`), UX sign-off PASS. Při ladění odhalen a opraven
> latentní bug JS enginu (skryté řádky se nesbalovaly kvůli `size || default` a `size <= 0` filtru) — opraveno
> v `buildAxisOffsets`/`updateAxisCache`/`getDrawableCells`, prospívá i existující funkci skrývání řádků.

## Cíl & rozsah
- **AutoFilter**: tlačítka filtru v záhlaví rozsahu/tabulky, dropdown se zaškrtávacím seznamem unikátních hodnot + fulltext, typové filtry (text/číslo/datum), filtr dle barvy, badge aktivního filtru, skrytí nevyhovujících řádků (přes `Row.IsHidden`).
- **Řazení**: vzestupně/sestupně, víceúrovňové, dle barvy, s volbami (záhlaví, citlivost na velikost, orientace).

OnlyOffice reference: `model/autofilters.js`, `view/AutoFilterDialog.js`, `view/SortDialog.js`, `view/SortOptionsDialog.js`, `view/CustomFilterDialog`.

Návaznost: po Fázi 1 jsou hodnoty typované → filtry/řazení rozlišují čísla/data/text korektně.

---

## ČÁST A — Datový model

### 3A.1 Model autofiltru
- [x] **(test)** `SpreadsheetAutoFilterTests`: `SpreadsheetAutoFilter { SpreadsheetRange Range, List<SpreadsheetColumnFilter> Columns }`; `SpreadsheetColumnFilter { int ColumnIndex, FilterKind, ISet<string> AllowedValues?, FilterCriteria? Criteria, ColorFilter? }`.
- [x] Vytvořit modely v `Spreadsheet/Data/` (Abstractions): `SpreadsheetAutoFilter`, `SpreadsheetColumnFilter`, enum `SpreadsheetFilterKind { Values, Text, Number, Date, Color }`, `SpreadsheetFilterCriteria` (operátor + operandy), `SpreadsheetColorFilter`.
- [x] Přidat `SpreadsheetSheet.AutoFilter` (nullable) + do `Clone()`.
- [x] **(test)** zelené.

### 3A.2 Model řazení
- [x] **(test)** `SpreadsheetSortSpecTests`: `SpreadsheetSortSpec { SpreadsheetRange Range, bool HasHeader, List<SpreadsheetSortLevel> Levels, bool ByRows }`; `SpreadsheetSortLevel { int KeyIndex, SortDirection Direction, SortOn(Value|CellColor|FontColor), object? ColorKey, bool CaseSensitive }`.
- [x] Vytvořit modely + enumy v `Spreadsheet/Data/`.
- [x] **(test)** zelené.

---

## ČÁST B — Engine (čistá logika)

### 3B.1 Filtr engine
- [x] **(test)** `SpreadsheetFilterEngineTests.DistinctValues`: vrátí setříděné unikátní hodnoty sloupce v rozsahu (respektuje typy z Fáze 1, formátované zobrazení pro UI).
- [x] **(test)** `Apply_ValuesFilter_HidesNonMatching`: vrátí indexy řádků ke skrytí dle `AllowedValues`.
- [x] **(test)** `Apply_TextCriteria` (contains/beginsWith/endsWith/equals/notEquals), `Apply_NumberCriteria` (>, <, between, top10, aboveAverage), `Apply_DateCriteria` (today/thisMonth/between, dynamické skupiny rok→měsíc→den).
- [x] **(test)** `Apply_ColorFilter` (dle `BackgroundColor`/`ForeColor`).
- [x] **(test)** Kombinace filtrů na více sloupcích = logické AND.
- [x] Vytvořit `Spreadsheet/Data/SpreadsheetFilterEngine.cs` — `IReadOnlyList<int> ComputeHiddenRows(SpreadsheetSheet, SpreadsheetAutoFilter)` + `IReadOnlyList<SpreadsheetFilterValue> DistinctValues(...)`.
- [x] **(test)** vše zelené.

### 3B.2 Sort engine
- [x] **(test)** `SpreadsheetSortEngineTests.SingleKey_Asc/Desc`: vrátí permutaci řádků (čísla < text < … dle Excel pořadí typů; prázdné vždy dole).
- [x] **(test)** `MultiLevel`: stabilní řazení dle více klíčů.
- [x] **(test)** `ByColor`: řazení dle barvy buňky/písma (pořadí dle definovaných color keys).
- [x] **(test)** `WithHeader_KeepsHeaderRow`.
- [x] **(test)** `CaseSensitive` varianta.
- [x] Vytvořit `Spreadsheet/Data/SpreadsheetSortEngine.cs` — `IReadOnlyList<int> ComputeOrder(SpreadsheetSheet, SpreadsheetSortSpec)`.
- [x] **(test)** zelené.

---

## ČÁST C — Commandy (atomické + undo)

### 3C.1 Aplikace/úprava filtru
- [x] **(test)** `SetAutoFilterCommandTests`: zapne autofiltr na rozsah (přidá `Sheet.AutoFilter`), Undo odebere a obnoví viditelnost řádků.
- [x] **(test)** `UpdateColumnFilterCommandTests`: změna kritéria sloupce přepočítá skryté řádky; Undo vrátí předchozí stav filtru i `Row.IsHidden`.
- [x] **(test)** `ClearAutoFilterCommand`: odebere filtr, zobrazí všechny řádky.
- [x] Vytvořit `Commands/SetAutoFilterCommand.cs`, `UpdateColumnFilterCommand.cs`, `ClearAutoFilterCommand.cs` — ukládají i původní `Row.IsHidden` množinu pro přesné Undo.
- [x] **(test)** zelené.

### 3C.2 Řazení
- [x] **(test)** `SortRangeCommandTests`: aplikuje permutaci na buňky rozsahu (přesune hodnoty, styly, sloučení? — definovat: sloučené buňky v řazeném rozsahu zakázat/varovat), přepočítá vzorce/odkazy; Undo vrátí přesně.
- [x] **(test)** `Sort_PreservesFormulasRelative`: relativní odkazy se posunou korektně (návaznost na `FormulaReferenceAdjuster`).
- [x] Vytvořit `Commands/SortRangeCommand.cs`.
- [x] **(test)** zelené.

---

## ČÁST D — JS canvas rendering

### 3D.1 Filtr tlačítka v záhlaví
- [x] Rozšířit `spreadsheet-canvas.js` o vykreslení **filtr dropdown ikony** v pravém rohu buněk záhlaví filtrovaného rozsahu (stav: nefiltrováno/aktivní filtr/seřazeno ▲▼).
- [x] Hit-test kliknutí na ikonu → C# event `OnFilterButtonClicked(columnIndex)`.
- [x] **(E2E)** ikony se zobrazují jen v hlavičkovém řádku rozsahu filtru.

### 3D.2 Skryté řádky
- [x] Ověřit, že engine respektuje `Row.IsHidden` při kreslení (mechanismus z dřívějška) i u zafiltrovaných řádků (žádné „díry", korektní čísla řádků).

---

## ČÁST E — UI (dropdown + dialogy)

### 3E.1 Filtr dropdown
- [x] **(bUnit, failing)** `TmSpreadsheetFilterDropdown` zobrazuje: řazení A→Z / Z→A / dle barvy, hledání, zaškrtávací seznam (Vybrat vše + hodnoty), OK/Zrušit — vše lokalizované.
- [x] Vytvořit `Components/Spreadsheet/Data/TmSpreadsheetFilterDropdown.razor(.cs/.css)`; otevírá se u ikony (pozice z canvasu).
- [x] Podsekce „Filtry textu/čísel/dat" → otevře `TmSpreadsheetCustomFilterDialog`.
- [x] Lokalizace `TmSpreadsheet_Filter_*`.
- [x] **(bUnit)** zelené.

### 3E.2 Custom filter dialog
- [x] **(bUnit)** `TmSpreadsheetCustomFilterDialog`: operátor + hodnota(y), A/NEBO kombinace dvou podmínek; typově přizpůsobené (text/číslo/datum).
- [x] Vytvořit dialog + lokalizace.
- [x] **(bUnit)** zelené.

### 3E.3 Sort dialog
- [x] **(bUnit)** `TmSpreadsheetSortDialog`: úrovně (přidat/odebrat/pořadí), klíč = sloupec, řadit podle (hodnota/barva buňky/barva písma), pořadí (A→Z/Z→A/vlastní), „Moje data mají záhlaví", Volby (citlivost, orientace).
- [x] Vytvořit dialog `Components/Spreadsheet/Dialogs/TmSpreadsheetSortDialog.razor(.cs/.css)` + lokalizace.
- [x] **(bUnit)** zelené.

### 3E.4 Toolbar / menu
- [x] Přidat do toolbaru (nová záložka **Data** nebo do Home) tlačítka: **Filtr** (zapnout/vypnout), **Seřadit vzestupně/sestupně**, **Vlastní řazení…**.
- [ ] Kontextové menu buňky: „Filtrovat dle hodnoty/barvy", „Seřadit".
- [x] Lokalizace `TmSpreadsheet_TabData`, `TmSpreadsheet_Data_Filter`, `…_SortAsc/_SortDesc/_CustomSort`.

---

## ČÁST F — E2E, screenshoty, XLSX

### 3F.1 E2E
- [x] **(E2E)** Zapnout filtr na sloupci → odškrtnout hodnotu → řádky se skryjí → badge aktivního filtru.
- [x] **(E2E)** Číselný filtr „> 100" skryje nevyhovující.
- [x] **(E2E)** Seřadit sestupně → pořadí řádků se změní, vzorce sedí.
- [x] **(E2E)** Vymazat filtr → vše viditelné.

### 3F.2 Screenshot + UX
- [x] Baseline `filter-01-dropdown.png`, `filter-02-active.png`, `sort-01-dialog.png`.
- [x] **UX review:** rozbalovací panel (šířka, scroll u mnoha hodnot), čitelnost zaškrtávátek, indikace aktivního filtru/řazení, prázdný stav hledání, klávesnice (šipky/Enter/Esc).

### 3F.3 XLSX round-trip
- [x] **(test)** `<autoFilter>` a stav řazení se exportují/importují (alespoň definice filtru a skryté řádky); ověřit otevření v Excelu/OnlyOffice.

---

## Definition of Done (Fáze 3)
- [x] AutoFilter: hodnotový + typové (text/číslo/datum) + barevné filtry (engine + commandy), dropdown, aktivní badge (funnel), skrytí řádků, atomické commandy s undo.
- [x] Řazení: jedno/víceúrovňové, dle barvy (engine), s volbami (záhlaví, citlivost); vzorce/odkazy konzistentní (`Sort_PreservesFormulasRelative`).
- [x] JS canvas: filtr ikony + hit-test + `.NET` callback. *(persistentní řazení indikátory ▲▼ v hlavičce: zatím ne — řazení je jednorázová operace; doplnit při potřebě.)*
- [x] Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [x] XLSX round-trip filtru (`<autoFilter>`) + skryté řádky.
- [x] Vše lokalizováno (58 klíčů × 3 resx + test mock), žádné placeholdery.
- [x] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 3 na ✅.

> **Zbývá jako drobné rozšíření (mimo jádro fáze, neblokuje):** kontextové menu buňky „Filtrovat dle hodnoty/barvy / Seřadit"; UI výběr konkrétní barvy ve „Filtru dle barvy" a v řazení „dle barvy" (enginy to už podporují); persistentní indikátor řazení v hlavičce.
