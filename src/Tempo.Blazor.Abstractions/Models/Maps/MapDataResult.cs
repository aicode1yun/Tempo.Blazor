namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// One data set for a <c>TmMap</c> viewport: either individual markers, aggregated
/// cluster points, or both. Returned by <c>IMapDataProvider.GetPointsAsync</c> — the
/// provider decides per zoom level which representation to serve (hybrid clustering).
/// </summary>
/// <param name="Markers">Individual markers to render, or null/empty when the zoom level serves clusters.</param>
/// <param name="Clusters">Server-side aggregated cluster points, or null/empty when the zoom level serves markers.</param>
public record MapDataResult(
    IReadOnlyList<MapMarker>? Markers = null,
    IReadOnlyList<MapClusterPoint>? Clusters = null)
{
    /// <summary>An empty result — nothing to render for the requested viewport.</summary>
    public static MapDataResult Empty { get; } = new();
}
