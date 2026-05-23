using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase8FloatingJavaScriptTests
{
    [Fact]
    public async Task Phase8_FloatingPositioning_FlipsShiftsAndConstrainsToScrollContainer()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const compute = sandbox.window.tmDocumentEditorEngine.__testHooks.computeFloatingPosition;

            const aboveSelection = compute(
                { left: 200, top: 180, width: 80, height: 20 },
                { width: 184, height: 40 },
                { viewportWidth: 640, viewportHeight: 480, placement: 'top' });
            assert.strictEqual(aboveSelection.placement, 'top');
            assert.strictEqual(aboveSelection.top, 132);
            assert.strictEqual(aboveSelection.left, 148);

            const shiftedFromRightEdge = compute(
                { left: 610, top: 120, width: 40, height: 20 },
                { width: 184, height: 40 },
                { viewportWidth: 640, viewportHeight: 480, placement: 'bottom' });
            assert.strictEqual(shiftedFromRightEdge.left, 448);
            assert.strictEqual(shiftedFromRightEdge.left + shiftedFromRightEdge.width <= 632, true);

            const flippedFromBottomEdge = compute(
                { left: 100, top: 452, width: 60, height: 20 },
                { width: 160, height: 44 },
                { viewportWidth: 640, viewportHeight: 480, placement: 'bottom' });
            assert.strictEqual(flippedFromBottomEdge.placement, 'top');
            assert.strictEqual(flippedFromBottomEdge.top, 400);

            const constrainedToScrollContainer = compute(
                { left: 560, top: 280, width: 60, height: 20 },
                { width: 180, height: 80 },
                {
                    viewportWidth: 800,
                    viewportHeight: 600,
                    placement: 'bottom',
                    constrainToScrollContainer: true,
                    scrollContainerRect: { left: 100, top: 80, width: 520, height: 340 }
                });
            assert.strictEqual(constrainedToScrollContainer.left, 432);
            assert.strictEqual(constrainedToScrollContainer.top + constrainedToScrollContainer.height <= 412, true);

            const containerRelative = compute(
                { left: 400, top: 220, width: 40, height: 20 },
                { width: 120, height: 40 },
                {
                    viewportWidth: 800,
                    viewportHeight: 600,
                    placement: 'bottom',
                    anchorIsContainerRelative: true,
                    scrollLeft: 180,
                    scrollTop: 90,
                    scrollContainerRect: { left: 100, top: 80, width: 520, height: 340 }
                });
            assert.strictEqual(containerRelative.left, 280);
            assert.strictEqual(containerRelative.top, 238);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    private static string GetWysiwygScriptPath()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
    }

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

        throw new InvalidOperationException("Repository root was not found.");
    }

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
            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(string scriptPath, string nodeScript)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tm-doc-runtime-phase8-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, nodeScript);
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
            File.Delete(tempFile);
        }
    }
}
