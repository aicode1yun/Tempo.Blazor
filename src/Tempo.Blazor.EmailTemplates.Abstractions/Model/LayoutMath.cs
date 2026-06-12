using System.Globalization;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>Column-width arithmetic shared by section editing and layout presets.</summary>
public static class LayoutMath
{
    /// <summary>
    /// Computes <paramref name="count"/> equal column widths as percentage strings that always
    /// total exactly 100% (the last column absorbs any rounding remainder).
    /// </summary>
    public static string[] EqualWidths(int count)
    {
        if (count <= 0) return Array.Empty<string>();

        // Two decimal places of precision; the last column takes the remainder so the sum is 100.
        var each = Math.Floor(10000m / count) / 100m;
        var widths = new string[count];
        for (int i = 0; i < count - 1; i++)
            widths[i] = Format(each);
        widths[count - 1] = Format(100m - each * (count - 1));
        return widths;
    }

    /// <summary>Formats a percentage value, trimming trailing zeros (e.g. <c>50%</c>, <c>33.33%</c>).</summary>
    public static string Format(decimal percent)
        => percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
