namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public static class AudioProviderDetector
{
    public static AudioProvider Detect(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return AudioProvider.Generic;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return AudioProvider.Generic;

        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("soundcloud.com"))
            return AudioProvider.SoundCloud;

        if (host.Contains("spotify.com") || host.Contains("open.spotify.com"))
            return AudioProvider.Spotify;

        return AudioProvider.Generic;
    }
}
