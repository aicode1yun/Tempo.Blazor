namespace Tempo.Blazor.Components.Toolbar;

/// <summary>Meaning of the content in the <see cref="TmFormActionBar.Status"/> slot.</summary>
/// <remarks>
/// The slot is generic, so the component cannot know whether the host is reporting a save that
/// succeeded or a validation error. Until 2.8.15 it guessed, and it guessed one way: the slot was
/// painted <c>--tm-color-success-text</c> unconditionally, which turned an error message green —
/// the colour contradicting the words. A host could not fix that from its own stylesheet either,
/// because the rule is scoped and wins the tie.
///
/// The severity is therefore stated, never inferred, and <see cref="None"/> is the default: with no
/// statement the slot inherits its colour and the component asserts nothing about content it did
/// not author.
/// </remarks>
public enum FormActionBarStatusSeverity
{
    /// <summary>No claim about the content; the slot inherits the surrounding colour.</summary>
    None,

    /// <summary>The operation completed.</summary>
    Success,

    /// <summary>The operation completed, but something needs attention.</summary>
    Warning,

    /// <summary>The operation failed.</summary>
    Error,

    /// <summary>Neutral information, e.g. unsaved changes.</summary>
    Info
}
