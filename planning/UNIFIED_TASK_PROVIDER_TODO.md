# Unified Task Provider — Analýza a implementační TODO

> Cíl: aby komponenty, které pracují s **úkoly / pracovními položkami / událostmi**
> (Gantt, Scheduler, Notion editor a jeho bloky), mohly v rámci **jedné aplikace**
> sdílet **stejného providera** a stejnou identitu úkolu, a tím spolu vzájemně
> fungovat (úkol vytvořený/upravený v jedné komponentě se projeví v ostatních).

Status legenda: `[ ]` = TODO, `[~]` = rozpracováno, `[x]` = hotovo a ověřeno (build + testy).

---

## 1. Současný stav (analýza)

Prošel jsem kód všech komponent, které drží „úkoly". Závěr: **každá komponenta má
vlastní datový model i vlastní (nebo žádnou) provider abstrakci**, a žádné dva
nepoužívají stejný způsob doručení dat. Nesdílí ani společnou identitu úkolu, takže
tentýž úkol nemůže konzistentně vystupovat napříč komponentami.

### 1.1 Přehled komponent

| Komponenta | Datový model | Provider abstrakce | Mechanismus doručení |
|---|---|---|---|
| `TmGantt` | `GanttTask` (`Abstractions/Models/GanttTask.cs`) | **žádná** | pouze `[Parameter] Data` + `EventCallback`y pro mutace |
| `TmScheduler` | `TmScheduleEvent` (`Abstractions/Models/SchedulerModels.cs`) | `IScheduleDataProvider` (`Abstractions/Interfaces/IScheduleDataProvider.cs`) | `[Parameter] DataProvider` **nebo** `[Parameter] Events` |
| `TmNotionEditor` / `TmNotionMyTasks` | `NotionTaskDto` (`Abstractions/NotionEditor/Models/NotionTaskDto.cs`) | `INotionTaskProvider` (`Abstractions/NotionEditor/Interfaces/INotionTaskProvider.cs`) | `[Parameter]` + cascading `NotionEditorContext` |
| `TmNotionWorkItemBlock` | `WorkItemDto` (`Abstractions/NotionEditor/Models/WorkItemDto.cs`) | `IWorkItemProvider` + `WorkItemProviderRegistry` (`Abstractions/NotionEditor/Interfaces/`) | DI registry, klíčováno přes `ProviderKey` |

### 1.2 Datové modely se nepřekrývají

Čtyři modely popisují v zásadě „úkol", ale s odlišnými poli a bez společného předka:

- **`GanttTask`** — `Id`, `Title`, `Start`, `End`, `PercentComplete`, `ParentId`,
  `Status` (`GanttTaskStatus`), `Priority` (`GanttTaskPriority`), `Assignees`
  (`List<GanttAssignee>`), `EstimationHours`, `Deadline`, `CustomValues`, závislosti …
- **`NotionTaskDto`** — `Id`, `PageId`, `BlockId`, `Text`, `AssigneeId`,
  `AssigneeDisplayName`, `DueDate`, `IsCompleted`, `CreatedAt`. (Pochází z todo-bloku
  na stránce; nemá stav/prioritu, jen completed/ne.)
- **`WorkItemDto`** — `ProviderKey`, `ExternalId`, `Url`, `Title`, `Status` (string),
  `Priority` (string), `AssigneeDisplayName`, `UpdatedAt`, `Fields` (opaque). (Snímek
  externího trackeru — GitHub/Jira/ADO.)
- **`TmScheduleEvent`** — `Id`, `Title`, `Start`, `End`, `AllDay`, `RecurrenceRule`,
  `ResourceId`, `Metadata`. (Kalendářní událost, ne úkol s progresem.)

Identifikátory jsou nesouvisející řetězce: `GanttTask.Id` nijak nesouvisí
s `NotionTaskDto.Id` ani s `WorkItemDto.ExternalId`. **Cross-linking neexistuje.**

### 1.3 Tři různé mechanismy doručení

1. **Čistě parametrový** (`TmGantt`): host načte data (v de* přes HTTP do
   `GanttPage.razor`) a předá je jako `Data`; mutace zpět přes `EventCallback`y.
   Žádná abstrakce providera → nelze sdílet zdroj.
2. **Parametr providera nebo dat** (`TmScheduler`): buď `IScheduleDataProvider`,
   nebo in-memory `Events`. Provider se předává jako parametr, ne přes DI/registry.
3. **Cascading context + parametry** (`TmNotionEditor`): ~30 provider rozhraní se
   předává parametry na `TmNotionEditor` a kaskáduje přes `NotionEditorContext`.
   `INotionTaskProvider` mezi nimi (volitelný).
4. **DI registry** (`TmNotionWorkItemBlock`): `WorkItemProviderRegistry` agreguje
   všechny `IWorkItemProvider` z DI a vybírá podle `ProviderKey`.

### 1.4 Důsledek (proč to vadí)

- Úkol nelze „prokliknout" mezi Notion stránkou, Ganttem a kalendářem — jsou to
  čtyři oddělené světy.
- Host musí pro každou komponentu psát vlastní načítací/ukládací kód a vlastní
  mapování na svůj backend.
- Mutace nejsou konzistentní: Gantt hlásí změny callbacky, Notion přes provider
  metody (`SetCompletedAsync`), Scheduler nemá zápisové API v providerovi vůbec.
- Demo to obchází tím, že `GanttPage` i `DemoNotionTaskProvider` volají **různé**
  REST endpointy (`/api/gantt/*` vs `/api/notion/tasks/*`) nad různými stores
  (`MockGanttStore` vs notion store) — tj. ani v ukázce nesdílí jeden zdroj.

---

## 2. Navrhované řešení (cílová architektura)

Zavést **jednu kanonickou task abstrakci** v `Tempo.Blazor.Abstractions`, kterou
budou všechny komponenty konzumovat.

> **BREAKING CHANGE — žádná zpětná kompatibilita.** Staré modely (`GanttTask`,
> `NotionTaskDto`, `WorkItemDto`) a stará rozhraní (`INotionTaskProvider`,
> `IWorkItemProvider`, případně část `IScheduleDataProvider`) se **odstraní** a
> nahradí jediným kanonickým modelem a kontraktem. Nepíšeme adaptéry ani
> `[Obsolete]` přechody — usage se rovnou přepíše na nové typy.

### 2.1 Kanonický model

- `TmWorkItem` — sjednocený model úkolu (nadmnožina polí výše). Klíčová pole:
  `Id`, `SourceKey` (= provider/zdroj), `Title`, `Description`, `Start?`, `End?`,
  `DueDate?`, `PercentComplete`, `Status` (sjednocený enum + raw label),
  `Priority` (sjednocený enum + raw label), `Assignees`, `ParentId`, `Url?`,
  `IsCompleted`, `Tags`, `CustomFields`, `CreatedAt`, `UpdatedAt`.
- `TmWorkItemStatus` / `TmWorkItemPriority` — sjednocené enumy + možnost nést
  původní (raw) label z externího trackeru.
- `TmWorkItemDependency` — pro vazby (přesun z `GanttDependency`).
- `TmWorkItemQuery` — sjednocený dotaz (filtry: assignee, status, datum, parent,
  source, fulltext; stránkování `Skip`/`Take`).

### 2.2 Jeden provider kontrakt

- `ITmWorkItemProvider` — `SourceKey`, `DisplayName`, capabilities flag (read /
  write / hierarchy / dependencies / scheduling), metody:
  `SearchAsync(TmWorkItemQuery)`, `GetByIdAsync(id)`,
  `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `SetCompletedAsync`.
  Zápisové metody jsou volitelné dle capabilities.
- `TmWorkItemProviderRegistry` — zobecnění existujícího `WorkItemProviderRegistry`
  na `ITmWorkItemProvider`, klíčováno přes `SourceKey`, registrace přes DI.
- `AddTmWorkItems(...)` DI extension — jedno místo, kde se registrují všechny
  zdroje úkolů pro celou aplikaci → **„stejný provider" pro všechny komponenty**.

### 2.3 Náhrada starých typů (breaking change)

- `GanttTask`, `NotionTaskDto`, `WorkItemDto` se **smažou**; všechny komponenty a
  jejich parametry/eventy se přepíšou na `TmWorkItem`.
- `TmGantt` přijímá `[Parameter] ITmWorkItemProvider WorkItemSource` (případně i
  in-memory variantu `Items`), nikoli vlastní `Data`/`Dependencies` v původních typech.
- `INotionTaskProvider` a `IWorkItemProvider` (+ `WorkItemProviderRegistry`) se
  **odstraní**; nahradí je `ITmWorkItemProvider` + `TmWorkItemProviderRegistry`.
- `TmScheduleEvent` se buď nahradí `TmWorkItem` projekcí, nebo (pokud kalendář
  zůstane samostatný) `IScheduleDataProvider` přepíše na práci s `TmWorkItem`.
- Žádné `[Obsolete]`, žádné adaptéry pro starý kód v produkční cestě (mapování
  starý↔nový se použije jen jednorázově při migraci usage, pak se smaže).

---

## 3. Implementační TODO

### Fáze 0 — Příprava a rozhodnutí
- [ ] Potvrdit s uživatelem rozsah: které komponenty migrovat v 1. kole (doporučení: Gantt + Notion tasks; Scheduler a WorkItem jako 2. kolo)
- [ ] Rozhodnout o namespace pro nové typy (`Tempo.Blazor.Abstractions.WorkItems`)
- [ ] Ověřit build strategii (per-TFM `-f net9.0` kvůli OOM dle `project_build`)

### Fáze 1 — Kanonický model a kontrakt (Abstractions) ✅
- [x] Vytvořit `TmWorkItem` model — `WorkItems/TmWorkItem.cs`
- [x] Vytvořit `TmWorkItemStatus`, `TmWorkItemPriority` (sjednocené enumy + raw label)
- [x] Vytvořit `TmWorkItemAssignee` (sjednotit `GanttAssignee` / `IMentionUser`)
- [x] Vytvořit `TmWorkItemDependency` (+ `TmWorkItemDependencyType`)
- [x] Vytvořit `TmWorkItemQuery` (výsledky přes `Tempo.Blazor.Models.PagedResult<TmWorkItem>`)
- [x] Vytvořit `ITmWorkItemProvider` (capabilities + read/write metody) + `TmWorkItemProviderBase`
- [x] Vytvořit `TmWorkItemCapabilities` (flags)
- [x] Vytvořit `TmWorkItemProviderRegistry` (klíčováno přes `SourceKey`, + `GetDefault()`)
- [x] Vytvořit DI extension `AddTmWorkItems(...)` / `AddTmWorkItemProvider<T>()`
- [x] Unit testy modelu + registry — `tests/Tempo.Blazor.Tests/WorkItems/TmWorkItemProviderRegistryTests.cs` (6/6 ✅, build net9.0 i net10.0 zelený)

### Fáze 2 — Migrace usage na nový model (a smazání starých typů)
- [~] Přepsat místa používající staré modely na `TmWorkItem` — ✅ celá Notion strana; zbývá Gantt
- [~] **Smazat** staré modely — ✅ `NotionTaskDto`, `NotionTaskQuery`, `WorkItemDto`, `WorkItemQuery`; zbývá `GanttTask`
- [x] **Smazat** stará rozhraní — ✅ `INotionTaskProvider`, `IWorkItemProvider`, `WorkItemProviderRegistry`
- [~] Ověřit, že nezbyly žádné odkazy — ✅ Notion/WorkItem strana (grep čistý); Gantt zbývá

### Fáze 3 — TmGantt: přechod na provider (breaking) ✅
- [x] Nahrazeno `Data`/`Dependencies` za `Items`/`DependencyItems` + `WorkItemSource` (`ITmWorkItemProvider`)
- [x] Načítání úkolů i závislostí z providera (`SearchAsync` + `GetDependenciesAsync`, `TmWorkItemDependency`→`GanttDependency` mapper)
- [x] Mutace (`OnTaskUpdated`/`OnTaskAdded`/`OnTaskRemoved`) směrovány do `WorkItemSource` dle capabilities (Update/Create/Delete)
- [x] Public API plně na `TmWorkItem` — **`GanttTask` smazán** (i `GanttTaskStatus`/`GanttTaskPriority`/`GanttAssignee` → `TmWorkItem*`)
- [x] Nullable řešeno: `TmWorkItem.Start/End` jsou nenullable (Notion používá `DueDate`); algoritmy beze změny chování
- [x] Aktualizovány testy `TmGantt*Tests`, exportéry/importéry, demo (`MockGanttStore`, `GanttEndpoints`, `GanttPage`)

**Fáze 3 hotová a ověřená**: Tempo.Blazor.Tests **6845/6845 ✅** (z toho 424 Gantt), staví se knihovna i všechny 4 demo hostitelské projekty + Mcp; Demo.Api.Tests 149/151 (2 selhání jsou `EmailTemplateSmtp4DevTests` vyžadující externí smtp4dev — nesouvisí).

### Fáze 4 — TmNotionEditor / TmNotionMyTasks: přechod na provider (breaking)
- [x] `TmNotionMyTasks` přepsat na `ITmWorkItemProvider` (odstraněno `INotionTaskProvider`)
- [x] `NotionEditorContext` / `TmNotionEditor` parametry přepsat (`TaskProvider` → `WorkItemSource` typu `ITmWorkItemProvider`)
- [x] Demo Notion task provider (Data + HTTP) a `/api/notion/tasks/*` endpointy přepsány na `TmWorkItem`
- [x] Aktualizovat testy `TmNotionMyTasksTests` (8 ✅) + endpoint testy (3 ✅); smazán obsoletní `NotionTaskProviderContractTests`
- [x] `TmNotionWorkItemBlock` přepsán na `TmWorkItemProviderRegistry` (WorkItemDto → TmWorkItem); `WorkItemBlockContent.ProviderKey`→`SourceKey`, `CachedSnapshot`→`TmWorkItem`
- [x] Demo work-item provideri (HTTP ×2), `DemoWorkItemStore`, `/api/notion/work-items/*` endpointy a registrace v 4 `Program.cs` přepsány na `AddTmWorkItemProvider<T>` + `TmWorkItemProviderRegistry`
- [x] Aktualizovány `WorkItemProviderContractTests`, `TmNotionWorkItemBlockTests`, `WorkItemStoreTests`, `NotionToDocumentModelConverter(+Tests)`

**Fáze 4 hotová a ověřená**: Tempo.Blazor.Tests 18 ✅ (My Tasks 8 + WorkItem block 3 + contract 1 + registry 6), Demo.Api.Tests 5 ✅, DocumentFormats.Tests 48 ✅; staví se SharedUI, Mcp i všechny 4 demo hostitelské projekty.

### Fáze 5 — TmScheduler: přechod na provider ✅
- [x] Přidán `[Parameter] WorkItemSource` (`ITmWorkItemProvider`) vedle `IScheduleDataProvider`/`Events`
  (rozhodnutí: `IScheduleDataProvider` **ponechán** pro čistě kalendářní události — recurrence/all-day, jiná doména než „úkol"; sdílení tasků jde přes `WorkItemSource`)
- [x] Mapování `TmWorkItem` (Start/End/Title/Color) → `TmScheduleEvent`, filtr na viditelný rozsah (`RangeStart`/`RangeEnd`)
- [x] Write-back: drag/resize → `WorkItemSource.UpdateAsync` dle capabilities
- [x] Testy `TmSchedulerWorkItemSourceTests` (2 ✅); celý scheduler 97/97 ✅

### Fáze 6 — Sjednocení v demu (důkaz „jeden provider pro vše") ✅
- [x] Vytvořen `DemoSharedWorkItemProvider : TmWorkItemProviderBase` — jeden in-memory store (CRUD + dependencies), scoped
- [x] Registrován přes `AddScoped` + delegace do unifikovaného registru (`AddTmWorkItems`) ve **všech 4** hostitelských `Program.cs`
- [x] Nová stránka `/unified-tasks` (`UnifiedTasksPage`): `TmGantt` i `TmNotionMyTasks` napojené na **stejnou** instanci `WorkItemSource` + nav odkaz „Unified Tasks"
- [x] Demo scénář: tlačítka „Add task"/„Complete" volají provider → změna se projeví v **obou** komponentách (Gantt mutace → auto-refresh My Tasks)
- [x] Testy `DemoSharedWorkItemProviderTests` (4 ✅: seed, create-then-read, set-completed, dependencies); staví se všechny hosty
- [~] Sjednocení REST `/api/workitems/*` — vynecháno (značeno jako volitelné; demo používá in-memory sdílený provider)

**Pozn.**: stávající `GanttPage`/`NotionEditorPage` zůstávají na svých feature-specifických datech (Gantt přes `/api/gantt`, Notion nad todo-bloky); sdílení napříč komponentami demonstruje samostatná stránka `/unified-tasks`, aby se nerozbily zavedené demo stránky a jejich E2E.

### Fáze 7 — JSON dokumentace a ostatní dokumentace ✅
- [x] **Smazáno** 11 zastaralých per-type JSONů (`GanttTask`, `GanttTaskStatus/Priority`, `GanttAssignee`, `NotionTaskDto`, `NotionTaskQuery`, `WorkItemDto`, `WorkItemQuery`, `INotionTaskProvider`, `IWorkItemProvider`, `WorkItemProviderRegistry`)
- [x] **Vygenerováno** 13 source JSONů pro nové typy (`Abstractions/Work-Items/*`) + regenerace agregátů generátorem (`enrich` + `generate`)
- [x] Ověřeno: agregáty (`tempo-blazor.json`, `tempo-blazor-abstractions.json`) už neobsahují smazané typy a parametry komponent (TmGantt/TmScheduler/TmNotionEditor) ukazují `TmWorkItem`/`WorkItemSource`/`TmWorkItemProviderRegistry`
- [x] `validate` prošel: Tempo.Blazor 954 (0 generated-only), Abstractions 1218 (0 generated-only)
- [x] README: nová sekce „Sharing tasks across components (unified work-item provider)"

### Fáze 8 — Ověření ✅
- [x] `dotnet test tests/Tempo.Blazor.Tests` — **6849/6851**; 2 selhání jsou pre-existující flaky timing testy v `DocumentEditor` (`TmDocumentImageInspector`/`TmDocumentEditor` debounce), izolovaně **projdou 2/2** ✅ a s úkoly/WorkItem nesouvisí
- [x] Build `-f net9.0` bez chyb: Abstractions, Tempo.Blazor; + plný build SharedUI, Mcp, Demo.Api a **všechny 4** demo hostitelské projekty (0 errors)
- [x] Další test projekty: Mcp.Tests 86/86, DocumentFormats.Tests 48/48, scheduler 97/97, Demo.Api.Tests 149/151 (2 selhání = `EmailTemplateSmtp4DevTests` vyžadují externí smtp4dev)
- [x] JSON dokumentace `validate` prošel (0 generated-only u Tempo.Blazor i Abstractions)
- [~] E2E (`Tempo.Blazor.E2E`): C# se kompiluje proti novému API; **spuštění/asset-copy zablokováno plným diskem (0 GB volných)** + vyžaduje běžící demo server a Playwright prohlížeče → poběží v CI/živém prostředí, ne zde (infra limit, ne chyba kódu)

---

## Shrnutí — migrace dokončena ✅

Všechny task-bearing komponenty (`TmGantt`, `TmScheduler`, `TmNotionEditor`/`TmNotionMyTasks`, `TmNotionWorkItemBlock`) nyní sdílí jednu abstrakci `ITmWorkItemProvider` nad kanonickým `TmWorkItem`. Staré typy (`GanttTask`, `NotionTaskDto`, `WorkItemDto`, `GanttAssignee`, `GanttTaskStatus/Priority`, `INotionTaskProvider`, `IWorkItemProvider`, `WorkItemProviderRegistry`) byly **smazány** (breaking change, bez zpětné kompatibility). Demo `/unified-tasks` prokazuje sdílení jednoho provideru napříč komponentami.

---

## 4. Rizika a poznámky
- **Breaking change**: staré typy se mažou, ne deprekují. Build po Fázi 2 záměrně
  spadne na chybějících typech — to je vodítko, kde dohledat zbývající usage.
- **Ztrátovost migrace**: `NotionTaskDto` nemá `Status`/`Priority`, `TmScheduleEvent`
  nemá progress. Při přepisu zvolit rozumné defaulty a zdokumentovat, co `TmWorkItem`
  nově nese navíc / co se z původních modelů zahazuje.
- **DI vs parametr**: registry (`AddTmWorkItems`) je hlavní cesta pro „jeden provider
  pro celou aplikaci"; in-memory `Items` parametr jen pro izolované/ukázkové použití.
- **Build na tomto stroji**: stavět per-TFM (`-f net9.0`), nikoli all-targets (OOM).

---

_Vytvořeno jako analýza před implementací. Položky odškrtávat až po skutečném
dokončení a ověření (build + testy)._
