using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Modeling;

/// <summary>Request passed to a modeling model provider.</summary>
public sealed class ModelingModelRequest
{
    private string _providerKey = string.Empty;
    private string _sourceKind = string.Empty;
    private string _sourceId = string.Empty;
    private string _notation = string.Empty;
    private string _viewpointKey = string.Empty;
    private Dictionary<string, JsonElement> _filterOptions = [];
    private string _culture = string.Empty;

    /// <summary>Provider key used to select a modeling provider.</summary>
    public string ProviderKey
    {
        get => _providerKey;
        set => _providerKey = value ?? string.Empty;
    }

    /// <summary>Kind of source requested from the provider.</summary>
    public string SourceKind
    {
        get => _sourceKind;
        set => _sourceKind = value ?? string.Empty;
    }

    /// <summary>Source identifier requested from the provider.</summary>
    public string SourceId
    {
        get => _sourceId;
        set => _sourceId = value ?? string.Empty;
    }

    /// <summary>Requested modeling notation key.</summary>
    public string Notation
    {
        get => _notation;
        set => _notation = value ?? string.Empty;
    }

    /// <summary>Requested viewpoint key.</summary>
    public string ViewpointKey
    {
        get => _viewpointKey;
        set => _viewpointKey = value ?? string.Empty;
    }

    /// <summary>Provider-specific filter options preserved as JSON values.</summary>
    public Dictionary<string, JsonElement> FilterOptions
    {
        get => _filterOptions;
        set => _filterOptions = value ?? [];
    }

    /// <summary>Requested UI or data culture name.</summary>
    public string Culture
    {
        get => _culture;
        set => _culture = value ?? string.Empty;
    }
}

/// <summary>Provides source-backed modeling models.</summary>
public interface IModelingModelProvider
{
    /// <summary>Unique provider key used by the modeling editor.</summary>
    string ProviderKey { get; }

    /// <summary>Loads a modeling model for the supplied request.</summary>
    /// <param name="request">Model request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded modeling model.</returns>
    Task<ModelingModelDto> GetModelAsync(ModelingModelRequest request, CancellationToken cancellationToken);
}

/// <summary>Options used when generating a diagram document from a modeling model.</summary>
public sealed class ModelingDiagramGenerationOptionsDto
{
    private string _viewId = string.Empty;
    private string _viewpointKey = string.Empty;
    private string _layoutHint = string.Empty;

    /// <summary>Requested modeling view identifier.</summary>
    public string ViewId
    {
        get => _viewId;
        set => _viewId = value ?? string.Empty;
    }

    /// <summary>Requested viewpoint key for generated views.</summary>
    public string ViewpointKey
    {
        get => _viewpointKey;
        set => _viewpointKey = value ?? string.Empty;
    }

    /// <summary>Whether generated modeling issues should be included in the result.</summary>
    public bool IncludeIssues { get; set; } = true;

    /// <summary>Layout hint such as grid, tree, TB, or LR.</summary>
    public string LayoutHint
    {
        get => _layoutHint;
        set => _layoutHint = value ?? string.Empty;
    }
}

/// <summary>Result of generating a diagram document from a modeling model.</summary>
public sealed class ModelingDiagramGenerationResultDto
{
    private List<ModelingIssueDto> _issues = [];

    /// <summary>Generated diagram document. May be null when generation failed before document creation.</summary>
    public DiagramDocument? Document { get; set; }

    /// <summary>Issues discovered during generation.</summary>
    public List<ModelingIssueDto> Issues
    {
        get => _issues;
        set => _issues = value ?? [];
    }

    /// <summary>Timestamp when the result was generated.</summary>
    public DateTimeOffset GeneratedAt { get; set; }
}
