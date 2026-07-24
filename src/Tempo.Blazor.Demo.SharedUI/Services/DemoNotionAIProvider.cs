using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionAIProvider : INotionAIProvider
{
    private const int ChunkSize = 36;
    private readonly DemoNotionDataProvider _dataProvider;
    private readonly NotionEditorBlockService _blockService;

    public DemoNotionAIProvider(
        DemoNotionDataProvider dataProvider,
        DemoNotionAggregateProvider aggregateProvider)
    {
        _dataProvider = dataProvider;
        _blockService = new NotionEditorBlockService(aggregateProvider);
    }

    public bool SlowResponses { get; set; }

    public bool FailRequests { get; set; }

    public async IAsyncEnumerable<string> GenerateAsync(
        AiCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ThrowIfFailureRequested();
        ValidatePrompt(request);
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = NormalizeWhitespace(StripHtml(request.Prompt));
        var context = NormalizeWhitespace(StripHtml(request.ContextHtml ?? string.Empty));
        var mode = request.Mode is null ? "completion" : request.Mode.Value.ToString();
        var culture = string.IsNullOrWhiteSpace(request.TargetCulture) ? "current culture" : request.TargetCulture.Trim();

        var response = new StringBuilder()
            .Append("Demo AI ")
            .Append(mode)
            .Append(": ")
            .Append(prompt);

        if (!string.IsNullOrWhiteSpace(request.PageId))
            response.Append(" (page ").Append(request.PageId.Trim()).Append(')');

        if (!string.IsNullOrWhiteSpace(context))
            response.Append(". Context considered: ").Append(TrimToLength(context, 180));

        if (request.Mode == AiImproveMode.Translate)
            response.Append(". Target culture: ").Append(culture);

        await foreach (var chunk in StreamChunksAsync(response.ToString(), cancellationToken))
            yield return chunk;
    }

    public async IAsyncEnumerable<string> ImproveTextAsync(
        string text,
        AiImproveMode mode,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ThrowIfFailureRequested();
        ValidateText(text, nameof(text));
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = NormalizeWhitespace(StripHtml(text));
        var improved = mode switch
        {
            AiImproveMode.Grammar => EnsureSentence(normalized),
            AiImproveMode.Shorten => Shorten(normalized),
            AiImproveMode.Lengthen => Lengthen(normalized),
            AiImproveMode.ChangeTone => $"Professional rewrite: {EnsureSentence(normalized)}",
            AiImproveMode.Simplify => Simplify(normalized),
            AiImproveMode.Translate => $"Translated text: {EnsureSentence(normalized)}",
            _ => EnsureSentence(normalized)
        };

        await foreach (var chunk in StreamChunksAsync(improved, cancellationToken))
            yield return chunk;
    }

    public async Task<string> SummarizePageAsync(string pageId, CancellationToken cancellationToken)
    {
        ThrowIfFailureRequested();
        ValidateText(pageId, nameof(pageId));
        cancellationToken.ThrowIfCancellationRequested();

        var page = await _dataProvider.GetPageAsync(pageId.Trim());
        cancellationToken.ThrowIfCancellationRequested();

        var blocks = await _blockService.GetBlocksAsync(pageId.Trim());
        cancellationToken.ThrowIfCancellationRequested();

        var blockTexts = blocks
            .OrderBy(block => block.Order)
            .Select(block => ExtractBlockText(block.Content))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Take(6)
            .ToArray();

        if (blockTexts.Length == 0)
            return $"Summary: {page.Title} has no textual blocks yet.";

        return $"Summary: {page.Title} covers {TrimToLength(string.Join("; ", blockTexts), 360)}.";
    }

    public async IAsyncEnumerable<string> AnswerQuestionAsync(
        string question,
        string? scopePageId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ThrowIfFailureRequested();
        ValidateText(question, nameof(question));
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedQuestion = NormalizeWhitespace(StripHtml(question));
        var scope = string.IsNullOrWhiteSpace(scopePageId) ? "the current workspace" : $"page {scopePageId.Trim()}";
        var response = $"Demo AI answer for {scope}: {EnsureSentence(normalizedQuestion)} The demo provider returns deterministic guidance suitable for repeatable tests.";

        await foreach (var chunk in StreamChunksAsync(response, cancellationToken))
            yield return chunk;
    }

    private async IAsyncEnumerable<string> StreamChunksAsync(
        string text,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var index = 0;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (SlowResponses)
                await Task.Delay(80, cancellationToken);
            var length = Math.Min(ChunkSize, text.Length - index);
            yield return text.Substring(index, length);
            index += length;
            await Task.Yield();
        }
    }

    private void ThrowIfFailureRequested()
    {
        if (FailRequests)
            throw new InvalidOperationException("AI request failed.");
    }

    private static string ExtractBlockText(IBlockContent content)
        => content switch
        {
            ITextBlockContent text => CleanHtml(text.Html),
            ICodeBlockContent code => NormalizeWhitespace(code.Code),
            IBookmarkBlockContent bookmark => NormalizeWhitespace(string.Join(" ", bookmark.Title, bookmark.Description, bookmark.Url)),
            IEmbedBlockContent embed => NormalizeWhitespace(string.Join(" ", embed.Url, embed.Caption)),
            IFileBlockContent file => NormalizeWhitespace(string.Join(" ", file.FileName, file.Caption)),
            IPdfBlockContent pdf => NormalizeWhitespace(string.Join(" ", pdf.Url, pdf.Caption)),
            IImageBlockContent image => NormalizeWhitespace(string.Join(" ", image.AltText, image.Caption)),
            IVideoBlockContent video => NormalizeWhitespace(string.Join(" ", video.Url, video.Caption)),
            IAudioBlockContent audio => NormalizeWhitespace(string.Join(" ", audio.Url, audio.Caption)),
            IMediaBlockContent media => NormalizeWhitespace(string.Join(" ", media.Url, media.Caption)),
            _ => string.Empty
        };

    private static string CleanHtml(string html) => NormalizeWhitespace(StripHtml(html));

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withoutTags = Regex.Replace(html, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(withoutTags);
    }

    private static string NormalizeWhitespace(string value)
        => Regex.Replace(value ?? string.Empty, "\\s+", " ").Trim();

    private static string EnsureSentence(string value)
    {
        var normalized = NormalizeWhitespace(value);
        if (string.IsNullOrEmpty(normalized))
            return normalized;

        return normalized.EndsWith('.') || normalized.EndsWith('!') || normalized.EndsWith('?')
            ? normalized
            : normalized + ".";
    }

    private static string Shorten(string value)
    {
        var normalized = NormalizeWhitespace(value);
        var sentenceEnd = normalized.IndexOfAny(['.', '!', '?']);
        return sentenceEnd > 0
            ? normalized[..(sentenceEnd + 1)]
            : EnsureSentence(TrimToLength(normalized, 120));
    }

    private static string Lengthen(string value)
        => $"Expanded version: {EnsureSentence(value)} This version adds context, keeps the original intent, and makes the next action easier to understand.";

    private static string Simplify(string value)
        => $"Simple version: {EnsureSentence(value.Replace("utilize", "use", StringComparison.OrdinalIgnoreCase).Replace("approximately", "about", StringComparison.OrdinalIgnoreCase))}";

    private static string TrimToLength(string value, int maxLength)
    {
        var normalized = NormalizeWhitespace(value);
        if (normalized.Length <= maxLength)
            return normalized;

        var cut = normalized.LastIndexOf(' ', Math.Max(0, maxLength - 1), Math.Min(maxLength, normalized.Length));
        if (cut < maxLength / 2)
            cut = maxLength;

        return normalized[..cut].TrimEnd() + "...";
    }

    private static void ValidatePrompt(AiCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArgumentException("Prompt is required.", nameof(request));
    }

    private static void ValidateText(string text, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", parameterName);
    }
}
