using System.Linq;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// R.4.8 cutover bridge — verifies the new model-owned engine running inside Blazor via
/// <c>TmDocumentCoreEngineHost</c> on the <c>/core-engine-host</c> demo page: the C# document
/// converts → renders as real positioned-DOM text, real keystrokes edit it, and the model
/// round-trips back to C# (<c>RequestDocumentAsync</c>). This is the live Blazor↔JS path the
/// cutover depends on (the harness tests drive the engine directly; this drives it through
/// the actual component lifecycle + IJSRuntime).
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CoreEngineHostBridgeE2ETests : WasmTestBase
{
    private async Task<IPage> OpenHostPageAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await page.GotoAsync($"{BaseUrl}/core-engine-host", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
                await WaitForCoreEngineHostReadyAsync(page);
                return page;
            }
            catch (TimeoutException) when (attempt == 0)
            {
                await TryResetCoreEngineNavigationAsync(page);
            }
        }

        return page;
    }

    private static Task WaitForCoreEngineHostReadyAsync(IPage page)
        => page.WaitForFunctionAsync(
            "() => { const el = document.querySelector('[data-testid=\"document-core-engine-host\"]'); return el && el.getAttribute('data-core-engine-ready') === 'true'; }",
            null,
            new PageWaitForFunctionOptions { Timeout = 45000 });

    private static async Task TryResetCoreEngineNavigationAsync(IPage page)
    {
        try
        {
            await page.GotoAsync("about:blank", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            // The retry below performs the authoritative core-engine readiness check.
        }
    }

    private static async Task WaitForCoreEngineEditorReadyAsync(IPage page)
    {
        const string readyExpression =
            "() => { const el = document.querySelector('[data-testid=\"document-core-engine-host\"]'); return el && el.getAttribute('data-core-engine-ready') === 'true'; }";

        try
        {
            await page.WaitForFunctionAsync(readyExpression, null, new PageWaitForFunctionOptions { Timeout = 45000 });
        }
        catch (TimeoutException)
        {
            await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
            await page.WaitForFunctionAsync(readyExpression, null, new PageWaitForFunctionOptions { Timeout = 45000 });
        }
    }

    [TestMethod]
    public async Task R49_CoreEngineHost_RendersConvertedDocument_TypesAndRoundTripsThroughBlazor()
    {
        var page = await OpenHostPageAsync();

        // The C# document converted + rendered as real positioned-DOM text.
        var renderedText = await page.EvaluateAsync<string>(@"() => {
            const host = document.querySelector('[data-testid=""document-core-engine-host""]');
            return Array.from(host.querySelectorAll('[data-render-block-id]')).map((b) => b.textContent).join(' | ');
        }");
        renderedText.Should().Contain("Bridge Demo", "the heading converts + renders via the core engine");
        renderedText.Should().Contain("Hello world", "the paragraph converts + renders via the core engine");

        // Heading semantics survived the bridge (role=heading from the converted level).
        var hasHeading = await page.EvaluateAsync<bool>(@"() => !!document.querySelector('[data-testid=""document-core-engine-host""] [role=""heading""]')");
        hasHeading.Should().BeTrue("the converted heading exposes role=heading");

        // Click into the paragraph (focuses the off-screen input + places the caret), go to
        // line end, type with the REAL keyboard.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + paraBox.Width / 2, paraBox.Y + paraBox.Height / 2);
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync("!");

        var domAfterType = await page.EvaluateAsync<string>(@"() => {
            const host = document.querySelector('[data-testid=""document-core-engine-host""]');
            const p = host.querySelector('[data-render-block-id=""p1""]');
            return p ? p.textContent : '';
        }");
        domAfterType.Should().Be("Hello world!", "real typing flows through the bridge into the rendered DOM");

        // Round-trip the live model back to C# and display it.
        await page.Locator("[data-testid='cmd-roundtrip']").ClickAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-testid=\"roundtrip-output\"]').textContent.indexOf('Hello world!') !== -1",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });
        var roundTrip = await page.Locator("[data-testid='roundtrip-output']").TextContentAsync();
        roundTrip.Should().Be("Hello world!", "the engine model round-trips back to the C# document");
        var dirty = await page.Locator("[data-testid='dirty-state']").TextContentAsync();
        dirty.Should().Be("true", "the bridge reports the document as dirty after the edit");

        TestContext.WriteLine($"R.4.8 host bridge: rendered '{renderedText}', typed → '{domAfterType}', round-trip → '{roundTrip}', dirty={dirty}");
    }

    [TestMethod]
    public async Task R50_TmDocumentEditor_WithCoreEnginePreviewFlag_HostsTheNewEngine()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });

        // The full TmDocumentEditor shell loads + mounts the core engine host (not legacy).
        await WaitForCoreEngineEditorReadyAsync(page);

        var json = await page.EvaluateAsync<string>(@"() => {
            const editorRoot = document.querySelector('[data-render-engine]');
            const host = document.querySelector('[data-testid=""document-core-engine-host""]');
            const legacy = document.querySelector('[data-testid=""document-wysiwyg-host""]');
            const text = host ? Array.from(host.querySelectorAll('[data-render-block-id]')).map((b) => b.textContent).join(' | ') : '';
            return JSON.stringify({
                renderEngine: editorRoot ? editorRoot.getAttribute('data-render-engine') : null,
                requested: editorRoot ? editorRoot.getAttribute('data-render-engine-requested') : null,
                hasCoreHost: !!host,
                hasLegacyHost: !!legacy,
                text,
            });
        }");
        using var r = System.Text.Json.JsonDocument.Parse(json);
        var root = r.RootElement;
        root.GetProperty("renderEngine").GetString().Should().Be("CoreEnginePreview", "the editor resolves to the core engine when opted in");
        root.GetProperty("requested").GetString().Should().Be("CoreEnginePreview");
        root.GetProperty("hasCoreHost").GetBoolean().Should().BeTrue("the new model-owned host renders inside TmDocumentEditor");
        root.GetProperty("hasLegacyHost").GetBoolean().Should().BeFalse("the legacy contenteditable host is NOT rendered in preview");
        root.GetProperty("text").GetString().Should().Contain("Preview Heading", "the document renders through the core engine");
        root.GetProperty("text").GetString().Should().Contain("Edited by the core engine", "the paragraph renders through the core engine");

        TestContext.WriteLine($"R.4.8 editor preview: render-engine={root.GetProperty("renderEngine").GetString()}, core host hosts '{root.GetProperty("text").GetString()}'");
    }

    [TestMethod]
    public async Task R51_TmDocumentEditor_ToolbarBoldAndUndo_RouteToCoreEngine()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Click into the paragraph (focus + caret), select the whole line with the keyboard.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 8, paraBox.Y + paraBox.Height / 2);
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("Shift+End");

        // Click the editor TOOLBAR bold button → must route to the core engine host.
        await page.Locator("[data-testid='document-bold']").First.ClickAsync();
        var weightAfterBold = await page.EvaluateAsync<string>(@"() => {
            const host = document.querySelector('[data-testid=""document-core-engine-host""]');
            const seg = Array.from(host.querySelectorAll('.tm-render-segment')).find((s) => /Edited/.test(s.textContent || ''));
            return seg ? getComputedStyle(seg).fontWeight : 'none';
        }");
        weightAfterBold.Should().BeOneOf("700", "bold", "the toolbar Bold button applies bold via the core engine");

        // Toolbar Undo button → reverts through the core engine's history.
        await page.Locator("[data-testid='document-undo'], [data-testid='document-editor-undo']").First.ClickAsync();
        var weightAfterUndo = await page.EvaluateAsync<string>(@"() => {
            const host = document.querySelector('[data-testid=""document-core-engine-host""]');
            const seg = Array.from(host.querySelectorAll('.tm-render-segment')).find((s) => /Edited/.test(s.textContent || ''));
            return seg ? getComputedStyle(seg).fontWeight : 'none';
        }");
        weightAfterUndo.Should().BeOneOf("400", "normal", "the toolbar Undo button reverts bold via the core engine");

        TestContext.WriteLine($"R.4.8 toolbar routing: bold→{weightAfterBold}, undo→{weightAfterUndo}");
    }

    [TestMethod]
    public async Task R52_TmDocumentEditor_ToolbarSave_PersistsCoreEngineEdits()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Edit through the core engine: click into p1, go to end, type with the real keyboard.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + paraBox.Width / 2, paraBox.Y + paraBox.Height / 2);
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync("!");

        // Click the editor TOOLBAR save button → SaveAsync pulls the live model from the core
        // engine and persists it (the demo surfaces the saved document text).
        await page.Locator("[data-testid='document-save']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-testid=\"saved-output\"]').textContent.indexOf('Edited by the core engine.!') !== -1",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });
        var saved = await page.Locator("[data-testid='saved-output']").TextContentAsync();
        saved.Should().Be("saved:Edited by the core engine.!", "the toolbar Save persisted the core engine's live edits");

        TestContext.WriteLine($"R.4.8 toolbar save: persisted '{saved}'");
    }

    [TestMethod]
    public async Task R53_Toolbar_ReflectsCoreEngineFormatting_AfterBoldCommand()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Select the whole paragraph line through the core engine.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 8, paraBox.Y + paraBox.Height / 2);
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("Shift+End");

        // The toolbar Bold button starts un-pressed (selection is not bold).
        var pressedBefore = await page.Locator("[data-testid='document-bold']").First.GetAttributeAsync("aria-pressed");
        pressedBefore.Should().NotBe("true", "the selection is not bold before the command");

        // Click toolbar Bold → the engine applies it AND the toolbar reads the engine's
        // formatting state back (SyncCoreEngineStateAsync → _formattingState.Bold → aria-pressed).
        await page.Locator("[data-testid='document-bold']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            "() => { const b = document.querySelector('[data-testid=\"document-bold\"]'); return b && b.getAttribute('aria-pressed') === 'true'; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        var pressedAfter = await page.Locator("[data-testid='document-bold']").First.GetAttributeAsync("aria-pressed");
        pressedAfter.Should().Be("true", "the toolbar reflects the core engine's active Bold formatting");

        TestContext.WriteLine("R.4.8 toolbar read-back: Bold aria-pressed flips true after the core engine applies bold");
    }

    [TestMethod]
    public async Task R54_Toolbar_NumberedList_RoutesToCoreEngine_RendersMarkersAndContinuesOnEnter()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Click into the paragraph and select the whole line.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 8, paraBox.Y + paraBox.Height / 2);
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("Shift+End");

        // Toolbar → Numbered list. Routes to the core engine, which lays out a hanging marker.
        await page.Locator("[data-testid='document-numbered-list']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            "() => { const m = document.querySelector('[data-testid=\"document-core-engine-host\"] [data-list-marker]'); return m && m.textContent === '1.'; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        // Toolbar reflects the active numbered list (formatting read-back).
        var pressed = await page.Locator("[data-testid='document-numbered-list']").First.GetAttributeAsync("aria-pressed");
        pressed.Should().Be("true", "the toolbar reflects the core engine's active numbered list");

        // Enter continues the list: go to end, start a new item, type → a SECOND numbered marker.
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.TypeAsync("Second item");
        await page.WaitForFunctionAsync(
            "() => { const ms = Array.from(document.querySelectorAll('[data-testid=\"document-core-engine-host\"] [data-list-marker]')).map(m => m.textContent); return ms.length >= 2 && ms[0] === '1.' && ms[1] === '2.'; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        var markers = await page.EvaluateAsync<string[]>(
            "() => Array.from(document.querySelectorAll('[data-testid=\"document-core-engine-host\"] [data-list-marker]')).map(m => m.textContent)");
        markers.Should().Equal(new[] { "1.", "2." }, "Enter continues the numbered list and items renumber automatically");

        TestContext.WriteLine($"R.4.8 lists: numbered markers after toolbar + Enter = [{string.Join(", ", markers)}]");
    }

    [TestMethod]
    public async Task R55_Toolbar_AddComment_ComposesIntoCoreEngine_HighlightsRangeAndShowsInRail()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Select the paragraph line in the core engine.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 8, paraBox.Y + paraBox.Height / 2);
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("Shift+End");

        // The Add-comment button lives on the Review ribbon tab — switch to it first.
        await page.Locator("[data-testid='document-ribbon-tab-review']").First.ClickAsync();
        await page.Locator("[data-testid='document-add-comment']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Toolbar → Add comment → the composer opens in the comments rail.
        await page.Locator("[data-testid='document-add-comment']").First.ClickAsync();
        await page.Locator("[data-testid='document-comment-new-composer']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Compose + submit → routed to the core engine's addComment (not the provider).
        await page.Locator("[data-testid='document-comment-input']").FillAsync("Needs review");
        await page.Locator("[data-testid='document-comment-submit']").First.ClickAsync();

        // The engine highlights the commented range (comment anchor mark → bg #fff3a3).
        await page.WaitForFunctionAsync(
            "() => { const host = document.querySelector('[data-testid=\"document-core-engine-host\"]'); return Array.from(host.querySelectorAll('.tm-render-segment')).some(s => getComputedStyle(s).backgroundColor === 'rgb(255, 243, 163)'); }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        // The comment surfaces in the rail (read back from the engine's model.comments).
        var railText = await page.Locator("[data-testid='document-comment-list']").TextContentAsync();
        railText.Should().Contain("Needs review", "the composed comment shows in the rail, read back from the core engine");

        TestContext.WriteLine("R.4.8 comments: composed comment highlighted the range + appears in the rail");
    }

    [TestMethod]
    public async Task R56_Toolbar_InsertImageUrl_ComposesIntoCoreEngine_RendersFloatingImage()
    {
        const string imageUrl = "https://example.com/picture.png";
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Place the caret in the paragraph (the image anchors at the engine's live caret).
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 20, paraBox.Y + paraBox.Height / 2);

        // The image button is on the Insert ribbon tab.
        await page.Locator("[data-testid='document-ribbon-tab-insert']").First.ClickAsync();
        await page.Locator("[data-testid='document-toolbar-image']").First.ClickAsync();
        await page.Locator("[data-testid='document-image-insert-url']").First.ClickAsync();

        // The core engine's Blazor image-URL dialog opens; enter a URL + insert.
        await page.Locator("[data-testid='document-core-image-dialog']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await page.Locator("[data-testid='document-core-image-url']").FillAsync(imageUrl);
        await page.Locator("[data-testid='document-core-image-insert']").First.ClickAsync();

        // The engine inserts a floating drawing — rendered as <figure data-object-id><img src=…>.
        await page.WaitForFunctionAsync(
            "(url) => { const host = document.querySelector('[data-testid=\"document-core-engine-host\"]'); return !!host && Array.from(host.querySelectorAll('img')).some(i => i.getAttribute('src') === url); }",
            imageUrl, new PageWaitForFunctionOptions { Timeout = 10000 });

        var figureCount = await page.Locator("[data-testid='document-core-engine-host'] figure[data-object-id]").CountAsync();
        figureCount.Should().BeGreaterThan(0, "the core engine renders the inserted image as a floating object");

        TestContext.WriteLine($"R.4.8 images: dialog → core engine inserted a floating image ({figureCount} figure[s])");
    }

    [TestMethod]
    public async Task R57_ImageDialog_Upload_ReadsFileAsDataUrl_InsertsIntoCoreEngine()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Place the caret, open the image dialog (URL menu item also exposes the file picker).
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 20, paraBox.Y + paraBox.Height / 2);
        await page.Locator("[data-testid='document-ribbon-tab-insert']").First.ClickAsync();
        await page.Locator("[data-testid='document-toolbar-image']").First.ClickAsync();
        await page.Locator("[data-testid='document-image-insert-url']").First.ClickAsync();
        await page.Locator("[data-testid='document-core-image-dialog']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Pick a tiny PNG → read client-side as a data URL → routed to engine.insertImage.
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
        await page.Locator("[data-testid='document-core-image-file']").SetInputFilesAsync(new FilePayload
        {
            Name = "dot.png",
            MimeType = "image/png",
            Buffer = png,
        });

        // The engine inserts the uploaded image (data: URL) as a floating object.
        await page.WaitForFunctionAsync(
            "() => { const host = document.querySelector('[data-testid=\"document-core-engine-host\"]'); return !!host && Array.from(host.querySelectorAll('img')).some(i => (i.getAttribute('src') || '').startsWith('data:image/png;base64,')); }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        var figureCount = await page.Locator("[data-testid='document-core-engine-host'] figure[data-object-id]").CountAsync();
        figureCount.Should().BeGreaterThan(0, "the uploaded image renders as a floating object in the core engine");

        TestContext.WriteLine("R.4.8 image upload: picked file → data URL → core engine inserted the image");
    }

    [TestMethod]
    public async Task R58_ImageInspector_SelectImage_PanelReflectsEngine_WrapModeRoundTrips()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Insert an image via the dialog.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 20, paraBox.Y + paraBox.Height / 2);
        await page.Locator("[data-testid='document-ribbon-tab-insert']").First.ClickAsync();
        await page.Locator("[data-testid='document-toolbar-image']").First.ClickAsync();
        await page.Locator("[data-testid='document-image-insert-url']").First.ClickAsync();
        await page.Locator("[data-testid='document-core-image-dialog']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await page.Locator("[data-testid='document-core-image-url']").FillAsync("https://example.com/inspect.png");
        await page.Locator("[data-testid='document-core-image-insert']").First.ClickAsync();

        var figure = page.Locator("[data-testid='document-core-engine-host'] figure[data-object-id]").First;
        await figure.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Click the image → JS→.NET object-selection event → the inspector panel appears.
        await figure.ClickAsync();
        await page.Locator("[data-testid='document-image-properties-panel']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        var activeObj = await page.Locator("[data-testid='document-image-properties-panel']").GetAttributeAsync("data-active-object-id");
        activeObj.Should().NotBeNullOrEmpty("the inspector reflects the core engine's selected object");

        // Change wrap mode → Square. Routes to the engine; the engine notifies back and the
        // inspector reflects the new wrap mode (full JS→.NET→engine→.NET round-trip).
        await page.Locator("[data-testid='document-image-inspector-wrap-square']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            "() => { const b = document.querySelector('[data-testid=\"document-image-inspector-wrap-square\"]'); return b && b.getAttribute('aria-pressed') === 'true'; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        TestContext.WriteLine("R.4.8 image inspector: select → panel reflects engine; wrap-mode round-trips JS→.NET→engine→.NET");
    }

    [TestMethod]
    public async Task R59_BlockStyle_HeadingDropdown_RoutesToCoreEngine_RendersHeading()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Place the caret in the paragraph, then pick Heading 1 from the block-style dropdown.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 20, paraBox.Y + paraBox.Height / 2);
        await page.Locator("[data-testid='document-block-style']").SelectOptionAsync("Heading1");

        // The engine applies the named style → the paragraph renders at the Heading 1 size (32px).
        await page.WaitForFunctionAsync(
            "() => { const host = document.querySelector('[data-testid=\"document-core-engine-host\"]'); const seg = host && host.querySelector('[data-render-block-id=\"p1\"] .tm-render-segment'); return seg && Math.round(parseFloat(getComputedStyle(seg).fontSize)) === 32; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        var value = await page.Locator("[data-testid='document-block-style']").InputValueAsync();
        value.Should().Be("Heading1", "the dropdown reflects the engine's paragraph style (read-back)");

        TestContext.WriteLine("R.4.8 headings: block-style dropdown → core engine setParagraphStyle (Heading 1 @32px)");
    }

    [TestMethod]
    public async Task R60_ImageAssetPicker_RoutesToCoreEngine_InsertsAssetImage()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 20, paraBox.Y + paraBox.Height / 2);

        // Insert tab → image menu → "from asset" inserts the demo's preset asset URL.
        await page.Locator("[data-testid='document-ribbon-tab-insert']").First.ClickAsync();
        await page.Locator("[data-testid='document-toolbar-image']").First.ClickAsync();
        await page.Locator("[data-testid='document-image-insert-asset']").First.ClickAsync();

        await page.WaitForFunctionAsync(
            "() => { const host = document.querySelector('[data-testid=\"document-core-engine-host\"]'); return !!host && Array.from(host.querySelectorAll('img')).some(i => i.getAttribute('src') === 'https://example.com/asset-cat.png'); }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        TestContext.WriteLine("R.4.8 asset picker: toolbar 'from asset' → core engine inserted the preset asset image");
    }

    [TestMethod]
    public async Task R61_ImageInspector_SizeEdit_RoutesToCoreEngine_ResizesObject()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Insert an image and select it → inspector.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 20, paraBox.Y + paraBox.Height / 2);
        await page.Locator("[data-testid='document-ribbon-tab-insert']").First.ClickAsync();
        await page.Locator("[data-testid='document-toolbar-image']").First.ClickAsync();
        await page.Locator("[data-testid='document-image-insert-url']").First.ClickAsync();
        await page.Locator("[data-testid='document-core-image-dialog']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await page.Locator("[data-testid='document-core-image-url']").FillAsync("https://example.com/sizeme.png");
        await page.Locator("[data-testid='document-core-image-insert']").First.ClickAsync();
        var figure = page.Locator("[data-testid='document-core-engine-host'] figure[data-object-id]").First;
        await figure.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await figure.ClickAsync();
        await page.Locator("[data-testid='document-image-properties-panel']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Set the width via the inspector → routes to engine resize. Works for an INLINE image too
        // (the painted object is sized from the model width). Tab blurs the field so @onchange fires.
        await page.Locator("[data-testid='document-image-inspector-width']").FillAsync("360");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForFunctionAsync(
            "() => { const f = document.querySelector('[data-testid=\"document-core-engine-host\"] figure[data-object-id]'); if (!f) return false; const w = parseFloat(f.style.width || '0'); return Math.abs(w - 360) < 5; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        TestContext.WriteLine("R.4.8 inspector size: width edit → core engine resize → inline image figure ~360px");
    }

    [TestMethod]
    public async Task R62_FindBar_RoutesToCoreEngine_HighlightsAndReplaces()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Focus the editor (click into the text), then open find+replace (Ctrl+H).
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 20, paraBox.Y + paraBox.Height / 2);
        await page.Keyboard.PressAsync("Control+h");
        await page.Locator("[data-testid='document-find-panel']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Type a query → routes to the engine's find → it highlights matches in the core host.
        await page.Locator("[data-testid='document-find-input']").FillAsync("engine");
        await page.WaitForFunctionAsync(
            "() => !!document.querySelector('[data-testid=\"document-core-engine-host\"] .tm-core-find-highlight')",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        // Replace all → routes to the engine; the rendered text changes.
        await page.Locator("[data-testid='document-replace-input']").FillAsync("motor");
        await page.Locator("[data-testid='document-find-replace-all']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            "() => { const host = document.querySelector('[data-testid=\"document-core-engine-host\"]'); const p = host.querySelector('[data-render-block-id=\"p1\"]'); return p && p.textContent.indexOf('motor') !== -1 && p.textContent.indexOf('engine') === -1; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        TestContext.WriteLine("R.4.8 find bar: query → core engine highlight; replace-all → text changed via the engine");
    }

    [TestMethod]
    public async Task R63_ImageInspector_AlignmentAndZOrder_RouteToCoreEngine()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Insert a floating image (Square wrap) and select it.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 20, paraBox.Y + paraBox.Height / 2);
        await page.Locator("[data-testid='document-ribbon-tab-insert']").First.ClickAsync();
        await page.Locator("[data-testid='document-toolbar-image']").First.ClickAsync();
        await page.Locator("[data-testid='document-image-insert-url']").First.ClickAsync();
        await page.Locator("[data-testid='document-core-image-dialog']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await page.Locator("[data-testid='document-core-image-url']").FillAsync("https://example.com/zalign.png");
        await page.Locator("[data-testid='document-core-image-insert']").First.ClickAsync();
        var figure = page.Locator("[data-testid='document-core-engine-host'] figure[data-object-id]").First;
        await figure.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await figure.ClickAsync();
        await page.Locator("[data-testid='document-image-properties-panel']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await page.Locator("[data-testid='document-image-inspector-wrap-square']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            "() => { const b = document.querySelector('[data-testid=\"document-image-inspector-wrap-square\"]'); return b && b.getAttribute('aria-pressed') === 'true'; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        // Z-order: bring forward → the figure's z-index rises above the default (5 → 6).
        // (The order section is a collapsed <details> — expand it first.)
        await page.Locator("[data-testid='document-image-inspector-section-order'] summary").First.ClickAsync();
        await page.Locator("[data-testid='document-image-inspector-bring-forward']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            "() => { const f = document.querySelector('[data-testid=\"document-core-engine-host\"] figure[data-object-id]'); return f && f.style.zIndex === '6'; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        // Alignment: centre the floating image → its x moves right of the left margin.
        var leftBefore = await page.EvaluateAsync<double>(
            "() => { const f = document.querySelector('[data-testid=\"document-core-engine-host\"] figure[data-object-id]'); return f ? parseFloat(f.style.left || '0') : 0; }");
        await page.Locator("[data-testid='document-image-inspector-align-center']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            $"() => {{ const f = document.querySelector('[data-testid=\"document-core-engine-host\"] figure[data-object-id]'); return f && parseFloat(f.style.left || '0') > {leftBefore.ToString(System.Globalization.CultureInfo.InvariantCulture)} + 20; }}",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        TestContext.WriteLine("R.4.8 inspector align/z-order: bring-forward → z-index 6; centre → figure shifts right (engine repositions)");
    }

    // R.4.8 follow-up — caption + absolute position through the real inspector.
    // Full JS→.NET→engine→.NET path: the inspector caption field routes to setObjectCaption
    // (engine renders a <figcaption>), and the position X field routes to setObjectPosition
    // (engine repositions the floating figure). Inline-resize is already covered by R61/R73.
    [TestMethod]
    public async Task R74_ImageInspector_CaptionAndPosition_RouteToCoreEngine()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Insert an image and select it → inspector.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 20, paraBox.Y + paraBox.Height / 2);
        await page.Locator("[data-testid='document-ribbon-tab-insert']").First.ClickAsync();
        await page.Locator("[data-testid='document-toolbar-image']").First.ClickAsync();
        await page.Locator("[data-testid='document-image-insert-url']").First.ClickAsync();
        await page.Locator("[data-testid='document-core-image-dialog']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await page.Locator("[data-testid='document-core-image-url']").FillAsync("https://example.com/caption.png");
        await page.Locator("[data-testid='document-core-image-insert']").First.ClickAsync();
        var figure = page.Locator("[data-testid='document-core-engine-host'] figure[data-object-id]").First;
        await figure.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await figure.ClickAsync();
        await page.Locator("[data-testid='document-image-properties-panel']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Caption: type a caption → routes to engine.setObjectCaption → renders a <figcaption>.
        await page.Locator("[data-testid='document-image-inspector-caption']").FillAsync("Figure A");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForFunctionAsync(
            "() => { const c = document.querySelector('[data-testid=\"document-core-engine-host\"] [data-testid=\"core-engine-object-caption\"]'); return c && c.textContent === 'Figure A'; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        // Make the image floating (Square) so an absolute offset visibly moves the figure.
        await page.Locator("[data-testid='document-image-inspector-wrap-square']").First.ClickAsync();
        await page.WaitForFunctionAsync(
            "() => { const b = document.querySelector('[data-testid=\"document-image-inspector-wrap-square\"]'); return b && b.getAttribute('aria-pressed') === 'true'; }",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        // Position: set X to a clearly larger offset → routes to engine.setObjectPosition →
        // the floating figure's left moves right (frame.x = bodyMargin + offset).
        var leftBefore = await page.EvaluateAsync<double>(
            "() => { const f = document.querySelector('[data-testid=\"document-core-engine-host\"] figure[data-object-id]'); return f ? parseFloat(f.style.left || '0') : 0; }");
        await page.Locator("[data-testid='document-image-inspector-position-x']").FillAsync("180");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForFunctionAsync(
            $"() => {{ const f = document.querySelector('[data-testid=\"document-core-engine-host\"] figure[data-object-id]'); return f && parseFloat(f.style.left || '0') > {leftBefore.ToString(System.Globalization.CultureInfo.InvariantCulture)} + 50; }}",
            null, new PageWaitForFunctionOptions { Timeout = 10000 });

        TestContext.WriteLine("R.4.8 inspector caption/position: caption → <figcaption> 'Figure A'; position-x 180 → floating figure shifts right (engine repositions)");
    }

    // R.5.3 — autosave end-to-end: typing into the core engine (no explicit Save) triggers a
    // debounced model-change event in JS → .NET → SaveAsync(AutoSave) → provider. The demo
    // surfaces the persisted text in [data-testid=saved-output], so we assert it updates on its own.
    [TestMethod]
    public async Task R80_Autosave_CoreEngineEdit_PersistsWithoutExplicitSave()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Click into the paragraph and type — without ever pressing Save.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 20, paraBox.Y + paraBox.Height / 2);
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync(" AUTOSAVED");

        // The debounced onChange (≈400ms) → autosave → provider → demo surfaces the saved text.
        await page.WaitForFunctionAsync(
            "() => { const el = document.querySelector('[data-testid=\"saved-output\"]'); return el && el.textContent.indexOf('AUTOSAVED') !== -1; }",
            null, new PageWaitForFunctionOptions { Timeout = 15000 });

        TestContext.WriteLine("R.5.3 autosave: core-engine typing → debounced onChange → SaveAsync(AutoSave) → provider persisted the edit (no explicit Save).");
    }

    [TestMethod]
    public async Task R81_ContextMenu_RightClick_ShowsCoreMenu_AndAddsComment()
    {
        // R.5.23a — a real right-click in the hosted editor raises the engine's onContextMenu,
        // which the C# editor surfaces as the core context menu; "Add comment" opens the composer.
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        await page.GotoAsync($"{BaseUrl}/core-engine-editor", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForCoreEngineEditorReadyAsync(page);

        // Select the paragraph (so comment/cut/copy are enabled), then right-click it.
        var paraBox = await page.Locator("[data-testid='document-core-engine-host'] [data-render-block-id='p1']").BoundingBoxAsync();
        paraBox.Should().NotBeNull();
        await page.Mouse.ClickAsync(paraBox!.X + 8, paraBox.Y + paraBox.Height / 2);
        await page.Keyboard.PressAsync("Home");
        await page.Keyboard.PressAsync("Shift+End");
        await page.Mouse.ClickAsync(paraBox.X + paraBox.Width / 2, paraBox.Y + paraBox.Height / 2,
            new MouseClickOptions { Button = MouseButton.Right });

        // The core context menu appears with the expected items.
        await page.WaitForSelectorAsync("[data-testid='document-core-context-menu']", new PageWaitForSelectorOptions { Timeout = 10000 });
        var menuItems = await page.EvaluateAsync<int>("() => document.querySelectorAll('[data-testid=\"document-core-context-menu\"] [role=\"menuitem\"]').length");
        menuItems.Should().BeGreaterThan(0, "the core context menu renders action items");
        var hasComment = await page.Locator("[data-testid='document-core-context-comment']").CountAsync();
        hasComment.Should().BeGreaterThan(0, "the menu offers Add comment over a selection");

        // Click "Add comment" → the comments panel/composer opens.
        await page.Locator("[data-testid='document-core-context-comment']").First.ClickAsync();
        // The menu dismisses after an action.
        var menuGone = await page.Locator("[data-testid='document-core-context-menu']").CountAsync();
        menuGone.Should().Be(0, "the context menu closes after choosing an action");

        TestContext.WriteLine($"R.5.23a context menu: right-click → core menu ({menuItems} items) → Add comment dismisses the menu.");
    }
}
