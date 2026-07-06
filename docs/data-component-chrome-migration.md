# Migrating data-component chrome and filtering (Tempo.Blazor 2.2.0)

Starting with version **2.2.0**, `TmDataTable` and `TmMultiViewList` expose explicit controls over their built-in toolbar, search state, and saved-views chrome. This lets the surrounding page own filtering without duplicate UI.

> All changes are **additive and backward-compatible**. Existing callers that rely on the previous defaults continue to work without modification.

## New API summary

| Parameter | Component(s) | Purpose |
|-----------|--------------|---------|
| `ShowToolbar` | `TmDataTable`, `TmMultiViewList` | When `false`, the entire toolbar container is suppressed. |
| `ShowViewManager` | `TmDataTable`, `TmMultiViewList` | When `false`, the saved-views picker is hidden even if a `ViewProvider` is supplied. |
| `SearchText` / `SearchTextChanged` | `TmDataTable`, `TmMultiViewList` | Two-way binding for the global search term; lets the page drive search from its own input. |
| `ToolbarMode` | `TmDataTable`, `TmMultiViewList` | Preset chrome mode: `Full`, `SearchOnly`, `ActionsOnly`, `ContentOnly`. |

### `ToolbarMode` presets

- `Full` — default; respects the individual `ShowSearch`, `ShowColumnPicker`, `ShowViewSwitcher`, `ShowViewManager`, and `ShowExternalFilterBuilder` flags.
- `SearchOnly` — renders only the global search input.
- `ActionsOnly` — renders only chrome actions (column picker / view switcher / view manager / group picker).
- `ContentOnly` — hides all toolbar chrome and the external filter builder; the component renders only the data surface.

## Scenarios

### Scenario 1: The page owns the filter toolbar

Use `ToolbarMode="DataToolbarMode.ContentOnly"` so the data component does not render its own search, filter builder, or view manager.

```razor
@using Tempo.Blazor.Components.DataTable

<TmSelect TValue="string" @bind-Value="_selectedDept" Options="_deptOptions" />

<TmDataTable TItem="Employee"
             Items="_filteredEmployees"
             ToolbarMode="DataToolbarMode.ContentOnly"
             EmptyTitle="No employees match the filter">
    <TmDataTableColumn TItem="Employee" Title="Name" Field="e => e.Name" />
    <TmDataTableColumn TItem="Employee" Title="Dept" Field="e => e.Dept" />
</TmDataTable>
```

For `TmMultiViewList`:

```razor
<TmMultiViewList TItem="Project"
                 Items="_filteredProjects"
                 ToolbarMode="DataToolbarMode.ContentOnly"
                 EmptyTitle="No projects found" />
```

### Scenario 2: Saved views without the inline filter builder

Keep the full chrome but hide only the inline external filter builder:

```razor
<TmDataTable TItem="Employee"
             Items="_employees"
             ViewProvider="_viewProvider"
             ViewContext="employees"
             ShowExternalFilterBuilder="false"
             ShowSearch="false" />
```

### Scenario 3: Controlled search from the page

Bind the search term to a page-level property and suppress the internal search input:

```razor
<TmTextInput @bind-Value="_searchText" Placeholder="Search..." />

<TmDataTable TItem="Employee"
             Items="_employees"
             SearchText="_searchText"
             SearchTextChanged="v => _searchText = v"
             ShowSearch="false" />
```

> When `SearchText` is set externally, the component still applies it to client-side items or passes it to `IDataTableDataProvider`, even if `ShowSearch="false"`.

## Replacing local workarounds

Before 2.2.0, applications often hid unwanted chrome with CSS wrappers or conditional `@if` blocks around the component. Replace those workarounds with the explicit API:

| Old workaround | Replacement |
|----------------|-------------|
| CSS rule hiding `.tm-data-table-toolbar` | `ShowToolbar="false"` or `ToolbarMode="DataToolbarMode.ContentOnly"` |
| Wrapping the component to hide the view manager | `ShowViewManager="false"` |
| Custom search input + ignoring the built-in search | `SearchText` / `SearchTextChanged` + `ShowSearch="false"` |
| CSS hiding `.tm-data-table-external-filters` | `ShowExternalFilterBuilder="false"` |

## PromptHelper consumer notes

For PromptHelper-generated apps that previously wrapped `TmDataTable` or `TmMultiViewList` to avoid duplicate filtering:

1. Remove CSS or conditional wrappers that hide toolbar chrome.
2. Pass `ToolbarMode="DataToolbarMode.ContentOnly"` when the host page provides filters.
3. Pass `SearchText` and `SearchTextChanged` when the page owns the search input, and set `ShowSearch="false"`.
4. Keep `ToolbarMode="DataToolbarMode.Full"` (or omit it) for standalone tables that should keep the original behavior.

## Package version

Use package version **2.2.0** or later:

```bash
dotnet add package Tempo.Blazor --version 2.2.0
```

The `Tempo.Blazor.All` metapackage is also updated to 2.2.0 and references the new core package.
