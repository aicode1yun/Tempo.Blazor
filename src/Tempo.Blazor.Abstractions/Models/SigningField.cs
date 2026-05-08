namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Field definition used by signing templates and submission snapshots.</summary>
public class SigningField
{
    /// <summary>Stable field identifier.</summary>
    public string Uuid { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Signer role identifier this field belongs to.</summary>
    public string? SubmitterUuid { get; set; }

    /// <summary>Internal field name.</summary>
    public string? Name { get; set; }

    /// <summary>Optional user-facing title shown during signing.</summary>
    public string? Title { get; set; }

    /// <summary>Optional user-facing field description.</summary>
    public string? Description { get; set; }

    /// <summary>Field type.</summary>
    public SigningFieldType Type { get; set; } = SigningFieldType.Text;

    /// <summary>Whether the signer must provide a value.</summary>
    public bool Required { get; set; }

    /// <summary>Whether the field value is read-only for the signer.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Whether the field can be prefilled before sending.</summary>
    public bool Prefillable { get; set; }

    /// <summary>Default field value. The concrete type depends on the field type.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Display and behavior preferences.</summary>
    public SigningFieldPreferences Preferences { get; set; } = new();

    /// <summary>Optional validation settings.</summary>
    public SigningFieldValidation? Validation { get; set; }

    /// <summary>Conditional rules controlling this field.</summary>
    public List<SigningFieldCondition> Conditions { get; set; } = [];

    /// <summary>Choice options for select, radio, and multiple-choice fields.</summary>
    public List<SigningFieldOption> Options { get; set; } = [];

    /// <summary>Document areas where this field appears.</summary>
    public List<SigningFieldArea> Areas { get; set; } = [];
}
