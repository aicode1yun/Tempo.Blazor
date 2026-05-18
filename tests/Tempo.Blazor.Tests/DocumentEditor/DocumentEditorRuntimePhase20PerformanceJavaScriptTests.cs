using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase20PerformanceJavaScriptTests
{
    [Fact]
    public async Task Phase20_DebugMetrics_ExposeInputMarkerFloatingAndClipboardCounters()
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
                JSON,
                Date,
                Math
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.innerHeight = 900;
            sandbox.window.scrollY = 0;
            sandbox.window.pageYOffset = 0;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const harness = sandbox.window.tmDocumentWysiwyg.__testHooks.createPerformanceMetricsHarness();
            harness.recordMarkerRender(true);
            harness.recordMarkerRender(false);
            harness.recordFloatingReposition();
            harness.recordClipboardNormalization();

            const metrics = harness.metrics();
            assert.strictEqual(metrics.InputOperationCount, 1);
            assert.strictEqual(metrics.LastInputLatencyMs, 12);
            assert.strictEqual(metrics.MaxInputLatencyMs, 12);
            assert.strictEqual(metrics.AverageInputLatencyMs, 12);
            assert.strictEqual(metrics.MarkerRenderAttemptCount, 2);
            assert.strictEqual(metrics.MarkerRenderCount, 1);
            assert.strictEqual(metrics.MarkerRenderSkippedCount, 1);
            assert.strictEqual(metrics.FloatingRepositionCount, 1);
            assert.strictEqual(metrics.ClipboardNormalizationCount, 1);
            assert.strictEqual(typeof metrics.LastMarkerRenderMs, 'number');
            assert.strictEqual(typeof metrics.LastFloatingRepositionMs, 'number');
            assert.strictEqual(typeof metrics.LastClipboardNormalizationMs, 'number');

            harness.clear();
            const cleared = harness.metrics();
            assert.strictEqual(cleared.InputOperationCount, 0);
            assert.strictEqual(cleared.MarkerRenderAttemptCount, 0);
            assert.strictEqual(cleared.FloatingRepositionCount, 0);
            assert.strictEqual(cleared.ClipboardNormalizationCount, 0);
            harness.dispose();

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase20_VirtualPageMetrics_KeepVirtualizedPagesAsPlaceholders()
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

            const hooks = sandbox.window.tmDocumentWysiwyg.__testHooks;
            const metrics = hooks.buildPageMetrics(
                Array.from({ length: 8 }, (_, index) => ({ index, blockIds: ['b' + index] })),
                [0, 1, 6],
                [2],
                6);

            assert.strictEqual(metrics.TotalPages, 8);
            assert.strictEqual(metrics.RenderedPages, 3);
            assert.strictEqual(metrics.VirtualizedPages, 5);
            assert.strictEqual(metrics.Pages[2].IsVirtual, true);
            assert.strictEqual(metrics.Pages[2].HasOverflow, true);
            assert.strictEqual(metrics.Pages[6].IsVirtual, false);
            assert.deepStrictEqual(metrics.Pages[7].BlockIds, ['b7']);

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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tm-doc-runtime-phase20-{Guid.NewGuid():N}.js");
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
