using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorLayoutPhase3JavaScriptTests
{
    [Fact]
    public async Task Phase3_TextRunMeasurement_UsesCacheAndInvalidatesByFontAndZoom()
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
                Math,
                Number,
                String,
                parseFloat,
                parseInt
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentWysiwyg.__testHooks;
            hooks.clearTextRunMeasureCache();

            const normalRequest = {
                Text: 'Measure me',
                FontFamily: 'Arial',
                FontSize: 12,
                FontWeight: '400',
                FontStyle: 'normal',
                LetterSpacing: 0,
                Zoom: 1
            };
            const normal = hooks.measureTextRun(normalRequest);
            const normalAgain = hooks.measureTextRun(normalRequest);
            const boldItalic = hooks.measureTextRun({
                ...normalRequest,
                FontWeight: '700',
                FontStyle: 'italic'
            });

            assert.ok(normal.Width > 0, 'normal text width is nonzero');
            assert.ok(normal.Height > 0, 'normal text height is nonzero');
            assert.strictEqual(normalAgain.Width, normal.Width, 'same request reuses cached measurement');
            assert.ok(boldItalic.Width > normal.Width, 'bold italic fallback is wider than normal');

            const normalKey = hooks.getTextRunMeasureCacheKey(normalRequest);
            const zoomKey = hooks.getTextRunMeasureCacheKey({ ...normalRequest, Zoom: 1.25 });
            const fontKey = hooks.getTextRunMeasureCacheKey({ ...normalRequest, FontFamily: 'Courier New' });
            assert.notStrictEqual(normalKey, zoomKey, 'zoom is part of cache key');
            assert.notStrictEqual(normalKey, fontKey, 'font family is part of cache key');

            const stats = hooks.getTextRunMeasureStats();
            assert.strictEqual(stats.MeasureCount, 2);
            assert.strictEqual(stats.MeasureCacheHits, 1);
            assert.strictEqual(stats.MeasureCacheSize, 2);

            hooks.clearTextRunMeasureCache();
            const cleared = hooks.getTextRunMeasureStats();
            assert.strictEqual(cleared.MeasureCacheSize, 0);
            assert.ok(cleared.MeasureInvalidations >= 1);

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
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
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
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(string scriptPath, string nodeScript)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { "-", scriptPath },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        await process.StandardInput.WriteAsync(nodeScript);
        process.StandardInput.Close();
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, standardOutput, standardError);
    }
}
