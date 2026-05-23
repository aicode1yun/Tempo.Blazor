using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for the reusable human-like document editor E2E platform.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase18E2ETests : DocumentEditorE2ETestBase
{
    private const string EngineHostSelector = "[data-testid='phase18-engine-host']";
    private const string UiHostSelector = "[data-testid='phase18-ui-host']";

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_HumanLikeHelpersCaptureInputFramesAndScreenshotArtifacts()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);
        using var console = BeginDocumentEditorConsoleCapture(page);
        await InstallStrictEngineSandboxAsync(page);

        var clickedLine = await ClickDocumentEditorVisualLineAsync(page, lineIndex: 0, xRatio: 0.9, EngineHostSelector);
        await DragDocumentEditorTextSelectionAsync(page, fromLineIndex: 0, toLineIndex: 0, EngineHostSelector);
        await ClickDocumentEditorVisualLineAsync(page, lineIndex: 0, xRatio: 0.95, EngineHostSelector);

        var probes = await TypeDocumentEditorTextByCharactersWithFrameProbesAsync(
            page,
            " ok",
            "Human typing in a visual line must keep the editor stable",
            EngineHostSelector);

        var screenshotPath = await CaptureDocumentEditorPageScreenshotAsync(page, "phase18_human_like_helpers", EngineHostSelector);
        var finalProbe = probes.Last();

        clickedLine.BlockId.Should().Be("p1");
        probes.Should().Contain(probe => probe.Stage.Contains("after animation frame", StringComparison.Ordinal));
        probes.Should().Contain(probe => probe.Stage.Contains("after 50 ms", StringComparison.Ordinal));
        probes.Should().Contain(probe => probe.Stage.Contains("after 150 ms", StringComparison.Ordinal));
        probes.Should().Contain(probe => probe.Stage.Contains("after idle layout", StringComparison.Ordinal));
        finalProbe.TextRectCount.Should().BeGreaterThan(0);
        finalProbe.CaretInsideActivePageBody.Should().BeTrue();
        finalProbe.Issues.Should().BeEmpty("human-like typing helper must fail the test on any dirty frame probe");
        File.Exists(screenshotPath).Should().BeTrue();
        console.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_ToolbarContextMenuImageDragAndVisualAssertionsAreReusable()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);
        await InstallStrictUiSandboxAsync(page);

        await ClickDocumentEditorVisualLineAsync(page, lineIndex: 0, xRatio: 0.5, UiHostSelector);
        await ClickDocumentEditorToolbarCommandAsync(page, "phase18-toolbar-command");
        await ExecuteDocumentEditorContextMenuCommandAsync(page, "[data-testid='phase18-context-target']", "phase18-context-command");
        await DragDocumentEditorImageResizeAsync(page, deltaX: 24, deltaY: 18, hostSelector: UiHostSelector);

        var probeSequence = await RunDocumentEditorActionWithFrameProbesAsync(
            page,
            "Toolbar command and context menu command must leave readable UI",
            () => Task.CompletedTask,
            UiHostSelector);
        await AssertStrictFrameProbesCleanAsync(page, probeSequence, "Toolbar command and context menu command must leave readable UI", UiHostSelector);

        var state = await page.EvaluateAsync<Phase18UiState>(
            """
            () => ({
                toolbarClicked: document.body.dataset.phase18ToolbarClicked === 'true',
                contextClicked: document.body.dataset.phase18ContextClicked === 'true',
                imageResizeStarted: document.body.dataset.phase18ImageResizeStarted === 'true',
                imageResizeFinished: document.body.dataset.phase18ImageResizeFinished === 'true'
            })
            """);
        var finalProbe = probeSequence.Last();

        state.ToolbarClicked.Should().BeTrue();
        state.ContextClicked.Should().BeTrue();
        state.ImageResizeStarted.Should().BeTrue();
        state.ImageResizeFinished.Should().BeTrue();
        finalProbe.TextTextOverlapCount.Should().Be(0);
        finalProbe.TextImageOverlapCount.Should().Be(0);
        finalProbe.TextCaptionOverlapCount.Should().Be(0);
        finalProbe.ToolbarOverlapCount.Should().Be(0);
        finalProbe.SidePanelClippingCount.Should().Be(0);
        finalProbe.ContextMenuVisible.Should().BeFalse();
        finalProbe.CaretInsideActivePageBody.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_FailureMessagesContainHumanBehaviorScreenshotAndJsonArtifact()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);
        await InstallStrictUiSandboxAsync(page);

        var probe = await CaptureStrictFrameProbeAsync(page, "phase18 forced failure", UiHostSelector);
        probe.Issues = ["text/image overlap: p1 -> img1"];
        var artifact = await CaptureStrictFailureArtifactsAsync(
            page,
            "Human should be able to read wrapped text without overlap",
            probe,
            UiHostSelector);
        var message = CreateStrictEngineFailureMessage(
            "Human should be able to read wrapped text without overlap",
            probe,
            artifact);

        File.Exists(artifact.ScreenshotPath).Should().BeTrue();
        File.Exists(artifact.JsonArtifactPath).Should().BeTrue();
        message.Should().Contain("Human should be able to read wrapped text without overlap");
        message.Should().Contain("Screenshot:");
        message.Should().Contain(".png");
        message.Should().Contain("JSON artifact:");
        message.Should().Contain(".json");
    }

    private static Task InstallStrictEngineSandboxAsync(IPage page)
        => page.EvaluateAsync(
            """
            () => {
                document.body.innerHTML = '<main data-testid="phase18-root"></main>';
                const root = document.querySelector('[data-testid="phase18-root"]');
                const host = document.createElement('div');
                host.setAttribute('data-testid', 'phase18-engine-host');
                host.style.cssText = 'position:fixed;left:40px;top:40px;width:760px;min-height:360px;background:#fff;color:#111827;z-index:10;padding:24px;border:1px solid #d1d5db;';
                root.appendChild(host);
                const id = window.tmDocumentEditorEngine.create(host, { instanceId: 'phase18-engine-sandbox' });
                window.tmDocumentEditorEngine.loadDocument(id, {
                    DocumentId: 'phase18-engine-doc',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha beta gamma delta.' }] } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'Second clean line for drag selection.' }] } }
                    ]
                });
            }
            """);

    private static Task InstallStrictUiSandboxAsync(IPage page)
        => page.EvaluateAsync(
            """
            () => {
                document.body.innerHTML = `
                    <main data-testid="phase18-ui-host" style="position:fixed;left:40px;top:40px;width:820px;min-height:420px;background:#fff;color:#111827;z-index:10;padding:24px;border:1px solid #d1d5db;">
                        <div class="tm-wysiwyg-page">
                            <div class="tm-wysiwyg-page__body" contenteditable="true" style="position:relative;width:720px;min-height:260px;padding:24px;line-height:24px;border:1px solid #e5e7eb;">
                                <p data-block-id="p1" style="margin:0 0 24px 0;">Readable text line for strict visual helpers.</p>
                                <figure data-testid="phase18-image" data-block-id="img1" style="position:relative;margin:18px 0 0 0;width:180px;height:104px;border:1px solid #93c5fd;background:#eff6ff;">
                                    <div style="width:180px;height:72px;background:#bfdbfe;"></div>
                                    <figcaption style="height:24px;line-height:24px;text-align:center;">Clean caption</figcaption>
                                    <span data-resize-handle="se" data-testid="phase18-image-resize-handle-se" style="position:absolute;right:-5px;bottom:-5px;width:10px;height:10px;background:#2563eb;"></span>
                                </figure>
                            </div>
                        </div>
                        <div data-testid="phase18-toolbar" style="position:absolute;left:24px;top:340px;height:36px;">
                            <button data-testid="phase18-toolbar-command" type="button">Bold</button>
                        </div>
                        <button data-testid="phase18-context-target" type="button" style="position:absolute;left:110px;top:340px;">Context target</button>
                        <aside data-testid="phase18-side-panel" style="position:absolute;left:560px;top:300px;width:220px;height:70px;overflow:visible;border:1px solid #e5e7eb;">
                            <button data-testid="phase18-side-action" style="margin:12px;">Side action</button>
                        </aside>
                    </main>`;
                document.querySelector('[data-testid="phase18-toolbar-command"]').addEventListener('click', () => {
                    document.body.dataset.phase18ToolbarClicked = 'true';
                });
                document.querySelector('[data-testid="phase18-context-target"]').addEventListener('contextmenu', event => {
                    event.preventDefault();
                    document.querySelector('[data-testid="phase18-context-menu"]')?.remove();
                    const menu = document.createElement('div');
                    menu.setAttribute('role', 'menu');
                    menu.setAttribute('data-testid', 'phase18-context-menu');
                    menu.style.cssText = `position:fixed;left:${event.clientX}px;top:${event.clientY}px;background:#fff;border:1px solid #d1d5db;padding:4px;z-index:9999;`;
                    menu.innerHTML = '<button data-testid="phase18-context-command" role="menuitem" type="button">Context command</button>';
                    document.body.appendChild(menu);
                    menu.querySelector('[data-testid="phase18-context-command"]').addEventListener('click', () => {
                        document.body.dataset.phase18ContextClicked = 'true';
                        menu.remove();
                    });
                });
                document.querySelector('[data-testid="phase18-image-resize-handle-se"]').addEventListener('mousedown', () => {
                    document.body.dataset.phase18ImageResizeStarted = 'true';
                });
                document.addEventListener('mouseup', () => {
                    if (document.body.dataset.phase18ImageResizeStarted === 'true') {
                        document.body.dataset.phase18ImageResizeFinished = 'true';
                    }
                });
            }
            """);

    private sealed class Phase18UiState
    {
        public bool ToolbarClicked { get; set; }
        public bool ContextClicked { get; set; }
        public bool ImageResizeStarted { get; set; }
        public bool ImageResizeFinished { get; set; }
    }
}
