using System.Runtime.CompilerServices;
using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionAIStreamingServiceTests
{
    [Fact]
    public async Task AggregateAsync_CombinesChunksInOrder()
    {
        var service = new NotionAIStreamingService();

        var result = await service.AggregateAsync(Stream("Alpha ", "Beta", " Gamma"), CancellationToken.None);

        result.Should().Be("Alpha Beta Gamma");
    }

    [Fact]
    public async Task GenerateTextAsync_EmptyPrompt_ThrowsValidationException()
    {
        var service = new NotionAIStreamingService();
        var provider = new ContractAIProvider();

        var act = async () => await service.GenerateTextAsync(
            provider,
            new AiCompletionRequest { Prompt = "   " },
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("request");
    }

    [Fact]
    public async Task GenerateTextAsync_CancelledToken_StopsBeforeProviderEnumeration()
    {
        var service = new NotionAIStreamingService();
        var provider = new ContractAIProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await service.GenerateTextAsync(
            provider,
            new AiCompletionRequest { Prompt = "Draft section" },
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        provider.EnumerationStarted.Should().BeFalse();
    }

    [Fact]
    public async Task AggregateAsync_CancelledDuringStream_ThrowsOperationCancelled()
    {
        var service = new NotionAIStreamingService();
        using var cts = new CancellationTokenSource();

        var act = async () => await service.AggregateAsync(CancellableStream(cts), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async IAsyncEnumerable<string> Stream(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }

    private static async IAsyncEnumerable<string> CancellableStream(
        CancellationTokenSource source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return "first";
        await source.CancelAsync();
        cancellationToken.ThrowIfCancellationRequested();
        yield return "second";
    }

    private sealed class ContractAIProvider : INotionAIProvider
    {
        public bool EnumerationStarted { get; private set; }

        public async IAsyncEnumerable<string> GenerateAsync(
            AiCompletionRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            EnumerationStarted = true;
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return "generated";
        }

        public async IAsyncEnumerable<string> ImproveTextAsync(
            string text,
            AiImproveMode mode,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return text;
        }

        public Task<string> SummarizePageAsync(string pageId, CancellationToken cancellationToken)
            => Task.FromResult(pageId);

        public async IAsyncEnumerable<string> AnswerQuestionAsync(
            string question,
            string? scopePageId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return question;
        }
    }
}
