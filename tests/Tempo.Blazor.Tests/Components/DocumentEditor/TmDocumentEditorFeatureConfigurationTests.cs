using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Components.DocumentEditor.Features;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentEditorFeatureConfigurationTests : LocalizationTestBase
{
    [Fact]
    public void Editor_UsesDefaultBuiltInFeatures_WhenHostDoesNotConfigureFeatures()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-ribbon-tab-insert']").Should().NotBeNull());
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.Find("[data-testid='document-toolbar-image']").Should().NotBeNull();
        cut.Find("[data-testid='document-toolbar-table']").Should().NotBeNull();
    }

    [Fact]
    public void Editor_DisabledImageFeature_RemovesImageToolbarItem()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.DisabledFeatures, [DocumentEditorFeatureNames.Image]));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-ribbon-tab-insert']").Should().NotBeNull());
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.FindAll("[data-testid='document-toolbar-image']").Should().BeEmpty();
        cut.Find("[data-testid='document-toolbar-table']").Should().NotBeNull();
    }

    [Fact]
    public void Editor_DisabledTableFeature_RemovesTableToolbarItem()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.DisabledFeatures, [DocumentEditorFeatureNames.Table]));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-ribbon-tab-insert']").Should().NotBeNull());
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.FindAll("[data-testid='document-toolbar-table']").Should().BeEmpty();
        cut.Find("[data-testid='document-toolbar-image']").Should().NotBeNull();
    }

    [Fact]
    public void Editor_DisabledImageAndTableFeatures_RemoveBothInsertCommands()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.DisabledFeatures, [
                DocumentEditorFeatureNames.Image,
                DocumentEditorFeatureNames.Table
            ]));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-ribbon-tab-insert']").Should().NotBeNull());
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.FindAll("[data-testid='document-toolbar-table']").Should().BeEmpty();
        cut.FindAll("[data-testid='document-toolbar-image']").Should().BeEmpty();
    }

    [Fact]
    public async Task Editor_DisabledTableFeature_IgnoresTableContextMenuRequests()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.DisabledFeatures, [DocumentEditorFeatureNames.Table]));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());

        var host = cut.FindComponent<TmDocumentWysiwygHost>();
        await cut.InvokeAsync(() => host.Instance.HandleTableContextMenuRequested(new WysiwygTableContextMenuRequest
        {
            Left = 200,
            Top = 120,
            Width = 224,
            Height = 196,
            Selection = new WysiwygSelectionSnapshot
            {
                Region = "TableCell",
                AnchorBlockId = "cell-block-1",
                AnchorInlineId = "cell-inline-1",
                ActiveTableCellId = "cell-1",
                TableCellPath = "table-1/row-0/cell-1",
                IsCollapsed = true
            }
        }));

        cut.FindAll("[data-testid='document-table-context-menu']").Should().BeEmpty();
    }
}
