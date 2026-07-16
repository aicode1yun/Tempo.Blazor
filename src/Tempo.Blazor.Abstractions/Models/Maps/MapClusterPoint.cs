namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// A server-side aggregated cluster rendered on a <c>TmMap</c> as a count bubble.
/// Produced by grid/tile aggregation on the backend (e.g. lat/lon cell key), so the
/// client never has to hold the individual points.
/// </summary>
/// <param name="Key">Host-defined cluster key (e.g. an encoded grid cell) reported back by cluster click events.</param>
/// <param name="Latitude">Latitude of the cluster centroid in decimal degrees (WGS84).</param>
/// <param name="Longitude">Longitude of the cluster centroid in decimal degrees (WGS84).</param>
/// <param name="Count">Number of aggregated points. A cluster with Count = 1 renders as a plain marker.</param>
/// <param name="Tooltip">Optional tooltip text shown on hover (e.g. an aggregated average). Null renders without a tooltip.</param>
public record MapClusterPoint(string? Key, double Latitude, double Longitude, int Count, string? Tooltip = null);
