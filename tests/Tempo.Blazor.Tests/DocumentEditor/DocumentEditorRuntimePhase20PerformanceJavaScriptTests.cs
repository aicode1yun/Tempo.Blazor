using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase20PerformanceJavaScriptTests
{
    [Fact]
    public async Task Phase20_WysiwygScript_PassesNodeSyntaxCheck()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "--check", scriptPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        process.ExitCode.Should().Be(0, stdout + stderr);
    }

    [Fact]
    public async Task Phase20_DebugMetrics_ReportNewEngineRenderAndLayoutCounters()
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

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            const engine = sandbox.window.tmDocumentEditorEngine;
            const root = createRoot();
            engine.create(root, { InstanceId: 'phase20' }, null);
            engine.loadDocument('phase20', {
                Document: {
                    DocumentId: 'phase20-doc',
                    Blocks: [
                        { Id: 'b1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'i1', Text: 'Hello' }] } }
                    ]
                }
            });

            const metrics = engine.getDebugMetrics('phase20');
            assert.strictEqual(metrics.TotalPages, 1);
            assert.strictEqual(metrics.RenderedPages, 1);
            assert.strictEqual(metrics.VirtualizedPages, 0);
            assert.ok(metrics.FullRenderCount >= 2);
            assert.ok(metrics.LayoutPassCount >= 2);
            assert.strictEqual(typeof metrics.LastLayoutPassMs, 'number');
            assert.strictEqual(typeof metrics.MaxLayoutPassMs, 'number');

            engine.clearDebugMetrics('phase20');
            const cleared = engine.getDebugMetrics('phase20');
            assert.strictEqual(cleared.FullRenderCount, 0);
            assert.strictEqual(cleared.LayoutPassCount, 0);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase20_PageMetrics_ReportRenderedPagesFromNewEngineLayout()
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
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            const engine = sandbox.window.tmDocumentEditorEngine;
            const root = createRoot();
            engine.create(root, { InstanceId: 'phase20-pages' }, null);
            engine.loadDocument('phase20-pages', {
                Document: {
                    DocumentId: 'phase20-pages-doc',
                    Blocks: Array.from({ length: 8 }, (_, index) => ({
                        Id: 'b' + index,
                        Type: 'Paragraph',
                        Content: { Type: 'Paragraph', Inlines: [{ Id: 'i' + index, Text: 'Paragraph ' + index }] }
                    }))
                }
            });

            const metrics = engine.getPageMetrics('phase20-pages');
            assert.ok(metrics.TotalPages >= 1);
            assert.strictEqual(metrics.RenderedPages, metrics.TotalPages);
            assert.strictEqual(metrics.VirtualizedPages, 0);
            assert.strictEqual(metrics.Pages[0].IsVirtual, false);
            assert.ok(metrics.Pages[0].BlockIds.length > 0);

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
