using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionPageReactionsE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF17: Page reactions support like/unlike, emoji toggles, providerless hidden state, empty state, many reactions, and capture a UX baseline.")]
    public async Task PageReactions_LikeEmojiAndEdges()
    {
        var page = await OpenNotionEditorAsync();
        await SeedPageReactionsEmptyPageAsync();

        var reactions = page.Locator(".tm-page-reactions").First;
        await reactions.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var likeButton = reactions.Locator(".tm-page-reactions__like").First;
        var likeCount = reactions.Locator(".tm-page-reactions__like-count").First;
        Assert.AreEqual("0", (await likeCount.TextContentAsync())?.Trim());

        await likeButton.ClickAsync();
        await ExpectTextAsync(likeCount, "1");
        Assert.AreEqual("true", await likeButton.GetAttributeAsync("aria-pressed"));

        await likeButton.ClickAsync();
        await ExpectTextAsync(likeCount, "0");
        Assert.AreEqual("false", await likeButton.GetAttributeAsync("aria-pressed"));

        await reactions.Locator(".tm-page-reactions__add").First.ClickAsync();
        await reactions.Locator(".tm-page-reactions__choice").Filter(new() { HasText = "🎉" }).ClickAsync();
        var celebration = reactions.Locator(".tm-page-reactions__pill[data-reaction='🎉']").First;
        await celebration.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await ExpectTextAsync(celebration.Locator(".tm-page-reactions__pill-count"), "1");
        Assert.AreEqual("true", await celebration.GetAttributeAsync("aria-pressed"));

        var capture = await CaptureBaselineAsync("page-reactions", "cf17-page-reactions-baseline", reactions);
        TestContext.WriteLine($"UX CF17 baseline captured: {capture.FullPagePath} / {capture.RegionPath}");

        var providerlessPage = await OpenNotionEditorAsync("?disableReactionProvider=true");
        await SeedPageReactionsEmptyPageAsync();
        Assert.AreEqual(0, await providerlessPage.Locator(".tm-page-reactions").CountAsync());

        var manyPage = await OpenNotionEditorAsync();
        await SeedPageReactionsManyPageAsync();
        var manyReactions = manyPage.Locator(".tm-page-reactions").First;
        await manyReactions.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.IsTrue(await manyReactions.Locator(".tm-page-reactions__pill").CountAsync() >= 5);
        Assert.IsTrue(await manyReactions.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Page reactions should not overflow horizontally.");
    }

    private static async Task ExpectTextAsync(ILocator locator, string expected)
    {
        await locator.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (string.Equals((await locator.InnerTextAsync()).Trim(), expected, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(150);
        }

        Assert.AreEqual(expected, (await locator.InnerTextAsync()).Trim());
    }
}
