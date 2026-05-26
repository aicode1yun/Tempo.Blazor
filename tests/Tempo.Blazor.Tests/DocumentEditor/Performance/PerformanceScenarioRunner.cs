using System.Diagnostics;

namespace Tempo.Blazor.Tests.DocumentEditor.Performance;

/// <summary>Shared Node.js scenario runner used by the performance baseline test suite.
/// Mirrors the pattern from <c>DocumentEditorImageWrapPhase16PerformanceJavaScriptTests</c>:
/// load <c>document-editor-wysiwyg.js</c> into a <c>vm</c> sandbox and run an assertion
/// script that prints <c>OK</c> when successful, or a JSON payload on the last line.</summary>
internal static class PerformanceScenarioRunner
{
    internal const string SandboxPrelude =
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
                Promise,
                Set,
                Map,
                Symbol
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
            sandbox.window.performance = { now: () => Date.now() };
            sandbox.window.localStorage = { getItem: () => null, setItem: () => {}, removeItem: () => {} };
            return sandbox;
        }

        function createRoot() {
            return {
                innerHTML: '',
                attributes: {},
                classList: { add() {}, toggle() {}, remove() {} },
                setAttribute(name, value) { this.attributes[name] = String(value); },
                removeAttribute(name) { delete this.attributes[name]; },
                contains() { return true; },
                addEventListener() {},
                removeEventListener() {},
                querySelector() { return null; },
                querySelectorAll() { return []; }
            };
        }

        const code = fs.readFileSync(process.argv[2], 'utf8');
        const sandbox = createSandbox();
        vm.createContext(sandbox);
        vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

        const engine = sandbox.window.tmDocumentEditorEngine;
        const probe = sandbox.window.tmDocumentEditorPerformance;
        const hooks = engine && engine.__testHooks;
        """;

    internal static string FindRepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
            if (File.Exists(Path.Combine(dir, "TempoBlazor.slnx"))) return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new InvalidOperationException("Could not locate repository root from " + AppContext.BaseDirectory);
    }

    internal static string GetWysiwygScriptPath()
        => Path.Combine(FindRepositoryRoot(), "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");

    internal static bool IsNodeAvailable()
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

    internal static async Task<ScenarioResult> RunAsync(string scenario, string nodeScript)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return new ScenarioResult(0, "OK", string.Empty, NodeAvailable: false);

        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-perf-{scenario}-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, SandboxPrelude + "\n" + nodeScript);
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
            return new ScenarioResult(process.ExitCode, stdout, stderr, NodeAvailable: true);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

internal sealed record ScenarioResult(int ExitCode, string StandardOutput, string StandardError, bool NodeAvailable = true)
{
    public void ShouldPass()
    {
        if (!NodeAvailable) return;
        if (ExitCode != 0)
        {
            throw new Xunit.Sdk.XunitException(
                "Performance scenario failed. stderr:\n" + StandardError + "\nstdout:\n" + StandardOutput);
        }
        var lastLine = StandardOutput.Trim().Split('\n').LastOrDefault()?.Trim() ?? string.Empty;
        if (lastLine != "OK" && !lastLine.StartsWith("{"))
        {
            throw new Xunit.Sdk.XunitException(
                "Performance scenario did not end with 'OK' or JSON payload. stdout:\n" + StandardOutput);
        }
    }

    public string? GetJsonPayload()
    {
        if (!NodeAvailable) return null;
        var lines = StandardOutput.Trim().Split('\n');
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("{") && line.EndsWith("}")) return line;
        }
        return null;
    }
}
