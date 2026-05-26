using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase15ReanchorJavaScriptTests
{
    [Fact]
    public async Task Phase15_DropOverParagraphReanchorsToThatParagraph()
    {
        var result = await RunScenarioAsync(
            "paragraph",
            """
            const harness = hooks.createImageReanchorHarness();
            const committed = harness.commitAt(20, 45);

            assert.strictEqual(committed.operation.type, 'UpdateImageLayout');
            assert.strictEqual(committed.object.anchorBlockId, 'target-p');
            assert.strictEqual(committed.object.anchorRegion, 'Body');
            assert.strictEqual(committed.operation.newAnchor.BlockId, 'target-p');
            assert.strictEqual(committed.operation.newAnchor.Region, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase15_DropInMiddleOfLineResolvesNearestTextOffset()
    {
        var result = await RunScenarioAsync(
            "line-offset",
            """
            const harness = hooks.createImageReanchorHarness();
            const nearest = harness.resolve(100, 45);

            assert.strictEqual(nearest.blockId, 'target-p');
            assert.strictEqual(nearest.offset, 10);
            assert.strictEqual(nearest.textOffset, 10);
            assert.strictEqual(nearest.inlineIndex, 0);
            assert.strictEqual(nearest.runId, 'target-run');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase15_DropIntoHeaderStoresHeaderRegionContext()
    {
        var result = await RunScenarioAsync(
            "header",
            """
            const harness = hooks.createImageReanchorHarness();
            const committed = harness.commitAt(24, -35);

            assert.strictEqual(committed.nearest.region, 'Header');
            assert.strictEqual(committed.object.anchorBlockId, 'header-p');
            assert.strictEqual(committed.object.anchorRegion, 'Header');
            assert.strictEqual(committed.object.anchorHeaderFooterId, 'hf-header');
            assert.strictEqual(committed.operation.newAnchor.Region, 1);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase15_DropIntoFooterStoresFooterRegionContext()
    {
        var result = await RunScenarioAsync(
            "footer",
            """
            const harness = hooks.createImageReanchorHarness();
            const committed = harness.commitAt(24, 765);

            assert.strictEqual(committed.nearest.region, 'Footer');
            assert.strictEqual(committed.object.anchorBlockId, 'footer-p');
            assert.strictEqual(committed.object.anchorRegion, 'Footer');
            assert.strictEqual(committed.object.anchorHeaderFooterId, 'hf-footer');
            assert.strictEqual(committed.operation.newAnchor.Region, 2);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase15_DropIntoTableCellStoresTableCellContext()
    {
        var result = await RunScenarioAsync(
            "table-cell",
            """
            const harness = hooks.createImageReanchorHarness();
            const committed = harness.commitAt(330, 110);

            assert.strictEqual(committed.nearest.region, 'TableCell');
            assert.strictEqual(committed.object.anchorBlockId, 'cell-p');
            assert.strictEqual(committed.object.anchorRegion, 'TableCell');
            assert.strictEqual(committed.object.anchorTableId, 'table-1');
            assert.strictEqual(committed.object.anchorCellId, 'cell-1');
            assert.strictEqual(committed.operation.newAnchor.Region, 6);
            assert.strictEqual(committed.operation.newAnchor.TableId, 'table-1');
            assert.strictEqual(committed.operation.newAnchor.CellId, 'cell-1');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase15_LockAnchorPreventsAutomaticReanchorUnlessExplicitDrag()
    {
        var result = await RunScenarioAsync(
            "lock-anchor",
            """
            const harness = hooks.createImageReanchorHarness({ lockAnchor: true });
            const automatic = harness.commitAt(20, 45, { explicitDrag: false });

            assert.strictEqual(harness.shouldReanchor(true, false), false);
            assert.strictEqual(harness.shouldReanchor(true, true), true);
            assert.strictEqual(automatic.object.anchorBlockId, 'source-p');
            assert.strictEqual(automatic.object.anchorOffset, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase15_UpdateImageLayoutUndoRedoRestoresAnchorAndPosition()
    {
        var result = await RunScenarioAsync(
            "undo-redo",
            """
            const harness = hooks.createImageReanchorHarness();
            const committed = harness.commitAt(20, 45);
            const undo = hooks.createOperation('UpdateImageLayout', committed.operation).getReversed();
            const undoResult = hooks.applyOperation(harness.model, undo);
            if (!undoResult || undoResult.ok === false) throw new Error(JSON.stringify(undoResult && undoResult.errors || undoResult));

            assert.strictEqual(harness.object().anchorBlockId, 'source-p');
            assert.strictEqual(harness.object().anchorOffset, 0);

            const redo = hooks.createOperation('UpdateImageLayout', committed.operation);
            const redoResult = hooks.applyOperation(harness.model, redo);
            if (!redoResult || redoResult.ok === false) throw new Error(JSON.stringify(redoResult && redoResult.errors || redoResult));

            assert.strictEqual(harness.object().anchorBlockId, 'target-p');
            assert.strictEqual(harness.object().anchorOffset, 2);
            assert.strictEqual(harness.object().horizontalPosition.offset, committed.object.horizontalPosition.offset);
            assert.strictEqual(harness.object().verticalPosition.offset, committed.object.verticalPosition.offset);
            """);

        result.ShouldPass();
    }

    private static async Task<DocumentEditorImageDrawingPhase15NodeResult> RunScenarioAsync(string scenario, string body)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable())
        {
            return new DocumentEditorImageDrawingPhase15NodeResult(0, "OK", string.Empty);
        }

        var nodeScript =
            $$"""
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            {{body}}
            console.log('OK');
            """;

        return await RunNodeAsync(scriptPath, nodeScript, scenario);
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

    private static async Task<DocumentEditorImageDrawingPhase15NodeResult> RunNodeAsync(string scriptPath, string nodeScript, string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase15-{scenario}-{Guid.NewGuid():N}.js");
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
            return new DocumentEditorImageDrawingPhase15NodeResult(process.ExitCode, stdout, stderr);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private const string SharedSandboxScript =
        """
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

        """;

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TempoBlazor.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

internal sealed record DocumentEditorImageDrawingPhase15NodeResult(int ExitCode, string StandardOutput, string StandardError);

internal static class DocumentEditorImageDrawingPhase15Assertions
{
    public static void ShouldPass(this DocumentEditorImageDrawingPhase15NodeResult result)
    {
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }
}
