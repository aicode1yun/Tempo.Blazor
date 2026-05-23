using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorLayoutPhase7DragTests
{
    [Fact]
    public async Task Phase7_ImageMoveSnapModel_SnapsToTextEdgesPageCenterObjectsAndLines()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Math, Number, String, parseInt };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const snap = sandbox.window.tmDocumentEditorEngine.__testHooks.computeImageMoveSnap;
            const base = {
                bodyRect: { X: 0, Y: 0, Width: 520, Height: 720 },
                objectSize: { Width: 120, Height: 80 },
                otherObjects: [
                    { Rect: { X: 300, Y: 160, Width: 90, Height: 60 } }
                ],
                lines: [
                    { Rect: { X: 136, Y: 200, Width: 280, Height: 18 } },
                    { Rect: { X: 136, Y: 236, Width: 280, Height: 18 } }
                ]
            };

            const left = snap({ x: 3, y: 20 }, base);
            assert.strictEqual(left.x, 0);
            assert.ok(left.guides.some(g => g.Kind === 'text-left'));

            const right = snap({ x: 397, y: 20 }, base);
            assert.strictEqual(right.x, 400);
            assert.ok(right.guides.some(g => g.Kind === 'text-right'));

            const center = snap({ x: 199, y: 20 }, base);
            assert.strictEqual(center.x, 200);
            assert.ok(center.guides.some(g => g.Kind === 'page-center-x'));

            const objectSnap = snap({ x: 178, y: 160 }, base);
            assert.strictEqual(objectSnap.x, 180);
            assert.ok(objectSnap.guides.some(g => g.Kind === 'object-left'));

            const lineSnap = snap({ x: 44, y: 197 }, base);
            assert.strictEqual(lineSnap.y, 200);
            assert.ok(lineSnap.guides.some(g => g.Kind === 'line-top'));

            const disabled = snap({ x: 199, y: 20 }, { ...base, disableSnap: true });
            assert.strictEqual(disabled.x, 199);
            assert.strictEqual(disabled.guides.length, 0);

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

    private static bool IsNodeAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(2000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(
        string scriptPath,
        string nodeScript)
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, nodeScript);
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", $"{tempFile} {scriptPath}")
            {
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

    private static string FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "TempoBlazor.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
