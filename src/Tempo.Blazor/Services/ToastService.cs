using Tempo.Blazor.Components.Feedback;

namespace Tempo.Blazor.Services;

/// <summary>
/// Describes a single toast notification instance.
/// </summary>
public sealed record ToastInstance
{
    /// <summary>Unique identifier for the toast.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>Severity level of the toast (Info, Success, Warning, Error).</summary>
    public ToastSeverity Severity { get; init; }
    /// <summary>Message content of the toast.</summary>
    public string Message { get; init; } = string.Empty;
    /// <summary>Optional title for the toast.</summary>
    public string? Title { get; init; }
    /// <summary>Duration in milliseconds before auto-dismiss. Default is 5000ms.</summary>
    public int Duration { get; init; } = 5000;
    /// <summary>Timestamp when the toast was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Injectable service for showing toast notifications.
/// Register as Scoped in DI. Use <see cref="TmToastContainer"/> in layout to render.
/// </summary>
public sealed class ToastService
{
    private readonly List<ToastInstance> _toasts = [];
    private readonly object _lock = new();

    /// <summary>Fired when a new toast is added.</summary>
    public event Action? OnChange;

    /// <summary>Current active toasts (read-only snapshot).</summary>
    public IReadOnlyList<ToastInstance> Toasts
    {
        get
        {
            lock (_lock)
                return _toasts.ToList();
        }
    }

    /// <summary>Show a success toast.</summary>
    public void ShowSuccess(string message, string? title = null, int duration = 5000)
        => Add(ToastSeverity.Success, message, title, duration);

    /// <summary>Show an error toast.</summary>
    public void ShowError(string message, string? title = null, int duration = 8000)
        => Add(ToastSeverity.Error, message, title, duration);

    /// <summary>Show a warning toast.</summary>
    public void ShowWarning(string message, string? title = null, int duration = 6000)
        => Add(ToastSeverity.Warning, message, title, duration);

    /// <summary>Show an info toast.</summary>
    public void ShowInfo(string message, string? title = null, int duration = 5000)
        => Add(ToastSeverity.Info, message, title, duration);

    /// <summary>Remove a toast by ID.</summary>
    public void Remove(string id)
    {
        lock (_lock)
            _toasts.RemoveAll(t => t.Id == id);
        OnChange?.Invoke();
    }

    /// <summary>Remove all toasts.</summary>
    public void Clear()
    {
        lock (_lock)
            _toasts.Clear();
        OnChange?.Invoke();
    }

    private void Add(ToastSeverity severity, string message, string? title, int duration)
    {
        var toast = new ToastInstance
        {
            Severity = severity,
            Message = message,
            Title = title,
            Duration = duration
        };
        lock (_lock)
            _toasts.Add(toast);
        OnChange?.Invoke();
    }
}
