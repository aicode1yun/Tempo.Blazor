# Sdílené abstrakce mezi komponentami — analýza a TODO

> Kontext: stejně jako u úkolů (viz [UNIFIED_TASK_PROVIDER_TODO.md](UNIFIED_TASK_PROVIDER_TODO.md))
> mají i další **cross-cutting koncepty** v každé komponentě vlastní model i vlastní
> provider. Když se komponenty použijí v **jedné aplikaci**, nesdílí uživatele,
> komentáře, přílohy, historii ani notifikace → nemůžou spolu dobře fungovat
> (např. komentář v Ganttu se neobjeví v Notion editoru, ačkoli jde o stejnou entitu).
>
> **Přístup: BREAKING CHANGE, žádná zpětná kompatibilita** (stejně jako u úkolů).

Status legenda: `[ ]` TODO, `[~]` rozpracováno, `[x]` hotovo a ověřeno (build + testy).

---

## Přehled duplicit (co jsem našel v kódu)

| Koncept | Duplicitní typy / rozhraní | Doporučený jednotný typ |
|---|---|---|
| **Uživatelé / lidé** | `IMentionUser` **(2 různé definice!)** — `Abstractions/Interfaces/IMentionUser.cs` i `Abstractions/NotionEditor/Models/IMentionUser.cs`; `IMentionDataProvider`; `INotionMentionProvider`; `GanttAssignee`; `TmScheduleResource`; `CurrentUserId` jako holý `string` | `TmUser` + `ITmPeopleProvider` + `ITmCurrentUser` |
| **Komentáře / diskuze** | `DocumentComment` **(2 různé definice!)** — `Abstractions/Models/DocumentComments.cs` i `DocumentEditor/Wysiwyg/Model/DocumentModel.cs`; `GanttComment`; `BlockComment`/`PageComment`/`TextAnchorComment`; `INotionCommentProvider`; `IDocumentCommentProvider`; `ICommentEntry` + `GanttCommentAdapter` | `TmComment` + `ITmCommentProvider` |
| **Přílohy / soubory** | `FileAttachment`; `GanttAttachment`; `SigningAttachment`; `IFileAttachmentProvider`; `INotionFileProvider`; `IDocumentImageProvider` | `TmAttachment` + `ITmFileProvider` (blob/upload) |
| **Historie / verze / audit** | `GanttHistoryEntry`; `AuditEntryDto`; `INotionHistoryProvider`; `IDocumentVersionProvider`; `INotionAuditProvider` | `TmActivityEntry` + `ITmActivityProvider` |
| **Notifikace** | `INotificationService`; `INotification`/`Notification`/`NotificationDto`; `INotificationItem`; `INotificationEvent`; `GanttNotification`(+`Settings`); `ToastService` | `TmNotification` + `ITmNotificationService` (+ toast jako UI vrstva) |
| **Štítky / stav / priorita** | `GanttTaskStatus`, `GanttTaskPriority`; mnoho `*Status` enumů; Notion labels; `GanttCustomField` | `TmTag`, `TmStatus`/`TmPriority`, `TmCustomField` |
| **Identita / oprávnění** | `CurrentUserId` (string) roztroušený; `INotionPermissionProvider` | součást `ITmCurrentUser` + `ITmAuthorizationProvider` |

### Co už je dobře sjednocené (neměnit)
- **Lokalizace** — `ITmLocalizer` injektovaný globálně přes `_Imports.razor` jako `Loc`. ✔
- **Document library** — `ITempoDocumentLibraryProvider` už sdílí wireframe/diagram/spreadsheet bloky. ✔ (vzor hodný následování pro ostatní koncepty)

---

## Priorita (návrh pořadí)

1. **Úkoly** — řeší se zvlášť v [UNIFIED_TASK_PROVIDER_TODO.md](UNIFIED_TASK_PROVIDER_TODO.md).
2. **Uživatelé / lidé** — protáhne se všemi ostatními (assignee, autor komentáře, mention, audit). Udělat hned po úkolech.
3. **Komentáře** — vysoká duplicita, jasný přínos sdílení.
4. **Přílohy / soubory** — střední.
5. **Historie / audit** — střední.
6. **Notifikace** — nižší, ale logické dokončení.
7. **Štítky / stav / priorita / custom fields** — průřezové, dotáhnout nakonec.

---

## TODO — 1. Uživatelé / lidé (`TmUser`)
- [ ] Sjednotit **dvě** definice `IMentionUser` do jednoho `TmUser` (smazat obě staré)
- [ ] `TmUser`: `Id`, `DisplayName`, `Email?`, `AvatarUrl?`, `IsVirtual`, `Color?`
- [ ] `ITmCurrentUser` — nahradit holé `CurrentUserId` stringy (NotionEditorContext aj.)
- [ ] `ITmPeopleProvider` — `SearchAsync`, `GetByIdsAsync` (nahradí `IMentionDataProvider` + `INotionMentionProvider`)
- [ ] Přepsat `GanttAssignee` → `TmUser`, `TmScheduleResource` → `TmUser` (nebo `TmResource : TmUser`)
- [ ] DI extension `AddTmPeople(...)`
- [ ] Smazat staré typy/rozhraní, přepsat usage
- [ ] Testy + JSON dokumentace (smazat staré, vygenerovat nové)

## TODO — 2. Komentáře (`TmComment`)
- [ ] `TmComment`: `Id`, `EntityRef` (typ+id cílové entity), `Author` (`TmUser`), `Body`, `CreatedAt`, `ResolvedAt?`, `ParentId?` (vlákna), `Anchor?` (textová kotva)
- [ ] `ITmCommentProvider` — `GetForEntityAsync`, `AddAsync`, `ResolveAsync`, `DeleteAsync`
- [ ] Sjednotit **dvě** definice `DocumentComment` + `GanttComment` + `BlockComment`/`PageComment`/`TextAnchorComment`
- [ ] Smazat `INotionCommentProvider`, `IDocumentCommentProvider`, `ICommentEntry`/`GanttCommentAdapter`
- [ ] Přepsat usage v Gantt panelu, Notion editoru, Document editoru
- [ ] Testy + JSON dokumentace

## TODO — 3. Přílohy / soubory (`TmAttachment`)
- [ ] `TmAttachment`: `Id`, `FileName`, `ContentType`, `SizeBytes`, `Url`, `UploadedBy` (`TmUser`), `UploadedAt`
- [ ] `ITmFileProvider` — upload/get/delete (blob), nahradí `IFileAttachmentProvider`, `INotionFileProvider`, `IDocumentImageProvider`
- [ ] Sjednotit `FileAttachment`, `GanttAttachment`, `SigningAttachment` → `TmAttachment` (zvážit signing specializaci)
- [ ] Přepsat usage, smazat staré typy
- [ ] Testy + JSON dokumentace

## TODO — 4. Historie / audit (`TmActivityEntry`)
- [ ] `TmActivityEntry`: `Id`, `EntityRef`, `Actor` (`TmUser`), `Action`, `Timestamp`, `Before?`/`After?` (diff), `Description`
- [ ] `ITmActivityProvider` — `GetForEntityAsync`, `AppendAsync`
- [ ] Sjednotit `GanttHistoryEntry`, `AuditEntryDto`; smazat `INotionHistoryProvider`, `IDocumentVersionProvider`, `INotionAuditProvider` (verze dokumentu zvážit zvlášť)
- [ ] Přepsat usage, smazat staré typy
- [ ] Testy + JSON dokumentace

## TODO — 5. Notifikace (`TmNotification`)
- [ ] `TmNotification` + `ITmNotificationService` (sjednotit `INotification`/`Notification`/`NotificationDto`/`INotificationItem`/`GanttNotification`)
- [ ] `ToastService` ponechat jako UI vrstvu nad `ITmNotificationService`
- [ ] Sjednotit nastavení (`GanttNotificationSettings`) do obecného `TmNotificationSettings`
- [ ] Přepsat usage, smazat staré typy
- [ ] Testy + JSON dokumentace

## TODO — 6. Štítky / stav / priorita / custom fields
- [ ] `TmTag`, `TmStatus`, `TmPriority`, `TmCustomField` (sjednotit `GanttTaskStatus`/`Priority`, Notion labels, `GanttCustomField`)
- [ ] Provázat s `TmWorkItem` (z task TODO)
- [ ] Přepsat usage, smazat staré typy
- [ ] Testy + JSON dokumentace

---

## Společná pravidla pro všechny koncepty
- [ ] Nové typy v `Tempo.Blazor.Abstractions` (sjednocený namespace, např. `Tempo.Blazor.Abstractions.Shared`)
- [ ] Každý koncept = jedno read/write provider rozhraní + DI extension `AddTm*`
- [ ] `EntityRef` (typ entity + id) jako společný způsob, jak komentář/příloha/audit ukazují na úkol, stránku, dokument…
- [ ] Breaking change: staré typy mazat, ne deprekovat
- [ ] Vzor: následovat již sdílený `ITempoDocumentLibraryProvider`
- [ ] Po každém konceptu: build `-f net9.0`, `dotnet test tests/Tempo.Blazor.Tests`, úklid `JsonDocumentation/`

---

_Analýza před implementací. Položky odškrtávat až po skutečném dokončení a ověření._
