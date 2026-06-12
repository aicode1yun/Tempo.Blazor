# Document editor WYSIWYG DOCX/ODT compatibility matrix

Stavy:

- `editable` - prvek je mapovaný do interního modelu a lze ho upravovat ve WYSIWYG editoru.
- `read-only render` - prvek lze zobrazit, ale editor ho neupravuje jako plnohodnotnou strukturu.
- `roundtrip` - prvek se exportuje a znovu importuje v podporované podmnožině.
- `degrade` - prvek se normalizuje na jednodušší model nebo se zachová jen kompatibilitní metadata.

## DOCX

| Prvek | Stav | Poznámka |
| --- | --- | --- |
| Odstavce a prázdné paragraph marks | `editable`, `roundtrip` | Prázdný odstavec zůstává samostatný `ParagraphBlockContent`. |
| Nadpisy 1-6 | `editable`, `roundtrip` | Word styly `HeadingN` se mapují na `HeadingBlockContent.Level`. |
| Inline marky | `editable`, `roundtrip` | Bold, italic, underline, strike, super/subscript, text color, highlight. |
| Hyperlinky | `editable`, `roundtrip` | Absolutní URL jako DOCX hyperlink relationship. |
| Seznamy | `editable`, `degrade` | Zachovává se ordered/bullet a základní úroveň, ne kompletní numbering styl. |
| Page break | `editable`, `roundtrip` | Uložený jako `w:br type="page"`. |
| Tabulky | `editable`, `roundtrip` | Podporované jsou `gridSpan` a vertikální merge `restart/continue`. |
| Obrázky inline | `editable`, `roundtrip` | Asset/provider nebo data URL se zapisuje jako image part. |
| Plovoucí obrázky | `editable`, `roundtrip` | Anchor, Square wrap, left/right horizontal alignment, distance from text, size, z-index a lock anchor s tolerancí layoutu. První runtime iterace používá CSS float pro left/right obtékání a responsive fallback na úzkých viewports. |
| Header/footer | `editable`, `roundtrip` | Primary, first page a even pages. Odd pages se normalizují na primary DOCX reference. |
| Footnote/endnote | `read-only render`, `roundtrip` | Reference a bodies jsou zachované, pokročilé numbering styly se normalizují. |
| Komentáře | `editable`, `roundtrip` | Text komentáře a podporovaná inline kotva přes `commentRangeStart/End`. |
| Track changes | `read-only render`, `roundtrip` | Podporovaná podmnožina: inserted/deleted runs a základní formatting revisions. |
| Restricted editing / content controls | `editable`, `roundtrip` | Podporovaná podmnožina: `w:documentProtection` s enforced read-only ochranou a same-block editovatelné oblasti jako `w:sdt` s tagem `tm-editable:{id}:{start}:{end}`. Import mapuje tyto content controls zpět na `DocumentRestrictedMarker`; složitější/nested SDT se v této fázi negarantují. |
| Sekce/page settings | `read-only render`, `roundtrip` | Page size, margins, orientation. |
| Neznámé OpenXML části | `degrade` | Importer je uchovává v `PreservedParts`, pokud nejde o hlavní document/media část. |
| Makra, OLE, SmartArt, charts, equations | `degrade` | Nejsou editovatelné; očekává se warning/preserved part podle typu balíčku. |

## ODT

| Prvek | Stav | Poznámka |
| --- | --- | --- |
| Odstavce a prázdné paragraph marks | `editable`, `roundtrip` | Prázdný `text:p` zůstává samostatný odstavec. |
| Nadpisy | `editable`, `roundtrip` | `text:h` + `text:outline-level`. |
| Inline marky | `editable`, `roundtrip` | Tempo styly pro bold, italic, underline a strike. |
| Hyperlinky | `editable`, `roundtrip` | `text:a` s `xlink:href`. |
| Seznamy | `editable`, `degrade` | Ordered/bullet podle style-name, detailní ODT list style se normalizuje. |
| Page break | `editable`, `roundtrip` | Tempo podporovaný `text:style-name="page-break"`. |
| Tabulky | `editable`, `roundtrip` | `table:number-columns-spanned`, `table:number-rows-spanned`, `covered-table-cell`. |
| Obrázky inline | `editable`, `roundtrip` | `draw:frame` + `draw:image`. |
| Plovoucí obrázky | `editable`, `roundtrip` | ODT frame anchor + Tempo metadata pro common anchor model. |
| Header/footer | `editable`, `roundtrip` | Zachováno přes Tempo kompatibilitní metadata v `content.xml`. |
| Komentáře/anotace | `editable`, `roundtrip` | Tempo comment metadata a inline `tm:comment-id`; nativní annotation import degraduje na warning. |
| Footnote/endnote | `read-only render`, `degrade` | Základní reference se čtou; plné note bodies nejsou v této fázi ODT subsetu garantované. |
| Track changes | `degrade` | ODT změnové konstrukty nejsou ve fázi 17 garantované jako editovatelný subset. |
| Page/section styles | `degrade` | Import může mapovat dostupné údaje, detailní styly se needitují. |
| Neznámé ZIP entries | `degrade` | Importer je uchovává v `PreservedParts`, mimo `content.xml`, `mimetype` a obrázky. |
