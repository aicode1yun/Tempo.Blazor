using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase18RegionScopeJavaScriptTests
{
    [Fact]
    public async Task Phase18_InsertImageIntoHeaderFooterAndTableCellStoresRegionAnchor()
    {
        var result = await RunScenarioAsync(
            "insert-region",
            """
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase18-insert-region',
                Blocks: [
                    { Id: 'body-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'body-r', Text: 'Body text' }] } },
                    { Id: 'table-1', Type: 'Table', Content: { Rows: [
                        { Id: 'row-1', Cells: [
                            { Id: 'cell-1', Blocks: [
                                { Id: 'cell-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'cell-r', Text: 'Cell text' }] } }
                            ] }
                        ] }
                    ] } }
                ],
                HeadersFooters: [
                    { Id: 'header-primary', Region: 'Header', Type: 'Header', Scope: 'Primary', Blocks: [
                        { Id: 'header-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'header-r', Text: 'Header' }] } }
                    ] },
                    { Id: 'footer-primary', Region: 'Footer', Type: 'Footer', Scope: 'Primary', Blocks: [
                        { Id: 'footer-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'footer-r', Text: 'Footer' }] } }
                    ] }
                ]
            });

            const headerResult = hooks.applyOperation(model, hooks.createOperation('InsertImage', {
                target: { blockId: 'header-p', offset: 6, region: 'Header', headerFooterId: 'header-primary' },
                objectId: 'header-img',
                image: { Url: '/header.png', AltText: 'Header image', Layout: { Kind: 0, Wrap: { Mode: 0 }, Transform: { Width: 48, Height: 24 } } },
                beforeSelection: { region: 'Header', headerFooterId: 'header-primary', blockId: 'header-p', offset: 6, isCollapsed: true }
            }, { source: 'phase18-header' }));
            assert.strictEqual(headerResult.ok, true, JSON.stringify(headerResult.errors || []));
            const header = hooks.findDrawingRunByObjectId(model, 'header-img');
            assert.strictEqual(header.object.anchorRegion, 'Header');
            assert.strictEqual(header.object.anchorHeaderFooterId, 'header-primary');
            assert.strictEqual(headerResult.nextSelection.region, 'Header');
            assert.strictEqual(headerResult.nextSelection.headerFooterId, 'header-primary');

            const footerResult = hooks.applyOperation(model, hooks.createOperation('InsertImage', {
                target: { blockId: 'footer-p', offset: 6, region: 'Footer', headerFooterId: 'footer-primary' },
                objectId: 'footer-img',
                image: { Url: '/footer.png', AltText: 'Footer image', Layout: { Kind: 0, Wrap: { Mode: 0 }, Transform: { Width: 48, Height: 24 } } },
                beforeSelection: { region: 'Footer', headerFooterId: 'footer-primary', blockId: 'footer-p', offset: 6, isCollapsed: true }
            }, { source: 'phase18-footer' }));
            assert.strictEqual(footerResult.ok, true, JSON.stringify(footerResult.errors || []));
            const footer = hooks.findDrawingRunByObjectId(model, 'footer-img');
            assert.strictEqual(footer.object.anchorRegion, 'Footer');
            assert.strictEqual(footer.object.anchorHeaderFooterId, 'footer-primary');
            assert.strictEqual(footerResult.nextSelection.region, 'Footer');
            assert.strictEqual(footerResult.nextSelection.headerFooterId, 'footer-primary');

            const cellResult = hooks.applyOperation(model, hooks.createOperation('InsertImage', {
                target: { blockId: 'cell-p', offset: 4, region: 'TableCell', tableId: 'table-1', cellId: 'cell-1' },
                objectId: 'cell-img',
                image: { Url: '/cell.png', AltText: 'Cell image', Layout: { Kind: 0, Wrap: { Mode: 0 }, Transform: { Width: 48, Height: 24 } } },
                beforeSelection: { region: 'TableCell', tableId: 'table-1', cellId: 'cell-1', activeTableId: 'table-1', activeTableCellId: 'cell-1', blockId: 'cell-p', offset: 4, isCollapsed: true }
            }, { source: 'phase18-cell' }));
            assert.strictEqual(cellResult.ok, true, JSON.stringify(cellResult.errors || []));
            const cell = hooks.findDrawingRunByObjectId(model, 'cell-img');
            assert.strictEqual(cell.object.anchorRegion, 'TableCell');
            assert.strictEqual(cell.object.anchorTableId, 'table-1');
            assert.strictEqual(cell.object.anchorCellId, 'cell-1');
            assert.strictEqual(cellResult.nextSelection.region, 'TableCell');
            assert.strictEqual(cellResult.nextSelection.activeTableId, 'table-1');
            assert.strictEqual(cellResult.nextSelection.activeTableCellId, 'cell-1');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase18_ObjectSelectionByIdInfersHeaderFooterAndTableCellRegion()
    {
        var result = await RunScenarioAsync(
            "selection-region",
            """
            const model = hooks.importFromCSharpJson(createRegionLayoutDocument());

            const headerSelection = hooks.createObjectSelectionSnapshot(model, { objectId: 'header-square' });
            assert.strictEqual(headerSelection.region, 'Header');
            assert.strictEqual(headerSelection.headerFooterId, 'header-primary');
            assert.strictEqual(headerSelection.objectSelection.region, 'Header');
            assert.strictEqual(headerSelection.objectSelection.headerFooterId, 'header-primary');

            const footerSelection = hooks.createObjectSelectionSnapshot(model, { objectId: 'footer-square' });
            assert.strictEqual(footerSelection.region, 'Footer');
            assert.strictEqual(footerSelection.headerFooterId, 'footer-primary');

            const cellSelection = hooks.createObjectSelectionSnapshot(model, { objectId: 'cell-square' });
            assert.strictEqual(cellSelection.region, 'TableCell');
            assert.strictEqual(cellSelection.activeTableId, 'table-1');
            assert.strictEqual(cellSelection.activeTableCellId, 'cell-1');
            assert.strictEqual(cellSelection.objectSelection.tableId, 'table-1');
            assert.strictEqual(cellSelection.objectSelection.cellId, 'cell-1');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase18_HeaderAndFooterImageExclusionsDoNotAffectBodyText()
    {
        var result = await RunScenarioAsync(
            "header-footer-layout",
            """
            const model = hooks.importFromCSharpJson(createRegionLayoutDocument());
            const layout = hooks.createParagraphLayoutEngine(null, {
                width: 640,
                height: 900,
                marginLeft: 40,
                marginRight: 40,
                marginTop: 40,
                marginBottom: 40,
                headerHeight: 80,
                footerHeight: 80,
                minReadableWidth: 32
            }).layoutDocument(model, {
                width: 640,
                height: 900,
                marginLeft: 40,
                marginRight: 40,
                marginTop: 40,
                marginBottom: 40,
                headerHeight: 80,
                footerHeight: 80,
                minReadableWidth: 32
            });

            const body = layout.blocks.find(block => block.blockId === 'body-p');
            assert.ok(body, 'missing body layout');
            const bodyInterval = body.lines[0].availableIntervals[0];
            assert.strictEqual(Math.round(bodyInterval.width), 560, JSON.stringify(bodyInterval));
            assert.strictEqual((layout.pages[0].exclusions || []).length, 0, 'header/footer exclusions must not be stored on the body page scope');

            const headerRegion = layout.headerFooterRegions.find(region => region.region === 'Header' && region.headerFooterId === 'header-primary');
            assert.ok(headerRegion, 'missing header region layout');
            assert.strictEqual(headerRegion.exclusions.length, 1, JSON.stringify(headerRegion));
            assert.strictEqual(headerRegion.exclusions[0].region, 'Header');
            assert.strictEqual(headerRegion.exclusions[0].headerFooterId, 'header-primary');
            const headerLine = headerRegion.blocks.find(block => block.blockId === 'header-p').lines[0];
            assert.ok(headerLine.availableIntervals.some(interval => interval.width < headerRegion.frame.width), JSON.stringify(headerLine.availableIntervals));

            const footerRegion = layout.headerFooterRegions.find(region => region.region === 'Footer' && region.headerFooterId === 'footer-primary');
            assert.ok(footerRegion, 'missing footer region layout');
            assert.strictEqual(footerRegion.exclusions.length, 1, JSON.stringify(footerRegion));
            assert.strictEqual(footerRegion.exclusions[0].region, 'Footer');
            assert.strictEqual(footerRegion.exclusions[0].headerFooterId, 'footer-primary');
            const footerLine = footerRegion.blocks.find(block => block.blockId === 'footer-p').lines[0];
            assert.ok(footerLine.availableIntervals.some(interval => interval.width < footerRegion.frame.width), JSON.stringify(footerLine.availableIntervals));
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase18_TableCellImageExclusionIsLocalToCell()
    {
        var result = await RunScenarioAsync(
            "table-cell-layout",
            """
            const model = hooks.importFromCSharpJson(createRegionLayoutDocument());
            const layout = hooks.createParagraphLayoutEngine(null, {
                width: 640,
                height: 900,
                marginLeft: 40,
                marginRight: 40,
                marginTop: 40,
                marginBottom: 40,
                headerHeight: 80,
                footerHeight: 80,
                minReadableWidth: 32
            }).layoutDocument(model, {
                width: 640,
                height: 900,
                marginLeft: 40,
                marginRight: 40,
                marginTop: 40,
                marginBottom: 40,
                headerHeight: 80,
                footerHeight: 80,
                minReadableWidth: 32
            });

            const body = layout.blocks.find(block => block.blockId === 'body-p');
            assert.ok(body, 'missing body layout');
            assert.strictEqual(Math.round(body.lines[0].availableIntervals[0].width), 560, JSON.stringify(body.lines[0].availableIntervals));

            const table = layout.blocks.find(block => block.blockId === 'table-1');
            assert.ok(table, 'missing table layout');
            assert.strictEqual(table.exclusions.length, 1, JSON.stringify(table.exclusions));
            assert.strictEqual(table.exclusions[0].region, 'TableCell');
            assert.strictEqual(table.exclusions[0].tableId, 'table-1');
            assert.strictEqual(table.exclusions[0].cellId, 'cell-1');

            const cell = table.cells.find(item => item.cellId === 'cell-1');
            assert.ok(cell, 'missing cell layout');
            assert.strictEqual(cell.exclusions.length, 1, JSON.stringify(cell));
            const cellParagraph = cell.blockLayouts.find(block => block.blockId === 'cell-p');
            assert.ok(cellParagraph, 'missing cell paragraph layout');
            assert.ok(cellParagraph.lines[0].availableIntervals.some(interval => interval.width < cell.contentFrame.width), JSON.stringify(cellParagraph.lines[0].availableIntervals));
            assert.strictEqual(cellParagraph.lines[0].region, 'TableCell');
            assert.strictEqual(cellParagraph.lines[0].tableId, 'table-1');
            assert.strictEqual(cellParagraph.lines[0].cellId, 'cell-1');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase18_CrossRegionDragDropIsRejectedUnlessExplicitlyAllowed()
    {
        var result = await RunScenarioAsync(
            "cross-region-drop",
            """
            const document = {
                DocumentId: 'phase18-cross-region-drop',
                Blocks: [
                    { Id: 'body-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'body-r', Text: 'Body target paragraph' }] } }
                ],
                HeadersFooters: [
                    { Id: 'header-primary', Region: 'Header', Type: 'Header', Scope: 'Primary', Blocks: [
                        { Id: 'header-p', Type: 'Paragraph', Content: { Inlines: [
                            createDrawingRun('header-run', 'header-drag', 'header-p', 1, 'header-primary'),
                            { Id: 'header-text', Text: 'Header source' }
                        ] } }
                    ] }
                ]
            };
            const bodyLine = {
                blockId: 'body-p',
                pageIndex: 0,
                region: 'Body',
                rect: { x: 20, y: 40, width: 220, height: 20 },
                referenceRect: { x: 20, y: 40, width: 220, height: 20 },
                start: 0,
                end: 21
            };

            const rejected = hooks.createImageMoveTrackHarness({
                document,
                objectId: 'header-drag',
                blockId: 'header-p',
                lineBoxes: [bodyLine]
            });
            const beforeRejected = rejected.begin(0, 0).modelJson;
            rejected.move(40, 48);
            const rejectedState = rejected.up(40, 48);
            const rejectedObject = hooks.normalizeImageObject(hooks.findDrawingRunByObjectId(rejected.model, 'header-drag').run, { blockId: 'header-p' });

            assert.strictEqual(rejectedState.commitCount, 1, JSON.stringify(rejectedState.commits));
            assert.strictEqual(rejectedState.commits[0].type, 'DropRejected');
            assert.strictEqual(rejectedState.commits[0].reason, 'cross-region-drop');
            assert.strictEqual(rejectedState.modelJson, beforeRejected, 'rejected drop must restore the original model');
            assert.strictEqual(rejectedObject.anchorRegion, 'Header');
            assert.strictEqual(rejectedObject.anchorHeaderFooterId, 'header-primary');

            const allowed = hooks.createImageMoveTrackHarness({
                document,
                objectId: 'header-drag',
                blockId: 'header-p',
                lineBoxes: [bodyLine],
                allowCrossRegionDrop: true
            });
            allowed.begin(0, 0);
            allowed.move(40, 48);
            const allowedState = allowed.up(40, 48);
            const allowedObject = hooks.normalizeImageObject(hooks.findDrawingRunByObjectId(allowed.model, 'header-drag').run, { blockId: 'body-p' });

            assert.strictEqual(allowedState.commitCount, 1, JSON.stringify(allowedState.commits));
            assert.strictEqual(allowedState.commits[0].type, 'UpdateImageLayout');
            assert.strictEqual(allowedObject.anchorBlockId, 'body-p');
            assert.strictEqual(allowedObject.anchorRegion, 'Body');
            """);

        result.ShouldPass();
    }

    private static async Task<DocumentEditorImageDrawingPhase18NodeResult> RunScenarioAsync(string scenario, string body)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable())
        {
            return new DocumentEditorImageDrawingPhase18NodeResult(0, "OK", string.Empty);
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

            function createDrawingRun(id, objectId, anchorBlockId, region, scopeId, tableId, cellId) {
                return {
                    $type: 'drawing',
                    Id: id,
                    ObjectId: objectId,
                    Kind: 0,
                    Source: 0,
                    Url: '/' + objectId + '.png',
                    AltText: objectId,
                    Layout: {
                        Kind: 1,
                        Anchor: {
                            BlockId: anchorBlockId,
                            Offset: 0,
                            InlineIndex: 0,
                            Region: region,
                            HeaderFooterId: scopeId || null,
                            TableId: tableId || null,
                            CellId: cellId || null,
                            MoveWithText: true,
                            FixedOnPage: false,
                            LockAnchor: false
                        },
                        Position: {
                            HorizontalRelativeTo: 2,
                            VerticalRelativeTo: 3,
                            HorizontalAlignment: 0,
                            VerticalAlignment: 1,
                            X: 0,
                            Y: 0
                        },
                        Wrap: {
                            Mode: 1,
                            DistanceLeft: 0,
                            DistanceRight: 8,
                            DistanceTop: 0,
                            DistanceBottom: 0
                        },
                        Transform: { Width: 120, Height: 36 },
                        Stacking: { ZIndex: 0, AllowOverlap: false }
                    }
                };
            }

            function createRegionLayoutDocument() {
                return {
                    DocumentId: 'phase18-layout-region',
                    Blocks: [
                        { Id: 'body-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'body-r', Text: 'Body paragraph must keep the full body width even when header footer and table cell images wrap text.' }] } },
                        { Id: 'table-1', Type: 'Table', Content: { Style: { Width: 360 }, Rows: [
                            { Id: 'row-1', Cells: [
                                { Id: 'cell-1', Width: 360, Blocks: [
                                    { Id: 'cell-p', Type: 'Paragraph', Content: { Inlines: [
                                        createDrawingRun('cell-run', 'cell-square', 'cell-p', 6, null, 'table-1', 'cell-1'),
                                        { Id: 'cell-text', Text: 'Cell text wraps locally beside its square image and should not affect body text.' }
                                    ] } }
                                ] }
                            ] }
                        ] } }
                    ],
                    HeadersFooters: [
                        { Id: 'header-primary', Region: 'Header', Type: 'Header', Scope: 'Primary', Blocks: [
                            { Id: 'header-p', Type: 'Paragraph', Content: { Inlines: [
                                createDrawingRun('header-run', 'header-square', 'header-p', 1, 'header-primary'),
                                { Id: 'header-text', Text: 'Header text wraps beside an image.' }
                            ] } }
                        ] },
                        { Id: 'footer-primary', Region: 'Footer', Type: 'Footer', Scope: 'Primary', Blocks: [
                            { Id: 'footer-p', Type: 'Paragraph', Content: { Inlines: [
                                createDrawingRun('footer-run', 'footer-square', 'footer-p', 2, 'footer-primary'),
                                { Id: 'footer-text', Text: 'Footer text wraps beside an image.' }
                            ] } }
                        ] }
                    ]
                };
            }

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

    private static async Task<DocumentEditorImageDrawingPhase18NodeResult> RunNodeAsync(string scriptPath, string nodeScript, string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase18-{scenario}-{Guid.NewGuid():N}.js");
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
            return new DocumentEditorImageDrawingPhase18NodeResult(process.ExitCode, stdout, stderr);
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

internal sealed record DocumentEditorImageDrawingPhase18NodeResult(int ExitCode, string StandardOutput, string StandardError);

internal static class DocumentEditorImageDrawingPhase18Assertions
{
    public static void ShouldPass(this DocumentEditorImageDrawingPhase18NodeResult result)
    {
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }
}
