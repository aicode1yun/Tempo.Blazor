using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase10AnnotationBoundaryJavaScriptTests
{
    [Fact]
    public async Task Phase10_CommentMembership_IsNotFormattingHighlightState()
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
                DocumentId: 'phase10-highlight',
                Blocks: [{
                    Id: 'p1',
                    Type: 'Paragraph',
                    Content: {
                        Inlines: [
                            { Id: 'commented', Text: 'commented', CommentIds: ['c1'] },
                            { Id: 'plain', Text: ' plain' }
                        ]
                    }
                }],
                Comments: [{ Id: 'c1' }]
            });

            const state = hooks.computeFormattingState(model, {
                region: 'Body',
                blockId: 'p1',
                offset: 4,
                isCollapsed: true
            });

            assert.strictEqual(state.highlightColor, null);
            assert.strictEqual(state.backgroundColor, null);
            assert.strictEqual(state.commandValues.backgroundColor, null);
            assert.strictEqual(state.inline.mixed.backgroundColor, false);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase10_InsertText_InsideCommentRangeInheritsCommentMembership()
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
            const model = createCommentModel(hooks);

            const result = hooks.applyOperation(model, hooks.createOperation('InsertText', {
                target: { blockId: 'p1', offset: 2 },
                text: 'X'
            }, { source: 'phase10' }));

            const inserted = runAtOffset(model.body.blocks[0], 2);
            const marker = hooks.buildRuntimeCommentMarkers(model).find(item => item.targetId === 'c1');

            assert.strictEqual(result.ok, true);
            assert.ok(inserted, 'inserted run should exist');
            assert.ok(inserted.text.includes('X'), `inserted text should remain in the commented run, got '${inserted.text}'`);
            assertCommentIds(inserted, ['c1']);
            assert.strictEqual(marker.startOffset, 0);
            assert.strictEqual(marker.endOffset, 5);

            console.log('OK');

            function createCommentModel(hooks) {
                return hooks.importFromCSharpJson({
                    DocumentId: 'phase10-inside',
                    Blocks: [{
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            Inlines: [
                                { Id: 'commented', Text: 'with', CommentIds: ['c1'] },
                                { Id: 'plain', Text: ' plain' }
                            ]
                        }
                    }],
                    Comments: [{ Id: 'c1' }]
                });
            }

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

            function assertCommentIds(run, expected) {
                assert.strictEqual(JSON.stringify(Array.from(run.commentIds || [])), JSON.stringify(expected));
            }
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase10_InsertText_AtCommentEdgesStaysOutsideCommentMembership()
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

            const right = createCommentModel(hooks);
            hooks.applyOperation(right, hooks.createOperation('InsertText', {
                target: { blockId: 'p1', offset: 4 },
                text: 'R'
            }, { source: 'phase10' }));
            const rightInserted = runAtOffset(right.body.blocks[0], 4);
            const rightPlainTail = right.body.blocks[0].content.runs.find(run => (run.text || '').includes(' plain'));
            const rightMarker = hooks.buildRuntimeCommentMarkers(right).find(item => item.targetId === 'c1');

            assert.ok(rightInserted, 'right-edge inserted run should exist');
            assert.ok(rightInserted.text.startsWith('R'), `right-edge run should begin with inserted text, got '${rightInserted.text}'`);
            assertCommentIds(rightInserted, []);
            assert.ok(rightPlainTail, 'plain text after the comment range should exist');
            assertCommentIds(rightPlainTail, []);
            assert.strictEqual(rightMarker.startOffset, 0);
            assert.strictEqual(rightMarker.endOffset, 4);

            const left = createCommentModel(hooks);
            hooks.applyOperation(left, hooks.createOperation('InsertText', {
                target: { blockId: 'p1', offset: 0 },
                text: 'L'
            }, { source: 'phase10' }));
            const leftInserted = left.body.blocks[0].content.runs.find(run => run.text === 'L');
            const leftMarker = hooks.buildRuntimeCommentMarkers(left).find(item => item.targetId === 'c1');

            assert.ok(leftInserted, 'left-edge inserted run should exist');
            assertCommentIds(leftInserted, []);
            assert.strictEqual(leftMarker.startOffset, 1);
            assert.strictEqual(leftMarker.endOffset, 5);

            console.log('OK');

            function createCommentModel(hooks) {
                return hooks.importFromCSharpJson({
                    DocumentId: 'phase10-edges',
                    Blocks: [{
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            Inlines: [
                                { Id: 'commented', Text: 'with', CommentIds: ['c1'] },
                                { Id: 'plain', Text: ' plain' }
                            ]
                        }
                    }],
                    Comments: [{ Id: 'c1' }]
                });
            }

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

            function assertCommentIds(run, expected) {
                assert.strictEqual(JSON.stringify(Array.from(run.commentIds || [])), JSON.stringify(expected));
            }
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase10_CommentAnchorTransform_UsesExplicitInsertionGravity()
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
            const comments = [{
                Id: 'c1',
                Anchor: {
                    Type: 1,
                    BlockId: 'p1',
                    StartOffset: 10,
                    EndOffset: 20
                }
            }];

            const left = hooks.transformRuntimeCommentAnchorsForTextChange(comments, 'p1', 10, 3, false)[0].Anchor;
            const inside = hooks.transformRuntimeCommentAnchorsForTextChange(comments, 'p1', 15, 3, false)[0].Anchor;
            const right = hooks.transformRuntimeCommentAnchorsForTextChange(comments, 'p1', 20, 3, false)[0].Anchor;

            assert.strictEqual(left.StartOffset, 13, 'left edge insertion is outside and shifts the comment right');
            assert.strictEqual(left.EndOffset, 23);
            assert.strictEqual(inside.StartOffset, 10);
            assert.strictEqual(inside.EndOffset, 23);
            assert.strictEqual(right.StartOffset, 10);
            assert.strictEqual(right.EndOffset, 20, 'right edge insertion must not extend the comment');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    private static string GetWysiwygScriptPath()
        => Path.Combine(FindRepositoryRoot(), "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");

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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-phase10-annotation-boundary-{Guid.NewGuid():N}.js");
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
                Promise
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
            sandbox.window.performance = { now: () => Date.now() };
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
