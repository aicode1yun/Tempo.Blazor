using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Phase 15 stress checkpoints for image layout performance and runtime stability.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase15ImageLayoutStressE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase15_ImageLayoutStress_KeepsSelectionUndoRedoAndConsoleClean()
    {
        var browserErrors = new List<string>();
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        page.Console += (_, message) =>
        {
            if (string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                browserErrors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => browserErrors.Add(error);

        await page.SetViewportSizeAsync(1500, 950);
        await page.GotoAsync($"{BaseUrl}/document-editor", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);

        await LoadStressDocumentAsync(page);
        await page.WaitForFunctionAsync(
            """
            () => document.querySelectorAll('[data-testid="document-wysiwyg-host"] figure.tm-wysiwyg-image[data-block-id^="phase15-stress-img-"]').length >= 10
            """,
            new PageWaitForFunctionOptions { Timeout = 10000 });

        var loaded = await ReadStressStatsAsync(page);
        Assert.AreEqual(10, loaded.ImageCount, "The stress document must render all ten image objects.");
        Assert.IsTrue(loaded.LayoutObjectCount >= 10, "All stress images should participate in the object layout layer.");
        Assert.IsTrue(loaded.FirstPageObjectCount >= 3, "At least three image objects must share one page.");
        CollectionAssert.IsSubsetOf(new[] { 1, 2, 3 }, loaded.FirstPageZIndexes, "The first page should keep multiple z-order levels.");
        Assert.IsTrue(loaded.LayoutLineCount >= 6, "The long paragraph should be split into visible layout lines around images.");
        Assert.IsTrue(loaded.LayoutPassCount >= 1, "Loading the stress document should record a layout pass.");

        var firstFigure = page.Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='phase15-stress-img-0']").First;
        await firstFigure.ClickAsync();
        await RunImageOperationSeriesAsync(page, "phase15-stress-img-0");

        var afterOperations = await ReadStressStatsAsync(page);
        Assert.AreEqual("phase15-stress-img-0", afterOperations.ActiveImageBlockId, "Image commands must keep runtime image selection stable.");
        Assert.IsTrue(afterOperations.SelectedImageCount >= 1, "The selected image should stay visibly selected after rapid image operations.");
        Assert.IsTrue(afterOperations.UndoDepth >= 20, "The image operation series should create undoable runtime transactions.");

        var undoRedo = await RunUndoRedoSeriesAsync(page, 20);
        Assert.AreEqual(20, undoRedo.UndoCount, "All image operations should be undoable.");
        Assert.AreEqual(20, undoRedo.RedoCount, "All image operations should be redoable.");

        var final = await ReadStressStatsAsync(page);
        Assert.AreEqual(10, final.ImageCount, "Undo/redo must not drop images from the document.");
        Assert.IsTrue(final.LayoutObjectCount >= 10, "Undo/redo must keep images in the object layout layer.");
        Assert.IsFalse(browserErrors.Any(), string.Join(Environment.NewLine, browserErrors));
    }

    private static Task LoadStressDocumentAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const snapshot = JSON.parse(window.tmDocumentEditorRuntime.getDocument(instanceId));
                const doc = snapshot.Document || snapshot.document;
                const dataUrl = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mNk+M9Qz0AEYBxVSF+FAAB1NQITqAqC5QAAAABJRU5ErkJggg==';
                const lorem = 'This long phase 15 paragraph intentionally wraps across several positioned images so layout lines, side text, object exclusions, and z-index ordering are all exercised together. ';
                const blocks = [{
                    Id: 'phase15-stress-intro',
                    Type: 0,
                    Order: 0,
                    Content: {
                        $type: 'paragraph',
                        Inlines: [{ Id: 'phase15-stress-intro-inline', Text: lorem.repeat(18) }]
                    }
                }];

                for (let i = 0; i < 10; i++) {
                    const row = Math.floor(i / 3);
                    const column = i % 3;
                    const width = 118 + (i % 3) * 18;
                    const height = 74 + (i % 2) * 16;
                    blocks.push({
                        Id: `phase15-stress-img-${i}`,
                        Type: 5,
                        Order: 1 + i,
                        Content: {
                            $type: 'image',
                            Source: 0,
                            Url: dataUrl,
                            AssetId: `phase15-stress-asset-${i}`,
                            AltText: `Phase 15 stress image ${i}`,
                            Caption: `Stress image ${i}`,
                            Size: { Width: width, Height: height, LockAspectRatio: true },
                            NaturalSize: { Width: width, Height: height, LockAspectRatio: true },
                            Alignment: 0,
                            Layout: {
                                Kind: 1,
                                Anchor: {
                                    BlockId: 'phase15-stress-intro',
                                    MoveWithText: true,
                                    FixedOnPage: false,
                                    LockAnchor: false
                                },
                                Position: {
                                    HorizontalRelativeTo: 0,
                                    VerticalRelativeTo: 0,
                                    X: 28 + column * 190,
                                    Y: 86 + row * 118,
                                    HorizontalAlignment: column === 2 ? 2 : 0,
                                    VerticalAlignment: 0
                                },
                                Wrap: {
                                    Mode: i % 4 === 3 ? 2 : 1,
                                    DistanceLeft: 8,
                                    DistanceRight: 10,
                                    DistanceTop: 5,
                                    DistanceBottom: 6,
                                    WrapContourPoints: [
                                        { X: 0, Y: 0 },
                                        { X: 1, Y: 0 },
                                        { X: 1, Y: 1 },
                                        { X: 0, Y: 1 }
                                    ]
                                },
                                Transform: {
                                    Width: width,
                                    Height: height,
                                    NaturalWidth: width,
                                    NaturalHeight: height,
                                    LockAspectRatio: true
                                },
                                Stacking: {
                                    ZIndex: i + 1,
                                    AllowOverlap: true
                                }
                            }
                        }
                    });
                }

                blocks.push({
                    Id: 'phase15-stress-tail',
                    Type: 0,
                    Order: 20,
                    Content: {
                        $type: 'paragraph',
                        Inlines: [{ Id: 'phase15-stress-tail-inline', Text: lorem.repeat(12) }]
                    }
                });

                doc.Blocks = blocks;
                doc.blocks = blocks;
                doc.Comments = [];
                doc.comments = [];
                window.tmDocumentEditorRuntime.loadDocument(instanceId, snapshot, true);
            }
            """);
    }

    private static Task RunImageOperationSeriesAsync(IPage page, string imageId)
    {
        return page.EvaluateAsync(
            """
            imageId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                for (let i = 0; i < 10; i++) {
                    window.tmDocumentEditorRuntime.executeCommand(instanceId, 'setImageObjectPosition', {
                        BlockId: imageId,
                        X: 36 + i * 6,
                        Y: 92 + i * 3,
                        HorizontalRelativeTo: 'Page',
                        VerticalRelativeTo: 'Page',
                        HorizontalPosition: 'Left'
                    });
                    window.tmDocumentEditorRuntime.executeCommand(instanceId, 'setImageSize', {
                        BlockId: imageId,
                        Width: 130 + i * 4,
                        Height: 82 + i * 2,
                        LockAspectRatio: false
                    });
                }
            }
            """,
            imageId);
    }

    private static Task<UndoRedoResult> RunUndoRedoSeriesAsync(IPage page, int count)
    {
        return page.EvaluateAsync<UndoRedoResult>(
            """
            count => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                let undoCount = 0;
                let redoCount = 0;
                for (let i = 0; i < count; i++) {
                    if (window.tmDocumentEditorRuntime.undo(instanceId)) undoCount++;
                }
                for (let i = 0; i < count; i++) {
                    if (window.tmDocumentEditorRuntime.redo(instanceId)) redoCount++;
                }

                return { undoCount, redoCount };
            }
            """,
            count);
    }

    private static Task<ImageLayoutStressStats> ReadStressStatsAsync(IPage page)
    {
        return page.EvaluateAsync<ImageLayoutStressStats>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const figures = Array.from(host?.querySelectorAll('figure.tm-wysiwyg-image[data-block-id^="phase15-stress-img-"]') || []);
                const layoutObjects = figures.filter(figure => figure.classList.contains('tm-wysiwyg-layout-object'));
                const firstPage = host?.querySelector('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual)');
                const firstPageObjects = Array.from(firstPage?.querySelectorAll('figure.tm-wysiwyg-layout-object[data-block-id^="phase15-stress-img-"]') || []);
                const debug = window.tmDocumentEditorRuntime?.getDebugSnapshot?.(instanceId) || {};
                const selection = window.tmDocumentEditorRuntime?.getRuntimeSelection?.(instanceId) || {};
                const firstPageZIndexes = firstPageObjects
                    .map(figure => Number.parseInt(figure.getAttribute('data-object-z-index') || figure.style.zIndex || '0', 10) || 0)
                    .filter(value => Number.isFinite(value))
                    .sort((a, b) => a - b);

                return {
                    imageCount: figures.length,
                    layoutObjectCount: layoutObjects.length,
                    firstPageObjectCount: firstPageObjects.length,
                    firstPageZIndexes,
                    layoutLineCount: host?.querySelectorAll('.tm-wysiwyg-layout-line[data-layout-line-id]').length || 0,
                    selectedImageCount: host?.querySelectorAll('figure.tm-wysiwyg-image--selected[data-block-id^="phase15-stress-img-"]').length || 0,
                    activeImageBlockId: selection.activeImageBlockId || selection.ActiveImageBlockId || debug.CurrentSelection?.ActiveImageBlockId || '',
                    undoDepth: Number(debug.UndoDepth || 0),
                    redoDepth: Number(debug.RedoDepth || 0),
                    layoutPassCount: Number(debug.LayoutPassCount || debug.Performance?.LayoutPassCount || 0)
                };
            }
            """);
    }

    private sealed class ImageLayoutStressStats
    {
        [JsonPropertyName("imageCount")]
        public int ImageCount { get; set; }

        [JsonPropertyName("layoutObjectCount")]
        public int LayoutObjectCount { get; set; }

        [JsonPropertyName("firstPageObjectCount")]
        public int FirstPageObjectCount { get; set; }

        [JsonPropertyName("firstPageZIndexes")]
        public int[] FirstPageZIndexes { get; set; } = [];

        [JsonPropertyName("layoutLineCount")]
        public int LayoutLineCount { get; set; }

        [JsonPropertyName("selectedImageCount")]
        public int SelectedImageCount { get; set; }

        [JsonPropertyName("activeImageBlockId")]
        public string? ActiveImageBlockId { get; set; }

        [JsonPropertyName("undoDepth")]
        public int UndoDepth { get; set; }

        [JsonPropertyName("redoDepth")]
        public int RedoDepth { get; set; }

        [JsonPropertyName("layoutPassCount")]
        public int LayoutPassCount { get; set; }
    }

    private sealed class UndoRedoResult
    {
        [JsonPropertyName("undoCount")]
        public int UndoCount { get; set; }

        [JsonPropertyName("redoCount")]
        public int RedoCount { get; set; }
    }
}
