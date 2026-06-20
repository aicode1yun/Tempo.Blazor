# Document editor: JS-renderovana collaboration architektura TDD TODO

Stav sepsany k 2026-05-14. Soubor je zivy implementacni checklist pro bod 3 z diskuze: collaboration nesmi byt text-only diff, nesmi znovu posilat vlastni lokalni echo do editor surface a remote zmeny se maji aplikovat primarne v JavaScript WYSIWYG enginu, ne pres Blazor rerender/snapshot behem psani.

## Cil

Prevest real-time collaboration z provizorniho block/text diffu na semanticky operation stream nad dokumentem:

- textove zmeny po inline/range operacich,
- inline formatovani jako mark operace,
- bloky vcetne obrazku/tabulek jako strukturalni operace,
- revize/suggestions jako vlastni operace,
- remote aplikace pres JS DOM patcher,
- Blazor zustava shell, model/persistence orchestrator a panelova vrstva.

## Pravidla implementace

- [ ] Kazdy krok zacina RED testem.
- [ ] Ke kazde fazi vznikne aspon jeden targeted unit/component test.
- [ ] Ke kazde uzivatelsky viditelne collaboration zmene vznikne e2e test.
- [ ] Provider boundary zustava v `Tempo.Blazor.Abstractions`.
- [ ] JS engine nesmi volat demo API primo.
- [ ] Remote operace se v aktivnim WYSIWYG editoru nesmi aplikovat pres full Blazor rerender.
- [ ] Vlastni echo operace se musi filtrovat pres `sessionId` a `operationId`.
- [ ] Text, inline marks, obrazky, tabulky a revize nesmi byt degradovane na plain text pri collaboration broadcastu.
- [ ] Po kazde dokoncene fazi odskrtnout tento checklist a doplnit poznamku s test prikazy.

## Aktualni zname limity

- [x] Existuje `IDocumentCollaborationProvider`.
- [x] Existuje `DocumentCollaborationSync`.
- [x] Existuje operation-log prototyp.
- [x] Existuje provider-backed demo collaboration.
- [x] Remote cursory se filtruji podle aktualni session.
- [x] Local diff pro existujici blok stale pouziva `SetBlockAttribute` s `AttributeName = "text"`.
- [x] Inline marks jako tucne/kurziva/link nejsou posilane jako granularni collaboration operace.
- [x] Uprava existujiciho image bloku neni reprezentovana jako granularni image/block update operace.
- [ ] Revizni znacky nejsou plnohodnotna collaboration operation sada.
- [ ] Remote update WYSIWYG surface je stale zavisly na Blazor model/snapshot ceste.
- [ ] JS engine nema verejny `applyRemoteOperationBatch` patcher.
- [ ] Client nema idempotentni cache aplikovanych `operationId`.

## Faze 0: Baseline a ochrana soucasnych limitu

### 0.1 Baseline testy

- [x] **RED:** Pridat characterization test, ktery ukaze, ze zmena existujiciho text runu dnes vygeneruje plain `SetBlockAttribute("text")`.
- [x] **RED:** Pridat characterization test, ktery ukaze, ze `ToggleMark` z WYSIWYG dnes nevytvori granularni collaboration mark operation.
- [x] **RED:** Pridat characterization test, ktery ukaze, ze update image blocku dnes neni samostatna image/block update collaboration operation.
- [x] **GREEN:** Testy oznacit jako aktualni limit nebo je napsat proti nove ocekavane architekture podle prvni implementovane faze.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentCollaboration" --logger "console;verbosity=minimal"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~WysiwygPatchApplierTests|FullyQualifiedName~TmDocumentEditorTests" --logger "console;verbosity=minimal"`.
- [x] Poznamenat vysledek sem.

Vysledek 2026-05-14:

- Pridany characterization testy v `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentCollaborationTests.cs`.
- `DocumentCollaboration` targeted run prosel: 12 passed, 0 failed, 0 skipped.
- `WysiwygPatchApplierTests|TmDocumentEditorTests` targeted run prosel: 55 passed, 0 failed, 0 skipped.
- Behem buildu zustavaji existujici warningy, hlavne `NU1603` pro `Microsoft.Extensions.Http` a starsi analyzer/XML warningy mimo tuto fazi.

### 0.2 E2E baseline

- [x] **RED:** E2E se dvema browser contexty potvrdi, ze vlastni lokalni psani se v prvnim klientovi nezdvoji po provider echo.
- [x] **RED:** E2E se dvema browser contexty vlozi remote text a overi, ze druhy klient zustane focusnuty ve WYSIWYG surface.
- [x] **RED:** E2E zachyti, ze remote zmena behem lokalniho focusu nesmi resetovat caret na zacatek dokumentu.
- [x] Spustit API a WASM demo.
- [x] Spustit targeted E2E collaboration testy.
- [x] Poznamenat vysledek sem.

Vysledek 2026-05-14:

- Pridan prubezny E2E test `DocumentEditor_Wysiwyg_CollaborationOwnTypingIsNotDuplicatedAfterProviderEcho`.
- Pridany budouci guard testy `DocumentEditor_Wysiwyg_CollaborationRemoteTextKeepsFocusedSurface` a `DocumentEditor_Wysiwyg_CollaborationRemoteTextDoesNotResetCaretToDocumentStart`; oba jsou oznacene `Ignore`, protoze aktualni limit je Blazor snapshot/state refresh misto JS operation patcheru.
- API spustene na `https://localhost:5100`, WASM demo na `https://localhost:7106`.
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditor_Wysiwyg_Collaboration" --logger "console;verbosity=minimal"` prosel: 1 passed, 0 failed, 2 skipped.

## Faze 1: Operation identity a echo filtering

### 1.1 Operation identity v modelech

- [x] **RED:** Unit test ocekava `OperationId` na `DocumentOperation`.
- [x] **GREEN:** Pridat `OperationId` s defaultem generovanym pri vytvoreni operace.
- [x] **RED:** Unit test ocekava `OriginSessionId` nebo ekvivalent v metadata operace/batche.
- [x] **GREEN:** Naplnit metadata pri `SubmitLocalBatchAsync`.
- [x] **RED:** Serialization test overi roundtrip `OperationId`.
- [x] **GREEN:** Zachovat JSON kompatibilitu pro stare operace bez `OperationId`.

### 1.2 Idempotentni operation log

- [x] **RED:** Unit test aplikuje stejny remote batch dvakrat a ocekava jednu zmenu.
- [x] **GREEN:** Pridat cache aplikovanych `OperationId`.
- [x] **RED:** Unit test overi, ze batch bez `OperationId` dostane stabilni fallback jen jednou pri appendu.
- [x] **GREEN:** Fallback operation id generovat v sync vrstve, ne v provideru.

### 1.3 Vlastni echo z provideru

- [x] **RED:** Unit test `ReconnectAsync` ignoruje batch ze stejne session.
- [x] **GREEN:** Preskocit remote batch, pokud `remoteBatch.SessionId == _session.Id`.
- [x] **RED:** Unit test overi, ze stejne `ClientId` z jine session se neignoruje automaticky.
- [x] **GREEN:** Echo filter delat primarne podle session, idempotence podle operation id.
- [x] **RED:** E2E dva klienti overi, ze lokalni text se po provider echo nezdvoji.
- [x] **GREEN:** Demo provider/session flow upravit bez poruseni remote cursor filtru.

Vysledek 2026-05-14:

- `DocumentOperation` ma nove `OperationId`; legacy C# alias `Id` stale funguje, ale nove JSON serializace posilaji jen `OperationId`.
- Stare JSON payloady s `"Id"` se nacitaji pres kompatibilni legacy alias.
- `DocumentOperationMetadata` nese `OriginSessionId`.
- `SubmitLocalBatchAsync` doplnuje chybejici `OperationId`, `OriginSessionId`, `ClientId`, `AuthorId` a logicky timestamp v sync vrstve.
- `DocumentOperationLog.Append` filtruje duplicitni `OperationId` a mutateuje vstupni batch na unikatni operace, aby remote duplicate uz nebyl aplikovan.
- `ApplyRemoteBatch`/`ReconnectAsync` ignoruji vlastni echo podle `SessionId`; stejny `ClientId` z jine session se stale aplikuje.
- Unit testy: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentOperationEngineTests|FullyQualifiedName~DocumentCollaborationTests" --logger "console;verbosity=minimal"` prosly: 22 passed.
- Regrese WYSIWYG: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~WysiwygPatchApplierTests|FullyQualifiedName~TmDocumentEditorTests" --logger "console;verbosity=minimal"` prosla: 55 passed.
- E2E collaboration: API bezelo na `https://localhost:5100`, WASM demo na `https://localhost:7106`; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditor_Wysiwyg_Collaboration" --logger "console;verbosity=minimal"` prosel: 1 passed, 0 failed, 2 skipped.

## Faze 2: Granularni textove operace

### 2.1 Public operation typy

- [x] **RED:** Unit test ocekava `InsertText` operation v collaboration batchi pri WYSIWYG `InsertText` patchi.
- [x] **GREEN:** Pridat/aktivovat `DocumentOperationType.InsertText`.
- [x] **RED:** Unit test ocekava `DeleteText` operation pri WYSIWYG delete patchi.
- [x] **GREEN:** Pridat/aktivovat `DocumentOperationType.DeleteText`.
- [x] **RED:** Unit test overi target fields: `BlockId`, `InlineId`, `Offset`, `Length`.
- [x] **GREEN:** Rozsirit `DocumentOperationTarget` o granularni inline/range fields.

### 2.2 Mapovani WYSIWYG patchu na operations

- [x] **RED:** Test `CreateLocalEditBatch` nebo nova mapper sluzba mapuje `InsertText` patch bez porovnani celeho snapshotu.
- [x] **GREEN:** Zavest `DocumentWysiwygOperationMapper`.
- [x] **RED:** Test overi, ze rychle psani batchuje vice insertu do jedne transakce, ale porad jako text op.
- [x] **GREEN:** Zachovat `TransactionId` v operation metadata.
- [x] **RED:** Test overi, ze delete pres revizni span nevygeneruje plain text snapshot.
- [x] **GREEN:** Delete mapper rozlisi normal delete a tracked deletion.

### 2.3 Applier pro granularni text

- [x] **RED:** Unit test aplikuje remote `InsertText` do text runu.
- [x] **GREEN:** Implementovat `DocumentOperationApplier.ApplyInsertText`.
- [x] **RED:** Unit test aplikuje remote `DeleteText`.
- [x] **GREEN:** Implementovat `ApplyDeleteText`.
- [x] **RED:** Unit test overi transformaci offsetu pri dvou soubeznych insertech.
- [x] **GREEN:** Doplnit deterministic ordering podle timestamp/client/operation id.
- [x] **RED:** Unit test overi delete vs insert konflikt.
- [x] **GREEN:** Definovat pravidlo konfliktu a doplnit resolver.

Vysledek 2026-05-14:

- `DocumentOperationTarget` ma `InlineId` a `Length`; textove operace porad zachovavaji `InlineIndex`, `Offset` a `Text`.
- `DocumentOperationMetadata` ma `TransactionId`, `RevisionId` a `RevisionType`.
- `DocumentWysiwygOperationMapper` mapuje WYSIWYG `InsertText`, `DeleteRange`, `DeleteContentBackward` a `DeleteContentForward` na granularni `InsertText`/`DeleteText` operace bez celosnapshotoveho text diffu.
- `DocumentCollaborationSync.CreateLocalPatchBatch` pouziva mapper a `TmDocumentEditor` ho vola pro lokalni WYSIWYG patche; pokud patch nema granularni reprezentaci, zustava fallback pres `CreateLocalEditBatch`.
- `DocumentOperationApplier` umi cilit text run pres stabilni `InlineId` a `DeleteText` pouziva explicitni `Length`.
- Conflict resolver pouziva `Length` a `InlineId` pri textovych range transformacich.
- Unit testy: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentWysiwygOperationMapperTests|FullyQualifiedName~DocumentOperationEngineTests|FullyQualifiedName~DocumentOperationConflictResolverTests|FullyQualifiedName~DocumentCollaborationTests" --logger "console;verbosity=minimal"` prosly: 36 passed.
- Regrese WYSIWYG/editor: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~WysiwygPatchApplierTests|FullyQualifiedName~TmDocumentEditorTests" --logger "console;verbosity=minimal"` prosla: 55 passed.
- E2E collaboration: API bezelo na `https://localhost:5100`, WASM demo na `https://localhost:7106`; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditor_Wysiwyg_Collaboration" --logger "console;verbosity=minimal"` prosel: 1 passed, 0 failed, 2 skipped.

## Faze 3: Inline marks jako collaboration operace

### 3.1 Mark operation model

- [x] **RED:** Unit test ocekava `AddInlineMark` operation pro tucne.
- [x] **GREEN:** Pridat operation typ pro pridani marku.
- [x] **RED:** Unit test ocekava `RemoveInlineMark` operation pro odebrani tucne.
- [x] **GREEN:** Pridat operation typ pro odebrani marku.
- [x] **RED:** Unit test overi mark payload pro italic, underline, link a comment/revision mark.
- [x] **GREEN:** Pridat serializovatelny mark payload.

### 3.2 Mapper z WYSIWYG commandu

- [x] **RED:** Component test klikne na Bold a ocekava collaboration mark operation.
- [x] **GREEN:** `ToggleMark` neprevadet na plain text diff.
- [x] **RED:** Component test pro Italic/Underline overi samostatne operace.
- [x] **GREEN:** Doplnit mapper pro vsechny podporovane inline marks.
- [x] **RED:** Test overi, ze link edit neposila cely blok jako text.
- [x] **GREEN:** Link zmenu posilat jako mark payload.

### 3.3 JS aplikace remote marks

- [x] **RED:** JS/unit test nebo Playwright evaluate test ocekava, ze `applyRemoteOperation` obali range do mark span.
- [x] **GREEN:** Implementovat JS DOM patch pro `AddInlineMark`.
- [x] **RED:** Test ocekava odebrani marku bez ztraty textu.
- [x] **GREEN:** Implementovat JS DOM patch pro `RemoveInlineMark`.
- [x] **RED:** E2E dva klienti: klient A ztucni text, klient B vidi tucny text bez ztraty caret/focus.
- [x] **GREEN:** Napojit remote mark batch do JS hostu.

Vysledek faze 3:
- `DocumentOperationType` ma `AddInlineMark`/`RemoveInlineMark` se zpetne kompatibilnimi aliasy `AddMark`/`RemoveMark`.
- `DocumentWysiwygOperationMapper` mapuje `ToggleMark`/`SetMarks` na granularni mark operace pro bold, italic, underline, link, comment anchor a revision mark.
- `DocumentOperationApplier` umi pridat/odebrat mark v rozsahu ciloveho inline runu bez ztraty textu a bez slucovani cizich inline hranic.
- `TmDocumentWysiwygHost` umi prijmout remote mark operations a poslat je do JS DOM patcheru bez full snapshotu.
- JS `applyRemoteOperation(s)` umi obalit range do mark span a odebrat remote mark bez ztraty textu.
- Unit testy prosly: 158 passed pro WYSIWYG/collaboration/editor subset.
- E2E prosly: `DocumentEditor_Wysiwyg_CollaborationRemoteBoldMarkKeepsFocusedSurface` a `DocumentEditor_Wysiwyg_CollaborationOwnTypingIsNotDuplicatedAfterProviderEcho` (2 passed).

## Faze 4: Blokove a objektove operace bez degradace na text

### 4.1 Insert/update/remove block

- [x] **RED:** Unit test vlozi paragraph block jako `InsertBlock` s plnym `DocumentBlock`.
- [x] **GREEN:** Zachovat existujici `InsertBlock` pro nove bloky.
- [x] **RED:** Unit test updatuje heading level jako `UpdateBlock` nebo `SetBlockAttribute("headingLevel")`, ne jako text.
- [x] **GREEN:** Doplnit block attribute mapper.
- [x] **RED:** Unit test remove block overi idempotenci.
- [x] **GREEN:** Remove block ignoruje uz odstraneny block pri duplicitnim replay.

### 4.2 Image operace

- [x] **RED:** Unit test vlozi image block pres provider asset id a operation obsahuje plny image content.
- [x] **GREEN:** `InsertBlock` prenasi image content vcetne asset id, url, alt textu a layoutu.
- [x] **RED:** Unit test zmeni alt text existujiciho image bloku a ocekava image/block update operation.
- [x] **GREEN:** Implementovat `UpdateBlock` pro image content.
- [x] **RED:** Unit test zmeni velikost/floating layout image a neposila text diff.
- [x] **GREEN:** Mapper porovna image content strukturovane.
- [x] **RED:** E2E dva klienti: klient A vlozi obrazek, klient B ho vidi bez reloadu.
- [x] **RED:** E2E dva klienti: klient A zmeni alt text/velikost, klient B vidi update bez full reloadu.

### 4.3 Table operace

- [x] **RED:** Unit test vlozi tabulku jako `InsertBlock` s table content.
- [x] **GREEN:** Table insert zustane strukturalni.
- [x] **RED:** Unit test editace bunky vytvori cell text operation, ne text celeho blocku.
- [x] **GREEN:** Pridat target `TableCellId` nebo ekvivalent.
- [x] **RED:** Unit test pridani radku/sloupce vytvori table structural operation.
- [x] **GREEN:** Doplnit table operation mapper/applier.
- [x] **RED:** E2E remote edit bunky nezresetuje caret v jine bunce.

Vysledek faze 4:
- `DocumentOperationType` ma `UpdateBlock`; `DocumentOperationTarget` ma `TableCellId` pro cilene nested table operace.
- `DocumentWysiwygOperationMapper` mapuje `InsertBlock`, `UpdateBlock` a `RemoveBlock` bez degradace objektu na text; heading level jde pres `SetBlockAttribute("headingLevel")`, image/table structural zmeny pres `UpdateBlock`, edit jedne bunky pres `SetBlockAttribute("table.cell.text")`.
- `DocumentOperationApplier` umi idempotentne mazat blok, nahradit `UpdateBlock`, zmenit heading level a aplikovat text jedne tabulkove bunky.
- JS `applyRemoteOperation(s)` umi remote `InsertBlock`, `UpdateBlock`, `DeleteBlock`, `headingLevel` a `table.cell.text` aplikovat do DOM/snapshotu bez full reloadu editor surface.
- Unit testy prosly: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentWysiwygOperationMapperTests|FullyQualifiedName~DocumentOperationEngineTests|FullyQualifiedName~DocumentCollaborationTests|FullyQualifiedName~TmDocumentWysiwygHostTests" --logger "console;verbosity=minimal"` -> 116 passed.
- E2E prosly s API `https://localhost:5100` a WASM `https://localhost:7106`: `DocumentEditor_Wysiwyg_CollaborationRemoteImageInsertRendersWithoutReload`, `DocumentEditor_Wysiwyg_CollaborationRemoteImageUpdateRendersWithoutFullReload`, `DocumentEditor_Wysiwyg_CollaborationRemoteTableCellEditDoesNotResetCaret` -> 3 passed.

## Faze 5: Revize a suggestions jako operation stream

### 5.1 Revision operation model

- [x] **RED:** Unit test ocekava `CreateRevision` operation pri tracked insertion.
- [x] **GREEN:** Pridat revision operation payload.
- [x] **RED:** Unit test ocekava `CreateRevision` operation pri tracked deletion.
- [x] **GREEN:** Deletion nese puvodni text a range.
- [x] **RED:** Unit test ocekava `AcceptRevision` operation.
- [x] **GREEN:** Accept aplikuje stejne pravidlo jako lokalni accept.
- [x] **RED:** Unit test ocekava `RejectRevision` operation.
- [x] **GREEN:** Reject aplikuje stejne pravidlo jako lokalni reject.

### 5.2 JS revision rendering

- [x] **RED:** JS/Playwright test aplikuje remote tracked insertion a vidi zeleny revision span.
- [x] **GREEN:** `applyRemoteOperation` vykresli insertion revision v DOM.
- [x] **RED:** JS/Playwright test aplikuje remote tracked deletion a vidi cerveny preskrtnuty span.
- [x] **GREEN:** `applyRemoteOperation` vykresli deletion revision v DOM.
- [x] **RED:** JS/Playwright test accept/reject odstrani dekorace bez full snapshotu.
- [x] **GREEN:** Implementovat JS patch pro accept/reject revision.

### 5.3 Panel revizi a suggestions

- [x] **RED:** Component test overi, ze remote revision update aktualizuje panel bez ztraty focusu.
- [x] **GREEN:** Blazor dostane modelovou udalost po JS apply, panel se aktualizuje oddelene od editor surface.
- [x] **RED:** E2E dva klienti: klient A napise tracked insertion, klient B vidi span i panel item.
- [x] **RED:** E2E klient B acceptne revizi, klient A vidi accepted stav bez reloadu.
- [x] **GREEN:** Provider broadcastuje revision accept/reject jako operation batch.

## Faze 6: JS remote operation patcher

### 6.1 Verejne JS API

- [x] **RED:** JS test ocekava `window.tmDocumentEditorWysiwyg.applyRemoteOperationBatch(instanceId, batch)`.
- [x] **GREEN:** Pridat public JS metodu.
- [x] **RED:** JS test ocekava idempotenci podle `operationId`.
- [x] **GREEN:** JS instance drzi applied operation ids.
- [x] **RED:** JS test ocekava ordered apply podle sequence/order v batchi.
- [x] **GREEN:** Batch apply stabilne seradi operace.

### 6.2 DOM patch pro text

- [x] **RED:** JS test aplikuje remote insert do existujiciho inline.
- [x] **GREEN:** Najit inline podle `data-inline-id` a vlozit text node/span.
- [x] **RED:** JS test aplikuje remote delete.
- [x] **GREEN:** Delete zachova sousedni revision/mark spans.
- [x] **RED:** JS test remote insert pred lokalnim caretem posune selection.
- [x] **GREEN:** Implementovat selection transform.
- [x] **RED:** JS test remote insert za lokalnim caretem selection nemeni.
- [x] **GREEN:** Selection transform pouziva block/inline/offset.

### 6.3 DOM patch pro bloky

- [x] **RED:** JS test aplikuje remote insert paragraph.
- [x] **GREEN:** Vytvorit DOM block bez volani Blazoru.
- [x] **RED:** JS test aplikuje remote insert image.
- [x] **GREEN:** Vytvorit image DOM block z operation payloadu.
- [x] **RED:** JS test aplikuje remote update image layout.
- [x] **GREEN:** Upravit existujici DOM block in-place.
- [x] **RED:** JS test aplikuje remote remove block.
- [x] **GREEN:** Odstranit DOM block a transformovat selection.

### 6.4 DOM patch pro formatovani

- [x] **RED:** JS test aplikuje bold/italic/underline range.
- [x] **GREEN:** Rozdelit text node/inline podle range a pridat mark wrapper.
- [x] **RED:** JS test odebere mark jen z casti textu.
- [x] **GREEN:** Split/merge inline wrapperu po remove mark.
- [x] **RED:** JS test overi, ze sousedni kompatibilni text runy se slouci.
- [x] **GREEN:** Implementovat DOM normalizaci.

## Faze 7: Blazor bridge bez full snapshot rerenderu

### 7.1 Remote apply cesta

- [x] **RED:** Component test ocekava, ze remote batch zavola `ApplyRemoteOperationBatchAsync` na WYSIWYG hostu.
- [x] **GREEN:** Pridat metodu hostu pro JS remote apply.
- [x] **RED:** Component test ocekava, ze pri aktivnim focusu se nevola `RefreshSnapshotAsync`.
- [x] **GREEN:** Remote collaboration refresh pouzije JS operation apply, ne snapshot.
- [x] **RED:** Component test ocekava fallback na snapshot jen kdyz JS operation apply vrati failure.
- [x] **GREEN:** Implementovat fallback s lokalizovanou recovery zpravou.

### 7.2 Model sync po JS apply

- [x] **RED:** Component test overi, ze po remote JS apply je C# model aktualizovany pres stejny operation applier.
- [x] **GREEN:** `DocumentCollaborationSync` aplikuje batch do `_document`, JS pouze renderuje.
- [x] **RED:** Component test overi, ze panel revizi/komentaru se po remote apply prekresli.
- [x] **GREEN:** Blazor rerenderuje panely, ale neprepisuje editor surface.
- [x] **RED:** Component test overi, ze selection state se neprepise starou C# selection.
- [x] **GREEN:** Selection sync je jednosmerne JS -> C# po throttle.

### 7.3 Error handling

- [x] **RED:** JS apply failure test vrati konkretni failed operation id.
- [x] **GREEN:** Bridge zaloguje failed op a prejde na snapshot fallback.
- [x] **RED:** Provider failure behem remote apply nezablokuje lokalni psani.
- [x] **GREEN:** Error state zobrazit mimo editor surface.

## Faze 8: Provider contract a demo API

### 8.1 Provider boundary verze

- [x] **RED:** Contract test ocekava protocol version na operation batchi.
- [x] **GREEN:** Pridat `ProtocolVersion`.
- [x] **RED:** Contract test overi odmítnutí nepodporované vyšší verze.
- [x] **GREEN:** Provider/sync vraci validacni chybu bez padu UI.
- [x] **RED:** Contract test overi upgrade stareho text-only batch formatu.
- [x] **GREEN:** Pridat kompatibilni upgrade layer jen pro legacy data.

### 8.2 Demo HTTP/SignalR provider

- [x] **RED:** API test overi ulozeni `OperationId`, `SessionId`, `ProtocolVersion`.
- [x] **GREEN:** Upravit demo API storage.
- [x] **RED:** Client provider test overi fetch remote batches bez vlastnich echo batchu nebo s jasnym echo metadata.
- [x] **GREEN:** Upravit client provider podle rozhodnuteho contractu.
- [x] **RED:** SignalR wrapper test overi push remote operation batch.
- [x] **GREEN:** Doplnit SignalR path bez UI zavislosti v abstractions.

## Faze 9: E2E matice realne spoluprace

### 9.1 Text a caret

- [x] **RED:** Dva klienti pisou na ruznych radcich; remote text se objevi bez skoku caretu lokalniho klienta.
- [x] **GREEN:** Selection transform + JS patch stabilizuji caret.
- [x] **RED:** Dva klienti pisou do stejneho odstavce; vysledek je deterministicky.
- [x] **GREEN:** Resolver seradi soubezne inserty deterministicky.
- [x] **RED:** Klient drzi klavesu a remote update nezpusobi zadrhavani nebo batch skoky.
- [x] **GREEN:** Remote apply je throttlovany a DOM-only.

### 9.2 Formatting

- [x] **RED:** Klient A ztucni text, klient B vidi bold bez reloadu.
- [x] **RED:** Klient A zmeni kurzivu, klient B vidi italic bez reloadu.
- [x] **RED:** Klient A prida link, klient B vidi link bez reloadu.
- [x] **GREEN:** Mark operations a JS patcher pokryji formatting matici.

### 9.3 Images a bloky

- [x] **RED:** Klient A vlozi obrazek pres provider, klient B vidi obrazek bez reloadu.
- [x] **RED:** Klient A zmeni velikost obrazku, klient B vidi zmenu bez reloadu.
- [x] **RED:** Klient A odstrani obrazek, klient B ho ztrati bez full snapshotu.
- [x] **GREEN:** Image/block operations a JS patcher pokryji objektove zmeny.

### 9.4 Revize

- [x] **RED:** Klient A zapne sledovani zmen a pise; klient B vidi zelenou insertion revizi i panel.
- [x] **RED:** Klient A maze text; klient B vidi cervenou deletion revizi i panel.
- [x] **RED:** Klient B prijme revizi; klient A vidi text bez revision marku.
- [x] **RED:** Klient B odmitne revizi; klient A vidi spravny vysledek bez reloadu.
- [x] **GREEN:** Revision operations jsou end-to-end.

## Faze 10: Odstraneni legacy text-only cesty

### 10.1 Zakaz plain text degradace

- [x] **RED:** Unit test selze, pokud `CreateLocalEditBatch` pro existujici rich paragraph vytvori `SetBlockAttribute("text")`.
- [x] **GREEN:** Nahradit text-only diff granularnim mapperem.
- [x] **RED:** Unit test overi, ze inline marks zustanou po roundtrip collaboration batchi.
- [x] **GREEN:** Odstranit nebo omezit `SetBlockAttribute("text")` na legacy/import fallback.
- [x] **RED:** Unit test overi, ze image update neztrati content.
- [x] **GREEN:** Rich block update pouziva structured payload.

### 10.2 Cleanup Blazor snapshot remote renderingu

- [x] **RED:** Component test overi, ze remote batch pri aktivnim WYSIWYG hostu nevola `ApplySnapshot`.
- [x] **GREEN:** `ApplySnapshot` zustava jen pro initial load, document switch, recovery a read-only preview.
- [x] **RED:** E2E overi, ze remote update pri focusu nezpusobi skok na zacatek dokumentu.
- [x] **GREEN:** Odstranit zbytecne `StateHasChanged` z live editor surface flow.

### 10.3 Dokumentace a poznamky

- [x] Popsat provider operation protocol v README nebo docs.
- [x] Popsat, ze Blazor nerenderuje live editor surface pri remote ops.
- [x] Popsat fallback snapshot recovery.
- [x] Aktualizovat `planning/document-editor-ot-crdt-decision.md`.
- [x] Aktualizovat `planning/document-editor-collab-suggestions-export-compare-tdd-todo.md` odkazem na tento detailni plan.
- [x] Aktualizovat `AGENTS.md`, pokud pribudou nove public provider contracts. (N/A - nepribyl novy public provider contract.)

## Prikazy pro prubezne overovani

```bash
dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentCollaboration" --logger "console;verbosity=minimal"
dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~WysiwygPatchApplierTests|FullyQualifiedName~TmDocumentEditorTests" --logger "console;verbosity=minimal"
dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditor" --logger "console;verbosity=minimal"
```

## Poznamky pro implementaci

- Soucasny `SetBlockAttribute("text")` je uzitecny jako legacy fallback, ale nesmi byt hlavni cesta pro WYSIWYG collaboration.
- JS engine ma byt autoritativni pro zivy DOM a selection mezi input eventy.
- C# model zustava autoritativni pro ulozeny dokument, export, provider save, operation replay a panely.
- Remote operation apply ma byt atomicke: JS DOM patch + C# model apply + panel update musi skoncit ve stejnem logickem stavu.
- Pri konfliktu mezi lokalnim neflushnutym typing patchem a remote op je preferovany postup: flush local pending op, seradit podle operation metadata, aplikovat remote patch, transformovat selection.
