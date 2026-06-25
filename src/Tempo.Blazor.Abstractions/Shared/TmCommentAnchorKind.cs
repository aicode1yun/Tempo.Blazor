namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Normalized anchor kind for a shared comment thread.</summary>
public enum TmCommentAnchorKind
{
    /// <summary>No structured anchor is available.</summary>
    None,

    /// <summary>The thread targets a logical content block.</summary>
    Block,

    /// <summary>The thread targets a text range.</summary>
    TextRange,

    /// <summary>The thread targets an entire page.</summary>
    Page,

    /// <summary>The thread targets a point on a page.</summary>
    PagePoint,

    /// <summary>The thread targets a rectangular area on a page.</summary>
    PageArea,

    /// <summary>The thread targets an immutable rendition anchor.</summary>
    Rendition,

    /// <summary>The thread targets an external system anchor.</summary>
    External
}
