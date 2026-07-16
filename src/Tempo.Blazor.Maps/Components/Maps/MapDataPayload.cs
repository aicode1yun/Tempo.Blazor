using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Maps;

/// <summary>
/// One atomic data set handed to the JS <c>setData</c> function. The JS side builds all
/// layers from this payload and swaps them in a single synchronous pass, so the map never
/// shows the old and new data set at the same time.
/// </summary>
/// <param name="Markers">Individual markers (Canvas rendered; clustered client-side when <paramref name="ClientClustering"/> is true).</param>
/// <param name="Clusters">Server-side aggregated cluster bubbles.</param>
/// <param name="ClientClustering">True to group <paramref name="Markers"/> into a leaflet.markercluster group.</param>
internal sealed record MapDataPayload(
    IReadOnlyList<MapMarker> Markers,
    IReadOnlyList<MapClusterPayloadItem> Clusters,
    bool ClientClustering);

/// <summary>
/// A <see cref="MapClusterPoint"/> enriched for rendering: culture-formatted count text and
/// a localized aria label, both produced on the .NET side so JS stays presentation-only.
/// </summary>
/// <param name="Key">Host-defined cluster key reported back by cluster clicks.</param>
/// <param name="Latitude">Cluster centroid latitude.</param>
/// <param name="Longitude">Cluster centroid longitude.</param>
/// <param name="Count">Number of aggregated points.</param>
/// <param name="CountText">Count formatted with the current culture (shown inside the bubble).</param>
/// <param name="AriaLabel">Localized accessible label for the cluster bubble.</param>
/// <param name="Tooltip">Optional tooltip text.</param>
internal sealed record MapClusterPayloadItem(
    string? Key,
    double Latitude,
    double Longitude,
    int Count,
    string CountText,
    string AriaLabel,
    string? Tooltip);
