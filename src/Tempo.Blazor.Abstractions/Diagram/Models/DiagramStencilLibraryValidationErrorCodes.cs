namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Stable validation error codes for data-driven diagram stencil libraries.</summary>
public static class DiagramStencilLibraryValidationErrorCodes
{
    /// <summary>The library document could not be deserialized.</summary>
    public const string InvalidJson = "invalidJson";

    /// <summary>The library is missing a set identifier.</summary>
    public const string MissingSetId = "missingSetId";

    /// <summary>The library is missing a display-name resource key.</summary>
    public const string MissingLibraryNameResourceKey = "missingLibraryNameResourceKey";

    /// <summary>A palette is missing its identifier.</summary>
    public const string MissingPaletteId = "missingPaletteId";

    /// <summary>A palette is missing a display-name resource key.</summary>
    public const string MissingPaletteNameResourceKey = "missingPaletteNameResourceKey";

    /// <summary>A stencil is missing its identifier.</summary>
    public const string MissingStencilId = "missingStencilId";

    /// <summary>A stencil is missing a display-name resource key.</summary>
    public const string MissingStencilNameResourceKey = "missingStencilNameResourceKey";

    /// <summary>A stencil does not declare its licensing origin.</summary>
    public const string MissingStencilOrigin = "missingStencilOrigin";
}
