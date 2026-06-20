using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionAIProviderContractTests
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void AiCompletionRequest_RoundtripsThroughJson()
    {
        var request = new AiCompletionRequest
        {
            Prompt = "Draft release notes",
            ContextHtml = "<p>CF27 provider contract</p>",
            PageId = "page-1",
            Mode = AiImproveMode.ChangeTone,
            TargetCulture = "cs-CZ"
        };

        var json = JsonSerializer.Serialize(request, _options);
        var roundtrip = JsonSerializer.Deserialize<AiCompletionRequest>(json, _options);

        roundtrip.Should().BeEquivalentTo(request);
    }

    [Fact]
    public void AiImproveMode_ContainsAllSupportedModes()
    {
        Enum.GetValues<AiImproveMode>()
            .Should()
            .Equal(
                AiImproveMode.Grammar,
                AiImproveMode.Shorten,
                AiImproveMode.Lengthen,
                AiImproveMode.ChangeTone,
                AiImproveMode.Simplify,
                AiImproveMode.Translate);
    }

    [Fact]
    public async Task INotionAIProvider_CanBeImplementedWithoutBlazorDependencies()
    {
        INotionAIProvider provider = new InMemoryAIProvider();

        var generated = await CollectAsync(provider.GenerateAsync(
            new AiCompletionRequest { Prompt = "Build outline", PageId = "page-1" },
            CancellationToken.None));
        var improved = await CollectAsync(provider.ImproveTextAsync("ship feature", AiImproveMode.Lengthen, CancellationToken.None));
        var summary = await provider.SummarizePageAsync("page-1", CancellationToken.None);
        var answer = await CollectAsync(provider.AnswerQuestionAsync("What changed?", "page-1", CancellationToken.None));

        generated.Should().Be("Generated: Build outline for page-1");
        improved.Should().Be("Lengthen: ship feature");
        summary.Should().Be("Summary for page-1");
        answer.Should().Be("Answer: What changed? in page-1");
    }

    private static async Task<string> CollectAsync(IAsyncEnumerable<string> stream)
    {
        var chunks = new List<string>();
        await foreach (var chunk in stream)
            chunks.Add(chunk);

        return string.Concat(chunks);
    }

    private sealed class InMemoryAIProvider : INotionAIProvider
    {
        public async IAsyncEnumerable<string> GenerateAsync(
            AiCompletionRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return $"Generated: {request.Prompt} for {request.PageId}";
        }

        public async IAsyncEnumerable<string> ImproveTextAsync(
            string text,
            AiImproveMode mode,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return $"{mode}: {text}";
        }

        public Task<string> SummarizePageAsync(string pageId, CancellationToken cancellationToken)
            => Task.FromResult($"Summary for {pageId}");

        public async IAsyncEnumerable<string> AnswerQuestionAsync(
            string question,
            string? scopePageId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return $"Answer: {question} in {scopePageId}";
        }
    }
}
