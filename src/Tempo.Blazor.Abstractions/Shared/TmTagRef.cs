using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Lightweight tag snapshot embedded in shared models.</summary>
public sealed class TmTagRef : ITag
{
    /// <summary>Stable tag identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display label captured at the time of embedding.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional CSS color value or design token.</summary>
    public string? Color { get; set; }

    /// <summary>Optional provider/source discriminator.</summary>
    public string? SourceKey { get; set; }

    /// <summary>Optional tenant, workspace, or application scope identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>Creates a tag reference from a plain label.</summary>
    /// <param name="label">Display label and fallback identifier.</param>
    /// <param name="color">Optional CSS color value or design token.</param>
    /// <param name="sourceKey">Optional provider/source discriminator.</param>
    public static TmTagRef FromLabel(string label, string? color = null, string? sourceKey = null)
    {
        var normalized = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
        return new TmTagRef
        {
            Id = normalized,
            Label = normalized,
            Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim(),
            SourceKey = string.IsNullOrWhiteSpace(sourceKey) ? null : sourceKey.Trim()
        };
    }

    /// <summary>Returns true when required identity fields are populated.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(Id)
        && !string.IsNullOrWhiteSpace(Label);

    string ITag.Name => Label;

    string ITag.Color => Color ?? string.Empty;
}
