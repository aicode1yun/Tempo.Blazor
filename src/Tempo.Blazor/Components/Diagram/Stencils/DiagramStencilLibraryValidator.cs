using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Stencils;

/// <summary>Validates data-driven diagram stencil libraries before registration.</summary>
public static class DiagramStencilLibraryValidator
{
    /// <summary>Validates <paramref name="library"/> and returns stable error codes.</summary>
    public static DiagramStencilLibraryValidationResult Validate(DiagramStencilLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);

        var result = new DiagramStencilLibraryValidationResult();

        if (string.IsNullOrWhiteSpace(library.SetId))
            Add(result, DiagramStencilLibraryValidationErrorCodes.MissingSetId, "setId");

        if (string.IsNullOrWhiteSpace(library.NameResourceKey))
            Add(result, DiagramStencilLibraryValidationErrorCodes.MissingLibraryNameResourceKey, "nameResourceKey");

        library.Palettes ??= [];
        for (var paletteIndex = 0; paletteIndex < library.Palettes.Count; paletteIndex++)
        {
            var palette = library.Palettes[paletteIndex];
            var palettePath = $"palettes[{paletteIndex}]";

            if (string.IsNullOrWhiteSpace(palette.PaletteId))
                Add(result, DiagramStencilLibraryValidationErrorCodes.MissingPaletteId, $"{palettePath}.paletteId");

            if (string.IsNullOrWhiteSpace(palette.NameResourceKey))
                Add(result, DiagramStencilLibraryValidationErrorCodes.MissingPaletteNameResourceKey, $"{palettePath}.nameResourceKey");

            palette.Stencils ??= [];
            for (var stencilIndex = 0; stencilIndex < palette.Stencils.Count; stencilIndex++)
            {
                var stencil = palette.Stencils[stencilIndex];
                var stencilPath = $"{palettePath}.stencils[{stencilIndex}]";

                if (string.IsNullOrWhiteSpace(stencil.Id))
                    Add(result, DiagramStencilLibraryValidationErrorCodes.MissingStencilId, $"{stencilPath}.id");

                if (string.IsNullOrWhiteSpace(stencil.NameResourceKey))
                    Add(result, DiagramStencilLibraryValidationErrorCodes.MissingStencilNameResourceKey, $"{stencilPath}.nameResourceKey");

                if (stencil.Origin == DiagramStencilOrigin.Unspecified)
                    Add(result, DiagramStencilLibraryValidationErrorCodes.MissingStencilOrigin, $"{stencilPath}.origin");
            }
        }

        return result;
    }

    private static void Add(DiagramStencilLibraryValidationResult result, string code, string path)
        => result.Errors.Add(new DiagramStencilLibraryValidationError
        {
            Code = code,
            Path = path
        });
}
