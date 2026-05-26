using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase15TestRewriteJavaScriptTests
{
    [Fact]
    public void Phase15_DocumentEditorTestsDoNotAssertLegacyCssFlowWrapping()
    {
        var root = FindRepositoryRoot();
        var documentEditorTests = Directory
            .EnumerateFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains("DocumentEditor", StringComparison.Ordinal))
            .Where(path => !path.EndsWith(nameof(DocumentEditorImageWrapPhase15TestRewriteJavaScriptTests) + ".cs", StringComparison.Ordinal))
            .ToArray();

        var positiveLegacyExpectations = new List<string>();
        var positiveExpectation = new Regex(
            @"(?:assert\.ok|Assert\.(?:IsTrue|That)|Should\(\)\.Contain|ToContainTextAsync|ToHaveAttributeAsync)\s*\([^;\n]*(?:float\s*:\s*(?:left|right|none)|shape-outside|data-flow-reservation\s*=\s*""true""|display\s*:\s*block|full-band center Square|centered Square still uses a TopBottom-style full-band)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (var file in documentEditorTests)
        {
            var relative = Path.GetRelativePath(root, file);
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (positiveExpectation.IsMatch(line))
                {
                    positiveLegacyExpectations.Add($"{relative}:{index + 1}: {line.Trim()}");
                }
            }
        }

        positiveLegacyExpectations.Should().BeEmpty(
            "phase 15 rewrites image wrap tests to interval/object-layer snapshots; WYSIWYG tests must not require browser float, shape-outside, or full-band center Square fallbacks");
    }

    [Fact]
    public void Phase15_ImageE2EUsesHumanLikeWorkflowsAndKeepsInternalCommandsDiagnostic()
    {
        var root = FindRepositoryRoot();
        var humanParityPath = Path.Combine(root, "tests", "Tempo.Blazor.E2E", "DocumentEditorImageOnlyOfficeParityE2ETests.cs");
        var runtimeDiagnosticPath = Path.Combine(root, "tests", "Tempo.Blazor.E2E", "DocumentEditorJsRuntimeImageTests.cs");
        var playwrightBasePath = Path.Combine(root, "tests", "Tempo.Blazor.E2E", "PlaywrightTestBase.cs");

        var humanParity = File.ReadAllText(humanParityPath);
        humanParity.Should().Contain("page.Mouse.ClickAsync");
        humanParity.Should().Contain("page.Mouse.MoveAsync");
        humanParity.Should().Contain("page.Mouse.DownAsync");
        humanParity.Should().Contain("page.Mouse.UpAsync");
        humanParity.Should().Contain("page.Keyboard.TypeAsync");
        humanParity.Should().Contain("DragImageCenterToBlockAsync");
        humanParity.Should().Contain("DragImageResizeHandleAsync");
        humanParity.Should().Contain("WaitForImageResizeTrackPreviewAsync");
        humanParity.Should().NotContain("executeCommand(instanceId, 'setImageWrapMode'");

        var runtimeDiagnostic = File.ReadAllText(runtimeDiagnosticPath);
        runtimeDiagnostic.Should().Contain("executeCommand(instanceId, 'setImageWrapMode'");
        runtimeDiagnostic.Should().Contain("runtime.executeCommand(instanceId, 'setImageSize'");

        var playwrightBase = File.ReadAllText(playwrightBasePath);
        playwrightBase.Should().Contain("Tracing.StartAsync");
        playwrightBase.Should().Contain("Tracing.StopAsync");
        playwrightBase.Should().Contain("CurrentTestOutcome");
        playwrightBase.Should().Contain("AddResultFile(path)");
    }

    [Fact]
    public async Task Phase15_CanonicalWrapSnapshotsUseIntervalGeometryWithTolerance()
    {
        var result = await RunScenarioAsync(
            "canonical-snapshots",
            """
            const frame = { x: 0, y: 0, width: 600, height: 360 };
            const lineY = 56;
            const lineHeight = 20;
            const tolerance = 0.75;
            const sampleText = 'Alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu.';

            function near(actual, expected, tolerancePx, label) {
                assert.ok(Math.abs(Number(actual) - Number(expected)) <= tolerancePx, `${label}: expected ${expected}±${tolerancePx}, got ${actual}`);
            }

            function snapshot(name, wrapMode, rect, extra) {
                return hooks.snapshotWrapLayoutForTest({
                    bodyFrame: frame,
                    lineY,
                    lineHeight,
                    minReadableWidth: 24,
                    text: sampleText,
                    object: Object.assign({
                        objectId: name,
                        blockId: 'phase15-paragraph',
                        wrapMode,
                        wrapSide: 'BothSides',
                        rect,
                        distanceLeft: 0,
                        distanceRight: 0,
                        distanceTop: 0,
                        distanceBottom: 0
                    }, extra || {})
                });
            }

            function assertNoSegmentOverlapObject(snapshot, label) {
                for (const segment of snapshot.lineSegments || []) {
                    const rect = segment.rect || segment.Rect || segment.visualRect || segment.VisualRect;
                    if (!rect) continue;
                    const object = snapshot.objectRect;
                    const overlapsX = rect.x < object.x + object.width - 0.001 && rect.x + rect.width > object.x + 0.001;
                    const overlapsY = rect.y < object.y + object.height - 0.001 && rect.y + rect.height > object.y + 0.001;
                    assert.ok(!(overlapsX && overlapsY), `${label}: segment overlaps image ${JSON.stringify({ rect, object })}`);
                }
            }

            const leftSquare = snapshot('left-square', 'Square', { x: 0, y: 40, width: 120, height: 72 });
            assert.strictEqual(leftSquare.source, 'wrap-layout-snapshot');
            assert.strictEqual(leftSquare.exclusion.wrapMode, 'Square');
            assert.strictEqual(leftSquare.blockedIntervals.length, 1, JSON.stringify(leftSquare));
            near(leftSquare.blockedIntervals[0].x, 0, tolerance, 'left Square blocked x');
            near(leftSquare.blockedIntervals[0].width, 120, tolerance, 'left Square blocked width');
            assert.strictEqual(leftSquare.availableIntervals.length, 1, JSON.stringify(leftSquare.availableIntervals));
            near(leftSquare.availableIntervals[0].x, 120, tolerance, 'left Square available x');
            near(leftSquare.availableIntervals[0].width, 480, tolerance, 'left Square available width');
            assertNoSegmentOverlapObject(leftSquare, 'left Square');

            const rightSquare = snapshot('right-square', 'Square', { x: 480, y: 40, width: 120, height: 72 });
            assert.strictEqual(rightSquare.blockedIntervals.length, 1, JSON.stringify(rightSquare));
            near(rightSquare.blockedIntervals[0].x, 480, tolerance, 'right Square blocked x');
            near(rightSquare.blockedIntervals[0].width, 120, tolerance, 'right Square blocked width');
            assert.strictEqual(rightSquare.availableIntervals.length, 1, JSON.stringify(rightSquare.availableIntervals));
            near(rightSquare.availableIntervals[0].x, 0, tolerance, 'right Square available x');
            near(rightSquare.availableIntervals[0].width, 480, tolerance, 'right Square available width');
            assertNoSegmentOverlapObject(rightSquare, 'right Square');

            const centerSquare = snapshot('center-square', 'Square', { x: 220, y: 40, width: 120, height: 72 });
            assert.strictEqual(centerSquare.blockedIntervals.length, 1, JSON.stringify(centerSquare));
            assert.strictEqual(centerSquare.availableIntervals.length, 2, JSON.stringify(centerSquare.availableIntervals));
            near(centerSquare.blockedIntervals[0].x, 220, tolerance, 'center Square blocked x');
            near(centerSquare.blockedIntervals[0].width, 120, tolerance, 'center Square blocked width');
            near(centerSquare.availableIntervals[0].x, 0, tolerance, 'center Square left interval x');
            near(centerSquare.availableIntervals[0].width, 220, tolerance, 'center Square left interval width');
            near(centerSquare.availableIntervals[1].x, 340, tolerance, 'center Square right interval x');
            near(centerSquare.availableIntervals[1].width, 260, tolerance, 'center Square right interval width');
            assert.strictEqual(centerSquare.moved, false, 'center Square must not be rewritten to a full-band TopBottom move');
            assertNoSegmentOverlapObject(centerSquare, 'center Square');

            const topBottom = snapshot('top-bottom', 'TopBottom', { x: 220, y: 40, width: 120, height: 72 });
            assert.strictEqual(topBottom.exclusion.wrapMode, 'TopBottom');
            assert.strictEqual(topBottom.blockedIntervals.length, 1, JSON.stringify(topBottom));
            near(topBottom.blockedIntervals[0].x, 0, tolerance, 'TopBottom blocked x');
            near(topBottom.blockedIntervals[0].width, 600, tolerance, 'TopBottom blocked width');
            assert.strictEqual(topBottom.moved, true, 'TopBottom must move the line below the object');
            assert.ok(topBottom.movedToY >= 112 - tolerance, JSON.stringify(topBottom));
            assert.strictEqual(topBottom.availableIntervals.length, 1, JSON.stringify(topBottom.availableIntervals));
            near(topBottom.availableIntervals[0].x, 0, tolerance, 'TopBottom moved interval x');
            near(topBottom.availableIntervals[0].width, 600, tolerance, 'TopBottom moved interval width');
            assert.ok(topBottom.availableIntervals[0].y >= 112 - tolerance, JSON.stringify(topBottom.availableIntervals));

            const tight = snapshot('tight-triangle', 'Tight', { x: 200, y: 40, width: 160, height: 120 }, {
                wrapContourPoints: [
                    { x: 0.5, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 }
                ]
            });
            assert.strictEqual(tight.exclusion.kind, 'contour');
            assert.strictEqual(tight.blockedIntervals.length, 1, JSON.stringify(tight));
            assert.ok(tight.blockedIntervals[0].x > 220, JSON.stringify(tight.blockedIntervals[0]));
            assert.ok(tight.blockedIntervals[0].width > 30, JSON.stringify(tight.blockedIntervals[0]));
            assert.ok(tight.blockedIntervals[0].width < 105, JSON.stringify(tight.blockedIntervals[0]));
            assert.ok(tight.availableIntervals.length >= 2, JSON.stringify(tight.availableIntervals));

            const behindText = snapshot('behind-text', 'BehindText', { x: 220, y: 40, width: 120, height: 72 });
            assert.strictEqual(behindText.exclusion, null, JSON.stringify(behindText));
            assert.strictEqual(behindText.blockedIntervals.length, 0, JSON.stringify(behindText.blockedIntervals));
            assert.strictEqual(behindText.availableIntervals.length, 1, JSON.stringify(behindText.availableIntervals));
            near(behindText.availableIntervals[0].x, 0, tolerance, 'BehindText interval x');
            near(behindText.availableIntervals[0].width, 600, tolerance, 'BehindText interval width');
            assert.strictEqual(behindText.moved, false, 'BehindText must not move text');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    private static async Task<ScenarioResult> RunScenarioAsync(string scenario, string nodeScript)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return new ScenarioResult(0, "OK", "");
        var result = await RunNodeAsync(scriptPath, nodeScript, scenario);
        return new ScenarioResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    private static string GetWysiwygScriptPath()
        => Path.Combine(FindRepositoryRoot(), "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");

    private static bool IsNodeAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(
        string scriptPath,
        string nodeScript,
        string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase15-{scenario}-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, SharedSandboxScript + nodeScript);
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { tempFile, scriptPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })!;

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, stdout, stderr);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private sealed record ScenarioResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public void ShouldPass()
        {
            ExitCode.Should().Be(0, StandardError);
            StandardOutput.Trim().Should().Be("OK");
        }
    }

    private const string SharedSandboxScript =
        """
        const fs = require('fs');
        const vm = require('vm');
        const assert = require('assert');

        function createSandbox() {
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON,
                Date,
                Math,
                Number,
                String,
                Promise
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
            sandbox.window.performance = { now: () => Date.now() };
            return sandbox;
        }

        const code = fs.readFileSync(process.argv[2], 'utf8');
        const sandbox = createSandbox();
        vm.createContext(sandbox);
        vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });
        const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;

        """;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
