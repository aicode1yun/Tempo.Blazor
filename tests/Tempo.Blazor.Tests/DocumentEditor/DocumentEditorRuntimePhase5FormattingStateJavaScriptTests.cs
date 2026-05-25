using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase5FormattingStateJavaScriptTests
{
    [Fact]
    public async Task Phase5_ComputeFormattingState_ReturnsCollapsedAndRangeInlineState()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase5-formatting',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            Inlines: [
                                { Id: 'plain', Text: 'plain ' },
                                { Id: 'bold', Text: 'bold', Marks: [{ Type: 0 }] },
                                { Id: 'space', Text: ' ' },
                                { Id: 'large', Text: 'large', Marks: [{ Type: 12, Value: '28pt' }] },
                                { Id: 'blue', Text: ' blue', Marks: [{ Type: 10, Value: '#2563eb' }] }
                            ]
                        }
                    }
                ]
            });

            const collapsedPlain = hooks.computeFormattingState(model, collapsed('p1', 2));
            assert.strictEqual(collapsedPlain.isDisabled, false);
            assert.strictEqual(collapsedPlain.bold, false);
            assert.strictEqual(collapsedPlain.fontSize, null);
            assert.strictEqual(collapsedPlain.textColor, null);

            const collapsedBold = hooks.computeFormattingState(model, collapsed('p1', 7));
            assert.strictEqual(collapsedBold.bold, true);

            const fullBold = hooks.computeFormattingState(model, range('p1', 6, 10));
            assert.strictEqual(fullBold.bold, true);
            assert.strictEqual(fullBold.inline.mixed.bold, false);

            const mixedBold = hooks.computeFormattingState(model, range('p1', 0, 10));
            assert.strictEqual(mixedBold.bold, 'mixed');
            assert.strictEqual(mixedBold.inline.mixed.bold, true);

            const mixedFontSize = hooks.computeFormattingState(model, range('p1', 10, 16));
            assert.strictEqual(mixedFontSize.fontSize, 'mixed');
            assert.strictEqual(mixedFontSize.inline.mixed.fontSize, true);

            const mixedTextColor = hooks.computeFormattingState(model, range('p1', 15, 21));
            assert.strictEqual(mixedTextColor.textColor, 'mixed');
            assert.strictEqual(mixedTextColor.inline.mixed.textColor, true);

            const empty = hooks.computeFormattingState(model, { region: 'Body', blockId: 'missing', offset: 0, isCollapsed: true });
            assert.strictEqual(empty.isDisabled, true);
            assert.strictEqual(empty.disabledReason, 'missing-selection');

            console.log('OK');

            function collapsed(blockId, offset) {
                return {
                    region: 'Body',
                    blockId,
                    anchor: { region: 'Body', blockId, offset },
                    focus: { region: 'Body', blockId, offset },
                    isCollapsed: true,
                    direction: 'none'
                };
            }

            function range(blockId, start, end) {
                return {
                    region: 'Body',
                    blockId,
                    anchor: { region: 'Body', blockId, offset: start },
                    focus: { region: 'Body', blockId, offset: end },
                    isCollapsed: start === end,
                    direction: 'forward'
                };
            }
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase5_ComputeFormattingState_ResolvesStableSelectionToken()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase5-token',
                Blocks: [
                    { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'hello', Marks: [{ Type: 0 }] }] } }
                ]
            });
            const token = hooks.createStableSelectionToken('phase5-token-instance', {
                region: 'Body',
                blockId: 'p1',
                anchor: { region: 'Body', blockId: 'p1', inlineId: 'r1', offset: 2 },
                focus: { region: 'Body', blockId: 'p1', inlineId: 'r1', offset: 2 },
                isCollapsed: true
            }, model);

            const state = hooks.computeFormattingState(model, token);
            assert.strictEqual(state.bold, true);
            assert.strictEqual(state.selection.blockId, 'p1');
            assert.strictEqual(state.selection.offset, 2);

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
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { "--version" },
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

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(string scriptPath, string nodeScript)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-phase5-formatting-{Guid.NewGuid():N}.js");
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
                Map,
                WeakMap
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
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
