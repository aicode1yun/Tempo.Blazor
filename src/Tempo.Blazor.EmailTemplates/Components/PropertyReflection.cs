using System.Reflection;

namespace Tempo.Blazor.EmailTemplates.Components;

/// <summary>The kind of editor a property maps to.</summary>
public enum PropertyFieldKind
{
    /// <summary>Single-line text.</summary>
    Text,

    /// <summary>Multi-line text (e.g. HTML content, CSS).</summary>
    MultilineText,

    /// <summary>A boolean toggle.</summary>
    Bool,

    /// <summary>An integer number.</summary>
    Number,
}

/// <summary>A single editable scalar property of a model object, with reflective get/set.</summary>
public sealed class PropertyField
{
    private readonly object _target;
    private readonly PropertyInfo _property;

    internal PropertyField(object target, PropertyInfo property, PropertyFieldKind kind)
    {
        _target = target;
        _property = property;
        Kind = kind;
    }

    /// <summary>Gets the property name (also used as the stable editor identifier).</summary>
    public string Name => _property.Name;

    /// <summary>Gets the editor kind.</summary>
    public PropertyFieldKind Kind { get; }

    /// <summary>Gets the current value as text (empty when null).</summary>
    public string GetText() => _property.GetValue(_target)?.ToString() ?? string.Empty;

    /// <summary>Sets the text value (stores null for empty on nullable string properties).</summary>
    public void SetText(string? value)
    {
        var isNullable = Nullable.GetUnderlyingType(_property.PropertyType) is not null
            || _property.PropertyType == typeof(string);
        _property.SetValue(_target, string.IsNullOrEmpty(value) && isNullable ? null : value);
    }

    /// <summary>Gets the current boolean value.</summary>
    public bool GetBool() => _property.GetValue(_target) is true;

    /// <summary>Sets the boolean value.</summary>
    public void SetBool(bool value) => _property.SetValue(_target, value);

    /// <summary>Gets the current number value.</summary>
    public int? GetNumber() => _property.GetValue(_target) as int?;

    /// <summary>Sets the number value.</summary>
    public void SetNumber(int? value)
    {
        if (_property.PropertyType == typeof(int)) _property.SetValue(_target, value ?? 0);
        else _property.SetValue(_target, value);
    }
}

/// <summary>Enumerates the editable scalar properties of email model objects via reflection.</summary>
public static class PropertyReflection
{
    private static readonly HashSet<string> Excluded = new(StringComparer.Ordinal)
    {
        "Id", "Type", "MjClasses", "ExtraAttributes",
    };

    private static readonly HashSet<string> Multiline = new(StringComparer.Ordinal) { "Content", "Css" };

    /// <summary>Returns the editable scalar fields of the target object (strings, bools, integers).</summary>
    public static IReadOnlyList<PropertyField> GetFields(object target)
    {
        var fields = new List<PropertyField>();
        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || Excluded.Contains(property.Name)) continue;

            var type = property.PropertyType;
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            PropertyFieldKind? kind = underlying switch
            {
                _ when underlying == typeof(string) =>
                    Multiline.Contains(property.Name) ? PropertyFieldKind.MultilineText : PropertyFieldKind.Text,
                _ when underlying == typeof(bool) => PropertyFieldKind.Bool,
                _ when underlying == typeof(int) => PropertyFieldKind.Number,
                _ => null,
            };

            if (kind is { } k) fields.Add(new PropertyField(target, property, k));
        }
        return fields;
    }
}
