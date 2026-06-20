using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>E2E and screenshot checks for modeling BPMN notation profile phase M17.</summary>
[TestClass]
[TestCategory("WASM")]
public sealed class ModelingBpmnProfileM17E2ETests : WasmTestBase
{
    private const string ModelingEditorUrl = "/modeling-editor";

    [TestMethod]
    [Description("Selecting BPMN 2.0 notation shows BPMN element types in the model tree")]
    public async Task BpmnNotationSelectionShowsBpmnElementTypes()
    {
        var page = await OpenLoadedModelingPageAsync("?notation=bpmn2&viewpoint=Process&scenario=bpmn2-generated");

        await Assertions.Expect(page.Locator("[data-testid='modeling-view-selector']")).ToHaveAttributeAsync("data-notation", "bpmn2");
        await Assertions.Expect(page.Locator("[data-testid='modeling-tree-node-bpmn2-validate']")).ToHaveAttributeAsync("data-semantic-type", "UserTask");
        await Assertions.Expect(page.Locator("[data-testid='modeling-tree-node-bpmn2-decision']")).ToHaveAttributeAsync("data-semantic-type", "ExclusiveGateway");
    }

    [TestMethod]
    [Description("BPMN model generation uses BPMN stencils instead of generic shapes")]
    public async Task BpmnGenerationUsesBpmnStencils()
    {
        var page = await OpenLoadedModelingPageAsync("?notation=bpmn2&viewpoint=Process&scenario=bpmn2-generated");

        await ExpectVisibleAsync(page, "[data-testid='modeling-diagram-preview'] g.tm-diagram-node[data-stencil-id='bpmn2.pool']");
        await ExpectVisibleAsync(page, "[data-testid='modeling-diagram-preview'] g.tm-diagram-node[data-stencil-id='bpmn2.event.start']");
        await ExpectVisibleAsync(page, "[data-testid='modeling-diagram-preview'] g.tm-diagram-node[data-stencil-id='bpmn2.task.user']");
        await ExpectVisibleAsync(page, "[data-testid='modeling-diagram-preview'] g.tm-diagram-node[data-stencil-id='bpmn2.gateway.exclusive']");
        await ExpectVisibleAsync(page, "[data-testid='modeling-diagram-preview'] g.tm-diagram-node[data-stencil-id='bpmn2.event.end']");
        Assert.AreEqual(0, await page.Locator("[data-testid='modeling-diagram-preview'] g.tm-diagram-node[data-model-element-id='bpmn2-validate'][data-stencil-id='general.rectangle']").CountAsync());

        await TakeScreenshotAsync(page, "bpmn-diagram-generated");
        await SaveStableScreenshotAsync(page, "bpmn-diagram-generated.png");
    }

    [TestMethod]
    [Description("BPMN SequenceFlow across Pool boundary is reported and the invalid edge is skipped")]
    public async Task BpmnCrossPoolSequenceFlowShowsWarningAndSkipsEdge()
    {
        var page = await OpenLoadedModelingPageAsync("?notation=bpmn2&viewpoint=Process&scenario=bpmn2-cross-pool");

        await WaitForIssueCountAtLeastAsync(page, 1);
        await Assertions.Expect(page.Locator("[data-testid='modeling-issue-list']")).ToContainTextAsync("SequenceFlow cannot cross Pool");
        Assert.AreEqual(0, await page.Locator("[data-testid='modeling-diagram-preview'] [data-edge-id='bpmn2-flow-cross-pool']").CountAsync());

        await TakeScreenshotAsync(page, "bpmn-issue-cross-pool");
        await SaveStableScreenshotAsync(page, "bpmn-issue-cross-pool.png");
    }

    [TestMethod]
    [Description("Unknown BPMN task type reports a warning and falls back to a generic node stencil")]
    public async Task BpmnUnknownAiTaskFallsBackWithWarning()
    {
        var page = await OpenLoadedModelingPageAsync("?notation=bpmn2&viewpoint=Process&scenario=bpmn2-ai-task");

        await ExpectVisibleAsync(page, "[data-testid='modeling-diagram-preview'] g.tm-diagram-node[data-model-element-id='bpmn2-ai-task'][data-stencil-id='general.rectangle']");
        await WaitForIssueCountAtLeastAsync(page, 1);
        await Assertions.Expect(page.Locator("[data-testid='modeling-issue-list']")).ToContainTextAsync("AiTask");
    }

    private async Task<IPage> OpenLoadedModelingPageAsync(string query = "")
    {
        var context = await CreateContextAsync();
        await context.AddInitScriptAsync("localStorage.setItem('tm-demo-culture', 'en');");
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}{ModelingEditorUrl}{query}", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForAppReadyAsync(page);
        await page.Locator("[data-testid='modeling-editor'][data-state='loaded']")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await page.Locator("[data-testid='modeling-diagram-preview']")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        return page;
    }

    private static async Task ExpectVisibleAsync(IPage page, string selector, int timeout = 10000)
    {
        await page.Locator(selector).First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeout });
    }

    private static Task WaitForIssueCountAtLeastAsync(IPage page, int minimumCount) =>
        page.WaitForFunctionAsync(
            """
            minimumCount => {
                const editor = document.querySelector("[data-testid='modeling-editor']");
                return Number(editor?.getAttribute('data-issue-count') ?? '0') >= minimumCount;
            }
            """,
            minimumCount,
            new PageWaitForFunctionOptions { Timeout = 10000 });

    private static async Task SaveStableScreenshotAsync(IPage page, string fileName)
    {
        var directory = Path.Combine(
            Path.GetDirectoryName(typeof(ModelingBpmnProfileM17E2ETests).Assembly.Location)!,
            "..",
            "..",
            "..",
            "TestResults",
            "modeling-m17");
        Directory.CreateDirectory(directory);

        var bytes = await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Type = ScreenshotType.Png,
            FullPage = true
        });

        Assert.IsTrue(bytes.Length > 20_000, $"{fileName} screenshot should contain the BPMN modeling editor.");
        await File.WriteAllBytesAsync(Path.Combine(directory, fileName), bytes);
    }
}
