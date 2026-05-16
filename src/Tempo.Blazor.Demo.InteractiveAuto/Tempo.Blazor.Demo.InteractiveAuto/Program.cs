using Tempo.Blazor.Configuration;
using Tempo.Blazor.Demo.Services;
using Tempo.Blazor.Demo.Validators;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.FluentValidation;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Services;

// Import Components namespace for App.razor
using Components = Tempo.Blazor.Demo.InteractiveAuto.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// HttpClient for API calls
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["DemoApi:BaseUrl"] ?? "https://localhost:5100")
});

builder.Services.AddHttpClient("DemoApi", c =>
    c.BaseAddress = new Uri(builder.Configuration["DemoApi:BaseUrl"] ?? "https://localhost:5100"));

// Register SharedUI services
builder.Services.AddScoped<PersonHttpDataProvider>();
builder.Services.AddScoped<ActivityHttpService>();
builder.Services.AddScoped<AttachmentHttpProvider>();
builder.Services.AddScoped<ImageHttpGalleryProvider>();
builder.Services.AddScoped<ViewHttpProvider>();
builder.Services.AddScoped<DemoDocumentEditorProvider>();
builder.Services.AddScoped<DemoDocumentCollaborationProvider>();
builder.Services.AddScoped(sp =>
{
    var baseUri = sp.GetRequiredService<IHttpClientFactory>().CreateClient("DemoApi").BaseAddress!.ToString().TrimEnd('/');
    return new SignalRDocumentCollaborationProvider($"{baseUri}/hubs/document-editor-collaboration");
});
builder.Services.AddScoped<DemoDocumentSuggestionProvider>();
builder.Services.AddScoped<DemoDocumentFormatProvider>();
builder.Services.AddScoped<DemoDocumentPdfExportProvider>();
builder.Services.AddScoped<DemoDocumentComparisonProvider>();
builder.Services.AddScoped<DemoDocumentImageUrlResolver>();
builder.Services.AddScoped<DemoDocumentTokenProvider>();
builder.Services.AddScoped<DemoNotionDataProvider>();
builder.Services.AddScoped<DemoNotionBlockProvider>();
builder.Services.AddScoped<MockNotionDatabaseProvider>();
builder.Services.AddScoped<MockNotionCommentProvider>();
builder.Services.AddScoped<MockNotionHistoryProvider>();
builder.Services.AddScoped<MockNotionMentionProvider>();
builder.Services.AddScoped<MockNotionSearchProvider>();
builder.Services.AddScoped<MockNotionWireframeDocumentProvider>();
builder.Services.AddScoped<MockNotionDiagramDocumentProvider>();
builder.Services.AddScoped<SignalRCollaborationProvider>();

// Register Tempo.Blazor services (ITmLocalizer, ThemeService, ToastService)
builder.Services.AddTempoBlazor();

// Register Dashboard services
builder.Services.AddSingleton<IWidgetRegistry, InMemoryWidgetRegistry>();
builder.Services.AddScoped<IDashboardProvider, InMemoryDashboardProvider>();

// Register FluentValidation validators from Demo assembly
builder.Services.AddTempoFluentValidation(typeof(PersonFormValidator).Assembly);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<Components.App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(Tempo.Blazor.Demo.InteractiveAuto.Client._Imports).Assembly,
        typeof(Tempo.Blazor.Demo.SharedUI.Pages.Home).Assembly);

app.Run();
