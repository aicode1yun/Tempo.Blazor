using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for deterministic high-quality document editor demo data.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase19E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_DemoResetReturnsCanonicalQualityScenarios()
    {
        await DocumentEditorE2EReset.ResetAsync();
        var firstDocument = await LoadContractDocumentAsync();
        var firstVersions = await GetApiJsonAsync("api/document-editor/documents/contract-demo/versions");
        var firstComments = await GetApiJsonAsync("api/document-editor/documents/contract-demo/comments");

        await DocumentEditorE2EReset.ResetAsync();
        var secondDocument = await LoadContractDocumentAsync();
        var secondVersions = await GetApiJsonAsync("api/document-editor/documents/contract-demo/versions");
        var secondComments = await GetApiJsonAsync("api/document-editor/documents/contract-demo/comments");

        var firstSnapshot = GetString(firstDocument.RootElement, "JsonSnapshot");
        var secondSnapshot = GetString(secondDocument.RootElement, "JsonSnapshot");

        firstSnapshot.Should().Be(secondSnapshot, "reset must return the same canonical contract document snapshot");
        Canonicalize(firstVersions.RootElement).Should().Be(Canonicalize(secondVersions.RootElement), "demo versions must not contain approval-breaking random ids or timestamps");
        Canonicalize(firstComments.RootElement).Should().Be(Canonicalize(secondComments.RootElement), "demo comments must keep stable ids and timestamps");

        using var snapshot = JsonDocument.Parse(firstSnapshot);
        AssertContractDemoScenarios(snapshot.RootElement);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_DefaultDemoReloadIsReadableAndOverlapFree()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        using var console = BeginDocumentEditorConsoleCapture(page);

        await page.WaitForTimeoutAsync(250);
        var probes = await RunDocumentEditorActionWithFrameProbesAsync(
            page,
            "Default contract demo reload must be readable without text/image overlap",
            () => Task.CompletedTask);
        await AssertStrictFrameProbesCleanAsync(page, probes, "Default contract demo reload must be readable without text/image overlap");

        var probe = probes.Last();
        var normalizedDocumentText = NormalizeWhitespace(probe.DocumentText);
        normalizedDocumentText.Should().Contain("This paragraph demonstrates a left positioned evidence preview.");
        normalizedDocumentText.Should().Contain("This paragraph proves the opposite wrap direction.");
        normalizedDocumentText.Should().NotContain("Content overflows page");
        probe.ImageRectCount.Should().BeGreaterThanOrEqualTo(4);
        probe.TextTextOverlapCount.Should().Be(0);
        probe.TextImageOverlapCount.Should().Be(0);
        probe.TextCaptionOverlapCount.Should().Be(0);
        probe.SidePanelClippingCount.Should().Be(0);
        console.Errors.Should().BeEmpty();
    }

    private static void AssertContractDemoScenarios(JsonElement document)
    {
        var blocks = GetArray(document, "Blocks").ToArray();
        blocks.Should().Contain(block => GetString(block, "Id") == "contract-normal-overview", "demo must contain normal readable text");
        blocks.Should().Contain(block => GetString(block, "Id") == "contract-pricing-table" && IsEnum(GetRequired(block, "Type"), "Table", 4), "demo must contain a table");

        var intro = FindBlock(blocks, "contract-intro");
        IsEnum(GetRequired(GetRequired(intro, "ParagraphProperties"), "Alignment"), "Justify", 3)
            .Should().BeTrue("demo must contain a justified paragraph");

        AssertImageScenario(blocks, "contract-left-wrap-image", "Square", 1, "Left", 0, "contract-evidence-asset", requiresAlt: true);
        AssertImageScenario(blocks, "contract-right-wrap-image", "Square", 1, "Right", 2, "contract-evidence-asset", requiresAlt: true);
        AssertImageScenario(blocks, "contract-top-bottom-image", "TopBottom", 4, "Center", 1, "contract-evidence-asset", requiresAlt: true);
        AssertImageScenario(blocks, "contract-inline-image", "Inline", 0, null, null, "contract-evidence-asset", requiresAlt: true);
        AssertImageScenario(blocks, "contract-missing-alt-image", "Inline", 0, null, null, "contract-evidence-asset", requiresAlt: false);

        var revisions = GetArray(document, "Revisions").ToArray();
        revisions.Should().Contain(revision => GetString(revision, "Id") == "contract-revision-scope" && IsEnum(GetRequired(revision, "Type"), "Insertion", 0));
        revisions.Should().Contain(revision => GetString(revision, "Id") == "contract-revision-deletion" && IsEnum(GetRequired(revision, "Type"), "Deletion", 1));

        var comments = GetArray(document, "Comments").ToArray();
        comments.Should().Contain(comment => GetString(comment, "Id") == "contract-comment-client-token");

        var plainText = ExtractPlainText(blocks);
        plainText.Should().Contain("realistic contract text");
        plainText.Should().Contain("left positioned evidence preview");
        plainText.Should().Contain("opposite wrap direction");
        plainText.Should().NotContain("ffff", "demo must be curated and readable, not debugging filler");
        plainText.Should().NotContain("dddd", "demo must be curated and readable, not debugging filler");
    }

    private static void AssertImageScenario(
        JsonElement[] blocks,
        string blockId,
        string wrapName,
        int wrapValue,
        string? horizontalName,
        int? horizontalValue,
        string stableAssetId,
        bool requiresAlt)
    {
        var image = FindBlock(blocks, blockId);
        IsEnum(GetRequired(image, "Type"), "Image", 5).Should().BeTrue($"{blockId} must be an image block");

        var content = GetRequired(image, "Content");
        var caption = GetString(content, "Caption");
        caption.Should().NotBeNullOrWhiteSpace($"{blockId} must expose a caption for UI/UX coverage");

        if (requiresAlt)
        {
            GetString(content, "AltText").Should().NotBeNullOrWhiteSpace($"{blockId} must have alt text");
        }
        else
        {
            GetString(content, "AltText").Should().BeNullOrWhiteSpace($"{blockId} intentionally drives the missing-alt warning");
        }

        var source = GetRequired(content, "Source");
        var assetId = GetString(content, "AssetId");
        var url = GetString(content, "Url");
        if (assetId is not null)
        {
            assetId.Should().Be(stableAssetId);
            IsEnum(source, "Asset", 1).Should().BeTrue($"{blockId} must use a stable provider asset id");
        }
        else
        {
            url.Should().Be("/document-editor-evidence.svg");
            IsEnum(source, "Url", 0).Should().BeTrue($"{blockId} must use the stable demo URL asset");
        }

        var layout = GetRequired(content, "Layout");
        var wrap = GetRequired(layout, "Wrap");
        IsEnum(GetRequired(wrap, "Mode"), wrapName, wrapValue).Should().BeTrue($"{blockId} must use {wrapName} wrapping");

        if (horizontalName is not null && horizontalValue is not null)
        {
            var position = GetRequired(layout, "Position");
            IsEnum(GetRequired(position, "HorizontalAlignment"), horizontalName, horizontalValue.Value)
                .Should().BeTrue($"{blockId} must be horizontally positioned as {horizontalName}");
        }
    }

    private static async Task<JsonDocument> LoadContractDocumentAsync()
        => await GetApiJsonAsync("api/document-editor/documents/contract-demo");

    private static async Task<JsonDocument> GetApiJsonAsync(string path)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        var response = await http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    private static JsonElement FindBlock(IEnumerable<JsonElement> blocks, string id)
        => blocks.FirstOrDefault(block => GetString(block, "Id") == id);

    private static IEnumerable<JsonElement> GetArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray();
    }

    private static JsonElement GetRequired(JsonElement element, string propertyName)
    {
        if (TryGetProperty(element, propertyName, out var value))
        {
            return value;
        }

        throw new AssertFailedException($"Expected JSON property '{propertyName}' was not found.");
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var camel = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
            return element.TryGetProperty(camel, out value);
        }

        value = default;
        return false;
    }

    private static bool IsEnum(JsonElement element, string stringValue, int numericValue)
        => element.ValueKind switch
        {
            JsonValueKind.String => string.Equals(element.GetString(), stringValue, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => element.TryGetInt32(out var value) && value == numericValue,
            _ => false
        };

    private static string Canonicalize(JsonElement element)
        => JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = false });

    private static string NormalizeWhitespace(string value)
        => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string ExtractPlainText(IEnumerable<JsonElement> blocks)
    {
        var chunks = new List<string>();
        foreach (var block in blocks)
        {
            if (!TryGetProperty(block, "Content", out var content))
            {
                continue;
            }

            foreach (var inline in GetArray(content, "Inlines"))
            {
                var text = GetString(inline, "Text") ?? GetString(inline, "DisplayName");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    chunks.Add(text);
                }
            }
        }

        return string.Join(" ", chunks);
    }
}
