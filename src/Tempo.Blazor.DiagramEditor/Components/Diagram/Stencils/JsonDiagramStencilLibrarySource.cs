namespace Tempo.Blazor.Components.Diagram.Stencils;

/// <summary>Descriptor for a JSON stencil library source.</summary>
public sealed class JsonDiagramStencilLibrarySource
{
    private readonly Func<string> _loadJson;

    private JsonDiagramStencilLibrarySource(string sourceId, bool isOptional, Func<string> loadJson)
    {
        SourceId = string.IsNullOrWhiteSpace(sourceId) ? throw new ArgumentException("Source identifier is required.", nameof(sourceId)) : sourceId;
        IsOptional = isOptional;
        _loadJson = loadJson ?? throw new ArgumentNullException(nameof(loadJson));
    }

    /// <summary>Unique source identifier used for optional library activation.</summary>
    public string SourceId { get; }

    /// <summary>Whether this source should be loaded only when requested.</summary>
    public bool IsOptional { get; }

    /// <summary>Creates a source that is loaded together with the provider.</summary>
    public static JsonDiagramStencilLibrarySource Required(string sourceId, Func<string> loadJson)
        => new(sourceId, false, loadJson);

    /// <summary>Creates a source that is loaded only after <see cref="JsonDiagramStencilProvider.LoadOptionalLibrary"/>.</summary>
    public static JsonDiagramStencilLibrarySource Optional(string sourceId, Func<string> loadJson)
        => new(sourceId, true, loadJson);

    /// <summary>Loads the JSON payload for this source.</summary>
    public string LoadJson() => _loadJson();
}
