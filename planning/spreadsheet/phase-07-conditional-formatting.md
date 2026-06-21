# Fáze 7 — Podmíněné formátování

> Stav: ☐ Neza­počato · Závisí na: Fáze 1 · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Pravidla, která mění vzhled buněk podle jejich hodnot: zvýraznění buněk (>, <, mezi, rovná se, text obsahuje, datum, duplicity), top/bottom (N, %), nadprůměr/podprůměr, **datové pruhy**, **barevné škály** (2/3 barvy), **sady ikon**, a pravidlo **vlastním vzorcem**. Správce pravidel (pořadí, „zastavit při splnění", rozsah platnosti).

OnlyOffice reference: `model/ConditionalFormatting.js`, `view/FormatRulesEditDlg.js`, `view/FormatRulesManagerDlg.js`.

---

## ČÁST A — Datový model

### 7A.1 Pravidla
- [ ] **(test)** `SpreadsheetConditionalFormatTests`: `SpreadsheetConditionalFormat { SpreadsheetRange Range, CfRuleType Type, int Priority, bool StopIfTrue, CfRuleParams }`.
- [ ] Vytvořit `Spreadsheet/Data/Conditional/`: `SpreadsheetConditionalFormat`, enum `CfRuleType { CellIs, Top10, AboveBelowAverage, DuplicateValues, UniqueValues, TextContains, TimePeriod, DataBar, ColorScale, IconSet, Expression }`, parametrické typy:
  - `CellIsParams { ComparisonOperator, Formula1, Formula2?, SpreadsheetCellStyle AppliedStyle }`
  - `Top10Params { bool Bottom, bool Percent, int Rank, style }`
  - `DataBarParams { minType, maxType, color, gradient, showValue, negativeColor, axis }`
  - `ColorScaleParams { stops: (type,value,color)[] }` (2 nebo 3)
  - `IconSetParams { setName, thresholds, reverse, showValueOnly }`
  - `ExpressionParams { Formula, style }`
- [ ] Přidat `SpreadsheetSheet.ConditionalFormats` (list) + `Clone()`.
- [ ] **(test)** zelené.

---

## ČÁST B — Engine (vyhodnocení)

### 7B.1 Hodnotová pravidla
- [ ] **(test)** `CfEngineTests.CellIs`: operátory (between/notBetween/equal/greater/less/…) → vrátí styl pro buňky, které vyhovují.
- [ ] **(test)** `Top10` / `Bottom` / `Percent`.
- [ ] **(test)** `AboveBelowAverage` (vč. ±std dev variant).
- [ ] **(test)** `Duplicate`/`Unique`.
- [ ] **(test)** `TextContains` / `BeginsWith` / `EndsWith`.
- [ ] **(test)** `TimePeriod` (today/yesterday/last7days/thisMonth…).
- [ ] **(test)** `Expression` (vlastní vzorec relativní k buňce → využít `FormulaEngine`).
- [ ] **(test)** Priorita + `StopIfTrue` (vyšší priorita vyhrává; stop ukončí další).

### 7B.2 Vizuální pravidla (výpočet metrik, ne stylu)
- [ ] **(test)** `DataBar_ComputesBarLength`: pro každou buňku spočítá poměr (0–1) dle min/max (auto/number/percent/percentile/formula) → délka pruhu.
- [ ] **(test)** `ColorScale_Interpolates`: barva interpolovaná mezi stop body (2/3 barvy).
- [ ] **(test)** `IconSet_PicksIcon`: dle prahů (procenta/percentil/číslo/vzorec) vybere index ikony; `reverse`.
- [ ] Vytvořit `Spreadsheet/Data/Conditional/SpreadsheetConditionalEngine.cs` → `CfCellResult Evaluate(cellRef, sheet, formulaContext)` vracející `AppliedStyle?` + `Visual?` (bar/colorscale/icon).
- [ ] **(test)** vše zelené.

---

## ČÁST C — Commandy
- [ ] **(test)** `AddConditionalFormatCommandTests`, `EditConditionalFormatCommandTests`, `DeleteConditionalFormatCommandTests`, `ReorderConditionalFormatCommandTests` (priorita) — vše s Undo.
- [ ] Vytvořit commandy v `Commands/`.
- [ ] **(test)** zelené.

---

## ČÁST D — JS canvas rendering
- [ ] Engine předá enginu kreslení **vrstvu podmíněného formátování** per buňka: efektivní styl (barva pozadí/písma/ohraničení) + případně data bar / color scale fill / ikona.
- [ ] Rozšířit `spreadsheet-canvas.js`:
  - [ ] kreslení data baru (gradient/plná, kladná/záporná osa, volitelně skrýt hodnotu),
  - [ ] color scale jako pozadí buňky,
  - [ ] icon set (vykreslit ikonu vlevo + zarovnat text),
  - [ ] aplikace stylu z `CellIs`/`Expression` pravidel **nad** základním stylem buňky (pořadí: základ → CF).
- [ ] Přepočet CF při změně hodnot v rozsahu (invalidace).
- [ ] **(E2E)** mřížka zobrazí data bary/škály/ikony.

---

## ČÁST E — UI

### 7E.1 Galerie pravidel (toolbar)
- [ ] **(bUnit)** tlačítko **Podmíněné formátování** (Home/Data) → menu: Zvýraznit pravidla buněk ▸, Pravidla nejvyšších/nejnižších ▸, Datové pruhy ▸, Barevné škály ▸, Sady ikon ▸, Nové pravidlo…, Vymazat pravidla ▸, Správa pravidel…
- [ ] Lokalizace `TmSpreadsheet_Cf_*`.

### 7E.2 Editor pravidla
- [ ] **(bUnit, failing)** `TmSpreadsheetCfRuleEditDialog`: výběr typu pravidla → dynamická pole; pro `CellIs` výběr formátu (znovupoužít `TmSpreadsheetFormatCellsDialog` nebo mini-preview); pro data bar/škálu/ikony příslušné editory s živým náhledem.
- [ ] Vytvořit dialog + lokalizace.
- [ ] **(bUnit)** zelené.

### 7E.3 Správce pravidel
- [ ] **(bUnit)** `TmSpreadsheetCfManagerDialog`: seznam pravidel (typ, formát náhled, platí pro, „zastavit při splnění"), změna pořadí (nahoru/dolů), přidat/upravit/smazat, přepínač rozsahu (list/výběr).
- [ ] Vytvořit dialog + lokalizace.
- [ ] **(bUnit)** zelené.

---

## ČÁST F — E2E, screenshot, XLSX
- [ ] **(E2E)** „zvýraznit > 100 červeně" → buňky se obarví; změna hodnoty pod 100 → barva zmizí.
- [ ] **(E2E)** data bary na sloupci čísel; barevná škála 3 barvy; sada ikon.
- [ ] Baseline `cf-01-databars.png`, `cf-02-colorscale.png`, `cf-03-iconset.png`, `cf-04-manager.png`.
- [ ] **UX review:** čitelnost textu přes data bar/škálu (kontrast!), zarovnání ikon, živý náhled v editoru, srozumitelnost správce.
- [ ] **(test)** XLSX round-trip `<conditionalFormatting>` (vč. dataBar/colorScale/iconSet/cfRule).

---

## Definition of Done (Fáze 7)
- [ ] Všechny typy pravidel (hodnotová, top/bottom, average, duplicate/unique, text, time, expression) + vizuální (data bar, color scale, icon set).
- [ ] Priorita + StopIfTrue; správce pravidel; živý náhled v editoru.
- [ ] JS canvas vykresluje všechny vizuální typy s dostatečným kontrastem.
- [ ] Commandy atomické + undo; přepočet při změně dat.
- [ ] Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [ ] XLSX round-trip; vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 7 na ✅.
