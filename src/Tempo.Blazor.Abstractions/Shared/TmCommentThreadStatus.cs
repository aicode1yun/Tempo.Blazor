namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Status of a shared comment thread.</summary>
public enum TmCommentThreadStatus
{
    /// <summary>The thread is active and still needs attention.</summary>
    Open,

    /// <summary>The thread has been resolved.</summary>
    Resolved
}
