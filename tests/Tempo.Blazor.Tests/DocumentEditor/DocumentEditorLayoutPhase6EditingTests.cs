using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorLayoutPhase6EditingTests
{
    [Fact]
    public async Task Phase6_LayoutTextEditModel_InsertsOnFirstSecondAndThirdVisualLines()
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

            const edit = sandbox.window.tmDocumentEditorEngine.__testHooks.applyLayoutTextEditModel;
            const text = 'First visual line wraps into second visual line and then into third visual line.';
            const segments = [
                { Id: 's1', StartOffset: 0, Text: text.slice(0, 19) },
                { Id: 's2', StartOffset: 19, Text: text.slice(19, 45) },
                { Id: 's3', StartOffset: 45, Text: text.slice(45) }
            ];

            const first = edit(segments, { inputType: 'insertText', offset: 6, data: '[A]' });
            assert.strictEqual(first.Text, text.slice(0, 6) + '[A]' + text.slice(6));
            assert.strictEqual(first.CaretOffset, 9);

            const second = edit(segments, { inputType: 'insertText', offset: 24, data: '[B]' });
            assert.strictEqual(second.Text, text.slice(0, 24) + '[B]' + text.slice(24));
            assert.strictEqual(second.CaretOffset, 27);

            const third = edit(segments, { inputType: 'insertText', offset: 52, data: '[C]' });
            assert.strictEqual(third.Text, text.slice(0, 52) + '[C]' + text.slice(52));
            assert.strictEqual(third.CaretOffset, 55);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase6_LayoutTextEditModel_BackspaceAndDeleteUseLogicalOffsetsAcrossVisualLines()
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

            const edit = sandbox.window.tmDocumentEditorEngine.__testHooks.applyLayoutTextEditModel;
            const text = 'abcdefghijABCDEFGHIJklmnopqrst';
            const segments = [
                { Id: 'line-1', StartOffset: 0, Text: text.slice(0, 10) },
                { Id: 'line-2', StartOffset: 10, Text: text.slice(10, 20) },
                { Id: 'line-3', StartOffset: 20, Text: text.slice(20) }
            ];

            const backspace = edit(segments, { inputType: 'deleteContentBackward', offset: 10, unit: 'character' });
            assert.strictEqual(backspace.DeletedText, 'j');
            assert.strictEqual(backspace.Text, 'abcdefghiABCDEFGHIJklmnopqrst');
            assert.strictEqual(backspace.CaretOffset, 9);

            const del = edit(segments, { inputType: 'deleteContentForward', offset: 20, unit: 'character' });
            assert.strictEqual(del.DeletedText, 'k');
            assert.strictEqual(del.Text, 'abcdefghijABCDEFGHIJlmnopqrst');
            assert.strictEqual(del.CaretOffset, 20);

            const paragraphStart = edit(segments, { inputType: 'deleteContentBackward', offset: 0, unit: 'character' });
            assert.strictEqual(paragraphStart.Handled, false);
            assert.strictEqual(paragraphStart.MergePrevious, true);

            const paragraphEnd = edit(segments, { inputType: 'deleteContentForward', offset: text.length, unit: 'character' });
            assert.strictEqual(paragraphEnd.Handled, false);
            assert.strictEqual(paragraphEnd.MergeNext, true);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase6_LayoutTextEditModel_ParagraphBreakSplitsByLogicalOffset()
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

            const edit = sandbox.window.tmDocumentEditorEngine.__testHooks.applyLayoutTextEditModel;
            const text = 'One wrapped paragraph split from the second visual line.';
            const segments = [
                { Id: 'line-1', StartOffset: 0, Text: text.slice(0, 14) },
                { Id: 'line-2', StartOffset: 14, Text: text.slice(14, 39) },
                { Id: 'line-3', StartOffset: 39, Text: text.slice(39) }
            ];

            const split = edit(segments, { inputType: 'insertParagraph', offset: 18 });
            assert.strictEqual(split.Handled, true);
            assert.strictEqual(split.SplitBefore, text.slice(0, 18));
            assert.strictEqual(split.SplitAfter, text.slice(18));
            assert.strictEqual(split.StartOffset, 18);

            const splitAtWrappedLineStart = edit(segments, { inputType: 'insertParagraph', offset: 14 });
            assert.strictEqual(splitAtWrappedLineStart.Handled, true);
            assert.strictEqual(splitAtWrappedLineStart.SplitBefore, text.slice(0, 14));
            assert.strictEqual(splitAtWrappedLineStart.SplitAfter, text.slice(14));

            const splitAtWrappedLineEnd = edit(segments, { inputType: 'insertParagraph', offset: 39 });
            assert.strictEqual(splitAtWrappedLineEnd.Handled, true);
            assert.strictEqual(splitAtWrappedLineEnd.SplitBefore, text.slice(0, 39));
            assert.strictEqual(splitAtWrappedLineEnd.SplitAfter, text.slice(39));

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase6_LayoutSnapshot_SegmentsCarryBlockOffsetsAcrossInlineRuns()
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

            const layout = sandbox.window.tmDocumentEditorEngine.__testHooks.createLayoutSnapshotForRender({
                Blocks: [{
                    Id: 'p1',
                    Type: 0,
                    Content: {
                        $type: 'paragraph',
                        Inlines: [
                            { $type: 'text', Id: 'approved', Text: 'Approved text. ' },
                            {
                                $type: 'text',
                                Id: 'revision',
                                Text: 'Priority support.',
                                Marks: [{ Type: 'Revision', RevisionId: 'rev-1', Value: 'Insertion' }]
                            }
                        ]
                    }
                }]
            });
            const paragraph = layout.Pages[0].Paragraphs.find(item => item.BlockId === 'p1');
            const revisionSegments = paragraph.Lines
                .flatMap(line => line.Segments)
                .filter(segment => segment.InlineId === 'revision');

            assert.ok(revisionSegments.length > 0, 'revision segments should be present');
            assert.ok(revisionSegments.every(segment => segment.BlockStartOffset >= 'Approved text. '.length),
                'revision block offsets must include the approved inline prefix');
            assert.strictEqual(revisionSegments[0].BlockStartOffset, 'Approved text. '.length);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase6_LayoutSnapshot_EmptyTextRunRendersEditableSegment()
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

            const layout = sandbox.window.tmDocumentEditorEngine.__testHooks.createLayoutSnapshotForRender({
                Blocks: [{
                    Id: 'empty-paragraph',
                    Type: 0,
                    Content: {
                        $type: 'paragraph',
                        Inlines: [{ $type: 'text', Id: 'empty-inline', Text: '' }]
                    }
                }]
            });
            const paragraph = layout.Pages[0].Paragraphs.find(item => item.BlockId === 'empty-paragraph');
            const segments = paragraph.Lines.flatMap(line => line.Segments);

            assert.strictEqual(segments.length, 1);
            assert.strictEqual(segments[0].InlineId, 'empty-inline');
            assert.strictEqual(segments[0].Text, '');
            assert.strictEqual(segments[0].Length, 0);
            assert.strictEqual(segments[0].BlockStartOffset, 0);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase6_MarkNormalization_KnowsRevisionAndCommentAnchorMarks()
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

            const normalize = sandbox.window.tmDocumentEditorEngine.__testHooks.normalizeMarkType;
            assert.strictEqual(normalize('Revision'), 'Revision');
            assert.strictEqual(normalize(8), 'Revision');
            assert.strictEqual(normalize('revision-anchor'), 'Revision');
            assert.strictEqual(normalize('CommentAnchor'), 'CommentAnchor');
            assert.strictEqual(normalize(7), 'CommentAnchor');

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
