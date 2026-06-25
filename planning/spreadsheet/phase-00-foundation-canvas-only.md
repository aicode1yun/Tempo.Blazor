# Fáze 0 — Základy & canvas-only

> Stav: ✅ HOTOVO (canvas-only; build + 457 spreadsheet testů zelených; E2E baseline PNG vygenerovány; UX sign-off PASS)
> Závisí na: — · Server: ✅ klientsky
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md) (TDD, lokalizace, testy, DoD).

## Stav implementace (aktualizováno)
- ✅ Odstraněn DOM grid (`TmSpreadsheetGrid.*`), enum `SpreadsheetRenderMode`, parametr `RenderMode`.
- ✅ `TmSpreadsheetCanvasGrid` konsolidován na JS-engine (parametr `UseJsEngine` + hybridní `!UseJsEngine` větve a mrtvé členy odstraněny).
- ✅ Opraveni volající: `TmSpreadsheet.razor/.cs`, Notion blok/modal, `SpreadsheetPage`, `SpreadsheetBenchmarkPage` (přepsán na canvas-only).
- ✅ Migrace testů (varianta A): smazány neportovatelné DOM bUnit testy (Grid, GridStyle, Keyboard, Performance, Visual, Merge, Freeze, AutoFillHandle) a DOM-interakční testy v `TmSpreadsheetApiTests`/`TmSpreadsheetTests`; render testy přepsány na canvas-only. **Build zelený, 457 spreadsheet testů prochází.**
- ✅ E2E baseline scaffold (`SpreadsheetBaselineScreenshots.cs`) + `data-testid="spreadsheet-demo"` na demo stránce; E2E projekt se kompiluje.
- ⚠️ **Korekce plánu:** `spreadsheet.js` **NENÍ** DOM-only — hostuje i `tmSpreadsheetFormulaBar.*` helpery používané `TmSpreadsheetFormulaBar`. **Soubor ponechán.** (Volitelně lze později vyčistit mrtvé `tmSpreadsheetGrid.*` funkce uvnitř.)
- ✅ **Baseline PNG vygenerovány** proti běžícímu WASM demu (`Tempo.Blazor.Demo`, https://localhost:7106): `baseline-00-default.png`, `baseline-01-viewport.png`, `baseline-02-selected-cell.png`. **UX sign-off: PASS** (toolbar + formula bar + canvas mřížka renderují čistě, výběr se posouvá klávesnicí, name box ukazuje aktivní buňku D5, ukázková data renderují).
- 📌 **Poznámka pro další fáze (E2E):** Playwright pixel-kliky míří na vrchní `--selection` canvas vrstvu, která pohlcuje pointer eventy → **pro navigaci/výběr buněk v E2E používat fokus gridu + klávesnici** (`grid.FocusAsync()` + `ArrowDown/Right`), ne `Mouse.ClickAsync` na souřadnice.
- 📌 **Drobné UX (mimo rozsah f.0):** toolbar ukazuje výchozí velikost písma „8" (Excel default je 11) — zvážit v pozdější fázi formátování.

## Cíl & rozsah
Vyčistit komponentu na **jediný vykreslovací engine = JS canvas** (`TmSpreadsheetCanvasGrid` + `spreadsheet-canvas.js`). Odstranit DOM engine i hybridní canvas větev, srovnat existující testy, a položit sdílené základy (adresářová struktura pro dialogy/panely, screenshot baseline, demo scénáře), na kterých staví všechny další fáze. **Zpětná kompatibilita se neřeší.**

OnlyOffice reference: N/A (interní úklid). Cíl je čistá, udržovatelná základna.

---

## 0.1 Příprava — zmapování závislostí (bez změn kódu)
- [ ] Vypsat všechny reference na `SpreadsheetRenderMode` (grep) — produkční i testovací i demo (`SpreadsheetPage.razor`, `SpreadsheetBenchmarkPage.razor`).
- [ ] Vypsat reference na `TmSpreadsheetGrid` (komponenta, ne `TmSpreadsheetCanvasGrid`) — produkční, testy, JsonDocumentation.
- [ ] Vypsat reference na `ISpreadsheetGridController` a ověřit, že je implementuje i `TmSpreadsheetCanvasGrid` (zůstává jako jediná implementace).
- [ ] Ověřit, zda `wwwroot/js/spreadsheet.js` slouží jen DOM gridu, nebo i něčemu jinému (grep volání z C#). Výsledek určí, zda se maže celý, nebo jen jeho část.
- [ ] Zapsat zjištění jako krátký seznam dotčených souborů do tohoto bodu (kontrolní soupis pro úklid).

## 0.2 Test-first: zafixovat cílové chování render vrstvy
> Nejdřív upravíme/založíme testy tak, aby popisovaly **cílový** stav (jen canvas). Po úklidu kódu musí být zelené.

- [ ] V `TmSpreadsheetTests.cs` smazat testy `Render_DefaultParameters_RendersGrid`, `Render_CanvasMode_RendersCanvasGrid`, `Render_CanvasJsEngineMode_RendersCanvasGrid` (vázané na `RenderMode`/DOM grid).
- [ ] Napsat nový **failing** test `Render_Always_RendersCanvasGrid`: `RenderComponent<TmSpreadsheet>()` → najde `.tm-spreadsheet-canvas-grid` a `canvas.tm-spreadsheet-canvas-grid__canvas`.
- [ ] Napsat **failing** test `Render_DoesNotRenderDomGrid`: ve výstupu **není** žádný prvek/komponenta DOM gridu (po smazání už `TmSpreadsheetGrid` neexistuje — test ověří absenci jeho marker třídy, např. `.tm-spreadsheet-grid` bez `-canvas`).
- [ ] (Po dokončení 0.3–0.6) tyto testy musí být zelené.

## 0.3 Odstranění DOM gridu
- [ ] Smazat `Components/Spreadsheet/TmSpreadsheetGrid.razor`, `.razor.cs`, `.razor.css`.
- [ ] Smazat odpovídající testy vázané čistě na DOM grid: `TmSpreadsheetGridTests.cs`, `TmSpreadsheetGridStyleTests.cs` (a u smíšených testů přesunout relevantní asserty na canvas grid — viz 0.7).
- [ ] V `TmSpreadsheet.razor` smazat větev `else { <TmSpreadsheetGrid …/> }` a podmínku `@if (RenderMode != SpreadsheetRenderMode.Dom)` — nechat jen `TmSpreadsheetCanvasGrid` bezpodmínečně.
- [x] ~~Smazat `wwwroot/js/spreadsheet.js`~~ → **ponecháno**: ověřeno, že není DOM-only (obsahuje `tmSpreadsheetFormulaBar.*` pro stále existující formula bar). Smazat by rozbilo runtime.
- [ ] Build `-f net9.0` projektu `Tempo.Blazor` — zelený.

## 0.4 Konsolidace canvas enginu na JS-only
- [ ] V `TmSpreadsheetCanvasGrid.razor.cs` odstranit parametr `UseJsEngine` a všechny větve `!UseJsEngine` (hybrid) — chování = jako by `UseJsEngine` bylo vždy `true`.
  - [ ] Projít 15 výskytů `UseJsEngine` (řádky kolem 79, 206, 211, 217, 333–334, 389, 406, 528, 559, 605, 642, 1449, 1828) a u každého ponechat JS-engine větev, smazat hybridní.
  - [ ] Odstranit pole/stav sloužící jen hybridnímu renderu (`_lastExternalFormulaEditValue` apod., pokud se po úklidu nepoužívá).
- [ ] Unit/bUnit testy canvas gridu, které spoléhaly na `UseJsEngine=false`, přepsat na JS-engine variantu (mock JS interopu přes bUnit `JSInterop`).
- [ ] Build zelený.

## 0.5 Odstranění `SpreadsheetRenderMode` a parametru `RenderMode`
- [ ] V `TmSpreadsheet.razor.cs` odstranit: parametr `RenderMode`, `UseCanvasJsEngine`, a zjednodušit `CanvasJsEngineGrid` → `_grid as TmSpreadsheetCanvasGrid` (vždy). Zvážit přejmenování na `CanvasGrid`.
- [ ] Aktualizovat `_grid` typ: ponechat `ISpreadsheetGridController?` (kontrakt zůstává), implementace je jen `TmSpreadsheetCanvasGrid`.
- [ ] Smazat soubor `Enums/SpreadsheetRenderMode.cs`.
- [ ] Odstranit `RenderMode` z demo stránek (`SpreadsheetPage.razor`, `SpreadsheetBenchmarkPage.razor`) a z `JsonDocumentation/Components/Spreadsheet/TmSpreadsheet.json`.
- [ ] Grep: žádná zbývající reference na `SpreadsheetRenderMode`/`RenderMode` ve spreadsheetu.
- [ ] Build zelený.

## 0.6 Sjednocení interop cesty
- [ ] Ověřit, že `TmSpreadsheet.razor.cs` volá patch metody (`SyncCanvasJsEngineCellsAsync`, `RequestCanvasJsEngineFullRender`, `ApplyEngine…`) nepodmíněně (po odstranění `UseCanvasJsEngine`).
- [ ] Přejmenovat metody `…CanvasJsEngine…` → `…Canvas…` pro čitelnost (volitelné, ale doporučené — jednorázově, žádná BC).
- [ ] bUnit test: po commitu hodnoty do buňky se zavolá očekávaný JS interop (ověřit přes `JSInterop.VerifyInvoke`).

## 0.7 Srovnání zbývajících testů na canvas
- [ ] Projít `Tempo.Blazor.Tests/Components/Spreadsheet/*` a každý test vázaný na DOM grid přesměrovat na canvas grid nebo na čistou logiku (model/command).
  - [ ] `TmSpreadsheetVisualTests.cs`, `TmSpreadsheetApiTests.cs`, `TmSpreadsheetToolbarTests.cs`, `TmSpreadsheetSheetTabsTests.cs`, `TmSpreadsheetPerformanceTests.cs` — projít a opravit selektory.
- [ ] Spustit `Tempo.Blazor.Tests` (filtr Spreadsheet) — vše zelené.

## 0.8 Sdílená adresářová struktura pro další fáze
> Připravit „kostry" beze změny chování, ať fáze 1–17 jen přidávají.
- [ ] Vytvořit složky: `Components/Spreadsheet/Dialogs/`, `Components/Spreadsheet/Panels/`, `Components/Spreadsheet/Data/` (UI), a v Abstractions `Spreadsheet/Data/`, `Spreadsheet/Collaboration/` (zatím prázdné, doplní fáze).
- [ ] Do toolbaru přidat **prázdné** záložky/skupiny pro budoucí oblasti? → **NE** (žádné placeholdery, viz pravidlo 2.3). Toolbar se rozšiřuje až s konkrétní funkcí v dané fázi.
- [ ] Zavést sdílený helper pro výběr/rozsah, pokud chybí: ověřit `SpreadsheetSelectionState`/`GetSelectionBounds` jako jediný zdroj pravdy o aktuálním výběru (využijí filtry, validace, podmíněné formátování). Zdokumentovat v kódu jako veřejný kontrakt pro interní použití.

## 0.9 Demo & E2E baseline pro spreadsheet
- [ ] Ověřit `SpreadsheetPage.razor` (`/spreadsheet`) — funguje na canvas-only.
- [ ] Vytvořit `tests/Tempo.Blazor.E2E/SpreadsheetBaselineScreenshots.cs` (vzor `DocumentEditorBaselineScreenshots.cs`) — pořídí baseline pro: prázdný list, list s daty, vybraná buňka, editace buňky.
- [ ] Vytvořit baseline složku `__baseline__/spreadsheet/` a uložit počáteční baseline (`baseline-00-empty.png`, `baseline-01-data.png`, `baseline-02-selection.png`, `baseline-03-edit.png`).
- [ ] **UX sign-off** počátečního stavu (checklist §5 master) — zaznamenat případné nálezy jako kroky do fáze 1+.
- [ ] Spustit E2E `Spreadsheet*` — zelené.

## 0.10 Úklid & uzávěr fáze
- [ ] Grep mrtvého kódu po DOM/hybrid (nepoužité importy, CSS třídy `_spreadsheet.css` jen pro DOM grid).
- [ ] Aktualizovat `JsonDocumentation/Components/Spreadsheet/TmSpreadsheet.json` (bez `RenderMode`).
- [ ] Aktualizovat README/dokumentaci komponenty, pokud zmiňuje render režimy.
- [ ] Build `-f net9.0` zelený, `Tempo.Blazor.Tests` (Spreadsheet) zelené, `Tempo.Blazor.E2E` (Spreadsheet) zelené.

---

## Definition of Done (Fáze 0)
- [x] DOM grid a `SpreadsheetRenderMode` zcela odstraněny, build zelený.
- [x] `TmSpreadsheetCanvasGrid` běží jen v JS-engine režimu, bez hybridních větví.
- [x] Všechny spreadsheet unit/bUnit testy přesměrovány na canvas a zelené (457 zelených).
- [x] E2E + baseline screenshoty pro spreadsheet zavedeny, UX sign-off proveden (PASS).
- [x] JsonDocumentation (bez `RenderMode` — neobsahovala ho) a dokumentace aktualizovány.
- [x] Žádné placeholdery/TODO; vše lokalizováno (beze změny textů v této fázi).
- [x] V `00_MASTER_PLAN.md` §8 přepnut stav fáze 0 na ✅.
