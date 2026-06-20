# Canvas engine - Faze 21: Accessibility, lokalizace a kvalita klávesnice (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 21** · Stav: automatizované gate hotové, manual NVDA/VoiceOver čeká · Priorita: P1

## Proč

Canvas sám není přístupný — engine musí udržovat sémantický a11y mirror pro screen readery, logické reading order (vč. bidi/RTL), live region, forced-colors, keyboard-only editaci, focus management a ARIA přes lokalizér. Reuse R.4.7 a11y.

## Cílový stav

- Accessibility mirror DOM pro screen readery (text, odstavce, nadpisy, tabulky, komentáře, revize).
- Logické reading order vč. bidi/RTL.
- Live region pro caret granularity, find, comments, save state.
- Forced-colors/high contrast.
- Keyboard-only editing full smoke; focus management mezi canvas/toolbar/panely/dialogy.
- ARIA labels přes `ITmLocalizer`; CZ/EN klíče; manual NVDA/VoiceOver gate.

## Clean-room
- [x] Vlastní; bez ONLYOFFICE kódu.

## Znovupoužití
- [ ] `core-engine/a11y.mjs` (R.4.7: root role=document, heading role/aria-level, accessible textbox, live region kontext kurzoru).
- [ ] C# `TmDocumentEditorLiveRegion`; `ITmLocalizer`.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/a11y/accessibility-mirror.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/a11y/live-region.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/a11y/__tests__/mirror.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasAccessibilityE2ETests.cs
```

## DoD
- [x] Automatizovaná ARIA brána zelená; keyboard-only smoke prochází.
- [ ] Manual NVDA/VoiceOver gate (follow-up, zaznamenat).

## Faze 21.1: Accessibility mirror

### 21.1.1 RED
- [x] `mirror.test.mjs`: mirror DOM odráží text/odstavce/nadpisy (role+aria-level)/tabulky/komentáře/revize; aktualizuje se s modelem.

### 21.1.2 GREEN + akceptace
- [x] `accessibility-mirror.mjs` (reuse a11y.mjs); accessible textbox.

## Faze 21.2: Reading order + bidi/RTL

### 21.2.1 RED
- [x] Logické reading order v mirroru vč. bidi/RTL (vizuální vs logické pořadí).

### 21.2.2 GREEN + akceptace
- [x] Reading order z modelu (ne z vizuálního layoutu); reuse bidi.

## Faze 21.3: Live region

### 21.3.1 RED
- [x] Live region ohlásí caret granularity (znak/slovo/řádek), find result, comment, save state.

### 21.3.2 GREEN + akceptace
- [x] `live-region.mjs` (reuse R.4.7 + TmDocumentEditorLiveRegion).

## Faze 21.4: Forced-colors + keyboard-only + focus

### 21.4.1 RED
- [x] Forced-colors/high contrast render; keyboard-only editing full smoke.
- [x] Focus management canvas↔toolbar↔panely↔dialogy.

### 21.4.2 GREEN + screenshot + akceptace
- [x] Forced-colors handling; E2E keyboard-only smoke; screenshot forced-colors.
- [x] Focus trap v dialozích.

## Faze 21.5: Lokalizace + manual gate

### 21.5.1 RED
- [x] ARIA labels přes `ITmLocalizer`; CZ/EN klíče kompletní.

### 21.5.2 GREEN + akceptace fáze 21
- [x] Lokalizační klíče; automatizovaná ARIA brána zelená.
- [ ] Manual NVDA/VoiceOver gate zaznamenán jako follow-up.

## Poznámky
- Mirror nesmí být persistence model — jen projekce canvas modelu.
- Math (E8) a tabulky (14) mají vlastní a11y popisy (MathML mirror, table headers).
