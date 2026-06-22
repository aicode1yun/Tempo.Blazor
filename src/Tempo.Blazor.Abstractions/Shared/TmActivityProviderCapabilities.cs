namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Operations supported by an <see cref="ITmActivityProvider"/>.</summary>
[Flags]
public enum TmActivityProviderCapabilities
{
    /// <summary>No optional operations are supported.</summary>
    None = 0,

    /// <summary>Provider can read activity entries for a specific entity.</summary>
    Read = 1 << 0,

    /// <summary>Provider can query activity entries across entities.</summary>
    Query = 1 << 1,

    /// <summary>Provider can append new activity entries.</summary>
    Append = 1 << 2
}
