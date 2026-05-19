using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Components.DocumentEditor.Registry;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>bUnit tests for command registry adapters and keyboard dispatch (Phases 2.4–2.5).</summary>
public class DocumentEditorCommandAdapterTests : LocalizationTestBase
{
    public DocumentEditorCommandAdapterTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ─── 2.4 Bold/Italic/Underline adapters ─────────────────────────────────

    [Fact]
    public async Task CtrlB_CallsToggleBoldRuntime()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "b",
            CtrlKey = true
        });

        HasJsCall("toggleBold").Should().BeTrue("Ctrl+B must dispatch toggleBold to JS runtime");
    }

    [Fact]
    public async Task CtrlI_CallsToggleItalicRuntime()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "i",
            CtrlKey = true
        });

        HasJsCall("toggleItalic").Should().BeTrue("Ctrl+I must dispatch toggleItalic to JS runtime");
    }

    [Fact]
    public async Task CtrlU_CallsToggleUnderlineRuntime()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "u",
            CtrlKey = true
        });

        HasJsCall("toggleUnderline").Should().BeTrue("Ctrl+U must dispatch toggleUnderline to JS runtime");
    }

    // ─── 2.4 Save adapter ────────────────────────────────────────────────────

    [Fact]
    public async Task CtrlS_TriggersOnSaveRequestedCallback()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        DocumentEditorSaveRequest? capturedSave = null;

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.OnSaveRequested, req => capturedSave = req));

        await InitJsEngineAsync(cut);

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "s",
            CtrlKey = true
        });

        cut.WaitForAssertion(() => capturedSave.Should().NotBeNull("save command must reach provider"));
    }

    // ─── 2.4 Undo/Redo adapters ─────────────────────────────────────────────

    [Fact]
    public async Task UndoRedoCommandState_FollowsWysiwygUndoState()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);
        var host = cut.FindComponent<TmDocumentWysiwygHost>();

        await cut.InvokeAsync(() => host.Instance.HandleUndoStateChanged(new WysiwygUndoState
        {
            CanUndo = true,
            CanRedo = true,
            NextUndoDescription = "Type text",
            NextRedoDescription = "Restore image",
            JsOwnedUndo = true
        }));

        var registry = GetEditorRegistry(cut);
        registry.GetState("undo")!.IsEnabled.Should().BeTrue();
        registry.GetState("undo")!.Value.Should().Be("Type text");
        registry.GetState("redo")!.IsEnabled.Should().BeTrue();
        registry.GetState("redo")!.Value.Should().Be("Restore image");

        await cut.InvokeAsync(() => host.Instance.HandleUndoStateChanged(new WysiwygUndoState
        {
            CanUndo = false,
            CanRedo = false,
            JsOwnedUndo = true
        }));

        registry.GetState("undo")!.IsEnabled.Should().BeFalse();
        registry.GetState("redo")!.IsEnabled.Should().BeFalse();
    }

    // ─── 2.4 Link / Insert adapters ─────────────────────────────────────────

    [Fact]
    public async Task LinkCommand_WithPayload_CallsRuntimeInsertLink()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);

        var payload = new WysiwygLinkPayload
        {
            Href = "https://example.test/phase-24",
            Title = "Phase 2.4 link"
        };

        await cut.InvokeAsync(() => GetEditorRegistry(cut)
            .GetRequired("link")
            .ExecuteAsync(new DocumentEditorCommandContext { HasDocument = true }, payload));

        HasJsCall("insertLink").Should().BeTrue("link command must execute the runtime insertLink command");
        HasJsArgument("https://example.test/phase-24").Should().BeTrue();
        HasJsArgument("Phase 2.4 link").Should().BeTrue();
    }

    [Fact]
    public async Task InsertTableCommand_CallsRuntimeInsertTableWithDefaultDimensions()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);

        await cut.InvokeAsync(() => GetEditorRegistry(cut)
            .GetRequired("insertTable")
            .ExecuteAsync(new DocumentEditorCommandContext { HasDocument = true }));

        HasJsCall("insertTable").Should().BeTrue("insertTable adapter must dispatch to the WYSIWYG runtime");
        HasJsArgument("rows = 2").Should().BeTrue();
        HasJsArgument("columns = 2").Should().BeTrue();
    }

    [Fact]
    public async Task InsertImageCommand_OpensWysiwygImageDialog()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);

        await cut.InvokeAsync(() => GetEditorRegistry(cut)
            .GetRequired("insertImage")
            .ExecuteAsync(new DocumentEditorCommandContext { HasDocument = true }));

        cut.Find("[data-testid='document-wysiwyg-image-dialog']").Should().NotBeNull();
    }

    [Theory]
    [InlineData("replaceImage")]
    [InlineData("setImageAltText")]
    [InlineData("toggleImageCaption")]
    [InlineData("setImageLink")]
    [InlineData("setImageWrapMode")]
    [InlineData("setImageSize")]
    public async Task ImageCommands_DispatchToWysiwygRuntime_WhenImageIsSelected(string commandName)
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);

        var context = new DocumentEditorCommandContext
        {
            HasDocument = true,
            SelectionSnapshot = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "img-1",
                ActiveImageBlockId = "img-1",
                Region = "Image"
            }
        };

        await cut.InvokeAsync(() => GetEditorRegistry(cut)
            .GetRequired(commandName)
            .ExecuteAsync(context, new { AltText = "Accessible image", WrapMode = "Square", Width = 240d }));

        HasJsCall(commandName).Should().BeTrue($"{commandName} must dispatch to the WYSIWYG runtime");
    }

    [Theory]
    [InlineData("insertTableRowBefore")]
    [InlineData("insertTableRowAfter")]
    [InlineData("insertTableColumnBefore")]
    [InlineData("insertTableColumnAfter")]
    [InlineData("deleteTableRow")]
    [InlineData("deleteTableColumn")]
    [InlineData("mergeTableCells")]
    [InlineData("splitTableCell")]
    [InlineData("tableProperties")]
    [InlineData("cellProperties")]
    public async Task TableCommands_DispatchToWysiwygRuntime_WhenCellIsSelected(string commandName)
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);

        var context = new DocumentEditorCommandContext
        {
            HasDocument = true,
            SelectionSnapshot = new WysiwygSelectionSnapshot
            {
                ActiveTableCellId = "cell-1",
                Region = "TableCell"
            }
        };

        await cut.InvokeAsync(() => GetEditorRegistry(cut)
            .GetRequired(commandName)
            .ExecuteAsync(context));

        HasJsCall(commandName).Should().BeTrue($"{commandName} must dispatch to the WYSIWYG runtime");
    }

    [Fact]
    public void FindReplaceCommandMetadata_MatchesRuntimeFirstContract()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
        {
            var registry = GetEditorRegistry(cut);
            registry.GetState("find")!.AffectsData.Should().BeFalse();
            registry.GetState("replace")!.AffectsData.Should().BeTrue();
            registry.GetState("replaceAll")!.AffectsData.Should().BeTrue();
        });
    }

    [Fact]
    public async Task CtrlF_OpensFindPanelThroughRegistryCommand()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);
        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "f",
            CtrlKey = true
        });

        cut.Find("[data-testid='document-find-panel']").Should().NotBeNull();
        cut.FindAll("[data-testid='document-replace-input']").Should().BeEmpty();
    }

    [Fact]
    public async Task CtrlH_OpensReplacePanelThroughRegistryCommand()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        await InitJsEngineAsync(cut);
        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "h",
            CtrlKey = true
        });

        cut.Find("[data-testid='document-find-panel']").Should().NotBeNull();
        cut.Find("[data-testid='document-replace-input']").Should().NotBeNull();
    }

    // ─── 2.4 ExportPdf / ImportDocx / ExportDocx enabled by provider caps ───

    [Fact]
    public void ExportPdfCommand_EnabledWhenProviderAvailableAndDocumentLoaded()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        var pdfProvider = new StubPdfExportProvider();

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.PdfExportProvider, pdfProvider)
                      .Add(p => p.Permissions, new DocumentEditorPermissions
                      {
                          CanRead = true,
                          CanEdit = true,
                          CanExport = true
                      }));

        cut.WaitForAssertion(() =>
        {
            var toolbar = cut.FindComponent<TmDocumentEditorToolbar>();
            toolbar.Instance.CanExportPdf.Should().BeTrue();
        });
    }

    [Fact]
    public void ExportPdfCommand_DisabledWhenNoPdfProvider()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() =>
        {
            var toolbar = cut.FindComponent<TmDocumentEditorToolbar>();
            toolbar.Instance.CanExportPdf.Should().BeFalse();
        });
    }

    [Fact]
    public void ImportDocxCommand_EnabledWhenFormatProviderSupportsDocxImport()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        var formatProvider = new StubFormatProvider(canImportDocx: true, canExportDocx: true);

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.FormatProvider, formatProvider)
                      .Add(p => p.Permissions, new DocumentEditorPermissions
                      {
                          CanRead = true,
                          CanEdit = true,
                          CanImport = true
                      }));

        cut.WaitForAssertion(() =>
        {
            var toolbar = cut.FindComponent<TmDocumentEditorToolbar>();
            toolbar.Instance.CanImportDocx.Should().BeTrue();
        });
    }

    [Fact]
    public void ExportDocxCommand_EnabledWhenFormatProviderSupportsDocxExport()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        var formatProvider = new StubFormatProvider(canImportDocx: false, canExportDocx: true);

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.FormatProvider, formatProvider)
                      .Add(p => p.Permissions, new DocumentEditorPermissions
                      {
                          CanRead = true,
                          CanEdit = true,
                          CanExport = true
                      }));

        cut.WaitForAssertion(() =>
        {
            var toolbar = cut.FindComponent<TmDocumentEditorToolbar>();
            toolbar.Instance.CanExportDocx.Should().BeTrue();
        });
    }

    // ─── 2.5 Keyboard shortcuts – disabled command is blocked ────────────────

    [Fact]
    public async Task ReadOnly_CtrlB_DoesNotCallToggleBold()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ReadOnly, true));

        await InitJsEngineAsync(cut);

        var callsBefore = CountJsCall("toggleBold");

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "b",
            CtrlKey = true
        });

        CountJsCall("toggleBold").Should().Be(callsBefore, "read-only mode disables the bold command");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static async Task InitJsEngineAsync(IRenderedComponent<TmDocumentEditor> cut)
    {
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-wysiwyg-host']").Should().NotBeNull());
        var host = cut.FindComponent<TmDocumentWysiwygHost>();
        await cut.InvokeAsync(() => host.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        }));
    }

    private bool HasJsCall(string commandName) =>
        JSInterop.Invocations.Any(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.executeCommand" &&
            invocation.Arguments.Any(arg => arg != null && arg.ToString()!.Contains(commandName)));

    private int CountJsCall(string commandName) =>
        JSInterop.Invocations.Count(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.executeCommand" &&
            invocation.Arguments.Any(arg => arg != null && arg.ToString()!.Contains(commandName)));

    private bool HasJsArgument(string expected) =>
        JSInterop.Invocations.Any(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.executeCommand" &&
            invocation.Arguments.Any(arg => arg?.ToString()?.Contains(expected, StringComparison.Ordinal) == true));

    private static DocumentEditorCommandRegistry GetEditorRegistry(IRenderedComponent<TmDocumentEditor> cut) =>
        cut.FindComponent<TmDocumentEditorToolbar>().Instance.CommandRegistry
        ?? throw new InvalidOperationException("Document editor toolbar did not receive the command registry.");

    // ─── Stubs ───────────────────────────────────────────────────────────────

    private sealed class StubPdfExportProvider : IDocumentPdfExportProvider
    {
        public Task<DocumentPdfExportResult> ExportPdfAsync(DocumentPdfExportRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentPdfExportResult { Content = [] });
    }

    private sealed class StubFormatProvider : IDocumentFormatProvider
    {
        private readonly bool _canImportDocx;
        private readonly bool _canExportDocx;

        public StubFormatProvider(bool canImportDocx, bool canExportDocx)
        {
            _canImportDocx = canImportDocx;
            _canExportDocx = canExportDocx;
        }

        public Task<IReadOnlyList<DocumentFormatProviderCapability>> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentFormatProviderCapability>>(
            [
                new DocumentFormatProviderCapability
                {
                    Format = DocumentFormatProviderKind.Docx,
                    CanImport = _canImportDocx,
                    CanExport = _canExportDocx
                }
            ]);

        public Task<DocumentFormatImportProviderResult> ImportAsync(DocumentFormatImportProviderRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentFormatImportProviderResult { Document = DocumentEditorDocument.Empty("stub") });

        public Task<DocumentFormatExportProviderResult> ExportAsync(DocumentFormatExportProviderRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentFormatExportProviderResult { Content = [] });
    }
}
