namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Visibility scope for a comment thread.</summary>
public enum TmCommentVisibility
{
    /// <summary>Default internal application visibility.</summary>
    Internal,

    /// <summary>Visible to external collaborators.</summary>
    External,

    /// <summary>Visible to client users.</summary>
    Client,

    /// <summary>Visible in public or shared outputs.</summary>
    Public
}
