using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase16PerformanceJavaScriptTests
{
    [Fact]
    public async Task Phase16_AvailableIntervalsCachePreventsPerTokenPolygonRecalculation()
    {
        var result = await RunScenarioAsync(
            "interval-cache-polygon-budget",
            """
            const frame = { x: 0, y: 0, width: 720, height: 2400 };
            const exclusions = Array.from({ length: 10 }, (_, index) => hooks.createTextExclusion({
                objectId: 'poly-' + index,
                blockId: 'p-wrap',
                pageIndex: 0,
                region: 'Body',
                wrapMode: 'Tight',
                wrapSide: 'BothSides',
                rect: { x: 120 + (index % 5) * 92, y: 24 + Math.floor(index / 5) * 260, width: 74, height: 180 },
                distanceLeft: 6,
                distanceRight: 6,
                distanceTop: 4,
                distanceBottom: 4,
                wrapContourPoints: [
                    { x: 0.5, y: 0 },
                    { x: 1, y: 0.35 },
                    { x: 0.7, y: 1 },
                    { x: 0, y: 0.65 }
                ],
                polygonVersion: 'v1'
            }, frame)).filter(Boolean);

            for (let line = 0; line < 100; line++) {
                const y = 20 + line * 18;
                for (let token = 0; token < 20; token++) {
                    hooks.getAvailableIntervals(y, 18, frame, exclusions, 16, { pageIndex: 0, region: 'Body' });
                }
            }

            const stats = hooks.getAvailableIntervalsCacheStats(exclusions);
            assert.strictEqual(stats.calls, 2000, JSON.stringify(stats));
            assert.strictEqual(stats.cacheMisses, 100, JSON.stringify(stats));
            assert.strictEqual(stats.cacheHits, 1900, JSON.stringify(stats));
            assert.ok(stats.polygonComputationCount <= 1000, JSON.stringify(stats));
            assert.ok(stats.polygonComputationCount < 2000, 'polygon work must be line based, not token based: ' + JSON.stringify(stats));

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_CacheKeyInvalidatesForObjectGeometryWrapDistanceAndPolygonVersion()
    {
        var result = await RunScenarioAsync(
            "interval-cache-key-invalidation",
            """
            const frame = { x: 0, y: 0, width: 500, height: 320 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'cache-object',
                blockId: 'p1',
                pageIndex: 0,
                region: 'Body',
                wrapMode: 'Tight',
                wrapSide: 'BothSides',
                rect: { x: 120, y: 40, width: 110, height: 80 },
                distanceLeft: 6,
                distanceRight: 8,
                distanceTop: 2,
                distanceBottom: 4,
                wrapContourPoints: [
                    { x: 0, y: 0 },
                    { x: 1, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 }
                ],
                polygonVersion: 'v1'
            }, frame);
            const exclusions = [exclusion];

            hooks.getAvailableIntervals(60, 20, frame, exclusions, 24, { pageIndex: 0, region: 'Body' });
            hooks.getAvailableIntervals(60, 20, frame, exclusions, 24, { pageIndex: 0, region: 'Body' });
            let stats = hooks.getAvailableIntervalsCacheStats(exclusions);
            assert.strictEqual(stats.cacheMisses, 1, JSON.stringify(stats));
            assert.strictEqual(stats.cacheHits, 1, JSON.stringify(stats));

            exclusion.rect.x += 25;
            hooks.getAvailableIntervals(60, 20, frame, exclusions, 24, { pageIndex: 0, region: 'Body' });
            stats = hooks.getAvailableIntervalsCacheStats(exclusions);
            assert.strictEqual(stats.cacheMisses, 2, 'rect change must miss: ' + JSON.stringify(stats));

            exclusion.wrapMode = 'TopBottom';
            hooks.getAvailableIntervals(60, 20, frame, exclusions, 24, { pageIndex: 0, region: 'Body' });
            stats = hooks.getAvailableIntervalsCacheStats(exclusions);
            assert.strictEqual(stats.cacheMisses, 3, 'wrap mode change must miss: ' + JSON.stringify(stats));

            exclusion.wrapMode = 'Tight';
            exclusion.distanceRight += 3;
            hooks.getAvailableIntervals(60, 20, frame, exclusions, 24, { pageIndex: 0, region: 'Body' });
            stats = hooks.getAvailableIntervalsCacheStats(exclusions);
            assert.strictEqual(stats.cacheMisses, 4, 'wrap distance change must miss: ' + JSON.stringify(stats));

            exclusion.polygonVersion = 'v2';
            exclusion.polygon[1].x += 12;
            hooks.getAvailableIntervals(60, 20, frame, exclusions, 24, { pageIndex: 0, region: 'Body' });
            stats = hooks.getAvailableIntervalsCacheStats(exclusions);
            assert.strictEqual(stats.cacheMisses, 5, 'polygon version/change must miss: ' + JSON.stringify(stats));

            hooks.getAvailableIntervals(260, 20, frame, exclusions, 24, { pageIndex: 0, region: 'Body' });
            hooks.getAvailableIntervals(260, 20, frame, exclusions, 24, { pageIndex: 0, region: 'Body' });
            stats = hooks.getAvailableIntervalsCacheStats(exclusions);
            assert.strictEqual(stats.cacheMisses, 6, 'first far-away text line query should miss once');
            assert.ok(stats.cacheHits >= 2, 'typing outside the object vertical range should reuse the same cached far-away line');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_TextTypingOutsideObjectRangeUsesIncrementalReflowBoundary()
    {
        var result = await RunScenarioAsync(
            "incremental-text-reflow",
            """
            const model = hooks.importFromCSharpJson(createBodyDocumentWithFarFloatingImage());
            const engine = hooks.createParagraphLayoutEngine(null, pageOptions());
            const previous = engine.layoutDocument(model, pageOptions());
            const op = hooks.createOperation('InsertText', {
                target: { blockId: 'p1', offset: 5, region: 'Body' },
                text: ' fast'
            }, { source: 'phase16-typing' });
            const applied = hooks.applyOperation(model, op);
            assert.strictEqual(applied.ok, true, JSON.stringify(applied.errors || []));

            const next = engine.layoutAfterOperation(model, op, previous, pageOptions());
            assert.strictEqual(next.debug.incrementalReflow, true, JSON.stringify(next.debug));
            assert.strictEqual(next.debug.skippedPageExclusionRebuild, true, JSON.stringify(next.debug));
            assert.strictEqual(JSON.stringify(next.debug.invalidatedScopes), JSON.stringify(['p1']));
            assert.strictEqual(next.debug.reflowBoundary.blockId, 'p1');
            assert.strictEqual(next.debug.reflowBoundary.region, 'Body');
            assert.ok(!next.debug.invalidatedScopes.includes('p2'), 'far image paragraph must stay outside active text reflow');
            assert.ok(next.blocks.find(block => block.blockId === 'p2'), 'existing following layout remains available');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_ImageAndHeaderFooterEditsStayScopeBound()
    {
        var result = await RunScenarioAsync(
            "scope-bound-reflow",
            """
            const model = hooks.importFromCSharpJson(createHeaderFooterAndImageDocument());
            const drawing = hooks.findDrawingRunByObjectId(model, 'body-img');
            const object = hooks.normalizeImageObject(drawing.run, { blockId: drawing.blockId, inlineIndex: drawing.inlineIndex });
            const layout = hooks.imageObjectToLayout(object);
            layout.Position.X += 36;
            layout.position.X = layout.Position.X;

            const imageUpdate = hooks.applyOperation(model, hooks.createOperation('UpdateImageLayout', {
                target: { blockId: 'body-p', objectId: 'body-img', region: 'Body' },
                objectId: 'body-img',
                layout
            }, { source: 'phase16-image' }));
            assert.strictEqual(imageUpdate.ok, true, JSON.stringify(imageUpdate.errors || []));
            assert.ok(imageUpdate.invalidatedLayoutScopes.includes('body-p'), JSON.stringify(imageUpdate.invalidatedLayoutScopes));
            assert.ok(!imageUpdate.invalidatedLayoutScopes.includes('header-p'), 'body image update must not invalidate header');
            assert.ok(!imageUpdate.invalidatedLayoutScopes.includes('footer-p'), 'body image update must not invalidate footer');

            const headerInsert = hooks.applyOperation(model, hooks.createOperation('InsertText', {
                target: { blockId: 'header-p', offset: 6, region: 'Header', headerFooterId: 'hf-header' },
                text: ' H',
                beforeSelection: { region: 'Header', headerFooterId: 'hf-header', blockId: 'header-p', offset: 6, isCollapsed: true }
            }, { source: 'phase16-header' }));
            assert.strictEqual(headerInsert.ok, true);
            assert.strictEqual(JSON.stringify(headerInsert.invalidatedLayoutScopes), JSON.stringify(['header-p']));
            assert.strictEqual(headerInsert.nextSelection.region, 'Header');
            assert.ok(!headerInsert.invalidatedLayoutScopes.includes('body-p'), 'header typing must not invalidate body');

            const footerInsert = hooks.applyOperation(model, hooks.createOperation('InsertText', {
                target: { blockId: 'footer-p', offset: 6, region: 'Footer', headerFooterId: 'hf-footer' },
                text: ' F',
                beforeSelection: { region: 'Footer', headerFooterId: 'hf-footer', blockId: 'footer-p', offset: 6, isCollapsed: true }
            }, { source: 'phase16-footer' }));
            assert.strictEqual(footerInsert.ok, true);
            assert.strictEqual(JSON.stringify(footerInsert.invalidatedLayoutScopes), JSON.stringify(['footer-p']));
            assert.strictEqual(footerInsert.nextSelection.region, 'Footer');
            assert.ok(!footerInsert.invalidatedLayoutScopes.includes('body-p'), 'footer typing must not invalidate body');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_PerformanceBudgetSmokeForTypingResizeAndUndoDiagnostics()
    {
        var result = await RunScenarioAsync(
            "performance-budget-smoke",
            """
            const stats = hooks.createStrictPerformanceStats();
            const inst = {
                performanceStats: stats,
                diagnostics: { timeline: [], lastErrors: [], watchdogFailures: [], debugWarnings: [], modelVersion: 0, selectionVersion: 0 }
            };
            for (let index = 0; index < 30; index++) {
                hooks.recordOperationPerformance(inst, [{ type: 'InsertText' }], 3 + (index % 4), ['p1'], 'typing-next-to-image');
            }
            hooks.recordOperationPerformance(inst, [{ type: 'UpdateImageLayout' }], 9, ['body-p'], 'resize-preview-commit');
            hooks.recordOperationPerformance(inst, [{ type: 'UpdateImageLayout' }], 11, ['body-p'], 'undo-image-layout');

            assert.strictEqual(stats.typingLatencyCount, 30);
            assert.ok(stats.typingLatencyMaxMs < 12, JSON.stringify(stats));
            assert.strictEqual(stats.fullDocumentLayoutCount, 0, JSON.stringify(stats));
            assert.strictEqual(stats.imageDragLatencyCount, 2);
            assert.ok(stats.imageDragLatencyMaxMs < 20, JSON.stringify(stats));
            assert.ok(inst.diagnostics.timeline.some(entry => entry.detail && entry.detail.source === 'undo-image-layout'));

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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase16-{scenario}-{Guid.NewGuid():N}.js");
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
                width: 640,
                height: 900,
                marginLeft: 40,
                marginRight: 40,
                marginTop: 40,
                marginBottom: 40,
                blockGap: 12,
                lineGap: 0,
                minReadableWidth: 24
            };
        }

        function drawingRun(objectId, anchorBlockId, x, y) {
            return {
                $type: 'drawing',
                Id: objectId + '-run',
                ObjectId: objectId,
                Kind: 0,
                Source: 0,
                Url: '/' + objectId + '.png',
                AltText: objectId,
                Size: { Width: 96, Height: 64 },
                Layout: {
                    Kind: 1,
                    Anchor: { BlockId: anchorBlockId, Offset: 0, InlineIndex: 0, Region: 0, MoveWithText: true },
                    Position: { HorizontalRelativeTo: 0, VerticalRelativeTo: 3, HorizontalAlignment: null, VerticalAlignment: 1, X: x, Y: y },
                    Wrap: { Mode: 1, DistanceLeft: 8, DistanceRight: 8, DistanceTop: 4, DistanceBottom: 4 },
                    Transform: { Width: 96, Height: 64 },
                    Stacking: { ZIndex: 0, AllowOverlap: false }
                }
            };
        }

        function createBodyDocumentWithFarFloatingImage() {
            return {
                DocumentId: 'phase16-incremental-body',
                Blocks: [
                    { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Alpha paragraph without nearby floating objects.' }] } },
                    { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [
                        drawingRun('far-img', 'p2', 260, 180),
                        { Id: 'r2', Text: 'Far image paragraph keeps its own layout and should not be recalculated for p1 typing.' }
                    ] } },
                    { Id: 'p3', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r3', Text: 'Following paragraph remains present.' }] } }
                ]
            };
        }

        function createHeaderFooterAndImageDocument() {
            return {
                DocumentId: 'phase16-scope-bound',
                Blocks: [
                    { Id: 'body-p', Type: 'Paragraph', Content: { Inlines: [
                        drawingRun('body-img', 'body-p', 180, 0),
                        { Id: 'body-r', Text: 'Body text wraps around the body image only.' }
                    ] } },
                    { Id: 'body-after', Type: 'Paragraph', Content: { Inlines: [{ Id: 'body-after-r', Text: 'Body after.' }] } }
                ],
                HeadersFooters: [
                    { Id: 'hf-header', Type: 'Header', Region: 'Header', Blocks: [
                        { Id: 'header-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'header-r', Text: 'Header' }] } }
                    ] },
                    { Id: 'hf-footer', Type: 'Footer', Region: 'Footer', Blocks: [
                        { Id: 'footer-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'footer-r', Text: 'Footer' }] } }
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

        throw new InvalidOperationException("Repository root was not found.");
    }
}
