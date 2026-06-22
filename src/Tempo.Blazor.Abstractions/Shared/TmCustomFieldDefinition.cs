namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Shared definition for a user-defined field that can be attached to entities.</summary>
public sealed class TmCustomFieldDefinition
{
    /// <summary>Stable custom field identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Value type.</summary>
    public TmCustomFieldType Type { get; set; } = TmCustomFieldType.Text;

    /// <summary>Predefined option values for list, multiselect, and label fields.</summary>
    public List<string> Options { get; set; } = [];

    /// <summary>Whether consumers should require a value before saving.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Entity types this field applies to. Empty means all entity types.</summary>
    public List<string> AppliesToEntityTypes { get; set; } = [];

    /// <summary>Optional provider/source discriminator.</summary>
    public string? SourceKey { get; set; }

    /// <summary>Optional longer description.</summary>
    public string? Description { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Returns true when required identity fields are populated.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(Id)
        && !string.IsNullOrWhiteSpace(Name);

    /// <summary>Returns true when the definition applies to the provided entity type.</summary>
    /// <param name="entityType">Logical entity type, for example <c>work-item</c>.</param>
    public bool AppliesTo(string? entityType)
        => AppliesToEntityTypes.Count == 0
        || (!string.IsNullOrWhiteSpace(entityType)
            && AppliesToEntityTypes.Any(value => string.Equals(value, entityType, StringComparison.OrdinalIgnoreCase)));
}
