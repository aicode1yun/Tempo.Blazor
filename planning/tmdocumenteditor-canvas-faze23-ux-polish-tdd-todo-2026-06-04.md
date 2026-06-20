# Canvas engine - Faze 23: UX/UI polish gate (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 23** · Stav: agent UX review + loading/error screenshot gate hotovo; human smoke čeká · Priorita: P1 (gate před cutoverem)

## Proč

Sběrná kvalitní brána: editor musí působit profesionálně (hustota, stíny, okraje, ostrost textu, barvy selection/caret/comments v light/dark, handly, žádné cards-in-cards, text nepřetéká, mobilní toolbar). Bez ní může být funkčně hotovo, ale vizuálně rozpadlé.

## Cílový stav

- Editor surface density, page shadows, margins, toolbar spacing reviewed.
- Canvas text sharpness na 1x/2x DPR.
- Selection/caret/comment/revision barvy v light/dark.
- Image/table handles a context menus pro touch/mouse.
- Žádné cards-inside-cards; text nepřetéká tlačítka/dropdowny.
- Mobilní toolbar overflow použitelný; empty/loading/error states profesionální.
- Screenshot galerie top scénářů přijata agent UX/UI review + human smoke před cutoverem.

## Clean-room
- [x] N/A (UX gate).

## Znovupoužití
- [x] Screenshot helpery (Faze 2); všechny feature screenshoty z fází 5–22 + E1–E12.
- [x] `planning/document-editor-ui-ux-redesign-analysis.md`, `document-editor-strict-human-e2e-ux-quality-todo-2026-05-18.md` jako reference kritérií.

## Doporučené nové soubory

```text
tests/Tempo.Blazor.E2E/DocumentEditorCanvasUxGalleryE2ETests.cs
tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/_gallery/   (výstup galerie)
```

## DoD
- [x] Každá položka checklistu má screenshot + agent verdikt. (Top 20, DPR, dark, mobile, empty i loading/error gate hotovo.)
- [ ] Human smoke přijat uživatelem (zaznamenat).

## Faze 23.1: Surface density + text sharpness

### 23.1.1
- [x] Density/shadows/margins/toolbar spacing review na všech viewportech; text sharpness 1x/2x.
- [x] Screenshot + verdikt.

## Faze 23.2: Barvy v light/dark

### 23.2.1
- [x] Selection/caret/comment/revision barvy v light i dark; kontrast.
- [x] Screenshot light + dark + verdikt.

## Faze 23.3: Handles + context menus + touch/mouse

### 23.3.1
- [x] Image/table handles ostré; context menus čitelné; touch i mouse targets.
- [x] Screenshot + verdikt.

## Faze 23.4: Layout hygiene

### 23.4.1
- [x] Žádné cards-inside-cards; text nepřetéká tlačítka/dropdowny; empty state.
- [x] Loading/error states mají samostatný screenshot gate.
- [x] Screenshot + verdikt pro layout hygiene a empty state.

## Faze 23.5: Mobilní + galerie + human smoke

### 23.5.1 Akceptace fáze 23
- [x] Mobilní toolbar overflow použitelný; screenshot galerie top scénářů (legacy-parity + rozšířené).
- [x] Agent UX/UI review všech scénářů.
- [ ] Human smoke přijat uživatelem před cutoverem (Faze 25).

## Poznámky
- Tahle fáze běží průběžně (každá feature fáze přidává screenshot do galerie), finální gate je tady.
- Kritéria sjednotná se `strict-human-e2e-ux-quality` plánem.
- Agent review artefakty: `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/_gallery/2026-06-04/Phase23_UxGallery_ReviewsTopTwentyCanvasScenarios/20260606173030351/manifest.json`, `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/_gallery/2026-06-04/Phase23_UxPolish_VerifiesDprDarkMobileAndStateSurfaces/20260606173453140/manifest.json` a `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/_gallery/2026-06-04/Phase23_UxPolish_CapturesLoadingAndErrorStateScreenshots/20260606173606008/manifest.json`.
