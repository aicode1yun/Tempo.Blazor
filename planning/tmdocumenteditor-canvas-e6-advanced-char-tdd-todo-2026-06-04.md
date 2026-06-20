# Canvas engine - E6: Pokročilé znakové formátování a change case (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **E6** · Stav: hotovo · Priorita: P2 (nad rámec legacy)

## Proč

Subscript/superscript, small caps, character spacing/scale, double strikethrough a change case jsou běžné Word/GDocs funkce. Legacy měl jen základní marks. Rozšiřuje inline formatting z Faze 9.

## Cílový stav

- Subscript, superscript (baseline shift + font scale), small caps, all caps, double strikethrough.
- Character spacing (expanded/condensed), character scale, kerning toggle.
- Change case (UPPER, lower, Sentence, Capitalize Each Word, tOGGLE).
- Increase/decrease font size step; clear character formatting (sjednotit s Faze 9.4).
- Canvas render: sub/superscript baseline, small caps glyph scaling, spacing mezi glyphy.

## Clean-room
- [x] Vlastní; bez ONLYOFFICE kódu.

## Znovupoužití
- [x] `commands/inline-format.mjs` (Faze 9); `layout/font-metrics.mjs` + segment-style pro baseline/scale/spacing.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/advanced-char.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/layout/__tests__/char-metrics.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/commands/__tests__/advanced-char.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasAdvancedCharE2ETests.cs
```

## DoD
- [x] Baseline/scale/spacing metriky testované číselně.
- [x] Save/reload; undo gate.

## Faze E6.1: Sub/superscript + small caps + double strike

### E6.1.1 RED
- [x] `char-metrics.test.mjs`: subscript baseline shift dolů + scale; superscript nahoru; small caps glyph scaling; double strikethrough 2 čáry.

### E6.1.2 GREEN + screenshot + akceptace
- [x] `advanced-char.mjs` marks + render; E2E H2O subscript, x^2 superscript, small caps.

## Faze E6.2: Character spacing/scale/kerning

### E6.2.1 RED
- [x] `advanced-char.test.mjs`: spacing expanded/condensed mění advance; character scale šíří glyphy; kerning toggle.

### E6.2.2 GREEN + screenshot + akceptace
- [x] Spacing/scale v layoutu; E2E expanded spacing.

## Faze E6.3: Change case + font size step

### E6.3.1 RED
- [x] Change case 5 variant; increase/decrease font size step; clear character formatting.

### E6.3.2 GREEN + screenshot + akceptace fáze E6
- [x] Change case command (transformace textu, undoable); font step; E2E change case + reload; undo gate.

## Poznámky
- Text effects (shadow/outline/glow) jako P3 follow-up.
- Sub/superscript se musí správně skládat s math (E8) — math má vlastní script layout.
