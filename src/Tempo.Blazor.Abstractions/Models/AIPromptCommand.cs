namespace Tempo.Blazor.Models;

/// <summary>
/// Represents a predefined command shown as a quick-action button in the AI prompt component.
/// </summary>
public sealed record AIPromptCommand
{
    /// <summary>Unique identifier for the command.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display text of the command button.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Optional icon name (from the icon registry).</summary>
    public string? Icon { get; init; }

    /// <summary>Optional longer description shown as a tooltip or subtitle.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the command button is disabled.</summary>
    public bool IsDisabled { get; init; }

    public AIPromptCommand() { }

    public AIPromptCommand(string id, string title, string? icon = null, string? description = null, bool isDisabled = false)
    {
        Id = id;
        Title = title;
        Icon = icon;
        Description = description;
        IsDisabled = isDisabled;
    }
}
