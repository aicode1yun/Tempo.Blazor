# Fáze D — zbývající extrakce do modulů (pracovní mapa)

**Vytvořeno**: 2026-05-29
**Zdroj**: `src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js` (monolit, **27 076 řádků**)
**Cíl modulů**: `src/Tempo.Blazor/wwwroot/js/document-editor/{core,history,layout,render,input,clipboard,objects,collaboration,accessibility,runtime}/*.mjs`
**Stav**: **237 `.mjs` modulů** extrahováno. Entry: `runtime/entry.mjs` (`version: phase-d-skeleton-187`).
**Testy**: `tests/Tempo.Blazor.Tests/DocumentEditor/Performance/PhaseDModuleExtractionTests.cs` (254 testů).

> **Pracovní postup**: čísla řádků jsou ze snapshotu monolitu 2026-05-29. Při každé extrakci nejdřív `grep -n` ověřit, že se řádky neposunuly. Po extrakci: `node -e "import('./runtime/entry.mjs')..."` smoke + příslušný test + odškrtnout zde.

---

## 0. Hotové opravy v této session

- [x] **REGRESE FIX**: `isSafeInlineCssColor` byl deklarován 2× (`render/inline-style.mjs` + `render/inline-style-sanitise.mjs`) a entry.mjs ho importoval z obou → `SyntaxError: Identifier ... already been declared`, padal `PhaseD2_RuntimeEntryPointReexportsAllMigratedModules`. Fix: `inline-style.mjs` re-exportuje canonical verzi ze `inline-style-sanitise.mjs` (parita s monolitem `{0,31}` keyword + kombinované rgb/hsl regex na ř. 20652), entry.mjs už ho importuje jen jednou.

---

## 1. Klíč k notaci

- **[pure]** — žádné DOM / instance-state závislosti → přímá extrakce (kopie + `export`).
- **[factory]** — engine-state závislost → `createXxx({ dep1, dep2 })` dependency injection (vzor: `transactions.mjs`, `indexes.mjs`, `apply-operation-dispatcher.mjs`).
- **[DOM]** — sahá na `document`/`window`/`element` → extrahovatelné jen jako factory s injektovaným DOM adaptérem; **nízká priorita / vysoké riziko**.
- **[bridge]** — Blazor JS-interop instance metoda; součást runtime wrapperu, extrahuje se až nakonec.

---

# R. PŘEPIS JÁDRA EDITORU — strategické rozhodnutí (2026-05-29)

> **Status**: rozhodnuto jít cestou přepisu render+input jádra. Cíl uživatele: **plná parita s Word / Google Docs / OnlyOffice** (vše — IME, bidi, přístupnost, tabulky, obrázky s obtékáním, revize, komentáře). Čas není limit, ale výsledek musí být pořádný. Postup: **inkrementálně, s průběžnými Playwright testy** (každý milník má E2E bránu).
>
> Tato sekce **nahrazuje krok 4.2.5** (migrace produkčního render path). Sekce D (extrakce modulů) zůstává platná jako *příprava* — vyextrahovaná datová + layout vrstva je základ nového jádra.

## R.0 ⚠️ LICENČNÍ VAROVÁNÍ — OnlyOffice je AGPL-3.0-only

OnlyOffice sdkjs (`/home/pavel/NetProjects/onlyfficeservergit`) je licencován pod **GNU AGPL-3.0-only** (ověřeno v licenčních hlavičkách každého souboru). To znamená:

- **NESMÍME kopírovat ani odvozovat kód** z OnlyOffice do Tempo.Blazor. AGPL je virální copyleft — pokud by se jakýkoliv jeho kód (i přepsaný „podle") dostal do produktu, **celý Tempo.Blazor produkt by musel být zveřejněn pod AGPL** (včetně serverové části, protože AGPL pokrývá i síťové použití).
- **SMÍME se inspirovat architekturou a myšlenkami** — architektonické vzory, datové struktury jako koncept, a algoritmy popsané obecně nejsou chráněné autorským právem. Ale **každý řádek musí vzniknout nezávisle** (clean-room: pochop princip → zavři zdroj → napiš vlastní implementaci).
- **Praktické pravidlo**: OnlyOffice používáme jako *referenci „jak to dělají profíci"* (jaké subsystémy existují, jak je rozdělit, jaké hrany pozor), NE jako zdroj kódu. Žádný copy-paste, žádné „přepiš mi tohle do TS".

## R.1 Co dělá OnlyOffice (zjištěno z analýzy sdkjs/word, ~431k řádků jen word + ~397k common)

| Subsystém | OnlyOffice přístup | Soubor (ref) |
|---|---|---|
| **Vykreslení** | **Canvas 2D** — celý dokument se maluje na `<canvas>` přes vlastní `Graphics` abstrakci (fillText/drawImage/rect/…) | `word/Drawing/Graphics.js` (~3100 ř.) |
| **Souřadnice** | **device-independent v mm** → px přes `zoom × g_dKoef_mm_to_pix × retina` | `Graphics.js`, `DrawingDocument.js` |
| **Kurzor** | Ručně kreslený blikající „target" (HTML element nad canvasem, `setInterval(DrawTarget, 500)`) | `DrawingDocument.js` (`id_target_cursor`) |
| **Výběr / handles** | Samostatný **overlay canvas** (`m_oOverlay`, vlastní 2D context) | `HtmlPage.js`, `common/Overlay.js` |
| **Vstup + IME** | **Skrytý `<textarea>` NEBO `contentEditable` div** (podle platformy — „a single textarea is not enough") chytá keydown + `compositionstart/update/end` | `common/text_input.js` (~1700 ř.) |
| **Layout / recalc** | Plný JS recalculation engine — řádkování, stránkování, sekce | `Paragraph_Recalculate.js` (~4750 ř.) |
| **Text shaping** | Bidi, grapheme clustery, shaping (HarfBuzz-like), dělení slov | `bidi-flow.js`, `TextShaper.js`, `GraphemesCounter.js`, `TextHyphenator.js` |
| **Hit-test** | Mouse na canvasu → souřadnice → pozice v modelu | `search-position-by-coords.js`, `position-calculator.js` |
| **Přístupnost** | Canvas není čitelný screen readery → OnlyOffice udržuje **paralelní off-DOM ARIA strom** | `common/` accessibility |

**Měřítko**: ~830 000 řádků JS, dedikovaný tým, roky. To je realistický „strop" plné parity — Tempo na to nemá mít ambici 1:1, ale architektura musí být **správná od začátku**, aby se k paritě dalo iterovat.

## R.2 Cílová architektura pro Tempo — **positioned-DOM, NE canvas**

**Klíčové rozhodnutí: NEjdeme cestou canvasu jako OnlyOffice/Google Docs.** Důvod — pro tým bez 50 lidí je canvas špatný kompromis:

| | Canvas (OO/GDocs) | **Positioned-DOM (volba Tempo)** |
|---|---|---|
| Kontrola layoutu | úplná | úplná (layout engine vlastní pozice) |
| Přístupnost | ✗ musíš postavit paralelní ARIA strom | ✓ reálné text-uzly → screen reader je čte |
| Find-in-page (Ctrl+F prohlížeče) | ✗ nefunguje | ✓ funguje nativně |
| Výběr / copy rich textu | ✗ reimplementuješ | ✓ částečně nativní |
| Rasterizace fontů | ✗ vlastní glyph metrics engine | ✓ prohlížeč kreslí text |
| Výkon na velkém dokumentu | výborný | dobrý **s virtualizací** (jen viditelné stránky) |
| Pixel-perfektní tisk | ✓ | ✓ (layout je mm-based, DOM jen prezentace) |

→ **Tempo = model-owned layout (jako OO) + positioned-DOM render (atomic renderer, už existuje) + virtualizace stránek (už existuje `buildPagePlan`).** Bereme z OnlyOffice *architekturu* (oddělený layout/render/input, off-screen IME input, app-kreslený kurzor, mm souřadnice, recalc subsystémy), ale render zůstává reálné DOM. To je nejlepší poměr kontroly a „zadarmo" funkcí.

**Druhé klíčové rozhodnutí: font metriky přes offscreen `canvas.measureText`.** Headless layout potřebuje reálné šířky znaků bez DOM reflow. Řešení: jeden offscreen `<canvas>` 2D context → `measureText().width` + ascent/descent, cachované. Tím layout engine dostane reálné metriky (ne dnešní syntetické „0.55× fontSize") BEZ čtení z dokumentového DOM. (Toto je jediné použití canvasu — jen na měření, ne na kreslení.)

## R.3 Co se znovupoužije vs. co se přepíše

| ✓ Znovupoužít (hotové, modulární z fáze D) | ✗ Přepsat (nové jádro) |
|---|---|
| `core/` — model, schema, import/export, marks, canonical | **font-metrics service** (offscreen canvas measureText) — NOVÉ |
| `history/` — operace, transakce, undo/redo, revize | **off-screen input surface** (textarea/contentEditable + IME) |
| `layout/paragraph-engine` + `line-breaker` + exclusions | **app-kreslený kurzor + selection overlay** (z layout snapshotu) |
| `objects/` — image/table/drawing pipelines | **hit-test pointer→model** (z layout snapshotu, ne z DOM) |
| `render/atomic-renderer` (positioned DOM, B1/B2 diff) | **bidi + grapheme + shaping** vrstva (rozšíření layoutu) |
| `runtime/` — boundary-patch, watchdog, command-execute | **render-commit** = jen atomic path (žádný contenteditable flow) |
| serializace, fingerprint, performance-probe | **accessibility ARIA** vrstva nad positioned-DOM |

**Zahozeno z legacy monolitu**: `render(inst)` string-HTML path (ř. 22078), contenteditable body povrch, `buildLayoutSnapshot` (DOM read-back layout — nahradí headless engine), DOM-measurement layout.

## R.4 Inkrementální plán s Playwright branami

> Každý milník: (a) TDD unit testy v Node, (b) **Playwright E2E brána** v reálném prohlížeči, (c) měření latence psaní proti baseline (`planning/baselines/perf-e2e-2026-05-26.csv`). Nový engine žije za feature-flagem `renderEngine: 'tempo-core'` (default OFF), produkční legacy běží paralelně až do R7 cutoveru. Nové moduly v `src/Tempo.Blazor/wwwroot/js/document-editor/core-engine/` (nový namespace, ať se nemíchá s extrakcí).

### R.4.0 Font-metrics service [foundation] ✅ (2026-05-29, entry version 202, 274 PhaseD testů)
- [x] `layout/font-metrics.mjs` — `createFontMetricsService({ createMeasureContext?, cacheLimit? })`. Reálné metriky přes injektovatelný 2D canvas context (default: OffscreenCanvas → `document.createElement('canvas')` → null). `measureRun({text, fontFamily, fontSize, bold?, italic?, fontWeight?, fontStyle?, letterSpacing?, zoom?}) → {width, ascent, descent, lineHeight}` (font-level bounding box ascent/descent, fallback actualBoundingBox → syntetický poměr). **LRU cache** (default cap 4096, evikce nejstaršího). Pure exporty: `normalizeFontMetricStyle`, `fontStringFromStyle`, `syntheticRunMetrics`, `computeFontMetricKey`.
- [x] **Drop-in pro engine**: `measureTextRun(request) → {Text, Width, Height}` + `measureText(text, style) → {width, height}`. V Node (bez canvasu) spadne na **identický** syntetický vzorec jako legacy `text-measurement` → engine byte-stabilní; v prohlížeči se upgraduje na reálné metriky.
- [x] Zapojeno do `paragraph-engine` jako **default measurement service** (`measurementService || createFontMetricsService()`); odstraněn dead import `createTextMeasurementService`; wrapper `serviceWithMeasureText` aplikován jen když service `measureText` nemá (font-metrics ho má nativně → fix non-writable-prop crashe).
- [x] Test `PhaseR0_FontMetricsServiceFallsBackSyntheticAndUsesRealContext` (Node fallback + injektovaný fake canvas: font string, advance, letterSpacing, zoom, ascent/descent, LRU evikce, cache hits). Namespace `layout.fontMetrics`.
- **Brána (Playwright, ZBÝVÁ)**: změřit šířku známého textu v reálném fontu v prohlížeči vs. `range.getBoundingClientRect` (tolerance < 1px) — vyžaduje běžící WASM demo, spustit při R.4.1 harness bráně.

### R.4.1 Headless render harness (positioned-DOM + virtualizace) [foundation] 🟡 ČÁSTEČNĚ (2026-05-29, entry version 203, 275 PhaseD testů + bundle 931 KB)
- [x] `core-engine/render-host.mjs` — `createRenderHost({ doc?, pageSettings?, layoutOptions?, measurementService? })` → `{ mount, setModel, setSelection, setViewport, render, getLayout, getSnapshot, getRenderer, getEngine, destroy }`. **První funkční pipeline**: model → `buildIndexes` → `paragraph-engine.layoutDocument` (reálné font metriky z R.4.0) → `createRenderSnapshot` → `atomic-renderer.render` do rootu. Žádný contenteditable, žádný DOM-readback layout.
- [x] **Virtualizace**: `setViewport({scrollTop, height, overscanPages?})` → `visiblePageIndices` filtruje `layout.blocks` jen na viditelné stránky (±overscan); page frames zůstávají (zachová scroll výšku). `setViewport(null)` = vykreslit vše.
- [x] **Opraveny 2 latentní chyby z 4.1 extrakce** (maskované stubem v PhaseD4 testu): paragraph-engine volal `createAnchoredDrawingResolvers` se 3 špatnými deps (potřebuje 7) a destrukturoval `createAnchoredDrawingRunCollector` výsledek jako objekt (vrací funkci přímo). Teď engine importuje resolver/collector + jejich deps přímo; injektuje se jen `findBlock`.
- [x] Test `PhaseR1_RenderHostRunsPipelineAndVirtualizes` (DOM stub: 120-odst. dok → 4 stránky/120 bloků vykresleno; viewport 1200px → jen 2 stránky/68 bloků; B1 fingerprint hit na re-render; graceful guards before-mount/after-destroy). Namespace `coreEngine.createRenderHost`. Bundle staví čistě (`npm run build:document-editor`, 931 KB, obsahuje createRenderHost).
- [x] **Playwright brána ✅ SPLNĚNA v reálném prohlížeči** (2026-05-29, headless Chromium vs běžící WASM demo 7106). Harness: `src/Tempo.Blazor.Demo/wwwroot/core-engine-harness.html` (načte dist bundle z `_content/Tempo.Blazor/js/document-editor.dist.js` → `window.tmDocumentEditorModules`). Test: `tests/Tempo.Blazor.E2E/CoreEngineRenderHostE2ETests.cs` (2 testy, oba zelené):
  - `R41_RenderHost_RendersMultiPageDocument_AndVirtualizes` — 100p dok → multi-page layout, všech 100 bloků vykresleno bez viewportu, virtualizace = strict subset, reálné canvas metriky aktivní.
  - `R40_FontMetrics_MatchBrowserTextMeasurement_WithinOnePixel` — **font-metrics vs `getBoundingClientRect`: diff 0.000–0.012px** (4 vzorky, A4 Arial 12/16/24px). Validuje R.4.0 offscreen-canvas přístup.
- **POZN — first-paint 100p = 485.7 ms** (cold, full-doc layout přes headless engine s reálným per-run `measureText`). Není to per-keystroke (jednorázové). **Optimalizace pro pozdější milník**: `layoutDocument` počítá layout CELÉHO dokumentu před virtualizací renderu → first-paint nese náklad celého dokumentu. Pro rychlý first-paint je potřeba i **virtualizovaný layout** (nejdřív jen viditelné stránky), ne jen virtualizovaný render. Font-metrics LRU cache pomáhá re-renderům, ne prvnímu.
- **Servery**: `dotnet run --project src/Tempo.Blazor.Demo --launch-profile https` (7106) + `dotnet run --project src/Tempo.Blazor.Demo.Api --launch-profile Tempo.Blazor.Demo.Api` (5100). Po změně `.mjs` modulu vždy `npm run build:document-editor` (harness načítá bundle, ne zdroje).

### R.4.2 Off-screen input surface [core — typing] ✅ (2026-05-29, entry version 204, 277 Node + 3 E2E)
- [x] `core-engine/input-surface.mjs` — `createInputSurface({ doc?, handlers })`. Skrytý **textarea** mimo viewport (left:-9999px, opacity:0, NE display:none — musí přijímat klávesy+IME). **Enter/Backspace/Delete/šipky v `keydown`** (deterministické bez ohledu na prázdný buffer — textarea jinak dělá Enter=insertLineBreak a Backspace na prázdném nemusí firovat beforeinput); **vkládání textu + paste v `beforeinput`** (preventDefault → intent → buffer zůstává prázdný); IME teče přes textarea, commit na `compositionend`. Vzor OnlyOffice `text_input` (architektura, clean-room).
- [x] `core-engine/edit-model.mjs` — pure model mutace zachovávající runs/marks (reuse insert-text-run + run-mutators): `applyInsertText` / `applyDeleteBackward` (+cross-block merge) / `applyDeleteForward` (+merge next) / `applyInsertParagraph` (split, top-level body). Vrací `{ ok, caret, structural }`.
- [x] render-host: `attachInput()` zapojí surface → intents → edit-model → caret + version bump + re-render. **Surface mountován MIMO render root** (root.parentNode/body) — `render()` dělá `replaceChildren(root)`, jinak by textarea odpadla a ztratila fokus (reálná chyba odhalená E2E).
- [x] **Bonus fix**: atomic renderer dostal opt-in `contentEditableRegions` (default true pro legacy paritu); render-host ho vypíná → **žádný contenteditable nikde** v novém enginu (header/footer regiony nesly legacy `contenteditable=true`).
- [x] Node testy `PhaseR2_EditModelMutatesRunsAndCaret` + `PhaseR2_InputSurfaceRoutesKeystrokesToModel` (incl. CJK composition commit). Namespace `coreEngine.createInputSurface` + `coreEngine.editModel`.
- [x] **Playwright brána ✅** (`R42_OffScreenInput_TypingRoutesToModel_NoVisibleContentEditable`): reálné psaní přes `page.Keyboard` → "Hello" + Enter (split na 2 bloky) + "World" → renderovaný text `Hello | World`, `contentEditableCount === 0`, capture = TEXTAREA.
- **POZN**: routování je zatím přes přímé model mutace (edit-model), NE přes plný history/operations + undo/redo. Undo/redo + transakce na novém povrchu = pozdější milník (R.4.6 nebo samostatný). Word-level delete (Ctrl+Backspace) zatím single-char.

### R.4.3 App-kreslený kurzor + selection overlay [core — caret] ✅ (2026-05-29, entry version 205, 278 Node + 4 E2E)
- [x] `core-engine/hit-test.mjs` — pure, z layout `caretStops` (přesný offset→x mapping → bez interpolace): `hitTestPoint(layout,x,y)` (nejbližší řádek dle y, pak nejbližší caret stop dle x), `caretStopAt`, `lineCaretStops`, `collectCaretStops`. Layout (document) souřadnice; caller převádí client→layout.
- [x] `core-engine/caret.mjs` — pure `moveCaretByKey` (ArrowLeft/Right přes hranice bloků, Home/End na řádku, Up/Down na nejbližší x v sousedním řádku), `caretRect`, `blockMaxOffset` + DOM `createCaretElement` (blikající caret div, CSS keyframes injektované do head, page-local positioning).
- [x] `core-engine/selection-overlay.mjs` — pure `selectionRectsForRange(layout, anchor, focus)` (per-řádek obdélník od leftmost po rightmost vybraný caret stop; collapsed → []), DOM `createSelectionRectElement`.
- [x] render-host wiring: `paintOverlays()` po renderu kreslí caret + selection rects page-local do správné page sekce; `moveCaret(key, shiftKey)` (repaint bez re-layoutu); `placeCaretFromClient` (client→layout přes section.getBoundingClientRect → hitTest). **mousedown + preventDefault** (udrží fokus na off-screen surface — reálná chyba odhalená E2E: plain mousedown na nefokusovatelném textu přesune fokus na body).
- [x] Node test `PhaseR3_CaretGeometryHitTestAndSelection` (hit-test, caret move přes hranice bloků, selection rects, host API). Namespace `coreEngine.hitTest/caret/selection`.
- [x] **Playwright brána ✅** (`R43_Caret_ClickPlacesCaret_ArrowsMove_ShiftSelects`): reálný klik za „Hello" → caret offset 5, ArrowRight → 6, Shift+ArrowRight → 1 selection rect + viditelný caret element.
- **POZN**: drag-výběr myší a double-click=slovo zatím NE (mousedown jen umisťuje caret; mousemove+up drag a word-select = R.4.6/dořešit). PageUp/PageDown taky zatím ne (jen Up/Down po řádcích).

### R.4.4 IME kompozice [core — i18n input] ✅ (2026-05-29, entry version 206, 279 Node + 5 E2E)
- [x] composition flow: `compositionstart` → preview text žije v modelu jako běžný text v rozsahu `[start, start+text.length)` (teče reálným layoutem → wrapping/caret/řádková výška zadarmo); `compositionupdate` → **nahradí** předchozí preview span (ne akumuluje), překreslí; `compositionend` → preview se zamění za finální string jako jeden edit. **Bez history/undo** (nový povrch zatím routuje přímou model mutací — undo je R.4.6).
- [x] `core-engine/edit-model.mjs` → `applyReplaceRange(model, blockId, start, end, text, deps)` (delete span + insert, vždy non-structural — kompozice nepřekračuje hranici bloku; ochrana proti prázdným runs).
- [x] `core-engine/selection-overlay.mjs` → `createCompositionUnderlineElement` (tenká pre-edit linka u dolního okraje řádku; per-řádek rects znovupoužité ze `selectionRectsForRange`).
- [x] render-host: stav `composition = { blockId, start, text }`; `compositionStart/Update/End` orchestrace; `paintOverlays` kreslí pre-edit underline když `composition.text`; gettery `isComposing()` + `getComposition()`. compositionEnd vyčistí `composition` PŘED renderem (underline se nepřekreslí). Prázdná data na end = zrušená kompozice (Escape) → preview se smaže.
- [x] **Apple/quirk ošetření** v `input-surface.mjs`: keydown guard `composing || e.isComposing || e.keyCode===229 || e.which===229` (IME „process" keydown před compositionstart se neroutuje jako control klávesa); `onInput` ignoruje `*[Cc]omposition*` inputType; render-host `compositionUpdate/End` lazy-startne kompozici když přijde update/end bez startu.
- [x] Node test `PhaseR4_ImeCompositionPreviewAndCommit` (replaceRange, plný compose přes off-screen surface, zrušená kompozice, lazy-start + keyCode 229). **POZN: Node DOM stub musel rozbalovat DocumentFragment při append/insert (jako reálný DOM) — jinak page sekce skončí vnořené pod FRAG a `querySelector` je nenajde.**
- [x] **Playwright brána ✅** (`R44_ImeComposition_PreviewUnderlines_CommitsFinalText`): reálné `CompositionEvent`y na off-screen textarea v živém prohlížeči → renderovaný DOM `Hi`→`Hiか`→`Hiかん`→`Hi感` (replace, ne akumulace), pre-edit underline viditelný během kompozice, commit `感じ` → DOM `Hi感じ`, caret 4, underline pryč, contenteditable=0.
- **POZN**: reálný hardwarový IME = manuální test (gate je programmatic events, jak plán předepisuje). CDP `Input.imeSetComposition` zvážen, ale off-screen (-9999px/opacity:0) textarea + headless IME je nespolehlivé → programmatic CompositionEvents jsou deterministická a plánem předepsaná brána.

### R.4.5 Text shaping: bidi + grapheme + combining [core — i18n layout] ✅ (2026-05-29, entry version 207, 280 Node + 6 E2E)
- [x] `layout/bidi.mjs` — clean-room UBA dle UAX #9: `bidiClass(cp)` (kompaktní range tabulka z Unicode bloků), `resolveLevels(text,dir?)` (P2/P3 base + W1–W7 + N1/N2 + I1/I2, per code-unit s surrogate handling), `reorderVisual(levels)` (L2 reverse-runs), `baseDirection`, `hasRtl`. **Rozsah: jedna isolating run sequence/odstavec; explicitní formátovací znaky (LRE/RLE/isolates) = removed (BN) — follow-up.**
- [x] `layout/grapheme.mjs` — `Intl.Segmenter('grapheme')` + surrogate/combining/ZWJ fallback: `graphemeBoundaries`, `nextGraphemeBoundary`, `prevGraphemeBoundary`, `isGraphemeBoundary`, `graphemeCount`. Caret se pohybuje po grafémech.
- [x] Integrace grapheme do `core-engine/caret.mjs` `moveCaretByKey(layout,pos,key,opts)` — když `opts.text`, ArrowLeft/Right krok po grafému (emoji/diakritika), jinak fallback ±1 code-unit. render-host `moveCaret` předává `blockText`.
- [x] `core-engine/bidi-line.mjs` `applyBidiToLayout(layout)` — post-layout pass: rekonstruuje text řádku ze segmentů, resolveLevels, **reorder+repack segmentů ve vizuálním pořadí** (od logical-left), tag `direction:'rtl'`, **přepočet caret-stop x z nové vizuální segment-boxu (mirror pro RTL)**. Pure-LTR řádky netknuté (fast path). render-host volá po `engine.layoutDocument`. Atomic renderer ctí `segment.direction` → `dir=rtl` + `unicode-bidi:isolate` (prohlížeč shapuje arabské joining + glyph pořadí v boxu; my vlastníme line/segment layout).
- [x] Node test `PhaseR5_BidiAndGraphemeShapingAndCaret` (bidi klasifikace/levels/L2 vč. Hebrew+čísla, grapheme emoji/combining, caret grapheme pohyb, integrace přes render-host: RTL dir tag + mirror caret x, LTR netknuté).
- [x] **Playwright brána ✅** (`R45_Bidi_RtlRendersVisually_AndGraphemeCaretSkipsEmoji`): reálné font-metriky — Hebrew+Arabic segmenty `dir=rtl`, caret x(offset 0) > x(konec) (mirror), `baseDirection` rtl, combining `nextGraphemeBoundary('éfg',0)===2`; **reálná klávesnice ArrowRight nad `a👍b`: 0→1→3** (přeskočí 2-unit emoji, ne offset 2), ArrowLeft 3→1.
- **POZN**: arabské joining/ligatury → per-char caret x v rámci segmentu je přibližný (cell width z LTR měření, ne ze shaped advances); přesná geometrie uvnitř arabského slova = follow-up (měření shaped substringů). Hebrejština (bez joining) je přesná. Vizuální pořadí + segment-hranice caret jsou správné. RTL paragraph-alignment (zarovnání k pravému okraji stránky) zatím ne (jen vizuální pořadí v rámci textového bloku).

### R.4.6 Parita featur na novém povrchu — postupně, každá s Playwright bránou
> Pořadí podle závislostí; každá položka = vlastní E2E test.
> **PERF ✅ R.4.6i-2 layout cache** (2026-05-29, entry version 218, 291 Node + 17 E2E): render-host memoizuje (post-bidi) layout celého dokumentu klíčem `model.version|pageSettings|layoutOptions`; `computeLayout()` spustí `engine.layoutDocument`+bidi jen na cache-miss. Scroll/viewport/selection re-rendery (beze změny modelu) layout znovupoužijí → `engine.layoutDocument` se NEspouští. Invalidace: každý edit (version++), setModel, undo/redo (model swap). `getLayoutComputeCount()` telemetrie. Node `PhaseR6i2` (1 compute pro 6 renderů, edit→2, undo→3). **Playwright `R46i2`: 150 odstavců → cold full layout 870ms, ale 5 scroll re-renderů průměr 131ms (1 layout compute pro 6 renderů) = ~6.6× rychlejší scroll.** POZN: stále COLD full-doc layout při 1. paintu/po editu — pravá inkrementální per-block re-layout (změnit jen 1 blok) = pozdější (vyžaduje hlubší zásah do paragraph-engine).
- [x] **inline marks + paragraph formatting ✅** (2026-05-29, entry version 208, 281 Node + 7 E2E) — `core-engine/edit-format.mjs`: `applyMarkToBlockRange` (toggle bool marks bold/italic/underline/strike; set-by-type value marks textcolor/highlight/fontfamily/fontsize), `blockRangeHasMark`, `setParagraphProperty` (alignment/spacing/indent…). render-host `toggleMark/applyMark/isMarkActive/setAlignment/setParagraphProperty` + `orderedSelectionBlocks` (multi-block selection→ordered per-block ranges, group toggle). `mergeTextStyle` rozšířen o underline/strikethrough→`text-decoration`. Marks mění jen run styling (ne text) → caret/anchor offsety zůstávají platné. Node `PhaseR6a`; **Playwright `R46a`**: reálná klávesnice Shift+ArrowRight×5 vybere „Hello", `toggleMark('bold')` → computed `font-weight:700` na „Hello" segmentu (zbytek 400), toggle off→400, `setAlignment('center')`. **POZN: collapsed selekce + marks = no-op (pending-format pro další znak = pozdější); lists ještě ne.**
- [x] **nadpisy + styly + outline ✅** (2026-05-29, entry version 211, 285 Node + 10 E2E) — `core-engine/paragraph-styles.mjs`: `DEFAULT_PARAGRAPH_STYLES` (Normal/Title/Heading1–6 → base style fontSize+fontWeight + outlineLevel), `applyParagraphStyle(block,name)` (nastaví `content.styleName` + base `content.style` + `headingLevel`; engine už čte `content.style` jako base → nadpis se vykreslí větší), `getDocumentOutline(model)` (heading bloky → [{blockId,level,text,styleName}]), `paragraphStyleName`. render-host: `setParagraphStyle/getParagraphStyle/getOutline` přes `selectionParagraphs()` (sdílené s alignment), undoable. Node `PhaseR6b`; **Playwright `R46b`**: setParagraphStyle('Heading1') → computed font-size 16→~32px, outline=1 (text+level), reálný Ctrl+Z → zpět 16px/Normal/outline prázdný. **POZN: TOC generování (vložit jako blok) + outline-level navigace v UI = pozdější; named styles jsou inline base-style (ne plný style-inheritance registry).**
- [x] **tabulky (vložení, řádky/sloupce, psaní v buňkách, render) ✅** (2026-05-29, entry version 212, 285 Node + 11 E2E) — engine z fáze D už lame tabulky (cell paragraphy → segments+caretStops tagované tableId/cellId). **OPRAVENA REÁLNÁ LATENTNÍ CHYBA: `paragraph-engine` volal `scopedMetadataDecorator.decorate(...)` ale `createScopedLayoutMetadataDecorator` vrací decorate funkci PŘÍMO → `.decorate is not a function` crashlo VŠECHNY tabulky + scoped/multi-page paths. Fix: volat `scopedMetadataDecorator(layout, ctx)`.** Atomic renderer: `localizeLayoutBlock` rozšířen o posun `cells`/`rows` rektů; nový `renderTableScope` (cell border divy + cell text segmenty přes `renderSegment`); routing `type==='table'` → renderTableScope (text layer). `core-engine/edit-table.mjs`: `createTableModel(r,c)`, `firstCellParagraphId`, `insertTableAfterBlock`, `addTableRow/Column` (unikátní cell id), `findTableContaining`. render-host: `insertTable({rows,cols})` (caret do první buňky), `addTableRow/addTableColumn` (na tabulce u caretu), `getTableInfo`. **Caret + psaní v buňkách funguje bez speciální cesty** (caret stopy buněk jsou v layout.blocks[].caretStops → collectCaretStops; edit-model findBlock/findBlockContainer už indexuje nested cell paragraphy). Undoable. Node `PhaseR6c`; **Playwright `R46c`**: insertTable 2×2 → 1 `.tm-render-table` + 4 `.tm-render-table-cell`, caret v r0c0, reálná klávesnice „Cell" → text v buňce, addRow/Column → 3×3, undo → 2×2. **POZN: merge/split buněk + výběr přes buňky + Tab-navigace mezi buňkami + resize sloupců zatím NE (pozdější); cell borders jednoduché 1px.**
- [x] **obrázky plovoucí s obtékáním + resize/move handles ✅** (2026-05-29, entry version 209, 282 Node + 8 E2E) — engine z fáze D už lame anchored drawings + text exclusions (ověřeno: text wrapuje za 160px obrázkem). NOVÉ na novém povrchu: `core-engine/object-overlay.mjs` (`createObjectElement` = positioned `<figure>` s reálným `<img>` + 8 resize handles když selected; `objectHitTest`; `resizeRectByHandle`). render-host: `paintObjects()` (kreslí `layout.objects` page-local jako overlay, NE přes atomic renderer → bez regrese), `insertImage({url,width,height,wrapMode,alt})` (drawing run přes `insertDrawingRunAtTextOffset`), `selectObject/clearObjectSelection`, `resizeSelectedObject`, `moveSelectedObject` (position offset); pointer wiring: klik na handle→resize drag, klik na obrázek→select+move drag, klik na text→deselect+caret (doc-level mousemove/up). Node `PhaseR6d`; **Playwright `R46d`**: insert→figure+img render + text wrapuje (firstSegX≥160); **reálný klik** vybere→8 handles; **reálný drag SE handle** → obrázek se zvětší + text se přewrapuje dál vpravo. **POZN: inline obrázky (wrapMode inline) zatím netestovány na novém povrchu; obrázek se kreslí jako overlay (ne v atomic rendereru) — sjednotit při cutoveru; rotation handle/aspect-lock UI zatím ne.**
- [x] **hlavičky/patičky + pole (číslo stránky / počet stran) ✅** (2026-05-29, entry version 217, 290 Node + 16 E2E) — engine z fáze D už lame `model.headers`/`model.footers` na KAŽDÉ stránce a **resolvuje field runy per-page** (`cloneBlockWithResolvedFields(block, pageNumber, totalPages)` → `resolveFieldRunText` pro `fieldType: pageNumber|pageCount`), renderer kreslí region per page (`renderHeaderFooterRegion`, řádky 588-589). NOVÉ: `core-engine/header-footer.mjs` (`textRun/pageNumberField/pageCountField` builders, `normalizeRegionRuns` string|run[], `setRegion`, `clearRegion`). render-host `setHeader/setFooter` (string nebo run[]), `clearHeader/clearFooter`, undoable. Node `PhaseR6e`; **Playwright `R46e`**: 60 odstavců → multi-page, `setFooter(['Page ', pageNumberField, ' of ', pageCountField])` → DOM footer „Page 1 of N" na str. 0, „Page 2 of N" na str. 1; header „Confidential". **POZN: pole datum/čas + různá hlavička první/sudé/liché stránky + editace hlaviček klikem zatím NE (jen set přes API).**
- [x] **revize (track changes) — insert/delete + accept/reject ✅** (2026-05-29, entry version 215, 288 Node + 14 E2E) — self-contained na novém povrchu (Phase-D revision-engine je operation-log/těžký): `core-engine/track-changes.mjs` — tracked insert nese `insertion` mark, tracked delete označí text `deletion` markem (NEodstraní → zůstane viditelný, přeškrtnutý). `acceptAllRevisions` (insertion→nech text+smaž mark; deletion→smaž text), `rejectAllRevisions` (zrcadlově), `listRevisions`, `hasRevisions`. render-host `setTrackChanges/isTrackChanges` + intents větví (insertText→insertion attrs přes `applyInsertText(...,attrs)`; Backspace/Delete→`trackedDeleteBackward/Forward` označí deletion+posune caret); `acceptAllRevisions/rejectAllRevisions/getRevisions/hasRevisions`, undoable. mergeTextStyle+decorationsFromMarks: insertion→underline+zelená `#1b7f3b`, deletion→line-through+červená `#c0392b`. **OPRAVENA 4. REÁLNÁ CHYBA: `applySegmentStyleToElement` nikdy NEČISTILO color/textDecoration na REUSED span (jen nastavovalo když přítomné) → stará barva/podtržení zůstaly po odebrání marku (unbold/unlink/accept). Fix: vždy přiřadit s '' resetem.** Node `PhaseR6f`; **Playwright `R46f`**: track on, reálná klávesnice „ World" → underline+`rgb(27,127,59)` + revize, acceptAll → čistý „Hello World" bez podtržení. **POZN: cross-block tracked delete (Backspace na začátku odstavce) + format-revize + per-revize accept/reject (jen accept/reject ALL) + review-módy (show original/final) zatím NE.**
- [x] **komentáře — kotvy + zvýraznění + resolve/remove ✅** (2026-05-29, entry version 216, 289 Node + 15 E2E) — `core-engine/comments.mjs`: kotva = `comment` mark `{type:'comment', value: commentId}` na rozsahu (ADDITIVNÍ → **overlapping komentáře koexistují** na stejném runu), metadata v `model.comments` ({id,author,text,resolved,createdAt}). `addCommentMarkToRange` (reuse exportovaného `transformRunsInRange` z edit-format), `stripCommentMark`, `commentAnchorText`, `commentIdsInRange`, `collectCommentIds`. render-host `addComment(text,author)` (na selekci), `getComments` (+ anchorText), `resolveComment` (strihne kotvy → highlight zmizí, záznam zůstane resolved), `removeComment`, `getCommentIdsAtCaret`, undoable. mergeTextStyle comment → backgroundColor `#fff3a3`. Node `PhaseR6g` (vč. overlap c1/c2); **Playwright `R46g`**: Shift×6 vybere „Review", addComment(Alex) → computed background + anchorText „Review", resolveComment → background pryč (`rgba(0,0,0,0)`). **POZN: vlákna (reply na komentář) + komentářová postranní lišta UI + per-comment navigace „další/předchozí" zatím NE (jen data+highlight+resolve/remove); marginálie panel je UI vrstva pozdější.**
- [x] **hyperlinky ✅** (2026-05-29, entry version 213, 286 Node + 12 E2E) — link = value mark `{type:'link', value:href}` na rozsahu (reuse mark systému z R.4.6a). `edit-format`: value-mark removal strihá podle TYPU (ne přesné hodnoty); `firstMarkValueInRange` (href pod caretem/v selekci). `mergeTextStyle` link → underline + Word-blue `#0563c1`. **Atomic renderer: segment teď nese `marks` (paragraph-engine segment enrichment += `marks`) → renderSegment přidá `data-href` + `role=link` + cursor:pointer.** `decorationsFromMarks` rozpoznává link → underline (applySegmentStyleToElement aplikuje textDecoration jen z `decorations` pole, ne ze style.textDecoration!). render-host `applyLink(href)/removeLink()/getLinkHref()`. Undoable. Node `PhaseR6h`; **Playwright `R46h`**: Shift+ArrowRight×4 vybere „site", applyLink → `data-href`+`role=link`+computed underline+`rgb(5,99,193)`, Ctrl+Z odebere. **POZN: bookmarks + klik-otevře-odkaz (Ctrl/Cmd+klik → window.open) zatím NE; bookmarks = R.4.6h-2.**
- [x] **find/replace ✅** (2026-05-29, entry version 214, 287 Node + 13 E2E) — `core-engine/find-replace.mjs` `findMatches(model, query, {caseSensitive, wholeWord})` (prohledá body paragraphy + table cells, vrací logické {blockId,start,end} rozsahy; default case-insensitive). render-host: `find(q,opts)` (sesbírá matches, vybere první), `findNext/findPrev` (wrap + vybere), `replaceCurrent`, `replaceAll` (R→L v rámci bloku → offsety platné, JEDEN undo krok), `clearFind`, `getFindState`. `paintOverlays` kreslí highlighty (`createFindHighlightElement` žlutá/aktivní oranžová). Node `PhaseR6fr`; **Playwright `R46fr`**: find „alpha" → 3 highlighty (1 current `core-engine-find-current`), findNext→idx1, replaceAll→„X beta X gamma X" v DOM, reálný Ctrl+Z → 1 krok zpět. **POZN: nativní prohlížečové Ctrl+F funguje taky (reálný DOM text); replace s regex/back-refs zatím ne.**
- [ ] kontrola pravopisu (per-paragraph deferred — viz fáze G v perf todo)
- [x] **undo/redo na novém povrchu ✅** (2026-05-29, entry version 210, 283 Node + 9 E2E) — `core-engine/undo-stack.mjs` `createUndoStack({clone,limit})`: **snapshot-based** (celý model klonován před editem → správné pro VŠECHNY typy editů: text/marks/odstavce/obrázky bez per-typ inverzí). **Coalescing** přes `coalesceKey`: typing run / delete run / jeden resize-drag = jeden undo krok; caret-move + `breakCoalescing()` (konec drag) ukončí run; redo se zruší novým editem; limit cap. render-host: `recordHistory(key)` před každou mutací, `undo/redo/canUndo/canRedo/getHistoryDepth`, `restoreState` (model swap + indexes=null + caret/selection restore + render). input-surface: **Ctrl/Cmd+Z = undo, Ctrl/Cmd+Shift+Z / Ctrl+Y = redo**. Node `PhaseR6i`; **Playwright `R46i`**: reálná klávesnice „Hello" (1 undo krok) → Ctrl+Z → '' → Ctrl+Y → „Hello"; bold → Ctrl+Z → unbold. **POZN: snapshot undo = jednoduché+korektní; operation-log (paměťově úspornější, fáze D `history/`) je pozdější optimalizace. setModel/destroy mažou historii.**

### R.4.7 Přístupnost [core — a11y] ✅ (2026-05-29, entry version 219, 292 Node + 18 E2E)
- [x] `core-engine/a11y.mjs`: `applyEditorAria(root,{label})` (root `role=document` + `aria-label` + `aria-roledescription='rich text editor'`), `headingAriaForBlock`, `describeCaretContext(model,caret,deps)` (mluvený popis: „Heading level N, <text>" / text odstavce), `createLiveRegion(doc)` (off-screen `role=status aria-live=polite aria-atomic`, dedup stejného textu).
- [x] **Input surface = přístupný textbox** (NE aria-hidden): `role=textbox` + `aria-multiline=true` + `aria-label` + `tabindex=0` (skrytý capture je accessible input; positioned-DOM nese obsah).
- [x] **Heading semantika v rendereru**: `renderParagraphScope` čte model block `headingLevel` → `role=heading` + `aria-level=N` (screen reader vidí strukturu; reálný text čte rovnou z DOM).
- [x] render-host: `applyEditorAria` na mount, live region mountován mimo root (přežije replaceChildren), `announceCaret()` na moveCaret/placeCaret/commitEdit/setSelection → ohlásí kontext kurzoru. Gettery `getLiveRegionElement/getLiveRegionText`.
- [x] Node `PhaseR7` (pure a11y + integrace: root role, textbox role, live region announce + update na změně bloku). **Playwright `R47`**: root `role=document` + `aria-label`, heading `role=heading aria-level=1` + reálný text „Chapter Title", textbox `role=textbox aria-multiline` (ne aria-hidden), live region `aria-live=polite` text „Heading level 1, Chapter Title", reálná klávesnice ArrowDown → live region „Body paragraph text.".
- **Brána**: automatizovaná část (ARIA atributy / accessibility tree) ✅ Playwright; **manuální NVDA/VoiceOver = lidský follow-up** (zbývá ověřit reálné předčítání). **POZN: vysoký kontrast (forced-colors caret/selection) + per-znak/slovo ohlašování (teď ohlašuje kontext odstavce) + reading-order pro bidi vizuální pořadí = pozdější.**

### R.4.8 Cutover [nahrazuje 4.2.5/4.2.6/4.2.7] — 🔶 ČÁSTEČNĚ (feature-flag seam HOTOVÝ; flip default + smazání legacy ZABLOKOVÁNO na interop bridge)
> **Plán + stav: `planning/r48-cutover-plan.md`.** (2026-05-29, entry version 219)
- [x] **Feature-flag seam ✅**: `DocumentEditorRenderEngine { Legacy, CoreEnginePreview }` (Abstractions) + `TmDocumentEditor.RenderEngine` parametr (default `Legacy`). **Fail-safe** `DocumentEditorRenderEngineFlag.Resolve(requested, hostedInteropReady)` → `CoreEnginePreview` spadne na `Legacy` dokud `CoreEngineHostedInteropReady==false` (teď false) → požadavek na preview NIKDY needitor nerozbije. Surfacováno jako `data-render-engine` / `data-render-engine-requested` na root. C# unit testy `DocumentEditorRenderEngineFlagTests` (3) zelené; build 0 chyb.
- [ ] **BLOKER: core-engine hosted bridge** — `TmDocumentCoreEngineHost` (C#↔JS most: serializace DocumentEditorDocument↔JS model, mount createRenderHost+attachInput, save/snapshot, undo/redo+dirty, toolbar command dispatch, selection/focus). Nový engine NEMÁ žádnou C# interop (ověřeno) → nemůže zatím nahradit legacy v hostované komponentě.
- [ ] flip `CoreEngineHostedInteropReady=true` + render core host když `EffectiveRenderEngine==CoreEnginePreview`.
- [ ] regress: celá E2E sada zelená na novém enginu (s flagem on); perf ≥ legacy na 30/100/500p.
- [ ] flip default → core; soak. **← BLOKOVÁNO na R.5 P0+P1 (viz níže).**
- [ ] **smazat legacy** (`render(inst)` string path + contenteditable body + `buildLayoutSnapshot` DOM-readback) — **NEVRATNÉ, až po zelené regresi + explicitním schválení.**
- **AKTUALIZACE 2026-05-30 (kompletní backlog parity)** → `planning/r5-core-engine-parity-and-cutover-backlog.md` (**fáze R.5**). Audit VŠEHO nehotového v novém enginu. **NALEZEN DATA-LOSS BLOKER:** `CoreEngineModelConverter` round-tripuje jen paragraphy/nadpisy/text/marks — **tabulky/obrázky/page-breaky se při save ZTRATÍ** (`CoreEngineModelConverter.cs:16-17,42`). Plus chybí rich clipboard, autosave přes core, klik-otevře-odkaz, bookmarks (P0); drag-výběr/PageUp-Dn/pending-format/tabulkové operace/inline-resize (P1); hloubka track-changes/komentářů/hlaviček/find-regex/TOC/bidi (P2); first-paint virtualizace/operation-log/a11y (P3); export-import/collab/context-menu/zoom/print/page-settings UI (P4). **Brána flipu: P0+P1 KOMPLET + regrese + manuální .docx smoke + explicitní schválení.**
- **Brána**: full Playwright suite + perf baseline ≥ legacy na 30/100/500p (na novém enginu, až bude bridge).
- **POZN**: standalone nový engine je feature-complete + ověřený (R.4.0–R.4.7, 292 Node + 18 core-engine E2E). Cutover čeká na bridge; legacy se NESMÍ smazat dřív (rozbilo by to editor).
- **AKTUALIZACE 2026-05-30**: bridge HOTOVÝ + 15 bridge E2E (R49–R63: routing/lists/comments/images/upload/inspector/headings/asset/find/align/z-order). **flip POŘÁD ZABLOKOVÁN — nově na PERF (viz R.4.9), ne na bridge.**
- **AKTUALIZACE 2026-05-30 (inspector follow-upy HOTOVÉ)**: drobné inspektor follow-upy **caption + position + inline-resize** dokončeny. caption/position: render-host `setSelectedObjectCaption` (→ `<figcaption data-testid=core-engine-object-caption>` přes object-overlay) + `setSelectedObjectPosition` (hp/vp.offset, align=null) → facade `setObjectCaption`/`setObjectPosition` → interop → host `SetObjectCaptionAsync`/`SetObjectPositionAsync` (+ DTO `Caption`/`X`/`Y`) → editor `SetActiveImageCaptionFromPanelAsync`/`ToggleActiveImageCaptionFromPanelAsync`/`SetActiveImagePositionFromPanelAsync` `UsingCoreEngine` větve + `_coreActiveImage` nese Caption/Position; razor inspector wire `CaptionChanged`/`ToggleCaption`/`PositionChanged`. **Bridge E2E `R74` (full inspector→C#→engine path): caption pole → `<figcaption>` 'Figure A'; Square + position-x 180 → plovoucí figure se posune doprava.** inline-resize ověřeno `R61`/`R73` (engine rect.width 240→360). entry version 236, **bridge E2E 16/16 (R49–R63+R74)**, 22 PhaseR Node zelené.

### R.4.9 Inkrementální render-on-edit [core — typing perf, **CUTOVER BLOCKER #1**] — 🔴 KRITICKÉ, NEZAČATO
> **Proč:** 3c (2026-05-30, testy `R64`/`R65` v `CoreEngineRenderHostE2ETests`) změřilo, že core engine **NENÍ na typing-paritě** — je dramaticky horší než legacy. Flipnout teď = udělat psaní HORŠÍ, přesný opak userova #1 cíle. Toto je vlastní hodnota přepisu.
>
> **Naměřeno (pravdivá single-keystroke main-thread latence, R65, bez Playwright round-tripu):**
> | odstavce | CORE mean | CORE p95 | legacy |
> |---|---|---|---|
> | 30 | 92 ms | 131 ms | ~0 ms |
> | 100 | 250 ms | 341 ms | ~0 ms |
> | 500 | **1428 ms** | 1657 ms | ~0 ms |
>
> **Proč legacy ~0:** používá nativní `contenteditable` (browser vloží znak + vykreslí sám). Core je app-drawn → KAŽDÝ keystroke dělá `render()` → `engine.layoutDocument` přes CELÝ dokument + překreslí DOM = O(N).
>
> **CÍL:** per-keystroke main-thread práce **<16 ms (1 frame), ideálně ~8 ms, A PLOCHO** vůči velikosti dokumentu (O(1), ne O(N)). To je „Word/GDocs pocit".
>
> **KLÍČOVÝ INSIGHT:** atomic renderer UŽ diffuje DOM (B1 fingerprint skip + B2 per-segment diff) → **DOM-patch strana je z velké části hotová**; bottleneck je **LAYOUT strana** (`engine.layoutDocument` přepočítává celý dokument). Hlavní práce R.4.9 = inkrementální LAYOUT + patch layout cache, NE přepis rendereru.

- [x] **R.4.9.1 Profiling probe** ✅ (2026-05-30, entry version 228, test `R66_PerfProfile_PerKeystrokeBreakdown`): render-host má `getLastRenderTimings()` (rozpad `render()`: computeLayout→{layoutDocument,bidi,list} + viewLayout + snapshot + renderer + overlays). **VÝSLEDEK = JASNÝ CÍL:**
  | | total | **layoutDocument** | snapshot | renderer | bidi/list/overlays |
  |---|---|---|---|---|---|
  | 30p | 249ms | **201ms (81%)** | 36ms (14%) | 8ms | ~4ms |
  | 100p | 446ms | **369ms (83%)** | 58ms (13%) | 16ms | ~3ms |
  | 500p | 2647ms | **2114ms (80%)** | 440ms (17%) | 73ms | ~21ms |
  **`engine.layoutDocument` = ~80% per-keystroke + O(N) = HLAVNÍ CÍL** (→ R.4.9.3 relayoutBlock). **`createRenderSnapshot` = #2 ~15% + taky O(N)** → musí být taky inkrementální (NOVÝ úkol R.4.9.3b). `renderer` (DOM patch) = ~3% — UŽ inkrementální (B1/B2 diff funguje, nepřepisovat). bidi/list/overlays = zanedbatelné.
- [x] **R.4.9.2 Dirty-block tracking** ✅ (2026-05-30, entry version 229, Node test `PhaseR10_EditModelReportsDirtyBlockIdsForIncrementalRender`): `edit-model.mjs` všechny funkce vrací `dirtyBlockIds` (+ `removedBlockIds`/`insertedBlockId` u structural). **Common fast path = non-structural → přesně 1 blok** (insertText/in-block delete/replaceRange → `[block.id]`); structural: merge → `dirty [survivor], removed [gone]`; split → `dirty [orig, new]`. render-host `commitEdit` ukládá `lastEditDirty = { blockIds, removedBlockIds, insertedBlockId, structural }`, exposed `getLastEditDirty()`. (Zatím pořád volá full `render()` — incremental cesta = R.4.9.6.) 22 PhaseR Node zelených.
- [x] **R.4.9.3 Per-block inkrementální layout** ✅ (2026-05-30, entry version 230, testy `R67`/`R68`): **frame reconstruction OVĚŘENA** (`R67`: `engine.layoutParagraph(block, {x,y,width z cached.rect})` reprodukuje cached block-layout EXAKTNĚ — segmenty text/x/width/height match). render-host `relayoutDirtyBlock(blockId)`: najde cached block-layout, re-layout přes `engine.layoutParagraph` + scoped bidi/list pass na `{blocks:[fresh]}`, swap do `lastLayout.blocks`, rebuild global caretStops aggregate, update layoutCache. **v1 fast path = Δheight===0** (typing bez zalomení řádku); fallback (`getLastIncrementalBail()` diag) na: multi-page blok (>1 fragment se stejným blockId — POZOR `fragmentIndex=0` je normální!), blok s floating objektem, height-delta≠0 (→ R.4.9.4), structural. **PERF WIN ~4×: 30p 92→26ms, 100p 250→57ms, 500p 1428→328ms.** `R68` golden: inkrementální layout BYTE-IDENTICKÝ s full render téhož modelu (usedIncremental=true, match=true; Enter→fallback). **POZOR: pořád O(N) — zbývá render strana** (snapshot+viewLayout+renderer iterace) → R.4.9.3b. 22 PhaseR Node (vč. upraveného `PhaseR6i2` — edit už není full recompute) + 15 bridge E2E (reálné psaní) zelené.
- [x] **R.4.9.3b Inkrementální snapshot** ✅ (2026-05-30, entry version 232, profil `R69`): `createRenderSnapshot` má `opts.cheap` cestu = přeskočí `flattenLayoutSegments` + `stableChecksum`. **KLÍČOVÉ ZJIŠTĚNÍ: skutečný bottleneck byl `sortObject(snapshot)` — REKURZIVNÍ deep-clone CELÉHO layoutu+modelu (O(N) na keystroke)!** Cheap cesta vrací PLAIN by-reference objekt (žádný sortObject; renderer čte layout/model/selection read-only). renderIncremental předává `{cheap:true, dirtyBlockId}`. **VÝSLEDEK = OBROVSKÝ: per-keystroke main-thread latence 30p 92→3ms (30×), 100p 250→11ms (23×), 500p 1428→45ms (32×). 30p/100p teď POD 16ms = Word/GDocs úroveň ✓.** golden (R68) pořád byte-identický; 22 PhaseR Node + 15 bridge E2E zelené. **ZBÝVÁ jen renderer:** R69 breakdown ukazuje, že po opravě snapshotu je jediný zbylý O(N) cost `renderer.render` (renderSnapshotFragment + replaceChildren = 2ms@30p, 5ms@100p, 32ms@500p). 30p/100p OK; 500p (45ms) chce in-place block patch (renderer fast-path = R.4.9.3b-2) místo rebuildu celého fragmentu.
- [x] **R.4.9.4 Y-reflow downstream** ✅ (2026-05-30, entry version 234, golden `R70`): keystroke co změní výšku bloku (zalomení/rozlomení řádku) UŽ NEpadá do full renderu — `relayoutDirtyBlock` posune Y **jen následujících bloků NA STEJNÉ STRÁNCE** o Δ (`shiftBlockLayoutY` translací rect/lines/segments/caretStops/baselines; listMarker block-relativní = neposouvá), vrací seznam dotčených bloků → `patchBlocks` je repozicuje in-place. **Pagination guard (fallback na full render):** Δ>0 přeteče page bottom; Δ<0 na ne-poslední stránce (under-pull dalšího bloku); dirty blok sám přeteče. `R70`: type→p10 wrap 1→2 řádky, **wrapIncremental=true** (reflow, ne fallback), p11.y posun +18, **golden byte-identický** s full render. Perf po R.4.9.4 ještě plošší (R65: 30p 1.8ms / 100p 3.5ms / 500p 3.5ms — wrap keystroky už nedělají pomalý full-render fallback). 22 PhaseR + golden zelené.
- [x] **R.4.9.10 Cross-page repaginace** ✅ (2026-05-30, entry version 235, golden `R71`): keystroke co přetlačí obsah přes hranici stránky UŽ NEpadá do full renderu — `repaginateFrom(i, fresh)` přepočítá flow od dirty bloku dolů (reuse cached line struktury, jen y/page přepočet), greedy block-level paginace dle `lastLayout.pageMetrics` + `createPageLayout` pro nové stránky, trim prázdných trailing stránek, rebuild page.blockIds. **2-pass: pre-check (BEZ mutace — všechny následující bloky musí být simple single-page paragraphs co se vejdou na stránku; fragmentIndex>0=continuation→fallback) pak mutation pass (garantovaně doběhne, žádná partial korupce).** Renderer: blok co změnil stránku → patchBlocks vrátí not-attached → renderIncremental fallback na full `renderer.render` (cheap snapshot, UŽ repaginovaný layout → BEZ layoutDocument). `R71`: type 250ch do p0 (80p doc, 3 stránky)→tail page 0 přejde na page 1, **incremental=250 fallback=0** (VŠECHNY keystroky incremental vč. repaginace), **golden byte-identický** s full render. 22 PhaseR + 15 bridge E2E zelené.
- **🎯 R.4.9 INKREMENTÁLNÍ RENDER-ON-EDIT KOMPLET HOTOVÁ** (entry version 235): per-block layout + cheap snapshot + renderer in-place patch + Y-reflow + cross-page repaginace. **Každý keystroke (typing / in-line wrap / cross-page) je incremental + flat <16ms (30p 1.8 / 100p 3.5 / 500p 3.5ms; před: 92/250/1428ms).** Golden byte-identický (R68/R70/R71). **CUTOVER BLOCKER #1 (typing perf) ZCELA VYŘEŠEN.** — když se výška editovaného bloku změní o Δ, posunout `rect.y` všech NÁSLEDUJÍCÍCH block-layoutů + page sekcí o Δ (levné, čistá translace; u virtualizace jen viditelné). Když Δ=0 (typing bez zalomení řádku) → překreslí se JEN ten blok, žádný reflow.
- [x] **R.4.9.5 Patch layout cache (ne recompute)** ✅ (hotové jako součást R.4.9.3/4/10): `relayoutDirtyBlock` po inkrementální mutaci nastaví `layoutCache = { signature: layoutSignature(), layout: lastLayout }` → následující full render (scroll/viewport) reuse incrementálně patchnutý layout, ŽÁDNÝ full recompute. bidi+list pass běží JEN na dirty bloku (`{blocks:[fresh]}`). Ověřeno `PhaseR6i2` (edit nezvyšuje `getLayoutComputeCount`).
- [x] **R.4.9.6 render-host `renderIncremental(dirtyIds)`** ✅ (2026-05-30): `commitEdit` fast path — non-structural + dirty.length===1 → `renderIncremental(dirty[0])` (relayoutDirtyBlock + viewLayout + snapshot + renderer.render + overlays); jinak full `render()`. POZN: viewLayout+snapshot+renderer pořád O(N) (jdou přes všechny bloky) → to je důvod, proč 500p pořád 328ms; R.4.9.3b to dořeší.
- [x] **R.4.9.7 Fallback na plný render** ✅ (hotové + ověřené existujícími testy): `commitEdit` → full `render()` pro structural (split/merge: `result.structural || dirty.length!==1`). `relayoutDirtyBlock` bail (→ full render) pro: tabulky/non-paragraph, multi-page split blok, blok s floating objektem, `repaginateFrom`=false. Cross-page repagination = už inkrementální (R.4.9.10). **IME composition** → `applyReplaceRange` (non-structural, 1 blok) → JDE přes incremental cestu a je KOREKTNÍ (ověřeno `PhaseR4` IME test). undo/redo/setModel/object/wrap/align/list-toggle → vlastní host metody volají full `render()`, ne `commitEdit` → bezpečné. Typing v list/bidi bloku → incremental + golden korektní (`R72`). Ověřeno: 22 PhaseR Node (vč. R4 IME, R6i undo, R9 lists) + 15 bridge E2E.
- [x] **R.4.9.8 Correctness gate (golden)** ✅ (2026-05-30, entry version 235): inkrementální layout === full render téhož modelu (laySig porovnání rect/segmenty/marker/direction). Pokrytí: `R68` insert no-wrap, `R70` same-page wrap (výška roste), `R71` cross-page repaginace, `R72` **list blok (marker '2.' zachován) + bidi/RTL blok (direction zachován)**. split/merge/floating-image → fallback na full render (triviálně korektní). Všechny golden byte-identické.
- [x] **R.4.9.3b-2 Renderer in-place block patch** ✅ (2026-05-30, entry version 233): atomic-renderer `patchBlocks(root, snapshot, blockIds)` — updatne JEN dirty bloky in-place (`renderParagraphScope(localizeLayoutBlockToPage)` na už připojeném cached containeru), **BEZ rebuildu fragmentu / replaceChildren**; vrací `{ok:false}` když blok není vykreslený (virtualized) → fallback na full render. renderIncremental ho zkusí první. **renderer cost 32→0.4ms @ 500p.**
- [x] **R.4.9.9 Perf gate** ✅ (2026-05-30, `R65`): median keystroke **< 16 ms** (frame budget) na 30/100/500p. **DOSAŽENO + PLOCHÉ (O(1)): 30p 3.3ms, 100p 4.9ms, 500p 5.2ms** (před R.4.9: 92/250/1428ms → 28×/51×/275× zrychlení). **TYPING PERF PARITA HOTOVÁ — core engine teď píše na Word/GDocs úrovni, nezávisle na velikosti dokumentu.** golden (R68) byte-identický; 22 PhaseR Node + 15 bridge E2E (reálné psaní) zelené.
- [x] **R.4.9.10 Repagination** ✅ HOTOVÉ (viz výše — `repaginateFrom`, golden `R71`). Tento řádek byl původní placeholder; implementace je o pár řádků výš.
- **Brána**: R65 per-keystroke <16 ms + ploché @30/100/500p; golden DOM parita (inkrementální == plný); všech 15 bridge E2E (R49–R63) zelených; pak teprve odblokuje R.4.8 flip.
- **POZN**: ~~po R.4.9 přehodnotit i `inline-image vizuální resize` (3c follow-up)~~ ✅ HOTOVO — `R73` probe ověřil, že inline resize funguje na engine úrovni (rect.width 240→360); `R61` ověřuje plnou inspector cestu. Caption + position dořešeny zároveň (viz R.4.8 inspector follow-upy + bridge `R74`).
- **AKTUALIZACE 2026-05-30 (perf-parita doc + posouzení legacy regrese HOTOVÉ)** → `planning/r48-perf-parity-and-legacy-regression-assessment.md`. **Část A (perf):** R65 pravá keystroke main-thread cena p50 **30p 1.40ms / 100p 1.50ms / 500p 3.30ms** (p95 max 4.1ms), R66 rozpad `layoutDocument=0.0ms` (incremental obchází full layout), před R.4.9 92/250/1428ms → **66×/167×/433×, plochá O(1), pod 16ms = Word/GDocs parita ✅**. Otevřený jen first-paint cold full-layout (R64: 75/248/867ms) = load-time, NE typing blocker → follow-up virtualizovaný first-layout. **Část B (regrese):** **592 legacy `DocumentEditor*E2ETests`** je nepřenositelná architekturou (selektory `document-wysiwyg-host`×95, `data-block-id`×235, `[contenteditable]`×54 vs core positioned-DOM `document-core-engine-host`/`data-render-block-id`/žádný contenteditable). Náhradní síť = **45 core E2E (16 bridge R49–R74 + 29 render-host R40–R73) + 22 PhaseR Node + 768/3 bUnit** pokrývá VŠECHNY hlavní feature-oblasti (mapa v doc). 3 bUnit fail = pre-existing (PDF/export cast), NEsouvisí s R.4.8. **Flip není blokován pokrytím — blokován rozhodnutím o must-have mezerách (bookmarks/klik-odkaz/autosave) + explicitním schválením (smazání legacy NEVRATNÉ).**

## R.5 Vazba na fázi D (co dělat teď hned)
- **Dokončit fázi D extrakci** (sekce 4.3–4.6, 5–7, 11) má smysl JEN pro moduly, které nový engine znovupoužije: `core/`, `history/`, `layout/`, `objects/` pipelines, `runtime/`. Tyto pokračují.
- **Krok 4.2.5 SE RUŠÍ** (nahrazen R.4.8 cutoverem). Krok 4.2.6 (B1.5 perf test) se přesouvá do R.4.1 brány.
- **Extrakce render-string-HTML (4.3) a contenteditable input (4.4 handlery) je nyní NÍZKÁ priorita** — tyto se v novém jádře zahazují, extrahovat je má smysl jen pokud to zlevní paralelní běh legacy do cutoveru (spíš ne).
- **Příští konkrétní krok**: R.4.0 (font-metrics service) — je to foundation, čistě testovatelné, a okamžitě zlepší kvalitu layoutu i ve stávajícím atomic rendereru.

---

## 2. Pure / nízká námaha — ✅ HOTOVO (2026-05-29, entry version 192, 257 PhaseD testů)

### 2.1 Image-insert pipeline → `objects/image-insert.mjs` [pure] ✅
`normalizeImageInsertPayload`/`firstDrawingRunFromSourceBlock` byly už hotové (factory v insert-image-payload). Nově extrahováno do `objects/image-insert.mjs` (přímé importy, ne factory):
- [x] `splitInlineListForDrawingInsert`
- [x] `readImageInsertDimension`
- [x] `createInlineDrawingLayoutForInsert`
- [x] `createDrawingRunFromImageInsert` (bere `normalizeImageInsertPayload` jako param)
- [x] `insertDrawingRunAtTextOffset`
- Test: `PhaseD2_ImageInsertModuleMatchesLegacyIifeByteForByte` (byte-parity proti `__testHooks`). Namespace `objects.imageInsert`.

### 2.2 Revision payload creators → `history/revision-helpers.mjs` [pure] ✅
`createInsertionRevisionPayload`/`createStructureRevisionPayload`/`createDeletionRevisionPayloadFactory` byly už hotové. Nově přidáno:
- [x] `createLiveInsertionRevisionPayloadFactory({ selectionToRange })` — derived range z selection
- Test: rozšířen `PhaseD2_RevisionPayloadFactoriesProduceTypedRecords`.

### 2.3 Formatting-state → `input/formatting-state.mjs` [factory] ✅
Factory `createFormattingStateModule({ findBlock, buildIndexes, validateStableSelectionToken? })`. Sub-helpery importovány přímo (command-classifiers, selection-range, runs-for-range, pending-marks, inherited-style, selection-token, first-block, marks). `toBlazorFormattingState` zůstal v `core/blazor-formatting-state.mjs` (nebyl duplikován).
- [x] `selectionDisabledReason`, `collectFormattingState`, `resolveFormattingSelection`, `computeFormattingState`, `formattingScalarValue`
- Test: `PhaseD2_FormattingStateModuleDerivesInlineAndDisabledState` (behavior). Namespace `input.createFormattingStateModule`.
- POZN: `dispatchFormattingState`/`scheduleFormattingStatePublish` zůstávají [bridge] — odloženo.

### 2.4 Object-selection ARIA/HTML → `render/object-aria-html.mjs` [pure] ✅
`createRenderObjectSelectionDescriptionAttribute`/`createRenderObjectResizeHandleHtml`/`createRenderObjectFocusPolicyAttributes` byly už hotové. Nově:
- [x] `renderObjectRotationHandleHtml` (statický span, bez deps — plain export)
- Test: rozšířen `PhaseD2_ObjectAriaHtmlBuildsSelectionResizeAndFocusAttrs`.

### 2.5 Canonical-document normalizers → `core/canonical-document.mjs` [pure] ✅
Self-contained (vlastní helpery hasOwn/cloneJson/readPair/writePair/ensureArray/ensureString/sortObjectDeep pro byte-paritu).
- [x] všechny normalizery (inline/block/content/table/image/headerFooter/document) + `fromCanonicalDocument`/`toCanonicalDocument`/`normalizeCanonicalSnapshot`/`diffCanonicalDocuments`/`roundTripCanonicalDocument`/`stripRuntimeFields`/`findFirstDifference`
- POZN: `_storeSnapshotRuntime`/`_snapshotFromRuntime` NEextrahovány (závisí na runtime `runtimeDocuments` mapě — patří do runtime bridge).
- Test: `PhaseD2_CanonicalDocumentModuleMatchesLegacyRuntimeSerialization` (byte-parity proti `tmDocumentEditorRuntime.__testHooks`). Namespace `core.canonical`.

### 2.6 Marker store → `core/marker-store.mjs` [pure/factory] ✅ (už bylo hotové)
- [x] `createMarkerStore` → `createMarkerStoreFactory` (core/marker-store.mjs)
- [x] `collectInlineCommentRanges` → core/inline-marker-ranges.mjs (`createInlineMarkerRanges`)
- [x] `collectInlineRevisionRanges` → core/inline-marker-ranges.mjs
- Kryto `PhaseD2_InlineMarkerRangesAccumulatePerIdAcrossRegions` + entry assertions.

---

## 3. Střední námaha — factory s injektovanými engine deps — ✅ HOTOVO (2026-05-29, entry version 194, 264 PhaseD testů)

### 3.1 Command dispatcher → `history/command-dispatcher.mjs` [factory] ✅
- [x] `createCommandDispatcherFactory({ findBlock, buildIndexes, createOperation, applyOperation, collectFormattingState, createTableController })` → `createCommandDispatcher(model, options)`. Interní `removeMarksForCommandInRange`/`clearFormattingInRange`/`findTableBlock`. Test `PhaseD3_CommandDispatcherRegistersCommandsAndSetsPendingMarks`. Namespace `history.createCommandDispatcherFactory`.

### 3.2 Revision engine → `history/revision-engine.mjs` [factory] ✅
- [x] `createRevisionEngineFactory(deps)` → `createRevisionEngine(model, options)`. Injektováno 12 model-mutátorů (ensureRevisionList/addRevision/getRevisionById/setRevisionForRange/applyRevisionMark/clearRevisionFromRuns/removeRevisionRuns/updateRevisionStatus/removeRangeText/splitParagraphPreservingInlineMetadata/findBlock/buildIndexes). `renderOverlay` používá `globalThis.document` (jen browser). Test `PhaseD3_RevisionEngineInsertsAndReviewsRevisions`.

### 3.3 Transaction + History controller → `history/history-controller.mjs` [factory] ✅
- [x] `createTransaction` — už bylo v `transactions.mjs` (reuse přes injekci).
- [x] `createHistoryControllerFactory(deps)` → `createHistoryController(model, options)` — obsahuje interně createHistoryRestoreOperation/createHistoryEntryFromTransaction/canCoalesceHistoryTyping/coalesceHistoryEntry. **POZN: paragraph engine + atomic renderer (sekce 4) jsou injektované deps** (`createParagraphLayoutEngine`, `createAtomicRenderer`) — modul je extrahován, ale plné funkční zapojení čeká na sekci 4. Test `PhaseD3_HistoryControllerWiresStacksAndCommits` (stubuje engine cores).

### 3.4 Table controller → `objects/table-controller.mjs` [factory] ✅
- [x] `createTableControllerFactory({ findBlock, buildIndexes, createOperation, pointerHitTest })` → `createTableController(model)`. Interní `createEmptyTableCell` (přes importBlock) + `findTableBlock`. Test `PhaseD3_TableControllerInsertsRowsAndRecordsOperations` (funkční — vkládá řádky/sloupce do reálného modelu). Namespace `objects.createTableControllerFactory`.

### 3.5 Object-selection snapshot → `core/object-selection-snapshot.mjs` [factory] ✅
- [x] `createObjectSelectionSnapshotFactory({ findDrawingRunByObjectId })`. Test `PhaseD3_ObjectSelectionSnapshotFactoryBuildsObjectMode`.

### 3.6 Selection-post-fixer → `core/selection-post-fixer.mjs` [factory] ✅
- [x] `createSelectionPostFixerFactory({ findBlock, findDrawingRunByObjectId, createObjectSelectionSnapshot })` → `createSelectionPostFixer(schema)`.
- [x] **navíc** `core/selection-normalize.mjs` — `createSelectionNormalizers({ findBlock })` → normalizeLogicalPosition/Range/SelectionSnapshot (prereq, nebyl extrahován). Testy `PhaseD3_SelectionPostFixerCanonicalisesSelection` + `PhaseD3_SelectionNormalizersClampPositionAndCollapseCrossLimit`.

### 3.7 ApplyOperation handlers — ✅ (už bylo hotové)
- [x] `replaceModelContents` → `core/replace-model.mjs` (`createReplaceModelContents`)
- [x] `applyRevisionDecision` → `history/handlers-revision-decision.mjs` (`createRevisionDecisionHandler`)
- [x] `applyRestoreSnapshot` → `history/handlers-restore-snapshot.mjs` (`createRestoreSnapshotHandler`)

---

## 4. Velké engine-jádra (vysoká námaha, hlavní zbývající objem)

### 4.1 Paragraph layout engine → `layout/paragraph-engine.mjs` [factory] — NEJVĚTŠÍ
- [ ] `createParagraphLayoutEngine` — **ř. 8994–10157 (~1160 ř.)**. Podpora už extrahována (`line-breaker.mjs`, `text-measurement.mjs`, `paragraph-tokenizer.mjs`, `paragraph-alignment.mjs`). Orchestrátor sám zbývá. Deps: measurementService, lineBreaker, exclusions manager.
- [ ] `createInlineObjectLayoutFromSegment` — **ř. 10189–10211**
- [ ] `layoutObjectBlock` — **ř. 10212–10255**

### 4.2 Atomic renderer + dokončení Fáze B1 → `render/atomic-renderer.mjs` [DOM/factory]

**Kontext B1** (viz `planning/tmdocumenteditor-performance-and-features-todo-2026-05-26.md` §4): Fáze B1 (strukturální keys + inkrementální DOM diff) je uvnitř `createAtomicRenderer` z velké části HOTOVÁ — per-blok fingerprint skip (`_computeParagraphFingerprint` FNV-1a + `container.__tmFingerprint`), per-segment Map diff (B2), `validateRenderInvariants` off-by-default (B3), shallow `localizeLayoutBlock` (B4). **ALE produkční render path NEpoužívá atomic renderer**: `render(inst)` v `renderEngine` (**ř. 22078**) dělá full string rebuild `inst.root.innerHTML = html.join('')` (**ř. 22120**). Skutečné dokončení B1 = (a) vyextrahovat atomic renderer jako modul a (b) **napojit produkční render path na jeho DOM diff** — to je hlavní výkonový přínos celé fáze D pro plynulost psaní. Bod (b) je odložený scope z Checkpointu B (~1500+ ř. refactoru) → vysoké riziko, dělat postupně.

#### 4.2.1 `createRenderSnapshot` — ✅ HOTOVO
- [x] `createRenderSnapshot` + `flattenLayoutSegments` + `stableChecksum` už v `render/render-snapshot.mjs`, zapojeno v entry. (Původně ř. 10256–10316.)

#### 4.2.2 Overlay / selection DOM helpery → `render/atomic-overlays.mjs` [DOM] ✅ (2026-05-29, entry version 200, 271 PhaseD testů)
- [x] `createRenderSelectionOverlay(doc)` → `renderSelectionOverlay(snapshot)` — overlay s `data-selection-block-id`
- [x] `createRenderRevisionOverlay(doc)` → `renderRevisionOverlay(snapshot)` — overlay s per-revision `data-revision-id/type` markery
- [x] `createRenderCommentMarkers(doc)` → `renderCommentMarkers(snapshot)` — overlay s per-comment `data-comment-id` markery
- [x] `restoreLogicalSelection(root, selection)` — zapíše JSON selection do `data-logical-selection`
- [x] `createObjectFocusPolicy(selected)` — čistá pure fn (bez DOM)
- [x] `createApplyObjectFocusPolicyToElement({ applyObjectSelectionAccessibility? })` → mutátor DOM elementu; `applyObjectSelectionAccessibility` (inst-scoped aria-describedby + resize labels) je opt-in injekce
- Test: `PhaseD4_AtomicOverlaysModuleBuildsOverlayNodes` (DOM stub). Namespace `render.atomicOverlays`.

#### 4.2.3 Extrakce `createAtomicRenderer` → `render/atomic-renderer.mjs` [DOM/factory] ✅ (2026-05-29, entry version 201, 272 PhaseD testů)
- [x] `createAtomicRendererFactory({ findBlock, applyObjectFocusPolicyToElement, renderSelectionOverlay, renderRevisionOverlay, renderCommentMarkers, restoreLogicalSelection, applySegmentStyleToElement?, doc? })` → `createAtomicRenderer(options)` — **ř. 10317–10837 (~520 ř.)**.
- [x] Zachovány beze změny B1/B2/B3/B4 stroje: `fingerprintHash`/`computeParagraphFingerprint`, fingerprint skip v `renderParagraphScope`, per-segment Map diff + `insertBefore`, shallow `localizeLayoutBlock`/`shiftRect`, `validateRenderInvariants` (`useDomMeasurements` opt-in), debug čítače (`paragraphFingerprintHits/Misses`, `segmentPatchCount`), `setDiagnostics`/`resetDebugCounters`.
- [x] Pure importy: `flattenLayoutSegments` (render-snapshot), `scopeIncludesBlock`/`rectsOverlap`/`domRectToRect`/`markOverlayNonText` (render-helpers), `applySegmentStyleToElement` (layout/segment-style).
- [x] DOM (`document`, text node, `Node.TEXT_NODE`→`nodeType===3`) přes injektovaný `doc` adaptér (factoryDeps.doc / opts.doc / globalThis.document) → testovatelné v Node se stubem.
- Test: `PhaseD4_AtomicRendererFactoryRendersAndSkipsByFingerprint` (DOM stub: render + B1 fingerprint hit na druhý render + rollback při root=null). Namespace `render.createAtomicRendererFactory`.
- **POZN**: tohle je extrakce — produkční render path stále používá string `render(inst)`; napojení = krok 4.2.5.

#### 4.2.4 B1 — block-level insert/remove/reorder v `render()` [B1.3/B1.4] ✅ (2026-05-29, 273 PhaseD testů)
- [x] **B1.4 eviction**: `pruneCaches(snapshot)` po každém renderu — projde `blockCache`, evikuje containery, jejichž block-key NENÍ ve `collectValidBlockKeys(snapshot)` (počítá se z plného `snapshot.layout.blocks` + header/footer region blocks, **ne** z „co se vykreslilo" → scoped/partial render neevikuje validní bloky mimo scope). Odebraný blok navíc uvolní své segmenty ze `segmentCache` (per-block `blockSegmentKeys` Map). Čítače `blockEvictionCount`/`segmentEvictionCount` v `debug()`.
- [x] **B1.3 insert/reorder**: vložené i přeskupené bloky se vykreslí v pořadí `snapshot.layout.blocks` (fragment-append zachovává pořadí, container reuse přes blockCache zachovává identitu). Ověřeno testem.
- Test: `PhaseD4_AtomicRendererInsertsInOrderAndEvictsRemovedBlocks` (DOM stub: pořadí p1/p2/p3 → reorder p2/p1/p4/p3 → removal p2+p4 → blockEvictionCount=2, segmentEvictionCount≥2, cachedBlockCount=2).
- **POZN — cíl „žádný full `replaceChildren`" NEDODĚLÁN**: `render()` stále volá `root.replaceChildren(fragment)` na top-level (rebuilduje page/layer wrappery, ale block containery reusuje přes cache → `replaceChildren` jen re-parentuje existující uzly). Plný top-level keyed reconcile (cache page/layer uzlů) je provázaný s migrací produkčního renderu → spadá do **4.2.5**, ne sem. Tím B1.2 (explicit „only changed block patched") + B1.5 (1000-block perf) zůstávají na 4.2.6.

#### 4.2.5 ❌ ZRUŠENO — nahrazeno sekcí **R (Přepis jádra)**, viz nahoře
> Po analýze (2026-05-29) se ukázalo, že „napojení produkčního render path na atomic renderer" NENÍ mechanická záměna, ale **architektonická výměna psacího povrchu** (contenteditable flow → model-owned positioned-DOM). Produkční `render(inst)` staví flowing HTML do contenteditable a `buildLayoutSnapshot` čte geometrii zpátky z DOM; atomic renderer staví absolutně pozicované spany z headless layoutu. To jsou dvě neslučitelné architektury. User zvolil cestu **přepisu jádra** (sekce R) místo migrace téhle jedné funkce. Krok 4.2.5 se proto ruší — jeho cíl (jediný DOM-diff render path) je obsažen v **R.4.8 cutover**.

#### 4.2.6 B1 testy (B1.2–B1.5)
Rozšířit `tests/.../AtomicRendererIncrementalDiffJavaScriptTests.cs` (už obsahuje B1.1, B2.1, B3.1):
- [ ] B1.2 „only changed block is patched" — po extrakci explicitní test (zatím jen implicitně přes fingerprint skip)
- [ ] B1.3 „inserted block placed at correct index"
- [ ] B1.4 „removed block disposed" (uzel + cache entry uvolněny)
- [ ] B1.5 „1000 block document: insert 1 char in block 500 mutates only 1 block" — **klíčový perf test** (ověří, že 4.2.4 + 4.2.5 reálně škálují)

### 4.3 Produkční string-HTML renderer → `render/engine-html.mjs` [pure, velké]
Velká rodina `render*Html` — **MNOHO už extrahováno** pod `createRender*`-factory názvy (ověřit přes entry.mjs render namespace). Zbývající orchestrátory:
> **Pozn. vazba na 4.2.5**: tyto string-builderery zásobují produkční `render(inst)` (ř. 22078, `innerHTML = html.join('')`). Pokud se 4.2.5 dokončí (napojení atomic DOM diff), string path se stane fallbackem / legacy — extrahovat ho má smysl pořád (testovatelnost + fallback za feature-flagem), ale `render(inst)` orchestrátor sám migruje 4.2.5, ne tato sekce.
- [ ] `renderParagraphRunsHtml` — **ř. 21247–21332**
- [ ] `renderWysiwygTextBlockHtml` — **ř. 21333–21349**
- [ ] `renderWysiwygBodyLayersHtml` — **ř. 21350–21369**
- [ ] `renderEngineBlockHtml` — **ř. 22037–22228**
- [ ] `renderEngineTableHtml` — **ř. 22229–22252**
- [ ] `renderHeaderFooterHtml` — **ř. 21983–22006**
- [ ] `buildSimpleHeaderFooterLayoutRegions` — **ř. 22007–22036**
- pozn.: projection helpery (`projectWysiwygParagraphAroundExclusions` 21671 atd.) — ověřit, většina už v `render/` (per entry.mjs tail).

### 4.4 Input pipeline + handlery → `input/input-pipeline.mjs` [DOM/factory]
- [ ] `createInputPipeline` — **ř. 11023–11426**. Deps: history, selection, applyOperation.
- [ ] `applyKeyboardInsertText` — **ř. 14157–14193**
- [ ] `applyKeyboardSplitParagraph` — **ř. 14194–14215**
- [ ] `applyKeyboardDelete` — **ř. 14253–14311**
- [ ] `handleEditorKeyDown` — **ř. 14430–14554** [DOM]
- [ ] `handleEditorBeforeInput` — **ř. 16939–17047** [DOM]
- [ ] composition: `handleEditorCompositionStart/Update/End` — **ř. 16835–16938** [DOM]

### 4.5 Clipboard paste → `clipboard/paste-pipeline.mjs` [DOM/factory]
`clipboard/paste-text.mjs` (text normalizace) hotovo. Zbývá DOM/HTML parse:
- [ ] `handleEditorPaste` — **ř. 17048–17118** [DOM]
- [ ] `copySelection` — **ř. 19765–19768** [bridge]

### 4.6 Selection engine + DOM↔model mapping → `layout/dom-selection.mjs` [DOM/factory]
- [ ] `createSelectionEngine` — **ř. 12562–12585**
- [ ] `createModelLayoutDomMapper` — **ř. 11961–12005**
- [ ] `logicalToDomRange` / `domRangeToLogical` / `domTextNodeToLogical` / `findTextNode` — **ř. 12006–12118** [DOM]
- [ ] `pointerHitTest` — **ř. 12432–12515** [DOM]
- [ ] `moveSelection` — **ř. 12516–12561**
- pozn.: `caret-rect.mjs` / `nearest-text-position-line-box.mjs` už existují (git status).

---

## 5. Objects — image/drawing UI interakce [DOM, nízká priorita]

`objects/` má 35 modulů (image-move-snap, image-move-track, image-resize-preview, active-image-target atd. dle git status). Zbývají DOM-vázané pointer/track orchestrátory:
- [ ] image move/resize track render — **ř. 15067–15715** (`createImageMoveTrack`, `computeImageMoveTrackPreview`, `renderImageMove*`, badge/guide render) [DOM]
- [ ] object pointer interaction — **ř. 15715–16011** (`beginObjectPointerInteraction`, `commitObjectPointerInteraction`, `computeObjectPointerCommitState`) [DOM]
- [ ] `createImagePreviewController` — **ř. 8847–8940** (možná částečně hotovo)
- pozn.: čisté geometrie už v `objects/geometry.mjs`, `image-move-snap.mjs`.

---

## 6. Toolbar / mini-toolbar bridge [DOM, nízká priorita]
- [ ] global toolbar bridge — **ř. 16085–16548** (`installGlobalToolbarButtonBridge`, selection preserve/restore, native button/select dispatch) [DOM]
- [ ] mini toolbar — **ř. 16703–16835** (`showMiniToolbarForSelection`, viewport refresh) [DOM]

---

## 7. Accessibility & keyboard handlers [DOM, nízká priorita]
`accessibility/announcements.mjs` (factory) + `labels.mjs` hotovo.
- [ ] `installAccessibilityAndKeyboardHandlers` — **ř. 17119–17308** [DOM]
- [ ] `removeAccessibilityAndKeyboardHandlers` — **ř. 17309–17349** [DOM]
- [ ] region/focus nav — **ř. 13849–14149** (`getRegionLabel`, `setActiveFocusRegion`, `focusNextRegion`, keyboard selection memory) [DOM/bridge]

---

## 8. Boundary patch / dirty state → `runtime/boundary-patch.mjs` [factory] ✅ (2026-05-29, entry version 195, 265 PhaseD testů)
- [x] `createBoundaryPatchModule({ attachOperationMethods, exportToCSharpJson, invokeBoundaryMethod, recordTimeline, ensurePerformanceStats, flushRuntimeRevisionsChanged })` — všechny 13 funkcí (ř. 17350–17570). Pure deps importovány (dirty-state, operation-affected, operation-classifiers, operation-types, selection-snapshot, first-block, helpers). Typing batch 500 ms, deferred batch 16 ms. Test `PhaseD8_BoundaryPatchModuleBuildsAndDispatches` (immediate dispatch + typing queue + merge). Namespace `runtime.createBoundaryPatchModule`.
- POZN: `shouldDeferBoundarySnapshot` odkládá SetParagraphAttribute/ApplyMark/RemoveMark jako "formatting visual" (nevolá Blazor synchronně pro každý stisk klávesy při formátování).

---

## 9. Watchdog lifecycle → `runtime/watchdog.mjs` [factory] ✅ (2026-05-29, entry version 196, 266 PhaseD testů)
- [x] `createWatchdogInstaller({ getMarkers?, getDebugSnapshot?, upsertMarker? })` → `installWatchdog(runtime)` → patchy `runtime.create/dispose/loadDocument/getDocument/executeCommand/applyRemoteOperation(Batch)` s try-catch + exponential backoff recovery, udržuje `watchdogContexts` Map, vrací `{ watchdogApi, uninstall }`. Pure deps z watchdog-helpers.mjs importovány přímo. `_resolveInstanceId` (DOM-závislý: querySelector) nevytažen — zůstává ve wrapperu na konci monolitu (sekce 11). Test `PhaseD9_WatchdogInstallerWrapsRuntimeAndRecovers` (lifecycle: create/loadDocument/executeCommand/dispose + uninstall). Namespace `runtime.createWatchdogInstaller`.

---

## 10. Runtime command execution → `runtime/command-execute.mjs` [factory] ✅ (2026-05-29, entry version 197, 267 PhaseD testů)
- [x] Pure exports: `readCommandName`, `readPayload`, `readSelectionToken`, `normalizeResult`. Factory: `createCommandExecutor({ getRuntime })` → `{ execute(instanceId, command) }`. Handles: missing instanceId/commandName → `invalid-command-request`, unavailable runtime → `runtime-unavailable`, exception → `command-exception`. Selección token + selection objekt forwarded do payload. Test `PhaseD10_CommandExecuteParseAndRoutes` (parser behavior + routing + exceptions). Namespace `runtime.commandExecute`.

---

## 11. Instance lifecycle + Blazor bridge [bridge — EXTRAHOVAT NAKONEC]
Hlavní JS-interop povrch. ~100 metod, **ř. 17571–20300**. Extrahuje se až po vyextrahování všech závislostí (engine cores, handlery), pak `create()` jen wires moduly.
- [ ] `create` / `dispose` / `loadDocument` — **ř. 17676–17864**
- [ ] `applyCommand` / `applyRuntimeFormattingCommand` / `applyRuntimeImageCommand` — **ř. 18151–18625**
- [ ] history bridge — **ř. 18626–18742** (`pushUndoTransaction`, `applyHistoryCommand`)
- [ ] snapshot/probe getters — **ř. 18743–19625**
- [ ] root/protection/search/scroll — **ř. 19627–19761**
- [ ] remote/comment/revision/marker bridge — **ř. 19765–20203**
- [ ] virtualizace + page plan — **ř. 20203–20455**

---

## 12. Collaboration [odloženo]
- [ ] remote ops / CRDT — `applyRemoteOperation(Batch)`, `applyRemoteCursor`, `applyStrictRemoteOperations` (ř. 19769–19783, 19118+). Lazy-load při connect. **Žádný samostatný `collaboration/` modul zatím (0 souborů).**

---

## 13. Performance probe → `runtime/performance-probe.mjs` [factory] ✅ (2026-05-29, entry version 198, 268 PhaseD testů)
- [x] `createPerformanceProbe({ getEngineMetrics?, now?, getElementPrototype? })` — všechny 6 public metod + interní `ensureReflowPatchInstalled`/`maybeUninstallReflowPatch`. Injektovaný `getElementPrototype` umožňuje Node.js testování bez DOM. Metriky: 18 engine delta polí z `METRIC_KEYS` + `MaxTypingBatchSize`/`ActiveRegion`. Test `PhaseD13_PerformanceProbeTracksReflowsAndInteropCalls` (startCapture/stop/isCapturing/getActiveCaptures/noteJsInteropCall/clearAll + reflow counting přes stubovaný Element.prototype). Namespace `runtime.createPerformanceProbe`.

---

## 14. Doporučené pořadí

1. **§2 pure bloky** (image-insert, revision-payloads, formatting-state, canonical-document, object-aria-html doplnit, marker-store ověřit) — rychlé výhry, byte-parity testovatelné.
2. **§3 factory bloky** (command-dispatcher, revision-engine, history-controller, table-controller, object-selection-snapshot, selection-post-fixer) — sjednotit legacy kopie s existujícími moduly.
3. **§8 boundary-patch, §9 watchdog, §10 command-execute, §13 perf-probe** — factory, ohraničené.
4. **§4 engine-jádra** (paragraph-engine NEJVĚTŠÍ, atomic-renderer, engine-html, input-pipeline) — hlavní objem.
5. **§5–§7, §11–§12 DOM/bridge** — nakonec, jen jako factory s injektovaným DOM adaptérem.

**Konvence při extrakci**: každý nový modul → import v `entry.mjs` + zařadit do domain namespace + bump `version` → přidat test do `PhaseDModuleExtractionTests.cs` (existence + behavior, ideálně byte-parity proti legacy `__testHooks` kde dostupné) → `node` smoke load entry → `dotnet test --filter PhaseDModuleExtraction`.
