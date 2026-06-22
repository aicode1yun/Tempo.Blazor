namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Provider for resolving the user currently interacting with Tempo.Blazor components.</summary>
public interface ITmCurrentUser
{
    /// <summary>Gets the current user state.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    ValueTask<TmCurrentUserState> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
