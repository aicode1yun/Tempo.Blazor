namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Provider-agnostic external work item snapshot.</summary>
public sealed class WorkItemDto
{
    /// <summary>Provider discriminator such as demo, github, jira, or azure-devops.</summary>
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>Provider-native identifier such as DEMO-101 or issue number.</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Absolute URL to the source work item.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>User-visible work item title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Provider-native status label.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Status color token or sanitized CSS color supplied by the provider.</summary>
    public string? StatusColor { get; set; }

    /// <summary>Provider-native work item type label.</summary>
    public string? TypeLabel { get; set; }

    /// <summary>Optional icon URL for the work item type.</summary>
    public string? TypeIconUrl { get; set; }

    /// <summary>Assigned person display name, when available.</summary>
    public string? AssigneeDisplayName { get; set; }

    /// <summary>Provider-native priority label, when available.</summary>
    public string? Priority { get; set; }

    /// <summary>Last updated timestamp reported by the provider.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Opaque provider-specific fields shown only by consumers that understand them.</summary>
    public Dictionary<string, string> Fields { get; set; } = [];
}
