namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>Whether a template variable is used as a single value or iterated as a collection.</summary>
public enum VariableKind
{
    /// <summary>A single scalar/object value.</summary>
    Scalar,

    /// <summary>A collection iterated by a <c>for</c> loop.</summary>
    Collection,
}
