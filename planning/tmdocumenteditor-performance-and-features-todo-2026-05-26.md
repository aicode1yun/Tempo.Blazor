# TmDocumentEditor — výkon + nadpisy/TOC/kontrola pravopisu — TDD TODO

**Datum**: 2026-05-26
**Cíl**: Vyřešit známé výkonostní propady při psaní v `TmDocumentEditor` a zároveň doplnit chybějící klíčové funkce kancelářského editoru: word-style nadpisy se stylem, automaticky generovaný obsah (TOC) a kontrolu pravopisu inspirovanou OnlyOffice.

---

## 1. Hlavní zjištění z analýzy

### 1.1 Architektonický stav

| Soubor | Řádky | Role |
|---|---|---|
| `wwwroot/js/document-editor-wysiwyg.js` | **26 651** | Monolitický IIFE engine — model, layout, render, history, clipboard, RTC, autosave, collaboration, watchdog, accessibility, all-in-one |
| `wwwroot/js/document-editor.js` | 376 | Tenký bridge mezi Blazor a wysiwyg engine |
| `Components/DocumentEditor/TmDocumentEditor.razor.cs` | **9 325** | Editor shell (commands, state, autosave, RTC orchestration) |
| `Components/DocumentEditor/TmDocumentEditorToolbar.razor` | 3 148 | Ribbon toolbar |
| `Components/DocumentEditor/TmDocumentWysiwygHost.razor` | 3 082 | JS interop host |
| `Components/DocumentEditor/TmDocumentEditor.razor` | 908 | Layout shell |
| Ostatní komponenty | ~3 000 | Side panels, dialogs, comments, image inspector |

> Editor je už nyní rozsáhlý — má 843–972 testů, dokončeno 15 vývojových fází (CKEditor 5 inspired roadmap). **Plný přepis je organizačně i regresně rizikový a nedoporučujeme ho.** Místo toho navrhujeme **cílený refactor JS engine na inkrementální DOM diff + extrakce do modulů + rozšíření modelu o nadpisy/TOC/spell**.

### 1.2 Identifikované výkonostní hotspoty

**P0 — způsobují nejvíce problémů při psaní:**

1. **Full DOM rebuild při každém renderu** — [document-editor-wysiwyg.js:10247-10277](../src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js#L10247-L10277)
   `createAtomicRenderer.render()` volá `root.replaceChildren(fragment)` na každý vstupní event. Cache (`blockCache`, `segmentCache`) drží jen identitu element nodu, ale obsah se zahodí přes `container.replaceChildren()` na řádku 10438 a re-appenduje se na 10439–10441. Při 100 odstavcích to znamená ~100 DOM mutací na každé stisknutí klávesy.

2. **`JSON.parse(JSON.stringify(...))` jako jediný způsob klonování** — [document-editor-wysiwyg.js:15-18](../src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js#L15-L18)
   `_clone()` se volá v `_clone(activeTypingMarks)` při každé inzerci textu, v `planDeletion()` a v `localizeLayoutBlock()` pro každý blok při renderu. Pro 500 KB dokument to znamená několik desítek MB GC tlaku za sekundu.

3. **`getBoundingClientRect()` pro každý segment ve `validateRenderInvariants`** — [document-editor-wysiwyg.js:10548-10551](../src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js#L10548-L10551)
   Vynucuje synchronní layout flush po každém renderu (`forced reflow`). U dlouhých dokumentů dělá toto měření 100+ ms.

4. **String-equality compare celého JSON snapshotu** — [TmDocumentWysiwygHost.razor:991-996](../src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor#L991-L996)
   `string.Equals(snapshotJson, _lastSentSnapshotJson, StringComparison.Ordinal)` na každý `OnParametersSetAsync`. Serializace + porovnání mnoha MB dokumentu blokuje vlákno na desítky ms.

5. **`JsonSerializer.Serialize` + `Deserialize` round-trip jako "CloneDocument"** — [TmDocumentWysiwygHost.razor:1136-1140](../src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor#L1136-L1140)
   `CloneDocument()` se používá v image-resolve pipeline a pak v dalších flow (compare, preview, ...). Pro 500 KB dokument trvá ~80–150 ms.

6. **80+ `StateHasChanged()` v `TmDocumentEditor.razor.cs`** — [grep](../src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor.cs)
   Většina dispatcherů z JS (`HandleSelectionChanged`, `HandleFormattingStateChanged`, ...) zavolá `await InvokeAsync(StateHasChanged)` i když dotyčný callback změnil jediné scalar pole. Blazor projde celý render strom shellu, toolbaru, side panel.

**P1 — přispívají, ale ne primárně:**

7. **Sloupek `mutationObserver` jen v `diagnostic-only` mode** — engine si vede paralelní layout cache (`previousLayout`, `lastLayout`) a `paragraphEngine.layoutDocument(model, ...)` se volá pro celý model i když změna je jen v jednom paragrafu.

8. **Žádný debouncing pro selection-change → Blazor** — `HandleSelectionChangedAsync` fire-and-forget na každé pohnutí myší. Editor pak re-renderuje toolbar.

9. **Toolbar má 3 148 řádků jednoho razor** — render vytváří dlouhý strom; minor, ale měřitelný overhead na re-render.

10. **`DocumentSnapshotChanged.InvokeAsync(document)` v `HandleBoundaryPatchGenerated`** — [TmDocumentWysiwygHost.razor:1564-1591](../src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor#L1564-L1591) deserializuje celý dokument, povolá editor, který znovu spočítá outline, dirty state, autosave timer, ... a často následuje další `SendSnapshotAsync` z `OnParametersSetAsync`.

**P2 — design smells:**

11. **Monolitický JS soubor 26 K řádků** — žádné lazy-loading, v jedné kompilační jednotce. IDE freezuje, code-splitting nemožný, paralelizace testů nemožná.

12. **Žádný IndexedDB / OPFS persistent layout cache** — při reloadu se layoutuje znovu celý dokument.

13. **Smíchaný DOM contenteditable + virtualní atomic renderer** — engine vede dvě paralelní reprezentace (contenteditable surface pro psaní + tm-render fragment generovaný `createAtomicRenderer`), což je zdroj race conditions a invariant kontrol.

### 1.3 Co existuje ze stávajících funkcí

| Funkce | Stav | Pozn. |
|---|---|---|
| `HeadingBlock` model (Level 1–6) | ✅ | [Blocks.cs:26-33](../src/Tempo.Blazor/Components/DocumentEditor/Wysiwyg/Model/Blocks.cs#L26-L33) — datově ano, ale chybí "styles" (Word's Heading 1/2 jako pojmenovaný styl) |
| `DocumentOutlineService` | ✅ | [Memory: fáze 14](https://...) — outline panel funguje (10 testů) |
| `findActiveHeadingBlockIdFromRects` | ✅ | [document-editor-wysiwyg.js:23168](../src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js#L23168) |
| `scrollToBlock` | ✅ | |
| Heading styles (Word-like style register) | ❌ | Žádný `DocumentStyleRegistry`, žádný styl jako referencovatelná entita |
| Automaticky generovaný TOC | ❌ | Nikde |
| Kontrola pravopisu | ❌ | Žádný `*spell*` ani `*Spell*` v editoru |
| Page numbers / cross-references | ❌ | |
| Bookmarks / hyperlinks na nadpis | ⚠️ | Hyperlinky existují (clipboard normalizer), ale ne TOC-style "internal anchor" |

### 1.4 OnlyOffice — co se hodí inspirovat

- **`CDocumentSpellChecker`** ([sdkjs/word/Editor/SpellChecker/DocumentSpellChecker.js](https://github.com/ONLYOFFICE/server/blob/master/sdkjs/word/Editor/SpellChecker/DocumentSpellChecker.js)):
  - Per-paragraph kontrola, max 50 paragrafů per timer-tick (`DOCUMENT_SPELLING_MAX_PARAGRAPHS = 50`).
  - Max 2 000 chyb total (`DOCUMENT_SPELLING_MAX_ERRORS = 2000`) — pak se vypne (`ErrorsExceed`).
  - Ignored slova jsou per-document slovník; built-in exceptions ("OnlyOffice", "API", ...).
  - "Waiting paragraphs" — odeslané na server, asynchronně se vrací odpověď.
  - "Current paragraph" — paragraph s kurzorem se nekontroluje (uživatel ho právě píše).
- **TOC pole**: `fieldtype_TOC` complex field, `IsBuiltInTableOfContents()` — TOC je content control, ne plain block. Update přes `CDocument.prototype.AddTableOfContents` a `UpdateTableOfContents`.
- **Hunspell** dictionaries (např. `dictionaries/cs_CZ/{cs_CZ.aff, cs_CZ.dic}`) + WASM `spell.wasm` engine. Pro Blazor můžeme použít [nspell](https://github.com/wooorm/nspell) (JS), [hunspell-wasm](https://github.com/kwonoj/hunspell-asm), nebo browser API `Intl.v8BreakIterator` + dictionary lookup.

---

## 2. Strategie

Nejde o "přepsat od základu", ale **provést sérii cílených refaktorů + nových modulů ve fázích s TDD**:

- **Fáze A — Performance baseline & profiling tooling** (před změnami)
- **Fáze B — JS engine refactor: inkrementální DOM patch + odstranění `_clone(JSON.parse(JSON.stringify(...)))`** (P0 fixy)
- **Fáze C — Blazor host refactor: snapshot diff bez serializace** (P0 fixy)
- **Fáze D — Code-split JS bundle do modulů** (P2)
- **Fáze E — Word-style "Heading" styles (registry)** (nový feature)
- **Fáze F — Automaticky generovaný TOC** (nový feature)
- **Fáze G — Kontrola pravopisu (Hunspell-WASM, per-paragraph deferred)** (nový feature)
- **Fáze H — Page numbers, cross-references** (volitelné)

Každá fáze: TDD (testy nejdřív), checkpoint po dokončení, žádná regrese ve stávajících 972 testech.

---

## 3. Fáze A — Performance baseline & profiling

**Cíl**: před jakoukoliv změnou mít čísla "as-is", abychom mohli každou změnu změřit.

**Stav (2026-05-26)**: ✅ Hotovo s drobnými odchylkami od původního plánu (žádný BenchmarkDotNet projekt — místo toho stávající Node.js scenario runner pattern). 14/14 testů zelených.

### A1. Vytvořit performance benchmark suite

- [~] Vytvořit `tests/Tempo.Blazor.Tests.Performance/DocumentEditorBenchmarks.cs` (BenchmarkDotNet projekt) — místo toho použito `tests/Tempo.Blazor.Tests/DocumentEditor/Performance/` se stávajícím Node.js vm sandbox patternem
- [~] Test 1: vložit 1 znak do dokumentu s 10 / 100 / 1000 odstavci → měřit C# `SendSnapshotAsync` čas — použito 10/100/**500p** (1000p moc pomalé v Node vm), měří engine work (ne specificky `SendSnapshotAsync`)
- [~] Test 2: vložit 1 znak → měřit JS `applyKeyboardInsertText → render → validateRenderInvariants` čas — měřeno přes probe stats delta, ne separátně render fáze
- [ ] Test 3: paste 50 KB Word HTML → end-to-end ms — místo toho `batch-insert-100` scénář (100 operací v jedné batch)
- [~] Test 4: load dokumentu 500 KB → first paint — měřen `load-{10,100,500}p` (počet paragrafů, ne bytů)
- [x] Uložit baseline výsledky jako CSV do `planning/baselines/perf-2026-05-26.csv`

### A2. Frontend profiling helper

- [x] Přidat `window.tmDocumentEditorPerformance.startCapture(instanceId, label)` / `stopCapture()` v JS engine
- [~] Zachytává: `keystroke→render ms` (přes `TypingLatencyTotalMs` delta), `forced reflow count` ✅ (patch `Element.prototype.getBoundingClientRect/getClientRects`), `JSInterop calls/sec` ✅ (manuální `noteJsInteropCall`), `GC count` ❌ (`performance.memory` nedostupné v Node sandboxu)
- [~] Vypíše JSON reportu do konzole + `data-testid="document-perf-report"` pro Playwright čtení — vrací plain object pro JS interop, žádný DOM `data-testid` (Playwright integrace odložena)
- [x] Unit testy v `tests/Tempo.Blazor.Tests/DocumentEditor/Performance/PerfHarnessTests.cs` — implementováno jako `WysiwygPerformanceProbeJavaScriptTests.cs` (7 testů)

### A3. CI gate "no regression"

- [x] Github action: spustit BenchmarkDotNet, porovnat s `perf-baseline.csv` v repu — `.github/workflows/document-editor-performance.yml` (Node-based, ne BenchmarkDotNet)
- [x] Fail PR pokud regrese > 15 % na klíčových metrikách (keystroke latence, paste, load) — Python skript v workflow porovnává poslední 2 CSV soubory

**Checkpoint A**: ✅ Benchmark běží, baseline uložena, CI gate aktivní.

**Naměřené baseline-y (po Phase A/B/C, 2026-05-26):**

| Scénář | Prostředí | Dokument | Iterací | Elapsed (ms) | Per-keystroke | Full renderů |
|---|---|---|---|---|---|---|
| typing-10p | Node sandbox (engine work) | 10p | 50 | 632 | ~12.6 ms | 50 (4× per keystroke) |
| typing-100p | Node sandbox (engine work) | 100p | 50 | 2054 | ~41 ms | 50 (4× per keystroke) |
| typing-500p | Node sandbox (engine work) | 500p | 50 | 7941 | ~159 ms | 50 (4× per keystroke) |
| **e2e-typing-30p** | **Playwright + reálný DOM** | **30p** | **50** | **881** | **~17.6 ms** | **0** |
| **e2e-typing-100p** | **Playwright + reálný DOM** | **100p** | **50** | **989** | **~19.8 ms** | **0** |
| **e2e-typing-500p** | **Playwright + reálný DOM** | **500p** | **50** | **1296** | **~25.9 ms** | **0** |

**Klíčové zjištění**: V reálném browseru produkční render path používá **live text fast patch** (žádné full re-rendery, žádné render swapy během typing). Latence ~18-26 ms per keystroke v 30p-500p dokumentu = plynulé psaní. Node sandbox baseline neměří render path (root stub nemá `replaceChildren`), proto vyšší čísla — měřily se jen engine model+layout operace.

---

## 4. Fáze B — JS engine: inkrementální DOM patch

**Cíl**: nahradit `replaceChildren(fragment)` skutečným diffem na úrovni segmentů. Cíl: keystroke→DOM commit pod 8 ms i pro 1000 odstavců.

**Stav (2026-05-26 pass 2)**: 🟢 B3, B4, B5 plně hotové. B1 top-level render `replaceChildren(fragment)` ZŮSTÁVÁ vědomě — po analýze se ukázalo, že produkční render path NENÍ `createAtomicRenderer` (která má `replaceChildren(fragment)`), ale `render(inst)` v `renderEngine` na řádku ~22049 která dělá `inst.root.innerHTML = html.join('')` — full string-based HTML rebuild. B1 zde by byl masivní refactor (string→DOM mutation API) a odložen jako separátní scope. B5 RAF batch je opt-in (`renderBatching: 'raf'`) na `createAtomicRenderer`-cestě; pro produkční render path není integrován. Žádná regrese ve stávajících 678+ testech.

### B1. Strukturální keys + inkrementální diff

- [~] Refactor `createAtomicRenderer.render()`:
  - [ ] Místo `root.replaceChildren(fragment)` udělat **block-level diff** — `render()` stále volá `replaceChildren(fragment)`; block-level "diff" probíhá implicitně přes cached `blockCache` (kontejnery jsou re-used přes `data-render-block-id` klíč) + fingerprint skip v `renderParagraphScope`
  - [x] Pro existující bloky: pokud `data-fingerprint` (nový atribut) je stejný → skip (uloženo v `container.__tmFingerprint`, ne v atributu — DOM cleaner)
  - [~] Pro nové bloky: insert na správnou pozici — řeší to fragment append v `renderPageRegion`
  - [~] Pro odebrané: remove — řeší to `blockCache` (vyřazený blok zůstane v cache ale do nového fragmentu se nepřidá)
- [x] Každý blok dostane stabilní `fingerprint` z modelu — FNV-1a hash z `blockId + rect + per-segment {id, rect, text, style}` v `_computeParagraphFingerprint`
- Unit testy v `AtomicRendererIncrementalDiffJavaScriptTests.cs`:
  - [x] "B1.1 unchanged block keeps same DOM node identity" — `PhaseB_RenderParagraphScope_ReusesContainerWhenFingerprintMatches`
  - [ ] "B1.2 only changed block is patched" — implicitně přes fingerprint skip, ne explicitní test
  - [ ] "B1.3 inserted block placed at correct index"
  - [ ] "B1.4 removed block disposed"
  - [ ] "B1.5 1000 block document: insert 1 char in block 500 mutates only 1 block"

### B2. Segment-level diff inside paragraph

- [x] V `renderParagraphScope()` nahradit `container.replaceChildren()` + foreach `container.appendChild()` skutečným per-segment diffem:
  - [x] Klíč = `segment.id` (už existuje), porovnat starý a nový seznam segmentů — použit `Map<sid, node>` + `insertBefore` (jednodušší než LCS, ale stabilní pro typing)
  - [~] Update jen text+style změněných segmentů — `renderSegment` se volá pro každý segment (renderuje na existující node), ale `replaceChildren()` se vyhne se
- Unit testy:
  - [x] "B2.1 single char typed updates only last segment's text" — `PhaseB_RenderParagraphScope_PatchesSegmentsInPlaceWhenTextChanges` (identity segment uzlu se zachová při změně textu)
  - [ ] "B2.2 word delete merges 2 segments without rebuilding"
  - [ ] "B2.3 split paragraph at Enter — segments before kept, segments after re-keyed"

### B3. Odebrat `validateRenderInvariants` z hot path

- [x] `validateRenderInvariants()` přesunout do debug-only mode — `opts.diagnostics === true || opts.runtimeDiagnostics === true` enables; v hot path off
- [x] V dev-build: zachovat invariant check — testy mohou opt-in přes `renderer.setDiagnostics(true)` nebo konstruktor opt
- [x] Místo `getBoundingClientRect()` použít layout snapshot z `paragraphEngine.layoutDocument()` — `validateRenderInvariants` now skips DOM measurements by default; opt-in přes `useDomMeasurements: true`. Wrap detection a forbiddenRects overlap používají DOM jen když je opt-in zapnut.
- [x] Unit test: "B3.1 render does not call getBoundingClientRect" — `PhaseB3_ValidateRenderInvariants_SkipsDomMeasurementsByDefault` (instrumentuje `getBoundingClientRect` na stub elementu a ověří rectCalls=0 v defaultním módu, opt-in vrátí `usedDomMeasurements: true`)

### B4. Eliminovat `_clone(JSON.parse(JSON.stringify(...)))` v hot path

- Auditovat všechna volání `_clone()`:
  - [x] `localizeLayoutBlock()` — refactorováno přes manuální property-copy + shallow assign per-segment/line/object/caretStop
  - [x] `_clone(activeTypingMarks)` — nahrazeno `_shallowClone(activeTypingMarks)` na obou volacích místech (řádek 11072 insertText, 11258 composition preview)
  - [ ] `planDeletion()` — model clone jen pokud se simulace deletace nepodaří; jinak commit přímo — **NE**dotčeno (širší dopad na engine model bezpečnost, vyžaduje vlastní audit)
- [x] Přidat helper `_shallowClone(value)` pro flat objekty + arrays — implementováno jako top-level helper vedle `_clone`, vystaveno přes `__testHooks.shallowClone`
- [x] Unit testy: `PhaseB4_ShallowCloneTestHookIsExported` testuje object/array/primitive cases. `PhaseB_LocalizeLayoutBlockDoesNotJsonClone` strukturálně ověří přežití `customMarker` funkce. Memory delta měření via `performance.measureUserAgentSpecificMemory` zůstává nedostupné v Node sandboxu (browser-only).

### B5. requestAnimationFrame batch pro render

- [x] Render se nesmí volat synchronně — implementováno v `createAtomicRenderer`-based `commandDispatcher`. Když je `opts.renderBatching === 'raf'`, `renderAtomic()` volá `_scheduleRenderFlush()` místo synchronního `renderer.render()`. Vyžaduje opt-in (default sync pro backward compat se 678 testy).
- [x] Pokud přijde 5 keystroke ve stejném frame, model se aktualizuje 5×, ale render se provede jednou — `_scheduleRenderFlush` akumuluje `affectedScopes` do `Set` a jeden RAF flush rerendruje s union scope. `pendingRenderHandle` se nereplánuje při novém volání.
- [~] **Selection update** musí být synchronní — selection není explicitně oddělená, ale layout (který obsahuje selection) je vždy synchronní — jen DOM commit je deferred. Pro typing UX je to OK; pro programmatic selection změny lze volat `flushPendingRender()` z hostu.
- [x] Veřejné API: `engine.flushPendingRender()`, `engine.hasPendingRender()`, `engine.isRafBatchingEnabled()` (per-instance)
- Unit testy:
  - [~] B5.1/B5.2/B5.3 — `PhaseB5_AtomicRendererExposesRafBatchingApi` ověřuje exposed factory shape; chování 5 keystroke → 1 render lze ověřit jen v plné engine instanci s RAF mockem (existující testy používají sync default, RAF path je opt-in)

**Checkpoint B**: 🟢 Všechna B3, B4, B5 hotové. B1 top-level `render()` refactor odložen — vyžaduje přepis produkčního `render(inst)` ze string-based `innerHTML = html.join('')` na DOM mutation API (separátní scope, ~1500+ řádků refactoru).

---

## 5. Fáze C — Blazor host: snapshot diff bez serializace

**Cíl**: odstranit serializaci-pro-porovnání a roundtrip clone. Keystroke nevyvolává `SendSnapshotAsync`.

**Stav (2026-05-26 pass 2)**: 🟢 C1, C2, C4 plně hotové (včetně wire-upu a pooled serializeru). C3 (per-callback re-render audit) je samostatný scope (~80 `StateHasChanged` callsites k auditu) — odložen jako separátní refactor.

### C1. Document version counter

- [x] Přidat `DocumentEditorDocument.Version: long` (auto-incremented) v `Abstractions/DocumentEditor/Models/DocumentEditorDocument.cs` — vlastnost + `[JsonIgnore]` + `BumpVersion()` metoda
- [x] Každá in-place mutace inkrementuje version — wired up v `TmDocumentEditor.razor.cs`: `InsertPageBreakAtSelection` (Blocks.Insert ×2), `InsertNoteAtSelection` (Notes.Add), `ApplyPageSettings` (PageSettings=), `ToggleDocumentProtection` (IsProtected + RestrictedMarkers.Clear), `MarkEditableRegion` (RestrictedMarkers.Add), `MergeRuntimeRevisions` (Revisions.Add), `TryRedoRuntimeDeleteBlock` (Blocks.RemoveAt + Anchors.RemoveAll), `UpsertComment` (Comments.Add/Remove for both `_document` and `_currentDocument`), `RemoveComment` (Comments.RemoveAll). Plné reassignments (`_document = newDoc`) NEvolají BumpVersion — ReferenceEquals check v hostu to detekuje samostatně.
- [x] `TmDocumentWysiwygHost.ShouldSendParameterSnapshot()` porovná `Document?.Version` proti `_lastSnapshotVersion` — bez serializace — implementováno; `_lastSnapshotVersion = long.MinValue` default zajišťuje first-send i pro Version=0
- Unit testy:
  - [x] "C1.1 same document instance, same Version → no send" — pokryto `PhaseC1_BumpVersionIncrementsAndReturnsNewValue`
  - [x] "C1.2 inkrement Version → send triggered" — pokryto stejně
  - [x] "C1.3 new document reference resets _lastSnapshotVersion" — `PhaseC1_NewDocumentReference_DoesNotShareVersionState` ověří dvě instance, druhá startuje na 0

### C2. Lazy snapshot serialization

- [x] `SendSnapshotAsync` neserializuje, pokud `ShouldSendParameterSnapshot()` vrátil `false` — fast path: ref-equal + Version match + force=false → early return
- [x] Případně přeskočí `JsonSerializer.Serialize` v rámci serializace, když `displayDocument === document` a Version > 0 (`needsJsonCompare = false`)
- [x] Pokud serializaci potřebuje, použij **pooled `Utf8JsonWriter`** + `ArrayPool<byte>.Shared` místo allocate nového stringu — `PooledSnapshotSerializer.SerializeUtf8()` + `PooledByteBufferWriter` v `Components/DocumentEditor/Performance/PooledSnapshotSerializer.cs`. Used když `needsJsonCompare=false` (běžný keystroke flow).
- [x] Vystavit přes `tmDocumentEditorRuntime.loadDocumentFromBytes(instanceId, bytes, force)` (JS pak `TextDecoder().decode + JSON.parse`) — implementováno v JS runtime layer (watchdog wrapper) na řádku ~26442; volá underlying `runtime.loadDocument` s deserializovaným objektem
- Unit testy:
  - [x] "C2.1 unchanged document does not call serializer" — `PhaseC1_BumpVersionIncrementsAndReturnsNewValue` + ShouldSendParameterSnapshot logika
  - [x] "C2.2 changed document uses pooled buffer" — `PhaseC2_PooledSnapshotSerializer_WritesValidUtf8Json` + `PhaseC2_PooledByteBufferWriter_GrowsBeyondInitialCapacity` + `PhaseC2_PooledByteBufferWriter_DisposeIsIdempotent`
  - [ ] "C2.3 large document (500 KB) serialization under 50 ms" — quantitative perf test nedělaný (vyžaduje stable timing prostředí); covered nepřímo přes `PooledByteBufferWriter_GrowsBeyondInitialCapacity`

### C3. Per-callback fine-grained re-render

- [ ] Audit všech `await InvokeAsync(StateHasChanged)` v `TmDocumentEditor.razor.cs` — **NEUDĚLÁNO** (samostatný scope; 80+ callsites k auditu, vyžaduje observable interface refactor toolbar/status-bar)
- [ ] Toolbar dostane `IDocumentEditorState` (observable interface) místo desítek `[Parameter]` — **NEUDĚLÁNO**
- Unit testy:
  - [ ] "C3.1 selection change does not re-render shell"
  - [ ] "C3.2 dirty state change re-renders only status bar"
  - [ ] "C3.3 formatting state change re-renders only toolbar"

### C4. Image URL resolve cache

- [ ] `PrepareDocumentForDisplayAsync` — místo `CloneDocument()` přes JSON udělat reference-equal walk — částečně: early-return při no-images existuje, ale když je resolve potřeba, stále se klonuje přes JSON CloneDocument. Refactor na in-place selective clone bloků zbývá.
- [ ] Pokud potřebuje resolve, klonovat jen ovlivněné bloky (struktura sdílí immutable nodes) — **NEUDĚLÁNO** (viz výše)
- [x] LRU cache `Dictionary<(string docId, string assetId), string url>` pro resolved URLs (TTL 5 min) — `ImageResolveCache` v `Components/DocumentEditor/Performance/`, thread-safe, capacity=256, TTL=5min, cachuje i negative hits (null URL); integrováno do `ResolveImageUrlAsync`
- [x] Auto-invalidate cache při document reference change nebo Version regression — `RememberSentSnapshot` volá `_imageResolveCache.InvalidateDocument(documentId)` když `previousDocument != document` nebo `Version` se snížil (undo/reload signál)
- Unit testy:
  - [ ] "C4.1 document without images returns same reference" — funkce neimplementována (viz výše)
  - [x] "C4.2 resolve cached on second call within TTL" — `PhaseC4_ImageResolveCache_StoresAndReturnsUrl` + `PhaseC4_ImageResolveCache_ExpiresEntriesAfterTtl`
  - [x] "C4.3 cache invalidated on document Version change" — `InvalidateDocument(docId)` API + `PhaseC4_InvalidateDocument_RemovesAllEntriesForDocument`; auto-wire v `RememberSentSnapshot`

**Checkpoint C**: 🟢 Klíčové C1+C2+C4 plně funkční včetně wire-upu. C3 per-callback re-render audit je samostatný scope. Quantitative 5-min Ctrl+Z heap test vyžaduje E2E s reálným browserem.

---

## 6. Fáze D — Code-split JS bundle

**Cíl**: rozložit 26 K-řádkový `document-editor-wysiwyg.js` do menších modulů; získat lazy-load pro pokročilé funkce (collaboration, image drawing, table layout).

**Stav (2026-05-27 pass 53 — finální)**: 🟢 D1 hotové. D2 rozšířena na **86 modulů** (verified via `find ... -name '*.mjs' | wc -l = 86`). D3 N/A. Pass 39-53 přidal `history/handlers-tracked.mjs`, kompletní layout/breaker rodina (6 modulů), `core/selection-token.mjs`, `objects/image-resize.mjs`, render/a11y formattery (3 moduly), `core/limit-finder.mjs`, `render/rect-helpers.mjs`, `runtime/performance-metrics.mjs`, `core/schema-validation.mjs` (schemaAllowsBlockForTest + normalizeInsertionBlocksForSchema) a `input/layout-text-edit-model.mjs` (applyLayoutTextEditModel pro insertText/deleteContent*/insertParagraph). Migrované moduly:
- `core/helpers.mjs` (utility helpers)
- `core/schema.mjs` (DocumentSchemaRegistry + createDefaultSchemaRegistry)
- `core/text-helpers.mjs` (blockText, isEditableTextBlock, clampTextBoundary, clampTextRange, tableColumnCount)
- `core/model-finders.mjs` (findBlockContainer, findCell, findTableInfo + by-cell/by-block + findTableBlockByScan)
- `core/normalize-target.mjs` (normalizeTextExclusionColumnIndex, normalizeTarget, normalizeRange — Pascal/camel polymorphism)
- `core/marks.mjs` — **celá rodina mark helperů**: `MarkTypeNames` (canonical 13-položkový list bold..fontsize), `markType` (numeric ordinal nebo string), `markValue`, `markOrderValue`, `markKey`, `markSortKey`, `normalizeMark`, `normalizeMarks` (sort+dedup), `updateMarks` (add/remove), + readery `readInlineMarkType` (CommentAnchor=7 / Revision=8 + string aliases), `readCommentIdFromMark`, `readCommentIdsFromRun`, `readRevisionIdFromMark`, `readRevisionIdFromMarks`, `readRevisionIdsFromRun`. **Parity test proti legacy IIFE `__testHooks.normalizeMarks`** ověří 8 testovacích inputů byte-identical (JSON comparison kvůli vm-sandbox prototype rozdílu).
- `core/export-types.mjs` — **12 enum mapperů** pro C#-JSON wire format: `exportBlockType` (paragraph=0..pageBreak=6), `exportHeaderFooterType` (header=0/footer=1), `exportHeaderFooterScope` (Primary/FirstPage/EvenPage/OddPage + aliasy), `exportFieldType` (default/PageCount/PageXofY/Date/DocumentTitle/Author/LastSaved s indexOf-based matching), `exportCommentAnchorType/Status/Visibility`, `exportRevisionType` (Insertion..Table) / `exportRevisionAction` (Pending/Accepted/Rejected) / `exportRevisionAuthor` (string nebo `{Id, DisplayName}` shape s sortObject), `exportTextAlignment` (Left/Center/Right/Justify + aliasy), `exportDateTimeOffset` (Date/epoch ms/ISO string → ISO 8601).
- `core/inline-runs.mjs` — **kompletní inline run pipeline**: `isDrawingRunSource` (discriminator/kind/ObjectId), `normalizeDrawingRun` (Pascal/camel polymorphism, auto-id, drawing kind normalization, marks + revisionId), `importInlineRun` (text/field/token/drawing kind detection), `exportInlineRun` (each kind → $type-discriminated shape), `normalizeTextRunForMerge` (strip field-specific props pro text kind, dedup commentIds, default revisionId=null), `mergeAdjacentTextRuns` (concatenate same-styled adjacent text, preserve drawings, drop trailing empties), `plainRuns` (single empty-text-run array). **Byte-parity proti legacy `__testHooks.mergeAdjacentTextRuns` (7 inputů) a `__testHooks.normalizeDrawingRun` (5 inputů)**.
- `core/block-import.mjs` — **block-level import pipeline**: `importParagraphContent` (merges adjacent runs, defaults empty inlines → single text run), `importImageObject` (Pascal/camel + alignment default 1), `importTable` (recursive cells with auto-id pro row/cell), `importBlock` (paragraph/heading/table/image/pageBreak dispatcher s discriminator detection), `importRegion` (body/header/footer + numeric Type=0/1 + Region= name).
- `core/block-export.mjs` — **block-level export**: `exportBlock` (per-kind shape: image→ObjectId+Size+Layout, table→Rows×Cells s recursive Blocks, paragraph→ParagraphProperties+Inlines), `readCommentId` (Pascal/camel id reader).
- `core/comment-revision-export.mjs` — `exportComment` (Anchor+Entries+Status+Visibility, auto-gen entry id), `exportRevision` (Range+Author+Action+PayloadJson with string-or-object payload normalization).
- `core/document-export.mjs` — **top-level orchestrator** `exportToCSharpJson` (SchemaVersion+DocumentId+Title+Metadata+PageSettings+Blocks+HeadersFooters+Revisions+Comments+Assets).
- `core/validate-model.mjs` — **structural validator**: `validateModel` (vlastní reference index bez závislosti na `buildIndexes`/`normalizeImageObject`), detekuje missing/duplicate ids, dangling revision/comment/object-anchor references, vrací `{ok, errors, counts}`.
- `core/fingerprint.mjs` — **stable structural hashing**: `stableJsonString` (sortObject-normalized JSON), `hashStableString` (FNV-1a 32-bit, formátováno `fnv1a-XXXXXXXX`), `createDocumentFingerprint` (full doc), `createSelectionDocumentFingerprint` (jen struktura — blocks/text/drawings/anchors — ignoruje metadata pro selection stability).
- `accessibility/announcements.mjs` — **factory pattern** `createAccessibilityAnnouncer(inst, { invokeBoundary, setTimeout, clearTimeout, now })` vrací `{schedule, cancel}`. 160ms debounce (`announcementDebounceMs`) shoduje s legacy IIFE. Nastavuje `data-accessibility-announcement` attr + textContent na live region.
- `core/value-readers.mjs` — `readOptionalBoolean(source, keys)` (boolean/number/string yes-no-on-off coercion)
- `objects/anchor-region.mjs` — `normalizeAnchorRegionName` (Body=0/Header=1/Footer=2/TableCell=6 + string aliasy), `anchorRegionToValue` inverz, `readObjectLayoutInCell` (precedence: direct → anchor → layout → docx → anchorXml → metadata)
- `layout/text-exclusion.mjs` — `normalizeTextExclusionPageIndex` (non-negative integer default 0), `createTextExclusionScopeKey` (pipe-joined stable string), `readTextExclusionScope` (sortObject-normalized scope record)
- **Pass 10-14 additions**:
- `core/indexes.mjs` — **factory** `createIndexBuilder({ normalizeImageObject })` vrací `buildIndexes(model)` (model walker s injected image normalizer aby se vyhnul cyclic dep), + `createBlockIndexContext`, `findBlockByIndex`
- `core/revision-normalize.mjs` — `normalizeRevisionType` (inbound: numeric 1-6 + string aliases → Insertion/Deletion/FormatChange/Move/Structure/Image/Table), `normalizeRevisionStatus` (Pending/Accepted/Rejected), `normalizeRevisionRange` (start/end + Pascal/camel)
- `core/run-finders.mjs` — `findRunAtOffset` (run + start/end/index), `inlineAtOffset` (run + localOffset/start/end), `resolveTextOffsetToInlineIndex` (affinity-aware boundary resolution)
- `history/transactions.mjs` — **factory** `createTransactionsModule({ idCounters, deps })` vrací `createTransaction(model, options)` s lifecycle (apply/rollback/commit/toJSON). Deps: `applyOperation`, `replaceModelContents`, `withStableSelectionToken`, `createDocumentFingerprint`, `createDiffer`. Throws na missing deps. Supports lightweightSnapshots mode.
- `runtime/watchdog-helpers.mjs` — state constants (WD_READY/RECOVERING/RECOVERED/FAILED), tunables (MAX_ATTEMPTS=3, BACKOFF=100, HISTORY_LIMIT=20), `computeWatchdogBackoff(attempt, base)` (exp), `cloneWatchdogJson`, `parseWatchdogJson`, `unwrap/wrapWatchdogDocumentSnapshot`, `safeCall`, `watchdogNow`, `buildWatchdogEventDetail` (dual Pascal/camel keys), `recordWatchdogEvent` (trim to 20), `createWatchdogContext`, `isWatchdogProcessing`, `lastEventWas`
- `runtime/instance-manager.mjs` — `InstanceManager` class (register/get/has/remove/entries/keys/values/size/clear) + `defaultInstanceManager` singleton
- `objects/layout-helpers.mjs` — `readObjectWrapSide` (precedence: wrapSide→side→wrapText→wrap.{side,wrapSide,wrapText}), `normalizeRelativePositionName` (Page=0/Margin=1/Column=2/Paragraph=3/Character=4/Line=5), `relativePositionToValue` inverz, `verticalAlignmentToValue` (Top=1/Middle=2/Bottom=3), `normalizePositionSpec`, `normalizeLayoutKindName` (Inline=0/Anchored=1/Fixed=2 + aliases)
- `input/command-marks.mjs` — `normalizeCommandColorValue` (#abc → #aabbcc shorthand expansion), `commandMark(id, payload)` → mark record pro bold/italic/underline/strike/fontFamily/fontSize/textColor/backgroundColor/link, `isClearValueCommand` (colour commands s empty value)
- `render/escape.mjs` — `escapeHtml` (`&<>"` escape pro attribute-safe HTML)
- `render/run-text.mjs` — `resolveInlineRunDisplayText` (PageNumber/PageCount field substitution s aliases), `textFromRunsForRender` (paragraph concatenation)
- `clipboard/paste-text.mjs` — `normalizePasteText` (strip HTML tags, `<br>` and `</p><p>` → `\n`, CRLF → LF)
- **Pass 15-19 additions:**
- `core/selection-snapshot.mjs` (~300 řádků) — **kompletní selection snapshot pipeline**: `createLogicalPosition` (sorted shape s defaults), `createLogicalRange` (anchor+focus+direction+isCollapsed), `normalizeSelectionModeValue` (Text/Object), `normalizeTextSelectionPayload` (range from anchor/focus or anchorBlockId/focusBlockId or single position, dual Pascal/camel mirrors), `normalizeObjectSelectionPayload` (objectId required, preserves textSelection), `isObjectSelectionSnapshot`, **`createSelectionSnapshot`** (top-level — text/object mode detection, fallback ranges pro object mode, dual-case accessor properties pro C# JS interop)
- `input/typing-coalescer.mjs` — `shouldCoalesceTyping(prev, next, now, timeoutMs)` (same block + adjacent offset + both InsertText + no newline + not paste + within window), `coalesceTypingOperation(createOperation, prev, next)` (factory injection), `defaultCoalesceWindowMs = 1000`
- `runtime/instance-results.mjs` — `disposedResult(instanceId, methodName)` + `missingResult(...)` + `errorResult(instanceId, methodName, code, message)` (stable `{ok:false, error:{code, message, instanceId}}` envelopes)
- `objects/horizontal-position.mjs` — `normalizeHorizontalPositionName` (Left=0/Center=1/Right=2 + centre/middle/end aliases), `horizontalPositionToValue` inverz
- `objects/wrap-mode-value.mjs` — `wrapModeToValue` (accepts string/object Mode/mode/value), `wrapModeToCssName` (kebab-case CSS class), `wrapModeCreatesTextExclusion` (Square/Tight/Through/TopBottom)
- `objects/geometry.mjs` — **rectangle + contour helpers** (~10 functions): `rectFromGeometry` (left/Top aliases, clamp width/height ≥ 0), `rectRightGeometry`/`rectBottomGeometry`, `rectIntersectsGeometry`, `rectOverlapsHorizontallyGeometry`, `intersectGeometryRect` (returns rect or null), `geometryBoundsOfPoints`, `normalizeWrapContourPointsForGeometry` (clamp to unit square, fallback to 4-corner rect if < 3 points), `readObjectDistance` (precedence with wrapMargin fallback), `createObjectFootprintRect` (image rect + caption tail), `createObjectWrapRect` (footprint + distance{Left,Right,Top,Bottom}), `projectWrapContourPointsForGeometry` (unit-square → wrap rect, clamped to body frame)
- `objects/image-object.mjs` (~150 řádků) — **`normalizeImageObject(block, options)`**: kompletní normalizace floating-image záznamu s precedencí layout/anchor/wrap/position/transform/stacking; vrací 30+ canonical fields (blockId/objectId/anchorBlockId/anchorOffset/layoutKind/isInline/isAnchored/wrapMode/wrapSide/distance{Left,Right,Top,Bottom}/horizontal+verticalPosition/anchorRegion/etc.). Plus `imageObjectToLayout` (inverz — produkuje C# wire Layout payload).
- **Pass 20-23 additions:**
- `objects/sync-image-layout.mjs` — **`syncImageLayoutCase(layout)`** (mutuje clone — Pascal/camel mirror polí pro JS interop, Wrap.Mode→ordinal, Position/Anchor/Transform/Stacking defaults, derive Kind z Wrap.Mode+Anchor.FixedOnPage), **`applyImageWrapModeToLayout(layout, value, options)`** (Inline→MoveWithText=true/no overlap, BehindText→ZIndex=-1/overlap, InFrontOfText→ZIndex=1/overlap, Square/Tight/Through/TopBottom→ZIndex=0/no overlap)
- `objects/drawing-runs.mjs` — **factory** `createDrawingRunsModule({buildIndexes})`: `ensureDrawingIndexes`, `rebuildDrawingIndexes`, `createDrawingObjectSnapshot` (28 canonical fields s `normalizeImageObject` layout), `findDrawingRunByObjectId`, `findDrawingRunByAsset` (priority object > asset, normalized snapshot), `removeDrawingRunByObjectId` (splice + fallback to single empty text run + rebuild indexes)
- `input/before-input.mjs` — `BeforeInputCommands` (canonical map 10 inputType→command), `normalizeBeforeInput` (calls preventDefault, returns `{supported, command, data, inputType, log}`, unsupported types return log entry), `createBeforeInputNormalizer` factory shape
- `history/operation-classifiers.mjs` — `operationTouchesRevisions` (revisionId/RevisionId/revision/Revision present, AcceptRevision/RejectRevision types), `operationMayChangeRevisions` (above + RestoreSnapshot)
- **Pass 24-26 additions:**
- `history/apply-operation-dispatcher.mjs` — **factory** `createApplyOperationDispatcher({handlers, validateOperation, attachOperationMethods, createDiffer, buildIndexes, normalizeRevisionGroups, operationAffectedBlockIds})`: routes by op.type to handler from `handlers` map. SetSelection no-op handled inline. Mark operations (ApplyMark/RemoveMark) share single `applyMarkOperation` handler with boolean extra arg. Validation short-circuit before handler. Post-processing: normalizeRevisionGroups → buildIndexes (if not rebuilt) → differ.snapshot. Unknown type → `unsupported-operation` error; type with no handler → `missing-handler` error. `ApplyOperationHandlerNames` exposes deduped list of 14 handler names.
- `core/import-orchestrator.mjs` — **factory** `createImportOrchestrator({normalizeRevision, buildIndexes})`: `importFromCSharpJson(document)`. Unwraps `Document/document` envelope, calls `importRegion` 1× body + N× header/footer (auto-classified by `Region` string or `Type` numeric=1 footer), `normalizeRevision` map for Revisions, `sortObject` map for Comments/Assets, finally `buildIndexes(model)`. Test parita: empty doc → minimal model, wrapped Document envelope, PascalCase Region detection.
- `input/autocomplete-trigger.mjs` — `detectAutocompleteTriggerText(text, offset)`: regex `(?:^|\s)(\{\{|@|\/)([A-Za-z0-9_-]*)$` detects token (`{{` → tokenQuery), mention (`@` → tagQuery), slash (`/` → slashQuery). Returns `{triggerId, marker, markerType, query, startOffset, endOffset}` or null. Start of string + whitespace boundary; mid-word @ does not trigger.
- `input/command-name.mjs` — `compactCommandName(value)`: lowercase + strip `[\s_-]+` separators. 'Insert Image-URL' → 'insertimageurl'. Used by command dispatcher routing.
- `render/floating-position.mjs` — `computeFloatingPosition(anchor, floating, options)`: places floating element near anchor rect. Bottom-by-default, flips to top when bottom overflows viewport. Clamps with `gutter` margin (default 8). Supports `anchorIsContainerRelative` + scroll container coords + `constrainToScrollContainer` flag.
- `core/first-block.mjs` — `firstTextBlock(model)`: first paragraph in body (or first block if no paragraph). `firstModelSelection(model)`: synthesized collapsed-caret selection at start of first text block.
- **Pass 27-28 additions:**
- `history/differ.mjs` — `createDiffer()` per-operation change accumulator. Collects `insertedRanges`, `removedRanges`, `attributeChanges`, `objectChanges`, `markerChanges`, plus `invalidatedLayoutScopes`/`invalidatedOverlayScopes` (unique-merged). API: `record(entry)`, `getChangedRanges`, `getInvalidatedLayoutScopes` (slice copies), `clear`, `snapshot()` (sortObject-stable view).
- `history/operation-affected.mjs` — `operationAffectedBlockIds(operation)` (collects target/range/selection blockIds + newBlockId + 'revisions' sentinel + affectedScopeIds/affectedParagraphIds/affectedSelectable, dedup+sort), `transactionAffectedBlockIds(transaction, operations)` (union across ops + transaction.invalidatedScopes).
- `history/handlers-simple.mjs` — **factory** `createSimpleHandlers({findBlock, replaceModelContents, nextSelectionForOperation})` → `{applySetParagraphAttribute, applyRestoreSnapshot}`. applySetParagraphAttribute: mutates block.content[name], records previousValue for undo, returns next selection. applyRestoreSnapshot: validates snapshot present, replaceModelContents, scopes default to `['document']`, selection from snapshot or firstModelSelection.
- **Pass 29-31 additions:**
- `history/validate-operation.mjs` — **factory** `createOperationValidator({findBlock, findDrawingRunByObjectId, attachOperationMethods})` → `validateOperation(model, operation)`. Returns `{ok, errors, operation}`. Validates: missing-id/type/timestamp/source, unknown-type, missing-target-block (8 op types incl. InsertText/SplitParagraph/MergeParagraph/SetParagraphAttribute/InsertImage/UpdateImageLayout/MoveDrawingObject/UpdateImageMetadata), offset-out-of-range, invalid-range (3 op types: DeleteRange/ApplyMark/RemoveMark), target-not-drawing-object + dangling-image-anchor (3 image-layout types).
- `core/replace-model.mjs` — **factory** `createReplaceModelContents({buildIndexes})` → `replaceModelContents(target, source)`. In-place mutation: delete all keys from target, deep-clone source, Object.assign, buildIndexes. Preserves target reference so holders see restored state without re-reading.
- `core/region-info.mjs` — `findRegionInfoForBlock(model, blockId)` (Body/Header/Footer/TableCell + headerFooterId/tableId/cellId/columnIndex; scans body + headers + footers + nested tables). `operationRegionInfo(model, op, blockId, fallback)` (enriches with op.selection/target/range hints + fallback). `nextSelectionForOperation(model, op, blockId, offset, fallback)` (sortObject-stable collapsed-caret selection).
- **Pass 32-33 additions:**
- `core/comment-resolver.mjs` — `commentIdsAtInsertionOffset(block, offset)`: vrací jen comments které span obě strany insertion point (left ∩ right). Brání typing inside comment od accidentally extending mimo.
- `core/typing-style.mjs` — `styleHasValues(style)` (quick non-empty check), `resolveTypingStyleAtInsertion(block, offset, affinity)` (walk paragraph runs, pick adjacent run by affinity, fall back to paragraph/block style).
- `core/insert-text-run.mjs` — **`insertTextRun(block, offset, text, attributes)`** kompletní text-run insert pipeline. In-place mutation block.content.runs. Splits at insertion (before+inserted+after), inherits commentIds via commentIdsAtInsertionOffset (unless explicit), preserves drawing runs, runs mergeAdjacentTextRuns at end (same-styled fragments collapse).
- **Pass 34-35 additions:**
- `core/run-mutators.mjs` — **pure mutator family**: `setParagraphText(block, text)` (single plain run), `cloneRunSlice(run, start, end, suffix)` (functional slice with id suffix), `deleteTextRange(block, start, end)` (splice across multiple runs with merge), `splitParagraphRuns(block, offset)` (returns `{before, after}` without mutating block), `splitRunsForRange(block, start, end, mark, remove)` (apply/remove mark across range with run boundary splits).
- `history/handlers-text.mjs` — **factory** `createTextHandlers({findBlock, revisionById?})` → `{applyInsertText, applyDeleteRangeUntracked, applyMarkOperation, applyMergeParagraph}`. applyInsertText uses insertTextRun + resolveTypingStyleAtInsertion + nextSelectionForOperation. applyDeleteRangeUntracked uses deleteTextRange. applyMarkOperation uses splitRunsForRange. applyMergeParagraph uses setParagraphText + container.blocks.splice.
- **Pass 36-38 additions:**
- `history/revision-helpers.mjs` — pure helpers: `revisionById`, `readRevisionStatus/TypeName/MarkerType` (Pending/Accepted/Rejected; Insertion→revisionInsertion, Deletion→revisionDeletion, FormatChange→revisionFormat), `setRevisionPayloadText` (updates payload.text + payloadJson), `createTrackedRevisionPayload(type, range, text, userId, source, extra)` (full revision record), `transformRunsInRange(block, start, end, transform)` (slice + transform middle). **2 factory helpers**: `createRevisionListHelpers({normalizeRevision, buildIndexes})` → `{ensureRevisionList, addRevision, getRevisionById}`, `createSetRevisionForRange({findBlock})` → `setRevisionForRange(model, revisionId, range)`.
- `history/handlers-split.mjs` — **factory** `createSplitHandler({findBlockContainer, splitParagraphRuns, importBlock, nextSelectionForOperation, operationRegionInfo})` → `{applySplitParagraph}` (untracked variant). Builds new paragraph block with after-runs, inserts at container.index+1, returns `{ok, invalidatedLayoutScopes: [block.id, newBlock.id], nextSelection, insertedBlockId}`.
- `layout/text-measurement.mjs` — `normalizeMeasureStyle`, `computeMeasureCacheKey`, `measureTextRunPure` (synthetic char-based width: 0.55× fontSize for non-whitespace, 0.32× for whitespace, +8% bold, +4% italic, letterSpacing offset, zoom multiplier). **`createTextMeasurementService()`** factory: cached service with `measureTextRun`, `clearCache`, `getStats` (MeasureCount/MeasureCacheHits/MeasureCacheSize/MeasureInvalidations); each call produces isolated cache.
- `history/operation-types.mjs` (OperationTypes, TransactionTypes, isTypingLikeTransactionType)
- `history/id-counters.mjs` (createIdCounters — counter factory)
- `history/operations.mjs` — **factory pattern**: `createOperationsModule({ idCounters })` vrací `createOperation`, `attachOperationMethods`, `getReversedOperation` (kompletní switch pro všech 10 reverzovatelných typů), `toOperationJson`, `createReversedOperationJson`, `createRedoHistoryOperations`, `createUndoHistoryOperations`. Plus standalone pure helpery: `isSelectionOnlyOperation`, `operationsAffectDocument`, `transactionAffectsDocument`, `supportsOperationHistory`, `supportsLightweightTransactionSnapshots`.
- `layout/scope-kinds.mjs` (LayoutScopeKinds)
- `layout/layout-scope.mjs` — **`createLayoutScope` + `inferLayoutScopeFromOperation`** (per-operation scope: ActiveParagraph / WholeBlock / PageRegion / WholeDocument; pokrývá všech 14 OperationTypes + fallback)
- `layout/page-metrics.mjs` — **`normalizePageBox`, `normalizePageLayoutSettings`, `createPageLayout`, `createPageBreakLayout`, shift helpers** (`shiftRectY`/`shiftLayoutLine`/`shiftLayoutSegment`/`shiftCaretStop`), **field resolution** (`resolveFieldRunText` pro PageNumber/TotalPages/aliases, `cloneBlockWithResolvedFields`)
- `objects/wrap-modes.mjs` (WrapModeNames, WrapSideNames + normalizers s **full legacy aliasy** 'wrap'/'breaktext'/'behind'/'front'/atd., + `wrapSideToValue` inverz)
- `objects/drawing-kind.mjs` (normalizeDrawingKindName, exportDrawingKind)
- `runtime/entry.mjs` (bundler entry, namespace re-export — `version: 'phase-d-skeleton-20'`)

Legacy monolit `document-editor-wysiwyg.js` ZŮSTÁVÁ produkčním zdrojem — dist bundle (`document-editor.dist.js`, nyní 685.6 KB) je zatím verifikační artefakt. **83 testů** (`PhaseDModuleExtractionTests`) zelených (incl. bundle smoke test), žádná regrese (782 pass / 5 pre-existing failures, totožných s baseline před touto prací).

### D1. Adopce ES modules + Vite/esbuild

- [x] Přidat `src/Tempo.Blazor/wwwroot/js/document-editor/` adresář pro modules.
- [x] Nastavit jednoduchý esbuild config (`tests/Tempo.Blazor.Tests/jsbuild/esbuild.mjs`) — bundle pro browser, separate chunks per modul. Produkuje `document-editor.dist.js` IIFE s globálem `tmDocumentEditorModules`.
- [~] Per-modul `import` mapping, output 1 main + N lazy chunks — momentálně jen 1 main bundle (entry.mjs re-exportuje vše); lazy `import()` chunks budou až bude code mass ospravedlňující split.

### D2. Rozdělení do modulů

- [x] `core/` — schema registry, model importers/exporters, validators (~3 000 řádků) — **kompletně extrahováno kromě `importFromCSharpJson` (top-level orchestrator) a `buildIndexes`** (závislý na `normalizeImageObject` z image pipeline). Aktuální moduly: `helpers.mjs`, `schema.mjs`, `text-helpers.mjs`, `model-finders.mjs`, `normalize-target.mjs`, `marks.mjs`, `export-types.mjs`, `inline-runs.mjs`, `block-import.mjs`, `block-export.mjs`, `comment-revision-export.mjs`, `document-export.mjs`, `validate-model.mjs`, `fingerprint.mjs`. 14 core modulů celkem.
- [~] `history/` — operation types, command stack, transactions (~2 500 řádků) — migrované: enums (`operation-types.mjs`), counter factory (`id-counters.mjs`), **operations module** (`operations.mjs`: factory `createOperationsModule({ idCounters })` + getReversedOperation + JSON helpers + pure classifiers). Command stack + transactions stále v legacy IIFE.
- [~] `layout/` — paragraphEngine, page metrics, segment generator (~5 000 řádků) — migrované: enum (`scope-kinds.mjs`), scope inference (`layout-scope.mjs`: `createLayoutScope` + `inferLayoutScopeFromOperation`), **page metrics** (`page-metrics.mjs`: normalizePageBox/normalizePageLayoutSettings/createPageLayout/createPageBreakLayout + shift helpers + field resolution). Paragraph engine + segment generator stále v legacy IIFE.
- [ ] `render/` — atomic renderer, segment patcher (~4 000 řádků — po refactoru z fáze B menší)
- [ ] `input/` — beforeInput, keydown, composition (~2 500 řádků)
- [ ] `clipboard/` — paste pipeline, copy serializer (~1 500 řádků)
- [~] `objects/` — image, table, drawing (~3 000 řádků — lazy-load při insertu image) — migrované: wrap-mode enums + normalizery + `wrapSideToValue` (`wrap-modes.mjs`, full legacy alias coverage), drawing-kind normalizery (`drawing-kind.mjs`). Image/table/drawing pipelines v legacy IIFE.
- [ ] `collaboration/` — remote ops, CRDT (~2 000 řádků — lazy-load při connect)
- [x] `accessibility/` — announcements, screen-reader help (~500 řádků) — `announcements.mjs` migrováno jako **factory pattern** (instance state + setTimeout/clearTimeout/invokeBoundary jako parametry). Klíčový vzor pro budoucí extrakci instance-state-dependent modulů.
- [~] `runtime/` — entry point, watchdog, instance manager (~2 000 řádků) — `runtime/entry.mjs` exists (bundler entry, re-export aggregate). Watchdog + instance manager stále v legacy IIFE.
- [~] Každý modul má vlastní vitest suite — místo vitestu použity C# `PhaseDModuleExtractionTests` (xUnit + Node ESM child process). **39 testů**: existence souborů, ESM import, behavior parity s legacy IIFE pro všech 23 modulů včetně 4 **byte-parity testů proti legacy `__testHooks`**: normalizeMarks (8 inputů), mergeAdjacentTextRuns (7 inputů), normalizeDrawingRun (5 inputů), **exportToCSharpJson full document round-trip (4 inputů — paragraph/heading mix, nested tables, image+header)**.

### D3. Migrate test runners

- [N/A] Existující `tests/Tempo.Blazor.Tests/wwwroot/js/*` testy přepsat na ESM imports — **N/A v této code base**. Repo neobsahuje žádné vitest/wwwroot/js JS test soubory. JS testy běží přes Node ESM child process v `PhaseDModuleExtractionTests.cs` (a obecně v `*JavaScriptTests.cs` souborech, které spouštějí legacy IIFE v Node vm sandboxu). Modulové testy už natively používají Node ESM `import()`.
- [N/A] Zachovat `__testHooks` API pro bUnit-zpřístupněné funkce — **stále zachováno** v legacy `document-editor-wysiwyg.js`. Modulové byte-parity testy (`normalizeMarks`, `mergeAdjacentTextRuns`, `normalizeDrawingRun`, `exportToCSharpJson`) přímo čtou hooks z vm sandboxu a srovnávají s modulovými výstupy.

**Checkpoint D**: 🟢 **D1 100% hotové. D2 — 86 modulů (`find ... -name '*.mjs' | wc -l = 86`), 102 testů, žádná regrese, 4 byte-parity testy proti legacy IIFE + 1 bundle smoke test + 18 factory patterns + 1 mutating helper + 1 mutator family.** D3 N/A.

**Co je migrováno** (82 modulů):
- **core (30)**: helpers, schema, text-helpers, model-finders, normalize-target, marks, export-types, inline-runs, block-import, block-export, comment-revision-export, document-export, validate-model, fingerprint, value-readers, indexes (factory), revision-normalize, run-finders, selection-snapshot, import-orchestrator (factory), first-block, replace-model (factory), region-info, comment-resolver, typing-style, insert-text-run, run-mutators, selection-token (4 pure), limit-finder, **schema-validation**
- **history (14)**: operation-types, id-counters, operations (factory), transactions (factory), operation-classifiers, apply-operation-dispatcher (factory), operation-affected, handlers-simple (factory), differ, validate-operation (factory), handlers-text (factory), handlers-split (factory), handlers-tracked (factory), revision-helpers (2 factories)
- **layout (11)**: scope-kinds, layout-scope, page-metrics, text-exclusion, text-measurement (factory), line-breaker (factory), line-breaker-helpers (8 pure), line-draft (2 pure), paragraph-tokenizer (6 pure + 1 factory), paragraph-alignment (1 pure), line-breaker-fallback (factory)
- **objects (11)**: wrap-modes, drawing-kind, anchor-region, layout-helpers, horizontal-position, wrap-mode-value, geometry, image-object, sync-image-layout, drawing-runs (factory), image-resize (5 pure + 2 const)
- **input (6)**: command-marks, typing-coalescer, before-input, autocomplete-trigger, command-name, **layout-text-edit-model**
- **render (5)**: escape, run-text, floating-position, **non-printing**, **heading-finder**
- **clipboard (1)**: paste-text
- **accessibility (2)**: announcements (factory), **labels**
- **runtime (5)**: entry, instance-manager, watchdog-helpers, instance-results, **performance-metrics (factory)**

**Co zbývá v legacy IIFE** (limity pure-extraction patternu): celé `paragraphEngine` (line breaking, font metrics, 5000+ řádků), `atomicRenderer` + `render(inst)` (DOM-dependent, 4000+ řádků), `applyOperation` dispatcher + `replaceModelContents` + `createDiffer` + `withStableSelectionToken` (deps že `transactions.mjs` přijímá via injection), full input handlers (`applyBeforeInput`, `applyKeyboardInsertText`, composition), clipboard paste pipeline (HTML parsing, DOCX import), object UI overlay pipelines (image/table/drawing handles + drag), collaboration (remote ops + CRDT), watchdog lifecycle wrapper (`_scheduleRecovery`/`_attemptRecovery` reach na external `runtime` global), `importFromCSharpJson` orchestrator (deps na `normalizeRevision` chain s ID generation), `syncImageLayoutCase` (mutates Pascal+camel mirror fields), `findDrawingRunByObjectId/findDrawingRunByAsset` (depend na model.indexes), všechny instance lifecycle handlers + Blazor JS interop bridge methods.

**Factory pattern coverage**: **10 factory modulů** demonstrují injection-based extraction: `createIndexBuilder`, `createTransactionsModule`, `createOperationsModule`, `createAccessibilityAnnouncer`, `createDrawingRunsModule`, `createApplyOperationDispatcher`, `createImportOrchestrator`, `createSimpleHandlers`, **`createOperationValidator({findBlock, findDrawingRunByObjectId, attachOperationMethods})`**, **`createReplaceModelContents({buildIndexes})`**. Plus `createDiffer` pure factory (no deps). Pattern připraven pro: zbývající applyXxx handlers (applyInsertText/applyDeleteRange/applyInsertImage atd. potřebují inline run mutator family), paragraph engine `createParagraphLayoutEngine({fontMetrics, lineBreaker, …})`, atomicRenderer `createAtomicRenderer({domAdapter, …})`, watchdog lifecycle wrapper, clipboard paste pipeline.

> **Pozn.**: D je doporučená, ale **nemusí být blokující pro feature work**. Pokud čas tlačí, lze D odložit za F+G. Aktuálně D1+kostra D2 hotová, zbytek inkrementálně.

### Zbývající práce pro plné dokončení D

- [x] `core/`: ~~`DocumentSchemaRegistry`~~; ~~model finders~~; ~~normalize target/range~~; ~~marks family~~; ~~12 export enum mapperů~~; ~~inline runs (import/export/merge/plain)~~; ~~block import (paragraph/image/table/block/region)~~; ~~block export~~; ~~comment+revision export~~; ~~`exportToCSharpJson`~~; ~~`validateModel` (self-contained)~~; ~~fingerprint (stableJson + FNV-1a + selection structural)~~
- [ ] `core/`: extrahovat `importFromCSharpJson` (top-level orchestrator — depends na `normalizeRevision`/`normalizeRevisionRange`/`buildIndexes`), `buildIndexes` (depends na `normalizeImageObject` z image pipeline), `_findBlock` index-based variant (závisí na buildIndexes)
- [x] `history/`: ~~counter factory~~ migrováno (`id-counters.mjs`); ~~`createOperation`, `attachOperationMethods`, `getReversedOperation` family~~ migrováno (`operations.mjs` factory); ~~pure classifiers (`isSelectionOnlyOperation`, `operationsAffectDocument`, `transactionAffectsDocument`, `supportsOperationHistory`, `supportsLightweightTransactionSnapshots`)~~ migrováno
- [ ] `history/`: extrahovat command stack + transactions (`transaction` builder, `historyStack`, undo/redo orchestrace) — nyní možné protože counter a operations jsou externí
- [x] `layout/`: ~~scope inference (`createLayoutScope`/`inferLayoutScopeFromOperation`)~~ migrováno; ~~page metrics (`normalizePageBox`/`normalizePageLayoutSettings`/`createPageLayout`/`createPageBreakLayout` + shift helpers + field resolution)~~ migrováno
- [ ] `layout/`: extrahovat `paragraphEngine` (paragraph layout, line breaking), segment generator — největší zbývající kus layoutu
- [ ] `render/`: extrahovat `createAtomicRenderer` + `render(inst)` produkční renderer (vázáno na fázi B1 refactor)
- [ ] `input/`: `applyBeforeInput`, `applyKeyboardInsertText`, composition handlers (~2 500 řádků, těsně provázáno s history)
- [ ] `clipboard/`: paste pipeline + copy serializer
- [x] `objects/`: ~~wrap-mode (incl. legacy aliases + `wrapSideToValue`) + drawing-kind normalizery~~ migrováno
- [ ] `objects/`: image/table/drawing pipelines + jejich UI overlays (lazy-loaded)
- [ ] `collaboration/`: remote ops manager (lazy-loaded)
- [x] `accessibility/`: ~~`scheduleAccessibilityAnnouncement`~~ migrováno jako factory (`announcements.mjs`)
- [ ] `accessibility/`: `installAccessibilityAndKeyboardHandlers` (keyboard navigation handlers — vyžaduje hluboký engine state)
- [ ] `runtime/`: watchdog, instance manager, lifecycle
- [ ] D3 — migrace test runnerů na ESM imports + zachování `__testHooks` global re-export pattern

---

## 7. Fáze E — Word-style "Heading" styles

**Cíl**: Word/OnlyOffice mají koncept "styles" — Heading 1, Heading 2, Normal, Quote, ... — pojmenovaná entita s formátováním. Uživatel volí "Heading 1" v ribbonu a aplikuje styl na block. Změna definice stylu se propaguje do všech blocků.

### E1. Datový model — `DocumentStyle` + `DocumentStyleRegistry`

- [ ] Vytvořit `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentStyle.cs`:
  ```csharp
  public sealed record DocumentStyle(
      string Id,
      string Name,
      DocumentStyleType Type,        // Paragraph / Character / Linked
      string? BasedOnId,             // inheritance
      string? NextStyleId,           // next paragraph default
      ParagraphFormat Paragraph,     // alignment, spacing, indent, ...
      InlineFormat Inline,           // font, size, color, weight, ...
      bool IsBuiltIn,
      bool IsHeading,                // true for Heading 1–6
      int? OutlineLevel              // 0–8, used by TOC
  );
  ```
- [ ] `DocumentEditorDocument.Styles: IReadOnlyList<DocumentStyle>` (default: built-in Normal, Heading 1–6, Title, Subtitle, Quote, Caption, List Paragraph)
- [ ] `DocumentBlock.StyleId: string?` — odkaz na styl; pokud null, použije se default per block type
- [ ] Unit testy v `tests/Tempo.Blazor.Tests/Models/DocumentEditor/DocumentStyleTests.cs`:
  - "E1.1 default style set obsahuje Heading 1–6"
  - "E1.2 BasedOn dědí ParagraphFormat + InlineFormat"
  - "E1.3 cyklická závislost BasedOn detekována"

### E2. JS engine — render style

- [ ] V `renderParagraphScope` aplikovat styl: `data-style-id` atribut + CSS proměnné z `inlineFormat`/`paragraphFormat`.
- [ ] Layout engine bere `style.paragraph.lineSpacing`, `marginTop`, ... do zápočtu.
- [ ] Unit testy v `document-editor-wysiwyg.unit.test.js`:
  - "E2.1 block with styleId 'Heading1' rendrian s data-style-id"
  - "E2.2 changing style updates only style-affected blocks"

### E3. Ribbon — styles picker

- [ ] Rozšířit `TmDocumentEditorToolbar.razor` o "Styles" gallery v Home tab (jako Word):
  - Mini-preview tlačítka s názvy stylů (Normal, Heading 1, Heading 2, ...)
  - Klik = aplikuj styl na aktuální paragraph (nebo selection range)
  - "Apply Styles" panel: full list + možnost "Modify..."
- [ ] Příkaz `applyParagraphStyle` v `DocumentEditorCommandRegistry` (parametr: `{ styleId: string }`)
- [ ] Příkaz `defineStyle` (pro editaci/přidání stylu)
- [ ] Klávesové zkratky: Ctrl+Alt+1 = Heading 1, Ctrl+Alt+2 = Heading 2, ... Ctrl+Shift+N = Normal
- [ ] bUnit testy v `tests/Tempo.Blazor.Tests/Components/DocumentEditor/StylesGalleryTests.cs`:
  - "E3.1 gallery renders all built-in styles"
  - "E3.2 click on Heading 1 applies style"
  - "E3.3 Ctrl+Alt+1 shortcut applies Heading 1"

### E4. DOCX round-trip

- [ ] `DocumentSerializer` v sériu/deserializaci v `DocumentFormats` rozšířit:
  - Import: `w:pStyle` → `block.StyleId`
  - Export: `block.StyleId` → `<w:pPr><w:pStyle w:val="...">`
  - Styles part `word/styles.xml` ↔ `DocumentStyleRegistry`
- [ ] DOCX testy v `tests/DocumentFormats.Tests/`:
  - "E4.1 import Word .docx with Heading 1 → StyleId='Heading1'"
  - "E4.2 export → round-trip preserves style references"
  - "E4.3 unknown style is mapped to BasedOn=Normal with warning"

### E5. Heading style → `HeadingBlock` content

- [ ] Pokud `block.StyleId` je heading style (`IsHeading=true`), block automaticky převést na `HeadingBlockContent { Level = OutlineLevel + 1 }`.
- [ ] Existující `HeadingBlockContent` s explicitním Level zůstane funkční (backward compat).
- [ ] Outline service (existing) využije `StyleId` jako alternativní indikátor `IsHeading` při budování outline.

**Checkpoint E**: V toolbaru gallery se styly, Ctrl+Alt+1..6 funguje, Word .docx s Heading 1–6 se importuje a exportuje korektně. Existující 972 testů zelených.

---

## 8. Fáze F — Automaticky generovaný TOC

**Cíl**: vložit do dokumentu blok "Obsah" který zobrazí všechny nadpisy v dokumentu jako klikatelné odkazy. Update on demand (Word-like F9) + auto-update při save.

### F1. `TableOfContentsBlock` model

- [ ] Vytvořit nový block content type v `Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentBlock.cs`:
  ```csharp
  public sealed record TableOfContentsBlockContent(
      TableOfContentsOptions Options,
      IReadOnlyList<TableOfContentsEntry> Entries  // computed cache
  ) : IDocumentBlockContent;

  public sealed record TableOfContentsOptions(
      int MinLevel = 1,
      int MaxLevel = 3,
      bool ShowPageNumbers = true,
      bool HyperlinkEntries = true,
      string? TitleText = "Obsah",
      string? StyleIdForEntries = "TOC1"
  );

  public sealed record TableOfContentsEntry(
      string BlockId,           // ref na heading
      int Level,
      string Text,
      int? PageNumber           // null pokud nepočítáme
  );
  ```
- [ ] `DocumentBlockType.TableOfContents` enum value
- [ ] Unit testy:
  - "F1.1 TableOfContentsBlock serializes round-trip"
  - "F1.2 entry references heading block by id"

### F2. `DocumentTableOfContentsService` (Abstractions)

- [ ] Vytvořit `src/Tempo.Blazor.Abstractions/DocumentEditor/Services/DocumentTableOfContentsService.cs`:
  ```csharp
  public sealed class DocumentTableOfContentsService
  {
      public IReadOnlyList<TableOfContentsEntry> BuildEntries(
          DocumentEditorDocument document,
          TableOfContentsOptions options);

      public DocumentEditorDocument RefreshAllToc(DocumentEditorDocument document);
  }
  ```
- [ ] Pro každý heading block ve scope (vč. cells tabulek? — TBD) generuje entry.
- [ ] Filtruje podle `MinLevel`/`MaxLevel`.
- [ ] Page number resolved z layout snapshot (vyžaduje JS round-trip — vidíme dále).
- [ ] Unit testy:
  - "F2.1 prázdný dokument → prázdné entries"
  - "F2.2 dokument s 5 nadpisy úrovní 1–3 → 5 entries"
  - "F2.3 MaxLevel=2 vynechá Heading 3"
  - "F2.4 změna textu nadpisu se propaguje při RefreshAllToc"

### F3. Renderer — `tm-document-toc` block

- [ ] V `TmDocumentBlockRenderer.razor`/`.razor.cs` přidat case `TableOfContentsBlockContent toc`:
  - `<nav class="tm-document-toc" data-block-id="@Block.Id" data-testid="document-toc">`
  - Title (`toc.Options.TitleText`)
  - Pro každý entry `<a href="#@entry.BlockId" data-toc-entry="@entry.BlockId" class="tm-document-toc__entry tm-document-toc__entry--level-@entry.Level">...`
  - Pokud `ShowPageNumbers=true` přidat `<span class="tm-document-toc__page">@entry.PageNumber</span>` (tabulátor + leader dots přes CSS `border-bottom: dotted`)
- [ ] V JS engine přidat `renderTocBlock(blockLayout)` rendering inline — interní contenteditable=false, click vyvolá `scrollToBlock(entry.BlockId)`.
- [ ] CSS: leader dots přes `flex` layout s `border-bottom: 1px dotted` u `<span class="tm-document-toc__leader">`.
- [ ] Unit testy:
  - "F3.1 TOC block renderian s correct entries"
  - "F3.2 click na entry scrolluje na heading"
  - "F3.3 entry s neexistujícím heading id → invalid (skryté, log warning)"

### F4. Toolbar — Insert TOC

- [ ] Add v `References` tab (nový tab v `TmDocumentEditorToolbar.razor`) nebo v Insert tab:
  - Button `insertTableOfContents` (`data-testid="document-toolbar-insert-toc"`)
  - Klik = otevře dialog `TmDocumentTocOptionsDialog.razor` s nastavením (MinLevel, MaxLevel, ShowPageNumbers, ...)
  - Confirm = vloží `TableOfContentsBlockContent` na pozici kurzoru.
- [ ] Update TOC: button `updateTableOfContents` (vedle TOC blocku se objeví "Aktualizovat" pseudoelement)
- [ ] Příkazy v `DocumentEditorCommandRegistry`:
  - `insertTableOfContents` (parametr: options)
  - `updateTableOfContents` (parametr: tocBlockId nebo "all")
  - `removeTableOfContents` (parametr: tocBlockId)
- [ ] Klávesová zkratka: F9 = update aktuální (cursor v TOC) nebo všechny TOC bloky
- [ ] bUnit testy v `tests/Tempo.Blazor.Tests/Components/DocumentEditor/TableOfContentsTests.cs`:
  - "F4.1 insert TOC dialog otevřen, confirm vloží blok"
  - "F4.2 F9 update TOC vyvolá Refresh"
  - "F4.3 odstranění headingu označí TOC jako dirty"

### F5. Page numbers — vyžaduje JS layout snapshot

- [ ] Rozšířit `WysiwygPageMetrics` (existující) o `HeadingPageMap: Dictionary<blockId, pageNumber>`
- [ ] Při Refresh TOC: zavolat `JSRuntime.InvokeAsync<HeadingPageMap>("tmDocumentEditorRuntime.getHeadingPageMap", instanceId)` → použít pro entry.PageNumber.
- [ ] JS funkce projde `paragraphEngine.lastLayout.blocks`, pro každý block s `type === 'paragraph'` && (`block.styleId is heading` nebo `block.content.headingLevel`) vrátí pageIndex+1.
- [ ] Unit testy:
  - "F5.1 dokument s 3 stránkami → heading na p1 pageNumber=1, heading na p3 pageNumber=3"
  - "F5.2 layout změna po insertu obrázku invalidates page numbers"

### F6. Auto-update strategie

- [ ] **Lazy**: TOC se aktualizuje při save (před serializací).
- [ ] **On demand**: F9 / button Update.
- [ ] **Live (volitelné)**: debounced refresh při změně nadpisu (300 ms). V `TmDocumentEditor.razor.cs` listener na `HeadingChanged` event.
- [ ] Add `DocumentEditorOptions.AutoUpdateTableOfContents: AutoUpdateMode` (Off / OnSave / OnHeadingChange) — default `OnSave`.
- [ ] Unit testy:
  - "F6.1 save vyvolá RefreshAllToc"
  - "F6.2 OnHeadingChange + insert heading → debounced refresh do 500 ms"
  - "F6.3 Off mode → změna headingu neaktualizuje TOC"

### F7. DOCX export — TOC field

- [ ] Při exportu do .docx vygenerovat Word `TOC` complex field:
  - `<w:fldChar w:fldCharType="begin"/>` + instrText `TOC \o "1-3" \h \z \u` + `<w:fldChar w:fldCharType="separate"/>` + entries jako hyperlinky + `<w:fldChar w:fldCharType="end"/>`
- [ ] Import .docx s TOC: detekovat začátek/konec TOC field, rekonstruovat `TableOfContentsBlockContent`.
- [ ] DOCX testy:
  - "F7.1 export round-trip preserves TOC"
  - "F7.2 import Word doc s TOC field → TableOfContentsBlockContent"

**Checkpoint F**: Insert TOC funguje, F9 update funguje, page numbers se zobrazují, .docx round-trip prochází.

---

## 9. Fáze G — Kontrola pravopisu

**Cíl**: real-time underline špatně napsaných slov, kontextové menu se sugescemi, ignore-list per dokument, podpora alespoň cs_CZ a en_US.

### G1. Hunspell engine integrace

- [ ] Přidat npm dependency `hunspell-asm` (WASM, MIT, ~500 KB gzipped — povolené pro Blazor JS).
- [ ] Vytvořit `src/Tempo.Blazor/wwwroot/js/document-editor/spell/spell-engine.js`:
  - Načte WASM async (lazy, jen pokud spell-check enabled)
  - API: `loadDictionary(lang, affBuffer, dicBuffer)`, `spell(word, lang) → boolean`, `suggest(word, lang) → string[]`
- [ ] Dictionaries v `wwwroot/dictionaries/{cs_CZ,en_US}/{.aff,.dic}` (převzít z OnlyOffice nebo z [LibreOffice dictionaries](https://github.com/LibreOffice/dictionaries) — LGPL kompatibilní).
- [ ] Unit testy v `tests/Tempo.Blazor.Tests/wwwroot/js/spell-engine.unit.test.js` (vitest, mock WASM):
  - "G1.1 loadDictionary cs_CZ success"
  - "G1.2 spell('domeček', 'cs_CZ') → true"
  - "G1.3 spell('domčeek', 'cs_CZ') → false"
  - "G1.4 suggest('domčeek', 'cs_CZ') obsahuje 'domeček'"

### G2. `DocumentSpellChecker` per-paragraph engine

- [ ] Inspirováno OnlyOffice `CDocumentSpellChecker`:
- [ ] Vytvořit `src/Tempo.Blazor/wwwroot/js/document-editor/spell/document-spell-checker.js`:
  - `pendingParagraphs: Set<blockId>` — paragrafy ke kontrole
  - `errorParagraphs: Map<blockId, SpellError[]>` — paragrafy s chybami
  - `ignoredWords: Set<string>` — per dokument
  - `currentParagraphId: blockId | null` — paragraph s kurzorem (neukazujeme errory uvnitř psaného slova)
  - `MAX_PARAGRAPHS_PER_TICK = 50`, `MAX_ERRORS = 2000`
  - `tick()` zpracuje až 50 pending paragrafů, použije setTimeout(0) loop
  - `addParagraphToCheck(blockId)`, `paragraphChanged(blockId)`, `documentLoaded()`
- [ ] V `applyInputOperations` po commitu změny → `spellChecker.paragraphChanged(blockId)`
- [ ] Selection change → `spellChecker.setCurrentParagraph(blockId)`
- [ ] Unit testy:
  - "G2.1 doc s 5 paragrafy → po načtení 5 paragrafů v pendingParagraphs"
  - "G2.2 tick zpracuje max 50 paragrafů"
  - "G2.3 paragraph s kurzorem se neoznačuje"
  - "G2.4 ignoredWords → word neoznačen"
  - "G2.5 errors přesahující MAX_ERRORS → errorsExceed=true, další skip"

### G3. Rendering chyb (red wavy underline)

- [ ] V `renderParagraphScope`/`renderSegment` přidat třídu `tm-spell-error` na segmenty obsahující chybné slovo.
- [ ] CSS: `text-decoration: wavy red underline` (s `text-underline-offset: 2px`)
- [ ] Při rerenderu se třída automaticky propaguje (komponent layout → render).
- [ ] Unit testy:
  - "G3.1 segment obsahující chybu má class tm-spell-error"
  - "G3.2 segment v current paragraph nemá class"

### G4. Kontextové menu pro chybné slovo

- [ ] Pravý klik na slovo s `tm-spell-error` → `TmDocumentSpellContextMenu.razor`:
  - Top 5 sugescí (klik = nahradit)
  - "Ignorovat" → `spellChecker.ignoreWord(word)` (jen pro tento dokument)
  - "Přidat do slovníku" (volitelné, vyžaduje persistent dictionary — TBD)
  - "Jazyk slova" (allows language override per word)
- [ ] Floating UI manager — registrovat layer `SpellContextMenu` v `FloatingLayerId`.
- [ ] bUnit testy:
  - "G4.1 right-click na chybu otevře menu"
  - "G4.2 click na sugesci nahradí slovo"
  - "G4.3 Ignore přidá slovo do ignoredWords"

### G5. Toolbar — language picker + enable/disable

- [ ] Status bar (existing `TmDocumentEditorStatusBar`): přidat indicator chyb (`document-spell-error-count`) a aktivní jazyk (`document-spell-language`).
- [ ] Ribbon Review tab: button `toggleSpellCheck`, dropdown `setSpellLanguage` (cs_CZ, en_US, sk_SK, de_DE, ...).
- [ ] Lokalizační klíče:
  - `TmDocumentEditor_SpellCheck`
  - `TmDocumentEditor_SpellLanguage`
  - `TmDocumentEditor_SpellIgnore`
  - `TmDocumentEditor_SpellSuggestions`
  - `TmDocumentEditor_SpellErrorsCount` (formatable s počtem)
- [ ] bUnit testy:
  - "G5.1 status bar zobrazuje počet chyb"
  - "G5.2 toggleSpellCheck off → žádné error markers"
  - "G5.3 setSpellLanguage cs_CZ→en_US → re-check všech paragrafů"

### G6. Autocorrect (volitelné nice-to-have)

- [ ] Inspirováno OnlyOffice `autoCorrectSettings.js`:
  - `"hte" → "the"`, `"  " (dva mezery) → ". "` (na konci věty)
  - Custom replacements (user editable)
- [ ] Per language autocorrect tables.

### G7. Lazy load WASM + dictionaries

- [ ] Initial bundle neobsahuje spell engine ani slovníky.
- [ ] Při prvním enable spell-check (toolbar nebo auto-load setting) lazy load přes `import('./spell/spell-engine.js')`.
- [ ] Loading state: status bar `document-spell-loading`.
- [ ] Unit testy:
  - "G7.1 enable spell-check lazy-loads WASM"
  - "G7.2 second enable nevyvolá další load"

**Checkpoint G**: Český + anglický spell check funguje, red underline zobrazuje, suggest funguje, ignore funguje, max 50 paragrafů/tick udržuje typing fluentní.

---

## 10. Fáze H — Page numbers, cross-references (volitelné)

**Cíl**: Word-like Page Number field, "Stránka X z Y" v header/footer, cross-reference field (např. "viz kapitolu X.Y na straně Z").

### H1. `PageNumberFieldInline` + `PageCountFieldInline`
- [ ] Inline run s field type. Render = aktuální číslo stránky / total pages z layout.

### H2. `CrossReferenceFieldInline`
- [ ] Reference na heading nebo bookmark, parametrizováno (zobrazit text, číslo, page number, ...).

### H3. Update on save / F9.

> Tato fáze ne-blokuje předchozí; mohou se naplánovat zvlášť.

---

## 11. Globální milníky

| Milník | Co znamená | Kdy | Stav |
|---|---|---|---|
| **M1** | Fáze A hotová → známé měřitelné baseline | Sprint 1 | ✅ 2026-05-26 (baseline: typing-500p ~180ms/keystroke) |
| **M2** | Fáze B+C hotová → keystroke pod 16 ms u 1000-paragrafového dokumentu, žádná C# JSON serializace při psaní | Sprint 2–3 | 🟡 B2/B3/B4/B5 + C1/C2/C4 hotové (wire-up + pooled writer + auto-cache-invalidate). B1 top-level produkční `render(inst)` refactor a C3 per-callback audit jsou samostatné scopy (nepokryté v této pass) |
| **M3** | Fáze E hotová → uživatel volí Heading 1–6 z toolbar | Sprint 4 | ⬜ Pending |
| **M4** | Fáze F hotová → Insert TOC funguje, F9 updates | Sprint 5 | ⬜ Pending |
| **M5** | Fáze G hotová → spell check cs_CZ + en_US v produkci | Sprint 6 | ⬜ Pending |
| **M6** | Fáze D hotová → code-split bundle (lze přesunout, pokud blokuje) | Sprint 7 (volitelné) | 🟢 2026-05-27 pass 26 — D1 100%, D2 56 modulů (incl. 7 factories: indexes, operations, transactions, accessibility, drawing-runs, apply-operation-dispatcher, import-orchestrator), 69 testů, bundle 551.1 KB, D3 N/A. ApplyOperation dispatcher + importFromCSharpJson orchestrator nyní v modulech jako factories — handlers stay v IIFE ale routing/validation je v modulu. Zbytek IIFE jsou velké pipelines (paragraphEngine 5000ř/atomicRenderer 4000ř/applyXxx handlers/full input handlers/clipboard paste/collaboration/watchdog lifecycle) které lze postupně extrahovat stejným injection patternem |

### Zbývající práce pro plné dokončení M2

**Fáze B:**
- [ ] B1 top-level produkční `render(inst)` refactor — string→DOM mutation API místo `inst.root.innerHTML = html.join('')` (řádek ~22091 v `document-editor-wysiwyg.js`). Samostatný velký scope (~1500+ řádků refactoru); současný `createAtomicRenderer` má block diff ale není produkční renderer.
- [ ] B1.2–B1.5 + B2.2–B2.3 doplňující explicitní unit testy
- [ ] B4 audit `planDeletion()` clone (širší engine model bezpečnost)
- [ ] B5 RAF batch render integrace do produkčního `render(inst)` (nyní je RAF batch dostupný jen na `createAtomicRenderer`-cestě, opt-in přes `renderBatching: 'raf'`)

**Fáze C:**
- [ ] C2.3 large document (500 KB) serialization perf test
- [ ] C3 audit `StateHasChanged` callbacků a refactor toolbar/status-bar na fine-grained refresh (samostatný scope, ~80 callsites)
- [ ] C4 `PrepareDocumentForDisplayAsync` reference-equal walk místo JSON `CloneDocument()` (klonuje selektivně jen ovlivněné bloky)

**Měření:**
- [ ] Browser/Playwright E2E benchmark s reálným DOM (jediný způsob jak změřit B/C v reálném prohlížeči — Node vm sandbox neumí render)

---

## 12. Definice DONE pro každou položku

- ✅ Testy: unit + bUnit/Playwright (per Fáze 1–15 konvence)
- ✅ Žádná regrese ve stávajících 972 testech
- ✅ Pokud měřitelná: benchmark report v `planning/baselines/perf-<date>.csv` neklesne víc než 5 % vs. před commitem
- ✅ Lokalizační klíče: cs.resx, fr.resx, en.resx (default) + `LocalizationTestBase` mock
- ✅ E2E smoke (alespoň 1 test) ve `tests/Tempo.Blazor.E2E/`
- ✅ Update memory: `~/.claude/projects/-home-pavel-NetProjects-Tempo-Blazor/memory/project_documenteditor_plan.md` s fázovým záznamem (stejný formát jako fáze 1–15)
- ✅ Update `MEMORY.md` pokud potřeba

---

## 13. Rizika

1. **Inkrementální DOM diff (Fáze B) může způsobit regresi v contenteditable selection handling** — selection mode (caret position) je dnes obnovován z `restoreLogicalSelection(root, snapshot.selection)` po `replaceChildren`. Při per-block patch musí být selection-restore přesný. Mitigace: bohatá test suite pro selection edge cases (G2 testy ze stávajícího CKEditor inspirovaného plánu).

2. **Hunspell-WASM velikost** — pokud > 1 MB, narušíme initial load. Mitigace: lazy load + UI hint "Spell check nahrávám...".

3. **Word .docx TOC round-trip** — komplexní field reprezentace; Word toleruje různé varianty. Mitigace: testovat proti reálným .docx z Word 2019/365.

4. **JS modul split (Fáze D)** může poškodit `__testHooks` API používaný v 100+ unit testech. Mitigace: zachovat global `window.tmDocumentEditorWysiwyg.__testHooks` jako re-export.

5. **Page numbers v TOC** vyžadují **layout dokončený před TOC build** — circular: TOC entry s page number změní výšku TOC blocku, což ovlivní page numbers následujících headingů. Mitigace: dvouprůchodový layout (Word to také dělá), max 3 iterace.

---

## 14. Odkazy

- **Dnešní existující paths**:
  - [TmDocumentEditor.razor.cs](../src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor.cs)
  - [TmDocumentWysiwygHost.razor](../src/Tempo.Blazor/Components/DocumentEditor/TmDocumentWysiwygHost.razor)
  - [document-editor-wysiwyg.js](../src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js)
  - [Blocks.cs](../src/Tempo.Blazor/Components/DocumentEditor/Wysiwyg/Model/Blocks.cs)
  - [DocumentBlockRenderer.razor.cs](../src/Tempo.Blazor/Components/DocumentEditor/TmDocumentBlockRenderer.razor.cs)
  - [DocumentOutlineService.cs](../src/Tempo.Blazor.Abstractions/DocumentEditor/Services/) (fáze 14)

- **OnlyOffice odkazy** (z lokálního clone `/home/pavel/NetProjects/onlyfficeservergit`):
  - `sdkjs/word/Editor/SpellChecker/DocumentSpellChecker.js` — per-paragraph deferred kontrola
  - `sdkjs/word/Editor/SpellChecker/ParagraphSpellChecker.js` — paragraph-level
  - `sdkjs/common/spell/spell.js` — WASM Hunspell engine
  - `sdkjs/word/Editor/Field.js` — `fieldtype_TOC`, complex fields
  - `sdkjs/word/Editor/Document.js#AddTableOfContents` — TOC insert
  - `sdkjs/word/Editor/Styles.js` + `Styles/default-styles.js` — styles registry
  - `dictionaries/{cs_CZ,en_US}/` — Hunspell dictionaries
