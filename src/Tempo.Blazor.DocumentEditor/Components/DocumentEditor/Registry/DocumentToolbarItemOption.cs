namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>One selectable option of a declarative toolbar select item (Fáze 17).</summary>
/// <param name="Value">Value submitted to the command payload when selected.</param>
/// <param name="Label">Display label; falls back to <paramref name="Value"/> when null.</param>
public sealed record DocumentToolbarItemOption(string Value, string? Label = null)
{
    /// <summary>Label shown in the select; falls back to the raw value.</summary>
    public string EffectiveLabel => Label ?? Value;
}
