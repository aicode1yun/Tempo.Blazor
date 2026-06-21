# Fáze 16 — A2: Revize / sledování změn (Track Changes / Review)

> Stav: ☐ Neza­počato · Závisí na: Fáze 15 · Server: ✅ lokálně / 🖥️ pro sdílení (Fáze 17)
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Režim sledování změn: každá úprava (hodnota, formát, vložení/smazání řádku/sloupce, přesun) se zaznamená jako **návrh** s autorem a časem. **Accept/Reject** jednotlivě i hromadně, navigace mezi změnami, barevné zvýraznění dle autora, tooltip s popisem. Historie revizí (log) a její perzistence.

OnlyOffice reference: review engine ve `sdkjs/common` + `collaborativeHistory.js`. **Interní vzor k převzetí:** document editor už revize má — `DocumentEditorJsRuntimeRevisionTests`. Zrcadlit jeho přístup (track changes nad command modelem).

---

## ČÁST A — Datový model revizí

### 16A.1 Záznam změny
- [ ] **(test)** `SpreadsheetRevisionTests`: `SpreadsheetRevision { string Id, RevisionType Type, string AuthorId, string AuthorName, DateTime Utc, RevisionTarget Target, object? OldValue, object? NewValue, RevisionState (Pending|Accepted|Rejected) }`; `RevisionType { CellValue, CellStyle, InsertRows, DeleteRows, InsertCols, DeleteCols, Move, SheetAdd, SheetDelete, SheetRename }`.
- [ ] Vytvořit modely v `Spreadsheet/Collaboration/`; přidat `SpreadsheetWorkbook.RevisionLog { bool TrackingEnabled, List<SpreadsheetRevision> Revisions }`.
- [ ] **(test)** zelené.

---

## ČÁST B — Engine (záznam nad commandy)

### 16B.1 Záchyt změn
- [ ] **(test)** `TrackChanges_WhenEnabled_RecordsRevision`: při zapnutém sledování `SetCellValueCommand` vytvoří `Pending` revizi s old/new; při vypnutém ne.
- [ ] Rozšířit `SpreadsheetCommandManager` (nebo dekorátor) o emitování revizí z commandů (každý command umí popsat svůj „diff" + autora z `ISpreadsheetUserContext`).
- [ ] **(test)** pokrýt všechny `RevisionType` (hodnota/styl/struktura/list).
- [ ] **(test)** zelené.

### 16B.2 Accept / Reject
- [ ] **(test)** `AcceptRevision_AppliesPermanently` (změna se „zafixuje", revize → Accepted), `RejectRevision_RevertsChange` (vrátí old hodnotu, revize → Rejected).
- [ ] **(test)** `AcceptAll` / `RejectAll`; konflikty (revize závislé na předchozí) řešit v pořadí.
- [ ] Vytvořit `Commands/AcceptRevisionCommand`, `RejectRevisionCommand` (+ batch varianty) — Undo vrací do Pending.
- [ ] **(test)** zelené.

---

## ČÁST C — JS canvas rendering
- [ ] Rozšířit `spreadsheet-canvas.js`: buňky s `Pending` revizí zvýraznit (barevný rámeček dle autora + rohový marker); tooltip s popisem změny (kdo/kdy/co → co).
- [ ] Indikace strukturálních změn (vložený/smazaný řádek) v záhlaví.
- [ ] **(E2E)** úprava ve sledovacím režimu obarví buňku barvou autora.

---

## ČÁST D — UI

### 16D.1 Záložka Revize + panel
- [ ] **(bUnit)** záložka **Revize**: Sledovat změny (toggle), Předchozí/Další změna, Přijmout/Odmítnout (▾ Přijmout vše/Odmítnout vše), Zobrazit změny.
- [ ] **(bUnit)** `TmSpreadsheetRevisionsPanel`: seznam návrhů (autor, čas, popis, list/buňka), filtr dle autora/stavu, akce přijmout/odmítnout, klik skočí na buňku.
- [ ] Vytvořit komponenty + lokalizace `TmSpreadsheet_Review_*`.
- [ ] **(bUnit)** zelené.

### 16D.2 Tooltip změny
- [ ] **(bUnit/E2E)** najetí na změněnou buňku → popisek „Jan Novák, 4. 6. 2026: 10 → 20".

---

## ČÁST E — Historie a perzistence
- [ ] **(test)** `RevisionLog` lze serializovat/deserializovat (interní formát) → přežije uložení/načtení.
- [ ] (Volitelně) jednoduchá „historie verzí": snímky stavu + návrat (návaznost na co-editaci/host perzistenci — zdokumentovat hranici komponenta vs. host).
- [ ] **(E2E)** zapnout sledování → udělat změny → znovu načíst → návrhy zůstávají.

---

## ČÁST F — Screenshot
- [ ] Baseline `review-01-tracked.png`, `review-02-panel.png`, `review-03-tooltip.png` + UX sign-off (rozlišitelnost barev autorů, kontrast, srozumitelnost popisu, navigace mezi změnami).

---

## Definition of Done (Fáze 16)
- [ ] Sledování změn pro hodnoty/styly/strukturu/listy; accept/reject jednotlivě i hromadně; navigace.
- [ ] Barevné zvýraznění dle autora + tooltip; panel revizí s filtrem; historie/persistence revizí.
- [ ] Záznam nad command modelem; autor z `ISpreadsheetUserContext`; eventy připravené na realtime (Fáze 17).
- [ ] Commandy atomické + undo; Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [ ] Vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 16 na ✅.
