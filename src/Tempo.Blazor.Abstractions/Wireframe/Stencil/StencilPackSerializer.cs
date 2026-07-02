using System.Text.Json;

namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Serializes and deserializes stencil packs to the canonical JSON contract.</summary>
public static class StencilPackSerializer
{
    public static string Serialize(StencilPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return JsonSerializer.Serialize(pack, StencilJsonOptions.Default);
    }

    public static StencilPack Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var pack = JsonSerializer.Deserialize<StencilPack>(json, StencilJsonOptions.Default);
        return pack ?? throw new JsonException("Stencil pack JSON deserialized to null.");
    }

    public static bool TryDeserialize(string json, out StencilPack? pack)
    {
        try
        {
            pack = Deserialize(json);
            return true;
        }
        catch (JsonException)
        {
            pack = null;
            return false;
        }
    }
}
