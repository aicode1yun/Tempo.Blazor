using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Shared tag definition used across Tempo components.</summary>
public sealed class TmTag : ITag
{
    /// <summary>Stable tag identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional CSS color value or design token.</summary>
    public string? Color { get; set; }

    /// <summary>Optional longer description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional provider/source discriminator.</summary>
    public string? SourceKey { get; set; }

    /// <summary>Optional tenant, workspace, or application scope identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Returns true when required identity fields are populated.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(Id)
        && !string.IsNullOrWhiteSpace(Label);

    /// <summary>Creates a lightweight reference snapshot.</summary>
    public TmTagRef ToRef()
        => new()
        {
            Id = Id,
            Label = Label,
            Color = Color,
            SourceKey = SourceKey,
            TenantId = TenantId
        };

    string ITag.Name => Label;

    string ITag.Color => Color ?? string.Empty;
}
