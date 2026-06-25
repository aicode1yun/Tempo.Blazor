# TmRichEditorSimple — čistá serializace tokenů (plain-text) + řídicí konstrukce (block chips)

Vytvořeno 2026-06-12. TDD plán (red-green-refactor; test MUSÍ nejdřív padnout). Rozšíření **sdílené core
komponenty** `TmRichEditorSimple` (+ `TmRichEditorFull`, sdílí token systém) o dvě opt-in schopnosti
plynoucí z diskuze nad e-mailovým editorem:

1. **Plain-text serializace tokenů (#1):** ukládat hodnotu BEZ chip `<span>` markupu (jen `{{ key }}`),
   ale při načtení `{{ key }}` zpět rehydratovat do chipů. Řeší prosakování editorového markupu do
   uložených dat (dnes `getHtml` vrací `<span class="tm-token-chip" data-token-key="…">{{display}}</span>`)
   a opravuje skrytou past, že chip používá v textu **display name** místo **key**.
2. **Řídicí konstrukce jako block-chipy (#2, „option B"):** `{{ for … }}` / `{{ if … }}` / `{{ end }}`
   reprezentované jako odlišně barevné NE-editovatelné chipy s hoverem, vkládatelné z `{{` menu. Editor
   **nevynucuje párování ani strukturu** — jen vizuálně označí a bezpečně vloží. Sémantiku (for/if/end)
   dodává konzument přes provider, ne core editor.

## Tvrdé hranice (kontext: core komponenta používaná Activity / NotionEditor / DocumentEditor / e-maily)

- **Default chování = bajt-identické s dneškem.** Vše nové je opt-in (`TokenSerialization.Html` default).
  Phase 0 to zamkne regresním testem.
- **Žádná Scriban/šablonová logika v core editoru.** Editor zná jen generický `{{` trigger, `IToken`
  a (nově) `TokenKind`. „for/if/end" je doména konzumenta (e-mailová vrstva), ne `TmRichEditorSimple`.
- **Žádné strukturální editování** (párování open/close, vnořené editovatelné regiony, AST). To je
  samostatný velký projekt — VĚDOMĚ mimo scope; option B je čistě vizuální + insert.
- Změny v `Tempo.Blazor.Abstractions` (`IToken`, `ITokenDataProvider`) MUSÍ být přidané přes
  default member/property hodnoty, aby se nerozbily existující implementace (Notion, DocumentEditor…).

## Současný stav (ověřeno 2026-06-12)

- `richEditor.js`: `getHtml` = syrové `element.innerHTML`; `setHtml` = prosté nastavení `innerHTML`
  (ŽÁDNÁ rehydratace `{{ }}`); `insertToken` vloží `<span class="tm-token-chip" data-token-key …
  contenteditable="false">{{displayName}}</span>`.
- `TmRichEditorSimple.razor`: `Value` se nahraje přes `setHtml` v `OnParametersSet`; `getHtml` se čte
  do `Value` při změnách → chip prosakuje do uložené hodnoty, text používá display name.
- `IToken` (`src/Tempo.Blazor.Abstractions/Interfaces/IToken.cs`): Key/DisplayName/Description/Category/
  Icon/ColorClass/TypeLabel. `ITokenDataProvider`: `SearchTokensAsync(query)`, `SupportsCreation`, `Refresh()`.
- Testy: bUnit `tests/Tempo.Blazor.Tests/Activity/TmRichEditorSimpleTests.cs` +
  `TmRichEditorFullTokenTests.cs`. JS chování `richEditor.js` se ověřuje Playwright E2E
  (`tests/Tempo.Blazor.E2E`); pure JS transformy lze vytáhnout do ESM helperu a pokrýt Node `.mjs`
  testem (vzor: `wwwroot/js/document-editor/**/__tests__/*.test.mjs`).

---

## FÁZE 0 — Baseline guard (zpětná kompatibilita)

- [ ] 0.1 bUnit/E2E test, který zachytí DNEŠNÍ výstup v `Html` módu (default): vložení tokenu →
      `Value` obsahuje `<span class="tm-token-chip" data-token-key=…>` (chip markup). Tento test
      MUSÍ zůstat zelený po celou dobu = důkaz, že default se nemění.
- [ ] 0.2 Inventář konzumentů `TmRichEditorSimple`/`TmRichEditorFull` (Activity comments, Notion,
      DocumentEditor, e-maily) + poznámka, že žádný nesmí nastavovat nové opt-in parametry, dokud
      nechce nové chování.

## FÁZE 1 — Abstractions: klasifikace + resolve-by-key (zpětně kompatibilní)

- [ ] 1.1 `IToken.Kind` → nový `enum TokenKind { Inline, BlockOpen, BlockClose }`; do `IToken` přidat
      `TokenKind Kind => TokenKind.Inline;` (**default na interface**, existující implementace beze změny).
      `TokenItem` (UI model) dostane `Kind` s defaultem `Inline`.
- [ ] 1.2 `ITokenDataProvider.GetTokenAsync(string key, CancellationToken)` → vrací `IToken?`;
      **default interface metoda** delegující na `SearchTokensAsync(key)` + exact-key match (existující
      providery fungují bez úprav).
- [ ] 1.3 Unit testy: default `Kind == Inline`; `GetTokenAsync` default najde token přes Search;
      provider bez override se chová jako dřív.

## FÁZE 2 — Plain-text serializace (výstup bez chipů)

- [ ] 2.1 `TmRichEditorSimple` (+Full): `public enum TokenSerialization { Html, PlainText }` +
      `[Parameter] public TokenSerialization TokenSerialization { get; set; } = TokenSerialization.Html`.
- [ ] 2.2 JS `tmRichEditor.getHtmlPlainTokens(element)`: naklonuje obsah, každý `.tm-token-chip`
      nahradí **textovým uzlem `{{ data-token-key }}`** (KLÍČ, ne display!), vrátí `innerHTML`.
      Extrahovat jako čistou funkci → Node `.mjs` test (vstup HTML s chipem → výstup `{{key}}`).
- [ ] 2.3 V `PlainText` módu se `Value`/`ValueChanged` plní přes `getHtmlPlainTokens` (ne `getHtml`).
      Test: vložení tokenu „first_name" (display „First name") → `Value` obsahuje `{{ first_name }}`,
      NEobsahuje `tm-token-chip` ani „First name".
- [ ] 2.4 Guard: v `Html` módu výstup beze změny (Phase 0.1 zelený).

## FÁZE 3 — Rehydratace při načtení (`{{ key }}` → chip)

- [ ] 3.1 JS `tmRichEditor.rehydrateTokens(element, tokenMetaMap)`: projde textové uzly, najde
      `{{ … }}` (mimo již existující chipy), nahradí chip spanem podle `tokenMetaMap[key]`
      (display/icon/colorClass/kind). Neznámé klíče → ponechat jako text (nebo neutrální chip — rozhodnout
      v 3.4). Čistá funkce → Node `.mjs` test.
- [ ] 3.2 `TmRichEditorSimple` v `PlainText` módu po `setHtml` posbírá klíče (`{{…}}`), zavolá
      `TokenProvider.GetTokenAsync` pro každý (batch), sestaví meta-mapu, zavolá `rehydrateTokens`.
- [ ] 3.3 Round-trip test (E2E, reálný prohlížeč): načíst `Value="Ahoj {{ first_name }}"` → zobrazí se
      chip → uložení → `Value` zase `{{ first_name }}` (idempotentní). bUnit ověří wiring (volání
      GetTokenAsync se správnými klíči).
- [ ] 3.4 Rozhodnout chování neznámého klíče (provider vrátí null): MVP = ponechat jako prostý text
      (bezpečné, needituje cizí `{{}}`). Zdokumentovat.
- [ ] 3.5 Edge: nerehydratovat `{{` uvnitř `<code>`/escapovaných sekvencí; `{{{ }}}` / `{% %}`
      mimo scope (Scriban má jen `{{ }}`). Test na „nesmí sežrat dvojité braces v code bloku".

## FÁZE 4 — Block-chipy pro řídicí konstrukce (#2 option B)

- [ ] 4.1 Render rozlišení podle `IToken.Kind`: `Inline` = dnešní chip; `BlockOpen`/`BlockClose` =
      chip s odlišnou CSS třídou (`tm-token-chip--block-open` / `--block-close`) + hover popisem.
      ŽÁDNÉ párování/validace v editoru.
- [ ] 4.2 Autocomplete (`TokenAutocomplete`) zobrazí block tokeny (kind badge) a umí je vložit
      (`insertToken` s kind → správná CSS třída + text z key).
- [ ] 4.3 Rehydratace (Phase 3) ctí `Kind`: `{{ for … }}` resolvnuté jako `BlockOpen` → block chip;
      skalár → inline chip; neresolvnuté → text. Test.
- [ ] 4.4 CSS pro block chipy (design tokeny `var(--tm-*)`, vizuálně odlišené od inline). Scoped/sdílené
      dle konvence richEditoru.
- [ ] 4.5 Dokumentovat: editor block tokeny NEpáruje; je to vizuální + insert affordance. Strukturální
      editace = budoucí samostatná featura.

## FÁZE 5 — Zapojení do e-mailového editoru (konzument)

- [ ] 5.1 `EmailVariableTokenProvider : ITokenDataProvider` v balíčku `Tempo.Blazor.EmailTemplates`
      (NE v Abstractions — logická vrstva zůstává bez UI závislostí). Zdroj tokenů: klíče ze
      sample-dat (zploštělé na tečkové cesty) + proměnné už použité v dokumentu
      (`EmailDocumentVariableExtractor`), `SupportsCreation = true`. Skaláry = `Inline`.
- [ ] 5.2 Provider dodá i řídicí snippety jako `BlockOpen`/`BlockClose`: `for … in …`, `if …`, `end`.
      (Key = doslovný Scriban výraz; display = lidský popis.)
- [ ] 5.3 Text blok: `TmEmailPropertyPanel` → `<TmRichEditorSimple SupportsTokens="true"
      TokenSerialization="PlainText" TokenProvider="…" TokenTrigger="{{" />`. Content se ukládá jako
      ryzí Scriban text (žádný chip markup), při reopenu se zobrazí chipy. → odpadá chip-leak
      caveat z [[project_email_template_editor]].
- [ ] 5.4 Retire bespoke řešení: odstranit `wwwroot/tm-email-variable-insert.js` cestu pro Text blok;
      `TmEmailVariablePicker` ponechat **jen** pro prostá pole (Subject/Preheader/button href/alt),
      kde se rich editor nepoužívá (slim verze) — nebo zvážit token-capable single-line variantu.
- [ ] 5.5 Aktualizovat e-mailové bUnit/E2E (variable picker → token dropdown insertion + round-trip
      Content = čistý `{{ }}`).

## FÁZE 6 — Regrese, dokumentace, paměť

- [ ] 6.1 Plná regrese VŠECH konzumentů: `Tempo.Blazor.Tests` (Activity/Notion/DocumentEditor token
      testy), e-mailové suity, dotčené E2E (sériově — paralelní `dotnet test` OOMuje, exit 137,
      `-- xUnit.parallelizeTestCollections=false`). Phase 0.1 guard MUSÍ zůstat zelený.
- [ ] 6.2 XML doc na nové public API (`TokenSerialization`, `TokenKind`, `GetTokenAsync`) — build bez
      CS1591 (TWAE).
- [ ] 6.3 COMPONENTS.md / JSON dokumentace (MCP): doplnit nové parametry `TmRichEditorSimple`.
- [ ] 6.4 Aktualizovat paměť ([[project_email_template_editor]]) + tento plán odškrtat.

## Otevřená rozhodnutí (k potvrzení před implementací)

- Neznámý klíč při rehydrataci: text vs neutrální chip (návrh: **text**, MVP).
- Block snippety: pevná sada (for/if/end) vs konfigurovatelné přes provider (návrh: **provider** —
  drží to core generický).
- Prostá pole (Subject…): ponechat slim `TmEmailVariablePicker`, nebo investovat do token-capable
  single-line inputu (návrh: **slim picker** teď, single-line input jako follow-up).

## Pozn. k testovací strategii
- C# wiring + parametry + provider: bUnit (`tests/Tempo.Blazor.Tests/Activity`) + unit (Abstractions).
- JS transformy (`getHtmlPlainTokens`, `rehydrateTokens`): vytáhnout do čistých funkcí + Node `.mjs`
  testy; chování v reálném DOM/contenteditable ověřit Playwright E2E (round-trip, block chipy, idempotence).
