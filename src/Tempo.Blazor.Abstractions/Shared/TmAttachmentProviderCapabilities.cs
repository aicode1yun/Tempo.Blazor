namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Operations supported by an <see cref="ITmAttachmentProvider"/>.</summary>
[Flags]
public enum TmAttachmentProviderCapabilities
{
    /// <summary>No optional operations are supported.</summary>
    None = 0,

    /// <summary>Provider can read entity attachments.</summary>
    Read = 1 << 0,

    /// <summary>Provider can add attachment links to entities.</summary>
    Add = 1 << 1,

    /// <summary>Provider can remove attachment links from entities.</summary>
    Remove = 1 << 2
}
