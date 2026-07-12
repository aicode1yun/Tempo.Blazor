using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Abstractions.Interfaces;

/// <summary>Supplies the set of recipients the digest runner should build digests for.</summary>
public interface INotificationRecipientSource
{
    /// <summary>Returns the recipients to consider for the current digest run.</summary>
    Task<IReadOnlyList<TmUserRef>> GetRecipientsAsync(CancellationToken cancellationToken = default);
}
