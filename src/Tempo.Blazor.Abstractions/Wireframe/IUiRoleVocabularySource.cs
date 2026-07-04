namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Supplies UI role definitions to <see cref="UiRoleVocabulary"/>.
/// Implement this interface to extend the built-in role vocabulary with
/// product-specific concepts or synonyms.
/// </summary>
public interface IUiRoleVocabularySource
{
    /// <summary>Unique identifier for this vocabulary source.</summary>
    string SourceId { get; }

    /// <summary>
    /// Higher priority wins when duplicate role slugs provide display metadata.
    /// Synonyms are always merged across sources.
    /// </summary>
    int Priority { get; }

    /// <summary>Returns all UI role definitions supplied by this source.</summary>
    IEnumerable<UiRoleDefinition> GetRoles();
}
