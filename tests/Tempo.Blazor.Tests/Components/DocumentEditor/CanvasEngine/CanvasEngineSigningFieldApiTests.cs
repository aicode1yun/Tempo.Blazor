using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor.CanvasEngine;

/// <summary>
/// bUnit coverage for the C# signing field bridge API (plan S2.17/S2.18): the host reads signing
/// fields from the engine, inserts them via the command, and forwards signer roles into engine options;
/// the editor delegates to the host (or fails loudly when the canvas engine is not active).
/// </summary>
public sealed class CanvasEngineSigningFieldApiTests : LocalizationTestBase
{
    private const string InteropModulePath = "./_content/Tempo.Blazor/js/document-editor-canvas/interop.mjs";

    [Fact]
    public async Task CanvasEngineHost_GetSigningFieldsAsync_MapsEngineDescriptorsToSigningFields()
    {
        SetupCanvasModule();
        var cut = RenderHost();
        cut.WaitForAssertion(() => cut.Instance.IsReady.Should().BeTrue());

        var fields = await cut.InvokeAsync(() => cut.Instance.GetSigningFieldsAsync("editor-export"));

        fields.Should().HaveCount(1);
        fields[0].Uuid.Should().Be("footer-field");
        fields[0].Type.Should().Be(SigningFieldType.Initials);
        fields[0].Areas.Should().HaveCount(2);
        fields[0].Areas.Should().OnlyContain(area => area.AttachmentUuid == "editor-export");
        fields[0].Areas.Select(area => area.Page).Should().ContainInOrder(0, 1);
    }

    [Fact]
    public async Task CanvasEngineHost_InsertSigningFieldAsync_ExecutesInsertCommand()
    {
        var module = SetupCanvasModule();
        var cut = RenderHost();
        cut.WaitForAssertion(() => cut.Instance.IsReady.Should().BeTrue());

        await cut.InvokeAsync(() => cut.Instance.InsertSigningFieldAsync(new SigningField
        {
            Type = SigningFieldType.Signature,
            SubmitterUuid = "signer",
            Required = true,
            Title = "Signature",
        }));

        var exec = module.Invocations.FirstOrDefault(invocation => invocation.Identifier == "execCommand");
        exec.Should().NotBeNull();
        exec.Arguments[1]?.ToString().Should().Be("insertSigningField");
        exec.Arguments[2]?.ToString().Should().Contain("signature");
    }

    [Fact]
    public void CanvasEngineHost_SigningRoles_FlowIntoEngineOptions()
    {
        var module = SetupCanvasModule();
        var cut = RenderHost(roles:
        [
            new SigningSubmitterRole { Uuid = "signer", Name = "Signer", Color = "#2563eb" },
        ]);
        cut.WaitForAssertion(() => cut.Instance.IsReady.Should().BeTrue());

        var mount = module.Invocations.First(invocation => invocation.Identifier == "mount");
        var optionsJson = mount.Arguments[3]?.ToString() ?? string.Empty;
        optionsJson.Should().Contain("signingRoles");
        optionsJson.Should().Contain("#2563eb");
    }

    [Fact]
    public async Task TmDocumentEditor_GetSigningFieldsAsync_ThrowsWhenCanvasEngineIsNotActive()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("signing-legacy");
        var cut = RenderDocumentEditorLegacy(parameters => parameters
            .Add(p => p.DocumentId, "signing-legacy")
            .Add(p => p.Provider, provider));

        var act = async () => await cut.InvokeAsync(() => cut.Instance.GetSigningFieldsAsync());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*canvas*");
    }

    [Fact]
    public async Task GetSelectionStateAsync_MapsSelectedSigningFieldWithScope()
    {
        var module = JSInterop.SetupModule(InteropModulePath);
        module.Setup<string>("mount", _ => true).SetResult("canvas-host-test-handle");
        module.Setup<bool>("isDirty", _ => true).SetResult(false);
        module.SetupVoid("focus", _ => true).SetVoidResult();
        module.Setup<string?>("getSelectionStateJson", _ => true).SetResult(
            """{"isCollapsed":true,"focusBlockId":"f1","signingFieldSelected":true,"signingField":{"uuid":"footer-field","fieldType":"initials","submitterUuid":"signer","required":true,"label":"Initials","headerFooterId":"footer-1","scope":"Primary","repeats":true}}""");
        module.SetupVoid("dispose", _ => true).SetVoidResult();

        var cut = RenderHost();
        cut.WaitForAssertion(() => cut.Instance.IsReady.Should().BeTrue());

        var selection = await cut.InvokeAsync(() => cut.Instance.GetSelectionStateAsync());

        selection.SigningFieldSelected.Should().BeTrue();
        selection.SigningField.Should().NotBeNull();
        selection.SigningField!.Uuid.Should().Be("footer-field");
        selection.SigningField.HeaderFooterId.Should().Be("footer-1");
        selection.SigningField.Repeats.Should().BeTrue();
        selection.SigningField.Required.Should().BeTrue();
    }

    private IRenderedComponent<TmDocumentCanvasEngineHost> RenderHost(IReadOnlyList<SigningSubmitterRole>? roles = null)
        => RenderComponent<TmDocumentCanvasEngineHost>(parameters =>
        {
            parameters.Add(p => p.Document, DocumentEditorDocument.Empty("signing-host"));
            parameters.Add(p => p.AriaLabel, "Document editor");
            parameters.Add(p => p.InputAriaLabel, "Document editor");
            if (roles is not null)
            {
                parameters.Add(p => p.SigningRoles, roles);
            }
        });

    private BunitJSModuleInterop SetupCanvasModule()
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
        module.Setup<string?>("execCommand", _ => true).SetResult("""{"changed":true,"commandId":"insertSigningField"}""");
        module.Setup<string?>("getSigningFieldsJson", _ => true).SetResult(
            """[{"uuid":"footer-field","fieldType":"initials","submitterUuid":"signer","required":false,"label":"Initials","options":[],"areas":[{"page":0,"x":0.4,"y":0.95,"width":0.12,"height":0.04},{"page":1,"x":0.4,"y":0.95,"width":0.12,"height":0.04}]}]""");
        module.SetupVoid("dispose", _ => true).SetVoidResult();
        return module;
    }
}
