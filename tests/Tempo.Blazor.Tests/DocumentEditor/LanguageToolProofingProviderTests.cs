using System.Net;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Proofing.LanguageTool;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Phase 7: reference LanguageTool integration. The provider speaks the LanguageTool v2 wire
/// protocol (POST /v2/check with form-encoded text+language) and maps the JSON matches onto
/// <see cref="DocumentProofingIssue"/> items the editor materializes into squiggles.
/// </summary>
public sealed class LanguageToolProofingProviderTests
{
    private const string CzechResponseJson =
        """
        {
          "software": { "name": "LanguageTool", "version": "6.4" },
          "language": { "code": "cs-CZ", "name": "Czech" },
          "matches": [
            {
              "message": "Pravopisná chyba",
              "shortMessage": "Překlep",
              "offset": 5,
              "length": 8,
              "replacements": [ { "value": "smlouva" }, { "value": "smlouvy" } ],
              "rule": { "id": "MORFOLOGIK_RULE_CS_CZ", "category": { "id": "TYPOS", "name": "Možný překlep" } }
            },
            {
              "message": "Pravopisná chyba",
              "offset": 30,
              "length": 7,
              "replacements": [ { "value": "chybou" } ],
              "rule": { "id": "MORFOLOGIK_RULE_CS_CZ", "category": { "id": "TYPOS", "name": "Možný překlep" } }
            }
          ]
        }
        """;

    private const string CzechSampleText = "Tato smlouvva byla uzavřena s chybbou";

    [Fact]
    public async Task CheckAsync_PostsFormEncodedTextAndLanguageToV2Check()
    {
        var handler = new RecordingHandler(CzechResponseJson);
        var provider = CreateProvider(handler, new LanguageToolProofingOptions
        {
            BaseAddress = new Uri("http://localhost:8010"),
            Language = "cs-CZ"
        });

        await provider.CheckAsync(new DocumentProofingCheckRequest { Text = CzechSampleText });

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be("http://localhost:8010/v2/check");
        handler.LastRequestBody.Should().Contain("text=");
        handler.LastRequestBody.Should().Contain("language=cs-CZ");
    }

    [Fact]
    public async Task CheckAsync_MapsMatchesToProofingIssues()
    {
        var provider = CreateProvider(new RecordingHandler(CzechResponseJson));

        var result = await provider.CheckAsync(new DocumentProofingCheckRequest { Text = CzechSampleText });

        result.Issues.Should().HaveCount(2);
        result.Issues[0].Word.Should().Be("smlouvva");
        result.Issues[0].Suggestions.Should().Equal("smlouva", "smlouvy");
        result.Issues[0].Message.Should().Be("Pravopisná chyba");
        result.Issues[0].RuleId.Should().Be("MORFOLOGIK_RULE_CS_CZ");
        result.Issues[0].CategoryId.Should().Be("TYPOS");
        result.Issues[1].Word.Should().Be("chybbou");
        result.Issues[1].Suggestions.Should().Equal("chybou");
    }

    [Fact]
    public async Task CheckAsync_RequestLanguageOverridesConfiguredLanguage()
    {
        var handler = new RecordingHandler(CzechResponseJson);
        var provider = CreateProvider(handler, new LanguageToolProofingOptions { Language = "en-US" });

        await provider.CheckAsync(new DocumentProofingCheckRequest { Text = CzechSampleText, Language = "cs-CZ" });

        handler.LastRequestBody.Should().Contain("language=cs-CZ");
    }

    [Fact]
    public async Task CheckAsync_SendsDisabledRulesAndCategories()
    {
        var handler = new RecordingHandler(CzechResponseJson);
        var provider = CreateProvider(handler, new LanguageToolProofingOptions
        {
            Language = "cs-CZ",
            DisabledRules = ["WHITESPACE_RULE"],
            DisabledCategories = ["STYLE"]
        });

        await provider.CheckAsync(new DocumentProofingCheckRequest { Text = CzechSampleText });

        handler.LastRequestBody.Should().Contain("disabledRules=WHITESPACE_RULE");
        handler.LastRequestBody.Should().Contain("disabledCategories=STYLE");
    }

    [Fact]
    public async Task CheckAsync_CustomDictionaryWordsAreNotReported()
    {
        var options = new LanguageToolProofingOptions { Language = "cs-CZ" };
        options.CustomDictionary.Add("smlouvva");
        var provider = CreateProvider(new RecordingHandler(CzechResponseJson), options);

        var result = await provider.CheckAsync(new DocumentProofingCheckRequest { Text = CzechSampleText });

        result.Issues.Should().ContainSingle().Which.Word.Should().Be("chybbou");
    }

    [Fact]
    public async Task AddToDictionary_SuppressesTheWordOnSubsequentChecks()
    {
        var provider = CreateProvider(new RecordingHandler(CzechResponseJson));

        provider.AddToDictionary("Chybbou");
        var result = await provider.CheckAsync(new DocumentProofingCheckRequest { Text = CzechSampleText });

        result.Issues.Should().ContainSingle().Which.Word.Should().Be("smlouvva");
    }

    [Fact]
    public async Task CheckAsync_EmptyText_ReturnsEmptyWithoutHttpCall()
    {
        var handler = new RecordingHandler(CzechResponseJson);
        var provider = CreateProvider(handler);

        var result = await provider.CheckAsync(new DocumentProofingCheckRequest { Text = "   " });

        result.Issues.Should().BeEmpty();
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_ServerError_Throws()
    {
        var provider = CreateProvider(new RecordingHandler("boom", HttpStatusCode.InternalServerError));

        var act = () => provider.CheckAsync(new DocumentProofingCheckRequest { Text = CzechSampleText });

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CheckAsync_MatchOffsetsOutOfRange_AreIgnored()
    {
        const string brokenJson =
            """
            {
              "matches": [
                { "message": "x", "offset": 900, "length": 5, "replacements": [], "rule": { "id": "R" } },
                { "message": "y", "offset": 5, "length": 8, "replacements": [ { "value": "smlouva" } ], "rule": { "id": "R" } }
              ]
            }
            """;
        var provider = CreateProvider(new RecordingHandler(brokenJson));

        var result = await provider.CheckAsync(new DocumentProofingCheckRequest { Text = CzechSampleText });

        result.Issues.Should().ContainSingle().Which.Word.Should().Be("smlouvva");
    }

    [Fact]
    public void CreateCzech_DefaultsToCzechLanguage()
    {
        var options = LanguageToolProofingOptions.CreateCzech();

        options.Language.Should().Be("cs-CZ");
    }

    private static LanguageToolProofingProvider CreateProvider(
        RecordingHandler handler,
        LanguageToolProofingOptions? options = null)
        => new(new HttpClient(handler), options ?? new LanguageToolProofingOptions
        {
            BaseAddress = new Uri("http://localhost:8010"),
            Language = "cs-CZ"
        });

    private sealed class RecordingHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
