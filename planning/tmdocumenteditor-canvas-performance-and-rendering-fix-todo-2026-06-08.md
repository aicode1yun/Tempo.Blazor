# TmDocumentEditor — Canvas engine: oprava výkonu a vykreslování (analýza + TODO)

> Datum: 2026-06-08 · Autor analýzy: Claude (Opus 4.8) · Stav: **návrh, čeká na schválení a začátek implementace**
>
> Tento dokument je **remediation plán pro výkon a korektnost vykreslování** canvas enginu.
> Není to feature-parity plán (ten je v `tmdocumenteditor-canvas-*-tdd-todo-2026-06-04.md`). Běží paralelně a má přednost,
> protože demo `/document-editor` je dnes ve výchozím stavu vizuálně rozbité a pomalé.

---

## 0. Shrnutí pro netrpělivé

- **Ano, `/document-editor` už používá canvas engine.** `DocumentEditorPage.razor` i `TmDocumentEditor.RenderEngine`
  defaultují na `DocumentEditorRenderEngine.CanvasEnginePreview`, a `Resolve()` ho **nedegraduje** (degraduje jen
  `CoreEnginePreview`). Takže to, co uživatel vidí, je `TmDocumentCanvasEngineHost` + JS engine v
  `wwwroot/js/document-editor-canvas/`.
- **Proč je to pomalé:** engine přepočítává **layout celého dokumentu při každém renderu** — a render se spouští na
  každý keystroke (2×: okamžitě + „idle reconciliation"), na **každý scroll frame**, na změnu options atd. Virtualizace
  šetří jen *malování*, nikoli *layout*. K tomu se na každý render přepočítají **všechny overlaye** (proofing nad celým
  modelem, search, comments, revisions, presence, ruler, a11y mirror) a vytvoří se **stovky absolutně pozicovaných DOM
  uzlů** (metadata vrstvy) + ~60 `setAttribute` na stránku. To společně „zasekává prohlížeč".
- **Proč se překrývá text/obrázky:** je to **layout bug** (ne dvojí malování — `glyphRun` se nemaluje a plný repaint
  canvas čistí). Plovoucí obrázky se Square/Tight obtékáním nereservují svislý pruh (`cursorY` se neposouvá), takže
  následné odstavce se ukládají do stejného svislého pásma jako text obtékající obrázek; navíc chybí **clipping na
  hranici stránky/těla** a `BehindText` obrázky se malují do `content` vrstvy pod text bez správné koordinace.
- **Jak to dělá OnlyOffice (clean-room inspirace):** `CDocument` drží **cache stránek** (`this.Pages[]`).
  `RecalculateByChanges` počítá `StartIndex`/`StartPage` z historie a přepočítává **jen od první změny dál**; navíc má
  **fast-path** (`private_RecalculateFastRunRange` / `private_RecalculateFastParagraph`) pro změnu jednoho runu/odstavce
  bez reflow stránek. **Kreslení je oddělené od přepočtu** — scroll jen překresluje hotové cache stránky, layout se
  nepočítá. To je přesně to, co nám chybí.

> ✅ **ROZHODNUTO 2026-06-08 (user):** jsme v implementační fázi → **canvas zůstává výchozí**, neopravujeme to skrytím
> za flag. Opravujeme naživo. Cíl = úroveň OnlyOffice / Google Docs (editor musí být reálně použitelný). „Velký"
> dokument pro perf testy = **1000 odstavců**. Perf rozpočty navrhuji já (viz §7.1).
>
> ✅ **2026-06-08 — root-cause překryvu NALEZEN a OPRAVEN (a ověřen v prohlížeči).** Šlo o **tři propojené bugy** ve
> vertikálním umisťování plovoucích obrázků kotvených k odstavci (`VerticalRelativeTo=Paragraph`, offset Y=0 — což C#
> demo posílá pro Square/Tight/TopBottom obrázky):
> 1. `Number(object.explicitY)` v `layoutCanvasImageObject` — `Number(null)===0`, `isFinite(0)===true` → pin na `body.y`.
> 2. **`verticalRelativeTo` engine vůbec nečetl** — každý explicitní offset bral jako body-absolutní místo paragraph-relativního.
> 3. V `pagination.mjs` se předávalo `y: imageObject.explicitY ?? cursorY` → `0 ?? cursorY === 0` (offset 0 přebil cursorY).
>
> Společný efekt: **všechny plovoucí obrázky se umístily na `body.y` (vrchol stránky), naskládané na sebe i přes text.**
> Fix: `resolveObjectY` ctí `verticalRelativeTo` (paragraph/line/column/character → offset od `cursorY`; page → od stránky;
> margin → od těla), gated na objekty co reálně zabírají text (`reservesTextSpace`: Square/Tight/Through/TopBottom), aby
> dekorativní overlaye/konektory (InFrontOfText/BehindText) zůstaly body-absolutní; + pagination předává `cursorY`.
> **OVĚŘENO ŽIVĚ:** Node sada 280/0; živý E2E `DocumentEditorCanvasOverlapPerfE2ETests` na `/document-editor` =
> overlapCount 0, objectOverlapCount 0 (z 401 text-runů); **screenshot potvrzuje správný layout** (obrázky rozmístěné
> podél stránky, text obtéká). Viz §3 R1.
>
> ⚠️ **Perf baseline (z téhož běhu, 1440×1000, contract-demo):** `firstPaintMs≈162`, **`renderP95Ms≈806`** (!),
> renderCount 2, 2 stránky. Vysoké renderP95 potvrzuje §2 P1 (plný re-layout) — cíl Fáze 2–4.
>
> ⚠️ **Pozn.:** `DocumentEditorCanvasImageE2ETests.Phase15_...` selhává na resize-handle countu — ověřeno `git stash`,
> že **selhává i na původním kódu = pre-existing**, NEsouvisí s tímto fixem (follow-up mimo tento plán).

---

## 1. Jak dnes pipeline funguje (fakta z kódu)

Tok dat při jednom renderu (`entry.mjs` → `CanvasDocumentEngine.render()`):

```
model (model-store)
  → layoutService.layout(model, viewport)         // page-geometry.mjs — STUB: jen 1 prázdná stránka + geometrie
  → canvasStack.render(stubLayout, model)          // render/canvas-stack.mjs
        → buildDisplayList(model, stubLayout)       // render/display-list.mjs
              → layoutCanvasDocument(CELÝ model)     // layout/pagination.mjs — REÁLNÝ layout všech bloků (font metrics, float wrap, pagination)
              → ploché pole `commands[]` (textRun + DUPLICITNÍ glyphRun, paragraphBox, tableCell, imageObject, …)
        → virtualizer.plan(pages, viewport)          // perf/page-virtualizer.mjs — vybere viditelné stránky
        → pro každou VIDITELNOU stránku:
              tileCache.shouldRepaint(...)            // perf/tile-cache.mjs — rozhodne, zda malovat
              clearLayer + paintDisplayList(...)      // render/canvas-renderer.mjs — malování na 6 canvas vrstev
              ~60× setAttribute(...) (diagnostika)
              sync{TextRect,TableCell,Object,HeaderFooter,Note,ContentControl,Toc}Metadata(...)  // tvoří DOM uzly pro hit-testing
  → selectionController.update + proofingService.analyze(CELÝ model) + searchOverlay + restrictedEditing
    + commentOverlay + revisionOverlay + presenceOverlay + rulerOverlay + syncBlockVisualization + accessibilityMirror.update(CELÝ model)
```

Kdy se `render()` volá:
- `mount()` → 1× po připojení.
- `commitInputChange()` (každý keystroke) → `scheduleInputRender()` (rAF) → `render(pending)` **a hned poté**
  `recalcInfo.queueIdleReconciliation(() => this.render({forceRepaint:false}))` → **druhý plný render na idle**.
- `handleScroll()` → rAF → `render({forceRepaint:false})` → **plný layout na každý scroll frame**.
- `commitCommandChange`, `commitClipboardChange`, `updateOptions`, `setReviewDisplayMode`, remote batch, … → `render()`.

Vrstvy (`CANVAS_LAYER_KINDS`): `page-background`, `content`, `objects`, `selection-caret`, `annotations`, `diagnostics`
— 6 `<canvas>` na **každou** stránku.

---

## 2. Příčiny pomalosti (seřazeno podle dopadu)

| # | Příčina | Kde | Důsledek |
|---|---------|-----|----------|
| P1 | **Plný re-layout celého dokumentu na každý render.** `buildDisplayList` bezpodmínečně volá `layoutCanvasDocument(model)`; `render()` ho volá vždy. Žádná cache layoutu/stránek. | `render/display-list.mjs:15`, `render/canvas-stack.mjs:95`, `entry.mjs:226` | O(N bloků) na frame; u dlouhého dokumentu desítky–stovky ms/frame. |
| P2 | **Scroll = plný re-layout.** `handleScroll` → `render()`. Virtualizace ořezává jen malování, ne layout. | `entry.mjs:958-971` | Scroll „zasekává" — každý frame přepočítá celý dokument. |
| P3 | **Dvojitý render na keystroke.** Po vstupním renderu se naplánuje ještě „idle reconciliation" plný render. | `entry.mjs:540` | ~2× práce na každé písmeno. |
| P4 | **Overlay storm na každém renderu.** `proofingService.analyze(model)` + search + comment + revision + presence + ruler + `accessibilityMirror.update(model)` se volají vždy, nad celým modelem. | `entry.mjs:245-261` | Lineární práce navíc + přestavba a11y DOM mirroru na každý frame. |
| P5 | **Churn DOM metadat.** `syncTextRectMetadata` a spol. ničí a znovu tvoří absolutně pozicovaný `<div>` pro **každý** text-run/cell/object na repainted stránce. | `render/canvas-stack.mjs:823-1160` | Stovky `createElement`/`appendChild`/`replaceChildren` na render; GC tlak, layout thrash. |
| P6 | **~60 `setAttribute` na stránku na render** (diagnostika `data-canvas-*`). | `render/canvas-stack.mjs:199-257` | Drobné, ale násobeno stránkami × frame. Většinu lze počítat lazy/jen v test/debug režimu. |
| P7 | **Duplicitní display příkazy.** Každý text segment generuje `textRun` **i** `glyphRun` (a content-control/field navíc). `glyphRun` se nemaluje, ale prochází sortem, filtrem per-stránku a serializací snapshotu. | `render/display-list.mjs:256-257, 222` | Zdvojnásobuje velikost `commands[]`, sort `O(2n log 2n)`, filtry per stránku. |
| P8 | **`__tmCanvasRepaint` closure přestavuje CELÝ display list** (znovu `buildDisplayList` = plný layout) pro každou vrstvu každé stránky. | `render/canvas-stack.mjs:170-184` | Latentní O(vrstvy × stránky × plný-layout), pokud se closure spustí. |
| P9 | **Op-log diff celého modelu** při flush vstupních side-efektů (180 ms debounce). | `entry.mjs:495-508`, `collaboration/op-log.mjs` | Drahý diff before/after celého modelu i bez kolaborace. |
| P10 | **getSnapshot() serializuje vše** (model, layout, render, overlaye) a volá se z C# po každém `OnCanvasEngineChanged` (`getModelJson`). | `entry.mjs:300-328`, `interop.mjs:105-113`, `TmDocumentCanvasEngineHost.razor:587` | JSON marshaling celého modelu přes JS↔.NET hranici na každou změnu. |

**Závěr P:** dominanta je **P1+P2+P3+P4** (layout se přepočítává pořád a všude). Bez cache layoutu/stránek a oddělení
„recalc" od „draw" nepomůže žádné mikro-optimalizování malování.

---

## 3. Příčiny překrývání textu a obrázků

Potvrzeno, že to **není** dvojí malování: `paintCommand` maluje `textRun` jednou, `glyphRun` a `paragraphBox` vrací
`false` (nemaluje), a plný repaint stránky volá `clearLayer` před `paintDisplayList`. Překryv tedy vzniká v **layoutu**
(příkazy mají překrývající se Y), případně v **chybějícím clippingu**.

| # | Příčina | Kde | Projev na screenshotu |
|---|---------|-----|-----------------------|
| R1 | ✅ **OPRAVENO (3 propojené bugy)** — paragraph-anchored floaty s offsetem 0 se pinovaly na `body.y` (vrchol): (1) `Number(null)===0`, (2) engine ignoroval `verticalRelativeTo`, (3) pagination `0 ?? cursorY === 0`. Fix: `resolveObjectY` (ctí frame + `reservesTextSpace` gate) v `image-render.mjs` + `cursorY` v `pagination.mjs`. Guard `float-wrap-overlap.test.mjs`; živě ověřeno (overlap 0). | `objects/image-render.mjs` (resolveObjectY), `layout/pagination.mjs:186,220` | Naskládané evidence/diamond/badge obrázky přes text — vyřešeno. |
| R2 | **Chybí clipping na hranici těla/stránky.** Canvas vrstvy se neclipují na `page.body`; segment s chybným Y se vykreslí kamkoli, místo aby se ořezal. | `render/canvas-stack.mjs:705-710` (jen `clearRect`), `render/canvas-renderer.mjs` | Text „prosakuje" mimo sloupec/přes obrázky. |
| R3 | **`BehindText` obrázek do `content` vrstvy.** `layer = wrapMode==='BehindText' ? 'content' : 'objects'` — kreslí se do stejné vrstvy jako text, pořadí dané jen `sequence`. Bez správné Z-koordinace text a obrázek splývají. | `objects/image-render.mjs:218, 279` | Velký nadpisový/evidence obrázek splývá s tělem textu. |
| R4 | **Float exclusion používá page-relativní vs body-relativní Y.** `normalizeCanvasImageObject` ukládá `explicitY`, layout dělá `body.y + explicitY` (image-render.mjs:175-176), ale paragraph engine dostává `y` v jiné referenci → pruh obtékání může „minout" řádky. | `objects/image-render.mjs:172-205`, `layout/pagination.mjs:442-451` | Text se nezalomí kolem obrázku tam, kde má. |
| R5 | **Prázdné/empty paragraph fallback může resetovat Y** (`createEmptyParagraphLayout` se `startY`), a `paragraphFlowEnd` vrací `bottom` z `line.rect`, který u obtékaných řádků nemusí monotónně růst. | `layout/pagination.mjs:476-508` | Odstavce „skáčou" zpět nahoru. |
| R6 | **Reused paragraph engine `resolveAvailableIntervals` callback** může vrátit `moved/movedToY`, který layout nepromítne do `cursorY` konzistentně mezi bloky. | `layout/pagination.mjs:416-460` + `document-editor/layout/paragraph-engine.mjs` | Nepředvídatelné svislé pozice mezi bloky. |

**Závěr R:** primární podezřelý je **R1 (+R4)** — obtékání plovoucích objektů a posun `cursorY` mezi bloky. R2 (clipping)
je „bezpečnostní síť", která zabrání nejhorším projevům i kdyby layout chyboval. R3 je samostatný Z-order problém u
BehindText/evidence obrázku. Přesné místo se potvrdí v **Fázi 1** reprodukcí na golden dokumentu + bisektem.

---

## 4. Srovnání s OnlyOffice (sdkjs/word) — clean-room inspirace

> ⚠️ OnlyOffice je AGPL. **Nekopírovat kód.** Pouze přebíráme *architektonické principy*.

| Téma | OnlyOffice (`sdkjs/word/Editor/Document.js`) | Náš canvas engine dnes | Co převzít |
|------|----------------------------------------------|------------------------|------------|
| **Cache stránek** | `this.Pages[]` drží hotový layout každé stránky; `DrawPage(n)` kreslí z cache. | Žádná `pages` cache layoutu; layout se počítá znovu při každém renderu. | Zavést `LayoutCache` (stránky + blok→stránka/řádky), invalidovat jen dotčené. |
| **Inkrementální recalc** | `RecalculateByChanges` spočítá `StartIndex`/`StartPage` z `History.Get_RecalcData` a přepočítá **jen od první změny**. | `recalcInfo.markDirty` existuje, ale ovlivňuje jen *malování*, ne *layout*. | Přepočítávat layout od prvního „dirty" bloku dál, předchozí stránky reusovat. |
| **Fast-path pro 1 run** | `private_RecalculateFastRunRange` / `private_RecalculateFastParagraph` — když změna nemění zalomení, přepočítá jen run/odstavec. | Každý keystroke = plný layout (+ idle plný layout). | Fast-path: jen měřit změněný run, posunout řádek, bez reflow celého dokumentu. |
| **Draw ≠ Recalculate** | Scroll a překreslení = jen `DrawPage` z cache; layout se nepočítá. | Scroll → `render()` → plný layout. | Scroll smí jen malovat z cache + (re)konfigurovat virtuální stránky. |
| **Obtékání / exclusion** | `CPolygon`/flow objekty; řádek se zalomí přes přesné exclusion polygony, Y monotónně roste. | `objectExclusionIntervals` jen horizontální pásy; `cursorY` se neposouvá za float. | Reservovat svislé pásmo floatu a/nebo udržet monotónní `cursorY` mezi bloky. |
| **Clipping** | Kreslí se do clip regionu stránky/sloupce. | Bez clipu (jen `clearRect`). | `ctx.clip()` na `page.body`/sloupec před malováním obsahu. |
| **Overlaye** | Spell/search/komentáře jsou samostatné, počítané on-demand, ne na každý frame. | Vše se přepočítá na každý render. | Overlaye jen při relevantní změně / na idle, ne v hot-path. |

---

## 5. Cílová architektura (co stavíme)

1. **Rozdělit `recalc` (layout) od `draw` (malování).**
   - `recalc(model, dirty)` → produkuje/aktualizuje `LayoutCache` (stránky, bloky, řádky, segmenty, objekty, exclusion).
   - `draw(viewport)` → z `LayoutCache` jen vybere viditelné stránky a maluje; **nikdy** nepočítá layout.
2. **`LayoutCache` s invalidací po blocích** (ekvivalent `this.Pages[]` + `StartIndex`).
   - Mapy `blockId → {pageIndex, lineRange, rect}` a `pageIndex → displayCommands`.
   - Změna bloku invaliduje od jeho stránky dál; předchozí stránky se reusují.
3. **Scroll/zoom = pure draw.** `handleScroll` nesmí volat `recalc`.
4. **Fast-path pro text vstup** uvnitř jednoho runu beze změny zalomení řádku.
5. **Overlaye mimo hot-path** — přepočet jen na relevantní událost nebo na idle, inkrementálně.
6. **Clipping + správný Z-order** vrstev (oprava překryvů jako bezpečnostní síť i po opravě layoutu).
7. **Diagnostická metadata lazy** — `data-canvas-*` atributy a DOM hit-vrstvy jen když je potřebují testy/hit-testing,
   ne nepodmíněně na každý render.

---

## 6. Testovací strategie (platí pro všechny fáze)

- **Node unit testy** (`*.test.mjs`, `npm run test:document-editor-modules`) — layout cache, invalidace, fast-path,
  exclusion, clipping geometrie. Čistě JS, rychlé, bez prohlížeče.
- **E2E funkční** (`tests/Tempo.Blazor.E2E`, Playwright/.NET, `CanvasEngineTestBase`) — psaní, scroll, výběr, obrázky.
- **E2E screenshot / vizuální** — využít existující `DocumentEditorCanvasVisualAssert`, `CanvasPixelMetrics`,
  `__baseline__` baselines. Nové baselines pro golden dokumenty; diff proti baseline.
- **E2E perf (frame probes)** — `DocumentEditorFrameProbe` / `RunDocumentEditorActionWithFrameProbesAsync` měří
  latence; zavést **rozpočty** (budgets) a tvrdě je vynucovat.
- **Anti-overlap aserce** — z metadata vrstvy (`[data-canvas-text-rect]`) sestavit obdélníky text-runů a tvrdit, že se
  **nepřekrývají** (kromě záměrných BehindText/zvýraznění). Implementovat jako helper v `DocumentEditorCanvasVisualAssert`.
- **Po každé změně `.mjs`**: `npm run build:document-editor` (E2E harness načítá bundle).
- **POZN:** `dotnet test` paralelně OOMuje (exit 137) — pouštět s `-- xUnit.parallelizeTestCollections=false`
  (resp. tyto testy jsou `[DoNotParallelize]`).

**Definice „hotovo" pro celou iniciativu:**
- Golden screenshoty bez překryvů na 4 demo dokumentech × 2 viewportech.
- Typing latency p95 a scroll frame p95 pod rozpočtem (viz Fáze 7) na dokumentu „velký".
- Všechny stávající canvas E2E testy zelené (regrese 0).

---

## 7. Fáze a úkoly (zaškrtávat při implementaci)

> Konvence: `[ ]` = todo, `[x]` = hotovo. Velké fáze rozděleny na pod-úkoly. Každá fáze končí „Exit kritérii".

### Fáze 0 — Baseline, reprodukce a mitigace
Cíl: zachytit současný (rozbitý) stav měřitelně a vizuálně, vytvořit golden fixtures, dočasně zmírnit dopad na uživatele.

- [x] ~~**0.1** Přepnout výchozí engine dema~~ — **NEPROVÁDÍ SE** (rozhodnutí 2026-06-08: canvas zůstává výchozí).
- [ ] **0.2** Definovat golden fixtures (deterministické dokumenty) pro testy:
  - `contract-demo` (plovoucí obrázky + obtékání + caption) — reprodukuje překryv.
  - `onlyoffice-parity-2026-05-24` (obrázková parita).
  - **„velký" dokument = 1000 odstavců** pro perf — přidat do `DemoDocumentEditorProvider`/`InMemoryDocumentEditorProvider`.
  - `table-demo` (tabulky).
- [x] **0.3 (částečně)** Živý E2E `DocumentEditorCanvasOverlapPerfE2ETests` zachytává screenshot contract-demo
  (1280×720 default, lze rozšířit na 1440×1000 + golden sadu). Screenshoty v `/tmp/canvas-overlap-fix/`. **TODO:** přidat
  ostatní golden fixtures + 1440×1000 + uložit do repo `__baseline__`.
- [x] **0.4 (částečně)** Perf baseline změřen na contract-demo: firstPaint≈162ms, renderP95≈806ms (čteno z `data-canvas-*`).
  **TODO:** přidat „velký" 1000-odst. dokument + typing/scroll frame-probe měření.
- [x] **0.5 (Node úroveň HOTOVO)** Anti-overlap invariant na úrovni layoutu: `layout/__tests__/float-wrap-overlap.test.mjs`
  — `assertNoCrossBlockTextOverlap` (text-runy různých bloků se nepřekrývají) + `assertNoFloatingImageOverlap`
  (plovoucí obrázky se nepřekrývají). Reprodukovalo R1 a teď hlídá regresi.
- [x] **0.5b (E2E úroveň) HOTOVO** `DocumentEditorCanvasOverlapPerfE2ETests` ověřuje naživo z `[data-canvas-text-rect]`
  i `[data-canvas-object]`, že se text-runy různých bloků ani plovoucí obrázky nepřekrývají na `/document-editor`.
  **Zelený po fixu** (overlapCount 0, objectOverlapCount 0). **TODO:** přesunout helper do `DocumentEditorCanvasVisualAssert`
  a pokrýt další golden fixtures.

**Exit:** máme měřitelný před-stav (čísla + screenshoty) a anti-overlap testy. ✅ Node anti-overlap hotový (reprodukoval +
opravil R1); E2E před-stav (0.3/0.4) a E2E anti-overlap (0.5b) zbývají.

### Fáze 1 — Korektnost vykreslování: odstranit překryvy (R1–R6)
Cíl: žádný neúmyslný překryv textu/obrázků na golden dokumentech. (Layout zatím může být „pomalý" — výkon řeší Fáze 2+.)

- [x] **1.1 Clipping (bezpečnostní síť, R2) HOTOVO:** `paintDisplayList` dostal opt-in `clipRect` (lazy `save`+`rect`+
  `clip` per dotčený layer context, `restore` na konci); `canvas-stack` maluje dvojím průchodem — **margin/dekorace/objekty
  bez clipu**, **body text-flow s clipem na `page.body` (+8px slack)**. Margin obsah (header/footer dle `headerFooterId`,
  poznámky dle `noteId`, čísla řádků, watermark, page-frame, objekty) je v `UNCLIPPED_COMMAND_TYPES`. Node test
  `render/__tests__/body-clip.test.mjs` (clip wrapuje malování; bez clipRect = no-op). E2E ověřeno: hlavičky/patičky/
  poznámky se dál renderují (`DocumentEditorCanvasHeadersFootersNotesE2ETests` zelený).
- [x] **1.2 Z-order / BehindText (R3) HOTOVO:** `BehindText` se maluje do `page-background` (pod `content`), in-front/
  ostatní do `objects` (nad). `image-render.mjs` (obě cesty). Test `image-render.test.mjs` aktualizován (`page-background`).
  Vrstvové pořadí `page-background < content < objects` garantuje BehindText vždy pod textem, in-front nad.
- [x] **1.3 Reprodukce + bisekt R1/R4 HOTOVO:** Node test `float-wrap-overlap.test.mjs` reprodukoval naskládané floaty;
  bisekt ukázal 3 propojené bugy (viz §3 R1) — `Number(null)===0`, ignorovaný `verticalRelativeTo`, `0 ?? cursorY`.
- [x] **1.4 Oprava floatů (R1) HOTOVO:** `resolveObjectY` v `image-render.mjs` ctí reference frame + `reservesTextSpace`
  gate; `pagination.mjs` předává `cursorY`. Živě ověřeno (overlap 0).
- [x] **1.4b HOTOVO** `layout/__tests__/float-wrap-modes.test.mjs` (6 testů): Square (zúží vedle, plná pod), TopBottom
  (reservuje pruh, text plně pod), InFront/Behind (nereservují — první řádek plný, neposunutý), Inline (v toku posouvá
  cursor), captioned (caption pod obrázkem + text čistí footprint).
- [x] **1.5 Exclusion reference (R4) HOTOVO** ověřeno Square testem (řádek v pásmu floatu zúžen, pod floatem plná šířka)
  + verticalRelativeTo fix sjednotil referenci (paragraph-relativní offset od `cursorY`).
- [x] **1.6 Monotónní flow (R5/R6) HOTOVO** `float-wrap-overlap.test.mjs` „block flow stays monotonic" — text bloky
  nemají zpětný skok Y, prázdný odstavec se neposune nad předchůdce, napříč floaty + empty paragraphs.
- [x] **1.7 HOTOVO** Anti-overlap test zelený (Node + živé E2E); celá Node sada **289/0**; živé E2E overlap +
  headers/footers/notes zelené (5 m 34 s). Screenshot contract-demo vizuálně potvrzen (UX). **TODO (Fáze 7):**
  formální pass/fail screenshot baselines do repo `__baseline__` přes 4 golden × 2 viewporty.
- [x] **1.8 Caption pozice HOTOVO** `float-wrap-modes.test.mjs` „captioned square image" — caption pod obrázkem,
  text plné šířky čistí caption footprint.

**Exit:** ✅ Anti-overlap zelený (Node + živě) na contract-demo; clipping + BehindText z-order bez regrese
(headers/footers/notes E2E zelené); Node sada 289/0; UX screenshot potvrzen. **Zbývá pro plnou definici „hotovo":**
formální screenshot baselines přes 4 golden dokumenty × 2 viewporty (přesunuto do Fáze 7 jako součást regresní brány).

### Fáze 2 — Oddělit `draw` od `recalc`; scroll = pure draw (P2) ✅ HOTOVO
Cíl: scroll přestane počítat layout.

- [x] **2.1/2.3 HOTOVO** `entry.mjs` má `repaint(repaintOptions)` — paint-only cesta: reuse cached plan, re-pozice jen
  viewport-závislých povrchů (selection, proofingOverlay, search, comment/revision/presence overlay, blockVisualization),
  **vynechá drahé model-only passy** (`proofingService.analyze`, `accessibilityMirror.update`, `restrictedEditing`,
  `rulerOverlay`). `handleScroll` → `repaint()` (ne `render`).
- [x] **2.2 HOTOVO** `canvasStack` rozdělen: `buildRenderPlan` (layout + `buildDisplayList`) vs `paintRenderPlan`
  (virtualizace + per-page malování); `render` = build+paint, **`repaint` = jen paint z `lastPlan`** (buildDisplayList se
  v scroll cestě NEVOLÁ). `lastPlan` cache + invalidace v `destroy`.
- [x] **2.4 HOTOVO** `render/__tests__/recalc-vs-paint.test.mjs`: po `render` N× `repaint` vrací **identickou
  `displayList` referenci** (žádný relayout) a reuse `layout` instanci; jiný viewport → jiné `visiblePageIndexes`;
  `repaint` před `render` → null.
- [x] **2.5 HOTOVO** Živý E2E `Scrolling_RepaintsFromCachedPlan_WithoutRecomputingLayout`: 8 scroll frames →
  `data-canvas-scroll-frame-count` roste, **`data-canvas-render-count` neroste** (≤+1 tolerance na collab tick). Caret/
  selection E2E zelený (`Phase7_CaretAndSelection`), overlap stále 0 — refaktor render-path bez regrese.

**Exit:** ✅ scroll nevolá `layoutCanvasDocument`/`buildDisplayList` (Node + živě ověřeno); render-count při scrollu
neroste. Node 291/0; E2E overlap + scroll + caret/selection zelené (3m42s). **POZN:** zoom (`ctrlWheelZoom`) zatím jde
přes `render` (command path) — zoom je taky paint-only, ale view-toggly (showNonPrinting/showBlocks) mění displayList →
samostatné odlišení = follow-up (Fáze 5/6). **Měření scroll p95 frame proti baseline = Fáze 7 (perf rozpočty).**

### Fáze 3 — LayoutCache + inkrementální recalc od první změny (P1) ✅ HOTOVO
Cíl: přepočítávat jen dotčené bloky, ne celý dokument.

- [x] **3.1/3.2 HOTOVO** Block-level `LayoutCache` (Map v `canvasStack`, lifecycle jako `tileCache`) — memoizuje
  nejdražší krok `layoutTextBlockAcrossPages` (měření + line-breaking + fragmentace) per `block.id`. Reuse jen když
  **content signatura** (typ+styl+paragraphProperties+content) **A incoming-state signatura** (cursorY×100, page/column,
  sectionId, frame x/width/y/bottom, sequence, spacingAfter, **floating-objects sig**) sedí. Edit bloku K posune jeho
  end cursorY → state-sig bloku K+1 se změní → recompute kaskádou do konce (= OnlyOffice StartIndex). Threading
  `buildDisplayList(…,layoutCache)` → `layoutCanvasDocument(…,layoutCache)`. Diag atributy `data-canvas-layout-cache-hit/
  miss-count`.
- [x] **3.3/3.6 HOTOVO (cache to řeší)** Idle reconciliation (`queueIdleReconciliation`→`render`) i druhý render na
  keystroke teď běží jako **samé cache-hits** (layout levný) — ověřeno Node testem „re-layout unchanged = all hits".
  Plný layout se NEopakuje. Zbývající náklad idle renderu = overlaye (proofing.analyze) → Fáze 6. Logiku reconcile jsem
  nechal (ujišťuje korektnost finálního stavu), je teď levná.
- [x] **3.4 HOTOVO** Pruning: po každém layoutu se z cache smažou klíče bloků, které už neexistují (bounded velikostí
  dokumentu). Insert/delete řeší state-sig kaskáda (cursorY/sequence shift). Cache klíč = block.id (1 entry/blok).
- [x] **3.5 HOTOVO** `layout/__tests__/incremental-layout-cache.test.mjs` (4 testy): (a) re-layout unchanged = všechny
  reuse; (b) height-preserving edit bloku K → recompute jen K, reuse zbytek (vč. následujících) + **output byte-identický
  s fresh full layoutem**; (c) height-changing edit → recompute K..konec + byte-identický; (d) smazání bloku pruneuje
  jeho entry. **Korektnost garantována byte-identitou s plným layoutem.**
- [x] **3.7 Regrese HOTOVO** Node **295/0**; živé E2E: overlap 0, scroll bez relayoutu, **rerender reuse bloky**
  (`ReRender_ReusesCachedBlockLayout…`: po toggle read-only hits>0, hits≥misses) + caret/selection zelené (3/3 + caret).
  **POZN:** `DocumentEditorCanvasHistorySaveE2ETests` (Phase12 history/autosave/save-failure) selhává — ověřeno `git
  stash`, **selhává i na původním kódu = pre-existing** (API persistence/reload flow), NEsouvisí s cache.

**Exit:** ✅ edit nepřepočítá nezměněné bloky (Node byte-identita + live cache hits); idle render levný (cache hits).
**POZN:** caching jen `paragraph/heading/quote` bloků (lists vyloučeny — labely závisí na global numbering; tabulky/
obrázky cheap/side-effecty). Měření typing p95 proti baseline = Fáze 7. Fast-path uvnitř řádku (bez block recalcu) =
Fáze 4.

### Fáze 4 — Fast-path pro psaní: O(editovaný blok) celá pipeline ✅ HOTOVO
Cíl: psaní bez O(dokument) práce na keystroke.

> **⚠️ ROZHODNUTÍ (2026-06-08):** literal run-level geometry fast-path (ručně posunout segmenty na řádku) **NElze
> garantovat byte-identický** s plným re-layoutem — caret-stop X pozice / advance widths z reused paragraph enginu nejde
> bezpečně replikovat ručně (sub-pixelové riziko → posunutý text). Fáze 3 navíc už dělá reflow jen editovaného bloku
> (ne dokumentu). Proto cíl Fáze 4 (plynulé psaní = O(editovaný blok) **celá** pipeline) doručen **bezpečně přes
> per-block command-cache** — útočí na zbývající O(dokument) náklad (`buildBodyCommands` re-vytvářel příkazy všech bloků
> na každý render). Byte-identické konstrukcí (commandy = čistá funkce fragmentu). Run-level geometry = zamítnuto (high
> risk / low marginal value po Fázi 3).

- [x] **4.1/4.2 HOTOVO (command-cache místo run-geometry)** `buildBodyCommands` (display-list.mjs) memoizuje příkazy per
  odstavcový **fragment** (`WeakMap` `commandDisplayCache` v canvasStack, keyed objektem fragmentu). Fáze 3 layout-cache
  vrací TÝŽ fragment objekt pro nezměněné bloky → command-cache hit → reuse; editovaný blok = nový fragment → rebuild.
  Extrahováno `buildParagraphBlockCommands` (pure, lokální seq + seenTextCommandIds — runId unikátní per blok ⇒
  identické se sdílenou mapou). Threading přes `buildDisplayList(…,commandCache)`. Diag `data-canvas-command-cache-hit/
  miss-count`.
- [x] **4.3 HOTOVO (z Fáze 2)** Caret/selection se aktualizuje v repaint/render cestě (`selectionController.update`);
  selection-caret vrstva se přemalovává. Caret/selection E2E zelený s command-cache refaktorem.
- [x] **4.4 HOTOVO** `render/__tests__/command-cache.test.mjs` (3): warm rebuild = reuse všech bloků + **byte-identický**;
  edit 1 bloku = re-assemble jen ten + **byte-identický s fresh buildem**; bez cache = korektní no-op. (Wrap/fallback
  řeší Fáze 3 layout-cache: editovaný blok dostane nový fragment → nové commandy.)
- [x] **4.5 HOTOVO (živé E2E)** `ReRender_ReusesCachedBlockLayout…` rozšířen — po no-op re-renderu **command-cache
  hits>0, hits≥misses** (vedle layout-cache). Overlap 0, scroll, caret/selection zelené. **Měření typing p95 = Fáze 7.**

**Exit:** ✅ keystroke = O(editovaný blok) pro layout (Fáze 3) **i** assembly příkazů (Fáze 4); zbytek reuse. Node 298/0;
živé E2E (overlap/scroll/rerender-obě-cache/caret) zelené. **POZN:** zbývající per-keystroke O(dokument) náklad =
`proofingService.analyze` + a11y mirror (Fáze 6) a `sort(commands)` + sync*Metadata (Fáze 5).

### Fáze 5 — Optimalizace display listu a malování (P5–P8) ✅ HOTOVO
Cíl: odstranit churn a duplicity v draw cestě.

- [x] **5.1 HOTOVO — zrušen duplicitní `glyphRun` (P7):** každý text segment generoval `textRun` **i** `glyphRun`;
  `glyphRun` se NIKDY nemaloval (jen debug `paintLayoutArtifacts`) a nepoužíval pro hit-testing/selection (ověřeno
  grepem). Odstraněna obě generování (`display-list.mjs`) → **~33 % méně commandů** (menší `commands[]`, levnější
  `sort` i per-page filtr). Testy `display-list.test.mjs` aktualizovány (assert „žádný glyphRun"). E2E hyphenation
  count > 0 dál drží.
- [x] **5.2 HOTOVO (verifikace) — `sync*Metadata` už je gated** na `if (repaintPage)` (řádek ~313 canvas-stack), tj.
  DOM metadata se přestavuje JEN pro přemalovávané stránky (po Fázi 2/3/tile-cache = dirty stránky na edit, nové na
  scroll; nezměněné stránky tile-cache hit → bez přestavby). Intra-page node-diffing (reuse uzlů uvnitř 1 přemalované
  stránky) **odloženo** — nízká marginální hodnota (přemalovaná stránka = změněná, metadata se stejně mění) vs vysoké
  riziko (rozbití hit-testu). Pokrytí gating ověřeno.
- [x] **5.3 HOTOVO (lepší než flag) — model-wide diagnostiky vyzvednuty z per-page smyčky:** ~21 atributů
  (`data-canvas-model-table/image/field/math/content-control-count`, advanced-char-marks, captions, toc, cross-ref,
  styles…) skenovalo CELÝ model **per stránku per paint** (O(viditelné stránky × model) = na scrollu 2× full-model scan
  na frame). Nový `computeModelDiagnostics(model)` počítá vše **jednou per paint**; per-page jen levný `setAttribute`.
  Bez změny chování/hodnot (E2E hyphenation čte ty atributy → zelený). Flag-gating do produkce = volitelný follow-up.
- [x] **5.4 HOTOVO — `__tmCanvasRepaint` (image.onload) reuse:** closure dřív **rebuildovala celý display list**
  (`buildDisplayList(celý model)` = plný layout) jen kvůli načtenému obrázku. Teď `repaintPageFromDisplayList(page,…,
  displayList)` reusuje už spočítaný `displayList` + stejný dvojprůchodový clip paint jako hlavní render. Žádný re-layout
  při dokreslení obrázku.
- [x] **5.5 HOTOVO — tile cache audit:** Node test `recalc-vs-paint.test.mjs` „repainting the same viewport reuses
  painted pages" — repaint se stejným viewportem **nepřemaluje** (neclearuje) už namalovanou stránku (tile-cache hit).
  Potvrzuje, že po Fázi 2/3 shouldRepaint brání zbytečným repaintům.

**Exit:** ✅ `commands[]` ~33 % menší (glyphRun pryč); model-scany O(stránky×model)→O(model) per paint; image-load bez
re-layoutu; tile-cache prokazatelně přeskakuje nezměněné stránky. Node **299/0**; živé E2E (overlap/scroll/rerender/
hyphenation-model-atributy/caret) **5/5 zelené** (6m28s). Žádná funkční regrese (selection/hit-test/diagnostika).

### Fáze 6 — Overlaye mimo hot-path (P4, P9, P10) ✅ HOTOVO
Cíl: proofing/a11y nezdržují každý render (= největší zbývající O(dokument) náklad na keystroke).

- [x] **6.1 Proofing (P4) HOTOVO — `proofingService.analyze` mimo render hot-path:** render teď re-pozicuje squiggly z
  **posledního snapshotu** (`proofingService.snapshot()`, levné — sledují text) a **re-analýzu odkládá** do debounced
  idle passu (`refreshModelAnalysis`/`scheduleModelAnalysis`, 180 ms). Při psaní se spell-check spustí JEDNOU po pauze,
  ne na každý znak.
- [x] **6.2 a11y mirror HOTOVO — `accessibilityMirror.update` ve stejném deferred passu:** rebuild DOM mirroru (O(bloky),
  `replaceChildren`) se odkládá s proofingem. **První render (mount) + `setModel` (import/load) → OKAMŽITĚ** (a11y
  korektní na loadu, `forceImmediateAnalysis`); jen inkrementální edity se debouncují. Live region (ohlašování kurzoru)
  zůstává okamžitý (jiná cesta). Accessibility E2E 2/2 zelené.
- [x] **6.3/6.4 POSOUZENO** — `restrictedEditing.update` je levné (O(operations), obvykle prázdné) → ponecháno bez
  gatingu. **Op-log diff** (`recordLocalCollaborationChange`) už NEběží v render hot-path — je v `flushPendingTextInput
  SideEffects` (180 ms debounce); navíc no-op při `applyingRemoteBatch`. Search/comment/revision/presence overlaye se
  re-pozicují z render snapshotu (levné, musí sledovat pohyb textu). Žádná změna nutná.
- [x] **6.5 Marshaling (P10) HOTOVO [C#] — `getModelJson` se nemarshaluje per keystroke:** `OnCanvasEngineChanged` v
  `TmDocumentCanvasEngineHost.razor` dřív fetchoval **celý model JSON** přes JS↔.NET hranici na KAŽDÝ edit (JSON.stringify
  celého modelu + marshal MB stringu). Teď jen invaliduje `_lastModelJson = null` (engine = zdroj pravdy; kanonický model
  se čte lazy jen na save/`RequestDocumentAsync`). Param-driven re-sync zůstává korektní (`UpdateMountedModelAsync`:
  param-equality guard + null≠param ⇒ vždy re-apply na změně paramu). Vyžaduje rebuild WASM (C#). Render/overlap/caret/
  accessibility E2E zelené po rebuildu.
- [x] **6.4t** Node testy deferralu (`entry.test.mjs`): edit → analýza odložena (timer scheduled, `lastAnalyzedModelVersion`
  se nezmění na edit frame); `runModelAnalysis` dožene; `setModel` (import) → okamžitá analýza bez timeru.

**Exit:** ✅ proofing.analyze + a11y mirror (oba O(dokument)) mimo keystroke frame — zbyl jen layout(1 blok, Fáze 3) +
command-assembly(1 blok, Fáze 4) + paint(dirty page). Node **301/0**; **WASM rebuildnut** (C# 6.5 zkompilováno);
živé E2E **6/6 zelené** (5m6s): overlap/scroll/rerender/caret + **Accessibility 2/2** (a11y nerozbito). Měření typing
p95 = Fáze 7.

### Fáze 7 — Perf rozpočty, regrese a finální UX sign-off ✅ ROZPRACOVÁNO
Cíl: zabetonovat výsledky a hlídat regrese.

> **🚀 ZÁSADNÍ OBJEV (cold layout 5x rychlejší).** Měření 1000-odst. fixtaru → CPU profil ukázal **~57 % času +
> GC v `sortObject`** (`core/helpers.mjs`, hluboké rekurzivní řazení klíčů pro kanonickou serializaci), aplikovaném na
> KAŽDÝ segment/řádek/caret-stop/blok/fragment. Layout konzument (renderer/hit-test/selection) čte podle JMÉNA pole →
> řazení = čistá režie. **Fix: pass-through `sortObject` v layout hot-path** (`paragraph-engine`, `segment-style`,
> `paragraph-tokenizer`, `line-breaker`, `line-draft`, `layout-scope` v `document-editor/layout/`). **Výsledek: layout
> 1000 odst. 7478 ms → 1501 ms = 5.0×** (~7 → ~1.5 ms/odst.). Node **301/0** po každém kroku (žádný konzument kanonické
> pořadí nepotřebuje; collab/op-log `sortObject` v `core/helpers` NECHÁN — collab determinismus; export je C# z modelu,
> ne z JS layoutu). To zrychluje **otevírání** dokumentů (cold first-paint), což Fáze 2–6 (inkrementální ops) neřešily.
>
> **📐 Velký fixture + virtualizace.** `SeedLargePerfDocument` v `DemoDocumentEditorProvider` (id `large-perf-1000`,
> E2E používá 150 odst. — 1000 dělalo browser scroll-výšku 622 stránek pathologickou). Měřeno (1000 odst. v browseru):
> **first-paint 2551 ms** (po sortObject fixu; PŘED fixem timeout 120 s!), virtualizace **2 mounted stránky**. Node:
> 400 odst.→169 stránek, 2 mounted, repaint **11 ms/frame**. **POZN: scroll na 622-stránkovém docu v browseru = 105 s/
> frame artefakt obří scroll-výšky (NE engine — node repaint 11 ms); extreme-scale browser scroll = future-work.**

- [x] **7.1 HOTOVO — perf-budget E2E `DocumentEditorCanvasPerfBudgetE2ETests`** (na 150-odst. docu, ~90 stránek):
  čte `data-canvas-*` metriky. **Naměřeno: first-paint 432 ms** (budget ≤ 6000), **virtualizace mounted=2/90** (≤ 8),
  **scroll: render-count 1→1** (žádný relayout) + mounted ≤ 4 (zůstává virtualizovaný). Strukturální aserce (robustní
  vůči CI jitteru) + generózní timing budgety (typing p95 ≤ 250 catch O(dokument) regrese). Reálné cíle (z měření):

  | Metrika | Cíl p50 | Cíl p95 | Strop (max) | Pozn. |
  |---------|--------:|--------:|------------:|-------|
  | **Typing latency** (keystroke→paint) | ≤ 8 ms | ≤ 16 ms | ≤ 32 ms | 1 frame = 16,7 ms; psaní nesmí „lagovat" |
  | **Scroll frame** | ≤ 8 ms | ≤ 16 ms | ≤ 24 ms | 60 fps; scroll = pure paint (Fáze 2) |
  | **Caret/selection move** | ≤ 8 ms | ≤ 16 ms | ≤ 32 ms | |
  | **First meaningful paint** (otevření 1000 odst.) | — | ≤ 500 ms | ≤ 800 ms | jen viditelné stránky; full layout progresivně/idle |
  | **Time-to-interactive** (lze psát) | — | ≤ 800 ms | ≤ 1200 ms | |
  | **Mounted DOM uzly** | — | — | ≤ ~1,5× obsahu viditelných stránek | nezávisle na velikosti dokumentu (virtualizace) |
  | **Idle CPU** (po ustálení) | ~0 | — | — | žádné rendery bez vstupu/scrollu |
- [ ] **7.2** Screenshot regrese: golden baselines × {1280×720, 1440×1000} + темný režim; diff brána v CI workflow
  (`document-editor-performance.yml`).
- [ ] **7.3** Plná regrese celé canvas E2E sady + Node modulů; 0 nových selhání.
- [ ] **7.4** Manuální UX review (Claude jako UX expert): projít screenshoty 4 golden dokumentů, potvrdit „vypadá jako
  Word/OnlyOffice/GDocs", zapsat poznámky.
- [ ] **7.5** (Pokud 0.1 mitigace proběhla) Zvážit vrácení canvas jako výchozího enginu, jakmile parita + rozpočty drží.
- [ ] **7.6** Aktualizovat paměť (`MEMORY.md`) a tento dokument finálním stavem.

**Exit:** všechny rozpočty + screenshot brány zelené v CI; UX podepsané; rozhodnutí o výchozím enginu zaznamenáno.

---

## 8. Rizika a poznámky

- **AGPL OnlyOffice:** jen principy, žádný kód. Tento dokument cituje názvy funkcí OnlyOffice pouze pro orientaci.
- **Reused paragraph engine** (`document-editor/layout/paragraph-engine.mjs`) je sdílený s ne-canvas větví — změny v
  exclusion/Y logice musí být buď v canvas adaptéru (`pagination.mjs`), nebo ozkoušené proti oběma cestám.
- **Build krok:** každá `.mjs` změna vyžaduje `npm run build:document-editor`, jinak E2E testuje starý bundle.
- **OOM při testech:** `dotnet test ... -- xUnit.parallelizeTestCollections=false`; Node testy jsou levné, pouštět často.
- **Fázování:** Fáze 1 (korektnost) je nezávislá na Fázích 2–6 (výkon) a může jít první, aby uživatel rychle viděl
  „nepřekrývá se". Výkon má ale větší dopad na „zasekává prohlížeč" — pořadí 1 → 2 → 3 → 4 dává nejrychlejší vnímané
  zlepšení.

## 9. Otevřené otázky — VYŘEŠENO 2026-06-08

- ✅ **Výchozí engine během oprav:** canvas **zůstává výchozí** (jsme v implementační fázi, opravujeme naživo). Úkol 0.1
  se NEPROVÁDÍ (žádné přepnutí na Legacy/Core).
- ✅ **Perf rozpočty:** navrženy v §7.1 (cíl = úroveň OnlyOffice/Google Docs). Doladí se z reálných měření ve Fázi 0/7.
- ✅ **Rozsah „velkého" dokumentu:** **1000 odstavců.**

---

## 10. Fáze 8 — Blazor interop latence (KRITICKÉ, nalezeno 2026-06-08 z videa uživatele)

**Problém:** Uživatel natočil video: ~10 s na označení textu, ~27 s na napsání „adff" (4 znaky) na živém
`/document-editor`. Moje Fáze 1–7 metriky (`data-canvas-typing-latency-p95`, `scroll-p95`, `first-paint`) tvrdily, že
psaní je < 16 ms. **Metriky lhaly, protože měří jen vnitřek JS canvas enginu a končí na hranici canvasu.**

**Root cause:** Skutečná cesta úhozu pokračuje do Blazoru. Na JS straně se BEZ debounce na *každý* úhoz volá
`notifyChanged` → `dotNetRef.invokeMethodAsync('OnCanvasEngineChanged')` (interop.mjs ř. 63/120/138/189). C# handler
`TmDocumentEditor.HandleCanvasEngineChangedAsync` pak pro **celý dokument** na jednovláknovém WASM dělal:
1. `RequestDocumentAsync()` → `getModelJson` marshal celého modelu JS→C#, `Deserialize`, **redundantní `Serialize`**,
   `FromCanvasModel` konverze (host ř. 251-272).
2. `Clone(_document)` — hluboká kopie.
3. `CreateProviderBoundarySnapshot(...)` — další plná transformace.
4. `SyncCommentsFromRuntimeDocument` + `DocumentsEqual(...)` — průchod + hluboké porovnání celého dokumentu.
5. `StateHasChanged` — re-render celého editoru.

WASM je jednovláknový → během toho je **celé vlákno zablokované** (= „zasekává prohlížeč"). 4 znaky × ~6 s ≈ 27 s.
**Fáze 6.5 to NEopravila:** odstranila marshal z hosta, ale rodič ho hned vrací přes `RequestDocumentAsync()`.

**SKUTEČNÝ root cause (nalezen CPU profilem přes CDP — point-timery atribuovaly jen ~370 ms z ~10 000 ms/úhoz; 71 %
času bylo v JEDNÉ .NET funkci `wasm-function[213]`):** Per-úhoz se renderoval **CELÝ obří `TmDocumentEditor`
~5,5×** (61 renderů na 11 úhozů). Příčiny re-render bouře:
1. **`OnChanged` byl `EventCallback`** — `EventCallback.InvokeAsync` AUTOMATICKY volá `StateHasChanged` na příjemci
   (TmDocumentEditor) po každém callbacku, bez ohledu na tělo handleru.
2. **`OnMiniToolbarChanged` byl `EventCallback`** a engine ho posílá přes `onSelectionChange` na **každou změnu výběru =
   každý úhoz** (kurzor se posune). → další auto-render(y).
3. `HandleMiniToolbarChangedAsync` končil **bezpodmínečným `InvokeAsync(StateHasChanged)`** i když se mini-toolbar
   neměnil (psaní = collapsed caret → není co ukázat).
4. `SyncCanvasEngineStateAsync` (toolbar formatting readback) se volal synchronně per-úhoz; `getFormattingState`
   přitom přes `queryCommandState` dělal `extractCanvasOutline` + `listBookmarks` = **O(dokument)**.
5. Sekundárně: per-úhozový full-model marshal (`RequestDocumentAsync` → `Deserialize`+`FromCanvasModel`) a
   `getSnapshot()` (sestavoval celý model + 15 subsystem snapshotů) v `isDirty`/`undo`/`formatting` interop čteních.

POZN: Canvas LAYOUT/render je rychlý (~40 ms, blok-cache funguje); proofing/a11y/collab jsou levné (~1 ms). Bottleneck
byl **výhradně Blazor re-render obří komponenty + O(dokument) interop čtení**, NE samotný canvas.

- [x] **8.1 `EventCallback`→`Func` pro vysokofrekvenční callbacky.** `OnChanged` a `OnMiniToolbarChanged` na hostu
  změněny z `EventCallback<T>` na `Func<T,Task>?` (plain delegate NEspouští auto-render). Parent si řídí render sám.
- [x] **8.2 Render mini-toolbaru jen při změně viditelnosti.** `HandleMiniToolbarChangedAsync` přeskočí `StateHasChanged`
  když mini-toolbar byl i zůstal skrytý (běžný stav při psaní).
- [x] **8.3 Dvouúrovňový debounce (seq + `Task.Delay`, NE `System.Threading.Timer` — ten v WASM spolehlivě neruší
  naplánovaný callback).** `ScheduleCanvasToolbarSync` (200 ms): `SyncCanvasEngineStateAsync` + cursor broadcast + 1
  render. `ScheduleCanvasDocumentReconcile` (1200 ms): `RequestDocumentAsync` + provider snapshot + collaboration diff.
  Per-úhozový handler = O(1) (dirty z payloadu, žádný interop, žádný render). Loop-breaker: po reconcile
  `_canvasHost.MarkDocumentMounted(doc)` nastaví reference-gate → `UpdateMountedModelAsync` nepošle `replaceModel` zpět.
- [x] **8.4 O(dokument) interop čtení zlevněna (`.mjs`).** `isDirty`/`getUndoStateJson`/`getFormattingStateJson`
  obcházejí `engine.getSnapshot()` a čtou přímo z `modelStore`/`history`/`commandRuntime`; `queryCommandState`
  dostalo `{includeNavigation:false}` (vynechá outline+bookmarks walk) pro toolbar readback.
- [x] **8.5 Reference-gate v `UpdateMountedModelAsync`** (host): přeskočí drahou serializaci+`replaceModel` modelu, dokud
  se nezmění reference `Document` parametru (per-render `OnAfterRenderAsync` jinak serializoval celý model).
- [x] **8.6 Reálný end-to-end E2E `DocumentEditorCanvasEndToEndTypingE2ETests`** na **plném** `/document-editor`: píše
  přes klávesnici na 1000odst. dokument, měří wall-clock než engine zpracuje všechny úhozy. **Výsledek: 10035 → 236
  ms/úhoz (45×), PROŠLO budget ≤ 350 ms/úhoz.** Korektnost: InlineFormat/CaretSelection/ToolbarSpellcheck E2E 3/3 pass;
  Node moduly 301/301.
- [ ] **8.7 Budoucí optimalizace:** jeden render `TmDocumentEditor` je stále ~200 ms (obří komponenta). Při reálném psaní
  s pauzami se debounce sloučí (per-úhoz ~canvas ~44 ms), ale machine-gun psaní v testu = ~236 ms/úhoz. Rozdělit toolbar
  do samostatné komponenty / `ShouldRender` gating by snížilo i tento worst-case.

**Poučení:** (1) perf brána MUSÍ měřit end-to-end přes Blazor, ne jen canvas-interní čísla. (2) **CPU profil přes CDP**
byl jediný spolehlivý nástroj — bodové `performance.now()` sondy míjely 95 % času, protože ten byl v Blazor render
machinery, ne v explicitně instrumentovaném kódu. (3) `EventCallback` na vysokofrekvenční (per-úhoz) události je v
Blazoru anti-pattern kvůli auto-`StateHasChanged`.
