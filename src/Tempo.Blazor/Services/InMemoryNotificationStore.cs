using System.Collections.Concurrent;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Services;

/// <summary>
/// In-memory implementation of <see cref="ITmNotificationService"/> for demos and tests.
/// </summary>
public class InMemoryNotificationStore : ITmNotificationService
{
    private readonly ConcurrentDictionary<string, List<TmNotification>> _store = new(StringComparer.OrdinalIgnoreCase);
    private int _unreadCount;

    /// <inheritdoc />
    public TmNotificationServiceCapabilities Capabilities
        => TmNotificationServiceCapabilities.Publish
        | TmNotificationServiceCapabilities.Read
        | TmNotificationServiceCapabilities.Query
        | TmNotificationServiceCapabilities.UnreadCount
        | TmNotificationServiceCapabilities.ReadState
        | TmNotificationServiceCapabilities.DeliveryAck;

    TmNotificationServiceCapabilities ITmCapabilityProvider<TmNotificationServiceCapabilities>.Capabilities => Capabilities;

    /// <summary>Total unread notification count across recipients.</summary>
    public int UnreadCount => _unreadCount;

    /// <inheritdoc />
    public event Action? OnChanged;

    /// <inheritdoc />
    public Task<TmNotification> PublishAsync(TmNotification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = Normalize(notification);
        var recipientUserId = normalized.EffectiveRecipientUserId;

        var list = _store.GetOrAdd(recipientUserId, _ => []);
        lock (list)
        {
            list.Insert(0, Clone(normalized));
        }

        if (!normalized.IsRead)
            Interlocked.Increment(ref _unreadCount);

        NotifyChanged();
        return Task.FromResult(Clone(normalized));
    }

    /// <inheritdoc />
    public Task MarkAsReadAsync(string notificationId, string recipientUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_store.TryGetValue(recipientUserId, out var list))
        {
            lock (list)
            {
                var n = list.FirstOrDefault(x => string.Equals(x.Id, notificationId, StringComparison.OrdinalIgnoreCase));
                if (n is not null && !n.IsRead)
                {
                    n.ReadAt = DateTimeOffset.UtcNow;
                    DecrementUnread();
                    NotifyChanged();
                }
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkAsDeliveredAsync(string notificationId, string recipientUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_store.TryGetValue(recipientUserId, out var list))
        {
            lock (list)
            {
                var n = list.FirstOrDefault(x => string.Equals(x.Id, notificationId, StringComparison.OrdinalIgnoreCase));
                if (n is not null && !n.IsDelivered)
                {
                    n.DeliveredAt = DateTimeOffset.UtcNow;
                    NotifyChanged();
                }
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkAllAsReadAsync(string recipientUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_store.TryGetValue(recipientUserId, out var list))
        {
            int previouslyUnread;
            lock (list)
            {
                previouslyUnread = list.Count(x => !x.IsRead);
                foreach (var n in list.Where(x => !x.IsRead))
                {
                    n.ReadAt = DateTimeOffset.UtcNow;
                }
            }
            if (previouslyUnread > 0)
            {
                AddUnread(-previouslyUnread);
                NotifyChanged();
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TmNotification>> GetNotificationsAsync(
        TmNotificationQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_store.TryGetValue(query.RecipientUserId, out var list))
            return Task.FromResult<IReadOnlyList<TmNotification>>(Array.Empty<TmNotification>());

        lock (list)
        {
            var result = ApplyQuery(list, query)
                .Select(Clone)
                .ToList();
            return Task.FromResult<IReadOnlyList<TmNotification>>(result);
        }
    }

    /// <inheritdoc />
    public Task<int> GetUnreadCountAsync(string recipientUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_store.TryGetValue(recipientUserId, out var list))
            return Task.FromResult(0);

        lock (list)
        {
            return Task.FromResult(list.Count(x => !x.IsRead));
        }
    }

    /// <summary>Clears all stored notifications.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _unreadCount, 0);
        NotifyChanged();
    }

    /// <summary>Clears all stored notifications and unread state.</summary>
    public void ClearAll()
    {
        _store.Clear();
        Interlocked.Exchange(ref _unreadCount, 0);
        NotifyChanged();
    }

    private static IEnumerable<TmNotification> ApplyQuery(IReadOnlyList<TmNotification> notifications, TmNotificationQuery query)
    {
        IEnumerable<TmNotification> result = notifications;

        if (!query.IncludeRead)
            result = result.Where(notification => !notification.IsRead);

        if (query.EntityRef is { } entityRef)
            result = result.Where(notification => notification.EntityRef?.Equals(entityRef) == true);
        else
        {
            if (!string.IsNullOrWhiteSpace(query.EntityType))
                result = result.Where(notification => string.Equals(notification.EntityRef?.EntityType, query.EntityType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.EntityId))
                result = result.Where(notification => string.Equals(notification.EntityRef?.EntityId, query.EntityId, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
            result = result.Where(notification => string.Equals(notification.Type, query.Type, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            result = result.Where(notification =>
                Contains(notification.Title, searchText) ||
                Contains(notification.Body, searchText) ||
                Contains(notification.Actor?.Id, searchText) ||
                Contains(notification.Actor?.DisplayName, searchText));
        }

        return result
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 1, 200));
    }

    private static TmNotification Normalize(TmNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var recipientUserId = notification.EffectiveRecipientUserId.Trim();
        if (string.IsNullOrWhiteSpace(recipientUserId))
            throw new ArgumentException("Recipient user id is required.", nameof(notification));

        var normalized = Clone(notification);
        normalized.Id = string.IsNullOrWhiteSpace(normalized.Id) ? Guid.NewGuid().ToString("N") : normalized.Id.Trim();
        normalized.RecipientUserId = recipientUserId;
        normalized.Type = normalized.Type.Trim();
        normalized.Title = normalized.Title.Trim();
        normalized.CreatedAt = normalized.CreatedAt == default ? DateTimeOffset.UtcNow : normalized.CreatedAt.ToUniversalTime();
        normalized.ReadAt = normalized.ReadAt?.ToUniversalTime();
        normalized.DeliveredAt = normalized.DeliveredAt?.ToUniversalTime();
        return normalized;
    }

    private static TmNotification Clone(TmNotification notification)
        => new()
        {
            Id = notification.Id,
            RecipientUserId = notification.RecipientUserId,
            Recipient = notification.Recipient is null ? null : Clone(notification.Recipient),
            Actor = notification.Actor is null ? null : Clone(notification.Actor),
            Type = notification.Type,
            Title = notification.Title,
            Body = notification.Body,
            Severity = notification.Severity,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            DeliveredAt = notification.DeliveredAt,
            ActionUrl = notification.ActionUrl,
            EntityRef = notification.EntityRef?.Normalize(),
            CorrelationId = notification.CorrelationId,
            Metadata = notification.Metadata is null ? null : new Dictionary<string, object>(notification.Metadata, StringComparer.OrdinalIgnoreCase)
        };

    private static TmUserRef Clone(TmUserRef user)
        => new()
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            UserName = user.UserName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Color = user.Color,
            IsVirtual = user.IsVirtual,
            SourceKey = user.SourceKey,
            TenantId = user.TenantId
        };

    private static bool Contains(string? value, string searchText)
        => value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;

    private void AddUnread(int delta)
    {
        var next = Interlocked.Add(ref _unreadCount, delta);
        if (next >= 0)
            return;

        Interlocked.Exchange(ref _unreadCount, 0);
    }

    private void DecrementUnread()
        => AddUnread(-1);

    private void NotifyChanged()
    {
        OnChanged?.Invoke();
    }
}
