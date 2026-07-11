using System.Collections.Concurrent;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Models;

/// <summary>
/// In-memory <see cref="IDataTableLayoutStore"/> keyed by table + user for the lifetime of the
/// instance. Suitable for demos, tests, and prototypes.
/// </summary>
public sealed class InMemoryDataTableLayoutStore : IDataTableLayoutStore
{
    private readonly ConcurrentDictionary<string, DataTableLayout> _store = new(StringComparer.Ordinal);

    private static string Key(string viewContext, string? userId) => $"{userId ?? "*"}::{viewContext}";

    /// <inheritdoc />
    public Task<DataTableLayout?> LoadLayoutAsync(string viewContext, string? userId = null, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(Key(viewContext, userId), out var layout) ? layout.Clone() : null);

    /// <inheritdoc />
    public Task SaveLayoutAsync(string viewContext, DataTableLayout layout, string? userId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _store[Key(viewContext, userId)] = layout.Clone();
        return Task.CompletedTask;
    }
}
