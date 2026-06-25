namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Authorizes user actions against shared Tempo entity references.</summary>
public interface ITmAuthorizationProvider
{
    /// <summary>Evaluates whether the requested action is allowed.</summary>
    /// <param name="request">Authorization request to evaluate.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    ValueTask<TmAuthorizationResult> AuthorizeAsync(
        TmAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}
