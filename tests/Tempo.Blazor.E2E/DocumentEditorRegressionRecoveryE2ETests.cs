using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Regression recovery baseline tests for the 2026-05-23 Google Docs engine plan.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Recovery_SeedContainsAllPhase0Scenarios()
    {
        using var response = await LoadRecoveryDocumentFromApiAsync();
        var snapshot = GetString(response.RootElement, "JsonSnapshot");
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot), "Recovery API response must include JsonSnapshot.");

        using var documentJson = JsonDocument.Parse(snapshot!);
        var document = documentJson.RootElement;
        var blocks = GetArray(document, "Blocks").ToArray();
        var headersFooters = GetArray(document, "HeadersFooters").ToArray();
        var revisions = GetArray(document, "Revisions").ToArray();
        var comments = GetArray(document, "Comments").ToArray();

        AssertHasHeaderFooter(headersFooters, "Header", "Recovery Primary Header");
        AssertHasHeaderFooter(headersFooters, "Footer", "Recovery Primary Footer");
        AssertHasFooterPageNumberField(headersFooters);

        AssertHasBlock(blocks, "recovery-comment-paragraph");
        AssertHasBlock(blocks, "recovery-insertion-revision-paragraph");
        AssertHasBlock(blocks, "recovery-deletion-revision-paragraph");
        AssertHasBlock(blocks, "recovery-selection-paragraph");
        AssertHasBlock(blocks, "recovery-url-image");
        AssertHasBlock(blocks, "recovery-provider-image");
        AssertHasBlock(blocks, "recovery-inline-image");
        AssertHasBlock(blocks, "recovery-left-wrap-image");
        AssertHasBlock(blocks, "recovery-right-wrap-image");
        AssertHasBlock(blocks, "recovery-top-bottom-image");
        AssertHasBlock(blocks, "recovery-missing-alt-image");
        AssertHasBlock(blocks, "recovery-table-under-images");

        Assert.IsTrue(comments.Any(comment => GetString(comment, "Id") == "recovery-comment-visible"),
            "Recovery seed must contain a stable comment.");
        Assert.IsTrue(revisions.Any(revision => GetString(revision, "Id") == "recovery-revision-insertion" && IsEnum(revision, "Type", "Insertion", 0)),
            "Recovery seed must contain a pending insertion revision.");
        Assert.IsTrue(revisions.Any(revision => GetString(revision, "Id") == "recovery-revision-deletion" && IsEnum(revision, "Type", "Deletion", 1)),
            "Recovery seed must contain a pending deletion revision.");
    }

    [TestMethod]
    public async Task Recovery_DocumentShowsHeadersFootersCommentsAndRevisions()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        try
        {
            await page.WaitForTimeoutAsync(500);

            var geometry = await CaptureEditorGeometryAsync(page);
            AssertVisibleRect(geometry.PageRect, "page");
            AssertVisibleRect(geometry.HeaderRect, "primary header");
            AssertVisibleRect(geometry.FooterRect, "primary footer");
            AssertVisibleRect(geometry.BodyRect, "body");

            StringAssert.Contains(geometry.VisibleText, "Recovery Primary Header");
            StringAssert.Contains(geometry.VisibleText, "Recovery Primary Footer");
            StringAssert.Contains(geometry.VisibleText, "visible comment anchor");
            StringAssert.Contains(geometry.VisibleText, "inserted recovery clause");
            StringAssert.Contains(geometry.VisibleText, "deleted recovery clause");

            Assert.IsTrue(geometry.CommentMarkerRects.Length > 0,
                "The recovery document must show at least one visible comment marker in the document text.");
            Assert.IsTrue(geometry.RevisionMarkerRects.Length >= 2,
                "The recovery document must show visible insertion and deletion revision markers in All Markup mode.");

            await CaptureEditorScreenshotAsync(page, "phase0-visible-baseline-success");
            await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_DocumentShowsHeadersFootersCommentsAndRevisions));
        }
        catch
        {
            await CaptureEditorScreenshotAsync(page, "phase0-visible-baseline-failure");
            throw;
        }
    }

    private static async Task<JsonDocument> LoadRecoveryDocumentFromApiAsync()
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        var response = await http.GetAsync("api/document-editor/documents/recovery-2026-05-23");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static void AssertVisibleRect(DocumentEditorRectProbe? rect, string name)
    {
        Assert.IsNotNull(rect, $"Expected visible {name} rect.");
        Assert.IsTrue(rect.Width > 0.5, $"Expected visible {name} width, got {rect.Width}.");
        Assert.IsTrue(rect.Height > 0.5, $"Expected visible {name} height, got {rect.Height}.");
    }

    private static void AssertHasBlock(JsonElement[] blocks, string id)
    {
        Assert.IsTrue(blocks.Any(block => GetString(block, "Id") == id), $"Recovery seed must contain block '{id}'.");
    }

    private static void AssertHasHeaderFooter(JsonElement[] headersFooters, string type, string expectedText)
    {
        var region = headersFooters.FirstOrDefault(item => IsEnum(item, "Type", type, type == "Header" ? 0 : 1));
        Assert.AreNotEqual(default, region, $"Recovery seed must contain primary {type.ToLowerInvariant()}.");
        StringAssert.Contains(ExtractPlainText(GetArray(region, "Blocks")), expectedText);
    }

    private static void AssertHasFooterPageNumberField(JsonElement[] headersFooters)
    {
        var footer = headersFooters.FirstOrDefault(item => IsEnum(item, "Type", "Footer", 1));
        Assert.AreNotEqual(default, footer, "Recovery seed must contain a footer.");
        var hasPageNumber = GetArray(footer, "Blocks")
            .SelectMany(block => GetArray(GetRequired(block, "Content"), "Inlines"))
            .Any(inline => IsEnum(inline, "FieldType", "PageNumber", 0));
        Assert.IsTrue(hasPageNumber, "Recovery footer must contain a page number field.");
    }

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

    private static bool IsEnum(JsonElement element, string propertyName, string stringValue, int numericValue)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => string.Equals(value.GetString(), stringValue, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => value.TryGetInt32(out var numeric) && numeric == numericValue,
            _ => false
        };
    }

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
                var text = GetString(inline, "Text")
                    ?? GetString(inline, "DisplayText")
                    ?? GetString(inline, "FallbackText")
                    ?? GetString(inline, "DisplayName");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    chunks.Add(text);
                }
            }
        }

        return string.Join(" ", chunks);
    }
}
