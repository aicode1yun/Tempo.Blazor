using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 22 document editor demo scenarios.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase22DemoDocsE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase22_DemoRoute_ExposesScenarioControlsAndToolbarModes()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await Assertions.Expect(page.GetByTestId("document-editor-demo-scenarios")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-toolbar-mode")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-disable-feature-images")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-disable-feature-tables")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-image-provider-enabled")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-table-scenario")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-review-scenario")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-paste-sample-button")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-autosave-error")).ToBeVisibleAsync();

        var toolbar = page.GetByTestId("document-toolbar");
        await Assertions.Expect(toolbar).ToHaveAttributeAsync("data-toolbar-mode", "Ribbon");
        await page.GetByTestId("document-editor-toolbar-mode").SelectOptionAsync("Compact");
        await Assertions.Expect(toolbar).ToHaveAttributeAsync("data-toolbar-mode", "Compact");
        await page.GetByTestId("document-editor-toolbar-mode").SelectOptionAsync("DistractionFree");
        await Assertions.Expect(toolbar).ToHaveAttributeAsync("data-toolbar-mode", "DistractionFree");
        await page.GetByTestId("document-editor-toolbar-mode").SelectOptionAsync("Ribbon");
        await Assertions.Expect(toolbar).ToHaveAttributeAsync("data-toolbar-mode", "Ribbon");
    }

    [TestMethod]
    public async Task Phase22_FeatureAndImageProviderToggles_UpdateEditorSurface()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await page.GetByTestId("document-ribbon-tab-insert").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-toolbar-image")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("document-toolbar-table")).ToBeVisibleAsync();

        await page.GetByTestId("document-editor-disable-feature-images").CheckAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-disabled-features")).ToContainTextAsync("image");
        await Assertions.Expect(page.GetByTestId("document-toolbar-image")).ToHaveCountAsync(0, new() { Timeout = 10000 });

        await page.GetByTestId("document-editor-disable-feature-tables").CheckAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-disabled-features")).ToContainTextAsync("table");
        await Assertions.Expect(page.GetByTestId("document-toolbar-table")).ToHaveCountAsync(0, new() { Timeout = 10000 });

        await page.GetByTestId("document-editor-disable-feature-images").UncheckAsync();
        await Assertions.Expect(page.GetByTestId("document-toolbar-image")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.GetByTestId("document-editor-image-provider-enabled").UncheckAsync();
        await page.GetByTestId("document-toolbar-image").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-image-insert-upload")).ToBeDisabledAsync();
    }

    [TestMethod]
    public async Task Phase22_TableReviewPasteAndAutosaveScenarios_AreUsable()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);

        await page.GetByTestId("document-editor-table-scenario").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-loaded"))
            .ToContainTextAsync("Table properties demo", new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] table").First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.Locator("[data-testid='document-wysiwyg-host'] table td").First.ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-table-toolbar-table-properties"))
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        await page.GetByTestId("document-editor-review-scenario").ClickAsync();
        await page.GetByTestId("document-side-panel-tab-comments").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-comment-thread").Filter(new() { HasText = "client token" }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.GetByTestId("document-side-panel-tab-revisions").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-revision-item"))
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        await page.GetByTestId("document-editor-paste-sample-button").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-editor-paste-sample-html"))
            .ToContainTextAsync("Paste report sample");

        await page.GetByTestId("document-editor-autosave-error").CheckAsync();
        await EditorTypeAsync(page, $" phase22-autosave-{DateTimeOffset.UtcNow:HHmmssfff}");
        await Assertions.Expect(page.GetByTestId("document-save-message"))
            .ToContainTextAsync(new Regex("Demo autosave provider failed", RegexOptions.IgnoreCase), new() { Timeout = 10000 });
    }
}
