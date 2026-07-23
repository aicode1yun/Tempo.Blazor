using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Services;

internal static class NotionInlineHtmlSanitizer
{
    public static string SanitizeHtmlFragment(string? html)
        => NotionHtmlSanitizer.SanitizeHtmlFragment(html);

    public static string SanitizeBlockContent(string? html)
        => NotionHtmlSanitizer.SanitizeBlockContent(html);

    public static string EncodePlainText(string? text)
        => NotionHtmlSanitizer.EncodePlainText(text);
}
