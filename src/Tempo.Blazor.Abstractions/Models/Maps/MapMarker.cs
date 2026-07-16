namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// A single point rendered on a <c>TmMap</c>. Batches of markers are drawn with a Canvas
/// renderer, so instances stay lightweight data records without any UI dependencies.
/// </summary>
/// <param name="Id">Host-defined identifier reported back by marker click events. May be null for non-interactive points.</param>
/// <param name="Latitude">Latitude in decimal degrees (WGS84).</param>
/// <param name="Longitude">Longitude in decimal degrees (WGS84).</param>
/// <param name="Title">Optional tooltip text shown on hover. Null renders the marker without a tooltip.</param>
public record MapMarker(string? Id, double Latitude, double Longitude, string? Title = null);
