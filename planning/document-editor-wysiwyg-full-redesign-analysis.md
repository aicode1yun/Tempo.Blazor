# TmDocumentEditor – Kompletní WYSIWYG Redesign Analýza
## Od blokového editoru ke skutečnému Word-like online processoru

**Datum:** 2026-05-11  
**Autor:** UX/UI Expert + FE Architekt  
**Cíl:** Kompletní přepis render/edit vrstvy na contenteditable WYSIWYG engine s word-like hlavičkami/zápatími, stránkováním, plnohodnotným ribbonem a inline editací.  
**Metodika:** TDD, nejmenší možné kroky, každý krok má RED→GREEN test, E2E screenshot comparison.  
**Základní rozhodnutí:** Contenteditable DOM rendering (ne Canvas) – realistický poměr pracnosti/výsledku.  
**Prováděcí plán:** [document-editor-wysiwyg-js-engine-tdd-todo.md](document-editor-wysiwyg-js-engine-tdd-todo.md) – implementace poběží podle samostatného checklistu pro architekturu Blazor shell + JavaScript editing engine.

---

## 1. Proč ne Canvas a proč contenteditable

| Aspekt | Canvas rendering | Contenteditable DOM |
|--------|-----------------|---------------------|
| **Text layout** | Musíš napsat vlastní word-wrap, kerning, bidi, line-height | Browser to dělá za tebe, pixel-perfect |
| **IME (Čínština, Japonština)** | Musíš implementovat vlastní IME composition | Browser-native, funguje automaticky |
| **Accessibility** | Žádný screen reader nevidí Canvas text | ARIA + nativní accessibility zdarma |
| **Copy/Paste** | Musíš implementovat vlastní clipboard | Browser-native + intercept |
| **Mobile/touch** | Vlastní touch handling | Browser-native |
| **Pracnost** | 6–12 měsíců týmu | 4–8 týdnů 1–2 vývojáři |
| **Google Docs** | Ano, ale používají Canvas jen pro *měření*, DOM pro rendering | Tato cesta |

**Rozhodnutí:** Contenteditable DOM s vlastním modelem (ProseMirror-like architektura). DOM není trvalá persistence, ale během editace není ani pasivní "jen view". Browser dočasně vlastní živý caret, IME composition, výběr a právě zapisovaný text. Interní model je autoritativní po dokončení řízené editační transakce, JSON model je persistence contract a mezi nimi je serializátor/deserializátor.

---

## 2. Vysokoúrovňová architektura

```
┌─────────────────────────────────────────────────────────────────────┐
│                            RIBBON / TOOLBAR                         │
│   (TmDocumentEditorRibbon – Word-like tabs with command dispatch)   │
└─────────────────────────────────────────────────────────────────────┘
                              ↓ Commands
┌─────────────────────────────────────────────────────────────────────┐
│                      DOCUMENT EDITOR ENGINE                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │
│  │   COMMAND    │  │  SELECTION   │  │   INPUT PIPELINE         │  │
│  │   MANAGER    │←→│   MANAGER    │←→│ beforeinput/input/paste  │  │
│  └──────────────┘  └──────────────┘  └──────────────────────────┘  │
│         ↓                   ↓                    ↓                  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │              DOCUMENT MODEL (C# tree)                        │  │
│  │  Document → Pages → Sections → Blocks → Inlines → TextRun   │  │
│  │  + marks (bold, italic, font, color, link...)               │  │
│  └──────────────────────────────────────────────────────────────┘  │
│         ↑ Serialization / Deserialization ↑                        │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │              DOM VIEW (contenteditable)                      │  │
│  │  <div contenteditable>                                        │  │
│  │    <div class="page">                                         │  │
│  │      <header contenteditable>...                              │  │
│  │      <div class="body">                                       │  │
│  │        <p><b>Bold text</b></p>                                │  │
│  │        <table>...</table>                                     │  │
│  │      </div>                                                   │  │
│  │      <footer contenteditable>...                              │  │
│  │    </div>                                                     │  │
│  │  </div>                                                       │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                              ↑ JS Interop
┌─────────────────────────────────────────────────────────────────────┐
│                      JS LAYER (document-editor.js)                  │
│  - getSelectionRange() → vrací { node, offset, blockId, inlineId } │
│  - setSelectionRange(range)                                         │
│  - attachInputPipeline(dotNetRef, rootElement)                      │
│  - attachMutationObserver(dotNetRef, rootElement) as guard/fallback │
│  - execCommandSafe(command, value) – our own, not document.execCmd  │
│  - getTextContent(node)                                             │
│  - pasteHandler(event) → intercept clipboard                        │
│  - imeHandler(event) → handle compositionstart/end                  │
│  - measureText(text, font) → canvas metrics for ruler/alignment    │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.1 Klíčové principy engine

1. **Authoritative model after transaction = C# Document Model** – během živé editace je DOM řízený browserem a po `beforeinput`/`input` transakci se změna převede do modelu. DOM se nesmí přerenderovat tak, aby ztratil caret nebo IME composition.
2. **Žádný `document.execCommand`** – všechny úpravy DOM provádí C# kód přes Blazor render nebo JS interop.
3. **Input pipeline je primární zdroj změn** – `beforeinput`, `input`, `paste`, `compositionstart/end` a keyboard shortcuts se mapují na explicitní commandy. `MutationObserver` je guard/fallback pro neočekávané DOM změny, ne hlavní sync engine.
4. **Selection Manager** synchronizuje browser selection s C# `DocumentSelection` objektem.
5. **Command pattern** – každá editace je příkaz (InsertText, DeleteText, ToggleMark, InsertBlock, ...), který lze undo/redo.

### 2.2 Kritické pravidlo pro contenteditable a Blazor

Contenteditable nelze řídit stejně jako běžnou Blazor komponentu s inputem. Browser při psaní, mazání, paste a IME composition provádí okamžité DOM mutace a drží caret uvnitř konkrétních text nodů. Pokud Blazor po každém znaku přerenderuje celý editable subtree, editor bude ztrácet focus, selection a někdy i rozpracovanou composition.

Proto engine používá dvoufázový režim:
- **Live DOM phase:** browser provede nativní editaci nebo ji `beforeinput` zachytí a převede na command.
- **Model commit phase:** změna se aplikuje do `DocumentModel`, uloží se selection mapping a pouze minimální část DOM se dorovná, pokud je to nutné.
- **Reconciliation phase:** `MutationObserver` ověří, že DOM odpovídá modelu. Pokud najde neočekávanou změnu, spustí parser/fallback, označí dokument jako dirty a obnoví selection.

Toto je zásadní omezení návrhu. `DocumentModel` je autoritativní pro uložený stav, undo/redo, export a provider kontrakt. DOM je ale autoritativní pro živou textovou interakci mezi `beforeinput` a commitem.

---

## 3. Document Model – Nový interní model

Stávající `DocumentEditorDocument` se používá pro persistenci (API→DB). Nový model je **interní paměťová reprezentace** optimalizovaná pro WYSIWYG editing.

### 3.1 Hierarchie

```csharp
// Root
document
  ├── Metadata (title, author, created, modified...)
  ├── PageSettings (A4, margins, orientation)
  ├── Sections[]
  │     └── SectionProperties (margins, columns, header/footer refs)
  ├── Pages[]  // virtual – počítá se z content + page settings
  │     ├── HeaderRegion
  │     ├── BodyRegion
  │     │     └── Blocks[]
  │     │           └── Block (Paragraph | Heading | List | Table | Image | PageBreak)
  │     │                 └── Inlines[]
  │     │                       └── Inline (TextRun | ImageInline | Break | Tab)
  │     │                             └── Marks[] (Bold | Italic | Underline | Font | Color | Link | Subscript | Superscript | Strikethrough | Highlight)
  │     └── FooterRegion
  ├── HeadersFooters[]
  │     └── HeaderFooter (Primary | FirstPage | EvenPage)
  │           └── Blocks[] (stejná struktura jako body)
  ├── Notes[]
  │     └── Note (Footnote | Endnote)
  │           └── Blocks[]
  ├── Comments[]
  │     └── Comment (anchor range, entries[])
  └── Revisions[] (track changes)
```

### 3.2 Klíčové třídy

```csharp
public abstract class DocumentNode
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public Dictionary<string, object> Attributes { get; } = new();
}

public class DocumentModel : DocumentNode
{
    public DocumentMetadata Metadata { get; set; } = new();
    public PageSettings PageSettings { get; set; } = PageSettings.DefaultA4();
    public List<Section> Sections { get; } = new();
    public List<Block> Body { get; } = new(); // Flat list of blocks, pages are virtual
    public List<HeaderFooter> HeadersFooters { get; } = new();
    public List<DocumentNote> Notes { get; } = new();
    public List<DocumentComment> Comments { get; } = new();
    public List<DocumentRevision> Revisions { get; } = new();
}

public abstract class Block : DocumentNode
{
    public abstract string Type { get; }
    public List<Inline> Inlines { get; } = new();
    public ParagraphProperties? Properties { get; set; }
}

public class ParagraphBlock : Block
{
    public override string Type => "paragraph";
}

public class HeadingBlock : Block
{
    public override string Type => "heading";
    public int Level { get; set; } = 1;
}

public class ListItemBlock : Block
{
    public override string Type => "listItem";
    public bool Ordered { get; set; }
    public int IndentLevel { get; set; }
}

public class TableBlock : Block
{
    public override string Type => "table";
    public List<TableRow> Rows { get; } = new();
    // Table doesn't have Inlines directly
}

public class ImageBlock : Block
{
    public override string Type => "image";
    public string Src { get; set; } = "";
    public string Alt { get; set; } = "";
    public ImageSize Size { get; set; } = new();
    public ImageLayout Layout { get; set; } = ImageLayout.Inline;
    public ImageAnchor? Anchor { get; set; }
    public ImageWrapMode WrapMode { get; set; } = ImageWrapMode.Inline;
    public ImagePosition Position { get; set; } = new();
}

public class ImageAnchor
{
    public string BlockId { get; set; } = "";
    public int CharacterOffset { get; set; }
    public bool MoveWithText { get; set; } = true;
    public bool LockAnchor { get; set; }
}

public enum ImageWrapMode
{
    Inline,
    Square,
    Tight,
    Through,
    TopAndBottom,
    BehindText,
    InFrontOfText
}

public class PageBreakBlock : Block
{
    public override string Type => "pageBreak";
}

public abstract class Inline : DocumentNode
{
    public List<Mark> Marks { get; } = new();
}

public class TextRun : Inline
{
    public string Text { get; set; } = "";
}

public class HardBreak : Inline { }

public abstract class Mark
{
    public abstract string Type { get; }
}

public class BoldMark : Mark { public override string Type => "bold"; }
public class ItalicMark : Mark { public override string Type => "italic"; }
public class UnderlineMark : Mark { public override string Type => "underline"; }
public class StrikethroughMark : Mark { public override string Type => "strikethrough"; }
public class SubscriptMark : Mark { public override string Type => "subscript"; }
public class SuperscriptMark : Mark { public override string Type => "superscript"; }
public class FontMark : Mark 
{ 
    public override string Type => "font"; 
    public string Family { get; set; } = "Calibri";
    public string Size { get; set; } = "11pt";
}
public class ColorMark : Mark 
{ 
    public override string Type => "color"; 
    public string Color { get; set; } = "#000000";
}
public class HighlightMark : Mark 
{ 
    public override string Type => "highlight"; 
    public string Color { get; set; } = "yellow";
}
public class LinkMark : Mark 
{ 
    public override string Type => "link"; 
    public string Href { get; set; } = "";
}

public class ParagraphProperties
{
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public double LineSpacing { get; set; } = 1.15;
    public string? SpaceBefore { get; set; }
    public string? SpaceAfter { get; set; }
    public string? LeftIndent { get; set; }
    public string? RightIndent { get; set; }
    public string? FirstLineIndent { get; set; }
}

public enum TextAlignment { Left, Center, Right, Justify }
```

### 3.2.1 Floating/anchored images jako Word-compatible cíl

Floating images nejsou jen absolutně pozicovaný `<div>`. Cílem je chování kompatibilní s Wordem, i když implementace bude dlouhá a rozdělená do více iterací. Model musí od začátku počítat s:
- anchorem k odstavci nebo konkrétní pozici v textu,
- volbou `MoveWithText` / `LockAnchor`,
- wrap módy `Square`, `Tight`, `TopAndBottom`, `BehindText`, `InFrontOfText`,
- relativní pozicí vůči stránce, marginu, sloupci nebo anchoru,
- resize/drag handles, které mění model, ne pouze DOM style,
- roundtrip mapováním do DOCX/ODT drawing/anchor modelu.

První implementace může umět jen podmnožinu interakcí, ale datový model a testovací kontrakty nesmí být navržené jako dočasné absolutní pozicování bez Word semantics.

### 3.3 Selection Model

```csharp
public class DocumentSelection
{
    // Anchor = kde výběr začal
    public DocumentPosition Anchor { get; set; } = new();
    // Focus = kde kurzor aktuálně je
    public DocumentPosition Focus { get; set; } = new();
    
    public bool IsCollapsed => Anchor.Equals(Focus);
    public bool IsForward => /* focus after anchor */;
    
    public DocumentPosition Start => IsForward ? Anchor : Focus;
    public DocumentPosition End => IsForward ? Focus : Anchor;
}

public class DocumentPosition
{
    public string BlockId { get; set; } = "";
    public int InlineIndex { get; set; } = 0; // index inline v bloku
    public int TextOffset { get; set; } = 0;  // offset uvnitř TextRun
}
```

---

## 4. DOM Rendering Strategy

### 4.1 Contenteditable Root

```html
<div class="tm-document-wysiwyg" contenteditable="true" spellcheck="false">
  <div class="tm-page" data-page="1">
    <header class="tm-page-header" contenteditable="true">
      <p>Header text</p>
    </header>
    <div class="tm-page-body">
      <p style="text-align: left;">
        <span data-inline-id="abc" data-marks="bold,italic">Bold italic text</span>
        <span data-inline-id="def">Normal text</span>
      </p>
      <h1 style="font-size: 24pt; font-weight: normal;">
        <span data-inline-id="ghi">Heading</span>
      </h1>
    </div>
    <footer class="tm-page-footer" contenteditable="true">
      <p>Footer text</p>
    </footer>
  </div>
</div>
```

### 4.2 Render Rules

| Model | DOM Output |
|-------|-----------|
| `ParagraphBlock` | `<p>` |
| `HeadingBlock` (level 1–6) | `<h1>`–`<h6>` se style (font-size, margin) |
| `ListItemBlock` (ordered) | `<li>` uvnitř `<ol>` (list se seskupuje) |
| `ListItemBlock` (unordered) | `<li>` uvnitř `<ul>` |
| `TableBlock` | `<table><tbody><tr><td>...</td></tr></tbody></table>` |
| `ImageBlock` (inline) | `<img>` uvnitř `<p>` |
| `ImageBlock` (floating) | `<div class="floating-image">` s absolutní pozicí |
| `PageBreakBlock` | `<div class="page-break">` nebo konec `<div class="tm-page">` |
| `TextRun` + `BoldMark` | `<b>` nebo `<span style="font-weight: bold">` |
| `TextRun` + `ColorMark` | `<span style="color: #rrggbb">` |
| `TextRun` + `FontMark` | `<span style="font-family: X; font-size: Y">` |
| `LinkMark` | `<a href="...">` |
| `HighlightMark` | `<mark style="background-color: ...">` |

### 4.3 Data Attributes pro DOM→Model mapování

Každý element má `data-node-id` pro zpětné mapování:
- `data-block-id` na `<p>`, `<h1>`, `<li>`, `<table>`
- `data-inline-id` na `<span>` uvnitř blocků
- `data-cell-id` na `<td>`
- `data-page-id` na `.tm-page`

---

## 5. JS Interop Layer – Detailní specifikace

Soubor `document-editor.js` bude nahrazen nebo rozšířen na **WYSIWYG Engine**.

### 5.1 Selection API

```javascript
// Vrátí aktuální selection jako serializovatelný objekt
tmDocumentEditor.getSelection = function(rootElement) {
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) return null;
    
    const range = sel.getRangeAt(0);
    return {
        anchorBlockId: getClosestBlockId(range.startContainer),
        anchorInlineId: getClosestInlineId(range.startContainer),
        anchorOffset: range.startOffset,
        focusBlockId: getClosestBlockId(range.endContainer),
        focusInlineId: getClosestInlineId(range.endContainer),
        focusOffset: range.endOffset
    };
};

// Nastaví selection z C# objektu
tmDocumentEditor.setSelection = function(rootElement, selection) {
    // najde DOM nody podle data-node-id, vytvoří Range, aplikuje Selection
};
```

### 5.2 Primární input pipeline

MutationObserver nesmí být hlavní mechanismus pro běžné psaní. Je příliš hrubý: browser může slučovat text nodes, vkládat vlastní elementy, normalizovat DOM a změnit selection dřív, než C# dostane stabilní obraz změny. Primární cesta musí být event-driven:

| Event | Účel | Výsledek |
|-------|------|----------|
| `beforeinput` | Zachytit typ změny před DOM mutací (`insertText`, `deleteContentBackward`, `insertParagraph`, `formatBold`, ...) | Pokud umíme operaci řídit, `preventDefault()` a dispatch commandu. |
| `input` | Potvrdit browserem provedenou změnu, kterou jsme neblokovali | Parse minimálního textového rozsahu a commit do modelu. |
| `compositionstart/update/end` | IME režim | Během composition neprovádět destruktivní re-render, commit až po `compositionend`. |
| `paste` | Clipboard | Sanitizovat HTML/plain text/images a převést na explicitní paste command. |
| `selectionchange` | Caret/range tracking | Debounced update `DocumentSelection`. |

```javascript
tmDocumentEditor.attachInputPipeline = function(rootElement, dotNetRef) {
    rootElement.addEventListener('beforeinput', (event) => {
        dotNetRef.invokeMethodAsync('OnBeforeInput', {
            inputType: event.inputType,
            data: event.data,
            isComposing: event.isComposing
        }).then(result => {
            if (result && result.preventDefault) {
                event.preventDefault();
            }
        });
    });

    rootElement.addEventListener('input', (event) => {
        dotNetRef.invokeMethodAsync('OnInputCommitted', {
            inputType: event.inputType,
            isComposing: event.isComposing
        });
    });
};
```

### 5.3 Mutation Observer jako guard/fallback

MutationObserver zůstává důležitý, ale jeho role je jiná:
- detekovat DOM změny, které nevznikly přes náš command pipeline,
- zachytit browser/autocorrect/extension zásahy,
- ověřit invarianty `data-block-id`, `data-inline-id`, `data-cell-id`,
- spustit omezený DOM parser jen pro dotčený blok nebo region,
- vyvolat telemetry/debug event, pokud DOM diverguje od modelu.

Nesmí se používat jako "vezmi všechny mutations a serializuj celý DOM zpět do modelu" pro každé psaní. To by bylo nestabilní, pomalé a velmi citlivé na rozdíly mezi prohlížeči.

```javascript
tmDocumentEditor.attachMutationObserver = function(rootElement, dotNetRef) {
    const observer = new MutationObserver((mutations) => {
        const changes = mutations.map(m => ({
            type: m.type, // 'childList' | 'characterData' | 'attributes'
            target: getNodePath(m.target),
            addedNodes: Array.from(m.addedNodes).map(n => serializeNode(n)),
            removedNodes: Array.from(m.removedNodes).map(n => serializeNode(n)),
            oldValue: m.oldValue,
            newValue: m.target.textContent || m.target.nodeValue
        }));
        dotNetRef.invokeMethodAsync('OnDomMutated', changes);
    });
    
    observer.observe(rootElement, {
        childList: true,
        subtree: true,
        characterData: true,
        characterDataOldValue: true,
        attributes: true,
        attributeOldValue: true
    });
};
```

### 5.4 IME Handler

```javascript
tmDocumentEditor.attachImeHandler = function(rootElement, dotNetRef) {
    rootElement.addEventListener('compositionstart', () => {
        dotNetRef.invokeMethodAsync('OnCompositionStart');
    });
    rootElement.addEventListener('compositionend', (e) => {
        dotNetRef.invokeMethodAsync('OnCompositionEnd', e.data);
    });
};
```

### 5.5 Paste Handler

```javascript
tmDocumentEditor.attachPasteHandler = function(rootElement, dotNetRef) {
    rootElement.addEventListener('paste', (e) => {
        e.preventDefault();
        const html = e.clipboardData.getData('text/html');
        const text = e.clipboardData.getData('text/plain');
        const files = Array.from(e.clipboardData.files);
        dotNetRef.invokeMethodAsync('OnPaste', html, text, files.map(f => ({name: f.name, type: f.type, size: f.size})));
    });
};
```

### 5.6 Key Intercept (pro zkratky)

```javascript
tmDocumentEditor.attachKeyHandler = function(rootElement, dotNetRef) {
    rootElement.addEventListener('keydown', (e) => {
        // Zablokuj default chování pro naše zkratky
        if (e.ctrlKey || e.metaKey) {
            switch(e.key) {
                case 'b': case 'i': case 'u': case 'k': case 'z': case 'y': case 's':
                    e.preventDefault();
                    dotNetRef.invokeMethodAsync('OnShortcut', e.key, e.ctrlKey, e.shiftKey, e.altKey);
                    break;
            }
        }
    });
};
```

---

## 6. Command System – Každá editace je Command

### 6.1 Command interface

```csharp
public interface IDocumentCommand
{
    string Name { get; }
    void Execute(DocumentModel model, DocumentSelection selection);
    void Undo(DocumentModel model, DocumentSelection selection);
    void Redo(DocumentModel model, DocumentSelection selection) => Execute(model, selection);
}
```

### 6.2 Příklady Commandů

| Command | Co dělá |
|---------|---------|
| `InsertTextCommand` | Vloží text na pozici kurzoru |
| `DeleteTextCommand` | Smaže text v daném rozsahu |
| `ToggleMarkCommand` | Přepne mark (bold, italic...) na výběru |
| `SetBlockTypeCommand` | Změní typ bloku (p → h1, p → li) |
| `SetParagraphPropertiesCommand` | Změní alignment, line spacing, indent |
| `InsertBlockCommand` | Vloží nový blok (paragraph, table, image, pageBreak) |
| `RemoveBlockCommand` | Smaže blok |
| `InsertTableCommand` | Vloží tabulku N×M |
| `InsertImageCommand` | Vloží obrázek |
| `ApplyLinkCommand` | Aplikuje hyperlink na výběr |
| `AcceptRevisionCommand` | Přijme track change |
| `RejectRevisionCommand` | Odmítne track change |

### 6.3 Command Stack

```csharp
public class DocumentCommandStack
{
    private readonly Stack<CommandEntry> _undo = new();
    private readonly Stack<CommandEntry> _redo = new();
    
    public void Push(IDocumentCommand command, DocumentModel beforeState, DocumentSelection selection);
    public bool CanUndo { get; }
    public bool CanRedo { get; }
    public string? NextUndoName { get; }
    public string? NextRedoName { get; }
    
    public void Undo();
    public void Redo();
    public void Clear();
}
```

---

## 7. Page Model – Headers, Footers, Pagination

### 7.1 Page jako render jednotka

Model je **flat** (seznam bloků), ale při renderingu se rozdělí na stránky podle:
- `PageBreakBlock`
- Výška obsahu > A4 height - margins

Pozor: stránkování podle výšky obsahu je největší skrytá komplexita celého editoru. Cílově chceme Word-like chování, ale nesmí se tvářit jako jednoduchý `if (height > pageHeight) split`. Skutečný layout musí řešit minimálně:
- stabilní měření bloků po vyrenderování do DOM,
- rozdělení paragraphu na stránce podle řádků, ne jen podle celého bloku,
- tabulky přes více stran včetně opakování header row,
- obrázky a floating objekty ukotvené k odstavci,
- footnotes, které snižují dostupnou výšku body regionu,
- section properties, margins, columns, first/even/odd headers a footers,
- deterministické výsledky po reloadu a při exportu.

Z toho plyne, že `PageView` není jen projekce seznamu bloků, ale výsledek samostatného **PageLayoutEngine**.

```csharp
public class PageView
{
    public int PageNumber { get; set; }
    public HeaderFooter? Header { get; set; }
    public HeaderFooter? Footer { get; set; }
    public List<Block> BodyBlocks { get; } = new();
}
```

### 7.1.1 PageLayoutEngine – cílový návrh

```csharp
public sealed class PageLayoutEngine
{
    public PageLayoutResult Layout(DocumentModel model, PageLayoutOptions options);
}

public sealed class PageLayoutResult
{
    public IReadOnlyList<PageView> Pages { get; init; } = [];
    public IReadOnlyList<PageLayoutWarning> Warnings { get; init; } = [];
    public bool IsApproximate { get; init; }
}
```

Implementační princip:
- **Fáze MVP:** explicitní `PageBreakBlock`, vizuální A4 stránky, bloky se stránkují pouze mezi bloky. Pokud blok přeteče, zobrazí se layout warning a nepokoušíme se o destruktivní split.
- **Fáze Word-like pagination:** měření line boxes přes DOM/JS, split paragraphů podle řádků, split tabulek podle řádků, footnote area a anchor-aware floating objekty.
- **Fáze hardening:** cache měření, invalidace po změně stylu/šířky/fontu, virtualization pro dlouhé dokumenty a E2E screenshot diff.

Tento plán zachovává dlouhodobý cíl Word-like stránkování, ale explicitně uznává, že jde o samostatný engine s vlastními testy, ne o vedlejší detail renderingu.

### 7.2 Header/Footer editace

- Header a footer jsou **samostatné contenteditable regiony** nad/pod stránkou.
- Kliknutí na header/footer aktivuje jejich editaci.
- Mají vlastní model (`HeadersFooters[]`), který se serializuje do JSON.
- Oddělené first-page / even-odd header/footer podle section properties.

### 7.3 Section Breaks

```csharp
public class SectionBreakBlock : Block
{
    public override string Type => "sectionBreak";
    public SectionProperties NewSectionProperties { get; set; } = new();
}
```

---

## 8. Table Model – True WYSIWYG

### 8.1 Model

```csharp
public class TableBlock : Block
{
    public override string Type => "table";
    public List<TableRow> Rows { get; } = new();
    public TableProperties? Properties { get; set; }
}

public class TableRow
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public List<TableCell> Cells { get; } = new();
}

public class TableCell
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
    public List<Block> Blocks { get; } = new(); // contenteditable inside cell
}
```

### 8.2 Interakce

- Kliknutí do buňky = focus do contenteditable buňky.
- Tab = další buňka, Shift+Tab = předchozí.
- Výběr buněk = drag nebo Shift+click.
- Context menu / ribbon: Merge cells, Split cell, Insert row/col, Delete row/col.
- Resize sloupců = drag na borderu sloupce.

### 8.3 Table Toolbar

Po kliknutí do tabulky se ribbon změní na **Table Tools** (jako ve Wordu):
- Design: styly tabulky, shading, borders
- Layout: Merge, Split, Cell size, Alignment

---

## 9. Track Changes & Comments – Inline Rendering

### 9.1 Track Changes (Revisions)

V DOM se renderují jako speciální značky:

```html
<span class="tm-revision tm-revision--insertion" data-revision-id="rev-1" data-author="John">
  inserted text
</span>
<span class="tm-revision tm-revision--deletion" data-revision-id="rev-2" data-author="John">
  deleted text
</span>
```

- **Insertion:** Zelené podtržení / zelený text.
- **Deletion:** Červené přeškrtnutí / červený text.
- **Formatting:** Modrý border okolo formátovaného textu.
- Review pane zobrazuje seznam všech změn s Accept/Reject.

### 9.2 Comments

```html
<span class="tm-comment-anchor" data-comment-id="c-1" style="background: #ffeb3b;">
  text with comment
</span>
```

- Comment rail vpravo zobrazuje thread.
- Kliknutí na anchor scrollne k commentu.
- Kliknutí na comment highlightne anchor.
- Více commentů na stejném textu = layered highlights.

---

## 10. Clipboard & Import/Export

### 10.1 Paste

| Zdroj | Zpracování |
|-------|-----------|
| **Word (HTML)** | Parsovat HTML → DocumentModel (zachovat styly, tabulky, obrázky) |
| **Excel (HTML)** | Parsovat jako tabulku |
| **Plain text** | Rozdělit na odstavce podle \n\n |
| **Obrázek** | Upload přes ImageProvider, vložit jako ImageBlock |
| **Web (HTML)** | Sanitizovat, zachovat b/i/u/a/table/img, zbytek dropnout |

### 10.2 Copy

- Pokud je výběr uvnitř editoru: serializovat vybrané bloky do HTML (pro Word) + plain text.
- Pokud je výběr ve Wordu: použít Clipboard API s MIME type `text/html`.

### 10.3 Export

| Formát | Přístup |
|--------|---------|
| **JSON** | Přímá serializace DocumentModel |
| **DOCX** | Existující `Tempo.Blazor.DocumentFormats` balíček |
| **ODT** | Existující `Tempo.Blazor.DocumentFormats` balíček |
| **HTML** | Render DocumentModel → HTML string |
| **PDF** | Export přes provider nebo print-to-pdf |
| **Markdown** | Simplified export |

---

## 11. Ribbon – Plnohodnotný Word-like

### 11.1 Struktura

```
TmDocumentEditorRibbon
├── TmRibbonTabList (Home, Insert, Layout, References, Review, View, TableTools*, PictureTools*)
│   └── TmRibbonTabButton
└── TmRibbonContent (active tab content)
    ├── TmRibbonGroup (Clipboard, Font, Paragraph, Styles, Editing...)
    │   └── TmRibbonControl (Button, Dropdown, SplitButton, ToggleButton, ColorPicker, Input)
```

### 11.2 Kontextové záložky

- Klik do tabulky → zobrazí se záložka **Table Tools** (Design + Layout).
- Klik na obrázek → záložka **Picture Tools** (Format).
- Klik na header/footer → záložka **Header & Footer Tools**.

### 11.3 Commands dispatch

Ribbon neprovádí změny přímo – posílá `DocumentCommand` do `DocumentEngine`.

```csharp
// Příklad: Bold button
private void ToggleBold()
{
    _engine.Execute(new ToggleMarkCommand(new BoldMark()));
}
```

---

## 12. Dopad na Frontend – Kompletní inventura

### 12.1 Co se SMAŽE/PŘEPÍŠE

Tato tabulka je inventura cílového dopadu, ne instrukce k jednorázovému smazání. Implementace musí jít přes paralelní WYSIWYG surface/engine za feature flagem nebo interním přepínačem. Stávající blokový editor, renderer a testy zůstávají funkční, dokud nová cesta nepokryje stejné chování a neprojde stejnou E2E sadou.

| Současný soubor | Akce | Důvod |
|-----------------|------|-------|
| `TmDocumentSurface.razor` | **Přepsat** | Nový contenteditable rendering |
| `TmDocumentSurface.razor.cs` | **Přepsat** | Nový engine, selection, mutation handling |
| `TmDocumentEditableBlock.razor` | **Smazat** | Blokový model zaniká |
| `TmDocumentEditableBlock.razor.cs` | **Smazat** | Blokový model zaniká |
| `TmDocumentBlockRenderer.razor` | **Smazat** | Read-only renderer bude součástí Surface |
| `TmDocumentInlineRenderer.razor` | **Smazat** | Inline rendering bude v Surface |
| `TmDocumentEditorToolbar.razor` | **Přepsat** | Nový ribbon s tab switching |
| `document-editor.js` | **Přepsat** | Nový JS engine (selection, mutation, paste, IME) |
| `_document-editor.css` | **Přepsat** | Nové styly pro WYSIWYG |
| `_document-editor-toolbar.css` | **Přepsat** | Nový ribbon styling |
| `_document-editor-comments.css` | **Upravit** | Comment highlights inline |
| `DocumentEditorKeyboardManager.cs` | **Přepsat** | Nový key handler pro contenteditable |
| `DocumentEditorCommandStack.cs` | **Upravit** | Rozšířit o nové commandy |
| `InMemoryDocumentEditorProvider.cs` | **Upravit** | Přizpůsobit novému modelu |
| `DemoDocumentEditorProvider.cs` | **Upravit** | Seed data pro nový engine |
| `DocumentEditorPage.razor` | **Upravit** | Odstranit staré tlačítka, ponechat jen editor |

### 12.2 Co se VYTVOŘÍ (nové soubory)

| Nový soubor | Popis |
|-------------|-------|
| `DocumentModel.cs` | Nový interní document tree model |
| `DocumentSelection.cs` | Selection model (anchor/focus) |
| `DocumentEngine.cs` | Hlavní engine (mutation → model sync) |
| `DocumentSerializer.cs` | Model ↔ JSON (stávající `DocumentEditorDocument`) |
| `DocumentDomRenderer.cs` | Model → DOM HTML string/content |
| `DocumentDomParser.cs` | DOM → Model (z mutation observer) |
| `DocumentSelectionManager.cs` | C# wrapper pro JS Selection API |
| `DocumentCommand*.cs` | Nové command třídy (~20 souborů) |
| `TmWysiwygSurface.razor` | Nová contenteditable surface komponenta |
| `TmWysiwygSurface.razor.cs` | Logika surface |
| `TmDocumentEditorRibbon.razor` | Nový ribbon shell |
| `TmRibbonTab*.razor` | Obsah jednotlivých záložek |
| `TmRibbonGroup.razor` | Ribbon group komponenta |
| `TmRibbonControls.razor` | Button, Dropdown, Toggle, ColorPicker, SplitButton |
| `TmTableToolbar.razor` | Kontextová záložka pro tabulky |
| `TmImageToolbar.razor` | Kontextová záložka pro obrázky |
| `TmHeaderFooterToolbar.razor` | Kontextová záložka pro header/footer |
| `TmStatusBar.razor` | Status bar dole |
| `TmRuler.razor` | Horizontální ruler |
| `TmDocumentEditorContextMenu.razor` | Right-click context menu |
| `document-editor-wysiwyg.js` | Nový JS engine |
| `_document-editor-wysiwyg.css` | Hlavní CSS |
| `_document-editor-ribbon.css` | Ribbon CSS |
| `_document-editor-ruler.css` | Ruler CSS |
| `_document-editor-status-bar.css` | Status bar CSS |

### 12.3 Co zůstává beze změn

| Soubor | Důvod |
|--------|-------|
| `TmDocumentEditor.razor` (shell) | Zachová se, ale content se změní |
| `TmDocumentCommentRail.razor` | Zůstává, jen se připojí na nový engine |
| `TmDocumentCommentThread.razor` | Zůstává |
| `TmDocumentCommentComposer.razor` | Zůstává |
| `TmDocumentVersionPanel.razor` | Zůstává |
| `TmDocumentVersionDialog.razor` | Zůstává |
| `TmDocumentDiffViewer.razor` | Zůstává (read-only komponenta) |
| `DocumentEditorDocument.cs` (abstractions) | Zůstává jako persistence model |
| Všechny `I*Provider` interfaces | Zůstávají |
| `DocumentFormats` projekt | Zůstává (DOCX/ODT import/export) |

---

## 13. Dopad na Backend – Kompletní inventura

### 13.1 API změny

| Endpoint | Změna | Důvod |
|----------|-------|-------|
| `GET /api/document-editor/{id}` | **Nezmění se** | Stále vrací `DocumentEditorDocument` JSON |
| `POST /api/document-editor/{id}/save` | **Nezmění se** | Příjímá `DocumentEditorDocument` JSON |
| `POST /api/document-editor/import/docx` | **Rozšířit** | Lepší parsování pro nový model (tabulky, styly) |
| `POST /api/document-editor/import/odt` | **Rozšířit** | Lepší parsování pro nový model |
| `GET /api/document-editor/{id}/export/html` | **Nový** | Pro WYSIWYG preview a clipboard |
| `GET /api/document-editor/{id}/export/pdf` | **Nový** | Volitelný – server-side PDF rendering |

### 13.2 Model změny

Stávající `DocumentEditorDocument` se používá jako **serialization contract** mezi klientem a serverem. Nový `DocumentModel` je **pure client-side** interní reprezentace.

Je potřeba napsat **robustní serializátor** mezi:
- `DocumentModel` (nový) ↔ `DocumentEditorDocument` (stávající persistence)

Toto zachovává backwards compatibility API.

### 13.3 Databáze

- **Žádné DB migrace** – JSON schéma zůstává stejné (nebo je rozšířeno).
- Nová pole v JSON: `paragraphProperties`, `fontFamily`, `fontSize`, `textAlignment`, `lineSpacing` atd. jsou optional – staré dokumenty je nemají.

---

## 14. Dopad na Testy – Kompletní inventura

### 14.1 Testy, které se NESMÍ odpojit

Stávající testy se nemažou ani dočasně nepřejmenovávají na `.Legacy`. Jsou bezpečnostní síť pro provider kontrakty, ukládání, komentáře, verze, offline režim, obrázky, signing integraci a format boundary. WYSIWYG redesign musí probíhat tak, aby staré testy buď dál procházely, nebo byly ve stejném kroku nahrazeny ekvivalentním testem stejného chování.

Zakázané postupy:
- dočasné vyřazení celých test souborů,
- přejmenování test projektu na `.Legacy`,
- mazání E2E scénářů bez náhrady,
- změna provider/API kontraktu jen proto, aby prošla nová surface.

Povolený postup:
- přidat nové WYSIWYG testy vedle stávajících,
- upravit UI selektory tam, kde se mění DOM struktura,
- ponechat behaviorální assertion stejný,
- mazat starý test až v commitu, který přidává nový test se stejným nebo vyšším pokrytím.

| Test soubor | Strategie |
|-------------|-----------|
| `TmDocumentEditingTests.cs` | Postupně přepsat na contenteditable interakce, ale zachovat scénáře psaní, undo/redo, inline formatting, insert block. |
| `TmDocumentRendererTests.cs` | Rozdělit na read-only renderer invariants a nové DOM rendering testy. Neztratit image/table/comment rendering. |
| `DocumentEditorCommandTests.cs` | Přidat nové inline command testy vedle block command testů, staré commandy odstranit až po migraci surface. |
| `TmDocumentEditorCssTests.cs` | Zachovat layout/accessibility invariants, aktualizovat selektory až po zavedení nových CSS tříd. |
| `TmDocumentEditorOfflineTests.cs` | Offline sync logika musí zůstat zelená po celou dobu; mění se jen UI cesta k editaci. |

### 14.2 Testy, které se UPRAVÍ (rozšíří/selektory)

| Test soubor | Úprava |
|-------------|--------|
| `TmDocumentEditorTests.cs` | Přepsat na nový ribbon selektory, nové DOM struktury. Zachovat testy provider, save, version, comment. |
| `TmDocumentCollaborationCursorOverlayTests.cs` | Upravit – cursor se nyní renderuje do contenteditable, ne přes overlay. |
| `DocumentEditorE2ETests.cs` | Aktualizovat screenshot baseline. Přidat testy pro inline formátování, table editing, header/footer. |
| `DocumentEditorFormatEndpointTests.cs` | Rozšířit o HTML export test. |

### 14.3 Testy, které ZŮSTANOU beze změn

| Test soubor | Důvod |
|-------------|-------|
| `DocumentEditorModelTests.cs` | Persistence model se nemění. |
| `DocumentEditorBlockAndReviewTests.cs` | Block/review persistence logika zůstává. |
| `DocumentEditorAdvancedFormatTests.cs` | Format model zůstává. |
| `DocumentEditorInMemoryAndAdapterTests.cs` | Adapter zůstává. |
| `DocumentEditorProviderTests.cs` | Provider kontrakty zůstávají. |
| `DocumentEditorSigningIntegrationTests.cs` | Signing se nemění. |
| `DocumentEditorOfflineImageRenditionProviderTests.cs` | Image provider zůstává. |
| `TmDocumentDiffViewerTests.cs` | Diff viewer je read-only, zůstává. |
| `Planning/DocumentEditorPhase0DesignTests.cs` | Design decision tests zůstávají. |

### 14.4 Nové testy, které se VYTVOŘÍ

| Test soubor | Co testuje |
|-------------|-----------|
| `DocumentModelTests.cs` (nový) | Nový DocumentModel tree (nodes, marks, selection). |
| `DocumentSerializerTests.cs` | Serializace nový model ↔ starý JSON. |
| `DocumentDomRendererTests.cs` | Render model → DOM HTML. |
| `DocumentDomParserTests.cs` | Parse DOM mutations → model changes. |
| `DocumentSelectionTests.cs` | Selection manager, position mapping. |
| `DocumentCommandTests.cs` (nový) | Každý command má svůj test: InsertText, DeleteText, ToggleMark, InsertTable, ... |
| `DocumentEngineTests.cs` | End-to-end engine: type text → mutation → model update → re-render. |
| `TmWysiwygSurfaceTests.cs` | Nová surface komponenta: contenteditable, selection sync, paste. |
| `TmRibbonTests.cs` | Ribbon tabs, groups, controls, command dispatch. |
| `TmRulerTests.cs` | Ruler rendering, margin display. |
| `TmStatusBarTests.cs` | Word count, zoom, page info. |
| `DocumentEditorE2ETests.cs` (rozšířit) | Inline bold/italic, table editing, header/footer edit, paste from Word. |

---

## 15. Implementační TODO List – Po nejmenších krocích (TDD)

> **Základní pravidlo:** Každý bod = RED test → implementace → GREEN test → commit.

### FÁZE 0: Příprava a základy modelu

#### 0.1 Branch a konfigurace
- [ ] **RED:** Vytvořit feature branch `feature/document-editor-wysiwyg`.
- [ ] Nevypínat, nepřejmenovávat ani nemazat stávající testy. Každý redesign krok musí zachovat existující behaviorální safety net.
- [ ] Přidat test tag/kategorii pro nové WYSIWYG testy, aby šly spouštět cíleně vedle existujících testů.
- [ ] Ujistit se, že `dotnet test` projde na čisté branch.

#### 0.2 Nový DocumentModel – základní strom
- [ ] **RED:** Test `DocumentModel` obsahuje `Body` (list `Block`).
- [ ] **RED:** Test `ParagraphBlock` má `Type = "paragraph"` a `Inlines`.
- [ ] **RED:** Test `TextRun` má `Text` a `Marks`.
- [ ] **RED:** Test `BoldMark`, `ItalicMark`, `UnderlineMark` mají správné `Type`.
- [ ] Implementace: `DocumentModel.cs`, `Block.cs`, `Inline.cs`, `Mark.cs` + všechny podtřídy.
- [ ] **GREEN:** Model testy projdou.

#### 0.3 DocumentSelection model
- [ ] **RED:** Test `DocumentSelection.IsCollapsed` je true když anchor == focus.
- [ ] **RED:** Test `DocumentSelection.Start/End` vrací správný směr.
- [ ] **RED:** Test `DocumentPosition` má BlockId, InlineIndex, TextOffset.
- [ ] Implementace: `DocumentSelection.cs`, `DocumentPosition.cs`.
- [ ] **GREEN:** Selection testy projdou.

#### 0.4 DocumentSerializer (nový model ↔ starý JSON)
- [ ] **RED:** Test serializace prázdného `DocumentModel` → `DocumentEditorDocument` JSON.
- [ ] **RED:** Test serializace paragraphu s TextRun → JSON.
- [ ] **RED:** Test deserializace JSON → `DocumentModel` (paragraph, heading, list, table, image).
- [ ] **RED:** Test round-trip: model → JSON → model (data se zachovají).
- [ ] **RED:** Test že starý JSON bez nových polí se deserializuje bez chyby.
- [ ] Implementace: `DocumentSerializer.cs`.
- [ ] **GREEN:** Serializer testy projdou.

---

### FÁZE 1: DOM Rendering Engine

#### 1.1 DocumentDomRenderer – Paragraph + TextRun
- [ ] **RED:** Test render prázdného modelu → `<div class="tm-document-wysiwyg"><div class="tm-page"><div class="tm-page-body"></div></div></div>`.
- [ ] **RED:** Test render jednoho paragraphu s TextRun → `<p><span>Hello</span></p>`.
- [ ] **RED:** Test render paragraphu s BoldMark → `<p><span style="font-weight: bold">Hello</span></p>`.
- [ ] **RED:** Test render heading level 2 → `<h2><span>Title</span></h2>`.
- [ ] Implementace: `DocumentDomRenderer.cs`.
- [ ] **GREEN:** Renderer testy projdou.

#### 1.2 Render – Lists
- [ ] **RED:** Test render 2 ordered list items → `<ol><li><span>Item 1</span></li><li><span>Item 2</span></li></ol>`.
- [ ] **RED:** Test render nested list (indent) → správná HTML struktura.
- [ ] **GREEN:** List render testy projdou.

#### 1.3 Render – Tables
- [ ] **RED:** Test render 2×2 table → `<table><tbody><tr><td><p>...</p></td>...</tr></tbody></table>`.
- [ ] **RED:** Test render colspan/rowspan.
- [ ] **GREEN:** Table render testy projdou.

#### 1.4 Render – Images
- [ ] **RED:** Test render inline image → `<img src="...">` uvnitř `<p>`.
- [ ] **RED:** Test render floating image → `<div class="floating-image">`.
- [ ] **GREEN:** Image render testy projdou.

#### 1.5 Render – Page structure (Header/Footer)
- [ ] **RED:** Test render stránky s header → `<header contenteditable>...</header>`.
- [ ] **RED:** Test render stránky s footer → `<footer contenteditable>...</footer>`.
- [ ] **RED:** Test render A4 dimensions v CSS.
- [ ] **GREEN:** Page structure testy projdou.

#### 1.6 Render – Marks (kompletní)
- [ ] **RED:** Test render Italic → `<i>` nebo `<span style="font-style: italic">`.
- [ ] **RED:** Test render Underline → `<u>`.
- [ ] **RED:** Test render Strikethrough → `<s>`.
- [ ] **RED:** Test render Subscript/Superscript → `<sub>`/`<sup>`.
- [ ] **RED:** Test render FontMark → `<span style="font-family: X; font-size: Y">`.
- [ ] **RED:** Test render ColorMark → `<span style="color: #rrggbb">`.
- [ ] **RED:** Test render HighlightMark → `<mark style="background-color: Y">`.
- [ ] **RED:** Test render LinkMark → `<a href="...">`.
- [ ] **RED:** Test kombinace více marks na jednom TextRun.
- [ ] **GREEN:** Marks render testy projdou.

---

### FÁZE 2: JS Interop Layer

#### 2.1 Selection API
- [ ] **RED:** Test JS `tmDocumentEditor.getSelection` vrací objekt s blockId/inlineId/offset.
- [ ] Implementace: `document-editor-wysiwyg.js` – `getSelection()`.
- [ ] **RED:** Test C# `DocumentSelectionManager.GetSelectionAsync` volá JS a vrací `DocumentSelection`.
- [ ] Implementace: `DocumentSelectionManager.cs`.
- [ ] **GREEN:** Selection API testy projdou.

#### 2.2 Mutation Observer
- [ ] **RED:** Test že po napsání textu do contenteditable se vyvolá `OnDomMutated` callback.
- [ ] Implementace: `document-editor-wysiwyg.js` – `attachMutationObserver()`.
- [ ] **RED:** Test C# `DocumentEngine` přijme mutation a aktualizuje model.
- [ ] Implementace: `DocumentEngine.cs` – `OnDomMutated()` handler.
- [ ] **GREEN:** Mutation testy projdou.

#### 2.3 IME Handler
- [ ] **RED:** Test že compositionstart/end se správně propaguje do C#.
- [ ] Implementace: IME handler v JS.
- [ ] **GREEN:** IME testy projdou.

#### 2.4 Paste Handler
- [ ] **RED:** Test že paste event zavolá C# `OnPaste` s html a text.
- [ ] Implementace: Paste handler v JS.
- [ ] **GREEN:** Paste handler testy projdou.

#### 2.5 Key Handler (zkratky)
- [ ] **RED:** Test že Ctrl+B zavolá C# `OnShortcut("b", true, false, false)`.
- [ ] Implementace: Key handler v JS.
- [ ] **GREEN:** Key handler testy projdou.

---

### FÁZE 3: Command System

#### 3.1 Command Stack
- [ ] **RED:** Test push command, undo vrátí stav, redo obnoví.
- [ ] **RED:** Test CanUndo/CanRedo/NextUndoName/NextRedoName.
- [ ] Implementace: `DocumentCommandStack.cs` (rozšířit existující nebo nový).
- [ ] **GREEN:** Command stack testy projdou.

#### 3.2 InsertTextCommand
- [ ] **RED:** Test vložení "Hello" na prázdný paragraph vytvoří TextRun.
- [ ] **RED:** Test vložení uprostřed TextRun rozdělí run a vloží nový text.
- [ ] **RED:** Test undo vrátí původní text.
- [ ] Implementace: `InsertTextCommand.cs`.
- [ ] **GREEN:** InsertText testy projdou.

#### 3.3 DeleteTextCommand
- [ ] **RED:** Test smazání znaku v TextRun zkrátí run.
- [ ] **RED:** Test smazání rozsahu přes více runs spojí zbývající text.
- [ ] Implementace: `DeleteTextCommand.cs`.
- [ ] **GREEN:** DeleteText testy projdou.

#### 3.4 ToggleMarkCommand
- [ ] **RED:** Test toggle Bold na výběru přidá BoldMark ke všem TextRun v rozsahu.
- [ ] **RED:** Test toggle Bold když už je bold – odebere mark.
- [ ] **RED:** Test na collapsed selection (caret) – nastaví mark pro future input.
- [ ] Implementace: `ToggleMarkCommand.cs`.
- [ ] **GREEN:** ToggleMark testy projdou.

#### 3.5 SetBlockTypeCommand
- [ ] **RED:** Test změna paragraph → heading level 2.
- [ ] **RED:** Test změna heading → paragraph.
- [ ] **RED:** Test změna paragraph → list item.
- [ ] Implementace: `SetBlockTypeCommand.cs`.
- [ ] **GREEN:** SetBlockType testy projdou.

#### 3.6 SetParagraphPropertiesCommand
- [ ] **RED:** Test změna alignment na Center.
- [ ] **RED:** Test změna line spacing na 2.0.
- [ ] Implementace: `SetParagraphPropertiesCommand.cs`.
- [ ] **GREEN:** ParagraphProperties testy projdou.

#### 3.7 InsertBlockCommand (PageBreak, Table, Image)
- [ ] **RED:** Test insert page break na pozici kurzoru.
- [ ] **RED:** Test insert table 3×3.
- [ ] **RED:** Test insert image URL.
- [ ] Implementace: `InsertBlockCommand.cs`.
- [ ] **GREEN:** InsertBlock testy projdou.

#### 3.8 RemoveBlockCommand
- [ ] **RED:** Test smazání bloku a merge textu s předchozím blokem.
- [ ] Implementace: `RemoveBlockCommand.cs`.
- [ ] **GREEN:** RemoveBlock testy projdou.

---

### FÁZE 4: Surface Komponenta (Blazor)

#### 4.1 TmWysiwygSurface skeleton
- [ ] **RED:** Test render surface má `contenteditable="true"` root.
- [ ] **RED:** Test render prázdného dokumentu zobrazí prázdnou stránku.
- [ ] Implementace: `TmWysiwygSurface.razor` + `.razor.cs`.
- [ ] **GREEN:** Surface skeleton testy projdou.

#### 4.2 Surface – Render dokumentu
- [ ] **RED:** Test že surface renderuje paragraph s textem.
- [ ] **RED:** Test že surface renderuje heading.
- [ ] **RED:** Test že surface renderuje tabulku.
- [ ] **GREEN:** Surface render testy projdou.

#### 4.3 Surface – Selection sync
- [ ] **RED:** Test že kliknutí do surface aktualizuje `DocumentSelection` v C#.
- [ ] **RED:** Test že `DocumentSelectionManager.SetSelectionAsync` nastaví browser selection.
- [ ] **GREEN:** Selection sync testy projdou.

#### 4.4 Surface – Typing (end-to-end)
- [ ] **RED:** Test že napsání "Hello" do contenteditable aktualizuje C# model.
- [ ] **RED:** Test že model change triggerne re-render (nebo partial update).
- [ ] **RED:** Test že undo po napsání vrátí prázdný dokument.
- [ ] Implementace: Spojení MutationObserver → Engine → Model → Renderer.
- [ ] **GREEN:** Typing E2E testy projdou.

#### 4.5 Surface – Formátování z ribbonu
- [ ] **RED:** Test že Ctrl+B v surface zavolá ToggleMarkCommand(Bold).
- [ ] **RED:** Test že model se aktualizuje a DOM zobrazí bold text.
- [ ] **GREEN:** Formatting testy projdou.

---

### FÁZE 5: Ribbon Redesign

#### 5.1 Ribbon shell – Tab switching
- [ ] **RED:** Test že ribbon má záložky Home, Insert, Layout, References, Review, View.
- [ ] **RED:** Test že klik na záložku změní obsah ribbonu.
- [ ] Implementace: `TmDocumentEditorRibbon.razor`.
- [ ] **GREEN:** Ribbon tab testy projdou.

#### 5.2 Home tab – Font group
- [ ] **RED:** Test Font dropdown obsahuje seznam fontů.
- [ ] **RED:** Test Size dropdown obsahuje Word velikosti.
- [ ] **RED:** Test Bold/Italic/Underline toggle dispatch command.
- [ ] **RED:** Test Color picker dispatch ColorMark command.
- [ ] **GREEN:** Font group testy projdou.

#### 5.3 Home tab – Paragraph group
- [ ] **RED:** Test Align buttons dispatch SetParagraphPropertiesCommand.
- [ ] **RED:** Test Line spacing dropdown dispatch command.
- [ ] **RED:** Test Indent buttons dispatch command.
- [ ] **GREEN:** Paragraph group testy projdou.

#### 5.4 Home tab – Styles group
- [ ] **RED:** Test Style buttons dispatch SetBlockTypeCommand.
- [ ] **GREEN:** Styles group testy projdou.

#### 5.5 Insert tab
- [ ] **RED:** Test Table grid picker zobrazí 10×8 buněk.
- [ ] **RED:** Test výběr v gridu dispatch InsertTableCommand(N,M).
- [ ] **RED:** Test Picture button otevře modal dialog.
- [ ] **GREEN:** Insert tab testy projdou.

#### 5.6 Layout tab
- [ ] **RED:** Test Margins dropdown změní CSS padding stránky.
- [ ] **RED:** Test Orientation toggle změní width/height stránky.
- [ ] **GREEN:** Layout tab testy projdou.

#### 5.7 Review tab
- [ ] **RED:** Test Track Changes toggle dispatch command.
- [ ] **RED:** Test New Comment dispatch command.
- [ ] **GREEN:** Review tab testy projdou.

---

### FÁZE 6: Page Layout – Headers, Footers, Ruler, Status Bar

#### 6.1 Header/Footer editace
- [ ] **RED:** Test že header region má `contenteditable="true"`.
- [ ] **RED:** Test že typing v header aktualizuje `DocumentModel.HeadersFooters`.
- [ ] **RED:** Test že first-page / even-odd header se přepíná podle section properties.
- [ ] Implementace: Header/Footer rendering v Surface.
- [ ] **GREEN:** Header/Footer testy projdou.

#### 6.2 Ruler
- [ ] **RED:** Test ruler existuje nad stránkou.
- [ ] **RED:** Test ruler zobrazuje margin boundaries.
- [ ] **RED:** Test ruler reaguje na změnu margins z Layout tabu.
- [ ] Implementace: `TmRuler.razor`.
- [ ] **GREEN:** Ruler testy projdou.

#### 6.3 Status Bar
- [ ] **RED:** Test status bar zobrazuje počet slov.
- [ ] **RED:** Test status bar zobrazuje aktuální stranu.
- [ ] **RED:** Test zoom slider změní CSS scale stránky.
- [ ] Implementace: `TmStatusBar.razor`.
- [ ] **GREEN:** Status bar testy projdou.

---

### FÁZE 7: Tables – True WYSIWYG

#### 7.1 Table rendering v surface
- [ ] **RED:** Test že tabulka v contenteditable je editovatelná (kliknutí do buňky).
- [ ] **RED:** Test Tab navigace mezi buňkami.
- [ ] **GREEN:** Table navigation testy projdou.

#### 7.2 Table commands
- [ ] **RED:** Test Insert row above/below.
- [ ] **RED:** Test Insert column left/right.
- [ ] **RED:** Test Delete row/column.
- [ ] **RED:** Test Merge cells.
- [ ] **RED:** Test Split cell.
- [ ] Implementace: Table commandy.
- [ ] **GREEN:** Table commands testy projdou.

#### 7.3 Table Tools ribbon (contextual)
- [ ] **RED:** Test že kliknutí do tabulky zobrazí Table Tools záložku.
- [ ] **RED:** Test Table Tools obsahuje Merge, Split, Distribute.
- [ ] **GREEN:** Table Tools testy projdou.

---

### FÁZE 8: Images – Inline & Floating

#### 8.1 Inline images
- [ ] **RED:** Test insert image URL vloží `<img>` do paragraphu.
- [ ] **RED:** Test resize image změní width/height.
- [ ] **GREEN:** Inline image testy projdou.

#### 8.2 Floating images
- [ ] **RED:** Test změna layout na floating vytvoří `ImageAnchor` navázaný na aktuální odstavec.
- [ ] **RED:** Test `MoveWithText=true` posune obrázek společně s anchor odstavcem při vložení textu nad ním.
- [ ] **RED:** Test `LockAnchor=true` zabrání přesunu anchoru přes UI drag.
- [ ] **RED:** Test `Square` wrap mód vytvoří text exclusion box a okolní text ho obtéká.
- [ ] **RED:** Test `TopAndBottom` wrap mód nepustí text vlevo/vpravo od obrázku.
- [ ] **RED:** Test `BehindText` a `InFrontOfText` respektují z-index vůči textové vrstvě.
- [ ] **RED:** Test drag image změní relativní X/Y pozici vůči zvolenému anchor reference frame.
- [ ] **RED:** Test resize image změní modelovou velikost a zachová anchor/wrap metadata.
- [ ] **RED:** Test DOCX export/import roundtrip zachová anchor, wrap mód, pozici a velikost pro podporovanou podmnožinu.
- [ ] **GREEN:** Floating image testy projdou pro první podporovanou Word-compatible podmnožinu.

#### 8.3 Image Tools ribbon (contextual)
- [ ] **RED:** Test Picture Tools záložka se zobrazí po kliknutí na obrázek.
- [ ] **GREEN:** Picture Tools testy projdou.

---

### FÁZE 9: Track Changes & Comments (Inline)

#### 9.1 Track Changes rendering
- [ ] **RED:** Test insertion revision renderuje se zeleným podtržením.
- [ ] **RED:** Test deletion revision renderuje s červeným přeškrtnutím.
- [ ] **GREEN:** Track changes render testy projdou.

#### 9.2 Track Changes commands
- [ ] **RED:** Test Accept revision odstraní revision mark a ponechá text.
- [ ] **RED:** Test Reject revision odstraní text (insertion) nebo obnoví text (deletion).
- [ ] **GREEN:** Track changes commands testy projdou.

#### 9.3 Comments inline
- [ ] **RED:** Test Add Comment vytvoří highlight span s `data-comment-id`.
- [ ] **RED:** Test comment thread se zobrazí v comment rail.
- [ ] **RED:** Test kliknutí na comment scrollne k anchoru.
- [ ] **GREEN:** Comments inline testy projdou.

---

### FÁZE 10: Clipboard & Paste

#### 10.1 Paste plain text
- [ ] **RED:** Test paste "Hello\n\nWorld" vytvoří 2 paragraphy.
- [ ] **GREEN:** Paste plain text testy projdou.

#### 10.2 Paste HTML (Word/Excel)
- [ ] **RED:** Test paste HTML s `<b>` vytvoří BoldMark.
- [ ] **RED:** Test paste HTML table vytvoří TableBlock.
- [ ] **RED:** Test paste HTML s obrázkem vytvoří ImageBlock.
- [ ] Implementace: HTML parser pro clipboard.
- [ ] **GREEN:** Paste HTML testy projdou.

#### 10.3 Paste image
- [ ] **RED:** Test paste image file zavolá ImageProvider upload.
- [ ] **GREEN:** Paste image testy projdou.

---

### FÁZE 11: Test Updates & E2E

#### 11.1 Přepsání legacy testů
- [ ] Přepsat `TmDocumentEditorTests.cs` pro nový ribbon + surface.
- [ ] Přepsat `TmDocumentEditingTests.cs` pro contenteditable interakce.
- [ ] Přepsat `TmDocumentRendererTests.cs` pro nový renderer.
- [ ] Přepsat `DocumentEditorCommandTests.cs` pro nové commandy.
- [ ] Přepsat `TmDocumentEditorCssTests.cs` pro nové CSS.

#### 11.2 E2E screenshot baseline
- [ ] Aktualizovat `DocumentEditorE2ETests.cs` baseline screenshoty.
- [ ] Přidat E2E test: Typing and formatting text.
- [ ] Přidat E2E test: Insert and edit table.
- [ ] Přidat E2E test: Edit header and footer.
- [ ] Přidat E2E test: Paste from Word (fixture DOCX → copy → paste).
- [ ] Přidat E2E test: Track changes accept/reject.
- [ ] Přidat E2E test: Comments add/reply/resolve.

#### 11.3 Localization
- [ ] Přidat nové lokalizační klíče do `TmResources.resx` a `TmResources.cs.resx`.
- [ ] Aktualizovat `MockTmLocalizer` v testech.

---

### FÁZE 12: Polish & Performance

#### 12.1 Virtualizace (pro velké dokumenty)
- [ ] **RED:** Test dokument s 100 stranami se renderuje bez zaseknutí.
- [ ] Implementace: Render pouze visible pages + buffer.
- [ ] **GREEN:** Virtualization testy projdou.

#### 12.2 Debounced save
- [ ] **RED:** Test že auto-save se spustí až po 500ms idle.
- [ ] **GREEN:** Debounce testy projdou.

#### 12.3 Dark mode
- [ ] **RED:** Test dark mode stránka má šedé pozadí, bílý papír.
- [ ] **GREEN:** Dark mode testy projdou.

#### 12.4 Responsive
- [ ] **RED:** Test mobilní šířka zobrazí kompaktní ribbon.
- [ ] **GREEN:** Responsive testy projdou.

---

## 16. Rizika, kompromisy a mitigation

| Riziko | Pravděpodobnost | Dopad | Mitigace |
|--------|----------------|-------|----------|
| **Contenteditable inconsistency mezi prohlížeči** | Vysoká | Střední | Primární `beforeinput/input` pipeline + command pattern; MutationObserver jen guard/fallback. Netlačíme na browser native undo. |
| **DOM/model divergence během živé editace** | Vysoká | Vysoká | Dvoufázový live DOM → model commit režim; žádný full re-render během IME composition; selection restore po minimální reconciliation. |
| **Performance při velkých dokumentech (>50 stran)** | Střední | Vysoká | Fáze 12.1 – virtualizace stránek; renderujeme jen visible viewport. |
| **Word-like pagination** | Vysoká | Vysoká | Samostatný `PageLayoutEngine`, měření přes DOM/JS, cache a postupná podpora: explicit page breaks → block pagination → line/table/footnote splitting. |
| **IME input (CJK jazyky)** | Střední | Vysoká | Fáze 2.3 – dedikovaný IME handler; composition events musí fungovat. |
| **Paste z Wordu je nekonzistentní** | Vysoká | Střední | Sanitizace HTML + graceful degradation; ne všechny Word styly se zachovají. |
| **Tabulky v contenteditable jsou buggy** | Vysoká | Střední | Omezíme podporu na základní operace; komplexní tabulky budou mít limitations. |
| **Floating/anchored images jako ve Wordu** | Vysoká | Vysoká | Navrhnout anchor/wrap/position model hned od začátku; implementovat dlouhodobě po podmnožinách a ověřovat DOCX/ODT roundtripem. |
| **Ztracení focus při re-renderu** | Střední | Střední | SelectionManager musí vždy obnovit selection po re-renderu. |
| **Stávající API contract se mění** | Nízká | Vysoká | Serializer zachovává JSON compatibilitu; provider API se nemění. |
| **Big-bang rewrite rozbije hotové chování** | Střední | Vysoká | Paralelní WYSIWYG surface, feature flag a zákaz odpojování existujících testů bez ekvivalentní náhrady. |
| **Časová náročnost** | Vysoká | Vysoká | Iterativní přístup – Fáze 0–4 je MVP (text + basic formatting), zbytek je iterace. |

---

## 17. Definition of Done (celkový)

- [ ] Uživatel vidí bílou A4 stránku na šedém pozadí.
- [ ] Uživatel může psát text jako ve Wordu (plynule, bez viditelných blokových hranic).
- [ ] Uživatel může aplikovat bold, italic, underline, color, highlight na výběr textu.
- [ ] Uživatel může měnit font a velikost písma.
- [ ] Uživatel může měnit alignment, line spacing, indentation.
- [ ] Uživatel může vkládat nadpisy H1–H6, seznamy, tabulky, obrázky, page breaky.
- [ ] Uživatel může editovat header a footer přímo na stránce.
- [ ] Uživatel vidí a používá Word-like ribbon se záložkami.
- [ ] Uživatel vidí ruler a status bar.
- [ ] Undo/Redo funguje pro všechny operace.
- [ ] Copy/Paste funguje z Wordu, Excelu, webu.
- [ ] Track Changes se zobrazují inline (zelená/červená).
- [ ] Comments se zobrazují inline (highlight) + v sidebaru.
- [ ] Dokument se serializuje do stejného JSON formátu jako předtím (backwards compatible).
- [ ] Všechny testy projdou (bUnit + E2E).
- [ ] Demo stránka funguje na `/document-editor`.

---

*Dokument je živý – bude se aktualizovat během implementace.*
