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
    public async Task CtrlB_CallsCanvasBoldCommand()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        InitCanvasEngine(cut);

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "b",
            CtrlKey = true
        });

        HasCanvasCommand("bold").Should().BeTrue("Ctrl+B must dispatch bold to the canvas runtime");
    }

    [Fact]
    public async Task CtrlI_CallsCanvasItalicCommand()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        InitCanvasEngine(cut);

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "i",
            CtrlKey = true
        });

        HasCanvasCommand("italic").Should().BeTrue("Ctrl+I must dispatch italic to the canvas runtime");
    }

    [Fact]
    public async Task CtrlU_CallsCanvasUnderlineCommand()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        InitCanvasEngine(cut);

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "u",
            CtrlKey = true
        });

        HasCanvasCommand("underline").Should().BeTrue("Ctrl+U must dispatch underline to the canvas runtime");
    }

    // ─── 2.4 Save adapter ────────────────────────────────────────────────────

    [Fact]
    public async Task CtrlS_TriggersOnSaveRequestedCallback()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        DocumentEditorSaveRequest? capturedSave = null;

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.OnSaveRequested, req => capturedSave = req));

        InitCanvasEngine(cut);

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "s",
            CtrlKey = true
        });

        cut.WaitForAssertion(() => capturedSave.Should().NotBeNull("save command must reach provider"));
    }

    // ─── 2.4 Undo/Redo adapters ─────────────────────────────────────────────

    [Fact]
    public void UndoRedoCommandState_FollowsCanvasUndoState()
    {
        var module = SetupDocumentCanvasModule();
        module.Setup<string?>("getUndoStateJson", _ => true)
            .SetResult("""{"canUndo":true,"canRedo":true,"nextUndoDescription":"Type text","nextRedoDescription":"Restore image"}""");

        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        var registry = GetEditorRegistry(cut);
        cut.WaitForAssertion(() =>
        {
            registry.GetState("undo")!.IsEnabled.Should().BeTrue();
            registry.GetState("redo")!.IsEnabled.Should().BeTrue();
        });
    }

    // ─── 2.4 Link / Insert adapters ─────────────────────────────────────────

    [Fact]
    public async Task LinkCommand_WithPayload_CallsCanvasLinkCommand()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        InitCanvasEngine(cut);

        var payload = new WysiwygLinkPayload
        {
            Href = "https://example.test/phase-24",
            Title = "Phase 2.4 link"
        };

        await cut.InvokeAsync(() => GetEditorRegistry(cut)
            .GetRequired("link")
            .ExecuteAsync(new DocumentEditorCommandContext { HasDocument = true }, payload));

        HasCanvasCommand("link").Should().BeTrue("link command must execute the canvas link command");
        HasCanvasArgument("https://example.test/phase-24").Should().BeTrue();
    }

    [Fact]
    public async Task InsertTableCommand_CallsRuntimeInsertTableWithDefaultDimensions()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        InitCanvasEngine(cut);

        await cut.InvokeAsync(() => GetEditorRegistry(cut)
            .GetRequired("insertTable")
            .ExecuteAsync(new DocumentEditorCommandContext { HasDocument = true }));

        HasCanvasCommand("insertTable").Should().BeTrue("insertTable adapter must dispatch to the canvas runtime");
        HasCanvasArgument("\"rows\":2").Should().BeTrue();
        HasCanvasArgument("\"columns\":2").Should().BeTrue();
    }

    [Fact]
    public async Task InsertImageCommand_OpensCanvasImageDialog()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        InitCanvasEngine(cut);

        await cut.InvokeAsync(() => GetEditorRegistry(cut)
            .GetRequired("insertImage")
            .ExecuteAsync(new DocumentEditorCommandContext { HasDocument = true }));

        cut.Find("[data-testid='document-canvas-image-dialog']").Should().NotBeNull();
    }

    [Theory]
    [InlineData("replaceImage")]
    [InlineData("setImageAltText")]
    [InlineData("toggleImageCaption")]
    [InlineData("setImageLink")]
    [InlineData("setImageWrapMode")]
    [InlineData("setImageSize")]
    public async Task ImageCommands_DispatchToCanvasRuntime_WhenImageIsSelected(string commandName)
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        InitCanvasEngine(cut);

        var context = new DocumentEditorCommandContext
        {
            HasDocument = true,
            SelectionSnapshot = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "contract-inline-image",
                ActiveObjectId = "contract-inline-image",
                Region = "Body",
                SelectionMode = "Object",
                ObjectSelection = new WysiwygObjectSelectionSnapshot
                {
                    Kind = "image",
                    ObjectId = "contract-inline-image",
                    AnchorBlockId = "contract-inline-image"
                }
            }
        };

        await cut.InvokeAsync(() => GetEditorRegistry(cut)
            .GetRequired(commandName)
            .ExecuteAsync(context, new { AltText = "Accessible image", WrapMode = "Square", Width = 240d }));

        HasCanvasCommand(commandName).Should().BeTrue($"{commandName} must dispatch to the canvas runtime");
    }

    [Theory]
    [InlineData("insertTableRowBefore")]
    [InlineData("insertTableRowAfter")]
    [InlineData("insertTableColumnBefore")]
    [InlineData("insertTableColumnAfter")]
    [InlineData("deleteTableRow")]
    [InlineData("deleteTableColumn")]
    [InlineData("deleteTable")]
    [InlineData("mergeTableCells")]
    [InlineData("splitTableCell")]
    [InlineData("tableProperties")]
    [InlineData("cellProperties")]
    public async Task TableCommands_DispatchToCanvasRuntime_WhenCellIsSelected(string commandName)
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        InitCanvasEngine(cut);

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

        HasCanvasCommand(commandName).Should().BeTrue($"{commandName} must dispatch to the canvas runtime");
    }

    [Fact]
    public void FindReplaceCommandMetadata_MatchesRuntimeFirstContract()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
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

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        InitCanvasEngine(cut);
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

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider));

        InitCanvasEngine(cut);
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

        var cut = RenderDocumentEditor(parameters =>
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

        var cut = RenderDocumentEditor(parameters =>
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

        var cut = RenderDocumentEditor(parameters =>
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

        var cut = RenderDocumentEditor(parameters =>
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
    public async Task ReadOnly_CtrlB_DoesNotCallCanvasBoldCommand()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-1")
                      .Add(p => p.Provider, provider)
                      .Add(p => p.ReadOnly, true));

        InitCanvasEngine(cut);

        var callsBefore = CountCanvasCommand("bold");

        await cut.Find(".tm-document-editor").KeyDownAsync(new KeyboardEventArgs
        {
            Key = "b",
            CtrlKey = true
        });

        CountCanvasCommand("bold").Should().Be(callsBefore, "read-only mode disables the bold command");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void InitCanvasEngine(IRenderedComponent<TmDocumentEditor> cut)
    {
        cut.WaitForAssertion(() => cut.Find("[data-testid='document-canvas-engine-host']").Should().NotBeNull());
    }

    private bool HasCanvasCommand(string commandName) =>
        SetupDocumentCanvasModule().Invocations.Any(invocation =>
            invocation.Identifier == "execCommand"
            && invocation.Arguments.Count >= 2
            && string.Equals(invocation.Arguments[1]?.ToString(), commandName, StringComparison.Ordinal));

    private int CountCanvasCommand(string commandName) =>
        SetupDocumentCanvasModule().Invocations.Count(invocation =>
            invocation.Identifier == "execCommand"
            && invocation.Arguments.Count >= 2
            && string.Equals(invocation.Arguments[1]?.ToString(), commandName, StringComparison.Ordinal));

    private bool HasCanvasArgument(string expected) =>
        SetupDocumentCanvasModule().Invocations.Any(invocation =>
            invocation.Identifier == "execCommand"
            && invocation.Arguments.Any(arg => arg?.ToString()?.Contains(expected, StringComparison.Ordinal) == true));

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
