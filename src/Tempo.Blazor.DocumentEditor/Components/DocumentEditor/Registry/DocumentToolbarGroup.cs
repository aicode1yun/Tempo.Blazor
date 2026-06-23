namespace Tempo.Blazor.Components.DocumentEditor.Registry;

public sealed record DocumentToolbarGroup
{
    /// <summary>Stable group identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Logical tab that owns this group.</summary>
    public DocumentToolbarTab Tab { get; init; } = DocumentToolbarTab.Home;

    /// <summary>Backward-compatible tab identifier.</summary>
    public string? TabId { get; init; }

    /// <summary>Localization key for the group label.</summary>
    public string? LabelKey { get; init; }

    /// <summary>Sort order inside the tab.</summary>
    public int Order { get; init; }
}
