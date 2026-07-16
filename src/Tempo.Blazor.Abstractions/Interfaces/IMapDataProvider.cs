using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Abstractions.Interfaces;

/// <summary>
/// Optional data source for <c>TmMap</c>: the component asks for fresh points whenever the
/// viewport settles (pan/zoom, debounced), cancelling the previous request first. Implementations
/// typically aggregate on the server and switch between clusters and individual markers by zoom.
/// The component stays fully usable without a provider (imperative <c>SetMarkersAsync</c> mode).
/// </summary>
public interface IMapDataProvider
{
    /// <summary>
    /// Loads the points visible in the given viewport. Called after the viewport settles;
    /// <paramref name="cancellationToken"/> is cancelled when a newer viewport supersedes this request.
    /// </summary>
    /// <param name="viewport">Current map center and zoom level.</param>
    /// <param name="cancellationToken">Cancelled when a newer request supersedes this one.</param>
    /// <returns>Markers and/or clusters to render for the viewport.</returns>
    Task<MapDataResult> GetPointsAsync(MapViewport viewport, CancellationToken cancellationToken);
}
