using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentEditorTests : LocalizationTestBase
{
    [Fact]
    public void Render_RendersWysiwygHostByDefault()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-wysiwyg-host']").Should().NotBeNull());
        cut.FindAll("[data-testid='document-paragraph-editor']").Should().BeEmpty();
    }

    [Fact]
    public void Render_RetainsBlazorShellAroundWysiwygHost()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__ribbon").Should().NotBeNull());
        cut.Find("[data-testid='document-save']").Should().NotBeNull();
        cut.Find(".tm-document-editor__comment-rail").Should().NotBeNull();
        cut.Find(".tm-document-editor__version-panel").Should().NotBeNull();
    }

    [Fact]
    public void Render_MissingProviderShowsError()
    {
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1"));

        cut.Find(".tm-document-editor__error").TextContent.Should().Contain("provider");
        cut.FindAll("[data-testid='document-wysiwyg-host']").Should().BeEmpty();
    }

    [Fact]
    public async Task WysiwygPatch_UpdatesDocumentAndExplicitSavePersistsIt()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var paragraph = seeded.Blocks.First(block => block.Content is ParagraphBlockContent);
        var inline = ((ParagraphBlockContent)paragraph.Content).Inlines.OfType<TextRun>().First();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());

        await cut.InvokeAsync(() => cut.FindComponent<TmDocumentWysiwygHost>().Instance.HandlePatchGenerated(new WysiwygPatch
        {
            Type = "InsertText",
            Data = "Draft ",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 0
            }
        }));

        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        GetParagraphText(saved).Should().StartWith("Draft ");
    }

    [Fact]
    public async Task KeyboardShortcuts_InvokeSaveThroughWysiwygShell()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-wysiwyg-host']").Should().NotBeNull());

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs { Key = "s", CtrlKey = true });

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
    }

    [Fact]
    public void ReadOnly_PassesReadOnlyStateToWysiwygHost()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ReadOnly, true));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Instance.ReadOnly.Should().BeTrue());
    }

    [Fact]
    public async Task InsertMenu_WithTokenProvider_InsertsTokenRunIntoWysiwygDocument()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var paragraph = seeded.Blocks.First(block => block.Content is ParagraphBlockContent);
        var inline = ((ParagraphBlockContent)paragraph.Content).Inlines.OfType<TextRun>().First();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.TokenProvider, new TestTokenProvider()));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());

        var host = cut.FindComponent<TmDocumentWysiwygHost>();
        await cut.InvokeAsync(() => host.Instance.HandleSelectionChanged(new WysiwygSelectionSnapshot
        {
            AnchorBlockId = paragraph.Id,
            AnchorInlineId = inline.Id,
            AnchorOffset = 0,
            IsCollapsed = true
        }));

        cut.Find("[data-testid='document-insert-menu']").Click();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-token-menu']").Should().NotBeNull());
        cut.Find(".tm-rte-token-item").Click();
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        var savedParagraph = saved.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>().First();
        savedParagraph.Inlines.OfType<TokenRun>().Should().Contain(token => token.Key == "matter.number");
    }

    private static string GetParagraphText(DocumentEditorDocument document)
    {
        var paragraph = document.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>().First();
        return string.Concat(paragraph.Inlines.Select(inline => inline switch
        {
            TextRun text => text.Text,
            TokenRun token => token.DisplayName,
            _ => string.Empty
        }));
    }

    private sealed class TestTokenProvider : ITokenDataProvider
    {
        public bool SupportsCreation => false;

        public void Refresh()
        {
        }

        public Task<IEnumerable<IToken>> SearchTokensAsync(string query, CancellationToken ct = default)
        {
            IEnumerable<IToken> tokens =
            [
                new TestToken
                {
                    Key = "matter.number",
                    DisplayName = "Matter number",
                    Description = "Matter reference number",
                    Category = "Matter",
                    TypeLabel = "Text"
                }
            ];

            return Task.FromResult(tokens);
        }
    }

    private sealed class TestToken : IToken
    {
        public string Key { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string? Description { get; init; }

        public string? Category { get; init; }

        public string? Icon { get; init; }

        public string? ColorClass { get; init; }

        public string? TypeLabel { get; init; }
    }
}
