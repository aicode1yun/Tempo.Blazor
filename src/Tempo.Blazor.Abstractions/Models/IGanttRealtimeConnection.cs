using Tempo.Blazor.Abstractions.WorkItems;
namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Abstraction for real-time (SignalR) connectivity in TmGantt.
/// Implement this interface to receive live task updates from a server hub.
/// </summary>
public interface IGanttRealtimeConnection
{
    /// <summary>Raised when a remote task update arrives.</summary>
    event Action<TmWorkItem> OnTaskUpdated;

    /// <summary>Broadcasts a local task change to the hub.</summary>
    Task SendTaskUpdate(TmWorkItem task);
}
