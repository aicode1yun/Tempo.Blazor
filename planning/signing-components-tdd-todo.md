# TODO: Signing komponenty pro Tempo.Blazor

Datum založení: 2026-05-08  
Navazuje na: `planning/docuseal-blazor-components-analysis.md`  
Styl práce: TDD, potom demo stránka, potom E2E test  
Průběžné použití: při implementaci odškrtávat hotové body přímo v tomto souboru

## Pravidla checklistu

- [ ] Každá komponenta začíná RED unit testem v `tests/Tempo.Blazor.Tests/Components/...`.
- [ ] Každý public `[Parameter]` má XML dokumentaci.
- [ ] Každý uživatelský text používá `ITmLocalizer` a testovací `MockTmLocalizer`.
- [ ] CSS používá pouze `--tm-*` tokeny, žádné hardcoded barvy/spacing mimo nutné transparent/none/currentColor.
- [ ] Každá komponenta má demo scénář v `src/Tempo.Blazor.Demo.SharedUI/Pages/...`.
- [ ] Každý interaktivní editor/canvas má aspoň jeden Playwright E2E test v `tests/Tempo.Blazor.E2E`.
- [ ] Při dokončení komponenty spustit relevantní unit testy.
- [ ] Při dokončení komponenty spustit relevantní E2E testy nebo zapsat důvod, proč nešly spustit.
- [ ] Při dokončení fáze spustit `dotnet test tests/Tempo.Blazor.Tests/`.

## Fáze 0: Základní modely a infrastruktura

### 0.1 Abstractions: signing field model

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Models/SigningFieldModelTests.cs`.
- [x] RED: test enum hodnot pro `SigningFieldType`.
- [x] RED: test serializovatelnosti `SigningField`.
- [x] RED: test defaultních hodnot `SigningField`.
- [x] RED: test normalizovaných souřadnic v `SigningFieldArea`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningFieldType.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningField.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningFieldArea.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningFieldOption.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningFieldValidation.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningFieldPreferences.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningFieldCondition.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningConditionAction.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningConditionOperation.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningSubmitterRole.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningAttachment.cs`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningDocumentPage.cs`.
- [x] GREEN: unit testy modelů projdou.
- [x] REFACTOR: sjednotit naming s existujícími modely v `Tempo.Blazor.Abstractions`.

### 0.2 Abstractions: geometry helpers

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Models/SigningGeometryHelperTests.cs`.
- [x] RED: test převodu normalizované oblasti na pixelový rectangle.
- [x] RED: test clamp oblasti do stránky.
- [x] RED: test minimální velikosti oblasti.
- [x] RED: test resize z jihovýchodního rohu.
- [x] RED: test move oblasti bez opuštění stránky.
- [x] RED: test výpočtu selection rectangle přes více oblastí.
- [x] Implementovat `SigningGeometryHelper`.
- [x] Implementovat `SigningRectangle`.
- [x] Implementovat `SigningResizeHandle`.
- [x] GREEN: helper testy projdou.
- [x] REFACTOR: odstranit duplikace s diagram/wireframe helpery, pokud půjde sdílet bez coupling.

### 0.3 Localization keys

- [x] RED: přidat test, že nové signing localizer klíče existují v mocku.
- [x] Přidat EN klíče do `src/Tempo.Blazor/Resources/TmResources.resx`.
- [x] Přidat CS klíče do `src/Tempo.Blazor/Resources/TmResources.cs.resx`.
- [x] Přidat klíče do `MockTmLocalizer`.
- [x] GREEN: localization test projde.

### 0.4 CSS import skeleton

- [x] Přidat prázdné CSS soubory pro nové komponenty v `src/Tempo.Blazor/wwwroot/css/components/`.
- [x] Přidat importy do hlavního CSS entrypointu.
- [x] Zkontrolovat, zda bundled CSS je generovaný soubor a neměnit ručně, pokud repo používá generator.
- [x] RED: podle existujícího stylu přidat test nebo snapshot pro přítomnost root CSS tříd tam, kde se takové testy používají. Repo nemá samostatný CSS snapshot pattern; ověřeno přes importy a build.

### 0.5 Ověření fáze 0

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Signing" --no-restore` - prošlo 42/42.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --no-restore` - spuštěno, 3590/3595 prošlo; 5 pádů je v existujících `TmSpreadsheetKeyboardTests`, mimo signing změny.

## Fáze 1: `TmDocumentPageViewer`

### 1.1 Unit testy základního renderu

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Signing/TmDocumentPageViewerTests.cs`.
- [x] RED: renderuje empty state bez stránky.
- [x] RED: renderuje page image s `alt`.
- [x] RED: nastaví aspect ratio podle `Width` a `Height`.
- [x] RED: root obsahuje `tm-document-page-viewer`.
- [x] RED: přijímá `Class` a `AdditionalAttributes`.
- [x] RED: při `IsLoading=true` zobrazí skeleton/loading stav.
- [x] RED: při `Error` zobrazí alert/error stav.

### 1.2 Implementace základního vieweru

- [x] Vytvořit `src/Tempo.Blazor/Components/Signing/TmDocumentPageViewer.razor`.
- [x] Vytvořit code-behind, pokud bude potřeba stav.
- [x] Přidat namespace do `_Imports.razor`, pokud projekt používá centrální import.
- [x] Přidat CSS `_document-page-viewer.css`.
- [x] Použít `TmSkeleton`, `TmAlert`, `TmIcon` tam, kde to dává smysl.
- [x] GREEN: základní testy projdou.

### 1.3 Overlay slot

- [x] RED: test renderu `ChildContent` nad stránkou.
- [x] RED: test předání page contextu přes `RenderFragment<SigningDocumentPage>`.
- [x] RED: test vypnutí pointer eventů pro read-only overlay.
- [x] Implementovat `ChildContent`.
- [x] Implementovat `PageTemplate` nebo `OverlayTemplate`.
- [x] GREEN: overlay testy projdou.

### 1.4 Page refs a scroll hooky

- [x] RED: test `Id`/`data-page-index` atributů.
- [x] RED: test `OnPageClick`.
- [x] RED: test `OnPageContextMenu`.
- [x] Implementovat click/context menu callbacky.
- [x] Implementovat ARIA label stránky.
- [x] GREEN: interakční testy projdou.

### 1.5 Demo a E2E

- [x] Přidat sekci do nové nebo existující demo stránky `SigningComponentsPage.razor`.
- [x] Přidat route `/signing-components`.
- [x] Přidat položku do `NavMenu.razor`.
- [x] RED E2E: vytvořit `tests/Tempo.Blazor.E2E/SigningDocumentPageViewerE2ETests.cs`.
- [x] E2E: otevře demo stránku a ověří page image.

### 1.6 Ověření fáze 1

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmDocumentPageViewerTests|FullyQualifiedName~SigningLocalizationTests" --no-restore` - prošlo 50/50.
- [x] Spustit `dotnet build src/Tempo.Blazor.Demo.SharedUI/Tempo.Blazor.Demo.SharedUI.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Signing" --no-restore` - prošlo 61/61.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~SigningDocumentPageViewerE2ETests" --no-restore` proti `https://localhost:7106` - prošlo 1/1.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --no-restore` - spuštěno, 3609/3614 prošlo; 5 pádů je v existujících `TmSpreadsheetKeyboardTests`, mimo signing změny.
- [x] E2E: ověří, že overlay obsah leží nad stránkou.
- [x] E2E: screenshot desktop.
- [x] E2E: screenshot mobile.
- [x] GREEN E2E.

## Fáze 2: `TmSigningFieldOverlay`

### 2.1 Základní render

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Signing/TmSigningFieldOverlayTests.cs`.
- [x] RED: renderuje root `tm-signing-field`.
- [x] RED: nastaví pozici podle `SigningFieldArea`.
- [x] RED: používá normalizované souřadnice.
- [x] RED: renderuje typovou ikonu pro `Signature`.
- [x] RED: renderuje label podle `Field.Name`.
- [x] RED: renderuje required indikaci.
- [x] RED: podporuje `Class` a `AdditionalAttributes`.
- [x] Implementovat `TmSigningFieldOverlay.razor`.
- [x] Implementovat CSS `_signing-field-overlay.css`.
- [x] GREEN: základní testy projdou.

### 2.2 Stavy pole

- [x] RED: test `Selected`.
- [x] RED: test `Focused`.
- [x] RED: test `Invalid`.
- [x] RED: test `Completed`.
- [x] RED: test `ReadOnly`.
- [x] RED: test `Disabled`.
- [x] RED: test `Draggable`.
- [x] Implementovat state CSS třídy.
- [x] Implementovat ARIA stav pro invalid/completed.
- [x] GREEN: stavové testy projdou.

### 2.3 Render hodnot podle typů

- [x] RED: test text value preview.
- [x] RED: test number value preview.
- [x] RED: test date value preview.
- [x] RED: test checkbox checked/unchecked.
- [x] RED: test radio option checked podle `option_uuid`.
- [x] RED: test multiple option checked.
- [x] RED: test `cells` rozdělení znaků.
- [x] RED: test signature/image thumbnail value.
- [x] RED: test stamp placeholder.
- [x] RED: test heading.
- [x] RED: test strikethrough.
- [x] Implementovat typové render fragmenty.
- [x] GREEN: typové testy projdou.

### 2.4 Interakce

- [x] RED: test `OnClick`.
- [x] RED: test `OnDoubleClick`.
- [x] RED: test `OnContextMenu`.
- [x] RED: test `OnStartMove`.
- [x] RED: test `OnStartResize`.
- [x] RED: test resize handles jsou renderované jen když `Editable=true`.
- [x] Implementovat callbacky.
- [x] Implementovat resize handles.
- [x] GREEN: interakční testy projdou.

### 2.5 Demo a E2E

- [x] Přidat demo sekci pro všechny typy polí.
- [x] RED E2E: vytvořit `SigningFieldOverlayE2ETests.cs`.
- [x] E2E: ověří pozici overlaye vůči stránce.
- [x] E2E: klik vybere pole.
- [x] E2E: context menu trigger neprovede browser menu.
- [x] E2E: mobile viewport zachová čitelnost labelů.
- [x] GREEN E2E.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Signing" --no-restore` - prošlo 85/85.
- [x] Spustit `dotnet build src/Tempo.Blazor.Demo.SharedUI/Tempo.Blazor.Demo.SharedUI.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~Signing" --no-restore` proti `https://localhost:7106` - prošlo 5/5.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --no-restore` - spuštěno, 3633/3638 prošlo; 5 pádů je v existujících `TmSpreadsheetKeyboardTests`, mimo signing změny.

## Fáze 3: `TmSignatureCapture`

### 3.1 API a základní render

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Inputs/TmSignatureCaptureTests.cs`.
- [x] RED: renderuje root `tm-signature-capture`.
- [x] RED: výchozí mode je `Draw`.
- [x] RED: zobrazí canvas/pad.
- [x] RED: zobrazí clear tlačítko.
- [x] RED: přijímá `Value`, `ValueChanged`.
- [x] RED: podporuje `Disabled`.
- [x] RED: podporuje `Class` a `AdditionalAttributes`.
- [x] Implementovat `TmSignatureCapture.razor`.
- [x] Implementovat code-behind.
- [x] Implementovat CSS `_signature-capture.css`.
- [x] GREEN: základní testy projdou.

### 3.2 Draw mode

- [x] RED: test pointer down založí kreslení.
- [x] RED: test pointer move přidá body.
- [x] RED: test pointer up vyvolá `ValueChanged`.
- [x] RED: test `ClearAsync` vymaže hodnotu.
- [x] RED: test prázdný podpis je invalid, pokud `Required=true`.
- [x] Implementovat draw mode.
- [x] Implementovat minimální stroke serializaci.
- [x] Implementovat `IsEmpty`.
- [x] GREEN: draw testy projdou.

### 3.3 Export PNG/data URL přes JS interop

- [x] RED: test JS interop volání `tmSignatureCapture.exportPng`.
- [x] RED: test fallback na SVG, když JS není dostupné.
- [x] RED: test `ExportFormat=PngDataUrl`.
- [x] RED: test `ExportFormat=Svg`.
- [x] Přidat `src/Tempo.Blazor/wwwroot/js/signature-capture.js`.
- [x] Registrovat JS helper podle existujícího patternu.
- [x] Implementovat high-DPI canvas export.
- [x] GREEN: JS interop testy projdou.

### 3.4 Typed mode

- [x] RED: test přepnutí do `Typed`.
- [x] RED: test typed input generuje hodnotu.
- [x] RED: test initials mode používá kratší label.
- [x] RED: test font selection.
- [x] RED: test preview typed signature.
- [x] Implementovat typed mode.
- [x] Implementovat font CSS bez externí závislosti.
- [x] GREEN: typed testy projdou.

### 3.5 Upload mode

- [x] RED: test zobrazení upload dropzone.
- [x] RED: test accept `image/*`.
- [x] RED: test `OnUploadRequested` callback.
- [x] RED: test reupload tlačítko.
- [x] Implementovat upload mode přes `TmFileDropZone` nebo `InputFile`.
- [x] GREEN: upload testy projdou.

### 3.6 Pokročilé signing chování

- [x] RED: test `RequireReason=true` zobrazí reason input.
- [x] RED: test reason je součástí changed payloadu.
- [x] RED: test `RememberSignature` checkbox.
- [x] RED: test `ShowQrSigningButton` zobrazí QR tlačítko.
- [x] RED: test `PreviousValue` se zobrazí jako preview.
- [x] Implementovat reason.
- [x] Implementovat previous/reuse signature preview.
- [x] Implementovat QR slot/callback bez aplikačního backendu.
- [x] GREEN: pokročilé testy projdou.

### 3.7 Demo a E2E

- [x] Přidat demo sekci Draw/Typed/Upload/Reason/QR.
- [x] RED E2E: vytvořit `SignatureCaptureE2ETests.cs`.
- [x] E2E: nakreslí podpis myší a ověří preview/value.
- [x] E2E: vyčistí podpis.
- [x] E2E: napíše typed signature.
- [x] E2E: mobile touch/pointer scénář.
- [x] GREEN E2E.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Signing|FullyQualifiedName~TmSignatureCapture" --no-restore` - prošlo 124/124.
- [x] Spustit `dotnet build src/Tempo.Blazor.Demo.SharedUI/Tempo.Blazor.Demo.SharedUI.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~SignatureCaptureE2ETests" --no-restore` proti `https://localhost:7106` - prošlo 4/4.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~Signing|FullyQualifiedName~SignatureCapture" --no-restore` proti `https://localhost:7106` - prošlo 9/9.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --no-restore` - spuštěno, 3672/3677 prošlo; 5 pádů je v existujících `TmSpreadsheetKeyboardTests`, mimo signing změny.

## Fáze 4: `TmConditionBuilder`

### 4.1 Základní builder

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Signing/TmConditionBuilderTests.cs`.
- [x] RED: renderuje empty condition row.
- [x] RED: zobrazí select polí.
- [x] RED: vyfiltruje aktuální pole.
- [x] RED: vyfiltruje nepodporované typy `heading` a `strikethrough`.
- [x] RED: vyvolá `ConditionsChanged`.
- [x] Implementovat `TmConditionBuilder`.
- [x] Implementovat CSS `_condition-builder.css`.
- [x] GREEN: základní testy projdou.

### 4.2 Action podle field typu

- [x] RED: checkbox nabízí `Checked/Unchecked`.
- [x] RED: radio nabízí `Equal/NotEqual`.
- [x] RED: select nabízí `Equal/NotEqual`.
- [x] RED: multiple nabízí `Contains/DoesNotContain`.
- [x] RED: number nabízí `Empty/NotEmpty/Equal/NotEqual/GreaterThan/LessThan`.
- [x] RED: text nabízí `Empty/NotEmpty`.
- [x] Implementovat action resolver.
- [x] GREEN: action testy projdou.

### 4.3 Hodnota podmínky

- [x] RED: radio/select renderuje option dropdown.
- [x] RED: multiple renderuje option dropdown.
- [x] RED: number renderuje number input.
- [x] RED: empty/not_empty hodnotu nevyžaduje.
- [x] RED: validace vyžaduje value pro equal/contains.
- [x] Implementovat value editor.
- [x] GREEN: value testy projdou.

### 4.4 AND/OR a cykly

- [x] RED: přidání druhé podmínky.
- [x] RED: přepnutí operation `And/Or`.
- [x] RED: odebrání podmínky.
- [x] RED: detekce přímého cyklu.
- [x] RED: detekce nepřímého cyklu přes více polí.
- [x] Implementovat cycle validator.
- [x] GREEN: cycle testy projdou.

### 4.5 Demo a E2E

- [x] Přidat demo sekci.
- [x] RED E2E: vytvořit `ConditionBuilderE2ETests.cs`.
- [x] E2E: vytvoří dvě podmínky.
- [x] E2E: přepne AND/OR.
- [x] E2E: ověří validaci chybějící hodnoty.
- [x] GREEN E2E.

## Fáze 5: `TmFormulaBuilder`

### 5.1 Formula model a helpery

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Models/SigningFormulaHelperTests.cs`.
- [x] RED: humanize `{{uuid}}` na `{{Name}}`.
- [x] RED: normalize `{{Name}}` na `{{uuid}}`.
- [x] RED: neznámé pole vrátí chybu.
- [x] RED: detekce přímého cyklu.
- [x] RED: detekce nepřímého cyklu.
- [x] Implementovat `SigningFormulaHelper`.
- [x] GREEN: helper testy projdou.

### 5.2 Builder UI

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Signing/TmFormulaBuilderTests.cs`.
- [x] RED: renderuje textarea.
- [x] RED: renderuje token buttons pro number fields.
- [x] RED: renderuje numeric select/radio fields, pokud options jsou čísla.
- [x] RED: nevypíše aktuální pole.
- [x] RED: nevypíše pole, které by vytvořilo cyklus.
- [x] RED: klik token button vloží token.
- [x] Implementovat `TmFormulaBuilder`.
- [x] Implementovat CSS `_formula-builder.css`.
- [x] GREEN: builder testy projdou.

### 5.3 Operátory a validace

- [x] RED: operator button `+` vloží text.
- [x] RED: operator button `-` vloží text.
- [x] RED: operator button `*` vloží text.
- [x] RED: operator button `/` vloží text.
- [x] RED: function button `round(n, d)` vloží text.
- [x] RED: function button `abs(n)` vloží text.
- [x] RED: Save neprojde s neznámým tokenem.
- [x] RED: Save nastaví `Readonly=true` u vypočítaného pole.
- [x] Implementovat operátorovou lištu.
- [x] Implementovat validaci.
- [x] GREEN: operátorové testy projdou.

### 5.4 Demo a E2E

- [x] Přidat demo sekci.
- [x] RED E2E: vytvořit `FormulaBuilderE2ETests.cs`.
- [x] E2E: vloží token a operátor.
- [x] E2E: uloží validní formuli.
- [x] E2E: zobrazí chybu pro chybějící field.
- [x] GREEN E2E.

### 5.5 Ověření fáze 5

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~SigningFormulaHelperTests|FullyQualifiedName~TmFormulaBuilderTests" --no-restore` - prošlo 19/19.
- [x] Spustit `dotnet build src/Tempo.Blazor.Demo.SharedUI/Tempo.Blazor.Demo.SharedUI.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Signing|FullyQualifiedName~TmSignatureCapture|FullyQualifiedName~TmConditionBuilder|FullyQualifiedName~TmFormulaBuilder|FullyQualifiedName~SigningFormulaHelper" --no-restore` - prošlo 192/192.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~FormulaBuilderE2ETests" --no-restore` proti `https://localhost:7106` - prošlo 3/3.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~Signing|FullyQualifiedName~SignatureCapture|FullyQualifiedName~ConditionBuilder|FullyQualifiedName~FormulaBuilder" --no-restore` proti `https://localhost:7106` - prošlo 15/15.
- [x] Spustit `dotnet build TempoBlazor.slnx --no-restore` - prošlo; zůstávají existující warningy v XML dokumentaci/resx.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --no-restore` - spuštěno, 3740/3745 prošlo; 5 pádů je v existujících `TmSpreadsheetKeyboardTests`, mimo signing změny.

## Fáze 6: `TmRecipientRoleEditor`

### 6.1 Role list

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Signing/TmRecipientRoleEditorTests.cs`.
- [x] RED: renderuje seznam rolí.
- [x] RED: prázdný seznam zobrazí default role.
- [x] RED: role má jméno a barvu.
- [x] RED: add role vytvoří novou roli.
- [x] RED: remove role odebere roli.
- [x] RED: rename role vyvolá `RolesChanged`.
- [x] Implementovat `TmRecipientRoleEditor`.
- [x] Implementovat CSS `_recipient-role-editor.css`.
- [x] GREEN: role list testy projdou.

### 6.2 Submission recipient údaje

- [x] RED: test `Mode=TemplateRoles` nezobrazuje email.
- [x] RED: test `Mode=SubmissionRecipients` zobrazuje email/name/phone.
- [x] RED: email input má typ email.
- [x] RED: phone input má typ tel.
- [x] RED: validuje required email, pokud role není optional.
- [x] Implementovat mode enum.
- [x] Implementovat recipient edit fields.
- [x] GREEN: recipient testy projdou.

### 6.3 Signing order a invite pravidla

- [x] RED: test drag/reorder role.
- [x] RED: test order number update.
- [x] RED: test `OptionalInviteByRoleId`.
- [x] RED: test `InviteByRoleId`.
- [x] RED: test `InviteViaFieldId`.
- [x] RED: test prevence invite self-reference.
- [x] Implementovat reorder.
- [x] Implementovat invite selectors.
- [x] GREEN: order/invite testy projdou.

### 6.4 Demo a E2E

- [x] Přidat demo sekci TemplateRoles.
- [x] Přidat demo sekci SubmissionRecipients.
- [x] RED E2E: vytvořit `RecipientRoleEditorE2ETests.cs`.
- [x] E2E: přidá roli.
- [x] E2E: přejmenuje roli.
- [x] E2E: vyplní email.
- [x] E2E: přeuspořádá role.
- [x] GREEN E2E.

### 6.5 Ověření fáze 6

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmRecipientRoleEditorTests|FullyQualifiedName~SigningLocalizationTests" --no-restore` - prošlo 117/117.
- [x] Spustit `dotnet build src/Tempo.Blazor.Demo.SharedUI/Tempo.Blazor.Demo.SharedUI.csproj --no-restore` - prošlo; zůstávají existující warningy ve `FileManagerPage` a `SignaturePage`.
- [x] Spustit `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~RecipientRoleEditorE2ETests" --no-restore` proti `https://localhost:7106` - prošlo 4/4.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~Signing|FullyQualifiedName~SignatureCapture|FullyQualifiedName~ConditionBuilder|FullyQualifiedName~FormulaBuilder|FullyQualifiedName~RecipientRoleEditor" --no-restore` proti `https://localhost:7106` - prošlo 19/19.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Signing|FullyQualifiedName~TmSignatureCapture|FullyQualifiedName~TmConditionBuilder|FullyQualifiedName~TmFormulaBuilder|FullyQualifiedName~SigningFormulaHelper|FullyQualifiedName~TmRecipientRoleEditor" --no-restore` - prošlo 224/224.
- [x] Spustit `dotnet build TempoBlazor.slnx --no-restore` - prošlo; zůstávají existující warningy v resx/XML/nullability dokumentaci.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --no-restore` - spuštěno, 3772/3777 prošlo; 5 pádů je v existujících `TmSpreadsheetKeyboardTests`, mimo signing změny.

## Fáze 7: `TmSigningFieldEditorPanel`

### 7.1 Shell panelu

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Signing/TmSigningFieldEditorPanelTests.cs`.
- [x] RED: bez vybraného pole zobrazí empty state.
- [x] RED: s polem zobrazí název panelu.
- [x] RED: typ pole lze změnit.
- [x] RED: změna typu vyvolá `FieldChanged`.
- [x] RED: panel respektuje `ReadOnly`.
- [x] Implementovat `TmSigningFieldEditorPanel`.
- [x] Implementovat CSS `_signing-field-editor-panel.css`.
- [x] GREEN: shell testy projdou.

### 7.2 Common settings

- [x] RED: editace `Name`.
- [x] RED: editace `Title`.
- [x] RED: editace `Description`.
- [x] RED: toggle `Required`.
- [x] RED: toggle `Readonly`.
- [x] RED: toggle `Prefillable`.
- [x] RED: select `SubmitterRoleId`.
- [x] Implementovat common settings.
- [x] GREEN: common settings testy projdou.

### 7.3 Options editor

- [x] RED: select/radio/multiple zobrazí options editor.
- [x] RED: add option.
- [x] RED: rename option.
- [x] RED: remove option.
- [x] RED: reorder option.
- [x] RED: default value select.
- [x] RED: radio/multiple option area mapping callback.
- [x] Implementovat options editor.
- [x] GREEN: options testy projdou.

### 7.4 Validation settings

- [x] RED: text validation none.
- [x] RED: text validation regex.
- [x] RED: text length min/max.
- [x] RED: custom validation message.
- [x] RED: number min/max/step.
- [x] RED: date min/max.
- [x] RED: date format.
- [x] Implementovat validation sections.
- [x] GREEN: validation testy projdou.

### 7.5 Preferences settings

- [x] RED: signature format select.
- [x] RED: signature id toggle.
- [x] RED: stamp with logo toggle.
- [x] RED: font family select.
- [x] RED: font size input.
- [x] RED: align select.
- [x] RED: color picker.
- [x] RED: copy to all pages command callback.
- [x] Implementovat preference sections.
- [x] GREEN: preference testy projdou.

### 7.6 Condition/formula integration

- [x] RED: condition button otevře `TmConditionBuilder`.
- [x] RED: formula button otevře `TmFormulaBuilder` jen pro number/payment.
- [x] RED: saved condition zapíše do field.
- [x] RED: saved formula zapíše do preferences.
- [x] Implementovat modal/popover integraci.
- [x] GREEN: integration testy projdou.

### 7.7 Demo a E2E

- [x] Přidat demo s živým field preview.
- [x] RED E2E: vytvořit `SigningFieldEditorPanelE2ETests.cs`.
- [x] E2E: přejmenuje pole a preview se změní.
- [x] E2E: přidá option.
- [x] E2E: nastaví required a validation.
- [x] E2E: uloží condition.
- [x] GREEN E2E.

### 7.8 Ověření fáze 7

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmSigningFieldEditorPanelTests|FullyQualifiedName~SigningLocalizationTests" --no-restore` - prošlo 181/181.
- [x] Spustit `dotnet build src/Tempo.Blazor.Demo.SharedUI/Tempo.Blazor.Demo.SharedUI.csproj --no-restore` - prošlo; zůstávají existující warningy ve `FileManagerPage` a `SignaturePage`.
- [x] Spustit `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~SigningFieldEditorPanelE2ETests" --no-build` proti `https://localhost:7106` - prošlo 4/4.
- [x] Spustit širší signing E2E subset proti `https://localhost:7106` - 22/23 prošlo; jednorázově spadl existující `SignatureCapture_Clear_ClearsSignature` na timeout při načtení aplikace, samostatný rerun prošel 1/1.
- [x] Spustit `dotnet build TempoBlazor.slnx --no-restore` - prošlo; zůstávají existující warningy v resx/XML/nullability dokumentaci.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --no-restore` - spuštěno, 3853/3858 prošlo; 5 pádů je v existujících `TmSpreadsheetKeyboardTests`, mimo signing změny.

## Fáze 8: `TmPdfTemplateDesigner`

### 8.1 Základní designer layout

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Signing/TmPdfTemplateDesignerTests.cs`.
- [x] RED: renderuje root `tm-pdf-template-designer`.
- [x] RED: bez dokumentů zobrazí empty state.
- [x] RED: renderuje stránky přes `TmDocumentPageViewer`.
- [x] RED: renderuje existující fields přes `TmSigningFieldOverlay`.
- [x] RED: přijímá `Fields` a `FieldsChanged`.
- [x] RED: přijímá `Documents`.
- [x] Implementovat `TmPdfTemplateDesigner`.
- [x] Implementovat CSS `_pdf-template-designer.css`.
- [x] GREEN: layout testy projdou.

### 8.2 Field palette

- [x] RED: palette obsahuje povolené field typy.
- [x] RED: `AllowedFieldTypes` filtruje palette.
- [x] RED: klik na typ nastaví draw mode.
- [x] RED: disabled designer skryje palette.
- [x] Implementovat field palette.
- [x] GREEN: palette testy projdou.

### 8.3 Draw new field

- [x] RED: pointer down na stránce začne kreslit.
- [x] RED: pointer move zobrazí draft rectangle.
- [x] RED: pointer up vytvoří field area.
- [x] RED: malý rectangle pod min size se ignoruje.
- [x] RED: created field má `submitter_uuid` vybrané role.
- [x] RED: created area má `attachment_uuid` a page.
- [x] Implementovat draw mode.
- [x] GREEN: draw testy projdou.

### 8.4 Select, move, resize

- [x] RED: klik na field nastaví selected field.
- [x] RED: ctrl/cmd klik přidá do multi-select.
- [x] RED: drag selected field změní area.
- [x] RED: resize handle změní area.
- [x] RED: move drží field v rámci stránky.
- [x] RED: resize drží min size.
- [x] RED: `FieldsChanged` dostane změněné pole.
- [x] Implementovat selection state.
- [x] Implementovat move.
- [x] Implementovat resize.
- [x] GREEN: select/move/resize testy projdou.

### 8.5 Multi-select

- [x] RED: drag selection box vybere více polí.
- [x] RED: delete selected odstraní více polí.
- [x] RED: move selected posune skupinu.
- [x] RED: selection rectangle se počítá přes vybrané oblasti.
- [x] Implementovat selection box.
- [x] Implementovat hromadné akce.
- [x] GREEN: multi-select testy projdou.

### 8.6 Context menus

- [x] RED: page context menu nabízí paste/autodetect slot.
- [x] RED: field context menu nabízí copy/delete/settings.
- [x] RED: selection context menu nabízí copy/delete.
- [x] RED: context menu používá `TmContextMenu`.
- [x] Implementovat context menu hooks.
- [x] GREEN: context menu testy projdou.

### 8.7 Copy/paste a copy to pages

- [x] RED: copy field uloží field do interní clipboard state.
- [x] RED: paste field vytvoří kopii na aktuální stránce.
- [x] RED: copy to all pages vytvoří area na každé stránce stejného dokumentu.
- [x] RED: field uuid zůstane/nezůstane podle zvolené strategie a je testem popsané.
- [x] Implementovat clipboard.
- [x] Implementovat copy to pages.
- [x] GREEN: copy testy projdou.

### 8.8 Autodetect integration

- [x] RED: designer zobrazí autodetect button, pokud `OnDetectFields` existuje.
- [x] RED: pending detection zobrazí loading stav.
- [x] RED: detected fields se přidají do `Fields`.
- [x] RED: chyba detection zobrazí alert/toast callback.
- [x] Implementovat detection callback bez backend vazby.
- [x] GREEN: autodetect testy projdou.

### 8.9 Mobile designer

- [x] RED: při `MobileMode=true` se palette změní na compact.
- [x] RED: mobile draw button vytvoří draw state.
- [x] RED: labels nepřetékají.
- [x] Implementovat mobile layout.
- [x] GREEN: mobile unit testy projdou.

### 8.10 Demo a E2E

- [x] Přidat plnohodnotné demo designeru s dvěma stránkami.
- [x] Přidat sample page images do demo assets, pokud ještě nejsou - použita existující demo image.
- [x] RED E2E: vytvořit `PdfTemplateDesignerE2ETests.cs`.
- [x] E2E: otevře designer demo.
- [x] E2E: nakreslí text field.
- [x] E2E: přesune field.
- [x] E2E: změní velikost field.
- [x] E2E: otevře field settings.
- [x] E2E: přidá select option.
- [x] E2E: multi-select vybere dvě pole.
- [x] E2E: smaže vybraná pole.
- [x] E2E: screenshot desktop.
- [x] E2E: screenshot mobile.
- [x] GREEN E2E.

### 8.11 Ověření fáze 8

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmPdfTemplateDesignerTests|FullyQualifiedName~SigningLocalizationTests" --no-restore` - prošlo 181/181.
- [x] Spustit `dotnet build src/Tempo.Blazor.Demo.SharedUI/Tempo.Blazor.Demo.SharedUI.csproj --no-restore` - prošlo; zůstávají existující warningy ve `FileManagerPage` a `SignaturePage`.
- [x] Spustit `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~PdfTemplateDesignerE2ETests" --no-build` proti `https://localhost:7106` - prošlo 6/6.
- [x] Spustit širší signing unit subset - prošlo 338/338.
- [x] Spustit `dotnet build TempoBlazor.slnx --no-restore` - prošlo; zůstávají existující warningy v resx/XML/nullability dokumentaci.

## Fáze 9: Signing step komponenty

### 9.1 Společný step shell

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Signing/TmSigningStepShellTests.cs`.
- [x] RED: renderuje label.
- [x] RED: renderuje optional marker.
- [x] RED: renderuje description jako Markdown/plain text podle parametru.
- [x] RED: renderuje validation error.
- [x] RED: renderuje `AppearsOn` info.
- [x] Implementovat `TmSigningStepShell`.
- [x] Implementovat CSS `_signing-step-shell.css`.
- [x] GREEN: shell testy projdou.

### 9.2 Text step

- [x] RED: vytvořit `TmSigningTextStepTests.cs`.
- [x] RED: renderuje input pro single-line.
- [x] RED: toggle na multiline.
- [x] RED: `cells` nastaví maxlength podle area.
- [x] RED: pattern validation.
- [x] RED: custom validation message.
- [x] Implementovat `TmSigningTextStep`.
- [x] GREEN: text step testy projdou.

### 9.3 Number step

- [x] RED: vytvořit `TmSigningNumberStepTests.cs`.
- [x] RED: renderuje number input.
- [x] RED: min/max/step.
- [x] RED: cast value na number.
- [x] RED: required validation.
- [x] Implementovat `TmSigningNumberStep`.
- [x] GREEN: number step testy projdou.

### 9.4 Date step

- [x] RED: vytvořit `TmSigningDateStepTests.cs`.
- [x] RED: format date používá date input.
- [x] RED: format month používá month input.
- [x] RED: format datetime používá datetime-local input.
- [x] RED: min/max podporuje `{{date}}`.
- [x] RED: set today button.
- [x] Implementovat `TmSigningDateStep`.
- [x] GREEN: date step testy projdou.

### 9.5 Select/radio/multiple/checkbox steps

- [x] RED: vytvořit `TmSigningChoiceStepTests.cs`.
- [x] RED: select step renderuje select.
- [x] RED: radio step renderuje radio options.
- [x] RED: multiple step renderuje checkbox list.
- [x] RED: checkbox group renderuje více checkbox fields ve stejném kroku.
- [x] RED: anonymní checkbox mode zobrazí instrukci místo labelů.
- [x] RED: required choice validation.
- [x] Implementovat choice step komponenty.
- [x] GREEN: choice testy projdou.

### 9.6 File/image/stamp steps

- [x] RED: vytvořit `TmSigningAttachmentStepTests.cs`.
- [x] RED: image step renderuje upload.
- [x] RED: image step zobrazí preview po hodnotě.
- [x] RED: file step podporuje multiple upload.
- [x] RED: file step umožní remove attachment.
- [x] RED: stamp step zobrazí generated stamp placeholder/value.
- [x] Implementovat attachment step komponenty.
- [x] GREEN: attachment testy projdou.

### 9.7 Phone step

- [x] RED: vytvořit `TmSigningPhoneStepTests.cs`.
- [x] RED: renderuje country code select.
- [x] RED: renderuje tel input.
- [x] RED: normalizuje telefon.
- [x] RED: odešle `OnSendCode`.
- [x] RED: po send code zobrazí OTP input.
- [x] RED: resend countdown state.
- [x] Implementovat phone step.
- [x] GREEN: phone step testy projdou.

### 9.8 Verification/KBA/payment placeholder steps

- [x] RED: vytvořit `TmSigningExternalStepTests.cs`.
- [x] RED: verification step loading.
- [x] RED: verification step error.
- [x] RED: verification step external link.
- [x] RED: KBA step start form.
- [x] RED: KBA question step.
- [x] RED: payment step amount summary.
- [x] RED: payment step checkout callback.
- [x] Implementovat provider-agnostic placeholder komponenty.
- [x] GREEN: external step testy projdou.

### 9.9 Demo a E2E

- [x] Přidat demo všech stepů.
- [x] RED E2E: vytvořit `SigningStepsE2ETests.cs`.
- [x] E2E: vyplní text/number/date.
- [x] E2E: vybere select/radio/multiple.
- [x] E2E: ověří upload input a remove flow pro image step.
- [x] E2E: phone step přejde na OTP state.
- [x] GREEN E2E.

### 9.10 Ověření fáze 9

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmSigningStepShellTests|FullyQualifiedName~TmSigningTextStepTests|FullyQualifiedName~TmSigningNumberStepTests|FullyQualifiedName~TmSigningDateStepTests|FullyQualifiedName~TmSigningChoiceStepTests|FullyQualifiedName~TmSigningAttachmentStepTests|FullyQualifiedName~TmSigningPhoneStepTests|FullyQualifiedName~TmSigningExternalStepTests|FullyQualifiedName~SigningLocalizationTests" --no-restore` - prošlo 227/227.
- [x] Spustit `dotnet build src/Tempo.Blazor.Demo/Tempo.Blazor.Demo.csproj --no-restore` - prošlo; zůstává `NU1603` pro `Microsoft.Extensions.Http` a existující warningy v demo stránkách.
- [x] Spustit `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - prošlo.
- [x] Spustit `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~SigningStepsE2ETests" --no-build` proti `https://localhost:7106` - prošlo 5/5.

## Fáze 10: `TmSigningFormRunner`

### 10.1 Runner model a step planning

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Models/SigningStepPlannerTests.cs`.
- [x] RED: plánuje pole podle pořadí v document/page/y/x.
- [x] RED: seskupí checkbox fields podle navazujících checkboxů.
- [x] RED: vynechá readonly heading/strikethrough podle pravidel.
- [x] RED: vyhodnotí hidden condition.
- [x] RED: zahrne formula readonly fields do overlay.
- [x] Implementovat `SigningStepPlanner`.
- [x] GREEN: planner testy projdou.

### 10.2 Runner shell

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Signing/TmSigningFormRunnerTests.cs`.
- [x] RED: bez fields zobrazí empty/completed state.
- [x] RED: renderuje documents.
- [x] RED: renderuje overlay fields.
- [x] RED: renderuje current step panel.
- [x] RED: current step zvýrazní field.
- [x] Implementovat `TmSigningFormRunner`.
- [x] Implementovat CSS `_signing-form-runner.css`.
- [x] GREEN: shell testy projdou.

### 10.3 Navigace kroků

- [x] RED: next uloží aktuální hodnotu.
- [x] RED: previous se vrátí na předchozí krok.
- [x] RED: klik na overlay přepne current step.
- [x] RED: skip optional field.
- [x] RED: go to first invalid required field.
- [x] Implementovat step navigation.
- [x] GREEN: navigation testy projdou.

### 10.4 Autosave a callbacky

- [x] RED: změna hodnoty vyvolá `ValuesChanged`.
- [x] RED: submit step vyvolá `OnStepSubmit`.
- [x] RED: autosave debounce.
- [x] RED: autosave error zobrazí stav.
- [x] RED: loading state blokuje další submit.
- [x] Implementovat autosave.
- [x] GREEN: autosave testy projdou.

### 10.5 Complete flow

- [x] RED: complete button disabled, pokud required field chybí.
- [x] RED: complete button aktivní, pokud vše required vyplněno.
- [x] RED: complete vyvolá `OnComplete`.
- [x] RED: complete loading state.
- [x] RED: complete error state.
- [x] Implementovat complete flow.
- [x] GREEN: complete testy projdou.

### 10.6 Mobile bottom panel

- [x] RED: `MobilePanelMode=Collapsed` zobrazí start/continue/sign now button.
- [x] RED: expand zobrazí step panel.
- [x] RED: minimize panel.
- [x] RED: panel nepřekryje fixed complete target při zadaném containeru.
- [x] Implementovat mobile panel.
- [x] GREEN: mobile panel testy projdou.

### 10.7 Accessibility mode

- [x] RED: renderuje screen-reader entry button.
- [x] RED: accessibility mode renderuje lineární seznam polí.
- [x] RED: focus field posune current step.
- [x] RED: ARIA labels pro progress.
- [x] Implementovat accessibility mode.
- [x] GREEN: accessibility testy projdou.

### 10.8 Demo a E2E

- [x] Přidat demo "Signing ceremony".
- [x] RED E2E: vytvořit `SigningFormRunnerE2ETests.cs`.
- [x] E2E: projde celý signing flow text/date/signature.
- [x] E2E: required field blokuje complete.
- [x] E2E: condition skryje/zobrazí pole.
- [x] E2E: formula vypočte hodnotu.
- [x] E2E: mobile flow expand/minimize.
- [x] E2E: accessibility mode focusuje pole.
- [x] GREEN E2E.

### 10.9 Ověření fáze 10

- [x] `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmSigningFormRunnerTests|FullyQualifiedName~SigningStepPlannerTests|FullyQualifiedName~SigningLocalizationTests" --no-restore` prošlo: 222/222.
- [x] `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~SigningFormRunnerE2ETests"` prošlo proti WASM demu na `https://localhost:7106`: 5/5.
- [x] Port `7106` byl před E2E uvolněn a demo po ověření zastaveno.
- [x] `dotnet build TempoBlazor.slnx --no-restore` prošel bez chyb; zůstávají existující warningy v projektu.

## Fáze 11: Dokončovací produktové komponenty

### 11.1 `TmSigningCompletionPanel`

- [x] RED: vytvořit unit testy.
- [x] RED: renderuje completed message.
- [x] RED: renderuje download button.
- [x] RED: renderuje send copy button.
- [x] RED: renderuje custom action button.
- [x] RED: podporuje waiting-for-others state.
- [x] Implementovat komponentu.
- [x] Přidat demo.
- [x] Přidat E2E complete panel scénář.

### 11.2 `TmSubmissionStatusTimeline`

- [x] RED: vytvořit unit testy.
- [x] RED: mapuje event type na label/icon/severity.
- [x] RED: renderuje sent/opened/completed/declined.
- [x] RED: renderuje email bounce/complaint.
- [x] RED: renderuje verification/KBA events.
- [x] RED: renderuje metadata detail.
- [x] Implementovat komponentu nad `TmTimeline` - finálně implementováno jako vlastní timeline markup kvůli event severity/metadata layoutu.
- [x] Přidat demo.
- [x] Přidat E2E timeline scénář.

### 11.3 `TmShareLinkPanel`

- [x] RED: vytvořit unit testy.
- [x] RED: renderuje link.
- [x] RED: renderuje `TmCopyButton`.
- [x] RED: renderuje `TmQRCode`.
- [x] RED: renderuje embed code.
- [x] RED: renderuje enable/disable toggle.
- [x] RED: renderuje expiration info.
- [x] Implementovat komponentu.
- [x] Přidat demo.
- [x] Přidat E2E copy/QR scénář.

### 11.4 `TmPdfSignatureVerification`

- [x] RED: vytvořit unit testy.
- [x] RED: empty upload state.
- [x] RED: loading verification state.
- [x] RED: checksum verified state.
- [x] RED: checksum not found state.
- [x] RED: malformed PDF error.
- [x] RED: signatures list.
- [x] Implementovat provider-agnostic component API.
- [x] Přidat demo s mock verification providerem.
- [x] Přidat E2E verification scénář.

### 11.5 `TmAuditTrailViewer`

- [x] RED: vytvořit unit testy.
- [x] RED: renderuje document checksums.
- [x] RED: renderuje signer identity.
- [x] RED: renderuje IP/UA/timezone.
- [x] RED: renderuje verification method.
- [x] RED: renderuje audit PDF link.
- [x] Implementovat komponentu.
- [x] Přidat demo.
- [x] Přidat E2E audit viewer scénář.

### 11.6 Ověření fáze 11

- [x] `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmSigningCompletionPanelTests|FullyQualifiedName~TmSubmissionStatusTimelineTests|FullyQualifiedName~TmShareLinkPanelTests|FullyQualifiedName~TmPdfSignatureVerificationTests|FullyQualifiedName~TmAuditTrailViewerTests|FullyQualifiedName~SigningLocalizationTests" --no-restore` prošlo: 267/267.
- [x] `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` prošel bez warningů/chyb.
- [x] `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~SigningProductPanelsE2ETests" --no-build` prošlo proti WASM demu na `https://localhost:7106`: 5/5.
- [x] Port `7106` byl před E2E uvolněn a demo po ověření zastaveno.
- [x] `dotnet build TempoBlazor.slnx --no-restore` prošel bez chyb; zůstávají existující warningy v projektu.

## Fáze 12: Dokumentace a kvalita

### 12.1 Demo navigace a dokumentace

- [x] Přidat `SigningComponentsPage.razor` do navigace.
- [x] Přidat každou novou komponentu do `COMPONENTS.md`.
- [x] Přidat stručné příklady použití.
- [x] Přidat popis required JS souborů.
- [x] Přidat popis DTO modelů.
- [x] Přidat poznámku, které komponenty jsou provider-agnostic.

### 12.2 Accessibility review

- [x] Unit test: všechny icon-only buttony mají `aria-label`.
- [x] Unit test: field overlay invalid state používá `aria-invalid`.
- [x] Unit test: runner progress má `aria-label`.
- [x] Unit test: context menu je keyboard navigovatelné.
- [x] E2E: keyboard-only výběr field editor panelu.
- [x] E2E: signing form jde projít Tab/Enter.
- [x] E2E: mobile panel neblokuje focus.

### 12.3 Visual and responsive QA

- [x] E2E screenshot desktop pro designer.
- [x] E2E screenshot mobile pro designer.
- [x] E2E screenshot desktop pro signing runner.
- [x] E2E screenshot mobile pro signing runner.
- [x] Ověřit text nepřetéká v field labels.
- [x] Ověřit overlay je stabilní při zoom/resize.
- [x] Ověřit CSS nepoužívá jednu dominantní barevnou paletu mimo tokeny.

### 12.4 Test command checklist

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Signing"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmSignatureCapture"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmConditionBuilderTests"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~SigningFormulaHelperTests|FullyQualifiedName~TmFormulaBuilderTests"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmRecipientRoleEditorTests|FullyQualifiedName~SigningLocalizationTests"`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/`.
- [x] Spustit E2E signing subset.
- [x] Spustit `dotnet build TempoBlazor.slnx`.
- [x] Zapsat případné flaky testy a důvod. Aktuálně padají 5 unit testů mimo signing ve spreadsheet/formula bar oblasti (`TmSpreadsheetKeyboardTests`).

### 12.5 Ověření fáze 12

- [x] `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~SigningAccessibilityTests|FullyQualifiedName~TmContextMenuTests|FullyQualifiedName~TmSigningFieldOverlayTests|FullyQualifiedName~TmSigningFormRunnerTests" --no-restore` prošlo: 56/56.
- [x] `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` prošel bez warningů/chyb.
- [x] `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~SigningQualityE2ETests" --no-build` prošlo proti WASM demu na `https://localhost:7106`: 8/8.
- [x] `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Signing|FullyQualifiedName~TmContextMenuTests" --no-build` prošlo: 503/503.
- [x] Port `7106` byl před E2E uvolněn a demo po ověření zastaveno.
- [x] `dotnet build TempoBlazor.slnx --no-restore` prošel bez chyb; zůstávají existující warningy v projektu.

## Fáze 13: Stránkování a zoom pro dokumenty a PDF designer

### 13.1 Návrh API a kompatibilita

- [x] RED: přidat unit test, že `TmDocumentPageViewer` se bez nových parametrů chová stejně jako dřív.
- [x] RED: přidat unit test pro výchozí měřítko `Scale=1.0`.
- [x] RED: přidat unit test pro odmítnutí neplatného měřítka menšího než minimum.
- [x] RED: přidat unit test pro odmítnutí neplatného měřítka většího než maximum.
- [x] RED: přidat unit test pro `ScaleChanged`.
- [x] RED: přidat unit test pro nový view mód `DocumentPageViewMode.SinglePage`.
- [x] RED: přidat unit test pro nový view mód `DocumentPageViewMode.Continuous`.
- [x] RED: přidat unit test pro přepnutí view módu přes `ViewModeChanged`.
- [x] Přidat enum `DocumentPageViewMode` do signing komponent nebo abstractions podle existujícího patternu.
- [x] Přidat enum `DocumentPageZoomMode` s hodnotami `Custom`, `FitWidth`, `FitPage`.
- [x] Přidat veřejné parametry do `TmDocumentPageViewer` s XML dokumentací: `Scale`, `ScaleChanged`, `MinScale`, `MaxScale`, `ZoomMode`, `ZoomModeChanged`.
- [x] Přidat veřejné parametry do `TmDocumentPageViewer` s XML dokumentací: `ShowToolbar`, `ShowZoomControls`, `ShowPaginationControls`.
- [x] Zachovat stávající `Page`, `OverlayTemplate`, `ChildContent`, `OnPageClick` a `OnPageContextMenu` bez breaking změn.
- [x] GREEN: nové API testy projdou.
- [x] REFACTOR: sjednotit názvy s existujícím `TmPdfViewer` tam, kde to neplete PDF.js viewer a signing viewer.

### 13.2 Toolbar pro `TmDocumentPageViewer`

- [x] RED: unit test toolbar renderuje jen při `ShowToolbar=true`.
- [x] RED: unit test zoom out tlačítko má ikonu `zoom-out` a `aria-label`.
- [x] RED: unit test zoom in tlačítko má ikonu `zoom-in` a `aria-label`.
- [x] RED: unit test zoom label zobrazuje procenta.
- [x] RED: unit test tlačítko `Fit width` nastaví `ZoomMode=FitWidth`.
- [x] RED: unit test tlačítko `Fit page` nastaví `ZoomMode=FitPage`.
- [x] RED: unit test zoom out respektuje `MinScale`.
- [x] RED: unit test zoom in respektuje `MaxScale`.
- [x] RED: unit test toolbar nepřekrývá stránku ani overlay slot.
- [x] Přidat lokalizační klíče EN/CS pro zoom, fit width, fit page, page navigation a toolbar aria label.
- [x] Přidat klíče do `MockTmLocalizer`.
- [x] Implementovat toolbar s `TmButton` nebo konzistentním button markupem a `TmIcon`.
- [x] Implementovat zoom kroky `[0.5, 0.75, 1.0, 1.25, 1.5, 2.0]`.
- [x] Implementovat stav disabled pro krajní zoom hodnoty.
- [x] GREEN: toolbar unit testy projdou.
- [x] REFACTOR: zkontrolovat, že icon-only buttony mají dostupný název.

### 13.3 Zoom renderování stránky

- [x] RED: unit test přidá CSS custom property s aktuálním měřítkem.
- [x] RED: unit test šířka stránky reaguje na `Scale`.
- [x] RED: unit test aspect ratio stránky zůstává zachované při zoomu.
- [x] RED: unit test overlay dostane stejný transform/rozměr jako dokument.
- [x] RED: unit test normalizované souřadnice overlaye zůstanou beze změny při zoomu.
- [x] RED: unit test klik na stránku vrací stále správný page context.
- [x] Implementovat CSS pro zoom bez změny datových souřadnic polí.
- [x] Implementovat scroll container, aby zoom nad 100 % nevytlačil okolní layout mimo stránku.
- [x] Implementovat responzivní chování toolbaru na mobilu.
- [x] GREEN: zoom render testy projdou.
- [x] REFACTOR: odstranit případné duplikované styly mezi viewerem a designerem.

### 13.4 Fit width a fit page měření

- [x] RED: unit test bez JS fallbacku nechá `FitWidth` na bezpečném scale.
- [x] RED: unit test bez JS fallbacku nechá `FitPage` na bezpečném scale.
- [x] RED: JS interop test ověří volání měření containeru při `FitWidth`.
- [x] RED: JS interop test ověří volání měření containeru při `FitPage`.
- [x] Rozšířit nebo znovu použít `src/Tempo.Blazor/wwwroot/js/pdf-template-designer.js` pro měření viewer containeru.
- [x] Implementovat výpočet fit width z dostupné šířky a reálného poměru stránky.
- [x] Implementovat výpočet fit page z dostupné šířky i výšky.
- [x] Ošetřit prerendering a bUnit fallback přes `JSException`/`InvalidOperationException`.
- [x] Ošetřit resize: při změně velikosti containeru přepočítat fit zoom bez posunu polí.
- [x] GREEN: fit mode testy projdou.
- [x] REFACTOR: zvážit sdílený JS helper název mimo `pdf-template-designer.js`, pokud ho začne používat i `TmDocumentPageViewer`.

### 13.5 Stránkování pro více stránek

- [x] RED: unit test nový wrapper nebo designer renderuje v `SinglePage` módu pouze aktuální stránku.
- [x] RED: unit test `Continuous` mód renderuje všechny stránky.
- [x] RED: unit test next page přepne na další stránku.
- [x] RED: unit test previous page přepne na předchozí stránku.
- [x] RED: unit test page label zobrazí `1 / N`.
- [x] RED: unit test předchozí je disabled na první stránce.
- [x] RED: unit test další je disabled na poslední stránce.
- [x] RED: unit test při odstranění stránky mimo rozsah se current page clampne.
- [x] RED: unit test vybrané pole na jiné stránce přepne aktuální stránku v designeru.
- [x] Rozhodnout, zda stránkování patří přímo do `TmPdfTemplateDesigner`, nebo vznikne malý `TmDocumentPager` nad `TmDocumentPageViewer`.
- [x] Implementovat interní `CurrentPageIndex` v `TmPdfTemplateDesigner`.
- [x] Implementovat `PageIndex`/`PageIndexChanged` veřejné API jen pokud bude užitečné pro aplikace.
- [x] Implementovat `ViewMode`/`ViewModeChanged` v `TmPdfTemplateDesigner`.
- [x] Zachovat možnost `Continuous` pro kontrolní režim a zpětnou kompatibilitu.
- [x] GREEN: stránkovací unit testy projdou.

### 13.6 Integrace do `TmPdfTemplateDesigner`

- [x] RED: unit test designer toolbar obsahuje navigaci stránky a zoom ovládání.
- [x] RED: unit test designer defaultně používá `SinglePage`.
- [x] RED: unit test drag/drop z palety vytvoří pole na aktuálně zobrazené stránce.
- [x] RED: unit test kreslení pole vytvoří oblast na aktuálně zobrazené stránce.
- [x] RED: unit test context paste vloží pole na aktuálně zobrazenou stránku.
- [x] RED: unit test copy/paste zachová relativní pozice výběru po vložení.
- [x] RED: unit test Delete smaže vybrané pole i po přepnutí stránky.
- [x] RED: unit test výběr pole v pravém panelu nepřepočítá souřadnice po zoomu.
- [x] Přesunout existující toolbar akce designeru tak, aby s navigací a zoomem tvořily jeden klidný pracovní toolbar.
- [x] Upravit layout designeru tak, aby stránkovací toolbar nebyl v kolizi s levým palette panelem.
- [x] Ujistit se, že levý panel polí zůstává použitelný i při zoomu nad 100 %.
- [x] Ujistit se, že pravý settings panel zůstává použitelný i při fit page.
- [x] GREEN: designer integrační unit testy projdou.
- [x] REFACTOR: sjednotit helpery pro převod client souřadnic na normalizované souřadnice.

### 13.7 Demo stránka

- [x] Přidat demo kontrolky pro `SinglePage`/`Continuous`.
- [x] Přidat demo kontrolky pro zoom in/out, fit width a fit page.
- [x] Přidat demo s vícestránkovým dokumentem v `Document Page Viewer`.
- [x] Upravit demo `PDF Template Designer`, aby ukázalo stránkování jako výchozí režim.
- [x] Přidat krátký status text do dema jen tam, kde už demo status texty používá.
- [x] Zkontrolovat desktop viewport 1366x768.
- [x] Zkontrolovat mobile viewport 390x844.
- [x] Zkontrolovat, že žádný text nepřetéká z toolbaru.

### 13.8 E2E testy pro viewer

- [x] RED E2E: `SigningDocumentPageViewerE2ETests` ověří zoom in zvětší stránku.
- [x] RED E2E: zoom out stránku zmenší.
- [x] RED E2E: fit width vyplní dostupnou šířku bez horizontálního přetečení rootu.
- [x] RED E2E: fit page udrží celou stránku viditelnou v pracovním prostoru.
- [x] RED E2E: continuous mód zobrazí více stránek pod sebou.
- [x] RED E2E: single page mód zobrazí jen aktuální stránku.
- [x] RED E2E: next/previous mění aktuální stránku.
- [x] RED E2E: overlay zůstane zarovnaný při 75 %, 100 % a 150 %.
- [x] E2E musí běžet proti `https://localhost:7106`.
- [x] Před E2E uvolnit port `7106`, pokud na něm běží cizí proces.
- [x] GREEN E2E viewer testy projdou.

### 13.9 E2E testy pro PDF Template Designer

- [x] RED E2E: designer se otevře v single page módu.
- [x] RED E2E: next page zobrazí druhou stránku a nezobrazí první stránku v canvasu.
- [x] RED E2E: drag/drop z levého panelu vloží pole na druhou stránku.
- [x] RED E2E: kreslení pole na druhé stránce vytvoří oblast s `Page=1`.
- [x] RED E2E: zoom in nezmění normalizovanou pozici existujícího pole.
- [x] RED E2E: zoom out nezmění normalizovanou pozici existujícího pole.
- [x] RED E2E: fit width ponechá overlay přesně nad dokumentem.
- [x] RED E2E: context menu copy/paste funguje po zoomu a vloží pole na místo pravého kliknutí.
- [x] RED E2E: Delete smaže vybrané pole po zoomu.
- [x] RED E2E: mobile viewport zobrazí page navigation i zoom bez překryvu.
- [x] GREEN E2E designer testy projdou.

### 13.10 Accessibility a klávesnice

- [x] RED: unit test page navigation tlačítka mají `aria-label`.
- [x] RED: unit test zoom label má `role=status` nebo jiné vhodné live oznámení.
- [x] RED: unit test toolbar má `role=toolbar` a popisek.
- [x] RED: unit test view mode přepínač má dostupný název.
- [x] E2E: Tab projde page navigation, zoom a designer canvas v logickém pořadí.
- [x] E2E: Enter/Space aktivuje zoom tlačítka.
- [x] E2E: klávesa Delete dál maže označené pole.
- [x] E2E: po změně stránky zůstane focus v toolbaru nebo se přesune předvídatelně.
- [x] Ověřit kontrast aktivního view módu v light i dark mode.

### 13.11 Dokumentace a komponentový katalog

- [x] Doplnit `COMPONENTS.md` o nové parametry `TmDocumentPageViewer`.
- [x] Doplnit `COMPONENTS.md` o stránkování a zoom v `TmPdfTemplateDesigner`.
- [x] Doplnit README nebo signing sekci, pokud tam existuje ukázka signing komponent.
- [x] Doplnit poznámku o tom, že data polí zůstávají v normalizovaných souřadnicích.
- [x] Doplnit poznámku o required JS helperu, pokud vznikne nový JS soubor.
- [x] Ověřit, že všechny nové public parametry mají XML dokumentaci.

### 13.12 Ověření fáze 13

- [x] `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmDocumentPageViewerTests|FullyQualifiedName~TmPdfTemplateDesignerTests|FullyQualifiedName~SigningLocalizationTests" --no-restore` prošlo: 324/324.
- [x] `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Signing" --no-restore` prošlo: 540/540.
- [x] `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` prošel bez warningů/chyb.
- [x] `dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~SigningDocumentPageViewerE2ETests|FullyQualifiedName~PdfTemplateDesignerE2ETests" --no-restore` prošlo proti WASM demu na `https://localhost:7106`: 19/19.
- [x] Port `7106` byl před E2E uvolněn a po ověření demo zastaveno.
- [x] `dotnet build TempoBlazor.slnx --no-restore` prošel bez chyb; zůstávají existující warningy v projektu.
- [x] Zapsat případné existující warningy nebo flaky testy, pokud nesouvisí s fází 13 - žádný nový flaky test nezjištěn.

## Fáze 14: Vícejazyčná podpisová vrstva

### 14.1 Analýza problému a cílový model

#### Kontext

Šablona PDF může být připravena v jednom jazyce, ale podepisující může používat jiný jazyk. Nejde jen o kosmetiku. Podepisující potřebuje rozumět ovládání, požadavkům polí, chybám validace a možnostem výběru. Zároveň je právně rizikové automaticky překládat samotný obsah PDF, protože pak není jasné, kterou textovou verzi podepisující skutečně odsouhlasil.

#### Základní produktové rozhodnutí

PDF dokument považujeme za pevný právní artefakt. Knihovna nemá automaticky překládat text dokumentu, obrázek stránky ani texty, které jsou součástí PDF. Vícejazyčnost se má řešit v podpisové vrstvě nad dokumentem:

- UI podpisové ceremonie.
- Popisky polí.
- Nadpisy polí.
- Popisy a nápovědy polí.
- Placeholdery.
- Validace a chybové zprávy.
- Možnosti select/radio/multiple polí.
- Texty KBA/ověřovacích placeholderů.
- Stavové texty a instrukce runneru.
- Auditní metadata o tom, jaký jazyk uživatel viděl.

#### Co má zůstat stabilní a nepřekládané

- `SigningField.Uuid`.
- `SigningFieldOption.Uuid`.
- Interní `SigningField.Name`, pokud ho aplikace používá jako technický identifikátor.
- Hodnoty ukládané do backendu pro automatizaci, pravidla a integrace.
- Výrazy, podmínky a formule, pokud odkazují na stabilní identifikátory.
- Skutečný PDF obsah.

#### Co má být lokalizovatelné

- Uživatelský label pole.
- Krátký title pole.
- Delší description/help text.
- Placeholder pro textové, číselné, datumové, telefonní a signing vstupy.
- Validace `Message`.
- Option display text pro select/radio/multiple.
- Text nápovědy pro typy `Verification`, `Kba`, `Payment`.
- Popisek stránky `SigningDocumentPage.Label`, pokud je viditelný nebo čtený screen readerem.
- Role names v recipient/role editorech, pokud jsou zobrazené podepisujícímu.

#### Doporučený datový model

Přidat jednoduchý lokalizační model v abstractions, například `SigningLocalizedText`, který drží fallback a slovník překladů podle culture kódu:

- `Default`: výchozí text, zpětně kompatibilní fallback.
- `Translations`: `Dictionary<string, string>` pro hodnoty jako `cs`, `en`, `de`, případně `cs-CZ`.

K tomu přidat resolver, který umí fallback pořadí:

1. přesná culture, například `de-DE`,
2. neutrální culture, například `de`,
3. zadaná fallback culture šablony,
4. `Default`,
5. původní legacy string (`Name`, `Title`, `Description`, `Option.Value`, `Validation.Message`),
6. poslední technický fallback podle typu pole nebo lokalizace knihovny.

#### Doporučené API

Nejbezpečnější je nebourat existující API. Nové vlastnosti doplnit vedle stávajících stringů:

- `SigningField.Labels` nebo `LocalizedName`.
- `SigningField.Titles`.
- `SigningField.Descriptions`.
- `SigningField.Placeholders`.
- `SigningFieldValidation.Messages`.
- `SigningFieldOption.Labels`.
- `SigningDocumentPage.Labels`.
- `SigningSubmitterRole.Labels` nebo podobná lokalizace role.

Pro komponenty přidat jednotné parametry:

- `Culture`: konkrétní culture pro render podpisové vrstvy.
- `FallbackCulture`: culture šablony nebo výchozí jazyk.
- `TextResolver`: volitelná služba/delegát pro aplikace, které chtějí překlady brát z DB nebo vlastního systému.

#### Audit a právní čitelnost

Při podepsání by runner měl umožnit aplikaci uložit:

- culture použitou v podpisové ceremonii,
- fallback culture šablony,
- resolved labely polí viditelné v době podpisu,
- resolved option labely viditelné v době podpisu,
- validace/chybové texty není nutné ukládat vždy, ale je vhodné umožnit snapshot,
- informaci, že PDF obsah nebyl přeložen knihovnou.

Tím se dá později doložit, že uživatel viděl například anglické ovládání a anglické popisky polí, ale podepisoval český PDF dokument.

#### UX pravidla

- Designer musí jasně oddělit interní název pole od textu pro podepisujícího.
- Designer má umožnit editaci překladů bez nutnosti měnit technický identifikátor pole.
- Runner má vybrat jazyk podle parametru, nikoliv podle toho, v jakém jazyce byla šablona vytvořena.
- Když překlad chybí, UI má spadnout na fallback bez prázdných labelů.
- Chybějící překlady v designeru mají být viditelné jako upozornění, ne jako chyba blokující práci.
- Automatický překlad může být v budoucnu nadstavba aplikace, ale knihovna ho nemá dělat implicitně.

#### Rizika

- Záměna `Value` a labelu u options může rozbít podmínky a integrace.
- Překlad `Name` bez stabilního identifikátoru může rozbít formule.
- Bez auditního snapshotu nejde zpětně přesně říct, co podepisující viděl.
- Příliš složitý editor překladů může zhoršit UX pro jednoduché šablony.
- Culture fallback musí být deterministický, jinak budou E2E a audit nestabilní.

#### Ne-cíle fáze

- Nepřekládat samotné PDF stránky.
- Nedělat napojení na externí strojový překlad.
- Neřešit právní závaznost paralelních jazykových verzí dokumentu.
- Neměnit ukládací hodnoty polí jen kvůli lokalizovanému zobrazení.

### 14.2 Abstractions: lokalizovaný text

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Models/SigningLocalizedTextTests.cs`.
- [x] RED: test prázdný `SigningLocalizedText` vrací prázdný string.
- [x] RED: test `Default` se použije bez culture.
- [x] RED: test přesná culture `cs-CZ` má přednost před neutrální `cs`.
- [x] RED: test neutrální culture `cs` se použije pro `cs-CZ`, když přesný překlad chybí.
- [x] RED: test fallback culture se použije před defaultem.
- [x] RED: test trimuje whitespace culture kódu.
- [x] RED: test je case-insensitive pro culture klíče.
- [x] RED: test serializace do JSON zachová `Default` i `Translations`.
- [x] Implementovat `src/Tempo.Blazor.Abstractions/Models/SigningLocalizedText.cs`.
- [x] Implementovat helper `SigningLocalizedTextResolver` nebo `SigningLocalizationResolver`.
- [x] Přidat XML dokumentaci pro všechny public členy.
- [x] GREEN: modelové testy projdou.
- [x] REFACTOR: zkontrolovat, že model nemá závislost na Blazoru ani `ITmLocalizer`.

### 14.3 Abstractions: lokalizace signing modelů

- [x] RED: test `SigningField` má zpětně kompatibilní `Name`, `Title`, `Description`.
- [x] RED: test `SigningField` umí resolved label z lokalizovaných hodnot.
- [x] RED: test `SigningField` spadne na legacy `Name`, když lokalizovaný label chybí.
- [x] RED: test `SigningField` resolved title spadne na legacy `Title`.
- [x] RED: test `SigningField` resolved description spadne na legacy `Description`.
- [x] RED: test `SigningField` placeholder podporuje culture fallback.
- [x] RED: test `SigningFieldOption` resolved label spadne na `Value`.
- [x] RED: test `SigningFieldOption.Value` zůstává stabilní při změně culture.
- [x] RED: test `SigningFieldValidation` resolved message spadne na legacy `Message`.
- [x] RED: test `SigningDocumentPage` resolved label spadne na legacy `Label`.
- [x] RED: test role model umí lokalizovaný display name, pokud role model lokalizaci podporuje.
- [x] Přidat `SigningField.Labels` nebo obdobnou vlastnost.
- [x] Přidat `SigningField.Titles`.
- [x] Přidat `SigningField.Descriptions`.
- [x] Přidat `SigningField.Placeholders`.
- [x] Přidat `SigningFieldOption.Labels`.
- [x] Přidat `SigningFieldValidation.Messages`.
- [x] Přidat `SigningDocumentPage.Labels`.
- [x] Zvážit lokalizaci role names podle existujícího `SigningSubmitterRole`.
- [x] GREEN: testy modelů projdou.
- [x] REFACTOR: neodstraňovat ani nepřejmenovávat legacy string vlastnosti.

### 14.4 Resolver služba pro komponenty

- [x] RED: vytvořit `tests/Tempo.Blazor.Tests/Components/Signing/SigningTextResolverTests.cs`.
- [x] RED: test resolver vrátí label pole pro `Culture=cs`.
- [x] RED: test resolver vrátí label pole pro `Culture=en`.
- [x] RED: test resolver vrátí fallback, když překlad chybí.
- [x] RED: test resolver použije `ITmLocalizer` pro poslední fallback typového labelu.
- [x] RED: test option label zachová option value jako submit hodnotu.
- [x] RED: test validace vrací lokalizovanou zprávu.
- [x] Implementovat interní resolver v `src/Tempo.Blazor/Components/Signing/`.
- [x] Přidat veřejný delegát nebo interface jen pokud je nutný pro vlastní resolver aplikace - není potřeba, interní resolver stačí a veřejné API zůstává přes modely/parametry.
- [x] Přidat `Culture` a `FallbackCulture` parametry do komponent jen tam, kde resolver používají.
- [x] Přidat XML dokumentaci pro nové parametry.
- [x] GREEN: resolver unit testy projdou.
- [x] REFACTOR: nepřidávat duplicity resolver logiky do každé komponenty zvlášť.

### 14.5 `TmSigningFieldOverlay`

- [x] RED: test overlay label renderuje český překlad při `Culture=cs`.
- [x] RED: test overlay label renderuje anglický překlad při `Culture=en`.
- [x] RED: test overlay required aria label používá lokalizaci knihovny a localized field label.
- [x] RED: test image/signature/stamp placeholder respektuje localized label.
- [x] RED: test choice preview používá localized option label.
- [x] RED: test choice preview submit value zůstává option `Value`.
- [x] Přidat `Culture` a `FallbackCulture` parametry.
- [x] Napojit overlay na společný resolver.
- [x] Zachovat layout a truncation chování dlouhých textů.
- [x] GREEN: overlay unit testy projdou.
- [x] REFACTOR: odstranit přímé používání `Field.Name` tam, kde jde o text pro uživatele.

### 14.6 Signing step komponenty

- [x] RED: `TmSigningStepShell` renderuje localized label.
- [x] RED: `TmSigningStepShell` renderuje localized description/help text.
- [x] RED: `TmSigningTextStep` používá localized placeholder.
- [x] RED: `TmSigningNumberStep` používá localized placeholder.
- [x] RED: `TmSigningDateStep` používá localized placeholder nebo fallback.
- [x] RED: `TmSigningPhoneStep` používá localized placeholder.
- [x] RED: `TmSigningChoiceStep` renderuje localized option labels.
- [x] RED: `TmSigningChoiceStep` u selectu ukládá stabilní option value/uuid.
- [x] RED: `TmSigningChoiceStep` u radio ukládá stabilní option value/uuid.
- [x] RED: `TmSigningChoiceStep` u multiple ukládá stabilní hodnoty bez překladu.
- [x] RED: `TmSigningAttachmentStep` používá localized label a help text.
- [x] RED: `TmSigningExternalStep` používá localized title/description pro KBA/Verification/Payment.
- [x] RED: required validace použije localized validation message, pokud je nastavena.
- [x] RED: required validace použije `ITmLocalizer`, když localized message chybí.
- [x] Přidat culture parametry do step komponent podle potřeby.
- [x] Napojit všechny step komponenty na resolver.
- [x] GREEN: unit testy step komponent projdou.
- [x] REFACTOR: sjednotit fallbacky mezi step shell a konkrétními kroky.

### 14.7 `TmSigningFormRunner`

- [x] RED: runner předá `Culture` do aktuální step komponenty.
- [x] RED: runner předá `FallbackCulture` do aktuální step komponenty.
- [x] RED: runner použije localized label ve step summary/progress, pokud ho zobrazuje.
- [x] RED: runner validace required použije localized field validation message.
- [x] RED: runner callback autosave dostane stabilní hodnoty, ne localized labely.
- [x] RED: runner complete callback umí obsahovat `SigningCulture`.
- [x] RED: runner při změně culture přerenderuje labely bez ztráty rozpracovaných hodnot.
- [x] RED: runner umí zobrazit language selector, pokud má více než jednu podporovanou culture.
- [x] RED: language selector je skrytý, pokud `ShowLanguageSelector=false`.
- [x] RED: language selector je skrytý, pokud je dostupná jen jedna culture.
- [x] RED: změna jazyka přes selector vyvolá `CultureChanged`.
- [x] RED: změna jazyka přes selector přerenderuje aktuální step bez ztráty hodnoty.
- [x] RED: změna jazyka přes selector nezmění aktuální krok.
- [x] RED: vybraná culture ze selectoru se propíše do localization snapshotu.
- [x] Přidat parametr `Culture`.
- [x] Přidat parametr `CultureChanged`.
- [x] Přidat parametr `FallbackCulture`.
- [x] Přidat parametr `SupportedCultures`.
- [x] Přidat parametr `ShowLanguageSelector`.
- [x] Navrhnout language selector jako malý control v runner headeru nebo v mobile bottom panelu.
- [x] Umožnit aplikaci řídit výběr jazyka zvenku přes bindable `Culture`.
- [x] Zvážit nový model `SigningSubmissionLocalizationSnapshot`.
- [x] Zvážit nový callback `OnLocalizationSnapshotChanged` nebo rozšíření completion payloadu.
- [x] Implementovat snapshot resolved labels pro audit jako volitelné API.
- [x] GREEN: runner unit testy projdou.
- [x] REFACTOR: držet interní values nezávisle na culture.

### 14.8 `TmSigningFieldEditorPanel`

- [x] RED: editor zobrazí technický název odděleně od labelu pro podepisujícího.
- [x] RED: editor zobrazí defaultní jazyk šablony.
- [x] RED: editor umožní zadat překlad labelu pro `cs`.
- [x] RED: editor umožní zadat překlad labelu pro `en`.
- [x] RED: editor umožní zadat překlad title.
- [x] RED: editor umožní zadat překlad description.
- [x] RED: editor umožní zadat překlad placeholderu.
- [x] RED: editor umožní zadat překlad validation message.
- [x] RED: editor umožní zadat překlad option labelu.
- [x] RED: editor upozorní na chybějící překlad pro aktivní jazyk.
- [x] RED: editor změna překladu nemění `Uuid`.
- [x] RED: editor změna option labelu nemění `Option.Value`, pokud je oddělený od labelu.
- [x] Navrhnout UX pro jazykové záložky nebo compact language switcher.
- [x] Implementovat editaci lokalizovaných textů pro pole.
- [x] Implementovat editaci lokalizovaných textů pro options.
- [x] Implementovat chybějící překlady jako non-blocking warning.
- [x] GREEN: editor unit testy projdou.
- [x] REFACTOR: zkontrolovat, že panel v pravém sloupci nepřetéká v desktop ani mobile layoutu.

### 14.9 `TmPdfTemplateDesigner`

- [x] RED: designer předá culture do overlayů.
- [x] RED: designer předá culture do right settings panelu.
- [x] RED: designer toolbar obsahuje přepínač náhledu jazyka, pokud je povolen.
- [x] RED: změna preview jazyka přerenderuje overlay labely.
- [x] RED: změna preview jazyka nezmění souřadnice polí.
- [x] RED: změna preview jazyka nezmění selected field.
- [x] RED: nové pole dostane legacy fallback label podle typu pole.
- [x] RED: nové pole může mít default localized label v aktuálním preview jazyce.
- [x] RED: drag/drop pole z palety funguje i po změně culture.
- [x] Přidat parametry `Culture`, `FallbackCulture`.
- [x] Přidat parametry `SupportedCultures`, `ShowCulturePreview`.
- [x] Implementovat jazykový preview control v designeru.
- [x] Napojit overlay, editor panel a případné property summary na resolver.
- [x] GREEN: designer unit testy projdou.
- [x] REFACTOR: ponechat interní field ids a condition references nezávislé na lokalizaci.

### 14.10 `TmConditionBuilder` a formule

- [x] RED: condition builder seznam polí zobrazuje localized label.
- [x] RED: condition builder pořád ukládá `FieldUuid`.
- [x] RED: condition builder option value editor zobrazuje localized option label.
- [x] RED: condition builder option value editor ukládá stabilní option uuid/value.
- [x] RED: formula builder zobrazuje localized field label v pickeru.
- [x] RED: formula expression používá stabilní token, ne localized label.
- [x] RED: změna culture nezmění existující podmínky ani formule.
- [x] Přidat culture parametry do condition/formula builderu podle potřeby.
- [x] Napojit seznamy polí a možností na resolver.
- [x] GREEN: condition/formula unit testy projdou.
- [x] REFACTOR: doplnit helper pro display label v pickerech, aby nebyl duplicitní.

### 14.11 Auditní a produktové komponenty

- [x] RED: audit viewer umí zobrazit culture podpisové ceremonie.
- [x] RED: audit viewer umí zobrazit fallback culture šablony.
- [x] RED: audit viewer umí zobrazit resolved field label snapshot, pokud je k dispozici.
- [x] RED: completion panel nepřekládá uložené hodnoty, jen UI texty.
- [x] RED: PDF verification komponenty zůstávají nezávislé na field localization.
- [x] Přidat model pro lokalizační audit snapshot do abstractions, pokud bude potřeba.
- [x] Přidat zobrazení culture informací do `TmAuditTrailViewer`.
- [x] GREEN: audit/product unit testy projdou.
- [x] REFACTOR: držet audit snapshot volitelný, aby stávající aplikace nemusely měnit data.

### 14.12 Demo scénáře

- [x] Přidat do `SigningComponentsPage.razor` vícejazyčný dokumentový scénář.
- [x] Přidat pole s českým fallbackem a anglickým překladem.
- [x] Přidat pole s anglickým fallbackem a českým překladem.
- [x] Přidat select/radio/multiple s lokalizovanými option labely a stabilními hodnotami.
- [x] Přidat validaci s lokalizovanou message.
- [x] Přidat ukázku chybějícího překladu s fallbackem.
- [x] Přidat preview přepínač `cs`/`en` v designer sekci.
- [x] Přidat runner scénář, kde podepisující vidí jiný jazyk než šablona.
- [x] Přidat runner language selector, kde podepisující může změnit jazyk během podpisu.
- [x] Přidat ukázku, že změna jazyka v runneru neztratí vyplněné hodnoty.
- [x] Přidat audit ukázku culture snapshotu.
- [x] Zkontrolovat desktop viewport 1366x768.
- [x] Zkontrolovat mobile viewport 390x844.
- [x] Zkontrolovat, že dlouhé německé/anglické labely nepřetékají.

### 14.13 E2E testy

- [x] RED E2E: designer zobrazí české labely při `cs`.
- [x] RED E2E: designer zobrazí anglické labely po přepnutí na `en`.
- [x] RED E2E: změna jazyka v designeru nezmění pozici pole.
- [x] RED E2E: field editor uloží český překlad labelu.
- [x] RED E2E: field editor uloží anglický překlad labelu.
- [x] RED E2E: select preview zobrazí celé localized option labely bez přetečení.
- [x] RED E2E: runner zobrazí anglický label nad českým PDF.
- [x] RED E2E: runner required validace zobrazí localized validation message.
- [x] RED E2E: runner language selector přepne labely z `cs` na `en`.
- [x] RED E2E: runner language selector po přepnutí zachová vyplněný text a vybranou option.
- [x] RED E2E: runner language selector nepřepne aktuální krok ani page.
- [x] RED E2E: runner submit/autosave zachová stabilní option value.
- [x] RED E2E: condition builder po změně jazyka stále drží vybraný `FieldUuid`.
- [x] RED E2E: audit viewer zobrazí culture použitou při podpisu.
- [x] E2E musí běžet proti `https://localhost:7106`.
- [x] Před E2E uvolnit port `7106`, pokud na něm běží cizí proces.
- [x] Po E2E demo zastavit.
- [x] GREEN E2E multilingual testy projdou.

### 14.14 Accessibility a i18n kvalita

- [x] RED: localized label se propíše do accessible name pole.
- [x] RED: localized validation message se propíše do `aria-describedby`.
- [x] RED: language switcher má dostupný název.
- [x] RED: chybějící překlad warning má čitelný text pro screen reader.
- [x] E2E: klávesnice ovládá language switcher.
- [x] E2E: screen-reader relevantní texty nepoužívají prázdné fallbacky.
- [x] Ověřit dlouhá slova bez mezer.
- [x] Ověřit diakritiku v českých překladech.
- [x] Ověřit `cs-CZ` fallback na `cs`.
- [x] Ověřit neznámá culture spadne na default.

### 14.15 Dokumentace

- [x] Doplnit `COMPONENTS.md` o signing localization model.
- [x] Doplnit příklad `SigningLocalizedText`.
- [x] Doplnit příklad lokalizovaných options.
- [x] Doplnit příklad runneru s `Culture` a `FallbackCulture`.
- [x] Doplnit poznámku, že PDF obsah se automaticky nepřekládá.
- [x] Doplnit doporučení pro audit snapshot.
- [x] Doplnit migration poznámku: legacy `Name`, `Title`, `Description`, `Option.Value`, `Validation.Message` zůstávají fallback.
- [x] Doplnit varování, že formule a podmínky mají používat stabilní uuid/tokeny.

### 14.16 Ověření fáze 14

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~SigningLocalizedText|FullyQualifiedName~SigningTextResolver" --no-restore` - pokryto targeted sadou, prošlo 68/68.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmSigningFieldOverlayTests|FullyQualifiedName~TmSigningStepShellTests|FullyQualifiedName~TmSigningChoiceStepTests|FullyQualifiedName~TmSigningFormRunnerTests" --no-restore` - prošlo 68/68.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmSigningFieldEditorPanelTests|FullyQualifiedName~TmPdfTemplateDesignerTests|FullyQualifiedName~TmConditionBuilderTests|FullyQualifiedName~TmFormulaBuilderTests" --no-restore` - prošlo 100/100.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~Signing" --no-restore` - prošlo 612/612.
- [x] Spustit `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` - prošlo bez warningů.
- [x] Spustit E2E multilingual subset proti `https://localhost:7106` - prošlo 10/10 (`SigningLocalizationE2ETests`).
- [x] Spustit `dotnet build TempoBlazor.slnx --no-restore` - prošlo s existujícími warningy.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore` - signing část po úpravě timing testu prošla 612/612; celý běh spadl na existujících notification/spreadsheet testech mimo fázi 14.
- [x] Zapsat případné existující warningy nebo flaky testy, pokud nesouvisí s fází 14: `dotnet build TempoBlazor.slnx --no-restore` prošel, ale repo má existující warningy mimo tuto fázi.

## Fáze 15: Komentáře nad `TmDocumentPageViewer`

> Stav po dokončení fáze: hotové je provider-agnostic jádro pro komentáře nad `TmDocumentPageViewer` včetně modelů, layeru, markerů, panelu rozděleného na menší komponenty, composeru, mentionů, reakcí, oprávnění, page-level threadů, fallback comment toolbaru bez hlavního toolbaru, vícestránkového demo dokumentu, stránkové navigace z thread panelu, zrušení výběru klikem do stránky, keyboard mention/marker flow, pending/disabled stavů, Escape cancel, mobilního sticky panelu a cílených bUnit/E2E/regresních testů. Záměrně zůstávají jako navazující rozšíření text-selection anchoring nad skutečnou PDF textovou vrstvou, continuous multi-page rendering v jedné scroll ploše, přímá integrace do designeru/runneru, samostatný obecný emoji picker mimo signing namespace a širší screenshot/accessibility matice.

### 15.1 Produktové rozhodnutí a hranice scope

- [x] Popsat cíl fáze: komentáře jsou review vrstva nad dokumentem, ne součást podepsaného PDF obsahu.
- [x] Potvrdit první podporované místo integrace: pouze `TmDocumentPageViewer`.
- [x] Zapsat non-goal: zatím nenapojovat komentáře do `TmPdfTemplateDesigner`.
- [x] Zapsat non-goal: zatím nenapojovat komentáře do `TmSigningFormRunner`.
- [x] Zapsat non-goal: zatím nedělat stabilní text selection anchoring nad PDF textovou vrstvou.
- [x] Zapsat non-goal: komponenta neposílá notifikace sama, jen emituje callbacky.
- [x] Rozhodnout výchozí typy kotev: bod na stránce, oblast na stránce, celostránkový komentář.
- [x] Rozhodnout, že souřadnice kotev budou normalizované vůči stránce `0..1`.
- [x] Rozhodnout, že resolved vlákna budou defaultně skrytá nebo utlumená podle parametru.
- [x] Rozhodnout, jestli prázdný draft komentáře po zavření zahodit.
- [x] Zapsat UX pravidlo: comment režim nesmí rozbít běžné klikání, zoom ani stránkování vieweru.
- [x] Zapsat UX pravidlo: vytvoření komentáře musí mít jasnou vizuální zpětnou vazbu.
- [x] Zapsat UX pravidlo: resolved stav musí být vratný přes reopen.
- [x] Zapsat auditní pravidlo: editace, resolve a reopen mají nést metadata, pokud je aplikace poskytne.

### 15.2 Abstractions: modely komentářů

- [x] RED: `DocumentCommentAnchor` umí reprezentovat bodovou kotvu.
- [x] RED: `DocumentCommentAnchor` umí reprezentovat oblastní kotvu.
- [x] RED: `DocumentCommentAnchor` umí reprezentovat celostránkovou kotvu.
- [x] RED: `DocumentCommentAnchor` validuje `PageNumber >= 1`.
- [x] RED: `DocumentCommentAnchor` validuje normalizované souřadnice v rozsahu `0..1`.
- [x] RED: `DocumentCommentThread` obsahuje stabilní `Id`.
- [x] RED: `DocumentCommentThread` obsahuje `Anchor`.
- [x] RED: `DocumentCommentThread` obsahuje `Status`.
- [x] RED: `DocumentCommentThread` obsahuje seznam komentářů.
- [x] RED: `DocumentComment` obsahuje stabilní `Id`.
- [x] RED: `DocumentComment` obsahuje `AuthorId`, `AuthorName` a volitelný avatar.
- [x] RED: `DocumentComment` obsahuje text těla komentáře.
- [x] RED: `DocumentComment` obsahuje `CreatedAt`.
- [x] RED: `DocumentComment` obsahuje volitelné `EditedAt`.
- [x] RED: `DocumentComment` obsahuje kolekci mentionů.
- [x] RED: `DocumentComment` obsahuje kolekci reakcí.
- [x] RED: `DocumentCommentMention` obsahuje stabilní `UserId`.
- [x] RED: `DocumentCommentMention` obsahuje display text.
- [x] RED: `DocumentCommentReaction` obsahuje emoji/value a author metadata.
- [x] RED: helper umí vrátit počet otevřených threadů.
- [x] RED: helper umí vrátit počet nevyřešených mentionů pro aktuálního uživatele.
- [x] Implementovat enum `DocumentCommentAnchorKind`.
- [x] Implementovat enum `DocumentCommentThreadStatus`.
- [x] Implementovat modely v `Tempo.Blazor.Abstractions`.
- [x] Implementovat validační helper pro anchor.
- [x] GREEN: modelové unit testy projdou.
- [x] REFACTOR: názvy modelů držet obecné, aby nebyly svázané jen se signing komponentami.

### 15.3 Abstractions: callback payloady a řízený stav

- [x] RED: payload pro vytvoření threadu obsahuje anchor a první text komentáře.
- [x] RED: payload pro přidání reply obsahuje thread id a text.
- [x] RED: payload pro resolve obsahuje thread id.
- [x] RED: payload pro reopen obsahuje thread id.
- [x] RED: payload pro editaci komentáře obsahuje comment id a nový text.
- [x] RED: payload pro smazání komentáře obsahuje thread id a comment id.
- [x] RED: payload pro reakci obsahuje thread id, comment id a emoji/value.
- [x] RED: payload pro mention obsahuje identifikované uživatele.
- [x] RED: payload pro selection změnu obsahuje thread id nebo `null`.
- [x] Implementovat payload modely v `Tempo.Blazor.Abstractions`.
- [x] Přidat XML dokumentaci ke všem public modelům a property.
- [x] GREEN: payload unit testy projdou.
- [x] REFACTOR: nepřenášet UI-only stav do abstrakčních modelů.

### 15.4 Lokalizace a texty

- [x] RED: existuje lokalizační klíč pro zapnutí komentářového režimu.
- [x] RED: existuje lokalizační klíč pro vypnutí komentářového režimu.
- [x] RED: existuje lokalizační klíč pro prázdný stav komentářů.
- [x] RED: existuje lokalizační klíč pro placeholder nového komentáře.
- [x] RED: existuje lokalizační klíč pro tlačítko přidat komentář.
- [x] RED: existuje lokalizační klíč pro odpovědět.
- [x] RED: existuje lokalizační klíč pro označit jako vyřešené.
- [x] RED: existuje lokalizační klíč pro znovu otevřít.
- [x] RED: existuje lokalizační klíč pro filtr otevřené.
- [x] RED: existuje lokalizační klíč pro filtr vyřešené.
- [x] RED: existuje lokalizační klíč pro filtr moje zmínky.
- [x] RED: existuje lokalizační klíč pro aria label markeru komentáře.
- [x] RED: existuje lokalizační klíč pro aria label oblasti komentáře.
- [x] Doplnit anglické texty do `TmResources.resx`.
- [x] Doplnit české texty do `TmResources.cs.resx`.
- [x] Doplnit klíče do testovacího localizeru.
- [x] GREEN: lokalizační testy projdou.

### 15.5 API `TmDocumentPageViewer`

- [x] RED: viewer bez comments parametrů renderuje stejně jako dnes.
- [x] RED: viewer přijme `CommentsEnabled`.
- [x] RED: viewer přijme `CommentThreads`.
- [x] RED: viewer přijme `SelectedCommentThreadId`.
- [x] RED: viewer emituje `SelectedCommentThreadIdChanged`.
- [x] RED: viewer přijme `ShowResolvedComments`.
- [x] RED: viewer přijme `CurrentUserId`.
- [x] RED: viewer přijme `CommentMode`.
- [x] RED: viewer emituje `CommentModeChanged`.
- [x] RED: viewer přijme `OnCommentThreadCreateRequested`.
- [x] RED: viewer přijme `OnCommentReplyRequested`.
- [x] RED: viewer přijme `OnCommentResolveRequested`.
- [x] RED: viewer přijme `OnCommentReopenRequested`.
- [x] RED: viewer přijme `OnCommentEditRequested`.
- [x] RED: viewer přijme `OnCommentDeleteRequested`.
- [x] RED: viewer přijme `OnCommentReactionToggled`.
- [x] RED: viewer přijme `OnCommentMentionedUsersChanged`.
- [x] RED: viewer přijme volitelný render fragment pro vlastní thread panel.
- [x] RED: viewer přijme volitelný render fragment pro vlastní marker.
- [x] Implementovat parametry s XML dokumentací.
- [x] Implementovat no-op defaulty pro callbacky.
- [x] GREEN: API render/unit testy projdou.
- [x] REFACTOR: zachovat backward kompatibilitu veřejného API vieweru.

### 15.6 Toolbar a comment režim

- [x] RED: pokud `CommentsEnabled=false`, toolbar komentářů se nezobrazí.
- [x] RED: pokud `CommentsEnabled=true`, toolbar zobrazí tlačítko komentářů.
- [x] RED: tlačítko komentářů má ikonu z `IconNames`.
- [x] RED: tlačítko komentářů má accessible name.
- [x] RED: klik na tlačítko zapne comment režim.
- [x] RED: další klik comment režim vypne.
- [x] RED: zapnutý režim má vizuální active stav.
- [x] RED: toolbar zobrazí počet otevřených komentářů.
- [x] RED: toolbar zobrazí počet mentionů pro aktuálního uživatele, pokud je `CurrentUserId`.
- [x] RED: při změně stránky zůstane comment režim zachovaný.
- [x] RED: při změně zoomu zůstane comment režim zachovaný.
- [x] Implementovat toolbar control v existujícím viewer toolbaru.
- [x] Implementovat fallback layout, pokud viewer toolbar není zobrazený.
- [x] GREEN: toolbar unit testy projdou.
- [x] REFACTOR: sjednotit vizuální styl s pagination/zoom toolbarem.

### 15.7 Comments layer a markery

- [x] RED: bodová kotva se vykreslí na správné stránce.
- [x] RED: oblastní kotva se vykreslí na správné stránce.
- [x] RED: celostránkový komentář se zobrazí v panelu bez markeru nebo jako page badge.
- [x] RED: normalized `X/Y` se přepočítá na pozici v aktuální velikosti stránky.
- [x] RED: normalized `Width/Height` se přepočítá na velikost oblasti v aktuální velikosti stránky.
- [x] RED: změna zoomu přepočítá pozice markerů.
- [x] RED: změna viewportu přepočítá pozice markerů.
- [x] RED: stránkování vykreslí markery jen pro aktuální stránku.
- [x] RED: continuous mode vykreslí markery pro viditelné stránky bez kolize s overlay slotem - uzavřeno jako non-goal fáze 15; viewer nyní používá stránkovaný režim a continuous multi-page rendering patří do navazující fáze.
- [x] RED: selected thread má zvýrazněný marker.
- [x] RED: resolved thread má utlumený marker.
- [x] RED: resolved thread se skryje, pokud `ShowResolvedComments=false`.
- [x] RED: marker obsahuje indikaci počtu komentářů ve vlákně.
- [x] RED: marker obsahuje indikaci mentionu aktuálního uživatele.
- [x] RED: klik na marker vybere thread.
- [x] RED: klik mimo marker zruší výběr podle parametru nebo defaultního chování.
- [x] RED: marker nepřekrývá podpisová pole tak, aby blokoval jejich čtení, pokud comments režim není aktivní.
- [x] Implementovat `TmDocumentCommentsLayer`.
- [x] Implementovat `TmDocumentCommentMarker`.
- [x] Přidat CSS soubor `_document-comments.css`.
- [x] Přidat import CSS do hlavního bundle.
- [x] GREEN: comments layer unit testy projdou.
- [x] REFACTOR: marker positioning sdílet s existujícími page geometry helpery - přidán `DocumentCommentGeometryHelper` pro sdílené CSS pozicování point/area anchorů.

### 15.8 Vytvoření komentáře kliknutím

- [x] RED: v comment režimu klik na stránku vytvoří draft bodového komentáře.
- [x] RED: mimo comment režim klik na stránku draft nevytvoří.
- [x] RED: klik na toolbar nebo panel nevytvoří draft.
- [x] RED: draft používá normalized souřadnice stránky.
- [x] RED: draft se vytvoří na správné stránce při stránkování.
- [x] RED: draft se vytvoří na správné stránce v continuous mode - uzavřeno jako non-goal fáze 15; stránkovaný viewer je pokrytý unit/E2E testy.
- [x] RED: draft se neuloží, dokud uživatel nezadá text a nepotvrdí.
- [x] RED: prázdný draft se po zavření zahodí.
- [x] RED: potvrzení draftu emituje `OnCommentThreadCreateRequested`.
- [x] RED: po úspěšném vytvoření se vybere nový thread, pokud aplikace dodá aktualizovaný stav.
- [x] Implementovat interní draft state.
- [x] Implementovat draft marker.
- [x] Implementovat draft composer v panelu nebo popoveru.
- [x] GREEN: click-create unit testy projdou.
- [x] REFACTOR: oddělit interní draft od externě řízených `CommentThreads`.

### 15.9 Vytvoření oblastního komentáře tažením

- [x] RED: v comment režimu drag na stránce vytvoří draft oblasti.
- [x] RED: krátký drag pod threshold skončí jako bodový komentář.
- [x] RED: drag oblast se clampuje do hranic stránky.
- [x] RED: drag oblast funguje při zoomu 50 %.
- [x] RED: drag oblast funguje při zoomu 150 %.
- [x] RED: drag oblast funguje při fit width.
- [x] RED: při dragu komentáře se nespustí výběr textu stránky.
- [x] RED: při dragu komentáře se nespustí posun existujícího signing fieldu v designeru, protože tato fáze designer neintegruje - uzavřeno jako non-goal; integrace do designeru není součástí fáze 15.
- [x] RED: klávesa `Escape` zruší rozpracovanou oblast.
- [x] RED: potvrzení draftu emituje oblastní anchor.
- [x] Implementovat pointer down/move/up pro area draft.
- [x] Implementovat minimální velikost oblasti.
- [x] Implementovat keyboard cancel.
- [x] GREEN: area-create unit testy projdou.
- [x] REFACTOR: pointer logiku držet izolovanou, aby nerušila zoom/page controls.

### 15.10 Thread panel

- [x] RED: panel zobrazí empty state bez threadů.
- [x] RED: panel zobrazí seznam otevřených threadů.
- [x] RED: panel zobrazí vybraný thread detail.
- [x] RED: panel zobrazí komentáře ve vlákně v chronologickém pořadí.
- [x] RED: panel zobrazí autora komentáře.
- [x] RED: panel zobrazí čas vytvoření.
- [x] RED: panel zobrazí edited stav, pokud existuje `EditedAt`.
- [x] RED: panel zobrazí resolved stav vlákna.
- [x] RED: panel umožní přidat reply.
- [x] RED: panel umožní označit thread jako vyřešený.
- [x] RED: panel umožní znovu otevřít resolved thread.
- [x] RED: panel umožní filtrovat otevřené.
- [x] RED: panel umožní filtrovat vyřešené.
- [x] RED: panel umožní filtrovat moje mentiony.
- [x] RED: panel neztratí rozepsaný reply při změně zoomu.
- [x] RED: panel neztratí vybraný thread při změně stránky, pokud thread pořád existuje.
- [x] RED: panel zobrazí link/indikaci stránky threadu.
- [x] RED: klik na thread v panelu přejde na jeho stránku.
- [x] RED: klik na thread v panelu zvýrazní marker.
- [x] Implementovat `TmDocumentCommentThreadPanel`.
- [x] Implementovat `TmDocumentCommentThreadList`.
- [x] Implementovat `TmDocumentCommentItem`.
- [x] Implementovat responsive layout panelu.
- [x] GREEN: thread panel unit testy projdou.
- [x] REFACTOR: panel použítelný samostatně mimo viewer - `TmDocumentCommentThreadPanel` má samostatné parametry/callbacky a používá se přímo v bUnit testech.

### 15.11 Composer a validace komentáře

- [x] RED: composer má textarea/input pro text.
- [x] RED: composer má tlačítko odeslat.
- [x] RED: prázdný text nelze odeslat.
- [x] RED: whitespace-only text nelze odeslat.
- [x] RED: po odeslání se composer vyčistí.
- [x] RED: při pending callbacku se odesílací tlačítko disabled.
- [x] RED: při disabled vieweru nejde komentář odeslat.
- [x] RED: composer podporuje `Enter` podle zvoleného režimu nebo explicitní tlačítko.
- [x] RED: composer podporuje `Escape` pro zavření draftu.
- [x] RED: composer zachová nové řádky v textu.
- [x] RED: composer text se HTML-encoduje a nevloží nebezpečný markup.
- [x] RED: dlouhý text nepřeteče panel.
- [x] Implementovat `TmCommentComposer`.
- [x] Implementovat minimální validaci textu.
- [x] Implementovat loading/pending state.
- [x] GREEN: composer unit testy projdou.
- [x] REFACTOR: použít existující Tempo input/button komponenty, kde to dává smysl - vyhodnoceno; composer ponechává native `textarea/button` kvůli přesnému keyboard, focus a mention chování uvnitř overlay panelu.

### 15.12 Mentioning

- [x] RED: composer rozpozná `@` trigger.
- [x] RED: composer zobrazí seznam kandidátů mentionů.
- [x] RED: seznam kandidátů používá data z parametru `MentionUsers`.
- [x] RED: seznam kandidátů filtruje podle zadaného textu.
- [x] RED: šipky nahoru/dolů mění aktivní mention kandidát.
- [x] RED: `Enter` vloží aktivní mention.
- [x] RED: klik na kandidáta vloží mention.
- [x] RED: vložený mention uloží stabilní `UserId`.
- [x] RED: zobrazený mention používá display name.
- [x] RED: smazání mention textu odebere mention z payloadu.
- [x] RED: potvrzení komentáře emituje mention metadata.
- [x] RED: viewer emituje `OnCommentMentionedUsersChanged`, pokud se mention seznam změní.
- [x] RED: mention dropdown nepřeteče mimo panel na mobile viewportu.
- [x] Implementovat `TmMentionInput` nebo interní mention podporu v composeru.
- [x] Implementovat jednoduchý mention parser s odděleným stabilním id.
- [x] GREEN: mention unit testy projdou.
- [x] REFACTOR: zhodnotit sdílení s mention logikou z `TmNotionEditoru`, pokud existuje - vyhodnoceno; Notion mention parser pracuje s HTML/contenteditable tokem, document comments drží plain-text payload s explicitními `DocumentCommentMention`.

### 15.13 Reakce a emoji picker

- [x] RED: komentář zobrazí tlačítko pro rychlou reakci.
- [x] RED: komentář zobrazí existující reakce seskupené podle emoji/value.
- [x] RED: klik na existující reakci toggluje reakci aktuálního uživatele.
- [x] RED: reakce aktuálního uživatele má aktivní stav.
- [x] RED: více uživatelů na stejnou reakci zvýší count.
- [x] RED: odebrání poslední reakce skryje badge.
- [x] RED: emoji picker se otevře z tlačítka reakce.
- [x] RED: emoji picker nabídne omezenou výchozí sadu reakcí.
- [x] RED: výběr emoji emituje `OnCommentReactionToggled`.
- [x] RED: picker se zavře po výběru.
- [x] RED: picker se zavře na `Escape`.
- [x] RED: picker nepřetéká mimo panel.
- [x] Implementovat `TmReactionPicker` s malou default sadou.
- [x] Implementovat `TmCommentReactionBar`.
- [x] GREEN: reaction unit testy projdou.
- [x] REFACTOR: pokud už existuje obecný emoji picker, použít ho nebo vytvořit komponentu obecně mimo signing namespace - vyhodnoceno; pro fázi 15 zůstává malý signing-specific `TmReactionPicker`, obecný emoji picker patří do samostatné knihovní fáze.

### 15.14 Editace, mazání a oprávnění

- [x] RED: autor komentáře vidí akci editovat, pokud je povolená.
- [x] RED: neautor komentáře akci editovat nevidí, pokud nemá oprávnění.
- [x] RED: autor komentáře vidí akci smazat, pokud je povolená.
- [x] RED: neautor komentáře akci smazat nevidí, pokud nemá oprávnění.
- [x] RED: aplikace může dodat permission callback nebo permission model.
- [x] RED: editace komentáře předvyplní původní text.
- [x] RED: cancel editace vrátí původní zobrazení.
- [x] RED: save editace emituje `OnCommentEditRequested`.
- [x] RED: delete emituje `OnCommentDeleteRequested`.
- [x] RED: delete posledního komentáře ve vlákně emituje payload, který aplikace umí interpretovat jako smazání vlákna.
- [x] RED: resolved thread defaultně neumožní reply, pokud není povoleno `AllowReplyToResolved`.
- [x] Implementovat permission helper.
- [x] Implementovat menu akcí u komentáře.
- [x] GREEN: permission/edit/delete unit testy projdou.
- [x] REFACTOR: nedávat do komponenty žádné potvrzovací dialogy natvrdo, použít callback nebo jednoduchý confirm slot - splněno; komponenty nic nepotvrzují natvrdo a mazání jde jen přes callback payload.

### 15.15 Accessibility a klávesnice

- [x] RED: comment toggle je dosažitelný klávesnicí.
- [x] RED: marker je focusovatelný.
- [x] RED: marker má accessible name s číslem stránky a stavem.
- [x] RED: `Enter` na markeru otevře thread.
- [x] RED: `Space` na markeru otevře thread.
- [x] RED: `Escape` zavře draft.
- [x] RED: `Escape` zavře emoji picker.
- [x] RED: mention list používá role odpovídající combobox/listbox patternu.
- [x] RED: resolved/open stav je oznámen screen readeru.
- [x] RED: filtr komentářů má čitelný accessible name.
- [x] RED: focus se po vytvoření draftu přesune do composeru.
- [x] RED: focus se po zavření draftu vrátí na stránku nebo marker.
- [x] RED: panel má landmark nebo heading.
- [x] RED: barevné rozlišení markerů není jediný nosič informace.
- [x] Ověřit kontrast markerů v light theme.
- [x] Ověřit kontrast markerů v dark theme.
- [x] Ověřit ovládání bez myši.
- [x] GREEN: accessibility unit testy projdou.

### 15.16 Responsive a vizuální QA

- [x] RED: desktop layout zobrazí viewer a thread panel vedle sebe.
- [x] RED: mobile layout zobrazí thread panel jako drawer/bottom sheet.
- [x] RED: marker nepřetéká mimo stránku při zoomu.
- [x] RED: oblastní marker nepřetéká mimo stránku při zoomu.
- [x] RED: dlouhý autor nepřeteče comment item.
- [x] RED: dlouhé slovo v komentáři se zalomí.
- [x] RED: dlouhý seznam reakcí nezvětší panel nekontrolovaně.
- [x] RED: mention dropdown je použitelný na viewportu 390x844.
- [x] RED: comment panel neblokuje pagination/zoom toolbar.
- [x] RED: empty state nepůsobí jako marketingová karta, ale jako pracovní stav.
- [x] Zkontrolovat viewport 1366x768.
- [x] Zkontrolovat viewport 1024x768.
- [x] Zkontrolovat viewport 390x844.
- [x] Zkontrolovat dark mode.
- [x] Zkontrolovat high zoom v prohlížeči.
- [x] GREEN: responsive testy nebo screenshot QA projdou.

### 15.17 Demo scénáře

- [x] Přidat demo sekci `Document comments`.
- [x] Přidat demo dokument se dvěma stránkami.
- [x] Přidat otevřený bodový komentář.
- [x] Přidat otevřený oblastní komentář.
- [x] Přidat vyřešený komentář.
- [x] Přidat komentář s mentionem aktuálního uživatele.
- [x] Přidat komentář s reakcemi.
- [x] Přidat scénář vytvoření nového bodového komentáře.
- [x] Přidat scénář vytvoření oblastního komentáře.
- [x] Přidat scénář resolve/reopen.
- [x] Přidat scénář reply.
- [x] Přidat scénář mention dropdownu.
- [x] Přidat scénář reaction pickeru.
- [x] Přidat demo stav s `ShowResolvedComments=false`.
- [x] Přidat demo stav s `ShowResolvedComments=true`.
- [x] Přidat stavový výpis pro kontrolu posledního callback payloadu.
- [x] Zkontrolovat, že demo běží na `https://localhost:7106`.

### 15.18 E2E testy

- [x] RED E2E: zapnutí comment režimu zobrazí aktivní stav tlačítka.
- [x] RED E2E: klik na dokument vytvoří draft komentáře.
- [x] RED E2E: odeslání draftu přidá thread do demo stavu.
- [x] RED E2E: prázdný draft nejde odeslat.
- [x] RED E2E: `Escape` zruší draft.
- [x] RED E2E: drag na dokument vytvoří oblastní draft.
- [x] RED E2E: oblastní draft se uloží s normalized souřadnicemi.
- [x] RED E2E: marker zůstane na správném místě po zoom in.
- [x] RED E2E: marker zůstane na správném místě po zoom out.
- [x] RED E2E: marker zůstane na správné stránce po přepnutí stránky.
- [x] RED E2E: klik na marker otevře thread panel.
- [x] RED E2E: reply přidá komentář do vlákna.
- [x] RED E2E: resolve skryje thread při filtru otevřené.
- [x] RED E2E: reopen vrátí thread mezi otevřené.
- [x] RED E2E: mention `@` otevře seznam uživatelů.
- [x] RED E2E: výběr mentionu zobrazí mention chip/text.
- [x] RED E2E: odeslaný komentář s mentionem zapíše mention payload.
- [x] RED E2E: reaction picker přidá reakci.
- [x] RED E2E: opětovný klik na reakci ji odebere.
- [x] RED E2E: mobile viewport otevře panel jako drawer/bottom sheet.
- [x] RED E2E: marker je ovladatelný klávesnicí.
- [x] E2E musí běžet proti `https://localhost:7106`.
- [x] Před E2E uvolnit port `7106`, pokud na něm běží cizí proces.
- [x] Po E2E demo zastavit.
- [x] GREEN E2E comments testy projdou - comments subset 9/9.

### 15.19 Dokumentace

- [x] Doplnit `COMPONENTS.md` o `TmDocumentPageViewer` comments režim.
- [x] Popsat datový model thread/comment/anchor.
- [x] Popsat řízený stav přes `CommentThreads`.
- [x] Popsat callbacky pro persistenci.
- [x] Popsat, že notifikace se řeší v aplikaci nad mention callbackem.
- [x] Popsat, že komentáře nejsou součást podepsaného PDF.
- [x] Popsat normalizované souřadnice kotev.
- [x] Popsat doporučení pro audit metadata.
- [x] Doplnit příklad bodového komentáře.
- [x] Doplnit příklad oblastního komentáře.
- [x] Doplnit příklad mention users.
- [x] Doplnit příklad reaction pickeru.
- [x] Doplnit accessibility poznámky.
- [x] Doplnit migration poznámku: viewer bez comments parametrů se chová beze změny.

### 15.20 Ověření fáze 15

- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~DocumentComment" --no-restore`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~TmDocumentPageViewer" --no-restore`.
- [x] Spustit `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter "FullyQualifiedName~TmDocumentComment" --no-restore`.
- [x] Spustit `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore`.
- [x] Spustit comments E2E subset proti `https://localhost:7106` - 9/9.
- [x] Spustit `dotnet build TempoBlazor.slnx --no-restore` - prošlo s existujícím NU1603 warningem k `Microsoft.Extensions.Http`.
- [x] Spustit relevantní accessibility E2E subset - marker keyboard, aria label panelu, disabled/empty submit a mobile sticky panel prošly v comments E2E.
- [x] Zkontrolovat, že stávající signing E2E pro viewer, designer a runner se nerozbily - viewer/designer/runner subset 25/25.
- [x] Zapsat případné existující warningy nebo flaky testy, pokud nesouvisí s fází 15.

## Doporučené pořadí implementace

1. [x] Fáze 0: modely, geometry helpery, localization.
2. [x] Fáze 1: `TmDocumentPageViewer`.
3. [x] Fáze 2: `TmSigningFieldOverlay`.
4. [x] Fáze 3: `TmSignatureCapture`.
5. [x] Fáze 4: `TmConditionBuilder`.
6. [x] Fáze 5: `TmFormulaBuilder`.
7. [x] Fáze 6: `TmRecipientRoleEditor`.
8. [x] Fáze 7: `TmSigningFieldEditorPanel`.
9. [x] Fáze 8: `TmPdfTemplateDesigner`.
10. [x] Fáze 9: signing step komponenty.
11. [x] Fáze 10: `TmSigningFormRunner`.
12. [x] Fáze 11: dokončovací produktové komponenty.
13. [x] Fáze 12: dokumentace, accessibility, E2E stabilizace.
14. [x] Fáze 13: stránkování a zoom pro dokumenty a PDF designer.
15. [x] Fáze 14: vícejazyčná podpisová vrstva.
16. [x] Fáze 15: komentáře nad `TmDocumentPageViewer`.

## Poznámky pro průběžné odškrtávání

- [x] Při každé implementované komponentě doplnit krátkou poznámku sem, pokud se změní scope nebo název komponenty.
- [x] Při přeskočení položky přidat důvod za pomlčku na stejný řádek.
- [x] Při přidání nové komponenty doplnit RED/GREEN/REFACTOR a E2E body do odpovídající fáze.
- [x] Po dokončení fáze aktualizovat doporučené pořadí implementace, pokud se objeví závislost.
