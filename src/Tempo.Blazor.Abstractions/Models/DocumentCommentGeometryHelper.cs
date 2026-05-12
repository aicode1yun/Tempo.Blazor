using System.Globalization;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Helpers for normalized document comment anchor geometry.</summary>
public static class DocumentCommentGeometryHelper
{
    /// <summary>Returns a CSS style for a point anchor positioned by normalized page coordinates.</summary>
    /// <param name="anchor">Point anchor to position.</param>
    /// <returns>CSS style string using percentage coordinates.</returns>
    public static string ToPointStyle(DocumentCommentAnchor anchor)
    {
        return string.Create(CultureInfo.InvariantCulture, $"left: {anchor.X * 100:0.###}%; top: {anchor.Y * 100:0.###}%;");
    }

    /// <summary>Returns a CSS style for an area anchor positioned by normalized page coordinates.</summary>
    /// <param name="anchor">Area anchor to position.</param>
    /// <returns>CSS style string using percentage coordinates and size.</returns>
    public static string ToAreaStyle(DocumentCommentAnchor anchor)
    {
        return string.Create(CultureInfo.InvariantCulture, $"left: {anchor.X * 100:0.###}%; top: {anchor.Y * 100:0.###}%; width: {anchor.Width * 100:0.###}%; height: {anchor.Height * 100:0.###}%;");
    }
}
