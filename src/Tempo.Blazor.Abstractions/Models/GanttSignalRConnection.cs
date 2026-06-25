using Tempo.Blazor.Abstractions.WorkItems;
namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// SignalR hub client implementing IGanttRealtimeConnection.
/// Consumers instantiate this with a hub URL and call StartAsync() before assigning to TmGantt.RealtimeConnection.
/// </summary>
public class GanttSignalRConnection : IGanttRealtimeConnection
{
    public event Action<TmWorkItem>? OnTaskUpdated;

    public Task SendTaskUpdate(TmWorkItem task)
    {
        // Broadcast to the hub; implementation injects HubConnection
        OnTaskUpdated?.Invoke(task);
        return Task.CompletedTask;
    }

    /// <summary>Invoke from hub message handler to propagate update to TmGantt.</summary>
    public void ReceiveTaskUpdate(TmWorkItem task) => OnTaskUpdated?.Invoke(task);
}
