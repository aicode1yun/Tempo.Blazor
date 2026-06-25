namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Operations supported by an <see cref="ITmCommentProvider"/>.</summary>
[Flags]
public enum TmCommentProviderCapabilities
{
    /// <summary>No optional operations are supported.</summary>
    None = 0,

    /// <summary>Provider can read comment threads.</summary>
    Read = 1 << 0,

    /// <summary>Provider can create comment threads.</summary>
    CreateThread = 1 << 1,

    /// <summary>Provider can append replies.</summary>
    Reply = 1 << 2,

    /// <summary>Provider can edit existing entries.</summary>
    EditEntry = 1 << 3,

    /// <summary>Provider can delete threads or entries.</summary>
    Delete = 1 << 4,

    /// <summary>Provider can resolve and reopen threads.</summary>
    Resolve = 1 << 5,

    /// <summary>Provider supports entry reactions.</summary>
    Reactions = 1 << 6,

    /// <summary>Provider supports read tracking.</summary>
    ReadTracking = 1 << 7,

    /// <summary>Provider supports thread subscriptions.</summary>
    Subscriptions = 1 << 8,

    /// <summary>Provider accepts rich text or HTML bodies.</summary>
    RichText = 1 << 9
}
