using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionTokenProvider : ITokenDataProvider
{
    private readonly List<DemoNotionToken> _tokens =
    [
        new("client.name",         "Client name",         "Full legal name of the client or counterparty.",   "Client",  "Text",   "👤"),
        new("client.email",        "Client email",        "Primary contact e-mail address.",                  "Client",  "Text",   "📧"),
        new("client.phone",        "Client phone",        "Primary contact phone number.",                    "Client",  "Text",   "📞"),
        new("client.address",      "Client address",      "Registered address of the client.",                "Client",  "Text",   "🏠"),
        new("case.number",         "Case number",         "Internal matter or court case reference.",         "Case",    "Text",   "📁"),
        new("case.court",          "Court",               "Name of the court handling the matter.",           "Case",    "Text",   "🏛️"),
        new("case.status",         "Case status",         "Current status of the case.",                     "Case",    "Text",   "📋"),
        new("lawyer.name",         "Lawyer name",         "Full name of the responsible lawyer.",             "Team",    "Text",   "👨‍⚖️"),
        new("lawyer.email",        "Lawyer email",        "E-mail address of the responsible lawyer.",        "Team",    "Text",   "📧"),
        new("company.name",        "Company name",        "Legal name of our organisation.",                  "Company", "Text",   "🏢"),
        new("company.reg_number",  "Company reg. number", "Company registration number.",                     "Company", "Text",   "🔢"),
        new("today",               "Today",               "Current date formatted as DD. MM. YYYY.",          "System",  "Date",   "📅"),
        new("now",                 "Now",                 "Current date and time.",                           "System",  "DateTime","🕐"),
        new("page.title",          "Page title",          "Title of the current page or document.",           "System",  "Text",   "📄"),
    ];

    public bool SupportsCreation => true;

    public Task<IEnumerable<IToken>> SearchTokensAsync(string query, CancellationToken ct = default)
    {
        IEnumerable<IToken> result = _tokens;
        if (!string.IsNullOrWhiteSpace(query))
        {
            result = result.Where(t =>
                t.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                || t.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (t.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (t.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return Task.FromResult(result);
    }

    public void Refresh() { }

    /// <summary>Adds a custom token to the in-memory list.</summary>
    public void AddToken(string displayName, string? description = null)
    {
        var key = "custom." + displayName.ToLowerInvariant()
            .Replace(' ', '_')
            .Replace('.', '_');
        // Deduplicate by key
        if (_tokens.Any(t => t.Key == key)) return;
        _tokens.Add(new(key, displayName, description, "Custom", "Text", "✨"));
    }

    private sealed record DemoNotionToken(
        string  Key,
        string  DisplayName,
        string? Description,
        string? Category,
        string? TypeLabel,
        string? Icon) : IToken
    {
        public string? ColorClass => null;
    }
}
