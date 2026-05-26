using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for deterministic high-quality document editor demo data.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase19E2ETests : DocumentEditorE2ETestBase
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(DocumentEditorJson.Options)
    {
        PropertyNameCaseInsensitive = true
    };

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_DemoResetReturnsCanonicalQualityScenarios()
    {
        await DocumentEditorE2EReset.ResetAsync();
        var firstDocument = await LoadContractDocumentAsync();
        var firstVersions = await GetApiJsonAsync("api/document-editor/documents/contract-demo/versions");
        var firstComments = await GetApiJsonAsync("api/document-editor/documents/contract-demo/comments");

        await DocumentEditorE2EReset.ResetAsync();
        var secondDocument = await LoadContractDocumentAsync();
        var secondVersions = await GetApiJsonAsync("api/document-editor/documents/contract-demo/versions");
        var secondComments = await GetApiJsonAsync("api/document-editor/documents/contract-demo/comments");

        var firstSnapshot = GetString(firstDocument.RootElement, "JsonSnapshot");
        var secondSnapshot = GetString(secondDocument.RootElement, "JsonSnapshot");

        firstSnapshot.Should().Be(secondSnapshot, "reset must return the same canonical contract document snapshot");
        Canonicalize(firstVersions.RootElement).Should().Be(Canonicalize(secondVersions.RootElement), "demo versions must not contain approval-breaking random ids or timestamps");
        Canonicalize(firstComments.RootElement).Should().Be(Canonicalize(secondComments.RootElement), "demo comments must keep stable ids and timestamps");

        using var snapshot = JsonDocument.Parse(firstSnapshot);
        AssertContractDemoScenarios(snapshot.RootElement);
        AssertContractDemoDrawingScenarios(DocumentEditorJson.Deserialize(firstSnapshot));
    }

    [TestMethod]
    public async Task Phase14_SaveReload_DrawingWrapPositionAndResizePersist()
    {
        await DocumentEditorE2EReset.ResetAsync();
        using var http = CreateApiClient();

        try
        {
            var document = await LoadDocumentAsync(http, "contract-demo")
                ?? throw new AssertFailedException("The contract demo document could not be loaded.");

            var center = FindDrawing(document, "contract-center-wrap-image");
            center.Layout.Wrap.Mode = DocumentWrapMode.TopBottom;
            center.Layout.Wrap.DistanceTop = 7;
            center.Layout.Wrap.DistanceBottom = 13;

            var offset = FindDrawing(document, "contract-offset-wrap-image");
            offset.Layout.Position.HorizontalAlignment = null;
            offset.Layout.Position.X = 276;
            offset.Layout.Position.Y = 18;
            offset.Layout.Wrap.DistanceLeft = 14;
            offset.Layout.Wrap.DistanceRight = 19;

            var tight = FindDrawing(document, "contract-tight-wrap-image");
            tight.Layout.Transform.Width = 154;
            tight.Layout.Transform.Height = 86;
            tight.Layout.Transform.LockAspectRatio = false;

            await SaveDocumentAsync(http, document);

            var reloaded = await LoadDocumentAsync(http, "contract-demo")
                ?? throw new AssertFailedException("The contract demo document could not be reloaded.");

            var reloadedCenter = FindDrawing(reloaded, "contract-center-wrap-image");
            reloadedCenter.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.TopBottom);
            reloadedCenter.Layout.Wrap.DistanceTop.Should().BeApproximately(7, 0.01);
            reloadedCenter.Layout.Wrap.DistanceBottom.Should().BeApproximately(13, 0.01);

            var reloadedOffset = FindDrawing(reloaded, "contract-offset-wrap-image");
            reloadedOffset.Layout.Position.HorizontalAlignment.Should().BeNull();
            reloadedOffset.Layout.Position.X.Should().BeApproximately(276, 0.01);
            reloadedOffset.Layout.Position.Y.Should().BeApproximately(18, 0.01);
            reloadedOffset.Layout.Wrap.DistanceLeft.Should().BeApproximately(14, 0.01);
            reloadedOffset.Layout.Wrap.DistanceRight.Should().BeApproximately(19, 0.01);

            var reloadedTight = FindDrawing(reloaded, "contract-tight-wrap-image");
            reloadedTight.Layout.Transform.Width.Should().BeApproximately(154, 0.01);
            reloadedTight.Layout.Transform.Height.Should().BeApproximately(86, 0.01);
            reloadedTight.Layout.Transform.LockAspectRatio.Should().BeFalse();
        }
        finally
        {
            await DocumentEditorE2EReset.ResetAsync();
        }
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_DefaultDemoReloadIsReadableAndOverlapFree()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        using var console = BeginDocumentEditorConsoleCapture(page);

        await page.WaitForTimeoutAsync(250);
        var probes = await RunDocumentEditorActionWithFrameProbesAsync(
            page,
            "Default contract demo reload must be readable without text/image overlap",
            () => Task.CompletedTask);
        await AssertStrictFrameProbesCleanAsync(page, probes, "Default contract demo reload must be readable without text/image overlap");

        var probe = probes.Last();
        var normalizedDocumentText = NormalizeWhitespace(probe.DocumentText);
        normalizedDocumentText.Should().Contain("This paragraph demonstrates a left positioned evidence preview.");
        normalizedDocumentText.Should().Contain("This paragraph proves the opposite wrap direction.");
        normalizedDocumentText.Should().NotContain("Content overflows page");
        probe.ImageRectCount.Should().BeGreaterThanOrEqualTo(4);
        probe.TextTextOverlapCount.Should().Be(0);
        probe.TextImageOverlapCount.Should().Be(0);
        probe.TextCaptionOverlapCount.Should().Be(0);
        probe.SidePanelClippingCount.Should().Be(0);
        console.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_Phase19_LoadWrapFootprintsMatchOnlyOfficeContracts()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 1000);
        using var console = BeginDocumentEditorConsoleCapture(page);

        var drawingRuns = (await ReadDocumentEditorDrawingRunsAsync(page))
            .ToDictionary(run => run.ObjectId, StringComparer.Ordinal);
        drawingRuns.Keys.Should().Contain([
            "contract-left-wrap-image",
            "contract-right-wrap-image",
            "contract-center-wrap-image",
            "contract-offset-wrap-image",
            "contract-top-bottom-image",
            "contract-tight-wrap-image",
            "contract-in-front-image",
            "contract-behind-text-image",
            "contract-header-logo-image",
            "contract-footer-logo-image",
            "contract-table-cell-image"
        ]);
        drawingRuns.Values.Should().Contain(run => run.Region == "Body" && run.WrapMode == "Square");
        drawingRuns.Values.Should().Contain(run => run.Region == "Header" && run.ObjectId == "contract-header-logo-image");
        drawingRuns.Values.Should().Contain(run => run.Region == "Footer" && run.ObjectId == "contract-footer-logo-image");
        drawingRuns.Values.Should().Contain(run => run.Region == "TableCell" && run.CellId == "contract-pricing-table-r1-evidence");
        drawingRuns.Values.Count(run => run.Region == "Body").Should().BeGreaterThanOrEqualTo(8, "body scope must exercise multiple image objects in one deterministic document");
        drawingRuns.Values.Should().Contain(run => run.Height >= 120 && run.ObjectId == "contract-top-bottom-image", "the demo must keep a tall image that spans multiple text line heights");

        var left = await CapturePhase19ImageFootprintAsync(page, "contract-left-wrap-image");
        AssertLinesStayOutsideImage(left);
        AssertHasRightSideText(left, "left-positioned Square image must leave text on its right side");

        var right = await CapturePhase19ImageFootprintAsync(page, "contract-right-wrap-image");
        AssertLinesStayOutsideImage(right);
        AssertHasLeftSideText(right, "right-positioned Square image must leave text on its left side");

        var center = await CapturePhase19ImageFootprintAsync(page, "contract-center-wrap-image");
        AssertLinesStayOutsideImage(center);
        AssertHasLeftSideText(center, "center Square image must expose a left interval");
        AssertHasRightSideText(center, "center Square image must expose a right interval");

        var topBottom = await CapturePhase19ImageFootprintAsync(page, "contract-top-bottom-image");
        topBottom.Layer.WrapMode.Should().Be("TopBottom", topBottom.Debug);
        topBottom.LineIntervals.Should().BeEmpty("TopBottom wrapping reserves the whole object band, so no line interval may pass through the image height");

        var tight = await CapturePhase19ImageFootprintAsync(page, "contract-tight-wrap-image");
        tight.Layer.WrapMode.Should().Be("Tight", tight.Debug);
        AssertLinesStayOutsideImage(tight);
        tight.LineIntervals.Should().NotBeEmpty("Tight wrapping must still publish real text intervals around the contour");

        var behind = await CapturePhase19ImageFootprintAsync(page, "contract-behind-text-image");
        behind.Layer.WrapMode.Should().Be("BehindText", behind.Debug);
        behind.Layer.ObjectLayer.Should().Be("behind-text", behind.Debug);
        behind.Layer.FlowReservationCount.Should().Be(0, "BehindText must not use legacy browser flow reservation anchors");

        var inFront = await CapturePhase19ImageFootprintAsync(page, "contract-in-front-image");
        inFront.Layer.WrapMode.Should().Be("InFrontOfText", inFront.Debug);
        inFront.Layer.ObjectLayer.Should().Be("in-front-of-text", inFront.Debug);
        inFront.Layer.FlowReservationCount.Should().Be(0, "InFrontOfText must not use legacy browser flow reservation anchors");

        var probes = await RunDocumentEditorActionWithFrameProbesAsync(
            page,
            "Phase 19 final wrap load regression suite",
            () => Task.CompletedTask);
        await AssertStrictFrameProbesCleanAsync(page, probes, "Phase 19 final wrap load regression suite");
        console.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_Phase19_TextEditingBesideWrappedImageSupportsUndoRedo()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        using var console = BeginDocumentEditorConsoleCapture(page);
        const string blockId = "contract-center-wrap-text";
        const string leftText = "phase19-left ";
        const string rightText = " phase19-right";

        await ScrollDocumentEditorObjectIntoViewAsync(page, "contract-center-wrap-image");
        var before = await ReadDocumentEditorBlockTextAsync(page, blockId);
        NormalizeWhitespace(before).Should().Contain("both sides of the centered preview");

        await ClickDocumentEditorBlockOffsetAsync(page, blockId, 0);
        await page.Keyboard.TypeAsync(leftText, new() { Delay = 0 });
        await WaitForEditorStableAsync(page, "phase19 type before centered image", blockId, leftText.Trim());
        var afterLeft = await ReadDocumentEditorModelBlockTextAsync(page, blockId);
        afterLeft.Should().Contain(leftText, "typing before a centered wrapped image must update the JS document model immediately");

        var endOffset = (await ReadDocumentEditorModelBlockTextAsync(page, blockId)).Length;
        await ClickDocumentEditorBlockOffsetAsync(page, blockId, endOffset);
        await page.Keyboard.TypeAsync(rightText, new() { Delay = 0 });
        await WaitForEditorStableAsync(page, "phase19 type after centered image", blockId, rightText.Trim());
        var afterRight = await ReadDocumentEditorModelBlockTextAsync(page, blockId);
        afterRight.Should().Contain(rightText, "typing after a centered wrapped image must update the JS document model immediately");

        await page.Keyboard.PressAsync("Backspace");
        await WaitForEditorStableAsync(page, "phase19 delete beside centered image", blockId);
        var afterDelete = await ReadDocumentEditorModelBlockTextAsync(page, blockId);
        afterDelete.Should().Contain("phase19-righ", "backspace beside a wrapped image should delete only the adjacent character");
        afterDelete.Should().NotContain(rightText, "the last typed character should be removed before undo");

        await page.Keyboard.PressAsync("Control+Z");
        await WaitForEditorStableAsync(page, "phase19 undo delete beside centered image", blockId, rightText.Trim());
        (await ReadDocumentEditorModelBlockTextAsync(page, blockId)).Should().Contain(rightText);

        await page.Keyboard.PressAsync("Control+Y");
        await WaitForEditorStableAsync(page, "phase19 redo delete beside centered image", blockId);
        (await ReadDocumentEditorModelBlockTextAsync(page, blockId)).Should().NotContain(rightText);

        var probes = await RunDocumentEditorActionWithFrameProbesAsync(
            page,
            "Phase 19 text editing beside wrapped image",
            () => Task.CompletedTask);
        await AssertStrictFrameProbesCleanAsync(page, probes, "Phase 19 text editing beside wrapped image");
        console.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_Phase19_MultipleImagesInOneParagraphAndTallImageRender()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 900);
        using var console = BeginDocumentEditorConsoleCapture(page);
        const string sandboxSelector = "[data-testid='phase19-scope-host'] [data-testid='document-wysiwyg-host']";

        var instanceId = await page.EvaluateAsync<string>(
            """
            () => {
                const shell = document.createElement('div');
                shell.setAttribute('data-testid', 'phase19-scope-host');
                shell.className = 'tm-document-editor';
                shell.style.cssText = 'position:fixed;left:320px;top:24px;width:760px;min-height:620px;background:white;z-index:20000;pointer-events:auto;';
                const host = document.createElement('div');
                host.setAttribute('data-testid', 'document-wysiwyg-host');
                shell.appendChild(host);
                document.body.appendChild(shell);

                const engine = window.tmDocumentEditorEngine;
                const instanceId = engine.create(host, { InstanceId: 'phase19-scope-e2e' }, null);
                const dataUrl = 'data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%22160%22 height=%2290%22 viewBox=%220 0 160 90%22%3E%3Crect width=%22160%22 height=%2290%22 fill=%22%232563eb%22/%3E%3Cpath d=%22M18 60h124M18 30h90%22 stroke=%22white%22 stroke-width=%226%22 stroke-linecap=%22round%22/%3E%3C/svg%3E';
                const layout = (objectId, inlineIndex, mode, align, width, height) => ({
                    Kind: mode === 'Inline' ? 0 : 1,
                    Anchor: { BlockId: 'phase19-multi-p', Offset: 24, InlineIndex: inlineIndex, Region: 'Body', MoveWithText: true, FixedOnPage: false },
                    Position: { HorizontalRelativeTo: 2, VerticalRelativeTo: 3, HorizontalAlignment: align },
                    Wrap: { Mode: mode === 'TopBottom' ? 4 : 1, DistanceLeft: 8, DistanceRight: 8, DistanceTop: 4, DistanceBottom: 8 },
                    Transform: { Width: width, Height: height, NaturalWidth: width, NaturalHeight: height, LockAspectRatio: true },
                    Stacking: { ZIndex: 0, AllowOverlap: false }
                });
                const drawing = (id, inlineIndex, mode, align, width, height, caption) => ({
                    $type: 'drawing',
                    Id: id + '-run',
                    ObjectId: id,
                    Kind: 0,
                    Source: 0,
                    Url: dataUrl,
                    AltText: caption,
                    Caption: caption,
                    Size: { Width: width, Height: height },
                    Layout: layout(id, inlineIndex, mode, align, width, height)
                });

                engine.loadDocument(instanceId, {
                    Document: {
                        DocumentId: 'phase19-scope-doc',
                        Blocks: [{
                            Id: 'phase19-multi-p',
                            Type: 'Paragraph',
                            Content: {
                                Type: 'Paragraph',
                                Inlines: [
                                    { Id: 'phase19-text-a', Text: 'A paragraph with several drawing runs must keep every object in the same logical paragraph while the visual layout exposes editable text before, between, beside, and after the images. ' },
                                    drawing('phase19-multi-left', 1, 'Square', 0, 96, 54, 'Left image in a multi-image paragraph'),
                                    { Id: 'phase19-text-b', Text: 'The second image shares the paragraph and proves that anchoring does not split the document model into fake image blocks. ' },
                                    drawing('phase19-multi-right', 3, 'Square', 2, 96, 54, 'Right image in a multi-image paragraph'),
                                    { Id: 'phase19-text-c', Text: 'The final object is deliberately taller than two normal line heights so top and bottom reservation is exercised in the same scope. ' },
                                    drawing('phase19-multi-tall', 5, 'TopBottom', 1, 140, 132, 'Tall image in a multi-image paragraph'),
                                    { Id: 'phase19-text-d', Text: 'Text continues after all images without requiring standalone image paragraphs.' }
                                ]
                            }
                        }]
                    }
                });
                return instanceId;
            }
            """);

        try
        {
            await page.WaitForSelectorAsync($"{sandboxSelector} [data-object-id='phase19-multi-left']", new() { State = WaitForSelectorState.Attached, Timeout = 10000 });

            var runs = await ReadDocumentEditorDrawingRunsAsync(page, blockId: "phase19-multi-p", hostSelector: sandboxSelector);
            runs.Should().HaveCount(3, "one paragraph must retain multiple drawing runs instead of synthetic image paragraphs");
            runs.Select(run => run.ObjectId).Should().Contain(["phase19-multi-left", "phase19-multi-right", "phase19-multi-tall"]);
            runs.Should().OnlyContain(run => run.BlockId == "phase19-multi-p" && run.Region == "Body");
            runs.Should().Contain(run => run.ObjectId == "phase19-multi-tall" && run.Height >= 132, "the scope scenario must include an image taller than two line heights");

            var probes = await RunDocumentEditorActionWithFrameProbesAsync(
                page,
                "Phase 19 multiple images in one paragraph scope",
                () => Task.CompletedTask,
                sandboxSelector);
            await AssertStrictFrameProbesCleanAsync(page, probes, "Phase 19 multiple images in one paragraph scope", sandboxSelector);
            console.Errors.Should().BeEmpty();
        }
        finally
        {
            await page.EvaluateAsync(
                """
                (instanceId) => {
                    window.tmDocumentEditorEngine?.dispose?.(instanceId);
                    document.querySelector('[data-testid="phase19-scope-host"]')?.remove();
                }
                """,
                instanceId);
        }
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_Phase19_ImageWrapDragResizeUndoRedoStayTransactional()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        using var console = BeginDocumentEditorConsoleCapture(page);

        var centerBefore = await ReadPhase19DrawingEditStateAsync(page, "contract-center-wrap-image");
        centerBefore.WrapMode.Should().Be("Square", centerBefore.Debug);

        var wrap = await ApplyPhase19ImageCommandAsync(page, "setImageWrapMode", new
        {
            objectId = "contract-center-wrap-image",
            wrapMode = "TopBottom"
        }, "contract-center-wrap-image");
        wrap.Ok.Should().BeTrue(wrap.Debug);
        wrap.WrapMode.Should().Be("TopBottom", wrap.Debug);

        var wrapUndo = await ApplyPhase19ImageCommandAsync(page, "undo", new { }, "contract-center-wrap-image");
        wrapUndo.Ok.Should().BeTrue(wrapUndo.Debug);
        wrapUndo.WrapMode.Should().Be(centerBefore.WrapMode, wrapUndo.Debug);

        var wrapRedo = await ApplyPhase19ImageCommandAsync(page, "redo", new { }, "contract-center-wrap-image");
        wrapRedo.Ok.Should().BeTrue(wrapRedo.Debug);
        wrapRedo.WrapMode.Should().Be("TopBottom", wrapRedo.Debug);

        await ApplyPhase19ImageCommandAsync(page, "undo", new { }, "contract-center-wrap-image");

        var offsetBefore = await ReadPhase19DrawingEditStateAsync(page, "contract-offset-wrap-image");
        var move = await ApplyPhase19ImageCommandAsync(page, "setImageObjectPosition", new
        {
            objectId = "contract-offset-wrap-image",
            x = offsetBefore.X + 18,
            y = offsetBefore.Y + 6,
            horizontalAlignment = "Left"
        }, "contract-offset-wrap-image");
        move.Ok.Should().BeTrue(move.Debug);
        move.X.Should().BeApproximately(offsetBefore.X + 18, 0.5, move.Debug);
        move.Y.Should().BeApproximately(offsetBefore.Y + 6, 0.5, move.Debug);

        var moveUndo = await ApplyPhase19ImageCommandAsync(page, "undo", new { }, "contract-offset-wrap-image");
        moveUndo.Ok.Should().BeTrue(moveUndo.Debug);
        moveUndo.X.Should().BeApproximately(offsetBefore.X, 0.5, moveUndo.Debug);
        moveUndo.Y.Should().BeApproximately(offsetBefore.Y, 0.5, moveUndo.Debug);

        var moveRedo = await ApplyPhase19ImageCommandAsync(page, "redo", new { }, "contract-offset-wrap-image");
        moveRedo.Ok.Should().BeTrue(moveRedo.Debug);
        moveRedo.X.Should().BeApproximately(offsetBefore.X + 18, 0.5, moveRedo.Debug);

        await ApplyPhase19ImageCommandAsync(page, "undo", new { }, "contract-offset-wrap-image");

        var resizeBefore = await ReadPhase19DrawingEditStateAsync(page, "contract-offset-wrap-image");
        var resize = await ApplyPhase19ImageCommandAsync(page, "setImageSize", new
        {
            objectId = "contract-offset-wrap-image",
            width = resizeBefore.Width + 24,
            height = resizeBefore.Height + 12,
            lockAspectRatio = false
        }, "contract-offset-wrap-image");
        resize.Ok.Should().BeTrue(resize.Debug);
        resize.Width.Should().BeApproximately(resizeBefore.Width + 24, 0.5, resize.Debug);
        resize.Height.Should().BeApproximately(resizeBefore.Height + 12, 0.5, resize.Debug);

        var resizeUndo = await ApplyPhase19ImageCommandAsync(page, "undo", new { }, "contract-offset-wrap-image");
        resizeUndo.Ok.Should().BeTrue(resizeUndo.Debug);
        resizeUndo.Width.Should().BeApproximately(resizeBefore.Width, 0.5, resizeUndo.Debug);
        resizeUndo.Height.Should().BeApproximately(resizeBefore.Height, 0.5, resizeUndo.Debug);

        var resizeRedo = await ApplyPhase19ImageCommandAsync(page, "redo", new { }, "contract-offset-wrap-image");
        resizeRedo.Ok.Should().BeTrue(resizeRedo.Debug);
        resizeRedo.Width.Should().BeApproximately(resizeBefore.Width + 24, 0.5, resizeRedo.Debug);

        var probes = await RunDocumentEditorActionWithFrameProbesAsync(
            page,
            "Phase 19 image wrap drag resize undo redo",
            () => Task.CompletedTask);
        await AssertStrictFrameProbesCleanAsync(page, probes, "Phase 19 image wrap drag resize undo redo");
        console.Errors.Should().BeEmpty();
    }

    private static async Task<Phase19ImageFootprintProbe> CapturePhase19ImageFootprintAsync(IPage page, string objectId)
    {
        await ScrollDocumentEditorObjectIntoViewAsync(page, objectId);
        await page.WaitForTimeoutAsync(50);
        var diagnostics = await ReadDocumentEditorImageDiagnosticsAsync(page, objectId);
        var layer = await ReadPhase19ImageLayerProbeAsync(page, objectId);
        return new Phase19ImageFootprintProbe(objectId, diagnostics.ImageRect, diagnostics.LineIntervals, layer, diagnostics.Debug);
    }

    private static Task ScrollDocumentEditorObjectIntoViewAsync(IPage page, string objectId)
        => page.EvaluateAsync(
            """
            (objectId) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const escaped = window.CSS?.escape ? window.CSS.escape(objectId) : String(objectId).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                const object = findImageObject(host, escaped);
                const anchorId = object?.getAttribute('data-anchor-block-id') || object?.getAttribute('data-model-block-id') || '';
                const anchor = anchorId
                    ? host?.querySelector(`[data-block-id="${window.CSS?.escape ? window.CSS.escape(anchorId) : anchorId}"], [data-render-block-id="${window.CSS?.escape ? window.CSS.escape(anchorId) : anchorId}"]`)
                    : null;
                (anchor || object)?.scrollIntoView({ block: 'center', inline: 'nearest' });

                function findImageObject(root, id) {
                    if (!root) return null;
                    const selectors = [
                        `.tm-wysiwyg-object-layer-item[data-object-id="${id}"]`,
                        `.tm-wysiwyg-inline-drawing[data-object-id="${id}"]`,
                        `figure.tm-wysiwyg-image[data-object-id="${id}"]`,
                        `.tm-render-image-widget[data-render-object-id="${id}"]`,
                        `.tm-wysiwyg-object-selection-overlay[data-object-id="${id}"]`,
                        `[data-object-id="${id}"]`,
                        `[data-render-object-id="${id}"]`,
                        `figure[data-block-id="${id}"]`,
                        `.tm-render-image-widget[data-render-block-id="${id}"]`
                    ];
                    for (const selector of selectors) {
                        const node = root.querySelector(selector);
                        if (node) return node;
                    }
                    return null;
                }
            }
            """,
            objectId);

    private static Task<Phase19ImageLayerProbe> ReadPhase19ImageLayerProbeAsync(IPage page, string objectId)
        => page.EvaluateAsync<Phase19ImageLayerProbe>(
            """
            (objectId) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const escaped = window.CSS?.escape ? window.CSS.escape(objectId) : String(objectId).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                const node = findImageObject(host, escaped, true)
                    || findImageObject(host, escaped, false);
                const rect = toRect(node?.getBoundingClientRect?.());
                const image = node?.querySelector?.('img') || null;
                return {
                    objectId,
                    objectLayer: node?.getAttribute('data-object-layer') || '',
                    wrapMode: node?.getAttribute('data-wrap-mode') || '',
                    flowReservationCount: host?.querySelectorAll('[data-flow-reservation="true"]').length || 0,
                    visible: isVisible(node),
                    imageLoaded: !image || image.complete === true,
                    rect,
                    debug: JSON.stringify({
                        objectId,
                        outerHtml: node?.outerHTML?.slice(0, 1200) || '',
                        rect,
                        visible: isVisible(node)
                    })
                };

                function findImageObject(root, id, visibleOnly) {
                    if (!root) return null;
                    const selectors = [
                        `.tm-wysiwyg-object-layer-item[data-object-id="${id}"]`,
                        `.tm-wysiwyg-inline-drawing[data-object-id="${id}"]`,
                        `figure.tm-wysiwyg-image[data-object-id="${id}"]`,
                        `.tm-render-image-widget[data-render-object-id="${id}"]`,
                        `.tm-wysiwyg-object-selection-overlay[data-object-id="${id}"]`,
                        `[data-object-id="${id}"][data-wrap-mode]`,
                        `[data-render-object-id="${id}"][data-wrap-mode]`,
                        `figure[data-block-id="${id}"]`,
                        `.tm-render-image-widget[data-render-block-id="${id}"]`
                    ];
                    for (const selector of selectors) {
                        const node = root.querySelector(selector);
                        if (!node) continue;
                        if (visibleOnly && !isVisible(node)) continue;
                        return node;
                    }
                    return null;
                }

                function isVisible(element) {
                    if (!element || element.closest('.tm-wysiwyg-page--virtual, [aria-hidden="true"]')) return false;
                    const rect = element.getBoundingClientRect();
                    const style = getComputedStyle(element);
                    return rect.width > 0.5
                        && rect.height > 0.5
                        && style.display !== 'none'
                        && style.visibility !== 'hidden'
                        && Number(style.opacity || 1) > 0.01;
                }

                function toRect(rect) {
                    return {
                        x: Number(rect?.x || rect?.left || 0),
                        y: Number(rect?.y || rect?.top || 0),
                        width: Number(rect?.width || 0),
                        height: Number(rect?.height || 0)
                    };
                }
            }
            """,
            objectId);

    private static Task<Phase19DrawingEditState> ReadPhase19DrawingEditStateAsync(IPage page, string objectId)
        => page.EvaluateAsync<Phase19DrawingEditState>(
            """
            (objectId) => {
                return readState(objectId, null);

                function readState(objectId, commandResult) {
                    const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                    const instanceId = host?.getAttribute('data-instance-id') || '';
                    const engine = window.tmDocumentEditorEngine || window.tmDocumentEditorRuntime;
                    const snapshot = engine?.getDocumentSnapshot?.(instanceId);
                    const documentModel = snapshot?.Document || snapshot?.document || snapshot?.csharpDocument || snapshot || null;
                    const debug = window.tmDocumentEditorEngine?.getDebugSnapshot?.(instanceId) || {};
                    const drawing = findDrawing(documentModel, objectId);
                    const layout = drawing?.Layout || drawing?.layout || {};
                    const position = layout.Position || layout.position || {};
                    const wrap = layout.Wrap || layout.wrap || {};
                    const transform = layout.Transform || layout.transform || {};
                    const anchor = layout.Anchor || layout.anchor || {};
                    return {
                        ok: commandResult ? commandResult.ok !== false : !!drawing,
                        objectId,
                        blockId: String(anchor.BlockId || anchor.blockId || ''),
                        wrapMode: normalizeWrapMode(wrap.Mode ?? wrap.mode ?? ''),
                        horizontalAlignment: normalizeHorizontal(position.HorizontalAlignment ?? position.horizontalAlignment ?? ''),
                        x: Number(position.X ?? position.x ?? 0) || 0,
                        y: Number(position.Y ?? position.y ?? 0) || 0,
                        width: Number(transform.Width ?? transform.width ?? drawing?.Size?.Width ?? drawing?.size?.width ?? 0) || 0,
                        height: Number(transform.Height ?? transform.height ?? drawing?.Size?.Height ?? drawing?.size?.height ?? 0) || 0,
                        undoDepth: Number(debug.undoDepth || debug.UndoDepth || 0) || 0,
                        redoDepth: Number(debug.redoDepth || debug.RedoDepth || 0) || 0,
                        debug: JSON.stringify({ commandResult, stateDebug: debug, drawing })
                    };
                }

                function findDrawing(documentModel, objectId) {
                    for (const block of collectBlocks(documentModel)) {
                        const content = block.Content || block.content || {};
                        const inlines = content.Inlines || content.inlines || content.Runs || content.runs || [];
                        for (const inline of Array.isArray(inlines) ? inlines : []) {
                            if (String(inline.ObjectId || inline.objectId || '') === objectId) return inline;
                        }
                    }
                    return null;
                }

                function collectBlocks(value) {
                    const blocks = [];
                    visit(value);
                    return blocks;

                    function visit(node) {
                        if (!node || typeof node !== 'object') return;
                        if (node.Id || node.id) blocks.push(node);
                        for (const key of ['Blocks', 'blocks', 'Children', 'children']) {
                            const collection = node[key];
                            if (Array.isArray(collection)) collection.forEach(visit);
                        }
                        const content = node.Content || node.content || {};
                        for (const row of Array.isArray(content.Rows || content.rows) ? (content.Rows || content.rows) : []) {
                            for (const cell of Array.isArray(row.Cells || row.cells) ? (row.Cells || row.cells) : []) {
                                for (const child of Array.isArray(cell.Blocks || cell.blocks) ? (cell.Blocks || cell.blocks) : []) visit(child);
                            }
                        }
                        for (const key of ['Document', 'document', 'Body', 'body']) visit(node[key]);
                        for (const header of Array.isArray(node.Headers || node.headers) ? (node.Headers || node.headers) : []) visit(header);
                        for (const footer of Array.isArray(node.Footers || node.footers) ? (node.Footers || node.footers) : []) visit(footer);
                    }
                }

                function normalizeWrapMode(mode) {
                    const raw = String(mode ?? '').trim();
                    const names = ['Inline', 'Square', 'Tight', 'Through', 'TopBottom', 'BehindText', 'InFrontOfText'];
                    if (/^\d+$/.test(raw)) return names[Number(raw)] || raw;
                    return raw || 'Inline';
                }

                function normalizeHorizontal(value) {
                    const raw = String(value ?? '').trim();
                    const names = ['Left', 'Center', 'Right'];
                    if (/^\d+$/.test(raw)) return names[Number(raw)] || raw;
                    return raw || '';
                }
            }
            """,
            objectId);

    private static Task<Phase19DrawingEditState> ApplyPhase19ImageCommandAsync(IPage page, string command, object payload, string objectId)
        => page.EvaluateAsync<Phase19DrawingEditState>(
            """
            ({ command, payload, objectId }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const result = window.tmDocumentEditorEngine?.applyCommand?.(instanceId, command, payload || {});
                return readState(objectId, result);

                function readState(objectId, commandResult) {
                    const engine = window.tmDocumentEditorEngine || window.tmDocumentEditorRuntime;
                    const snapshot = engine?.getDocumentSnapshot?.(instanceId);
                    const documentModel = snapshot?.Document || snapshot?.document || snapshot?.csharpDocument || snapshot || null;
                    const debug = window.tmDocumentEditorEngine?.getDebugSnapshot?.(instanceId) || {};
                    const drawing = findDrawing(documentModel, objectId);
                    const layout = drawing?.Layout || drawing?.layout || {};
                    const position = layout.Position || layout.position || {};
                    const wrap = layout.Wrap || layout.wrap || {};
                    const transform = layout.Transform || layout.transform || {};
                    const anchor = layout.Anchor || layout.anchor || {};
                    return {
                        ok: commandResult ? commandResult.ok !== false : !!drawing,
                        objectId,
                        blockId: String(anchor.BlockId || anchor.blockId || ''),
                        wrapMode: normalizeWrapMode(wrap.Mode ?? wrap.mode ?? ''),
                        horizontalAlignment: normalizeHorizontal(position.HorizontalAlignment ?? position.horizontalAlignment ?? ''),
                        x: Number(position.X ?? position.x ?? 0) || 0,
                        y: Number(position.Y ?? position.y ?? 0) || 0,
                        width: Number(transform.Width ?? transform.width ?? drawing?.Size?.Width ?? drawing?.size?.width ?? 0) || 0,
                        height: Number(transform.Height ?? transform.height ?? drawing?.Size?.Height ?? drawing?.size?.height ?? 0) || 0,
                        undoDepth: Number(debug.undoDepth || debug.UndoDepth || 0) || 0,
                        redoDepth: Number(debug.redoDepth || debug.RedoDepth || 0) || 0,
                        debug: JSON.stringify({ command, payload, commandResult, stateDebug: debug, drawing })
                    };
                }

                function findDrawing(documentModel, objectId) {
                    for (const block of collectBlocks(documentModel)) {
                        const content = block.Content || block.content || {};
                        const inlines = content.Inlines || content.inlines || content.Runs || content.runs || [];
                        for (const inline of Array.isArray(inlines) ? inlines : []) {
                            if (String(inline.ObjectId || inline.objectId || '') === objectId) return inline;
                        }
                    }
                    return null;
                }

                function collectBlocks(value) {
                    const blocks = [];
                    visit(value);
                    return blocks;

                    function visit(node) {
                        if (!node || typeof node !== 'object') return;
                        if (node.Id || node.id) blocks.push(node);
                        for (const key of ['Blocks', 'blocks', 'Children', 'children']) {
                            const collection = node[key];
                            if (Array.isArray(collection)) collection.forEach(visit);
                        }
                        const content = node.Content || node.content || {};
                        for (const row of Array.isArray(content.Rows || content.rows) ? (content.Rows || content.rows) : []) {
                            for (const cell of Array.isArray(row.Cells || row.cells) ? (row.Cells || row.cells) : []) {
                                for (const child of Array.isArray(cell.Blocks || cell.blocks) ? (cell.Blocks || cell.blocks) : []) visit(child);
                            }
                        }
                        for (const key of ['Document', 'document', 'Body', 'body']) visit(node[key]);
                        for (const header of Array.isArray(node.Headers || node.headers) ? (node.Headers || node.headers) : []) visit(header);
                        for (const footer of Array.isArray(node.Footers || node.footers) ? (node.Footers || node.footers) : []) visit(footer);
                    }
                }

                function normalizeWrapMode(mode) {
                    const raw = String(mode ?? '').trim();
                    const names = ['Inline', 'Square', 'Tight', 'Through', 'TopBottom', 'BehindText', 'InFrontOfText'];
                    if (/^\d+$/.test(raw)) return names[Number(raw)] || raw;
                    return raw || 'Inline';
                }

                function normalizeHorizontal(value) {
                    const raw = String(value ?? '').trim();
                    const names = ['Left', 'Center', 'Right'];
                    if (/^\d+$/.test(raw)) return names[Number(raw)] || raw;
                    return raw || '';
                }
            }
            """,
            new { command, payload, objectId });

    private static void AssertLinesStayOutsideImage(Phase19ImageFootprintProbe probe)
    {
        probe.Layer.Visible.Should().BeTrue(probe.Debug);
        probe.Layer.ImageLoaded.Should().BeTrue(probe.Debug);
        probe.ImageRect.Width.Should().BeGreaterThan(1, probe.Debug);
        probe.ImageRect.Height.Should().BeGreaterThan(1, probe.Debug);
        probe.LineIntervals.Should().OnlyContain(
            line => !HorizontallyIntersects(line, probe.ImageRect),
            "text intervals must be cut around the actual image rectangle instead of crossing it. Probe: {0}",
            probe.Debug);
    }

    private static void AssertHasLeftSideText(Phase19ImageFootprintProbe probe, string because)
        => probe.LineIntervals.Should().Contain(
            line => line.X + line.Width <= probe.ImageRect.X + 2,
            because + ". Probe: {0}",
            probe.Debug);

    private static void AssertHasRightSideText(Phase19ImageFootprintProbe probe, string because)
        => probe.LineIntervals.Should().Contain(
            line => line.X >= probe.ImageRect.X + probe.ImageRect.Width - 2,
            because + ". Probe: {0}",
            probe.Debug);

    private static bool HorizontallyIntersects(DocumentEditorLineIntervalProbe line, DocumentEditorRectProbe image)
        => line.X < image.X + image.Width - 1
           && line.X + line.Width > image.X + 1;

    private static void AssertContractDemoScenarios(JsonElement document)
    {
        var blocks = GetArray(document, "Blocks").ToArray();
        blocks.Should().Contain(block => GetString(block, "Id") == "contract-normal-overview", "demo must contain normal readable text");
        blocks.Should().Contain(block => GetString(block, "Id") == "contract-pricing-table" && IsEnum(GetRequired(block, "Type"), "Table", 4), "demo must contain a table");
        blocks.Where(IsImageBlock).Should().BeEmpty("demo images must use drawing runs instead of top-level image blocks");

        var intro = FindBlock(blocks, "contract-intro");
        IsEnum(GetRequired(GetRequired(intro, "ParagraphProperties"), "Alignment"), "Justify", 3)
            .Should().BeTrue("demo must contain a justified paragraph");

        AssertImageScenario(blocks, "contract-left-wrap-image", "Square", 1, "Left", 0, "contract-evidence-asset", requiresAlt: true);
        AssertImageScenario(blocks, "contract-right-wrap-image", "Square", 1, "Right", 2, "contract-evidence-asset", requiresAlt: true);
        AssertImageScenario(blocks, "contract-center-wrap-image", "Square", 1, "Center", 1, "contract-evidence-asset", requiresAlt: true);
        AssertImageScenario(blocks, "contract-top-bottom-image", "TopBottom", 4, "Center", 1, "contract-evidence-asset", requiresAlt: true);
        AssertImageScenario(blocks, "contract-inline-image", "Inline", 0, null, null, "contract-evidence-asset", requiresAlt: true);
        AssertImageScenario(blocks, "contract-missing-alt-image", "Inline", 0, null, null, "contract-evidence-asset", requiresAlt: false);
        AssertImageScenario(blocks, "contract-behind-text-image", "BehindText", 5, null, null, "contract-evidence-asset", requiresAlt: true);

        var revisions = GetArray(document, "Revisions").ToArray();
        revisions.Should().Contain(revision => GetString(revision, "Id") == "contract-revision-scope" && IsEnum(GetRequired(revision, "Type"), "Insertion", 0));
        revisions.Should().Contain(revision => GetString(revision, "Id") == "contract-revision-deletion" && IsEnum(GetRequired(revision, "Type"), "Deletion", 1));

        var comments = GetArray(document, "Comments").ToArray();
        comments.Should().Contain(comment => GetString(comment, "Id") == "contract-comment-client-token");

        var plainText = ExtractPlainText(blocks);
        plainText.Should().Contain("realistic contract text");
        plainText.Should().Contain("left positioned evidence preview");
        plainText.Should().Contain("opposite wrap direction");
        plainText.Should().Contain("both sides of the centered preview");
        plainText.Should().Contain("arbitrary drag-like offset");
        plainText.Should().Contain("custom diamond contour");
        plainText.Should().Contain("front and behind layer serialization");
        plainText.Should().NotContain("ffff", "demo must be curated and readable, not debugging filler");
        plainText.Should().NotContain("dddd", "demo must be curated and readable, not debugging filler");
    }

    private static void AssertContractDemoDrawingScenarios(DocumentEditorDocument document)
    {
        document.Blocks.Should().NotContain(block => block.Content is ImageBlockContent, "phase 14 wrap scenarios must be drawing runs");
        var drawings = DocumentImagePersistence.EnumerateDrawingRuns(document)
            .ToDictionary(drawing => drawing.ObjectId, StringComparer.Ordinal);

        drawings.Keys.Should().Contain([
            "contract-left-wrap-image",
            "contract-right-wrap-image",
            "contract-center-wrap-image",
            "contract-offset-wrap-image",
            "contract-top-bottom-image",
            "contract-tight-wrap-image",
            "contract-in-front-image",
            "contract-behind-text-image",
            "contract-header-logo-image",
            "contract-footer-logo-image",
            "contract-table-cell-image"
        ]);

        AssertDrawing(drawings["contract-left-wrap-image"], DocumentWrapMode.Square, DocumentImageHorizontalPosition.Left, DocumentRenditionAnchorScope.Body);
        AssertDrawing(drawings["contract-right-wrap-image"], DocumentWrapMode.Square, DocumentImageHorizontalPosition.Right, DocumentRenditionAnchorScope.Body);
        AssertDrawing(drawings["contract-center-wrap-image"], DocumentWrapMode.Square, DocumentImageHorizontalPosition.Center, DocumentRenditionAnchorScope.Body);
        drawings["contract-center-wrap-image"].Layout.Anchor.BlockId.Should().Be("contract-center-wrap-text");

        var offset = drawings["contract-offset-wrap-image"];
        offset.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        offset.Layout.Position.HorizontalAlignment.Should().BeNull();
        offset.Layout.Position.X.Should().BeGreaterThan(40);
        offset.Layout.Position.Y.Should().BeGreaterThan(0);

        var topBottom = drawings["contract-top-bottom-image"];
        topBottom.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.TopBottom);
        topBottom.Layout.Position.HorizontalAlignment.Should().Be(DocumentImageHorizontalPosition.Center);

        var tight = drawings["contract-tight-wrap-image"];
        tight.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Tight);
        tight.Layout.Wrap.Side.Should().Be(DocumentObjectWrapSide.Largest);
        tight.Layout.Wrap.WrapContourPoints.Should().HaveCountGreaterThanOrEqualTo(4);

        drawings["contract-in-front-image"].Layout.Kind.Should().Be(DocumentObjectLayoutKind.Fixed);
        drawings["contract-in-front-image"].Layout.Wrap.Mode.Should().Be(DocumentWrapMode.InFrontOfText);
        drawings["contract-in-front-image"].Layout.Anchor.FixedOnPage.Should().BeTrue();

        drawings["contract-behind-text-image"].Layout.Kind.Should().Be(DocumentObjectLayoutKind.Fixed);
        drawings["contract-behind-text-image"].Layout.Wrap.Mode.Should().Be(DocumentWrapMode.BehindText);
        drawings["contract-behind-text-image"].Layout.Anchor.FixedOnPage.Should().BeTrue();

        AssertDrawing(drawings["contract-header-logo-image"], DocumentWrapMode.Inline, null, DocumentRenditionAnchorScope.Header);
        drawings["contract-header-logo-image"].Layout.Anchor.HeaderFooterId.Should().Be("contract-header-primary");
        AssertDrawing(drawings["contract-footer-logo-image"], DocumentWrapMode.Inline, null, DocumentRenditionAnchorScope.Footer);
        drawings["contract-footer-logo-image"].Layout.Anchor.HeaderFooterId.Should().Be("contract-footer-primary");

        var tableCell = drawings["contract-table-cell-image"];
        tableCell.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.TableCell);
        tableCell.Layout.Anchor.TableId.Should().Be("contract-pricing-table");
        tableCell.Layout.Anchor.CellId.Should().Be("contract-pricing-table-r1-evidence");
        tableCell.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        tableCell.Docx?.LayoutInCell.Should().BeTrue();
    }

    private static void AssertDrawing(
        DocumentDrawingRun drawing,
        DocumentWrapMode wrapMode,
        DocumentImageHorizontalPosition? horizontalPosition,
        DocumentRenditionAnchorScope region)
    {
        drawing.Layout.Wrap.Mode.Should().Be(wrapMode);
        drawing.Layout.Anchor.Region.Should().Be(region);
        drawing.Caption.Should().NotBeNullOrWhiteSpace();
        drawing.AltText.Should().NotBeNullOrWhiteSpace();
        if (horizontalPosition.HasValue)
        {
            drawing.Layout.Position.HorizontalAlignment.Should().Be(horizontalPosition);
        }
    }

    private static void AssertImageScenario(
        JsonElement[] blocks,
        string blockId,
        string wrapName,
        int wrapValue,
        string? horizontalName,
        int? horizontalValue,
        string stableAssetId,
        bool requiresAlt)
    {
        var image = FindBlock(blocks, blockId);
        IsEnum(GetRequired(image, "Type"), "Paragraph", 0).Should().BeTrue($"{blockId} must be a drawing-run paragraph");

        var content = GetRequired(image, "Content");
        var drawing = GetArray(content, "Inlines")
            .FirstOrDefault(inline => string.Equals(GetString(inline, "ObjectId"), blockId, StringComparison.Ordinal));
        drawing.ValueKind.Should().NotBe(JsonValueKind.Undefined, $"{blockId} must expose a drawing run");

        var caption = GetString(drawing, "Caption");
        caption.Should().NotBeNullOrWhiteSpace($"{blockId} must expose a caption for UI/UX coverage");

        if (requiresAlt)
        {
            GetString(drawing, "AltText").Should().NotBeNullOrWhiteSpace($"{blockId} must have alt text");
        }
        else
        {
            GetString(drawing, "AltText").Should().BeNullOrWhiteSpace($"{blockId} intentionally drives the missing-alt warning");
        }

        var source = GetRequired(drawing, "Source");
        var assetId = GetString(drawing, "AssetId");
        var url = GetString(drawing, "Url");
        if (assetId is not null)
        {
            assetId.Should().Be(stableAssetId);
            IsEnum(source, "Asset", 1).Should().BeTrue($"{blockId} must use a stable provider asset id");
        }
        else
        {
            url.Should().Be("/document-editor-evidence.svg");
            IsEnum(source, "Url", 0).Should().BeTrue($"{blockId} must use the stable demo URL asset");
        }

        var layout = GetRequired(drawing, "Layout");
        var wrap = GetRequired(layout, "Wrap");
        IsEnum(GetRequired(wrap, "Mode"), wrapName, wrapValue).Should().BeTrue($"{blockId} must use {wrapName} wrapping");

        if (horizontalName is not null && horizontalValue is not null)
        {
            var position = GetRequired(layout, "Position");
            IsEnum(GetRequired(position, "HorizontalAlignment"), horizontalName, horizontalValue.Value)
                .Should().BeTrue($"{blockId} must be horizontally positioned as {horizontalName}");
        }
    }

    private static async Task<JsonDocument> LoadContractDocumentAsync()
        => await GetApiJsonAsync("api/document-editor/documents/contract-demo");

    private static HttpClient CreateApiClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        return new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5100") };
    }

    private static async Task<DocumentEditorDocument?> LoadDocumentAsync(HttpClient http, string documentId)
    {
        var result = await http.GetFromJsonAsync<DocumentEditorLoadResult>(
            $"/api/document-editor/{Uri.EscapeDataString(documentId)}",
            ApiJsonOptions);
        return result?.Document;
    }

    private static async Task SaveDocumentAsync(HttpClient http, DocumentEditorDocument document)
    {
        var response = await http.PutAsJsonAsync(
            $"/api/document-editor/{Uri.EscapeDataString(document.DocumentId)}",
            new DocumentEditorSaveRequest
            {
                DocumentId = document.DocumentId,
                Document = document,
                ConcurrencyMode = DocumentEditorConcurrencyMode.Force
            },
            ApiJsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static DocumentDrawingRun FindDrawing(DocumentEditorDocument document, string objectId)
        => DocumentImagePersistence.EnumerateDrawingRuns(document)
               .SingleOrDefault(drawing => string.Equals(drawing.ObjectId, objectId, StringComparison.Ordinal))
           ?? throw new AssertFailedException($"Expected drawing '{objectId}' was not found.");

    private static async Task<JsonDocument> GetApiJsonAsync(string path)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        var response = await http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    private static JsonElement FindBlock(IEnumerable<JsonElement> blocks, string id)
        => blocks.FirstOrDefault(block => GetString(block, "Id") == id);

    private static bool IsImageBlock(JsonElement block)
    {
        if (!TryGetProperty(block, "Type", out var type))
        {
            return false;
        }

        return IsEnum(type, "Image", 5);
    }

    private static IEnumerable<JsonElement> GetArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray();
    }

    private static JsonElement GetRequired(JsonElement element, string propertyName)
    {
        if (TryGetProperty(element, propertyName, out var value))
        {
            return value;
        }

        throw new AssertFailedException($"Expected JSON property '{propertyName}' was not found.");
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var camel = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
            return element.TryGetProperty(camel, out value);
        }

        value = default;
        return false;
    }

    private static bool IsEnum(JsonElement element, string stringValue, int numericValue)
        => element.ValueKind switch
        {
            JsonValueKind.String => string.Equals(element.GetString(), stringValue, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => element.TryGetInt32(out var value) && value == numericValue,
            _ => false
        };

    private static string Canonicalize(JsonElement element)
        => JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = false });

    private static string NormalizeWhitespace(string value)
        => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string ExtractPlainText(IEnumerable<JsonElement> blocks)
    {
        var chunks = new List<string>();
        foreach (var block in blocks)
        {
            if (!TryGetProperty(block, "Content", out var content))
            {
                continue;
            }

            foreach (var inline in GetArray(content, "Inlines"))
            {
                var text = GetString(inline, "Text") ?? GetString(inline, "DisplayName");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    chunks.Add(text);
                }
            }
        }

        return string.Join(" ", chunks);
    }

    private sealed record Phase19ImageFootprintProbe(
        string ObjectId,
        DocumentEditorRectProbe ImageRect,
        DocumentEditorLineIntervalProbe[] LineIntervals,
        Phase19ImageLayerProbe Layer,
        string Debug);

    private sealed class Phase19ImageLayerProbe
    {
        [JsonPropertyName("objectId")] public string ObjectId { get; set; } = string.Empty;
        [JsonPropertyName("objectLayer")] public string ObjectLayer { get; set; } = string.Empty;
        [JsonPropertyName("wrapMode")] public string WrapMode { get; set; } = string.Empty;
        [JsonPropertyName("flowReservationCount")] public int FlowReservationCount { get; set; }
        [JsonPropertyName("visible")] public bool Visible { get; set; }
        [JsonPropertyName("imageLoaded")] public bool ImageLoaded { get; set; }
        [JsonPropertyName("rect")] public DocumentEditorRectProbe Rect { get; set; } = new();
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }

    private sealed class Phase19DrawingEditState
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("objectId")] public string ObjectId { get; set; } = string.Empty;
        [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
        [JsonPropertyName("wrapMode")] public string WrapMode { get; set; } = string.Empty;
        [JsonPropertyName("horizontalAlignment")] public string HorizontalAlignment { get; set; } = string.Empty;
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("width")] public double Width { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
        [JsonPropertyName("undoDepth")] public int UndoDepth { get; set; }
        [JsonPropertyName("redoDepth")] public int RedoDepth { get; set; }
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }
}
