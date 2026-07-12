# `data-testid` convention (K9)

Every reusable Tempo.Blazor component emits its internal test ids through a single
shared helper so that end-to-end tests get **stable, namespaceable** selectors without
the library ever hard-coding a brittle literal.

## The base class

All library components inherit `Tempo.Blazor.Components.TmComponentBase`
([`src/Tempo.Blazor/Components/TmComponentBase.cs`](../src/Tempo.Blazor/Components/TmComponentBase.cs)):

```csharp
public abstract class TmComponentBase : ComponentBase
{
    /// <summary>Optional explicit data-testid for the component's root element.</summary>
    [Parameter] public string? DataTestId { get; set; }

    /// <summary>Optional namespace applied to every internal test id.</summary>
    [Parameter] public string? TestIdPrefix { get; set; }

    protected string TestId(string name)
        => string.IsNullOrEmpty(TestIdPrefix) ? name : $"{TestIdPrefix}-{name}";

    protected string? RootTestId(string? defaultName = null)
        => !string.IsNullOrEmpty(DataTestId) ? DataTestId
        :  !string.IsNullOrEmpty(defaultName) ? TestId(defaultName)
        :  null;
}
```

## Authoring rule

In markup, **never write a literal `data-testid`** — bind it through `@TestId(...)`:

```razor
@* ✔ correct *@
<button data-testid="@TestId("save")">…</button>

@* ✔ dynamic parts stay inside the interpolation *@
<div data-testid="@TestId($"row-{item.Id}")">…</div>

@* ✘ rejected — a literal will fail DataTestIdConventionGuardTests *@
<button data-testid="save">…</button>
```

In a `.razor.cs` `RenderTreeBuilder`, pass `TestId("save")` instead of the literal.

## Why prefixing

`TestId` is identity when `TestIdPrefix` is null, so **every existing selector keeps
working** — the migration was fully backward compatible (2.0.x consumers compile and
behave unchanged). When two instances of the same component appear on one page, a host
can disambiguate their internal ids:

```razor
<TmDataTable TestIdPrefix="orders" … />   @* → data-testid="orders-search-input" *@
<TmDataTable TestIdPrefix="invoices" … /> @* → data-testid="invoices-search-input" *@
```

`DataTestId` overrides the component's root test id (resolve it with `RootTestId("default")`).

## Enforcement

`tests/Tempo.Blazor.Tests/Components/DataTestIdConventionGuardTests.cs` is a source-text
sweep over every library `.razor`: any `data-testid` whose value does not start with `@`
(a literal, or a mixed `foo-@bar` value that would not be prefixable) fails the build.
Demo/app projects (`*.Demo*`, `ReportServer.Web`) are out of scope.
