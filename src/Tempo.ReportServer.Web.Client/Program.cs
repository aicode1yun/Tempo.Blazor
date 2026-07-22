using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Web.Client;
using Tempo.ReportServer.Web.Services;
using Tempo.Reporting.Abstractions.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Deserialize the auth state serialized by the host so [Authorize]/AuthorizeView work in the
// browser runtime without a second login or a second OIDC client.
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

// Same-origin "Host" client used only to fetch short-lived access tokens from /auth/token.
builder.Services.AddHttpClient(
    WasmAccessTokenProvider.HostHttpClientName,
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

// The WebAssembly leg of IAccessTokenProvider (in-memory token, re-fetched from /auth/token).
builder.Services.AddScoped<IAccessTokenProvider, WasmAccessTokenProvider>();

// Shared, client-safe UI/data services registered symmetrically on both the
// InteractiveServer host and this WebAssembly leg (see AddCommonServices). This also registers the
// typed ITempoReportServerClient, which resolves the IAccessTokenProvider above.
builder.Services.AddCommonServices(builder.Configuration);

await builder.Build().RunAsync();
