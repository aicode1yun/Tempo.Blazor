# Canvas engine - E1: Numbering, multilevel lists a list styly (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **E1** · Stav: hotovo · Priorita: P1 (nad rámec legacy)

## Proč

Legacy měl jen jednoduché bullet/numbered toggle. Word/GDocs/OnlyOffice mají plné numbering definitions: víceúrovňové seznamy, formáty (decimal/roman/letter/bullet/legal), start-at, restart/continue, list styly. Bez toho nelze tvrdit paritu.

## Cílový stav

- Numbering definition: abstract num, level 0–8, format (decimal, lower/upper-roman, lower/upper-letter, bullet, none), text template (`%1.%2`), start-at, suffix, indent/hang per level.
- Apply bullet/numbered; change level Tab/Shift+Tab; format z pickeru.
- Restart numbering; continue numbering; set numbering value.
- Legal/multilevel (1, 1.1, 1.1.1) s návazností mezi odstavci.
- List label layout bez overlapu; list style reference + define-new-list-style.

## Clean-room
- [x] Numbering engine vlastní; ONLYOFFICE `Numbering/*` jen inspirace (AbstractNum/Num/Lvl koncept).

## Znovupoužití
- [ ] `core-engine/list-model.mjs`, `list-layout.mjs` (rozšířit z basic toggle na definitions).
- [ ] Faze 10.2 list toggle/nesting; paragraph layout (Faze 6) pro label.

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/lists/numbering-definition.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/lists/numbering-engine.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/lists/__tests__/numbering-sequence.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/lists/__tests__/multilevel.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasNumberingE2ETests.cs
```

## DoD
- [x] Number sequence stabilní po vložení/smazání/přesunu odstavce.
- [x] Save/reload zachová numbering; undo gate.
- [x] Multilevel vypadá jako Word (čísla sedí, hanging indent).

## Faze E1.1: Numbering definition model

### E1.1.1 RED
- [x] Abstract num + 9 levelů; format, text template, start-at, suffix, indent/hang per level; Num instance odkazuje abstract + overrides.

### E1.1.2 GREEN + akceptace
- [x] `numbering-definition.mjs`; validace; converter round-trip.

## Faze E1.2: Numbering engine (sekvence)

### E1.2.1 RED
- [x] `numbering-sequence.test.mjs`: engine spočítá label per odstavec dle pořadí; vložení/smazání/přesun přepočítá; restart/continue/set-value.

### E1.2.2 GREEN + akceptace
- [x] `numbering-engine.mjs`; counter resolution per level; restart/continue/set-value ops.

## Faze E1.3: Apply + change level + format picker

### E1.3.1 RED
- [x] Apply bullet/numbered; Tab/Shift+Tab mění level (0–8); format picker mění formát levelu.

### E1.3.2 GREEN + screenshot + akceptace
- [x] Commandy; E2E seznam + change level + format.

## Faze E1.4: Multilevel + label layout

### E1.4.1 RED
- [x] `multilevel.test.mjs`: legal 1/1.1/1.1.1 návaznost; label layout (šířka, zarovnání, hanging) bez overlapu s textem.

### E1.4.2 GREEN + screenshot + akceptace
- [x] Multilevel template render; label layout; E2E multilevel screenshot.

## Faze E1.5: List style + perzistence

### E1.5.1 RED
- [x] List style reference; define-new-list-style; save/reload zachová numbering.

### E1.5.2 GREEN + screenshot + akceptace fáze E1
- [x] List style; provider save/reload; E2E restart numbering + reload; undo gate.
- [x] Screenshot: multilevel seznam vypadá jako Word.

## Poznámky
- DOCX numbering (numbering.xml) roundtrip kde provider podporuje = Faze 19 smoke.
- Provázání s heading numbering (number heading) = E5/E4 follow-up.
