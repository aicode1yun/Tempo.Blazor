namespace Tempo.Blazor.Modeling;

/// <summary>
/// Projects a semantic <see cref="ModelingModelDto"/> into a diagram document (plus non-blocking
/// issues), enforcing notation and viewpoint rules. Abstraction over the modeling package's
/// <c>ModelingDiagramGenerator</c> so headless callers (MCP tools) can request a rendered view
/// without referencing the Blazor component package.
/// </summary>
public interface IModelingDiagramProjector
{
    /// <summary>Generates a diagram projection of <paramref name="model"/> for the supplied options.</summary>
    /// <param name="model">Model to project.</param>
    /// <param name="options">Optional view/viewpoint/layout options.</param>
    ModelingDiagramGenerationResultDto Generate(
        ModelingModelDto model,
        ModelingDiagramGenerationOptionsDto? options = null);
}
