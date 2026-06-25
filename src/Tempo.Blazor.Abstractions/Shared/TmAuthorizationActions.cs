namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Common action names used by <see cref="ITmAuthorizationProvider"/> requests.</summary>
public static class TmAuthorizationActions
{
    /// <summary>View or read an entity.</summary>
    public const string View = "view";

    /// <summary>Create a child entity or new entity in a container.</summary>
    public const string Create = "create";

    /// <summary>Update an entity.</summary>
    public const string Update = "update";

    /// <summary>Edit an entity. Equivalent to update for interactive editors.</summary>
    public const string Edit = "edit";

    /// <summary>Add or manage comments on an entity.</summary>
    public const string Comment = "comment";

    /// <summary>Delete an entity.</summary>
    public const string Delete = "delete";

    /// <summary>Manage access rules for an entity.</summary>
    public const string ManagePermissions = "manage-permissions";
}
