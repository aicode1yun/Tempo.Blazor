using System.Runtime.CompilerServices;
using System.Text;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>Shared core helpers for validating and aggregating streamed Notion AI provider responses.</summary>
public sealed class NotionAIStreamingService
{
    /// <summary>Streams generated completion chunks after validating the request.</summary>
    public async IAsyncEnumerable<string> GenerateAsync(
        INotionAIProvider provider,
        AiCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ValidateCompletionRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        await foreach (var chunk in provider.GenerateAsync(request, cancellationToken).WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
        }
    }

    /// <summary>Aggregates generated completion chunks into a single string.</summary>
    public Task<string> GenerateTextAsync(
        INotionAIProvider provider,
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
        => AggregateAsync(GenerateAsync(provider, request, cancellationToken), cancellationToken);

    /// <summary>Aggregates improved text chunks into a single string.</summary>
    public Task<string> ImproveTextAsync(
        INotionAIProvider provider,
        string text,
        AiImproveMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ValidateRequiredText(text, nameof(text));
        cancellationToken.ThrowIfCancellationRequested();
        return AggregateAsync(provider.ImproveTextAsync(text, mode, cancellationToken), cancellationToken);
    }

    /// <summary>Aggregates answer chunks into a single string.</summary>
    public Task<string> AnswerQuestionTextAsync(
        INotionAIProvider provider,
        string question,
        string? scopePageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ValidateRequiredText(question, nameof(question));
        cancellationToken.ThrowIfCancellationRequested();
        return AggregateAsync(provider.AnswerQuestionAsync(question, scopePageId, cancellationToken), cancellationToken);
    }

    /// <summary>Aggregates any streamed AI response into a single string.</summary>
    public async Task<string> AggregateAsync(IAsyncEnumerable<string> stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new StringBuilder();
        await foreach (var chunk in stream.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(chunk))
                builder.Append(chunk);
        }

        return builder.ToString();
    }

    /// <summary>Validates a completion request before dispatching it to a provider.</summary>
    public static void ValidateCompletionRequest(AiCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArgumentException("Prompt is required.", nameof(request));
    }

    private static void ValidateRequiredText(string text, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", parameterName);
    }
}
