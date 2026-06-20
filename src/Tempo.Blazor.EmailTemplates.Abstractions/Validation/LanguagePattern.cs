using System.Text.RegularExpressions;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Validation;

/// <summary>Shared language-code pattern (e.g. <c>en</c> or <c>en-US</c>).</summary>
internal static partial class LanguagePattern
{
    [GeneratedRegex("^[a-zA-Z]{2,3}(?:-[a-zA-Z]{2,4})?$", RegexOptions.ExplicitCapture, 1000)]
    public static partial Regex Regex { get; }
}
