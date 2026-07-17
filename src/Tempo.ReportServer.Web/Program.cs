using Tempo.ReportServer.Api;
using Tempo.ReportServer.Api.Security;
using Tempo.ReportServer.Web;
using Tempo.ReportServer.Web.Client;
using Tempo.ReportServer.Web.Services;
using Tempo.Reporting.Abstractions.Dtos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Cookie + OpenID Connect (Keycloak) BFF authentication with a server-side token store.
// No-op when "Authentication:Oidc" is unconfigured, so the self-contained demo keeps running.
// NOTE (Fáze 5 carry-forward): the WASM leg (/auth/token minimal endpoint,
// WasmAccessTokenProvider, AddAuthenticationStateSerialization) is intentionally NOT wired here —
// this host is InteractiveServer, so the server-side ServerAccessTokenProvider is sufficient.
var oidcOptions = builder.AddReportServerWebAuthentication();

// Typed client for the persistent report server API host (base URL from configuration,
// same "ReportServer:BaseUrl" key used on the API side). Registered when configured so the
// self-contained demo keeps working without a running API host; catalog pages are migrated
// off the in-memory ReportServerCatalogStore onto this client as the API host is deployed.
if (!string.IsNullOrWhiteSpace(builder.Configuration[ReportServerClientExtensions.BaseUrlConfigurationKey]))
{
    var apiBaseUrl = new Uri(builder.Configuration[ReportServerClientExtensions.BaseUrlConfigurationKey]!, UriKind.Absolute);
    var clientBuilder = builder.Services.AddHttpClient<ITempoReportServerClient, TempoReportServerClient>(
        client => client.BaseAddress = apiBaseUrl);
    if (oidcOptions.IsConfigured)
    {
        // Forward the signed-in user's access token to the API host (BFF pattern).
        clientBuilder.AddHttpMessageHandler<ReportServerAccessTokenHandler>();
    }
}
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "ReportServerEmbeddingDemo",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});
// Client-safe UI/data services shared with the WebAssembly leg (symmetric DI).
builder.Services.AddCommonServices(builder.Configuration);

// Host-only (server) services: report-server security + demo API key store.
builder.Services.AddSingleton<IReportApiKeyStore, DemoReportApiKeyStore>();
builder.Services.AddReportServerSecurity();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

UseProjectStaticAssets(app, "Tempo.Blazor.Reporting");
UseProjectStaticAssets(app, "Tempo.Blazor");
app.UseStaticFiles();
app.UseCors("ReportServerEmbeddingDemo");
if (oidcOptions.IsConfigured)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.UseAntiforgery();

app.MapReportServerDemoApi();
if (oidcOptions.IsConfigured)
{
    app.MapGet("/account/login", (string? returnUrl) => Results.Challenge(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
        [Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme]));
    app.MapPost("/account/logout", () => Results.SignOut(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" },
        [
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
            Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme,
        ]));
}
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Tempo.ReportServer.Web.Client._Imports).Assembly);

app.Run();

static void UseProjectStaticAssets(WebApplication app, string projectName)
{
    var assetRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", projectName, "wwwroot"));
    if (!Directory.Exists(assetRoot))
    {
        return;
    }

    var normalizedRoot = assetRoot.EndsWith(Path.DirectorySeparatorChar)
        ? assetRoot
        : assetRoot + Path.DirectorySeparatorChar;
    var requestPrefix = $"/_content/{projectName}";

    app.Use(async (context, next) =>
    {
        if (!HttpMethods.IsGet(context.Request.Method) &&
            !HttpMethods.IsHead(context.Request.Method))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var requestPath = context.Request.Path.Value;
        if (requestPath is null ||
            !requestPath.StartsWith(requestPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var relativePath = requestPath[requestPrefix.Length..].TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.Ordinal) || !File.Exists(fullPath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = ContentTypeFor(fullPath);
        context.Response.ContentLength = new FileInfo(fullPath).Length;
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await context.Response.SendFileAsync(fullPath).ConfigureAwait(false);
        }
    });
}

static string ContentTypeFor(string path)
    => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".css" => "text/css; charset=utf-8",
        ".html" => "text/html; charset=utf-8",
        ".js" or ".mjs" => "application/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".png" => "image/png",
        ".svg" => "image/svg+xml",
        ".woff2" => "font/woff2",
        _ => "application/octet-stream",
    };
