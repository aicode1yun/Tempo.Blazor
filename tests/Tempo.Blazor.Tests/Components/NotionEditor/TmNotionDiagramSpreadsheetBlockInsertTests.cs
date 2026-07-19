using Bunit;
using FluentAssertions;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Blocks.TempoBlocks;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Demo.Services;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Fixtures;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>Phase 3 insert/refresh/not-found tests for the diagram and spreadsheet blocks.</summary>
public class TmNotionDiagramSpreadsheetBlockInsertTests : LocalizationTestBase
{
    private static NotionEditorContext Context(
        ITempoDocumentLibraryProvider? library = null,
        IDiagramDocumentProvider? diagram = null,
        ISpreadsheetDocumentProvider? spreadsheet = null)
        => new()
        {
            DataProvider = Substitute.For<INotionDataProvider>(),
            BlockProvider = Substitute.For<INotionBlockProvider>(),
            DocumentLibraryProvider = library,
            DiagramDocumentProvider = diagram,
            SpreadsheetDocumentProvider = spreadsheet
        };

    // ── Diagram ────────────────────────────────────────────────────────────────

    [Fact]
    public void Diagram_Placeholder_ShowsInsertExisting_WithLibrary()
    {
        var cut = Render<TmNotionDiagramBlock>(p => p
            .AddCascadingValue(Context(library: new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All)))
            .Add(c => c.Content, (IDiagramBlockContent?)null));

        cut.FindAll(".tm-notion-tempo-block__insert-existing").Should().NotBeEmpty();
    }

    [Fact]
    public void Diagram_LinkInsert_SetsIdAndPreview()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var id = lib.AddDocument(TempoDocumentKind.Diagram, "Flow", "/", previewSvg: "<svg id=\"d\"/>");
        DiagramBlockContent? saved = null;

        var cut = Render<TmNotionDiagramBlock>(p => p
            .AddCascadingValue(Context(library: lib))
            .Add(c => c.Content, (IDiagramBlockContent?)null)
            .Add(c => c.OnContentSaved, c => saved = c));

        cut.Find(".tm-notion-tempo-block__insert-existing").Click();
        cut.WaitForState(() => cut.FindAll(".tm-dod-row").Count > 0);
        cut.FindAll(".tm-dod-row").First(r => r.GetAttribute("data-name") == "Flow").Click();
        cut.Find(".tm-dod-open").Click();

        saved.Should().NotBeNull();
        saved!.DiagramDocumentId.Should().Be(id);
        saved.SvgPreviewCache.Should().Be("<svg id=\"d\"/>");
    }

    [Fact]
    public async Task Diagram_CopyInsert_CreatesIndependentDocument()
    {
        var diagrams = new MockNotionDiagramDocumentProvider();
        var (sourceId, _) = await diagrams.CreateDiagramDocumentAsync("Source");
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        lib.AddDocument(TempoDocumentKind.Diagram, "Source", "/", previewSvg: "<svg/>", id: sourceId);
        DiagramBlockContent? saved = null;

        var cut = Render<TmNotionDiagramBlock>(p => p
            .AddCascadingValue(Context(library: lib, diagram: diagrams))
            .Add(c => c.Content, (IDiagramBlockContent?)null)
            .Add(c => c.OnContentSaved, c => saved = c));

        cut.Find(".tm-notion-tempo-block__insert-existing").Click();
        cut.WaitForState(() => cut.FindAll(".tm-dod-row").Count > 0);
        cut.Find(".tm-dod-mode-copy").Change(true);
        cut.FindAll(".tm-dod-row").First().Click();
        cut.Find(".tm-dod-open").Click();

        cut.WaitForState(() => saved is not null);
        saved!.DiagramDocumentId.Should().NotBe(sourceId).And.NotBe(Guid.Empty);
        (await diagrams.GetDiagramDocumentAsync(saved.DiagramDocumentId)).Should().NotBeNull();
    }

    [Fact]
    public void Diagram_DeletedDocument_ShowsNotFound()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var content = new DiagramBlockContent { DiagramDocumentId = Guid.NewGuid(), SvgPreviewCache = "<svg/>" };

        var cut = Render<TmNotionDiagramBlock>(p => p
            .AddCascadingValue(Context(library: lib))
            .Add(c => c.Content, content));

        cut.WaitForState(() => cut.FindAll(".tm-notion-tempo-block__not-found").Count > 0);
        cut.Markup.Should().Contain("Document not found");
    }

    // ── Spreadsheet ────────────────────────────────────────────────────────────

    [Fact]
    public void Spreadsheet_Placeholder_ShowsInsertExisting_WithLibrary()
    {
        var cut = Render<TmNotionSpreadsheetBlock>(p => p
            .AddCascadingValue(Context(library: new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All)))
            .Add(c => c.Content, (ISpreadsheetBlockContent?)null));

        cut.FindAll(".tm-notion-tempo-block__insert-existing").Should().NotBeEmpty();
    }

    [Fact]
    public void Spreadsheet_LinkInsert_SetsId()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var id = lib.AddDocument(TempoDocumentKind.Spreadsheet, "Budget", "/");
        SpreadsheetBlockContent? saved = null;

        var cut = Render<TmNotionSpreadsheetBlock>(p => p
            .AddCascadingValue(Context(library: lib))
            .Add(c => c.Content, (ISpreadsheetBlockContent?)null)
            .Add(c => c.OnContentSaved, c => saved = c));

        cut.Find(".tm-notion-tempo-block__insert-existing").Click();
        cut.WaitForState(() => cut.FindAll(".tm-dod-row").Count > 0);
        cut.FindAll(".tm-dod-row").First(r => r.GetAttribute("data-name") == "Budget").Click();
        cut.Find(".tm-dod-open").Click();

        cut.WaitForState(() => saved is not null);
        saved!.SpreadsheetDocumentId.Should().Be(id);
    }

    [Fact]
    public async Task Spreadsheet_CopyInsert_CreatesIndependentDocument()
    {
        var sheets = new MockNotionSpreadsheetDocumentProvider();
        var (sourceId, _) = await sheets.CreateSpreadsheetDocumentAsync("Source");
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        lib.AddDocument(TempoDocumentKind.Spreadsheet, "Source", "/", id: sourceId);
        SpreadsheetBlockContent? saved = null;

        var cut = Render<TmNotionSpreadsheetBlock>(p => p
            .AddCascadingValue(Context(library: lib, spreadsheet: sheets))
            .Add(c => c.Content, (ISpreadsheetBlockContent?)null)
            .Add(c => c.OnContentSaved, c => saved = c));

        cut.Find(".tm-notion-tempo-block__insert-existing").Click();
        cut.WaitForState(() => cut.FindAll(".tm-dod-row").Count > 0);
        cut.Find(".tm-dod-mode-copy").Change(true);
        cut.FindAll(".tm-dod-row").First().Click();
        cut.Find(".tm-dod-open").Click();

        cut.WaitForState(() => saved is not null);
        saved!.SpreadsheetDocumentId.Should().NotBe(sourceId).And.NotBe(Guid.Empty);
        (await sheets.GetSpreadsheetDocumentAsync(saved.SpreadsheetDocumentId)).Should().NotBeNull();
    }

    [Fact]
    public void Spreadsheet_DeletedDocument_ShowsNotFound()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var content = new SpreadsheetBlockContent { SpreadsheetDocumentId = Guid.NewGuid() };

        var cut = Render<TmNotionSpreadsheetBlock>(p => p
            .AddCascadingValue(Context(library: lib))
            .Add(c => c.Content, content));

        cut.WaitForState(() => cut.FindAll(".tm-notion-tempo-block__not-found").Count > 0);
        cut.Markup.Should().Contain("Document not found");
    }
}
