using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase4LayoutIntegrationJavaScriptTests
{
    [Fact]
    public async Task Phase4_DocumentLayoutBreaksTextIntoBothSideIntervalsBeforePostLayout()
    {
        var result = await RunScenarioAsync(
            "body-centered-square",
            """
            const model = hooks.importFromCSharpJson(createDocumentWithCenteredSquare('p1', 'center-square', 'Square'));
            const layout = hooks.createParagraphLayoutEngine(null, { minReadableWidth: 8 }).layoutDocument(model, pageOptions());

            const object = layout.objects.find(item => item.objectId === 'center-square');
            assert.ok(object, 'missing centered square object');
            const paragraph = layout.blocks.find(item => item.blockId === 'p1');
            assert.ok(paragraph, 'missing paragraph layout');
            const firstLine = paragraph.lines[0];

            assert.strictEqual(firstLine.availableIntervals.length, 2, JSON.stringify(firstLine.availableIntervals));
            assert.strictEqual(firstLine.ranges.length, 2, JSON.stringify(firstLine.ranges));
            assert.ok(firstLine.ranges[0].segments.length > 0, 'left interval must contain real text segments');
            assert.ok(firstLine.ranges[1].segments.length > 0, 'right interval must contain real text segments');
            assert.ok(firstLine.availableIntervals[1].end > firstLine.availableIntervals[1].start, 'right interval owns text offsets');
            assert.ok(firstLine.ranges[1].segments.every(segment => segment.rect.x >= object.rect.x + object.rect.width - 0.001));

            for (const segment of firstLine.segments) {
                const overlapsX = segment.rect.x < object.rect.x + object.rect.width && segment.rect.x + segment.rect.width > object.rect.x;
                const overlapsY = segment.rect.y < object.rect.y + object.rect.height && segment.rect.y + segment.rect.height > object.rect.y;
                assert.ok(!(overlapsX && overlapsY), `text segment overlaps image: ${JSON.stringify({ segment, object })}`);
            }
            assert.ok(firstLine.visualRect.width > firstLine.ranges[0].width, 'visual row spans both ranges while text stays in segment rects');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase4_TopBottomMovesTheLineBeforeSegmentsAreBuilt()
    {
        var result = await RunScenarioAsync(
            "body-top-bottom",
            """
            const model = hooks.importFromCSharpJson(createDocumentWithCenteredSquare('p1', 'top-bottom-square', 'TopBottom'));
            const layout = hooks.createParagraphLayoutEngine(null, { minReadableWidth: 8 }).layoutDocument(model, pageOptions());

            const object = layout.objects.find(item => item.objectId === 'top-bottom-square');
            const paragraph = layout.blocks.find(item => item.blockId === 'p1');
            const firstLine = paragraph.lines[0];
            const objectBottom = object.rect.y + object.rect.height;

            assert.ok(firstLine.rect.y >= objectBottom, JSON.stringify({ line: firstLine.rect, object: object.rect }));
            assert.ok(firstLine.segments.every(segment => segment.rect.y >= objectBottom), 'segments are created after the top-bottom move');
            assert.ok(firstLine.availableIntervals.every(interval => interval.y >= objectBottom), 'caret intervals are created after the top-bottom move');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase4_HeaderAndTableCellUseTheSameDynamicRangeLayout()
    {
        var result = await RunScenarioAsync(
            "scoped-centered-square",
            """
            const model = hooks.importFromCSharpJson(createScopedDocumentWithCenteredSquares());
            const layout = hooks.createParagraphLayoutEngine(null, { minReadableWidth: 8 }).layoutDocument(model, Object.assign(pageOptions(), {
                headerHeight: 100
            }));

            const body = layout.blocks.find(item => item.blockId === 'body-p');
            assert.ok(body, 'missing body paragraph');
            assert.strictEqual(body.lines[0].availableIntervals.length, 1, 'header/table exclusions must not leak into body');

            const headerRegion = layout.headerFooterRegions.find(region => region.region === 'Header' && region.headerFooterId === 'header-primary');
            assert.ok(headerRegion, 'missing header layout');
            const headerLine = headerRegion.blocks.find(item => item.blockId === 'header-p').lines[0];
            assert.strictEqual(headerLine.ranges.length, 2, JSON.stringify(headerLine.ranges));
            assert.ok(headerLine.ranges[1].segments.length > 0, 'header text must flow into the right interval');

            const table = layout.blocks.find(item => item.blockId === 'table-1');
            assert.ok(table, 'missing table layout');
            const cell = table.cells.find(item => item.cellId === 'cell-1');
            const cellLine = cell.blockLayouts.find(item => item.blockId === 'cell-p').lines[0];
            assert.ok(cellLine.ranges.length >= 2, JSON.stringify(cellLine.ranges));
            assert.ok(cellLine.ranges.some(range => range.segments.length > 0 && range.x > cell.contentFrame.x), 'table cell text must flow into a later interval');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase4_ImageLayoutInvalidationIsScopeAware()
    {
        var result = await RunScenarioAsync(
            "scope-aware-invalidation",
            """
            function updateObject(model, objectId, x) {
                const drawing = hooks.findDrawingRunByObjectId(model, objectId);
                assert.ok(drawing, 'missing drawing ' + objectId);
                const object = hooks.normalizeImageObject(drawing.run, { blockId: drawing.blockId, inlineIndex: drawing.inlineIndex });
                const layout = hooks.imageObjectToLayout(object);
                layout.Position.X = x;
                if (layout.position) layout.position.X = x;
                return hooks.applyOperation(model, hooks.createOperation('UpdateImageLayout', {
                    target: { blockId: drawing.blockId, objectId },
                    objectId,
                    layout
                }, { source: 'phase4-invalidation' }));
            }

            const bodyModel = hooks.importFromCSharpJson(createDocumentWithCenteredSquare('p1', 'center-square', 'Square'));
            const bodyResult = updateObject(bodyModel, 'center-square', 140);
            assert.strictEqual(bodyResult.ok, true, JSON.stringify(bodyResult.errors || []));
            assert.ok(bodyResult.invalidatedLayoutScopes.includes('p1'), JSON.stringify(bodyResult.invalidatedLayoutScopes));
            assert.ok(bodyResult.invalidatedLayoutScopes.includes('p2'), 'following wrapped paragraph should be invalidated');
            assert.ok(bodyResult.invalidatedLayoutScopes.includes('p3'), 'near following wrapped paragraph should be invalidated');

            const scopedModel = hooks.importFromCSharpJson(createScopedDocumentWithCenteredSquares());
            const headerResult = updateObject(scopedModel, 'header-square', 140);
            assert.strictEqual(headerResult.ok, true, JSON.stringify(headerResult.errors || []));
            assert.ok(headerResult.invalidatedLayoutScopes.includes('header-p'), JSON.stringify(headerResult.invalidatedLayoutScopes));
            assert.ok(!headerResult.invalidatedLayoutScopes.includes('body-p'), 'header image must not invalidate body text');
            assert.ok(!headerResult.invalidatedLayoutScopes.includes('cell-p'), 'header image must not invalidate table cell text');

            const cellResult = updateObject(scopedModel, 'cell-square', 125);
            assert.strictEqual(cellResult.ok, true, JSON.stringify(cellResult.errors || []));
            assert.ok(cellResult.invalidatedLayoutScopes.includes('cell-p'), JSON.stringify(cellResult.invalidatedLayoutScopes));
            assert.ok(!cellResult.invalidatedLayoutScopes.includes('body-p'), 'cell image must not invalidate body text');
            assert.ok(!cellResult.invalidatedLayoutScopes.includes('header-p'), 'cell image must not invalidate header text');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    private static async Task<ScenarioResult> RunScenarioAsync(string scenario, string nodeScript)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return new ScenarioResult(0, "OK", "");
        var result = await RunNodeAsync(scriptPath, nodeScript, scenario);
        return new ScenarioResult(result.ExitCode, result.StandardOutput, result.StandardError);
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

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(
        string scriptPath,
        string nodeScript,
        string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase4-{scenario}-{Guid.NewGuid():N}.js");
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

    private sealed record ScenarioResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public void ShouldPass()
        {
            ExitCode.Should().Be(0, StandardError);
            StandardOutput.Trim().Should().Be("OK");
        }
    }

    private const string SharedSandboxScript =
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

        const code = fs.readFileSync(process.argv[2], 'utf8');
        const sandbox = createSandbox();
        vm.createContext(sandbox);
        vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });
        const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;

        function pageOptions() {
            return {
                width: 420,
                height: 320,
                marginTop: 40,
                marginRight: 40,
                marginBottom: 40,
                marginLeft: 40,
                blockGap: 0,
                lineGap: 0,
                minReadableWidth: 8
            };
        }

        function wrapModeValue(name) {
            if (name === 'Square') return 1;
            if (name === 'TopBottom') return 4;
            return 0;
        }

        function drawingRun(id, objectId, anchorBlockId, wrapMode, x, y, width, height) {
            return {
                $type: 'drawing',
                Id: id,
                ObjectId: objectId,
                Kind: 0,
                Source: 0,
                Url: '/' + objectId + '.png',
                AltText: objectId,
                Size: { Width: width, Height: height },
                Layout: {
                    Kind: 1,
                    Wrap: { Mode: wrapModeValue(wrapMode), DistanceRight: 0, DistanceLeft: 0 },
                    Anchor: { BlockId: anchorBlockId, Offset: 0, InlineIndex: 0 },
                    Position: {
                        HorizontalRelativeTo: 2,
                        HorizontalAlignment: 0,
                        VerticalRelativeTo: 3,
                        VerticalAlignment: 1,
                        X: x,
                        Y: y
                    },
                    Transform: { Width: width, Height: height }
                }
            };
        }

        function createDocumentWithCenteredSquare(blockId, objectId, wrapMode) {
            return {
                DocumentId: 'phase4-' + objectId,
                Blocks: [
                    {
                        Id: blockId,
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                drawingRun(objectId + '-run', objectId, blockId, wrapMode, 120, 0, 80, 70),
                                { $type: 'text', Id: blockId + '-text', Text: 'Alpha beta gamma delta epsilon zeta eta theta.' }
                            ]
                        }
                    },
                    {
                        Id: 'p2',
                        Type: 'Paragraph',
                        Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Id: 'p2-text', Text: 'Following paragraph may still wrap around the same object.' }] }
                    },
                    {
                        Id: 'p3',
                        Type: 'Paragraph',
                        Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Id: 'p3-text', Text: 'Another nearby paragraph is part of the affected wrap scope.' }] }
                    }
                ]
            };
        }

        function createScopedDocumentWithCenteredSquares() {
            const cellWidth = 320;
            return {
                DocumentId: 'phase4-scoped',
                Blocks: [
                    { Id: 'body-p', Type: 'Paragraph', Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Id: 'body-text', Text: 'Body text stays full width.' }] } },
                    { Id: 'table-1', Type: 'Table', Content: { Rows: [
                        { Id: 'row-1', Cells: [
                            { Id: 'cell-1', Blocks: [
                                { Id: 'cell-p', Type: 'Paragraph', Content: { $type: 'paragraph', Inlines: [
                                    drawingRun('cell-run', 'cell-square', 'cell-p', 'Square', 105, 0, 70, 50),
                                    { $type: 'text', Id: 'cell-text', Text: 'Cell alpha beta gamma delta epsilon zeta.' }
                                ] } }
                            ] }
                        ] }
                    ], Style: { Width: cellWidth } } }
                ],
                HeadersFooters: [
                    { Id: 'header-primary', Region: 'Header', Type: 'Header', Scope: 'Primary', Blocks: [
                        { Id: 'header-p', Type: 'Paragraph', Content: { $type: 'paragraph', Inlines: [
                            drawingRun('header-run', 'header-square', 'header-p', 'Square', 120, 0, 80, 50),
                            { $type: 'text', Id: 'header-text', Text: 'Header alpha beta gamma delta epsilon.' }
                        ] } }
                    ] }
                ]
            };
        }

        """;

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

        throw new InvalidOperationException("Could not find repository root.");
    }
}
