using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor.CanvasEngine;

/// <summary>
/// bUnit coverage for the engine-sourced content-control popover (perf plan N2): the popover state
/// rides the O(selection) selection payload, so opening it never marshals the full document
/// (<c>getModelJson</c>) into C# — the last per-settled-edit O(document) pull.
/// </summary>
public sealed class CanvasEngineContentControlPopoverTests : LocalizationTestBase
{
    private const string InteropModulePath = "./_content/Tempo.Blazor.DocumentEditor/js/document-editor-canvas/interop.mjs";

    private const string DateControlSelectionJson =
        """
        {
            "isCollapsed": true,
            "focusBlockId": "p1",
            "focusOffset": 8,
            "contentControlSelected": true,
            "contentControl": {
                "controlId": "cc-date-1",
                "kind": "date",
                "title": "Delivery date",
                "isRequired": true,
                "lockContent": false,
                "text": "",
                "selectedValue": "",
                "dateIso": "2026-07-10",
                "assetId": "",
                "items": []
            }
        }
        """;

    [Fact]
    public async Task GetSelectionStateAsync_MapsContentControlAtCaret()
    {
        var module = JSInterop.SetupModule(InteropModulePath);
        module.Setup<string>("mount", _ => true).SetResult("canvas-host-test-handle");
        module.Setup<bool>("isDirty", _ => true).SetResult(false);
        module.SetupVoid("focus", _ => true).SetVoidResult();
        module.Setup<string?>("getSelectionStateJson", _ => true).SetResult(
            """
            {
                "isCollapsed": true,
                "focusBlockId": "p1",
                "contentControlSelected": true,
                "contentControl": {
                    "controlId": "cc-dd-1",
                    "kind": "dropDown",
                    "title": "country",
                    "isRequired": false,
                    "lockContent": true,
                    "selectedValue": "cz",
                    "items": [
                        { "value": "cz", "displayText": "Czechia" },
                        { "value": "sk", "displayText": "Slovakia" }
                    ]
                }
            }
            """);
        module.SetupVoid("dispose", _ => true).SetVoidResult();

        var cut = Render<TmDocumentCanvasEngineHost>(parameters =>
        {
            parameters.Add(p => p.Document, Tempo.Blazor.DocumentEditor.Models.DocumentEditorDocument.Empty("cc-host"));
            parameters.Add(p => p.AriaLabel, "Document editor");
            parameters.Add(p => p.InputAriaLabel, "Document editor");
        });
        cut.WaitForAssertion(() => cut.Instance.IsReady.Should().BeTrue());

        var selection = await cut.InvokeAsync(() => cut.Instance.GetSelectionStateAsync());

        selection.ContentControlSelected.Should().BeTrue();
        selection.ContentControl.Should().NotBeNull();
        selection.ContentControl!.ControlId.Should().Be("cc-dd-1");
        selection.ContentControl.Kind.Should().Be("dropDown");
        selection.ContentControl.Title.Should().Be("country");
        selection.ContentControl.LockContent.Should().BeTrue();
        selection.ContentControl.SelectedValue.Should().Be("cz");
        selection.ContentControl.Items.Should().HaveCount(2);
        selection.ContentControl.Items[0].Value.Should().Be("cz");
        selection.ContentControl.Items[0].DisplayText.Should().Be("Czechia");
    }

    [Fact]
    public void ContentControlPopover_OpensFromSelectionState_WithoutFullDocumentMarshal()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("cc-popover-doc");
        SetDocumentCanvasSelectionStateJson(DateControlSelectionJson);

        var cut = RenderDocumentEditor(parameters => parameters
            .Add(p => p.DocumentId, "cc-popover-doc")
            .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
        {
            var popover = cut.Find("[data-testid='document-canvas-content-control-popover']");
            popover.GetAttribute("data-control-id").Should().Be("cc-date-1");
            popover.GetAttribute("data-control-kind").Should().Be("Date");
        });

        var module = SetupDocumentCanvasModule();
        module.Invocations.Where(invocation => invocation.Identifier == "getModelJson")
            .Should().BeEmpty("the popover must be driven by the selection state, not a full-document marshal");
    }

    [Fact]
    public void ContentControlPopover_UnknownKind_DoesNotOpen()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("cc-popover-unknown");
        SetDocumentCanvasSelectionStateJson(
            """
            {
                "isCollapsed": true,
                "focusBlockId": "p1",
                "contentControlSelected": true,
                "contentControl": { "controlId": "cc-x", "kind": "mystery", "title": "X" }
            }
            """);

        var cut = RenderDocumentEditor(parameters => parameters
            .Add(p => p.DocumentId, "cc-popover-unknown")
            .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-canvas-engine-host']").Should().NotBeNull());
        cut.FindAll("[data-testid='document-canvas-content-control-popover']").Should().BeEmpty();
    }

    [Fact]
    public void ContentControlPopover_NoFocusBlock_StaysClosed()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("cc-popover-empty");
        SetDocumentCanvasSelectionStateJson("""{"isCollapsed":true,"focusBlockId":""}""");

        var cut = RenderDocumentEditor(parameters => parameters
            .Add(p => p.DocumentId, "cc-popover-empty")
            .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-canvas-engine-host']").Should().NotBeNull());
        cut.FindAll("[data-testid='document-canvas-content-control-popover']").Should().BeEmpty();
    }
}
