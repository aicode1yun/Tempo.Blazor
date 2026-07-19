using Bunit;
using FluentAssertions;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Blocks.TempoBlocks;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Fixtures;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>Phase 4 live-refresh tests for the wireframe block (subscribe / refresh / unsubscribe).</summary>
public class TmNotionWireframeBlockLiveRefreshTests : LocalizationTestBase
{
    private static NotionEditorContext Context(
        ITempoDocumentLibraryProvider library, ITempoDocumentChangeNotifier notifier)
        => new()
        {
            DataProvider = Substitute.For<INotionDataProvider>(),
            BlockProvider = Substitute.For<INotionBlockProvider>(),
            DocumentLibraryProvider = library,
            DocumentChangeNotifier = notifier
        };

    [Fact]
    public void LinkedBlock_SubscribesToItsDocument_OnMount()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var id = lib.AddDocument(TempoDocumentKind.Wireframe, "Home", "/", previewSvg: "<svg id=\"v1\"/>");
        var notifier = new FakeDocumentChangeNotifier();
        var content = new WireframeBlockContent { WireframeDocumentId = id, SvgPreviewCache = "<svg id=\"v1\"/>" };

        var cut = Render<TmNotionWireframeBlock>(p => p
            .AddCascadingValue(Context(lib, notifier))
            .Add(c => c.Content, content));

        cut.WaitForState(() => notifier.IsSubscribed(TempoDocumentKind.Wireframe, id));
        notifier.IsSubscribed(TempoDocumentKind.Wireframe, id).Should().BeTrue();
    }

    [Fact]
    public void RemoteChange_RefreshesPreview_WithoutReload()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var id = lib.AddDocument(TempoDocumentKind.Wireframe, "Home", "/", previewSvg: "<svg id=\"v1\"/>");
        var notifier = new FakeDocumentChangeNotifier();
        WireframeBlockContent? saved = null;
        var content = new WireframeBlockContent { WireframeDocumentId = id, SvgPreviewCache = "<svg id=\"v1\"/>" };

        var cut = Render<TmNotionWireframeBlock>(p => p
            .AddCascadingValue(Context(lib, notifier))
            .Add(c => c.Content, content)
            .Add(c => c.OnContentSaved, c => saved = c));

        cut.WaitForState(() => notifier.IsSubscribed(TempoDocumentKind.Wireframe, id));

        // Simulate an edit elsewhere, then a remote change notification.
        lib.UpdatePreview(TempoDocumentKind.Wireframe, id, "<svg id=\"v2\"/>");
        cut.InvokeAsync(() => notifier.RaiseAsync(new TempoDocumentChange
        {
            Kind = TempoDocumentKind.Wireframe,
            DocumentId = id,
            ChangeType = TempoDocumentChangeType.Saved,
            ModifiedAt = DateTime.UtcNow
        }));

        cut.WaitForState(() => cut.Markup.Contains("v2"));
        cut.Markup.Should().Contain("v2");
        saved!.SvgPreviewCache.Should().Be("<svg id=\"v2\"/>");
    }

    [Fact]
    public void RemoteChange_ForOtherDocument_IsIgnored()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var id = lib.AddDocument(TempoDocumentKind.Wireframe, "Home", "/", previewSvg: "<svg id=\"v1\"/>");
        var notifier = new FakeDocumentChangeNotifier();
        var content = new WireframeBlockContent { WireframeDocumentId = id, SvgPreviewCache = "<svg id=\"v1\"/>" };

        var cut = Render<TmNotionWireframeBlock>(p => p
            .AddCascadingValue(Context(lib, notifier))
            .Add(c => c.Content, content));
        cut.WaitForState(() => notifier.IsSubscribed(TempoDocumentKind.Wireframe, id));

        lib.UpdatePreview(TempoDocumentKind.Wireframe, id, "<svg id=\"v2\"/>");
        cut.InvokeAsync(() => notifier.RaiseAsync(new TempoDocumentChange
        {
            Kind = TempoDocumentKind.Wireframe,
            DocumentId = Guid.NewGuid(), // different document
            ChangeType = TempoDocumentChangeType.Saved,
            ModifiedAt = DateTime.UtcNow
        }));

        cut.Markup.Should().Contain("v1").And.NotContain("v2");
    }

    [Fact]
    public async Task Dispose_Unsubscribes()
    {
        var lib = new InMemoryDocumentLibraryProvider(DocumentLibraryCapabilities.All);
        var id = lib.AddDocument(TempoDocumentKind.Wireframe, "Home", "/", previewSvg: "<svg id=\"v1\"/>");
        var notifier = new FakeDocumentChangeNotifier();
        var content = new WireframeBlockContent { WireframeDocumentId = id, SvgPreviewCache = "<svg id=\"v1\"/>" };

        var cut = Render<TmNotionWireframeBlock>(p => p
            .AddCascadingValue(Context(lib, notifier))
            .Add(c => c.Content, content));
        cut.WaitForState(() => notifier.IsSubscribed(TempoDocumentKind.Wireframe, id));

        await DisposeComponentsAsync();

        notifier.IsSubscribed(TempoDocumentKind.Wireframe, id).Should().BeFalse();
    }
}
