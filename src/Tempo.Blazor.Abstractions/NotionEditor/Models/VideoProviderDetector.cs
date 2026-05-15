namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public static class VideoProviderDetector
{
    public static VideoProvider Detect(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return VideoProvider.Generic;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return VideoProvider.Generic;

        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("youtube.com") || host.Contains("youtu.be"))
            return VideoProvider.YouTube;

        if (host.Contains("vimeo.com"))
            return VideoProvider.Vimeo;

        if (host.Contains("loom.com"))
            return VideoProvider.Loom;

        return VideoProvider.Generic;
    }
}
