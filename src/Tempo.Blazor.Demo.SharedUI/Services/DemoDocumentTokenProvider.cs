using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoDocumentTokenProvider : ITokenDataProvider, IDocumentTokenValueProvider
{
    private static readonly IReadOnlyList<DemoDocumentToken> Tokens =
    [
        new("client.name", "Client name", "Client or counterparty full name.", "Client", "Text"),
        new("case.number", "Case number", "Internal matter or court case reference.", "Case", "Text"),
        new("lawyer.name", "Lawyer name", "Responsible lawyer.", "Team", "Text"),
        new("today", "Today", "Current date formatted for preview.", "System", "Date")
    ];

    private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["client.name"] = "ACME Ltd.",
        ["lawyer.name"] = "JUDr. Petra Novakova",
        ["today"] = DateTime.Today.ToString("d")
    };

    public bool SupportsCreation => false;

    public Task<IEnumerable<IToken>> SearchTokensAsync(string query, CancellationToken ct = default)
    {
        IEnumerable<IToken> result = Tokens;
        if (!string.IsNullOrWhiteSpace(query))
        {
            result = result.Where(token =>
                token.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                || token.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (token.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return Task.FromResult(result);
    }

    public Task<IReadOnlyDictionary<string, DocumentTokenValue>> ResolveTokenValuesAsync(
        DocumentTokenResolutionContext context,
        IReadOnlyList<TokenRun> tokens,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, DocumentTokenValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            result[token.Key] = Values.TryGetValue(token.Key, out var value)
                ? new DocumentTokenValue
                {
                    Key = token.Key,
                    Value = value,
                    DisplayValue = value,
                    HasValue = true,
                    TokenType = token.TokenType,
                    TypeLabel = token.TypeLabel
                }
                : DocumentTokenValue.Missing(token.Key);
        }

        return Task.FromResult<IReadOnlyDictionary<string, DocumentTokenValue>>(result);
    }

    public void Refresh()
    {
    }

    private sealed record DemoDocumentToken(
        string Key,
        string DisplayName,
        string? Description,
        string? Category,
        string? TypeLabel) : IToken
    {
        public string? Icon => null;

        public string? ColorClass => null;
    }
}
