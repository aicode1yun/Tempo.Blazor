using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.DocumentLibrary;

namespace Tempo.Blazor.Demo.Api.Data;

/// <summary>
/// Populates <see cref="DocumentLibraryStore"/> with a realistic folder structure and a few
/// sample documents per kind, so the open dialog and demo pages have content to show.
/// Idempotent: seeds only once.
/// </summary>
public sealed class DocumentLibrarySeeder
{
    private readonly DocumentLibraryStore _store;
    private bool _seeded;
    private readonly object _gate = new();

    public DocumentLibrarySeeder(DocumentLibraryStore store) => _store = store;

    public void EnsureSeeded()
    {
        lock (_gate)
        {
            if (_seeded)
            {
                return;
            }
            _seeded = true;
            SeedWireframes();
            SeedDiagrams();
            SeedSpreadsheets();
        }
    }

    private void SeedWireframes()
    {
        _store.CreateFolder(TempoDocumentKind.Wireframe, "/", "Designs");
        _store.CreateFolder(TempoDocumentKind.Wireframe, "/Designs", "Mobile");
        _store.CreateFolder(TempoDocumentKind.Wireframe, "/", "Archive");

        Wireframe("Home page", "/Designs", "Anna");
        Wireframe("Checkout", "/Designs", "Béla");
        Wireframe("Login screen", "/Designs/Mobile", "Anna");
        Wireframe("Old prototype", "/Archive", "Cyril");
    }

    private void Wireframe(string name, string folder, string author)
    {
        var doc = new WireframeDocument { Title = name };
        doc.EnsureActivePage();
        doc.Elements.Add(new WireframeElement { Type = "TmCard", X = 24, Y = 24, W = 320, H = 40 });
        doc.Elements.Add(new WireframeElement { Type = "TmButton", X = 24, Y = 96, W = 120, H = 36 });

        _store.CreateDocument(
            TempoDocumentKind.Wireframe, name, folder,
            WireframeSerializer.Serialize(doc),
            PlaceholderSvg(name, "#6366f1"),
            author);
    }

    private void SeedDiagrams()
    {
        _store.CreateFolder(TempoDocumentKind.Diagram, "/", "Flows");

        Diagram("Onboarding flow", "/Flows", "Anna");
        Diagram("System architecture", "/Flows", "Béla");
    }

    private void Diagram(string name, string folder, string author)
    {
        var doc = new DiagramDocument { Title = name };
        _store.CreateDocument(
            TempoDocumentKind.Diagram, name, folder,
            JsonSerializer.Serialize(doc, DiagramJsonOptions.Default),
            PlaceholderSvg(name, "#0ea5e9"),
            author);
    }

    private void SeedSpreadsheets()
    {
        _store.CreateFolder(TempoDocumentKind.Spreadsheet, "/", "Reports");

        Spreadsheet("Q1 budget", "/Reports", "Cyril");
        Spreadsheet("Roadmap", "/Reports", "Anna");
    }

    private void Spreadsheet(string name, string folder, string author)
    {
        var workbook = new SpreadsheetWorkbook();
        var json = JsonSerializer.Serialize(workbook, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        });
        _store.CreateDocument(
            TempoDocumentKind.Spreadsheet, name, folder, json,
            PlaceholderSvg(name, "#10b981"),
            author);
    }

    private static string PlaceholderSvg(string label, string color)
    {
        var safe = System.Security.SecurityElement.Escape(label);
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 120" width="160" height="120">
              <rect width="160" height="120" rx="8" fill="{color}1a" stroke="{color}" stroke-width="2"/>
              <rect x="16" y="20" width="128" height="14" rx="3" fill="{color}"/>
              <rect x="16" y="46" width="96" height="10" rx="3" fill="{color}80"/>
              <rect x="16" y="64" width="110" height="10" rx="3" fill="{color}80"/>
              <text x="80" y="104" font-size="10" text-anchor="middle" fill="{color}">{safe}</text>
            </svg>
            """;
    }
}
