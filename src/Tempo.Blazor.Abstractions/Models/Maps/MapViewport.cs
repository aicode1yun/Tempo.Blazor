namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// The visible area of a <c>TmMap</c>: the map center and the current zoom level.
/// Reported by viewport-changing events (zoom, pan) and accepted by <c>SetViewAsync</c>.
/// </summary>
/// <param name="Latitude">Latitude of the map center in decimal degrees (WGS84).</param>
/// <param name="Longitude">Longitude of the map center in decimal degrees (WGS84).</param>
/// <param name="Zoom">Leaflet zoom level. Fractional values are supported (e.g. 7.5).</param>
public record MapViewport(double Latitude, double Longitude, double Zoom);
