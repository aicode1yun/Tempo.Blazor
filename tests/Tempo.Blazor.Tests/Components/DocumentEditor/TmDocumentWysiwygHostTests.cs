using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>TDD tests for TmDocumentWysiwygHost (WYSIWYG JS engine shell).</summary>
public class TmDocumentWysiwygHostTests : LocalizationTestBase
{
    public TmDocumentWysiwygHostTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Host_Renders_WithTestId()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>();

        cut.Find("[data-testid='document-wysiwyg-host']").Should().NotBeNull();
    }

    [Fact]
    public void Host_DoesNotRenderDocumentContentInsideBlazorMarkup()
    {
        var document = CreateEmptyDocument();
        document.Blocks.Add(new DocumentBlock
        {
            Id = "blazor-boundary-block",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Id = "blazor-boundary-inline", Text = "JS owns this rendered text" }]
            }
        });

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters => parameters
            .Add(p => p.Document, document));

        cut.Markup.Should().NotContain("JS owns this rendered text");
        cut.Markup.Should().Contain("data-testid=\"document-wysiwyg-host\"");
    }

    [Fact]
    public void Host_RendersRulerAndZoomStateOnRoot()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters => parameters
            .Add(p => p.ShowRuler, true)
            .Add(p => p.ZoomPercent, 125)
            .Add(p => p.ZoomPageWidth, true));

        var host = cut.Find("[data-testid='document-wysiwyg-host']");
        host.GetAttribute("class").Should().Contain("tm-document-wysiwyg-host--ruler");
        host.GetAttribute("class").Should().Contain("tm-document-wysiwyg-host--zoom-page-width");
        host.GetAttribute("style").Should().Contain("--tm-document-zoom: 1.25");
        host.GetAttribute("data-zoom-percent").Should().Be("125");
        host.GetAttribute("data-zoom-mode").Should().Be("page-width");
    }

    [Fact]
    public void Host_Accessibility_ExposesEditableSurfaceRoleAndLabels()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>();

        var host = cut.Find("[data-testid='document-wysiwyg-host']");
        host.GetAttribute("role").Should().Be("textbox");
        host.GetAttribute("aria-label").Should().Be("WYSIWYG document surface");
        host.GetAttribute("aria-multiline").Should().Be("true");
        host.GetAttribute("aria-readonly").Should().Be("false");

        var describedBy = host.GetAttribute("aria-describedby");
        describedBy.Should().NotBeNullOrWhiteSpace();
        var help = cut.Find($"#{describedBy}");
        help.GetAttribute("data-testid").Should().Be("document-wysiwyg-accessibility-help");
        help.TextContent.Should().Contain("Use Tab");
    }

    [Fact]
    public void Host_Lifecycle_CallsJsCreate()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.create");
        JSInterop.Invocations.Should().NotContain(invocation =>
            invocation.Identifier.StartsWith("tmDocumentWysiwyg.", StringComparison.Ordinal)
            || invocation.Identifier.StartsWith("tmDocumentEditorWysiwyg.", StringComparison.Ordinal));
    }

    [Fact]
    public void Host_Lifecycle_PassesOptionsToJsCreate()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.ReadOnly, true));

        var invocation = JSInterop.Invocations.FirstOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.create");
        invocation.Should().NotBeNull();
        invocation.Arguments.Should().HaveCount(3);

        var options = invocation.Arguments[1] as WysiwygEditorOptions;
        options.Should().NotBeNull();
        options.ReadOnly.Should().BeTrue();
        options.ProtocolVersion.Should().Be(1);
        options.EnableMutationGuard.Should().BeTrue();
        options.AccessibilityHelp.Should().Contain("Use Tab");
        options.PageLabel.Should().Be("Page {0}");
        options.BodyLabel.Should().Be("Document body, page {0}");
        options.HeaderLabel.Should().Be("Header, page {0}");
        options.FooterLabel.Should().Be("Footer, page {0}");
        options.ImageResizeHandleLabel.Should().Be("Resize image");
        options.ImageRetryLabel.Should().Be("Retry");
        options.ReviewDisplayMode.Should().Be(DocumentReviewDisplayMode.AllMarkup);
    }

    [Fact]
    public async Task Host_Lifecycle_SendsSuggestionsInSnapshot()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var document = CreateEmptyDocument();
        document.Blocks.Add(new DocumentBlock
        {
            Id = "block-1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "Original" }]
            }
        });
        var suggestions = new[]
        {
            new DocumentSuggestion
            {
                DocumentId = document.DocumentId,
                Type = DocumentSuggestionType.InsertText,
                Range = new DocumentRevisionRange { BlockId = "block-1" },
                SuggestedText = "Suggested text"
            }
        };

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, document)
                      .Add(p => p.Suggestions, suggestions));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var applySnapshotCall = JSInterop.Invocations
            .FirstOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument");
        applySnapshotCall.Should().NotBeNull();
        var snapshot = applySnapshotCall!.Arguments[1] as WysiwygDocumentSnapshot;
        snapshot.Should().NotBeNull();
        snapshot!.Suggestions.Should().ContainSingle(item =>
            item.Range.BlockId == "block-1" && item.SuggestedText == "Suggested text");
    }

    [Fact]
    public async Task Host_Lifecycle_CallsJsDispose()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.dispose", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.DisposeAsync();

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.dispose");
    }

    [Fact]
    public async Task Host_TextContextMenuRequested_ForwardsSelectionAndPosition()
    {
        WysiwygTextContextMenuRequest? captured = null;
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters => parameters
            .Add(p => p.TextContextMenuRequested, request => captured = request));
        var selection = new WysiwygSelectionSnapshot
        {
            AnchorBlockId = "block-1",
            AnchorInlineId = "inline-1",
            AnchorOffset = 1,
            FocusBlockId = "block-1",
            FocusInlineId = "inline-1",
            FocusOffset = 4,
            IsCollapsed = false
        };

        await cut.Instance.HandleTextContextMenuRequested(new WysiwygTextContextMenuRequest
        {
            ClientX = 280,
            ClientY = 180,
            Left = 240,
            Top = 160,
            Width = 240,
            Height = 268,
            Selection = selection
        });

        captured.Should().NotBeNull();
        captured!.Left.Should().Be(240);
        captured.Selection.Should().BeSameAs(selection);
    }

    [Fact]
    public async Task Host_TableContextMenuRequested_ForwardsSelectionAndPosition()
    {
        WysiwygTableContextMenuRequest? captured = null;
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters => parameters
            .Add(p => p.TableContextMenuRequested, request => captured = request));
        var selection = new WysiwygSelectionSnapshot
        {
            Region = "TableCell",
            AnchorBlockId = "cell-block-1",
            AnchorInlineId = "cell-inline-1",
            ActiveTableCellId = "cell-1",
            TableCellPath = "table-1/row-0/cell-1",
            IsCollapsed = true
        };

        await cut.Instance.HandleTableContextMenuRequested(new WysiwygTableContextMenuRequest
        {
            ClientX = 280,
            ClientY = 180,
            Left = 240,
            Top = 160,
            Width = 224,
            Height = 196,
            Selection = selection
        });

        captured.Should().NotBeNull();
        captured!.Left.Should().Be(240);
        captured.Selection.Should().BeSameAs(selection);
    }

    [Fact]
    public async Task Host_MiniToolbarChanged_ForwardsVisibleAndHiddenRequests()
    {
        var calls = new List<WysiwygMiniToolbarRequest?>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters => parameters
            .Add(p => p.MiniToolbarChanged, request => calls.Add(request)));

        await cut.Instance.HandleMiniToolbarChanged(new WysiwygMiniToolbarRequest
        {
            IsVisible = true,
            Left = 100,
            Top = 80,
            Width = 184,
            Height = 40,
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "block-1", IsCollapsed = false }
        });
        await cut.Instance.HandleMiniToolbarChanged(null);

        calls.Should().HaveCount(2);
        calls[0]!.IsVisible.Should().BeTrue();
        calls[0]!.Left.Should().Be(100);
        calls[1].Should().BeNull();
    }

    [Fact]
    public async Task Host_ApplyRemoteOperationBatch_CallsJsBatchPatcher()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.Setup<WysiwygRemoteOperationBatchApplyResult>("tmDocumentEditorRuntime.applyRemoteOperationBatch", _ => true)
            .SetResult(WysiwygRemoteOperationBatchApplyResult.Ok(applied: 1));
        var document = CreateEmptyDocument();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, document));
        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });
        var operations = new[]
        {
            new DocumentOperation
            {
                Type = DocumentOperationType.AddInlineMark,
                Target = new DocumentOperationTarget { BlockId = "b1", InlineId = "i1", Offset = 0, Length = 5 },
                Mark = new InlineMark { Type = InlineMarkType.Bold }
            }
        };

        var result = await cut.Instance.ApplyRemoteOperationBatchAsync(operations, document);

        result.Success.Should().BeTrue();
        result.Applied.Should().Be(1);
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.applyRemoteOperationBatch");
    }

    [Fact]
    public async Task Host_ApplyRemoteOperationBatch_ReturnsQueuedTransactionState()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.Setup<WysiwygRemoteOperationBatchApplyResult>("tmDocumentEditorRuntime.applyRemoteOperationBatch", _ => true)
            .SetResult(WysiwygRemoteOperationBatchApplyResult.Ok(queued: 2));
        var document = CreateEmptyDocument();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, document));
        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var result = await cut.Instance.ApplyRemoteOperationBatchAsync(
            [
                new DocumentOperation
                {
                    Type = DocumentOperationType.InsertText,
                    Target = new DocumentOperationTarget { BlockId = "b1", InlineId = "i1", Offset = 0 },
                    Text = "A"
                },
                new DocumentOperation
                {
                    Type = DocumentOperationType.InsertText,
                    Target = new DocumentOperationTarget { BlockId = "b1", InlineId = "i1", Offset = 1 },
                    Text = "B"
                }
            ],
            document);

        result.Success.Should().BeTrue();
        result.Applied.Should().Be(0);
        result.Queued.Should().Be(2);
    }

    [Fact]
    public async Task Host_ScrollToRevision_CallsJsRevisionNavigator()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.scrollToRevision", _ => true).SetVoidResult();
        var document = CreateEmptyDocument();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, document));
        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        await cut.Instance.ScrollToRevisionAsync("revision-1");

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.scrollToRevision"
            && invocation.Arguments.Count == 2
            && object.Equals(invocation.Arguments[1], "revision-1"));
    }

    [Fact]
    public async Task Host_InlineRevisionReview_RaisesReviewRequest()
    {
        WysiwygRevisionReviewRequest? received = null;
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters => parameters
            .Add(p => p.RevisionReviewRequested, request =>
            {
                received = request;
            }));

        await cut.Instance.HandleRevisionReviewRequested("revision-1", "Accepted");

        received.Should().NotBeNull();
        received!.RevisionId.Should().Be("revision-1");
        received.Action.Should().Be(DocumentRevisionAction.Accepted);
    }

    // ─── HandleClipboardPasteRequested ───────────────────────────────────────

    [Fact]
    public async Task Host_HandleClipboardPasteRequested_PlainHtml_ReturnsBlocksJson()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>();

        var json = await cut.Instance.HandleClipboardPasteRequested("<p>Hello world</p>", "Hello world");

        json.Should().NotBeNullOrEmpty();
        var blocks = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(json);
        blocks.Should().NotBeNull();
        blocks!.Length.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Host_HandleClipboardPasteRequested_EmptyHtmlWithPlainText_ReturnsBlocksJson()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>();

        var json = await cut.Instance.HandleClipboardPasteRequested("", "Line one\nLine two");

        var blocks = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(json);
        blocks.Should().NotBeNull();
        blocks!.Length.Should().Be(2);
    }

    [Fact]
    public async Task Host_HandleClipboardPasteRequested_GoogleSheetsHtml_ReturnsTableBlock()
    {
        const string html = "<google-sheets-html-origin><table><tr><td>A</td><td>B</td></tr></table></google-sheets-html-origin>";
        var cut = RenderComponent<TmDocumentWysiwygHost>();

        var json = await cut.Instance.HandleClipboardPasteRequested(html, "A\tB");

        var blocks = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(json);
        blocks.Should().NotBeNull();
        blocks!.Length.Should().Be(1);
        blocks[0].GetProperty("Type").GetInt32().Should().Be(4); // DocumentBlockType.Table = 4
    }

    [Fact]
    public async Task Host_HandleClipboardPasteRequested_UrlPlainText_ReturnsBlockWithLinkMark()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>();

        var json = await cut.Instance.HandleClipboardPasteRequested("", "https://example.com");

        var blocks = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(json);
        blocks.Should().NotBeNull();
        blocks!.Length.Should().Be(1);
        blocks[0].GetProperty("Type").GetInt32().Should().Be(0); // Paragraph
        var content = blocks[0].GetProperty("Content");
        var inlines = content.GetProperty("Inlines");
        inlines.GetArrayLength().Should().Be(1);
        var marks = inlines[0].GetProperty("Marks");
        marks.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Host_HandleClipboardPasteRequested_EmptyInput_ReturnsEmptyArray()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>();

        var json = await cut.Instance.HandleClipboardPasteRequested("", "");

        var blocks = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(json);
        blocks.Should().NotBeNull();
        blocks!.Length.Should().Be(0);
    }

    [Fact]
    public async Task Host_HandleClipboardPasteRequested_TableIntoTableCell_UsesSchemaFallback()
    {
        const string html = "<table><tr><td><p>Cell text</p></td></tr></table>";
        var cut = RenderComponent<TmDocumentWysiwygHost>();
        await cut.Instance.HandleSelectionChanged(new WysiwygSelectionSnapshot { Region = "TableCell", ActiveTableCellId = "cell-1" });

        var json = await cut.Instance.HandleClipboardPasteRequested(html, "Cell text");

        var blocks = JsonSerializer.Deserialize<JsonElement[]>(json);
        blocks.Should().NotBeNull();
        blocks!.Should().ContainSingle();
        blocks[0].GetProperty("Type").GetInt32().Should().Be(0);
        cut.Find("[data-testid='document-paste-report']").TextContent.Should().Contain("Paste adjusted");
    }

    [Fact]
    public async Task Host_HandleClipboardPasteRequested_UnsafeHtml_ShowsPasteReport()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>();

        var json = await cut.Instance.HandleClipboardPasteRequested("<p>Safe</p><script>alert(1)</script>", "Safe");

        var blocks = JsonSerializer.Deserialize<JsonElement[]>(json);
        blocks.Should().NotBeNull();
        blocks!.Should().ContainSingle();
        cut.Find("[data-testid='document-paste-report']").TextContent.Should().Contain("Paste adjusted");
        cut.Find("[data-testid='document-paste-report-toggle']").Click();
        cut.Find("[data-testid='document-paste-report-details']").TextContent.Should().Contain("stripped-element");
    }

    [Fact]
    public async Task Host_HandleClipboardPasteRequested_CapturesDeveloperDebugSnapshot()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>();

        await cut.Instance.HandleClipboardPasteRequested("<p>Phase 18</p><script>alert(1)</script>", "Phase 18");

        var snapshot = cut.Instance.GetClipboardDebugSnapshot();
        snapshot.RawHtml.Should().Contain("Phase 18");
        snapshot.PlainText.Should().Be("Phase 18");
        snapshot.NormalizedJson.Should().Contain("Phase 18");
        snapshot.Warnings.Should().Contain(warning => warning.Code == "stripped-element");
    }

    [Fact]
    public async Task Host_ApplyRemoteOperationBatch_RemembersSnapshotAndDoesNotApplySnapshotOnParameterUpdate()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.Setup<WysiwygRemoteOperationBatchApplyResult>("tmDocumentEditorRuntime.applyRemoteOperationBatch", _ => true)
            .SetResult(WysiwygRemoteOperationBatchApplyResult.Ok(applied: 1));
        var document = CreateEmptyDocument();
        document.Blocks.Add(CreateParagraphBlock("Original text"));
        var synchronized = Clone(document);
        ((ParagraphBlockContent)synchronized.Blocks[0].Content).Inlines.OfType<TextRun>().Single().Text = "Remote text";
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, document));
        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });
        var snapshotCallsBeforeRemote = JSInterop.Invocations.Count(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.loadDocument");

        var result = await cut.Instance.ApplyRemoteOperationBatchAsync(
            [
                new DocumentOperation
                {
                    Type = DocumentOperationType.UpdateBlock,
                    Target = new DocumentOperationTarget { BlockId = synchronized.Blocks[0].Id },
                    Block = synchronized.Blocks[0]
                }
            ],
            synchronized);
        cut.SetParametersAndRender(parameters => parameters.Add(p => p.Document, synchronized));

        result.Success.Should().BeTrue();
        JSInterop.Invocations.Count(invocation => invocation.Identifier == "tmDocumentEditorRuntime.loadDocument")
            .Should().Be(snapshotCallsBeforeRemote);
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.applyRemoteOperationBatch");
    }

    [Fact]
    public void Host_JsFailure_ShowsFallback()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true)
                 .SetException(new JSException("Engine not found"));

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        cut.Find(".tm-document-wysiwyg-host__fallback").Should().NotBeNull();
        cut.Find(".tm-document-wysiwyg-host__fallback").TextContent.Should()
           .Contain("WYSIWYG editing engine could not be loaded");
    }

    [Fact]
    public void Host_JsFailure_DoesNotThrow()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true)
                 .SetException(new JSException("Engine not found"));

        var act = () => RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        act.Should().NotThrow();
    }

    [Fact]
    public void Host_DefaultState_ShowsLoadingSkeleton()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>();

        // Before JS ready callback the loading skeleton should be visible.
        cut.FindAll(".tm-skeleton").Should().NotBeEmpty();
    }

    [Fact]
    public async Task Host_JsReadyCallback_HidesLoadingSkeleton()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>();

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        cut.FindAll(".tm-skeleton").Should().BeEmpty();
    }

    [Fact]
    public async Task Host_RequestSnapshotAsync_ReturnsDeserializedDocument()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var snapshotJson = @"{""ProtocolVersion"":1,""Document"":{""SchemaVersion"":1,""DocumentId"":""doc-1"",""Blocks"":[{""Id"":""b-1"",""Type"":0,""Order"":10,""Content"":{""$type"":""paragraph"",""Inlines"":[{""$type"":""text"",""Id"":""i-0"",""Text"":""Snapshot text""}]}}]}}";
        JSInterop.Setup<string>("tmDocumentEditorRuntime.getDocument", _ => true).SetResult(snapshotJson);

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var result = await cut.Instance.RequestSnapshotAsync();

        result.Should().NotBeNull();
        result!.DocumentId.Should().Be("doc-1");
        var firstBlock = result.Blocks.FirstOrDefault();
        firstBlock.Should().NotBeNull();
        var paragraph = firstBlock!.Content as ParagraphBlockContent;
        paragraph.Should().NotBeNull();
        var textRun = paragraph!.Inlines.FirstOrDefault() as TextRun;
        textRun.Should().NotBeNull();
        textRun!.Text.Should().Be("Snapshot text");
    }

    [Fact]
    public async Task Host_RequestRuntimeDocumentAsync_UsesRuntimeFacade()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var snapshotJson = @"{""ProtocolVersion"":1,""Document"":{""SchemaVersion"":1,""DocumentId"":""runtime-doc"",""Blocks"":[]}}";
        JSInterop.Setup<string>("tmDocumentEditorRuntime.getDocument", _ => true).SetResult(snapshotJson);

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var result = await cut.Instance.RequestRuntimeDocumentAsync();

        result.Should().NotBeNull();
        result!.DocumentId.Should().Be("runtime-doc");
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.getDocument");
    }

    [Fact]
    public async Task Host_RequestSnapshotAsync_ReturnsNullWhenJsNotReady()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        // Do NOT call HandleJsEngineReady — _jsReady is false.
        var result = await cut.Instance.RequestSnapshotAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task Host_RequestDebugSnapshotAsync_ReturnsJsDiagnostics()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.Setup<WysiwygDebugSnapshot>("tmDocumentEditorRuntime.getDebugSnapshot", _ => true)
            .SetResult(new WysiwygDebugSnapshot
            {
                InstanceId = "test-instance",
                HasInstance = true,
                ActiveBlockId = "block-1",
                ActiveInlineId = "inline-1",
                PendingTransactionId = "tx-1",
                ActiveDomPath = "div[data-testid=\"document-wysiwyg-host\"] > p[data-block-id=\"block-1\"]",
                CurrentSelection = new WysiwygSelectionSnapshot
                {
                    AnchorBlockId = "block-1",
                    AnchorInlineId = "inline-1",
                    AnchorOffset = 3,
                    IsCollapsed = true
                }
            });

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var result = await cut.Instance.RequestDebugSnapshotAsync();

        result.Should().NotBeNull();
        result!.HasInstance.Should().BeTrue();
        result.ActiveBlockId.Should().Be("block-1");
        result.ActiveInlineId.Should().Be("inline-1");
        result.PendingTransactionId.Should().Be("tx-1");
        result.ActiveDomPath.Should().Contain("document-wysiwyg-host");
        result.CurrentSelection.Should().NotBeNull();
        result.CurrentSelection!.AnchorOffset.Should().Be(3);
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.getDebugSnapshot");
    }

    [Fact]
    public async Task Host_RequestDebugSnapshotAsync_ReturnsNullWhenJsNotReady()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        var result = await cut.Instance.RequestDebugSnapshotAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task Host_RequestFormattingStateAsync_ReturnsJsSelectionFormattingState()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.Setup<WysiwygFormattingState>("tmDocumentEditorRuntime.getFormattingState", _ => true)
            .SetResult(new WysiwygFormattingState
            {
                Bold = WysiwygFormattingValue.Active,
                Italic = WysiwygFormattingValue.Mixed,
                Underline = WysiwygFormattingValue.Inactive
            });

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var result = await cut.Instance.RequestFormattingStateAsync();

        result.Should().NotBeNull();
        result!.Bold.Should().Be(WysiwygFormattingValue.Active);
        result.Italic.Should().Be(WysiwygFormattingValue.Mixed);
        result.Underline.Should().Be(WysiwygFormattingValue.Inactive);
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.getFormattingState");
    }

    [Fact]
    public async Task Host_RequestRuntimeSelectionStateAsync_UsesRuntimeFacade()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.Setup<WysiwygFormattingState>("tmDocumentEditorRuntime.getFormattingState", _ => true)
            .SetResult(new WysiwygFormattingState { Bold = WysiwygFormattingValue.Active });

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var result = await cut.Instance.RequestRuntimeSelectionStateAsync();

        result.Should().NotBeNull();
        result!.Bold.Should().Be(WysiwygFormattingValue.Active);
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.getFormattingState");
    }

    [Fact]
    public async Task Host_RequestRuntimeSelectionAsync_UsesRuntimeFacade()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.Setup<WysiwygSelectionSnapshot>("tmDocumentEditorRuntime.getRuntimeSelection", _ => true)
            .SetResult(new WysiwygSelectionSnapshot
            {
                AnchorNodeId = "inline-1",
                FocusNodeId = "inline-1",
                AnchorBlockId = "block-1",
                AnchorInlineId = "inline-1",
                AnchorOffset = 4,
                IsCollapsed = true
            });

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var result = await cut.Instance.RequestRuntimeSelectionAsync();

        result.Should().NotBeNull();
        result!.AnchorNodeId.Should().Be("inline-1");
        result.AnchorOffset.Should().Be(4);
        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.getRuntimeSelection");
    }

    [Fact]
    public async Task Host_RequestFormattingStateAsync_ReturnsNullWhenJsNotReady()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        var result = await cut.Instance.RequestFormattingStateAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task Host_CaptureTextSelectionAnchorAsync_ReturnsAnchorWhenTextSelected()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var anchor = new DocumentCommentAnchor
        {
            Type = DocumentCommentAnchorType.TextRange,
            BlockId = "b-1",
            StartInlineIndex = 0,
            StartOffset = 3,
            EndInlineIndex = 0,
            EndOffset = 10
        };
        JSInterop.Setup<DocumentCommentAnchor?>("tmDocumentEditorRuntime.captureCommentAnchor", _ => true).SetResult(anchor);

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var result = await cut.Instance.CaptureTextSelectionAnchorAsync();

        result.Should().NotBeNull();
        result!.Type.Should().Be(DocumentCommentAnchorType.TextRange);
        result.BlockId.Should().Be("b-1");
        result.StartOffset.Should().Be(3);
        result.EndOffset.Should().Be(10);
    }

    [Fact]
    public async Task Host_CaptureTextSelectionAnchorAsync_ReturnsNullWhenJsNotReady()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        var result = await cut.Instance.CaptureTextSelectionAnchorAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task Host_UpsertCommentAsync_ForwardsCommentToJsRuntime()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.upsertComment", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));
        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });
        var instanceId = JSInterop.Invocations
            .First(i => i.Identifier == "tmDocumentEditorRuntime.create")
            .Arguments[1]
            .Should()
            .BeOfType<WysiwygEditorOptions>()
            .Subject
            .InstanceId;

        var comment = new DocumentComment
        {
            Id = "comment-1",
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.TextRange,
                BlockId = "b-1",
                StartOffset = 2,
                EndOffset = 8
            }
        };

        await cut.Instance.UpsertCommentAsync(comment);

        var invocation = JSInterop.Invocations
            .LastOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.upsertComment");
        invocation.Should().NotBeNull();
        invocation!.Arguments[0].Should().Be(instanceId);
        invocation.Arguments[1].Should().BeSameAs(comment);
    }

    [Fact]
    public async Task Host_RemoveCommentAsync_ForwardsCommentIdToJsRuntime()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.removeComment", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));
        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });
        var instanceId = JSInterop.Invocations
            .First(i => i.Identifier == "tmDocumentEditorRuntime.create")
            .Arguments[1]
            .Should()
            .BeOfType<WysiwygEditorOptions>()
            .Subject
            .InstanceId;

        await cut.Instance.RemoveCommentAsync("comment-1");

        var invocation = JSInterop.Invocations
            .LastOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.removeComment");
        invocation.Should().NotBeNull();
        invocation!.Arguments[0].Should().Be(instanceId);
        invocation.Arguments[1].Should().Be("comment-1");
    }

    [Fact]
    public async Task Host_ScrollToCommentAsync_ForwardsCommentIdToJsRuntime()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.scrollToComment", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));
        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });
        var instanceId = JSInterop.Invocations
            .First(i => i.Identifier == "tmDocumentEditorRuntime.create")
            .Arguments[1]
            .Should()
            .BeOfType<WysiwygEditorOptions>()
            .Subject
            .InstanceId;

        await cut.Instance.ScrollToCommentAsync("comment-1");

        var invocation = JSInterop.Invocations
            .LastOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.scrollToComment");
        invocation.Should().NotBeNull();
        invocation!.Arguments[0].Should().Be(instanceId);
        invocation.Arguments[1].Should().Be("comment-1");
    }

    [Fact]
    public async Task Host_PatchGenerated_ForwardsToEventCallback()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        var patch = new WysiwygPatch { Type = "InsertText", Data = "hello" };
        await cut.Instance.HandlePatchGenerated(patch);

        patches.Should().ContainSingle();
        patches[0].Type.Should().Be("InsertText");
        patches[0].Data.Should().Be("hello");
    }

    [Fact]
    public async Task Host_PatchGenerated_ForwardsBeforeAndAfterSelection()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        var patch = new WysiwygPatch
        {
            Type = "InsertText",
            Data = "x",
            TransactionId = "tx-1",
            BeforeSelection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i1",
                AnchorOffset = 3,
                IsCollapsed = true
            },
            AfterSelection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i1",
                AnchorOffset = 4,
                IsCollapsed = true
            }
        };
        patch.Selection = patch.BeforeSelection;

        await cut.Instance.HandlePatchGenerated(patch);

        patches.Should().ContainSingle();
        patches[0].TransactionId.Should().Be("tx-1");
        patches[0].BeforeSelection!.AnchorOffset.Should().Be(3);
        patches[0].AfterSelection!.AnchorOffset.Should().Be(4);
    }

    [Fact]
    public async Task Host_SelectionChanged_ForwardsToEventCallback()
    {
        var selections = new List<WysiwygSelectionSnapshot?>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.DocumentSelectionChanged, EventCallback.Factory.Create<WysiwygSelectionSnapshot?>(this, s => selections.Add(s))));

        var snapshot = new WysiwygSelectionSnapshot { AnchorBlockId = "b1", IsCollapsed = true };
        await cut.Instance.HandleSelectionChanged(snapshot);

        selections.Should().ContainSingle();
        selections[0].Should().NotBeNull();
        selections[0]!.AnchorBlockId.Should().Be("b1");
    }

    [Fact]
    public async Task Host_SelectionChanged_ForwardsRangeSelection()
    {
        var selections = new List<WysiwygSelectionSnapshot?>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.DocumentSelectionChanged, EventCallback.Factory.Create<WysiwygSelectionSnapshot?>(this, s => selections.Add(s))));

        var snapshot = new WysiwygSelectionSnapshot
        {
            AnchorBlockId = "b1",
            AnchorInlineId = "i1",
            AnchorOffset = 2,
            FocusBlockId = "b1",
            FocusInlineId = "i1",
            FocusOffset = 5,
            IsCollapsed = false,
            Direction = "forward"
        };
        await cut.Instance.HandleSelectionChanged(snapshot);

        selections.Should().ContainSingle();
        selections[0]!.IsCollapsed.Should().BeFalse();
        selections[0]!.AnchorOffset.Should().Be(2);
        selections[0]!.FocusOffset.Should().Be(5);
    }

    [Fact]
    public async Task Host_SelectionChanged_ForwardsRegionMetadata()
    {
        var selections = new List<WysiwygSelectionSnapshot?>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.DocumentSelectionChanged, EventCallback.Factory.Create<WysiwygSelectionSnapshot?>(this, s => selections.Add(s))));

        var snapshot = new WysiwygSelectionSnapshot
        {
            Region = "Header",
            PageIndex = 1,
            HeaderFooterId = "header-primary",
            AnchorBlockId = "hb1",
            AnchorInlineId = "hi1",
            AnchorOffset = 4,
            AnchorBlockOffset = 12,
            FocusBlockId = "hb1",
            FocusInlineId = "hi1",
            FocusOffset = 4,
            FocusBlockOffset = 12,
            IsCollapsed = true,
            ActiveTableCellId = "cell-1",
            TableCellPath = "table-1/row-0/cell-1"
        };

        await cut.Instance.HandleSelectionChanged(snapshot);

        selections.Should().ContainSingle();
        selections[0]!.Region.Should().Be("Header");
        selections[0]!.PageIndex.Should().Be(1);
        selections[0]!.HeaderFooterId.Should().Be("header-primary");
        selections[0]!.AnchorBlockOffset.Should().Be(12);
        selections[0]!.FocusBlockOffset.Should().Be(12);
        selections[0]!.TableCellPath.Should().Be("table-1/row-0/cell-1");
    }

    [Fact]
    public async Task Host_SelectionChanged_ForwardsDirection()
    {
        var selections = new List<WysiwygSelectionSnapshot?>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.DocumentSelectionChanged, EventCallback.Factory.Create<WysiwygSelectionSnapshot?>(this, s => selections.Add(s))));

        var snapshot = new WysiwygSelectionSnapshot
        {
            AnchorBlockId = "b1",
            Direction = "backward",
            IsCollapsed = false
        };
        await cut.Instance.HandleSelectionChanged(snapshot);

        selections[0]!.Direction.Should().Be("backward");
    }

    [Fact]
    public async Task Host_RestoreSelection_CallsJsRestoreSelection()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.restoreSelection", _ => true).SetVoidResult();

        var doc = CreateEmptyDocument();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var snapshot = new WysiwygSelectionSnapshot
        {
            AnchorBlockId = "b1",
            AnchorInlineId = "i1",
            AnchorOffset = 3,
            FocusBlockId = "b1",
            FocusInlineId = "i1",
            FocusOffset = 3,
            IsCollapsed = true
        };
        await cut.Instance.RestoreSelectionAsync(snapshot);

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.restoreSelection");
    }

    [Fact]
    public async Task Host_UndoRequested_ForwardsToEventCallback()
    {
        var called = false;
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.UndoRequested, EventCallback.Factory.Create(this, () => called = true)));

        await cut.Instance.HandleUndoRequested();

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Host_RedoRequested_ForwardsToEventCallback()
    {
        var called = false;
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.RedoRequested, EventCallback.Factory.Create(this, () => called = true)));

        await cut.Instance.HandleRedoRequested();

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Host_SaveRequested_ForwardsToEventCallback()
    {
        var called = false;
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.SaveRequested, EventCallback.Factory.Create(this, () => called = true)));

        await cut.Instance.HandleSaveRequested();

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Host_RuntimeRecovered_ForwardsToEventCallback()
    {
        var called = false;
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.RuntimeRecovered, EventCallback.Factory.Create(this, () => called = true)));

        await cut.Instance.HandleRuntimeRecovered();

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Host_RuntimeRecoveryFailed_ForwardsToEventCallback()
    {
        var called = false;
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.RuntimeRecoveryFailed, EventCallback.Factory.Create(this, () => called = true)));

        await cut.Instance.HandleRuntimeRecoveryFailed();

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Host_RuntimeRecoveryDetail_ForwardsTypedTelemetry()
    {
        WysiwygRuntimeRecoveryDetail? captured = null;
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.RuntimeRecoveryDetailChanged, detail => captured = detail));

        await cut.Instance.HandleRuntimeRecovered(new WysiwygRuntimeRecoveryDetail
        {
            Event = "runtimeRecovered",
            Source = "command",
            Attempt = 1,
            BackoffMs = 100
        });

        captured.Should().NotBeNull();
        captured!.Source.Should().Be("command");
        captured.Attempt.Should().Be(1);
    }

    [Fact]
    public async Task Host_ExecuteEditorCommandAsync_CallsJsExecuteCommand()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.executeCommand", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        await cut.Instance.ExecuteEditorCommandAsync("toggleMark", new WysiwygMarkPayload { MarkType = "Bold" });

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.executeCommand");
    }

    [Fact]
    public async Task Host_ExecuteRuntimeCommandAsync_CallsRuntimeFacade()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.executeCommand", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        await cut.Instance.ExecuteRuntimeCommandAsync("toggleMark", new WysiwygMarkPayload { MarkType = "Bold" });

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.executeCommand");
    }

    [Fact]
    public async Task Host_HandleCommandToggleMark_GeneratesToggleMarkPatch()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        var snapshot = new WysiwygSelectionSnapshot
        {
            AnchorBlockId = "b1",
            AnchorInlineId = "i1",
            AnchorOffset = 0,
            FocusBlockId = "b1",
            FocusInlineId = "i1",
            FocusOffset = 5,
            IsCollapsed = false
        };

        await cut.Instance.HandleSelectionChanged(snapshot);
        await cut.Instance.HandleCommandToggleMark(new WysiwygMarkPayload { MarkType = "Bold" });

        patches.Should().ContainSingle();
        patches[0].Type.Should().Be("ToggleMark");
        patches[0].MarkType.Should().Be("Bold");
        patches[0].Selection.Should().NotBeNull();
        patches[0].Selection!.AnchorBlockId.Should().Be("b1");
    }

    [Fact]
    public async Task Host_HandleCommandToggleMark_UsesPayloadSelectionWhenProvided()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        var payloadSelection = new WysiwygSelectionSnapshot
        {
            AnchorBlockId = "b2",
            AnchorInlineId = "i2",
            AnchorOffset = 3,
            FocusBlockId = "b2",
            FocusInlineId = "i2",
            FocusOffset = 8,
            IsCollapsed = false
        };

        await cut.Instance.HandleCommandToggleMark(new WysiwygMarkPayload
        {
            MarkType = "Italic",
            Selection = payloadSelection
        });

        patches.Should().ContainSingle();
        patches[0].MarkType.Should().Be("Italic");
        patches[0].Selection!.AnchorBlockId.Should().Be("b2");
    }

    [Fact]
    public async Task Host_HandleCommandToggleMark_UsesLinkHrefAsPatchData()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        await cut.Instance.HandleCommandToggleMark(new WysiwygMarkPayload
        {
            MarkType = "Link",
            Href = "https://example.test",
            Title = "Example link",
            Data = "ignored",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i1",
                AnchorOffset = 0,
                FocusBlockId = "b1",
                FocusInlineId = "i1",
                FocusOffset = 5,
                IsCollapsed = false
            }
        });

        patches.Should().ContainSingle();
        patches[0].Type.Should().Be("ToggleMark");
        patches[0].MarkType.Should().Be("Link");
        patches[0].Data.Should().Be("https://example.test");
        patches[0].LinkTitle.Should().Be("Example link");
    }

    [Fact]
    public async Task Host_HandleCommandToggleMark_EmptyMarkType_DoesNothing()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        await cut.Instance.HandleCommandToggleMark(new WysiwygMarkPayload { MarkType = "" });

        patches.Should().BeEmpty();
    }

    [Fact]
    public async Task Host_ImageUrlDialog_GeneratesInsertImagePatch()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        await cut.Instance.HandleSelectionChanged(new WysiwygSelectionSnapshot { AnchorBlockId = "b1" });
        await cut.Instance.OpenImageDialogAsync();

        cut.Find("[data-testid='document-wysiwyg-image-url-input']").Input("https://example.test/image.png");
        cut.Find("[data-testid='document-wysiwyg-image-alt-input']").Input("Example image");
        cut.Find("[data-testid='document-wysiwyg-insert-image-url']").Click();

        patches.Should().ContainSingle();
        patches[0].Type.Should().Be("InsertBlock");
        patches[0].BlockType.Should().Be("Image");
        patches[0].Selection!.AnchorBlockId.Should().Be("b1");
        var image = patches[0].Block!.Content as ImageBlockContent;
        image.Should().NotBeNull();
        image!.Source.Should().Be(DocumentImageSource.Url);
        image.Url.Should().Be("https://example.test/image.png");
        image.AltText.Should().Be("Example image");
    }

    [Fact]
    public async Task Host_ImageDialog_RendersThroughFloatingPortalRoot()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.OpenImageDialogAsync();

        var portal = cut.Find("[data-testid='document-wysiwyg-floating-root']");
        portal.ClassList.Should().Contain("tm-document-editor__floating-root");
        portal.QuerySelector("[data-testid='document-wysiwyg-image-dialog']").Should().NotBeNull();
    }

    [Fact]
    public async Task Host_TokenPopover_RendersThroughFloatingPortalRoot()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.TokenProvider, new TestTokenProvider()));

        await cut.Instance.OpenTokenMenuAsync();

        var portal = cut.Find("[data-testid='document-wysiwyg-floating-root']");
        portal.ClassList.Should().Contain("tm-document-editor__floating-root");
        portal.QuerySelector("[data-testid='document-wysiwyg-token-popover']").Should().NotBeNull();
    }

    [Fact]
    public async Task Host_TokenPopover_EscapeClosesPopover()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.TokenProvider, new TestTokenProvider()));

        await cut.Instance.OpenTokenMenuAsync();

        await cut.Find("[data-testid='document-wysiwyg-token-popover']")
            .KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("[data-testid='document-wysiwyg-token-popover']").Should().BeEmpty();
    }

    [Fact]
    public async Task Host_TokenPopover_EnterInsertsHighlightedTokenAndClosesPopover()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.TokenProvider, new TestTokenProvider())
                      .Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        await cut.Instance.OpenTokenMenuAsync();

        await cut.Find("[data-testid='document-wysiwyg-token-popover']")
            .KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        patches.Should().ContainSingle();
        patches[0].Inline.Should().BeOfType<TokenRun>()
            .Which.Key.Should().Be("client.name");
        cut.FindAll("[data-testid='document-wysiwyg-token-popover']").Should().BeEmpty();
    }

    [Fact]
    public async Task Host_MentionAutocomplete_EnterInsertsMentionText()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.MentionProvider, new TestMentionProvider())
                      .Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        await cut.Instance.OpenMentionMenuAsync();

        cut.Find("[data-testid='document-wysiwyg-autocomplete-popover']").Should().NotBeNull();
        await cut.Find("[data-testid='document-wysiwyg-autocomplete-popover']")
            .KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        patches.Should().ContainSingle();
        patches[0].Type.Should().Be("InsertText");
        patches[0].Data.Should().Be("@alex");
    }

    [Fact]
    public async Task Host_SlashAutocomplete_ShowsCoreInsertCommands()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.OpenSlashCommandMenuAsync();

        var popover = cut.Find("[data-testid='document-wysiwyg-autocomplete-popover']");
        popover.TextContent.Should().Contain("Table");
        popover.TextContent.Should().Contain("Image");
        popover.TextContent.Should().Contain("Page break");
    }

    [Fact]
    public async Task Host_SlashAutocomplete_TableCommandExecutesRuntimeInsertTable()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });
        await cut.Instance.OpenSlashCommandMenuAsync();
        cut.Find("[data-testid='document-autocomplete-search']").Input("table");

        await cut.Find("[data-testid='document-wysiwyg-autocomplete-popover']")
            .KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.executeCommand"
            && invocation.Arguments.Any(argument => argument != null && argument.ToString() == "insertTable"));
    }

    [Fact]
    public async Task Host_ImageUrlDialog_RejectsUnsafeUrl()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        await cut.Instance.OpenImageDialogAsync();

        cut.Find("[data-testid='document-wysiwyg-image-url-input']").Input("javascript:alert(1)");
        cut.Find("[data-testid='document-wysiwyg-insert-image-url']").Click();

        patches.Should().BeEmpty();
        cut.Find(".tm-document-paste-error").TextContent.Should().Contain("not allowed");
    }

    [Fact]
    public async Task Host_ImageUploadRequested_UploadsImageViaProviderAndReturnsAssetBlock()
    {
        var provider = new CapturingImageProvider();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.ImageProvider, provider));

        var block = await cut.Instance.HandleImageUploadRequested(new WysiwygImagePayload
        {
            Source = DocumentImageSource.Clipboard,
            FileName = "paste.png",
            ContentType = "image/png",
            SizeBytes = 1,
            Base64Data = "AA==",
            AltText = "Pasted"
        });

        provider.UploadRequests.Should().ContainSingle();
        provider.UploadedBytes.Should().Equal(new byte[] { 0 });
        block.Should().NotBeNull();
        block!.Type.Should().Be(DocumentBlockType.Image);
        var image = block.Content as ImageBlockContent;
        image.Should().NotBeNull();
        image!.Source.Should().Be(DocumentImageSource.Asset);
        image.AssetId.Should().Be("asset-1");
        image.Url.Should().Be("https://cdn.example.test/asset-1.png");
        image.AltText.Should().Be("Pasted");
    }

    [Fact]
    public async Task Host_ImageUploadRequested_ClipboardImageWithoutProviderShowsPasteReportWarning()
    {
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        var block = await cut.Instance.HandleImageUploadRequested(new WysiwygImagePayload
        {
            Source = DocumentImageSource.Clipboard,
            FileName = "paste.png",
            ContentType = "image/png",
            SizeBytes = 1,
            Base64Data = "AA==",
            AltText = "Pasted"
        });

        block.Should().BeNull();
        cut.Find("[data-testid='document-paste-report']").TextContent.Should().Contain("Paste adjusted");
        cut.Find("[data-testid='document-paste-report-toggle']").Click();
        cut.Find("[data-testid='document-paste-report-details']").TextContent.Should().Contain("image-provider-missing");
    }

    [Fact]
    public async Task Host_DocumentWithProviderAssetImage_ResolvesDisplayUrlBeforeSnapshot()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var provider = new CapturingImageProvider();
        var doc = CreateEmptyDocument();
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "asset-image-1",
            Type = DocumentBlockType.Image,
            Order = 10,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = "asset-1",
                AltText = "Provider image"
            }
        });

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc)
                      .Add(p => p.ImageProvider, provider));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        provider.ResolveRequests.Should().ContainSingle(request => request.AssetId == "asset-1");
        var applySnapshotCall = JSInterop.Invocations
            .FirstOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument");

        applySnapshotCall.Should().NotBeNull();
        var snapshot = applySnapshotCall!.Arguments[1] as WysiwygDocumentSnapshot;
        snapshot.Should().NotBeNull();
        var image = snapshot!.Document.Blocks[0].Content as ImageBlockContent;
        image.Should().NotBeNull();
        image!.Url.Should().Be("https://cdn.example.test/asset-1.png");
        ((ImageBlockContent)doc.Blocks[0].Content).Url.Should().BeNull();
    }

    [Fact]
    public async Task Host_ProviderImageButton_UploadsAndSendsImageInsertCommandToJs()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.executeCommand", _ => true).SetVoidResult();

        var provider = new CapturingImageProvider();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.ImageProvider, provider));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });
        await cut.Instance.OpenImageDialogAsync();

        cut.Find("[data-testid='document-wysiwyg-upload-demo-image']").Click();

        provider.UploadRequests.Should().ContainSingle();
        JSInterop.Invocations.Any(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.executeCommand"
            && invocation.Arguments.Any(argument => argument is not null && string.Equals(argument.ToString(), "insertImageBlock", StringComparison.Ordinal)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Host_ImageSelection_DoesNotRenderLargeImageInspectorOverlay()
    {
        var doc = CreateEmptyDocument();
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "img-1",
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Url = "https://example.test/image.png",
                AltText = "Diagram"
            }
        });

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleSelectionChanged(new WysiwygSelectionSnapshot
        {
            AnchorBlockId = "img-1",
            FocusBlockId = "img-1",
            ActiveImageBlockId = "img-1",
            Region = "Image",
            IsCollapsed = true
        });

        cut.FindAll("[data-testid='document-image-inspector']").Should().BeEmpty();
    }

    [Fact]
    public void Host_AcceptsPermissionsParameter()
    {
        var permissions = new DocumentEditorPermissions { CanEdit = false, CanComment = true };
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Permissions, permissions)
                      .Add(p => p.Document, CreateEmptyDocument()));

        cut.Instance.Permissions.CanEdit.Should().BeFalse();
        cut.Instance.Permissions.CanComment.Should().BeTrue();
    }

    [Fact]
    public async Task Host_DocumentSnapshotChanged_ForwardsToEventCallback()
    {
        var snapshots = new List<DocumentEditorDocument>();
        var doc = CreateEmptyDocument();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc)
                      .Add(p => p.DocumentSnapshotChanged, EventCallback.Factory.Create<DocumentEditorDocument>(this, d => snapshots.Add(d))));

        var newDoc = DocumentEditorDocument.Empty("snap-1");
        await cut.Instance.DocumentSnapshotChanged.InvokeAsync(newDoc);

        snapshots.Should().ContainSingle();
        snapshots[0].DocumentId.Should().Be("snap-1");
    }

    [Fact]
    public async Task Host_DocumentPatchGenerated_ForwardsToEventCallback()
    {
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        var patch = new WysiwygPatch { Type = "InsertText", Data = "hello" };
        await cut.Instance.HandlePatchGenerated(patch);

        patches.Should().ContainSingle();
        patches[0].Type.Should().Be("InsertText");
    }

    [Fact]
    public async Task Host_JsReady_SendsSnapshotToJs()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var doc = CreateEmptyDocument();
        doc.Metadata.Title = "Ready doc";
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentEditorRuntime.loadDocument");
    }

    [Fact]
    public async Task Host_DocumentParameterChanged_SendsSnapshotToJs()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var doc = CreateEmptyDocument();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        // Changing the Document parameter should trigger a new snapshot send.
        var newDoc = DocumentEditorDocument.Empty("doc-changed");
        cut.SetParametersAndRender(parameters =>
            parameters.Add(p => p.Document, newDoc));

        var applySnapshotCalls = JSInterop.Invocations
            .Where(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument")
            .ToList();

        applySnapshotCalls.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Host_SameDocumentReferenceChanged_DoesNotRoundtripSnapshot()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var doc = CreateEmptyDocument();
        var paragraph = CreateParagraphBlock("Hello");
        doc.Blocks.Add(paragraph);
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        ((ParagraphBlockContent)paragraph.Content).Inlines[0] = new TextRun { Id = "typed", Text = "Hello!" };
        cut.SetParametersAndRender(parameters =>
            parameters.Add(p => p.Document, doc));

        var applySnapshotCalls = JSInterop.Invocations
            .Where(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument")
            .ToList();

        applySnapshotCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Host_RefreshSnapshotAsync_ForcesSameReferenceSnapshot()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var doc = CreateEmptyDocument();
        var paragraph = CreateParagraphBlock("Hello");
        doc.Blocks.Add(paragraph);
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        ((ParagraphBlockContent)paragraph.Content).Inlines[0] = new TextRun { Id = "undo", Text = "Undo text" };
        await cut.Instance.RefreshSnapshotAsync();

        var applySnapshotCalls = JSInterop.Invocations
            .Where(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument")
            .ToList();

        applySnapshotCalls.Should().HaveCount(2);
    }

    [Fact]
    public void JsFile_ExistsInWebRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "..", "..", "..", "..", "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
            if (File.Exists(candidate))
            {
                File.ReadAllText(candidate).Should().Contain("window.tmDocumentWysiwyg");
                File.ReadAllText(candidate).Should().Contain("window.tmDocumentEditorRuntime");
                return;
            }
            current = current.Parent;
        }

        // Fallback: check absolute path from repo root.
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        File.Exists(jsPath).Should().BeTrue($"JS file not found at {jsPath}");
        File.ReadAllText(jsPath).Should().Contain("window.tmDocumentWysiwyg");
        File.ReadAllText(jsPath).Should().Contain("window.tmDocumentEditorRuntime");
    }

    [Fact]
    public void JsFile_ContainsRuntimeFacadeFunctions()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(jsPath);

        jsText.Should().Contain("window.tmDocumentEditorRuntime");
        jsText.Should().Contain("function loadDocument");
        jsText.Should().Contain("function getDocument");
        jsText.Should().Contain("function executeCommand");
        jsText.Should().Contain("function onTransactionCommitted");
        jsText.Should().Contain("function onSelectionStateChanged");
    }

    [Fact]
    public void JsFile_ContainsPaginationFunctions()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(jsPath);
        jsText.Should().Contain("_createPageElement");
        jsText.Should().Contain("_checkPageOverflow");
        jsText.Should().Contain("_applyDocumentTheme");
        jsText.Should().Contain("_applyPageMetrics");
        jsText.Should().Contain("tm-wysiwyg-page");
    }

    [Fact]
    public void JsFile_ContainsRemoteInlineMarkOperationPatcher()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(jsPath);
        jsText.Should().Contain("applyRemoteOperation");
        jsText.Should().Contain("_applyRemoteInlineMark");
        jsText.Should().Contain("_wrapInlineRangeWithRemoteMark");
        jsText.Should().Contain("_removeRemoteInlineMark");
        jsText.Should().Contain("applyLink");
    }

    [Fact]
    public void JsFile_ContainsRemoteOperationBatchPatcher()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(jsPath);
        jsText.Should().Contain("window.tmDocumentEditorWysiwyg");
        jsText.Should().Contain("applyRemoteOperationBatch");
        jsText.Should().Contain("appliedOperationIds");
        jsText.Should().Contain("_sortRemoteBatchOperations");
        jsText.Should().Contain("_getRemoteOperationStableSortKey");
        jsText.Should().Contain("_applyRemoteInsertText");
        jsText.Should().Contain("_applyRemoteDeleteText");
        jsText.Should().Contain("_restoreRemoteSelectionAfterTextChange");
        jsText.Should().Contain("queued: operations.length");
        jsText.Should().Contain("_scheduleRemoteQueueFlush(inst, 25)");
        jsText.Should().Contain("QueuedRemoteBatchCount");
        jsText.Should().Contain("_updateImageBlockInPlace");
        jsText.Should().Contain("_removeRemoteInlineMarkRange");
        jsText.Should().Contain("_transformRemoteOperationsAgainstPendingTransactions");
        jsText.Should().Contain("pendingCollaborationTransactions");
        jsText.Should().Contain("applyRemoteCursor");
        jsText.Should().Contain("tm-wysiwyg-remote-cursor");
    }

    [Fact]
    public void JsFile_ContainsDebugSnapshotApi()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(jsPath);
        jsText.Should().Contain("getDebugSnapshot");
        jsText.Should().Contain("ActiveDomPath");
        jsText.Should().Contain("PendingTransactionId");
        jsText.Should().Contain("LastInputType");
        jsText.Should().Contain("CurrentSelection");
    }

    [Fact]
    public void JsFile_ContainsPhase1SelectionRegionAndTransactionGuards()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(jsPath);
        jsText.Should().Contain("_resolveSelectionRegion");
        jsText.Should().Contain("tableCellPath");
        jsText.Should().Contain("anchorBlockOffset");
        jsText.Should().Contain("focusBlockOffset");
        jsText.Should().Contain("beforeSelection");
        jsText.Should().Contain("afterSelection");
        jsText.Should().Contain("hasPendingLocalSnapshot");
        jsText.Should().Contain("_queueRemoteOperationBatch");
        jsText.Should().Contain("_flushQueuedRemoteOperationBatches");
    }

    [Fact]
    public void JsFile_ContainsWordLikeEnterAndSoftBreakPipeline()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(jsPath);
        jsText.Should().Contain("_handleStructuralBeforeInput");
        jsText.Should().Contain("_handleJsOwnedTextBeforeInput");
        jsText.Should().Contain("_applyParagraphBreakToDom");
        jsText.Should().Contain("_applySoftBreakToDom");
        jsText.Should().Contain("_mergeCurrentBlockWithPrevious");
        jsText.Should().Contain("SplitBlock");
        jsText.Should().Contain("InsertSoftBreak");
        jsText.Should().Contain("JsOwnedInputCount");
        jsText.Should().Contain("compositionupdate");
        jsText.Should().Contain("data-inline-break");
        jsText.Should().Contain("_serializeInlineText");
        jsText.Should().Contain("_positionAfterInlineBreak");
    }

    [Fact]
    public void JsFile_ContainsSelectionAwareFormattingPipeline()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(jsPath);
        jsText.Should().Contain("pendingTypingMarks");
        jsText.Should().Contain("_executeToggleMarkCommand");
        jsText.Should().Contain("_captureSelectionSnapshot(inst)");
        jsText.Should().Contain("_splitInlineForMark");
        jsText.Should().Contain("getFormattingState");
        jsText.Should().Contain("_getSelectionMarkState");
    }

    [Fact]
    public void CssFile_ContainsPaginationStyles()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var cssPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css");
        var cssText = File.ReadAllText(cssPath);
        cssText.Should().Contain(".tm-wysiwyg-page");
        cssText.Should().Contain(".tm-wysiwyg-page--overflow");
        cssText.Should().Contain(".tm-wysiwyg-page__overflow-warning");
    }

    // ── Phase 12: Headers/Footers ─────────────────────────────────────────────

    [Fact]
    public void JsFile_ContainsHeaderFooterFunctions()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var jsText = File.ReadAllText(jsPath);
        jsText.Should().Contain("_renderHeaderFooterForPage");
        jsText.Should().Contain("_resolveHeaderFooter");
        jsText.Should().Contain("_renderHeaderFooterRegion");
        jsText.Should().Contain("_serializeHeaderFooterRegions");
        jsText.Should().Contain("_activatePageRegion");
        jsText.Should().Contain("closeHeaderFooter");
        jsText.Should().Contain("_renderNoteRegionsForPage");
        jsText.Should().Contain("tm-wysiwyg-page__header");
        jsText.Should().Contain("tm-wysiwyg-page__footer");
        jsText.Should().Contain("tm-wysiwyg-page__notes");
    }

    [Fact]
    public void CssFile_ContainsHeaderFooterStyles()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        repoRoot.Should().NotBeNull("Could not locate repository root");
        var cssPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css");
        var cssText = File.ReadAllText(cssPath);
        cssText.Should().Contain(".tm-wysiwyg-page__header");
        cssText.Should().Contain(".tm-wysiwyg-page__footer");
        cssText.Should().Contain(".tm-wysiwyg-page__body");
    }

    [Fact]
    public async Task Host_DocumentWithHeader_SendsHeaderBlocksToJs()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var doc = CreateDocumentWithHeaderFooter();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var applySnapshotCall = JSInterop.Invocations
            .FirstOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument");

        applySnapshotCall.Should().NotBeNull();
        var snapshot = applySnapshotCall!.Arguments[1] as WysiwygDocumentSnapshot;
        snapshot.Should().NotBeNull();
        snapshot!.Document.HeadersFooters.Should().HaveCount(2);
        snapshot.Document.HeadersFooters[0].Type.Should().Be(DocumentHeaderFooterType.Header);
        snapshot.Document.HeadersFooters[0].Blocks.Should().HaveCount(1);
        snapshot.Document.HeadersFooters[1].Type.Should().Be(DocumentHeaderFooterType.Footer);
    }

    [Fact]
    public async Task Host_DocumentWithDifferentFirstPage_SendsSectionPropertiesToJs()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var doc = CreateDocumentWithHeaderFooter(differentFirstPage: true);

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var applySnapshotCall = JSInterop.Invocations
            .FirstOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument");

        applySnapshotCall.Should().NotBeNull();
        var snapshot = applySnapshotCall!.Arguments[1] as WysiwygDocumentSnapshot;
        snapshot.Should().NotBeNull();
        snapshot!.Document.Sections.Should().HaveCountGreaterThanOrEqualTo(1);
        snapshot.Document.Sections[0].Properties.DifferentFirstPage.Should().BeTrue();
    }

    private static DocumentEditorDocument CreateDocumentWithHeaderFooter(bool differentFirstPage = false)
    {
        var doc = DocumentEditorDocument.Empty("wysiwyg-hf-test");
        var sectionId = doc.Sections[0].Id;

        var headerId = Guid.NewGuid().ToString("N");
        var footerId = Guid.NewGuid().ToString("N");

        doc.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = headerId,
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.Primary,
            SectionId = sectionId,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "h-1",
                    Type = DocumentBlockType.Paragraph,
                    Order = 10,
                    Content = new ParagraphBlockContent
                    {
                        Inlines = [new TextRun { Id = "hi-1", Text = "Header text" }]
                    }
                }
            ]
        });

        doc.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = footerId,
            Type = DocumentHeaderFooterType.Footer,
            Scope = DocumentHeaderFooterScope.Primary,
            SectionId = sectionId,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "f-1",
                    Type = DocumentBlockType.Paragraph,
                    Order = 10,
                    Content = new ParagraphBlockContent
                    {
                        Inlines = [new TextRun { Id = "fi-1", Text = "Footer text" }]
                    }
                }
            ]
        });

        doc.Sections[0].Properties = new DocumentSectionProperties
        {
            DifferentFirstPage = differentFirstPage,
            HeaderFooterReferences =
            [
                new DocumentHeaderFooterReference
                {
                    HeaderFooterId = headerId,
                    Type = DocumentHeaderFooterType.Header,
                    Scope = DocumentHeaderFooterScope.Primary
                },
                new DocumentHeaderFooterReference
                {
                    HeaderFooterId = footerId,
                    Type = DocumentHeaderFooterType.Footer,
                    Scope = DocumentHeaderFooterScope.Primary
                }
            ]
        };

        return doc;
    }

    // ── Phase 11: Pagination MVP ──────────────────────────────────────────────

    [Fact]
    public async Task Host_DocumentWithPageSettings_SendsPageSettingsToJs()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var doc = CreateEmptyDocument();
        doc.PageSettings = new DocumentPageSettings
        {
            Size = DocumentPageSize.A4,
            Margins = new DocumentPageMargins { Top = 72, Right = 72, Bottom = 72, Left = 72 },
            Landscape = false
        };

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var applySnapshotCall = JSInterop.Invocations
            .FirstOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument");

        applySnapshotCall.Should().NotBeNull();
        var snapshot = applySnapshotCall!.Arguments[1] as WysiwygDocumentSnapshot;
        snapshot.Should().NotBeNull();
        snapshot!.Document.PageSettings.Should().NotBeNull();
        snapshot.Document.PageSettings.Size.Name.Should().Be("A4");
        snapshot.Document.PageSettings.Margins.Top.Should().Be(72);
        snapshot.Document.PageSettings.Landscape.Should().BeFalse();
    }

    [Fact]
    public async Task Host_DocumentWithPageBreak_SendsPageBreakBlockToJs()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var doc = CreateEmptyDocument();
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "b-1",
            Type = DocumentBlockType.Paragraph,
            Order = 10,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Id = "i-1", Text = "Page 1" }]
            }
        });
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "b-2",
            Type = DocumentBlockType.PageBreak,
            Order = 20,
            Content = new PageBreakBlockContent()
        });
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "b-3",
            Type = DocumentBlockType.Paragraph,
            Order = 30,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Id = "i-2", Text = "Page 2" }]
            }
        });

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var applySnapshotCall = JSInterop.Invocations
            .FirstOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument");

        applySnapshotCall.Should().NotBeNull();
        var snapshot = applySnapshotCall!.Arguments[1] as WysiwygDocumentSnapshot;
        snapshot.Should().NotBeNull();
        snapshot!.Document.Blocks.Should().HaveCount(3);
        snapshot.Document.Blocks[0].Type.Should().Be(DocumentBlockType.Paragraph);
        snapshot.Document.Blocks[1].Type.Should().Be(DocumentBlockType.PageBreak);
        snapshot.Document.Blocks[2].Type.Should().Be(DocumentBlockType.Paragraph);
    }

    // ── Phase 13: Tables ────────────────────────────────────────────────────

    private static DocumentBlock CreateParagraphBlock(string text)
    {
        return new DocumentBlock
        {
            Id = $"b-{Guid.NewGuid():N}",
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Id = $"i-{Guid.NewGuid():N}", Text = text }]
            }
        };
    }

    [Fact]
    public async Task Host_DocumentWithTable_SendsTableBlockToJs()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var doc = CreateEmptyDocument();
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "b-table-1",
            Type = DocumentBlockType.Table,
            Order = 10,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "c-1-1",
                                ColumnSpan = 1,
                                RowSpan = 1,
                                Blocks = [CreateParagraphBlock("A1")]
                            },
                            new TableCellContent
                            {
                                Id = "c-1-2",
                                ColumnSpan = 1,
                                RowSpan = 1,
                                Blocks = [CreateParagraphBlock("B1")]
                            }
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "c-2-1",
                                ColumnSpan = 1,
                                RowSpan = 1,
                                Blocks = [CreateParagraphBlock("A2")]
                            },
                            new TableCellContent
                            {
                                Id = "c-2-2",
                                ColumnSpan = 1,
                                RowSpan = 1,
                                Blocks = [CreateParagraphBlock("B2")]
                            }
                        ]
                    }
                ]
            }
        });

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var applySnapshotCall = JSInterop.Invocations
            .FirstOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument");

        applySnapshotCall.Should().NotBeNull();
        var snapshot = applySnapshotCall!.Arguments[1] as WysiwygDocumentSnapshot;
        snapshot.Should().NotBeNull();
        snapshot!.Document.Blocks.Should().ContainSingle();
        snapshot.Document.Blocks[0].Type.Should().Be(DocumentBlockType.Table);
        var table = snapshot.Document.Blocks[0].Content as TableBlockContent;
        table.Should().NotBeNull();
        table!.Rows.Should().HaveCount(2);
        table.Rows[0].Cells.Should().HaveCount(2);
    }

    [Fact]
    public async Task Host_DocumentWithTable_MergedCell_SendsColSpanAndRowSpan()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();

        var doc = CreateEmptyDocument();
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "b-table-2",
            Type = DocumentBlockType.Table,
            Order = 10,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "c-1-1",
                                ColumnSpan = 2,
                                RowSpan = 1,
                                Blocks = [CreateParagraphBlock("Merged A1-B1")]
                            }
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "c-2-1",
                                ColumnSpan = 1,
                                RowSpan = 2,
                                Blocks = [CreateParagraphBlock("Merged A2-A3")]
                            },
                            new TableCellContent
                            {
                                Id = "c-2-2",
                                ColumnSpan = 1,
                                RowSpan = 1,
                                Blocks = [CreateParagraphBlock("B2")]
                            }
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "c-3-2",
                                ColumnSpan = 1,
                                RowSpan = 1,
                                Blocks = [CreateParagraphBlock("B3")]
                            }
                        ]
                    }
                ]
            }
        });

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var applySnapshotCall = JSInterop.Invocations
            .FirstOrDefault(i => i.Identifier == "tmDocumentEditorRuntime.loadDocument");

        var snapshot = applySnapshotCall!.Arguments[1] as WysiwygDocumentSnapshot;
        var table = snapshot!.Document.Blocks[0].Content as TableBlockContent;
        table!.Rows[0].Cells[0].ColumnSpan.Should().Be(2);
        table.Rows[0].Cells[0].RowSpan.Should().Be(1);
        table.Rows[1].Cells[0].ColumnSpan.Should().Be(1);
        table.Rows[1].Cells[0].RowSpan.Should().Be(2);
    }

    [Fact]
    public void JsFile_ContainsTableFunctions()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        File.Exists(jsPath).Should().BeTrue($"JS file not found at {jsPath}");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("_renderTable");
        js.Should().Contain("_serializeTable");
        js.Should().Contain("HandleTableContextMenuRequested");
        js.Should().Contain("_beginTableTransaction");
        js.Should().Contain("_commitTableTransaction");
        js.Should().Contain("insertTableRowBefore");
        js.Should().Contain("insertTableRow");
        js.Should().Contain("insertTableRowAfter");
        js.Should().Contain("insertTableColumnBefore");
        js.Should().Contain("insertTableColumn");
        js.Should().Contain("insertTableColumnAfter");
        js.Should().Contain("deleteTableRow");
        js.Should().Contain("deleteTableColumn");
        js.Should().Contain("mergeTableCells");
        js.Should().Contain("splitTableCell");
        js.Should().Contain("_applyTableCellStyle");
        js.Should().Contain("_serializeTableCellBorders");
        js.Should().Contain("setTableProperties");
        js.Should().Contain("setCellProperties");
        js.Should().Contain("resizeTableColumn");
        js.Should().Contain("_renderTableHandles");
    }

    [Fact]
    public void JsFile_ContainsTableCellNavigation()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("_findNextTableCell");
        js.Should().Contain("_findPreviousTableCell");
        js.Should().Contain("Tab navigation between table cells");
    }

    [Fact]
    public void JsFile_ContainsTableActiveCellTracking()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("activeTableCellId");
        js.Should().Contain("data-cell-id");
    }

    [Fact]
    public void JsFile_ContainsInlineImageCommandsAndClipboardUpload()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("insertImageUrl");
        js.Should().Contain("insertImageNode");
        js.Should().Contain("_getClipboardImageFile");
        js.Should().Contain("HandleImageUploadRequested");
        js.Should().Contain("_isSafeImageUrl");
        js.Should().Contain("replaceImage");
        js.Should().Contain("setImageSize");
        js.Should().Contain("_insertImageUploadPlaceholder");
        js.Should().Contain("document-wysiwyg-image-upload-placeholder");
    }

    [Fact]
    public void JsFile_PreservesTokenMetadata()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("data-token-key");
        js.Should().Contain("data-token-type");
        js.Should().Contain("data-inline-atomic");
        js.Should().Contain("_closestAtomicInlineElement");
        js.Should().Contain("contenteditable', 'false'");
    }

    [Fact]
    public void JsFile_SerializesLinkTitleAndRejectsUnsafeHref()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("getLinkInfo");
        js.Should().Contain("_sanitizeLinkHref");
        js.Should().Contain("data-link-title");
        js.Should().Contain("linkTitle");
    }

    [Fact]
    public void JsFile_BatchesTypingInterop()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("_queueInsertTextPatch");
        js.Should().Contain("_flushPendingInputPatch");
        js.Should().Contain("_scheduleSelectionNotification");
        js.Should().Contain("_canMergeInsertTextPatches");
    }

    [Fact]
    public void JsFile_ContainsFloatingImageLayoutDragAndResize()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("_applyFloatingImageLayout");
        js.Should().Contain("_onFloatingImagePointerDown");
        js.Should().Contain("_dispatchImageUpdatePatch");
        js.Should().Contain("setImageWrapMode");
        js.Should().Contain("tm-wysiwyg-image--wrap-");
        js.Should().Contain("tm-wysiwyg-image__resize-handle");
        js.Should().Contain("LockAnchor");
    }

    [Fact]
    public void JsFile_ContainsImageObjectSelectionContextMenuAndDropUpload()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("_selectImageFigure");
        js.Should().Contain("ActiveImageBlockId");
        js.Should().Contain("_moveCaretFromImageSelection");
        js.Should().Contain("_showImageContextMenu");
        js.Should().Contain("document-wysiwyg-image-context-menu");
        js.Should().Contain("document-wysiwyg-image-delete");
        js.Should().Contain("_onRootDrop");
        js.Should().Contain("tm-document-wysiwyg-host--image-drop-target");
        js.Should().Contain("_attachImageLoadState");
        js.Should().Contain("_recordImageNaturalSize");
        js.Should().Contain("document-wysiwyg-image-retry");
        js.Should().Contain("_beginInlineImageMoveDrag");
        js.Should().Contain("_dispatchImageMovePatch");
        js.Should().Contain("document-wysiwyg-image-insertion-caret");
        js.Should().Contain("_applyRemoteMoveBlock");
    }

    [Fact]
    public void JsFile_ContainsTextContextMenuAndMiniToolbarBridge()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("_placeFloatingElement");
        js.Should().Contain("_scheduleMiniToolbar");
        js.Should().Contain("_hideMiniToolbar");
        js.Should().Contain("HandleTextContextMenuRequested");
        js.Should().Contain("HandleMiniToolbarChanged");
    }

    [Fact]
    public void JsFile_ContainsWordExcelClipboardPasteAndCopyPipeline()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("_parsePlainTextPaste");
        js.Should().Contain("_parseClipboardHtml");
        js.Should().Contain("_readClipboardTable");
        js.Should().Contain("_insertClipboardBlocks");
        js.Should().Contain("_serializeSelectionForClipboard");
        js.Should().Contain("_writeClipboardPayload");
        js.Should().Contain("text/html");
        js.Should().Contain("text/plain");
        js.Should().Contain("copySelection");
    }

    [Fact]
    public void JsFile_ContainsRevisionDisplayModeAndNavigationApi()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("setReviewDisplayMode");
        js.Should().Contain("_applyReviewDisplayMode");
        js.Should().Contain("scrollToRevision");
        js.Should().Contain("HandleRevisionReviewRequested");
        js.Should().Contain("document-inline-revision-review");
        js.Should().Contain("tm-wysiwyg-revision--selected");
    }

    [Fact]
    public void CssFile_ContainsTableStyles()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var cssPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css");
        File.Exists(cssPath).Should().BeTrue($"CSS file not found at {cssPath}");
        var css = File.ReadAllText(cssPath);
        css.Should().Contain(".tm-wysiwyg-table");
        css.Should().Contain(".tm-wysiwyg-table td");
        css.Should().Contain(".tm-wysiwyg-table td:focus");
    }

    [Fact]
    public void CssFile_ContainsFloatingImageWrapStyles()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var cssPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css");
        var css = File.ReadAllText(cssPath);
        css.Should().NotContain(".tm-wysiwyg-image-sidecar-text");
        css.Should().NotContain("data-wrap-sidecar-for");
        css.Should().Contain(".tm-wysiwyg-image--wrap-top-bottom");
        css.Should().Contain(".tm-wysiwyg-image--wrap-behind-text");
        css.Should().Contain(".tm-wysiwyg-image--wrap-in-front-of-text");
        css.Should().Contain(".tm-wysiwyg-image__resize-handle");
        css.Should().Contain(".tm-wysiwyg-image__retry");
        css.Should().Contain(".tm-wysiwyg-image-insertion-caret");
    }

    // ── Phase 16.1 – New JSInterop calls ────────────────────────────────────

    [Fact]
    public void JsFile_ContainsShowBlocksAndProtectionAndBodyHtml()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var jsPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
        var js = File.ReadAllText(jsPath);
        js.Should().Contain("setShowBlocks");
        js.Should().Contain("setProtectionMode");
        js.Should().Contain("_isInsideProtectedEditableRegion");
        js.Should().Contain("getBodyHtml");
        js.Should().Contain("tm-wysiwyg--show-blocks");
    }

    [Fact]
    public void CssFile_ContainsShowBlocksStyles()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }
        repoRoot.Should().NotBeNull("Could not find repository root (.git directory).");
        var cssPath = Path.Combine(repoRoot!.FullName, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css");
        var css = File.ReadAllText(cssPath);
        css.Should().Contain(".tm-wysiwyg--show-blocks .tm-wysiwyg-block");
        css.Should().Contain("attr(data-block-type)");
    }

    [Fact]
    public async Task Host_SetShowBlocksAsync_CallsJsSetShowBlocks()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.setShowBlocks", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        await cut.Instance.SetShowBlocksAsync(true);

        JSInterop.Invocations.Should().Contain(i =>
            i.Identifier == "tmDocumentWysiwyg.setShowBlocks"
            && i.Arguments.OfType<bool>().Contains(true));
    }

    [Fact]
    public async Task Host_SetProtectionModeAsync_CallsJsSetProtectionMode()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.setProtectionMode", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        await cut.Instance.SetProtectionModeAsync(true, []);

        JSInterop.Invocations.Should().Contain(i =>
            i.Identifier == "tmDocumentWysiwyg.setProtectionMode");
    }

    [Fact]
    public async Task Phase7_Host_SetSearchMarkersAsync_CallsRuntimeMarkerBridge()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.setSearchMarkers", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        await cut.Instance.SetSearchMarkersAsync(["block-1"], [3], [5]);

        var invocation = JSInterop.Invocations.Should().ContainSingle(i =>
            i.Identifier == "tmDocumentWysiwyg.setSearchMarkers").Subject;
        invocation.Arguments[0].Should().BeOfType<string>().Which.Should().NotBeNullOrWhiteSpace();
        invocation.Arguments[1].Should().BeEquivalentTo(new[] { "block-1" });
        invocation.Arguments[2].Should().BeEquivalentTo(new[] { 3 });
        invocation.Arguments[3].Should().BeEquivalentTo(new[] { 5 });
    }

    [Fact]
    public async Task Phase7_Host_ScrollToSearchResultAsync_ActivatesRuntimeSearchMarker()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.scrollToSearchResult", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        await cut.Instance.ScrollToSearchResultAsync("block-1", 3, 5);

        var invocation = JSInterop.Invocations.Should().ContainSingle(i =>
            i.Identifier == "tmDocumentWysiwyg.scrollToSearchResult").Subject;
        invocation.Arguments[0].Should().BeOfType<string>().Which.Should().NotBeNullOrWhiteSpace();
        invocation.Arguments.Should().ContainInOrder("block-1", 3, 5);
    }

    [Fact]
    public async Task Host_GetBodyHtmlAsync_CallsJsGetBodyHtmlAndReturnsResult()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentEditorRuntime.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentEditorRuntime.loadDocument", _ => true).SetVoidResult();
        JSInterop.Setup<string>("tmDocumentWysiwyg.getBodyHtml", _ => true)
            .SetResult("<p>Hello</p>");

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var html = await cut.Instance.GetBodyHtmlAsync();

        html.Should().Be("<p>Hello</p>");
    }

    [Fact]
    public async Task Host_TableSelection_RendersTableToolbar()
    {
        var document = new DocumentEditorDocument
        {
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "table-1",
                    Type = DocumentBlockType.Table,
                    Content = new TableBlockContent
                    {
                        Rows =
                        [
                            new TableRowContent
                            {
                                Cells =
                                [
                                    new TableCellContent { Id = "cell-1", Blocks = [] },
                                    new TableCellContent { Id = "cell-2", Blocks = [] }
                                ]
                            }
                        ]
                    }
                }
            ]
        };
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters => parameters
            .Add(p => p.Document, document));

        await cut.Instance.HandleSelectionChanged(new WysiwygSelectionSnapshot
        {
            Region = "TableCell",
            ActiveTableCellId = "cell-1"
        });

        cut.Find("[data-testid='document-table-toolbar']").Should().NotBeNull();
        cut.Find("[data-testid='document-table-toolbar-insert-row-after']").Should().NotBeNull();
        cut.Find("[data-testid='document-table-toolbar-table-properties']").Should().NotBeNull();
    }

    private static DocumentEditorDocument CreateEmptyDocument()
    {
        return DocumentEditorDocument.Empty("wysiwyg-test-1");
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
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
                    Key = "client.name",
                    DisplayName = "Client name",
                    Description = "Client display name",
                    Category = "Client",
                    TypeLabel = "Text"
                }
            ];

            return Task.FromResult(tokens);
        }
    }

    private sealed class TestMentionProvider : IMentionDataProvider
    {
        public Task<IEnumerable<IMentionUser>> SearchUsersAsync(string query, CancellationToken ct = default)
        {
            IEnumerable<IMentionUser> users =
            [
                new TestMentionUser
                {
                    Id = "user-1",
                    UserName = "alex",
                    DisplayName = "Alex Johnson",
                    AvatarUrl = null
                }
            ];

            return Task.FromResult(users);
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

    private sealed class TestMentionUser : IMentionUser
    {
        public string Id { get; init; } = string.Empty;

        public string UserName { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string? AvatarUrl { get; init; }
    }

    private sealed class CapturingImageProvider : IDocumentImageProvider
    {
        public List<DocumentImageUploadRequest> UploadRequests { get; } = [];

        public List<DocumentImageResolveRequest> ResolveRequests { get; } = [];

        public byte[] UploadedBytes { get; private set; } = [];

        public async Task<DocumentImageUploadResult> UploadAsync(
            DocumentImageUploadRequest request,
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            UploadRequests.Add(request);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            UploadedBytes = memory.ToArray();
            return new DocumentImageUploadResult
            {
                Success = true,
                AssetId = "asset-1",
                Url = "https://cdn.example.test/asset-1.png"
            };
        }

        public Task<DocumentImageResolveResult> ResolveAsync(
            DocumentImageResolveRequest request,
            CancellationToken cancellationToken = default)
        {
            ResolveRequests.Add(request);
            return Task.FromResult(new DocumentImageResolveResult
            {
                Success = true,
                Url = "https://cdn.example.test/asset-1.png"
            });
        }

        public Task DeleteDraftAssetAsync(string documentId, string assetId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<DocumentImageCommitResult> CommitAssetsAsync(
            string documentId,
            IReadOnlyList<string> assetIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DocumentImageCommitResult { Success = true, AssetIds = [.. assetIds] });
        }

        public Task<DocumentImageResolveResult> RefreshUrlAsync(
            DocumentImageResolveRequest request,
            CancellationToken cancellationToken = default)
        {
            return ResolveAsync(request, cancellationToken);
        }
    }
}
