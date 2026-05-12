namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Option for select, radio, or multiple-choice signing fields.</summary>
public class SigningFieldOption
{
    /// <summary>Stable option identifier.</summary>
    public string Uuid { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Localized display labels for this option.</summary>
    public SigningLocalizedText Labels { get; set; } = new();

    /// <summary>Displayed and submitted option value.</summary>
    public string Value { get; set; } = string.Empty;
}
