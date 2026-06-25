# Fáze 17 — A3: Současná editace více lidmi (Co-editing)

> Stav: ☐ Neza­počato · Závisí na: Fáze 15, 16 · Server: 🖥️ **nutný backend** · Náročnost: 🔴 XL
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Realtime spolupráce více uživatelů nad jedním sešitem: rozesílání atomických operací, slučování souběžných změn, **živé kurzory a výběry** ostatních (barevně + jméno), presence, zamykání buněk (strict režim) nebo živé sloučení (fast režim), reconnect/replay. Komentáře (15) a revize (16) se sdílí v reálném čase.

**Architektura — reuse:** zrcadlit existující `Tempo.Blazor.Collaboration` / `SignalRDocumentCollaborationProvider` (model „operation-batch + cursor", hub metody Join/Leave/BroadcastOperationBatch/GetOperationBatches/BroadcastCursor/GetCursors/RemoteOperationBatchReceived/RemoteCursorReceived) a `IDocumentCollaborationProvider`/`...Realtime`, `InMemoryDocumentCollaborationProvider`, `DocumentCollaborationSync`, hub v `Tempo.Blazor.Demo.Api/Hubs/`.

> ⚠️ Před implementací potvrdit s uživatelem: **strict vs. fast** režim jako výchozí, a zda cílit Blazor Server (sdílený server-side stav, levnější) nebo WASM + SignalR backend (plné OT/CRDT). Tato fáze předpokládá **operation-based sloučení** přes SignalR backend, analogicky k document editoru.

---

## ČÁST A — Operační model (předpoklad)

### 17A.1 Serializovatelná operace
- [ ] **(test)** `SpreadsheetOperationTests`: `SpreadsheetOperation` pokrývá atomické změny (set value/formula, set style, insert/delete row/col, merge, rename/add/delete sheet, comment*, revision*) — serializace/deserializace round-trip.
- [ ] Vytvořit `Spreadsheet/Collaboration/SpreadsheetOperation.cs` + `SpreadsheetOperationBatch { string DocId, string UserId, long Seq, List<SpreadsheetOperation> Ops }` (Abstractions).
- [ ] **(test)** zelené.

### 17A.2 Most command ↔ operace
- [ ] **(test)** `CommandToOperation`: každý `ISpreadsheetCommand` umí vyprodukovat odpovídající `SpreadsheetOperation`(s) a naopak je aplikovat z přijaté operace (bez znovu-emitování → bez smyčky).
- [ ] Rozšířit commandy o `ToOperations()` / aplikační cestu „apply remote operation" (která neprodukuje další broadcast).
- [ ] **(test)** zelené pro všechny typy commandů.

---

## ČÁST B — Transformace / slučování

### 17B.1 Konflikty a transformace
- [ ] **(test)** `OperationTransformTests`: souběžné operace na různých buňkách komutují; na téže buňce vyhrává deterministicky (LWW dle Seq/času/UserId) ve fast režimu; strukturální operace (insert/delete row) transformují indexy souběžných operací.
- [ ] **(test)** `IndexShift`: insert/delete řádku/sloupce posune odkazy souběžných operací i kotvy komentářů/revizí.
- [ ] Vytvořit `Spreadsheet/Collaboration/SpreadsheetOperationTransformer.cs` (OT pro tabulkový model: souřadnicové posuny + LWW na buňce).
- [ ] **(test)** zelené (matice souběhů).

> Pozn.: Plné OT je náročné. Pro tabulky stačí praktická podmnožina: per-buňka LWW + souřadnicová transformace strukturálních operací. Zdokumentovat omezení.

---

## ČÁST C — Provider a transport (reuse Tempo.Blazor.Collaboration)

### 17C.1 Abstrakce provideru
- [ ] **(test)** `ISpreadsheetCollaborationProvider` (analog `IDocumentCollaborationProvider`): Join/Leave dokumentu, broadcast operation batch, get batches (reconnect), broadcast cursor, get cursors; `IObservable`/eventy pro příchozí.
- [ ] Vytvořit `Spreadsheet/Collaboration/ISpreadsheetCollaborationProvider.cs` (+ realtime varianta) a `InMemorySpreadsheetCollaborationProvider` (pro testy a běh bez serveru) v Abstractions.
- [ ] **(test)** zelené (in-memory loopback: dvě „session" si vymění operace).

### 17C.2 SignalR provider + hub
- [ ] Rozšířit `Tempo.Blazor.Collaboration` o `SignalRSpreadsheetCollaborationProvider` (zrcadlí `SignalRDocumentCollaborationProvider`, stejné hub-metoda konvence).
- [ ] Přidat `SpreadsheetCollaborationHub` do `Tempo.Blazor.Demo.Api/Hubs/` (server-side řazení Seq, skupiny dle DocId, perzistence batchů pro replay).
- [ ] **(test)** integ. test hub ↔ provider (Join, broadcast, receive, reconnect replay).

### 17C.3 Sync služba
- [ ] **(test)** `SpreadsheetCollaborationSync` (analog `DocumentCollaborationSync`): napojí provider na `SpreadsheetCommandManager` (odeslání lokálních ops, aplikace vzdálených přes transformer), drží `Seq`, řeší reconnect (dožádá chybějící batche).
- [ ] Vytvořit službu + testy (dvě instance konvergují na stejný stav).
- [ ] **(test)** zelené.

---

## ČÁST D — Presence: kurzory a výběry

### 17D.1 Model + broadcast
- [ ] **(test)** `SpreadsheetCursorTests`: `SpreadsheetCollaboratorCursor { UserId, Name, Color, SheetIndex, ActiveCellRef, SelectionRange }` serializace; throttling odesílání.
- [ ] Napojit změnu výběru (`OnSelect`) na broadcast kurzoru (throttled).
- [ ] **(test)** zelené.

### 17D.2 Overlay živých kurzorů (JS canvas)
- [ ] **(bUnit)** `TmSpreadsheetCollaboratorCursors` overlay (analog `TmDocumentCollaborationCursorOverlay` / `TmNotionCollaborationCursors`): vykreslí barevné rámečky výběrů + jmenovky ostatních, ukotvené při scrollu/zoomu.
- [ ] Vytvořit komponentu/overlay + interop pro pozice z canvasu + lokalizace `TmSpreadsheet_Collab_*`.
- [ ] **(E2E)** dvě session: kurzor/výběr jednoho je vidět u druhého.

---

## ČÁST E — Aplikace na obsah, komentáře, revize
- [ ] **(test)** vzdálená operace se projeví v gridu bez ztráty lokálního rozdělané editace (merge u needitovaných buněk; u editované buňky řešit dle režimu).
- [ ] Sdílení **komentářů** (15) a **revizí** (16) přes stejný operační kanál (operace `Comment*`/`Revision*`).
- [ ] Presence „kdo právě edituje buňku" indikátor.
- [ ] **(E2E)** uživatel A přidá komentář → uživatel B ho vidí živě; A udělá změnu ve sledovacím režimu → B vidí návrh.

---

## ČÁST F — Robustnost
- [ ] **(test)** reconnect: po výpadku se dožádají chybějící batche a stav konverguje.
- [ ] **(test)** řazení Seq na serveru je autoritativní; klient přeskládá lokální optimistické operace.
- [ ] Ošetřit late-join (nový účastník dostane snapshot + tail operací).
- [ ] **(E2E)** odpojit/připojit jednu session → dožene změny.

---

## ČÁST G — Demo + screenshot
- [ ] Demo scénář se dvěma okny (jako document editor collaboration demo) na `/spreadsheet` + hub v Demo.Api.
- [ ] **(E2E)** `SpreadsheetCollaborationRealtimeTests` (vzor `DocumentEditorCollaborationRealtimeTests`): dvě session, konvergence obsahu/kurzorů/komentářů.
- [ ] Baseline `collab-01-cursors.png`, `collab-02-presence.png` + UX sign-off (rozlišitelnost barev, jmenovky nepřekáží, plynulost).

---

## Definition of Done (Fáze 17)
- [ ] Operační model + most command↔operace + praktická transformace (LWW + souřadnicové posuny).
- [ ] `ISpreadsheetCollaborationProvider` (in-memory + SignalR), hub v Demo.Api, sync služba, reconnect/replay/late-join.
- [ ] Živé kurzory/výběry + presence; sdílené komentáře (15) a revize (16).
- [ ] Architektura (strict/fast, Server/WASM) odsouhlasena s uživatelem před implementací.
- [ ] Unit + integ. + bUnit + E2E (dvě session) + screenshoty zelené, UX sign-off PASS.
- [ ] Omezení (rozsah OT) zdokumentováno ve veřejném API; vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 17 na ✅.
