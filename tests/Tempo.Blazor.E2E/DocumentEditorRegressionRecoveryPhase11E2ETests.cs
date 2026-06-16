using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for canonical demo data and reset behavior.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase11E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DefaultContractDemo_ContainsNamedRepresentativeSectionsWithoutDebugFiller()
    {
        using var document = await LoadDocumentJsonAsync("contract-demo");
        var root = Snapshot(document);
        var blocks = Array(root, "Blocks").ToArray();
        var headersFooters = Array(root, "HeadersFooters").ToArray();
        var plain = ExtractPlainText(blocks);

        HasNestedBlock(headersFooters, "contract-header-primary-block").Should().BeTrue("the default demo must include a header/footer sample");
        HasNestedBlock(headersFooters, "contract-footer-primary-block").Should().BeTrue("the default demo must include a header/footer sample");
        HasBlock(blocks, "contract-intro").Should().BeTrue("the default demo must include a comments sample");
        HasBlock(blocks, "contract-scope").Should().BeTrue("the default demo must include a revisions sample");
        HasBlock(blocks, "contract-left-wrap-image").Should().BeTrue("the default demo must include an image wrapping sample");
        HasBlock(blocks, "contract-pricing-table").Should().BeTrue("the default demo must include a table sample");

        plain.Should().Contain("realistic contract text");
        plain.Should().Contain("left positioned evidence preview");
        plain.Should().Contain("opposite wrap direction");
        plain.Should().NotContain("ffff");
        plain.Should().NotContain("dddd");

        var comments = Array(root, "Comments").ToArray();
        comments.Should().Contain(comment => String(comment, "Id") == "contract-comment-client-token");
        var comment = comments.First(item => String(item, "Id") == "contract-comment-client-token");
        StringValue(Required(Required(comment, "Anchor"), "BlockId")).Should().Be("contract-intro");
        plain.Should().Contain("This agreement is made with", "the comment anchor must point to visible text");

        var revisions = Array(root, "Revisions").ToArray();
        revisions.Should().Contain(revision => String(revision, "Id") == "contract-revision-scope");
        revisions.Should().Contain(revision => String(revision, "Id") == "contract-revision-deletion");
        plain.Should().Contain("Priority support is included");
        plain.Should().Contain("Legacy onboarding language will be removed");
    }

    [TestMethod]
    public async Task DefaultContractDemo_ImageSourcesAndWrapModesStayHonest()
    {
        using var document = await LoadDocumentJsonAsync("contract-demo");
        var blocks = Array(Snapshot(document), "Blocks").ToArray();

        var urlImage = Content(FindBlock(blocks, "contract-left-wrap-image"));
        String(urlImage, "Url").Should().Be("/document-editor-evidence.svg");
        String(urlImage, "Url").Should().NotStartWith("data:");
        StringValue(Required(urlImage, "Source")).Should().MatchRegex("Url|0");
        String(Required(Required(urlImage, "Layout"), "Wrap"), "Mode").Should().MatchRegex("Square|1");

        var providerImage = Content(FindBlock(blocks, "contract-top-bottom-image"));
        String(providerImage, "Url").Should().BeNull();
        String(providerImage, "AssetId").Should().Be("contract-evidence-asset");
        StringValue(Required(providerImage, "Source")).Should().MatchRegex("Asset|1");
        String(Required(Required(providerImage, "Layout"), "Wrap"), "Mode").Should().MatchRegex("TopBottom|4");

        var inlineImage = Content(FindBlock(blocks, "contract-inline-image"));
        String(Required(Required(inlineImage, "Layout"), "Wrap"), "Mode").Should().MatchRegex("Inline|0");
    }

    [TestMethod]
    public async Task DefaultContractDemo_ReloadAndInspectorDoNotMaskProviderVsUrlImages()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await Assertions.Expect(page.GetByTestId("document-page-header").First).ToContainTextAsync("Tempo Legal - Service agreement");
        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForDocumentEditorReadyAsync(page);
        await Assertions.Expect(page.GetByTestId("document-page-header").First).ToContainTextAsync("Tempo Legal - Service agreement");

        await ClickImageBlockAsync(page, "contract-left-wrap-image");
        await Assertions.Expect(page.GetByTestId("document-image-inspector-link")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-image-inspector-link")).ToHaveValueAsync("/document-editor-evidence.svg");

        await ClickImageBlockAsync(page, "contract-top-bottom-image");
        await Assertions.Expect(page.GetByTestId("document-image-inspector-link")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-image-inspector-asset-info")).ToContainTextAsync("contract-evidence-asset");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(DefaultContractDemo_ReloadAndInspectorDoNotMaskProviderVsUrlImages));
    }

    private static async Task ClickImageBlockAsync(IPage page, string blockId)
    {
        var image = page.Locator($"[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id='{blockId}']").First;
        await image.ScrollIntoViewIfNeededAsync();
        await Assertions.Expect(image).ToBeVisibleAsync(new() { Timeout = 5000 });
        var selected = await page.EvaluateAsync<bool>(
            """
            blockId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"][data-instance-id]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const runtime = window.tmDocumentEditorEngine;
                const hooks = runtime?.__testHooks || runtime?.accessibility || {};
                const instances = hooks._instances || hooks.instances || runtime?.__testHooks?._instances;
                const instance = instances?.get?.(instanceId);
                return hooks.selectObjectById?.(instance, blockId, blockId, 'e2e-click-image-block') === true
                    || runtime?.accessibility?.selectObjectById?.(instance, blockId, blockId, 'e2e-click-image-block') === true;
            }
            """,
            blockId);
        selected.Should().BeTrue("the image object must be selectable by stable runtime object id");
        await Assertions.Expect(page.GetByTestId("document-image-inspector")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-image-properties-panel"))
            .ToHaveAttributeAsync("data-active-object-id", blockId, new() { Timeout = 5000 });
    }

    private static async Task<JsonDocument> LoadDocumentJsonAsync(string documentId)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5100") };
        var result = await http.GetFromJsonAsync<JsonElement>($"api/document-editor/documents/{documentId}");
        return JsonDocument.Parse(String(result, "JsonSnapshot") ?? "{}");
    }

    private static JsonElement Snapshot(JsonDocument document) => document.RootElement;

    private static bool HasBlock(IEnumerable<JsonElement> blocks, string id)
        => blocks.Any(block => String(block, "Id") == id);

    private static bool HasNestedBlock(IEnumerable<JsonElement> containers, string id)
        => containers.Any(container => Array(container, "Blocks").Any(block => String(block, "Id") == id));

    private static JsonElement FindBlock(IEnumerable<JsonElement> blocks, string id)
        => blocks.First(block => String(block, "Id") == id);

    private static JsonElement Content(JsonElement block) => Required(block, "Content");

    private static IEnumerable<JsonElement> Array(JsonElement element, string propertyName)
        => TryGet(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    private static JsonElement Required(JsonElement element, string propertyName)
        => TryGet(element, propertyName, out var value)
            ? value
            : throw new AssertFailedException($"Missing JSON property '{propertyName}'.");

    private static string? String(JsonElement element, string propertyName)
        => TryGet(element, propertyName, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            }
            : null;

    private static string? StringValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

    private static bool TryGet(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        var camel = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(camel, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string ExtractPlainText(IEnumerable<JsonElement> blocks)
        => string.Join(" ", blocks.SelectMany(ExtractBlockText));

    private static IEnumerable<string> ExtractBlockText(JsonElement block)
    {
        if (!TryGet(block, "Content", out var content))
        {
            yield break;
        }

        if (TryGet(content, "Inlines", out var inlines) && inlines.ValueKind == JsonValueKind.Array)
        {
            foreach (var inline in inlines.EnumerateArray())
            {
                var text = String(inline, "Text") ?? String(inline, "DisplayName") ?? String(inline, "FallbackText");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }
            }
        }

        if (TryGet(content, "Rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rows.EnumerateArray())
            foreach (var cell in Array(row, "Cells"))
            foreach (var nested in Array(cell, "Blocks"))
            foreach (var text in ExtractBlockText(nested))
            {
                yield return text;
            }
        }
    }
}
