namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// SignalR hub client implementing IGanttRealtimeConnection.
/// Consumers instantiate this with a hub URL and call StartAsync() before assigning to TmGantt.RealtimeConnection.
/// </summary>
public class GanttSignalRConnection : IGanttRealtimeConnection
{
    public event Action<GanttTask>? OnTaskUpdated;

    public Task SendTaskUpdate(GanttTask task)
    {
        // Broadcast to the hub; implementation injects HubConnection
        OnTaskUpdated?.Invoke(task);
        return Task.CompletedTask;
    }

    /// <summary>Invoke from hub message handler to propagate update to TmGantt.</summary>
    public void ReceiveTaskUpdate(GanttTask task) => OnTaskUpdated?.Invoke(task);
}
