namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// Declares which operations a <see cref="ITmWorkItemProvider"/> supports so consumers
/// can enable/disable UI affordances (e.g. drag to reschedule, create, link dependencies).
/// </summary>
[Flags]
public enum TmWorkItemCapabilities
{
    /// <summary>No capabilities.</summary>
    None = 0,

    /// <summary>Read / query items.</summary>
    Read = 1 << 0,

    /// <summary>Create new items.</summary>
    Create = 1 << 1,

    /// <summary>Update existing items.</summary>
    Update = 1 << 2,

    /// <summary>Delete items.</summary>
    Delete = 1 << 3,

    /// <summary>Supports parent/child hierarchy.</summary>
    Hierarchy = 1 << 4,

    /// <summary>Supports dependencies between items.</summary>
    Dependencies = 1 << 5,

    /// <summary>Supports scheduling (Start/End dates).</summary>
    Scheduling = 1 << 6,

    /// <summary>Read + write (Create | Update | Delete).</summary>
    ReadWrite = Read | Create | Update | Delete,

    /// <summary>Everything.</summary>
    All = Read | Create | Update | Delete | Hierarchy | Dependencies | Scheduling
}
