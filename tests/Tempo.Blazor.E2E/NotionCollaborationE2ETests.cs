using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// EB14 coverage for Notion editor collaboration presence and cursor overlays.
/// </summary>
[TestClass]
public sealed class NotionCollaborationE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("EB14: zero collaborators keeps presence avatars and block cursor overlays hidden")]
    public async Task EB14_Collaboration_NoCollaborators_HidesPresence()
    {
        var page = await OpenNotionEditorAsync();
        await SeedCollaborationPageAsync();
        await ShowCollaborationNoUsersAsync();
        await WaitForCollaborationStateAsync(page, 0, 0);

        Assert.AreEqual(0, await page.Locator(".tm-collab-avatar").CountAsync(), "Presence avatar bar should not render without remote collaborators.");
        Assert.AreEqual(0, await page.Locator(".tm-collab-active").CountAsync(), "No block should be marked with a remote cursor.");

        var capture = await CaptureBaselineAsync("collaboration", "zero-collaborators", EditorRegion(page));
        Assert.IsTrue(File.Exists(capture.FullPagePath), "Zero-collaborator full-page baseline should be written.");
        Assert.IsTrue(File.Exists(capture.RegionPath), "Zero-collaborator editor baseline should be written.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("EB14: one and several collaborators render readable presence avatars and cursor labels")]
    public async Task EB14_Collaboration_OneAndManyCursors_RenderReadablePresence()
    {
        var page = await OpenNotionEditorAsync();
        await SeedCollaborationPageAsync();

        await ShowCollaborationOneCursorAsync();
        await WaitForCollaborationStateAsync(page, 1, 1);
        var oneLabel = page.Locator("[data-block-id='eb140000-0000-0000-0000-000000000002']").First;
        Assert.AreEqual("Ada Lovelace", await oneLabel.GetAttributeAsync("data-collab-user"));
        Assert.AreEqual("1", await oneLabel.GetAttributeAsync("data-collab-count"));
        var oneCapture = await CaptureBaselineAsync("collaboration", "one-cursor", EditorRegion(page));
        Assert.IsTrue(File.Exists(oneCapture.RegionPath), "Single-cursor editor baseline should be written.");

        await ShowCollaborationManyCursorsAsync();
        await WaitForCollaborationStateAsync(page, 3, 3);
        var avatarTitles = await page.Locator(".tm-collab-avatar").EvaluateAllAsync<string[]>(
            "els => els.map(el => el.getAttribute('title')).filter(Boolean)");
        CollectionAssert.Contains(avatarTitles, "Ada Lovelace");
        CollectionAssert.Contains(avatarTitles, "Ben Carter");
        CollectionAssert.Contains(avatarTitles, "Camila Reyes");
        var manyCapture = await CaptureBaselineAsync("collaboration", "many-cursors", EditorRegion(page));
        Assert.IsTrue(File.Exists(manyCapture.RegionPath), "Multi-cursor editor baseline should be written.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("EB14: long collaborator names and overlapping cursors stay legible")]
    public async Task EB14_Collaboration_LongNamesAndOverlap_StayLegible()
    {
        var page = await OpenNotionEditorAsync();
        await SeedCollaborationPageAsync();

        await ShowCollaborationLongNamesAsync();
        await WaitForCollaborationStateAsync(page, 2, 2);
        var longNameAvatar = page.Locator(".tm-collab-avatar[title='Alexandria Catherine Montgomery-Smythe']").First;
        await longNameAvatar.HoverAsync();
        var longLabel = page.Locator("[data-block-id='eb140000-0000-0000-0000-000000000003']").First;
        Assert.AreEqual("Alexandria Catherine Montgomery-Smythe", await longLabel.GetAttributeAsync("data-collab-user"));
        var longNameCapture = await CaptureBaselineAsync("collaboration", "long-names", EditorRegion(page));
        Assert.IsTrue(File.Exists(longNameCapture.RegionPath), "Long-name editor baseline should be written.");

        await ShowCollaborationOverlappingCursorsAsync();
        await WaitForCollaborationStateAsync(page, 3, 1);
        var overlapBlock = page.Locator("[data-block-id='eb140000-0000-0000-0000-000000000004']").First;
        var overlapNames = await overlapBlock.GetAttributeAsync("data-collab-user");
        Assert.IsTrue(await overlapBlock.EvaluateAsync<bool>("el => el.classList.contains('tm-collab-active--overlap')"), "Overlap class should be applied to blocks with several remote cursors.");
        StringAssert.Contains(overlapNames, "Morgan Lee");
        StringAssert.Contains(overlapNames, "Priya Shah");
        StringAssert.Contains(overlapNames, "Tomas Urban");

        var overlapCapture = await CaptureBaselineAsync("collaboration", "overlapping-cursors", EditorRegion(page));
        Assert.IsTrue(File.Exists(overlapCapture.RegionPath), "Overlapping-cursor editor baseline should be written.");
    }

    private static ILocator EditorRegion(IPage page) => page.Locator(".tm-notion-editor").First;

    private static async Task WaitForCollaborationStateAsync(IPage page, int avatarCount, int activeBlockCount)
    {
        await page.WaitForFunctionAsync(
            """
            expected => {
                const [avatars, activeBlocks] = expected;
                return document.querySelectorAll('.tm-collab-avatar').length === avatars &&
                       document.querySelectorAll('.tm-collab-active').length === activeBlocks;
            }
            """,
            new[] { avatarCount, activeBlockCount },
            new PageWaitForFunctionOptions { Timeout = 10000 });
    }
}
