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

/// <summary>
/// Phase 3 tests for the wireframe block's "Insert existing…" (link/copy), stale-preview
/// refresh, and missing-document degradation.
/// </summary>
public class TmNotionWireframeBlockInsertTests : LocalizationTestBase
{
    private static NotionEditorContext Context(
        ITempoDocumentLibraryProvider? library, IWireframeDocumentProvider? wireframe)
        => new()
        {
            DataProvider = Substitute.For<INotionDataProvider>(),
            BlockProvider = Substitute.For<INotionBlockProvider>(),
            DocumentLibraryProvider = library,
            WireframeDocumentProvider = wireframe
        };

    private IRenderedComponent<TmNotionWireframeBlock> Render(
        NotionEditorContext ctx, IWireframeBlockContent? content,
        Action<WireframeBlockContent>? onSaved = null, bool readOnly = false)
        => Render<TmNotionWireframeBlock>(p =>
        {
            p.AddCascadingValue(ctx);
            p.Add(c => c.Content, content);
            p.Add(c => c.ReadOnly, readOnly);
            if (onSaved is not null)
            {
                p.Add(c => c.OnContentSaved, onSaved);
            }
        });

    // ── 3.2 Placeholder ────────────────────────────────────────────────────────

    [Fact]
    public void Placeholder_ShowsInsertExisting_WhenLibraryProviderPresent()
    {
        var ctx = Context(new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All), null);

        var cut = Render(ctx, content: null);

        cut.FindAll(".tm-notion-tempo-block__insert-existing").Should().NotBeEmpty();
    }

    [Fact]
    public void Placeholder_HidesInsertExisting_WhenNoLibraryProvider()
    {
        var ctx = Context(null, null);

        var cut = Render(ctx, content: null);

        cut.FindAll(".tm-notion-tempo-block__insert-existing").Should().BeEmpty();
    }

    // ── 3.3 Opens dialog ─────────────────────────────────────────────────────────

    [Fact]
    public void InsertExisting_OpensDialog()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        lib.AddDocument(TempoDocumentKind.Wireframe, "Home", "/", previewSvg: "<svg id=\"a\"/>");
        var cut = Render(Context(lib, null), content: null);

        cut.Find(".tm-notion-tempo-block__insert-existing").Click();

        cut.WaitForState(() => cut.FindAll(".tm-document-open-dialog").Count > 0);
        cut.FindAll(".tm-document-open-dialog").Should().NotBeEmpty();
    }

    // ── 3.4 Link insert ──────────────────────────────────────────────────────────

    [Fact]
    public void LinkInsert_SetsDocumentIdAndPreviewFromLibrary()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var id = lib.AddDocument(TempoDocumentKind.Wireframe, "Home", "/", previewSvg: "<svg id=\"link\"/>");
        WireframeBlockContent? saved = null;
        var cut = Render(Context(lib, null), content: null, onSaved: c => saved = c);

        cut.Find(".tm-notion-tempo-block__insert-existing").Click();
        cut.WaitForState(() => cut.FindAll(".tm-dod-row").Count > 0);
        cut.FindAll(".tm-dod-row").First(r => r.GetAttribute("data-name") == "Home").Click();
        cut.Find(".tm-dod-open").Click();

        saved.Should().NotBeNull();
        saved!.WireframeDocumentId.Should().Be(id);
        saved.SvgPreviewCache.Should().Be("<svg id=\"link\"/>");
    }

    // ── 3.5 Copy insert ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CopyInsert_CreatesIndependentDocument()
    {
        var wireframe = new MockNotionWireframeDocumentProvider();
        var (sourceId, _) = await wireframe.CreateWireframeDocumentAsync("Source");

        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        lib.AddDocument(TempoDocumentKind.Wireframe, "Source", "/",
            previewSvg: "<svg id=\"src\"/>", id: sourceId);

        WireframeBlockContent? saved = null;
        var cut = Render(Context(lib, wireframe), content: null, onSaved: c => saved = c);

        cut.Find(".tm-notion-tempo-block__insert-existing").Click();
        cut.WaitForState(() => cut.FindAll(".tm-dod-row").Count > 0);
        cut.Find(".tm-dod-mode-copy").Change(true);
        cut.FindAll(".tm-dod-row").First(r => r.GetAttribute("data-name") == "Source").Click();
        cut.Find(".tm-dod-open").Click();

        cut.WaitForState(() => saved is not null);
        saved!.WireframeDocumentId.Should().NotBe(sourceId).And.NotBe(Guid.Empty);
        // The copy is a distinct document in the wireframe store.
        (await wireframe.GetWireframeDocumentAsync(saved.WireframeDocumentId)).Should().NotBeNull();
    }

    // ── 3.8 Stale preview refresh ─────────────────────────────────────────────────

    [Fact]
    public void LinkedBlock_RefreshesPreviewFromLibrary_OnLoad()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var id = lib.AddDocument(TempoDocumentKind.Wireframe, "Home", "/", previewSvg: "<svg id=\"fresh\"/>");

        WireframeBlockContent? saved = null;
        var content = new WireframeBlockContent { WireframeDocumentId = id, SvgPreviewCache = "<svg id=\"stale\"/>" };
        var cut = Render(Context(lib, null), content: content, onSaved: c => saved = c);

        cut.WaitForState(() => saved is not null);
        saved!.SvgPreviewCache.Should().Be("<svg id=\"fresh\"/>");
        cut.Markup.Should().Contain("fresh");
    }

    [Fact]
    public void ReadOnlyLinkedBlock_RefreshesPreviewInMemory_WithoutPersisting()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var id = lib.AddDocument(TempoDocumentKind.Wireframe, "Home", "/", previewSvg: "<svg id=\"fresh\"/>");

        WireframeBlockContent? saved = null;
        var content = new WireframeBlockContent { WireframeDocumentId = id, SvgPreviewCache = "<svg id=\"stale\"/>" };
        var cut = Render(Context(lib, null), content: content, onSaved: c => saved = c, readOnly: true);

        cut.WaitForState(() => cut.Markup.Contains("fresh"));
        cut.Markup.Should().Contain("fresh");
        saved.Should().BeNull(); // not persisted in read-only mode
    }

    // ── 3.9 Missing document ──────────────────────────────────────────────────────

    [Fact]
    public void LinkedBlock_DeletedDocument_ShowsNotFound()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        // No document added → GetEntryAsync returns null.
        var content = new WireframeBlockContent
        {
            WireframeDocumentId = Guid.NewGuid(),
            SvgPreviewCache = "<svg id=\"old\"/>"
        };
        var cut = Render(Context(lib, null), content: content);

        cut.WaitForState(() => cut.FindAll(".tm-notion-tempo-block__not-found").Count > 0);
        cut.Markup.Should().Contain("Document not found");
    }
}
