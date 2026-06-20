# Analýza DocuSeal pro přepis do .NET 10 API a Blazoru

Datum analýzy: 2026-05-08  
Zdrojová aplikace: `/home/pavel/NetProjects/docuseal`  
Cílová knihovna komponent: `/home/pavel/NetProjects/Tempo.Blazor`

## Shrnutí

DocuSeal je produkt pro elektronické podepisování dokumentů, ale technicky nejde jen o CRUD nad PDF. Klíčová hodnota aplikace je ve třech oblastech:

1. **Template builder**: editor šablon nad PDF/obrázky/dynamickými dokumenty, kde se kreslí, přesouvají, mění a konfigurují podpisová a formulářová pole.
2. **Signing form**: veřejný responzivní podpisový formulář, který provází podepisujícího přes pole dokumentu, umí podpis kreslit/psát/nahrát, pracuje s přílohami, 2FA, KBA/ID ověřením, podmínkami a výpočty.
3. **Document processing backend**: nahrávání a zpracování dokumentů, render preview stránek, detekce polí, generování výsledných PDF, audit trail, digitální podpisy, časová razítka, webhooky a API.

Tempo.Blazor už pokrývá velkou část běžného aplikačního UI: tabulky, formuláře, pickery, file upload, PDF viewer, signature pad, QR code, modal/dialog, toasty, menu, sidebar, data table, document manager, activity timeline, rich/markdown editor, stepper, diagram/canvas, splittery a další.

Pro plnohodnotný přepis DocuSeal ale chybí několik specializovaných komponent. Nejdůležitější jsou **PDF template designer s absolutními field overlayi**, **signing ceremony/form runner**, **pokročilý podpisový capture**, **field settings panel**, **recipient/role editor**, **condition/formula builder pro signing pole**, **document preview/annotation viewer**, **audit trail/event timeline pro podpisy** a několik integračních/settings komponent.

## Prozkoumané části DocuSeal

Hlavní soubory a oblasti:

- `config/routes.rb`: veřejné podpisové linky, dashboard, šablony, submissions, API, settings, webhooky.
- `app/models`: `Template`, `Submission`, `Submitter`, `CompletedSubmitter`, `CompletedDocument`, `SubmissionEvent`, `TemplateFolder`, `WebhookUrl`, `WebhookEvent`, `DynamicDocument`.
- `db/schema.rb`: perzistence šablon, polí, submission snapshotů, submitter hodnot, eventů, webhooků, storage, OAuth/MCP tokenů.
- `app/javascript/template_builder`: Vue editor šablon, PDF overlay, pole, podmínky, formule, dynamické dokumenty.
- `app/javascript/submission_form`: Vue signing runtime, kroky formuláře, podpisy, initials, přílohy, telefon/2FA, payment, KBA, ID verification.
- `lib/templates`: nahrávání dokumentů, AcroForm extrakce, preview obrázky, ML detekce polí.
- `lib/submissions`: tvorba submissions, generování výsledných PDF, preview PDF, audit trail.
- `lib/submitters`: autorizace podpisového formuláře, ukládání hodnot, validace, generování podpisů/stampů, API serializace.

## Architektura DocuSeal

### Backend a technologie

DocuSeal je Rails aplikace s PostgreSQL, ActiveStorage, Sidekiq joby a Vue 3 frontendy. Z pohledu přepisu do .NET 10 odpovídají vrstvy přibližně takto:

| DocuSeal | .NET 10 ekvivalent |
|---|---|
| Rails controllers | ASP.NET Core Minimal API / Controllers |
| ActiveRecord models | EF Core entity + value objecty |
| ActiveStorage | storage služba nad disk/S3/Azure/GCS |
| Sidekiq jobs | background worker, např. Hangfire/Quartz/BackgroundService |
| Rails mailers | email service + šablony |
| HexaPDF/Pdfium/Vips | PDF/image processing služby |
| Vue template builder | Blazor interactive komponenty + JS interop/canvas |
| Vue submission form | Blazor signing runtime |

### Doménový model

Základní entity:

- **Account**: tenant/organizace, locale, timezone, konfigurace.
- **User**: uživatel účtu, 2FA, podpis a initials jako přílohy, access tokeny.
- **TemplateFolder**: strom složek šablon.
- **Template**: definice šablony. Obsahuje `schema`, `fields`, `submitters`, `preferences`, `variables_schema`, dokumentové přílohy a sdílený link.
- **Submission**: konkrétní obálka/podpisový proces vytvořený ze šablony. Drží snapshot polí, schématu a rolí, aby změny šablony nerozbily už rozeslané obálky.
- **Submitter**: konkrétní podepisující. Drží email/telefon/jméno, `values`, metadata, stav `awaiting/sent/opened/completed/declined`, veřejný slug.
- **SubmissionEvent**: auditní události: odeslání emailu/SMS, open/click, start form, verification, complete, decline, delegate.
- **CompletedSubmitter / CompletedDocument**: denormalizovaná data pro hotové podpisy a ověřování checksumů.
- **WebhookUrl / WebhookEvent / WebhookAttempt**: integrace a doručování webhooků.
- **DynamicDocument / DynamicDocumentVersion**: dynamicky generované HTML dokumenty a jejich verze převedené do PDF/oblastí.

### Field model

Pole nejsou samostatná tabulka. Jsou uložená jako JSON v `Template.fields` a snapshotovaná v `Submission.template_fields`.

Typy polí nalezené v builderu:

- `text`
- `signature`
- `initials`
- `date`
- `datenow`
- `number`
- `image`
- `file`
- `select`
- `checkbox`
- `multiple`
- `radio`
- `cells`
- `stamp`
- `payment`
- `phone`
- `verification`
- `kba`
- `heading`
- `strikethrough`

Typické vlastnosti pole:

- `uuid`
- `submitter_uuid`
- `name`, `title`, `description`
- `type`
- `required`, `readonly`, `prefillable`
- `default_value`
- `preferences`
- `validation`: `pattern`, `message`, `min`, `max`, `step`
- `conditions`: `field_uuid`, `value`, `action`, `operation`
- `options`: `uuid`, `value`
- `areas`: absolutní oblasti nad dokumentem: `x`, `y`, `w`, `h`, `page`, `attachment_uuid`, `cell_w`, `option_uuid`

Souřadnice oblastí jsou normalizované vůči stránce dokumentu. To je důležité pro Blazor komponenty: UI musí pracovat v poměrových souřadnicích a až při renderu převádět na pixely.

## Hlavní workflow

### 1. Nahrání dokumentu a vytvoření šablony

DocuSeal umí nahrát PDF/obrázky/ZIP. U PDF:

- detekuje šifrované PDF a umí vyžádat heslo,
- extrahuje AcroForm pole,
- renderuje preview stránek jako obrázky,
- ukládá metadata včetně počtu stránek a původních anotací,
- může automaticky převést nalezená PDF pole na signing fields.

V Blazor/.NET přepisu to znamená potřebu backend služeb pro:

- upload a storage,
- PDF page rendering,
- AcroForm parser,
- document metadata,
- async job status,
- bezpečný proxy/download URL.

### 2. Template builder

Builder je nejkomplexnější UI část. Obsahuje:

- náhled všech stránek dokumentu,
- absolutní overlay polí nad stránkami,
- kreslení nového pole tažením myší/prstem,
- drag & drop polí z palety,
- resize/move polí,
- multi-select přes selection box,
- kontextová menu nad stránkou/polem/výběrem,
- kopírování polí na jiné stránky,
- autodetekci polí přes SSE,
- přepínání typu pole,
- editaci názvu, popisku, default hodnoty, validací, fontu a podmínek,
- správu options pro select/radio/multiple,
- přiřazení pole k podepisujícímu/roli,
- mobilní alternativu builderu,
- dynamické dokumenty postavené nad HTML/Tiptap editorem.

### 3. Vytvoření submission

Submission lze vytvořit:

- ručním zadáním příjemců,
- z email listu/bulk režimu,
- přes veřejný link,
- přes API,
- přes embed,
- self-sign režimem.

Při vytvoření se řeší:

- role/submitter mapping,
- pořadí podepisujících,
- volitelní invitee,
- per-submitter message,
- default values,
- readonly fields,
- expirace,
- preference posílání emailu/SMS,
- snapshot šablony.

### 4. Signing form

Podepisující prochází kroky podle polí přiřazených jeho `submitter_uuid`. UI se chová jako "ceremony runner":

- zvýrazňuje aktuální pole v dokumentu,
- na mobilu minimalizuje/rozbaluje spodní panel,
- průběžně ukládá kroky,
- validuje required/regex/min/max,
- skrývá podmíněná pole,
- počítá formule,
- nahrazuje `{{date}}`,
- umí dokončení, decline, delegate, invite další strany,
- po dokončení zobrazí download/send copy/redirect/confetti.

### 5. Generování výsledku

Po dokončení DocuSeal:

- vyplní hodnoty do PDF,
- vloží podpisy, initials, obrázky, stamp, checkboxy, text, linky na přílohy,
- vygeneruje výsledné dokumenty,
- volitelně merge/combined PDF,
- vygeneruje audit trail PDF,
- přidá digitální podpis / timestamp podle konfigurace,
- uloží checksumy kvůli ověření,
- odešle webhooky a emaily.

## Aktuální pokrytí v Tempo.Blazor

Tempo.Blazor už obsahuje komponenty, které budou pro přepis užitečné:

| Potřeba DocuSeal | Tempo.Blazor stav |
|---|---|
| běžné inputy | `TmTextInput`, `TmTextArea`, `TmNumberInput`, `TmCurrencyInput`, `TmMaskedTextBox` |
| checkbox/radio/select/multiselect | `TmCheckbox`, `TmRadioGroup`, `TmSelect`, `TmMultiSelect`, `TmFilterableDropdown` |
| datum/čas | `TmDatePicker`, `TmDateTimePicker`, `TmTimePicker`, range pickery |
| upload/přílohy | `TmFileDropZone`, `TmAttachmentManager`, `TmFileManager`, `TmDocumentManager` |
| PDF zobrazení | `TmPdfViewer` |
| jednoduchý podpis | `TmSignature` |
| QR/link/share | `TmQRCode`, `TmCopyButton` |
| dashboard tabulky | `TmDataTable`, `TmMultiViewList`, `TmBulkActionBar`, `TmFilterBuilder` |
| layout shell | `TmSidebar`, `TmTopBar`, `TmBreadcrumbs`, `TmDrawer`, `TmSplitter`, `TmDockManager` |
| modal/dialog/toast | `TmModal`, `TmDialog`, `TmToastContainer`, `TmAlert`, `TmPopover`, `TmTooltip` |
| workflow/progress | `TmStepper`, `TmProgressBar`, `TmTimeline`, `TmActivityTimeline` |
| rich/markdown content | `TmMarkdownEditor`, `TmRichEditorFull`, `TmNotionEditor` |
| formule/výrazy obecně | `TmExpressionEditor` |
| kanvas/editor základ | `TmDiagramCanvas`, `TmWireframeDesignerCanvas`, `TmWorkflowDesignerCanvas` |
| settings UI | `TmFormSection`, `TmFormRow`, `TmToggleSection`, `TmTabs`, `TmMenu` |

Největší mezera není v běžném UI, ale v signing-specific kompozitních komponentách a v PDF overlay editoru.

## Chybějící nebo nedostatečné komponenty v Tempo.Blazor

### P0: Komponenty nutné pro MVP podpisové aplikace

#### 1. `TmPdfTemplateDesigner`

Nejdůležitější chybějící komponenta. Měla by být specializovaná na tvorbu podpisových šablon nad PDF/obrázky.

Potřebné schopnosti:

- render stránek dokumentu v přesném aspect ratio,
- overlay vrstvy v normalizovaných souřadnicích,
- kreslení nových polí tažením,
- drag & drop z palety polí,
- move/resize jednoho pole,
- multi-select a hromadný move/delete/copy,
- page context menu a field context menu,
- zoom/fit width,
- scroll to field/page,
- mobile režim pro kreslení polí,
- eventy `FieldAdded`, `FieldChanged`, `FieldMoved`, `FieldResized`, `FieldRemoved`, `SelectionChanged`,
- možnost externě dodat thumbnails/page images i PDF.js renderer,
- podpora readonly preview režimu.

Tempo.Blazor má `TmPdfViewer`, `TmDiagramCanvas` a `TmWireframeDesignerCanvas`, ale žádná z těchto komponent přímo neřeší PDF signing overlay a normalizované oblasti nad stránkami.

#### 2. `TmSigningFieldOverlay`

Nižší stavební komponenta pro zobrazení jednoho pole v dokumentu.

Potřebné schopnosti:

- typy polí `text/signature/initials/date/number/image/file/select/checkbox/radio/multiple/cells/stamp/payment/phone/verification/kba/heading/strikethrough`,
- barevné odlišení podle role/submittera,
- selected/focused/invalid/completed/readonly stavy,
- resize handles,
- ikona typu pole,
- label/placeholder/value preview,
- cell rendering pro `cells`,
- checkbox/radio/multiple rendering podle option area,
- conditional/formula readonly preview.

Toto by mohlo být interní pro designer i signing form, ale vyplatí se to oddělit kvůli sdílení.

#### 3. `TmSigningFormRunner`

Komponenta pro veřejné podepisování dokumentu, tedy Blazor náhrada `submission_form/form.vue`.

Potřebné schopnosti:

- přijmout schema dokumentu, fields, submitter, hodnoty a attachments,
- spočítat pořadí kroků podle polí a podmínek,
- zvýraznit aktuální pole v dokumentu,
- vykreslit krokový panel pro aktuální typ pole,
- validovat required/regex/min/max/step,
- podporovat readonly/default/formula hodnoty,
- průběžně volat autosave/submit step API,
- dokončit submission,
- umět collapsed/expanded mobilní spodní panel,
- progress navigaci,
- accessibility/screen-reader režim jako v DocuSeal.

Bez této komponenty by se signing UI skládalo ručně a rychle by vznikla aplikační logika mimo knihovnu.

#### 4. `TmSignatureCapture`

Současné `TmSignature` je dobrý základ, ale pro DocuSeal nestačí.

Chybějící schopnosti:

- kreslení přes kvalitní canvas/signature-pad algoritmus,
- export PNG/blob/data URL, nejen SVG string,
- import a preview existujícího podpisu,
- upload fotografie podpisu,
- typed signature režim s podpisovým fontem,
- initials režim,
- clear/redraw/reupload,
- validace prázdného a příliš jednoduchého podpisu,
- volitelný signing reason,
- "reuse signature" a "remember signature",
- QR podpis na jiném dotykovém zařízení,
- pen color, pozadí, DPI scaling.

Doporučení: původní `TmSignature` nechat jako jednoduchý input a přidat specializovanou komponentu `TmSignatureCapture`.

#### 5. `TmSigningFieldEditorPanel`

Postranní nebo dropdown panel pro konfiguraci jednoho pole.

Potřebné oblasti:

- typ pole,
- název/title/popis,
- required/readonly/prefillable,
- default value,
- options editor pro select/radio/multiple,
- text validation: none, email/phone/custom regex, délka min/max,
- number validation: min/max/step/format,
- date validation: min/max/today, format date/month/datetime,
- signature format: drawn/typed/upload kombinace,
- stamp settings,
- font settings: size, family, align, color, style,
- copy to all pages / move between pages,
- condition/formula modal hooks.

Tempo má běžné form components, ale nemá signing-specific panel.

#### 6. `TmRecipientRoleEditor`

DocuSeal šablony pracují s rolemi/podepisujícími, ne jen s jedním adresátem.

Potřebné schopnosti:

- seznam rolí submitterů,
- přidat/odebrat/rename roli,
- barva role,
- email/name/phone při vytváření submission,
- pořadí podepisování,
- optional invite,
- invite by role / invite via field,
- requester/self-sign označení,
- merge více rolí při API/bulk submission.

Tempo má `TmMultiSelect`, `TmEntityPicker`, `TmDataTable`, ale ne kompozitní editor rolí.

#### 7. `TmDocumentPageViewer`

PDF viewer v Tempo je viewer nad PDF.js. DocuSeal builder a signing form ale často pracují s předrenderovanými obrázky stránek a overlayi.

Potřebné schopnosti:

- zobrazit page image s přesným `width/height`,
- udržet aspect ratio,
- overlay slot pro pole/anotace,
- scroll/page refs,
- lazy loading,
- externí odkazy/anotace,
- page loading/error skeleton.

Může být vnitřní stavební blok pro `TmPdfTemplateDesigner` a `TmSigningFormRunner`.

### P1: Komponenty pro kompletní produkt

#### 8. `TmConditionBuilder`

DocuSeal podporuje podmínky viditelnosti/chování polí.

Potřebné schopnosti:

- více podmínek,
- AND/OR,
- výběr závislého pole,
- akce podle typu: checked/unchecked, equal/not_equal, contains/does_not_contain, empty/not_empty, greater_than/less_than,
- prevence cyklů,
- validace proti smazaným polím,
- serializace do jednoduchého DTO.

`TmFilterBuilder` je podobný koncept, ale je orientovaný na filtrování dat, ne na field dependencies.

#### 9. `TmFormulaBuilder`

DocuSeal používá formule pro number/payment pole s tokeny `{{field_uuid}}`.

Tempo má `TmExpressionEditor`, ale pro signing je potřeba specializace:

- seznam dostupných číselných polí,
- vložení tokenu podle názvu,
- humanize/normalize field tokenů,
- operátory `+ - * / ^`,
- funkce `round`, `abs` a případně další,
- detekce cyklů a chybějících polí,
- preview/test výpočtu nad sample values.

#### 10. `TmSigningStepInput`

Sada specializovaných inputů pro signing ceremony. Lze ji implementovat jako jednu polymorfní komponentu nebo více komponent:

- `TmSigningTextStep`
- `TmSigningNumberStep`
- `TmSigningDateStep`
- `TmSigningSelectStep`
- `TmSigningCheckboxStep`
- `TmSigningMultiSelectStep`
- `TmSigningImageStep`
- `TmSigningFileStep`
- `TmSigningPhoneStep`
- `TmSigningPaymentStep`
- `TmSigningVerificationStep`
- `TmSigningKbaStep`

Běžné Tempo inputy pokryjí HTML prvky, ale signing step potřebuje jednotné label/description/appears-on/optional UX, hidden hodnoty, scroll-to-field a integraci s hodnotami/attachments.

#### 11. `TmSigningCompletionPanel`

Panel po dokončení:

- stav dokončeno,
- download jednotlivých/combined dokumentů,
- send copy email,
- custom completed button/message,
- redirect,
- confetti,
- informace o waiting for others.

#### 12. `TmSubmissionStatusTimeline`

DocuSeal má eventy na úrovni submission a submitterů. Tempo má `TmTimeline` a activity komponenty, ale signing potřebuje specializované mapování:

- sent/opened/clicked/started/completed/declined/delegated,
- email/SMS bounce/complaint,
- 2FA/phone/email verification,
- KBA start/complete/fail,
- webhook delivery events,
- metadata IP/UA/timezone.

#### 13. `TmAuditTrailViewer`

Komponenta pro zobrazení audit logu a checksumů:

- seznam dokumentů,
- original/result SHA-256,
- signed at, IP, UA, email, phone,
- verification method,
- signer role,
- odkazy na audit PDF a výsledné PDF.

#### 14. `TmPdfSignatureVerification`

DocuSeal má endpoint pro ověření PDF podpisů a checksumů.

Komponenta by měla:

- přijmout PDF upload,
- zobrazit výsledek checksum verification,
- zobrazit podpisy z PDF: signer, signing reason, signing time, signature type, verification messages,
- odlišit malformed/unsigned/untrusted/verified.

#### 15. `TmShareLinkPanel`

Pro šablony a submissions:

- vygenerovaný veřejný link,
- copy link,
- QR code,
- embed code,
- sdílení stavu enabled/disabled,
- expirace a 2FA preference.

Tempo má QR a copy button, ale chybí kompozitní signing share panel.

#### 16. `TmTemplateCard` a `TmSubmissionCard`

Dashboard DocuSeal používá cards/list pro šablony, složky a submissions.

Tempo má `TmCard`, `TmMultiViewList`, `TmDataTable`, ale produkt by získal opakovaně použitelnou sadu:

- název, autor, folder, access/shared ikona,
- preview thumbnail,
- counts/status,
- quick actions: edit, send, share, clone, archive, restore, delete,
- grid/list density.

Toto je méně univerzální, může zůstat v aplikaci, ale při opakovaném použití dává smysl v Tempo.

### P2: Komponenty pro enterprise/integrace

#### 17. `TmWebhookEndpointManager`

Správa webhooků:

- URL editor,
- event multi-select,
- secret reveal/regenerate,
- status posledních doručení,
- resend/test,
- attempts detail.

#### 18. `TmApiTokenManager`

Správa API/MCP tokenů:

- vytvoření tokenu,
- jednorázové zobrazení secretu,
- prefix/hash metadata,
- revoke/archive,
- copy.

#### 19. `TmSmtpSettingsForm`, `TmStorageSettingsForm`, `TmEsignSettingsForm`

DocuSeal má rozsáhlé settings:

- SMTP,
- S3/Azure/GCS/disk storage,
- e-sign certificate/PKCS,
- timestamp server,
- trusted certs,
- personalization/logo/email templates.

Většina může být aplikační UI složené z existujících Tempo komponent. Do knihovny bych je přidával jen pokud mají být reusable napříč více produkty.

#### 20. `TmIdentityVerificationStep`

DocuSeal integruje eID Easy widget a KBA. Obecně v Tempo může být wrapper:

- redirect/widget container,
- loading/error state,
- required-field guard,
- provider-specific adapter.

Protože provider bude aplikačně specifický, do Tempo je vhodnější dát abstraktní step rozhraní než pevnou integraci.

#### 21. `TmPaymentCollectionStep`

DocuSeal používá Stripe payment/checkout field.

Do Tempo bych nedával přímo Stripe business logiku, ale reusable UI pro payment step:

- amount/currency summary,
- checkout state,
- paid receipt attachment,
- error/retry.

## Komponenty, které pravděpodobně nechávat mimo Tempo.Blazor

Některé části jsou spíš aplikační než knihovní:

- konkrétní DocuSeal API DTO a endpoint naming,
- konkrétní eID Easy integrace,
- konkrétní Stripe Connect OAuth,
- Sidekiq/Hangfire job orchestrace,
- PDF signing certifikáty a časová razítka,
- webhook payload mapping,
- email šablony,
- multi-tenant billing/pro licensing,
- MCP nástroje,
- konkrétní texty a marketing/onboarding tour.

Tempo by mělo dodat stavebnice a specializované signing UI komponenty, ale doménová orchestrace má zůstat v aplikaci.

## Doporučený backlog pro Tempo.Blazor

### Fáze 1: Stavební bloky pro PDF overlay

1. `TmDocumentPageViewer`
2. `TmSigningFieldOverlay`
3. modely v `Tempo.Blazor.Abstractions`, např. `SigningField`, `SigningFieldArea`, `SigningFieldType`, `SigningFieldOption`, `SigningSubmitterRole`, `SigningFieldCondition`
4. testy souřadnic, resize/move a vykreslení oblastí

### Fáze 2: Template designer MVP

1. `TmPdfTemplateDesigner`
2. `TmSigningFieldEditorPanel`
3. `TmRecipientRoleEditor`
4. field palette/context menu
5. save/change eventy bez backend závislosti

### Fáze 3: Signing runtime MVP

1. `TmSigningFormRunner`
2. `TmSigningStepInput` sada
3. `TmSignatureCapture`
4. mobile bottom panel
5. autosave a complete callbacks

### Fáze 4: Pokročilé signing features

1. `TmConditionBuilder`
2. `TmFormulaBuilder`
3. `TmSubmissionStatusTimeline`
4. `TmSigningCompletionPanel`
5. accessibility mode

### Fáze 5: Integrace a administrace

1. `TmShareLinkPanel`
2. `TmPdfSignatureVerification`
3. `TmAuditTrailViewer`
4. `TmWebhookEndpointManager`
5. volitelně token/settings manager komponenty

## Poznámky k .NET 10 API návrhu

Přepis nebude jen UI práce. Pro plnohodnotnou funkcionalitu je potřeba navrhnout backend služby:

- `DocumentStorageService`: upload, metadata, signed/proxy URL, download, malware/extension guard.
- `PdfPreviewService`: render stránek do PNG/JPEG, page metadata.
- `PdfFormFieldExtractionService`: AcroForm -> signing field DTO.
- `FieldDetectionService`: volitelná ML/OCR detekce polí.
- `TemplateService`: CRUD šablon, folders, clone, archive, shared link.
- `SubmissionService`: create from template, role mapping, snapshot fields/schema.
- `SubmitterService`: signing access, values update, validation, completion.
- `SigningFormulaService`: výpočty a prevence cyklů.
- `SigningConditionService`: evaluace podmínek a required polí.
- `PdfGenerationService`: vyplnění PDF, flatten, attach images/signatures, merge.
- `AuditTrailService`: audit PDF + event log.
- `PdfSignatureService`: certifikáty, timestamp server, PDF signature verification.
- `NotificationService`: email/SMS pozvánky a reminder.
- `WebhookDispatcher`: enqueue, retry, attempts, signing secrets.
- `SearchIndexService`: fulltext přes templates/submissions/submitters.

Z Rails modelu je důležité převzít princip snapshotů: `Submission` by měl mít vlastní kopii template fields/schema/submitters, aby pozdější editace šablony neměnila rozeslané obálky.

## Rizika a rozhodnutí před implementací

1. **PDF knihovna pro .NET**: je potřeba rozhodnout, čím nahradit HexaPDF/Pdfium/Vips. Kritická je licence, podpora podpisů, flatteningu, PDF/A, AcroForm, renderingu stránek a timestampů.
2. **Canvas vs. SVG vs. DOM overlay**: builder může být DOM overlay nad page images, ale pro výkon a mobile touch bude nutné dobře navrhnout pointer handling.
3. **PDF.js vs. server-rendered pages**: pro template builder je server-rendered page image jednodušší a přesnější vůči výslednému PDF. `TmPdfViewer` se hodí pro preview/verify, ne nutně jako základ builderu.
4. **Accessibility signing režim**: DocuSeal má speciální screen-reader mód. Ten je pro e-sign produkt důležitý a měl by být zahrnut už v návrhu `TmSigningFormRunner`.
5. **Právní/audit požadavky**: UI komponenty musí předávat metadata pro IP/UA/timezone/verification method, ale důvěryhodnost musí řešit backend.
6. **Offline/long-running operace**: preview generation, result generation a audit trail jsou async; UI potřebuje job/progress stavy.

## Závěr

Tempo.Blazor má dobrý základ pro administraci, formuláře, tabulky, soubory a běžné layouty. Pro DocuSeal-like aplikaci ale musí přibýt specializovaná signing vrstva:

- PDF template designer,
- signing form runner,
- pokročilý signature capture,
- field/recipient/condition/formula editory,
- audit/verification/share komponenty.

Nejvyšší priorita je nezačínat stránkami dashboardu, protože ty půjdou složit z existujících komponent. Kritická a zatím nepokrytá část je interaktivní práce s poli nad dokumentem a veřejný signing runtime. Jakmile budou tyto komponenty v Tempo.Blazor, zbytek aplikace bude převážně .NET API doménová logika a běžné Blazor obrazovky.
