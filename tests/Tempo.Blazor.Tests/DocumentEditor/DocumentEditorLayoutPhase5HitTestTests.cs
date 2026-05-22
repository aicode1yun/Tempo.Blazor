using System.Diagnostics;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorLayoutPhase5HitTestTests
{
    [Fact]
    public async Task Phase5_HitTestGeometry_DistinguishesControlsRegionsAndNone()
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

            const hitTest = sandbox.window.tmDocumentWysiwyg.__testHooks.hitTestLayoutGeometry;
            const base = {
                RootRect: { X: 0, Y: 0, Width: 640, Height: 760 },
                PageRects: [{ PageIndex: 0, Rect: { X: 20, Y: 20, Width: 560, Height: 700 } }],
                BodyRects: [{ PageIndex: 0, Rect: { X: 80, Y: 120, Width: 440, Height: 520 } }],
                HeaderFooters: [
                    { Kind: 'HeaderFooter', Region: 'Header', HeaderFooterId: 'header-1', PageIndex: 0, Rect: { X: 80, Y: 40, Width: 440, Height: 60 } },
                    { Kind: 'HeaderFooter', Region: 'Footer', HeaderFooterId: 'footer-1', PageIndex: 0, Rect: { X: 80, Y: 650, Width: 440, Height: 50 } }
                ],
                TableCells: [
                    { CellId: 'cell-a1', Rect: { X: 120, Y: 160, Width: 100, Height: 36 } }
                ],
                Controls: [
                    { Kind: 'ImageResizeHandle', ObjectId: 'object-1', BlockId: 'img-1', Rect: { X: 260, Y: 170, Width: 12, Height: 12 }, LayerPriority: 20, ZIndex: 4 },
                    { Kind: 'ImageRotateHandle', ObjectId: 'object-1', BlockId: 'img-1', Rect: { X: 300, Y: 140, Width: 16, Height: 16 }, LayerPriority: 20, ZIndex: 4 },
                    { Kind: 'ImageLayoutBubble', ObjectId: 'object-1', BlockId: 'img-1', Rect: { X: 330, Y: 170, Width: 50, Height: 20 }, LayerPriority: 20, ZIndex: 4 }
                ],
                Objects: [],
                Lines: []
            };

            const at = (x, y) => hitTest({ ...base, X: x, Y: y });

            assert.strictEqual(at(262, 172).Kind, 'ImageResizeHandle');
            assert.strictEqual(at(304, 144).Kind, 'ImageRotateHandle');
            assert.strictEqual(at(340, 178).Kind, 'ImageLayoutBubble');

            const table = at(140, 170);
            assert.strictEqual(table.Kind, 'TableCell');
            assert.strictEqual(table.CellId, 'cell-a1');

            const header = at(110, 60);
            assert.strictEqual(header.Kind, 'HeaderFooter');
            assert.strictEqual(header.Region, 'Header');
            assert.strictEqual(header.HeaderFooterId, 'header-1');

            const footer = at(110, 670);
            assert.strictEqual(footer.Kind, 'HeaderFooter');
            assert.strictEqual(footer.Region, 'Footer');

            assert.strictEqual(at(40, 180).Kind, 'PageMargin');
            assert.strictEqual(at(620, 180).Kind, 'None');

            assert.strictEqual(typeof sandbox.window.tmDocumentWysiwyg.DocumentHitTestService, 'function');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase5_HitTestGeometry_MapsCaretOnFirstSecondAndThirdWrappedLines()
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

            const hitTest = sandbox.window.tmDocumentWysiwyg.__testHooks.hitTestLayoutGeometry;
            const line = (index, y, startOffset) => ({
                Id: `line-${index}`,
                BlockId: 'paragraph-1',
                VisualLineIndex: index,
                Rect: { X: 180, Y: y, Width: 220, Height: 16 },
                Segments: [{
                    Id: `segment-${index}`,
                    LineId: `line-${index}`,
                    InlineId: 'inline-1',
                    BlockId: 'paragraph-1',
                    StartOffset: startOffset,
                    TextLength: 20,
                    Rect: { X: 180, Y: y, Width: 220, Height: 16 }
                }]
            });
            const request = {
                RootRect: { X: 0, Y: 0, Width: 640, Height: 760 },
                PageRects: [{ PageIndex: 0, Rect: { X: 0, Y: 0, Width: 600, Height: 740 } }],
                BodyRects: [{ PageIndex: 0, Rect: { X: 40, Y: 20, Width: 520, Height: 680 } }],
                HeaderFooters: [],
                TableCells: [],
                Controls: [],
                Objects: [{
                    Kind: 'ImageObject',
                    ObjectId: 'image-object-1',
                    BlockId: 'image-1',
                    Layer: 'object',
                    LayerPriority: 20,
                    ZIndex: 1,
                    Rect: { X: 40, Y: 20, Width: 100, Height: 60 },
                    WrapRect: { X: 40, Y: 20, Width: 130, Height: 80 },
                    VisualRects: [{ X: 40, Y: 20, Width: 100, Height: 60 }],
                    Selectable: true
                }],
                Lines: [
                    line(0, 24, 0),
                    line(1, 44, 20),
                    line(2, 64, 40)
                ]
            };

            const at = (x, y) => hitTest({ ...request, X: x, Y: y });

            const firstStart = at(180, 28);
            assert.strictEqual(firstStart.Kind, 'TextCaret');
            assert.strictEqual(firstStart.LayoutLineId, 'line-0');
            assert.strictEqual(firstStart.LayoutSegmentId, 'segment-0');
            assert.strictEqual(firstStart.VisualLineIndex, 0);
            assert.strictEqual(firstStart.Offset, 0);

            const firstMiddle = at(290, 28);
            assert.strictEqual(firstMiddle.Kind, 'TextCaret');
            assert.strictEqual(firstMiddle.Offset, 10);

            const firstEnd = at(400, 28);
            assert.strictEqual(firstEnd.Kind, 'TextCaret');
            assert.strictEqual(firstEnd.Offset, 20);

            const secondLine = at(290, 48);
            assert.strictEqual(secondLine.Kind, 'TextCaret');
            assert.strictEqual(secondLine.LayoutLineId, 'line-1');
            assert.strictEqual(secondLine.VisualLineIndex, 1);
            assert.strictEqual(secondLine.Offset, 30);

            const thirdLine = at(290, 68);
            assert.strictEqual(thirdLine.Kind, 'TextCaret');
            assert.strictEqual(thirdLine.LayoutLineId, 'line-2');
            assert.strictEqual(thirdLine.VisualLineIndex, 2);
            assert.strictEqual(thirdLine.Offset, 50);

            const leftGapBetweenWrapAndText = at(160, 28);
            assert.strictEqual(leftGapBetweenWrapAndText.Kind, 'TextCaret');
            assert.strictEqual(leftGapBetweenWrapAndText.LayoutLineId, 'line-0');
            assert.strictEqual(leftGapBetweenWrapAndText.Offset, 0);

            const insideWrapButOutsideVisual = at(160, 48);
            assert.strictEqual(insideWrapButOutsideVisual.Kind, 'TextCaret');
            assert.notStrictEqual(insideWrapButOutsideVisual.Kind, 'ImageObject');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase5_HitTestGeometry_SelectsOnlyVisualObjectAndHonorsLayerAndZIndex()
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

            const hitTest = sandbox.window.tmDocumentWysiwyg.__testHooks.hitTestLayoutGeometry;
            const request = {
                RootRect: { X: 0, Y: 0, Width: 720, Height: 760 },
                PageRects: [{ PageIndex: 0, Rect: { X: 0, Y: 0, Width: 680, Height: 740 } }],
                BodyRects: [{ PageIndex: 0, Rect: { X: 20, Y: 20, Width: 640, Height: 680 } }],
                HeaderFooters: [],
                TableCells: [],
                Controls: [],
                Objects: [
                    {
                        Kind: 'ImageObject',
                        ObjectId: 'left-object',
                        BlockId: 'left-image',
                        Layer: 'object',
                        LayerPriority: 20,
                        ZIndex: 1,
                        Rect: { X: 40, Y: 40, Width: 100, Height: 80 },
                        VisualRects: [{ X: 40, Y: 40, Width: 100, Height: 80 }],
                        Selectable: true
                    },
                    {
                        Kind: 'ImageObject',
                        ObjectId: 'second-object',
                        BlockId: 'second-image',
                        Layer: 'object',
                        LayerPriority: 20,
                        ZIndex: 2,
                        Rect: { X: 170, Y: 40, Width: 100, Height: 80 },
                        VisualRects: [{ X: 170, Y: 40, Width: 100, Height: 80 }],
                        Selectable: true
                    },
                    {
                        Kind: 'ImageObject',
                        ObjectId: 'covered-object',
                        BlockId: 'covered-image',
                        Layer: 'object',
                        LayerPriority: 20,
                        ZIndex: 1,
                        Rect: { X: 100, Y: 170, Width: 120, Height: 80 },
                        VisualRects: [{ X: 100, Y: 170, Width: 120, Height: 80 }],
                        Selectable: true
                    },
                    {
                        Kind: 'ImageObject',
                        ObjectId: 'front-object',
                        BlockId: 'front-image',
                        Layer: 'in-front-of-text',
                        LayerPriority: 30,
                        ZIndex: 0,
                        Rect: { X: 120, Y: 190, Width: 120, Height: 80 },
                        VisualRects: [{ X: 120, Y: 190, Width: 120, Height: 80 }],
                        Selectable: true
                    },
                    {
                        Kind: 'ImageObject',
                        ObjectId: 'low-z-object',
                        BlockId: 'low-z-image',
                        Layer: 'object',
                        LayerPriority: 20,
                        ZIndex: 1,
                        Rect: { X: 280, Y: 170, Width: 120, Height: 80 },
                        VisualRects: [{ X: 280, Y: 170, Width: 120, Height: 80 }],
                        Selectable: true
                    },
                    {
                        Kind: 'ImageObject',
                        ObjectId: 'high-z-object',
                        BlockId: 'high-z-image',
                        Layer: 'object',
                        LayerPriority: 20,
                        ZIndex: 9,
                        Rect: { X: 300, Y: 190, Width: 120, Height: 80 },
                        VisualRects: [{ X: 300, Y: 190, Width: 120, Height: 80 }],
                        Selectable: true
                    },
                    {
                        Kind: 'ImageObject',
                        ObjectId: 'behind-text-object',
                        BlockId: 'behind-text-image',
                        Layer: 'behind-text',
                        LayerPriority: 0,
                        ZIndex: 20,
                        Rect: { X: 430, Y: 170, Width: 120, Height: 80 },
                        VisualRects: [{ X: 430, Y: 170, Width: 120, Height: 80 }],
                        Selectable: false
                    },
                    {
                        Kind: 'ImageObject',
                        ObjectId: 'captioned-object',
                        BlockId: 'captioned-image',
                        Layer: 'object',
                        LayerPriority: 20,
                        ZIndex: 1,
                        Rect: { X: 40, Y: 300, Width: 120, Height: 100 },
                        VisualRects: [
                            { X: 40, Y: 300, Width: 120, Height: 70 },
                            { X: 40, Y: 374, Width: 120, Height: 20 }
                        ],
                        Selectable: true
                    },
                    {
                        Kind: 'ImageObject',
                        ObjectId: 'selection-box-object',
                        BlockId: 'selection-box-image',
                        Layer: 'object',
                        LayerPriority: 20,
                        ZIndex: 1,
                        Rect: { X: 190, Y: 300, Width: 120, Height: 80 },
                        VisualRects: [{ X: 186, Y: 296, Width: 128, Height: 88 }],
                        Selectable: true
                    }
                ],
                Lines: [{
                    Id: 'line-around-image',
                    BlockId: 'paragraph-1',
                    VisualLineIndex: 0,
                    Rect: { X: 150, Y: 45, Width: 360, Height: 16 },
                    Segments: [{
                        Id: 'segment-around-image',
                        LineId: 'line-around-image',
                        InlineId: 'inline-1',
                        BlockId: 'paragraph-1',
                        StartOffset: 0,
                        TextLength: 40,
                        Rect: { X: 150, Y: 45, Width: 360, Height: 16 }
                    }]
                }, {
                    Id: 'line-behind-text',
                    BlockId: 'paragraph-2',
                    VisualLineIndex: 1,
                    Rect: { X: 420, Y: 190, Width: 180, Height: 16 },
                    Segments: [{
                        Id: 'segment-behind-text',
                        LineId: 'line-behind-text',
                        InlineId: 'inline-2',
                        BlockId: 'paragraph-2',
                        StartOffset: 0,
                        TextLength: 20,
                        Rect: { X: 420, Y: 190, Width: 180, Height: 16 }
                    }]
                }]
            };

            const at = (x, y) => hitTest({ ...request, X: x, Y: y });

            const left = at(60, 60);
            assert.strictEqual(left.Kind, 'ImageObject');
            assert.strictEqual(left.ActiveImageBlockId, 'left-image');
            assert.strictEqual(left.ActiveObjectId, 'left-object');

            const farRightOfLeft = at(150, 60);
            assert.strictEqual(farRightOfLeft.Kind, 'TextCaret');
            assert.notStrictEqual(farRightOfLeft.ActiveImageBlockId, 'left-image');

            const second = at(190, 60);
            assert.strictEqual(second.Kind, 'ImageObject');
            assert.strictEqual(second.ActiveImageBlockId, 'second-image');

            const inFrontWins = at(130, 200);
            assert.strictEqual(inFrontWins.Kind, 'ImageObject');
            assert.strictEqual(inFrontWins.ActiveImageBlockId, 'front-image');

            const zIndexWins = at(320, 205);
            assert.strictEqual(zIndexWins.Kind, 'ImageObject');
            assert.strictEqual(zIndexWins.ActiveImageBlockId, 'high-z-image');

            const behindTextDoesNotBlockCaret = at(450, 195);
            assert.strictEqual(behindTextDoesNotBlockCaret.Kind, 'TextCaret');
            assert.strictEqual(behindTextDoesNotBlockCaret.LayoutLineId, 'line-behind-text');
            assert.strictEqual(behindTextDoesNotBlockCaret.ActiveImageBlockId, null);

            const captionClick = at(55, 382);
            assert.strictEqual(captionClick.Kind, 'ImageObject');
            assert.strictEqual(captionClick.ActiveImageBlockId, 'captioned-image');

            const selectionBoxClick = at(188, 298);
            assert.strictEqual(selectionBoxClick.Kind, 'ImageObject');
            assert.strictEqual(selectionBoxClick.ActiveImageBlockId, 'selection-box-image');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public void Phase5_SelectionModelsExposeLayoutHitTargetState()
    {
        var snapshot = new WysiwygSelectionSnapshot
        {
            LayoutLineId = "line-1",
            LayoutSegmentId = "segment-1",
            VisualLineIndex = 2,
            ActiveObjectId = "object-1",
            HitTargetKind = "TextCaret"
        };

        var state = new DocumentEditorSelectionState
        {
            LayoutLineId = snapshot.LayoutLineId,
            LayoutSegmentId = snapshot.LayoutSegmentId,
            VisualLineIndex = snapshot.VisualLineIndex,
            ActiveObjectId = snapshot.ActiveObjectId,
            HitTargetKind = snapshot.HitTargetKind
        };

        state.LayoutLineId.Should().Be("line-1");
        state.LayoutSegmentId.Should().Be("segment-1");
        state.VisualLineIndex.Should().Be(2);
        state.ActiveObjectId.Should().Be("object-1");
        state.HitTargetKind.Should().Be("TextCaret");

        state.Clear();
        state.LayoutLineId.Should().BeNull();
        state.LayoutSegmentId.Should().BeNull();
        state.VisualLineIndex.Should().BeNull();
        state.ActiveObjectId.Should().BeNull();
        state.HitTargetKind.Should().BeNull();
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-blazor-phase5-hit-test-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, nodeScript);
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { tempFile, scriptPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }) ?? throw new InvalidOperationException("Could not start node process.");

            var standardOutput = await process.StandardOutput.ReadToEndAsync();
            var standardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, standardOutput, standardError);
        }
        finally
        {
            try
            {
                File.Delete(tempFile);
            }
            catch
            {
                // Best-effort cleanup of the temporary node test script.
            }
        }
    }
}
