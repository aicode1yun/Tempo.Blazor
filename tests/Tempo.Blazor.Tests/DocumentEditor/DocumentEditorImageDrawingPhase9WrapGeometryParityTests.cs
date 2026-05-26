using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase9WrapGeometryParityTests
{
    [Fact]
    public async Task Phase9_CSharpAndJavaScriptReturnSameExclusionRectForSquare()
    {
        if (!IsNodeAvailable()) return;

        var scenario = Scenario("Square", 4, 8, 6, 10);
        var expected = CreateCSharpZone(scenario);
        var js = await RunGeometryScenarioAsync(scenario);

        expected.Should().NotBeNull();
        AssertJsRect(js.RootElement.GetProperty("zone").GetProperty("rect"), expected!.Rect);
        js.RootElement.GetProperty("zone").GetProperty("kind").GetString().Should().Be("rectangular");
    }

    [Fact]
    public async Task Phase9_CSharpAndJavaScriptReturnSameFullWidthExclusionForTopBottom()
    {
        if (!IsNodeAvailable()) return;

        var scenario = Scenario("TopBottom", 2, 4, 6, 8);
        var expected = CreateCSharpZone(scenario);
        var js = await RunGeometryScenarioAsync(scenario);

        expected.Should().NotBeNull();
        AssertJsRect(js.RootElement.GetProperty("zone").GetProperty("rect"), expected!.Rect);
        js.RootElement.GetProperty("zone").GetProperty("kind").GetString().Should().Be("fullWidth");
    }

    [Theory]
    [InlineData("BehindText")]
    [InlineData("InFrontOfText")]
    public async Task Phase9_CSharpAndJavaScriptIgnoreOverlayModesForExclusions(string mode)
    {
        if (!IsNodeAvailable()) return;

        var scenario = Scenario(mode, 4, 8, 6, 10);
        var expected = CreateCSharpZone(scenario);
        var js = await RunGeometryScenarioAsync(scenario);

        expected.Should().BeNull();
        js.RootElement.GetProperty("zone").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(12, 0, 0, 0)]
    [InlineData(0, 12, 0, 0)]
    [InlineData(0, 0, 12, 0)]
    [InlineData(0, 0, 0, 12)]
    public async Task Phase9_CSharpAndJavaScriptRespectIndividualWrapDistances(double left, double right, double top, double bottom)
    {
        if (!IsNodeAvailable()) return;

        var scenario = Scenario("Square", left, right, top, bottom);
        var expected = CreateCSharpZone(scenario);
        var js = await RunGeometryScenarioAsync(scenario);

        expected.Should().NotBeNull();
        AssertJsRect(js.RootElement.GetProperty("zone").GetProperty("rect"), expected!.Rect);
    }

    [Fact]
    public async Task Phase9_CSharpAndJavaScriptProcessContourPolygonTheSameWay()
    {
        if (!IsNodeAvailable()) return;

        var scenario = Scenario(
            "Tight",
            0,
            0,
            0,
            0,
            [
                new(0.5, 0),
                new(1, 0.5),
                new(0.5, 1),
                new(0, 0.5)
            ]);
        var expected = CreateCSharpZone(scenario);
        var js = await RunGeometryScenarioAsync(scenario);
        var zone = js.RootElement.GetProperty("zone");

        expected.Should().NotBeNull();
        AssertJsRect(zone.GetProperty("rect"), expected!.Rect);
        var polygon = zone.GetProperty("polygon").EnumerateArray().ToArray();
        polygon.Should().HaveCount(expected.Polygon.Count);
        for (var i = 0; i < polygon.Length; i++)
        {
            GetDouble(polygon[i], "x").Should().BeApproximately(expected.Polygon[i].X, 0.01);
            GetDouble(polygon[i], "y").Should().BeApproximately(expected.Polygon[i].Y, 0.01);
        }
    }

    [Fact]
    public async Task Phase9_CSharpAndJavaScriptReturnSameAvailableIntervalsForContour()
    {
        if (!IsNodeAvailable()) return;

        var scenario = Scenario(
            "Through",
            0,
            0,
            0,
            0,
            [
                new(0.1, 0),
                new(1, 0.15),
                new(0.78, 0.55),
                new(1, 1),
                new(0, 0.88),
                new(0.22, 0.45)
            ],
            lineY: 165,
            lineHeight: 14,
            minWidth: 1);
        var zone = CreateCSharpZone(scenario);
        zone.Should().NotBeNull();
        var expectedIntervals = DocumentLayoutGeometryHelper.GetAvailableLineIntervals(
            scenario.LineY,
            scenario.LineHeight,
            [zone!],
            scenario.Body,
            scenario.MinWidth);
        var js = await RunGeometryScenarioAsync(scenario);
        var intervals = js.RootElement.GetProperty("intervals").EnumerateArray().ToArray();

        intervals.Should().HaveCount(expectedIntervals.Count);
        for (var i = 0; i < intervals.Length; i++)
        {
            GetDouble(intervals[i], "x").Should().BeApproximately(expectedIntervals[i].X, 0.01);
            GetDouble(intervals[i], "width").Should().BeApproximately(expectedIntervals[i].Width, 0.01);
        }
    }

    [Fact]
    public async Task Phase9_JavaScriptNoAvailableIntervalFallbackMovesBelowBlockingExclusion()
    {
        if (!IsNodeAvailable()) return;

        var scenario = Scenario("TopBottom", 0, 0, 0, 0, lineY: 140, lineHeight: 20, minWidth: 1);
        var js = await RunGeometryScenarioAsync(scenario);

        js.RootElement.GetProperty("available").GetProperty("moved").GetBoolean().Should().BeTrue();
        GetDouble(js.RootElement.GetProperty("available"), "movedToY").Should().BeGreaterThan(140);
        js.RootElement.GetProperty("intervals").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Phase9_JavaScriptNormalizesObjectLayoutToStructuredShapeAndKeepsZeroOffsets()
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
            const layout = hooks.imageObjectToLayout({
                anchorBlockId: 'p1',
                anchorOffset: 0,
                anchorInlineIndex: 1,
                layoutKind: 'Anchored',
                wrapMode: 'Square',
                horizontalPosition: { relativeTo: 'Column', align: 'Left', offset: 0 },
                verticalPosition: { relativeTo: 'Paragraph', align: 'Top', offset: 0 },
                distanceLeft: 0,
                distanceRight: 12,
                distanceTop: 0,
                distanceBottom: 8,
                width: 120,
                height: 80,
                zIndex: 4
            });

            assert.ok(layout.Anchor, 'structured Anchor must be present');
            assert.ok(layout.Position, 'structured Position must be present');
            assert.ok(layout.Wrap, 'structured Wrap must be present');
            assert.ok(layout.Transform, 'structured Transform must be present');
            assert.strictEqual(layout.WrapMode, undefined, 'flat WrapMode must not be emitted');
            assert.strictEqual(layout.X, undefined, 'flat X must not be emitted');
            assert.strictEqual(layout.Position.X, 0, 'zero X must survive normalization');
            assert.strictEqual(layout.Position.Y, 0, 'zero Y must survive normalization');
            assert.strictEqual(layout.Wrap.DistanceLeft, 0, 'zero DistanceLeft must survive normalization');
            assert.strictEqual(layout.Wrap.DistanceRight, 12);
            assert.strictEqual(layout.Stacking.ZIndex, 4);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, SharedSandboxScript + nodeScript, "layout-shape");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase9_JavaScriptObjectOverlapPolicyMovesLaterObjectWhenOverlapIsForbidden()
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
            const body = { x: 72, y: 72, width: 456, height: 656 };
            const first = {
                objectId: 'first',
                blockId: 'p1',
                wrapMode: 'Square',
                allowOverlap: false,
                rect: { x: 100, y: 120, width: 120, height: 80 }
            };
            const second = {
                objectId: 'second',
                blockId: 'p2',
                wrapMode: 'Square',
                allowOverlap: false,
                rect: { x: 110, y: 130, width: 120, height: 80 }
            };

            hooks.resolveObjectOverlap([first], second, body);

            assert.strictEqual(second.rect.y, 208);
            assert.strictEqual(second.rect.x, 110);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, SharedSandboxScript + nodeScript, "overlap");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    private static GeometryScenario Scenario(
        string mode,
        double distanceLeft,
        double distanceRight,
        double distanceTop,
        double distanceBottom,
        IReadOnlyList<ContourPoint>? contour = null,
        double lineY = 140,
        double lineHeight = 20,
        double minWidth = 1)
        => new(
            mode,
            Rect(100, 120, 120, 80),
            Rect(72, 72, 456, 656),
            distanceLeft,
            distanceRight,
            distanceTop,
            distanceBottom,
            contour ?? [],
            lineY,
            lineHeight,
            minWidth);

    private static DocumentExclusionZone? CreateCSharpZone(GeometryScenario scenario)
    {
        var mode = Enum.Parse<DocumentWrapMode>(scenario.Mode);
        var layout = new DocumentObjectLayout
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Wrap = new DocumentObjectWrap
            {
                Mode = mode,
                DistanceLeft = scenario.DistanceLeft,
                DistanceRight = scenario.DistanceRight,
                DistanceTop = scenario.DistanceTop,
                DistanceBottom = scenario.DistanceBottom,
                WrapContourPoints = scenario.Contour
                    .Select(point => new DocumentObjectWrapPoint { X = point.X, Y = point.Y })
                    .ToList()
            },
            Transform = new DocumentObjectTransform
            {
                Width = scenario.ObjectRect.Width,
                Height = scenario.ObjectRect.Height
            }
        };
        var objectBox = new DocumentObjectLayoutBox
        {
            Id = "phase9-object",
            BlockId = "phase9-block",
            ObjectRect = scenario.ObjectRect,
            MediaRect = scenario.ObjectRect.Clone(),
            FootprintRect = scenario.ObjectRect.Clone(),
            WrapRect = DocumentLayoutGeometryHelper.ComputeWrapRect(scenario.ObjectRect, layout.Wrap),
            Layout = layout
        };

        return DocumentLayoutGeometryHelper.CreateExclusionZone(objectBox, scenario.Body);
    }

    private static async Task<JsonDocument> RunGeometryScenarioAsync(GeometryScenario scenario)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return JsonDocument.Parse("{}");

        var input = new
        {
            mode = scenario.Mode,
            rect = new
            {
                x = scenario.ObjectRect.X,
                y = scenario.ObjectRect.Y,
                width = scenario.ObjectRect.Width,
                height = scenario.ObjectRect.Height
            },
            body = new
            {
                x = scenario.Body.X,
                y = scenario.Body.Y,
                width = scenario.Body.Width,
                height = scenario.Body.Height
            },
            scenario.DistanceLeft,
            scenario.DistanceRight,
            scenario.DistanceTop,
            scenario.DistanceBottom,
            contour = scenario.Contour.Select(point => new { x = point.X, y = point.Y }).ToArray(),
            lineY = scenario.LineY,
            lineHeight = scenario.LineHeight,
            minWidth = scenario.MinWidth
        };

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const input = JSON.parse(process.argv[3]);
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const object = {
                objectId: 'phase9-object',
                blockId: 'phase9-block',
                wrapMode: input.mode,
                distanceLeft: input.DistanceLeft,
                distanceRight: input.DistanceRight,
                distanceTop: input.DistanceTop,
                distanceBottom: input.DistanceBottom,
                wrapContourPoints: input.contour,
                width: input.rect.width,
                height: input.rect.height,
                rect: input.rect,
                allowOverlap: false
            };
            const zone = hooks.createTextExclusion(object, input.body);
            const available = zone
                ? hooks.getAvailableIntervals(input.lineY, input.lineHeight, input.body, [zone], input.minWidth)
                : { intervals: [], moved: false, movedToY: input.lineY };
            console.log(JSON.stringify({
                zone,
                available,
                intervals: available.intervals,
                wrapRect: hooks.createObjectWrapRect(object, object.rect)
            }));
            """;

        var result = await RunNodeAsync(scriptPath, SharedSandboxScript + nodeScript, "geometry", JsonSerializer.Serialize(input));
        result.ExitCode.Should().Be(0, result.StandardError);
        return JsonDocument.Parse(result.StandardOutput);
    }

    private static void AssertJsRect(JsonElement actual, DocumentLayoutRect expected)
    {
        GetDouble(actual, "x").Should().BeApproximately(expected.X, 0.01);
        GetDouble(actual, "y").Should().BeApproximately(expected.Y, 0.01);
        GetDouble(actual, "width").Should().BeApproximately(expected.Width, 0.01);
        GetDouble(actual, "height").Should().BeApproximately(expected.Height, 0.01);
    }

    private static double GetDouble(JsonElement element, string property)
        => element.TryGetProperty(property, out var lower)
            ? lower.GetDouble()
            : element.GetProperty(char.ToUpperInvariant(property[0]) + property[1..]).GetDouble();

    private static DocumentLayoutRect Rect(double x, double y, double width, double height)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

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

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(
        string scriptPath,
        string nodeScript,
        string scenario,
        string? input = null)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase9-{scenario}-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, nodeScript);
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { tempFile, scriptPath, input ?? "{}" },
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

    private sealed record GeometryScenario(
        string Mode,
        DocumentLayoutRect ObjectRect,
        DocumentLayoutRect Body,
        double DistanceLeft,
        double DistanceRight,
        double DistanceTop,
        double DistanceBottom,
        IReadOnlyList<ContourPoint> Contour,
        double LineY,
        double LineHeight,
        double MinWidth);

    private sealed record ContourPoint(double X, double Y);
}
