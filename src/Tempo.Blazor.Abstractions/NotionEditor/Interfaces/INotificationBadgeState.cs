namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotificationBadgeState
{
    int UnreadCount { get; }
    event Action? OnChanged;
    void Increment();
    void Reset();
}
