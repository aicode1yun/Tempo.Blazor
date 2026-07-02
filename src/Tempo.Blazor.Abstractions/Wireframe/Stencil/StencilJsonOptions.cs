using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Shared <see cref="JsonSerializerOptions"/> for stencil pack serialization.</summary>
public static class StencilJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new StencilJsonValueConverter(),
            new RenderNodeConverter()
        }
    };
}
