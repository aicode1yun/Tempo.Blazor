# TmDocumentEditor – UX/UI Redesign Analýza: Od blokového editoru k Word-like online editoru

**Datum:** 2026-05-11  
**Autor:** UX/UI Expert Analysis  
**Cíl:** Přeměnit `TmDocumentEditor` z editoru s blokovým mentálním modelem na vizuálně i interakčně Word-like online editor při zachování stávajícího interního JSON modelu.  
**Metodika:** TDD, malé inkrementální kroky, po každém kroku zelené testy.

---

## 1. Executive Summary

Současný `TmDocumentEditor` je **technicky page-oriented blokový editor**, ale **mentálně a vizuálně působí jako Notion-style block editor**. Uživatel vidí:

- Každý blok jako samostatnou "kartičku" s borderem.
- `<textarea>` pro každý odstavec/nadpis/seznam s vlastním rámečkem.
- Aktivní blok zvýrazněný modrým outline (box-shadow).
- Formuláře pro úpravu obrázků přímo pod obrázkem.
- Toolbar jako statický ribbon bez přepínání záložek.

**Cílový stav** je editor, který vizuálně a interakčně evokuje Microsoft Word Online / Google Docs / OnlyOffice:

- Jedna souvislá stránka bez viditelných hranic bloků.
- Plynulý textový povrch (textarea vypadá jako plain text, ne jako input).
- Plnohodnotný ribbon se záložkami, které mění obsah toolbaru.
- Status bar dole (strana, počet slov, zoom).
- Ruler (pravítko) pro vizuální kontrolu marginů.
- Modální/lightweight overlay dialogy pro Insert operace.
- Inline formátovací toolbar (případně) při výběru textu.
- Realistická A4 stránka se stínem na šedém pozadí.

> **Technická realita:** Skutečný WYSIWYG word processor s arbitrárním inline výběrem a contenteditable engine by vyžadoval kompletní přepsání render vrstvy (z `<textarea>` na `contenteditable` nebo vlastní Canvas/WebGL renderer). Tato analýza pracuje s **kompromisem**: zachováme stávající blokový model a `<textarea>` per block, ale vizuálně je „schováme“ za word-like fasádu. To nám umožní dramaticky zlepšit UX při rozumném implementačním úsilí.

---

## 2. Současný stav – Detailní rozbor problémů

### 2.1 Vizuální problémy (UI)

| Oblast | Současný stav | Problém | Word-like očekávání |
|--------|--------------|---------|---------------------|
| **Stránka** | Bílý obdélník s `box-shadow: var(--tm-shadow-md)`, šířka `52rem`, padding `var(--tm-space-6)` | Příliš široká, nepůsobí jako A4; stín je příliš subtilní; chybí „papírový" feeling | A4 proportions (~210×297 ratio při 96 DPI = ~794×1123 px), realistický drop shadow, šedé pozadí editoru |
| **Bloky** | Každý blok má `.tm-document-editable-block` s `border: 1px solid transparent` a aktivní má modrý border + box-shadow | Bloky vypadají jako samostatné entity; aktivní blok připomíná Notion | Žádné viditelné hranice mezi bloky; aktivace je indikována jen caret nebo jemným outline |
| **Textarea** | `.tm-document-editable-text` má `border: 1px solid var(--tm-color-border)`, `border-radius`, `resize: vertical` | Vypadá jako formulářový input, ne jako dokumentový text | Borderless, transparentní background, žádný resize handle; plynulý text |
| **Toolbar** | Statický ribbon se 6 záložkami (všechny zobrazeny najednou jako jedna řada tlačítek) | Záložky nefungují – vše je vidět naráz; chybí skupiny (Font, Paragraph...); chybí dropdowny | Klik na záložku mění obsah ribbonu; skupiny s popisky; split-buttony; dropdowny (font, size) |
| **Nadpisy** | H1–H6 mají všechny stejnou velikost (`var(--tm-font-size-lg)`) kromě H1 a H2 | Nadpisy nejsou dostatečně diferencované | H1=24pt, H2=18pt, H3=14pt, H4=12pt, H5=11pt, H6=10pt (Calibri-like) |
| **Obrázky** | Editor formuláře přímo pod obrázkem (URL, Alt, Caption, Width...), pak toolbar tlačítka | Příliš mnoho inline UI noise kolem obrázku | Inline resize handles; caption jako figcaption; properties v sidebaru nebo modalu |
| **Insert UI** | `.tm-document-insert-panel` jako box pod dokumentem s 4 tlačítky | Nepůsobí profesionálně; chybí insert table picker | Dropdowny z toolbaru; modální dialogy; grid picker pro tabulky |
| **Status** | Titulek a status v záhlaví stránky; dirty/save message v ribbonu | Status je rozptýlený; chybí informace o pozici kurzoru | Status bar dole jako ve Wordu (strana X z Y, slova, jazyk, zoom slider) |
| **Revisions** | `.tm-document-revisions-panel` jako box pod dokumentem | Zabírá místo v dokumenu; není to Word-like | Review pane vpravo nebo jako overlay; accept/reject inline |
| **Footnotes** | `.tm-document-notes-editor` jako box pod dokumentem | Ruší flow dokumentu | Word-like footnote area na konci stránky nebo bottom pane |
| **Comments** | `.tm-document-editor__comment-rail` vpravo – OK | Dobré umístění, ale vizuálně příliš „boxy" | Čistší karty, lepší typografie, avatary autorů, barevné pruhy |

### 2.2 Interakční problémy (UX)

| Problém | Dopad na uživatele | Word-like očekávání |
|---------|-------------------|---------------------|
| Formátování (bold/italic/underline) se aplikuje na **celý blok**, ne na výběr textu | Uživatel nemůže ztučnit jedno slovo v odstavci | Formátování se aplikuje na aktuální výběr (selection) nebo celý blok, pokud není výběr |
| Enter v odstavci vytvoří **nový blok**, ale nepracuje jako ve Wordu (new paragraph) | Překvapivé chování pro Word uživatele | Enter = nový odstavec; Shift+Enter = nový řádek ve stejném bloku (soft break) |
| Backspace v prázdném odstavci sloučí s předchozím | OK chování, ale vizuálně je to zřetelné „skákání" bloku | Plynulé sloučení bez vizuálního skoku |
| Chybí **font family / font size** výběr | Uživatel nemůže změnit písmo nebo velikost | Dropdowny v ribbonu pro font a size |
| Chybí **text alignment** (left/center/right/justify) | Odstavce jsou vždy left-aligned | Skupina Paragraph v ribbonu |
| Chybí **line spacing** a **paragraph spacing** | Nelze nastavit mezery mezi odstavci | Ovládání v ribbonu nebo dialogu |
| Chybí **text color / highlight color** | Žádné barevné formátování textu | Color picker v ribbonu |
| Chybí **subscript / superscript / strikethrough** | Omezené formátování | Ribbon tlačítka |
| Chybí **indent / outdent** pro odstavce (ne jen listy) | Nelze odsadit odstavec | Ribbon tlačítka Increase/Decrease Indent |
| Chybí **find & replace** | Základní funkce textového editoru chybí | Ctrl+F / Ctrl+H dialog |
| Chybí **undo/redo history** vizualizace | Uživatel neví, co undo provede | Dropdown u undo tlačítka s historií |
| Toolbar záložky **nejsou interaktivní** – vše je vidět najednou | Ribbon neplní svou funkci | Klik na záložku mění ribbon obsah |

### 2.3 Pozitiva, která zůstanou

- Page-oriented layout se stránkou uprostřed ✅
- Comments rail vpravo ✅
- Version panel vpravo ✅
- Command stack (undo/redo) ✅
- Track changes model ✅
- Header/footer model ✅
- Footnote/endnote model ✅
- Image upload/provider model ✅
- Offline draft model ✅
- Audit model ✅

---

## 3. Cílový design – Word-like specifikace

### 3.1 Celková kompozice (layout)

```
┌─────────────────────────────────────────────────────────────┐
│ [Ribbon Tabs: Home | Insert | Layout | References | Review │ View ]  ← sticky
├─────────────────────────────────────────────────────────────┤
│ [Ribbon Content – mění se podle záložky]                    │  ← sticky
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ [Ruler: horizontal]                                 │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │                                                     │   │
│  │  Header area (subtle, grey)                        │   │
│  │                                                     │   │
│  │  ┌───────────────────────────────────────────────┐ │   │
│  │  │                                               │ │   │
│  │  │   DOCUMENT CONTENT                            │ │   │
│  │  │   (continuous text flow, no block borders)    │ │   │
│  │  │                                               │ │   │
│  │  │   Paragraphs, headings, lists, tables,        │ │   │
│  │  │   images inline...                            │ │   │
│  │  │                                               │ │   │
│  │  └───────────────────────────────────────────────┘ │   │
│  │                                                     │   │
│  │  Footer area (subtle, grey)                        │   │
│  │                                                     │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │ [Ruler: vertical]                                   │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  (pozadí mimo stránku: šedé #f3f4f6 / var(--tm-color-surface-secondary))
├─────────────────────────────────────────────────────────────┤
│ [Status Bar]  Page 1 of 1  |  Words: 42  |  CS  |  [100%]  │  ← sticky bottom
└─────────────────────────────────────────────────────────────┘
```

- **Page canvas:** Šedé pozadí editoru (`--tm-color-surface-secondary`), bílá stránka uprostřed.
- **Stránka:** A4 proportions, white background, realistický drop-shadow (`0 4px 24px rgba(0,0,0,0.12)`), padding odpovídající marginům (např. 2.54cm = ~96px default).
- **Ruler:** Horizontální nad stránkou, vertikální vlevo (volitelně). Ukazuje marginy, odsazení, tab stop.
- **Status bar:** Přidat nový pruh dole s informacemi.

### 3.2 Ribbon – Detailní specifikace záložek

#### Tab: Home

```
┌─────────────┬──────────────────────────────┬─────────────────────┬───────────┬────────────┐
│ Clipboard   │ Font                         │ Paragraph           │ Styles    │ Editing    │
│ ┌───┬───┐  │ [Font▼] [Size▼] [B][I][U]   │ [Align▼] [Spacing▼] │ [Heading  │ [Find▼]    │
│ │💾 │📋 │  │ [Color🎨][Highlight🖍️]       │ [Indent▼][List▼]    │ 1 ▼]      │ [Replace▼] │
│ └───┴───┘  │ [Sub][Super][Strikethrough]  │ [Line spacing▼]     │           │            │
│ Save Paste  │ Clear formatting             │ Borders & Shading   │           │            │
└─────────────┴──────────────────────────────┴─────────────────────┴───────────┴────────────┘
```

**Clipboard group:**
- Save (disk icon)
- Undo / Redo (malé šipky vedle sebe)
- Paste (disabled dokud není clipboard content)

**Font group:**
- Font family dropdown (Arial, Calibri, Times New Roman... + custom)
- Font size dropdown (8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 28, 32, 36, 48, 72)
- Bold, Italic, Underline (toggle buttons s aktivním stavem)
- Font color (A s barevným pruhem pod ním)
- Highlight color (marker icon)
- Subscript, Superscript, Strikethrough
- Clear formatting (eraser)

**Paragraph group:**
- Align Left, Center, Right, Justify (toggle group)
- Line spacing dropdown (1, 1.15, 1.5, 2, 2.5, 3)
- Space Before / Space After (malé inputy)
- Increase / Decrease indent
- Bullets / Numbering dropdowns
- Borders & Shading (simplified)

**Styles group:**
- Quick styles: Normal, Heading 1–4 (buttony)
- Dropdown pro více stylů

**Editing group:**
- Find / Replace dropdown

#### Tab: Insert

```
┌─────────────┬─────────────────────┬──────────────┬─────────────┬─────────────┐
│ Pages       │ Tables              │ Illustrations│ Links       │ Comments    │
│ [CoverPage] │ [Table ▼]           │ [Picture ▼]  │ [Link 🔗]   │ [Comment 💬]│
│ [BlankPage] │ [Excel Spreadsheet] │ [Shapes ▼]   │ [Bookmark 🔖]│             │
│ [PageBreak] │                     │ [Chart ▼]    │ [Cross-ref ▼]│             │
└─────────────┴─────────────────────┴──────────────┴─────────────┴─────────────┘
```

- **Table:** Dropdown s grid pickerem (hover myší vybírá rozměry 1×1 až 10×8)
- **Picture:** URL vs Upload vs Clipboard submenu
- **Shapes:** Základní shapes (rectangle, ellipse, line, arrow)
- **Comments:** Add comment

#### Tab: Layout

```
┌─────────────────┬─────────────────────┬─────────────────┬───────────────────┐
│ Page Setup      │ Paragraph           │ Arrange         │ Page Background   │
│ [Margins ▼]     │ [Indent ▼]          │ [Position ▼]    │ [Watermark ▼]     │
│ [Orientation ▼] │ [Spacing Before/After│ [Wrap Text ▼]  │ [Page Color ▼]    │
│ [Size ▼]        │                     │ [Bring Fwd ▼]   │                   │
│ [Columns ▼]     │                     │ [Send Back ▼]   │                   │
│ [Breaks ▼]      │                     │                 │                   │
└─────────────────┴─────────────────────┴─────────────────┴───────────────────┘
```

#### Tab: References

```
┌─────────────────┬─────────────────────┬─────────────────┐
│ Table of Contents│ Footnotes          │ Captions        │
│ [TOC ▼]         │ [Insert Footnote]   │ [Insert Caption ▼]│
│ [Update Table]  │ [Insert Endnote]    │                 │
│                 │ [Next Footnote]     │                 │
└─────────────────┴─────────────────────┴─────────────────┘
```

#### Tab: Review

```
┌─────────────────┬─────────────────────┬─────────────────┬───────────────────┐
│ Proofing        │ Comments            │ Tracking        │ Changes           │
│ [Spelling ▼]    │ [New Comment]       │ [Track Changes ▼]│ [Accept ▼]        │
│ [Word Count]    │ [Delete Comment ▼]  │ [Show Markup ▼] │ [Reject ▼]        │
│ [Thesaurus]     │ [Previous/Next]     │ [Reviewing Pane ▼]│ [Previous/Next] │
└─────────────────┴─────────────────────┴─────────────────┴───────────────────┘
```

#### Tab: View

```
┌─────────────────┬─────────────────────┬─────────────────┐
│ Views           │ Show                │ Zoom            │
│ [Print Layout ☑]│ [Ruler ☑]           │ [Zoom ▼]        │
│ [Web Layout]    │ [Gridlines]         │ [100%]          │
│ [Outline]       │ [Document Map]      │ [One Page]      │
│ [Draft]         │ [Thumbnails]        │ [Page Width]    │
└─────────────────┴─────────────────────┴─────────────────┘
```

### 3.3 Page Surface – Vizuální specifikace

#### Stránka (Page Canvas)

```css
.tm-document-editor__page-surface {
    /* A4 at 96 DPI */
    width: 210mm;        /* ~794px */
    min-height: 297mm;   /* ~1123px */
    padding: 25.4mm;     /* ~96px default margins */
    background: #ffffff;
    box-shadow: 0 4px 24px rgba(0, 0, 0, 0.12), 0 0 1px rgba(0, 0, 0, 0.08);
    border: none;
    border-radius: 0;
    margin: 0 auto;
}

.tm-document-editor__workspace {
    background: var(--tm-color-surface-secondary); /* šedé pozadí mimo stránku */
}
```

#### Ruler

- Horizontální ruler nad stránkou: šedý pruh (`--tm-color-surface-elevated`), tick marks, drag handles pro left indent, first line indent, right indent, hanging indent.
- Vertikální ruler vlevo (volitelné): margin top/bottom.

#### Status Bar

```css
.tm-document-editor__status-bar {
    position: sticky;
    bottom: 0;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 var(--tm-space-3);
    height: 1.75rem;
    background: var(--tm-color-surface);
    border-top: 1px solid var(--tm-color-border);
    font-size: var(--tm-font-size-xs);
    color: var(--tm-color-text-muted);
}
```

Obsah:
- Levá strana: Page X of Y | Words: NNN | Characters: NNNN | Language: CS
- Pravá strana: [Zoom out] [Slider: 50%-200%] [Zoom in] [100%]

### 3.4 Blokové editace – "Schování" blokového modelu

Abychom zachovali stávající `<textarea>` per block architekturu, ale vizuálně vypadali jako Word:

#### Textarea restyling

```css
.tm-document-editable-text {
    display: block;
    width: 100%;
    min-height: auto;           /* ne fixed */
    padding: 0;                 /* žádný padding – text začíná na marginu stránky */
    margin: 0;
    color: var(--tm-color-text);
    font-family: 'Calibri', 'Segoe UI', sans-serif;  /* Word-like font */
    font-size: 11pt;            /* Word default */
    line-height: 1.15;          /* Word default line spacing */
    background: transparent;
    border: none;
    border-radius: 0;
    outline: none;
    resize: none;
    overflow: hidden;           /* skrýt scrollbar */
}

/* Odstranit border kolem aktivního bloku */
.tm-document-editable-block {
    position: relative;
    margin: 0;                  /* marginy řídí paragraph spacing, ne box model */
    border: none;
    border-radius: 0;
}

.tm-document-editable-block--active {
    border-color: transparent;
    box-shadow: none;
}
```

#### Paragraph spacing

```css
.tm-document-block--paragraph {
    margin-bottom: 8pt;         /* Word default paragraph spacing after */
    min-height: auto;
}

.tm-document-block--paragraph:last-child {
    margin-bottom: 0;
}
```

#### Nadpisy

```css
.tm-document-heading--h1 {
    font-size: 24pt;      /* Word H1 */
    font-weight: var(--tm-font-weight-normal); /* Word nadpisy nejsou bold */
    color: var(--tm-color-text);
    margin-bottom: 12pt;
    margin-top: 24pt;
    line-height: 1.25;
}

.tm-document-heading--h2 {
    font-size: 18pt;
    margin-bottom: 8pt;
    margin-top: 18pt;
    line-height: 1.25;
}

.tm-document-heading--h3 {
    font-size: 14pt;
    margin-bottom: 6pt;
    margin-top: 14pt;
}
/* ... */
```

### 3.5 Table Insert – Grid Picker

Místo tlačítka "Insert Table" s okamžitým vložením 2×2:

```
[Insert] → [Table] → Dropdown overlay:
┌────────────────────────┐
│ □ □ □ □ □ □ □ □ □ □   │  ← hover vybírá N×M
│ □ □ □ □ □ □ □ □ □ □   │
│ □ □ □ □ □ □ □ □ □ □   │
│ □ □ □ □ □ □ □ □ □ □   │
│ □ □ □ □ □ □ □ □ □ □   │
│ □ □ □ □ □ □ □ □ □ □   │
│ □ □ □ □ □ □ □ □ □ □   │
│ □ □ □ □ □ □ □ □ □ □   │
│                        │
│ Insert table...        │  ← pro custom dialog
└────────────────────────┘
```

### 3.6 Image Editing – Inline overlay

Místo formuláře pod obrázkem:

- **Výběr obrázku:** Zobrazí se inline toolbar nahoře (align, wrap, delete, caption toggle)
- **Resize:** Drag handles na rozích (pouze v editable režimu)
- **Properties:** V kontextovém menu nebo v pravém sidebaru (Layout tab)
- **Caption:** `figcaption` pod obrázkem, editovatelný kliknutím

### 3.7 Inline Formatting Toolbar (volitelné, nice-to-have)

Při výběru textu se může zobrazit floating toolbar (jako ve Word Online / Google Docs):

```
┌─────────────────────────────────────┐
│ [B][I][U] | [Link] | [Color] | [...]│
└─────────────────────────────────────┘
```

### 3.8 Comments – Vylepšený vzhled

- **Thread karta:** Čistější design, avatar autora (initials), časová značka.
- **Barevný pruh:** Každý thread má barevný pruh vlevo (cyklicky z palety).
- **Resolved state:** Přeškrtnutý text, šedá barva, collapse.
- **Mention:** @ mention s dropdown.

---

## 4. Analýza dopadu na existující testy

### 4.1 Testy, které **musí** projít beze změn (core logika)

| Test soubor | Důvod |
|-------------|-------|
| `DocumentEditorModelTests.cs` | JSON model se nemění |
| `DocumentEditorOperationsOfflineRenditionTests.cs` | Offline/rendition model se nemění |
| `DocumentEditorBlockAndReviewTests.cs` | Block model a revisions logika se nemění |
| `DocumentEditorAdvancedFormatTests.cs` | Inline mark model se nemění (pouze UI) |
| `DocumentEditorInMemoryAndAdapterTests.cs` | Adapter logika se nemění |
| `DocumentEditorProviderTests.cs` | Provider kontrakty se nemění |
| `DocumentEditorCommandTests.cs` | Command stack se nemění (pouze visual commands přibudou) |
| `DocumentEditorSigningIntegrationTests.cs` | Signing integrace se nemění |
| `DocumentEditorOfflineImageRenditionProviderTests.cs` | Image provider logika se nemění |
| `TmDocumentEditorOfflineTests.cs` | Offline UI zůstává na hlavní komponentě |
| `TmDocumentDiffViewerTests.cs` | Diff viewer je separátní komponenta |
| `TmDocumentCollaborationCursorOverlayTests.cs` | Cursor overlay se nemění |
| `TmDocumentEditorCssTests.cs` | ⚠️ Tento test bude pravděpodobně selhávat – nutné aktualizovat expected CSS classes |
| `Localization/TmDocumentEditorLocalizationTests.cs` | Klíče se mohou přidat, ale existující by měly zůstat |

### 4.2 Testy, které **budou pravděpodobně selhávat** a je třeba je upravit

| Test soubor | Selhávající důvod | Očekávaná úprava |
|-------------|-------------------|------------------|
| `TmDocumentEditorTests.cs` | Závisí na DOM struktuře: `.tm-document-editor__ribbon-tab` obsah, toolbar button selectors, page layout classes | Aktualizovat selektory, přidat testy na tab switching |
| `TmDocumentEditingTests.cs` | Závisí na `.tm-document-editable-block`, textarea borders, `data-testid="document-paragraph-editor"` | Aktualizovat selektory, přepsat testy aby nezávisely na border/background styles |
| `TmDocumentRendererTests.cs` | Pravděpodobně méně změn, ale heading sizes se mění | Aktualizovat expected CSS classes pro headings |
| `DocumentEditorE2ETests.cs` | Screenshot comparison – staré screenshoty budou zásadně odlišné | Aktualizovat baseline screenshoty po redesignu |
| `Planning/DocumentEditorPhase0DesignTests.cs` | Pokud kontroluje DOM strukturu | Aktualizovat expected markup |

### 4.3 Konkrétní změny v selektorech

| Současný selektor | Nový selektor | Důvod |
|-------------------|---------------|-------|
| `.tm-document-editor__ribbon-tab` | `.tm-document-editor__ribbon-tab` (zůstane, ale přibude interaktivita) | Záložky zůstanou, ale bude přibývat test na aktivní tab |
| `.tm-document-editor__ribbon-groups` | `.tm-document-editor__ribbon-content--home` atd. | Ribbon obsah se změní podle záložky |
| `.tm-document-editable-block--active` | odstraněn nebo změněn | Blok border se ruší |
| `.tm-document-editable-text` | `.tm-document-editable-text` | Zůstane, ale border/background se mění |
| `data-testid="document-paragraph-editor"` | zůstane | Textarea zůstává, ale styling se mění |
| `data-testid="document-heading-editor"` | zůstane | Textarea zůstává |
| `data-testid="document-list-editor"` | zůstane | Textarea zůstává |
| `.tm-document-insert-panel` | `data-testid="document-insert-table-dropdown"` atd. | Insert panel se mění na dropdowny |
| `.tm-document-image-editor__fields` | odstraněn nebo skryt | Image editor form se ruší |
| `.tm-document-notes-editor` | `.tm-document-editor__footnote-pane` nebo skryt | Poznámky se přesunou |
| `.tm-document-revisions-panel` | `.tm-document-editor__review-pane` | Revisions se přesunou do sidebaru |

---

## 5. Implementační TODO List

> **Pravidla:** Každý krok má RED test, implementaci, GREEN test. Po každé fázi commit.  
> **Priorita:** Vizualní změny jdou před funkčními (quick wins).  
> **Závislosti:** CSS skin může jít nezávisle; Ribbon tabs vyžadují novou komponentu; Ruler/Status bar jsou nové komponenty.

### Fáze A: Page & Surface Skin (Quick Wins – největší vizuální dopad)

#### A.1 Page canvas restyling
- [ ] **RED:** Test že stránka má width `210mm` a min-height `297mm` (A4 proportions).
- [ ] **RED:** Test že stránka má bílou barvu a realistický drop shadow.
- [ ] **RED:** Test že workspace background je šedé (`surface-secondary`).
- [ ] **RED:** Test že stránka nemá border-radius.
- [ ] Implementace: Upravit `.tm-document-editor__page-surface` CSS.
- [ ] Implementace: Upravit `.tm-document-editor__workspace` CSS (šedé pozadí, odstranit grid layout pro hlavní plochu).
- [ ] **GREEN:** Page canvas testy projdou.

#### A.2 Odstranění blokových borderů
- [ ] **RED:** Test že `.tm-document-editable-block` nemá border ani border-radius.
- [ ] **RED:** Test že aktivní blok nemá modrý outline/box-shadow.
- [ ] **RED:** Test že `.tm-document-editable-text` nemá border, border-radius, resize handle.
- [ ] **RED:** Test že `.tm-document-editable-text` má transparent background.
- [ ] Implementace: Upravit `.tm-document-editable-block`, `.tm-document-editable-block--active`, `.tm-document-editable-text` CSS.
- [ ] **GREEN:** Block border testy projdou.

#### A.3 Typography – Word defaults
- [ ] **RED:** Test že base font je `11pt` Calibri-like (fallback chain).
- [ ] **RED:** Test že line-height je `1.15`.
- [ ] **RED:** Test že H1 = `24pt`, H2 = `18pt`, H3 = `14pt`, H4–H6 postupně menší.
- [ ] **RED:** Test že headings nemají bold (font-weight normal).
- [ ] **RED:** Test že odstavec má margin-bottom `8pt` (ne gap mezi bloky).
- [ ] Implementace: Upravit `.tm-document-block`, `.tm-document-heading--h*`, `.tm-document-block--paragraph` CSS.
- [ ] **GREEN:** Typography testy projdou.

#### A.4 Workspace layout – odstranění grid pro hlavní plochu
- [ ] **RED:** Test že workspace používá flexbox nebo block layout místo grid (stránka uprostřed, sidebar volitelně vpravo).
- [ ] **RED:** Test že comment rail a version panel jsou vpravo od stránky, ne pod ní.
- [ ] Implementace: Upravit `.tm-document-editor__workspace` na flex layout: main + aside.
- [ ] **GREEN:** Layout testy projdou.

#### A.5 Image editor – inline restyling
- [ ] **RED:** Test že image editor formulář není viditelný pod obrázkem (skrytý nebo přesunutý).
- [ ] **RED:** Test že image má inline toolbar po aktivaci.
- [ ] Implementace: Přesunout image properties do `TmDocumentImageEditor.razor` jako inline overlay (absolutely positioned toolbar nad obrázkem).
- [ ] Implementace: Caption jako `figcaption`, editovatelný on-click.
- [ ] **GREEN:** Image inline testy projdou.

### Fáze B: Ribbon Redesign (Toolbar)

#### B.1 Tab switching mechanismus
- [ ] **RED:** Test že klik na záložku změní aktivní tab (class `tm-document-editor__ribbon-tab--active`).
- [ ] **RED:** Test že aktivní tab mění zobrazený obsah ribbonu.
- [ ] **RED:** Test že záložky jsou: Home, Insert, Layout, References, Review, View.
- [ ] Implementace: Přidat `@bind-ActiveTab` nebo callback mechanismus do `TmDocumentEditorToolbar`.
- [ ] Implementace: Každá záložka renderuje vlastní skupinu komponent.
- [ ] **GREEN:** Tab switching testy projdou.

#### B.2 Home tab – Font group
- [ ] **RED:** Test že Home tab obsahuje Font group s: Font dropdown, Size dropdown, Bold, Italic, Underline.
- [ ] **RED:** Test že Font dropdown obsahuje alespoň 5 fontů.
- [ ] **RED:** Test že Size dropdown obsahuje standardní Word velikosti.
- [ ] **RED:** Test že Bold/Italic/Underline toggle správně mění aktivní stav (CSS class).
- [ ] Implementace: Vytvořit `TmDocumentEditorRibbonHome.razor` (nebo inline v toolbaru).
- [ ] Implementace: Font dropdown jako `<select>` nebo `TmDropdown` s font preview.
- [ ] Implementace: Size dropdown jako `<select>`.
- [ ] **GREEN:** Home Font testy projdou.

#### B.3 Home tab – Paragraph group
- [ ] **RED:** Test že Paragraph group obsahuje: Align Left/Center/Right/Justify.
- [ ] **RED:** Test že Align buttons mají toggle chování (pouze jeden aktivní).
- [ ] **RED:** Test že Line spacing dropdown existuje.
- [ ] **RED:** Test že Increase/Decrease indent existují.
- [ ] **RED:** Test že Bullets / Numbering dropdowny existují.
- [ ] Implementace: Paragraph group komponenta.
- [ ] **GREEN:** Paragraph group testy projdou.

#### B.4 Home tab – Styles group
- [ ] **RED:** Test že Styles group obsahuje: Normal, Heading 1, Heading 2, Heading 3, Heading 4.
- [ ] **RED:** Test že klik na styl změní aktivní blok na příslušný typ.
- [ ] Implementace: Styles group komponenta.
- [ ] **GREEN:** Styles testy projdou.

#### B.5 Insert tab
- [ ] **RED:** Test že Insert tab obsahuje: Table dropdown, Picture dropdown, Link, Comment, Page Break.
- [ ] **RED:** Test že Table dropdown zobrazí grid picker (10×8).
- [ ] **RED:** Test že klik na grid cell vloží tabulku odpovídající velikosti.
- [ ] **RED:** Test že Picture dropdown nabízí URL, Upload, Clipboard.
- [ ] Implementace: Insert tab komponenta.
- [ ] Implementace: Table grid picker komponenta (`TmTableGridPicker`).
- [ ] **GREEN:** Insert tab testy projdou.

#### B.6 Layout tab (základní)
- [ ] **RED:** Test že Layout tab obsahuje: Margins dropdown (Normal, Narrow, Wide), Orientation (Portrait/Landscape), Page Break.
- [ ] Implementace: Layout tab komponenta (marginy se aplikují na stránku).
- [ ] **GREEN:** Layout tab testy projdou.

#### B.7 Review tab (přesun existujících funkcí)
- [ ] **RED:** Test že Review tab obsahuje: Track Changes toggle, New Comment, Accept/Reject change.
- [ ] **RED:** Test že Track Changes je přesunut z Home do Review.
- [ ] Implementace: Review tab komponenta (presun existujících callbacků).
- [ ] **GREEN:** Review tab testy projdou.

#### B.8 View tab (základní)
- [ ] **RED:** Test že View tab obsahuje: Ruler toggle, Zoom dropdown (50%, 75%, 100%, 125%, 150%, 200%).
- [ ] Implementace: View tab komponenta.
- [ ] **GREEN:** View tab testy projdou.

### Fáze C: Nové komponenty (Status Bar, Ruler)

#### C.1 Status Bar
- [ ] **RED:** Test že status bar existuje a má class `tm-document-editor__status-bar`.
- [ ] **RED:** Test že status bar zobrazuje počet slov (word count) z dokumentu.
- [ ] **RED:** Test že status bar zobrazuje zoom level (default 100%).
- [ ] **RED:** Test že zoom slider změní CSS transform scale stránky.
- [ ] Implementace: Vytvořit `TmDocumentEditorStatusBar.razor`.
- [ ] Implementace: Word count service (prochází texty bloků).
- [ ] Implementace: Zoom state – CSS transform na page surface.
- [ ] **GREEN:** Status bar testy projdou.

#### C.2 Horizontal Ruler
- [ ] **RED:** Test že ruler existuje nad stránkou.
- [ ] **RED:** Test že ruler zobrazuje tick marks (cm/inch).
- [ ] **RED:** Test že ruler zobrazuje margin boundaries.
- [ ] Implementace: Vytvořit `TmDocumentEditorRuler.razor` (SVG nebo div-based).
- [ ] Implementace: Ruler reaguje na margin changes z Layout tabu.
- [ ] **GREEN:** Ruler testy projdou.

### Fáze D: Insert UI Redesign

#### D.1 Table Grid Picker
- [ ] **RED:** Test že grid picker má 10×8 buněk.
- [ ] **RED:** Test že hover vybere N×M rozsah.
- [ ] **RED:** Test že klik vloží tabulku dané velikosti.
- [ ] **RED:** Test že grid picker se zavře po výběru.
- [ ] Implementace: Vytvořit `TmTableGridPicker.razor` (dropdown overlay).
- [ ] **GREEN:** Grid picker testy projdou.

#### D.2 Insert Image Dialog (modal)
- [ ] **RED:** Test že Insert Image otevře `TmModal` dialog místo inline panelu.
- [ ] **RED:** Test že dialog má taby: URL, Upload, Clipboard.
- [ ] Implementace: Předělat `TmDocumentImageDialog` na `TmModal`-based dialog.
- [ ] **GREEN:** Image dialog testy projdou.

### Fáze E: Footnotes, Revisions, Notes – přesun do panelů

#### E.1 Footnote pane
- [ ] **RED:** Test že footnotes/endnotes jsou zobrazeny jako Word-like bottom pane nebo v sidebaru, ne jako box pod dokumentem.
- [ ] Implementace: Přesunout `.tm-document-notes-editor` do pravého sidebaru pod komentáře nebo jako separátní tab.
- [ ] **GREEN:** Footnote pane testy projdou.

#### E.2 Review pane (Revisions)
- [ ] **RED:** Test že revisions panel je v pravém sidebaru (ne pod dokumentem).
- [ ] **RED:** Test že každá revision má Accept/Reject inline (ne jen tlačítka).
- [ ] Implementace: Přesunout revisions do sidebaru, redesign karet.
- [ ] **GREEN:** Review pane testy projdou.

### Fáze F: Advanced Formatting (formátování na úrovni bloku)

> Poznámka: Vzhledem k `<textarea>` per block architektuře se formátování stále aplikuje na celý blok. Toto je známé omezení.

#### F.1 Font family & size
- [ ] **RED:** Test že Font dropdown mění font-family aktivního bloku (přes CSS class nebo inline style).
- [ ] **RED:** Test že Size dropdown mění font-size aktivního bloku.
- [ ] Implementace: Přidat `FontFamily` a `FontSize` do block modelu nebo použít CSS class mapping.
- [ ] **GREEN:** Font/size testy projdou.

#### F.2 Text alignment
- [ ] **RED:** Test že Align Left/Center/Right/Justify mění `text-align` aktivního bloku.
- [ ] Implementace: Přidat `TextAlign` do block modelu nebo CSS class.
- [ ] **GREEN:** Alignment testy projdou.

#### F.3 Line spacing & paragraph spacing
- [ ] **RED:** Test že Line spacing dropdown mění line-height bloku.
- [ ] **RED:** Test že Space Before/After inputy mění margin-top/bottom bloku.
- [ ] Implementace: Přidat spacing properties do modelu.
- [ ] **GREEN:** Spacing testy projdou.

#### F.4 Text color & highlight
- [ ] **RED:** Test že Font color picker mění barvu textu v bloku.
- [ ] **RED:** Test že Highlight color picker mění background textu.
- [ ] Implementace: Přidat color properties do inline marks (pokud se aplikuje na celý blok, jinak nový mark type).
- [ ] **GREEN:** Color testy projdou.

#### F.5 Strikethrough, Subscript, Superscript
- [ ] **RED:** Test že Strikethrough toggle přidá mark.
- [ ] **RED:** Test že Subscript/Superscript toggle přidá mark.
- [ ] Implementace: Rozšířit `InlineMarkType` o Strikethrough, Subscript, Superscript (pokud ještě neexistují).
- [ ] **GREEN:** Extra marks testy projdou.

### Fáze G: Test Updates & E2E

#### G.1 Aktualizace bUnit testů
- [ ] Upravit `TmDocumentEditorTests.cs` – aktualizovat selektory na nový ribbon.
- [ ] Upravit `TmDocumentEditingTests.cs` – odstranit závislost na `.tm-document-editable-block--active` border.
- [ ] Upravit `TmDocumentRendererTests.cs` – aktualizovat heading size expectations.
- [ ] Upravit `TmDocumentEditorCssTests.cs` – aktualizovat expected CSS properties.

#### G.2 Aktualizace E2E testů
- [ ] Aktualizovat `DocumentEditorE2ETests.cs` – přepsat screenshot baseline.
- [ ] Přidat E2E test: Tab switching v ribbonu.
- [ ] Přidat E2E test: Insert table via grid picker.
- [ ] Přidat E2E test: Zoom in/out.

#### G.3 Aktualizace MockTmLocalizer
- [ ] Přidat nové lokalizační klíče pro ribbon groups, status bar, ruler, zoom.

#### G.4 Aktualizace Demo stránky
- [ ] Upravit `DocumentEditorPage.razor` – odstranit staré tlačítka (Export DOCX/ODT může jít do Insert tabu nebo zůstat nahoře).
- [ ] Upravit demo seed dokumentů – realističtější obsah pro Word-like feel.

### Fáze H: Polish & Accessibility

#### H.1 Keyboard navigation v ribbonu
- [ ] **RED:** Test že Alt+N aktivuje Insert tab (nebo podobná kombinace).
- [ ] **RED:** Test že Tab naviguje mezi skupinami v ribbonu.
- [ ] Implementace: Keyboard manager rozšíření pro ribbon.
- [ ] **GREEN:** Ribbon keyboard testy projdou.

#### H.2 Responsive design
- [ ] **RED:** Test že na mobilní šířce (< 768px) se ribbon zmenší (compact mode).
- [ ] **RED:** Test že status bar se skryje na mobilu nebo zjednoduší.
- [ ] Implementace: Media query úpravy v CSS.
- [ ] **GREEN:** Responsive testy projdou.

#### H.3 Dark mode
- [ ] **RED:** Test že dark mode stránka má tmavě šedé pozadí (ne černé) a stránka zůstává bílá (jako papír).
- [ ] Implementace: Dark mode token overrides pro document editor.
- [ ] **GREEN:** Dark mode testy projdou.

---

## 6. Souhrn rizik a kompromisů

| Riziko | Mitigace |
|--------|----------|
| **`<textarea>` per block není opravdu Word** | Transparent restyling schová hranice; uživatel stále vidí plynulý text. Pro true inline selection by bylo nutné přepsat na `contenteditable` (odhad 2–3 týdny plné práce). |
| **Stávající testy selžou masivně** | Fáze G je dedikovaná na opravu testů; většina změn je v CSS/DOM, ne v logice. |
| **Rozsah je velký** | Fáze A–D jsou critical path; E–H jsou nice-to-have. Možno iterovat. |
| **Performance s rulerem a status barem** | Ruler je statický SVG – minimální dopad. Word count se počítá na změnu, ne na každý keystroke. |
| **Backwards compatibility** | JSON model se nemění; API komponenty se nerozšiřuje, pouze UI. Parametry `TmDocumentEditor` zůstanou stejné. |

---

## 7. Checklist pro začátek implementace

- [ ] Prohlédnout si aktuální demo na `/document-editor`.
- [ ] Založit feature branch `feature/document-editor-word-ui`.
- [ ] Ujistit se, že všechny stávající testy jsou zelené před změnami.
- [ ] Začít Fází A.1 (Page canvas) – největší vizuální dopad, nejnižší riziko.
- [ ] Po každé fázi commit a push.
- [ ] Po Fázi A spustit E2E screenshot comparison a uložit nový baseline.
- [ ] Průběžně aktualizovat tento TODO list (odškrtávat hotové).

---

*Dokument je živý – bude se aktualizovat během implementace.*
