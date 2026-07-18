using Bunit;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Phase 8: role-matrix enforcement in TmDocumentEditor. SuggestOnly can propose but never edit
/// directly — track changes is forced on and its toggle is locked, and accept/reject of revisions
/// is denied. Commenter gets a read-only canvas with commenting still available.
/// </summary>
public class TmDocumentEditorRolePermissionsTests : LocalizationTestBase
{
    [Fact]
    public void SuggestOnly_ForcesTrackChangesOnAndLocksTheToggle()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-suggest-only");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-suggest-only")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.Permissions, DocumentEditorPermissions.ForRole(DocumentEditorRole.SuggestOnly)));
        cut.WaitForElement("[data-testid='document-canvas-engine-host']");

        // Track changes is on without any user action… (the toggle lives on the Review ribbon tab)
        cut.WaitForElement("[data-testid='document-ribbon-tab-review']").Click();
        var toggle = cut.WaitForElement("[data-testid='document-track-changes']");
        toggle.GetAttribute("aria-pressed").Should().Be("true", "suggest-only edits must always be tracked");

        // …and the toggle cannot turn it off.
        toggle.Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-track-changes']")
            .GetAttribute("aria-pressed").Should().Be("true", "the toggle is locked for suggest-only"));

        // The canvas engine itself is told to track every edit.
        JSInterop.Invocations
            .Where(invocation => invocation.Identifier is "mount" or "setOptions")
            .SelectMany(invocation => invocation.Arguments)
            .OfType<string>()
            .Should().Contain(json => json.Contains("\"trackChanges\":{\"enabled\":true"),
                "the engine must record every edit as a revision");

        cut.Instance.CanReviewRevisionsGate.Should().BeFalse("suggest-only must not accept/reject revisions");

        // Typing stays possible (the proposals ARE tracked edits) — the input is not read-only.
        cut.Find("[data-testid='document-canvas-hidden-input']").HasAttribute("readonly").Should().BeFalse();
    }

    [Fact]
    public void Commenter_HasReadOnlyCanvasButCanComment()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-commenter");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-commenter")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.ShowComments, true)
                      .Add(p => p.Permissions, DocumentEditorPermissions.ForRole(DocumentEditorRole.Commenter)));
        cut.WaitForElement("[data-testid='document-canvas-engine-host']");

        // The canvas is not editable…
        JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "mount")
            .SelectMany(invocation => invocation.Arguments)
            .OfType<string>()
            .Should().Contain(json => json.Contains("\"canEdit\":false"),
                "a commenter must not edit content");

        // …the hidden input is hard-blocked (typing cannot bypass the C# command gates)…
        cut.Find("[data-testid='document-canvas-hidden-input']").HasAttribute("readonly").Should().BeTrue();

        // …but commenting stays available.
        cut.Instance.CanCommentGate.Should().BeTrue();
        cut.Instance.CanReviewRevisionsGate.Should().BeFalse();
    }

    [Fact]
    public void Editor_KeepsDirectEditingAndReview()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-editor-role");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-editor-role")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.Permissions, DocumentEditorPermissions.ForRole(DocumentEditorRole.Editor)));
        cut.WaitForElement("[data-testid='document-canvas-engine-host']");

        cut.WaitForElement("[data-testid='document-ribbon-tab-review']").Click();
        var toggle = cut.WaitForElement("[data-testid='document-track-changes']");
        toggle.GetAttribute("aria-pressed").Should().Be("false", "editors edit directly by default");
        cut.Instance.CanReviewRevisionsGate.Should().BeTrue();
    }
}
