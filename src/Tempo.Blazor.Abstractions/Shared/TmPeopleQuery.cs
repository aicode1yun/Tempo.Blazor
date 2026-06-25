namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Query options for resolving users from an <see cref="ITmPeopleProvider"/>.</summary>
public sealed class TmPeopleQuery
{
    /// <summary>Free-text search matched against display name, username, or e-mail.</summary>
    public string? SearchText { get; set; }

    /// <summary>Specific user ids to resolve. When non-empty, providers should return these users regardless of <see cref="SearchText"/>.</summary>
    public IReadOnlyList<string> Ids { get; set; } = [];

    /// <summary>Includes virtual users/resources when true.</summary>
    public bool IncludeVirtual { get; set; } = true;

    /// <summary>Maximum number of users to return.</summary>
    public int Take { get; set; } = 20;
}
