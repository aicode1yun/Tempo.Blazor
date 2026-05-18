namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Queues document editor aria-live announcements and exposes the current message.</summary>
public sealed class DocumentEditorAnnouncer
{
    private readonly Queue<DocumentEditorAnnouncement> _queue = new();

    /// <summary>Most recent message exposed to the live region.</summary>
    public string? CurrentMessage { get; private set; }

    /// <summary>Number of queued announcements waiting behind the current message.</summary>
    public int QueuedCount => _queue.Count;

    /// <summary>Adds a message to the queue and returns the active announcement.</summary>
    public DocumentEditorAnnouncement? Announce(string? message, DocumentEditorAnnouncementPoliteness politeness = DocumentEditorAnnouncementPoliteness.Polite)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var announcement = new DocumentEditorAnnouncement(message.Trim(), politeness, DateTimeOffset.UtcNow);
        if (CurrentMessage is null)
        {
            CurrentMessage = announcement.Message;
            return announcement;
        }

        _queue.Enqueue(announcement);
        return announcement;
    }

    /// <summary>Advances to the next queued announcement, if any.</summary>
    public DocumentEditorAnnouncement? DequeueNext()
    {
        if (_queue.Count == 0)
        {
            return null;
        }

        var next = _queue.Dequeue();
        CurrentMessage = next.Message;
        return next;
    }

    /// <summary>Clears current and queued announcements.</summary>
    public void Clear()
    {
        _queue.Clear();
        CurrentMessage = null;
    }
}

/// <summary>One document editor aria-live announcement.</summary>
public sealed record DocumentEditorAnnouncement(
    string Message,
    DocumentEditorAnnouncementPoliteness Politeness,
    DateTimeOffset CreatedAt);

/// <summary>Aria-live politeness level.</summary>
public enum DocumentEditorAnnouncementPoliteness
{
    /// <summary>Announce when the assistive technology is idle.</summary>
    Polite,

    /// <summary>Announce immediately.</summary>
    Assertive
}
