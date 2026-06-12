# Code-review fixes TODO — canvas engine (2026-06-07)

Implementační plán pro nálezy z code review před commitem. Postupuj po nejmenších krocích,
odškrtávej `[x]` jen to, co je **skutečně hotové a ověřené** (test/build prošel).

## Globální poznámky k buildu/testům
- Po každé změně `.mjs` souboru: `npm run build:document-editor` (harness/dist načítá bundle, ne zdroj).
- `dotnet test` paralelně OOMuje (exit 137) → spouštěj s `-- xUnit.parallelizeTestCollections=false`.
- Node testy layout enginu: viz `tests/Tempo.Blazor.Tests/jsbuild/esbuild.mjs` harness.
- Pořadí práce: nejdřív correctness (1,2,3), pak DOCX fidelita (5,6,7), pak behavior (8), pak cleanup (9,10).

---

## Nález 1 — `allcaps` → neplatná hodnota `all-small-caps` v canvas font shorthandu
Soubor: [paragraph-tokenizer.mjs:66](../src/Tempo.Blazor/wwwroot/js/document-editor/layout/paragraph-tokenizer.mjs#L66),
měření v [font-metrics.mjs:63-71](../src/Tempo.Blazor/wwwroot/js/document-editor/layout/font-metrics.mjs#L63).

- [x] 1.1 Reprodukovat: najít/přidat Node test, který změří šířku běhu s markem `allcaps` a ověří, že `ctx.font` není odmítnut (šířka != fallback šířce neoznačeného běhu při jiné velikosti).
- [x] 1.2 Rozhodnout správnou sémantiku all-caps: má se text renderovat VERZÁLKAMI (uppercase glyfy), ne kapitálkami. Ověřit, jak se to dělá v atomic rendereru (kreslí `segment.text` přímo?).
- [x] 1.3 V tokenizeru přestat mapovat `allcaps` na `fontVariantCaps`. Místo toho nastavit příznak transformace textu (např. `style.textTransform = 'uppercase'` nebo `style.allCaps = true`).
- [x] 1.4 V tokenizeru/edit pipeline aplikovat uppercase na vykreslovaný text segmentu **bez** změny model-textu (offsety kurzoru musí zůstat na původních code-pointech 1:1; pozor na znaky, kde `toUpperCase()` mění délku, např. `ß`→`SS` — pokud to hrozí, mapovat per-grapheme nebo to zatím zakázat a dokumentovat).
- [x] 1.5 Zajistit, že `fontStringFromStyle` už nikdy nedostane `all-small-caps` (ponechat jen `small-caps` pro `smallcaps`).
- [x] 1.6 Ověřit, že měření (`measureText`) i kreslení používají stejný (uppercase) text, aby šířky seděly s glyfy.
- [x] 1.7 `npm run build:document-editor`.
- [x] 1.8 Spustit Node test z 1.1 + existující tokenizer/font-metrics testy → zelené.
- [ ] 1.9 (manuálně) v prohlížeči: běh s all-caps markem se vykreslí verzálkami, kurzor sedí, lámání řádků sedí.

---

## Nález 2 — Find/Replace přeskakuje `ContentControlBlockContent`
Soubory: [DocumentSearchService.cs:64-87](../src/Tempo.Blazor.Abstractions/DocumentEditor/Services/DocumentSearchService.cs#L64),
[DocumentReplaceService.cs:57-72,166-184](../src/Tempo.Blazor.Abstractions/DocumentEditor/Services/DocumentReplaceService.cs#L57).
Typ: [ContentControlBlockContent](../src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentBlocks.cs#L218) má vnořené `Blocks`.

- [x] 2.1 Test (search): dokument s blokovým content controlem obsahujícím odstavec s hledaným textem → `Search` ho najde.
- [x] 2.2 Test (replace): `ReplaceAll` nahradí text uvnitř content controlu.
- [x] 2.3 V `DocumentSearchService` switchi přidat `case ContentControlBlockContent cc:` → `CollectFromBlocks(cc.Blocks, ...)` (analogicky k `TableBlockContent`).
- [x] 2.4 V `DocumentReplaceService.FindBlockInList` (ř. ~57-72) přidat rekurzi do `ContentControlBlockContent.Blocks` (vedle větve pro table cells).
- [x] 2.5 Ověřit, zda `GetInlines`/`SetInlines` potřebují ContentControl — NE (control nemá vlastní inlines, jen child blocky), takže stačí rekurze přes blocky. Potvrdit.
- [x] 2.6 Zkontrolovat, zda existuje i třetí místo, kde se prochází stromem bloků (outline/word-count/operation applier) a má stejnou díru. Pokud ano, založit follow-up poznámku (mimo scope tohoto fixu).
  - Follow-up mimo scope: `DocumentOutlineService`, `DocumentTextDiffHelper`, `DocumentEditorSchema.WalkBlocks` a `DocumentAnchorMapBuilder` mají table-aware průchody, ale ne všechny zatím rekurzují do `ContentControlBlockContent.Blocks`.
- [x] 2.7 `dotnet test` na search/replace testy → zelené.

---

## Nález 3 — `mergeSoftHyphenTokens` zahazuje 2.+ soft hyphen a shy přes hranici běhů
Soubor: [line-breaker.mjs:466-493](../src/Tempo.Blazor/wwwroot/js/document-editor/layout/line-breaker.mjs#L466).

- [x] 3.1 Test A: slovo se 2 měkkými spojovníky (`edi­tor­ial`) → po merge je to **jeden** word token s oběma `­` uvnitř (ne `['edi­tor', 'ial']`).
- [x] 3.2 Test B: shy mezi dvěma běhy s různým `runId` → soft hyphen se nesmí ztratit; ověřit chování (buď merge i přes runId, nebo zachovat shy jako break-opportunity s vykresleným spojovníkem).
- [x] 3.3 Test C: lámání řádku ve slově s 2+ shy → na zalomení se vykreslí spojovník (ne neviditelné rozdělení).
- [x] 3.4 Přepsat smyčku tak, aby slévala **řetězec** `word (shy word)+` do jednoho tokenu (while/akumulace), ne jen jednu trojici. Po slití nastavit `index` za poslední spotřebovaný token.
- [x] 3.5 Vyřešit cross-run případ: rozhodnout, zda merge vyžaduje shodný `runId`. Pokud ano, lone shy na hranici běhů **nesmí** spadnout do „drop" větve — buď ho ponech jako vlastní break-token, který line-breaker umí vykreslit jako spojovník, nebo merge proveď i přes runId a styl řeš per-segment.
- [x] 3.6 Ověřit offset mapping: merged token musí mít `start`/`end` pokrývající celý původní rozsah (vč. shy), aby hit-test/kurzor seděly.
- [x] 3.7 `npm run build:document-editor`.
- [x] 3.8 Node testy A/B/C + existující line-breaker testy → zelené.
- [ ] 3.9 (manuálně) prohlížeč: slovo `edi­tor­ial` v úzkém sloupci se láme se spojovníkem na obou bodech, kurzor projde znak po znaku.

---

## Nález 5 — Duplicitní a divergentní `FlattenMath*` (exporter vs importer)
Exporter: [DocumentDocxExporter.cs:453-465](../src/Tempo.Blazor.DocumentFormats/Docx/DocumentDocxExporter.cs#L453)
(má `subSup`, `nary`, zlomek v závorkách). Importer: [DocumentDocxImporter.cs:2448-2459](../src/Tempo.Blazor.DocumentFormats/Docx/DocumentDocxImporter.cs#L2448)
(nemá `subSup`/`nary`, jiný formát zlomku).

- [x] 5.1 Rozhodnout kanonický výstup flatten (jeden formát zlomku — buď `(num)/(den)` nebo `num/den` — a kompletní sadu element typů: fraction, radical, sup, sub, subSup, nary, matrix, fallback).
- [x] 5.2 Test: matematický obsah s `subSup` a `nary` → flatten vrací neprázdný očekávaný text (chytá současný bug importeru, kde se zploští na prázdno).
- [x] 5.3 Vytvořit sdílený helper `Internal/DocumentMathText.cs` (`internal static`), metody `FlattenMathContent`/`FlattenMathElement` s kanonickou logikou z 5.1.
- [x] 5.4 Exporter: smazat lokální `FlattenMathContent`/`FlattenMathElement`, volat sdílený helper.
- [x] 5.5 Importer: smazat lokální kopie, volat sdílený helper.
- [x] 5.6 Zkontrolovat, zda se tím nezměnil žádný existující DOCX export/import golden test (byte-parity). Pokud ano, vyhodnotit, zda je nový výstup správnější, a aktualizovat baseline vědomě.
- [x] 5.7 `dotnet test` na DocumentFormats testy → zelené.

---

## Nález 6 — `ParseFieldType` default → `Ref` ztrácí neznámé typy polí
Soubor: [DocumentDocxImporter.cs:846-867](../src/Tempo.Blazor.DocumentFormats/Docx/DocumentDocxImporter.cs#L846),
zpětný export [BuildFieldInstruction:408](../src/Tempo.Blazor.DocumentFormats/Docx/DocumentDocxExporter.cs#L408).
Enum: [DocumentFieldType:649](../src/Tempo.Blazor.Abstractions/DocumentEditor/Models/DocumentInline.cs#L649).

- [x] 6.1 Zjistit, zda `DocumentFieldType` má hodnotu pro „neznámé/generické" pole (např. `Unknown`/`Raw`). Pokud ne, přidat ji.
- [x] 6.2 Zajistit, že `DocumentFieldRun` umí uchovat **surovou instrukci** (raw instruction string) pro round-trip i u neznámého typu (ověřit, že property existuje — `Instruction` se už plní na ř. 787/400).
- [x] 6.3 Test: import DOCX s polem `MERGEFIELD Name` (nebo `HYPERLINK`) → typ je `Unknown`/`Raw`, instrukce zachována.
- [x] 6.4 Test (round-trip): re-export takového pole vyprodukuje **původní** instrukci, ne `REF …`.
- [x] 6.5 `ParseFieldType` default změnit z `=> DocumentFieldType.Ref` na neznámý typ (z 6.1).
- [x] 6.6 `BuildFieldInstruction`: pro neznámý typ emitovat uloženou raw instrukci místo přepisu na `REF`.
- [x] 6.7 `dotnet test` na DOCX field testy → zelené.

---

## Nález 7 — Kolize `SECTIONPAGES`: `SectionPageNumber` se importuje jako `SectionPageCount`
Exporter: [DocumentDocxExporter.cs:433-434](../src/Tempo.Blazor.DocumentFormats/Docx/DocumentDocxExporter.cs#L433)
(oba typy → `"SECTIONPAGES"`). Importer: [DocumentDocxImporter.cs:866](../src/Tempo.Blazor.DocumentFormats/Docx/DocumentDocxImporter.cs#L866).

- [x] 7.1 Zjistit správné OOXML instrukce: `SECTIONPAGES` = počet stránek v sekci; číslo stránky v sekci se ve Wordu typicky dělá přes `PAGE` v rámci section restartu — ověřit, jaký field code Word reálně používá pro „page within section" (může to být jen `PAGE`). Podle toho zvolit odlišný instrukční řetězec nebo přepínač.
- [x] 7.2 Test: export `SectionPageNumber` a `SectionPageCount` vyprodukuje **různé** instrukce.
- [x] 7.3 Test (round-trip bez tempo field-json sidecar): cizí DOCX se `SECTIONPAGES` se importuje jako `SectionPageCount`; samostatný `SectionPageNumber` field round-tripuje na správný typ.
- [x] 7.4 Upravit `BuildFieldInstruction` tak, aby `SectionPageNumber` a `SectionPageCount` měly odlišné instrukce.
- [x] 7.5 Upravit `ParseFieldType` tak, aby rozeznal obě varianty.
- [x] 7.6 `dotnet test` na field/section testy → zelené.

---

## Nález 8 — Vyhledávání: přechod z překryvných na nepřekryvné shody (potvrdit záměr)
Soubor: [DocumentSearchService.cs:174](../src/Tempo.Blazor.Abstractions/DocumentEditor/Services/DocumentSearchService.cs#L174)
(`pos = idx + Math.Max(1, query.Text.Length)`).

- [ ] 8.1 Potvrdit s uživatelem/produktovým záměrem, že nepřekryvné chování je žádané (pro replace-all typicky ano).
- [x] 8.2 Pokud ANO: přidat explicitní test, který chování zafixuje (`aa` v `aaaa` → 2 shody) + komentář k řádku, proč nepřekryvné.
- [ ] 8.3 Pokud NE: vrátit `pos = idx + 1` a doplnit test na 3 shody.
- [x] 8.4 Zkontrolovat, že žádný existující search test na tomto chování nestojí (grep testů na overlapping/count).
- [x] 8.5 `dotnet test` → zelené.

---

## Nález 9 — Plýtvání v hot-path layout smyčce
Soubory: [font-metrics.mjs:77,82](../src/Tempo.Blazor/wwwroot/js/document-editor/layout/font-metrics.mjs#L77),
`measureToken`/tokenizer v [line-breaker.mjs](../src/Tempo.Blazor/wwwroot/js/document-editor/layout/line-breaker.mjs)
a [paragraph-tokenizer.mjs](../src/Tempo.Blazor/wwwroot/js/document-editor/layout/paragraph-tokenizer.mjs).

- [x] 9.1 `font-metrics.mjs`: `Array.from(style.text).length` (count code-pointů pro letter-spacing) počítat jen když `style.letterSpacing` je nenulový; jinak count přeskočit/levně.
- [x] 9.2 `measureToken`: nahradit `kind.replace(/[\s_-]/g,'').toLowerCase() === 'math'` přímým `token.type === 'math'` (nebo příznakem nastaveným tokenizerem). Ověřit, kde se `kind` plní.
- [x] 9.3 Tokenizer: deep-clone `style`/`marks` provést **jednou per run**, ne per emitovaný token (sdílet referenci nebo klonovat na úrovni běhu). Pozor, aby se sdílený objekt nemutoval downstream — pokud se mutuje, zachovat clone, ale jen na hranici běhu.
- [x] 9.4 (volitelně) `finishCurrent`/hyphenated rescan: místo `segments.some(s => s.hyphenated)` nastavit boolean příznak na řádku při pushi hyphenated segmentu.
- [x] 9.5 Ověřit, že žádná optimalizace nezměnila výstup: existující layout/line-breaker Node testy → zelené.
- [ ] 9.6 (volitelně) mikro-benchmark/profil na velkém dokumentu před/po, zapsat čísla do PR poznámky.
- [x] 9.7 `npm run build:document-editor`.

---

## Nález 10 — Duplikace dispatch/markup kódu
Soubory: [TmDocumentEditor.razor.cs:8168-8200](../src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor.cs#L8168),
[RouteToCanvasEngineAsync:7444](../src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditor.razor.cs#L7444),
[TmDocumentEditorToolbar.razor (equation gallery)](../src/Tempo.Blazor/Components/DocumentEditor/TmDocumentEditorToolbar.razor),
[DocumentDocxExporter.GetInlinePlainText](../src/Tempo.Blazor.DocumentFormats/Docx/DocumentDocxExporter.cs),
[DocumentModelText.GetInlineText:21](../src/Tempo.Blazor.DocumentFormats/Internal/DocumentModelText.cs#L21).

### 10a — duplicitní mark-switch v `ToggleInlineMarkAsync`
- [x] 10a.1 Vytvořit privátní `static string MarkCommandId(InlineMarkType markType)` s tím jedním switchem.
- [x] 10a.2 Nahradit oba inline switche (`RouteToCoreEngineAsync(...)` i `RouteToCanvasEngineAsync(...)`) voláním `MarkCommandId(markType)`.
- [x] 10a.3 Component testy editoru → zelené.

### 10b — opakovaný blok `Exec + Sync + Focus + return` (~20×)
- [x] 10b.1 Přidat overload `RouteToCanvasEngineAsync(string command, object? argument, bool focus)` který po Exec+Sync zavolá i `FocusAsync` (ponechat stávající 2-arg overload, který focus nedělá).
- [x] 10b.2 Najít všechna místa s ručním `if (UsingCanvasEngine && _canvasHost is not null) { Exec; Sync; Focus; return; }` (grep `_canvasHost.ExecCommandAsync`/`FocusAsync`).
- [x] 10b.3 Postupně je nahradit voláním nového overloadu; dbát na to, aby se zachovaly případné `StateHasChanged` (ty, co je navíc, vyřešit zvlášť).
- [x] 10b.4 Po každé dávce náhrad: component testy → zelené.

### 10c — `GetInlinePlainText` duplikuje `DocumentModelText.GetInlineText`
- [x] 10c.1 Rozšířit `DocumentModelText.GetInlineText` o chybějící inline typy z exporterové verze (`DocumentFieldRun`, `DocumentMathRun`, `DocumentContentControlRun`), sjednotit fallback pro drawing/token.
- [x] 10c.2 Smazat `GetInlinePlainText` v exporteru, volat sdílený helper.
- [x] 10c.3 Ověřit, že ostatní konzumenti `GetInlineText` (ODT importer atd.) nezměnili výstup nečekaně — DOCX/ODT testy → zelené.

### 10d — galerie rovnic není data-driven
- [x] 10d.1 Definovat kolekci `EquationPaletteItems` (preset id, ikona/label key, data-testid) po vzoru `SymbolPaletteItems`/`EmojiPaletteItems`.
- [x] 10d.2 Nahradit ~28 ručních tlačítek jedním `@foreach` renderem; zachovat stávající data-testid hodnoty (E2E na ně mohou spoléhat — ověřit grep `document-equation-`).
- [x] 10d.3 Component/E2E testy toolbaru → zelené.

---

## Závěr
- [x] Spustit celou relevantní test sadu (`-- xUnit.parallelizeTestCollections=false`) + Node layout testy → vše zelené.
- [x] `npm run build:document-editor` finálně (kvůli .mjs změnám v 1/3/9).
- [x] Projít `git diff`, zkontrolovat že nezůstal mrtvý kód po deduplikaci.
- [x] Aktualizovat tento soubor (odškrtnutí) a teprve pak commit.
</content>
</invoke>
