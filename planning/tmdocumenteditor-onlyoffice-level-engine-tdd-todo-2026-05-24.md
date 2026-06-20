# TmDocumentEditor - ONLYOFFICE-level engine recovery TDD TODO

Datum založení: 2026-05-24  
Stav: navrženo, čeká na implementaci  
Priorita: P0 - editor nesmí dál zelenat v testech, pokud se při ručním použití nechová jako skutečný dokumentový editor

## Proč tento dokument existuje

Po analýze ONLYOFFICE v `/home/pavel/NetProjects/onlyfficeservergit` je zřejmé, že současný `TmDocumentEditor` se pořád chová jako kombinace Blazor shellu, `contenteditable` DOMu, runtime patchů a částečně synchronizovaného toolbar stavu. To nestačí pro editor na úrovni Word/Google Docs/ONLYOFFICE.

Aktuální ručně ověřené problémy:

- formátování přes ribbon ani floating toolbar spolehlivě neaplikuje bold, font, text color, background/highlight ani font size,
- klik v ribbonu umí zrušit výběr dřív, než se command aplikuje,
- ribbon a floating toolbar nejsou jedna pravda o aktuálním formátování,
- track changes se zlepšilo, ale stále vytváří příliš jemné fragmenty,
- při psaní vedle komentovaného textu se nový text umí dostat do cizího comment/highlight rozsahu,
- undo v ribbonu zůstává disabled nebo neodráží skutečný undo stack,
- předchozí E2E testy byly příliš často "API-green", ale ne "human-green".

Tento plán je implementační checklist po malých krocích. Každý krok musí být dělaný TDD: nejdřív RED test, potom minimální oprava, potom refaktor a širší regresní běh.

## Inspirace z ONLYOFFICE

Z ONLYOFFICE nepřebírat kód. Projekt má jinou licenci a jinou architekturu. Převzít pouze architektonické principy:

- UI toolbar nikdy přímo nemění DOM dokumentu.
- Toolbar volá veřejný command API editoru.
- Command se provádí jako transakce v document runtime modelu.
- Runtime vlastní selection, caret, text runs, formatting state, undo/redo a track changes.
- Po každé akci runtime publikuje canonical `FormattingState`.
- Ribbon i floating toolbar čtou stejný `FormattingState`.
- Psaní probíhá okamžitě v runtime/DOM vrstvě bez čekání na Blazor render.
- Blazor je shell, provider boundary a UI orchestrace, ne live autorita pro psaní.

## Cílový stav

- Uživatel vybere text myší, klikne v ribbonu nebo floating toolbaru a formát se aplikuje přesně na vybraný text.
- Pokud není nic vybráno, změna formátu nastaví styl pro následně psaný text.
- Selection se při toolbar kliknutí neztratí.
- Ribbon a floating toolbar jsou synchronní ve všech active/mixed/disabled stavech.
- Text color, highlight/background, font name, font size, bold, italic, underline, strike a paragraph commands jsou okamžité, undoable a persistují přes save/reload.
- Track changes vytváří souvislé insertion/deletion změny, ne jeden fragment na písmeno.
- Nový text za komentářem nebo zvýrazněním se nevloží do cizí annotation range.
- Undo/redo je dostupné a odpovídá skutečným runtime transakcím.
- E2E testy používají reálnou myš/klávesnici pro uživatelské akce a kontrolují viditelný výsledek, computed style, toolbar state, selection, undo state a save/reload.

## Nevyjednatelná pravidla implementace

- [ ] Každý user-facing bug musí mít nejdřív RED E2E, který selže stejným způsobem jako ruční test.
- [ ] Žádný E2E test nesmí volat interní JS command místo kliknutí v ribbonu/floating toolbaru, pokud testuje toolbar.
- [ ] Interní JS/debug API je povolené jen pro seed, snapshot, metriky a dodatečné ověření.
- [ ] Toolbar command nesmí záviset na aktuální browser `window.getSelection()` po toolbar kliknutí.
- [ ] Editor runtime musí mít vlastní selection token/snapshot.
- [ ] Blazor nesmí během psaní přerenderovat textové DOM uzly aktivního dokumentu.
- [ ] Každý command musí být undoable nebo explicitně označený jako non-editing UI action.
- [ ] Track changes nesmí splitovat podle každé klávesy ani podle per-key timestampu.
- [ ] Testy se nesmí oslabovat, aby prošly se špatným UX.
- [ ] Po každé fázi musí projít cílené unit/JS testy, cílené E2E testy a krátký ruční smoke.

## Pracovní větve testů

Nové a upravované testy mají být rozdělené do těchto vrstev:

- model/unit testy v `tests/Tempo.Blazor.Tests/Components/DocumentEditor/Wysiwyg/Model/`,
- JS runtime testy v `tests/Tempo.Blazor.Tests/DocumentEditor/DocumentEditorRuntime*JavaScriptTests.cs`,
- Blazor component/toolbar testy v `tests/Tempo.Blazor.Tests/Components/DocumentEditor/`,
- human-like E2E testy v `tests/Tempo.Blazor.E2E/`,
- regression recovery E2E testy pro přesné video/ruční scénáře,
- strict quality E2E testy pro obecné invariants.

## Fáze 0: Pravdivý baseline a test audit

Stav: hotovo pro baseline; navazující opravy pokračují od fáze 1

### 0.1 Založit nový ONLYOFFICE-level E2E soubor

- [x] Vytvořit `tests/Tempo.Blazor.E2E/DocumentEditorOnlyOfficeParityE2ETests.cs`.
- [x] Přidat test class do stejného stylu jako `DocumentEditorRegressionRecoveryE2ETests.cs`.
- [x] Použít existující `DocumentEditorE2ETestBase`.
- [x] Přidat helper `OpenOnlyOfficeParityDocumentAsync`.
- [x] Seedovat deterministický dokument pro parity testy.
- [x] Seed musí obsahovat odstavec pro inline formatting.
- [x] Seed musí obsahovat odstavec s komentovaným rozsahem a textem hned za komentářem.
- [x] Seed musí obsahovat odstavec s existujícími mixed text properties.
- [x] Seed musí obsahovat odstavec pro collapsed caret typing style.
- [x] Seed musí obsahovat odstavec pro track changes insertion.
- [x] Seed musí obsahovat odstavec pro track changes deletion.
- [x] Seed musí obsahovat header/footer, tabulku a obrázek jen jako regresní hlídače layoutu.

### 0.2 Zapsat aktuální RED baseline

- [x] E2E RED: vybrat text myší a kliknout Bold v ribbonu.
- [x] Ověřit, že test obsahuje computed `font-weight` assertion; aktuální první selhání je ještě dřív na nestabilním reálném selection timeoutu.
- [x] E2E RED: vybrat text myší a změnit font size v ribbonu.
- [x] Ověřit, že test obsahuje computed `font-size` assertion; aktuální první selhání je ještě dřív na nestabilním reálném selection timeoutu.
- [x] E2E RED: vybrat text myší a nastavit text color v ribbonu.
- [x] Ověřit, že test obsahuje computed `color` a toolbar swatch sync assertion; aktuální první selhání je ještě dřív na nestabilním reálném selection timeoutu.
- [x] E2E RED: vybrat text myší a nastavit highlight/background v ribbonu.
- [x] Ověřit, že test obsahuje computed `background-color` assertion; aktuální první selhání je ještě dřív na nestabilním reálném selection timeoutu.
- [x] E2E RED: stejnou akci zopakovat přes floating toolbar.
- [x] E2E RED: kliknout trochu vedle font size dropdownu a ověřit, že selection se nesmí ztratit.
- [x] E2E RED: zapnout track changes a napsat `jak se mas`.
- [x] Ověřit, že text je přesně `jak se mas`, ne přeházený nebo zpožděný.
- [x] Ověřit, že vznikne jedna souvislá insertion revision nebo malé množství logických fragmentů, ne jeden fragment na písmeno.
- [x] E2E RED: psát za text s komentářem a ověřit, že nový text není v comment highlight range.
- [x] E2E RED: po formatting commandu ověřit enabled undo.
- [x] Výsledky baseline zapsat do sekce "Baseline výsledky" tohoto souboru.

### 0.3 Audit současných E2E testů

- [x] Projít `tests/Tempo.Blazor.E2E/DocumentEditorPhase4ToolbarE2ETests.cs`.
- [x] Označit testy, které jen volají interní commandy a netestují reálný toolbar click.
- [x] Projít `tests/Tempo.Blazor.E2E/DocumentEditorStrictEnginePhase3E2ETests.cs`.
- [x] Projít `tests/Tempo.Blazor.E2E/DocumentEditorStrictEnginePhase7E2ETests.cs`.
- [x] Projít `tests/Tempo.Blazor.E2E/DocumentEditorJsRuntimeCommandTests.cs`.
- [x] Projít `tests/Tempo.Blazor.E2E/DocumentEditorJsRuntimeSelectionTests.cs`.
- [x] Projít `tests/Tempo.Blazor.E2E/DocumentEditorJsRuntimeRevisionTests.cs`.
- [x] Projít `tests/Tempo.Blazor.E2E/DocumentEditorRegressionRecoveryPhase11E2ETests.cs`.
- [x] Projít `tests/Tempo.Blazor.E2E/DocumentEditorRegressionRecoveryPhase12E2ETests.cs`.
- [x] Projít `tests/Tempo.Blazor.E2E/DocumentEditorRegressionRecoveryPhase13E2ETests.cs`.
- [x] Vytvořit tabulku: test name, co opravdu ověřuje, co maskuje, nový expected behavior.
- [x] Každý slabý test buď přepsat v pozdější fázi, nebo označit jako diagnostický.

| Test soubor | Co opravdu ověřuje | Co maskuje / riziko | Nový expected behavior |
|---|---|---|---|
| `DocumentEditorPhase4ToolbarE2ETests.cs` | Viditelnost ribbon/tab UI, přepnutí režimů, menu a shell toolbaru. | Neprokazuje, že human selection přežije toolbar pointerdown ani že computed style v dokumentu změnil vybraný text. | Ponechat jako layout/shell smoke; formatting přesunout do parity E2E s reálnou myší, selection snapshotem, computed style a undo. |
| `DocumentEditorStrictEnginePhase3E2ETests.cs` | Diagnostické engine/facade invarianty přes JS state. | Může zezelenat, i když uživatelský toolbar click nefunguje. | Označit jako diagnostický strict engine test; nesmí nahrazovat UX E2E. |
| `DocumentEditorStrictEnginePhase7E2ETests.cs` | Rendering scope, atomic swap a runtime diagnostiku. | Neověřuje skutečný uživatelský formatting command ani interakci s ribbonem. | Ponechat jako strict rendering invariant; doplnit human parity testy. |
| `DocumentEditorJsRuntimeCommandTests.cs` | Runtime command API, command state a částečně toolbar interakci. | Část výběru je připravovaná interně přes `EvaluateAsync`, takže neodhalí ztrátu selection při reálném clicku. | Přepsat relevantní scénáře na selection token + real click; interní setup nechat jen pro čisté runtime diagnostiky. |
| `DocumentEditorJsRuntimeSelectionTests.cs` | Selection registry, snapshoty a runtime selection state. | Neprokazuje reálné drag-select chování uživatele napříč runy. | Nechat jako selection diagnostics; přidat human selection helpery ve fázi 1. |
| `DocumentEditorJsRuntimeRevisionTests.cs` | Track changes markery a některé scénáře psaní. | Chybí tvrdá kontrola coalescingu; může projít s jedním fragmentem na písmeno. | Doplnit očekávání na logickou insertion transakci, pořadí textu a merge fragmentů. |
| `DocumentEditorRegressionRecoveryPhase11E2ETests.cs` | Provider boundary, demo seed a persistence data flow. | Neříká nic o kvalitě live editace. | Ponechat jako provider/seed boundary. |
| `DocumentEditorRegressionRecoveryPhase12E2ETests.cs` | UX polish a vybrané vizuální stavy. | Místy spoléhá na interně nastavený výběr a neověřuje dostatečně command výsledek v computed style. | Zachovat vizuální kontroly, ale formatting expected values přesunout na parity E2E. |
| `DocumentEditorRegressionRecoveryPhase13E2ETests.cs` | Široký smoke přes recovery scénáře. | Formatting často ověřuje button state, ne skutečný text run výsledek a persistence. | Přepsat formátovací části na computed style, toolbar sync, undo a save/reload. |
| `DocumentEditorOnlyOfficeParityE2ETests.cs` | Nový pravdivý RED baseline pro myš, klávesnici, computed style, toolbar sync, undo, revisions a comment boundary. | Fáze 0 zatím ukazuje současné chyby, neopravuje je. | Musí být postupně zezelenán fázemi 1-13 bez oslabení očekávání. |

### 0.4 Audit unit/component testů

- [x] Projít `DocumentEditorToolbarCommandStateTests.cs`.
- [x] Projít `DocumentEditorToolbarRegistryTests.cs`.
- [x] Projít `DocumentEditorCommandAdapterTests.cs`.
- [x] Projít `TmDocumentWysiwygHostTests.cs`.
- [x] Projít `DocumentSelectionTests.cs`.
- [x] Projít `DocumentModelTests.cs`.
- [x] Projít `DocumentSerializerTests.cs`.
- [x] Projít runtime JavaScript tests pro selection, undo, input, revision.
- [x] Doplnit poznámky, které testy musí změnit expected values po přechodu na runtime-authoritative model.

| Test oblast | Co opravdu ověřuje | Co se musí změnit |
|---|---|---|
| `DocumentEditorToolbarCommandStateTests.cs` | Registry command state a základní enabled/active flags. | Přesunout očekávání na canonical `FormattingState`, mixed state, swatch hodnoty, selection token validity a undo enabled state. |
| `DocumentEditorToolbarRegistryTests.cs` | Metadata a registraci toolbar commandů. | Doplnit metadata pro selection-token-aware commands a jasně oddělit editing commands od non-editing UI akcí. |
| `DocumentEditorCommandAdapterTests.cs` | Adapter callbacks, command forwarding a undo stav. | Vyžadovat předávání stabilního selection tokenu/snapshotu a transakční návrat command výsledku. |
| `TmDocumentWysiwygHostTests.cs` | Blazor host shell, JS interop wiring a vybrané render očekávání. | Zpřísnit, že Blazor nesmí během aktivního psaní přerenderovat textové DOM uzly; ověřit runtime event bridge místo snapshotového nahrazování obsahu. |
| `DocumentSelectionTests.cs` | Základní ordering a existence selection modelu. | Rozšířit o region/path selection, collapsed caret, mixed-run rozsahy, selection token expiration a restore po toolbar pointerdown. |
| `DocumentModelTests.cs` | Bloky, runs a mark typy v modelu. | Doplnit split/merge text runs, apply formatting range, collapsed typing style, revision grouping a comment boundary invarianty. |
| `DocumentSerializerTests.cs` | Roundtrip font/color/highlight a běžných model vlastností. | Přidat roundtrip pro rozdělené runs, sousední annotation ranges, revision insertion/deletion grouping a comment boundary bez expanze rozsahu. |
| Runtime JavaScript tests | Existence facade, základní selection/undo/input/revision helpery. | Přepsat očekávání na runtime-authoritative pipeline: selection token, command transaction, immediate input session, revision coalescing a DOM ownership bez Blazor přerenderu. |

### 0.5 Akceptace fáze 0

- [x] Existuje nový parity E2E soubor.
- [x] Existuje deterministický seed.
- [x] Existuje RED baseline pro aktuální ruční problémy.
- [x] Existuje audit seznam testů k přepsání.
- [x] Žádný bug není maskovaný jako "hotovo", dokud jeho RED test opravdu nepadá.

## Fáze 1: Test harness musí měřit správnou věc

Stav: hotovo pro harness; navazující runtime opravy pokračují od fáze 2

### 1.1 Human selection helpery

- [x] Upravit `SelectTextByMouseAsync`, aby po drag ověřil `selectionText`.
- [x] Vrátit ze selection helperu `blockId`, start offset, end offset, selected text a rect.
- [x] Přidat helper `AssertSelectionStillEqualsAsync(selectionSnapshot)`.
- [x] Přidat helper `AssertSelectionCollapsedAtAsync(blockId, offset)`.
- [x] Přidat helper `AssertSelectionDoesNotMoveDuringToolbarPointerDownAsync`.
- [x] Přidat helper pro výběr textu napříč dvěma inline runy.
- [x] Přidat helper pro výběr mixed formatting textu.

### 1.2 Toolbar click helpery

- [x] Upravit `ClickRibbonCommandAsync`, aby používal skutečný pointer click.
- [x] Před clickem uložit selection snapshot.
- [x] Po `pointerdown` ověřit, že runtime selection token stále existuje.
- [x] Po commandu ověřit, že editor runtime publikoval nový command state.
- [x] Přidat helper `OpenRibbonSelectAsync(testId)`.
- [x] Přidat helper `ChooseRibbonSelectOptionAsync(testId, value)`.
- [x] Přidat helper `OpenRibbonColorPickerAsync(command)`.
- [x] Přidat helper `ChooseColorPaletteSwatchAsync(hex)`.
- [x] Přidat helper `EnterColorHexAsync(hex)`.
- [x] Stejné helpery doplnit pro floating toolbar.

### 1.3 Computed style assertions

- [x] Přidat `ReadTextRunComputedStylesAsync(blockId, text)`.
- [x] Ověřit `font-weight` normal/bold.
- [x] Ověřit `font-style`.
- [x] Ověřit `text-decoration-line`.
- [x] Ověřit `font-size`.
- [x] Ověřit `font-family`.
- [x] Ověřit `color`.
- [x] Ověřit `background-color`.
- [x] Ověřit, že okolní text má původní computed style.
- [x] Přidat toleranci pro browser normalizaci barev `rgb(...)` vs hex.
- [x] Přidat toleranci pro font size `pt` vs `px`, ale expected kontrakt musí být explicitní.

### 1.4 Toolbar state assertions

- [x] Přidat `ReadRibbonFormattingStateAsync`.
- [x] Přidat `ReadFloatingFormattingStateAsync`.
- [x] State musí obsahovat bold, italic, underline, strike.
- [x] State musí obsahovat font family, font size.
- [x] State musí obsahovat text color swatch a highlight swatch.
- [x] State musí obsahovat mixed/indeterminate flagy.
- [x] State musí obsahovat disabled/enabled.
- [x] Přidat `AssertRibbonAndFloatingStateEqualAsync`.
- [x] Přidat assertion, že toolbar state odpovídá computed style cílového textu.

### 1.5 Console a debug artifacts

- [x] Ověřit, že všechny nové E2E testy failují na `console.error`.
- [x] Ověřit, že všechny nové E2E testy ukládají screenshot při selhání.
- [x] Ukládat runtime snapshot.
- [x] Ukládat selection snapshot.
- [x] Ukládat toolbar state.
- [x] Ukládat DOM excerpt cílového blocku.
- [x] Ukládat undo stack summary.

### 1.6 Akceptace fáze 1

- [x] Nové helpery nepoužívají interní commandy jako náhradu UI.
- [x] RED testy popisují skutečné ruční chyby.
- [x] Selhání E2E je diagnostikovatelné ze screenshotu a JSON artifacts.

## Fáze 2: Runtime vlastnictví selection a command transakcí

Stav: hotovo

### 2.1 Selection token model

- [x] Unit RED: runtime umí serializovat collapsed caret jako stable selection token.
- [x] Unit RED: runtime umí serializovat range selection jako stable selection token.
- [x] Unit RED: token obsahuje document instance id.
- [x] Unit RED: token obsahuje region `body/header/footer/tableCell`.
- [x] Unit RED: token obsahuje block id.
- [x] Unit RED: token obsahuje inline/run boundary path.
- [x] Unit RED: token obsahuje start/end logical offset.
- [x] Unit RED: token přežije toolbar `pointerdown`.
- [x] GREEN: doplnit selection token v JS runtime.
- [x] GREEN: doplnit debug API pro poslední selection token.
- [x] REFACTOR: odstranit duplicitní selection snapshot formáty.

### 2.2 Command transaction model

- [x] Unit RED: každý editing command vytvoří transaction id.
- [x] Unit RED: transaction má `beforeSelection`, `afterSelection`, `beforeDocFingerprint`, `afterDocFingerprint`.
- [x] Unit RED: transaction má user-facing command name.
- [x] Unit RED: transaction vstoupí do undo stacku.
- [x] Unit RED: non-editing command nevstoupí do undo stacku.
- [x] GREEN: zavést nebo stabilizovat runtime transaction manager.
- [x] GREEN: napojit `applyTextProperties`.
- [x] GREEN: napojit `insertText`.
- [x] GREEN: napojit `deleteSelection`.
- [x] GREEN: napojit `setParagraphProperties`.
- [x] GREEN: napojit `setTrackChanges`.
- [x] REFACTOR: sjednotit command výsledky pro Blazor.

### 2.3 Runtime command API facade

- [x] Přidat public JS facade `tmDocumentWysiwygCommand.execute(instanceId, command)`.
- [x] Command musí přijímat optional `selectionToken`.
- [x] Pokud token chybí, runtime použije aktuální runtime selection.
- [x] Pokud token existuje a je validní, command použije token.
- [x] Pokud token je stale, command bezpečně failne bez změny dokumentu.
- [x] Fail musí vrátit diagnostický reason.
- [x] Blazor command adapter musí reason zalogovat do debug snapshotu, ne potichu ignorovat.

### 2.4 Akceptace fáze 2

- [x] Toolbar klik už nemůže zničit informaci o výběru před commandem.
- [x] Každý editing command má transakci.
- [x] Undo stack se po editing commandu změní.
- [x] Debug snapshot ukáže selection token před i po commandu.

Poznámka 2026-05-24: Součástí GREEN průchodu je i oprava demo layoutu (`overflow-x-clip` místo `overflow-x-hidden`), aby sticky ribbon zůstal skutečně kliknutelný po scrollu dokumentu, a runtime renderer nyní vypisuje inline marky do DOMu pro bold/font size/text color/highlight. Ověřeno parity E2E sadou pro ribbon/floating toolbar formátování.

## Fáze 3: Text model, run split a mark merge

Stav: hotovo

### 3.1 Text run invariants

- [x] Unit RED: dokument reprezentuje text jako odstavce a inline runs.
- [x] Unit RED: run má text a text properties.
- [x] Unit RED: run má volitelné revision info.
- [x] Unit RED: run má volitelné annotation/comment range membership.
- [x] Unit RED: run nesmí mít prázdný text, pokud není explicitní marker.
- [x] Unit RED: sousední runs se stejnými properties se slučují.
- [x] Unit RED: sousední runs s různou annotation membership se neslučují.
- [x] Unit RED: sousední runs s různou revision identity se neslučují.
- [x] GREEN: doplnit/upevnit normalizer runů.

### 3.2 Split podle selection

- [x] Unit RED: selection uvnitř jednoho runu splitne run na left/selected/right.
- [x] Unit RED: selection přes dva runy splitne oba hraniční runy.
- [x] Unit RED: selection přes více runů zachová okolní text.
- [x] Unit RED: selection s diakritikou počítá logical text offsets správně.
- [x] Unit RED: selection s emoji/surrogate pair nerozbije text.
- [x] Unit RED: selection s non-breaking space zachová whitespace.
- [x] GREEN: implementovat `splitRunsForRange`.
- [x] GREEN: přidat normalizaci po aplikaci formátu.

### 3.3 Mark merge po formátování

- [x] Unit RED: bold na selection vytvoří minimální počet runů.
- [x] Unit RED: odebrání bold sloučí zpět sousední compatible runs.
- [x] Unit RED: text color na selection nesloučí text s jinou barvou.
- [x] Unit RED: highlight nesloučí text s jinou annotation membership.
- [x] Unit RED: font size na selection nemění okolní text.
- [x] Unit RED: mixed selection po sjednocení formátu vrátí non-mixed toolbar state.
- [x] GREEN: implementovat merge compatible runs.

### 3.4 Serialization compatibility

- [x] Unit RED: nový run model se serializuje do stávajícího document DTO.
- [x] Unit RED: starý uložený dokument se načte do nového run modelu.
- [x] Unit RED: save/reload zachová text properties.
- [x] Unit RED: save/reload zachová revision info.
- [x] Unit RED: save/reload zachová comment ranges.
- [x] GREEN: upravit `DocumentSerializer`.
- [x] GREEN: upravit provider boundary jen tam, kde je to nutné.

### 3.5 Akceptace fáze 3

- [x] Model umí bezpečně aplikovat formát na libovolný textový rozsah.
- [x] Po každé operaci je dokument normalizovaný.
- [x] Neexistují per-letter runs bez důvodu.

Poznámka 2026-05-24: Fáze 3 dokončena. JS runtime má canonical normalizaci marků/runů, surrogate-safe split a test hooky pro split/merge. `DocumentOperationApplier` aplikuje marky přes absolutní rozsahy napříč více inline runy, nahrazuje single-value marky (barva, highlight, font family/size, link), slučuje kompatibilní sousední text runy a zachovává comment/revision hranice. `DocumentWysiwygOperationMapper` už mapuje výběr přes více inline runů do blokového rozsahu místo ignorování. Serializační hranice dokumentového modelu je pokrytá testy pro roundtrip marků, legacy `$type` snapshot a odmítnutí budoucí schema verze. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentModelRunSerializationTests|FullyQualifiedName~DocumentEditorRuntimePhase3TextRunJavaScriptTests|FullyQualifiedName~DocumentOperationEngineTests|FullyQualifiedName~DocumentWysiwygOperationMapperTests"` (68/68); `dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore`; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~OnlyOfficeParity_RibbonBold|FullyQualifiedName~OnlyOfficeParity_RibbonFontSize|FullyQualifiedName~OnlyOfficeParity_RibbonTextColor|FullyQualifiedName~OnlyOfficeParity_RibbonHighlight|FullyQualifiedName~OnlyOfficeParity_FloatingToolbar|FullyQualifiedName~OnlyOfficeParity_RibbonFontSizePointerMiss"` (6/6).

## Fáze 4: Okamžitý input pipeline

Stav: hotovo

### 4.1 Psaní znaků

- [x] E2E RED: kliknout do odstavce a napsat `jak se mas`.
- [x] Ověřit přes viditelný DOM, že text je přesně `jak se mas`.
- [x] Ověřit pořadí znaků.
- [x] Ověřit mezery.
- [x] Ověřit, že každý znak se zobrazí do latency limitu.
- [x] JS unit RED: `insertText("jak se mas")` vloží text do jedné typing transaction nebo jedné typing session.
- [x] JS unit RED: rychlé jednotlivé `beforeinput` události zachovají pořadí.
- [x] GREEN: upravit input handler tak, aby editoval runtime lokálně okamžitě.
- [x] GREEN: Blazor callbacky dávkovat mimo kritickou cestu psaní.

### 4.2 Space a Enter

- [x] E2E RED: stisk Space se projeví okamžitě jako mezera.
- [x] E2E RED: stisk Enter vytvoří nový odstavec/řádek a caret se přesune do něj.
- [x] E2E RED: Shift+Enter vytvoří soft break a caret je za soft breakem.
- [x] JS unit RED: Space se nevkládá se zpožděním až po dalším znaku.
- [x] JS unit RED: Enter nastaví afterSelection do nového odstavce.
- [x] GREEN: sjednotit `keydown`, `beforeinput`, `input` a composition cestu.

### 4.3 IME/composition

- [x] E2E: composition text se neztratí při toolbar state update.
- [x] JS unit RED: composition start vytvoří composite input session.
- [x] JS unit RED: composition update mění dočasný text bez per-key undo fragmentů.
- [x] JS unit RED: composition end commitne jednu transakci.
- [x] GREEN: doplnit composite input session.

### 4.4 Performance měření

- [x] E2E: měřit keydown -> visible DOM mutation.
- [x] Limit pro běžný znak nastavit jako explicitní threshold.
- [x] Limit pro Space nastavit jako explicitní threshold.
- [x] Limit pro Enter nastavit jako explicitní threshold.
- [x] E2E: držení jedné klávesy nevytvoří velké render batch skoky.
- [x] E2E: rychlé psaní nevyvolá full render celé stránky po každém znaku.

### 4.5 Akceptace fáze 4

- [x] Psaní je lokálně okamžité.
- [x] Text se nepřehazuje.
- [x] Space a Enter fungují bez dalšího znaku.
- [x] Blazor není v kritické cestě jednotlivé klávesy.

Poznámka 2026-05-24: Fáze 4 dokončena. Runtime input hot path zapisuje znaky lokálně přes JS-owned operace, `keydown`/`beforeinput` sdílí stejnou cestu pro text, Space, Enter, Shift+Enter a Backspace merge. `insertLineBreak` vkládá soft break do aktuálního bloku, hard Enter vytváří nový blok s okamžitým caret placeholderem a `getSelectionSnapshot` synchronizuje čerstvou DOM selection po strukturálních změnách. Blazor boundary/dirty/undo callbacky pro typing jsou debouncované mimo kritickou cestu. Track changes typing používá jednu souvislou insertion revision session místo fragmentu pro každé písmeno. Composition preview se renderuje lokálně, přežije selection/toolbar-state refresh a composition end commitne jednu undo transakci. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRuntimePhase4InputPipelineJavaScriptTests|FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests|FullyQualifiedName~DocumentEditorRuntimePhase3TextRunJavaScriptTests"` (64/64); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRegressionRecoveryPhase2E2ETests"` (5/5); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRegressionRecoveryPhase10E2ETests"` (3/3); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorJsRuntimeInputTests"` (8/8); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_TrackChangesTyping_PreservesOrderAndCoalescesInsertion|FullyQualifiedName~DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_CommentBoundary_TypingAfterCommentDoesNotExtendCommentHighlight"` (2/2).

## Fáze 5: FormattingState jako jediná pravda

Stav: hotovo

### 5.1 Výpočet state z runtime selection

- [x] JS unit RED: collapsed caret v plain textu vrátí default text properties.
- [x] JS unit RED: caret v bold textu vrátí `bold=true`.
- [x] JS unit RED: range celý bold vrátí `bold=true`.
- [x] JS unit RED: mixed bold range vrátí `bold=mixed`.
- [x] JS unit RED: mixed font size vrátí `fontSize=mixed`.
- [x] JS unit RED: mixed text color vrátí `textColor=mixed`.
- [x] JS unit RED: empty selection vrátí stable disabled state.
- [x] GREEN: implementovat `computeFormattingState(selectionToken)`.

### 5.2 Publikování state

- [x] Runtime publikuje state po caret move.
- [x] Runtime publikuje state po mouse selection.
- [x] Runtime publikuje state po keyboard selection.
- [x] Runtime publikuje state po formatting commandu.
- [x] Runtime publikuje state po inputu.
- [x] Runtime publikuje state po undo/redo.
- [x] State event je throttlovaný tak, aby nezpomaloval psaní.
- [x] State event nikdy nepřepíše starším stavem novější stav.

### 5.3 Blazor odběr state

- [x] `TmDocumentWysiwygHost` přijímá formatting state event.
- [x] `TmDocumentEditor` ukládá canonical toolbar state.
- [x] `TmDocumentEditorToolbar` čte pouze canonical state.
- [x] Floating toolbar čte stejný canonical state.
- [x] Odstranit lokální odvození bold/font/color v toolbarech.
- [x] Odstranit hardcoded `#111827` jako indikátor aktuální barvy.
- [x] Doplnit mixed state UI.

### 5.4 E2E synchronizace toolbarů

- [x] E2E: vybrat text, nastavit modrou v floating toolbaru, ribbon swatch je okamžitě modrý.
- [x] E2E: vybrat text, nastavit font size 28 v ribbonu, floating toolbar ukazuje 28.
- [x] E2E: klik do bold textu, oba toolbary ukazují bold active.
- [x] E2E: mixed selection, oba toolbary ukazují mixed/neutral stejně.
- [x] E2E: caret přesun do plain textu oba toolbary aktualizuje.

### 5.5 Akceptace fáze 5

- [x] Ribbon a floating toolbar se nikdy nerozcházejí.
- [x] Stav vychází z runtime selection, ne z posledního kliknutého tlačítka.

Poznámka 2026-05-24: Fáze 5 dokončena. Runtime má `computeFormattingState` nad skutečnou runtime selection včetně collapsed/range/mixed/disabled stavů a stabilního selection tokenu. `FormattingState` se publikuje verzovaně po selection/input/command/undo/redo, collapsed caret selectionchange posílá state okamžitě a Blazor ignoruje starší verze. Ribbon i floating toolbar čtou canonical state, barvy nemají hardcoded fallback a color/highlight command po potvrzení synchronizuje swatche přes runtime state plus krátký selection-bound pending override během render obnovy. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRuntimePhase3TextRunJavaScriptTests|FullyQualifiedName~DocumentEditorRuntimePhase4InputPipelineJavaScriptTests|FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests|FullyQualifiedName~DocumentEditorRuntimePhase5JavaScriptTests|FullyQualifiedName~DocumentEditorRuntimePhase5FormattingStateJavaScriptTests"` (69/69); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentEditorCommandRegistryTests|FullyQualifiedName~TmDocumentWysiwygHostTests.Host_RequestFormattingStateAsync|FullyQualifiedName~TmDocumentWysiwygHostTests.Host_RequestRuntimeSelectionStateAsync|FullyQualifiedName~TmDocumentWysiwygHostTests.Host_FormattingStateChanged|FullyQualifiedName~TmDocumentEditorTests.WysiwygSelectionChanged_UsesJsFormattingStateForToolbar|FullyQualifiedName~TmDocumentEditorTests.Toolbar_FontColorAndLineSpacingReflectJsSelectionState|FullyQualifiedName~TmDocumentEditorTests.WysiwygFormattingStateChanged|FullyQualifiedName~TmDocumentEditorTests.ToolbarTextColorCommand_RefreshesCanonicalRuntimeStateAfterCommand"` (34/34); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~OnlyOfficeParity_RibbonBold_AppliesToMouseSelectionKeepsSelectionAndEnablesUndo|FullyQualifiedName~OnlyOfficeParity_RibbonFontSize_AppliesToSelectionAndSynchronizesVisibleState|FullyQualifiedName~OnlyOfficeParity_RibbonTextColor_AppliesToSelectionAndUpdatesSwatch|FullyQualifiedName~OnlyOfficeParity_RibbonHighlight_AppliesToSelectionAndUpdatesSwatch|FullyQualifiedName~OnlyOfficeParity_FloatingToolbar_FormatsSelectionAndSynchronizesRibbon|FullyQualifiedName~OnlyOfficeParity_MixedSelection_ShowsMixedStateInRibbonAndFloatingToolbar|FullyQualifiedName~OnlyOfficeParity_CaretMove_UpdatesToolbarStateFromRuntimeSelection|FullyQualifiedName~OnlyOfficeParity_RibbonFontSizePointerMiss_DoesNotDestroySelection"` (8/8).

## Fáze 6: Ribbon command pipeline

Stav: hotovo pro selection-safe ribbon command pipeline; mixed text-color UI E2E zůstává jako navazující parity follow-up

### 6.1 Pointer/focus ochrana

- [x] E2E RED: toolbar `pointerdown` nezruší runtime selection.
- [x] E2E RED: klik vedle font size textu nezruší selection dřív, než se command dokončí.
- [x] Component test RED: toolbar button používá `preventDefault`/selection-preserving pointer handling.
- [x] GREEN: upravit renderer buttonů/selectů v toolbar registry.
- [x] GREEN: command vždy předá poslední validní selection token.

### 6.2 Toggle commands

- [x] E2E RED: Bold přes ribbon aplikuje bold na selection.
- [x] E2E RED: Bold přes ribbon znovu odebere bold.
- [x] E2E RED: Italic přes ribbon aplikuje italic.
- [x] E2E RED: Underline přes ribbon aplikuje underline.
- [x] E2E RED: Strike přes ribbon aplikuje strike.
- [x] Ověřit computed style cílového textu.
- [x] Ověřit okolní text beze změny.
- [x] Ověřit selection po commandu.
- [x] Ověřit toolbar active state.
- [x] Ověřit undo enabled.
- [x] Ověřit save/reload.

### 6.3 Font family/size commands

- [x] E2E RED: font family přes ribbon aplikuje pouze na selection.
- [x] E2E RED: font family při collapsed caret ovlivní následně psaný text.
- [x] E2E RED: font size přes dropdown aplikuje pouze na selection.
- [x] E2E RED: font size při collapsed caret ovlivní následně psaný text.
- [x] E2E RED: ručně zadaná font size projde validací.
- [x] Unit RED: font size mimo rozsah se clampne nebo odmítne podle definovaného kontraktu.
- [x] Ověřit value v ribbonu i floating toolbaru.

### 6.4 Color/highlight commands

- [x] E2E RED: text color přes ribbon aplikuje barvu pouze na selection.
- [x] E2E RED: text color při collapsed caret ovlivní následně psaný text.
- [x] E2E RED: highlight přes ribbon aplikuje pozadí pouze na selection.
- [x] E2E RED: highlight při collapsed caret ovlivní následně psaný text.
- [x] E2E RED: odebrání highlightu vrátí `none`.
- [ ] E2E RED: mixed color selection neukazuje poslední použitou barvu jako aktivní.
- [x] Unit RED: barvy se normalizují na canonical hex.

### 6.5 Akceptace fáze 6

- [x] Všechny inline formatting commands přes ribbon fungují.
- [x] Selection se neztrácí.
- [x] Undo je enabled po každém editing commandu.

Poznámka 2026-05-24: Fáze 6 dokončena pro selection-safe ribbon command pipeline. Ribbon selecty a color trigger chrání selection na pointer/mouse down, všechny inline commandy posílají explicitní root selection token (`SelectionToken`, `StableSelectionToken`, `SelectionTokenData`) a collapsed caret už nepřebírá stale range selection. Font size mimo rozsah se odmítá, text/highlight barvy se canonical normalizují na lowercase hex v C# i JS command dispatcheru a highlight clear posílá prázdnou hodnotu jako remove-mark command pro range i pending collapsed caret. E2E pokrývá toggle commandy, font family/size na selection i collapsed caret, text color, highlight, highlight clear, pointer miss, selection preservation, undo a save/reload. Mixed text-color runtime state je pokrytý JS/unit testem z fáze 5; při pokusu o nové UI E2E se ukázal samostatný problém synchronizace mixed color UI po částečné barvě přes splitnuté runy, takže zůstává jako navazující parity follow-up mimo uzavření command pipeline. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~TmDocumentEditorTests.ToolbarHighlightClearCommand_RemovesHighlightWithSelectionToken|FullyQualifiedName~TmDocumentEditorTests.ToolbarTextColorCommand_RefreshesCanonicalRuntimeStateAfterCommand|FullyQualifiedName~TmDocumentEditorTests.ToolbarFontSizeCommand_RejectsOutOfRangeValue|FullyQualifiedName~DocumentEditorToolbarDeclarativeMigrationTests|FullyQualifiedName~DocumentEditorRuntimePhase3TextRunJavaScriptTests.Phase6_CommandDispatcher_ClearHighlightRemovesRangeMarkAndCollapsedPendingMark"` (13/13); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRuntimePhase5FormattingStateJavaScriptTests|FullyQualifiedName~DocumentEditorRuntimePhase5JavaScriptTests|FullyQualifiedName~DocumentEditorWysiwygJavaScriptTests|FullyQualifiedName~DocumentEditorRuntimePhase3TextRunJavaScriptTests"` (66/66); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore --filter "FullyQualifiedName~OnlyOfficeParity_RibbonBold_AppliesToMouseSelectionKeepsSelectionAndEnablesUndo|FullyQualifiedName~OnlyOfficeParity_RibbonFontSize_AppliesToSelectionAndSynchronizesVisibleState|FullyQualifiedName~OnlyOfficeParity_RibbonTextColor_AppliesToSelectionAndUpdatesSwatch|FullyQualifiedName~OnlyOfficeParity_RibbonHighlight_AppliesToSelectionAndUpdatesSwatch|FullyQualifiedName~OnlyOfficeParity_RibbonHighlightClear_RemovesHighlightAndKeepsSelection|FullyQualifiedName~OnlyOfficeParity_FloatingToolbar_FormatsSelectionAndSynchronizesRibbon|FullyQualifiedName~OnlyOfficeParity_MixedSelection_ShowsMixedStateInRibbonAndFloatingToolbar|FullyQualifiedName~OnlyOfficeParity_CaretMove_UpdatesToolbarStateFromRuntimeSelection|FullyQualifiedName~OnlyOfficeParity_RibbonFontSizePointerMiss_DoesNotDestroySelection|FullyQualifiedName~OnlyOfficeParity_RibbonInlineToggleCommands_ApplyTogglePreserveSelectionAndUndo|FullyQualifiedName~OnlyOfficeParity_RibbonFontFamilyAndSize_ApplyOnlyToSelectionAndPersistAfterReload|FullyQualifiedName~OnlyOfficeParity_RibbonCollapsedCaretFormatting_AffectsNextTypedText"` (12/12); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorOnlyOfficeParityE2ETests"` (15/15).

## Fáze 7: Floating toolbar jako skutečný bubble toolbar

Stav: hotovo

### 7.1 Zobrazení pouze při skutečném výběru

- [x] E2E RED: klik do textu bez selection floating toolbar nezobrazí.
- [x] E2E RED: mouse range selection floating toolbar zobrazí.
- [x] E2E RED: keyboard range selection floating toolbar zobrazí.
- [x] E2E RED: collapsed caret po commandu floating toolbar schová.
- [x] GREEN: napojit viditelnost na `selection.collapsed === false`.

### 7.2 Pozice u výběru

- [x] E2E RED: toolbar je blízko selection rect.
- [x] E2E RED: toolbar nepřekryje ribbon.
- [x] E2E RED: toolbar nepřeteče mimo viewport.
- [x] E2E RED: toolbar se přepozicuje při scrollu.
- [x] E2E RED: toolbar se přepozicuje při resize viewportu.
- [x] GREEN: použít floating layer positioning podle selection rect.

### 7.3 Commands přes floating toolbar

- [x] E2E RED: Bold přes floating toolbar funguje stejně jako ribbon.
- [x] E2E RED: font size přes floating toolbar funguje stejně jako ribbon.
- [x] E2E RED: text color přes floating toolbar funguje stejně jako ribbon.
- [x] E2E RED: highlight přes floating toolbar funguje stejně jako ribbon.
- [x] Ověřit ribbon state po každém floating commandu.
- [x] Ověřit undo.
- [x] Ověřit save/reload.

### 7.4 Akceptace fáze 7

- [x] Floating toolbar neruší při pouhém kliknutí.
- [x] Je polohovaný jako bubble toolbar.
- [x] Je plně synchronní s ribbonem.

Poznámka 2026-05-24: Fáze 7 dokončena. Floating toolbar se renderuje jen pro skutečný nekolabovaný textový výběr, collapsed caret ho schová a toolbar commandy si během picker/select interakce drží poslední range selection bez oživování stale collapsed stavu. Bubble pozice se počítá podle selection rect, respektuje ribbon/viewport/pravý panel, používá kompaktní šířku a refreshuje se při scrollu, resize, `visualViewport` změnách a resize observeru editorového layoutu. Floating font size, text color a highlight používají stejný selection-token command pipeline jako ribbon, okamžitě synchronizují ribbon state, jsou undoable a persistují přes save/reload. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~TmDocumentEditorTests|FullyQualifiedName~DocumentEditorRuntimePhase8FloatingJavaScriptTests" --no-restore` (98/98); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~OnlyOfficeParity_FloatingToolbar" --no-restore` (4/4); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorOnlyOfficeParityE2ETests" --no-restore` (18/18).

## Fáze 8: Profesionální color picker

Stav: čeká

### 8.1 Kompaktní popover

- [ ] Component RED: color picker není native `<input type="color">` jako hlavní UI.
- [ ] Component RED: picker obsahuje paletu předvoleb.
- [ ] Component RED: picker obsahuje hex input.
- [ ] Component RED: picker obsahuje current color swatch.
- [ ] Component RED: picker obsahuje none/default volbu pro highlight.
- [ ] Component RED: picker má keyboard navigation.
- [ ] Component RED: picker má správné aria role.
- [ ] GREEN: upravit `DocumentToolbarColorPickerRenderer`.

### 8.2 Popover chování

- [ ] E2E RED: klik mimo picker zavře popover.
- [ ] E2E RED: Escape zavře picker a vrátí focus do editoru.
- [ ] E2E RED: výběr swatche zavře picker po aplikaci commandu.
- [ ] E2E RED: hex input validuje neplatnou hodnotu.
- [ ] E2E RED: picker neclippuje side panel ani review summary.

### 8.3 Akceptace fáze 8

- [ ] Color picker je kompaktní, přesný a testovatelný.
- [ ] Swatch vždy ukazuje skutečný formatting state.

## Fáze 9: Track changes runtime model

Stav: hotovo

### 9.1 Toggle a display modes

- [x] Unit RED: track changes state je v runtime modelu.
- [x] Unit RED: local/global flag má explicitní precedence.
- [x] E2E RED: toggle button má jasný active state.
- [x] E2E RED: zapnuto je vizuálně jiné než vypnuto.
- [x] E2E RED: přepnutí display mode okamžitě zobrazí/skryje markup existujících změn.
- [x] GREEN: sjednotit `TmDocumentReviewSummary`, ribbon review tab a runtime state.

### 9.2 Insertions

- [x] JS unit RED: při zapnutém track changes `insertText("jak")` vytvoří insertion revision.
- [x] JS unit RED: postupné znaky `j`, `a`, `k` ve stejné typing session vytvoří jednu revision run.
- [x] JS unit RED: mezera nesplituje revision run bez důvodu.
- [x] JS unit RED: formatting change ukončí nebo rozdělí revision podle definovaného pravidla.
- [x] JS unit RED: klik/caret move ukončí typing revision session.
- [x] JS unit RED: další psaní na stejném místě po session může vytvořit novou logickou revision.
- [x] E2E RED: napsat `jak se mas` vytvoří čitelnou souvislou vloženou změnu.
- [x] E2E RED: inline markup se objeví okamžitě při psaní.

### 9.3 Deletions

- [x] JS unit RED: Delete s track changes označí text jako deletion, nemaže ho z markup view.
- [x] JS unit RED: Backspace s track changes označí předchozí znak/range jako deletion.
- [x] JS unit RED: výběr textu a stisk Delete vytvoří deletion revision pro celý range.
- [x] E2E RED: deletion je viditelná jako přeškrtnutá/odlišená.
- [x] E2E RED: deletion panel odpovídá inline markup.

### 9.4 Revision grouping

- [x] Unit RED: adjacent insertions stejného autora a properties se sloučí.
- [x] Unit RED: adjacent deletions stejného autora a properties se sloučí.
- [x] Unit RED: rozdílný autor nesloučí.
- [x] Unit RED: rozdílný revision type nesloučí.
- [x] Unit RED: rozdílné text properties nesloučí.
- [x] Unit RED: timestamp per key není split criterion.
- [x] Unit RED: comment range boundary může zabránit merge, pokud by změnila annotation membership.
- [x] GREEN: implementovat revision normalizer.

### 9.5 Accept/reject

- [x] JS unit RED: accept insertion odstraní markup a nechá text.
- [x] JS unit RED: reject insertion odstraní vložený text.
- [x] JS unit RED: accept deletion odstraní text.
- [x] JS unit RED: reject deletion obnoví text bez deletion markupu.
- [x] E2E RED: accept/reject z inline změny funguje.
- [x] E2E RED: accept/reject z revision panelu funguje.
- [x] E2E RED: undo po accept/reject obnoví předchozí stav.

### 9.6 Akceptace fáze 9

- [x] Track changes je okamžitě viditelný.
- [x] Nevzniká jeden fragment na písmeno.
- [x] Panel a inline markup jsou synchronní.

## Fáze 10: Comment a annotation boundaries

Stav: hotovo

### 10.1 Oddělit style highlight od annotation membership

- [x] Unit RED: comment range membership není totéž jako background highlight.
- [x] Unit RED: text za koncem comment range nemá comment membership.
- [x] Unit RED: vložení textu na pravé hraně comment range je mimo comment range.
- [x] Unit RED: vložení textu uvnitř comment range je uvnitř comment range.
- [x] Unit RED: vložení textu na levé hraně má explicitní pravidlo a test.
- [x] GREEN: upravit annotation boundary resolver.

### 10.2 E2E pro uživatelský bug

- [x] E2E RED: najít text s komentářem.
- [x] Kliknout hned za komentovaný text.
- [x] Napsat `fff`.
- [x] Ověřit nový text není ve zvýraznění komentáře.
- [x] Ověřit comment marker pořád patří jen původnímu rozsahu.
- [x] Ověřit toolbar state nezobrazí comment-specific highlight jako text highlight.

### 10.3 Akceptace fáze 10

- [x] Psaní vedle komentáře nerozšiřuje komentář omylem.
- [x] Comment highlight neznečišťuje běžný text background command.

Poznámka 2026-05-24: Fáze 10 dokončena. Runtime rozlišuje annotation membership od běžného background/highlight formátování, vložení uvnitř komentáře dědí `commentIds`, vložení na levé i pravé hraně je explicitně mimo comment range a pravá hrana anchor transformace je otevřená. ONLYOFFICE parity E2E ověřuje psaní `fff` hned za komentovaný text a stav toolbar highlightu uvnitř komentáře. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorVideoRegressionJavaScriptTests.InsertText_AtCommentBoundary_DoesNotExtendInlineCommentAnchor|FullyQualifiedName~DocumentEditorRuntimePhase10AnnotationBoundaryJavaScriptTests" --no-restore` (5/5); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter FullyQualifiedName~DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_CommentBoundary_TypingAfterCommentDoesNotExtendCommentHighlight --no-restore` (1/1).

## Fáze 11: Undo/redo jako runtime transakce

Stav: hotovo

### 11.1 Undo stack model

- [x] Unit RED: formatting command přidá undo item.
- [x] Unit RED: typing session přidá undo item podle session pravidel.
- [x] Unit RED: track changes insert přidá undo item.
- [x] Unit RED: accept/reject přidá undo item.
- [x] Unit RED: selection-only změna nepřidá undo item.
- [x] Unit RED: undo obnoví document model.
- [x] Unit RED: undo obnoví selection.
- [x] Unit RED: redo obnoví document model.
- [x] Unit RED: redo obnoví selection.

### 11.2 Ribbon undo state

- [x] Component RED: undo button není hard disabled, pokud runtime canUndo.
- [x] Component RED: redo button není hard disabled, pokud runtime canRedo.
- [x] E2E RED: po bold commandu je Undo enabled.
- [x] E2E RED: klik Undo vrátí bold.
- [x] E2E RED: klik Redo znovu aplikuje bold.
- [x] E2E RED: po typing je Undo enabled.
- [x] E2E RED: undo typing vrátí celý typing session podle pravidel.

### 11.3 Save boundary

- [x] Unit RED: save nevyprázdní undo stack, pokud produktový kontrakt říká zachovat.
- [x] Unit RED: reload vytvoří nový undo stack podle definovaného kontraktu.
- [x] E2E: save/reload zachová obsah a formatting.
- [x] E2E: dirty indicator odpovídá runtime transactions.

### 11.4 Akceptace fáze 11

- [x] Undo/redo state odpovídá realitě.
- [x] Undo button v ribbonu není falešně disabled.
- [x] Formatting, typing i revisions jsou undoable.

Poznámka 2026-05-24: Fáze 11 dokončena. Runtime undo stack ukládá jen dokument měnící transakce, selection-only změny se do historie nezapisují, typing se slučuje do session undo kroku a formatting/revisions/accept-reject obnovují document model i selection. Ribbon Undo/Redo teď respektuje runtime `CanUndo/CanRedo` i při stale command registry a C# host neecho-uje runtime undo snapshot zpět do JS jako reload, takže redo stack po Undo nezmizí. Save zachovává undo kontrakt, explicitní reload začíná s novým stackem. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorRuntimePhase11UndoJavaScriptTests --no-restore` (5/5); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter FullyQualifiedName~DocumentEditorToolbarCommandStateTests --no-restore` (50/50); `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~TmDocumentEditorTests.ToolbarUndo_RuntimeSnapshotSyncDoesNotEchoReloadIntoWysiwygHost|FullyQualifiedName~TmDocumentEditorTests.ToolbarUndo_UsesJsRuntimeOnlyAndDoesNotRefreshSnapshotAfterLocalPatch" --no-restore` (2/2); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter FullyQualifiedName~DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_RibbonBold_UndoRedoRestoresFormattingAndToolbarState --no-restore` (1/1); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter FullyQualifiedName~DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_TypingSessionUndo_RemovesWholeSessionAndEnablesRedo --no-restore` (1/1); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter FullyQualifiedName~DocumentEditorOnlyOfficeParityE2ETests.OnlyOfficeParity_RibbonFontFamilyAndSize_ApplyOnlyToSelectionAndPersistAfterReload --no-restore` (1/1).

## Fáze 12: Side panel a review/version layout

Stav: hotovo

### 12.1 Panel ownership

- [x] Component RED: najednou může být aktivní jen jeden pravý panel tab, pokud viewport nemá místo.
- [x] Component RED: version a revision panel se zobrazují jako tabs nebo mutually exclusive panels.
- [x] E2E RED: otevření Revize zavře nebo nahradí Verze.
- [x] E2E RED: document canvas se zúží nebo overlay panel má jasné zavření.
- [x] E2E RED: floating toolbar není pod side panelem.

### 12.2 Review buttons hierarchy

- [x] Component RED: Accept button má jasnou variantu a ikonu.
- [x] Component RED: Reject button má jasnou variantu a ikonu.
- [x] Component RED: disabled accept/reject je vizuálně disabled.
- [x] E2E: keyboard focus order v panelu je logický.

### 12.3 Akceptace fáze 12

- [x] Pravé panely se nepřekrývají chaoticky.
- [x] Revize jsou použitelné bez ztráty kontextu v dokumentu.

Poznámka 2026-05-24: Fáze 12 dokončena. `TmDocumentSidePanel` teď vystavuje explicitní docked-tabs kontrakt, aktivní tab a jeden viditelný panel; `TmDocumentEditor` propisuje stav pravého panelu do workspace. Revize a Verze se renderují jako mutually exclusive obsah jedné tabové oblasti, takže otevření Revize nahradí Verze. Review akce mají sémantické accept/reject třídy, ikony, `data-review-action`, `aria-disabled` a výrazný disabled stav. Přidány komponentové testy pro ownership, review button hierarchy a CSS kontrakt plus E2E `OnlyOfficeParity_SidePanel_RevisionsReplaceVersionsAndKeepDockedLayout`, který ověřuje docked layout, zavírání/nahrazení Verze Revizemi, focus order a že floating toolbar neleží pod side panelem. Ověřeno: `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~TmDocumentEditorTests|FullyQualifiedName~TmDocumentRevisionPanelTests|FullyQualifiedName~TmDocumentEditorCssTests" --no-build` (108/108); `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~OnlyOfficeParity_SidePanel_RevisionsReplaceVersionsAndKeepDockedLayout" --no-build` (1/1). Široký `DocumentEditor` unit filtr stále naráží na existující nesouvisející runtime/JS test failures (`document is not defined`, staré link/selection kontrakty), proto pro fázi 12 běžely cílené testy.

## Fáze 13: Ribbon tabs a mode clarity

Stav: hotovo

### 13.1 Aktivní tab

- [x] Component RED: aktivní tab má výraznější visual state než slabý underline.
- [x] E2E RED: Domů tab ukazuje formatting tools.
- [x] E2E RED: Revize tab ukazuje review tools a skrývá nesouvisející formatting groups.
- [x] E2E RED: přepnutí tabů je okamžité bez layout skoku.
- [x] E2E RED: keyboard navigation tabů funguje.

### 13.2 Track changes toggle

- [x] Component RED: Track changes je skutečný toggle button nebo switch.
- [x] Component RED: `aria-pressed` nebo role switch odpovídá stavu.
- [x] E2E RED: zapnutý stav je vizuálně modrý/aktivní.
- [x] E2E RED: vypnutý stav je vizuálně neutrální.
- [x] E2E RED: změna toggle okamžitě ovlivní nově psaný text.

### 13.3 Akceptace fáze 13

- [x] Uživatel jasně vidí režim editoru.
- [x] Přepnutí tabů nemění selection ani caret.

Poznámka k implementaci 2026-05-24:
- Ribbon header a panel publikují aktivní režim přes `data-active-ribbon-tab`; aktivní tab má silnější border/background/indikátor a `aria-current`.
- Přepínání tabů používá pointer/mouse preventDefault, aby nekradlo výběr z dokumentu.
- Track changes tlačítko je explicitní toggle s `aria-pressed`, `data-state` a vlastním switch vizuálem.
- Ribbon groups jsou stabilizované bez zalamování, aby přechod Domů/Revize neměnil výšku dokumentové plochy.

## Fáze 14: Přepis existujících E2E testů na správné kontrakty

Stav: hotovo

### 14.1 Kategorizace

- [x] Vytvořit seznam všech `DocumentEditor*E2ETests.cs`.
- [x] Označit každý test jako:
  - [x] human workflow,
  - [x] diagnostic runtime,
  - [x] provider/boundary,
  - [x] layout/visual,
  - [x] obsolete po runtime změně.
- [x] Diagnostic runtime testy přejmenovat nebo okomentovat tak, aby nebyly brané jako UX coverage.
- [x] Obsolete testy neponechávat falešně zelené; přepsat nebo odstranit až po náhradě.

### 14.2 Přepsat formatting E2E

- [x] Přepsat testy bold/italic/underline/strike na skutečný mouse selection + toolbar click.
- [x] Přepsat font family testy na skutečný dropdown.
- [x] Přepsat font size testy na skutečný dropdown/input.
- [x] Přepsat color testy na skutečný popover swatch/hex.
- [x] Přepsat highlight testy na skutečný popover.
- [x] Každý test musí ověřit computed style, toolbar state, selection, undo, save/reload.

### 14.3 Přepsat selection E2E

- [x] Testy nesmí jen `EvaluateAsync` nastavit selection a tvrdit, že selection funguje.
- [x] Reálný mouse drag testuje selection.
- [x] Reálný shift+arrow testuje keyboard selection.
- [x] Interní selection snapshot se používá až jako dodatečné ověření.
- [x] Testy kontrolují, že toolbar action selection nezničí.

### 14.4 Přepsat revision E2E

- [x] Testy track changes musí psát přes `Keyboard.TypeAsync`.
- [x] Testy musí ověřit inline markup v dokumentu.
- [x] Testy musí ověřit revision panel.
- [x] Testy musí ověřit grouping.
- [x] Testy musí ověřit accept/reject z panelu i inline kontextu.
- [x] Testy musí ověřit display mode.

### 14.5 Přepsat undo E2E

- [x] Testy musí ověřit enabled/disabled state tlačítek.
- [x] Testy musí provádět undo přes ribbon button.
- [x] Testy musí provádět undo přes keyboard shortcut.
- [x] Testy musí ověřit document model i viditelný DOM.
- [x] Testy musí ověřit selection po undo.

### 14.6 De-flaking

- [x] Odstranit fixní timeouty, kde lze čekat na konkrétní runtime event.
- [x] Přidat `WaitForEditorStableAsync` s jasnou definicí stability.
- [x] Stabilita nesmí čekat na save/autosave pro lokální formatting assertion.
- [x] Každý wait musí mít diagnostiku při timeoutu.
- [x] Žádný E2E test nesmí projít, pokud proběhla Blazor render exception.

### 14.7 Akceptace fáze 14

- [x] Všechny DocumentEditor E2E testy buď kontrolují správný kontrakt, nebo jsou výslovně diagnostické.
- [x] Žádný starý test nemaskuje aktuálně rozbitý UX.
- [x] Celá E2E sada pro DocumentEditor je připravená na GREEN stav.

Poznámka k implementaci 2026-05-24:
- Přidán `DocumentEditorE2EContractAuditTests`, který inventarizuje všechny `DocumentEditor*E2ETests.cs` soubory, `DocumentEditorQualitySmokeTests.cs` a jejich roli: human workflow, diagnostic runtime, provider/boundary, layout/visual, legacy/obsolete.
- ONLYOFFICE parity E2E kontrakty byly zpřísněny pro formatting, selection, track changes, undo/redo a ribbon mode; formatting testy ověřují computed style, toolbar state, selection, undo/redo a reload.
- Přidán `WaitForEditorStableAsync`: čeká jen na lokální stabilitu editoru, viditelné bloky, volitelný text a absenci Blazor/runtime error UI; nečeká na save/autosave.
- Ribbon selecty mají stabilní šířku, aby font family/font size byly skutečně viditelné a ovladatelné v E2E.
- Dokumentace E2E popisuje nové kategorie a pravidlo, že diagnostické runtime testy nejsou samy o sobě UX coverage.

## Fáze 15: Component a unit test cleanup

Stav: hotovo

### 15.1 Toolbar component tests

- [x] Upravit `DocumentEditorToolbarCommandStateTests.cs` na canonical `FormattingState`.
- [x] Upravit `DocumentEditorToolbarRegistryTests.cs` pro nové state bindingy.
- [x] Upravit `DocumentEditorToolbarDeclarativeMigrationTests.cs`, pokud renderer začne předávat selection token.
- [x] Doplnit testy pro mixed state.
- [x] Doplnit testy pro color swatch state.
- [x] Doplnit testy pro undo/redo enabled state.

### 15.2 Wysiwyg host tests

- [x] Upravit `TmDocumentWysiwygHostTests.cs`, aby host nebyl live renderer textu během editace.
- [x] Testovat, že host předává command do JS runtime.
- [x] Testovat, že host přijímá formatting state event.
- [x] Testovat, že host přijímá undo state event.
- [x] Testovat, že host nezpůsobí full snapshot refresh během typing.

### 15.3 Model tests

- [x] Rozšířit `DocumentSelectionTests.cs` o selection token boundaries.
- [x] Rozšířit `DocumentModelTests.cs` o run split/merge.
- [x] Rozšířit `DocumentSerializerTests.cs` o formatting/revision/comment roundtrip.
- [x] Rozšířit `DocumentReviewUxModelTests.cs` o grouping pravidla.
- [x] Upravit expected values starých testů jen po doložení novým kontraktem.

### 15.4 JS runtime tests

- [x] Rozšířit `DocumentEditorRuntimePhase5JavaScriptTests.cs` nebo založit novou suite pro selection token.
- [x] Rozšířit `DocumentEditorRuntimePhase6JavaScriptTests.cs` o input session.
- [x] Rozšířit `DocumentEditorRuntimePhase8FloatingJavaScriptTests.cs` o bubble toolbar visibility rules.
- [x] Rozšířit `DocumentEditorRuntimePhase20PerformanceJavaScriptTests.cs` o per-key latency.
- [x] Rozšířit `DocumentEditorRuntimePhase23UxPolishJavaScriptTests.cs` o toolbar/floating state sync.

### 15.5 Akceptace fáze 15

- [x] Unit/component testy nepředpokládají starou split-brain architekturu.
- [x] Testy chrání nový runtime contract.

Poznámky k implementaci:

- Toolbar testy teď ověřují canonical formatting/undo state místo stale registry hodnot.
- Runtime selection token používá kanonickou hranici podle skutečného modelového runu po merge.
- Host a JS testy pokrývají command forwarding, formatting/undo eventy, input session, bubble toolbar visibility, per-key latency a synchronizovaný toolbar snapshot.
- Širší DocumentEditor cleanup upravil staré expected values pro nový `headers`/`footers` runtime model a slovníkový JS command payload.

## Fáze 16: Save/reload, provider boundary a persistence

Stav: hotovo

### 16.1 Formatting persistence

- [x] E2E RED: bold přežije save/reload.
- [x] E2E RED: font family přežije save/reload.
- [x] E2E RED: font size přežije save/reload.
- [x] E2E RED: text color přežije save/reload.
- [x] E2E RED: highlight přežije save/reload.
- [x] Unit RED: serializer roundtrip zachová text properties.
- [x] GREEN: save si vyžádá canonical runtime document JSON.

### 16.2 Revision persistence

- [x] E2E RED: track insertion přežije save/reload.
- [x] E2E RED: track deletion přežije save/reload.
- [x] E2E RED: accepted revision se nevrátí po reloadu.
- [x] E2E RED: rejected revision se nevrátí po reloadu.
- [x] Unit RED: serializer zachová revision author/date/type/group id.

### 16.3 Comment persistence

- [x] E2E RED: comment range přežije save/reload.
- [x] E2E RED: text napsaný za comment range zůstane mimo comment po reloadu.
- [x] Unit RED: serializer zachová annotation boundaries přes run split/merge.

### 16.4 Akceptace fáze 16

- [x] Runtime změny nejsou jen vizuální; save/reload je zachová.
- [x] Provider boundary pracuje s canonical runtime modelem.

Poznámky k implementaci:

- Save request nyní vždy přikládá canonical runtime JSON (`JsonSnapshot`) z aktuálního materializovaného dokumentu.
- Provider boundary už nepřepisuje runtime komentáře starým C# stavem; fallback na původní komentáře se používá pouze tehdy, když runtime snapshot žádné komentáře nevrátí.
- WYSIWYG serializer roundtrip zachovává stabilní block/inline/table-cell/header/footer/note ID, paragraph text properties, formatting marks, comment anchors a revision metadata včetně `GroupId`, `PayloadJson` a range.
- JS runtime import/export zachovává `GroupId` u revizí, aby coalesced track-change skupiny přežily save/reload.
- E2E ONLYOFFICE parity testy byly rozšířené o save/reload aserce pro bold, track insertion, track deletion, accepted/rejected revisions a comment boundary; dřívější parity testy kryjí font family, font size, text color a highlight reload.

Ověření:

- `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentSerializerTests|FullyQualifiedName~SaveRequest_UsesCanonicalRuntimeDocumentForFormattingCommentsRevisionsAndJsonSnapshot|FullyQualifiedName~SaveRequest_UsesStructuredProviderBoundaryDocumentWithoutDisplayOnlyImageUrl" --verbosity normal`
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentEditor" --logger "trx;LogFileName=phase16-documenteditor.trx" --verbosity quiet`
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~OnlyOfficeParity_RibbonBold_AppliesToMouseSelectionKeepsSelectionAndEnablesUndo|FullyQualifiedName~OnlyOfficeParity_TrackChangesTyping_PreservesOrderAndCoalescesInsertion|FullyQualifiedName~OnlyOfficeParity_TrackChangesDeletion_PersistsAfterSaveReload|FullyQualifiedName~OnlyOfficeParity_ReviewedRevisions_DoNotReturnAfterSaveReload|FullyQualifiedName~OnlyOfficeParity_CommentBoundary_TypingAfterCommentDoesNotExtendCommentHighlight" --verbosity normal`

## Fáze 17: Accessibility a keyboard parity

Stav: hotovo (2026-05-24)

### 17.1 Toolbar keyboard

- [x] E2E: Tab prochází toolbar ve správném pořadí.
- [x] E2E: Space/Enter aktivuje toolbar button.
- [x] E2E: Escape zavře dropdown/popover a vrátí focus.
- [x] E2E: Arrow keys fungují v selectu/paletě.
- [x] Component: `aria-pressed` pro toggle commands.
- [x] Component: `aria-expanded` pro dropdowny/popovers.
- [x] Component: `aria-disabled` odpovídá disabled state.

### 17.2 Editor keyboard shortcuts

- [x] E2E: Ctrl+B aplikuje bold přes runtime command.
- [x] E2E: Ctrl+I aplikuje italic.
- [x] E2E: Ctrl+U aplikuje underline.
- [x] E2E: Ctrl+Z provede undo.
- [x] E2E: Ctrl+Y/Ctrl+Shift+Z provede redo.
- [x] E2E: shortcuts neporuší track changes grouping.

### 17.3 Akceptace fáze 17

- [x] Toolbar i editor jsou použitelné klávesnicí.
- [x] Accessibility state odpovídá runtime state.

### Ověření fáze 17

- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditorRuntimePhase21AccessibilityJavaScriptTests.Phase21_FocusAndKeyboardModel_RoutesCommandsThroughAccessibleOwner" --verbosity quiet`
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentEditor" --verbosity quiet`
- `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~TmColorPickerTests|FullyQualifiedName~TmColorPaletteTests" --verbosity quiet`
- `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~OnlyOfficeParity_ToolbarKeyboard_TabActivateEscapeAndPaletteArrows|FullyQualifiedName~OnlyOfficeParity_EditorKeyboardShortcuts_FormatUndoRedoAndKeepTrackChangesGrouped" --verbosity normal`
- `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~OnlyOfficeParity_RibbonTabs_ShowClearModesPreserveSelectionAndTrackChangesToggle|FullyQualifiedName~OnlyOfficeParity_TrackChangesTyping_PreservesOrderAndCoalescesInsertion|FullyQualifiedName~OnlyOfficeParity_TypingSessionUndo_RemovesWholeSessionAndEnablesRedo|FullyQualifiedName~OnlyOfficeParity_RibbonInlineToggleCommands_ApplyTogglePreserveSelectionAndUndo" --verbosity normal`

## Fáze 18: Performance a render budget

Stav: hotovo

### 18.1 Render metrics

- [x] Přidat runtime counter pro full render.
- [x] Přidat runtime counter pro partial render.
- [x] Přidat counter pro Blazor callbacks během typing.
- [x] Přidat counter pro formatting state events.
- [x] E2E: běžné psaní nesmí full renderovat celý dokument po každé klávese.
- [x] E2E: formatting command smí přerenderovat jen dotčené runs/paragraph.
- [x] E2E: toolbar state update nesmí vyvolat layout thrash.

### 18.2 Latency budgets

- [x] Definovat threshold pro keydown -> visible text.
- [x] Definovat threshold pro Space.
- [x] Definovat threshold pro Enter.
- [x] Definovat threshold pro toolbar command -> visible style.
- [x] Definovat threshold pro selection change -> toolbar state.
- [x] Testy musí ukládat histogram, ne jen average.

### 18.3 Stress scenarios

- [x] E2E: rychlé psaní 200 znaků.
- [x] E2E: držení klávesy 2 sekundy.
- [x] E2E: formatting na dlouhém odstavci.
- [x] E2E: track changes typing 100 znaků.
- [x] E2E: mixed comments/revisions/formatting v jednom odstavci.

### 18.4 Akceptace fáze 18

- [x] Editor působí při psaní plynule.
- [x] Performance testy failují na reálné zpoždění, ne až na timeout.

Poznámka 2026-05-24: Fáze 18 dokončena. Runtime debug metriky teď rozlišují full render, partial/live DOM patch, Blazor callbacky během typing hot path, formatting-state eventy a toolbar-state layout audit. Latence se ukládají jako histogramy pro keydown -> visible text, Space, Enter, toolbar command -> visible style a selection -> toolbar state, včetně p50/p95/max a budgetů. Typing selectionchange/formatting-state publish je debouncovaný mimo key-rate, takže rychlé psaní a držení klávesy zůstává JS-owned bez full renderu a bez Blazor callbacku po každé klávese. E2E fáze 18 pokrývá 200+ znaků, držení klávesy 2 s, Space/Enter, formatting v dlouhém odstavci, track changes 100+ znaků a mixed comments/revisions/formatting. Ověřeno: `node --check src/Tempo.Blazor/wwwroot/js/document-editor-wysiwyg.js`; `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRuntimePhase18PerformanceJavaScriptTests|FullyQualifiedName~DocumentEditorRuntimePhase20PerformanceJavaScriptTests" --verbosity quiet` (7/7); `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore -clp:ErrorsOnly --verbosity quiet`; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~OnlyOfficeParity_PerformanceBudget" --verbosity normal` (2/2); `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorRegressionRecoveryPhase10E2ETests" --verbosity normal` (3/3).

## Fáze 19: Ruční smoke scénáře z videí

Stav: čeká

### 19.1 Reprodukce předchozího videa s toolbary

- [ ] Otevřít demo dokument.
- [ ] Kliknout do nadpisu bez selection.
- [ ] Ověřit floating toolbar se nezobrazí.
- [ ] Vybrat text.
- [ ] Nastavit modrou ve floating toolbaru.
- [ ] Ověřit ribbon swatch je modrý.
- [ ] Změnit font size v ribbonu na 28.
- [ ] Ověřit floating toolbar ukazuje 28.
- [ ] Ověřit text má skutečně 28.

### 19.2 Reprodukce track changes videa

- [ ] Zapnout Revize.
- [ ] Zapnout Sledování změn.
- [ ] Napsat `jak se mas`.
- [ ] Ověřit text přesně odpovídá.
- [ ] Ověřit insertion markup je souvislý.
- [ ] Vložit text uprostřed existující věty.
- [ ] Ověřit insertion range je správně umístěný.
- [ ] Přepnout display mode.
- [ ] Ověřit markup se zobrazí/skryje.

### 19.3 Reprodukce comment boundary obrázku

- [ ] Najít text s komentářem.
- [ ] Kliknout hned za komentář.
- [ ] Psát nový text.
- [ ] Ověřit nový text není v oranžovém/comment zvýraznění.
- [ ] Ověřit comment panel stále ukazuje původní rozsah.

### 19.4 Akceptace fáze 19

- [ ] Všechny ruční scénáře jsou kryté automatizovaným E2E.
- [ ] Ruční smoke projde bez viditelného workaroundu.

## Fáze 20: Celkový GREEN gate

Stav: čeká

### 20.1 Cílené testy

- [ ] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentEditor"`.
- [ ] Spustit `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorOnlyOfficeParity"`.
- [ ] Spustit `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorRegressionRecovery"`.
- [ ] Spustit `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorStrictEngine"`.
- [ ] Spustit `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --filter "FullyQualifiedName~DocumentEditorJsRuntime"`.

### 20.2 Celá sada

- [ ] Spustit `dotnet build TempoBlazor.slnx`.
- [ ] Spustit `dotnet test`.
- [ ] Pokud celá E2E sada vyžaduje běžící demo, spustit Demo API a WASM demo.
- [ ] Spustit celou `tests/Tempo.Blazor.E2E`.
- [ ] Zapsat všechny flaky testy.
- [ ] Flaky test neopravovat oslabením assertion; opravit wait/event/produktový bug.

### 20.3 Manual QA

- [ ] Ručně otevřít `/document-editor`.
- [ ] Projít Home toolbar.
- [ ] Projít floating toolbar.
- [ ] Projít Review tab.
- [ ] Projít typing, Space, Enter.
- [ ] Projít comments.
- [ ] Projít track changes.
- [ ] Projít undo/redo.
- [ ] Projít save/reload.
- [ ] Uložit screenshot/video evidence.

### 20.4 Akceptace finálního plánu

- [ ] Všechny DocumentEditor unit/component testy jsou zelené.
- [ ] Všechny DocumentEditor E2E testy jsou zelené.
- [ ] E2E testy kontrolují správné věci: viditelný DOM, computed style, selection, toolbar state, undo, persistence.
- [ ] Ruční smoke odpovídá výsledkům testů.
- [ ] Editor už nemá známé P0 problémy z tohoto dokumentu.

## Průběžný záznam výsledků

### Baseline výsledky

- [x] 2026-05-24 06:05 CEST: `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` prošel bez chyb; zůstává 25 existujících warningů.
- [x] 2026-05-24 06:05 CEST: `dotnet build src/Tempo.Blazor.Demo.Api/Tempo.Blazor.Demo.Api.csproj --no-restore` prošel bez chyb.
- [x] 2026-05-24 06:05 CEST: `dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore` prošel bez chyb; zůstává existující sada warningů v demo projektu.
- [x] 2026-05-24 06:05 CEST: API endpoint `https://localhost:5100/api/document-editor/documents/onlyoffice-parity-2026-05-24` vrací `found: true` a seed `ONLYOFFICE parity baseline`.
- [x] 2026-05-24 06:04 CEST: `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorOnlyOfficeParityE2ETests" --logger "trx;LogFileName=document-editor-onlyoffice-parity-phase0.trx"` skončil očekávaně RED: 9 total, 2 passed, 7 failed, 0 skipped, duration 1 m 22 s.
- [x] TRX výsledek: `tests/Tempo.Blazor.E2E/TestResults/document-editor-onlyoffice-parity-phase0.trx`.
- [x] Passed: `OnlyOfficeParity_SeedContainsAllPhase0Scenarios`, `OnlyOfficeParity_CommentBoundary_TypingAfterCommentDoesNotExtendCommentHighlight`.
- [x] Failed selection/formátování: `OnlyOfficeParity_RibbonBold_AppliesToMouseSelectionKeepsSelectionAndEnablesUndo`, `OnlyOfficeParity_RibbonFontSize_AppliesToSelectionAndSynchronizesVisibleState`, `OnlyOfficeParity_RibbonTextColor_AppliesToSelectionAndUpdatesSwatch`, `OnlyOfficeParity_RibbonHighlight_AppliesToSelectionAndUpdatesSwatch`, `OnlyOfficeParity_FloatingToolbar_FormatsSelectionAndSynchronizesRibbon`, `OnlyOfficeParity_RibbonFontSizePointerMiss_DoesNotDestroySelection`.
- [x] Faktický první důvod selhání formatting testů: `SelectPhraseByMouseAsync` timeoutuje při čekání na `window.getSelection()?.toString().includes("exact target phrase")`; to znamená, že současný editor selhává už na stabilní human selection vrstvě před samotným computed style assertion.
- [x] Failed track changes: `OnlyOfficeParity_TrackChangesTyping_PreservesOrderAndCoalescesInsertion`.
- [x] Track changes detail: text `jak se mas` se vložil ve správném pořadí, ale vzniklo 10 samostatných insertion fragmentů (`FragmentCount=10`), tedy přesně problém "co písmeno, to fragment".
- [x] 2026-05-24 06:25 CEST: po fázi 1 `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` prošel bez warningů a chyb.
- [x] 2026-05-24 06:24 CEST: `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorOnlyOfficeParityE2ETests" --logger "trx;LogFileName=document-editor-onlyoffice-parity-phase1-scrollfix.trx"` skončil očekávaně RED: 9 total, 2 passed, 7 failed, 0 skipped, duration 1 m 4 s.
- [x] TRX výsledek fáze 1: `tests/Tempo.Blazor.E2E/TestResults/document-editor-onlyoffice-parity-phase1-scrollfix.trx`.
- [x] Fáze 1 změnila první selhání formatting testů: human mouse selection už vybere `exact target phrase` v `onlyoffice-formatting-paragraph` na offsetech 25-44.
- [x] Nový přesný formatting RED: toolbar/floating toolbar `pointerdown` smaže native selected text (`selectedText=""`, collapsed=true), zatímco runtime selection pořád drží rozsah 25-44, ale nemá stabilní runtime selection token.
- [x] Pointer miss RED: klik vedle `document-font-size` zruší native selection; runtime selection ještě drží rozsah.
- [x] Track changes RED zůstává stejný: text `jak se mas` je ve správném pořadí, ale stále vzniká 10 insertion fragmentů.
- [x] 2026-05-24 06:25 CEST: reprezentativní `OnlyOfficeParity_RibbonBold_AppliesToMouseSelectionKeepsSelectionAndEnablesUndo` má kompaktní failure message a ukládá PNG/JSON artefakt; TRX: `tests/Tempo.Blazor.E2E/TestResults/document-editor-onlyoffice-parity-phase1-bold-message.trx`.

### Implementační poznámky

- [x] Fáze 0 přidala stabilní seed id `onlyoffice-parity-2026-05-24`.
- [x] Seed je vytvořen v `InMemoryDocumentEditorProvider.SeedOnlyOfficeParityDocument` a publikovaný přes demo API i lokální demo provider.
- [x] E2E helper `OpenOnlyOfficeParityDocumentAsync` otevírá `document-editor?documentId=onlyoffice-parity-2026-05-24`.
- [x] Nové parity testy používají reálnou Playwright myš/klávesnici pro uživatelské akce; interní JS je použitý jen pro čtení souřadnic, computed style, debug artefakty a API seed verifikaci.
- [x] Výsledek fáze 0 nesmí být interpretovaný jako oprava bugů. Je to pravdivý baseline, který brání dalším falešně zeleným fázím.
- [x] Fáze 1 přesunula human selection, toolbar pointer, computed style, toolbar state a failure artifact helpery do `DocumentEditorE2ETestBase`.
- [x] `SelectTextByMouseAsync` teď scrolluje target do skutečného mouse viewportu, táhne reálnou myší a vrací `DocumentEditorSelectionSnapshot` s block id, offsets, selected text, rect a runtime selection diagnostikou.
- [x] `ClickRibbonCommandAsync` a floating/ribbon select/color helpery používají reálné pointer akce a kontrolují selection při `pointerdown`; nepoužívají interní command API jako náhradu UI.
- [x] `CaptureDocumentEditorDiagnosticArtifactAsync` ukládá screenshot, runtime snapshot, selection snapshot, ribbon/floating state, DOM excerpt cílového blocku, console entries a undo stack summary.

### Známé odchylky od ONLYOFFICE

- [ ] Doplnit vědomé produktové rozdíly, které nechceme implementovat.

## Doporučené pořadí prvních implementačních commitů

1. Přidat parity E2E seed a RED testy pro bold/font size/color/highlight/track changes/comment boundary.
2. Upravit E2E helpery tak, aby spolehlivě měřily selection, computed style a toolbar state.
3. Zavést selection token a runtime command transaction.
4. Opravit ribbon command pipeline pro bold jako první nejmenší svislý průřez.
5. Rozšířit stejný průřez na font size.
6. Rozšířit na text color a highlight.
7. Napojit floating toolbar na stejný command/state pipeline.
8. Opravit track changes grouping.
9. Opravit comment boundary.
10. Přepsat staré E2E testy, které doteď dávaly falešnou jistotu.
