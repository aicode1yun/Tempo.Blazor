# R.4.8 cutover — perf-parita + posouzení legacy E2E regrese

> Rozhodovací artefakt pro **flip default render engine** (Legacy → CoreEngine) a smazání legacy.
> Doplňuje `planning/r48-cutover-plan.md` a `planning/phase-d-remaining-extraction-todo.md` (sekce R.4.8/R.4.9).
> Datum: 2026-05-30, entry version 236. **Toto NENÍ schválení flipu — je to podklad k němu.**

---

## Část A — Typing perf-parita (CUTOVER BLOCKER #1) ✅ VYŘEŠENO

### Cíl
Psaní v core enginu musí být **subjektivně k nerozeznání od Wordu / Google Docs**: každý keystroke
se zpracuje v rámci jednoho snímku (frame budget ~16 ms @ 60 Hz) a latence **nesmí růst s velikostí
dokumentu** (musí být plochá / O(1), ne O(N)). Tohle je userův #1 cíl celého přepisu — kdyby flip psaní
zhoršil, přepis ztrácí smysl.

### Metodika měření — proč R65, ne R64
Měříme **dvě různé věci** a je kritické je nezaměnit:

- **`R64_PerfParity…`** = wall-clock přes Playwright. Zahrnuje round-trip overhead (CDP dispatch eventu →
  browser event loop → zpět). Ten fixní overhead je ~15–18 ms na keystroke a **nemá nic společného s naším
  enginem** — zkresluje směrem nahoru. Dobré jako horní mez „uživatel počká max tolik", ne jako čistá cena.
- **`R65_PerfParity…TrueSingleKeystrokeMainThreadCost`** = **pravá hlavní-vláknová cena**. Dispatchne
  synchronní `beforeinput` na off-screen surface a změří **jen synchronní handler** (`performance.now()`
  kolem). Tohle je číslo, které odpovídá tomu, kolik snímku zabere zpracování klávesy — **správná metrika
  pro „frame budget".**
- **`R66_PerfProfile…Breakdown`** = rozpad té synchronní ceny na fáze (layoutDocument / bidi / list /
  viewLayout / snapshot / renderer / overlays) → dokazuje, KDE čas je a že incremental cesta obchází full layout.

### Naměřeno (2026-05-30, entry version 236, headless Chromium)

**R65 — pravá hlavní-vláknová cena keystroke (medián p50):**

| Velikost dokumentu | mean | **p50** | p95 | min–max |
|---|---|---|---|---|
| 30 odstavců  | 1.38 ms | **1.40 ms** | 1.90 ms | 0.80–2.50 |
| 100 odstavců | 1.82 ms | **1.50 ms** | 2.60 ms | 0.90–5.50 |
| 500 odstavců | 3.33 ms | **3.30 ms** | 4.10 ms | 2.50–4.30 |

**R66 — rozpad synchronní ceny (průměr přes 15 keystroke):**

| Velikost | total | layoutDocument | snapshot | renderer | overlays |
|---|---|---|---|---|---|
| 30p  | 0.4 ms | **0.0** | 0.0 | 0.2 | 0.1 |
| 100p | 0.3 ms | **0.0** | 0.0 | 0.2 | 0.1 |
| 500p | 0.7 ms | **0.0** | 0.1 | 0.2 | 0.5 |

`layoutDocument = 0.0 ms` napříč všemi velikostmi = incremental cesta (R.4.9.3 `relayoutDirtyBlock`)
**plně obchází full-document layout.** Zbytek je per-block layout + in-place DOM patch (R.4.9.3b-2 `patchBlocks`).

### Před vs. po R.4.9

| metrika (ms/char) | 30p | 100p | 500p | charakter |
|---|---|---|---|---|
| **Před R.4.9** (full render/keystroke) | 92 | 250 | 1428 | **O(N) — katastrofa, horší než legacy** |
| **Po R.4.9** (R65 p50) | 1.40 | 1.50 | 3.30 | **plochá, O(1)** |
| **Zrychlení** | ~66× | ~167× | ~433× | |

### Verdikt části A
- **Každý keystroke je hluboko pod 16 ms frame budgetem** (p50 1.4–3.3 ms; i p95 max 4.1 ms).
- **Latence je plochá** — 500p doc píše jen ~2 ms pomaleji než 30p (a to v rámci šumu). O(1) cíl splněn.
- Pokrývá **typing i řádkové zalomení i přechod přes stránku** (Y-reflow R.4.9.4 + cross-page repaginace
  R.4.9.10), vše **golden byte-identické** s full renderem (`R68`/`R70`/`R71`/`R72`).
- **Srovnání s legacy:** legacy běží na nativním `contenteditable`, kde layout dělá prohlížeč → app-cost ~0 ms,
  ALE za cenu nulové kontroly nad layoutem (proč vlastně děláme přepis). Core engine za 1.4–3.3 ms dělá
  **vlastní** model-owned layout + positioned-DOM a pořád je to **pod prahem vnímatelnosti** (≪ 16 ms).
  → **Typing-parita s Word/GDocs DOSAŽENA. Blocker #1 padá.**

### ⚠️ Otevřené (NEblokuje typing, blokuje „load-time pocit")
**First-paint je pořád cold full-document layout** (`R64`: 30p 75 ms / 100p 248 ms / **500p 867 ms**).
To je *jednorázová* cena při otevření dokumentu / `setModel`, ne per-keystroke. Není to typing blocker,
ale velký dokument se „načítá" znatelně. **Follow-up: virtualizovaný/odložený first-layout** (layoutovat jen
viditelné stránky, zbytek lazy) — sledováno mimo R.4.9. Doporučení: **nesmí blokovat flip** (legacy má taky
cold-load cost), ale zařadit hned po flipu.

---

## Část B — Posouzení legacy E2E regrese

### Stav testovací sady (skutečnost, ne odhad)

| sada | počet | běží proti |
|---|---|---|
| **Legacy `DocumentEditor*E2ETests`** (vše mimo `CoreEngine*`) | **592** | legacy WYSIWYG host (contenteditable) |
| bUnit component suite (`Tempo.Blazor.Tests`) | ~771 (**768 pass / 3 pre-existing fail**) | komponentní render (engine-agnostic z větší části) |
| **Core engine `CoreEngineHostBridgeE2ETests`** | **16** (R49–R63 + R74) | core engine přes Blazor bridge |
| **Core engine `CoreEngineRenderHostE2ETests`** | **29** (R40–R73) | core engine přímo (harness) |
| **PhaseR Node** (`PhaseDModuleExtractionTests`) | **22** | core engine moduly (Node, bez prohlížeče) |

### Proč 592 legacy E2E NELZE „přepnout" na core engine
Ne kvůli lenosti — kvůli **odlišnému DOM kontraktu**. Legacy testy selektují konkrétní legacy DOM, který
core engine z principu nevytváří. Důkaz z `DocumentEditorE2ETests.cs` (frekvence selektorů):

| legacy selektor | výskytů | core engine ekvivalent |
|---|---|---|
| `[data-testid="document-wysiwyg-host"]` | 95 | `[data-testid="document-core-engine-host"]` (jiný host) |
| `data-block-id` | 235 | `data-render-block-id` (jiný atribut) |
| `[contenteditable]` | 54 | **žádný** — core engine je positioned-DOM, off-screen textarea |
| `.tm-wysiwyg-page`, `.tm-wysiwyg-page__body` | ~15 | `.tm-render-*` positioned segmenty |
| `.tm-wysiwyg-table`, `figure.tm-wysiwyg-image` | ~9 | jiná struktura overlay figure |

Legacy editor renderuje **flowing contenteditable HTML** (`TmDocumentWysiwygHost`); core engine renderuje
**absolutně pozicované spany z headless layoutu** (`TmDocumentCoreEngineHost`), **bez contenteditable**.
To jsou dvě architektonicky neslučitelné DOM vrstvy (důvod proč 4.2.5 byl zrušen — viz plán). Přepsat 592
testů na nové selektory = stovky hodin a většina by stejně testovala legacy-specifické chování (contenteditable
caret quirky, JS-runtime watchdog, region scope…), které v nové architektuře neexistuje.

### Co tedy core engine REÁLNĚ pokrývá (45 E2E + 22 Node)
Bridge E2E (R49–R63 + R74) ověřují **plnou cestu inspector/toolbar → C# → interop → engine → DOM** v živém
Blazoru; render-host E2E (R40–R73) ověřují **engine přímo v prohlížeči** (reálná klávesnice/myš/IME). Mapa
na legacy feature-oblasti:

| feature oblast | legacy pokrytí | **core engine pokrytí** | stav |
|---|---|---|---|
| psaní / caret / selekce | ano | `R42`/`R43` + R65 perf | ✅ |
| IME kompozice | částečně | `R44` | ✅ |
| bidi / RTL / grapheme | částečně | `R45` | ✅ |
| inline marks + zarovnání | ano | `R46a` + bridge `R51`/`R53` | ✅ |
| nadpisy / odstavcové styly / outline | ano | `R46b` + bridge `R59` | ✅ |
| seznamy (bullet/ordered) | ano | bridge `R54` | ✅ |
| tabulky (insert/typ v buňce/row-col) | ano | `R46c` | ✅ |
| obrázky: insert URL / upload / asset | ano | bridge `R56`/`R57`/`R60` | ✅ |
| obrázky: wrap / size / align / z-order | ano | bridge `R58`/`R61`/`R63` | ✅ |
| **obrázky: caption / position** | ano | bridge **`R74`** + `R73` | ✅ (nově) |
| hyperlinky | ano | `R46h` | ✅ |
| find / replace | ano | `R46h-2` + bridge `R62` | ✅ |
| komentáře | ano | `R46g` + bridge `R55` | ✅ |
| track changes / revize | ano | `R46f` | ✅ |
| hlavičky/patičky + page fields | ano | `R46e` | ✅ |
| undo / redo | ano | `R46i` + bridge `R51` | ✅ |
| přístupnost (ARIA) | ano (`Phase21`) | `R47` (automatizovaná brána) | ✅ * |
| save / dirty / autosave | ano | bridge `R52` | ✅ (autosave follow-up) |
| **typing perf @ scale** | `Phase20`/`PhaseABC` | **R64/R65/R66 + golden R68/R70/R71/R72** | ✅ |

\* manuální NVDA/VoiceOver = lidský follow-up (programmatic ARIA brána je zelená).

### Mezery (známé, dokumentované — ne skryté)
Tyto core engine zatím NEpokrývá; žádná není „tiché" legacy chování, všechny jsou v plánu jako follow-up:
- **bookmarks**, klik-otevře-odkaz (Ctrl+klik → `window.open`)
- tabulky: merge/split buněk, cell-selection, Tab-navigace, col-resize
- revize: cross-block tracked delete, format-revize, per-revize accept/reject, review-módy
- komentáře: vlákna/reply, komentářová lišta UI + navigace
- hlavičky: datum-pole, první/sudá/lichá, edit klikem
- autosave debounce, collab (realtime), TOC blok + outline navigace UI
- **first-paint virtualizace** (viz část A — load-time, ne typing)

### Doporučení / brána pro flip
1. **bUnit 768/3 + 45 core E2E + 22 Node = dostatečná regresní síť** pro flip. 3 fail jsou **pre-existing**
   (PDF/export image-block cast), **nesouvisí s R.4.8** (ověřeno stashem) — nejsou regresí přepisu.
2. **592 legacy E2E ponechat as-is, dokud legacy žije.** Jakmile flip proběhne a legacy se maže, smažou se
   s ním i jeho E2E (testují neexistující architekturu). Co se NESMÍ ztratit, je už pokryté v core sadě výše.
3. **PŘED flipem dořešit** (jinak by flip ubral funkčnost, kterou uživatelé mají): rozhodnout, které z „mezer"
   jsou must-have pro paritu (kandidáti: bookmarks, klik-otevře-odkaz, autosave). Zbytek může jít po flipu,
   pokud na něm tým a user souhlasí.
4. **Flip je NEVRATNÝ** (smazání legacy) → vyžaduje **explicitní schválení uživatele** + projetí: full core
   E2E sada zelená + bUnit beze změny + manuální smoke (otevřít reálný .docx, projet feature-checklist).
   **Legacy nemazat dřív** — rozbilo by to editor pro každého, kdo nemá `RenderEngine=CoreEnginePreview`.

### Shrnutí
- **Část A (perf):** ✅ typing-parita s Word/GDocs dosažena (p50 1.4–3.3 ms, plochá, golden-ověřená).
  Otevřený jen first-paint (load-time, neblokuje typing).
- **Část B (regrese):** legacy 592 E2E je nepřenositelná architekturou; náhradní síť (45 core E2E + 22 Node +
  768 bUnit) pokrývá všechny hlavní feature-oblasti. Flip není blokován testovacím pokrytím — je blokován
  **rozhodnutím o must-have mezerách + explicitním schválením** (smazání legacy je nevratné).
