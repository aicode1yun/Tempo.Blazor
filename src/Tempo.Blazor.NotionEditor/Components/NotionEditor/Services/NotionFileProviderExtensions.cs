using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Components.NotionEditor.Services;

internal static class NotionFileProviderExtensions
{
    public static async Task<(string AssetId, string Url)> UploadNotionFileAsync(
        this ITmFileProvider provider,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(content);

        var upload = await provider.UploadAsync(
            new TmFileUploadRequest
            {
                FileName = fileName,
                ContentType = contentType,
                Purpose = "notion-media"
            },
            content,
            cancellationToken);

        if (!upload.Success)
            throw new InvalidOperationException(upload.ErrorMessage ?? "File upload failed.");

        var assetId = upload.AssetId;
        if (string.IsNullOrWhiteSpace(assetId))
            throw new InvalidOperationException("File upload did not return an asset id.");

        if (!string.IsNullOrWhiteSpace(upload.Url))
            return (assetId, upload.Url);

        var resolve = await provider.ResolveAsync(
            new TmFileResolveRequest
            {
                AssetId = assetId,
                Purpose = "notion-media"
            },
            cancellationToken);

        if (!resolve.Success || string.IsNullOrWhiteSpace(resolve.Url))
            throw new InvalidOperationException(resolve.ErrorMessage ?? "File URL could not be resolved.");

        return (assetId, resolve.Url);
    }
}
