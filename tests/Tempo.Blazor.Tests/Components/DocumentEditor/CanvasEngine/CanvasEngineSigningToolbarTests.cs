using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor.CanvasEngine;

/// <summary>
/// The signing-fields toolbar group (plan S2.19/S2.20) appears on the Insert tab only when the canvas
/// engine is active AND signer roles are configured — otherwise it has zero impact on the toolbar.
/// </summary>
public sealed class CanvasEngineSigningToolbarTests : LocalizationTestBase
{
    private const string InteropModulePath = "./_content/Tempo.Blazor/js/document-editor-canvas/interop.mjs";

    [Fact]
    public void SigningToolbarButton_IsVisible_WhenCanvasEngineHasSigningRoles()
    {
        SetupCanvasModule();
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("signing-toolbar-roles");

        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, "signing-toolbar-roles")
            .Add(p => p.Provider, provider)
            .Add(p => p.SigningRoles, new[] { new SigningSubmitterRole { Uuid = "signer", Name = "Signer", Color = "#2563eb" } }));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-canvas-engine-host']").Should().NotBeNull());
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.FindAll("[data-testid='document-insert-signing-field']").Should().ContainSingle();
        cut.FindAll("[data-testid='document-signing-group']").Should().ContainSingle();
    }

    [Fact]
    public void SigningToolbarButton_IsHidden_WhenNoSigningRoles()
    {
        SetupCanvasModule();
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("signing-toolbar-noroles");

        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, "signing-toolbar-noroles")
            .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-canvas-engine-host']").Should().NotBeNull());
        cut.Find("[data-testid='document-ribbon-tab-insert']").Click();

        cut.FindAll("[data-testid='document-insert-signing-field']").Should().BeEmpty();
        cut.FindAll("[data-testid='document-signing-group']").Should().BeEmpty();
    }

    private void SetupCanvasModule()
    {
        var module = JSInterop.SetupModule(InteropModulePath);
        module.Setup<string>("mount", _ => true).SetResult("canvas-host-test-handle");
        module.Setup<bool>("isDirty", _ => true).SetResult(false);
        module.SetupVoid("markSaved", _ => true).SetVoidResult();
        module.SetupVoid("focus", _ => true).SetVoidResult();
        module.Setup<string?>("getFormattingStateJson", _ => true).SetResult("""{"bold":false,"alignment":"left"}""");
        module.Setup<string?>("getUndoStateJson", _ => true).SetResult("""{"canUndo":false,"canRedo":false}""");
        module.Setup<string?>("getSelectionStateJson", _ => true).SetResult("""{"isCollapsed":true}""");
        module.Setup<string?>("getDiagnosticsJson", _ => true).SetResult("""{"architectureName":"CanvasDocumentEngine"}""");
        module.SetupVoid("dispose", _ => true).SetVoidResult();
    }
}
