# TmDocumentEditor: fáze 0 - inventura a návrh API

Datum: 2026-05-10  
Status: Phase 0 approved  
Navazuje na: `planning/document-editor-tdd-todo.md`

## Shrnutí rozhodnutí

`TmDocumentEditor` bude obecná page-oriented komponenta pro authoring strukturovaných dokumentů. Source of truth je interní blokový JSON uložený přes provider v `Tempo.Blazor.Abstractions`. DOCX/ODT import a export bude ve volitelném server-side balíku `Tempo.Blazor.DocumentFormats`, který bude používat demo API jako první reálný konzument.

UI editoru má být ve stylu Wordu: stránka uprostřed, horní toolbar/ribbon se standardními Home/Insert/Review akcemi a dokumentová práce s textem. Bloky jsou interní persistence/rendering/operation detail, ne Notion-like uživatelský model. Slash menu není primární interakce a znak `/` v textu má zůstat běžný znak.

Editor má být signing-ready přes neměnnou `DocumentRendition`, ale `TmDocumentEditor` nesmí přímo referencovat signing komponenty.

## Inventura: `TmRichEditorFull`

Použitelné části:

- toolbar pattern a rozdělení na menší dialogy,
- link/image/table dialog UX,
- mention autocomplete pattern,
- existující `ITokenDataProvider` pro token autocomplete,
- token chip UX včetně hover metadat,
- word/character count,
- základní disabled/read-only režim,
- localized toolbar labels,
- testovací patterny pro tokeny.

Limity pro právní/document editor:

- používá `contenteditable` a `document.execCommand`,
- nemá stabilní semantický dokumentový model,
- nemá page model, section model ani layout model,
- neumí spolehlivý round-trip do DOCX/ODT,
- nemá stabilní anchor systém pro comments, footnotes, revisions a signing placeholders,
- DOM mutace nejsou vhodné jako source of truth,
- není vhodný základ pro operation log, OT/CRDT ani offline merge,
- browser HTML výstup není dostatečně deterministický pro právní audit.

Rozhodnutí: reuse UX nápady a token provider, ne engine.

## Inventura: `TmNotionEditor`

Použitelné části:

- provider-based architektura,
- blokový model a command stack pattern,
- async undo/redo a batch commands,
- comment panel UX,
- page/block history pattern,
- import/export service pattern,
- demo provider pattern přes `Tempo.Blazor.Demo.Api`,
- collaboration shell přes SignalR provider.

Existující bloky použitelné jako inspirace:

- text/paragraph,
- heading,
- quote,
- bullet list,
- numbered list,
- todo/list variants,
- table/table row,
- image,
- file/PDF/media,
- divider,
- code,
- linked page/breadcrumb jako nepřímá inspirace.

Co oddělit:

- Notion page tree a workspace navigace,
- Notion database bloky,
- Notion synced blocks,
- slash menu UX jako primární insert flow,
- Notion-specific page settings,
- last-write-wins collaboration model.

Rozhodnutí: reuse technické patterny, ne Notion UX a ne namespace/modely jako public contract pro document editor.

## Inventura: comments, history a commands

`INotionCommentProvider` podporuje block comments, text-anchor comments, page comments, read tracking, reactions a subscriptions. Pro `TmDocumentEditor` vytvoříme vlastní document comment provider, protože potřebujeme anchory přes bloky, inline ranges, footnotes, headers/footers, floating objects a rendition anchors.

`INotionHistoryProvider` podporuje page versions, restore a diff. Pro `TmDocumentEditor` bude vlastní version provider nad document JSON snapshotem, operation logem a immutable rendition vazbou.

`NotionCommandStack` je dobrý pattern: async command stack, max depth, batch scope, rollback. Pro document editor vznikne `DocumentEditorCommandStack` s document-specific commands.

`NotionHtmlExporter` a `NotionMarkdownExporter` jsou užitečné jako service shape a testovací inspirace. `TmDocumentEditor` bude mít vlastní exportéry, protože musí řešit section properties, headers/footers, notes, revisions a document anchors.

## Demo provider rozhodnutí

Demo integrace bude podobná Notion editoru:

- `Tempo.Blazor.Demo.Api` drží serverové endpointy a mock/persistent store,
- `Tempo.Blazor.Demo.SharedUI` drží HTTP providery pro UI,
- Playwright E2E testuje skutečný průchod UI -> API -> provider -> reload.

Pro document editor vzniknou demo endpointy pro JSON document load/save, comments, versions, images, renditions a document-format import/export.

## Public API Surface

Minimální public komponenta:

```csharp
public sealed partial class TmDocumentEditor
{
    [Parameter, EditorRequired] public string DocumentId { get; set; } = default!;
    [Parameter, EditorRequired] public IDocumentEditorProvider Provider { get; set; } = default!;
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public DocumentEditorMode Mode { get; set; } = DocumentEditorMode.Edit;
    [Parameter] public bool ShowToolbar { get; set; } = true;
    [Parameter] public bool ShowComments { get; set; } = true;
    [Parameter] public bool ShowVersionHistory { get; set; }
    [Parameter] public TimeSpan? AutoSaveInterval { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public EventCallback<DocumentEditorDocument> OnDocumentLoaded { get; set; }
    [Parameter] public EventCallback<DocumentEditorChangeEventArgs> OnDocumentChanged { get; set; }
    [Parameter] public EventCallback<DocumentEditorSaveRequest> OnSaveRequested { get; set; }
    [Parameter] public EventCallback<DocumentVersion> OnVersionCreated { get; set; }
    [Parameter] public EventCallback<DocumentComment> OnCommentCreated { get; set; }
    [Parameter] public EventCallback<DocumentEditorAuditEvent> OnAuditEvent { get; set; }
}
```

Core provider groups:

- `IDocumentEditorProvider`,
- `IDocumentCommentProvider`,
- `IDocumentVersionProvider`,
- `IDocumentImageProvider`,
- `IDocumentOfflineStore`,
- `IDocumentSyncProvider`,
- `IDocumentRenditionProvider`,
- `IDocumentTokenValueProvider`.

Document format package contracts:

- `IDocumentFormatImporter`,
- `IDocumentFormatExporter`,
- `DocumentFormatPackage`,
- `DocumentFormatImportResult`,
- `DocumentFormatExportResult`,
- `DocumentFormatCompatibilityWarning`.

## Interní JSON model

První schema:

- `schemaVersion`,
- `documentId`,
- `metadata`,
- `pageSettings`,
- `sections`,
- `blocks`,
- `comments`,
- `versions` přes provider, ne nutně vložené v dokumentu,
- `notes`,
- `headersFooters`,
- `revisions`,
- `assets`,
- `anchors`.

Pravidla:

- JSON snapshot je source of truth.
- DOCX/ODT nejsou source of truth.
- Provider ukládá normalized JSON snapshot.
- Každý snapshot má stabilní schema version.
- Migrační hook bude `IDocumentSchemaMigrator`.
- Operation log je budoucí delta vrstva nad snapshotem.
- CRDT/OT vyžaduje nejdříve deterministic operation model a conflict tests.

## Volitelný balík `Tempo.Blazor.DocumentFormats`

Finální název: `Tempo.Blazor.DocumentFormats`.

Projekty:

- `src/Tempo.Blazor.DocumentFormats/Tempo.Blazor.DocumentFormats.csproj`,
- `tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj`.

Package:

- samostatný NuGet,
- reference na `Tempo.Blazor.Abstractions`,
- žádná reference z `Tempo.Blazor` na `Tempo.Blazor.DocumentFormats`,
- demo API bude první reálný konzument.

DOCX strategie:

- použít Open XML SDK (`DocumentFormat.OpenXml`),
- mapovat WordprocessingML do interního JSON modelu,
- import/export běží server-side.

ODT strategie:

- použít ZIP + XML parsing (`System.IO.Compression`, LINQ to XML),
- mapovat `content.xml`, `styles.xml`, `meta.xml` do interního JSON modelu,
- import/export běží server-side.

## DOCX v1 compatibility matrix

Supported:

- paragraphs,
- headings,
- inline marks: bold, italic, underline, strikethrough, superscript, subscript,
- hyperlinks,
- bullet/numbered lists,
- page breaks,
- tables including horizontally and vertically merged cells,
- images from URL/provider asset mapped to DOCX image parts,
- inline images,
- floating/anchored images with anchor, relative position, wrap mode and size,
- headers/footers: primary, first page, even/odd,
- footnotes/endnotes,
- comments,
- section properties: page size, margins, orientation, header/footer references,
- tracked changes as Word revisions: insertions, deletions and basic formatting changes,
- basic metadata.

Normalized:

- style definitions into editor attributes,
- spacing and indentation into nearest supported model,
- complex theme/font information,
- complex floating layout into documented layout properties with tolerance.

Preserved when possible:

- unsupported custom XML parts,
- unknown relationships,
- unsupported media parts,
- custom document properties.

Dropped with warning:

- macros,
- embedded OLE objects,
- SmartArt,
- charts,
- unsupported equations,
- unsupported content controls,
- encrypted/protected document behavior.

## ODT v1 compatibility matrix

Supported:

- paragraphs,
- headings,
- inline marks: bold, italic, underline, strikethrough, superscript, subscript,
- hyperlinks,
- bullet/numbered lists,
- page breaks,
- tables including merged cells,
- images,
- anchored/floating frames where mappable,
- headers/footers,
- footnotes/endnotes,
- annotations/comments,
- page/section styles mapped to section properties,
- tracked changes where ODT representation maps to the revision model,
- basic metadata.

Normalized:

- style definitions into editor attributes,
- text spans into inline marks,
- table styling into supported table properties,
- ODT frame anchoring into common anchor model with tolerance.

Preserved when possible:

- unsupported styles,
- unknown package parts,
- unsupported embedded objects,
- custom metadata.

Dropped with warning:

- macros/scripts,
- complex drawings,
- embedded charts,
- unsupported formulas/equations,
- master document features,
- unsupported change tracking constructs.

## High-risk fallback pravidla

Merged cells:

- v1 supports row/column spans in the model,
- unsupported malformed table grids fail import with compatibility warning.

Headers/footers:

- primary, first page and even/odd are supported,
- unsupported linked-to-previous edge cases are preserved or warned.

Footnotes/endnotes:

- note references and note bodies are supported,
- exotic numbering restarts are normalized to section note settings.

Comments:

- comments are mapped to document comment anchors,
- if an exact text range cannot be restored, the importer creates a degraded block anchor with warning.

Tracked changes:

- insert/delete/basic formatting revisions are supported,
- complex move revisions are preserved if possible or warned.

Floating/anchored layout:

- anchor, wrap mode, relative position, size and order are supported,
- pixel-perfect Word layout is not guaranteed; tests use documented geometric tolerance.

## Fixture a E2E pravidla

Každá high-risk položka musí mít:

- ručně vytvořený DOCX fixture,
- ručně vytvořený ODT fixture, pokud ODT daný prvek podporuje,
- import test,
- export test,
- round-trip test,
- demo API test,
- Playwright smoke test přes demo UI.

## Signing-ready boundary

Editor produkuje `DocumentRendition`, která je immutable a navázaná na `DocumentVersionId`.

Signing workflow používá:

- `DocumentRenditionId`,
- `DocumentVersionId`,
- `DocumentRenditionPage`,
- `DocumentRenditionAnchor`.

`TmDocumentEditor` nesmí přímo referencovat signing komponenty. Souřadnice signing polí se ukládají vůči konkrétní rendition, ne vůči živému dokumentu.
