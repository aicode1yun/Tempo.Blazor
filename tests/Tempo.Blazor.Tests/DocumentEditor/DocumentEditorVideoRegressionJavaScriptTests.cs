using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorVideoRegressionJavaScriptTests
{
    [Fact]
    public async Task FormattingDispatcher_SynchronizesCollapsedAndRangeFontState()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[1], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const model = hooks.importFromCSharpJson({
                DocumentId: 'video-regression-formatting',
                Blocks: [
                    { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Acme s.r.o.' }] } }
                ]
            });

            const collapsed = hooks.createCommandDispatcher(model, {
                selection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });
            assert.strictEqual(collapsed.executeCommand('setFontSize', { Value: '28pt' }).ok, true);
            assert.strictEqual(collapsed.executeCommand('textColor', { Value: '#2563eb' }).ok, true);
            const collapsedState = collapsed.getFormattingSnapshot();
            assert.strictEqual(collapsedState.commandValues.fontSize, '28pt');
            assert.strictEqual(collapsedState.commandValues.textColor, '#2563eb');
            assert.strictEqual(
                JSON.stringify(collapsed.getPendingTypingMarks().map(mark => Number(mark.type ?? mark.Type)).sort((left, right) => left - right)),
                JSON.stringify([10, 12]));

            const ranged = hooks.createCommandDispatcher(model, {
                selection: {
                    blockId: 'p1',
                    offset: 0,
                    anchor: { blockId: 'p1', offset: 0 },
                    focus: { blockId: 'p1', offset: 4 },
                    isCollapsed: false
                }
            });
            assert.strictEqual(ranged.executeCommand('setFontSize', { Value: '28pt' }).ok, true);
            assert.ok(model.body.blocks[0].content.runs.some(run =>
                (run.text || '').includes('Acme')
                && (run.marks || []).some(mark => Number(mark.type ?? mark.Type) === 12 && (mark.value || mark.Value) === '28pt')));

            const numericEnumModel = hooks.importFromCSharpJson({
                DocumentId: 'video-regression-numeric-font-size',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            Inlines: [
                                {
                                    Id: 'r1',
                                    Text: 'Service',
                                    Marks: [{ Type: 12, Value: '24pt' }]
                                }
                            ]
                        }
                    }
                ]
            });
            const numericEnumDispatcher = hooks.createCommandDispatcher(numericEnumModel, {
                selection: {
                    blockId: 'p1',
                    anchor: { blockId: 'p1', offset: 0 },
                    focus: { blockId: 'p1', offset: 7 },
                    isCollapsed: false
                }
            });
            assert.strictEqual(numericEnumDispatcher.executeCommand('setFontSize', { Value: '28pt' }).ok, true);
            const fontSizeMarks = numericEnumModel.body.blocks[0].content.runs
                .flatMap(run => run.marks || [])
                .filter(mark => String(mark.type ?? mark.Type).toLowerCase() === 'fontsize' || Number(mark.type ?? mark.Type) === 12);
            assert.strictEqual(JSON.stringify(fontSizeMarks.map(mark => mark.value ?? mark.Value)), JSON.stringify(['28pt']));

            const paragraphModel = hooks.importFromCSharpJson({
                DocumentId: 'video-regression-paragraph-alignment',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        ParagraphProperties: { Alignment: 2 },
                        Content: { Inlines: [{ Id: 'r1', Text: 'Aligned' }] }
                    }
                ]
            });
            assert.strictEqual(String(paragraphModel.body.blocks[0].content.alignment), '2');
            paragraphModel.body.blocks[0].content.alignment = 'right';
            const exported = hooks.exportToCSharpJson(paragraphModel);
            assert.strictEqual(exported.Blocks[0].ParagraphProperties.Alignment, 2);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task InsertText_AtCommentBoundary_DoesNotExtendInlineCommentAnchor()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[1], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const model = hooks.importFromCSharpJson({
                DocumentId: 'video-regression-comment-boundary',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            Inlines: [
                                { Id: 'commented', Text: 'with', CommentIds: ['c1'] },
                                { Id: 'plain', Text: ' Acme s.r.o.' }
                            ]
                        }
                    }
                ],
                Comments: [{ Id: 'c1' }]
            });

            const op = hooks.createOperation('InsertText', {
                target: { blockId: 'p1', offset: 4 },
                text: ' fff',
                marks: []
            }, { source: 'test' });
            const result = hooks.applyOperation(model, op);
            assert.strictEqual(result.ok, true);
            const inserted = runAtOffset(model.body.blocks[0], 4);
            const marker = hooks.buildRuntimeCommentMarkers(model).find(item => item.targetId === 'c1');
            assert.ok(inserted, 'inserted run should exist');
            assert.ok((inserted.text || '').startsWith(' fff'), `inserted run should start with typed text, got '${inserted.text || ''}'`);
            assert.strictEqual(JSON.stringify(Array.from(inserted.commentIds || inserted.CommentIds || [])), '[]');
            assert.strictEqual(marker.startOffset, 0);
            assert.strictEqual(marker.endOffset, 4);

            console.log('OK');

            function runAtOffset(block, offset) {
                let cursor = 0;
                for (const run of block.content.runs) {
                    const text = run.text || '';
                    const start = cursor;
                    const end = cursor + text.length;
                    if (offset >= start && offset < end) return run;
                    cursor = end;
                }

                return null;
            }
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task KeyboardSelection_UsesRememberedCaretWhenDomSelectionSnapsToBlockStart()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[1], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const inst = {
                lastKeyboardSelection: { blockId: 'p1', offset: 3, isCollapsed: true },
                lastKeyboardSelectionExpiresAt: Date.now() + 1000,
                lastKeyboardInputAt: Date.now()
            };

            const chosen = hooks.chooseKeyboardSelection(
                inst,
                { blockId: 'p1', offset: 0, isCollapsed: true },
                'keydown-dom');

            assert.strictEqual(chosen.blockId, 'p1');
            assert.strictEqual(chosen.offset, 3);

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
            process?.WaitForExit(3000);
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
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", nodeScript, scriptPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
