using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Smoke coverage for the shared Notion E2E infrastructure.
/// </summary>
[TestClass]
public sealed class NotionInfraSmokeE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [Description("Opening the Notion editor through the shared helper renders the editor shell and stores baseline screenshots")]
    public async Task OpenNotionEditor_ShowsEditorAndCapturesBaseline()
    {
        var page = await OpenNotionEditorAsync();

        var editor = page.Locator(".tm-notion-editor").First;
        Assert.IsTrue(await editor.IsVisibleAsync(), "The Notion editor shell should be visible.");

        var capture = await CaptureBaselineAsync("infra", "smoke-editor");
        Assert.IsTrue(File.Exists(capture.FullPagePath), $"Full-page baseline was not written: {capture.FullPagePath}");
        Assert.IsTrue(File.Exists(capture.RegionPath), $"Region baseline was not written: {capture.RegionPath}");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Deterministic rich seed can be captured for desktop and mobile UX review")]
    public async Task Baseline_RichSeed_DesktopAndMobile()
    {
        await SetViewportAsync(1280, 720);
        var desktop = await OpenNotionEditorAsync();
        await SeedRichPageAsync();
        var desktopCapture = await CaptureBaselineAsync("infra", "rich-desktop");

        Assert.IsTrue(File.Exists(desktopCapture.FullPagePath), "Desktop full-page baseline should be written.");
        Assert.IsTrue(File.Exists(desktopCapture.RegionPath), "Desktop editor-region baseline should be written.");

        await SetViewportAsync(390, 844);
        var mobile = await OpenNotionEditorAsync();
        await SeedRichPageAsync();
        var mobileCapture = await CaptureBaselineAsync("infra", "rich-mobile");

        Assert.IsTrue(File.Exists(mobileCapture.FullPagePath), "Mobile full-page baseline should be written.");
        Assert.IsTrue(File.Exists(mobileCapture.RegionPath), "Mobile editor-region baseline should be written.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Deterministic empty seed renders a stable blank editing state")]
    public async Task Baseline_EmptySeed_CapturesBlankEditorState()
    {
        var page = await OpenNotionEditorAsync();
        await SeedEmptyPageAsync();

        var emptyBlock = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        await emptyBlock.WaitForAsync();

        var capture = await CaptureBaselineAsync("infra", "empty-desktop");
        Assert.IsTrue(File.Exists(capture.FullPagePath), "Empty full-page baseline should be written.");
        Assert.IsTrue(File.Exists(capture.RegionPath), "Empty editor-region baseline should be written.");
    }
}
