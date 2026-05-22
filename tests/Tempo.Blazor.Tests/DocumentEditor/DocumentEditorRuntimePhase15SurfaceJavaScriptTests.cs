using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase15SurfaceJavaScriptTests
{
    [Fact]
    public async Task Phase15_PageMetricsNonPrintingAndActiveHeadingHelpers()
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
                [
                    { index: 0, blockIds: ['h1', 'p1'] },
                    { index: 1, blockIds: ['h2'] },
                    { index: 2, blockIds: ['p3'] }
                ],
                [0, 2],
                [2],
                1);

            assert.strictEqual(metrics.TotalPages, 3);
            assert.strictEqual(metrics.RenderedPages, 2);
            assert.strictEqual(metrics.VirtualizedPages, 1);
            assert.strictEqual(metrics.ActivePageIndex, 1);
            assert.strictEqual(metrics.Pages[1].IsVirtual, true);
            assert.strictEqual(metrics.Pages[2].HasOverflow, true);
            assert.deepStrictEqual(JSON.parse(JSON.stringify(metrics.Pages[0].BlockIds)), ['h1', 'p1']);

            assert.strictEqual(hooks.formatNonPrintingText('A B\tC\n'), 'A·B→C¶\n');
            assert.strictEqual(hooks.findActiveHeadingBlockIdFromRects([
                { id: 'h1', top: -20 },
                { id: 'h2', top: 90 },
                { id: 'h3', top: 260 }
            ], 120), 'h2');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase15_DebugSnapshot_ExposesLayoutPerformanceAndInvalidationMetrics()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const activeElement = {
                nodeType: 1,
                tagName: 'DIV',
                className: '',
                classList: [],
                parentElement: null,
                getAttribute: () => null,
                closest: () => null
            };
            const sandbox = {
                window: {},
                document: { activeElement },
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON,
                Date,
                Math,
                Node: { ELEMENT_NODE: 1, TEXT_NODE: 3 }
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.innerHeight = 900;
            sandbox.window.scrollY = 0;
            sandbox.window.pageYOffset = 0;
            sandbox.window.performance = { now: () => Date.now() };
            sandbox.window.getSelection = () => ({ rangeCount: 0 });
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const harness = sandbox.window.tmDocumentWysiwyg.__testHooks.createPerformanceMetricsHarness();
            const previousSnapshot = {
                Pages: [{
                    PageIndex: 0,
                    Objects: [{
                        BlockId: 'img-a',
                        ObjectRect: { X: 10, Y: 20, Width: 120, Height: 80 },
                        WrapRect: { X: 6, Y: 16, Width: 128, Height: 88 },
                        WrapMode: 1,
                        ZIndex: 1
                    }]
                }]
            };
            const draggedSnapshot = {
                Pages: [{
                    PageIndex: 0,
                    Objects: [{
                        BlockId: 'img-a',
                        ObjectRect: { X: 42, Y: 20, Width: 120, Height: 80 },
                        WrapRect: { X: 38, Y: 16, Width: 128, Height: 88 },
                        WrapMode: 1,
                        ZIndex: 1
                    }]
                }]
            };
            const resizedSnapshot = {
                Pages: [{
                    PageIndex: 0,
                    Objects: [{
                        BlockId: 'img-a',
                        ObjectRect: { X: 42, Y: 20, Width: 160, Height: 96 },
                        WrapRect: { X: 38, Y: 16, Width: 168, Height: 104 },
                        WrapMode: 1,
                        ZIndex: 1
                    }]
                }]
            };

            harness.recordLayoutPass('image-drag-preview', previousSnapshot, draggedSnapshot);
            harness.recordLayoutPass('image-resize-preview', draggedSnapshot, resizedSnapshot);

            const metrics = harness.metrics();
            assert.strictEqual(metrics.LayoutPassCount, 2);
            assert.strictEqual(metrics.LayoutDragReflowCount, 1);
            assert.strictEqual(metrics.LayoutResizeReflowCount, 1);
            assert.strictEqual(metrics.LayoutInvalidatedPages.length, 1);
            assert.strictEqual(metrics.LayoutInvalidatedPages[0], 0);
            assert.strictEqual(metrics.LayoutInvalidatedPageCount, 1);
            assert.strictEqual(typeof metrics.LastLayoutPassMs, 'number');

            const snapshot = harness.snapshot();
            assert.strictEqual(snapshot.HasInstance, true);
            assert.strictEqual(snapshot.Performance.LayoutPassCount, 2);
            assert.strictEqual(snapshot.Performance.LayoutDragReflowCount, 1);
            assert.strictEqual(snapshot.Performance.LayoutResizeReflowCount, 1);
            assert.strictEqual(snapshot.Performance.LayoutInvalidatedPages.length, 1);
            assert.strictEqual(snapshot.Performance.LayoutInvalidatedPages[0], 0);
            assert.strictEqual(snapshot.LayoutPassCount, 2);
            assert.strictEqual(snapshot.LayoutInvalidatedPageCount, 1);

            harness.dispose();
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tm-doc-runtime-phase15-{Guid.NewGuid():N}.js");
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
