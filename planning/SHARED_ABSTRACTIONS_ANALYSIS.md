# Sdílené abstrakce mezi komponentami - analýza a TODO

> Kontext: po sjednocení úkolů v [UNIFIED_TASK_PROVIDER_TODO.md](UNIFIED_TASK_PROVIDER_TODO.md)
> už existuje jeden `TmWorkItem` + `ITmWorkItemProvider`, ale další průřezové koncepty
> pořád zůstávají rozdělené podle komponent. Když se komponenty použijí v jedné aplikaci,
> nesdílí uživatele, komentáře, přílohy, historii ani notifikace. Výsledek: stejná entita
> může mít v Ganttu, Notion editoru, document editoru a signing UI různé autory,
> komentáře, soubory a auditní stopu.
>
> **Přístup: BREAKING CHANGE, žádná veřejná zpětná kompatibilita.** Při migraci lze
> dočasně použít interní mapování, ale před odškrtnutím fáze musí být staré veřejné typy
> a rozhraní odstraněné.

Status legenda: `[ ]` TODO, `[~]` rozpracováno, `[x]` hotovo a ověřeno (build + testy).

---

## Korekce po task refaktoru

- `TmWorkItem` / `ITmWorkItemProvider` už existují a jsou vzor pro provider registry,
  capabilities a DI registraci.
- `GanttAssignee`, `GanttTaskStatus` a `GanttTaskPriority` už byly nahrazeny work-item
  typy. Neplánovat je znovu jako existující dluh.
- V `TmWorkItem` ale pořád zůstávají `GanttAttachment` a `GanttComment`, takže přílohy
  a komentáře jsou přímý nedodělaný zbytek task sjednocení.
- U komentářů nejde jen přejmenovat třídu. V kódu existují minimálně dvě odlišné
  kotevní domény: text/block/import kotvy document editoru a page/point/area kotvy
  signing/document vieweru.
- `IDocumentVersionProvider` není totéž co audit/activity log. Verze dokumentu držet
  odděleně, maximálně emitovat `TmActivityEntry`.

---

## Přehled duplicit

| Koncept | Duplicitní typy / rozhraní | Cílový směr |
|---|---|---|
| **Uživatelé / lidé** | `IMentionUser` ve dvou namespacech (`Tempo.Blazor.Interfaces`, `Tempo.Blazor.NotionEditor.Models`), `IMentionDataProvider`, `INotionMentionProvider`, `TmWorkItemAssignee`, `DocumentCommentUser`, `DocumentCommentMention`, `DocumentEditorAuthor`, `TmScheduleResource`, roztroušené `CurrentUserId` stringy | `TmUser`, `TmUserRef`, `ITmPeopleProvider`, `ITmCurrentUser`; u scheduleru zvážit `TmResource`, protože resource nemusí být člověk |
| **Komentáře / diskuze** | `GanttComment`; dvě rodiny `DocumentComment` (`Abstractions/Models/DocumentComments.cs` a `Abstractions/DocumentEditor/Models/DocumentComments.cs`); `BlockComment`/`PageComment`/`TextAnchorComment`; `INotionCommentProvider`; `IDocumentCommentProvider`; `ICommentEntry` + `GanttCommentAdapter` | `TmCommentThread` + `TmCommentEntry` + `TmCommentAnchor` + `ITmCommentProvider` s capabilities |
| **Přílohy / soubory** | `FileAttachment`, `GanttAttachment`, `SigningAttachment`, `ChatAttachment`, `IFileAttachmentProvider`, `INotionFileProvider`, `IDocumentImageProvider` | oddělit blob/asset provider (`ITmFileProvider`) od vazby souboru na entitu (`ITmAttachmentProvider`), společný `TmAttachment` |
| **Historie / audit / aktivita** | `GanttHistoryEntry`, `AuditEntryDto`, `INotionHistoryProvider`, `INotionAuditProvider`, `IDocumentAuditSink`; `IDocumentVersionProvider` jen souvisí | `TmActivityEntry` + `ITmActivityProvider`; document versions ponechat jako samostatný koncept |
| **Notifikace** | `INotificationService`, `INotification`/`Notification`/`NotificationDto`, `INotificationItem`, `INotificationEvent`, `GanttNotification`, `GanttNotificationSettings`, `NotificationBadgeState`; `ToastService` je transient UI vrstva | `TmNotification` + `ITmNotificationService` + badge/read state; `ToastService` ponechat jako UI službu nebo bridge |
| **Štítky / custom fields / stav** | `GanttCustomField`, stringové `Tags` v `TmWorkItem`, Notion labels, více doménových `*Status` enumů | `TmTag`, `TmCustomFieldDefinition`, `TmCustomFieldValue`; `TmStatus`/`TmPriority` zavést jen tam, kde je skutečně průřezová workflow doména |
| **Identita / oprávnění** | `CurrentUserId` parametry a konstanty, `INotionPermissionProvider`, různé `CanEdit`/`CanDelete` snapshoty v modelech | `ITmCurrentUser` + `ITmAuthorizationProvider`; UI může mít explicitní parameter override, ale default má jít z DI |

### Co už je dobře sjednocené

- **Lokalizace** - `ITmLocalizer` injektovaný globálně přes `_Imports.razor` jako `Loc`.
- **Document library** - `ITempoDocumentLibraryProvider` sdílí wireframe/diagram/spreadsheet bloky.
- **Work items** - `TmWorkItem`, `ITmWorkItemProvider`, `TmWorkItemProviderRegistry` a `AddTmWorkItems(...)` jsou vzor pro další průřezové providery.

---

## Navržené pořadí

1. **Fáze 0: společný základ a aktuální inventář** - zpřesnit namespace, `TmEntityRef`, provider capabilities a grep seznam starých typů.
2. **Uživatelé / lidé** - nutné pro autory komentářů, assignee, mentions, audit a notifikace.
3. **Komentáře** - vysoký přínos, ale dělat až po lidech a `TmEntityRef`.
4. **Přílohy / soubory** - odstraní `GanttAttachment` z `TmWorkItem` a sjednotí upload/metadata příběh.
5. **Historie / audit / aktivita** - sjednotit logy, ponechat document versions bokem.
6. **Notifikace** - navázat na komentáře/audit/people.
7. **Štítky / custom fields** - dotáhnout nakonec, protože se dotýkají `TmWorkItem` i dalších domén.
8. **Oprávnění** - postupně zavést `ITmAuthorizationProvider` tam, kde dnes modely nesou `CanEdit`/`CanDelete` nebo komponenty řeší `CurrentUserId`.

---

## TODO - Fáze 0: společný základ

- [x] Aktualizovat grep inventář a odstranit ze seznamu už smazané typy (`GanttAssignee`, `GanttTaskStatus`, `GanttTaskPriority`).
- [x] Rozhodnout namespace pro nové sdílené typy: `Tempo.Blazor.Abstractions.Shared`.
- [x] Zavést `TmEntityRef`: `EntityType`, `EntityId`, `SourceKey?`, `TenantId?`, `DisplayName?`, `Url?`.
- [x] Zavést společnou konvenci pro capabilities flags u providerů (podle `TmWorkItemCapabilities`).
- [x] Rozhodnout, které provider metody budou v jednom rozhraní a které budou volitelné sub-kontrakty (např. reactions/read tracking/subscriptions).
- [x] Testy pro `TmEntityRef` + JSON dokumentace.

Ověření fáze 0:

- [x] `dotnet build src/Tempo.Blazor.Abstractions/Tempo.Blazor.Abstractions.csproj -f net9.0 --no-restore`
- [x] `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj -f net9.0 --no-restore`
- [x] `dotnet test tests/Tempo.Blazor.Tests/ --filter "FullyQualifiedName~TmEntityRefTests|FullyQualifiedName~TmProviderCapabilityExtensionsTests" --no-restore`
- [x] `dotnet test tests/Tempo.Blazor.Tests/ --no-restore`
- [x] `dotnet test tests/Tempo.Blazor.Tests/ --no-build`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj -- JsonDocumentation generate --package Tempo.Blazor.Abstractions`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj -- JsonDocumentation validate`

## TODO - 1. Uživatelé / lidé

- [x] `TmUser`: `Id`, `DisplayName`, `UserName?`, `Email?`, `AvatarUrl?`, `Color?`, `IsVirtual`.
- [x] `TmUserRef`: lehký snapshot pro vkládání do komentářů/auditu/notifikací bez nutnosti načítat celého uživatele.
- [x] `ITmCurrentUser`: async získání aktuálního uživatele + volitelné skupiny/claims; komponenty můžou mít explicitní `CurrentUserId` override jen pro demo/test scénáře.
- [x] `ITmPeopleProvider`: `SearchAsync(TmPeopleQuery)`, `GetByIdAsync`, `GetByIdsAsync`.
- [x] Nahradit `IMentionDataProvider` + `INotionMentionProvider`. Oba veřejné mention providery jsou odstraněné; lidé jdou přes `ITmPeopleProvider` a Notion page-link autocomplete přes existující `INotionSearchProvider`.
- [x] Sjednotit dvě definice `IMentionUser` a smazat staré typy. Obě veřejné definice jsou odstraněné; UI mention výsledky používají `TmUser` / `TmUserRef`.
- [x] Rozhodnout `TmWorkItemAssignee`: ponechat jako assignment snapshot se scheduling metadaty (`HourlyRate`, virtual resource) a doplnit bridge `ToUserRef()` / `FromUserRef(...)`.
- [x] U `TmScheduleResource` nerozhodovat automaticky pro `TmUser`; zavedený je sdílený `TmResource` / `TmResourceRef` a `TmScheduleResource` zůstává scheduler snapshot s bridge `ToResource()` / `FromResource(...)`.
- [x] Přepsat mentions/autocomplete v rich editoru, Notion editoru a document comments na `ITmPeopleProvider`; Notion `[[ page ]]` autocomplete je oddělený přes `INotionSearchProvider`.
- [x] Testy + JSON dokumentace.

Ověření fáze 1:

- [x] `dotnet build src/Tempo.Blazor.Abstractions/Tempo.Blazor.Abstractions.csproj -f net9.0 --no-restore`
- [x] `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj -f net9.0 --no-restore`
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~Tempo.Blazor.Tests.Shared"`
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~Tempo.Blazor.Tests.Shared|FullyQualifiedName~DocumentAutocompleteTests"`
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~Tempo.Blazor.Tests.Shared|FullyQualifiedName~DocumentAutocompleteTests|FullyQualifiedName~CommentMentionHelperTests|FullyQualifiedName~TmNotionTodoActionItemTests|FullyQualifiedName~TmNotionPageInfoPanelTests"`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj -- JsonDocumentation generate --package Tempo.Blazor.Abstractions`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj -- JsonDocumentation generate --package Tempo.Blazor`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj -- JsonDocumentation validate`
- [x] `rg "INotionMentionProvider|IMentionDataProvider|IMentionUser"` je čistý mimo historický popis v tomto plánu.

## TODO - 2. Komentáře

- [x] `TmCommentThread`: `Id`, `EntityRef`, `Anchor?`, `Status`, `Visibility?`, `CreatedAt`, `UpdatedAt?`, `ResolvedAt?`, `ResolvedBy?`, `ReadByUserIds`, `SubscribedUserIds`, `ExternalId?`, `SourceFormat?`.
- [x] `TmCommentEntry`: `Id`, `ThreadId`, `ParentEntryId?`, `Author` (`TmUserRef`), `Body`, `BodyFormat` (`PlainText`/`Html`/`Markdown`), `CreatedAt`, `EditedAt?`, `Mentions`, `Reactions`, `Metadata`.
- [x] `TmCommentAnchor`: podpořit `None`, `Block`, `TextRange`, `Page`, `PagePoint`, `PageArea`, `Rendition`, `External`; zachovat text offsets, page geometry, highlighted text, external/rendition anchor id a `IsOrphaned`.
- [x] `ITmCommentProvider` s capabilities: `Read`, `CreateThread`, `Reply`, `EditEntry`, `Delete`, `Resolve`, `Reactions`, `ReadTracking`, `Subscriptions`, `RichText`.
- [x] Minimální metody: `GetForEntityAsync`, `CreateThreadAsync`, `ReplyAsync`, `UpdateEntryAsync`, `DeleteThreadAsync`, `DeleteEntryAsync`, `ResolveAsync`, `ReopenAsync`.
- [x] Volitelné metody/sub-kontrakty pro reactions, read tracking a subscriptions; necpát vše do povinného minima.
- [x] Migrovat nejdřív `GanttComment` + `TmWorkItem.Comments`, potom signing/document-viewer comments, potom Notion comments, potom document editor comments. Signing/document-viewer UI request modely zůstávají doménové, ale mají `DocumentViewerCommentBridge` na `TmCommentThread`.
- [x] Smazat `INotionCommentProvider`, `IDocumentCommentProvider`, `ICommentEntry` a `GanttCommentAdapter` až po přepsání usage.
- [x] Ošetřit rich HTML obsah z Notion komentářů odděleně od plain text komentářů (body format + sanitizační odpovědnost hosta).
- [x] Testy + JSON dokumentace.

Ověření fáze 2:

- [x] `dotnet build src/Tempo.Blazor.Abstractions/Tempo.Blazor.Abstractions.csproj -f net9.0 --no-restore`
- [x] `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj -f net9.0 --no-restore`
- [x] `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore`
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~TmCommentAbstractionsTests|FullyQualifiedName~DocumentCommentModelTests|FullyQualifiedName~CommentMentionHelperTests|FullyQualifiedName~CommentNotificationOrchestratorTests|FullyQualifiedName~TmActivity|FullyQualifiedName~TmGanttPhase4Tests|FullyQualifiedName~TmNotionPageCommentSectionTests"`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor.Abstractions`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation validate`
- [x] `rg "INotionCommentProvider|IDocumentCommentProvider|ICommentEntry|GanttComment|GanttCommentAdapter"` je čistý mimo historický popis v tomto plánu.

## TODO - 3. Přílohy / soubory

- [x] `TmAttachment`: `Id`, `EntityRef`, `FileName`, `ContentType`, `SizeBytes`, `Url?`, `AssetId?`, `UploadedBy` (`TmUserRef`), `UploadedAt`, `Purpose?`, `Metadata`.
- [x] `ITmFileProvider`: upload blobu/streamu, resolve URL/access ticket, delete; capabilities pro draft assets, signed URLs, chunk upload.
- [x] `ITmAttachmentProvider`: list/add/remove vazeb souboru k `TmEntityRef`.
- [x] Sjednotit `GanttAttachment` a odstranit ho z `TmWorkItem`.
- [x] Sjednotit `FileAttachment` a `SigningAttachment`; signing attachment shell zůstává jako komponenta, starý model byl odstraněn.
- [x] `IDocumentImageProvider` migrovat opatrně: dokumentová specializace zůstává a nově rozšiřuje `ITmFileProvider` kvůli obecnému upload/resolve/delete kontraktu.
- [x] Zkontrolovat `ChatAttachment`: zůstává chat-specific, doplněn bridge na/z `TmAttachment`.
- [x] Testy + JSON dokumentace.

Ověření fáze 3:

- [x] `dotnet build src/Tempo.Blazor.Abstractions/Tempo.Blazor.Abstractions.csproj -f net9.0 --no-restore`
- [x] `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj -f net9.0 --no-restore`
- [x] `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore`
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~TmAttachmentAbstractionsTests|FullyQualifiedName~TmActivityAttachmentsTests|FullyQualifiedName~TmActivityLogAccessibilityTests|FullyQualifiedName~TmAttachmentManagerTests|FullyQualifiedName~TmDocumentManagerTests|FullyQualifiedName~TmGanttPhase4Tests|FullyQualifiedName~TmNotionImageBlockPasteTests|FullyQualifiedName~TmNotionMediaBlockDragDropTests|FullyQualifiedName~TmNotionMediaUploadDialogLibraryTests|FullyQualifiedName~DocumentEditorOfflineImageRenditionProviderTests|FullyQualifiedName~TmSigningAttachmentStepTests"` - prošlo 92/92.
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build` - prošlo 6877/6877.
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor.Abstractions`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation validate`
- [x] `rg "IFileAttachment|IFileAttachmentProvider|FileAttachment|FileChunkData|GanttAttachment|SigningAttachment|INotionFileProvider"` je čistý mimo historický popis v tomto plánu a názvy signing komponent/testů.

## TODO - 4. Historie / audit / aktivita

- [x] `TmActivityEntry`: `Id`, `EntityRef`, `Actor` (`TmUserRef`), `Action`, `Timestamp`, `Summary?`, `Before?`, `After?`, `Diff?`, `CorrelationId?`, `Metadata`.
- [x] `ITmActivityProvider`: `GetForEntityAsync`, `QueryAsync`, `AppendAsync`.
- [x] Sjednotit `GanttHistoryEntry`, `AuditEntryDto`, `INotionHistoryProvider`, `INotionAuditProvider`, `IDocumentAuditSink`.
- [x] `IDocumentVersionProvider` nesmazat jako součást auditu; verze dokumentu jsou snapshot/restore doména. Notion page history má nově `INotionVersionProvider`, document versions zůstaly oddělené a audit události se zapisují přes `TmActivityEntry`.
- [x] Přepsat Gantt history drawer a Notion audit log panel na nový provider.
- [x] Testy + JSON dokumentace.

Ověření fáze 4:

- [x] `dotnet build src/Tempo.Blazor.Abstractions/Tempo.Blazor.Abstractions.csproj -f net9.0 --no-restore`
- [x] `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj -f net9.0 --no-restore`
- [x] `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore -m:1 /p:BuildInParallel=false /v:m` - prošlo, 0 chyb; warningy existují.
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~NotionAuditContractTests|FullyQualifiedName~TmNotionAuditLogPanelTests|FullyQualifiedName~TmNotionPageHistoryDiffTests|FullyQualifiedName~TmGanttPhase4Tests|FullyQualifiedName~DocumentEditorProviderTests|FullyQualifiedName~DocumentEditorOfflineImageRenditionProviderTests"` - prošlo 60/60.
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build` - prošlo 6877/6877.
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor.Abstractions`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation validate`
- [x] `rg "GanttHistoryEntry|AuditEntryDto|INotionHistoryProvider|INotionAuditProvider|IDocumentAuditSink|AuditSink|audit sink"` je čistý mimo historický popis v tomto plánu.

## TODO - 5. Notifikace

- [x] `TmNotification`: `Id`, `Recipient`/`RecipientUserId`, `Actor?`, `Type`, `Title`, `Body?`, `Severity`, `CreatedAt`, `ReadAt?`, `ActionUrl?`, `EntityRef?`, `Metadata`.
- [x] `ITmNotificationService`: `PublishAsync`, `GetNotificationsAsync`, `GetUnreadCountAsync`, `MarkAsReadAsync`, `MarkAllAsReadAsync`.
- [x] Sjednotit `INotification`/`Notification`/`NotificationDto`/`INotificationItem`/`INotificationEvent` a `GanttNotification`.
- [x] Zachovat `ToastService` jako transient UI vrstvu. Toast zůstal UI služba; persistentní stav jde přes `ITmNotificationService` a `TmNotificationToastContainer` pouze zobrazuje poslední service události.
- [x] Sjednotit badge/read state (`INotificationBadgeState`, `NotificationBadgeState`) s novým providerem.
- [x] Sjednotit nastavení (`GanttNotificationSettings`) do obecnějších notification preferences jen pokud jsou reálně používané mimo Gantt.
- [x] Testy + JSON dokumentace.

Ověření fáze 5:

- [x] `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore -m:1 /p:BuildInParallel=false /v:m` - prošlo, 0 chyb; warningy existují.
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~InMemoryNotificationStoreTests|FullyQualifiedName~TmNotificationBellTests|FullyQualifiedName~TmNotificationBellLocalizationTests|FullyQualifiedName~CommentNotificationOrchestratorTests|FullyQualifiedName~CommentMentionHelperTests|FullyQualifiedName~TmNotionWatchNotificationsTests|FullyQualifiedName~TmGanttPhase5Tests" -m:1 /p:BuildInParallel=false /v:m` - prošlo 95/95.
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build -m:1 /p:BuildInParallel=false /v:m` - prošlo 6877/6877.
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor.Abstractions`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation validate`
- [x] `rg "INotificationService|INotificationBadgeState|NotificationEvent|NotificationType|NotificationDto|INotification|INotificationItem|NotificationSeverity|GanttNotification|GanttNotificationSettings|NotificationBadgeState"` je čistý mimo historický popis v tomto plánu a ignorované build cache soubory.

## TODO - 6. Štítky / custom fields / stav

- [x] `TmTag`: `Id`, `Label`, `Color?`, `Description?`, `SourceKey?`, `TenantId?`, `Metadata`.
- [x] `TmTagRef`: lehký tag snapshot pro vazby na entity a komponenty bez nutnosti nést plnou definici tagu.
- [x] `TmCustomFieldDefinition`: `Id`, `Name`, `Type`, `Options`, `IsRequired`, `AppliesToEntityTypes`, `SourceKey?`, `Description?`, `Metadata`.
- [x] `TmCustomFieldValue`: `DefinitionId`, `EntityRef`, `Value`, `Metadata`.
- [x] Migrovat `GanttCustomField` na obecný custom-field model. Staré veřejné typy `GanttCustomField` a `GanttFieldType` jsou odstraněné; Gantt používá `TmCustomFieldDefinition` a `TmCustomFieldType`.
- [x] Rozhodnout, jestli `TmWorkItem.Tags: List<string>` zůstane kompatibilní jednoduchá vrstva, nebo se nahradí `List<TmTagRef>`. Nahrazeno za `List<TmTagRef>`; pro jednoduché scénáře zůstává helper `TagLabels` a `SetTagLabels(...)`.
- [x] `TmStatus`/`TmPriority` nezavádět předčasně. `TmWorkItemStatus` a `TmWorkItemPriority` zůstávají doménově přesnější; obecný status dává smysl až při druhé doméně se stejným workflow významem.
- [x] Testy + JSON dokumentace.

Ověření fáze 6:

- [x] `dotnet build src/Tempo.Blazor.Abstractions/Tempo.Blazor.Abstractions.csproj -f net10.0 --no-restore -m:1 /p:BuildInParallel=false /v:m` - prošlo, 0 chyb; warningy existují.
- [x] `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj -f net10.0 --no-restore -m:1 /p:BuildInParallel=false /v:m` - prošlo, 0 chyb; warning `NU1603` existuje.
- [x] `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore -m:1 /p:BuildInParallel=false /v:m` - prošlo, 0 chyb; warningy existují.
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build --filter "FullyQualifiedName~TmTagAndCustomFieldTests|FullyQualifiedName~TmGanttPhase3Tests|FullyQualifiedName~TmTagPickerTests|FullyQualifiedName~TmTagPickerLocalizationTests" -m:1 /p:BuildInParallel=false /v:m` - prošlo 60/60.
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build -m:1 /p:BuildInParallel=false /v:m` - prošlo 6884/6884.
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor.Abstractions`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation validate`
- [x] `rg "GanttCustomField|GanttFieldType|GanttFieldType_"` je čistý mimo historický popis v tomto plánu a ignorované build cache soubory.

## TODO - 7. Identita a oprávnění

- [x] `ITmAuthorizationProvider`: `AuthorizeAsync(TmAuthorizationRequest)`.
- [x] `TmAuthorizationRequest`: `User`, `GroupIds`, `Action`, `EntityRef`, `Metadata`.
- [x] `TmAuthorizationResult`: `Allowed`, `Reason`, `Metadata`; plus helper konstanty `TmAuthorizationActions`.
- [x] Postupně nahradit uložené `CanEdit`/`CanDelete` snapshoty tam, kde jde o aktuální oprávnění, ne o historický stav. V této fázi je nahrazeno efektivní page access rozhodování v `TmNotionEditor`; komentářové/přílohové `CanEdit`/`CanDelete` zatím zůstávají modelové snapshoty/explicitní UI overrides.
- [x] Nahradit Notion-specific `INotionPermissionProvider` společným providerem nebo ho adaptovat nad společný provider. `INotionPermissionProvider` zůstává pro správu Notion page restrictions dialogu; efektivní přístup editoru umí běžet přes `ITmAuthorizationProvider`, když Notion-specific provider není předaný. Demo `DemoNotionPermissionProvider` implementuje oba kontrakty.
- [x] `TmNotionEditor.CurrentUserId` je explicitní override; když je prázdné, editor zkusí `ITmCurrentUser` z DI a fallbackuje na demo user.
- [x] Testy pro fallback chování: bez auth providera se editor řídí explicitním `ReadOnly`; se sdíleným providerem mapuje `view/comment/edit`; bez explicitního user id používá `ITmCurrentUser`.

Ověření fáze 7:

- [x] `dotnet build src/Tempo.Blazor.Abstractions/Tempo.Blazor.Abstractions.csproj -f net10.0 --no-restore -m:1 /p:BuildInParallel=false /v:m` - prošlo, 0 chyb; warningy existují.
- [x] `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj -f net10.0 --no-restore -m:1 /p:BuildInParallel=false /v:m` - prošlo, 0 chyb; warning `NU1603` existuje.
- [x] `dotnet build tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore -m:1 /p:BuildInParallel=false /v:m` - prošlo, 0 chyb; warningy existují.
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-restore --filter "FullyQualifiedName~TmAuthorizationTests|FullyQualifiedName~TmNotionRestrictionsTests|FullyQualifiedName~TmPeopleProviderTests" -m:1 /p:BuildInParallel=false /v:m` - prošlo 13/13.
- [x] `dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --no-build -m:1 /p:BuildInParallel=false /v:m` - prošlo 6890/6890.
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor.Abstractions`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation generate --package Tempo.Blazor`
- [x] `dotnet run --project JsonDocumentation/JsonDocumentationGenerator/JsonDocumentationGenerator.csproj --no-restore -- JsonDocumentation validate`
- [x] `rg "ITmAuthorizationProvider|TmAuthorizationRequest|TmAuthorizationResult|TmAuthorizationActions|AuthorizationProvider"` ukazuje nové sdílené kontrakty, Notion adapter usage, demo wiring, testy a JSON dokumentaci.

---

## Společná pravidla pro všechny koncepty

- [ ] Nové veřejné typy v `Tempo.Blazor.Abstractions`; kód, XML dokumentace a komentáře anglicky.
- [ ] Provider minimální jádro + capabilities; nevytvářet monolitické rozhraní, které musí implementovat každá aplikace.
- [ ] `TmEntityRef` používat pro komentáře, přílohy, audit a notifikace.
- [ ] Veřejné staré typy mazat, ne deprekovat. Interní dočasné mapování je dovolené jen během migrace.
- [ ] Nepřekládat specializovanou doménu násilím: scheduler resource nemusí být user, document version není audit entry, toast není perzistentní notifikace.
- [ ] Po každé fázi: `dotnet build src/Tempo.Blazor.Abstractions/Tempo.Blazor.Abstractions.csproj -f net9.0`, `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj -f net9.0`, relevantní targeted tests, potom `dotnet test tests/Tempo.Blazor.Tests/`.
- [ ] Po každé fázi: úklid `JsonDocumentation/`, regenerace agregátů a validace dokumentace.
- [ ] Před odškrtnutím fáze: grep musí být čistý pro odstraněné veřejné typy/rozhraní a demo musí mít aspoň jeden scénář ukazující sdílení nového providera.

---

## Rizika

- **Přehnané sjednocení** - ne každý podobně vypadající typ patří do jednoho modelu.
- **Ztráta kotev u komentářů** - textové a page geometry kotvy musí zůstat bezeztrátové pro import/export i signing UI.
- **Provider nafouknutí** - reactions, read tracking, subscriptions, draft files a signed URLs patří do capabilities/sub-kontraktů.
- **Current user v komponentách** - DI default je dobrý, ale demo/test a server-side host může pořád potřebovat explicitní override.
- **Document versions** - nemazat spolu s auditem; jen napojit activity log.

---

_Analýza před implementací. Položky odškrtávat až po skutečném dokončení a ověření._
