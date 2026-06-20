using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Block content for an embedded external work item.</summary>
public interface IWorkItemBlockContent : IBlockContent
{
    /// <summary>Provider discriminator used for registry lookup.</summary>
    string ProviderKey { get; }

    /// <summary>Provider-native identifier.</summary>
    string ExternalId { get; }

    /// <summary>Last known snapshot used when live refresh fails.</summary>
    WorkItemDto? CachedSnapshot { get; }

    /// <summary>Current display variant.</summary>
    WorkItemDisplayMode DisplayMode { get; }
}
