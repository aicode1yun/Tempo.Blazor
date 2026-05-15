using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentWysiwyg.create");
    }

    [Fact]
    public void Host_Lifecycle_PassesOptionsToJsCreate()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.ReadOnly, true));

        var invocation = JSInterop.Invocations.FirstOrDefault(i => i.Identifier == "tmDocumentWysiwyg.create");
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
    }

    [Fact]
    public async Task Host_Lifecycle_SendsSuggestionsInSnapshot()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

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
            .FirstOrDefault(i => i.Identifier == "tmDocumentWysiwyg.applySnapshot");
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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.dispose", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.DisposeAsync();

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentWysiwyg.dispose");
    }

    [Fact]
    public void Host_JsFailure_ShowsFallback()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true)
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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true)
                 .SetException(new JSException("Engine not found"));

        var act = () => RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        act.Should().NotThrow();
    }

    [Fact]
    public void Host_DefaultState_ShowsLoadingSkeleton()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>();

        // Before JS ready callback the loading skeleton should be visible.
        cut.FindAll(".tm-skeleton").Should().NotBeEmpty();
    }

    [Fact]
    public async Task Host_JsReadyCallback_HidesLoadingSkeleton()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();

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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

        var snapshotJson = @"{""ProtocolVersion"":1,""Document"":{""SchemaVersion"":1,""DocumentId"":""doc-1"",""Blocks"":[{""Id"":""b-1"",""Type"":0,""Order"":10,""Content"":{""$type"":""paragraph"",""Inlines"":[{""$type"":""text"",""Id"":""i-0"",""Text"":""Snapshot text""}]}}]}}";
        JSInterop.Setup<string>("tmDocumentWysiwyg.getSnapshot", _ => true).SetResult(snapshotJson);

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
    public async Task Host_RequestSnapshotAsync_ReturnsNullWhenJsNotReady()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        // Do NOT call HandleJsEngineReady — _jsReady is false.
        var result = await cut.Instance.RequestSnapshotAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task Host_CaptureTextSelectionAnchorAsync_ReturnsAnchorWhenTextSelected()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

        var anchor = new DocumentCommentAnchor
        {
            Type = DocumentCommentAnchorType.TextRange,
            BlockId = "b-1",
            StartInlineIndex = 0,
            StartOffset = 3,
            EndInlineIndex = 0,
            EndOffset = 10
        };
        JSInterop.Setup<DocumentCommentAnchor?>("tmDocumentWysiwyg.captureCommentAnchor", _ => true).SetResult(anchor);

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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        var result = await cut.Instance.CaptureTextSelectionAnchorAsync();
        result.Should().BeNull();
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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.restoreSelection", _ => true).SetVoidResult();

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
            invocation.Identifier == "tmDocumentWysiwyg.restoreSelection");
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
    public async Task Host_ExecuteEditorCommandAsync_CallsJsExecuteCommand()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.executeCommand", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument()));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        await cut.Instance.ExecuteEditorCommandAsync("toggleMark", new WysiwygMarkPayload { MarkType = "Bold" });

        JSInterop.Invocations.Should().Contain(invocation =>
            invocation.Identifier == "tmDocumentWysiwyg.executeCommand");
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
    public async Task Host_ProviderImageButton_UploadsAndSendsImageNodeToJs()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.insertImageNode", _ => true).SetVoidResult();

        var provider = new CapturingImageProvider();
        var patches = new List<WysiwygPatch>();
        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, CreateEmptyDocument())
                      .Add(p => p.ImageProvider, provider)
                      .Add(p => p.DocumentPatchGenerated, EventCallback.Factory.Create<WysiwygPatch>(this, p => patches.Add(p))));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });
        await cut.Instance.OpenImageDialogAsync();

        cut.Find("[data-testid='document-wysiwyg-upload-demo-image']").Click();

        provider.UploadRequests.Should().ContainSingle();
        patches.Should().ContainSingle(patch => patch.BlockType == "Image");
        JSInterop.Invocations.Should().Contain(invocation => invocation.Identifier == "tmDocumentWysiwyg.insertImageNode");
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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

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
            invocation.Identifier == "tmDocumentWysiwyg.applySnapshot");
    }

    [Fact]
    public async Task Host_DocumentParameterChanged_SendsSnapshotToJs()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

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
            .Where(i => i.Identifier == "tmDocumentWysiwyg.applySnapshot")
            .ToList();

        applySnapshotCalls.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Host_SameDocumentReferenceChanged_DoesNotRoundtripSnapshot()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

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
            .Where(i => i.Identifier == "tmDocumentWysiwyg.applySnapshot")
            .ToList();

        applySnapshotCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Host_RefreshSnapshotAsync_ForcesSameReferenceSnapshot()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

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
            .Where(i => i.Identifier == "tmDocumentWysiwyg.applySnapshot")
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
        jsText.Should().Contain("tm-wysiwyg-page");
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
        jsText.Should().Contain("tm-wysiwyg-page__header");
        jsText.Should().Contain("tm-wysiwyg-page__footer");
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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

        var doc = CreateDocumentWithHeaderFooter();

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var applySnapshotCall = JSInterop.Invocations
            .FirstOrDefault(i => i.Identifier == "tmDocumentWysiwyg.applySnapshot");

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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

        var doc = CreateDocumentWithHeaderFooter(differentFirstPage: true);

        var cut = RenderComponent<TmDocumentWysiwygHost>(parameters =>
            parameters.Add(p => p.Document, doc));

        await cut.Instance.HandleJsEngineReady(new WysiwygEngineReadyEventArgs
        {
            InstanceId = "test-instance",
            ProtocolVersion = 1
        });

        var applySnapshotCall = JSInterop.Invocations
            .FirstOrDefault(i => i.Identifier == "tmDocumentWysiwyg.applySnapshot");

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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

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
            .FirstOrDefault(i => i.Identifier == "tmDocumentWysiwyg.applySnapshot");

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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

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
            .FirstOrDefault(i => i.Identifier == "tmDocumentWysiwyg.applySnapshot");

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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

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
            .FirstOrDefault(i => i.Identifier == "tmDocumentWysiwyg.applySnapshot");

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
        JSInterop.SetupVoid("tmDocumentWysiwyg.create", _ => true).SetVoidResult();
        JSInterop.SetupVoid("tmDocumentWysiwyg.applySnapshot", _ => true).SetVoidResult();

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
            .FirstOrDefault(i => i.Identifier == "tmDocumentWysiwyg.applySnapshot");

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
        js.Should().Contain("insertTableRow");
        js.Should().Contain("insertTableColumn");
        js.Should().Contain("deleteTableRow");
        js.Should().Contain("deleteTableColumn");
        js.Should().Contain("mergeTableCells");
        js.Should().Contain("splitTableCell");
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
        js.Should().Contain("contenteditable', 'false'");
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
        css.Should().Contain(".tm-wysiwyg-image--wrap-square");
        css.Should().Contain("shape-outside");
        css.Should().Contain(".tm-wysiwyg-image--wrap-top-bottom");
        css.Should().Contain(".tm-wysiwyg-image--wrap-behind-text");
        css.Should().Contain(".tm-wysiwyg-image--wrap-in-front-of-text");
        css.Should().Contain(".tm-wysiwyg-image__resize-handle");
    }

    private static DocumentEditorDocument CreateEmptyDocument()
    {
        return DocumentEditorDocument.Empty("wysiwyg-test-1");
    }

    private sealed class CapturingImageProvider : IDocumentImageProvider
    {
        public List<DocumentImageUploadRequest> UploadRequests { get; } = [];

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
