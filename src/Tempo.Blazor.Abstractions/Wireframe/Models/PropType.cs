namespace Tempo.Blazor.Components.Wireframe.Models;

/// <summary>Data type of a wireframe component property.</summary>
public enum PropType
{
    /// <summary>Plain text string.</summary>
    String,
    /// <summary>Integer number.</summary>
    Int,
    /// <summary>Floating-point number.</summary>
    Double,
    /// <summary>Boolean (true/false).</summary>
    Bool,
    /// <summary>Fixed set of string options – use <see cref="PropDef.Options"/>.</summary>
    Enum,
    /// <summary>CSS color value (hex, rgb, etc.).</summary>
    Color,
    /// <summary>TmIcon name string.</summary>
    Icon,
    /// <summary>List of strings (comma-separated in UI, JSON array in storage).</summary>
    StringList,
    /// <summary>Arbitrary JSON object.</summary>
    Object
}
