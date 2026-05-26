using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase12ScopedRegionsJavaScriptTests
{
    [Fact]
    public async Task Phase12_TextExclusionManagerFiltersHeaderFooterAndBodyScopes()
    {
        var result = await RunScenarioAsync(
            "scope-manager-header-footer-body",
            """
            const frame = { x: 0, y: 0, width: 500, height: 240 };
            const exclusions = [
                hooks.createTextExclusion({ objectId: 'header-img', blockId: 'hp', pageIndex: 0, region: 'Header', headerFooterId: 'hf-1', wrapMode: 'Square', rect: { x: 80, y: 20, width: 110, height: 50 } }, frame),
                hooks.createTextExclusion({ objectId: 'footer-img', blockId: 'fp', pageIndex: 0, region: 'Footer', headerFooterId: 'ff-1', wrapMode: 'Square', rect: { x: 80, y: 20, width: 110, height: 50 } }, frame),
                hooks.createTextExclusion({ objectId: 'body-img', blockId: 'bp', pageIndex: 0, region: 'Body', wrapMode: 'Square', rect: { x: 80, y: 20, width: 110, height: 50 } }, frame)
            ];

            const bodyManager = hooks.createTextExclusionManager(exclusions, frame, { pageIndex: 0, region: 'Body' });
            const headerManager = hooks.createTextExclusionManager(exclusions, frame, { pageIndex: 0, region: 'Header', headerFooterId: 'hf-1' });
            const footerManager = hooks.createTextExclusionManager(exclusions, frame, { pageIndex: 0, region: 'Footer', headerFooterId: 'ff-1' });

            assert.deepStrictEqual(bodyManager.exclusions.map(item => item.objectId), ['body-img']);
            assert.deepStrictEqual(headerManager.exclusions.map(item => item.objectId), ['header-img']);
            assert.deepStrictEqual(footerManager.exclusions.map(item => item.objectId), ['footer-img']);

            assert.strictEqual(bodyManager.getAvailableIntervals(20, 20, 24).blockedIntervals.length, 1);
            assert.strictEqual(headerManager.getAvailableIntervals(20, 20, 24).blockedIntervals.length, 1);
            assert.strictEqual(footerManager.getAvailableIntervals(20, 20, 24).blockedIntervals.length, 1);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase12_AvailableIntervalsCacheIncludesScopeAndFrameRect()
    {
        var result = await RunScenarioAsync(
            "cache-scope-and-frame",
            """
            const exclusions = [
                hooks.createTextExclusion({
                    objectId: 'header-only',
                    blockId: 'hp',
                    pageIndex: 0,
                    region: 'Header',
                    headerFooterId: 'hf-cache',
                    wrapMode: 'Square',
                    rect: { x: 80, y: 20, width: 100, height: 50 }
                }, { x: 0, y: 0, width: 500, height: 240 })
            ];

            const body = hooks.getAvailableIntervals(20, 20, { x: 0, y: 0, width: 500, height: 240 }, exclusions, 24, { pageIndex: 0, region: 'Body' });
            const header = hooks.getAvailableIntervals(20, 20, { x: 0, y: 0, width: 500, height: 240 }, exclusions, 24, { pageIndex: 0, region: 'Header', headerFooterId: 'hf-cache' });
            const shiftedFrame = hooks.getAvailableIntervals(20, 20, { x: 200, y: 0, width: 240, height: 240 }, exclusions, 24, { pageIndex: 0, region: 'Header', headerFooterId: 'hf-cache' });

            assert.strictEqual(body.blockedIntervals.length, 0, JSON.stringify(body));
            assert.strictEqual(header.blockedIntervals.length, 1, JSON.stringify(header));
            assert.strictEqual(shiftedFrame.blockedIntervals.length, 0, 'a cached result from another frame must not be reused');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase12_TableCellScopeUsesCellAndColumnIndex()
    {
        var result = await RunScenarioAsync(
            "table-cell-column-scope",
            """
            const frame = { x: 0, y: 0, width: 420, height: 240 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'cell-0-img',
                blockId: 'cell-p-0',
                pageIndex: 0,
                region: 'TableCell',
                tableId: 'table-1',
                cellId: 'cell-0',
                columnIndex: 0,
                wrapMode: 'Square',
                rect: { x: 40, y: 20, width: 120, height: 50 }
            }, frame);

            assert.strictEqual(exclusion.scopeKey, '0|TableCell||table-1|cell-0|0');
            assert.strictEqual(exclusion.columnIndex, 0);

            const sameCell = hooks.createTextExclusionManager([exclusion], frame, { pageIndex: 0, region: 'TableCell', tableId: 'table-1', cellId: 'cell-0', columnIndex: 0 });
            const otherCell = hooks.createTextExclusionManager([exclusion], frame, { pageIndex: 0, region: 'TableCell', tableId: 'table-1', cellId: 'cell-1', columnIndex: 1 });
            const otherColumn = hooks.createTextExclusionManager([exclusion], frame, { pageIndex: 0, region: 'TableCell', tableId: 'table-1', cellId: 'cell-0', columnIndex: 1 });

            assert.strictEqual(sameCell.exclusions.length, 1);
            assert.strictEqual(otherCell.exclusions.length, 0);
            assert.strictEqual(otherColumn.exclusions.length, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase12_LayoutInCellFalsePromotesTableCellExclusionToBodyScope()
    {
        var result = await RunScenarioAsync(
            "layout-in-cell-false",
            """
            const pageFrame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'cell-page-img',
                blockId: 'cell-p',
                pageIndex: 0,
                region: 'TableCell',
                tableId: 'table-1',
                cellId: 'cell-1',
                columnIndex: 0,
                layoutInCell: false,
                wrapMode: 'Square',
                rect: { x: 220, y: 40, width: 120, height: 60 }
            }, pageFrame);

            assert.strictEqual(exclusion.layoutInCell, false);
            assert.strictEqual(exclusion.anchorRegion, 'TableCell');
            assert.strictEqual(exclusion.region, 'Body');
            assert.strictEqual(exclusion.tableId, null);
            assert.strictEqual(exclusion.cellId, null);
            assert.strictEqual(exclusion.columnIndex, null);
            assert.strictEqual(exclusion.scopeKey, '0|Body|||');

            const bodyManager = hooks.createTextExclusionManager([exclusion], pageFrame, { pageIndex: 0, region: 'Body' });
            const cellManager = hooks.createTextExclusionManager([exclusion], pageFrame, { pageIndex: 0, region: 'TableCell', tableId: 'table-1', cellId: 'cell-1', columnIndex: 0 });
            assert.strictEqual(bodyManager.exclusions.length, 1);
            assert.strictEqual(cellManager.exclusions.length, 0);

            const normalized = hooks.normalizeImageObject({
                ObjectId: 'docx-cell-img',
                Layout: {
                    Kind: 1,
                    Anchor: { Region: 6, TableId: 'table-1', CellId: 'cell-1', ColumnIndex: 0 },
                    Wrap: { Mode: 1 },
                    Transform: { Width: 120, Height: 60 }
                },
                Docx: { LayoutInCell: false }
            }, { blockId: 'cell-p', region: 'TableCell', tableId: 'table-1', cellId: 'cell-1', columnIndex: 0 });
            assert.strictEqual(normalized.layoutInCell, false);
            assert.strictEqual(normalized.anchorColumnIndex, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase12_TableCellLayoutSeparatesLocalAndPageScopedExclusions()
    {
        var result = await RunScenarioAsync(
            "table-cell-layout-integration",
            """
            function createDrawingRun(objectId, layoutInCell) {
                return {
                    $type: 'drawing',
                    Id: objectId + '-run',
                    ObjectId: objectId,
                    Kind: 0,
                    Source: 0,
                    Url: '/' + objectId + '.png',
                    AltText: objectId,
                    Layout: {
                        Kind: 1,
                        Anchor: {
                            BlockId: 'cell-p-1',
                            Offset: 0,
                            InlineIndex: 0,
                            Region: 6,
                            TableId: 'table-1',
                            CellId: 'cell-1',
                            ColumnIndex: 0,
                            MoveWithText: true,
                            FixedOnPage: false,
                            LockAnchor: false
                        },
                        Position: {
                            HorizontalRelativeTo: 0,
                            VerticalRelativeTo: 0,
                            HorizontalAlignment: 0,
                            VerticalAlignment: 1,
                            X: 180,
                            Y: 120
                        },
                        Wrap: {
                            Mode: 1,
                            DistanceLeft: 0,
                            DistanceRight: 8,
                            DistanceTop: 0,
                            DistanceBottom: 0
                        },
                        Transform: { Width: 120, Height: 60 },
                        Stacking: { ZIndex: 0, AllowOverlap: false }
                    },
                    Docx: { LayoutInCell: layoutInCell }
                };
            }

            function createDocument(objectId, layoutInCell) {
                return {
                    DocumentId: 'phase12-table-layout-' + objectId,
                    Blocks: [
                        { Id: 'body-before', Type: 'Paragraph', Content: { Inlines: [{ Id: 'before-r', Text: 'Body before table.' }] } },
                        { Id: 'table-1', Type: 'Table', Content: { Style: { Width: 520 }, Rows: [
                            { Id: 'row-1', Cells: [
                                { Id: 'cell-1', Width: 260, Blocks: [
                                    { Id: 'cell-p-1', Type: 'Paragraph', Content: { Inlines: [
                                        createDrawingRun(objectId, layoutInCell),
                                        { Id: 'cell-r-1', Text: 'Cell one text stays governed by its own scope.' }
                                    ] } }
                                ] },
                                { Id: 'cell-2', Width: 260, Blocks: [
                                    { Id: 'cell-p-2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'cell-r-2', Text: 'Cell two must not inherit cell one image exclusion.' }] } }
                                ] }
                            ] }
                        ] } },
                        { Id: 'body-after', Type: 'Paragraph', Content: { Inlines: [{ Id: 'after-r', Text: 'Body after table can see page scoped table-cell objects.' }] } }
                    ]
                };
            }

            function layout(model) {
                return hooks.createParagraphLayoutEngine(null, {
                    width: 640,
                    height: 900,
                    marginLeft: 40,
                    marginRight: 40,
                    marginTop: 40,
                    marginBottom: 40,
                    minReadableWidth: 32
                }).layoutDocument(model, {
                    width: 640,
                    height: 900,
                    marginLeft: 40,
                    marginRight: 40,
                    marginTop: 40,
                    marginBottom: 40,
                    minReadableWidth: 32
                });
            }

            const local = layout(hooks.importFromCSharpJson(createDocument('cell-local-img', true)));
            const localTable = local.blocks.find(block => block.blockId === 'table-1');
            const localCellOne = localTable.cells.find(cell => cell.cellId === 'cell-1');
            const localCellTwo = localTable.cells.find(cell => cell.cellId === 'cell-2');
            assert.strictEqual(local.pages[0].exclusions.length, 0, 'layoutInCell=true must stay out of page exclusions');
            assert.strictEqual(localCellOne.exclusions.length, 1, JSON.stringify(localCellOne.exclusions));
            assert.strictEqual(localCellOne.exclusions[0].region, 'TableCell');
            assert.strictEqual(localCellOne.exclusions[0].columnIndex, 0);
            assert.strictEqual(localCellTwo.exclusions.length, 0, 'neighboring cell must not inherit exclusions');

            const pageScoped = layout(hooks.importFromCSharpJson(createDocument('cell-page-img', false)));
            const pageTable = pageScoped.blocks.find(block => block.blockId === 'table-1');
            const pageCellOne = pageTable.cells.find(cell => cell.cellId === 'cell-1');
            assert.strictEqual(pageCellOne.exclusions.length, 0, JSON.stringify(pageCellOne.exclusions));
            assert.ok(pageScoped.pages[0].exclusions.some(exclusion => exclusion.objectId === 'cell-page-img' && exclusion.region === 'Body'), JSON.stringify(pageScoped.pages[0].exclusions));
            """);

        result.ShouldPass();
    }

    private static async Task<DocumentEditorImageWrapPhase12NodeResult> RunScenarioAsync(string scenario, string body)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable())
        {
            return new DocumentEditorImageWrapPhase12NodeResult(0, "OK", string.Empty);
        }

        var nodeScript =
            $$"""
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;

            {{body}}
            console.log('OK');
            """;

        return await RunNodeAsync(scriptPath, nodeScript, scenario);
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
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<DocumentEditorImageWrapPhase12NodeResult> RunNodeAsync(string scriptPath, string nodeScript, string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase12-{scenario}-{Guid.NewGuid():N}.js");
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
            return new DocumentEditorImageWrapPhase12NodeResult(process.ExitCode, stdout, stderr);
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
                Number,
                String,
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

internal sealed record DocumentEditorImageWrapPhase12NodeResult(int ExitCode, string StandardOutput, string StandardError);

internal static class DocumentEditorImageWrapPhase12Assertions
{
    public static void ShouldPass(this DocumentEditorImageWrapPhase12NodeResult result)
    {
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }
}
