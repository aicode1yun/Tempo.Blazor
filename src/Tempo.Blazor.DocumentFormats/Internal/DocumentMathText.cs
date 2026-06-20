using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Internal;

internal static class DocumentMathText
{
    public static string FlattenMathContent(DocumentMathContent? content)
        => content is null ? string.Empty : string.Concat(content.Elements.Select(FlattenMathElement));

    public static string FlattenMathElement(DocumentMathElement? element)
    {
        if (element is null)
        {
            return string.Empty;
        }

        return NormalizeType(element.Type) switch
        {
            "fraction" => $"({FlattenMathContent(element.Numerator)})/({FlattenMathContent(element.Denominator)})",
            "radical" => string.IsNullOrEmpty(FlattenMathContent(element.Degree))
                ? $"sqrt({FlattenMathContent(element.Radicand)})"
                : $"root({FlattenMathContent(element.Degree)}, {FlattenMathContent(element.Radicand)})",
            "sup" => $"{FlattenMathContent(element.Base)}^{FlattenMathContent(element.Superscript)}",
            "sub" => $"{FlattenMathContent(element.Base)}_{FlattenMathContent(element.Subscript)}",
            "subsup" => $"{FlattenMathContent(element.Base)}_{FlattenMathContent(element.Subscript)}^{FlattenMathContent(element.Superscript)}",
            "nary" => FlattenNary(element),
            "matrix" => "[" + string.Join("; ", element.Rows.Select(row => string.Join(", ", row.Cells.Select(FlattenMathContent)))) + "]",
            "delimiter" => $"{element.Open}{FlattenMathContent(element.Content)}{element.Close}",
            "function" => $"{FlattenMathContent(element.FunctionName)}({FlattenMathContent(element.Base ?? element.Content)})",
            "accent" => $"{FlattenMathContent(element.Base)}{element.Accent}",
            "bar" => $"{FlattenMathContent(element.Base)}",
            "limit" => $"{FlattenMathContent(element.Base)}_{FlattenMathContent(element.LowerLimit)}^{FlattenMathContent(element.UpperLimit)}",
            _ => FirstNonEmpty(element.Text, FlattenMathContent(element.Content), FlattenMathContent(element.Base))
        };
    }

    private static string FlattenNary(DocumentMathElement element)
    {
        var lower = FlattenMathContent(element.LowerLimit);
        var upper = FlattenMathContent(element.UpperLimit);
        var limits = string.Concat(
            string.IsNullOrEmpty(lower) ? string.Empty : $"_{lower}",
            string.IsNullOrEmpty(upper) ? string.Empty : $"^{upper}");
        var body = FlattenMathContent(element.Base);
        return $"{element.Operator}{limits}{(string.IsNullOrEmpty(body) ? string.Empty : " " + body)}";
    }

    private static string NormalizeType(string? type)
        => string.Concat((type ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? string.Empty;
}
