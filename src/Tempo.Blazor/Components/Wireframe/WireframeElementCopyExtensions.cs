using System.Text.Json;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>Deep-copy extension for <see cref="WireframeElement"/>.</summary>
public static class WireframeElementCopyExtensions
{
    /// <summary>Creates a deep copy of the element with a new Id.</summary>
    public static WireframeElement DeepCopy(this WireframeElement src)
    {
        var json = JsonSerializer.Serialize(src, WireframeJsonOptions.Default);
        var copy = JsonSerializer.Deserialize<WireframeElement>(json, WireframeJsonOptions.Default)!;
        copy.Id = Guid.NewGuid().ToString("N")[..8];
        return copy;
    }
}
