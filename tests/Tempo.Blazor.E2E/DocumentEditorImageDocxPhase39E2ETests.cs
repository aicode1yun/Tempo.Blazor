using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>End-to-end DOCX import/edit/export coverage for imported DrawingML image objects.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:Images")]
[TestCategory("DocumentEditor:DOCX")]
[TestCategory("DocumentEditor:HumanWorkflow")]
[TestCategory("DocumentEditor:ProviderBoundary")]
[DoNotParallelize]
public sealed class DocumentEditorImageDocxPhase39E2ETests : DocumentEditorE2ETestBase
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";

    [TestMethod]
    public async Task Phase39_ImportEditExport_PreservesInlineSquareHeaderAndTableDrawingMl()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var fixturePath = await DownloadImageParityFixtureViaDemoEndpointAsync();

        await ImportDocxThroughToolbarAsync(page, fixturePath);

        var inline = await WaitForDrawingByAltTextAsync(page, "Inline recovery image");
        var square = await WaitForDrawingByAltTextAsync(page, "Left wrapped recovery image");
        var header = await WaitForDrawingByAltTextAsync(page, "Header logo evidence");
        var table = await WaitForDrawingByAltTextAsync(page, "Table cell evidence image");

        await Assertions.Expect(page.GetByTestId($"document-wysiwyg-drawing-object-{inline.ObjectId}")).ToBeAttachedAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.GetByTestId($"document-wysiwyg-drawing-object-{square.ObjectId}")).ToBeAttachedAsync(new() { Timeout = 10000 });
        Assert.AreEqual("Header", header.Region, $"Header drawing must remain anchored in header region. {header.AltText}/{header.ObjectId}");
        Assert.AreEqual("TableCell", table.Region, $"Table drawing must remain anchored in table cell region. {table.AltText}/{table.ObjectId}");

        const string inlineMarker = "phase39-inline-before";
        const string squareMarker = "phase39-square-beside";
        const string headerMarker = "phase39-header-edit";
        const string tableMarker = "phase39-table-cell-edit";

        await ClickBeforeImageAsync(page, inline.ObjectId);
        await page.Keyboard.InsertTextAsync($"{inlineMarker} ");
        await WaitForEditorStableAsync(page, "inline imported image edit", expectedVisibleText: inlineMarker, timeoutMs: 10000);

        await ClickDocumentEditorBlockOffsetAsync(page, square.AnchorBlockId, 8);
        await page.Keyboard.InsertTextAsync($" {squareMarker} ");
        await WaitForEditorStableAsync(page, "square imported image edit", square.AnchorBlockId, squareMarker, timeoutMs: 10000);

        await ClickDocumentEditorBlockOffsetAsync(page, header.AnchorBlockId, 6);
        await page.Keyboard.InsertTextAsync($" {headerMarker} ");
        await WaitForEditorStableAsync(page, "header imported image edit", header.AnchorBlockId, headerMarker, timeoutMs: 10000);

        await ClickDocumentEditorBlockOffsetAsync(page, table.AnchorBlockId, 8);
        await page.Keyboard.InsertTextAsync($" {tableMarker} ");
        await WaitForEditorStableAsync(page, "table imported image edit", table.AnchorBlockId, tableMarker, timeoutMs: 10000);

        await OpenDocumentJsonDebugAsync(page);
        await Assertions.Expect(page.GetByTestId("document-docx-drawing-debug")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(page.GetByTestId("document-docx-drawing-debug-content")).ToContainTextAsync("Header logo evidence", new() { Timeout = 10000 });
        await page.GetByTestId("document-json-debug-close").ClickAsync();

        var exportedPath = await ExportDocxThroughToolbarAsync(page);
        var documentXml = ReadDocxXml(exportedPath, "word/document.xml");
        var headerXml = ReadFirstHeaderXml(exportedPath);

        AssertDocxTextContains(documentXml, inlineMarker, "main document inline edit");
        AssertDocxTextContains(documentXml, squareMarker, "main document square edit");
        AssertDocxTextContains(documentXml, tableMarker, "main document table edit");
        AssertDocxTextContains(headerXml, headerMarker, "header edit");
        Assert.IsTrue(documentXml.Descendants(Wp + "inline").Any(), "Exported document.xml must keep inline DrawingML hosts.");
        Assert.IsTrue(documentXml.Descendants(Wp + "anchor").Any(), "Exported document.xml must keep anchored DrawingML hosts.");
        Assert.IsTrue(documentXml.Descendants(Wp + "wrapSquare").Any(), "Exported document.xml must keep square wrap DrawingML.");
        Assert.IsTrue(documentXml.Descendants(W + "tc").Any(cell => cell.Descendants(W + "drawing").Any()), "Exported document.xml must keep table-cell drawings inside w:tc.");
        Assert.IsTrue(headerXml.Descendants(W + "drawing").Any(), "Exported header XML must keep header drawings.");
        Assert.IsFalse(documentXml.ToString(SaveOptions.DisableFormatting).Contains("[Image]", StringComparison.Ordinal), "DOCX export must not fall back to text image placeholders.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Phase39_ImportEditExport_PreservesInlineSquareHeaderAndTableDrawingMl));
    }

    [TestMethod]
    public async Task Phase39_ImportedCropInspectorAndWrapChange_ExportsUpdatedDrawingMl()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var fixturePath = await DownloadImageParityFixtureViaDemoEndpointAsync();
        await ImportDocxThroughToolbarAsync(page, fixturePath);

        var cropped = await WaitForDrawingByAltTextAsync(page, "Cropped image parity");
        Assert.IsTrue(cropped.CropLeft > 0 && cropped.CropTop > 0 && cropped.CropRight > 0 && cropped.CropBottom > 0,
            $"Imported cropped image must expose crop values. Object={cropped.ObjectId}, crop={cropped.CropLeft}/{cropped.CropTop}/{cropped.CropRight}/{cropped.CropBottom}");

        await ClickImageCenterAsync(page, cropped.ObjectId);
        await EnsureImageInspectorVisibleAsync(page);
        await Assertions.Expect(page.GetByTestId("document-image-inspector-width")).ToBeVisibleAsync(new() { Timeout = 10000 });

        await OpenDocumentJsonDebugAsync(page);
        await Assertions.Expect(page.GetByTestId("document-docx-drawing-debug-content")).ToContainTextAsync("Cropped image parity", new() { Timeout = 10000 });
        await Assertions.Expect(page.GetByTestId("document-docx-drawing-debug-content")).ToContainTextAsync("Crop", new() { Timeout = 10000 });
        await page.GetByTestId("document-json-debug-close").ClickAsync();

        var square = await WaitForDrawingByAltTextAsync(page, "Left wrapped recovery image");
        await ClickImageCenterAsync(page, square.ObjectId);
        await Assertions.Expect(page.GetByTestId("document-image-wrap-panel")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.GetByTestId("document-image-wrap-top-bottom").ClickAsync();
        var afterWrap = await WaitForDrawingByObjectIdAsync(
            page,
            square.ObjectId,
            run => string.Equals(run.WrapMode, "TopBottom", StringComparison.Ordinal),
            "imported square wrap mode changed to TopBottom");
        Assert.AreEqual("TopBottom", afterWrap.WrapMode, $"Wrap mode must update through the image toolbar. Object={afterWrap.ObjectId}");

        var exportedPath = await ExportDocxThroughToolbarAsync(page);
        var documentXml = ReadDocxXml(exportedPath, "word/document.xml");
        var croppedHost = FindDrawingHostByAltText(documentXml, "Cropped image parity");
        Assert.IsNotNull(croppedHost, "Exported DOCX must keep the cropped image host.");
        Assert.IsTrue(croppedHost!.Descendants().Any(element => element.Name.LocalName == "srcRect"), "Crop source rectangle must stay in exported DrawingML.");
        var squareHost = FindDrawingHostByAltText(documentXml, "Left wrapped recovery image");
        Assert.IsNotNull(squareHost, "Exported DOCX must keep the edited square image host.");
        Assert.IsTrue(squareHost!.Elements(Wp + "wrapTopAndBottom").Any(), "Changed wrap mode must be exported as wp:wrapTopAndBottom.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Phase39_ImportedCropInspectorAndWrapChange_ExportsUpdatedDrawingMl));
    }

    [TestMethod]
    public async Task Phase39_ImportedImageResizeUndo_RestoresSizeWithOneUndoStep()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);
        var fixturePath = await DownloadImageParityFixtureViaDemoEndpointAsync();
        await ImportDocxThroughToolbarAsync(page, fixturePath);

        var image = await WaitForDrawingByAltTextAsync(page, "Left wrapped recovery image");
        var beforeWidth = image.Width;
        var beforeHeight = image.Height;

        await DragImageResizeHandleAsync(page, image.ObjectId, 48, 28);
        var resized = await WaitForDrawingByObjectIdAsync(
            page,
            image.ObjectId,
            run => Math.Abs(run.Width - beforeWidth) > 6 || Math.Abs(run.Height - beforeHeight) > 6,
            "imported image resize");

        Assert.IsTrue(resized.Width > beforeWidth || resized.Height > beforeHeight,
            $"Resize must change the imported image size. Before={beforeWidth:0.##}x{beforeHeight:0.##}, after={resized.Width:0.##}x{resized.Height:0.##}");

        await page.Keyboard.PressAsync("Control+Z");
        var afterUndo = await WaitForDrawingByObjectIdAsync(
            page,
            image.ObjectId,
            run => Math.Abs(run.Width - beforeWidth) <= 1.5 && Math.Abs(run.Height - beforeHeight) <= 1.5,
            "single undo after imported image resize");

        Assert.AreEqual(beforeWidth, afterUndo.Width, 1.5, "One undo step must restore imported image width.");
        Assert.AreEqual(beforeHeight, afterUndo.Height, 1.5, "One undo step must restore imported image height.");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Phase39_ImportedImageResizeUndo_RestoresSizeWithOneUndoStep));
    }

    private static async Task<string> DownloadImageParityFixtureViaDemoEndpointAsync()
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        var content = await http.GetByteArrayAsync("api/document-editor/image-parity/export-docx");
        Assert.IsTrue(content.Length >= 500, "Image parity DOCX fixture endpoint must return a non-empty DOCX payload.");
        var path = Path.Combine(Path.GetTempPath(), $"tempo-phase39-image-parity-{Guid.NewGuid():N}.docx");
        await File.WriteAllBytesAsync(path, content);
        return path;
    }

    private static async Task ImportDocxThroughToolbarAsync(IPage page, string docxPath)
    {
        await page.GetByTestId("document-ribbon-tab-references").ClickAsync();
        await page.GetByTestId("document-import-docx-label").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-import-docx-panel")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await page.GetByTestId("document-import-docx").SetInputFilesAsync(docxPath);
        await Assertions.Expect(page.GetByTestId("document-format-message")).ToContainTextAsync(new Regex("Imported|Importováno"), new() { Timeout = 15000 });
        await WaitForEditorStableAsync(page, "DOCX import through toolbar", timeoutMs: 15000);
    }

    private static async Task<string> ExportDocxThroughToolbarAsync(IPage page)
    {
        await page.GetByTestId("document-ribbon-tab-references").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-export-docx")).ToBeEnabledAsync(new() { Timeout = 10000 });
        var download = await page.RunAndWaitForDownloadAsync(
            async () => await page.GetByTestId("document-export-docx").ClickAsync());
        await Assertions.Expect(page.GetByTestId("document-format-message")).ToContainTextAsync(new Regex("DOCX exported|Exportováno"), new() { Timeout = 10000 });
        return await AssertDownloadedFileAsync(download, ".docx", 500, "DOCX export");
    }

    private static async Task<string> AssertDownloadedFileAsync(IDownload download, string expectedExtension, long minBytes, string label)
    {
        var path = await download.PathAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(path), $"{label} must provide a downloaded file path.");
        Assert.IsTrue(File.Exists(path), $"{label} must exist at '{path}'.");
        Assert.IsTrue(new FileInfo(path).Length >= minBytes, $"{label} must contain at least {minBytes} bytes.");
        var extension = Path.GetExtension(download.SuggestedFilename);
        Assert.AreEqual(expectedExtension, extension, ignoreCase: true, $"{label} suggested filename should use {expectedExtension}.");
        return path!;
    }

    private static async Task<DocumentEditorDrawingRunProbe> WaitForDrawingByAltTextAsync(IPage page, string altText)
    {
        return await WaitForDrawingAsync(
            page,
            run => string.Equals(run.AltText, altText, StringComparison.Ordinal),
            $"drawing with alt text '{altText}'");
    }

    private static async Task<DocumentEditorDrawingRunProbe> WaitForDrawingByObjectIdAsync(
        IPage page,
        string objectId,
        Func<DocumentEditorDrawingRunProbe, bool> predicate,
        string description)
    {
        return await WaitForDrawingAsync(
            page,
            run => string.Equals(run.ObjectId, objectId, StringComparison.Ordinal) && predicate(run),
            description);
    }

    private static async Task<DocumentEditorDrawingRunProbe> WaitForDrawingAsync(
        IPage page,
        Func<DocumentEditorDrawingRunProbe, bool> predicate,
        string description)
    {
        DocumentEditorDrawingRunProbe[] latest = [];
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            latest = await ReadDocumentEditorDrawingRunsAsync(page);
            var match = latest.FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            await page.WaitForTimeoutAsync(150);
        }

        Assert.Fail($"Timed out waiting for {description}. Latest drawings: {string.Join(", ", latest.Select(run => $"{run.AltText}/{run.ObjectId}/{run.WrapMode}/{run.Region}/{run.Width:0.##}x{run.Height:0.##}"))}");
        return new DocumentEditorDrawingRunProbe();
    }

    private static async Task OpenDocumentJsonDebugAsync(IPage page)
    {
        await page.GetByTestId("document-ribbon-tab-view").ClickAsync();
        await page.GetByTestId("document-view-json").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-json-debug-modal")).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    private static async Task EnsureImageInspectorVisibleAsync(IPage page)
    {
        var panel = page.GetByTestId("document-image-properties-panel");
        if (await panel.CountAsync() == 0 || !await panel.IsVisibleAsync())
        {
            var moreOptions = page.GetByTestId("document-image-more-options");
            if (await moreOptions.CountAsync() > 0 && await moreOptions.IsVisibleAsync())
            {
                await moreOptions.ClickAsync();
            }

            var propertiesTab = page.GetByTestId("document-side-panel-tab-properties");
            if (await propertiesTab.CountAsync() > 0)
            {
                await propertiesTab.ClickAsync();
            }
        }

        await Assertions.Expect(panel).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    private static async Task ClickBeforeImageAsync(IPage page, string imageId)
    {
        await ScrollImageIntoViewAsync(page, imageId);
        var rect = await ReadDocumentEditorImageRectAsync(page, imageId);
        await page.Mouse.ClickAsync((float)Math.Max(1, rect.X - 4), (float)(rect.Y + rect.Height / 2));
    }

    private static async Task ClickImageCenterAsync(IPage page, string imageId)
    {
        await ScrollImageIntoViewAsync(page, imageId);
        var rect = await ReadDocumentEditorImageRectAsync(page, imageId);
        await page.Mouse.ClickAsync((float)(rect.X + rect.Width / 2), (float)(rect.Y + rect.Height / 2));
    }

    private static async Task DragImageResizeHandleAsync(IPage page, string imageId, double deltaX, double deltaY)
    {
        await ClickImageCenterAsync(page, imageId);
        await page.WaitForTimeoutAsync(50);
        var handle = await page.EvaluateAsync<DocumentEditorPointProbe>(
            """
            imageId => {
                const escaped = CSS.escape(imageId);
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const figure = host?.querySelector(`[data-object-id="${escaped}"], [data-block-id="${escaped}"]`);
                if (!figure) throw new Error(`Could not find image '${imageId}'.`);
                const overlay = host?.querySelector(`[data-testid="document-wysiwyg-object-selection-overlay"][data-object-id="${escaped}"]`);
                const handle = overlay?.querySelector?.('[data-resize-handle="se"], [data-testid$="resize-handle-se"], .tm-wysiwyg-object-resize-handle--se')
                    || figure.querySelector('[data-resize-handle="se"], [data-testid$="resize-handle-se"], .tm-wysiwyg-object-resize-handle--se, .tm-wysiwyg-image__resize-handle')
                    || figure;
                const rect = handle.getBoundingClientRect();
                return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
            }
            """,
            imageId);

        await page.Mouse.MoveAsync((float)handle.X, (float)handle.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(handle.X + deltaX), (float)(handle.Y + deltaY), new() { Steps = 10 });
        await page.Mouse.UpAsync();
    }

    private static async Task ScrollImageIntoViewAsync(IPage page, string imageId)
    {
        await page.EvaluateAsync(
            """
            imageId => {
                const escaped = CSS.escape(imageId);
                const image = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-testid="document-wysiwyg-object-layer-item"][data-object-id="${escaped}"]`)
                    || document.querySelector(`[data-testid="document-wysiwyg-host"] [data-object-id="${escaped}"]`)
                    || document.querySelector(`[data-testid="document-wysiwyg-host"] [data-block-id="${escaped}"]`);
                if (!image) throw new Error(`Could not find image '${imageId}' to scroll into view.`);
                image.scrollIntoView({ block: 'center', inline: 'center', behavior: 'instant' });
            }
            """,
            imageId);
        await page.WaitForTimeoutAsync(100);
    }

    private static XDocument ReadDocxXml(string path, string entryName)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry(entryName);
        Assert.IsNotNull(entry, $"DOCX package must contain '{entryName}'.");
        using var stream = entry!.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static XDocument ReadFirstHeaderXml(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.Entries
            .Where(candidate => candidate.FullName.StartsWith("word/header", StringComparison.OrdinalIgnoreCase)
                && candidate.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        Assert.IsNotNull(entry, "DOCX package must contain a header XML part.");
        using var stream = entry!.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static void AssertDocxTextContains(XDocument xml, string expected, string context)
    {
        var text = string.Concat(xml.Descendants(W + "t").Select(node => node.Value));
        Assert.IsTrue(text.Contains(expected, StringComparison.Ordinal), $"Expected {context} to contain '{expected}'. Text: {text}");
    }

    private static XElement? FindDrawingHostByAltText(XDocument xml, string altText)
    {
        return xml.Descendants()
            .Where(element => element.Name == Wp + "inline" || element.Name == Wp + "anchor")
            .FirstOrDefault(element => string.Equals((string?)element.Element(Wp + "docPr")?.Attribute("descr"), altText, StringComparison.Ordinal));
    }
}
