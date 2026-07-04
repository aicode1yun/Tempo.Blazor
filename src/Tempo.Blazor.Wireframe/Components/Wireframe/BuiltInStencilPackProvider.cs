using System.Reflection;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>Provides built-in Tempo stencil-pack definitions backed by trusted native renderers.</summary>
public sealed class BuiltInStencilPackProvider : IWireframeComponentProvider
{
    private const string ResourceSuffix = ".StencilPacks.tempo.stencil.json";

    private static readonly Lazy<string> PackJson = new(ReadEmbeddedPackJson);
    private static readonly Lazy<StencilPack> Pack = new(LoadAndValidatePack);
    private static readonly Lazy<IReadOnlyList<WireframeComponentDef>> Definitions = new(CompileDefinitions);

    /// <inheritdoc/>
    public string ProviderId => "TempoBuiltInPack";

    /// <inheritdoc/>
    public int Priority => 0;

    /// <inheritdoc/>
    public IEnumerable<WireframeComponentDef> GetDefinitions() => Definitions.Value;

    internal static string ReadPackJson() => PackJson.Value;

    internal static StencilPack LoadPack() => Pack.Value;

    private static IReadOnlyList<WireframeComponentDef> CompileDefinitions()
    {
        var schemaByType = new BuiltInComponentSchemas()
            .GetSchemas()
            .ToDictionary(schema => schema.Type, StringComparer.Ordinal);

        return new StencilPackCompiler(NativeRendererRegistry.TempoBuiltIn)
            .Compile(LoadPack())
            .Select(def => schemaByType.TryGetValue(def.Type, out var schema)
                ? WithSchemaMetadata(def, schema)
                : def)
            .ToArray();
    }

    private static StencilPack LoadAndValidatePack()
    {
        var pack = StencilPackSerializer.Deserialize(ReadPackJson());
        ValidatePack(pack);
        return pack;
    }

    private static void ValidatePack(StencilPack pack)
    {
        if (!string.Equals(pack.Format, "tempo-stencil", StringComparison.Ordinal))
            throw new InvalidDataException("Built-in Tempo stencil pack has an unsupported format.");

        if (pack.FormatVersion != 1)
            throw new InvalidDataException("Built-in Tempo stencil pack has an unsupported format version.");

        if (!string.Equals(pack.Id, "tempo", StringComparison.Ordinal)
            || !string.Equals(pack.Namespace, "tempo", StringComparison.Ordinal)
            || !pack.IsBuiltIn)
        {
            throw new InvalidDataException("Built-in Tempo stencil pack must use id/namespace 'tempo' and isBuiltIn=true.");
        }

        if (pack.Components.Count == 0)
            throw new InvalidDataException("Built-in Tempo stencil pack must contain components.");

        var invalidComponent = pack.Components.FirstOrDefault(component =>
            component.Native is { } native
                ? !string.Equals(component.Type, native.NativeType, StringComparison.Ordinal)
                : component.Render is null);
        if (invalidComponent is not null)
        {
            throw new InvalidDataException(
                $"Built-in Tempo stencil component '{invalidComponent.Type}' must either render declaratively or map to a same-named native renderer.");
        }
    }

    private static WireframeComponentDef WithSchemaMetadata(
        WireframeComponentDef def,
        WireframeComponentSchema schema)
        => new()
        {
            Type = def.Type,
            ScopeAppId = def.ScopeAppId,
            LocalType = def.LocalType,
            Category = schema.Category,
            DisplayName = schema.DisplayName,
            Roles = schema.Roles ?? def.Roles,
            Icon = def.Icon,
            DefaultWidth = schema.DefaultWidth,
            DefaultHeight = schema.DefaultHeight,
            Props = schema.Props,
            RenderSvg = def.RenderSvg,
            IsBuiltIn = def.IsBuiltIn,
            PackId = def.PackId,
            NativeType = def.NativeType,
            Impl = def.Impl,
            SizePresets = schema.SizePresets,
        };

    private static string ReadEmbeddedPackJson()
    {
        var assembly = typeof(BuiltInStencilPackProvider).Assembly;
        var matches = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new FileNotFoundException(
                $"Expected exactly one embedded Tempo stencil pack resource ending with '{ResourceSuffix}', found {matches.Length}.");
        }

        using var stream = assembly.GetManifestResourceStream(matches[0])
            ?? throw new FileNotFoundException($"Embedded Tempo stencil pack resource '{matches[0]}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
