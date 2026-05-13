using Microsoft.EntityFrameworkCore;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.Demo.Api.Endpoints;
using Tempo.Blazor.Demo.Api.Hubs;
using Tempo.Blazor.Demo.Api.Services;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Models;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "diagrams.db");
builder.Services.AddDbContext<DemoDiagramDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(
        "http://localhost:5010",
        "https://localhost:7106")
     .AllowAnyMethod()
     .AllowAnyHeader()
     .AllowCredentials()));   // required for SignalR WebSocket handshake

builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton<MockPersonStore>();
builder.Services.AddSingleton<MockUserStore>();
builder.Services.AddSingleton<MockActivityStore>();
builder.Services.AddSingleton<MockAttachmentStore>();
builder.Services.AddSingleton<MockImageStore>();
builder.Services.AddSingleton<MockViewStore>();
builder.Services.AddSingleton<MockDropdownStore>();
builder.Services.AddSingleton<MockScheduleStore>();
builder.Services.AddSingleton<MockTokenStore>();
builder.Services.AddSingleton<MockWireframeStore>();
builder.Services.AddSingleton<MockNotionDataStore>();
builder.Services.AddSingleton<MockNotionBlockStore>();
builder.Services.AddSingleton<MockNotionDatabaseStore>();
builder.Services.AddSingleton<DemoDocumentEditorStore>();
builder.Services.AddSingleton<DemoDocumentFormatProvider>();
builder.Services.AddSingleton<DemoDocumentPdfExportProvider>();
builder.Services.AddSingleton<DemoDocumentComparisonProvider>();
builder.Services.AddSingleton<InMemoryDocumentCollaborationProvider>();
builder.Services.AddSingleton<InMemoryDocumentSuggestionProvider>();
builder.Services.AddSingleton<IDiagramExportService, DemoDiagramExportService>();
builder.Services.AddSingleton<WireframeExportService>();
builder.Services.AddScoped<DemoDiagramHistoryStore>();
builder.Services.AddScoped<IDiagramHistoryStore>(sp => sp.GetRequiredService<DemoDiagramHistoryStore>());

var app = builder.Build();

app.UseCors();

app.MapPersonEndpoints();
app.MapUserEndpoints();
app.MapActivityEndpoints();
app.MapAttachmentEndpoints();
app.MapImageEndpoints();
app.MapViewEndpoints();
app.MapDropdownEndpoints();
app.MapScheduleEndpoints();
app.MapImportExportEndpoints();
app.MapTokenEndpoints();
app.MapWireframeEndpoints();
app.MapWireframeExportEndpoints();
app.MapDiagramExportEndpoints();
app.MapDiagramHistoryEndpoints();
app.MapNotionEditorEndpoints();
app.MapDatabaseEndpoints();
app.MapDocumentEditorEndpoints();
app.MapHub<NotionCollaborationHub>("/hubs/notion-collaboration");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DemoDiagramDbContext>();
    db.Database.EnsureCreated();
}

app.Run();

public partial class Program { }
