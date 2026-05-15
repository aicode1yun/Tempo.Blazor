namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Mutation record captured by the MutationObserver guard.</summary>
public sealed class WysiwygMutationRecord
{
    /// <summary>Type of mutation: childList, attributes, or characterData.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Node name of the mutated target.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Attribute name if the mutation was an attribute change.</summary>
    public string? AttributeName { get; set; }
}
