using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentEditorStatusBarTests : LocalizationTestBase
{
    // ── Pending indicator ───────────────────────────────────────────────────

    [Fact]
    public void StatusBar_NoPending_DoesNotShowPendingSpan()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.PendingCount, 0));

        cut.FindAll("[data-testid='document-pending-status']").Should().BeEmpty();
    }

    [Fact]
    public void StatusBar_PendingCountOne_ShowsPendingSpan()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.PendingCount, 1)
             .Add(x => x.PendingMessage, "Saving..."));

        cut.Find("[data-testid='document-pending-status']").Should().NotBeNull();
    }

    [Fact]
    public void StatusBar_PendingMessage_ShowsMessageText()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.PendingCount, 1)
             .Add(x => x.PendingMessage, "Saving..."));

        cut.Find("[data-testid='document-pending-status']").TextContent.Trim()
           .Should().Contain("Saving...");
    }

    [Fact]
    public void StatusBar_AutosaveWaiting_ShowsPendingText()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.PendingCount, 1)
             .Add(x => x.PendingMessage, "Autosave pending..."));

        cut.Find("[data-testid='document-pending-status']").TextContent.Trim()
           .Should().Contain("Autosave pending...");
    }

    [Fact]
    public void StatusBar_AutosaveSaving_ShowsSavingText()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.PendingCount, 1)
             .Add(x => x.PendingMessage, "Saving..."));

        cut.Find("[data-testid='document-pending-status']").TextContent.Trim()
           .Should().Contain("Saving...");
    }

    [Fact]
    public void StatusBar_SaveErrorWithRetry_ShowsRetryButton()
    {
        var called = false;
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.SaveMessage, "Save failed.")
             .Add(x => x.CanRetrySave, true)
             .Add(x => x.OnRetrySave, () => called = true));

        cut.Find("[data-testid='document-save-retry']").Click();

        called.Should().BeTrue();
    }

    [Fact]
    public void StatusBar_SaveErrorWithoutRetry_HidesRetryButton()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.SaveMessage, "Save failed.")
             .Add(x => x.CanRetrySave, false));

        cut.FindAll("[data-testid='document-save-retry']").Should().BeEmpty();
    }

    [Fact]
    public void StatusBar_PendingCountWithoutMessage_ShowsGenericPendingText()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.PendingCount, 2)
             .Add(x => x.PendingMessage, (string?)null));

        cut.Find("[data-testid='document-pending-status']").TextContent.Trim()
           .Should().Contain("2");
    }

    [Fact]
    public void StatusBar_PendingActive_HidesDirtyStatus()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.PendingCount, 1)
             .Add(x => x.PendingMessage, "Saving...")
             .Add(x => x.IsDirty, true));

        // Pending takes priority — dirty span should not appear
        cut.FindAll("[data-testid='document-dirty-status']").Should().BeEmpty();
    }

    [Fact]
    public void StatusBar_NoPending_ShowsDirtyWhenSet()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.PendingCount, 0)
             .Add(x => x.IsDirty, true));

        cut.Find("[data-testid='document-dirty-status']").Should().NotBeNull();
    }

    // ── Save message ────────────────────────────────────────────────────────

    [Fact]
    public void StatusBar_SaveMessage_ShowsSaveMessageSpan()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.SaveMessage, "Saved!"));

        cut.Find("[data-testid='document-save-message']").TextContent.Trim()
           .Should().Be("Saved!");
    }

    [Fact]
    public void StatusBar_NoSaveMessage_DoesNotShowSaveMessageSpan()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.SaveMessage, (string?)null));

        cut.FindAll("[data-testid='document-save-message']").Should().BeEmpty();
    }

    // ── Metrics ─────────────────────────────────────────────────────────────

    [Fact]
    public void StatusBar_RendersWordAndPageCount()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.WordCount, 42)
             .Add(x => x.PageCount, 3));

        cut.Find("[data-testid='document-status-word-count']").TextContent
           .Should().Contain("42");
        cut.Find("[data-testid='document-status-page-count']").TextContent
           .Should().Contain("3");
    }

    [Fact]
    public void StatusBar_ZoomLabel_IsDisplayed()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.ZoomLabel, "75%"));

        cut.Find("[data-testid='document-status-zoom']").TextContent
           .Should().Contain("75%");
    }

    // ── Runtime message ─────────────────────────────────────────────────────

    [Fact]
    public void StatusBar_RuntimeMessage_ShowsMessageSpan()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.RuntimeMessage, "Editor recovered."));

        cut.Find("[data-testid='document-runtime-message']").TextContent.Trim()
           .Should().Contain("Editor recovered.");
    }

    [Fact]
    public void StatusBar_NoRuntimeMessage_DoesNotShowMessageSpan()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.RuntimeMessage, (string?)null));

        cut.FindAll("[data-testid='document-runtime-message']").Should().BeEmpty();
    }

    [Fact]
    public void StatusBar_RuntimeMessage_RecoveredHasRecoveredClass()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.RuntimeMessage, "Recovered!")
             .Add(x => x.RuntimeFailed, false));

        cut.Find("[data-testid='document-runtime-message']").ClassList
           .Should().Contain("tm-document-editor__runtime-message--recovered");
    }

    [Fact]
    public void StatusBar_RuntimeMessage_FailedHasFailedClass()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.RuntimeMessage, "Recovery failed!")
             .Add(x => x.RuntimeFailed, true));

        cut.Find("[data-testid='document-runtime-message']").ClassList
           .Should().Contain("tm-document-editor__runtime-message--failed");
    }

    [Fact]
    public void StatusBar_RuntimeMessage_HasAlertRole()
    {
        var cut = Render<TmDocumentEditorStatusBar>(p =>
            p.Add(x => x.RuntimeMessage, "Alert!"));

        cut.Find("[data-testid='document-runtime-message']")
           .GetAttribute("role").Should().Be("alert");
    }
}
