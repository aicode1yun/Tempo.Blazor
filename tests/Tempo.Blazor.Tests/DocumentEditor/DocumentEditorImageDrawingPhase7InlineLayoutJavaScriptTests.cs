using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase7InlineLayoutJavaScriptTests
{
    [Fact]
    public async Task Phase7_InlineDrawingParticipatesInParagraphLayoutAsInlineBox()
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
            const model = hooks.importFromCSharpJson(createDocument({ width: 42, height: 34 }));
            const block = model.body.blocks[0];
            const engine = hooks.createParagraphLayoutEngine(null, { minReadableWidth: 12 });
            const layout = engine.layoutParagraph(block, { x: 10, y: 20, width: 320, lineGap: 0, minReadableWidth: 12 });

            const line = layout.lines[0];
            assert.ok(line, 'inline drawing paragraph should produce a line');
            assert.strictEqual(layout.lines.length, 1, 'text before and after the inline drawing should remain on one line when there is room');
            assert.ok(line.rect.height >= 34, 'inline object height must increase the line box height');

            const objectSegment = layout.segments.find(segment => segment.kind === 'drawing');
            assert.ok(objectSegment, 'layout must include a drawing segment');
            assert.strictEqual(objectSegment.type, 'inlineObject');
            assert.strictEqual(objectSegment.objectId, 'phase7-inline-object');
            assert.strictEqual(objectSegment.rect.width, 42);
            assert.strictEqual(objectSegment.objectRect.width, 42);
            assert.strictEqual(objectSegment.objectRect.height, 34);

            const before = layout.segments.find(segment => segment.runId === 'before');
            const after = layout.segments.find(segment => segment.runId === 'after');
            assert.ok(before && after, 'text segments around the drawing must be preserved');
            assert.ok(before.rect.x + before.rect.width <= objectSegment.objectRect.x + 0.1, 'drawing starts after text before it');
            assert.ok(objectSegment.objectRect.x + objectSegment.objectRect.width <= after.rect.x + 0.1, 'text after drawing starts after object advance width');

            const beforeCaret = layout.caretStops.find(stop => stop.objectId === 'phase7-inline-object' && stop.affinity === 'before');
            const afterCaret = layout.caretStops.find(stop => stop.objectId === 'phase7-inline-object' && stop.affinity === 'after');
            assert.ok(beforeCaret, 'layout must expose a caret stop before the drawing object');
            assert.ok(afterCaret, 'layout must expose a caret stop after the drawing object');
            assert.strictEqual(beforeCaret.offset, 6);
            assert.strictEqual(afterCaret.offset, 6);
            assert.ok(Math.abs(beforeCaret.rect.x - objectSegment.objectRect.x) < 0.1, 'before-object caret x must match object left edge');
            assert.ok(Math.abs(afterCaret.rect.x - (objectSegment.objectRect.x + objectSegment.objectRect.width)) < 0.1, 'after-object caret x must include object advance width');

            assert.strictEqual(layout.inlineObjects.length, 1, 'paragraph layout must publish the inline object rect');
            assert.strictEqual(layout.inlineObjects[0].wrapMode, 'Inline');
            assert.strictEqual(layout.inlineObjects[0].createsTextExclusion, false, 'inline drawings must not create exclusion zones');
            assert.strictEqual(line.availableIntervals.length, 1, 'inline drawings must keep the normal editable line interval');
            assert.strictEqual(line.availableIntervals[0].width, 320);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "layout");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase7_InlineDrawingWrapsLikeSingleInlineBoxWhenItDoesNotFit()
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
            const model = hooks.importFromCSharpJson(createDocument({ width: 84, height: 28 }));
            const block = model.body.blocks[0];
            const engine = hooks.createParagraphLayoutEngine(null, { minReadableWidth: 12 });
            const layout = engine.layoutParagraph(block, { x: 0, y: 0, width: 100, lineGap: 0, minReadableWidth: 12 });

            const before = layout.segments.find(segment => segment.runId === 'before');
            const objectSegment = layout.segments.find(segment => segment.kind === 'drawing');
            assert.ok(before && objectSegment, 'text and drawing segments must both exist');
            assert.notStrictEqual(objectSegment.lineId, before.lineId, 'drawing should wrap to the next line when text plus object width does not fit');
            assert.ok(objectSegment.rect.y > before.rect.y, 'wrapped drawing must be placed below the preceding text line');
            assert.strictEqual(objectSegment.rect.width, 84);
            assert.strictEqual(objectSegment.objectRect.height, 28);

            const objectLine = layout.lines.find(line => line.id === objectSegment.lineId);
            assert.ok(objectLine, 'wrapped drawing line must exist');
            assert.ok(objectLine.rect.height >= 28, 'wrapped drawing line must use the object height');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "wrap");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase7_RenderParagraphRunsRendersInlineDrawingInsideTextFlow()
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
            const model = hooks.importFromCSharpJson(createDocument({ width: 42, height: 34 }));
            const block = model.body.blocks[0];
            const html = hooks.renderParagraphRunsHtmlForTest({
                model,
                selection: {
                    selectionMode: 'Object',
                    activeObjectId: 'phase7-inline-object',
                    objectSelection: { objectId: 'phase7-inline-object', anchorBlockId: 'p1' }
                }
            }, block, 1, 1);

            assert.ok(html.indexOf('Alpha ') >= 0, 'text before drawing must be rendered');
            assert.ok(html.indexOf(' omega') >= 0, 'text after drawing must be rendered');
            assert.ok(html.indexOf('tm-wysiwyg-inline-drawing') >= 0, 'drawing must render as inline drawing element');
            assert.ok(html.indexOf('data-object-id="phase7-inline-object"') >= 0, 'inline drawing must expose object id');
            assert.ok(html.indexOf('data-block-id="p1"') >= 0, 'inline drawing must stay anchored to paragraph block');
            assert.ok(html.indexOf('contenteditable="false"') >= 0, 'inline drawing must not become an editable text island');
            assert.ok(html.indexOf('width:42px') >= 0, 'inline drawing width must come from transform/size');
            assert.ok(html.indexOf('height:34px') >= 0, 'inline drawing height must come from transform/size');
            assert.ok(html.indexOf('<img ') >= 0, 'image drawing should render the image payload');
            assert.ok(html.indexOf('tm-wysiwyg-object--selected') >= 0, 'selected inline drawing must expose object selection styling');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "render");
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

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(string scriptPath, string nodeScript, string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase7-{scenario}-{Guid.NewGuid():N}.js");
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

        function createDocument(size) {
            return {
                DocumentId: 'image-drawing-phase7',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                { $type: 'text', Id: 'before', Text: 'Alpha ' },
                                {
                                    $type: 'drawing',
                                    Id: 'drawing-run',
                                    ObjectId: 'phase7-inline-object',
                                    Kind: 0,
                                    Source: 0,
                                    Url: '/phase7-inline.png',
                                    AltText: 'Phase 7 inline image',
                                    Size: { Width: size.width, Height: size.height },
                                    Layout: {
                                        Kind: 0,
                                        Wrap: { Mode: 0 },
                                        Anchor: { BlockId: 'p1', Offset: 6, InlineIndex: 1 },
                                        Transform: { Width: size.width, Height: size.height }
                                    }
                                },
                                { $type: 'text', Id: 'after', Text: ' omega' }
                            ]
                        }
                    }
                ]
            };
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
