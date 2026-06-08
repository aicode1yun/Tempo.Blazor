using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.E2E.CanvasEngine;

namespace Tempo.Blazor.E2E;

/// <summary>Phase 2 shared canvas document editor visual harness tests.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:CanvasEngine")]
[DoNotParallelize]
public sealed class DocumentEditorCanvasHarnessE2ETests : CanvasEngineTestBase
{
    /// <summary>Viewport matrix for the shared canvas visual harness.</summary>
    public static IEnumerable<object[]> Viewports
        => CanvasViewports.Select(viewport => new object[] { viewport });

    [DataTestMethod]
    [DynamicData(nameof(Viewports))]
    public async Task Phase2_OpenCanvasEngineDocument_CapturesScreenshotsManifestAndPassesSmokeGates(CanvasEngineViewport viewport)
    {
        var canvasPage = await OpenCanvasEngineDocumentAsync("phase-2-empty-a4", viewport);
        var manifest = canvasPage.CreateManifest(
            nameof(DocumentEditorCanvasHarnessE2ETests),
            nameof(Phase2_OpenCanvasEngineDocument_CapturesScreenshotsManifestAndPassesSmokeGates));

        manifest.UserActions.Add("Open the canvas document engine harness for the requested seed document.");
        manifest.UserActions.Add("Capture before and after screenshots around a hidden input focus action.");
        manifest.UserActions.Add("Read canvas backing-store metrics and validate the shared visual gate contract.");
        manifest.ExpectedVisibleChanges = "A centered A4-like page is visible on a quiet document workspace across the viewport matrix.";
        manifest.ExpectedModelChanges = "The seed document remains unchanged; focus is routed through the hidden input bridge.";

        manifest.ScreenshotPaths.Add(await canvasPage.CaptureFullAsync("00-before-full.png"));
        manifest.ScreenshotPaths.Add(await canvasPage.CaptureEditorAsync("01-before-editor.png"));

        await canvasPage.FocusHiddenInputAsync();

        manifest.ScreenshotPaths.Add(await canvasPage.CaptureFullAsync("02-after-full.png"));
        manifest.ScreenshotPaths.Add(await canvasPage.CaptureEditorAsync("03-after-editor.png"));
        manifest.ScreenshotPaths.Add(await canvasPage.CaptureCanvasCropAsync("04-canvas-crop.png", await canvasPage.GetPageSurfaceClipAsync()));
        manifest.ScreenshotPaths.Add(await canvasPage.CaptureControlAsync("05-focused-control.png", "[data-testid='document-canvas-engine-root']"));

        var metrics = await DocumentEditorCanvasVisualAssert.AssertCanvasNonBlankAsync(canvasPage.Canvas);
        manifest.Metrics["canvas"] = metrics;
        manifest.Metrics["viewportDevicePixelRatio"] = await canvasPage.Page.EvaluateAsync<double>("() => window.devicePixelRatio || 1");

        await DocumentEditorCanvasVisualAssert.AssertNoTextOverlapAsync(canvasPage.Page);
        await DocumentEditorCanvasVisualAssert.AssertNoUiOverlapAsync(canvasPage.Page);
        await DocumentEditorCanvasVisualAssert.AssertToolbarStateMatchesModelAsync(canvasPage.Page);
        await DocumentEditorCanvasVisualAssert.AssertScreenshotLooksIntentionalAsync(
            manifest,
            "The after screenshot reads as an intentional blank document surface with stable margins, sharp page edges, and no overlapping UI.");

        await canvasPage.WriteManifestAsync(manifest);
    }

    [TestMethod]
    public async Task Phase2_VisualAssertsRejectBrokenRenderAndAcceptInstrumentedGreenCases()
    {
        var viewport = CanvasViewports[0];
        var canvasPage = await OpenCanvasEngineDocumentAsync("phase-2-assert-contract", viewport);

        await InstallAssertionFixturesAsync(canvasPage.Page);

        await Assert.ThrowsExceptionAsync<AssertFailedException>(
            () => DocumentEditorCanvasVisualAssert.AssertCanvasNonBlankAsync(canvasPage.Page.Locator("[data-testid='blank-canvas-red-gate']")),
            "A transparent canvas must fail the non-blank gate.");
        await DocumentEditorCanvasVisualAssert.AssertCanvasNonBlankAsync(canvasPage.Page.Locator("[data-testid='painted-canvas-green-gate']"));

        await Assert.ThrowsExceptionAsync<AssertFailedException>(
            () => DocumentEditorCanvasVisualAssert.AssertTextPixelsChangedAsync(canvasPage.Page, "[data-testid='before-canvas']", "[data-testid='before-canvas']"),
            "Identical canvas frames must fail the changed-pixels gate.");
        await DocumentEditorCanvasVisualAssert.AssertTextPixelsChangedAsync(canvasPage.Page, "[data-testid='before-canvas']", "[data-testid='after-canvas']");

        await Assert.ThrowsExceptionAsync<AssertFailedException>(
            () => DocumentEditorCanvasVisualAssert.AssertCaretVisibleAsync(canvasPage.Page.GetByTestId("hidden-caret-red-gate")),
            "A hidden caret must fail the caret visibility gate.");
        await DocumentEditorCanvasVisualAssert.AssertCaretVisibleAsync(canvasPage.Page.GetByTestId("visible-caret-green-gate"));

        await Assert.ThrowsExceptionAsync<AssertFailedException>(
            () => DocumentEditorCanvasVisualAssert.AssertSelectionVisibleAsync(canvasPage.Page.GetByTestId("hidden-selection-red-gate")),
            "A hidden selection must fail the selection visibility gate.");
        await DocumentEditorCanvasVisualAssert.AssertSelectionVisibleAsync(canvasPage.Page.GetByTestId("visible-selection-green-gate"));

        await Assert.ThrowsExceptionAsync<AssertFailedException>(
            () => DocumentEditorCanvasVisualAssert.AssertNoTextOverlapAsync(canvasPage.Page, "[data-red-text-overlap]"),
            "Overlapping text rectangles must fail the text overlap gate.");
        await DocumentEditorCanvasVisualAssert.AssertNoTextOverlapAsync(canvasPage.Page, "[data-green-text-overlap]");

        await Assert.ThrowsExceptionAsync<AssertFailedException>(
            () => DocumentEditorCanvasVisualAssert.AssertNoUiOverlapAsync(canvasPage.Page, "[data-red-ui-overlap]"),
            "Overlapping UI rectangles must fail the UI overlap gate.");
        await DocumentEditorCanvasVisualAssert.AssertNoUiOverlapAsync(canvasPage.Page, "[data-green-ui-overlap]");

        await Assert.ThrowsExceptionAsync<AssertFailedException>(
            () => DocumentEditorCanvasVisualAssert.AssertToolbarStateMatchesModelAsync(canvasPage.Page, new Dictionary<string, bool> { ["bold"] = false, ["italic"] = true }),
            "Toolbar pressed states must fail when they diverge from the model.");
        await DocumentEditorCanvasVisualAssert.AssertToolbarStateMatchesModelAsync(canvasPage.Page, new Dictionary<string, bool> { ["bold"] = true, ["italic"] = false });
    }

    private static Task InstallAssertionFixturesAsync(IPage page)
        => page.EvaluateAsync(
            """
            () => {
                const fixture = document.createElement('section');
                fixture.setAttribute('data-testid', 'canvas-assertion-fixtures');
                fixture.style.cssText = 'position:fixed;left:8px;top:8px;width:460px;height:360px;pointer-events:none;opacity:0.01;z-index:1;';
                fixture.innerHTML = `
                    <canvas data-testid="blank-canvas-red-gate" width="120" height="80"></canvas>
                    <canvas data-testid="painted-canvas-green-gate" width="120" height="80"></canvas>
                    <canvas data-testid="before-canvas" width="120" height="80"></canvas>
                    <canvas data-testid="after-canvas" width="120" height="80"></canvas>
                    <span data-testid="hidden-caret-red-gate" style="display:none;width:2px;height:24px;"></span>
                    <span data-testid="visible-caret-green-gate" style="position:absolute;left:130px;top:12px;width:2px;height:24px;background:#111;"></span>
                    <span data-testid="hidden-selection-red-gate" style="visibility:hidden;width:60px;height:24px;"></span>
                    <span data-testid="visible-selection-green-gate" style="position:absolute;left:140px;top:48px;width:68px;height:22px;background:#7db7ff;"></span>
                    <span data-red-text-overlap data-id="red-text-a" style="position:absolute;left:10px;top:130px;width:80px;height:22px;"></span>
                    <span data-red-text-overlap data-id="red-text-b" style="position:absolute;left:60px;top:138px;width:80px;height:22px;"></span>
                    <span data-green-text-overlap data-id="green-text-a" style="position:absolute;left:10px;top:180px;width:80px;height:22px;"></span>
                    <span data-green-text-overlap data-id="green-text-b" style="position:absolute;left:100px;top:180px;width:80px;height:22px;"></span>
                    <span data-red-ui-overlap data-id="red-ui-a" style="position:absolute;left:230px;top:130px;width:90px;height:32px;"></span>
                    <span data-red-ui-overlap data-id="red-ui-b" style="position:absolute;left:300px;top:145px;width:90px;height:32px;"></span>
                    <span data-green-ui-overlap data-id="green-ui-a" style="position:absolute;left:230px;top:190px;width:90px;height:32px;"></span>
                    <span data-green-ui-overlap data-id="green-ui-b" style="position:absolute;left:330px;top:190px;width:90px;height:32px;"></span>
                    <button data-canvas-toolbar-command="bold" aria-pressed="true" type="button">B</button>
                    <button data-canvas-toolbar-command="italic" aria-pressed="false" type="button">I</button>
                `;
                document.body.appendChild(fixture);

                const painted = fixture.querySelector('[data-testid="painted-canvas-green-gate"]').getContext('2d');
                painted.fillStyle = '#ffffff';
                painted.fillRect(0, 0, 120, 80);
                painted.strokeStyle = '#111827';
                painted.strokeRect(8, 8, 104, 64);

                const before = fixture.querySelector('[data-testid="before-canvas"]').getContext('2d');
                before.fillStyle = '#ffffff';
                before.fillRect(0, 0, 120, 80);

                const after = fixture.querySelector('[data-testid="after-canvas"]').getContext('2d');
                after.fillStyle = '#ffffff';
                after.fillRect(0, 0, 120, 80);
                after.fillStyle = '#111827';
                after.fillRect(24, 24, 48, 14);
            }
            """);
}
