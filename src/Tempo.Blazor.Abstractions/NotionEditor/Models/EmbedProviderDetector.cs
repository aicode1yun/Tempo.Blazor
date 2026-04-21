namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public static class EmbedProviderDetector
{
    public static EmbedProvider Detect(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return EmbedProvider.Generic;

        var uri = new Uri(url, UriKind.RelativeOrAbsolute);
        if (!uri.IsAbsoluteUri)
            return EmbedProvider.Generic;

        var host = uri.Host.ToLowerInvariant();

        return host switch
        {
            var h when h.Contains("google.com/maps") || h.Contains("maps.google.com") => EmbedProvider.GoogleMaps,
            var h when h.Contains("drive.google.com") => EmbedProvider.GoogleDrive,
            var h when h.Contains("figma.com") => EmbedProvider.Figma,
            var h when h.Contains("miro.com") => EmbedProvider.Miro,
            var h when h.Contains("whimsical.com") => EmbedProvider.Whimsical,
            var h when h.Contains("framer.com") => EmbedProvider.Framer,
            var h when h.Contains("codepen.io") => EmbedProvider.CodePen,
            var h when h.Contains("codesandbox.io") => EmbedProvider.CodeSandbox,
            var h when h.Contains("gist.github.com") => EmbedProvider.GitHubGist,
            var h when h.Contains("twitter.com") || h.Contains("x.com") => EmbedProvider.Twitter,
            var h when h.Contains("typeform.com") => EmbedProvider.Typeform,
            var h when h.Contains("airtable.com") => EmbedProvider.Airtable,
            _ => EmbedProvider.Generic
        };
    }
}
