namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Request for resolving a provider-managed asset to an access URL.</summary>
public sealed class TmFileResolveRequest
{
    /// <summary>Provider-managed asset identifier.</summary>
    public string AssetId { get; set; } = string.Empty;

    /// <summary>Entity that owns or references the asset, when needed by the provider.</summary>
    public TmEntityRef? EntityRef { get; set; }

    /// <summary>Optional purpose, such as "document-image" or "notion-media".</summary>
    public string? Purpose { get; set; }

    /// <summary>True when the caller wants a fresh URL even if a cached one exists.</summary>
    public bool Refresh { get; set; }

    /// <summary>Arbitrary metadata for provider use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
