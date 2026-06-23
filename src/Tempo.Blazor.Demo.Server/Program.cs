using Tempo.Blazor.Configuration;
using Tempo.Blazor.Demo.Services;
using Tempo.Blazor.Demo.SharedUI.Services;
using Tempo.Blazor.Demo.Validators;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.FluentValidation;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.Reporting.Configuration;
using Tempo.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// HttpClient for API calls
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["DemoApi:BaseUrl"] ?? "https://localhost:5100")
});

builder.Services.AddHttpClient("DemoApi", c =>
    c.BaseAddress = new Uri(builder.Configuration["DemoApi:BaseUrl"] ?? "https://localhost:5100"));

// Register SharedUI services
builder.Services.AddScoped<SignalRCollaborationProvider>();
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
builder.Services.AddScoped<DemoMentionProvider>();
builder.Services.AddScoped<DemoNotionDataProvider>();
builder.Services.AddScoped<DemoNotionBlockProvider>();
builder.Services.AddScoped<DemoNotionMediaLibraryProvider>();
builder.Services.AddScoped<DemoNotionFileProvider>();
builder.Services.AddScoped<DemoNotionTokenProvider>();
builder.Services.AddScoped<DemoNotionAIProvider>();
builder.Services.AddScoped<DemoNotionReactionProvider>();
builder.Services.AddScoped<DemoNotionAnalyticsProvider>();
builder.Services.AddScoped<DemoNotionPagePropertiesProvider>();
builder.Services.AddScoped<DemoSmartLinkProvider>();
builder.Services.AddScoped<DemoNotionDatabaseProvider>();
builder.Services.AddTmWorkItemProvider<DemoWorkItemProvider>();
builder.Services.AddTmWorkItemProvider<DemoOpsWorkItemProvider>();
builder.Services.AddScoped<DemoSharedWorkItemProvider>();
builder.Services.AddScoped<ITmWorkItemProvider>(sp => sp.GetRequiredService<DemoSharedWorkItemProvider>());
builder.Services.AddScoped<MockNotionDatabaseProvider>();
builder.Services.AddScoped<MockNotionCommentProvider>();
builder.Services.AddScoped<MockNotionHistoryProvider>();
builder.Services.AddScoped<MockNotionMentionProvider>();
builder.Services.AddScoped<MockNotionSearchProvider>();
builder.Services.AddScoped<MockNotionWireframeDocumentProvider>();
builder.Services.AddScoped<MockNotionDiagramDocumentProvider>();
builder.Services.AddScoped<ApiSpreadsheetDocumentProvider>();

// Register Tempo.Blazor services (ITmLocalizer, ThemeService, ToastService)
builder.Services.AddTempoBlazor();
builder.Services.AddTempoBlazorReporting();
builder.Services.AddInMemoryNotifications();
builder.Services.AddScoped<DemoReportEmbeddingSourceFactory>();

// Register Dashboard services
builder.Services.AddSingleton<IWidgetRegistry, InMemoryWidgetRegistry>();
builder.Services.AddScoped<IDashboardProvider, InMemoryDashboardProvider>();

// Register FluentValidation validators from Demo assembly
builder.Services.AddTempoFluentValidation(typeof(PersonFormValidator).Assembly);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
