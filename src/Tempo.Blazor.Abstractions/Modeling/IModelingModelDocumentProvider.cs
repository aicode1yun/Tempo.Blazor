namespace Tempo.Blazor.Modeling;

/// <summary>
/// Host contract for reading and persisting stored modeling models (<see cref="ModelingModelDto"/>)
/// by id, so MCP tooling can list / read / mutate architecture models the same way the diagram and
/// wireframe suites do. Mirrors <c>IDiagramDocumentProvider</c>. The read-only, source-backed
/// <see cref="IModelingModelProvider"/> (editor loading path) is unaffected.
/// </summary>
/// <remarks>
/// The document library (<c>ITempoDocumentLibraryProvider</c> keyed by
/// <c>TempoDocumentKind.Modeling</c>) supplies listing and the optimistic-concurrency token
/// (<c>ModifiedAt</c>); this provider supplies the model payloads.
/// </remarks>
public interface IModelingModelDocumentProvider
{
    /// <summary>Returns the stored model, or null when it does not exist.</summary>
    Task<ModelingModelDto?> GetModelingModelDocumentAsync(Guid documentId);

    /// <summary>Persists the model and returns the saved instance.</summary>
    Task<ModelingModelDto> SaveModelingModelDocumentAsync(Guid documentId, ModelingModelDto model);

    /// <summary>Creates a new empty model with the supplied title, returning its id and payload.</summary>
    Task<(Guid Id, ModelingModelDto Document)> CreateModelingModelDocumentAsync(string title);

    /// <summary>
    /// Creates a model scoped to a specific application. <paramref name="scopeAppId"/> (GUID string)
    /// lets multi-app hosts disambiguate the target app for stateless callers such as MCP tools.
    /// The default implementation ignores the scope and delegates to
    /// <see cref="CreateModelingModelDocumentAsync(string)"/>; multi-app providers override it.
    /// </summary>
    Task<(Guid Id, ModelingModelDto Document)> CreateModelingModelDocumentAsync(string title, string? scopeAppId)
        => CreateModelingModelDocumentAsync(title);
}
