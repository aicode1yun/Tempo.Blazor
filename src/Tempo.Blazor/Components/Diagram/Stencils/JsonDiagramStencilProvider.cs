using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Stencils;

/// <summary>Loads diagram stencil libraries from JSON sources.</summary>
public sealed class JsonDiagramStencilProvider : IDiagramStencilProvider
{
    private readonly List<JsonDiagramStencilLibrarySource> _sources;
    private readonly Dictionary<string, DiagramStencilSet> _loadedSets = new(StringComparer.Ordinal);
    private bool _requiredSourcesLoaded;

    /// <summary>Creates a provider with JSON library sources.</summary>
    public JsonDiagramStencilProvider(IEnumerable<JsonDiagramStencilLibrarySource> sources, int priority = 50)
    {
        _sources = sources?.ToList() ?? throw new ArgumentNullException(nameof(sources));
        Priority = priority;
    }

    /// <inheritdoc />
    public int Priority { get; }

    /// <inheritdoc />
    public IEnumerable<DiagramStencilSet> GetStencilSets()
    {
        EnsureRequiredSourcesLoaded();
        return _loadedSets.Values
            .OrderBy(set => set.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Loads an optional source and makes its stencils available from <see cref="GetStencilSets"/>.</summary>
    public void LoadOptionalLibrary(string sourceId)
    {
        var source = _sources.FirstOrDefault(s =>
            s.IsOptional && string.Equals(s.SourceId, sourceId, StringComparison.Ordinal));

        if (source is null)
            throw new InvalidOperationException("Optional diagram stencil library source was not found.");

        LoadSource(source);
    }

    private void EnsureRequiredSourcesLoaded()
    {
        if (_requiredSourcesLoaded)
            return;

        foreach (var source in _sources.Where(s => !s.IsOptional))
            LoadSource(source);

        _requiredSourcesLoaded = true;
    }

    private void LoadSource(JsonDiagramStencilLibrarySource source)
    {
        var library = DeserializeLibrary(source.LoadJson());
        var validation = DiagramStencilLibraryValidator.Validate(library);
        if (!validation.IsValid)
            throw new DiagramStencilLibraryValidationException(validation.Errors);

        var set = CreateStencilSet(library);
        _loadedSets[set.Id] = set;
    }

    private static DiagramStencilLibrary DeserializeLibrary(string json)
    {
        try
        {
            var library = JsonSerializer.Deserialize<DiagramStencilLibrary>(json, DiagramJsonOptions.Default);
            if (library is not null)
                return library;
        }
        catch (JsonException)
        {
        }

        throw new DiagramStencilLibraryValidationException(
        [
            new DiagramStencilLibraryValidationError
            {
                Code = DiagramStencilLibraryValidationErrorCodes.InvalidJson,
                Path = "$"
            }
        ]);
    }

    private static DiagramStencilSet CreateStencilSet(DiagramStencilLibrary library)
    {
        var stencils = new List<DiagramStencil>();

        foreach (var palette in library.Palettes.OrderBy(p => p.Order).ThenBy(p => p.PaletteId, StringComparer.Ordinal))
        {
            for (var stencilIndex = 0; stencilIndex < palette.Stencils.Count; stencilIndex++)
            {
                var stencil = palette.Stencils[stencilIndex];
                NormalizeStencil(library, palette, stencil, stencilIndex);
                stencils.Add(stencil);
            }
        }

        return new DiagramStencilSet
        {
            Id = library.SetId,
            Name = library.NameResourceKey,
            NameResourceKey = library.NameResourceKey,
            Stencils = stencils
                .OrderBy(stencil => stencil.PaletteId, StringComparer.Ordinal)
                .ThenBy(stencil => stencil.Order)
                .ThenBy(stencil => stencil.NameResourceKey, StringComparer.Ordinal)
                .ToList()
        };
    }

    private static void NormalizeStencil(
        DiagramStencilLibrary library,
        DiagramStencilPalette palette,
        DiagramStencil stencil,
        int stencilIndex)
    {
        if (string.IsNullOrWhiteSpace(stencil.SetId))
            stencil.SetId = library.SetId;

        if (string.IsNullOrWhiteSpace(stencil.SetNameResourceKey))
            stencil.SetNameResourceKey = library.NameResourceKey;

        if (string.IsNullOrWhiteSpace(stencil.PaletteId))
            stencil.PaletteId = palette.PaletteId;

        if (string.IsNullOrWhiteSpace(stencil.PaletteNameResourceKey))
            stencil.PaletteNameResourceKey = palette.NameResourceKey;

        if (stencil.PaletteOrder == 0)
            stencil.PaletteOrder = palette.Order;

        if (string.IsNullOrWhiteSpace(stencil.Name))
            stencil.Name = stencil.NameResourceKey;

        if (string.IsNullOrWhiteSpace(stencil.Category))
            stencil.Category = library.SetId;

        if (stencil.Order == 0)
            stencil.Order = palette.Order + stencilIndex;

        stencil.Tags ??= [];
        stencil.Keywords ??= [];
        stencil.Ports ??= [];
        stencil.ConnectionPoints ??= [];
        stencil.DefaultData ??= [];
        stencil.Layout ??= new();
    }
}
