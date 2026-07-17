using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Tempo.ReportServer.Web.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Shared, client-safe UI/data services registered symmetrically on both the
// InteractiveServer host and this WebAssembly leg (see AddCommonServices).
builder.Services.AddCommonServices(builder.Configuration);

await builder.Build().RunAsync();
