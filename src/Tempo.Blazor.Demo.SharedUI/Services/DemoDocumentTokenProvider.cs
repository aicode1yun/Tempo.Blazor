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

    /// <summary>
    /// Selected assembly test-data set ("a" or "b") used by the document-assembly demo template;
    /// the host page sets it from the <c>assemblyData</c> query parameter.
    /// </summary>
    public string AssemblyDataset { get; set; } = "a";

    private Dictionary<string, DocumentTokenValue> CreateAssemblyValues()
    {
        var isSetA = !string.Equals(AssemblyDataset, "b", StringComparison.OrdinalIgnoreCase);
        var values = new Dictionary<string, DocumentTokenValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["contract.amount"] = new()
            {
                Key = "contract.amount",
                Value = isSetA ? "25000" : "500",
                DisplayValue = isSetA ? "25000" : "500",
            },
            ["contract.client"] = new()
            {
                Key = "contract.client",
                Value = isSetA ? "ACME Ltd." : "Malý odběratel s.r.o.",
                DisplayValue = isSetA ? "ACME Ltd." : "Malý odběratel s.r.o.",
            },
            ["items"] = new()
            {
                Key = "items",
                Rows = isSetA
                    ?
                    [
                        new Dictionary<string, string?> { ["name"] = "Licence", ["price"] = "20000" },
                        new Dictionary<string, string?> { ["name"] = "Implementace", ["price"] = "4000" },
                        new Dictionary<string, string?> { ["name"] = "Podpora", ["price"] = "1000" },
                    ]
                    :
                    [
                        new Dictionary<string, string?> { ["name"] = "Konzultace", ["price"] = "500" },
                    ],
            },
        };
        return values;
    }

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
        // Assembly demo values (conditions, repeating rows, computed totals) are always available —
        // expressions and conditional blocks reference them without a matching token run.
        var result = CreateAssemblyValues();
        foreach (var token in tokens)
        {
            if (Values.TryGetValue(token.Key, out var value))
            {
                result[token.Key] = new DocumentTokenValue
                {
                    Key = token.Key,
                    Value = value,
                    DisplayValue = value,
                    HasValue = true,
                    TokenType = token.TokenType,
                    TypeLabel = token.TypeLabel
                };
            }
            else if (!result.ContainsKey(token.Key))
            {
                result[token.Key] = DocumentTokenValue.Missing(token.Key);
            }
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
