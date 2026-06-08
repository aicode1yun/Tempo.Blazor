using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.E2E.CanvasEngine;

/// <summary>Visual assertions shared by canvas document editor E2E gates.</summary>
public static class DocumentEditorCanvasVisualAssert
{
    /// <summary>Reads deterministic pixel metrics directly from a canvas backing store.</summary>
    public static Task<CanvasPixelMetrics> ReadCanvasPixelMetricsAsync(ILocator canvas)
        => canvas.EvaluateAsync<CanvasPixelMetrics>(
            """
            canvas => {
                const context = canvas.getContext('2d', { willReadFrequently: true });
                const width = canvas.width || 0;
                const height = canvas.height || 0;
                const pixelCount = width * height;
                if (!context || pixelCount === 0) {
                    return {
                        width,
                        height,
                        pixelCount,
                        nonTransparentPixels: 0,
                        distinctColorCount: 0,
                        minX: null,
                        minY: null,
                        maxX: null,
                        maxY: null
                    };
                }

                const data = context.getImageData(0, 0, width, height).data;
                const colors = new Set();
                let nonTransparentPixels = 0;
                let minX = width;
                let minY = height;
                let maxX = -1;
                let maxY = -1;
                const sampleStride = Math.max(1, Math.floor(pixelCount / 4096));

                for (let offset = 0, pixel = 0; offset < data.length; offset += 4, pixel++) {
                    const alpha = data[offset + 3];
                    if (pixel % sampleStride === 0) {
                        colors.add(`${data[offset]},${data[offset + 1]},${data[offset + 2]},${alpha}`);
                    }

                    if (alpha === 0) {
                        continue;
                    }

                    nonTransparentPixels++;
                    const x = pixel % width;
                    const y = Math.floor(pixel / width);
                    minX = Math.min(minX, x);
                    minY = Math.min(minY, y);
                    maxX = Math.max(maxX, x);
                    maxY = Math.max(maxY, y);
                }

                return {
                    width,
                    height,
                    pixelCount,
                    nonTransparentPixels,
                    distinctColorCount: colors.size,
                    minX: maxX >= 0 ? minX : null,
                    minY: maxY >= 0 ? minY : null,
                    maxX: maxX >= 0 ? maxX : null,
                    maxY: maxY >= 0 ? maxY : null
                };
            }
            """);

    /// <summary>Asserts that a canvas contains intentional non-uniform painted content.</summary>
    public static async Task<CanvasPixelMetrics> AssertCanvasNonBlankAsync(ILocator canvas)
    {
        var metrics = await ReadCanvasPixelMetricsAsync(canvas);
        Assert.IsTrue(metrics.PixelCount > 0, "Canvas must have a non-zero backing store.");
        Assert.IsTrue(metrics.NonTransparentRatio > 0.001, $"Canvas must paint visible pixels. Ratio: {metrics.NonTransparentRatio:P4}.");
        Assert.IsTrue(metrics.DistinctColorCount >= 2, $"Canvas must not be a single-color fill. Distinct sampled colors: {metrics.DistinctColorCount}.");
        Assert.IsTrue(metrics.MinX.HasValue && metrics.MinY.HasValue && metrics.MaxX.HasValue && metrics.MaxY.HasValue, "Canvas painted bounds must be available.");
        return metrics;
    }

    /// <summary>Stores a canvas region backing-store snapshot in the browser process for a later pixel diff.</summary>
    public static Task<CanvasPixelMetrics> CaptureCanvasRegionSnapshotAsync(ILocator canvas, string key, double x, double y, double width, double height)
        => canvas.EvaluateAsync<CanvasPixelMetrics>(
            """
            (canvas, args) => {
                const context = canvas.getContext('2d', { willReadFrequently: true });
                const rect = canvas.getBoundingClientRect();
                const scaleX = rect.width > 0 ? (canvas.width || 0) / rect.width : 1;
                const scaleY = rect.height > 0 ? (canvas.height || 0) / rect.height : 1;
                const sampleX = Math.max(0, Math.min(canvas.width || 0, Math.round((Number(args.x) || 0) * scaleX)));
                const sampleY = Math.max(0, Math.min(canvas.height || 0, Math.round((Number(args.y) || 0) * scaleY)));
                const sampleWidth = Math.max(1, Math.min((canvas.width || 0) - sampleX, Math.round((Number(args.width) || 1) * scaleX)));
                const sampleHeight = Math.max(1, Math.min((canvas.height || 0) - sampleY, Math.round((Number(args.height) || 1) * scaleY)));
                const pixelCount = sampleWidth * sampleHeight;
                if (!context || pixelCount <= 0) {
                    return {
                        width: sampleWidth,
                        height: sampleHeight,
                        pixelCount,
                        nonTransparentPixels: 0,
                        distinctColorCount: 0,
                        minX: null,
                        minY: null,
                        maxX: null,
                        maxY: null
                    };
                }

                const image = context.getImageData(sampleX, sampleY, sampleWidth, sampleHeight);
                canvas.__tempoE2eCanvasRegionSnapshots = canvas.__tempoE2eCanvasRegionSnapshots || Object.create(null);
                canvas.__tempoE2eCanvasRegionSnapshots[String(args.key || 'default')] = {
                    x: sampleX,
                    y: sampleY,
                    width: sampleWidth,
                    height: sampleHeight,
                    data: new Uint8ClampedArray(image.data)
                };

                const colors = new Set();
                let nonTransparentPixels = 0;
                let minX = sampleWidth;
                let minY = sampleHeight;
                let maxX = -1;
                let maxY = -1;
                const sampleStride = Math.max(1, Math.floor(pixelCount / 4096));

                for (let offset = 0, pixel = 0; offset < image.data.length; offset += 4, pixel++) {
                    const alpha = image.data[offset + 3];
                    if (pixel % sampleStride === 0) {
                        colors.add(`${image.data[offset]},${image.data[offset + 1]},${image.data[offset + 2]},${alpha}`);
                    }

                    if (alpha === 0) {
                        continue;
                    }

                    nonTransparentPixels++;
                    const px = pixel % sampleWidth;
                    const py = Math.floor(pixel / sampleWidth);
                    minX = Math.min(minX, px);
                    minY = Math.min(minY, py);
                    maxX = Math.max(maxX, px);
                    maxY = Math.max(maxY, py);
                }

                return {
                    width: sampleWidth,
                    height: sampleHeight,
                    pixelCount,
                    nonTransparentPixels,
                    distinctColorCount: colors.size,
                    minX: maxX >= 0 ? minX : null,
                    minY: maxY >= 0 ? minY : null,
                    maxX: maxX >= 0 ? maxX : null,
                    maxY: maxY >= 0 ? maxY : null
                };
            }
            """,
            new { key, x, y, width, height });

    /// <summary>Asserts that a previously captured canvas region changed by real RGBA pixels.</summary>
    public static async Task<CanvasPixelDelta> AssertCanvasRegionChangedFromSnapshotAsync(ILocator canvas, string key, int minimumChangedPixels)
    {
        var delta = await canvas.EvaluateAsync<CanvasPixelDelta>(
            """
            (canvas, key) => {
                const snapshots = canvas.__tempoE2eCanvasRegionSnapshots || Object.create(null);
                const snapshot = snapshots[String(key || 'default')];
                const context = canvas.getContext('2d', { willReadFrequently: true });
                if (!context || !snapshot || !snapshot.data) {
                    return { pixelCount: 0, changedPixels: 0, minX: null, minY: null, maxX: null, maxY: null };
                }

                const current = context.getImageData(snapshot.x, snapshot.y, snapshot.width, snapshot.height).data;
                const before = snapshot.data;
                const pixelCount = snapshot.width * snapshot.height;
                let changedPixels = 0;
                let minX = snapshot.width;
                let minY = snapshot.height;
                let maxX = -1;
                let maxY = -1;

                for (let offset = 0, pixel = 0; offset < before.length && offset < current.length; offset += 4, pixel++) {
                    if (
                        before[offset] === current[offset]
                        && before[offset + 1] === current[offset + 1]
                        && before[offset + 2] === current[offset + 2]
                        && before[offset + 3] === current[offset + 3]
                    ) {
                        continue;
                    }

                    changedPixels++;
                    const px = pixel % snapshot.width;
                    const py = Math.floor(pixel / snapshot.width);
                    minX = Math.min(minX, px);
                    minY = Math.min(minY, py);
                    maxX = Math.max(maxX, px);
                    maxY = Math.max(maxY, py);
                }

                return {
                    pixelCount,
                    changedPixels,
                    minX: maxX >= 0 ? minX : null,
                    minY: maxY >= 0 ? minY : null,
                    maxX: maxX >= 0 ? maxX : null,
                    maxY: maxY >= 0 ? maxY : null
                };
            }
            """,
            key);

        Assert.IsTrue(delta.PixelCount > 0, $"Canvas region snapshot '{key}' must exist and cover pixels.");
        Assert.IsTrue(delta.ChangedPixels >= minimumChangedPixels, $"Canvas region '{key}' must change by at least {minimumChangedPixels:N0} pixels. Changed: {delta.ChangedPixels:N0}.");
        Assert.IsTrue(delta.MinX.HasValue && delta.MinY.HasValue && delta.MaxX.HasValue && delta.MaxY.HasValue, $"Canvas region '{key}' diff bounds must be available.");
        return delta;
    }

    /// <summary>Asserts that at least one canvas matching a selector contains intentional painted content.</summary>
    public static async Task<CanvasPixelMetrics> AssertAnyCanvasNonBlankAsync(IPage page, string selector)
    {
        var canvases = page.Locator(selector);
        var count = await canvases.CountAsync();
        Assert.IsTrue(count > 0, $"Canvas selector '{selector}' must match at least one canvas.");

        var diagnostics = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var metrics = await ReadCanvasPixelMetricsAsync(canvases.Nth(index));
            diagnostics.Add($"#{index}: {metrics.Width}x{metrics.Height}, ratio={metrics.NonTransparentRatio:P4}, colors={metrics.DistinctColorCount}");
            if (metrics.PixelCount > 0
                && metrics.NonTransparentRatio > 0.001
                && metrics.DistinctColorCount >= 2
                && metrics.MinX.HasValue
                && metrics.MinY.HasValue
                && metrics.MaxX.HasValue
                && metrics.MaxY.HasValue)
            {
                return metrics;
            }
        }

        Assert.Fail($"At least one canvas matching '{selector}' must paint visible non-uniform pixels. Candidates: {string.Join("; ", diagnostics)}.");
        return new CanvasPixelMetrics();
    }

    /// <summary>Asserts that two canvas backing stores differ in visible pixels.</summary>
    public static async Task<CanvasPixelDelta> AssertTextPixelsChangedAsync(IPage page, string beforeCanvasSelector, string afterCanvasSelector)
    {
        var delta = await page.EvaluateAsync<CanvasPixelDelta>(
            """
            ([beforeCanvasSelector, afterCanvasSelector]) => {
                const beforeCanvas = document.querySelector(beforeCanvasSelector);
                const afterCanvas = document.querySelector(afterCanvasSelector);
                if (!beforeCanvas || !afterCanvas) {
                    return { pixelCount: 0, changedPixels: 0, minX: null, minY: null, maxX: null, maxY: null };
                }

                const width = Math.min(beforeCanvas.width || 0, afterCanvas.width || 0);
                const height = Math.min(beforeCanvas.height || 0, afterCanvas.height || 0);
                const pixelCount = width * height;
                if (pixelCount === 0) {
                    return { pixelCount, changedPixels: 0, minX: null, minY: null, maxX: null, maxY: null };
                }

                const beforeData = beforeCanvas.getContext('2d', { willReadFrequently: true }).getImageData(0, 0, width, height).data;
                const afterData = afterCanvas.getContext('2d', { willReadFrequently: true }).getImageData(0, 0, width, height).data;
                let changedPixels = 0;
                let minX = width;
                let minY = height;
                let maxX = -1;
                let maxY = -1;

                for (let offset = 0, pixel = 0; offset < beforeData.length; offset += 4, pixel++) {
                    if (
                        beforeData[offset] === afterData[offset] &&
                        beforeData[offset + 1] === afterData[offset + 1] &&
                        beforeData[offset + 2] === afterData[offset + 2] &&
                        beforeData[offset + 3] === afterData[offset + 3]
                    ) {
                        continue;
                    }

                    changedPixels++;
                    const x = pixel % width;
                    const y = Math.floor(pixel / width);
                    minX = Math.min(minX, x);
                    minY = Math.min(minY, y);
                    maxX = Math.max(maxX, x);
                    maxY = Math.max(maxY, y);
                }

                return {
                    pixelCount,
                    changedPixels,
                    minX: maxX >= 0 ? minX : null,
                    minY: maxY >= 0 ? minY : null,
                    maxX: maxX >= 0 ? maxX : null,
                    maxY: maxY >= 0 ? maxY : null
                };
            }
            """,
            new[] { beforeCanvasSelector, afterCanvasSelector });

        Assert.IsTrue(delta.PixelCount > 0, "Canvas diff must compare non-empty backing stores.");
        Assert.IsTrue(delta.ChangedPixels > 0, "Canvas diff must detect changed text/content pixels.");
        Assert.IsTrue(delta.MinX.HasValue && delta.MinY.HasValue && delta.MaxX.HasValue && delta.MaxY.HasValue, "Canvas diff must expose changed bounds.");
        return delta;
    }

    /// <summary>Asserts that a caret element is visible and has measurable geometry.</summary>
    public static async Task AssertCaretVisibleAsync(ILocator caret)
        => await AssertVisibleRectAsync(caret, "Caret");

    /// <summary>Asserts that a selection element is visible and has measurable geometry.</summary>
    public static async Task AssertSelectionVisibleAsync(ILocator selection)
        => await AssertVisibleRectAsync(selection, "Selection");

    /// <summary>Asserts that text rectangles do not geometrically overlap.</summary>
    public static Task AssertNoTextOverlapAsync(IPage page, string selector = "[data-canvas-text-rect]")
        => AssertNoOverlapAsync(page, selector, "Text rectangles");

    /// <summary>Asserts that UI rectangles do not geometrically overlap.</summary>
    public static Task AssertNoUiOverlapAsync(IPage page, string selector = "[data-canvas-ui-rect]")
        => AssertNoOverlapAsync(page, selector, "UI rectangles");

    /// <summary>Asserts that toolbar command pressed states match expected model state.</summary>
    public static async Task AssertToolbarStateMatchesModelAsync(IPage page, IReadOnlyDictionary<string, bool>? expectedPressedStates = null)
    {
        expectedPressedStates ??= new Dictionary<string, bool>();
        var actualStateJson = await page.EvaluateAsync<string>(
            """
            () => JSON.stringify(Object.fromEntries(Array.from(document.querySelectorAll('[data-canvas-toolbar-command]')).map(button => [
                button.getAttribute('data-canvas-toolbar-command'),
                button.getAttribute('aria-pressed') === 'true'
            ]).filter(([command]) => !!command)))
            """);
        var actualStates = JsonSerializer.Deserialize<Dictionary<string, bool>>(
            actualStateJson ?? "{}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];

        Assert.AreEqual(expectedPressedStates.Count, actualStates.Count, "Toolbar command count must match the model state count.");
        foreach (var (command, expected) in expectedPressedStates)
        {
            Assert.IsTrue(actualStates.ContainsKey(command), $"Toolbar command '{command}' must be rendered.");
            Assert.AreEqual(expected, actualStates[command], $"Toolbar command '{command}' pressed state must match the model.");
        }
    }

    /// <summary>Asserts screenshot evidence exists and records reviewer notes in the manifest.</summary>
    public static Task AssertScreenshotLooksIntentionalAsync(CanvasVisualReviewManifest manifest, string reviewerNotes)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(reviewerNotes), "Reviewer notes must describe the visual verdict.");
        Assert.IsTrue(manifest.ScreenshotPaths.Count > 0, "Visual review must include screenshots.");

        foreach (var path in manifest.ScreenshotPaths)
        {
            var info = new FileInfo(path);
            Assert.IsTrue(info.Exists, $"Screenshot must exist: {path}");
            Assert.IsTrue(info.Length > 0, $"Screenshot must not be empty: {path}");
        }

        manifest.UxReviewerNotes = reviewerNotes;
        return Task.CompletedTask;
    }

    private static async Task AssertVisibleRectAsync(ILocator locator, string name)
    {
        Assert.IsTrue(await locator.IsVisibleAsync(), $"{name} must be visible.");
        var box = await locator.BoundingBoxAsync();
        Assert.IsNotNull(box, $"{name} must expose a bounding box.");
        Assert.IsTrue(box.Width > 0, $"{name} width must be greater than zero.");
        Assert.IsTrue(box.Height > 0, $"{name} height must be greater than zero.");
    }

    private static async Task AssertNoOverlapAsync(IPage page, string selector, string label)
    {
        var rectJson = await page.EvaluateAsync<string>(
            """
            selector => JSON.stringify(Array.from(document.querySelectorAll(selector)).map((node, index) => {
                const rect = node.getBoundingClientRect();
                return {
                    id: node.getAttribute('data-id') || node.getAttribute('data-testid') || `${selector}:${index}`,
                    blockId: node.getAttribute('data-block-id') || '',
                    runId: node.getAttribute('data-run-id') || '',
                    text: node.getAttribute('data-canvas-text') || '',
                    startOffset: Number(node.getAttribute('data-canvas-start-offset') || '0') || 0,
                    endOffset: Number(node.getAttribute('data-canvas-end-offset') || '0') || 0,
                    x: rect.x,
                    y: rect.y,
                    width: rect.width,
                    height: rect.height
                };
            }).filter(rect => rect.width > 0 && rect.height > 0))
            """,
            selector) ?? "[]";
        var rects = JsonSerializer.Deserialize<List<CanvasDomRect>>(rectJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];

        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                var overlapWidth = Math.Min(rects[i].Right, rects[j].Right) - Math.Max(rects[i].X, rects[j].X);
                var overlapHeight = Math.Min(rects[i].Bottom, rects[j].Bottom) - Math.Max(rects[i].Y, rects[j].Y);
                Assert.IsFalse(overlapWidth > 0.5 && overlapHeight > 0.5, $"{label} overlap: {DescribeRect(rects[i])} intersects {DescribeRect(rects[j])}.");
            }
        }
    }

    private static string DescribeRect(CanvasDomRect rect)
        => $"{rect.Id} block={rect.BlockId} run={rect.RunId} offsets={rect.StartOffset}-{rect.EndOffset} text='{TrimForMessage(rect.Text)}' x={rect.X:0.##} y={rect.Y:0.##} w={rect.Width:0.##} h={rect.Height:0.##}";

    private static string TrimForMessage(string value)
    {
        var text = (value ?? string.Empty).ReplaceLineEndings(" ").Trim();
        return text.Length <= 48 ? text : string.Concat(text.AsSpan(0, 45), "...");
    }

    private sealed class CanvasDomRect
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("blockId")]
        public string BlockId { get; set; } = string.Empty;

        [JsonPropertyName("runId")]
        public string RunId { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("startOffset")]
        public int StartOffset { get; set; }

        [JsonPropertyName("endOffset")]
        public int EndOffset { get; set; }

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        public double Right => X + Width;
        public double Bottom => Y + Height;
    }
}
