using Tempo.Blazor.Models;

namespace Tempo.Blazor.Interfaces;

/// <summary>
/// Per-user persistence hook for a data table's column layout (widths and pin state).
/// Separate from <see cref="IDataTableViewProvider"/> (saved views): a layout store keeps the
/// single "current" layout of a table for a user, so resized/pinned columns survive reloads.
/// </summary>
public interface IDataTableLayoutStore
{
    /// <summary>Loads the stored layout for a table and user, or null when none is stored.</summary>
    /// <param name="viewContext">Table instance identifier (matches TmDataTable.ViewContext).</param>
    /// <param name="userId">Optional user scope.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<DataTableLayout?> LoadLayoutAsync(string viewContext, string? userId = null, CancellationToken ct = default);

    /// <summary>Persists the layout for a table and user.</summary>
    /// <param name="viewContext">Table instance identifier (matches TmDataTable.ViewContext).</param>
    /// <param name="layout">Layout to persist.</param>
    /// <param name="userId">Optional user scope.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveLayoutAsync(string viewContext, DataTableLayout layout, string? userId = null, CancellationToken ct = default);
}
