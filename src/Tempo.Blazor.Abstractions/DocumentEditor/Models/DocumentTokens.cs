using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Context used when resolving template token values for a document.</summary>
public sealed class DocumentTokenResolutionContext
{
    /// <summary>Document identifier.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Optional culture name used for formatting resolved values.</summary>
    public string? CultureName { get; set; }

    /// <summary>Optional actor requesting the resolution.</summary>
    public DocumentEditorAuthor? Author { get; set; }

    /// <summary>Additional host application metadata.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}

/// <summary>Resolved value for a template token.</summary>
public sealed class DocumentTokenValue
{
    /// <summary>Token key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Whether a concrete value exists for the token.</summary>
    public bool HasValue { get; set; } = true;

    /// <summary>Raw value supplied by the host application.</summary>
    public string? Value { get; set; }

    /// <summary>Optional display value used in template preview.</summary>
    public string? DisplayValue { get; set; }

    /// <summary>Optional token type metadata.</summary>
    public string? TokenType { get; set; }

    /// <summary>
    /// Optional collection value: one dictionary per row (column name → cell value). Used by
    /// document assembly for repeating sections and aggregate functions (SUM/COUNT).
    /// </summary>
    public List<Dictionary<string, string?>>? Rows { get; set; }

    /// <summary>Optional short type label.</summary>
    public string? TypeLabel { get; set; }

    /// <summary>Creates a resolved token value.</summary>
    public static DocumentTokenValue Resolved(string key, string? value, string? displayValue = null)
    {
        return new DocumentTokenValue
        {
            Key = key,
            Value = value,
            DisplayValue = displayValue ?? value,
            HasValue = !string.IsNullOrWhiteSpace(displayValue ?? value)
        };
    }

    /// <summary>Creates a missing token value marker.</summary>
    public static DocumentTokenValue Missing(string key)
    {
        return new DocumentTokenValue
        {
            Key = key,
            HasValue = false
        };
    }
}

/// <summary>Validation result for tokens used by a document template.</summary>
public sealed class DocumentTokenValidationResult
{
    /// <summary>Whether all discovered tokens are valid.</summary>
    public bool IsValid => MissingTokenKeys.Count == 0 && DuplicateTokenKeys.Count == 0;

    /// <summary>Token keys used by the document.</summary>
    public List<string> TokenKeys { get; set; } = [];

    /// <summary>Token keys that are not available in the provided token catalog.</summary>
    public List<string> MissingTokenKeys { get; set; } = [];

    /// <summary>Token keys used more than once.</summary>
    public List<string> DuplicateTokenKeys { get; set; } = [];
}

/// <summary>Helper methods for working with document template tokens.</summary>
public static class DocumentTokenHelper
{
    /// <summary>Creates a document token run from an autocomplete token.</summary>
    public static TokenRun FromToken(IToken token)
    {
        return new TokenRun
        {
            Key = token.Key,
            DisplayName = token.DisplayName,
            Description = token.Description,
            ColorClass = token.ColorClass,
            TypeLabel = token.TypeLabel,
            TokenType = NormalizeTokenType(token.TypeLabel)
        };
    }

    /// <summary>Extracts all token runs from a document.</summary>
    public static IReadOnlyList<TokenRun> ExtractTokens(DocumentEditorDocument? document)
    {
        if (document is null)
        {
            return [];
        }

        var tokens = new List<TokenRun>();
        WalkBlocks(document.Blocks, tokens);
        return tokens;
    }

    /// <summary>Validates document token keys against an optional catalog of known token keys.</summary>
    public static DocumentTokenValidationResult ValidateTokens(
        DocumentEditorDocument? document,
        IEnumerable<string>? knownTokenKeys = null)
    {
        var tokens = ExtractTokens(document);
        var keys = tokens
            .Select(token => token.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new DocumentTokenValidationResult { TokenKeys = keys };

        result.DuplicateTokenKeys = tokens
            .Where(token => !string.IsNullOrWhiteSpace(token.Key))
            .GroupBy(token => token.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (knownTokenKeys is not null)
        {
            var known = knownTokenKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            result.MissingTokenKeys = keys
                .Where(key => !known.Contains(key))
                .ToList();
        }

        return result;
    }

    private static string? NormalizeTokenType(string? typeLabel)
    {
        return string.IsNullOrWhiteSpace(typeLabel)
            ? null
            : typeLabel.Trim().ToLowerInvariant().Replace(' ', '-');
    }

    private static void WalkBlocks(IEnumerable<DocumentBlock> blocks, List<TokenRun> tokens)
    {
        foreach (var block in blocks)
        {
            switch (block.Content)
            {
                case ParagraphBlockContent paragraph:
                    WalkInlines(paragraph.Inlines, tokens);
                    break;
                case HeadingBlockContent heading:
                    WalkInlines(heading.Inlines, tokens);
                    break;
                case ListBlockContent list:
                    WalkInlines(list.Inlines, tokens);
                    break;
                case QuoteBlockContent quote:
                    WalkInlines(quote.Inlines, tokens);
                    break;
                case ContentControlBlockContent contentControl:
                    WalkBlocks(contentControl.Blocks, tokens);
                    break;
                case TableBlockContent table:
                    foreach (var cell in table.Rows.SelectMany(row => row.Cells))
                    {
                        WalkBlocks(cell.Blocks, tokens);
                    }

                    break;
            }
        }
    }

    private static void WalkInlines(IEnumerable<InlineContent> inlines, List<TokenRun> tokens)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TokenRun token:
                    tokens.Add(token);
                    break;
                case DocumentContentControlRun contentControl:
                    WalkInlines(contentControl.Inlines, tokens);
                    break;
            }
        }
    }
}
