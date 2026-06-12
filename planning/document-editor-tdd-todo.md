# TODO: TmDocumentEditor pro Tempo.Blazor

Datum založení: 2026-05-10  
Motivace: rozšířit Tempo.Blazor o editor právních/strukturovaných dokumentů použitelný v Advocatus CRM/DMS  
Styl práce: TDD, malé kroky, průběžné demo, průběžné odškrtávání hotových bodů  
Navazuje na existující: `TmRichEditorFull`, `TmMarkdownEditor`, `TmNotionEditor` pouze technicky/modelově, komentáře, notifikace, PDF viewer, signing komponenty  

## Strategické rozhodnutí

`TmRichEditorFull` je dobrý lightweight HTML editor, ale stojí na `contenteditable` + `document.execCommand`. To je pro právní editor s verzemi, komentáři, budoucím diffem a exportem příliš křehký základ.

Nový editor stavět jako samostatný modul `DocumentEditor`:

- veřejná komponenta: `TmDocumentEditor`
- interní menší komponenty: Word-like toolbar/ribbon, page surface, block renderer, comment rail, version panel
- modely a providery v `Tempo.Blazor.Abstractions`
- vstup pro samotnou komponentu je interní blokový JSON
- source of truth pro hostitelskou aplikaci je interní JSON
- DOCX a LibreOffice/ODT jsou podporované přes samostatný volitelný server-side balík
- interní editor používá page-oriented sémantický dokumentový model od první verze
- uživatelské UI je dokumentový editor ve stylu Wordu, ne blokový/Notion editor
- bloky jsou interní persistence/rendering/operation detail, ne primární mentální model uživatele
- první real-time verze vzniká postupně přes vlastní operation log a conflict model
- první verze bez snahy dokonale nahradit Microsoft Word
- první verze core komponenty musí umět bezpečně načíst blokový JSON, editovat, ukládat, verzovat, komentovat a připravit se na offline režim
- první verze core komponenty musí podporovat obrázky vložené URL, přes provider/upload a ze schránky
- první volitelný document-format balík musí umět import/export minimálního DOCX/ODT do/z interního JSON modelu

## Produktová rozhodnutí

- [x] Hlavní komponenta se jmenuje `TmDocumentEditor`.
- [x] Editor je page-oriented od první verze.
- [x] Source of truth je interní blokový JSON.
- [x] Abstractions pro core editor pracují s blokovým JSONem, ne s DOCX/ODT streamem.
- [x] DOCX/ODT import/export je v samostatném projektu a samostatném NuGet balíčku.
- [x] Aplikace, která chce DOCX/ODT, přidá volitelný balík do API/server vrstvy.
- [x] Komentáře mají mít datový model a provider interface v abstractions.
- [x] Právní číslování odstavců není v první vlně.
- [x] Offline podpora se promýšlí od začátku přes správné interface hranice.
- [x] Obrázky patří do první verze: URL, provider/upload a vložení ze schránky.
- [x] Editor má být použitelný jako authoring vrstva pro signing aplikaci, ale bez přímé závislosti na signing komponentách.
- [x] UI má být Word-like: stránka, pás nástrojů/ribbon, standardní insert/format/review akce; žádné Notion-style slash menu jako hlavní interakce.
- [ ] Token provider: reuse existujícího `ITokenDataProvider` pro autocomplete, ale doplnit hodnotový/templating provider.

DOCX/ODT nejsou source of truth v databázi. Jsou výměnné formáty:

- DOCX jako hlavní právní kancelářský import/export formát,
- LibreOffice/OpenDocument Text (`.odt`) jako druhý import/export formát,
- HTML/Markdown pouze jako pomocné exporty pro preview, diff, testování a integrace.

Editor nebude editovat přímo syrové DOCX/ODT XML v DOMu. Bude mít interní sémantický model a volitelnou import/export vrstvu:

- volitelný server-side balík načte DOCX/ODT package,
- mapper převede podporované části na `DocumentEditorDocument`,
- editor pracuje nad interním blokovým JSON modelem,
- provider ukládá interní JSON jako source of truth,
- volitelný server-side balík vytvoří nové DOCX/ODT package,
- round-trip testy hlídají, co se zachová a co se vědomě znormalizuje.

Vlastní OT/CRDT engine není zakázaný. Jen se nesmí začít jako magická velká koule. Nejdřív musí existovat deterministický operation model, testy konfliktů, log operací a až potom síťová spolupráce.

## Non-goals pro první implementační vlnu

- [ ] Neimplementovat real-time spolupráci bez předchozího operation logu.
- [ ] Neslibovat plnou DOCX/ODT round-trip kompatibilitu bez testované compatibility matrix.
- [ ] Neřešit kvalifikovaný elektronický podpis.
- [ ] Neřešit OCR.
- [ ] Neřešit soudní/eIDAS compliance uvnitř komponentové knihovny.
- [ ] Nezabudovat Advocatus doménové entity přímo do Tempo.Blazor.
- [ ] Nepřepisovat existující `TmRichEditorFull`, dokud nový editor nemá stabilní API.
- [ ] Nedělat Notion-like slash menu jako primární způsob vkládání obsahu.

## Cílový první výsledek

Po dokončení první stabilní vlny má uživatel knihovny umět:

- vložit `TmDocumentEditor` do Blazor aplikace,
- dodat mu `IDocumentEditorProvider`,
- otevřít dokument, který je interně uložený jako blokový JSON,
- editovat nadpisy, odstavce, seznamy a jednoduché tabulky,
- používat Word-like toolbar/ribbon a klávesové zkratky,
- přidávat komentáře k bloku a později k textovému rozsahu,
- vytvořit explicitní verzi dokumentu,
- zobrazit historii verzí,
- importovat DOCX,
- exportovat DOCX,
- importovat ODT,
- exportovat ODT,
- exportovat HTML a Markdown jako pomocné formáty,
- zachytit save/version/comment události v auditním systému hostitelské aplikace.

## Otevřené produktové otázky

- [x] Rozhodnout, zda bude hlavní komponenta pojmenovaná `TmDocumentEditor` nebo `TmLegalDocumentEditor` - `TmDocumentEditor`.
- [x] Rozhodnout, zda má být dokument pageless jako Google Docs, nebo page-oriented už od první verze - page-oriented.
- [x] Rozhodnout, zda první verze ukládá blokový JSON vedle původního DOCX/ODT package - core ukládá blokový JSON; DOCX/ODT jsou volitelné import/export artefakty.
- [x] Rozhodnout, zda storage source of truth je DOCX/ODT package, interní JSON, nebo obojí - interní JSON.
- [ ] Rozhodnout minimální DOCX compatibility matrix pro první release.
- [ ] Rozhodnout minimální ODT compatibility matrix pro první release.
- [ ] Rozhodnout, zda HTML export bude garantovaný public contract, nebo jen preview format.
- [ ] Rozhodnout, zda Markdown export má být jen pomocný.
- [ ] Rozhodnout, zda šablonové proměnné mají používat existující token provider z rich editoru - předběžně ano pro autocomplete, rozšířit pro value resolution.
- [x] Rozhodnout, zda komentáře klienta mají být součástí editoru, nebo pouze datovým modelem - datový model a provider v abstractions; UI obecné, hostitel řeší role.
- [x] Rozhodnout, zda právní číslování odstavců bude v první vlně - ne.
- [x] Rozhodnout, zda se má v první vlně podporovat offline draft - ano, návrhem abstrakcí od začátku.

## Pravidla implementace

- [ ] Každý public `[Parameter]` má XML dokumentaci.
- [ ] Všechny uživatelské texty jdou přes `ITmLocalizer`.
- [ ] EN klíče jsou v `TmResources.resx`.
- [ ] CS klíče jsou v `TmResources.cs.resx`.
- [ ] Testovací lokalizace je doplněná v `MockTmLocalizer`.
- [ ] CSS používá `--tm-*` tokeny.
- [ ] Žádné nové hardcoded barvy mimo `transparent`, `none`, `currentColor`.
- [ ] Každá významná komponenta má bUnit test.
- [ ] Každá fáze má demo scénář v `Tempo.Blazor.Demo.SharedUI`.
- [ ] Každý vertikální řez má průběžný Playwright E2E test, ne až závěrečnou E2E fázi.
- [ ] E2E testy vznikají hned po demo/API zapojení daného řezu.
- [ ] Interaktivní editing má Playwright test před rozšiřováním další velké funkce.
- [ ] Import/export má API integrační test a E2E test přes demo UI.
- [ ] JS interop musí být SSR/test tolerantní.
- [ ] Každý JS callback do Blazoru používá dispatcher-safe pattern.
- [ ] Nezavádět doménové závislosti na Advocatus.

## Průběžné E2E pravidlo

E2E není závěrečná kontrola. Každý vertikální řez musí končit malým end-to-end scénářem:

- model/provider testy,
- komponentové bUnit testy,
- demo API endpoint, pokud řez vyžaduje server,
- demo SharedUI stránka nebo scénář,
- Playwright test proti demu.

To platí zejména pro:

- první načtení JSON dokumentu,
- editaci a save,
- offline draft recovery,
- komentáře,
- verze,
- DOCX import přes demo API,
- DOCX export přes demo API,
- ODT import přes demo API,
- ODT export přes demo API,
- vložení obrázku přes URL,
- upload obrázku přes provider,
- vložení obrázku ze schránky,
- vytvoření neměnné document rendition pro podpisový workflow.

## Fáze 0: Inventura a návrh API

### 0.1 Inventura existujících editorů

- [x] Projít `src/Tempo.Blazor/Components/Activity/TmRichEditorFull.razor`.
- [x] Sepsat funkce použitelné z `TmRichEditorFull`.
- [x] Sepsat limity `execCommand` implementace.
- [x] Projít `src/Tempo.Blazor/Components/NotionEditor`.
- [x] Sepsat existující bloky použitelné pro dokumenty.
- [x] Projít `INotionCommentProvider`.
- [x] Projít `INotionHistoryProvider`.
- [x] Projít `NotionCommandStack`.
- [x] Projít `NotionHtmlExporter`.
- [x] Projít `NotionMarkdownExporter`.
- [x] Projít demo providery v `Tempo.Blazor.Demo.SharedUI/Services`.
- [x] Zapsat rozhodnutí, co reuse a co oddělit.

### 0.2 Návrh public API

- [x] RED: vytvořit API approval test pro public typy document editoru.
- [x] Navrhnout minimální parametry `TmDocumentEditor`.
- [x] Navrhnout `DocumentId`.
- [x] Navrhnout `Provider`.
- [x] Navrhnout `ReadOnly`.
- [x] Navrhnout `Mode`.
- [x] Navrhnout `ShowToolbar`.
- [x] Navrhnout `ShowComments`.
- [x] Navrhnout `ShowVersionHistory`.
- [x] Navrhnout `AutoSaveInterval`.
- [x] Navrhnout `Class`.
- [x] Navrhnout `OnDocumentLoaded`.
- [x] Navrhnout `OnDocumentChanged`.
- [x] Navrhnout `OnSaveRequested`.
- [x] Navrhnout `OnVersionCreated`.
- [x] Navrhnout `OnCommentCreated`.
- [x] Navrhnout `OnAuditEvent`.
- [x] GREEN: API approval test projde pro první návrh.

### 0.3 Rozhodnutí o formátu dokumentu

- [x] Navrhnout blokový JSON model.
- [x] Definovat rozdíl mezi interním modelem, volitelným DOCX package, volitelným ODT package a exportním HTML.
- [x] Definovat stabilní `SchemaVersion`.
- [x] Definovat migrační hook pro budoucí verze schématu.
- [x] Definovat pravidlo, zda provider ukládá celý snapshot jako JSON, DOCX/ODT stream, nebo oba artefakty - core provider ukládá JSON snapshot.
- [x] Definovat prostor pro budoucí delta ukládání.
- [x] Přidat dokumentační poznámku, že CRDT vyžaduje operation log a compatibility testy.

### 0.4 Volitelný import/export balík

- [x] Navrhnout nový projekt `src/Tempo.Blazor.DocumentFormats/`.
- [x] Navrhnout test projekt `tests/Tempo.Blazor.DocumentFormats.Tests/`.
- [x] Nastavit projekt jako samostatný NuGet balík.
- [x] Rozhodnout finální název balíku: `Tempo.Blazor.DocumentFormats`, `Tempo.Blazor.DocumentIO`, `Tempo.Blazor.DocumentConversion`, nebo jiný.
- [x] Rozhodnout, zda balík bude záviset na OpenXML SDK.
- [x] Rozhodnout, jaká knihovna/strategie se použije pro ODT.
- [x] Definovat server-side použití: API přijme DOCX/ODT, převede na JSON, uloží přes aplikaci.
- [x] Definovat server-side použití: API načte JSON, převede na DOCX/ODT, vrátí soubor.
- [x] Core `Tempo.Blazor` nesmí vyžadovat document format balík.
- [x] Core `Tempo.Blazor.Abstractions` může obsahovat jen obecné modely, které balík mapuje.
- [x] Demo API bude první reálný konzument document-format balíku.
- [x] Demo API bude poskytovat ukázkové endpointy pro import/export dokumentu.
- [x] Demo UI bude volat demo API, ne import/export přímo v browseru.

### 0.5 Compatibility matrix pro DOCX/ODT

- [x] Sepsat podporované DOCX elementy pro první release volitelného balíku.
- [x] Sepsat podporované ODT elementy pro první release volitelného balíku.
- [x] Rozlišit `supported`, `preserved`, `normalized`, `dropped`.
- [x] V1 target: merged table cells jsou podporované, ne pouze preserved.
- [x] V1 target: headers/footers jsou podporované.
- [x] V1 target: footnotes/endnotes jsou podporované.
- [x] V1 target: comments jsou podporované.
- [x] V1 target: section properties jsou podporované.
- [x] V1 target: tracked changes / Word revisions jsou podporované.
- [x] V1 target: floating/anchored image layout má být co nejvěrnější Wordu.
- [x] Definovat podporu odstavců.
- [x] Definovat podporu nadpisů.
- [x] Definovat podporu inline stylů.
- [x] Definovat podporu hyperlinků.
- [x] Definovat podporu seznamů.
- [x] Definovat podporu tabulek včetně merged cells.
- [x] Definovat podporu comments.
- [x] Definovat podporu footnotes.
- [x] Definovat podporu endnotes.
- [x] Definovat podporu headers/footers.
- [x] Definovat podporu section properties.
- [x] Definovat podporu floating/anchored layoutu.
- [x] Definovat podporu page breaks.
- [x] Definovat podporu tracked changes.
- [x] Definovat, které prvky první release pouze zachová bez editace.
- [x] U každé high-risk položky rozhodnout minimální editovatelný rozsah a fallback.
- [x] U každé high-risk položky přidat round-trip fixture dokument.
- [x] U každé high-risk položky přidat E2E smoke scénář.

## Fáze 1: Abstractions modely

### 1.1 Základní dokument

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Models/DocumentEditor/DocumentModelTests.cs`.
- [x] RED: test defaultních hodnot dokumentu.
- [x] RED: test serializace dokumentu do JSON.
- [x] RED: test `SchemaVersion`.
- [x] RED: test prázdného dokumentu.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentEditorDocument.cs`.
- [x] Implementovat `DocumentEditorMetadata`.
- [x] Implementovat `DocumentEditorAuthor`.
- [x] Implementovat `DocumentEditorStatus`.
- [x] Implementovat `DocumentEditorMode`.
- [x] Implementovat `DocumentPageSettings`.
- [x] Implementovat `DocumentPageSize`.
- [x] Implementovat `DocumentPageMargins`.
- [x] Implementovat `DocumentSection`.
- [x] Implementovat `DocumentSectionProperties`.
- [x] Implementovat `DocumentHeaderFooterReference`.
- [x] GREEN: model testy projdou.

### 1.2 Blokový model

- [x] RED: test vytvoření paragraph bloku.
- [x] RED: test vytvoření heading bloku.
- [x] RED: test vytvoření bullet list bloku.
- [x] RED: test vytvoření numbered list bloku.
- [x] RED: test vytvoření quote bloku.
- [x] RED: test vytvoření table bloku.
- [x] RED: test vytvoření table bloku s merged cells.
- [x] RED: test vytvoření table buňky s row span.
- [x] RED: test vytvoření table buňky s column span.
- [x] RED: test vytvoření image bloku s URL.
- [x] RED: test vytvoření image bloku s provider asset id.
- [x] RED: test vytvoření page break bloku.
- [x] RED: test pořadí bloků podle `Order`.
- [x] Implementovat `DocumentBlock`.
- [x] Implementovat `DocumentBlockType`.
- [x] Implementovat `ParagraphBlockContent`.
- [x] Implementovat `HeadingBlockContent`.
- [x] Implementovat `ListBlockContent`.
- [x] Implementovat `QuoteBlockContent`.
- [x] Implementovat `TableBlockContent`.
- [x] Implementovat `TableRowContent`.
- [x] Implementovat `TableCellContent`.
- [x] Implementovat `TableCellMerge`.
- [x] Implementovat `TableCellSpan`.
- [x] Implementovat `ImageBlockContent`.
- [x] Implementovat `DocumentImageSource`.
- [x] Implementovat `DocumentImageAlignment`.
- [x] Implementovat `DocumentImageSize`.
- [x] Implementovat `PageBreakBlockContent`.
- [x] GREEN: blokové testy projdou.

### 1.3 Inline obsah

- [x] RED: test plain text runu.
- [x] RED: test bold marku.
- [x] RED: test italic marku.
- [x] RED: test underline marku.
- [x] RED: test link marku.
- [x] RED: test token/merge field runu.
- [x] RED: test comment anchor marku.
- [x] Implementovat `InlineContent`.
- [x] Implementovat `TextRun`.
- [x] Implementovat `InlineMark`.
- [x] Implementovat `InlineMarkType`.
- [x] Implementovat `LinkMarkData`.
- [x] Implementovat `TokenRun`.
- [x] Implementovat `CommentAnchorMarkData`.
- [x] GREEN: inline testy projdou.

### 1.4 Komentáře

- [x] RED: test komentáře ke bloku.
- [x] RED: test komentáře k textovému rozsahu.
- [x] RED: test komentáře importovaného z DOCX.
- [x] RED: test komentáře importovaného z ODT anotace.
- [x] RED: test thread odpovědí.
- [x] RED: test resolved komentáře.
- [x] RED: test klientského komentáře jako externího autora.
- [x] Implementovat `DocumentComment`.
- [x] Implementovat `DocumentCommentEntry`.
- [x] Implementovat `DocumentCommentAnchor`.
- [x] Implementovat `DocumentCommentAnchorType`.
- [x] Implementovat `DocumentCommentStatus`.
- [x] Implementovat `DocumentCommentVisibility`.
- [x] GREEN: komentářové testy projdou.

### 1.4.1 Footnotes a endnotes

- [x] RED: test footnote reference v inline obsahu.
- [x] RED: test endnote reference v inline obsahu.
- [x] RED: test footnote body s blokovým obsahem.
- [x] RED: test endnote body s blokovým obsahem.
- [x] RED: test více referencí na jednu poznámku, pokud bude podporováno.
- [x] RED: test číslování poznámek podle section settings.
- [x] Implementovat `DocumentNote`.
- [x] Implementovat `DocumentNoteType`.
- [x] Implementovat `DocumentNoteReferenceRun`.
- [x] Implementovat `DocumentNoteNumbering`.
- [x] GREEN: footnote/endnote testy projdou.

### 1.4.2 Headers a footers

- [x] RED: test primary header.
- [x] RED: test primary footer.
- [x] RED: test first page header/footer.
- [x] RED: test even/odd header/footer.
- [x] RED: test header/footer navázaný na section.
- [x] RED: test header/footer obsahuje odstavce a obrázky.
- [x] Implementovat `DocumentHeaderFooter`.
- [x] Implementovat `DocumentHeaderFooterType`.
- [x] Implementovat `DocumentHeaderFooterScope`.
- [x] GREEN: header/footer model testy projdou.

### 1.4.3 Tracked changes / revisions

- [x] RED: test inserted text revision.
- [x] RED: test deleted text revision.
- [x] RED: test formatting change revision.
- [x] RED: test moved content revision, pokud bude podporováno ve v1.
- [x] RED: test revision author.
- [x] RED: test revision timestamp.
- [x] RED: test accept revision.
- [x] RED: test reject revision.
- [x] Implementovat `DocumentRevision`.
- [x] Implementovat `DocumentRevisionType`.
- [x] Implementovat `DocumentRevisionRange`.
- [x] Implementovat `DocumentRevisionAuthor`.
- [x] Implementovat `DocumentRevisionAction`.
- [x] GREEN: tracked changes model testy projdou.

### 1.4.4 Floating/anchored layout

- [x] RED: test inline image layout.
- [x] RED: test floating image anchored to paragraph.
- [x] RED: test horizontal position relative to page.
- [x] RED: test horizontal position relative to margin.
- [x] RED: test vertical position relative to paragraph.
- [x] RED: test text wrapping square.
- [x] RED: test text wrapping tight jako preserved/normalized fallback.
- [x] RED: test z-index/order více floating objektů.
- [x] Implementovat `DocumentAnchor`.
- [x] Implementovat `DocumentAnchorType`.
- [x] Implementovat `DocumentFloatingLayout`.
- [x] Implementovat `DocumentWrapMode`.
- [x] Implementovat `DocumentRelativePosition`.
- [x] GREEN: floating/anchored layout model testy projdou.

### 1.5 Verze

- [x] RED: test minor verze.
- [x] RED: test major verze.
- [x] RED: test popisu verze.
- [x] RED: test autora verze.
- [x] RED: test snapshot hash.
- [x] Implementovat `DocumentVersion`.
- [x] Implementovat `DocumentVersionKind`.
- [x] Implementovat `DocumentVersionSnapshot`.
- [x] Implementovat helper pro výpočet stabilního hashe snapshotu.
- [x] GREEN: version testy projdou.

### 1.6 Audit eventy

- [x] RED: test audit eventu pro otevření dokumentu.
- [x] RED: test audit eventu pro změnu obsahu.
- [x] RED: test audit eventu pro vytvoření verze.
- [x] RED: test audit eventu pro komentář.
- [x] RED: test audit eventu pro export.
- [x] Implementovat `DocumentEditorAuditEvent`.
- [x] Implementovat `DocumentEditorAuditAction`.
- [x] Implementovat `DocumentEditorAuditTarget`.
- [x] Implementovat `DocumentEditorAuditResult`.
- [x] GREEN: audit model testy projdou.

### 1.7 Operation model pro budoucí vlastní OT/CRDT

- [x] RED: test insert text operace.
- [x] RED: test delete text operace.
- [x] RED: test add mark operace.
- [x] RED: test remove mark operace.
- [x] RED: test insert block operace.
- [x] RED: test delete block operace.
- [x] RED: test move block operace.
- [x] RED: test set block attribute operace.
- [x] RED: test operace má stable operation id.
- [x] RED: test operace nese author id.
- [x] RED: test operace nese logical timestamp.
- [x] Implementovat `DocumentOperation`.
- [x] Implementovat `DocumentOperationType`.
- [x] Implementovat `DocumentOperationTarget`.
- [x] Implementovat `DocumentOperationMetadata`.
- [x] Implementovat `DocumentOperationBatch`.
- [x] GREEN: operation model testy projdou.

### 1.8 Offline modely

- [x] RED: test draftu navázaného na document id.
- [x] RED: test draftu navázaného na base version id.
- [x] RED: test draft obsahuje JSON snapshot.
- [x] RED: test draft obsahuje operation batches.
- [x] RED: test sync statusu.
- [x] RED: test konfliktu při zastaralé base verzi.
- [x] Implementovat `DocumentOfflineDraft`.
- [x] Implementovat `DocumentOfflineDraftState`.
- [x] Implementovat `DocumentSyncStatus`.
- [x] Implementovat `DocumentSyncConflict`.
- [x] Implementovat `DocumentSyncConflictResolution`.
- [x] GREEN: offline model testy projdou.

### 1.9 Image modely

- [x] RED: test URL obrázku.
- [x] RED: test provider obrázku podle asset id.
- [x] RED: test clipboard obrázku před uploadem jako lokální draft asset.
- [x] RED: test alt textu.
- [x] RED: test caption.
- [x] RED: test width/height.
- [x] RED: test aspect ratio lock.
- [x] RED: test content type whitelist.
- [x] RED: test max file size validation.
- [x] Implementovat `DocumentImageAsset`.
- [x] Implementovat `DocumentImageUploadRequest`.
- [x] Implementovat `DocumentImageUploadResult`.
- [x] Implementovat `DocumentImageResolveRequest`.
- [x] Implementovat `DocumentImageResolveResult`.
- [x] Implementovat `DocumentClipboardImage`.
- [x] Implementovat `DocumentImageValidationOptions`.
- [x] GREEN: image model testy projdou.

### 1.10 Rendition model pro signing a neměnné výstupy

- [x] RED: test rendition odkazuje na konkrétní document id.
- [x] RED: test rendition odkazuje na konkrétní version id.
- [x] RED: test rendition má stabilní hash.
- [x] RED: test rendition obsahuje stránky s rozměry.
- [x] RED: test rendition page má preview image URL nebo provider asset id.
- [x] RED: test rendition může nést PDF attachment id.
- [x] RED: test anchor map vrací normalizované souřadnice na stránce.
- [x] RED: test anchor map umí token/placeholder anchor.
- [x] RED: test rendition je immutable.
- [x] Implementovat `DocumentRendition`.
- [x] Implementovat `DocumentRenditionPage`.
- [x] Implementovat `DocumentRenditionAnchor`.
- [x] Implementovat `DocumentRenditionAnchorType`.
- [x] Implementovat `DocumentRenditionHash`.
- [x] Implementovat `DocumentRenditionStatus`.
- [x] GREEN: rendition model testy projdou.

## Fáze 2: Provider kontrakty

### 2.1 Základní provider

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorProviderTests.cs`.
- [x] RED: test načtení dokumentu podle ID.
- [x] RED: test načtení blokového JSON snapshotu.
- [x] RED: test uložení blokového JSON snapshotu.
- [x] RED: test vytvoření verze.
- [x] RED: test načtení verzí.
- [x] RED: test načtení komentářů.
- [x] RED: test vytvoření komentáře.
- [x] RED: test resolve komentáře.
- [x] Implementovat `IDocumentEditorProvider`.
- [x] Implementovat `IDocumentVersionProvider`, pokud bude lepší oddělit.
- [x] Implementovat `IDocumentCommentProvider`, pokud bude lepší oddělit.
- [x] Implementovat `IDocumentAuditSink`, pokud bude lepší oddělit.
- [x] GREEN: provider kontrakt testy projdou.

### 2.1.1 JSON provider contract

- [x] RED: test provider vrací `DocumentEditorDocument`.
- [x] RED: test provider umí vrátit raw JSON, pokud hostitel nechce materializaci v provideru.
- [x] RED: test provider ukládá normalized JSON.
- [x] RED: test provider vrací concurrency token.
- [x] RED: test save s neplatným concurrency tokenem vrátí konflikt.
- [x] Implementovat `DocumentEditorLoadResult`.
- [x] Implementovat `DocumentEditorSaveRequest`.
- [x] Implementovat `DocumentEditorSaveResult`.
- [x] Implementovat `DocumentEditorConcurrencyMode`.
- [x] GREEN: JSON provider testy projdou.

### 2.1.2 Offline provider boundaries

- [x] RED: test uložení offline draftu.
- [x] RED: test načtení offline draftu.
- [x] RED: test smazání offline draftu po úspěšném syncu.
- [x] RED: test listu pending draftů.
- [x] RED: test submit operation batches při návratu online.
- [x] Implementovat `IDocumentOfflineStore`.
- [x] Implementovat `IDocumentSyncProvider`.
- [x] Implementovat `DocumentOfflineOptions`.
- [x] Implementovat `DocumentSyncRequest`.
- [x] Implementovat `DocumentSyncResult`.
- [x] GREEN: offline provider testy projdou.

### 2.1.3 Image provider boundaries

- [x] RED: test uploadu obrázku ze streamu.
- [x] RED: test resolve URL podle asset id.
- [x] RED: test smazání nepoužitého draft assetu.
- [x] RED: test potvrzení assetu při uložení dokumentu.
- [x] RED: test validace content type.
- [x] RED: test validace velikosti.
- [x] RED: test short-lived URL refresh.
- [x] Navrhnout `IDocumentImageProvider`.
- [x] Navrhnout `IDocumentImageUrlResolver`, pokud nestačí existující `IImageUrlResolver`.
- [x] Reuse existující `IImageUrlResolver`, pokud půjde bez ztráty sémantiky.
- [x] Navrhnout `DocumentImageProviderOptions`.
- [x] Implementovat in-memory image provider pro testy a demo.
- [x] GREEN: image provider testy projdou.

### 2.1.4 Rendition provider boundaries

- [x] RED: test vytvoření rendition z document version.
- [x] RED: test načtení rendition podle id.
- [x] RED: test načtení stránek rendition.
- [x] RED: test načtení anchor map.
- [x] RED: test odmítnutí vytvoření rendition z dirty/unversioned dokumentu.
- [x] RED: test audit eventu pro vytvoření rendition.
- [x] Navrhnout `IDocumentRenditionProvider`.
- [x] Navrhnout `DocumentRenditionRequest`.
- [x] Navrhnout `DocumentRenditionResult`.
- [x] Navrhnout `DocumentRenditionOptions`.
- [x] Implementovat in-memory/demo rendition provider.
- [x] GREEN: rendition provider testy projdou.

### 2.2 In-memory provider pro testy a demo

- [x] RED: test založení ukázkového dokumentu.
- [x] RED: test persistence změny v paměti.
- [x] RED: test verzování v paměti.
- [x] RED: test komentářového threadu v paměti.
- [x] Implementovat `InMemoryDocumentEditorProvider`.
- [x] Implementovat seed pro prázdný dokument.
- [x] Implementovat seed pro smlouvu.
- [x] Implementovat seed pro žalobu/podání.
- [x] GREEN: in-memory provider testy projdou.

### 2.3 Adaptér na Notion model

- [x] RED: test převodu `DocumentEditorDocument` na Notion page/bloky.
- [x] RED: test převodu Notion bloků na document editor bloky.
- [x] RED: test zachování komentářových anchorů při převodu.
- [x] Implementovat `DocumentEditorNotionAdapter`.
- [x] Rozhodnout, zda bude adaptér interní nebo public.
- [x] GREEN: adapter testy projdou.

## Fáze 3: Shell komponenta

### 3.1 `TmDocumentEditor` skeleton

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/DocumentEditor/TmDocumentEditorTests.cs`.
- [x] RED: render bez provideru zobrazí chybový stav.
- [x] RED: render s providerem načte dokument.
- [x] RED: root má třídu `tm-document-editor`.
- [x] RED: `Class` se přidá na root.
- [x] RED: `AdditionalAttributes` se propíšou na root.
- [x] RED: `ReadOnly` přidá modifier.
- [x] Implementovat `src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor`.
- [x] Implementovat `TmDocumentEditor.razor.cs`.
- [x] Implementovat CSS pro `TmDocumentEditor` v `wwwroot/css/components/_document-editor.css`.
- [x] Přidat namespace do `_Imports.razor`, pokud je potřeba.
- [x] Přidat CSS import do `tempo-blazor.css`.
- [x] GREEN: skeleton testy projdou.
- [x] E2E: otevřít demo stránku se skeleton editorem.

### 3.2 Loading, error, empty states

- [x] RED: test loading stavu.
- [x] RED: test error stavu.
- [x] RED: test retry akce.
- [x] RED: test prázdného dokumentu.
- [x] Použít existující `TmSkeleton`.
- [x] Použít existující `TmAlert`.
- [x] Lokalizovat všechny texty.
- [x] GREEN: stavové testy projdou.

### 3.3 Layout editoru

- [x] RED: test Word-like toolbar/ribbon regionu.
- [x] RED: test document surface regionu.
- [x] RED: test comment rail regionu.
- [x] RED: test version panel regionu.
- [x] Implementovat základní layout bez vnořených karet.
- [x] Implementovat page-centered layout podobný Wordu: horní nástroje, dokument uprostřed, review/comment panel volitelně vpravo.
- [x] Implementovat responsive režim pro úzkou šířku.
- [x] Implementovat CSS s tokeny.
- [x] GREEN: layout testy projdou.

## Fáze 4: Render dokumentu

### 4.1 Read-only renderer

- [x] RED: test renderu paragraph bloku.
- [x] RED: test renderu heading 1.
- [x] RED: test renderu heading 2.
- [x] RED: test renderu bullet listu.
- [x] RED: test renderu numbered listu.
- [x] RED: test renderu quote.
- [x] RED: test renderu table.
- [x] RED: test renderu image URL bloku.
- [x] RED: test renderu provider image bloku.
- [x] RED: test renderu image alt textu.
- [x] RED: test renderu image caption.
- [x] RED: test renderu page break.
- [x] Implementovat `TmDocumentBlockRenderer`.
- [x] Implementovat `TmDocumentInlineRenderer`.
- [x] Implementovat sanitizovaný render textu.
- [x] Implementovat render linku s bezpečnými atributy.
- [x] Implementovat bezpečný render image URL.
- [x] Implementovat provider image URL resolving.
- [x] Implementovat loading state obrázku.
- [x] Implementovat broken image state.
- [x] GREEN: renderer testy projdou.
- [x] E2E: načíst ukázkový JSON dokument z demo API a ověřit text v prohlížeči.
- [x] E2E: načíst ukázkový JSON dokument s obrázkem a ověřit zobrazení obrázku.

### 4.2 Document surface

- [x] RED: test root třídy `tm-document-surface`.
- [x] RED: test read-only surface není editovatelný.
- [x] RED: test edit surface má správné ARIA atributy.
- [x] Implementovat `TmDocumentSurface`.
- [x] Přidat parameter pro `PageMode`.
- [x] Přidat parameter pro `Width`.
- [x] Přidat parameter pro `MaxWidth`.
- [x] GREEN: surface testy projdou.

### 4.3 Typografie dokumentu

- [x] Přidat CSS pro dokumentový text.
- [x] Přidat CSS pro nadpisy.
- [x] Přidat CSS pro seznamy.
- [x] Přidat CSS pro citace.
- [x] Přidat CSS pro tabulky.
- [x] Přidat CSS pro page break.
- [x] Ověřit světlý režim.
- [x] Ověřit tmavý režim.
- [x] Ověřit mobilní šířku.

## Fáze 5: Editace bloků

### 5.1 Aktivní blok a selection state

- [x] RED: test nastavení aktivního bloku po kliknutí.
- [x] RED: test zrušení aktivního bloku.
- [x] RED: test read-only blok nelze aktivovat pro editaci.
- [x] Implementovat `DocumentEditorSelectionState`.
- [x] Implementovat `ActiveBlockId`.
- [x] Implementovat `FocusedInlineRange` jako připravený model.
- [x] GREEN: selection testy projdou.

### 5.2 Editace paragraph bloku

- [x] RED: test input změny odstavce.
- [x] RED: test `ValueChanged`/provider save není volán při každém renderu.
- [x] RED: test Enter vytvoří nový odstavec.
- [x] RED: test Backspace v prázdném odstavci sloučí blok.
- [x] Implementovat editable paragraph komponentu.
- [x] Implementovat debounced local change.
- [x] Implementovat update command.
- [x] GREEN: paragraph editing testy projdou.
- [x] E2E: upravit odstavec, uložit a ověřit reload z demo API.

### 5.3 Editace heading bloku

- [x] RED: test změny textu headingu.
- [x] RED: test změny levelu headingu.
- [x] RED: test Enter za headingem vytvoří paragraph.
- [x] Implementovat editable heading.
- [x] GREEN: heading testy projdou.

### 5.4 Editace listů

- [x] RED: test vytvoření bullet list itemu.
- [x] RED: test Enter vytvoří další item.
- [x] RED: test Enter v prázdném itemu ukončí list.
- [x] RED: test Tab zvýší indent.
- [x] RED: test Shift+Tab sníží indent.
- [x] Implementovat editable list block.
- [x] Implementovat normalizaci list itemů.
- [x] GREEN: list testy projdou.

### 5.5 Editace tabulek

- [x] RED: test editace buňky.
- [x] RED: test přidání řádku.
- [x] RED: test přidání sloupce.
- [x] RED: test smazání řádku.
- [x] RED: test smazání sloupce.
- [x] RED: test merge vybraných buněk.
- [x] RED: test split merged buňky.
- [x] RED: test editace obsahu merged buňky.
- [x] RED: test navigace klávesnicí přes merged cells.
- [x] Implementovat basic table editor.
- [x] Implementovat merged cell editor.
- [x] Implementovat toolbar/context akce pro tabulku.
- [x] GREEN: table testy projdou.
- [x] E2E: vytvořit tabulku, sloučit buňky, uložit a ověřit reload.

### 5.5.1 Headers/footers UI

- [x] RED: test zobrazení header oblasti v page-oriented režimu.
- [x] RED: test zobrazení footer oblasti v page-oriented režimu.
- [x] RED: test editace primary header.
- [x] RED: test editace primary footer.
- [x] RED: test přepnutí first page header/footer.
- [x] RED: test přepnutí even/odd header/footer.
- [x] Implementovat header/footer editing surface.
- [x] Implementovat section-aware header/footer selection.
- [x] GREEN: header/footer UI testy projdou.
- [x] E2E: upravit header/footer, uložit a ověřit reload.

### 5.5.2 Footnotes/endnotes UI

- [x] RED: test vložení footnote.
- [x] RED: test vložení endnote.
- [x] RED: test editace footnote body.
- [x] RED: test editace endnote body.
- [x] RED: test smazání note reference.
- [x] RED: test přepočítání viditelného pořadí footnotes.
- [x] Implementovat note reference renderer.
- [x] Implementovat note editor panel/area.
- [x] GREEN: footnote/endnote UI testy projdou.
- [x] E2E: vložit footnote, uložit a ověřit reload.

### 5.5.3 Tracked changes UI

- [x] RED: test zapnutí track changes režimu.
- [x] RED: test vložený text vytvoří insertion revision.
- [x] RED: test smazaný text vytvoří deletion revision.
- [x] RED: test formátování vytvoří formatting revision.
- [x] RED: test accept revision.
- [x] RED: test reject revision.
- [x] RED: test zobrazení revisions panelu.
- [x] Implementovat `TrackChangesEnabled`.
- [x] Implementovat revision rendering.
- [x] Implementovat accept/reject commands.
- [x] GREEN: tracked changes UI testy projdou.
- [x] E2E: zapnout track changes, upravit text, accept/reject a ověřit reload.

### 5.5.4 Floating/anchored object UI

- [x] RED: test změny image layout inline -> floating.
- [x] RED: test drag floating obrázku v rámci stránky.
- [x] RED: test změny wrap mode.
- [x] RED: test změny anchor paragraph.
- [x] RED: test resize floating obrázku zachová anchor.
- [x] Implementovat floating object handles.
- [x] Implementovat anchor marker.
- [x] Implementovat wrap mode toolbar.
- [x] GREEN: floating image UI testy projdou.
- [x] E2E: vložit floating obrázek, posunout ho, uložit a ověřit reload.

### 5.6 Obrázky

- [x] RED: test vložení image bloku přes URL.
- [x] RED: test vložení image bloku přes upload/provider.
- [x] RED: test vložení image bloku ze schránky.
- [x] RED: test změny alt textu.
- [x] RED: test změny caption.
- [x] RED: test změny alignment.
- [x] RED: test resize obrázku.
- [x] RED: test smazání obrázku.
- [x] RED: test read-only režim obrázek neupraví.
- [x] Implementovat `TmDocumentImageBlock`.
- [x] Implementovat `TmDocumentImageDialog`.
- [x] Implementovat toolbar akci Insert Image.
- [x] Implementovat image upload přes provider.
- [x] Implementovat URL insert bez provideru.
- [x] Implementovat image resize handle.
- [x] Implementovat image caption editor.
- [x] GREEN: image editing testy projdou.
- [x] E2E: vložit obrázek přes URL a ověřit reload.
- [x] E2E: nahrát obrázek přes demo API provider a ověřit reload.

### 5.7 Clipboard paste obrázků

- [x] RED: test JS paste event rozpozná image item.
- [x] RED: test paste image volá provider upload.
- [x] RED: test paste image vloží image block na aktuální pozici.
- [x] RED: test paste image bez provideru zobrazí lokalizovanou chybu.
- [x] RED: test paste image respektuje max file size.
- [x] RED: test paste image respektuje allowed content types.
- [x] Implementovat `document-editor.js` paste hook.
- [x] Implementovat `OnClipboardImagePasted` JSInvokable callback.
- [x] Implementovat dispatcher-safe callback.
- [x] Implementovat pending upload state.
- [x] GREEN: clipboard paste testy projdou.
- [x] E2E: vložit obrázek ze schránky a ověřit image block v dokumentu.

### 5.8 Insert UI ve stylu Wordu

- [x] RED: test tlačítko Insert otevře menu/panel pro vložení obsahu.
- [x] RED: test Insert Paragraph vloží odstavec na aktuální pozici.
- [x] RED: test Insert Heading vloží nadpis na aktuální pozici.
- [x] RED: test Insert Table otevře dialog/selector pro tabulku.
- [x] RED: test Insert Image otevře image flow.
- [x] RED: test `/` v textu zůstane normální znak a neotevře slash menu.
- [x] Implementovat Insert skupinu v toolbaru/ribbonu.
- [x] Implementovat Word-like dropdown/dialog pro vložení tabulky.
- [x] Implementovat Word-like insert image flow.
- [x] Lokalizovat položky Insert UI.
- [x] GREEN: insert UI testy projdou.

## Fáze 6: Word-like toolbar/ribbon a příkazy

### 6.1 Command model

- [x] RED: test command stack undo.
- [x] RED: test command stack redo.
- [x] RED: test update block command.
- [x] RED: test insert block command.
- [x] RED: test delete block command.
- [x] RED: test batch command.
- [x] Reuse pattern z `NotionCommandStack`.
- [x] Implementovat `DocumentEditorCommandStack`.
- [x] Implementovat `UpdateDocumentBlockCommand`.
- [x] Implementovat `InsertDocumentBlockCommand`.
- [x] Implementovat `DeleteDocumentBlockCommand`.
- [x] Implementovat `MoveDocumentBlockCommand`.
- [x] GREEN: command testy projdou.

### 6.2 Word-like toolbar/ribbon skeleton

- [x] RED: test renderu Word-like toolbaru/ribbonu.
- [x] RED: test toolbar/ribbon je skrytý při `ShowToolbar=false`.
- [x] RED: test disabled stav v read-only režimu.
- [x] Implementovat `TmDocumentEditorToolbar`.
- [x] Implementovat skupiny nástrojů po vzoru Wordu: Home, Insert, Layout, References, Review, View podle dostupného rozsahu.
- [x] V první verzi zobrazit minimálně Home, Insert a Review skupiny.
- [x] Použít `TmIcon`/lucide registry, kde existuje.
- [x] Přidat tooltipy k ikonovým tlačítkům.
- [x] Přidat image toolbar button.
- [x] GREEN: toolbar skeleton testy projdou.

### 6.3 Formátovací příkazy

- [x] RED: test bold command.
- [x] RED: test italic command.
- [x] RED: test underline command.
- [x] RED: test link command.
- [x] RED: test clear formatting command.
- [x] Implementovat inline mark toggling.
- [x] Implementovat link dialog.
- [x] GREEN: formatting testy projdou.

### 6.4 Klávesové zkratky

- [x] RED: test Ctrl+S vyvolá save.
- [x] RED: test Ctrl+Z vyvolá undo.
- [x] RED: test Ctrl+Y vyvolá redo.
- [x] RED: test Ctrl+B vyvolá bold.
- [x] RED: test Ctrl+I vyvolá italic.
- [x] RED: test Ctrl+K vyvolá link dialog.
- [x] RED: test Escape zavře otevřený panel.
- [x] Implementovat `DocumentEditorKeyboardManager`.
- [x] GREEN: keyboard testy projdou.

## Fáze 7: Ukládání a autosave

### 7.1 Dirty state

- [x] RED: test editor není dirty po načtení.
- [x] RED: test editor je dirty po změně.
- [x] RED: test save dirty stav vyčistí.
- [x] RED: test save failure dirty stav ponechá.
- [x] Implementovat dirty tracking.
- [x] Implementovat last saved timestamp.
- [x] GREEN: dirty state testy projdou.

### 7.2 Explicitní save

- [x] RED: test klik na Save zavolá provider.
- [x] RED: test Ctrl+S zavolá provider.
- [x] RED: test `OnSaveRequested`.
- [x] RED: test audit event pro save.
- [x] Implementovat save pipeline.
- [x] Implementovat disabled stav během save.
- [x] Implementovat error stav při selhání.
- [x] GREEN: save testy projdou.
- [x] E2E: Ctrl+S uloží dokument přes demo API a zobrazí saved stav.

### 7.3 Autosave

- [x] RED: test autosave se nespustí bez změn.
- [x] RED: test autosave se spustí po změně a intervalu.
- [x] RED: test autosave se vypne při `AutoSaveInterval=null`.
- [x] RED: test autosave nedělá major verzi.
- [x] Implementovat timer bezpečný pro dispose.
- [x] Implementovat autosave status text.
- [x] GREEN: autosave testy projdou.

## Fáze 8: Verze dokumentu

### 8.1 Vytvoření verze

- [x] RED: test vytvoření minor verze.
- [x] RED: test vytvoření major verze.
- [x] RED: test povinný komentář pro major verzi, pokud je zapnuto.
- [x] RED: test audit event pro verzi.
- [x] Implementovat `TmDocumentVersionDialog`.
- [x] Implementovat provider call.
- [x] GREEN: version create testy projdou.
- [x] E2E: vytvořit major verzi přes demo UI a ověřit ji v historii.

### 8.2 Version panel

- [x] RED: test renderu seznamu verzí.
- [x] RED: test empty state bez verzí.
- [x] RED: test výběru verze.
- [x] RED: test zavření panelu.
- [x] Implementovat `TmDocumentVersionPanel`.
- [x] Lokalizovat panel.
- [x] GREEN: version panel testy projdou.

### 8.3 Read-only version preview

- [x] RED: test preview vybrané verze.
- [x] RED: test návrat na aktuální verzi.
- [x] RED: test preview je read-only.
- [x] Implementovat preview mode.
- [x] GREEN: preview testy projdou.

### 8.4 Restore verze

- [x] RED: test restore vytvoří novou aktuální změnu.
- [x] RED: test restore nezničí historickou verzi.
- [x] RED: test audit event pro restore.
- [x] Implementovat confirm dialog.
- [x] Implementovat restore command.
- [x] GREEN: restore testy projdou.

## Fáze 9: Komentáře

### 9.1 Block comments

- [x] RED: test tlačítko komentáře u aktivního bloku.
- [x] RED: test vytvoření komentáře k bloku.
- [x] RED: test odpovědi v threadu.
- [x] RED: test resolve threadu.
- [x] RED: test reopening threadu.
- [x] Implementovat `TmDocumentCommentRail`.
- [x] Implementovat `TmDocumentCommentThread`.
- [x] Implementovat `TmDocumentCommentComposer`.
- [x] Reuse mention helper tam, kde dává smysl.
- [x] GREEN: block comment testy projdou.
- [x] E2E: přidat komentář přes demo UI, reloadnout dokument a ověřit komentář.

### 9.2 Text range comments

- [x] RED: test zachycení textové selection.
- [x] RED: test vytvoření anchoru z textové selection.
- [x] RED: test highlight komentovaného rozsahu.
- [x] RED: test klik na highlight otevře thread.
- [x] Implementovat JS selection helper.
- [x] Implementovat stabilní anchor pro inline content.
- [x] Implementovat fallback, když anchor nejde přesně obnovit.
- [x] GREEN: text comment testy projdou.

### 9.3 Externí/klientské komentáře

- [x] RED: test externí autor má jiný visual state.
- [x] RED: test read-only uživatel může komentovat, pokud má permission.
- [x] RED: test read-only uživatel nemůže editovat dokument.
- [x] Implementovat `CanComment`.
- [x] Implementovat `CanResolveComments`.
- [x] Implementovat `CanDeleteOwnComments`.
- [x] GREEN: permission comment testy projdou.

## Fáze 10: Šablonové proměnné

### 10.1 Token model a reuse existujícího provideru

- [x] RED: test token runu v dokumentu.
- [x] RED: test token renderu jako chip.
- [x] RED: test token serializace.
- [x] Reuse existující `ITokenDataProvider` pro autocomplete.
- [x] RED: test token preview hodnoty přes nový value provider.
- [x] RED: test token bez hodnoty zobrazí missing state.
- [x] RED: test token type metadata.
- [x] Implementovat document token helper.
- [x] Navrhnout `IDocumentTokenValueProvider`.
- [x] Navrhnout `DocumentTokenValue`.
- [x] Navrhnout `DocumentTokenResolutionContext`.
- [x] Navrhnout `DocumentTokenValidationResult`.
- [x] GREEN: token model testy projdou.

### 10.2 Token insertion

- [x] RED: test `{{` otevře token menu.
- [x] RED: test výběr tokenu vloží token run.
- [x] RED: test token je non-editable chip.
- [x] RED: test token lze smazat jako jednotku.
- [x] Implementovat `TmDocumentTokenMenu`.
- [x] GREEN: token insertion testy projdou.

### 10.3 Template preview

- [x] RED: test preview nahradí token hodnotou.
- [x] RED: test chybějící hodnota zobrazí placeholder.
- [x] RED: test návrat z preview nemění dokument.
- [x] Implementovat `DocumentTemplatePreviewService`.
- [x] GREEN: preview testy projdou.

## Fáze 11: Import/export

### 11.0 Document format balík

- [x] Vytvořit samostatný projekt `Tempo.Blazor.DocumentFormats`.
- [x] Vytvořit samostatný test projekt `Tempo.Blazor.DocumentFormats.Tests`.
- [x] Přidat package metadata pro samostatný NuGet.
- [x] Přidat referenci na `Tempo.Blazor.Abstractions`.
- [x] Nepřidávat referenci z `Tempo.Blazor` na document format balík.
- [x] Přidat README pro server-side použití.
- [x] Přidat referenci z `Tempo.Blazor.Demo.Api` na document format projekt.
- [x] Přidat demo API endpoint `POST /api/document-editor/import/docx`.
- [x] Přidat demo API endpoint `GET /api/document-editor/{id}/export/docx`.
- [x] Přidat demo API endpoint `POST /api/document-editor/import/odt`.
- [x] Přidat demo API endpoint `GET /api/document-editor/{id}/export/odt`.
- [x] Přidat demo API endpoint `GET /api/document-editor/{id}` pro JSON source of truth.
- [x] Přidat demo API endpoint `PUT /api/document-editor/{id}` pro JSON save.
- [x] Přidat demo API endpointy pro komentáře.
- [x] Přidat demo API endpointy pro verze.
- [x] Přidat demo API endpoint `POST /api/document-editor/images`.
- [x] Přidat demo API endpoint `GET /api/document-editor/images/{id}` nebo ticket URL endpoint.
- [x] Přidat mock/persistent store pro demo dokumenty.
- [x] Přidat mock/persistent store pro demo obrázky.

### 11.1 DOCX import ve volitelném balíku

- [x] RED: test importu minimálního DOCX souboru.
- [x] RED: test importu odstavce.
- [x] RED: test importu nadpisu.
- [x] RED: test importu bold/italic/underline textu.
- [x] RED: test importu hyperlinku.
- [x] RED: test importu bullet listu.
- [x] RED: test importu numbered listu.
- [x] RED: test importu jednoduché tabulky.
- [x] RED: test importu tabulky s horizontálně sloučenými buňkami.
- [x] RED: test importu tabulky s vertikálně sloučenými buňkami.
- [x] RED: test importu inline/anchored obrázku jako image block.
- [x] RED: test importu floating obrázku s anchor/wrap mode.
- [x] RED: test importu page breaku.
- [x] RED: test importu headers.
- [x] RED: test importu footers.
- [x] RED: test importu section properties.
- [x] RED: test importu footnotes.
- [x] RED: test importu endnotes.
- [x] RED: test importu comments.
- [x] RED: test importu tracked insertions.
- [x] RED: test importu tracked deletions.
- [x] RED: test importu tracked formatting changes.
- [x] RED: test zachování nepodporovaných částí jako package preservation part.
- [x] Vybrat knihovnu/strategii pro OpenXML práci.
- [x] Implementovat `DocumentDocxImporter`.
- [x] Implementovat `DocxPackageReader`.
- [x] Implementovat mapování styles na editor marks/block attributes.
- [x] Implementovat mapování DOCX image part na image asset callback.
- [x] Implementovat mapování DOCX merged cells.
- [x] Implementovat mapování DOCX headers/footers.
- [x] Implementovat mapování DOCX footnotes/endnotes.
- [x] Implementovat mapování DOCX comments.
- [x] Implementovat mapování DOCX section properties.
- [x] Implementovat mapování DOCX revisions.
- [x] Implementovat mapování DOCX drawing anchors.
- [x] GREEN: DOCX import testy projdou.
- [x] API test: demo API importuje DOCX a vrátí/uloží JSON dokument.
- [x] E2E: nahrát DOCX v demo UI a zobrazit importovaný dokument.

### 11.2 DOCX export ve volitelném balíku

- [x] RED: test exportu minimálního DOCX souboru.
- [x] RED: test exportu odstavce.
- [x] RED: test exportu nadpisu.
- [x] RED: test exportu inline stylů.
- [x] RED: test exportu hyperlinku.
- [x] RED: test exportu listu.
- [x] RED: test exportu tabulky.
- [x] RED: test exportu tabulky s merged cells.
- [x] RED: test exportu image URL/provider assetu jako DOCX image part.
- [x] RED: test exportu floating/anchored image layoutu.
- [x] RED: test exportu komentáře, pokud bude v compatibility matrix.
- [x] RED: test exportu headers/footers.
- [x] RED: test exportu footnotes/endnotes.
- [x] RED: test exportu section properties.
- [x] RED: test exportu tracked changes jako Word revisions.
- [x] RED: test otevřitelnosti výsledného DOCX package strukturou OpenXML.
- [x] Implementovat `DocumentDocxExporter`.
- [x] Implementovat `DocxPackageWriter`.
- [x] GREEN: DOCX export testy projdou.
- [x] API test: demo API exportuje JSON dokument jako DOCX.
- [x] E2E: kliknout Export DOCX v demo UI a ověřit stažení/response.

### 11.3 ODT import ve volitelném balíku

- [x] RED: test importu minimálního ODT souboru.
- [x] RED: test importu odstavce.
- [x] RED: test importu nadpisu.
- [x] RED: test importu bold/italic/underline textu.
- [x] RED: test importu hyperlinku.
- [x] RED: test importu listu.
- [x] RED: test importu jednoduché tabulky.
- [x] RED: test importu tabulky s merged cells.
- [x] RED: test importu obrázku jako image block.
- [x] RED: test importu anchored/floating obrázku.
- [x] RED: test importu headers.
- [x] RED: test importu footers.
- [x] RED: test importu page/section styles.
- [x] RED: test importu footnotes.
- [x] RED: test importu endnotes.
- [x] RED: test importu annotations/comments.
- [x] RED: test importu tracked changes, pokud ODT reprezentace dovolí mapování.
- [x] RED: test zachování nepodporovaných částí jako package preservation part.
- [x] Vybrat knihovnu/strategii pro OpenDocument XML práci.
- [x] Implementovat `DocumentOdtImporter`.
- [x] Implementovat `OdtPackageReader`.
- [x] Implementovat mapování ODT image part na image asset callback.
- [x] Implementovat mapování ODT merged cells.
- [x] Implementovat mapování ODT headers/footers.
- [x] Implementovat mapování ODT footnotes/endnotes.
- [x] Implementovat mapování ODT annotations/comments.
- [x] Implementovat mapování ODT page/section styles.
- [x] Implementovat mapování ODT change tracking.
- [x] Implementovat mapování ODT frame anchors.
- [x] GREEN: ODT import testy projdou.
- [x] API test: demo API importuje ODT a vrátí/uloží JSON dokument.
- [x] E2E: nahrát ODT v demo UI a zobrazit importovaný dokument.

### 11.4 ODT export ve volitelném balíku

- [x] RED: test exportu minimálního ODT souboru.
- [x] RED: test exportu odstavce.
- [x] RED: test exportu nadpisu.
- [x] RED: test exportu inline stylů.
- [x] RED: test exportu hyperlinku.
- [x] RED: test exportu listu.
- [x] RED: test exportu tabulky.
- [x] RED: test exportu tabulky s merged cells.
- [x] RED: test exportu image URL/provider assetu jako ODT image part.
- [x] RED: test exportu anchored/floating image layoutu.
- [x] RED: test exportu headers/footers.
- [x] RED: test exportu footnotes/endnotes.
- [x] RED: test exportu page/section properties.
- [x] RED: test exportu comments/annotations.
- [x] RED: test exportu tracked changes, pokud bude kompatibilní s ODT targetem.
- [x] RED: test otevřitelnosti výsledného ODT package strukturou ZIP/XML.
- [x] Implementovat `DocumentOdtExporter`.
- [x] Implementovat `OdtPackageWriter`.
- [x] GREEN: ODT export testy projdou.
- [x] API test: demo API exportuje JSON dokument jako ODT.
- [x] E2E: kliknout Export ODT v demo UI a ověřit stažení/response.

### 11.5 Round-trip testy ve volitelném balíku

- [x] RED: DOCX -> model -> DOCX zachová text.
- [x] RED: DOCX -> model -> DOCX zachová základní styly.
- [x] RED: DOCX -> model -> DOCX zachová základní obrázky.
- [x] RED: DOCX -> model -> DOCX zachová merged cells.
- [x] RED: DOCX -> model -> DOCX zachová headers/footers.
- [x] RED: DOCX -> model -> DOCX zachová footnotes/endnotes.
- [x] RED: DOCX -> model -> DOCX zachová comments.
- [x] RED: DOCX -> model -> DOCX zachová section properties.
- [x] RED: DOCX -> model -> DOCX zachová tracked revisions.
- [x] RED: DOCX -> model -> DOCX zachová floating/anchored layout v definované toleranci.
- [x] RED: DOCX -> model -> ODT zachová text.
- [x] RED: ODT -> model -> ODT zachová text.
- [x] RED: ODT -> model -> ODT zachová základní obrázky.
- [x] RED: ODT -> model -> ODT zachová merged cells.
- [x] RED: ODT -> model -> ODT zachová headers/footers.
- [x] RED: ODT -> model -> ODT zachová footnotes/endnotes.
- [x] RED: ODT -> model -> ODT zachová comments/annotations.
- [x] RED: ODT -> model -> DOCX zachová text.
- [x] RED: nepodporované části se buď zachovají, nebo explicitně nahlásí jako dropped.
- [x] Implementovat `DocumentPackageRoundTripReport`.
- [x] Implementovat compatibility warnings.
- [x] GREEN: round-trip testy projdou.

### 11.6 HTML export

- [x] RED: test paragraph HTML exportu.
- [x] RED: test heading HTML exportu.
- [x] RED: test list HTML exportu.
- [x] RED: test table HTML exportu.
- [x] RED: test token HTML exportu.
- [x] Implementovat `DocumentHtmlExporter`.
- [x] Implementovat bezpečné escapování.
- [x] GREEN: HTML export testy projdou.

### 11.7 Markdown export

- [x] RED: test paragraph Markdown exportu.
- [x] RED: test heading Markdown exportu.
- [x] RED: test list Markdown exportu.
- [x] RED: test quote Markdown exportu.
- [x] Implementovat `DocumentMarkdownExporter`.
- [x] GREEN: Markdown export testy projdou.

### 11.8 HTML import

- [x] RED: test importu jednoduchého odstavce.
- [x] RED: test importu nadpisu.
- [x] RED: test importu listu.
- [x] RED: test ignorování nebezpečných elementů.
- [x] Implementovat `DocumentHtmlImporter`.
- [x] GREEN: HTML import testy projdou.

### 11.9 PDF boundary

- [x] Navrhnout interface `IDocumentPdfExportProvider`.
- [x] RED: test, že export PDF akce je skrytá bez provideru.
- [x] RED: test, že export PDF zavolá provider.
- [x] Implementovat provider boundary.
- [x] GREEN: PDF boundary testy projdou.

## Fáze 12: Diff a revize

### 12.1 Text diff helper

- [x] RED: test přidaného textu.
- [x] RED: test smazaného textu.
- [x] RED: test změněného textu.
- [x] RED: test stabilního výstupu pro češtinu.
- [x] Implementovat jednoduchý word-level diff.
- [x] GREEN: diff helper testy projdou.

### 12.2 Version diff viewer

- [x] RED: test výběru dvou verzí.
- [x] RED: test inline diff renderu.
- [x] RED: test side-by-side shellu.
- [x] Implementovat `TmDocumentDiffViewer`.
- [x] Implementovat diff panel ve version panelu.
- [x] GREEN: diff viewer testy projdou.

### 12.3 Track changes boundary

- [x] Zapsat, že plné Word-like track changes nejsou v první vlně.
- [x] Navrhnout model `DocumentSuggestion`.
- [x] Navrhnout provider boundary pro suggestions.
- [x] Připravit TODO pro pozdější implementaci bez zásahu do core modelu.

## Fáze 12.5: Vlastní OT/CRDT engine

### 12.5.1 Rozhodnutí OT vs CRDT

- [x] Sepsat rozdíl OT vs CRDT pro dokumentový model Tempo.
- [x] Vyhodnotit blokové operace.
- [x] Vyhodnotit textové operace.
- [x] Vyhodnotit inline marks.
- [x] Vyhodnotit komentářové anchory.
- [x] Vyhodnotit tabulky.
- [x] Vyhodnotit undo/redo.
- [x] Vyhodnotit offline edits.
- [x] Rozhodnout první algoritmus pro prototyp.

### 12.5.2 Operation log

- [x] RED: test append-only operation logu.
- [x] RED: test replay operací na prázdný dokument.
- [x] RED: test replay operací na existující dokument.
- [x] RED: test idempotentního replay podle operation id.
- [x] RED: test odmítnutí operace s neznámým schema version.
- [x] Implementovat `DocumentOperationLog`.
- [x] Implementovat `DocumentOperationApplier`.
- [x] Implementovat `DocumentOperationValidationResult`.
- [x] GREEN: operation log testy projdou.

### 12.5.3 Konflikty bez sítě

- [x] RED: test souběžný insert do stejné pozice.
- [x] RED: test souběžný delete stejného rozsahu.
- [x] RED: test insert proti delete.
- [x] RED: test mark proti delete.
- [x] RED: test souběžné přejmenování bloku.
- [x] RED: test souběžný přesun bloku.
- [x] Implementovat conflict resolver pro text.
- [x] Implementovat conflict resolver pro bloky.
- [x] Implementovat deterministic ordering.
- [x] GREEN: conflict resolver testy projdou.

### 12.5.4 Síťová synchronizace

- [x] Navrhnout `IDocumentCollaborationProvider`.
- [x] RED: test join document session.
- [x] RED: test leave document session.
- [x] RED: test broadcast operation batch.
- [x] RED: test receive remote operation batch.
- [x] RED: test broadcast cursor.
- [x] RED: test receive cursor.
- [x] Implementovat provider boundary.
- [x] Implementovat in-memory test provider.
- [x] Implementovat SignalR demo provider.
- [x] GREEN: collaboration provider testy projdou.

### 12.5.5 Integration do editoru

- [x] RED: test lokální editace vytvoří operation batch.
- [x] RED: test remote operation se aplikuje bez ztráty lokálního dirty stavu.
- [x] RED: test remote cursor se zobrazí.
- [x] RED: test reconnect provede catch-up z operation logu.
- [x] Implementovat `DocumentCollaborationSync`.
- [x] Implementovat collaboration cursor overlay.
- [x] Implementovat optimistic local apply.
- [x] Implementovat reconciliation.
- [x] GREEN: editor collaboration testy projdou.

## Fáze 12.6: Offline podpora

### 12.6.1 Offline režim v core editoru

- [x] RED: test editor bez offline store funguje normálně online-only.
- [x] RED: test editor s offline store uloží draft při výpadku save.
- [x] RED: test editor zobrazí offline status.
- [x] RED: test editor načte lokální draft před serverovou verzí, pokud je novější.
- [x] RED: test offline draft obsahuje vložený clipboard obrázek jako pending asset.
- [x] RED: test uživatel může lokální draft zahodit.
- [x] RED: test uživatel může lokální draft zkusit synchronizovat.
- [x] Implementovat `OfflineMode`.
- [x] Implementovat `PreferLocalDraft`.
- [x] Implementovat offline status UI.
- [x] Implementovat draft recovery prompt.
- [x] Implementovat offline image asset handling.
- [x] GREEN: offline core testy projdou.

### 12.6.2 IndexedDB adapter jako volitelná implementace

- [x] Rozhodnout, zda IndexedDB adapter patří do core balíku, nebo samostatného browser storage balíku.
- [x] RED: test graceful fallback bez JS.
- [x] RED: test uložení draftu přes JS interop.
- [x] RED: test načtení draftu přes JS interop.
- [x] RED: test smazání draftu přes JS interop.
- [x] Implementovat `IndexedDbDocumentOfflineStore`, pokud bude součástí core.
- [x] Rozhodnuto: pozdější `Tempo.Blazor.OfflineStorage` není potřeba pro první adapter.
- [x] GREEN: IndexedDB adapter testy projdou.

### 12.6.3 Konflikty při návratu online

- [x] RED: test sync proti stejné base verzi projde bez konfliktu.
- [x] RED: test sync proti novější serverové verzi vrátí konflikt.
- [x] RED: test merge přes operation log.
- [x] RED: test fallback na side-by-side conflict review.
- [x] Implementovat conflict banner.
- [x] Implementovat conflict review shell.
- [x] Implementovat accept local.
- [x] Implementovat accept server.
- [x] Implementovat create copy.
- [x] GREEN: offline conflict testy projdou.

## Fáze 13: Oprávnění a audit

### 13.1 Permission model

- [x] RED: test `CanRead`.
- [x] RED: test `CanEdit`.
- [x] RED: test `CanComment`.
- [x] RED: test `CanCreateVersion`.
- [x] RED: test `CanExport`.
- [x] RED: test `CanViewAudit`.
- [x] Implementovat `DocumentEditorPermissions`.
- [x] Propagovat permissions do toolbaru/ribbonu.
- [x] Propagovat permissions do comment railu.
- [x] GREEN: permission testy projdou.

### 13.2 Audit sink

- [x] RED: test audit event při načtení dokumentu.
- [x] RED: test audit event při uložení.
- [x] RED: test audit event při exportu.
- [x] RED: test audit event při komentáři.
- [x] RED: test audit event při vytvoření verze.
- [x] Implementovat audit dispatch.
- [x] Zajistit, že audit failure nerozbije UI, pokud hostitel zvolí non-blocking mode.
- [x] GREEN: audit sink testy projdou.

## Fáze 13.5: Signing-ready integration contract

### 13.5.1 Freeze/finalize workflow

- [x] RED: test finalize vyžaduje uloženou document version.
- [x] RED: test finalize vytvoří immutable rendition.
- [x] RED: test finalized rendition nejde změnit editací zdrojového dokumentu.
- [x] RED: test nová editace dokumentu vyžaduje novou rendition.
- [x] RED: test rendition obsahuje hash zdrojového JSON snapshotu.
- [x] Implementovat `FinalizeForRenditionCommand`.
- [x] Implementovat UI akci "Create rendition" nebo provider-only hook podle rozhodnutí.
- [x] GREEN: finalize workflow testy projdou.

### 13.5.2 Anchor map pro signing pole

- [x] RED: test token anchor se namapuje na page/x/y/w/h.
- [x] RED: test explicitní signing placeholder se namapuje na page/x/y/w/h.
- [x] RED: test anchor v header/footer je označen scope informací.
- [x] RED: test anchor v tabulce s merged cells má správný bounding box.
- [x] RED: test anchor u floating objektu respektuje final layout.
- [x] Navrhnout `DocumentAnchorMapBuilder`.
- [x] Navrhnout `DocumentSigningPlaceholder`.
- [x] Navrhnout mapping z document anchoru na normalizované souřadnice kompatibilní se `SigningFieldArea`.
- [x] GREEN: anchor map testy projdou.

### 13.5.3 Demo signing bridge

- [x] Demo API: endpoint vytvoří rendition pro dokument.
- [x] Demo API: endpoint vrátí rendition pages.
- [x] Demo API: endpoint vrátí anchor map.
- [x] Demo SharedUI: scénář "Create signing template from document".
- [x] E2E: vytvořit dokument, finalize rendition, otevřít signing designer nad rendition.
- [x] E2E: automaticky vložit signing field podle anchoru.

### 13.5.4 Boundary pravidla

- [x] `TmDocumentEditor` nesmí referencovat signing komponenty.
- [x] Signing komponenty nesmí vyžadovat mutable editor state.
- [x] Pro signing se používá pouze `DocumentRenditionId` / `DocumentVersionId`.
- [x] Souřadnice signing polí se ukládají vůči konkrétní rendition, ne vůči živému dokumentu.
- [x] Změna zdrojového dokumentu po finalizaci nevytváří tichou změnu podepisovaného obsahu.

## Fáze 14: Lokalizace

### 14.1 Klíče

- [x] Sepsat všechny nové klíče s prefixem `TmDocumentEditor_`.
- [x] Přidat EN klíče do `TmResources.resx`.
- [x] Přidat CS klíče do `TmResources.cs.resx`.
- [x] Zvážit FR klíče, protože projekt má `TmResources.fr.resx`.
- [x] Přidat klíče do `MockTmLocalizer`.

### 14.2 Testy lokalizace

- [x] RED: test existence klíčů v mocku.
- [x] RED: test komponenta nerenderuje fallback hardcoded text pro toolbar/ribbon.
- [x] RED: test empty/error states používají lokalizaci.
- [x] GREEN: localization testy projdou.

## Fáze 15: Styling a design systém

### 15.1 CSS struktura

- [x] Vytvořit `_document-editor.css`.
- [x] Vytvořit `_document-editor-toolbar.css`, pokud bude soubor příliš velký.
- [x] Vytvořit `_document-editor-comments.css`, pokud bude soubor příliš velký.
- [x] Přidat import do `tempo-blazor.css`.
- [x] Ověřit, zda bundled CSS je generovaný soubor.

### 15.2 Vizuální stavy

- [x] Nastylovat loading.
- [x] Nastylovat empty.
- [x] Nastylovat error.
- [x] Nastylovat read-only.
- [x] Nastylovat dirty.
- [x] Nastylovat saving.
- [x] Nastylovat focused block.
- [x] Nastylovat comment highlight.
- [x] Nastylovat selected comment.
- [x] Nastylovat version preview.

### 15.3 Responsivita

- [x] Desktop: Word-like toolbar/ribbon nahoře, dokument uprostřed, komentáře/review panel vpravo.
- [x] Tablet: komentáře jako collapsible panel.
- [x] Mobil: toolbar/ribbon zhuštěný do skupin nebo overflow menu, komentáře v draweru.
- [x] Ověřit žádné překrývání textů.
- [x] Ověřit toolbar/ribbon buttony mají stabilní rozměr.

### 15.4 Dark mode

- [x] Ověřit surface v dark mode.
- [x] Ověřit komentáře v dark mode.
- [x] Ověřit toolbar/ribbon v dark mode.
- [x] Ověřit diff barvy v dark mode.
- [x] Ověřit focus ring v dark mode.

## Fáze 16: Demo

### 16.1 Demo provider

- [x] Vytvořit `DemoDocumentEditorProvider`.
- [x] Přidat ukázkový dokument "Smlouva".
- [x] Přidat ukázkový dokument "Podání".
- [x] Přidat ukázkové komentáře.
- [x] Přidat ukázkové verze.
- [x] Přidat ukázkové tokeny.
- [x] Přidat ukázkové image URL.
- [x] Přidat demo image provider.
- [x] Přidat ukázkový dokument s obrázkem.

### 16.2 Demo stránka

- [x] Vytvořit stránku v `Tempo.Blazor.Demo.SharedUI/Pages`.
- [x] Přidat route pro document editor.
- [x] Přidat navigaci v demo menu.
- [x] Ukázat edit režim.
- [x] Ukázat read-only/client review režim.
- [x] Ukázat version panel.
- [x] Ukázat comments rail.
- [x] Ukázat export actions.

### 16.3 Demo scénáře

- [x] Scénář: právník upraví odstavec a uloží.
- [x] Scénář: právník vytvoří major verzi.
- [x] Scénář: klient přidá komentář bez možnosti editace.
- [x] Scénář: právník vyřeší komentář.
- [x] Scénář: právník zobrazí rozdíl verzí.
- [x] Scénář: právník vloží token.
- [x] Scénář: právník vloží obrázek přes URL.
- [x] Scénář: právník nahraje obrázek přes provider.
- [x] Scénář: právník vloží obrázek ze schránky.

## Fáze 17: Unit a component test coverage

### 17.1 Modely

- [x] Spustit model testy document editoru.
- [x] Doplnit edge cases pro serializaci.
- [x] Doplnit edge cases pro starší `SchemaVersion`.
- [x] Doplnit edge cases pro neznámý block type.

### 17.2 Komponenty

- [x] Spustit komponentové testy shellu.
- [x] Spustit komponentové testy toolbaru/ribbonu.
- [x] Spustit komponentové testy rendereru.
- [x] Spustit komponentové testy komentářů.
- [x] Spustit komponentové testy verzí.

### 17.3 JS interop

- [x] Testovat komponenty s `JSRuntimeMode.Loose`.
- [x] Přidat test graceful fallbacku bez JS.
- [x] Přidat test dispose bez výjimky.

## Fáze 18: Playwright E2E

Tato fáze není první místo, kde se píšou E2E testy. Slouží jako souhrnná hardening fáze po průběžných E2E scénářích z jednotlivých řezů.

### 18.1 Základní editing

- [x] E2E: otevřít demo stránku editoru.
- [x] E2E: kliknout do odstavce.
- [x] E2E: napsat text.
- [x] E2E: uložit Ctrl+S.
- [x] E2E: ověřit save status.

### 18.2 Komentáře

- [x] E2E: vybrat blok.
- [x] E2E: přidat komentář.
- [x] E2E: odpovědět na komentář.
- [x] E2E: resolve komentář.

### 18.3 Verze

- [x] E2E: vytvořit major verzi.
- [x] E2E: otevřít historii.
- [x] E2E: zobrazit verzi.
- [x] E2E: zobrazit diff.

### 18.4 Visual sanity

- [x] E2E screenshot desktop.
- [x] E2E screenshot tablet.
- [x] E2E screenshot mobile.
- [x] Ověřit, že toolbar/ribbon nepřekrývá dokument.
- [x] Ověřit, že comment rail nepřekrývá text.
- [x] Ověřit, že resize handle obrázku nepřekrývá caption.
- [x] Ověřit, že velký obrázek nerozbije page layout.

## Fáze 19: Dokumentace

### 19.1 AGENT/dev dokumentace

- [ ] Doplnit požadovaný script/CSS setup, pokud vznikne nový JS soubor.
- [ ] Popsat základní použití `TmDocumentEditor`.
- [ ] Popsat provider kontrakt.
- [ ] Popsat audit eventy.
- [ ] Popsat non-goals.

### 19.2 README/API docs

- [ ] Přidat krátký příklad basic editoru.
- [ ] Přidat příklad read-only client review režimu.
- [ ] Přidat příklad provideru.
- [ ] Přidat příklad exportu.
- [ ] Přidat poznámku k DOCX/PDF provider boundary.

## Fáze 20: Integrační příprava pro Advocatus

### 20.1 Hostitelské napojení

- [ ] Sepsat, jak Advocatus mapuje MatterDocument na `DocumentEditorDocument`.
- [ ] Sepsat, jak Advocatus mapuje uživatele/klienta na `DocumentEditorAuthor`.
- [ ] Sepsat, jak Advocatus ukládá verze.
- [ ] Sepsat, jak Advocatus ukládá audit eventy.
- [ ] Sepsat, jak Advocatus řeší document permissions.

### 20.2 Bezpečnostní hranice

- [ ] Editor nikdy sám nerozhoduje o právu číst dokument.
- [ ] Editor nikdy sám neukládá tajná data mimo provider.
- [ ] Editor neposílá obsah do externích služeb bez explicitního provideru.
- [ ] Editor nepoužívá AI endpoint bez explicitního provideru.
- [ ] Editor neposkytuje "forensický audit"; pouze eventy pro hostitelskou aplikaci.

### 20.3 Budoucí rozšíření

- [ ] Real-time collaboration přes provider boundary.
- [ ] Suggestions/track changes.
- [ ] DOCX import/export přes serverový provider.
- [ ] PDF export přes serverový provider.
- [ ] Právní číslování článků a odstavců.
- [ ] Citace paragrafů a judikatury.
- [ ] Porovnání dokumentů mimo verze.
- [ ] Šablonové podmínky a opakující se bloky.

## První doporučený implementační řez

Tento řez má dodat něco malé, ale skutečně použitelné:

- [x] Fáze 0 hotová.
- [x] Fáze 1.1 až 1.3 hotová.
- [x] Fáze 1.7 hotová aspoň pro základní textové operace.
- [x] Fáze 2.1 až 2.2 hotová.
- [x] Fáze 2.1.1 hotová.
- [x] Fáze 3 hotová.
- [x] Fáze 4.1 hotová.
- [x] Fáze 5.1 až 5.3 hotová.
- [x] Fáze 6.1 až 6.2 hotová.
- [x] Fáze 7.1 až 7.2 hotová.
- [x] Fáze 11.1 až 11.2 hotová pro minimální DOCX.
- [x] Fáze 11.3 až 11.4 hotová pro minimální ODT.
- [x] Jednoduchá demo stránka hotová.
- [x] Unit testy pro tento řez zelené.

Výsledek: editor umí načíst minimální DOCX/ODT, převést ho do interního modelu, zobrazit odstavce/nadpisy, upravit text, uložit snapshot, exportovat minimální DOCX/ODT a běžet v demu. Teprve potom má smysl přidávat komentáře, verze, diff a síťovou spolupráci.

## Průběžný stav

- [x] Vytvořen tento implementační TODO soubor.
- [x] Zahájena Fáze 0.
- [x] Dokončena Fáze 0 návrhovým artefaktem `planning/document-editor-phase-0-design.md`.
- [x] Zahájena implementace modelů.
- [x] Dokončena Fáze 1 modely v `Tempo.Blazor.Abstractions/DocumentEditor/Models`.
- [x] Dokončena Fáze 2 provider kontrakty, in-memory demo providery a Notion adaptér.
- [x] První demo editoru běží.
- [x] První unit testy jsou zelené.
- [x] První E2E test je zelený.
- [x] Dokončena Fáze 3 Word-like shell editora.
- [x] Dokončena Fáze 4 read-only renderer, document surface, typografie a demo API/E2E ověření.
- [x] Dokončena Fáze 5 jako první funkční řez editace bloků, tabulek, obrázků, headers/footers, notes, revisions, Insert UI a save/reload přes demo API.
- [x] Dokončena Fáze 6 command stackem, Word-like toolbar komponentou, základními formatting příkazy, keyboard shortcuty a průběžným E2E ověřením.
- [x] Dokončena Fáze 7 dirty trackingem, save pipeline, last saved stavem, audit callbackem, autosave timerem a demo API/E2E ověřením Ctrl+S save.
- [x] Dokončena Fáze 8 verzovacím dialogem, version panelem, read-only preview, restore tokem, demo API endpointy a E2E ověřením vytvoření major verze.
- [x] Dokončena Fáze 9 komentářovým railem, thread/composer UI, provider operacemi pro reply/resolve/reopen, text range anchor helperem, permission parametry, mention autocomplete reuse a demo API/E2E ověřením reloadu komentáře.
- [x] Dokončena Fáze 10 šablonovými tokeny, reuse `ITokenDataProvider`, `IDocumentTokenValueProvider`, preview službou, Word-like token chip UI a demo/E2E ověřením preview hodnot.
- [x] Dokončena Fáze 11 volitelným balíkem `Tempo.Blazor.DocumentFormats`, DOCX/ODT import/export pipeline, HTML/Markdown exportem, HTML importem, PDF provider boundary, demo API endpointy, demo UI import/export a unit/API/E2E ověřením.
- [x] Dokončena Fáze 12 word-level diff helperem, `TmDocumentDiffViewer`, výběrem dvou verzí ve version panelu a suggestions boundary mimo core dokumentový JSON.
- [x] Dokončena Fáze 12.5 vlastním operation-log prototypem s CRDT-style deterministickým řazením, conflict resolverem, collaboration provider boundary, in-memory/SignalR adapterem, sync koordinátorem a cursor overlayem.
- [x] Dokončena Fáze 12.6 offline režimem v editoru, draft recovery/sync bannerem, pending clipboard image assets, IndexedDB adapterem a základním conflict review shellem.
