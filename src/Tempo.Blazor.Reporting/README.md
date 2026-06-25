# Tempo.Blazor.Reporting

Blazor components and client-side services for Tempo Reporting. The package provides a viewer,
parameter panel, report explorer, lightweight designer, embedded engine integration, and remote
Report Server integration.

## Setup

```csharp
using Tempo.Blazor.Reporting.Configuration;

builder.Services.AddTempoBlazorReporting();
```

Add the stylesheet:

```html
<link href="_content/Tempo.Blazor.Reporting/css/tempo-blazor-reporting.css" rel="stylesheet" />
```

## Components

| Component | Purpose |
|-----------|---------|
| `TmReportViewer` | Renders report snapshots on a canvas with paging, zoom, refresh, print, parameter toggle, and PDF/CSV/XLSX export actions. |
| `TmReportParameterPanel` | Renders string, number, date, boolean, single-select, and multi-select parameters with required-value validation. |
| `TmReportExplorer` | Folder tree plus grid/list catalog for stored reports, search, open actions, and optional folder management. |
| `TmReportDesigner` | Lightweight report definition editor with page setup, bands, element palette, field list, validation, preview, and save/publish events. |

## Embedded Viewer

Use `EmbeddedReportSource` when the Blazor app owns the definition and can provide data locally.

```razor
@using Tempo.Blazor.Reporting.Components
@using Tempo.Blazor.Reporting.Models
@using Tempo.Blazor.Reporting.Services

<TmReportViewer ReportSource="_source"
                TenantId="northwind"
                UserId="embedded-user"
                CultureName="en-US" />

@code {
    private IReportSource? _source;

    protected override void OnInitialized()
    {
        _source = new EmbeddedReportSource(ReportDefinitions.SalesSummary(), DataProvider);
    }
}
```

## Remote Viewer

Use `RemoteReportSource` when reports are rendered by Tempo Report Server or another compatible
HTTP endpoint.

```razor
@using Tempo.Blazor.Reporting.Components
@using Tempo.Blazor.Reporting.Models
@using Tempo.Blazor.Reporting.Services
@inject IHttpClientFactory HttpClientFactory

<TmReportViewer ReportSource="_source" />

@code {
    private IReportSource? _source;

    protected override void OnInitialized()
    {
        var client = HttpClientFactory.CreateClient("Reports");
        _source = new RemoteReportSource(client, "sales-summary");
    }
}
```

For Blazor WebAssembly, keep API keys on a backend-for-frontend and proxy remote rendering through
that backend. Server-side Blazor can attach report API keys directly to the named `HttpClient`.
