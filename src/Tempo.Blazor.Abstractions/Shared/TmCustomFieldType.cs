namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Supported shared custom field value types.</summary>
public enum TmCustomFieldType
{
    /// <summary>Single-line or short free text.</summary>
    Text,

    /// <summary>Numeric value serialized by providers using their preferred format.</summary>
    Number,

    /// <summary>Date or date-time value.</summary>
    Date,

    /// <summary>Single value selected from predefined options.</summary>
    List,

    /// <summary>Boolean checkbox value.</summary>
    Checkbox,

    /// <summary>CSS color value or design token.</summary>
    Color,

    /// <summary>Multiple values selected from predefined options.</summary>
    Multiselect,

    /// <summary>One or more user references.</summary>
    People,

    /// <summary>One or more label/tag values.</summary>
    Labels
}
