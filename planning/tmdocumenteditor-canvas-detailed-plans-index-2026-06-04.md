# TmDocumentEditor Canvas engine - index detailních plánů

Datum založení: 2026-06-04
Nadřazený plán: `planning/tmdocumenteditor-canvas-onlyoffice-inspired-engine-tdd-todo-2026-06-04.md`
Účel: rozpad **všech velkých fází** master canvas plánu (core 0–26 i E1–E12) do samostatných detailních TDD + E2E plánů ve formátu jako E7/E8.

Pořadí = implementační (dle závislostí: core 0–16 → E1–E6 → core 17–18 → E7–E12 → core 19–24).

Malé/setup/gate fáze (0 baseline, 1 architektonický spike, 25 cutover, 26 soak) zůstávají v master plánu a nemají samostatný detailní plán.

**Stav: 33/33 detailních plánů hotovo (vč. dříve vytvořených E7/E8).** Vše navrženo, čeká na implementaci; žádný kód canvas enginu zatím nevznikl.

## Detailní plány (implementační pořadí)

### Jádro enginu (core)

- [x] Faze 2 — Test harness a screenshot evaluator → [faze02](tmdocumenteditor-canvas-faze02-test-harness-tdd-todo-2026-06-04.md)
- [x] Faze 3 — Blazor host a render flag → [faze03](tmdocumenteditor-canvas-faze03-host-flag-tdd-todo-2026-06-04.md)
- [x] Faze 4 — Canonical canvas model a converter → [faze04](tmdocumenteditor-canvas-faze04-model-converter-tdd-todo-2026-06-04.md)
- [x] Faze 5 — Canvas render pipeline → [faze05](tmdocumenteditor-canvas-faze05-render-pipeline-tdd-todo-2026-06-04.md)
- [x] Faze 6 — Text measurement, line breaking, pagination → [faze06](tmdocumenteditor-canvas-faze06-text-layout-pagination-tdd-todo-2026-06-04.md)
- [x] Faze 7 — Hit testing, caret, selection → [faze07](tmdocumenteditor-canvas-faze07-hittest-caret-selection-tdd-todo-2026-06-04.md)
- [x] Faze 8 — Input pipeline, IME, immediate typing → [faze08](tmdocumenteditor-canvas-faze08-input-ime-tdd-todo-2026-06-04.md)
- [x] Faze 9 — Command dispatcher a inline formatting → [faze09](tmdocumenteditor-canvas-faze09-commands-inline-format-tdd-todo-2026-06-04.md)
- [x] Faze 10 — Paragraph commands, styly, ruler → [faze10](tmdocumenteditor-canvas-faze10-paragraph-ruler-tdd-todo-2026-06-04.md)
- [x] Faze 11 — Clipboard → [faze11](tmdocumenteditor-canvas-faze11-clipboard-tdd-todo-2026-06-04.md)
- [x] Faze 12 — History, dirty state, save, autosave → [faze12](tmdocumenteditor-canvas-faze12-history-save-tdd-todo-2026-06-04.md)
- [x] Faze 13 — Toolbar shell, mini toolbar, context menu, spellcheck → [faze13](tmdocumenteditor-canvas-faze13-toolbar-contextmenu-spellcheck-tdd-todo-2026-06-04.md)
- [x] Faze 14 — Tables → [faze14](tmdocumenteditor-canvas-faze14-tables-tdd-todo-2026-06-04.md)
- [x] Faze 15 — Images and drawings → [faze15](tmdocumenteditor-canvas-faze15-images-drawings-tdd-todo-2026-06-04.md)
- [x] Faze 16 — Headers, footers, fields, notes, page settings → [faze16](tmdocumenteditor-canvas-faze16-headers-footers-notes-tdd-todo-2026-06-04.md)
- [x] Faze 17 — Comments, revisions, restricted editing → [faze17](tmdocumenteditor-canvas-faze17-comments-revisions-tdd-todo-2026-06-04.md)
- [x] Faze 18 — Search, replace, outline, bookmarks, TOC navigation → [faze18](tmdocumenteditor-canvas-faze18-search-outline-toc-tdd-todo-2026-06-04.md)
- [x] Faze 19 — Import/export a externí formáty → [faze19](tmdocumenteditor-canvas-faze19-import-export-tdd-todo-2026-06-04.md)
- [x] Faze 20 — Collaboration a offline → [faze20](tmdocumenteditor-canvas-faze20-collaboration-offline-tdd-todo-2026-06-04.md)
- [x] Faze 21 — Accessibility, lokalizace, klávesnice → [faze21](tmdocumenteditor-canvas-faze21-accessibility-tdd-todo-2026-06-04.md)
- [x] Faze 22 — Performance a velké dokumenty → [faze22](tmdocumenteditor-canvas-faze22-performance-tdd-todo-2026-06-04.md)
- [x] Faze 23 — UX/UI polish gate → [faze23](tmdocumenteditor-canvas-faze23-ux-polish-tdd-todo-2026-06-04.md)
- [x] Faze 24 — Parity regression suite → [faze24](tmdocumenteditor-canvas-faze24-parity-regression-tdd-todo-2026-06-04.md)

### Rozšířená parita (E)

- [x] E1 — Numbering, multilevel lists, list styly → [e1](tmdocumenteditor-canvas-e1-numbering-lists-tdd-todo-2026-06-04.md)
- [x] E2 — Tab stops a pravítko → [e2](tmdocumenteditor-canvas-e2-tabstops-ruler-tdd-todo-2026-06-04.md)
- [x] E3 — Sekce, sloupce, line numbering, page setup → [e3](tmdocumenteditor-canvas-e3-sections-columns-tdd-todo-2026-06-04.md)
- [x] E4 — Styly: management a typy stylů → [e4](tmdocumenteditor-canvas-e4-styles-tdd-todo-2026-06-04.md)
- [x] E5 — Fields, cross-reference, captions, bibliografie → [e5](tmdocumenteditor-canvas-e5-fields-crossref-tdd-todo-2026-06-04.md)
- [x] E6 — Pokročilé znakové formátování a change case → [e6](tmdocumenteditor-canvas-e6-advanced-char-tdd-todo-2026-06-04.md)
- [x] E7 — Tvary, textová pole, čáry, grafy → [e7](tmdocumenteditor-canvas-e7-shapes-textboxes-charts-tdd-todo-2026-06-04.md)
- [x] E8 — Matematika / rovnice → [e8](tmdocumenteditor-canvas-e8-math-equations-tdd-todo-2026-06-04.md)
- [x] E9 — Content controls / formuláře → [e9](tmdocumenteditor-canvas-e9-content-controls-forms-tdd-todo-2026-06-04.md)
- [x] E10 — Autocorrect, autoformat, format painter, symboly → [e10](tmdocumenteditor-canvas-e10-autocorrect-formatpainter-tdd-todo-2026-06-04.md)
- [x] E11 — View modes, zoom, print → [e11](tmdocumenteditor-canvas-e11-viewmodes-print-tdd-todo-2026-06-04.md)
- [x] E12 — Hyphenation, page background, pokročilé tabulky → [e12](tmdocumenteditor-canvas-e12-hyphenation-advtables-tdd-todo-2026-06-04.md)

## Konvence detailních plánů

Každý plán má: hlavičku (proč / cílový stav / clean-room pravidla / znovupoužití / testovací soubory / DoD), fáze X.Y (RED test první → implementace → E2E+screenshot → akceptace), per-fáze save/reload a undo gates, odkaz zpět na master fázi a zápis řádků do parity suite (master Faze 24).

## Doporučené implementační pořadí (z master plánu)

core 0–16 → E1–E6 → core 17–18 → E7–E12 → core 19–23 → core 24 parity → 25 cutover → 26 soak.
Dvoustupňová akceptace: **legacy-parity preview** (0–24) vs **full-quality** (+ E1–E12).
