using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Text.Json;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Interfaces;
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
        cut.Find("[data-testid='document-side-panel']").Should().NotBeNull();
        cut.Find("[data-testid='document-side-panel-tab-comments']").Should().NotBeNull();
        cut.Find("[data-testid='document-side-panel-tab-revisions']").Should().NotBeNull();
        cut.Find("[data-testid='document-side-panel-tab-versions']").Should().NotBeNull();
        cut.Find("[data-testid='document-side-panel-tab-properties']").Should().NotBeNull();
        cut.Find("[data-testid='document-version-panel']").Should().NotBeNull();
    }

    [Fact]
    public void Render_ExposesAccessibilityLandmarkLabels()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-wysiwyg-host']").Should().NotBeNull());
        var root = cut.Find(".tm-document-editor");
        root.GetAttribute("role").Should().Be("application");
        root.GetAttribute("aria-label").Should().Be("Document editor");
        cut.Find("[data-testid='document-toolbar']").GetAttribute("aria-label").Should().Be("Document editor toolbar");
        cut.Find(".tm-document-editor__surface").GetAttribute("aria-label").Should().Be("Document surface");
        cut.Find("[data-testid='document-side-panel']").GetAttribute("aria-label").Should().Be("Document side panel");
        cut.Find("[data-testid='document-status-bar']").GetAttribute("aria-label").Should().Be("Document status");
    }

    [Fact]
    public void SidePanel_RendersUnifiedTabsAndSwitchesContent()
    {
        var activeTab = DocumentSidePanelTab.Comments;
        var cut = RenderComponent<TmDocumentSidePanel>(parameters => parameters
            .Add(p => p.ActiveTab, activeTab)
            .Add(p => p.ActiveTabChanged, tab => activeTab = tab)
            .Add(p => p.CommentsCount, 2)
            .Add(p => p.RevisionsCount, 1)
            .Add(p => p.VersionsCount, 3)
            .Add(p => p.CommentsContent, builder => builder.AddContent(0, "comments content"))
            .Add(p => p.RevisionsContent, builder => builder.AddContent(0, "revisions content"))
            .Add(p => p.VersionsContent, builder => builder.AddContent(0, "versions content"))
            .Add(p => p.PropertiesContent, builder => builder.AddContent(0, "properties content")));

        cut.Find("[data-testid='document-side-panel-tab-comments']")
            .GetAttribute("aria-selected")
            .Should()
            .Be("true");
        cut.Markup.Should().Contain("comments content");

        cut.Find("[data-testid='document-side-panel-tab-revisions']").Click();

        activeTab.Should().Be(DocumentSidePanelTab.Revisions);
    }

    [Fact]
    public void SidePanel_ClosedStateShowsEdgeToggle()
    {
        var opened = false;
        var cut = RenderComponent<TmDocumentSidePanel>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.OnOpen, () => opened = true));

        cut.FindAll("[data-testid='document-side-panel']").Should().BeEmpty();
        cut.Find("[data-testid='document-side-panel-edge-toggle']").Click();

        opened.Should().BeTrue();
    }

    [Fact]
    public void Editor_CloseSidePanelFreesWorkspaceAndEdgeToggleReopens()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-side-panel']").Should().NotBeNull());
        cut.Find("[data-testid='document-side-panel-close']").Click();

        cut.Find(".tm-document-editor__workspace")
            .GetAttribute("class")
            .Should()
            .Contain("tm-document-editor__workspace--side-panel-closed");
        cut.FindAll("[data-testid='document-side-panel']").Should().BeEmpty();

        cut.Find("[data-testid='document-side-panel-edge-toggle']").Click();
        cut.Find("[data-testid='document-side-panel']").Should().NotBeNull();
    }

    [Fact]
    public void Editor_RibbonCommandsReopenSidePanelTabs()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-side-panel']").Should().NotBeNull());
        cut.Find("[data-testid='document-side-panel-close']").Click();

        cut.Find("[data-testid='document-ribbon-tab-review']").Click();
        cut.Find("[data-testid='document-open-revisions']").Click();

        cut.Find("[data-testid='document-side-panel-tab-revisions']")
            .GetAttribute("aria-selected")
            .Should()
            .Be("true");
        cut.Find("[data-testid='document-revision-panel']").Should().NotBeNull();

        cut.Find("[data-testid='document-side-panel-close']").Click();
        cut.Find("[data-testid='document-ribbon-tab-view']").Click();
        cut.Find("[data-testid='document-open-versions']").Click();

        cut.Find("[data-testid='document-side-panel-tab-versions']")
            .GetAttribute("aria-selected")
            .Should()
            .Be("true");
        cut.Find("[data-testid='document-version-panel']").Should().NotBeNull();
    }

    [Fact]
    public void RibbonTabs_SwitchVisibleCommandGroups()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.CanExportPdf, true)
            .Add(p => p.CanImportDocx, true)
            .Add(p => p.CanExportDocx, true)
            .Add(p => p.CanCompareDocuments, true)
            .Add(p => p.CanPreviewTemplate, true));

        cut.Find("[data-testid='document-ribbon-tab-home']")
            .GetAttribute("aria-selected")
            .Should()
            .Be("true");
        cut.Find("[data-testid='document-save']").Should().NotBeNull();
        cut.FindAll("[data-testid='document-toolbar-table']").Should().BeEmpty();

        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();
        cut.Find("[data-testid='document-toolbar-table']").Should().NotBeNull();
        cut.Find("[data-testid='document-toolbar-image']").Should().NotBeNull();
        cut.FindAll("[data-testid='document-bold']").Should().BeEmpty();

        cut.Find("[data-testid='document-ribbon-tab-references']").Click();
        cut.Find("[data-testid='document-export-pdf']").Should().NotBeNull();
        cut.Find("[data-testid='document-import-docx-label']").Should().NotBeNull();

        cut.Find("[data-testid='document-ribbon-tab-review']").Click();
        cut.Find("[data-testid='document-track-changes']").Should().NotBeNull();
        cut.Find("[data-testid='document-review-display-mode']").Should().NotBeNull();
        cut.Find("[data-testid='document-open-comments']").Should().NotBeNull();
        cut.Find("[data-testid='document-open-revisions']").Should().NotBeNull();
        cut.Find("[data-testid='document-compare-open']").Should().NotBeNull();
        cut.FindAll("[data-testid='document-template-preview']").Should().BeEmpty();

        cut.Find("[data-testid='document-ribbon-tab-view']").Click();
        cut.Find("[data-testid='document-template-preview']").Should().NotBeNull();
        cut.Find("[data-testid='document-open-versions']").Should().NotBeNull();
    }

    [Fact]
    public void Toolbar_ReferencesTabKeepsExportCommandsVisibleButDisabledWhenUnavailable()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        cut.Find("[data-testid='document-ribbon-tab-references']").Click();

        cut.Find("[data-testid='document-export-pdf']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='document-export-docx']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='document-import-docx-label']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Toolbar_LayoutTabExposesHeaderFooterScopeToggles()
    {
        var differentFirstPage = false;
        var differentOddEven = true;
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.DifferentFirstPage, differentFirstPage)
            .Add(p => p.DifferentOddAndEvenPages, differentOddEven)
            .Add(p => p.DifferentFirstPageChanged, value => differentFirstPage = value)
            .Add(p => p.DifferentOddAndEvenPagesChanged, value => differentOddEven = value));

        cut.Find("[data-testid='document-ribbon-tab-layout']").Click();

        cut.Find("[data-testid='document-different-first-page']").GetAttribute("aria-pressed").Should().Be("false");
        cut.Find("[data-testid='document-different-odd-even']").GetAttribute("aria-pressed").Should().Be("true");

        cut.Find("[data-testid='document-different-first-page']").Click();
        cut.Find("[data-testid='document-different-odd-even']").Click();

        differentFirstPage.Should().BeTrue();
        differentOddEven.Should().BeFalse();
    }

    [Fact]
    public void Toolbar_ViewTabExposesRulerAndZoomControls()
    {
        var showRuler = true;
        var zoomPercent = 100;
        var pageWidthRequested = false;
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.ShowRuler, showRuler)
            .Add(p => p.ZoomPercent, zoomPercent)
            .Add(p => p.ShowRulerChanged, value => showRuler = value)
            .Add(p => p.ZoomPercentChanged, value => zoomPercent = value)
            .Add(p => p.OnZoomPageWidth, () => pageWidthRequested = true));

        cut.Find("[data-testid='document-ribbon-tab-view']").Click();

        cut.Find("[data-testid='document-toggle-ruler']").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("[data-testid='document-zoom-100']").TextContent.Should().Contain("100%");

        cut.Find("[data-testid='document-toggle-ruler']").Click();
        cut.Find("[data-testid='document-zoom-in']").Click();
        cut.Find("[data-testid='document-zoom-page-width']").Click();

        showRuler.Should().BeFalse();
        zoomPercent.Should().Be(110);
        pageWidthRequested.Should().BeTrue();
    }

    [Fact]
    public void StatusBar_ShowsSaveMetricsRegionAndZoom()
    {
        var cut = RenderComponent<TmDocumentEditorStatusBar>(parameters => parameters
            .Add(p => p.IsDirty, true)
            .Add(p => p.SaveMessage, "Saved")
            .Add(p => p.LastSavedAt, new DateTimeOffset(2026, 5, 15, 8, 30, 0, TimeSpan.Zero))
            .Add(p => p.WordCount, 42)
            .Add(p => p.PageCount, 3)
            .Add(p => p.ActiveRegionLabel, "body")
            .Add(p => p.ZoomLabel, "110%"));

        cut.Find("[data-testid='document-status-bar']").Should().NotBeNull();
        cut.Find("[data-testid='document-status-bar']").GetAttribute("aria-label").Should().Be("Document status");
        cut.Find("[data-testid='document-dirty-status']").TextContent.Should().Contain("Unsaved changes");
        cut.Find("[data-testid='document-save-message']").TextContent.Should().Contain("Saved");
        cut.Find("[data-testid='document-status-word-count']").TextContent.Should().Contain("42 words");
        cut.Find("[data-testid='document-status-page-count']").TextContent.Should().Contain("3 pages");
        cut.Find("[data-testid='document-status-region']").TextContent.Should().Contain("body");
        cut.Find("[data-testid='document-status-zoom']").TextContent.Should().Contain("110%");
    }

    [Fact]
    public async Task Editor_StatusBarReplacesRibbonSaveStatusAndCountsDocumentText()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        seeded.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.PageBreak,
            Content = new PageBreakBlockContent()
        });
        seeded.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 999,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "additional words" }]
            }
        });
        await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = seeded,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        });

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-status-bar']").Should().NotBeNull());
        cut.FindAll(".tm-document-editor__ribbon-status").Should().BeEmpty();
        cut.Find("[data-testid='document-status-page-count']").TextContent.Should().Contain("2 pages");
        cut.Find("[data-testid='document-status-word-count']").TextContent.Should().Contain("words");
        cut.Find("[data-testid='document-status-region']").TextContent.Should().Contain("body");
        cut.Find("[data-testid='document-status-zoom']").TextContent.Should().Contain("100%");
    }

    [Fact]
    public void Toolbar_HeaderFooterModeShowsContextualTabAndCloseCommand()
    {
        var closeCalled = false;
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.ActiveRegion, "Header")
            .Add(p => p.OnCloseHeaderFooter, () => closeCalled = true));

        cut.Find("[data-testid='document-ribbon-tab-header-footer']")
            .GetAttribute("aria-selected")
            .Should()
            .Be("true");
        cut.Find("[data-testid='document-close-header-footer']").Should().NotBeNull();

        cut.Find("[data-testid='document-close-header-footer']").Click();

        closeCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RibbonTabs_SupportKeyboardNavigationAndSelectedState()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();
        var home = cut.Find("[data-testid='document-ribbon-tab-home']");

        await home.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });

        cut.Find("[data-testid='document-ribbon-tab-insert']")
            .GetAttribute("aria-selected")
            .Should()
            .Be("true");
        cut.Find("[data-testid='document-ribbon-tab-insert']")
            .GetAttribute("tabindex")
            .Should()
            .Be("0");

        await cut.Find("[data-testid='document-ribbon-tab-insert']")
            .KeyDownAsync(new KeyboardEventArgs { Key = "End" });

        cut.Find("[data-testid='document-ribbon-tab-view']")
            .GetAttribute("aria-selected")
            .Should()
            .Be("true");
    }

    [Fact]
    public async Task Editor_EscapeClosesSidePanelAndRequestsDocumentFocus()
    {
        JSInterop.SetupVoid("tmDocumentWysiwyg.focus", _ => true).SetVoidResult();
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-side-panel']").Should().NotBeNull());

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("[data-testid='document-side-panel']").Should().BeEmpty();
        JSInterop.Invocations.Should().Contain(invocation => invocation.Identifier == "tmDocumentWysiwyg.focus");
    }

    [Fact]
    public async Task Editor_F10EnablesRibbonKeyboardMode()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-toolbar']").Should().NotBeNull());

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs { Key = "F10" });

        cut.Find(".tm-document-editor").GetAttribute("data-ribbon-keyboard-mode").Should().Be("true");
        cut.Find("[data-testid='document-toolbar']").GetAttribute("data-keyboard-mode").Should().Be("true");
    }

    [Fact]
    public async Task Toolbar_LinkDialog_LoadsExistingLinkTitleAndValidatesSafeUrl()
    {
        WysiwygLinkPayload? applied = null;
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.LinkInfoProvider, () => Task.FromResult<WysiwygLinkInfo?>(new WysiwygLinkInfo
            {
                Href = "https://example.test/old",
                Title = "Old title"
            }))
            .Add(p => p.OnLinkApplied, payload =>
            {
                applied = payload;
            }));

        await cut.Find("[data-testid='document-link']").ClickAsync(new MouseEventArgs());

        cut.Find("[data-testid='document-link-url']").GetAttribute("value").Should().Be("https://example.test/old");
        cut.Find("[data-testid='document-link-title']").GetAttribute("value").Should().Be("Old title");

        await cut.Find("[data-testid='document-link-url']").InputAsync(new ChangeEventArgs { Value = "javascript:alert(1)" });
        await cut.Find("[data-testid='document-apply-link']").ClickAsync(new MouseEventArgs());

        applied.Should().BeNull();
        cut.Find("[data-testid='document-link-error']").TextContent.Should().Contain("safe URL");

        await cut.Find("[data-testid='document-link-url']").InputAsync(new ChangeEventArgs { Value = "https://example.test/new" });
        await cut.Find("[data-testid='document-link-title']").InputAsync(new ChangeEventArgs { Value = "New title" });
        await cut.Find("[data-testid='document-apply-link']").ClickAsync(new MouseEventArgs());

        applied.Should().NotBeNull();
        applied!.Href.Should().Be("https://example.test/new");
        applied.Title.Should().Be("New title");
    }

    [Fact]
    public void Toolbar_ReadOnlyDisablesEditingCommandsButKeepsReviewAndViewNavigation()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.ReadOnly, true)
            .Add(p => p.CanUndo, true)
            .Add(p => p.CanRedo, true)
            .Add(p => p.CanTrackChanges, true)
            .Add(p => p.CanPreviewTemplate, true));

        cut.Find("[data-testid='document-save']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='document-bold']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='document-bold']")
            .GetAttribute("title")
            .Should()
            .Contain("Read-only");

        cut.Find("[data-testid='document-ribbon-tab-review']").HasAttribute("disabled").Should().BeFalse();
        cut.Find("[data-testid='document-ribbon-tab-review']").Click();
        cut.Find("[data-testid='document-review-display-mode']").HasAttribute("disabled").Should().BeFalse();
        cut.Find("[data-testid='document-track-changes']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='document-track-changes']")
            .GetAttribute("title")
            .Should()
            .Contain("Read-only");

        cut.Find("[data-testid='document-ribbon-tab-view']").Click();
        cut.Find("[data-testid='document-template-preview']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Toolbar_FormattingButtonsExposeActiveAndMixedStates()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.BoldState, WysiwygFormattingValue.Active)
            .Add(p => p.ItalicState, WysiwygFormattingValue.Mixed)
            .Add(p => p.UnderlineState, WysiwygFormattingValue.Inactive)
            .Add(p => p.ParagraphAlignment, DocumentTextAlignment.Center));

        cut.Find("[data-testid='document-bold']")
            .GetAttribute("class")
            .Should()
            .Contain("tm-document-editor__ribbon-button--active");
        cut.Find("[data-testid='document-bold']").GetAttribute("aria-pressed").Should().Be("true");

        cut.Find("[data-testid='document-italic']")
            .GetAttribute("class")
            .Should()
            .Contain("tm-document-editor__ribbon-button--mixed");
        cut.Find("[data-testid='document-italic']").GetAttribute("aria-pressed").Should().Be("mixed");

        cut.Find("[data-testid='document-underline']")
            .GetAttribute("class")
            .Should()
            .NotContain("tm-document-editor__ribbon-button--active");
        cut.Find("[data-testid='document-underline']").GetAttribute("aria-pressed").Should().Be("false");

        cut.Find("[data-testid='document-align-center']")
            .GetAttribute("class")
            .Should()
            .Contain("tm-document-editor__ribbon-button--active");
        cut.Find("[data-testid='document-align-center']").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("[data-testid='document-align-left']").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public void Toolbar_ParagraphControlsRenderAndExposeMixedAlignmentState()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.ParagraphAlignment, DocumentTextAlignment.Left)
            .Add(p => p.ParagraphAlignmentMixed, true));

        cut.Find("[data-testid='document-align-left']")
            .GetAttribute("aria-pressed")
            .Should()
            .Be("mixed");
        cut.Find("[data-testid='document-align-justify']").Should().NotBeNull();
        cut.Find("[data-testid='document-line-spacing']").TextContent.Should().Contain("1.5");
        cut.Find("[data-testid='document-spacing-before']").Should().NotBeNull();
        cut.Find("[data-testid='document-spacing-after']").Should().NotBeNull();
        cut.Find("[data-testid='document-increase-indent']").Should().NotBeNull();
        cut.Find("[data-testid='document-decrease-indent']").Should().NotBeNull();
    }

    [Fact]
    public void Toolbar_RendersFontControlsFromProviderData()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.FontFamilies, new[]
            {
                new DocumentFontFamily { Key = "georgia", DisplayName = "Georgia", CssFamily = "Georgia, serif" }
            }));

        cut.Find("[data-testid='document-font-family']").TextContent.Should().Contain("Georgia");
        cut.Find("[data-testid='document-font-size']").TextContent.Should().Contain("12");
        cut.Find("[data-testid='document-font-color']").Should().NotBeNull();
        cut.Find("[data-testid='document-highlight-color']").Should().NotBeNull();
    }

    [Fact]
    public async Task SaveRequest_UsesStructuredProviderBoundaryDocumentWithoutDisplayOnlyImageUrl()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

        var provider = new InMemoryDocumentEditorProvider();
        var document = CreatePhase17ProviderDocument();
        await SeedDocumentAsync(provider, document);

        var domSnapshot = Clone(document);
        ((ImageBlockContent)domSnapshot.Blocks.Single(block => block.Id == "image-1").Content).Url = "blob:https://app.test/display-only";
        domSnapshot.HeadersFooters.Clear();
        domSnapshot.Theme = new DocumentEditorTheme { BodyFontFamily = "Browser default", BodyFontSize = 9 };
        JSInterop.Setup<string>("tmDocumentWysiwyg.getSnapshot", _ => true)
            .SetResult(JsonSerializer.Serialize(new WysiwygDocumentSnapshot { Document = domSnapshot }, DocumentEditorJson.Options));

        DocumentEditorSaveRequest? captured = null;
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-phase17")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.OnSaveRequested, request => captured = request));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        await cut.InvokeAsync(() => cut.FindComponent<TmDocumentWysiwygHost>().Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs()));

        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => captured.Should().NotBeNull());
        captured!.Document.Should().NotBeNull();
        AssertPhase17Metadata(captured.Document!);
        var image = (ImageBlockContent)captured.Document!.Blocks.Single(block => block.Id == "image-1").Content;
        image.Source.Should().Be(DocumentImageSource.Asset);
        image.AssetId.Should().Be("asset-1");
        image.Url.Should().BeNull();
        JsonSerializer.Serialize(captured.Document, DocumentEditorJson.Options).Should().NotContain("display-only");
    }

    [Fact]
    public async Task ExportRequests_ReceiveStructuredMetadataForDocxAndPdfProviders()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditor.downloadFile", _ => true).SetVoidResult();

        var provider = new InMemoryDocumentEditorProvider();
        var document = CreatePhase17ProviderDocument();
        await SeedDocumentAsync(provider, document);

        var domSnapshot = Clone(document);
        ((ImageBlockContent)domSnapshot.Blocks.Single(block => block.Id == "image-1").Content).Url = "https://cdn.test/display-url.png";
        JSInterop.Setup<string>("tmDocumentWysiwyg.getSnapshot", _ => true)
            .SetResult(JsonSerializer.Serialize(new WysiwygDocumentSnapshot { Document = domSnapshot }, DocumentEditorJson.Options));

        var pdfProvider = new CapturingPdfExportProvider();
        var formatProvider = new CapturingDocumentFormatProvider();
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-phase17")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.PdfExportProvider, pdfProvider)
                      .Add(p => p.FormatProvider, formatProvider));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        await cut.InvokeAsync(() => cut.FindComponent<TmDocumentWysiwygHost>().Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs()));
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-ribbon-tab-references']").Should().NotBeNull());
        cut.Find("[data-testid='document-ribbon-tab-references']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-export-docx']").Should().NotBeNull());

        cut.Find("[data-testid='document-export-docx']").Click();
        cut.WaitForAssertion(() => formatProvider.LastExportRequest.Should().NotBeNull());

        cut.Find("[data-testid='document-export-pdf']").Click();
        cut.WaitForAssertion(() => pdfProvider.LastRequest.Should().NotBeNull());

        AssertPhase17Metadata(formatProvider.LastExportRequest!.Document);
        AssertPhase17Metadata(pdfProvider.LastRequest!.Document);
        ((ImageBlockContent)formatProvider.LastExportRequest.Document.Blocks.Single(block => block.Id == "image-1").Content)
            .Url.Should().BeNull();
        ((ImageBlockContent)pdfProvider.LastRequest.Document.Blocks.Single(block => block.Id == "image-1").Content)
            .Url.Should().BeNull();
        pdfProvider.LastRequest.Options.PageSetup.PageSize.Name.Should().Be("A4");
        formatProvider.LastExportRequest.Format.Should().Be(DocumentFormatProviderKind.Docx);
    }

    [Fact]
    public async Task WysiwygSelectionChanged_UpdatesRibbonFormattingState()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);
        inline.Text = "Bold ";
        inline.Marks.Add(new InlineMark { Type = InlineMarkType.Bold });
        var plainInline = new TextRun { Id = "plain-inline", Text = "plain" };
        ((ParagraphBlockContent)paragraph.Content).Inlines.Add(plainInline);
        await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = seeded,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        });

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        var host = cut.FindComponent<TmDocumentWysiwygHost>();

        await cut.InvokeAsync(() => host.Instance.HandleSelectionChanged(new WysiwygSelectionSnapshot
        {
            AnchorBlockId = paragraph.Id,
            AnchorInlineId = inline.Id,
            AnchorOffset = 2,
            FocusBlockId = paragraph.Id,
            FocusInlineId = inline.Id,
            FocusOffset = 2,
            IsCollapsed = true
        }));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-bold']").GetAttribute("aria-pressed").Should().Be("true"));

        await cut.InvokeAsync(() => host.Instance.HandleSelectionChanged(new WysiwygSelectionSnapshot
        {
            AnchorBlockId = paragraph.Id,
            AnchorInlineId = plainInline.Id,
            AnchorOffset = 7,
            FocusBlockId = paragraph.Id,
            FocusInlineId = plainInline.Id,
            FocusOffset = 7,
            IsCollapsed = true
        }));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-bold']").GetAttribute("aria-pressed").Should().Be("false"));
    }

    [Fact]
    public async Task WysiwygSelectionChanged_InHeaderShowsContextualRibbonAndFormatsHeaderSelection()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        DocumentHeaderFooterResolver.EnsurePrimaryHeadersFooters(seeded);
        var header = seeded.HeadersFooters.First(headerFooter => headerFooter.Type == DocumentHeaderFooterType.Header);
        var headerParagraph = header.Blocks[0];
        var headerInline = ((ParagraphBlockContent)headerParagraph.Content).Inlines.OfType<TextRun>().Single();
        headerInline.Text = "Header";
        headerInline.Marks.Add(new InlineMark { Type = InlineMarkType.Bold });
        await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = seeded,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        });

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        var host = cut.FindComponent<TmDocumentWysiwygHost>();

        await cut.InvokeAsync(() => host.Instance.HandleSelectionChanged(new WysiwygSelectionSnapshot
        {
            Region = "Header",
            HeaderFooterId = header.Id,
            AnchorBlockId = headerParagraph.Id,
            AnchorInlineId = headerInline.Id,
            AnchorOffset = 2,
            FocusBlockId = headerParagraph.Id,
            FocusInlineId = headerInline.Id,
            FocusOffset = 2,
            IsCollapsed = true
        }));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-ribbon-tab-header-footer']").GetAttribute("aria-selected").Should().Be("true"));
        cut.Find("[data-testid='document-ribbon-tab-home']").Click();
        cut.Find("[data-testid='document-bold']").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public async Task TextContextMenuRequested_RendersMenuAndRunsBoldAgainstRestoredSelection()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);
        var selection = new WysiwygSelectionSnapshot
        {
            AnchorBlockId = paragraph.Id,
            AnchorInlineId = inline.Id,
            AnchorOffset = 0,
            FocusBlockId = paragraph.Id,
            FocusInlineId = inline.Id,
            FocusOffset = 4,
            IsCollapsed = false
        };

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        var host = cut.FindComponent<TmDocumentWysiwygHost>();
        await cut.InvokeAsync(() => host.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        }));

        await cut.InvokeAsync(() => host.Instance.HandleTextContextMenuRequested(new WysiwygTextContextMenuRequest
        {
            Left = 200,
            Top = 120,
            Width = 240,
            Height = 268,
            Selection = selection
        }));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-text-context-menu']").TextContent.Should().Contain("Bold"));
        cut.Find("[data-testid='document-context-comment']").TextContent.Should().Contain("Comment");
        cut.Find("[data-testid='document-context-bold']").Click();

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentWysiwyg.restoreSelection");
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentWysiwyg.executeCommand"
            && invocation.Arguments.Count >= 2
            && invocation.Arguments[1] != null
            && invocation.Arguments[1]!.ToString() == "toggleMark");
    }

    [Fact]
    public async Task TableContextMenuRequested_RendersMenuAndRunsStructuredTableCommand()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        var selection = new WysiwygSelectionSnapshot
        {
            Region = "TableCell",
            AnchorBlockId = "cell-block-1",
            AnchorInlineId = "cell-inline-1",
            ActiveTableCellId = "cell-1",
            TableCellPath = "table-1/row-0/cell-1",
            IsCollapsed = true
        };

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        var host = cut.FindComponent<TmDocumentWysiwygHost>();
        await cut.InvokeAsync(() => host.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        }));

        await cut.InvokeAsync(() => host.Instance.HandleTableContextMenuRequested(new WysiwygTableContextMenuRequest
        {
            Left = 200,
            Top = 120,
            Width = 224,
            Height = 196,
            Selection = selection
        }));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-table-context-menu']").TextContent.Should().Contain("Add row"));
        cut.Find("[data-testid='document-table-insert-row']").Click();

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentWysiwyg.restoreSelection");
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentWysiwyg.executeCommand"
            && invocation.Arguments.Count >= 2
            && invocation.Arguments[1] != null
            && invocation.Arguments[1]!.ToString() == "insertTableRow");
    }

    [Fact]
    public async Task MiniToolbarChanged_RendersToolbarAndRestoresSelectionBeforeBoldCommand()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);
        var selection = new WysiwygSelectionSnapshot
        {
            AnchorBlockId = paragraph.Id,
            AnchorInlineId = inline.Id,
            AnchorOffset = 0,
            FocusBlockId = paragraph.Id,
            FocusInlineId = inline.Id,
            FocusOffset = 4,
            IsCollapsed = false
        };

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        var host = cut.FindComponent<TmDocumentWysiwygHost>();
        await cut.InvokeAsync(() => host.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        }));

        await cut.InvokeAsync(() => host.Instance.HandleMiniToolbarChanged(new WysiwygMiniToolbarRequest
        {
            IsVisible = true,
            Left = 220,
            Top = 96,
            Width = 184,
            Height = 40,
            Selection = selection
        }));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-mini-toolbar']").Should().NotBeNull());
        cut.Find("[data-testid='document-mini-bold']").Click();

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentWysiwyg.restoreSelection");
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentWysiwyg.executeCommand"
            && invocation.Arguments.Count >= 2
            && invocation.Arguments[1] != null
            && invocation.Arguments[1]!.ToString() == "toggleMark");
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
    public async Task TrackChanges_InsertText_CreatesPendingInlineRevision()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.TrackChangesEnabled, true));

        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "Draft ",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 0
            }
        });

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='document-revision-item']").Should().HaveCount(1));
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        var revision = saved.Revisions.Should().ContainSingle().Subject;
        revision.Type.Should().Be(DocumentRevisionType.Insertion);
        revision.Action.Should().Be(DocumentRevisionAction.Pending);
        revision.PayloadJson.Should().Be("Draft ");
        GetParagraphText(saved).Should().StartWith("Draft ");
        GetRevisionTextRuns(saved).Should().ContainSingle(run => run.Text == "Draft ");
    }

    [Fact]
    public async Task TrackChanges_InsertText_WithSameRevisionId_AppendsToSinglePendingRevision()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);
        const string revisionId = "revision-live-insert";

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.TrackChangesEnabled, true));

        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "D",
            RevisionId = revisionId,
            RevisionType = "Insertion",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 0
            }
        });
        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "raft ",
            RevisionId = revisionId,
            RevisionType = "Insertion",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = $"rev-{revisionId}",
                AnchorOffset = 1
            }
        });

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='document-revision-item']").Should().HaveCount(1));
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        var revision = saved.Revisions.Should().ContainSingle().Subject;
        revision.Id.Should().Be(revisionId);
        revision.PayloadJson.Should().Be("Draft ");
        GetRevisionTextRuns(saved).Should().ContainSingle(run => run.Text == "Draft ");
    }

    [Fact]
    public async Task TrackChanges_InsertBlock_DoesNotDropPendingInlineRevisions()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.TrackChangesEnabled, true));

        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "Draft ",
            RevisionId = "revision-before-enter",
            RevisionType = "Insertion",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 0
            }
        });
        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Paragraph",
            RevisionType = "Structural",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = "rev-revision-before-enter",
                AnchorOffset = 6
            },
            Block = new DocumentBlock
            {
                Id = "tracked-enter-block",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Id = "tracked-enter-inline", Text = string.Empty }]
                }
            }
        });

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='document-revision-item']").Should().HaveCount(1));
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        saved.Revisions.Should().ContainSingle().Subject.PayloadJson.Should().Be("Draft ");
        saved.Blocks.Should().Contain(block => block.Id == "tracked-enter-block");
        GetRevisionTextRuns(saved).Should().ContainSingle(run => run.Text == "Draft ");
    }

    [Fact]
    public async Task TrackChanges_AcceptInsertion_KeepsTextAndClearsRevisionMark()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.TrackChangesEnabled, true));

        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "Draft ",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 0
            }
        });

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-revision-accept']").Should().NotBeNull());
        cut.Find("[data-testid='document-revision-accept']").Click();
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        saved.Revisions.Should().ContainSingle().Subject.Action.Should().Be(DocumentRevisionAction.Accepted);
        GetParagraphText(saved).Should().StartWith("Draft ");
        GetRevisionTextRuns(saved).Should().BeEmpty();
    }

    [Fact]
    public async Task TrackChanges_RejectInsertion_RemovesTextAndClearsRevisionMark()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.TrackChangesEnabled, true));

        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "Draft ",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 0
            }
        });

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-revision-reject']").Should().NotBeNull());
        cut.Find("[data-testid='document-revision-reject']").Click();
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        saved.Revisions.Should().ContainSingle().Subject.Action.Should().Be(DocumentRevisionAction.Rejected);
        GetParagraphText(saved).Should().StartWith("This agreement");
        GetRevisionTextRuns(saved).Should().BeEmpty();
    }

    [Fact]
    public async Task TrackChanges_AcceptDeletion_RemovesDeletedText()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.TrackChangesEnabled, true));

        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "DeleteContentBackward",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 4
            }
        });

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='document-revision-item']").Should().HaveCount(1));
        cut.Find("[data-testid='document-save']").Click();
        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));

        var pending = (await provider.LoadAsync("doc-1")).Document!;
        var revision = pending.Revisions.Should().ContainSingle().Subject;
        revision.Type.Should().Be(DocumentRevisionType.Deletion);
        revision.Action.Should().Be(DocumentRevisionAction.Pending);
        revision.PayloadJson.Should().Be("s");
        GetParagraphText(pending).Should().StartWith("This agreement");
        GetRevisionTextRuns(pending).Should().ContainSingle(run => run.Text == "s");

        cut.Find("[data-testid='document-revision-accept']").Click();
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var accepted = (await provider.LoadAsync("doc-1")).Document!;
        accepted.Revisions.Should().ContainSingle().Subject.Action.Should().Be(DocumentRevisionAction.Accepted);
        GetParagraphText(accepted).Should().StartWith("Thi agreement");
        GetRevisionTextRuns(accepted).Should().BeEmpty();
    }

    [Fact]
    public async Task TrackChanges_ToggleMark_CreatesFormattingRevision()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.TrackChangesEnabled, true));

        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Bold",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 0,
                FocusBlockId = paragraph.Id,
                FocusInlineId = inline.Id,
                FocusOffset = 4,
                IsCollapsed = false
            }
        });

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-revision-item']").TextContent.Should().Contain("Formatting"));
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        var revision = saved.Revisions.Should().ContainSingle().Subject;
        revision.Type.Should().Be(DocumentRevisionType.Formatting);
        var payload = JsonSerializer.Deserialize<DocumentFormattingRevisionPayload>(revision.PayloadJson!, DocumentEditorJson.Options);
        payload!.MarkType.Should().Be(InlineMarkType.Bold);
        payload.NewActive.Should().BeTrue();
        GetRevisionTextRuns(saved).Should().ContainSingle(run =>
            run.Text == "This"
            && run.Marks.Any(mark => mark.Type == InlineMarkType.Bold)
            && run.Marks.Any(mark => mark.Type == InlineMarkType.Revision));
    }

    [Fact]
    public async Task TrackChanges_RejectFormatting_RevertsMarkAndClearsRevisionMark()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.TrackChangesEnabled, true));

        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Bold",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 0,
                FocusBlockId = paragraph.Id,
                FocusInlineId = inline.Id,
                FocusOffset = 4,
                IsCollapsed = false
            }
        });

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-revision-reject']").Should().NotBeNull());
        cut.Find("[data-testid='document-revision-reject']").Click();
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        saved.Revisions.Should().ContainSingle().Subject.Action.Should().Be(DocumentRevisionAction.Rejected);
        GetRevisionTextRuns(saved).Should().BeEmpty();
        var firstParagraph = saved.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>().First();
        firstParagraph.Inlines.OfType<TextRun>().Should().NotContain(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Bold));
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
    public async Task Collaboration_RemoteRevisionUpdateRefreshesPanelWithoutReplacingWysiwygHost()
    {
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();
        JSInterop.Setup<WysiwygRemoteOperationBatchApplyResult>("tmDocumentWysiwyg.applyRemoteOperationBatch", _ => true)
            .SetResult(WysiwygRemoteOperationBatchApplyResult.Ok(applied: 1));

        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);
        var collaborationProvider = new InMemoryDocumentCollaborationProvider();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.CollaborationProvider, collaborationProvider)
                      .Add(p => p.CollaborationClientId, "client-a")
                      .Add(p => p.CollaborationSyncInterval, TimeSpan.FromMilliseconds(20)));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        var wysiwygHost = cut.FindComponent<TmDocumentWysiwygHost>().Instance;
        await cut.InvokeAsync(() => wysiwygHost.HandleJsEngineReady(new WysiwygEngineReadyEventArgs()));
        var snapshotCallsBeforeRemote = JSInterop.Invocations.Count(invocation => invocation.Identifier == "tmDocumentWysiwyg.applySnapshot");

        var remoteSession = await collaborationProvider.JoinAsync(new DocumentCollaborationJoinRequest
        {
            DocumentId = "doc-1",
            ClientId = "client-b",
            Author = new DocumentEditorAuthor { Id = "client-b", DisplayName = "Remote reviewer" }
        });
        await collaborationProvider.BroadcastOperationBatchAsync(remoteSession.Id, new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations =
            [
                CreateRemoteRevisionOperation("remote-revision", paragraph.Id, inline.Id!, "Remote ")
            ]
        });

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-revision-item']").TextContent.Should().Contain("Remote"), TimeSpan.FromSeconds(5));
        cut.FindComponent<TmDocumentWysiwygHost>().Instance.Should().BeSameAs(wysiwygHost);
        JSInterop.Invocations.Should().Contain(invocation => invocation.Identifier == "tmDocumentWysiwyg.applyRemoteOperationBatch");
        JSInterop.Invocations.Count(invocation => invocation.Identifier == "tmDocumentWysiwyg.applySnapshot")
            .Should().Be(snapshotCallsBeforeRemote, "successful remote JS apply must not refresh the WYSIWYG surface snapshot");
        JSInterop.Invocations.Should().NotContain(invocation => invocation.Identifier == "tmDocumentWysiwyg.restoreSelection");

        cut.Find("[data-testid='document-save']").Click();
        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        saved.Revisions.Should().ContainSingle(revision => revision.Id == "remote-revision");
    }

    [Fact]
    public async Task Collaboration_RemoteBatchQueuedByJsTransactionDoesNotForceSnapshotRefresh()
    {
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();
        JSInterop.Setup<WysiwygRemoteOperationBatchApplyResult>("tmDocumentWysiwyg.applyRemoteOperationBatch", _ => true)
            .SetResult(WysiwygRemoteOperationBatchApplyResult.Ok(queued: 1));

        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);
        var collaborationProvider = new InMemoryDocumentCollaborationProvider();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.CollaborationProvider, collaborationProvider)
                      .Add(p => p.CollaborationClientId, "client-a")
                      .Add(p => p.CollaborationSyncInterval, TimeSpan.FromMilliseconds(20)));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        var wysiwygHost = cut.FindComponent<TmDocumentWysiwygHost>().Instance;
        await cut.InvokeAsync(() => wysiwygHost.HandleJsEngineReady(new WysiwygEngineReadyEventArgs()));
        var snapshotCallsBeforeRemote = JSInterop.Invocations.Count(invocation => invocation.Identifier == "tmDocumentWysiwyg.applySnapshot");

        var remoteSession = await collaborationProvider.JoinAsync(new DocumentCollaborationJoinRequest
        {
            DocumentId = "doc-1",
            ClientId = "client-b",
            Author = new DocumentEditorAuthor { Id = "client-b", DisplayName = "Remote reviewer" }
        });
        await collaborationProvider.BroadcastOperationBatchAsync(remoteSession.Id, new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations =
            [
                new DocumentOperation
                {
                    OperationId = "queued-remote-insert",
                    Type = DocumentOperationType.InsertText,
                    Target = new DocumentOperationTarget { BlockId = paragraph.Id, InlineId = inline.Id, Offset = 0, Length = 7 },
                    Text = "Queued "
                }
            ]
        });

        cut.WaitForAssertion(() =>
            JSInterop.Invocations.Should().Contain(invocation => invocation.Identifier == "tmDocumentWysiwyg.applyRemoteOperationBatch"),
            TimeSpan.FromSeconds(5));
        JSInterop.Invocations.Count(invocation => invocation.Identifier == "tmDocumentWysiwyg.applySnapshot")
            .Should().Be(snapshotCallsBeforeRemote, "queued remote operations are owned by the JS transaction queue");
        cut.FindComponent<TmDocumentWysiwygHost>().Instance.Should().BeSameAs(wysiwygHost);
    }

    [Fact]
    public async Task Collaboration_RemoteApplyFailureFallsBackToSnapshotAndShowsRecoveryMessage()
    {
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();
        JSInterop.Setup<WysiwygRemoteOperationBatchApplyResult>("tmDocumentWysiwyg.applyRemoteOperationBatch", _ => true)
            .SetResult(WysiwygRemoteOperationBatchApplyResult.Failed("op-failed"));

        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);
        var collaborationProvider = new InMemoryDocumentCollaborationProvider();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.CollaborationProvider, collaborationProvider)
                      .Add(p => p.CollaborationClientId, "client-a")
                      .Add(p => p.CollaborationSyncInterval, TimeSpan.FromMilliseconds(20)));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        var host = cut.FindComponent<TmDocumentWysiwygHost>().Instance;
        await cut.InvokeAsync(() => host.HandleJsEngineReady(new WysiwygEngineReadyEventArgs()));
        var snapshotCallsBeforeRemote = JSInterop.Invocations.Count(invocation => invocation.Identifier == "tmDocumentWysiwyg.applySnapshot");

        var remoteSession = await collaborationProvider.JoinAsync(new DocumentCollaborationJoinRequest
        {
            DocumentId = "doc-1",
            ClientId = "client-b",
            Author = new DocumentEditorAuthor { Id = "client-b", DisplayName = "Remote reviewer" }
        });
        await collaborationProvider.BroadcastOperationBatchAsync(remoteSession.Id, new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations = [CreateRemoteRevisionOperation("remote-revision-fallback", paragraph.Id, inline.Id!, "Fallback ")]
        });

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-save-message']").TextContent.Should().Contain("op-failed"), TimeSpan.FromSeconds(5));
        JSInterop.Invocations.Count(invocation => invocation.Identifier == "tmDocumentWysiwyg.applySnapshot")
            .Should().BeGreaterThan(snapshotCallsBeforeRemote, "failed remote JS apply must fall back to a synchronized snapshot");
    }

    [Fact]
    public async Task Collaboration_ProviderFailureDuringRefreshDoesNotBlockLocalTyping()
    {
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

        var provider = new InMemoryDocumentEditorProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var (paragraph, inline) = GetFirstParagraphTextRun(seeded);

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.CollaborationProvider, new ThrowingReconnectCollaborationProvider())
                      .Add(p => p.CollaborationClientId, "client-a")
                      .Add(p => p.CollaborationSyncInterval, TimeSpan.FromMilliseconds(20)));

        cut.WaitForAssertion(() =>
            cut.FindComponent<TmDocumentWysiwygHost>().Should().NotBeNull());
        await cut.InvokeAsync(() => cut.FindComponent<TmDocumentWysiwygHost>().Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs()));
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-save-message']").TextContent.Should().Contain("Collaboration is unavailable"), TimeSpan.FromSeconds(5));

        await ApplyWysiwygPatchAsync(cut, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "Local ",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 0
            }
        });
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        GetParagraphText(saved).Should().StartWith("Local ");
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

        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();
        cut.Find("[data-testid='document-insert-menu']").Click();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-token-menu']").Should().NotBeNull());
        cut.Find(".tm-rte-token-item").Click();
        cut.Find("[data-testid='document-ribbon-tab-home']").Click();
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        var savedParagraph = saved.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>().First();
        savedParagraph.Inlines.OfType<TokenRun>().Should().Contain(token => token.Key == "matter.number");
    }

    private static Task ApplyWysiwygPatchAsync(IRenderedComponent<TmDocumentEditor> cut, WysiwygPatch patch)
        => cut.InvokeAsync(() => cut.FindComponent<TmDocumentWysiwygHost>().Instance.HandlePatchGenerated(patch));

    private static (DocumentBlock Paragraph, TextRun Inline) GetFirstParagraphTextRun(DocumentEditorDocument document)
    {
        var paragraph = document.Blocks.First(block => block.Content is ParagraphBlockContent);
        var inline = ((ParagraphBlockContent)paragraph.Content).Inlines.OfType<TextRun>().First();
        return (paragraph, inline);
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }

    private static async Task SeedDocumentAsync(InMemoryDocumentEditorProvider provider, DocumentEditorDocument document)
    {
        provider.SeedEmptyDocument(document.DocumentId);
        await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = document.DocumentId,
            Document = document,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        });
    }

    private static DocumentEditorDocument CreatePhase17ProviderDocument()
    {
        var document = DocumentEditorDocument.Empty("doc-phase17");
        document.Metadata.Title = "Phase 17 contract";
        document.Theme = new DocumentEditorTheme
        {
            BodyFontFamily = "Aptos, Arial, sans-serif",
            BodyFontSize = 12,
            BodyLineHeight = 1.3,
            ParagraphSpacingAfter = 10
        };
        document.Sections[0].Id = "section-1";
        document.Sections[0].Properties.HeaderFooterReferences =
        [
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "header-1",
                Type = DocumentHeaderFooterType.Header,
                Scope = DocumentHeaderFooterScope.Primary
            },
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "footer-1",
                Type = DocumentHeaderFooterType.Footer,
                Scope = DocumentHeaderFooterScope.Primary
            }
        ];
        document.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-1",
            Type = DocumentBlockType.Paragraph,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = DocumentTextAlignment.Right,
                LineSpacing = 1.5,
                SpacingAfter = 12
            },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = "inline-1",
                        Text = "Provider export text",
                        Marks =
                        [
                            new InlineMark { Type = InlineMarkType.FontFamily, Value = "Georgia" },
                            new InlineMark { Type = InlineMarkType.FontSize, Value = "18pt" },
                            new InlineMark { Type = InlineMarkType.Revision, RevisionId = "revision-1", Value = "Insertion" }
                        ]
                    }
                ]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Id = "image-1",
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = "asset-1",
                AltText = "Provider image",
                Caption = "Provider image caption",
                Size = new DocumentImageSize { Width = 300, Height = 150 },
                FloatingLayout = new DocumentFloatingLayout
                {
                    Inline = false,
                    WrapMode = DocumentWrapMode.Square,
                    X = 24,
                    Y = 12,
                    ZIndex = 3
                }
            }
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "header-1",
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks = [CreateTextBlock("header-block-1", "Header phase 17")]
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "footer-1",
            Type = DocumentHeaderFooterType.Footer,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks = [CreateTextBlock("footer-block-1", "Footer phase 17")]
        });
        document.Revisions.Add(new DocumentRevision
        {
            Id = "revision-1",
            Type = DocumentRevisionType.Insertion,
            Range = new DocumentRevisionRange { BlockId = "paragraph-1", StartInlineIndex = 0, EndInlineIndex = 0, StartOffset = 0, EndOffset = 20 },
            Author = new DocumentRevisionAuthor { Id = "reviewer-1", DisplayName = "Reviewer" },
            CreatedAt = DateTimeOffset.Parse("2026-05-14T12:00:00Z"),
            Action = DocumentRevisionAction.Pending
        });

        return document;
    }

    private static DocumentBlock CreateTextBlock(string id, string text)
    {
        return new DocumentBlock
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = text }]
            }
        };
    }

    private static void AssertPhase17Metadata(DocumentEditorDocument document)
    {
        document.DocumentId.Should().Be("doc-phase17");
        document.Theme.BodyFontFamily.Should().Contain("Aptos");
        document.Blocks.Single(block => block.Id == "paragraph-1")
            .ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Right);
        var paragraph = (ParagraphBlockContent)document.Blocks.Single(block => block.Id == "paragraph-1").Content;
        var run = paragraph.Inlines.OfType<TextRun>().Single();
        run.Marks.Should().Contain(mark => mark.Type == InlineMarkType.FontFamily && mark.Value == "Georgia");
        run.Marks.Should().Contain(mark => mark.Type == InlineMarkType.FontSize && mark.Value == "18pt");
        run.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Revision && mark.RevisionId == "revision-1");
        document.HeadersFooters.Should().Contain(headerFooter => headerFooter.Id == "header-1");
        document.HeadersFooters.Should().Contain(headerFooter => headerFooter.Id == "footer-1");
        document.Revisions.Should().ContainSingle(revision => revision.Id == "revision-1");
        var image = (ImageBlockContent)document.Blocks.Single(block => block.Id == "image-1").Content;
        image.Size.Width.Should().Be(300);
        image.FloatingLayout!.WrapMode.Should().Be(DocumentWrapMode.Square);
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

    private static IReadOnlyList<TextRun> GetRevisionTextRuns(DocumentEditorDocument document)
    {
        var paragraph = document.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>().First();
        return paragraph.Inlines
            .OfType<TextRun>()
            .Where(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Revision))
            .ToList();
    }

    private static DocumentOperation CreateRemoteRevisionOperation(string revisionId, string blockId, string inlineId, string text)
        => new()
        {
            Type = DocumentOperationType.CreateRevision,
            Target = new DocumentOperationTarget
            {
                BlockId = blockId,
                InlineId = inlineId,
                InlineIndex = 0,
                Offset = 0,
                Length = text.Length
            },
            Text = text,
            Revision = new DocumentRevision
            {
                Id = revisionId,
                Type = DocumentRevisionType.Insertion,
                Range = new DocumentRevisionRange
                {
                    BlockId = blockId,
                    StartInlineIndex = 0,
                    StartOffset = 0,
                    EndInlineIndex = 0,
                    EndOffset = text.Length
                },
                Author = new DocumentRevisionAuthor { Id = "client-b", DisplayName = "Remote reviewer" },
                PayloadJson = text,
                Action = DocumentRevisionAction.Pending
            },
            Metadata = new DocumentOperationMetadata
            {
                AuthorId = "client-b",
                ClientId = "client-b",
                RevisionId = revisionId,
                RevisionType = nameof(DocumentRevisionType.Insertion),
                LogicalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        };

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

    private sealed class CapturingPdfExportProvider : IDocumentPdfExportProvider
    {
        public DocumentPdfExportRequest? LastRequest { get; private set; }

        public Task<DocumentPdfExportResult> ExportPdfAsync(
            DocumentPdfExportRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = Clone(request);
            return Task.FromResult(new DocumentPdfExportResult
            {
                Content = [1, 2, 3],
                ContentType = "application/pdf",
                FileName = "phase17.pdf"
            });
        }
    }

    private sealed class CapturingDocumentFormatProvider : IDocumentFormatProvider
    {
        public DocumentFormatExportProviderRequest? LastExportRequest { get; private set; }

        public Task<IReadOnlyList<DocumentFormatProviderCapability>> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DocumentFormatProviderCapability> capabilities =
            [
                new DocumentFormatProviderCapability
                {
                    Format = DocumentFormatProviderKind.Docx,
                    CanImport = true,
                    CanExport = true,
                    FileExtensions = [".docx"]
                }
            ];

            return Task.FromResult(capabilities);
        }

        public Task<DocumentFormatImportProviderResult> ImportAsync(
            DocumentFormatImportProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DocumentFormatImportProviderResult
            {
                Document = DocumentEditorDocument.Empty(request.DocumentId),
                Format = request.Format
            });
        }

        public Task<DocumentFormatExportProviderResult> ExportAsync(
            DocumentFormatExportProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            LastExportRequest = Clone(request);
            return Task.FromResult(new DocumentFormatExportProviderResult
            {
                Content = [4, 5, 6],
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                FileName = "phase17.docx",
                Format = request.Format
            });
        }
    }

    private sealed class ThrowingReconnectCollaborationProvider : InMemoryDocumentCollaborationProvider
    {
        public override Task<IReadOnlyList<DocumentCollaborationOperationBatch>> GetOperationBatchesAsync(
            string documentId,
            long afterSequence,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated collaboration transport failure.");
        }
    }
}
