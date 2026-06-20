namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public static class EmbedProviderDetector
{
    public static EmbedProvider Detect(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return EmbedProvider.Generic;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return EmbedProvider.Generic;

        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("google.com") && uri.AbsolutePath.Contains("/maps")) return EmbedProvider.GoogleMaps;
        if (host == "maps.google.com") return EmbedProvider.GoogleMaps;
        if (host == "drive.google.com")   return EmbedProvider.GoogleDrive;
        if (host.Contains("figma.com"))   return EmbedProvider.Figma;
        if (host.Contains("miro.com"))    return EmbedProvider.Miro;
        if (host.Contains("whimsical.com")) return EmbedProvider.Whimsical;
        if (host.Contains("framer.com"))  return EmbedProvider.Framer;
        if (host.Contains("codepen.io"))  return EmbedProvider.CodePen;
        if (host.Contains("codesandbox.io")) return EmbedProvider.CodeSandbox;
        if (host == "gist.github.com")    return EmbedProvider.GitHubGist;
        if (host.Contains("twitter.com") || host.Contains("x.com")) return EmbedProvider.Twitter;
        if (host.Contains("typeform.com")) return EmbedProvider.Typeform;
        if (host.Contains("airtable.com")) return EmbedProvider.Airtable;

        return EmbedProvider.Generic;
    }

    public static string GetEmbedUrl(string url, EmbedProvider provider) => provider switch
    {
        EmbedProvider.GoogleMaps   => ToGoogleMapsEmbed(url),
        EmbedProvider.GoogleDrive  => ToGoogleDriveEmbed(url),
        EmbedProvider.Figma        => ToFigmaEmbed(url),
        EmbedProvider.Miro         => ToMiroEmbed(url),
        EmbedProvider.Whimsical    => url,
        EmbedProvider.Framer       => url,
        EmbedProvider.CodePen      => ToCodePenEmbed(url),
        EmbedProvider.CodeSandbox  => ToCodeSandboxEmbed(url),
        EmbedProvider.GitHubGist   => ToGistEmbed(url),
        EmbedProvider.Twitter      => ToTwitterEmbed(url),
        EmbedProvider.Typeform     => url,
        EmbedProvider.Airtable     => url,
        _                          => url
    };

    private static string ToGoogleMapsEmbed(string url)
    {
        // Already an embed URL? Return as-is.
        if (url.Contains("/maps/embed")) return url;
        // https://www.google.com/maps/place/... → embed/v1/place?q=...
        return $"https://www.google.com/maps?output=embed&q={Uri.EscapeDataString(url)}";
    }

    private static string ToGoogleDriveEmbed(string url)
    {
        // https://drive.google.com/file/d/{id}/view → /preview
        if (url.Contains("/view"))
            return url.Replace("/view", "/preview");
        if (url.Contains("/edit"))
            return url.Replace("/edit", "/preview");
        return url;
    }

    private static string ToFigmaEmbed(string url)
    {
        // https://www.figma.com/file/... → https://www.figma.com/embed?embed_host=share&url=...
        if (url.Contains("figma.com/embed")) return url;
        return $"https://www.figma.com/embed?embed_host=share&url={Uri.EscapeDataString(url)}";
    }

    private static string ToMiroEmbed(string url)
    {
        // https://miro.com/app/board/{id}/ → embed with viewOnly=1
        if (url.Contains("/embed/")) return url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
        var path = uri.AbsolutePath; // /app/board/id/
        return $"https://miro.com/app/live-embed{path.Replace("/app/board", string.Empty)}";
    }

    private static string ToCodePenEmbed(string url)
    {
        // https://codepen.io/user/pen/id → /embed/id
        if (url.Contains("/embed/")) return url;
        return url.Replace("/pen/", "/embed/");
    }

    private static string ToCodeSandboxEmbed(string url)
    {
        // https://codesandbox.io/s/id → /embed/id
        if (url.Contains("/embed/")) return url;
        return url.Replace("/s/", "/embed/");
    }

    private static string ToGistEmbed(string url)
    {
        // GitHub Gist: embed via <script> is not possible in iframe; use nbviewer as proxy
        if (!Uri.TryCreate(url, UriKind.Absolute, out _)) return url;
        return url.EndsWith(".js") ? url : url + ".js";
    }

    private static string ToTwitterEmbed(string url)
    {
        // Twitter/X embeds require oEmbed API; return platform.twitter.com tweet player URL
        // https://twitter.com/user/status/id → https://platform.twitter.com/embed/Tweet.html?id=id
        var parts = url.Split('/');
        var idIdx = Array.IndexOf(parts, "status");
        if (idIdx >= 0 && idIdx + 1 < parts.Length)
        {
            var tweetId = parts[idIdx + 1].Split('?')[0];
            return $"https://platform.twitter.com/embed/Tweet.html?id={tweetId}";
        }
        return url;
    }
}
