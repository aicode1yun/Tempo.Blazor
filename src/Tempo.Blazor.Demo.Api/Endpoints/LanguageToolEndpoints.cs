using System.Text.RegularExpressions;

namespace Tempo.Blazor.Demo.Api.Endpoints;

/// <summary>
/// LanguageTool-protocol demo endpoint (<c>POST /languagetool/v2/check</c>) backed by a small
/// Czech demo dictionary. It lets the reference <c>LanguageToolProofingProvider</c> from
/// Tempo.Blazor.Proofing.LanguageTool run end-to-end (form-encoded request, LanguageTool v2 JSON
/// response) without a self-hosted LanguageTool container; production deployments point the
/// provider at a real server instead (see docs/proofing-languagetool.md).
/// </summary>
public static class LanguageToolEndpoints
{
    private static readonly Regex WordPattern = new(@"[\p{L}\p{M}][\p{L}\p{M}'’-]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Known Czech demo misspellings with their corrections.</summary>
    private static readonly Dictionary<string, string[]> CzechMisspellings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["smlouvva"] = ["smlouva", "smlouvy"],
        ["chybbou"] = ["chybou"],
        ["dodavatell"] = ["dodavatel"],
        ["objednávkka"] = ["objednávka"],
        ["splatnostt"] = ["splatnost"]
    };

    /// <summary>Maps the LanguageTool-compatible check endpoint.</summary>
    public static void MapLanguageToolEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/languagetool/v2/check", async (HttpRequest request) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = "LanguageTool checks are form-encoded (application/x-www-form-urlencoded)." });
            }

            var form = await request.ReadFormAsync();
            var text = form["text"].ToString();
            if (string.IsNullOrEmpty(text))
            {
                return Results.BadRequest(new { message = "The 'text' form field is required." });
            }

            var language = form["language"].ToString();
            var matches = new List<object>();
            if (IsCzechOrAuto(language))
            {
                foreach (Match token in WordPattern.Matches(text))
                {
                    if (!CzechMisspellings.TryGetValue(token.Value, out var suggestions))
                    {
                        continue;
                    }

                    matches.Add(new
                    {
                        message = "Pravopisná chyba",
                        shortMessage = "Překlep",
                        offset = token.Index,
                        length = token.Length,
                        replacements = suggestions.Select(value => new { value }).ToArray(),
                        rule = new
                        {
                            id = "DEMO_CS_SPELLER",
                            description = "Czech demo dictionary speller",
                            category = new { id = "TYPOS", name = "Možný překlep" }
                        }
                    });
                }
            }

            return Results.Json(new
            {
                software = new { name = "Tempo.Demo.LanguageTool", version = "1.0" },
                language = new { code = string.IsNullOrWhiteSpace(language) ? "auto" : language, name = "Czech (demo)" },
                matches
            });
        });
    }

    private static bool IsCzechOrAuto(string language)
        => string.IsNullOrWhiteSpace(language)
           || string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase)
           || language.StartsWith("cs", StringComparison.OrdinalIgnoreCase);
}
