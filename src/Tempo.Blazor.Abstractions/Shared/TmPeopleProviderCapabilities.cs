namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Operations supported by an <see cref="ITmPeopleProvider"/>.</summary>
[Flags]
public enum TmPeopleProviderCapabilities
{
    /// <summary>No people operations are supported.</summary>
    None = 0,

    /// <summary>Provider can search users by free text.</summary>
    Search = 1 << 0,

    /// <summary>Provider can resolve users by stable id.</summary>
    Resolve = 1 << 1,

    /// <summary>Provider supports the usual read operations.</summary>
    Read = Search | Resolve,

    /// <summary>Provider supports every people operation currently defined.</summary>
    All = Read
}
