namespace Tempo.Blazor.Components.Wireframe.Stencil;

/// <summary>Non-fatal validation warning produced while inspecting a stencil pack.</summary>
public sealed record StencilPackValidationWarning(
    string Code,
    string Message,
    string? ComponentType = null,
    string? Role = null);

/// <summary>Validation helpers for stencil pack metadata that should not block loading.</summary>
public static class StencilPackValidator
{
    /// <summary>Warning code emitted when a component references a role outside the vocabulary.</summary>
    public const string UnknownRole = "unknown-role";

    /// <summary>
    /// Validates component role references against <paramref name="vocabulary"/>.
    /// Unknown roles are reported as warnings so packs can be loaded before new role
    /// definitions are added to the vocabulary.
    /// </summary>
    public static IReadOnlyList<StencilPackValidationWarning> ValidateRoles(
        StencilPack pack,
        UiRoleVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(vocabulary);

        var warnings = new List<StencilPackValidationWarning>();
        foreach (var component in pack.Components)
        {
            foreach (var role in component.Roles ?? [])
            {
                if (string.IsNullOrWhiteSpace(role) || vocabulary.Find(role) is not null)
                    continue;

                warnings.Add(new StencilPackValidationWarning(
                    UnknownRole,
                    $"Stencil component '{component.Type}' references unknown UI role '{role}'.",
                    component.Type,
                    role));
            }
        }

        return warnings;
    }
}
