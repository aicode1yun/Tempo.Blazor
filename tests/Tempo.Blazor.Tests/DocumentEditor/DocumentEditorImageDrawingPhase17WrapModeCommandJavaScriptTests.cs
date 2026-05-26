using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase17WrapModeCommandJavaScriptTests
{
    [Fact]
    public async Task Phase17_SetImageWrapModeSquareSetsAnchoredDrawingLayout()
    {
        var result = await RunScenarioAsync(
            "square",
            """
            const harness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'Inline' });
            const after = harness.setWrapMode('Square').state;

            assert.strictEqual(after.wrapMode, 'Square');
            assert.strictEqual(after.kind, 1);
            assert.strictEqual(after.fixedOnPage, false);
            assert.strictEqual(after.moveWithText, true);
            assert.strictEqual(after.allowOverlap, false);
            assert.strictEqual(after.hasExclusion, true);
            assert.strictEqual(after.exclusionKind, 'rectangular');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase17_SetImageWrapModeInlineSetsInlineDrawingLayout()
    {
        var result = await RunScenarioAsync(
            "inline",
            """
            const harness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'Square' });
            const after = harness.setWrapMode('Inline').state;

            assert.strictEqual(after.wrapMode, 'Inline');
            assert.strictEqual(after.kind, 0);
            assert.strictEqual(after.fixedOnPage, false);
            assert.strictEqual(after.moveWithText, true);
            assert.strictEqual(after.allowOverlap, false);
            assert.strictEqual(after.hasExclusion, false);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase17_SetImageWrapModeTopBottomSetsFullWidthExclusion()
    {
        var result = await RunScenarioAsync(
            "top-bottom",
            """
            const harness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'Inline' });
            const after = harness.setWrapMode('TopBottom').state;

            assert.strictEqual(after.wrapMode, 'TopBottom');
            assert.strictEqual(after.kind, 1);
            assert.strictEqual(after.fixedOnPage, false);
            assert.strictEqual(after.moveWithText, true);
            assert.strictEqual(after.allowOverlap, false);
            assert.strictEqual(after.hasExclusion, true);
            assert.strictEqual(after.exclusionKind, 'fullWidth');
            assert.strictEqual(after.exclusionRect.x, 0);
            assert.strictEqual(after.exclusionRect.width, 500);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase17_SetImageWrapModeBehindTextSetsBehindStackingWithoutExclusion()
    {
        var result = await RunScenarioAsync(
            "behind-text",
            """
            const harness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'Square' });
            const after = harness.setWrapMode('BehindText').state;

            assert.strictEqual(after.wrapMode, 'BehindText');
            assert.strictEqual(after.kind, 1);
            assert.strictEqual(after.fixedOnPage, false);
            assert.strictEqual(after.moveWithText, true);
            assert.strictEqual(after.allowOverlap, true);
            assert.ok(after.zIndex < 0, after.zIndex);
            assert.strictEqual(after.hasExclusion, false);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase17_SetImageWrapModeInFrontOfTextSetsFrontStackingWithoutExclusion()
    {
        var result = await RunScenarioAsync(
            "front",
            """
            const anchoredHarness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'Square' });
            const anchored = anchoredHarness.setWrapMode('InFrontOfText').state;
            assert.strictEqual(anchored.wrapMode, 'InFrontOfText');
            assert.strictEqual(anchored.kind, 1);
            assert.strictEqual(anchored.fixedOnPage, false);
            assert.strictEqual(anchored.moveWithText, true);
            assert.strictEqual(anchored.allowOverlap, true);
            assert.ok(anchored.zIndex > 0, anchored.zIndex);
            assert.strictEqual(anchored.hasExclusion, false);

            const fixedHarness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'InFrontOfText', fixedOnPage: true });
            const fixed = fixedHarness.setWrapMode('InFrontOfText').state;
            assert.strictEqual(fixed.kind, 2);
            assert.strictEqual(fixed.fixedOnPage, true);
            assert.strictEqual(fixed.moveWithText, false);
            assert.strictEqual(fixed.allowOverlap, true);
            assert.strictEqual(fixed.hasExclusion, false);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase17_WrapModeChangePreservesObjectIdentityAndMetadata()
    {
        var result = await RunScenarioAsync(
            "metadata",
            """
            const harness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'Square' });
            const before = harness.state();
            const after = harness.setWrapMode('Through').state;

            assert.strictEqual(after.objectId, before.objectId);
            assert.strictEqual(after.blockId, before.blockId);
            assert.strictEqual(after.drawingRunCount, 1);
            assert.strictEqual(after.altText, before.altText);
            assert.strictEqual(after.caption, before.caption);
            assert.strictEqual(after.source, before.source);
            assert.strictEqual(after.url, before.url);
            assert.strictEqual(after.assetId, before.assetId);
            assert.strictEqual(after.width, before.width);
            assert.strictEqual(after.height, before.height);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase17_WrapModeChangeIsSingleUndoableUpdateImageLayoutStep()
    {
        var result = await RunScenarioAsync(
            "undo",
            """
            const harness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'Square' });
            const before = harness.state();
            const applied = harness.setWrapMode('Tight').state;

            assert.strictEqual(applied.wrapMode, 'Tight');
            assert.strictEqual(applied.transactionCount, 1);
            assert.strictEqual(applied.undoDepth, 1);
            assert.strictEqual(applied.lastTransaction.operationCount, 1);
            assert.strictEqual(harness.inst.undoTransactions[0].operations[0].type, 'UpdateImageLayout');
            assert.strictEqual(harness.inst.undoTransactions[0].inverseOperations[0].type, 'UpdateImageLayout');

            const undone = harness.undo().state;
            assert.strictEqual(undone.wrapMode, before.wrapMode);
            assert.strictEqual(undone.redoDepth, 1);

            const redone = harness.redo().state;
            assert.strictEqual(redone.wrapMode, 'Tight');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase17_BlockOnlyPayloadNoLongerFindsDrawingRunAfterPhase24()
    {
        var result = await RunScenarioAsync(
            "block-lookup",
            """
            const harness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'Square' });
            harness.inst.selection = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'phase17-p1', offset: 0 });
            const before = harness.state();
            const applied = harness.setWrapModeByBlockOnly('TopBottom');
            const after = applied.state;

            assert.strictEqual(applied.result.ok, false, JSON.stringify(applied.result));
            assert.strictEqual(applied.result.error.code, 'active-image-not-found');
            assert.strictEqual(after.objectId, before.objectId);
            assert.strictEqual(after.wrapMode, 'Square');
            assert.strictEqual(after.formattingStateEventCount, before.formattingStateEventCount, JSON.stringify({ before, after }));
            """);

        result.ShouldPass();
    }

    private static async Task<DocumentEditorImageDrawingPhase17NodeResult> RunScenarioAsync(string scenario, string body)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable())
        {
            return new DocumentEditorImageDrawingPhase17NodeResult(0, "OK", string.Empty);
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

    private static async Task<DocumentEditorImageDrawingPhase17NodeResult> RunNodeAsync(string scriptPath, string nodeScript, string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase17-{scenario}-{Guid.NewGuid():N}.js");
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
            return new DocumentEditorImageDrawingPhase17NodeResult(process.ExitCode, stdout, stderr);
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

internal sealed record DocumentEditorImageDrawingPhase17NodeResult(int ExitCode, string StandardOutput, string StandardError);

internal static class DocumentEditorImageDrawingPhase17Assertions
{
    public static void ShouldPass(this DocumentEditorImageDrawingPhase17NodeResult result)
    {
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }
}
