using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Internal;

/// <summary>
/// Produces a human-readable placeholder for an inline signing field when exporting to a document
/// format that does not model signing fields (plan S2.25/S2.26, O3). One-way by design: the canonical
/// JSON remains the source of truth, so importers do not reparse the placeholder.
/// </summary>
internal static class SigningFieldPlaceholder
{
    public static string Text(DocumentSigningFieldRun field)
    {
        var label = string.IsNullOrWhiteSpace(field.Label) ? field.FieldType : field.Label;
        var role = string.IsNullOrWhiteSpace(field.SubmitterUuid) ? string.Empty : $" ({field.SubmitterUuid})";
        return $"⟦Pole: {label}{role}⟧";
    }
}
