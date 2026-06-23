using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Blazor.Reporting.Models;

/// <summary>Parameter definition plus resolved viewer options.</summary>
public sealed record ReportViewerParameterMetadata
{
    /// <summary>Creates empty metadata.</summary>
    public ReportViewerParameterMetadata()
    {
    }

    /// <summary>Creates metadata for a definition.</summary>
    public ReportViewerParameterMetadata(
        ReportParameterDefinition definition,
        IReadOnlyList<ReportViewerParameterOption>? options = null)
    {
        Definition = definition;
        Options = options?.ToArray() ?? [];
    }

    /// <summary>Parameter definition.</summary>
    public ReportParameterDefinition Definition { get; init; } = new();

    /// <summary>Resolved available values.</summary>
    public IReadOnlyList<ReportViewerParameterOption> Options { get; init; } = [];

    /// <summary>True when options are loaded from a data set and may depend on previous parameter values.</summary>
    public bool IsCascading { get; init; }
}
