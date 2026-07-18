namespace Tempo.Blazor.Proofing.LanguageTool;

/// <summary>Configuration for <see cref="LanguageToolProofingProvider"/>.</summary>
public sealed class LanguageToolProofingOptions
{
    /// <summary>
    /// Base address of the LanguageTool server (self-hosted or hosted). The provider posts to
    /// <c>{BaseAddress}/v2/check</c>. When null, the <see cref="HttpClient.BaseAddress"/> of the
    /// injected client is used, falling back to the self-host default <c>http://localhost:8010</c>.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Default BCP-47 language sent to the server when the check request does not carry its own
    /// language. Use <c>"auto"</c> for server-side language detection.
    /// </summary>
    public string Language { get; set; } = "auto";

    /// <summary>Optional mother tongue hint for false-friend detection.</summary>
    public string? MotherTongue { get; set; }

    /// <summary>LanguageTool rule ids disabled for every check (e.g. WHITESPACE_RULE).</summary>
    public IReadOnlyList<string> DisabledRules { get; set; } = [];

    /// <summary>LanguageTool category ids disabled for every check (e.g. STYLE).</summary>
    public IReadOnlyList<string> DisabledCategories { get; set; } = [];

    /// <summary>
    /// Client-side custom dictionary: findings whose flagged word is contained here are suppressed
    /// without a server round-trip. Case-insensitive. Use this for app-specific terminology (brand
    /// names, Czech legal terms) that a stock LanguageTool dictionary flags.
    /// </summary>
    public ISet<string> CustomDictionary { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Maximum number of characters sent per check; longer text is truncated.</summary>
    public int MaxTextLength { get; set; } = 20_000;

    /// <summary>Maximum number of replacement suggestions kept per finding.</summary>
    public int MaxSuggestionsPerIssue { get; set; } = 6;

    /// <summary>Creates options preconfigured for Czech proofing (<c>cs-CZ</c>).</summary>
    public static LanguageToolProofingOptions CreateCzech() => new() { Language = "cs-CZ" };
}
