using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components;

/// <summary>
/// Shared base for Tempo.Blazor components that standardises the <c>data-testid</c> convention.
/// <para>
/// Components render their internal test ids through <see cref="TestId(string)"/>: when a host sets
/// <see cref="TestIdPrefix"/>, every internal id becomes <c>"{prefix}-{name}"</c> so multiple instances
/// of the same component on one page don't collide; when the prefix is null the bare name is kept, so
/// existing E2E selectors keep working (fully backward compatible). <see cref="DataTestId"/> overrides
/// the component's root test id (use <see cref="RootTestId(string?)"/> to resolve it).
/// </para>
/// </summary>
public abstract class TmComponentBase : ComponentBase
{
    /// <summary>
    /// Overrides the component's root <c>data-testid</c>. When null the component's own default is used
    /// (or the <see cref="TestIdPrefix"/>-namespaced default via <see cref="RootTestId(string?)"/>).
    /// </summary>
    [Parameter] public string? DataTestId { get; set; }

    /// <summary>
    /// When set, namespaces every internal <c>data-testid</c> of this component as <c>"{TestIdPrefix}-{name}"</c>
    /// so multiple instances of the same component on one page produce unique, targetable test ids.
    /// </summary>
    [Parameter] public string? TestIdPrefix { get; set; }

    /// <summary>Resolves an internal test id, applying <see cref="TestIdPrefix"/> when one is set.</summary>
    /// <param name="name">The component-local, convention id (e.g. <c>"row-editing"</c>).</param>
    protected string TestId(string name)
        => string.IsNullOrEmpty(TestIdPrefix) ? name : $"{TestIdPrefix}-{name}";

    /// <summary>
    /// Resolves the component's root <c>data-testid</c>: <see cref="DataTestId"/> when the host set it,
    /// otherwise the (prefix-aware) <paramref name="defaultName"/>, or null when there is no default.
    /// </summary>
    /// <param name="defaultName">The component's built-in root id, or null when it has none.</param>
    protected string? RootTestId(string? defaultName = null)
        => !string.IsNullOrEmpty(DataTestId) ? DataTestId
        : !string.IsNullOrEmpty(defaultName) ? TestId(defaultName)
        : null;
}
