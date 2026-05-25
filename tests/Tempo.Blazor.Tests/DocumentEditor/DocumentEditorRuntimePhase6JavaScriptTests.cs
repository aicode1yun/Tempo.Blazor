using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase6JavaScriptTests
{
    [Fact]
    public async Task Phase6_RuntimeSchemaBridge_RejectsPageBreakOutsideBodyAndNormalizesTableCellPaste()
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

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            assert.strictEqual(hooks.schemaAllowsBlock(6, 'Body'), true);
            assert.strictEqual(hooks.schemaAllowsBlock(6, 'Header'), false);
            assert.strictEqual(hooks.schemaAllowsBlock(4, 'TableCell'), false);
            assert.strictEqual(hooks.schemaAllowsToolbarBlockCommand(4, 'Image'), true);
            assert.strictEqual(hooks.schemaAllowsToolbarBlockCommand(4, 'TableCell'), false);

            const pageBreak = hooks.normalizeInsertionBlocksForSchema([
                { Id: 'pb-1', Type: 6, Content: { $type: 'pageBreak' } }
            ], 'Footer');
            assert.strictEqual(pageBreak.blocks.length, 0);
            assert.strictEqual(pageBreak.warnings[0].code, 'block-rejected-by-schema');

            const nestedTable = hooks.normalizeInsertionBlocksForSchema([
                {
                    Id: 'tbl-1',
                    Type: 4,
                    Content: {
                        $type: 'table',
                        Rows: [
                            {
                                Cells: [
                                    {
                                        Id: 'cell-1',
                                        Blocks: [
                                            { Id: 'p-1', Type: 0, Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Text: 'Nested' }] } }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
                }
            ], 'TableCell');
            assert.strictEqual(nestedTable.blocks.length, 1);
            assert.strictEqual(nestedTable.blocks[0].Type, 0);
            assert.strictEqual(nestedTable.warnings.some(w => w.code === 'table-unwrapped-in-table-cell'), true);

            const image = hooks.normalizeInsertionBlocksForSchema([
                { Id: 'img-1', Type: 5, Content: { $type: 'image', Url: 'https://example.com/a.png' } }
            ], 'TableCell');
            assert.strictEqual(image.blocks[0].Content.AltText, '');
            assert.strictEqual(image.warnings.some(w => w.code === 'image-alt-text-defaulted'), true);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase6_InputSession_UsesModelOperationsAndCoalescesTrackedTyping()
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
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase6-input',
                Blocks: [
                    { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: '' }] } }
                ]
            });
            let preventDefaultCount = 0;
            const pipeline = hooks.createInputPipeline({
                model,
                selection: { blockId: 'p1', offset: 0, isCollapsed: true },
                trackChanges: true,
                userId: 'author-1'
            });

            const j = pipeline.handleBeforeInput({ inputType: 'insertText', data: 'j', preventDefault() { preventDefaultCount++; } });
            const a = pipeline.handleBeforeInput({ inputType: 'insertText', data: 'a', preventDefault() { preventDefaultCount++; } });
            const k = pipeline.handleBeforeInput({ inputType: 'insertText', data: 'k', preventDefault() { preventDefaultCount++; } });
            const debug = pipeline.debug();
            const exported = hooks.exportToCSharpJson(model);

            assert.strictEqual(j.ok, true);
            assert.strictEqual(a.ok, true);
            assert.strictEqual(k.ok, true);
            assert.strictEqual(preventDefaultCount, 3);
            assert.strictEqual(exported.Blocks[0].Content.Inlines[0].Text, 'jak');
            assert.strictEqual(model.revisions.length, 1, 'one input session should become one tracked insertion revision');
            assert.strictEqual(model.revisions[0].payload.text, 'jak');
            assert.strictEqual(debug.browserMutationUsed, false);
            assert.strictEqual(debug.mutationObserverMode, 'diagnostic-only');
            assert.ok(debug.boundaryPatchCount >= 1);

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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tm-doc-runtime-phase6-{Guid.NewGuid():N}.js");
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
