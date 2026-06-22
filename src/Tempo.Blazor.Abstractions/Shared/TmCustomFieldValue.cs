namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Shared custom field value linked to an entity.</summary>
public sealed class TmCustomFieldValue
{
    /// <summary>Identifier of the custom field definition.</summary>
    public string DefinitionId { get; set; } = string.Empty;

    /// <summary>Entity this value belongs to.</summary>
    public TmEntityRef EntityRef { get; set; } = new();

    /// <summary>Serialized field value. Consumers interpret it according to the field definition type.</summary>
    public string? Value { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Returns true when required identity fields are populated.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(DefinitionId)
        && EntityRef.IsValid;
}
