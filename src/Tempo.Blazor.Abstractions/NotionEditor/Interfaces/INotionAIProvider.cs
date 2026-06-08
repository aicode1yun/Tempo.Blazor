using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>
/// Provides AI-powered Notion editor operations. Implementations may call an LLM,
/// a private enterprise AI gateway, or another deterministic provider.
/// </summary>
public interface INotionAIProvider
{
    /// <summary>Streams a generated completion for the supplied request.</summary>
    IAsyncEnumerable<string> GenerateAsync(AiCompletionRequest request, CancellationToken cancellationToken);

    /// <summary>Streams an improved version of the supplied text.</summary>
    IAsyncEnumerable<string> ImproveTextAsync(string text, AiImproveMode mode, CancellationToken cancellationToken);

    /// <summary>Returns a concise page summary for the supplied page id.</summary>
    Task<string> SummarizePageAsync(string pageId, CancellationToken cancellationToken);

    /// <summary>Streams an answer for a question, optionally scoped to one page.</summary>
    IAsyncEnumerable<string> AnswerQuestionAsync(string question, string? scopePageId, CancellationToken cancellationToken);
}
