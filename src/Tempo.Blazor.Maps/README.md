# Tempo.Blazor.Maps

Interactive Leaflet maps for Blazor (WebAssembly, Server, InteractiveAuto) with **fully bundled
assets** — Leaflet 1.9.4 and leaflet.markercluster 1.5.3 ship inside the package, so there is
**no CDN dependency** (offline/CSP-safe).

## Features

- **`TmMap`** — Leaflet map with OpenStreetMap tiles (attribution always visible), pan/zoom, and
  fractional zoom levels.
- **Canvas marker batches** — thousands of points render through a shared `L.canvas` renderer,
  never as DOM markers.
- **Atomic data swaps** — replacing markers or clusters happens in one synchronous JS pass; the
  map never double-renders or flashes empty.
- **Client-side clustering** — `UseClientClustering` groups markers with leaflet.markercluster
  (zoom-to-bounds, spiderfy), or drive cluster groups imperatively via `AddClusterGroupAsync`
  / `AddMarkersToClusterAsync`.
- **Server-side clusters** — feed pre-aggregated `MapClusterPoint`s and get count bubbles with
  culture-formatted counts and localized aria labels; a `Count == 1` cluster renders as a plain
  marker.
- **`IMapDataProvider`** — optional data source called after the viewport settles (debounced,
  default 300 ms) with cancellation of superseded requests; hybrid clusters↔markers switching
  by zoom level is a one-interface implementation.
- **Events** — `OnMarkerClick`, `OnClusterClick` (with the server cluster key), `OnZoomChanged`,
  `OnMapClick`, `OnDataError`.
- **Design system** — styling via `--tm-*` tokens (dark mode included), localization via
  `ITmLocalizer` (en/cs/fr built in), WCAG-friendly markup.

## Getting started

```csharp
// Program.cs — Tempo.Blazor core services
builder.Services.AddTempoBlazor();
```

```html
<!-- index.html / App.razor — design-system layer for the map -->
<link href="_content/Tempo.Blazor/css/tempo-blazor.css" rel="stylesheet" />
<link href="_content/Tempo.Blazor.Maps/css/tempo-blazor-maps.css" rel="stylesheet" />
<!-- Leaflet JS/CSS are loaded automatically by the component from the bundled assets. -->
```

```razor
@using Tempo.Blazor.Components.Maps
@using Tempo.Blazor.Abstractions.Models

<TmMap CenterLatitude="49.8175"
       CenterLongitude="15.473"
       Zoom="7.5"
       Height="480px"
       Markers="@markers"
       OnMarkerClick="HandleMarkerClick" />

@code {
    private readonly IReadOnlyList<MapMarker> markers =
    [
        new("praha", 50.0755, 14.4378, "Praha"),
        new("brno", 49.1951, 16.6068, "Brno"),
    ];

    private void HandleMarkerClick(MapMarker marker) { /* ... */ }
}
```

### Client-side clustering

```razor
<TmMap Markers="@manyMarkers"
       UseClientClustering="true"
       OnClusterClick="HandleClusterClick" />
```

### Hybrid provider (server-side aggregation)

```csharp
public sealed class PriceMapProvider : IMapDataProvider
{
    public async Task<MapDataResult> GetPointsAsync(MapViewport viewport, CancellationToken ct)
    {
        return viewport.Zoom >= 12
            ? new MapDataResult(Markers: await LoadMarkersAsync(viewport, ct))
            : new MapDataResult(Clusters: await LoadGridClustersAsync(viewport, ct));
    }
}
```

```razor
<TmMap DataProvider="@provider"
       OnClusterClick="DrillDown"
       OnDataError="ShowError" />
```

## Imperative API

| Method | Purpose |
|--------|---------|
| `SetViewAsync(MapViewport)` | Move the map (no animation). |
| `GetViewportAsync()` | Read the current center + zoom. |
| `SetMarkersAsync` / `SetClusterPointsAsync` | Atomically replace the data set. |
| `AddMarkersAsync` | Add a Canvas marker batch on top. |
| `AddClusterGroupAsync` / `AddMarkerToClusterAsync` / `AddMarkersToClusterAsync` | Imperative client clustering. |
| `ClearMarkersAsync` | Remove all markers, clusters, and data layers. |
| `InvalidateSizeAsync` | Recalculate size after the container becomes visible (hidden tab). |
| `RefreshDataAsync` | Ask the provider for fresh data immediately. |

## Theming

The marker color comes from the `--tm-map-marker-color` token (defaults to
`--tm-color-danger`); cluster bubbles use `--tm-color-primary`. Override per app or per map:

```css
.my-page .tm-map {
    --tm-map-marker-color: var(--tm-color-primary);
}
```

## License

MIT — see the repository license. Map data © OpenStreetMap contributors.
