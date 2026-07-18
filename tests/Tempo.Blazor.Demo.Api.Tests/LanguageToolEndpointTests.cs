using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Proofing.LanguageTool;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>
/// Phase 7: the demo API hosts a LanguageTool-protocol endpoint
/// (<c>POST /languagetool/v2/check</c>) backed by a small Czech demo dictionary, so the reference
/// <see cref="LanguageToolProofingProvider"/> can be exercised end-to-end without a self-hosted
/// LanguageTool container. The wire format mirrors the real LanguageTool v2 JSON.
/// </summary>
public class LanguageToolEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CzechTextWithErrors = "Tato smlouvva byla uzavřena s chybbou.";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public LanguageToolEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Check_FlagsKnownCzechMisspellingsWithOffsetsAndSuggestions()
    {
        using var response = await PostCheckAsync(CzechTextWithErrors, "cs-CZ");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var matches = json.RootElement.GetProperty("matches");
        matches.GetArrayLength().Should().Be(2);

        var first = matches[0];
        first.GetProperty("offset").GetInt32().Should().Be(5);
        first.GetProperty("length").GetInt32().Should().Be(8);
        first.GetProperty("replacements")[0].GetProperty("value").GetString().Should().Be("smlouva");
        first.GetProperty("rule").GetProperty("category").GetProperty("id").GetString().Should().Be("TYPOS");

        var second = matches[1];
        CzechTextWithErrors.Substring(
            second.GetProperty("offset").GetInt32(),
            second.GetProperty("length").GetInt32()).Should().Be("chybbou");
    }

    [Fact]
    public async Task Check_CorrectCzechText_ReturnsNoMatches()
    {
        using var response = await PostCheckAsync("Tato smlouva byla uzavřena bez chyb.", "cs-CZ");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("matches").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Check_NonCzechLanguage_ReturnsNoMatches()
    {
        using var response = await PostCheckAsync(CzechTextWithErrors, "en-US");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("matches").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Check_NonFormContentType_ReturnsBadRequest()
    {
        using var response = await _client.PostAsync(
            "/languagetool/v2/check",
            new StringContent("{\"text\":\"x\"}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Check_MissingText_ReturnsBadRequest()
    {
        using var response = await _client.PostAsync(
            "/languagetool/v2/check",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("language", "cs-CZ")]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReferenceProvider_AgainstDemoEndpoint_ReportsCzechIssues()
    {
        var provider = new LanguageToolProofingProvider(
            _factory.CreateClient(),
            new LanguageToolProofingOptions
            {
                BaseAddress = new Uri(_client.BaseAddress!, "languagetool"),
                Language = "cs-CZ"
            });

        var result = await provider.CheckAsync(new DocumentProofingCheckRequest { Text = CzechTextWithErrors });

        result.Issues.Should().HaveCount(2);
        result.Issues[0].Word.Should().Be("smlouvva");
        result.Issues[0].Suggestions.Should().Contain("smlouva");
        result.Issues[1].Word.Should().Be("chybbou");
        result.Issues[1].Suggestions.Should().Contain("chybou");
    }

    private Task<HttpResponseMessage> PostCheckAsync(string text, string language)
        => _client.PostAsync(
            "/languagetool/v2/check",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("language", language)
            ]));
}
