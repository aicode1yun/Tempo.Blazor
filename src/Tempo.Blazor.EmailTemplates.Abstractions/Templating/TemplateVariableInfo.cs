namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>Whether a template variable is used as a single value or iterated as a collection.</summary>
public enum VariableKind
{
    /// <summary>A single scalar/object value.</summary>
    Scalar,

    /// <summary>A collection iterated by a <c>for</c> loop.</summary>
    Collection,
}

/// <summary>A variable referenced by a template, with its dotted path and inferred kind.</summary>
/// <param name="Path">The dotted variable path (e.g. <c>user.address.city</c>).</param>
/// <param name="Kind">Whether the variable is iterated as a collection.</param>
public sealed record TemplateVariableInfo(string Path, VariableKind Kind);
