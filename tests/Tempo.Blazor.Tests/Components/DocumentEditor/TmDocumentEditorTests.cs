using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentEditorTests : LocalizationTestBase
{
    [Fact]
    public void Render_WithoutProvider_DisplaysErrorState()
    {
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1"));

        cut.Find(".tm-document-editor").Should().NotBeNull();
        cut.Find(".tm-document-editor__error").TextContent.Should().Contain("Document provider is not configured");
    }

    [Fact]
    public void Render_WithProvider_LoadsDocument()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.Find(".tm-document-editor__document-title").TextContent.Should().Contain("Service agreement"));
        cut.Find(".tm-document-editor__status").TextContent.Should().Contain("Loaded");
    }

    [Fact]
    public void Render_RootHasBaseClassCustomClassAndAdditionalAttributes()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.Class, "custom-editor")
                      .AddUnmatched("data-testid", "document-editor"));

        var root = cut.Find(".tm-document-editor");
        root.ClassList.Should().Contain("custom-editor");
        root.GetAttribute("data-testid").Should().Be("document-editor");
    }

    [Fact]
    public void Render_ReadOnlyAddsModifierAndDisablesToolbarActions()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ReadOnly, true));

        var root = cut.Find(".tm-document-editor");
        root.ClassList.Should().Contain("tm-document-editor--readonly");
        cut.FindAll(".tm-document-editor__ribbon-button")
            .Where(button => button.GetAttribute("data-testid") != "document-add-comment")
            .Should().OnlyContain(button => button.HasAttribute("disabled"));
        cut.Find("[data-testid='document-add-comment']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Render_ShowToolbarFalse_HidesWordLikeToolbar()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ShowToolbar, false));

        cut.FindAll(".tm-document-editor__ribbon").Should().BeEmpty();
        cut.Find(".tm-document-editor__page-surface").Should().NotBeNull();
    }

    [Fact]
    public void Render_LoadingStateUsesSkeleton()
    {
        var provider = new DelayedDocumentProvider();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.Find(".tm-document-editor__loading").Should().NotBeNull();
        cut.FindAll(".tm-skeleton").Should().NotBeEmpty();
    }

    [Fact]
    public void Render_ErrorStateProvidesRetryAction()
    {
        var provider = new FailingDocumentProvider();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.Find(".tm-document-editor__error").TextContent.Should().Contain("Failed to load document"));
        cut.Find(".tm-document-editor__retry").Click();

        provider.LoadAttempts.Should().Be(2);
    }

    [Fact]
    public void Render_EmptyDocumentDisplaysEmptyState()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
            cut.Find(".tm-document-editor__empty").TextContent.Should().Contain("This document is empty"));
    }

    [Fact]
    public void Render_ProvidesWordLikeLayoutRegions()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__ribbon").Should().NotBeNull());
        cut.Find(".tm-document-editor__surface").Should().NotBeNull();
        cut.Find(".tm-document-editor__page-surface").Should().NotBeNull();
        cut.Find(".tm-document-editor__comment-rail").Should().NotBeNull();
        cut.Find(".tm-document-editor__version-panel").Should().NotBeNull();
        cut.FindAll(".tm-document-editor__ribbon-tab").Select(tab => tab.TextContent.Trim())
            .Should().Contain(["Home", "Insert", "Layout", "References", "Review", "View"]);
        cut.Find("[data-testid='document-toolbar-image']").Should().NotBeNull();
    }

    [Fact]
    public void Render_CanHideCommentsAndVersionHistoryPanels()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ShowComments, false)
                      .Add(p => p.ShowVersionHistory, false));

        cut.FindAll(".tm-document-editor__comment-rail").Should().BeEmpty();
        cut.FindAll(".tm-document-editor__version-panel").Should().BeEmpty();
    }

    [Fact]
    public void Toolbar_FormattingCommandsUseCommandStackUndoAndRedo()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        DocumentEditorDocument? loaded = null;

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.OnDocumentLoaded, EventCallback.Factory.Create<DocumentEditorDocument>(this, document => loaded = document)));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-paragraph-editor']").Click();
        cut.Find("[data-testid='document-bold']").Click();

        FirstTextRun(loaded!).Marks.Should().Contain(mark => mark.Type == InlineMarkType.Bold);
        cut.Find("[data-testid='document-undo']").Click();
        FirstTextRun(loaded!).Marks.Should().NotContain(mark => mark.Type == InlineMarkType.Bold);
        cut.Find("[data-testid='document-redo']").Click();
        FirstTextRun(loaded!).Marks.Should().Contain(mark => mark.Type == InlineMarkType.Bold);
    }

    [Fact]
    public void Toolbar_LinkDialogAndClearFormattingUpdateActiveBlock()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        DocumentEditorDocument? loaded = null;

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.OnDocumentLoaded, EventCallback.Factory.Create<DocumentEditorDocument>(this, document => loaded = document)));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-paragraph-editor']").Click();
        cut.Find("[data-testid='document-link']").Click();
        cut.Find("[data-testid='document-link-dialog'] input").Input("https://example.test");
        cut.Find("[data-testid='document-apply-link']").Click();

        FirstTextRun(loaded!).Marks.Any(mark =>
            mark.Type == InlineMarkType.Link && mark.Link?.Href == "https://example.test").Should().BeTrue();

        cut.Find("[data-testid='document-clear-formatting']").Click();
        FirstTextRun(loaded!).Marks.Should().BeEmpty();
    }

    [Fact]
    public void KeyboardShortcuts_InvokeSaveUndoRedoAndFormattingCommands()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        DocumentEditorDocument? loaded = null;

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.OnDocumentLoaded, EventCallback.Factory.Create<DocumentEditorDocument>(this, document => loaded = document)));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-paragraph-editor']").Click();
        cut.Find(".tm-document-editor").KeyDown(new KeyboardEventArgs { Key = "b", CtrlKey = true });
        FirstTextRun(loaded!).Marks.Should().Contain(mark => mark.Type == InlineMarkType.Bold);

        cut.Find(".tm-document-editor").KeyDown(new KeyboardEventArgs { Key = "z", CtrlKey = true });
        FirstTextRun(loaded!).Marks.Should().NotContain(mark => mark.Type == InlineMarkType.Bold);

        cut.Find(".tm-document-editor").KeyDown(new KeyboardEventArgs { Key = "y", CtrlKey = true });
        FirstTextRun(loaded!).Marks.Should().Contain(mark => mark.Type == InlineMarkType.Bold);

        cut.Find(".tm-document-editor").KeyDown(new KeyboardEventArgs { Key = "s", CtrlKey = true });
        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
    }

    [Fact]
    public void DirtyState_LoadChangeSaveAndSaveFailureUseExpectedState()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.FindAll(".tm-document-editor__dirty").Should().BeEmpty();

        cut.Find("[data-testid='document-paragraph-editor']").Input("Dirty text");
        cut.Find(".tm-document-editor__dirty").TextContent.Should().Contain("Unsaved changes");

        cut.Find("[data-testid='document-save']").Click();
        cut.WaitForAssertion(() => cut.FindAll(".tm-document-editor__dirty").Should().BeEmpty());
        cut.Find("[data-testid='document-last-saved']").TextContent.Should().Contain("Last saved");

        provider.FailNextSave = true;
        cut.Find("[data-testid='document-paragraph-editor']").Input("Still dirty");
        cut.Find("[data-testid='document-save']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-save-message']").TextContent.Should().Contain("Save failed"));
        cut.Find(".tm-document-editor__dirty").TextContent.Should().Contain("Unsaved changes");
    }

    [Fact]
    public void ExplicitSave_ClickAndCtrlSCallProviderCallbackAndAudit()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var callbackRequests = new List<DocumentEditorSaveRequest>();
        var author = new DocumentEditorAuthor { Id = "user-1", DisplayName = "User One" };
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.Author, author)
                      .Add(p => p.OnSaveRequested, EventCallback.Factory.Create<DocumentEditorSaveRequest>(this, request => callbackRequests.Add(request))));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-paragraph-editor']").Input("Click save");
        cut.Find("[data-testid='document-save']").Click();
        cut.WaitForAssertion(() => provider.SaveRequests.Should().ContainSingle());

        cut.Find("[data-testid='document-paragraph-editor']").Input("Keyboard save");
        cut.Find(".tm-document-editor").KeyDown(new KeyboardEventArgs { Key = "s", CtrlKey = true });
        cut.WaitForAssertion(() => provider.SaveRequests.Should().HaveCount(2));

        callbackRequests.Should().HaveCount(2);
        callbackRequests.Should().OnlyContain(request => !request.IsAutosave && request.Author == author);
        provider.AuditEvents.Where(item => item.Action == DocumentEditorAuditAction.Save).Should().HaveCount(2);
    }

    [Fact]
    public void SaveButton_IsDisabledWhileSaveIsRunning()
    {
        var provider = new PendingSaveProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-paragraph-editor']").Input("Saving");
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-save']").HasAttribute("disabled").Should().BeTrue());
        provider.Complete();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-save']").HasAttribute("disabled").Should().BeFalse());
    }

    [Fact]
    public async Task Autosave_DoesNotRunWithoutChangesAndCanBeDisabled()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.AutoSaveInterval, TimeSpan.FromMilliseconds(25)));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        await Task.Delay(90);
        provider.SaveRequests.Should().BeEmpty();

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.AutoSaveInterval, null));
        cut.Find("[data-testid='document-paragraph-editor']").Input("Autosave disabled");
        await Task.Delay(90);
        provider.SaveRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Autosave_SavesDirtyDocumentAsAutosaveWithoutMajorVersion()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.AutoSaveInterval, TimeSpan.FromMilliseconds(25)));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-paragraph-editor']").Input("Autosaved text");

        await Task.Delay(150);

        provider.SaveRequests.Should().ContainSingle();
        provider.SaveRequests[0].IsAutosave.Should().BeTrue();
        provider.SaveRequests[0].VersionKind.Should().Be(DocumentVersionKind.Autosave);
        provider.SaveRequests[0].VersionKind.Should().NotBe(DocumentVersionKind.Major);
        cut.Find("[data-testid='document-save-message']").TextContent.Should().Contain("Autosaved");
    }

    [Fact]
    public void Versions_CreateMinorAndMajorVersionThroughDialog()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var createdVersions = new List<DocumentVersion>();
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.OnVersionCreated, EventCallback.Factory.Create<DocumentVersion>(this, version => createdVersions.Add(version))));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-version-create-open']").Should().NotBeNull());
        cut.Find("[data-testid='document-version-create-open']").Click();
        cut.Find("[data-testid='document-version-label']").Input("Draft 1");
        cut.Find("[data-testid='document-version-create-submit']").Click();

        cut.WaitForAssertion(() => provider.VersionRequests.Should().ContainSingle());
        provider.VersionRequests[0].Kind.Should().Be(DocumentVersionKind.Minor);
        createdVersions.Should().ContainSingle(version => version.Label == "Draft 1");
        provider.AuditEvents.Should().Contain(item => item.Action == DocumentEditorAuditAction.CreateVersion);

        cut.Find("[data-testid='document-version-create-open']").Click();
        cut.Find("[data-testid='document-version-kind']").Change(DocumentVersionKind.Major.ToString());
        cut.Find("[data-testid='document-version-label']").Input("1.0");
        cut.Find("[data-testid='document-version-description']").Input("Approved contract");
        cut.Find("[data-testid='document-version-create-submit']").Click();

        cut.WaitForAssertion(() => provider.VersionRequests.Should().HaveCount(2));
        provider.VersionRequests[1].Kind.Should().Be(DocumentVersionKind.Major);
        provider.VersionRequests[1].Description.Should().Be("Approved contract");
        cut.FindAll("[data-testid='document-version-item']").Should().HaveCount(2);
    }

    [Fact]
    public void Versions_RequireDescriptionForMajorVersionWhenEnabled()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.RequireMajorVersionDescription, true));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-version-create-open']").Should().NotBeNull());
        cut.Find("[data-testid='document-version-create-open']").Click();
        cut.Find("[data-testid='document-version-kind']").Change(DocumentVersionKind.Major.ToString());
        cut.Find("[data-testid='document-version-create-submit']").Click();

        cut.Find("[data-testid='document-version-error']").TextContent.Should().Contain("Major versions require a description");
        provider.VersionRequests.Should().BeEmpty();
    }

    [Fact]
    public void Versions_PanelRendersEmptyStateListSelectionAndCanClose()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-version-empty']").TextContent.Should().Contain("No versions yet"));
        cut.Find("[data-testid='document-version-panel-close']").Click();
        cut.FindAll("[data-testid='document-version-panel']").Should().BeEmpty();
    }

    [Fact]
    public void Versions_PreviewSelectedVersionAndReturnToCurrentReadOnlySurface()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-version-create-open']").Should().NotBeNull());
        cut.Find("[data-testid='document-version-create-open']").Click();
        cut.Find("[data-testid='document-version-label']").Input("Baseline");
        cut.Find("[data-testid='document-version-create-submit']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-version-item']").Should().NotBeNull());

        cut.Find("[data-testid='document-paragraph-editor']").Input("Current unsaved text");
        cut.Find("[data-testid='document-version-item']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-version-preview-state']").TextContent.Should().Contain("Previewing"));
        cut.FindAll("[data-testid='document-paragraph-editor']").Should().BeEmpty();
        cut.Find(".tm-document-editor__surface").TextContent.Should().Contain("This agreement is made with");

        cut.Find("[data-testid='document-version-current']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").GetAttribute("value").Should().Be("Current unsaved text"));
    }

    [Fact]
    public async Task Versions_RestoreSelectedVersionAsDirtyCurrentDraftAndAuditEvent()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-version-create-open']").Should().NotBeNull());
        cut.Find("[data-testid='document-version-create-open']").Click();
        cut.Find("[data-testid='document-version-label']").Input("Baseline");
        cut.Find("[data-testid='document-version-create-submit']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-version-item']").Should().NotBeNull());

        cut.Find("[data-testid='document-paragraph-editor']").Input("Text to replace");
        cut.Find("[data-testid='document-version-item']").Click();
        cut.Find("[data-testid='document-version-restore']").Click();
        cut.Find("[data-testid='document-version-restore-confirm-button']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").GetAttribute("value").Should().Contain("This agreement is made with Client name."));
        cut.Find(".tm-document-editor__dirty").TextContent.Should().Contain("Unsaved changes");
        (await provider.GetVersionsAsync("doc-1")).Should().ContainSingle();
        provider.AuditEvents.Should().Contain(item => item.Action == DocumentEditorAuditAction.RestoreVersion);
    }

    [Fact]
    public async Task Comments_ActiveBlockButtonCreatesBlockCommentAndAuditEvent()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-paragraph-editor']").Click();
        cut.Find("[data-testid='document-block-comment']").Click();
        cut.Find("[data-testid='document-comment-input']").Input("Please review this clause.");
        cut.Find("[data-testid='document-comment-submit']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-comment-list']").TextContent.Should().Contain("Please review this clause."));
        provider.CommentRequests.Should().ContainSingle();
        provider.CommentRequests[0].Anchor.Type.Should().Be(DocumentCommentAnchorType.Block);
        (await provider.GetCommentsAsync("doc-1")).Should().ContainSingle(comment => comment.Entries.Any(entry => entry.Text == "Please review this clause."));
        provider.AuditEvents.Should().Contain(item => item.Action == DocumentEditorAuditAction.Comment);
    }

    [Fact]
    public void Comments_ReplyResolveAndReopenThread()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-paragraph-editor']").Click();
        cut.Find("[data-testid='document-block-comment']").Click();
        cut.Find("[data-testid='document-comment-input']").Input("Initial thread");
        cut.Find("[data-testid='document-comment-submit']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-comment-list']").TextContent.Should().Contain("Initial thread"));

        cut.Find("[data-testid='document-comment-reply-input']").Input("Reply text");
        cut.Find("[data-testid='document-comment-reply-submit']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-comment-list']").TextContent.Should().Contain("Reply text"));
        provider.CommentReplies.Should().ContainSingle(entry => entry.Text == "Reply text");

        cut.Find("[data-testid='document-comment-resolve']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-comment-status']").TextContent.Should().Contain("Resolved"));
        cut.Find("[data-testid='document-comment-reopen']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-comment-status']").TextContent.Should().Contain("Open"));
    }

    [Fact]
    public void Comments_TextRangeSelectionCreatesAnchorAndReadOnlyHighlight()
    {
        var provider = new TrackingDocumentProvider();
        var document = provider.SeedContractDocument("doc-1");
        var paragraphBlock = document.Blocks.First(block => block.Content is ParagraphBlockContent);
        JSInterop.Setup<DocumentCommentAnchor?>("tmDocumentEditor.getTextSelectionAnchor", _ => true)
            .SetResult(new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.TextRange,
                BlockId = paragraphBlock.Id,
                StartInlineIndex = 0,
                StartOffset = 5,
                EndInlineIndex = 0,
                EndOffset = 14
            });

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ReadOnly, true)
                      .Add(p => p.CanComment, true));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-add-comment']").Should().NotBeNull());
        cut.Find("[data-testid='document-add-comment']").Click();
        cut.Find("[data-testid='document-comment-input']").Input("Selected text comment");
        cut.Find("[data-testid='document-comment-submit']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-comment-list']").TextContent.Should().Contain("Selected text comment"));
        provider.CommentRequests.Should().ContainSingle(comment => comment.Anchor.Type == DocumentCommentAnchorType.TextRange);
        cut.Find("[data-testid='document-comment-highlight']").GetAttribute("data-comment-id").Should().NotBeNullOrWhiteSpace();
        cut.Find("[data-testid='document-comment-highlight']").Click();
        cut.Find(".tm-document-comment-thread--selected").Should().NotBeNull();
        cut.FindAll("[data-testid='document-paragraph-editor']").Should().BeEmpty();
    }

    [Fact]
    public void Comments_ExternalAuthorAndResolvePermissionUseVisualStates()
    {
        var provider = new TrackingDocumentProvider();
        var document = provider.SeedContractDocument("doc-1");
        _ = provider.CreateCommentAsync("doc-1", new DocumentComment
        {
            Visibility = DocumentCommentVisibility.External,
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.Block,
                BlockId = document.Blocks.First().Id
            },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { DisplayName = "Client" },
                    IsExternalAuthor = true,
                    Text = "Client-side note"
                }
            ]
        }).GetAwaiter().GetResult();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.CanResolveComments, false));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-comment-list']").TextContent.Should().Contain("Client-side note"));
        cut.Find(".tm-document-comment-thread--external").Should().NotBeNull();
        cut.Find(".tm-document-comment-entry--external").Should().NotBeNull();
        cut.Find("[data-testid='document-comment-resolve']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Comments_ReadOnlyUserCanCommentWhenPermissionAllows()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ReadOnly, true)
                      .Add(p => p.CanComment, true));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-add-comment']").Should().NotBeNull());
        cut.Find("[data-testid='document-add-comment']").Click();
        cut.Find("[data-testid='document-comment-input']").Input("Read-only comment");
        cut.Find("[data-testid='document-comment-submit']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-comment-list']").TextContent.Should().Contain("Read-only comment"));
        cut.FindAll("[data-testid='document-paragraph-editor']").Should().BeEmpty();
        cut.Find("[data-testid='document-save']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void TemplatePreview_ReplacesTokensAndToggleBackKeepsEditableDocument()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var tokenValues = new TestTokenValueProvider(new Dictionary<string, DocumentTokenValue>
        {
            ["client.name"] = DocumentTokenValue.Resolved("client.name", "ACME Ltd.")
        });
        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.TokenValueProvider, tokenValues));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-template-preview']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__status").TextContent.Should().Contain("Template preview"));
        cut.Find(".tm-document-surface").TextContent.Should().Contain("ACME Ltd.");
        cut.FindAll("[data-testid='document-paragraph-editor']").Should().BeEmpty();
        cut.Find("[data-testid='document-save']").HasAttribute("disabled").Should().BeTrue();

        cut.Find("[data-testid='document-template-preview']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").GetAttribute("value").Should().Contain("Client name"));
    }

    [Fact]
    public async Task Comments_CanDeleteOwnThreadOnly()
    {
        var provider = new TrackingDocumentProvider();
        var document = provider.SeedContractDocument("doc-1");
        var blockId = document.Blocks.First().Id;
        var ownComment = await provider.CreateCommentAsync("doc-1", new DocumentComment
        {
            Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.Block, BlockId = blockId },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { Id = "user-1", DisplayName = "User One" },
                    Text = "Own comment"
                }
            ]
        });
        await provider.CreateCommentAsync("doc-1", new DocumentComment
        {
            Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.Block, BlockId = blockId },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { Id = "user-2", DisplayName = "User Two" },
                    Text = "Other comment"
                }
            ]
        });

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.Author, new DocumentEditorAuthor { Id = "user-1", DisplayName = "User One" })
                      .Add(p => p.CanDeleteOwnComments, true));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-comment-list']").TextContent.Should().Contain("Own comment"));
        cut.FindAll("[data-testid='document-comment-delete']").Should().ContainSingle();
        cut.Find("[data-testid='document-comment-delete']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-comment-status-message']").TextContent.Should().Contain("Comment deleted"));
        cut.Find("[data-testid='document-comment-list']").TextContent.Should().NotContain("Own comment");
        cut.Find("[data-testid='document-comment-list']").TextContent.Should().Contain("Other comment");
        provider.DeletedCommentIds.Should().ContainSingle(ownComment.Id);
        (await provider.GetCommentsAsync("doc-1")).Should().NotContain(comment => comment.Id == ownComment.Id);
    }

    [Fact]
    public void Toolbar_HidesPdfExportWithoutProvider()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__document-title").TextContent.Should().Contain("Service agreement"));
        cut.FindAll("[data-testid='document-export-pdf']").Should().BeEmpty();
    }

    [Fact]
    public void Toolbar_PdfExportCallsProvider()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        var pdfProvider = new TestPdfExportProvider();
        DocumentPdfExportResult? exported = null;

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.PdfExportProvider, pdfProvider)
                      .Add(p => p.OnPdfExported, EventCallback.Factory.Create<DocumentPdfExportResult>(this, result => exported = result)));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-export-pdf']").Should().NotBeNull());
        cut.Find("[data-testid='document-export-pdf']").Click();

        cut.WaitForAssertion(() => exported.Should().NotBeNull());
        pdfProvider.Requests.Should().ContainSingle();
        pdfProvider.Requests[0].DocumentId.Should().Be("doc-1");
        exported!.ContentType.Should().Be("application/pdf");
        cut.Find("[data-testid='document-save-message']").TextContent.Should().Contain("PDF exported");
    }

    [Fact]
    public void Permissions_CanReadFalse_ShowsDeniedStateAndDoesNotLoad()
    {
        var provider = new FailingDocumentProvider();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.Permissions, new DocumentEditorPermissions { CanRead = false }));

        cut.Find(".tm-document-editor__error").TextContent.Should().Contain("permission to read");
        provider.LoadAttempts.Should().Be(0);
    }

    [Fact]
    public void Permissions_CanEditFalse_RendersReadOnlyEditorButStillAllowsComments()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.Permissions, new DocumentEditorPermissions { CanEdit = false }));

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor").ClassList.Should().Contain("tm-document-editor--readonly"));
        cut.FindAll("[data-testid='document-paragraph-editor']").Should().BeEmpty();
        cut.Find("[data-testid='document-save']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='document-bold']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='document-add-comment']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Permissions_CanCommentFalse_DisablesToolbarAndRailCommentActions()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.Permissions, new DocumentEditorPermissions { CanComment = false }));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-add-comment']").Should().NotBeNull());
        cut.Find("[data-testid='document-add-comment']").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("[data-testid='document-comment-new']").Should().BeEmpty();
        cut.FindAll("[data-testid='document-block-comment']").Should().BeEmpty();
    }

    [Fact]
    public void Permissions_CanCreateVersionFalse_DisablesVersionCreation()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.Permissions, new DocumentEditorPermissions { CanCreateVersion = false }));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-version-create-open']").Should().NotBeNull());
        cut.Find("[data-testid='document-version-create-open']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-testid='document-version-create-open']").Click();
        cut.FindAll("[data-testid='document-version-dialog']").Should().BeEmpty();
    }

    [Fact]
    public void Permissions_CanExportFalse_HidesPdfExportAction()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.PdfExportProvider, new TestPdfExportProvider())
                      .Add(p => p.Permissions, new DocumentEditorPermissions { CanExport = false }));

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__document-title").TextContent.Should().Contain("Service agreement"));
        cut.FindAll("[data-testid='document-export-pdf']").Should().BeEmpty();
    }

    [Fact]
    public void Permissions_CanViewAudit_ExposesRootPermissionFlag()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.Permissions, new DocumentEditorPermissions { CanViewAudit = true }));

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor").GetAttribute("data-can-view-audit").Should().Be("True"));
    }

    [Fact]
    public void Audit_LoadDispatchesOpenEvent()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var author = new DocumentEditorAuthor { Id = "user-1", DisplayName = "User One" };

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.Author, author));

        cut.WaitForAssertion(() => provider.AuditEvents.Should().ContainSingle(item =>
            item.Action == DocumentEditorAuditAction.Open
            && item.Result == DocumentEditorAuditResult.Success
            && item.Actor != null
            && item.Actor.Id == author.Id));
    }

    [Fact]
    public void Audit_ExportDispatchesExportEvent()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var pdfProvider = new TestPdfExportProvider();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.PdfExportProvider, pdfProvider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-export-pdf']").Should().NotBeNull());
        cut.Find("[data-testid='document-export-pdf']").Click();

        cut.WaitForAssertion(() => provider.AuditEvents.Should().Contain(item =>
            item.Action == DocumentEditorAuditAction.Export
            && item.Result == DocumentEditorAuditResult.Success));
    }

    [Fact]
    public void Audit_NonBlockingFailureDoesNotBreakSaveWorkflow()
    {
        var provider = new TrackingDocumentProvider();
        provider.SeedContractDocument("doc-1");
        var auditSink = new FailingAuditSink();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.AuditSink, auditSink)
                      .Add(p => p.AuditFailureMode, DocumentEditorAuditFailureMode.NonBlocking));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-paragraph-editor']").Input("Saved despite audit failure");
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-save-message']").TextContent.Should().Contain("Saved"));
        provider.SaveRequests.Should().ContainSingle();
        auditSink.Attempts.Should().BeGreaterThan(0);
    }

    private static TextRun FirstTextRun(DocumentEditorDocument document)
    {
        return document.Blocks
            .Select(block => block.Content)
            .OfType<ParagraphBlockContent>()
            .SelectMany(paragraph => paragraph.Inlines)
            .OfType<TextRun>()
            .First();
    }

    private sealed class DelayedDocumentProvider : IDocumentEditorProvider
    {
        private readonly TaskCompletionSource<DocumentEditorLoadResult> _tcs = new();

        public Task<DocumentEditorLoadResult> LoadAsync(string documentId, DocumentEditorLoadOptions? options = null, CancellationToken cancellationToken = default)
            => _tcs.Task;

        public Task<string?> LoadJsonAsync(string documentId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<DocumentEditorSaveResult> SaveAsync(DocumentEditorSaveRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentEditorSaveResult());

        public Task<DocumentVersion> CreateVersionAsync(DocumentVersionCreateRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentVersion());

        public Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(string documentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentVersion>>([]);

        public Task<IReadOnlyList<DocumentComment>> GetCommentsAsync(string documentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentComment>>([]);

        public Task<DocumentComment> CreateCommentAsync(string documentId, DocumentComment comment, CancellationToken cancellationToken = default)
            => Task.FromResult(comment);

        public Task<DocumentComment> AddCommentReplyAsync(string documentId, string commentId, DocumentCommentEntry entry, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentComment());

        public Task<DocumentComment> ResolveCommentAsync(string documentId, string commentId, DocumentEditorAuthor resolvedBy, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentComment());

        public Task<DocumentComment> ReopenCommentAsync(string documentId, string commentId, DocumentEditorAuthor reopenedBy, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentComment());

        public Task DeleteCommentAsync(string documentId, string commentId, DocumentEditorAuthor deletedBy, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FailingDocumentProvider : IDocumentEditorProvider
    {
        public int LoadAttempts { get; private set; }

        public Task<DocumentEditorLoadResult> LoadAsync(string documentId, DocumentEditorLoadOptions? options = null, CancellationToken cancellationToken = default)
        {
            LoadAttempts++;
            return Task.FromResult(new DocumentEditorLoadResult
            {
                Found = false,
                ErrorMessage = "Failed to load document"
            });
        }

        public Task<string?> LoadJsonAsync(string documentId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<DocumentEditorSaveResult> SaveAsync(DocumentEditorSaveRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentEditorSaveResult());

        public Task<DocumentVersion> CreateVersionAsync(DocumentVersionCreateRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentVersion());

        public Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(string documentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentVersion>>([]);

        public Task<IReadOnlyList<DocumentComment>> GetCommentsAsync(string documentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentComment>>([]);

        public Task<DocumentComment> CreateCommentAsync(string documentId, DocumentComment comment, CancellationToken cancellationToken = default)
            => Task.FromResult(comment);

        public Task<DocumentComment> AddCommentReplyAsync(string documentId, string commentId, DocumentCommentEntry entry, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentComment());

        public Task<DocumentComment> ResolveCommentAsync(string documentId, string commentId, DocumentEditorAuthor resolvedBy, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentComment());

        public Task<DocumentComment> ReopenCommentAsync(string documentId, string commentId, DocumentEditorAuthor reopenedBy, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentComment());

        public Task DeleteCommentAsync(string documentId, string commentId, DocumentEditorAuthor deletedBy, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TrackingDocumentProvider : InMemoryDocumentEditorProvider
    {
        public List<DocumentEditorSaveRequest> SaveRequests { get; } = [];

        public List<DocumentVersionCreateRequest> VersionRequests { get; } = [];

        public List<DocumentComment> CommentRequests { get; } = [];

        public List<DocumentCommentEntry> CommentReplies { get; } = [];

        public List<string> DeletedCommentIds { get; } = [];

        public bool FailNextSave { get; set; }

        public override async Task<DocumentEditorSaveResult> SaveAsync(DocumentEditorSaveRequest request, CancellationToken cancellationToken = default)
        {
            SaveRequests.Add(request);
            if (FailNextSave)
            {
                FailNextSave = false;
                return new DocumentEditorSaveResult
                {
                    Success = false,
                    ErrorMessage = "Save failed"
                };
            }

            return await base.SaveAsync(request, cancellationToken);
        }

        public override async Task<DocumentVersion> CreateVersionAsync(DocumentVersionCreateRequest request, CancellationToken cancellationToken = default)
        {
            VersionRequests.Add(request);
            return await base.CreateVersionAsync(request, cancellationToken);
        }

        public override async Task<DocumentComment> CreateCommentAsync(string documentId, DocumentComment comment, CancellationToken cancellationToken = default)
        {
            CommentRequests.Add(comment);
            return await base.CreateCommentAsync(documentId, comment, cancellationToken);
        }

        public override async Task<DocumentComment> AddCommentReplyAsync(string documentId, string commentId, DocumentCommentEntry entry, CancellationToken cancellationToken = default)
        {
            CommentReplies.Add(entry);
            return await base.AddCommentReplyAsync(documentId, commentId, entry, cancellationToken);
        }

        public override async Task DeleteCommentAsync(string documentId, string commentId, DocumentEditorAuthor deletedBy, CancellationToken cancellationToken = default)
        {
            DeletedCommentIds.Add(commentId);
            await base.DeleteCommentAsync(documentId, commentId, deletedBy, cancellationToken);
        }
    }

    private sealed class TestTokenValueProvider : IDocumentTokenValueProvider
    {
        private readonly IReadOnlyDictionary<string, DocumentTokenValue> _values;

        public TestTokenValueProvider(IReadOnlyDictionary<string, DocumentTokenValue> values)
        {
            _values = values;
        }

        public Task<IReadOnlyDictionary<string, DocumentTokenValue>> ResolveTokenValuesAsync(
            DocumentTokenResolutionContext context,
            IReadOnlyList<TokenRun> tokens,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_values);
        }
    }

    private sealed class TestPdfExportProvider : IDocumentPdfExportProvider
    {
        public List<DocumentPdfExportRequest> Requests { get; } = [];

        public Task<DocumentPdfExportResult> ExportPdfAsync(DocumentPdfExportRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new DocumentPdfExportResult
            {
                Content = [0x25, 0x50, 0x44, 0x46],
                FileName = "document.pdf"
            });
        }
    }

    private sealed class FailingAuditSink : IDocumentAuditSink
    {
        public int Attempts { get; private set; }

        public Task RecordAsync(DocumentEditorAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException("Audit sink unavailable.");
        }
    }

    private sealed class PendingSaveProvider : InMemoryDocumentEditorProvider
    {
        private readonly TaskCompletionSource<DocumentEditorSaveResult> _pending = new();

        public override Task<DocumentEditorSaveResult> SaveAsync(DocumentEditorSaveRequest request, CancellationToken cancellationToken = default)
            => _pending.Task;

        public void Complete()
        {
            _pending.TrySetResult(new DocumentEditorSaveResult
            {
                Success = true,
                Document = null,
                ConcurrencyToken = Guid.NewGuid().ToString("N")
            });
        }
    }
}
