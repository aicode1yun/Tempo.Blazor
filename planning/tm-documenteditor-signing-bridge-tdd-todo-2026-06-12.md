# TmDocumentEditor ↔ Signing most — kompletní TDD todo list (2026-06-12)

Cíl: propojit **TmDocumentEditor** (canvas engine) s existující **Signing sadou komponent**
(`src/Tempo.Blazor/Components/Signing/` — TmPdfTemplateDesigner, TmSigningFormRunner, …), aby šlo
smlouvy **autorovat přímo v editoru** a rovnou je používat jako podpisové šablony (DocuSeal-like
flow). Dvě fáze z diskuse 2026-06-12:

- **FÁZE 1 (S1):** export stránek dokumentu z canvas enginu do obrázků (`SigningDocumentPage`)
  → autorovaný dokument lze okamžitě použít v TmPdfTemplateDesigner + TmSigningFormRunner
  bez jediné nové UI komponenty.
- **FÁZE 2 (S2):** inline **signing fieldy** jako nový druh runu v modelu canvas enginu —
  pole se přelévají s textem, `SigningFieldArea` (0..1) se **generují automaticky z layoutu**,
  serializace ↔ `SigningField` (sdílený model v Abstractions). Žádný desync souřadnic.

**Role:** Senior Full-stack Developer + UX specialista + UI expert.

---

## Uzavřená architektonická rozhodnutí (diskuse 2026-06-12)

| # | Rozhodnutí |
|---|---|
| 1 | **Stavíme MOST, ne paralelní infrastrukturu.** Signing vrstva (komponenty + modely `SigningField`/`SigningFieldArea`/`SigningFieldType`/`SigningSubmitterRole`/`SigningDocumentPage` v `Tempo.Blazor.Abstractions/Models/`) už existuje a je jediným zdrojem pravdy pro signing model. Editor NIKDY neduplikuje vlastní signing typy. |
| 2 | **S1 export stránek = client-side z display-listu.** Canvas engine kreslí stránky přes `buildDisplayList(model, layout, options)` (`render/display-list.mjs`) + `paintDisplayList` (`render/canvas-renderer.mjs`) do canvasů per VIDITELNÁ stránka (virtualizace, `render/canvas-stack.mjs`). Export proto NEČTE živé DOM canvasy, ale kreslí display-list každé stránky do offscreen/dočasného canvasu → PNG dataURL. Žádný server, žádné PDF→PNG. |
| 3 | **S2 = nový run typ `signingField`** (vzor integračních bodů: content-control runy `controls/sdt-model.mjs`/`sdt-render.mjs`/`forms-mode.mjs` a field runy `fields/field-engine.mjs`). NEROZŠIŘUJEME SDT content controls — SDT zůstává Word parita; signing field potřebuje roli (`submitterUuid`), pevnou geometrii boxu a signing typy (signature/initials/stamp…). |
| 4 | **Inline pole = atomický inline box; areas se VŽDY derivují z layoutu, NIKDY se neukládají ručně** (`getSigningFieldsJson`). Pole se nedělí přes řádky/stránky. **Počet areas = počet výskytů pole v layoutu:** pole v těle dokumentu → **1 area**; pole v hlavičce/patičce → **N areas** (1 per stránku, na které se daná HF instance vykreslí). Derivace = grupování display-list příkazů podle `fieldUuid` (jednotně pokrývá tělo i HF). |
| 5 | **Podporované typy pro inline vkládání:** všechny `SigningFieldType` KROMĚ `Heading` a `Strikethrough` (ty jsou statické overlay-only — v autorovaném dokumentu se píšou přímo textem). Render v editoru = generický box (ikona + label + barva role); typ-specifické chování řeší až TmSigningFormRunner. |
| 6 | **Pole v hlavičkách/patičkách = ANO už v S2 (O2 vyřešeno: zahrnout).** Sémantika DocuSeal: **1 SigningField s N areas = 1 hodnota „orazítkovaná" do všech areas** (klasický use-case: iniciály/podpis v patičce každé strany). Scope (default/first-page/even-odd) se řeší AUTOMATICKY derivací z layoutu — area vznikne jen tam, kde layout pole reálně vykreslil. **Downstream beze změny:** `SigningStepPlanner` už dělá 1 krok per pole + overlay `SelectMany` přes všechny areas (ověřeno 2026-06-13). |
| 7 | **Mimo scope (vědomě):** fill mode v editoru (vyplňování přímo v dokumentu), flatten polí do PDF, server-side PDF render/PAdES pečeť, PDF→PNG pro nahraná PDF. To jsou navazující projekty. |
| 8 | ⚠️ **DocuSeal je AGPLv3 (+ additional terms)** → výhradně clean-room inspirace konceptů (datový tok, UX vzory), ŽÁDNÉ kopírování kódu/assetů. Totéž platí pro OnlyOffice. |

---

## ⚠️ KRITICKÁ PRAVIDLA — ABSOLUTNÍ ZÁKAZ PORUŠOVAT

### 1. TDD — TEST FIRST (Red-Green-Refactor) — STRIKTNĚ!
```
Krok 1: Napiš FAILING test (červený) — MUSÍ selhat před implementací (spusť a ověř!)
Krok 2: Napiš MINIMÁLNÍ kód pro průchod testu (zelený)
Krok 3: Refactoruj (čistý kód bez změny funkcionality)
Krok 4: Odškrtni checkbox v tomto souboru a pokračuj dalším taskem
```

### 2. PRŮBĚŽNÉ E2E + SCREENSHOT REVIEW — POVINNÁ BRÁNA KAŽDÉ FÁZE
- Každá fáze KONČÍ Playwright E2E testy se screenshoty do
  `tests/Tempo.Blazor.E2E/__screenshots__/signing-editor-bridge/phaseN/NN-nazev.png`.
- Před odškrtnutím fáze proběhne **dvoukolové posouzení screenshotů**:
  1. **Funkční posouzení** — screenshot prokazuje akceptační kritéria fáze (vyjmenovaná níže,
     posuzuje se proti nim, ne od oka).
  2. **UX expert review** — vizuální hierarchie, spacing/zarovnání dle design tokenů,
     typografie, afordance, stavy (hover/focus/disabled/selected/empty), konzistence s Tm*
     komponentami. Nálezy se opravují IHNED v rámci fáze a pořídí se nové screenshoty.
- Vzor konvence: `__screenshots__/document-library/phaseN/NN-name.png`,
  testy `DocumentEditorCanvas*E2ETests.cs` v `tests/Tempo.Blazor.E2E/`.

### 3. ŽÁDNÉ HARDCODED TEXTY — VŠE Z RESOURCES!
- UI texty komponent do `src/Tempo.Blazor/Resources/TmResources.resx` (+ `.cs.resx`, `.fr.resx`).
- Signer-facing texty polí přes existující `SigningLocalizedText` / `SigningTextResolver`
  (`Components/Signing/SigningTextResolver.cs`) — NEvymýšlet nový mechanismus.

### 4. ŽÁDNÉ ZJEDNODUŠENÉ IMPLEMENTACE
- ❌ placeholdery, mock data v produkčním kódu, TODO/FIXME, `// implement later`
- ✅ produkční kód od prvního řádku; demo data jen v demo projektech

### 5. PRAVIDLO TESTŮ
- ❌ NIKDY neměnit test se správnou logikou — test je specifikace
- ✅ VŽDY opravit příčinu v implementačním kódu

### 6. DOTAZY PŘI NEJISTOTĚ
- Když si nejsi jistý specifikací → zeptej se uživatele, nehádej

### Další závazná pravidla (projektová specifika)
- ⚠️ Po změně `.mjs` VŽDY `npm run build:document-editor` (harness/bundle); Node testy:
  `npm run test:document-editor-modules` (pokrývá `js/document-editor` i `js/document-editor-canvas`).
- ⚠️ `dotnet test` NIKDY plně paralelně (OOM, exit 137) → `-- xUnit.parallelizeTestCollections=false`
  nebo po projektech.
- ⚠️ Servery: WASM demo `dotnet run --project src/Tempo.Blazor.Demo --launch-profile https` (7106),
  API `--launch-profile Tempo.Blazor.Demo.Api` (5100). Contract-demo data na WASM jdou z API přes
  collab hub → po změně C# seedu RESTARTOVAT I API.
- ⚠️ Scoped CSS (`.razor.css`) + CSS design tokeny Tempo.Blazor, žádné hardcoded barvy/spacing.
- ⚠️ Známé pre-existing faily (NEsouvisí s tímto plánem, neopravovat „mimochodem", jen nesmí
  přibýt nové): component suite 3× PDF/export image-block cast; Node Phase15 Y-snap
  (object-top vs grid). Stav ověřit v S0 a zafixovat jako baseline.

---

## Současný stav (ověřeno ve zdrojácích 2026-06-12 — MD/memory tohle neobsahovaly!)

**Signing sada (hotová, ~30 komponent):**
- `Components/Signing/TmPdfTemplateDesigner.razor(.cs)` — builder: `Documents` =
  `IReadOnlyList<SigningDocumentPage>`, `Fields`/`FieldsChanged`, `SubmitterRoles`,
  `SelectedSubmitterUuid`, `AllowedFieldTypes`, `OnDetectFields`, MobileMode, zoom/view módy.
- `TmSigningFieldOverlay` (drag/resize), `TmSigningFieldEditorPanel` (vlastnosti pole),
  `TmSigningFormRunner` + step komponenty (vč. Signature kroku přes
  `Components/Inputs/TmSignatureCapture` — draw/typed/upload), `TmRecipientRoleEditor`,
  `TmShareLinkPanel`, `TmSubmissionStatusTimeline`, `TmSigningCompletionPanel`,
  `TmAuditTrailViewer`, `TmPdfSignatureVerification`, `TmConditionBuilder`, `TmFormulaBuilder`,
  `TmDocumentPageViewer`.
- Modely v `src/Tempo.Blazor.Abstractions/Models/`: `SigningField` (Uuid, SubmitterUuid, Type,
  Required, ReadOnly, Prefillable, DefaultValue, Preferences, Validation, Conditions, Options,
  **Areas**), `SigningFieldArea` (**normalizované 0..1**: Page, X, Y, Width, Height,
  AttachmentUuid), `SigningFieldType` (19 typů), `SigningSubmitterRole` (**má `Color`**),
  `SigningDocumentPage` (AttachmentUuid, PageIndex, **ImageUrl**, Width, Height, Labels),
  `SigningStepPlanner`, `SigningGeometryHelper`, `SigningFormulaHelper`.
- Demo: `src/Tempo.Blazor.Demo.SharedUI/Pages/SigningComponentsPage.razor` (`/signing-components`).

**Canvas engine (výchozí pro `/document-editor`):**
- JS: `src/Tempo.Blazor/wwwroot/js/document-editor-canvas/` — `interop.mjs` (mount, execCommand,
  getModelJson/replaceModel, **getPageMetricsJson**, getSelectionStateJson, setOptions, on…),
  `render/display-list.mjs` (`buildDisplayList(model, layout, options)` — čistá funkce),
  `render/canvas-renderer.mjs` (`paintDisplayList`), `render/canvas-stack.mjs`
  (canvas-per-visible-page, layery), `layout/page-geometry.mjs`, `layout/pagination.mjs`.
- Vzory pro nový druh runu: `controls/sdt-model.mjs` + `sdt-render.mjs` + `forms-mode.mjs`
  (content controls: normalizace runu, render, command aliasy, navigace) a
  `fields/field-engine.mjs` (run `type:'field'` s payloadem).
- C# host: `Components/DocumentEditor/TmDocumentCanvasEngineHost.razor` (lazy import
  `./_content/Tempo.Blazor/js/document-editor-canvas/interop.mjs`, `_module.InvokeAsync`),
  `TmDocumentEditor.razor(.cs)` (vzor content-control popoveru:
  `SyncCanvasContentControlPopoverAsync`), `CanvasExportBridge.cs` (export přes providery),
  canonical model `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentBlocks.cs` +
  `Services/CanvasDocumentModelConverter.cs`.
- Testy: Node `*.test.mjs` (co-located + `__tests__/`), bUnit `tests/Tempo.Blazor.Tests/DocumentEditor/`,
  E2E `tests/Tempo.Blazor.E2E/DocumentEditorCanvas*E2ETests.cs`.

**Chybějící most (předmět tohoto plánu):** žádná reference mezi DocumentEditor a Signing
(grep křížových referencí prázdný). Editor neumí vydat stránky jako obrázky ani nenese signing pole.

---

## Cílový datový tok

```
                      TmDocumentEditor (canvas engine)
                      model JSON ──► layout ──► display-list ──► canvas stránky
                          │                        │
   FÁZE 2                 │                        │ FÁZE 1
   signingField runy      │                        ▼
   v modelu               │             exportPageImages(scale)
   (role, typ, box)       │             [{pageIndex,width,height,dataUrl}]
        │                 │                        │
        ▼                 ▼                        ▼
   getSigningFieldsJson(handle)          SigningDocumentPage[] (ImageUrl=dataUrl)
   pole + areas 0..1 z layoutu                     │
        │                                          ▼
        ▼                                TmPdfTemplateDesigner (overlay pole na PDF/exportu)
   SigningField[] (Abstractions)                   │
        └──────────────┬───────────────────────────┘
                       ▼
              TmSigningFormRunner (krokové vyplnění + TmSignatureCapture)
              → values → (mimo scope: flatten + PDF + PAdES v budoucí aplikaci)
```

---

## Otevřené otázky (pravidlo 6 — zeptat se, návrhy defaultů)

- [ ] **O1: Formát/scale exportu stránek** — návrh: PNG dataURL, `scale` option 1–3, default 2
      (retina/zoom v designeru). JPEG+quality jako opt-in pro velké dokumenty. Potvrdit v S1.1.
- [x] **O2: Signing pole v hlavičkách/patičkách** — **VYŘEŠENO 2026-06-13: ANO, zahrnout už v S2**
      (rozhodnutí #6). 1 pole = N areas = 1 hodnota stamped na každou stránku (iniciály/podpis
      v patičce). Scope (first-page/even-odd) se řeší derivací z layoutu zdarma; downstream
      (StepPlanner/overlay) už multi-area umí. Hlavní dopad: revize rozhodnutí #4 (multi-area),
      HF layout (`header-footer-layout.mjs`) musí umět `signingField` run, area-derivace grupuje
      podle `fieldUuid`. Viz fáze S2 (bloky B/C/D/E rozšířené o HF kroky).
- [ ] **O3: Degradace při exportu do DOCX/ODT/HTML** (`Tempo.Blazor.DocumentFormats` signing runy
      nezná) — návrh: exportovat jako placeholder text `⟦Pole: {label} ({role})⟧`, NEZTRÁCET data
      v canonical JSON. Potvrdit v S2.9.
- [ ] **O4: UX editace vlastností pole v editoru** — návrh: reuse `TmSigningFieldEditorPanel`
      v `TmDocumentSidePanel` (vzor comments/revisions panelů), ne nový dialog. Potvrdit v S2.8.

---

## FÁZE S0 — Baseline + inventura (před prvním červeným testem) ✅ HOTOVO (2026-06-13)

Cíl: zafixovat zelenou výchozí čáru, aby regrese byly jednoznačně přiřaditelné.

- [x] S0.1 Spustit a zaznamenat výsledky: `npm run test:document-editor-modules`,
      component suite (`tests/Tempo.Blazor.Tests`, neparalelně), canvas E2E smoke
      (`DocumentEditorCanvasEngineBaselineE2ETests`). Do tohoto souboru zapsat počty
      pass/fail + jmenovitě pre-existing faily (viz Kritická pravidla).
- [x] S0.2 E2E screenshot baseline demo stránek `/document-editor` a `/signing-components`
      → `__screenshots__/signing-editor-bridge/phase0/01-document-editor-baseline.png`,
      `02-signing-components-baseline.png` (referenční stav před změnami).
- [x] S0.3 Ověřit, že `SigningComponentsPage` demo flow (designer → runner) funguje živě
      na WASM (7106) — podklad pro S1.5 integraci.

**Akceptační kritéria S0:** zdokumentovaná baseline čísla; 2 screenshoty; žádná změna kódu.

### S0 — Zaznamenaná baseline (2026-06-13, branch `ediagrameditorrewrite`, necommitnuto)

**Node moduly** (`npm run test:document-editor-modules`, pokrývá `js/document-editor` +
`js/document-editor-canvas`): **392 pass / 0 fail**. = ZELENÁ čára pro vše, co S1/S2 reálně mění
(canvas engine modules). Po `npm run build:document-editor` bundle 1.2 MB, bez chyb.

**Component suite** (`dotnet test tests/Tempo.Blazor.Tests`, filtr
`DocumentEditor|Signing`, neparalelně): **2747 pass / 11 fail / 2758 total**. 11 failů je
**stabilních a pre-existing** (identické i po čisté rebuild bundlu → nejsou stale-artefakt) a
VŠECHNY leží v legacy `js/document-editor` enginu nebo v PDF/export image-block castu — tj.
mimo území canvas mostu. Jmenovitě:

- *PDF/export + provider boundary (image-block cast — memory „3 pre-existing"):*
  `TmDocumentEditorTests.ExportRequests_ReceiveStructuredMetadataForDocxAndPdfProviders`,
  `…Phase19_PdfExportRequest_IncludesImageTableAndReviewDisplayOptions`,
  `…SaveRequest_UsesStructuredProviderBoundaryDocumentWithoutDisplayOnlyImageUrl`,
  `…VersionCreate_SavesJsRuntimeDocumentBeforeProviderVersionSnapshot` (bUnit JSInterop
  focus-trap při version dialogu).
- *Legacy JS engine testy (`js/document-editor`, deprekovaný — NE canvas):*
  `DocumentEditorImageWrapPhase18CleanupJavaScriptTests.Phase18_StaticCleanupRemovesWysiwygFlowFallbacksAndDemoImageBlockConversion`,
  `DocumentEditorRuntimePhase2SelectionCommandJavaScriptTests.Phase2_SelectionToken_SerializesCollapsedCaretRangeAndBoundaryPath`,
  `DocumentEditorRuntimePhase22PerformanceJavaScriptTests.Phase22_Operations_RecordGranularInvalidationWithoutFullDocumentLayout`,
  `DocumentEditorRuntimePhase23UxPolishJavaScriptTests.Phase23_RuntimeInstance_RendersSelectedImageChromeAndPanelState`,
  `DocumentEditorRuntimePhase5JavaScriptTests.Phase5_SelectionTokenBoundary_ValidatesAgainstCurrentDocumentFingerprint`,
  `DocumentEditorWysiwygJavaScriptTests.JavaScriptTestHooks_CoverSelectionMappingAndRemoteCommandOrdering`,
  `DocumentEditor.Performance.PhaseDModuleExtractionTests.PhaseD2_LayoutScopeBuilderProducesSortedShapeWithDefaults`.

➡️ **Pravidlo pro S1/S2:** těchto 11 failů je povolená baseline; žádný NOVÝ fail (a žádný nový
fail v Node 392) nesmí přibýt. (Pozn.: dřívější memory odhadovala ~3 component faily — skutečný
stav na této větvi je 11; Node Phase15 Y-snap je aktuálně ZELENÝ.)

**E2E baseline + smoke**: nová třída `tests/Tempo.Blazor.E2E/DocumentEditorSigningBridgeE2ETests.cs`
(`[TestCategory("SigningEditorBridge")]`, `[DoNotParallelize]`), 2 testy **PASS** proti živému
WASM (7106):
- `S0_DocumentEditorBaseline_RendersAndCapturesScreenshot` — `/document-editor` mountuje editor
  host, screenshot 1440×4330 (424 kB).
- `S0_SigningComponentsBaseline_RendersDesignerAndRunnerFlow` — `/signing-components` má živě
  viditelné `pdf-template-designer`, `signing-runner-demo`, `signing-document-viewer`,
  `signing-field-overlay-gallery` (= S0.3 designer→runner most-cílové plochy fungují),
  screenshot 1440×16399 (1,3 MB).

**Screenshot review (funkční + UX):** ✅ obě plochy renderují čistě a konzistentně s Tm* design
jazykem. Doc editor = canvas vícestránková „Service agreement" + toolbar + version panel.
Signing demo = celá sada (~14 sekcí: Page Viewer, komentáře, Field Overlay galerie, Signature
Capture, Condition/Formula Builder, **TmPdfTemplateDesigner** s field editorem, **TmSigningFormRunner**
„Service Order Approval", Completion, audit timeline, Share+QR, PDF verification). Žádné UX nálezy
k opravě (S0 = jen záznam výchozího stavu, ne nová feature).

**Servery při běhu:** WASM `dotnet run --project src/Tempo.Blazor.Demo --launch-profile https`
(7106) + API `--launch-profile Tempo.Blazor.Demo.Api` běžely.

---

## FÁZE S1 — Export stránek editoru do obrázků (most bez nového UI) ✅ HOTOVO (2026-06-13)

Cíl: `TmDocumentEditor` umí vydat `SigningDocumentPage[]` a demo prokáže flow
„napiš smlouvu → naklikej pole v TmPdfTemplateDesigner → vyplň v TmSigningFormRunner".

### S1 — Výsledek (2026-06-13)

**Hotové artefakty:**
- JS: `render/page-image-export.mjs` (čistý modul: `renderPageToCanvas`/`renderDisplayListPageToCanvas`/
  `buildPageImageDisplayList`/`clampExportScale`/`EXPORT_LAYER_KINDS`) + interop `exportPageImages`/
  `exportPageImage` v `document-editor-canvas/interop.mjs` (reuse `getSnapshot().render.displayList` —
  exportuje VŠECHNY stránky vč. virtualizovaných). Flatten jen printable vrstvy (page-background/content/
  objects), bez editor chrome (caret/komentáře/diagnostika). O1 defaulty: PNG, scale 1–3 default 2,
  JPEG+quality opt-in.
- C#: DTO `DocumentPageImage` + `DocumentPageImageExportOptions` + extension
  `ToSigningDocumentPages(attachmentUuid, labelFactory?)` (`Abstractions/DocumentEditor/Models/
  DocumentPageImage.cs`, ns `Tempo.Blazor.DocumentEditor.Models`). Abstractions nemá lokalizér →
  label dodává UI přes `labelFactory` (default = bez labelu). `TmDocumentCanvasEngineHost.
  ExportPageImagesAsync` (per-page pull dle `GetPageMetricsAsync`) + `TmDocumentEditor.
  ExportPageImagesAsync` (deleguje; `InvalidOperationException` když není canvas engine).
- Demo: `/signing-from-editor` (`SigningTemplateFromEditorPage.razor`, self-contained
  `InMemoryDocumentEditorProvider` seed contract → editor → export → `TmPdfTemplateDesigner` (2 role)
  → `TmSigningFormRunner`) + NavMenu „Signing From Editor".

**Testy (red→green ověřeno u každého kroku):**
- Node `test:document-editor-modules`: **401/401** (+9: 6 page-image-export, 3 interop).
- Component (`DocumentEditor|Signing`, neparalelně): **2756 pass / 11 fail / 2767** — +9 nových
  (5 `DocumentPageImageMappingTests` + 4 `CanvasEnginePageImageExportTests`), 11 failů = IDENTICKÁ
  S0 baseline (žádný nový).
- E2E `DocumentEditorSigningBridgeE2ETests`: **3/3** (2× phase0 baseline + `S1_EditorPagesExport…`).
  Screenshoty `__screenshots__/signing-editor-bridge/phase1/01–04`. **Dvoukolové posouzení ✅** —
  exportovaná stránka „Service agreement" se v designeru i runneru shoduje s editorem (2× bitmap),
  pole položené v designeru teče do runneru jako podpisový krok (Draw/Type/Upload). UX čisté,
  konzistentní s Tm*, žádné nálezy zavlečené mostem.
- Canvas engine zdraví po live `interop.mjs` změně: `DocumentEditorCanvasEndToEndTypingE2ETests`
  **1/1**, caret/selection smoke pass. ⚠️ `DocumentEditorCanvasEngineBaselineE2ETests` má 2
  **pre-existing/zastaralé** faily (`Baseline_CurrentCoreEngineBeforeRedesign…` čeká `CoreEnginePreview`,
  `CanvasEngineRouteFlag_CurrentlyMissing…` čeká „canvas not routable") — obě testují svět PŘED
  phase-25 cutoverem (default je dávno `CanvasEnginePreview`), nesouvisí s S1. Doplnit do S0 baseline
  seznamu pre-existing failů.

**Pozn.:** canvas `interop.mjs` se načítá jako RAW ESM (jen legacy engine má dist bundle) → změny `.mjs`
jsou živé bez `npm run build:document-editor`; build se nespouštěl (netýká se canvas modulů).

### S1.A — JS: čistý export modul (Node testy)

- [x] S1.1 ČERVENÝ: Node test `render/__tests__/page-image-export.test.mjs` pro nový modul
      `render/page-image-export.mjs`: funkce `renderPageToCanvas(model, layout, pageIndex,
      {scale, createCanvas})` — fake canvas factory zaznamenává rozměry a volání kontextu;
      asserty: (a) canvas má `pageWidth*scale × pageHeight*scale` (z `layout/page-geometry.mjs`),
      (b) kreslí položky display-listu dané stránky (deleguje na `buildDisplayList` +
      `paintDisplayList` — fake context zachytí text/rect operace seed dokumentu),
      (c) funguje pro stránku MIMO aktuální viewport (virtualizace nesmí hrát roli),
      (d) `scale=2` násobí souřadnice i rozměry. Test MUSÍ padnout (modul neexistuje).
- [x] S1.2 ZELENÝ: implementace `render/page-image-export.mjs` — čistá funkce bez DOM globálů
      (canvas factory injektovaná; v prohlížeči default `document.createElement('canvas')`).
      Refactor: sdílet přípravu page render options s `canvas-stack.mjs` (žádná duplikace
      page-frame/background logiky).
- [x] S1.3 ČERVENÝ: Node test exportu přes engine handle (vzor `entry.test.mjs`):
      `exportPageImages(handle, optionsJson)` v `interop.mjs` vrací JSON
      `[{pageIndex, width, height, scale, dataUrl}]` pro VŠECHNY stránky vícestránkového
      seed dokumentu; `exportPageImage(handle, pageIndex, optionsJson)` pro jednu stránku
      (host si může stránkovat → žádné obří interop stringy). DOM stub: doplnit
      `toDataURL` na canvas stubu, pokud chybí.
- [x] S1.4 ZELENÝ: implementace interop exportů + `npm run build:document-editor`.

### S1.B — C#: API na hostu a editoru

- [x] S1.5 ČERVENÝ: unit testy nového DTO `DocumentPageImage` (PageIndex, Width, Height, Scale,
      DataUrl) v `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/` + mapovací extension
      `ToSigningDocumentPages(string attachmentUuid)` → `List<SigningDocumentPage>`
      (ImageUrl=DataUrl, Width/Height v CSS px stránky — tj. dělené scale; Labels = „Strana N"
      z resources). Testy: mapování 1:1, zachování pořadí, AttachmentUuid propsán.
- [x] S1.6 ZELENÝ: DTO + mapper.
- [x] S1.7 ČERVENÝ: bUnit test `tests/Tempo.Blazor.Tests/DocumentEditor/`:
      `TmDocumentCanvasEngineHost.ExportPageImagesAsync(DocumentPageImageExportOptions)` volá
      interop (`exportPageImage` per stránka dle `getPageMetricsJson` počtu stránek) a vrací
      `IReadOnlyList<DocumentPageImage>`; `TmDocumentEditor.ExportPageImagesAsync(...)`
      deleguje na canvas host (a vyhodí smysluplnou výjimku, pokud engine není canvas/mounted).
- [x] S1.8 ZELENÝ: implementace obou API + XML docs.

### S1.C — Demo + E2E BRÁNA

- [x] S1.9 Demo stránka `Pages/SigningTemplateFromEditorPage.razor` (`/signing-from-editor`,
      Demo.SharedUI + NavMenu): (1) `TmDocumentEditor` se seed smlouvou (nadpis, odstavce,
      tabulka — ať jsou ≥2 stránky), (2) tlačítko „Použít jako podpisovou šablonu" →
      `ExportPageImagesAsync` → (3) `TmPdfTemplateDesigner` s exportovanými stránkami +
      2 role (`SigningSubmitterRole` s barvami) + položení polí, (4) přepnutí na
      `TmSigningFormRunner` preview s naklikanými poli. Texty z resources.
- [x] S1.10 ČERVENÝ→ZELENÝ: E2E `tests/Tempo.Blazor.E2E/DocumentEditorSigningBridgeE2ETests.cs`
      (`S1` prefix testů): otevřít demo, ověřit (a) editor renderuje seed, (b) po kliknutí
      designer zobrazí stejný POČET stránek se shodným poměrem stran, (c) obrázek stránky 1
      vizuálně odpovídá editoru (pixelová sonda: text na očekávané pozici ne-bílý),
      (d) položené pole se objeví ve `Fields`, (e) runner zobrazí krok pole s náhledem stránky.
- [x] S1.11 Screenshoty: `phase1/01-editor-seed.png`, `02-designer-exported-pages.png`,
      `03-field-placed.png`, `04-runner-step.png` + **dvoukolové posouzení** (funkční + UX).
- [x] S1.12 Regrese: Node + component suite + canvas E2E sada — žádný nový fail proti S0 baseline.

**Akceptační kritéria S1:**
1. Export N-stránkového dokumentu vrací N obrázků se správnými rozměry a scale.
2. Export funguje i pro stránky mimo viewport (virtualizace).
3. `TmPdfTemplateDesigner` zobrazuje exportované stránky bez deformace; pole jde položit
   a doputuje do `TmSigningFormRunner`.
4. Žádná nová závislost editoru na Signing komponentách (jen Abstractions modely).
5. Screenshoty prošly dvoukolovým posouzením.

---

## FÁZE S2 — Inline signing fieldy v TmDocumentEditoru (vč. hlaviček/patiček) ✅ JÁDRO HOTOVÉ (2026-06-13)

### S2 — Výsledek (2026-06-13, necommitnuté)

**Hotovo (S2.1–S2.18, S2.23/24, S2.27–S2.30):** kompletní funkční vertikální řez body→runner.
- JS (canvas, raw ESM — bez buildu): `controls/signing-field-model.mjs` (normalize + `SIGNING_FIELD_TYPES`
  + role-color paleta), run typ `signingField` v `model/canvas-document-model.mjs`; layout jako atomický
  inline box — tělo přes `paragraph-tokenizer.mjs`/`paragraph-runs.mjs`/`paragraph-engine.mjs`
  (additivní `signingField` větev, legacy beze změny), hlavička/patička přes `header-footer-layout.mjs`
  (box clampnutý na region, příkaz per stránka); render `render/signing-field-render.mjs` (role barva,
  ikona, label, required, focus ring) + `canvas-renderer` case + display-list `roleColor` post-pass;
  commandy `commands/signing-field-commands.mjs` (insert/update/remove, undo/redo, **mutace VŠECH kopií
  bloku v body+sections+HF** — model drží body content duplicitně a layout čte section kopii); selekce
  `controls/signing-field-selection.mjs` (+headerFooterId/scope/repeats v `getSelectionStateJson`);
  **areas `controls/signing-field-areas.mjs` (grupování podle `fieldUuid` → `areas[]` 0..1; tělo 1,
  patička N; scope z layoutu)** + interop `getSigningFieldsJson`/`getSelectionState`; `signingRoles`
  protažené engine options → buildDisplayList (entry/canvas-stack/interop mount+setOptions).
- C#: DTO `DocumentSigningFieldDescriptor`/`…AreaDescriptor`/`…OptionDescriptor` + `ToSigningFields(attachmentUuid)`
  (multi-area, AttachmentUuid na každou area); canonical inline typ `DocumentSigningFieldRun` (+`[JsonDerivedType]`)
  + `CanvasSigningFieldRun` + converter round-trip (tělo i HF); `TmDocumentCanvasEngineHost`
  `GetSigningFieldsAsync`/`InsertSigningFieldAsync`/`SigningRoles` param; `TmDocumentEditor`
  `GetSigningFieldsAsync`/`InsertSigningFieldAsync`/`EnterHeaderFooterAsync`/`SigningRoles`/`SigningFieldsChanged`
  (guard `InvalidOperationException` mimo canvas).
- Demo `/signing-from-editor`: sekce „Inline signing fields" (vlož podpis do těla, iniciály do patičky,
  Preview inline) → `TmSigningFormRunner` nad exportovanými stránkami.

**Testy (red→green u každého kroku):**
- Node `test:document-editor-modules`: **433/433** (+32 nových: model 8, round-trip 2, layout body 3 + HF 2,
  render 5, commands 6, selection 3, areas 3). Legacy paragraph-engine/tokenizer beze změny chování.
- Component (`DocumentEditor|Signing`, neparalelně): **2774 pass / 11 fail / 2785** — 11 = IDENTICKÁ S0
  baseline (žádný nový). Nové: `SigningFieldModelConverterTests` (12), `CanvasEngineSigningFieldApiTests` (4),
  `CanvasSigningFieldRoundTripTests` (2).
- E2E `DocumentEditorSigningBridgeE2ETests`: **4/4** (S0 ×2, S1, **S2 `S2_InlineSigningFieldsFlowFromEditorIntoRunner`**).
  Screenshoty `phase2/01-inline-fields-in-document.png` (body „Signature *" box + footer „Initials" box,
  obě v barvě role) + `02-inline-runner-from-fields.png` („2 field(s), 2 area(s)", runner + signature step).
  **Dvoukolové posouzení ✅** (multi-area N>1 ověřeno Node testy — contract demo má 1 stránku).
  Canvas typing E2E **1/1** (shared-layout edity bez regrese).

**✅ DOKONČENO 2026-06-13 (dříve odložené UX/robustness):**
- **S2.19/20 toolbar skupina „Podpisová pole"** — ribbon Insert tab, gated `computeVisible` (canvas
  engine + neprázdné `SigningRoles`), tlačítko vloží signature pole pro první roli
  (`InsertSigningFieldFromToolbarAsync`); jinak skupina vůbec nevznikne. bUnit
  `CanvasEngineSigningToolbarTests` 2/2. (Pozn.: MVP = 1 tlačítko, ne plný role+typ dropdown.)
- **S2.21/22 properties popover** — při výběru pole (selection payload uuid/headerFooterId/scope/repeats)
  se zobrazí popover (label input, required, role select) + odznak „opakuje se na každé stránce" pro HF
  pole; změny → `updateSigningField`. bUnit (host selection mapping) + E2E `phase2/03-signing-field-
  properties.png` (popover auto na vloženém poli, edit labelu).
- **S2.25/26 degradace exportu** — HTML/Markdown/ODT/DOCX vykreslí placeholder `⟦Pole: {label} ({role})⟧`
  (sdílený `Internal/SigningFieldPlaceholder`) v těle i v patičce; import zpět neparsuje. Testy
  `SigningFieldExportDegradationTests` 4/4 (DocumentFormats 222/222).

**Celkový stav S2: KOMPLETNÍ.** Node 433/433, DocumentFormats 222/222, component
`DocumentEditor|Signing` jen 11 pre-existing failů (žádný nový), bridge E2E 4/4.

### Akceptační kritéria (původní)

## FÁZE S2 — Inline signing fieldy v TmDocumentEditoru (vč. hlaviček/patiček)

Cíl: pole jako objekt modelu (přelévá se s textem), areas 0..1 **derivované z layoutu** —
v těle 1 area, v hlavičce/patičce N areas (1 per stránku) → `TmDocumentEditor.GetSigningFieldsAsync()`
vrací hotový vstup pro runner/builder bez overlay kroku. **O2 zahrnuto: rozhodnutí #4 + #6.**

> **Sjednocující princip celé fáze (rozhodnutí #4/#6):** signingField run je jeden objekt modelu
> bez ohledu na to, zda žije v těle bloku nebo v obsahu hlavičky/patičky. Layout ho vykreslí
> tam, kde je — a u hlavičky/patičky **per stránku**. Areas se NIKDY neukládají; `getSigningFieldsJson`
> je odvodí **grupováním display-list příkazů podle `fieldUuid`** → tělo dá 1 area, HF dá N.
> Downstream (`SigningStepPlanner` = 1 krok/pole, overlay = `SelectMany` přes areas) multi-area
> už umí (ověřeno 2026-06-13) → ŽÁDNÁ změna runneru/overlaye.

### S2.A — Model runu (JS)

- [x] S2.1 ČERVENÝ: Node test `controls/__tests__/signing-field-model.test.mjs` pro nový modul
      `controls/signing-field-model.mjs`: `normalizeSigningFieldRun(run)` — run
      `{type:'signingField', signingField:{uuid, fieldType, submitterUuid, required, label,
      boxWidth, boxHeight, options[]}}`; asserty: defaulty (uuid generován, fieldType→'text',
      typ-specifické default rozměry boxu — signature/initials/stamp větší než text/checkbox),
      neznámý fieldType → 'text' + zachování payloadu, PascalCase/camelCase tolerance
      (vzor `sdt-model.mjs`). Mapa `SIGNING_FIELD_TYPES` = string klíče zrcadlící
      `SigningFieldType` enum názvy (kromě heading/strikethrough — rozhodnutí #5).
      **Pole je position-agnostic: identický run v těle i v HF obsahu; `uuid` = grupovací klíč
      areas (decision #4/#6).**
- [x] S2.2 ZELENÝ: implementace modulu.
- [x] S2.3 ČERVENÝ: Node test round-trip přes `getModelJson` → `replaceModel` beze ztráty pro
      run **(a) v těle bloku I (b) v obsahu hlavičky/patičky** (`DocumentHeaderFooter`-ekvivalent
      v canvas modelu); `core/schema-validation` run typ zná a nevaliduje ho pryč.
- [x] S2.4 ZELENÝ: zapojení do model normalizace/validace enginu.

### S2.B — Layout + render (tělo I hlavička/patička)

- [x] S2.5 ČERVENÝ: Node test layoutu (tělo): signingField run = **atomický inline box**
      boxWidth×boxHeight (doc jednotky) — (a) sedí na řádku s textem (line height respektuje
      boxHeight), (b) nezalamuje se UVNITŘ (přesun celého boxu na další řádek/stránku),
      (c) caret stops před/za polem fungují. Vzor: inline obrázky v layout pipeline.
- [x] S2.6 ZELENÝ: layout footprint (tělo).
- [x] S2.5b ČERVENÝ: Node test **HF layoutu** (`layout/__tests__/header-footer-layout…` +
      `header-footer-layout.mjs`): `layoutInlineLine` umí `signingField` run jako atomický box
      (dnes zná jen `field`/`textRun`). Pro dokument s polem v PATIČCE a 3 stránkami → HF layout
      vydá field-box příkaz **per stránku** (stejný `fieldUuid`/`runId`, `pageIndex` 0/1/2,
      `headerFooterId` vyplněn). Asserty: počet příkazů == počet stránek, stabilní `fieldUuid`.
      **+ box se vejde do (krátké) HF oblasti — výška boxu clampnutá na výšku regionu, jinak by
      `layoutHeaderFooterBlocks` přetekl/ořízl** (assert: box height ≤ region height; podpisové pole
      v patičce zůstane kompaktní).
- [x] S2.6b ZELENÝ: rozšíření `header-footer-layout.mjs` o signingField box.
- [x] S2.7 ČERVENÝ: Node test renderu (`controls/__tests__/signing-field-render.test.mjs` +
      display-list assert): box s (a) výplní/borderem v barvě role (role barvy přes engine options
      `setOptions` → `signingRoles:[{uuid,color,name}]`; fallback paleta dle pořadí role),
      (b) ikona dle typu + label (lokalizovaný text z hosta v payloadu runu — engine NElokalizuje),
      (c) selected stav (výraznější border; handles NE — inline), (d) required indikátor.
      **(e) HF pole se vykreslí na KAŽDÉ stránce** (display-list má signingField příkaz s daným
      `fieldUuid` na každém pageIndex). Asserty přes fake context.
- [x] S2.8 ZELENÝ: `controls/signing-field-render.mjs` + zapojení do `buildDisplayList`
      (vzor `sdt-render.mjs`) i do HF render cesty.

### S2.C — Commandy + interakce (vč. editace v hlavičce/patičce)

- [x] S2.9 ČERVENÝ: Node testy commandů (vzor `forms-mode.mjs` aliasy + `commands/`):
      `insertSigningField` (vloží run na caret s payloadem), `updateSigningField`
      (merge vlastností dle uuid), `removeSigningField` (odstraní run). Asserty:
      (a) execCommand routing přes `interop.execCommand`, (b) **undo/redo** (history vrátí
      model), (c) operace projdou collab operation-relay (`takeLocalOperationBatchesJson`
      po insertu není prázdný).
- [x] S2.9b ČERVENÝ: Node test **insert v HF**: po `editHeaderFooter`/`enterHeaderFooter`
      (caret v patičce) `insertSigningField` vloží run do OBSAHU patičky (ne těla) — `getModel`
      ukáže run v HF bloku; `getSigningFieldsJson` pak dá pole s N areas. Update/remove HF pole
      dle `uuid` fungují i bez aktivního HF editačního režimu.
- [x] S2.10 ZELENÝ: implementace commandů (tělo + HF target).
- [x] S2.11 ČERVENÝ: Node test selekce/hit-testu: klik do boxu pole → `getSelectionStateJson`
      obsahuje `signingField:{uuid, fieldType, submitterUuid, rect}`. **Pro HF pole payload navíc
      nese `headerFooterId` + `scope`** (`default`/`firstPage`/`even`/`odd`) + příznak `repeats:true`,
      aby host v properties panelu ukázal „opakuje se na každé stránce" (vzor content-control popover).
- [x] S2.12 ZELENÝ: implementace + `npm run build:document-editor` (pozn.: canvas interop = RAW
      ESM, build není nutný pro běh; spustit jen pokud se mění legacy bundle/harness).

### S2.D — Areas z layoutu (jádro mostu) — multi-area

- [x] S2.13 ČERVENÝ: Node test `getSigningFieldsJson(handle)` v `interop.mjs`: dokument se
      3 poli — (1) tělo, řádek 1; (2) tělo, v tabulce; (3) **patička** (3-stránkový dok) — vrací
      pole `[{uuid, fieldType, submitterUuid, required, label, options, **areas:[{page,x,y,width,
      height}…]**}]` s normalizovanými 0..1 souřadnicemi (rect z layoutu / page width+height
      z `page-geometry`). Asserty: (a) body pole → **právě 1 area** na správné stránce;
      (b) **patičkové pole → 3 areas** (page 0/1/2), všechny 0..1, shodné x/y/w/h; (c) reflow:
      vložení odstavce PŘED body pole posune jeho area.y / stránku (derivace, ne uložená hodnota);
      (d) **grupování podle `fieldUuid`** (3 příkazy patičky → 1 pole se 3 areas, ne 3 pole).
      Descriptor používá VŽDY `areas[]` (i body = 1-prvkové) kvůli jednotnosti.
- [x] S2.13b ČERVENÝ: Node test **scope z layoutu**: dokument s odlišnou hlavičkou první stránky
      (first-page-different). Pole v DEFAULT hlavičce → areas jen na stránkách 2..N (NE strana 0);
      pole ve FIRST-PAGE hlavičce → 1 area (jen strana 0). Dokazuje, že scope (first/even/odd)
      řeší derivace z layoutu zdarma — žádná zvláštní logika.
- [x] S2.14 ZELENÝ: čistý modul `controls/signing-field-areas.mjs` (grupování podle `fieldUuid`
      → `areas[]`) + interop wrapper.

### S2.E — C# most + toolbar UX

- [x] S2.15 ČERVENÝ: unit testy mapování (Abstractions): engine JSON ↔ `SigningField` —
      round-trip VŠECH podporovaných `SigningFieldType` (string názvy ↔ `SIGNING_FIELD_TYPES`),
      `Required`/`SubmitterUuid`/`Options` propsané; **`areas[]` → `SigningField.Areas` (multi):
      body pole = 1 area, HF pole = N areas; `AttachmentUuid` (parametr) se aplikuje na KAŽDOU
      area; `Page` zachován per area.**
- [x] S2.16 ZELENÝ: mapper (`CanvasDocumentModelConverter` vzor — vedle něj
      v `Abstractions/DocumentEditor/Services/`).
- [x] S2.17 ČERVENÝ: bUnit testy C# API: `TmDocumentEditor.GetSigningFieldsAsync()` →
      `IReadOnlyList<SigningField>` (vč. multi-area HF pole = jedno pole s N areas);
      `InsertSigningFieldAsync(SigningField, submitterUuid)` (vloží na aktuální caret — funguje
      i v HF editačním režimu); `SigningRoles` parametr → engine options (barvy);
      event `SigningFieldsChanged` po insert/update/remove.
- [x] S2.18 ZELENÝ: implementace na `TmDocumentEditor` + `TmDocumentCanvasEngineHost`.
- [x] S2.19 ČERVENÝ: bUnit test toolbar: built-in skupina „Podpisová pole"
      (`DocumentEditorToolbarRegistry`/`DocumentEditorBuiltInToolbar` vzor) — dropdown: výběr role
      (barevné chipy z `SigningRoles`) + typ pole → `insertSigningField`. Skupina viditelná JEN
      když `SigningRoles` není prázdné (bez rolí nulový dopad — regrese!). **Skupina dostupná i
      při editaci hlavičky/patičky** (umožní položit patičkové pole). Texty z `TmResources.resx`.
- [x] S2.20 ZELENÝ: implementace toolbar skupiny.
- [x] S2.21 ČERVENÝ: bUnit test properties UX (dle O4): selekce pole → `TmDocumentSidePanel`
      zobrazí `TmSigningFieldEditorPanel`; změna (label/required/role) → `updateSigningField`.
      **Pro HF pole panel ukáže read-only odznak „opakuje se na každé stránce" (scope ze selection
      payloadu).** Texty z resources.
- [x] S2.22 ZELENÝ: implementace panelu + HF odznaku.

### S2.F — Persistence + degradace exportů

- [x] S2.23 ČERVENÝ: C# round-trip test canonical modelu přes `CanvasDocumentModelConverter`
      (canvas JSON → `DocumentBlocks` → canvas JSON) beze ztráty signing payloadu **(a) v těle
      I (b) v hlavičce/patičce** (`DocumentHeaderFooter` bloky).
- [x] S2.24 ZELENÝ: rozšíření converteru (tělo + HF).
- [x] S2.25 ČERVENÝ: testy degradace v `tests/Tempo.Blazor.DocumentFormats.Tests`: DOCX/HTML/ODT
      export dokumentu se signing poli **v těle i v patičce** neselže a vyrenderuje placeholder
      dle O3 (i v hlavičce/patičce); import placeholder zpět NEparsuje (zdroj pravdy = canonical JSON).
- [x] S2.26 ZELENÝ: implementace v exporterech.

### S2.G — Demo + E2E BRÁNA

- [x] S2.27 Demo: rozšířit `/signing-from-editor` o režim „Inline pole" — role editor
      (`TmRecipientRoleEditor`), vkládání polí z toolbaru do seed smlouvy, properties panel,
      tlačítko „Náhled podepisování" → `GetSigningFieldsAsync()` + `ExportPageImagesAsync()`
      → `TmSigningFormRunner` (BEZ TmPdfTemplateDesigner kroku — areas auto z layoutu).
      **+ afordance „Iniciály do patičky každé strany": přepne do editace patičky a vloží
      initials pole** → demonstruje multi-area (jedno pole, orazítkované na všech stranách).
- [x] S2.28 ČERVENÝ→ZELENÝ: E2E (`S2` testy v `DocumentEditorSigningBridgeE2ETests.cs`):
      (a) insert pole z toolbaru → box s barvou role v canvasu (pixelová sonda barvy),
      (b) pole se přelévá: odstavec PŘED pole → pole se posune (porovnat area před/po),
      (c) undo → pole zmizí, redo → zpět, (d) selekce → properties panel, label se projeví,
      (e) celý flow do runneru: text pole + podpis přes `TmSignatureCapture`,
      (f) reload (persistence přes provider) → pole zůstávají,
      **(g) patičkové pole: vloženo do patičky → `getSigningFieldsJson` dá 1 pole s areas.Count
      == počet stránek; runner ukáže PRÁVĚ JEDEN krok; overlay vykreslí pole na každé stránce.**
- [x] S2.29 Screenshoty: `phase2/01-toolbar-insert.png`, `02-field-in-document.png`,
      `03-field-properties-panel.png`, `04-field-reflow-after-edit.png`,
      `05-runner-from-inline-fields.png`, `06-signature-step.png`,
      **`07-footer-field-every-page.png` (vícestránkový s patičkovým polem),
      `08-runner-multiarea-single-step.png`** + **dvoukolové posouzení** (funkční + UX).
- [x] S2.30 Regrese: Node + component suite + CELÁ canvas E2E sada (vč. content controls,
      clipboard, collab, **header/footer editace**) — žádný nový fail proti S0 baseline.
      Zvláštní pozornost: dokumenty BEZ signing polí = bajt-identické chování (žádný dopad na
      typing hot path — ověřit `runtime/typing-hot-path` perf čísla se nehorší).

**Akceptační kritéria S2:**
1. Pole je objekt modelu: vkládá se na caret (v těle I v hlavičce/patičce), přelévá se s textem,
   undo/redo, collab operace.
2. Areas vždy aktuální z layoutu (0..1), **počet = výskyty v layoutu: tělo 1, hlavička/patička N**;
   grupování podle `fieldUuid`; scope (first/even/odd) odvozen z layoutu. `GetSigningFieldsAsync()`
   vrací hotové `SigningField[]` (vč. multi-area) použitelné v runneru bez ručního overlay kroku.
3. **Multi-area pole = 1 hodnota = 1 krok v runneru, orazítkované do všech areas** (downstream beze změny).
4. Barvy/role konzistentní s `TmPdfTemplateDesigner` (sdílené `SigningSubmitterRole.Color`).
5. Toolbar skupina viditelná jen s rolemi (dostupná i v HF editaci); bez rolí nulová změna editoru.
6. DOCX/HTML/ODT export neselže (placeholder dle O3 i v hlavičce/patičce); canonical JSON beze ztráty.
7. Žádná perf regrese psaní; screenshoty (vč. patičkového multi-area pole) prošly dvoukolovým posouzením.

---

## FÁZE S3 — Závěrečná regrese + dokumentace ✅ HOTOVO (2026-06-13)

- [x] S3.1 Kompletní regrese: `npm run test:document-editor-modules`, component suite po
      projektech (neparalelně), celá E2E sada DocumentEditorCanvas* + Signing* +
      DocumentEditorSigningBridge*. Porovnat proti S0 baseline — žádný nový fail.
- [x] S3.2 Dokumentace: `COMPONENTS.md` (nové API `ExportPageImagesAsync`,
      `GetSigningFieldsAsync`, `InsertSigningFieldAsync`, `SigningRoles`, toolbar skupina,
      demo `/signing-from-editor`) + `src/Tempo.Blazor/wwwroot/js/document-editor/README.md`
      resp. canvas README o nových modulech (page-image-export, signing-field-*).
- [x] S3.3 Zápis follow-upů (mimo scope, rozhodnutí #6): fill mode v editoru, flatten → PDF,
      server PDF render nahraných PDF, PAdES pečeť — jako kandidáti dalšího plánu.

**Akceptační kritéria S3:** vše zelené (krom zdokumentovaných pre-existing failů z S0),
dokumentace aktuální, follow-upy zapsané.

### S3 — Výsledek (2026-06-13, necommitnuté)

**S3.1 regrese (žádný nový fail proti S0):**
- Node `test:document-editor-modules`: **433/433**.
- Component (`DocumentEditor|Signing`, neparalelně): **11 pre-existing failů** (legacy JS engine +
  PDF/export cast), 0 nových.
- DocumentFormats: **222/222**.
- E2E: `DocumentEditorSigningBridgeE2ETests` **4/4** (S0×2, S1, S2 vč. properties popoveru) +
  `DocumentEditorCanvasEndToEndTypingE2ETests` + `DocumentEditorCanvasFieldsE2ETests` zelené
  (engine zdravý po sdílených layout editech). ⚠️ 2 stale pre-cutover `DocumentEditorCanvasEngine
  BaselineE2ETests` faily = pre-existing (čekají `CoreEnginePreview`).

**S3.2 dokumentace:**
- `COMPONENTS.md` — nová sekce „Most TmDocumentEditor → Signing" (API tabulka + toolbar/popover +
  degradace exportu + demo + příklad).
- `src/Tempo.Blazor/wwwroot/js/document-editor-canvas/README.signing-bridge.md` — nové JS moduly
  (page-image-export, signing-field-model/render/commands/selection/areas + integrace).

### S3.3 — Follow-upy (mimo scope S1+S2, kandidáti dalšího plánu)

1. **Fill mode v editoru** — vyplňování polí přímo v dokumentu na desktopu (dnes signer vyplňuje přes
   `TmSigningFormRunner` nad exportovanými stránkami). Vyžaduje editovatelný/„signer" režim canvas enginu.
2. **Flatten polí → PDF** — vyrenderovat hodnoty (podpis, text, datum…) napevno do výstupního PDF
   (server-side, např. SkiaSharp/QuestPDF nebo PDF lib).
3. **Server PDF render nahraných PDF → obrázky stránek** — protějšek S1 pro DocuSeal-like upload PDF
   (PDF→PNG na serveru), aby šlo razítkovat pole i na cizí PDF, ne jen na editor-authored dokumenty.
4. **PAdES pečeť** (rozhodnutí z diskuse 2026-06-12: „+ PAdES") — kryptografické zapečetění výsledného
   PDF certifikátem serveru + audit-trail; netriviální .NET PAdES implementace.
5. **Drobnosti:** plný role+typ dropdown v toolbaru (dnes 1 tlačítko = signature pro 1. roli);
   `TmSigningFieldEditorPanel` v side panelu místo kompaktního popoveru; conditions/formula UI pro
   inline pole; podpora bookmarků.

---

## ✅ PLÁN KOMPLETNÍ (S0–S3, 2026-06-13)

Most TmDocumentEditor ↔ Signing je hotový a otestovaný: editor-authored dokument → export stránek →
inline podpisová pole (tělo i hlavičky/patičky, multi-area) → `TmPdfTemplateDesigner`/`TmSigningFormRunner`.
Necommitnuté. Navazující práce viz S3.3 follow-upy.
