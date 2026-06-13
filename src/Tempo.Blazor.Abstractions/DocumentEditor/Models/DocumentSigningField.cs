using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>
/// Canvas engine descriptor for one signing field, as derived from the editor layout (plan S2). Areas
/// are normalized 0..1 page rectangles — one for a body field, one per page for a header/footer field.
/// </summary>
public sealed class DocumentSigningFieldDescriptor
{
    /// <summary>Stable field identifier (shared by every area of the field).</summary>
    public string Uuid { get; set; } = string.Empty;

    /// <summary>Field type name (camelCase, mirrors <see cref="SigningFieldType"/>).</summary>
    public string FieldType { get; set; } = "text";

    /// <summary>Signer role identifier the field belongs to.</summary>
    public string SubmitterUuid { get; set; } = string.Empty;

    /// <summary>Whether the signer must provide a value.</summary>
    public bool Required { get; set; }

    /// <summary>User-facing field label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Choice options for select/radio/multiple fields.</summary>
    public List<DocumentSigningFieldOptionDescriptor> Options { get; set; } = [];

    /// <summary>Normalized 0..1 page rectangles where the field renders.</summary>
    public List<DocumentSigningFieldAreaDescriptor> Areas { get; set; } = [];
}

/// <summary>Canvas engine descriptor for a single signing field area (normalized 0..1 page rect).</summary>
public sealed class DocumentSigningFieldAreaDescriptor
{
    /// <summary>Zero-based page index.</summary>
    public int Page { get; set; }

    /// <summary>Normalized horizontal position (0..1).</summary>
    public double X { get; set; }

    /// <summary>Normalized vertical position (0..1).</summary>
    public double Y { get; set; }

    /// <summary>Normalized width (0..1).</summary>
    public double Width { get; set; }

    /// <summary>Normalized height (0..1).</summary>
    public double Height { get; set; }
}

/// <summary>Canvas engine descriptor for a signing field choice option.</summary>
public sealed class DocumentSigningFieldOptionDescriptor
{
    /// <summary>Submitted option value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Display label.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>Maps canvas engine signing field descriptors into the shared <see cref="SigningField"/> model.</summary>
public static class DocumentSigningFieldMappingExtensions
{
    /// <summary>
    /// Projects engine signing field descriptors into <see cref="SigningField"/> instances. The
    /// attachment identifier is applied to every area (so a multi-page header/footer field's areas all
    /// reference the same exported document). The field label is exposed via <see cref="SigningField.Title"/>.
    /// </summary>
    public static List<SigningField> ToSigningFields(
        this IEnumerable<DocumentSigningFieldDescriptor> descriptors,
        string attachmentUuid)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        return descriptors
            .Select(descriptor => new SigningField
            {
                Uuid = string.IsNullOrEmpty(descriptor.Uuid) ? Guid.NewGuid().ToString("N") : descriptor.Uuid,
                SubmitterUuid = descriptor.SubmitterUuid,
                Type = ParseFieldType(descriptor.FieldType),
                Required = descriptor.Required,
                Title = descriptor.Label,
                Options = descriptor.Options
                    .Select(option => new SigningFieldOption { Value = option.Value })
                    .ToList(),
                Areas = descriptor.Areas
                    .Select(area => new SigningFieldArea
                    {
                        AttachmentUuid = attachmentUuid,
                        Page = area.Page,
                        X = area.X,
                        Y = area.Y,
                        Width = area.Width,
                        Height = area.Height,
                    })
                    .ToList(),
            })
            .ToList();
    }

    private static SigningFieldType ParseFieldType(string fieldType)
        => Enum.TryParse<SigningFieldType>(fieldType, ignoreCase: true, out var parsed)
            ? parsed
            : SigningFieldType.Text;
}
