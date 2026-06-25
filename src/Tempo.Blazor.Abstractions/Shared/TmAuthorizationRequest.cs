namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Authorization request for an action against a shared entity reference.</summary>
public sealed class TmAuthorizationRequest
{
    /// <summary>User requesting the action, or null for an anonymous caller.</summary>
    public TmUserRef? User { get; set; }

    /// <summary>Group identifiers associated with the requesting user.</summary>
    public IReadOnlyList<string> GroupIds { get; set; } = [];

    /// <summary>Action being requested, for example <c>view</c>, <c>comment</c>, or <c>edit</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Entity targeted by the request.</summary>
    public TmEntityRef EntityRef { get; set; } = new();

    /// <summary>Optional provider-specific metadata.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Returns true when the request has enough information to be evaluated.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(Action)
        && EntityRef.IsValid;

    /// <summary>Creates a normalized authorization request.</summary>
    /// <param name="user">User requesting the action, or null for anonymous.</param>
    /// <param name="action">Action being requested.</param>
    /// <param name="entityRef">Target entity reference.</param>
    /// <param name="groupIds">Optional group identifiers for the user.</param>
    /// <param name="metadata">Optional provider-specific metadata.</param>
    public static TmAuthorizationRequest Create(
        TmUserRef? user,
        string action,
        TmEntityRef entityRef,
        IEnumerable<string>? groupIds = null,
        Dictionary<string, object>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(entityRef);

        return new TmAuthorizationRequest
        {
            User = user,
            Action = string.IsNullOrWhiteSpace(action) ? string.Empty : action.Trim(),
            EntityRef = entityRef.Normalize(),
            GroupIds = groupIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [],
            Metadata = metadata
        };
    }
}
