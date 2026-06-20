using FluentAssertions;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.DocumentLibrary;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>Tests for <see cref="DocumentLibrarySeeder"/>.</summary>
public sealed class DocumentLibrarySeederTests
{
    private sealed class NullPublisher : ITempoDocumentChangePublisher
    {
        public Task PublishAsync(TempoDocumentChange change, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static DocumentLibraryStore SeededStore()
    {
        var store = new DocumentLibraryStore(new NullPublisher());
        new DocumentLibrarySeeder(store).EnsureSeeded();
        return store;
    }

    [Fact]
    public void Seeds_WireframeFolderStructure()
    {
        var tree = SeededStore().GetFolderTree(TempoDocumentKind.Wireframe);

        tree.Children.Select(c => c.Path).Should().Contain(["/Designs", "/Archive"]);
        tree.Children.Single(c => c.Path == "/Designs").Children
            .Should().Contain(c => c.Path == "/Designs/Mobile");
    }

    [Fact]
    public void Seeds_WireframeDocuments_WithPreviewAndAuthor()
    {
        var page = SeededStore().Browse(new DocumentLibraryQuery
        {
            Kind = TempoDocumentKind.Wireframe,
            FolderPath = "/Designs"
        });

        page.Items.Should().Contain(i => i.Name == "Home page");
        page.Items.First(i => i.Name == "Home page").PreviewSvg.Should().Contain("<svg");
        page.Items.First(i => i.Name == "Home page").Author.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Seeds_DiagramAndSpreadsheetDocuments()
    {
        var store = SeededStore();

        store.Browse(new DocumentLibraryQuery { Kind = TempoDocumentKind.Diagram, FolderPath = "/Flows" })
            .Items.Should().NotBeEmpty();
        store.Browse(new DocumentLibraryQuery { Kind = TempoDocumentKind.Spreadsheet, FolderPath = "/Reports" })
            .Items.Should().NotBeEmpty();
    }

    [Fact]
    public void EnsureSeeded_IsIdempotent()
    {
        var store = new DocumentLibraryStore(new NullPublisher());
        var seeder = new DocumentLibrarySeeder(store);
        seeder.EnsureSeeded();
        seeder.EnsureSeeded();

        store.Browse(new DocumentLibraryQuery { Kind = TempoDocumentKind.Wireframe, FolderPath = "/Designs" })
            .Items.Count(i => i.Name == "Home page").Should().Be(1);
    }

    [Fact]
    public void SeededWireframePayload_IsValidWireframeDocumentJson()
    {
        var store = SeededStore();
        var entry = store.Browse(new DocumentLibraryQuery
        {
            Kind = TempoDocumentKind.Wireframe, FolderPath = "/Designs"
        }).Items.First();

        var payload = store.GetDocument(TempoDocumentKind.Wireframe, entry.Id)!.PayloadJson;

        payload.Should().Contain("\"title\"").And.Contain("TmCard");
    }
}
