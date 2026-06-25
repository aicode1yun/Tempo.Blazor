namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Operations supported by an <see cref="ITmFileProvider"/>.</summary>
[Flags]
public enum TmFileProviderCapabilities
{
    /// <summary>No optional operations are supported.</summary>
    None = 0,

    /// <summary>Provider can upload complete streams.</summary>
    Upload = 1 << 0,

    /// <summary>Provider can resolve asset ids to URLs or access tickets.</summary>
    Resolve = 1 << 1,

    /// <summary>Provider can delete assets.</summary>
    Delete = 1 << 2,

    /// <summary>Provider supports draft assets that are committed later.</summary>
    DraftAssets = 1 << 3,

    /// <summary>Provider can commit draft assets.</summary>
    CommitDraftAssets = 1 << 4,

    /// <summary>Provider returns short-lived or signed URLs.</summary>
    SignedUrls = 1 << 5,

    /// <summary>Provider can refresh short-lived URLs.</summary>
    RefreshUrls = 1 << 6,

    /// <summary>Provider supports chunked upload.</summary>
    ChunkUpload = 1 << 7
}
