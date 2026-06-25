using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Localization;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Resolves user-facing signing labels with component localization fallbacks.</summary>
internal static class SigningTextResolver
{
    public static string FieldLabel(SigningField? field, string? culture, string? fallbackCulture, ITmLocalizer localizer)
    {
        return SigningLocalizationResolver.ResolveFieldLabel(field, culture, fallbackCulture, FieldTypeLabel(field?.Type ?? SigningFieldType.Text, localizer));
    }

    public static string FieldTitle(SigningField? field, string? culture, string? fallbackCulture, ITmLocalizer localizer)
    {
        return SigningLocalizationResolver.ResolveFieldTitle(field, culture, fallbackCulture, FieldLabel(field, culture, fallbackCulture, localizer));
    }

    public static string FieldDescription(SigningField? field, string? culture, string? fallbackCulture)
    {
        return SigningLocalizationResolver.ResolveFieldDescription(field, culture, fallbackCulture);
    }

    public static string FieldPlaceholder(SigningField? field, string? culture, string? fallbackCulture, ITmLocalizer localizer)
    {
        return SigningLocalizationResolver.ResolveFieldPlaceholder(field, culture, fallbackCulture, FieldTypeLabel(field?.Type ?? SigningFieldType.Text, localizer));
    }

    public static string OptionLabel(SigningFieldOption? option, string? culture, string? fallbackCulture)
    {
        return SigningLocalizationResolver.ResolveOptionLabel(option, culture, fallbackCulture, option?.Value);
    }

    public static string ValidationMessage(SigningFieldValidation? validation, string? culture, string? fallbackCulture, string finalFallback)
    {
        return SigningLocalizationResolver.ResolveValidationMessage(validation, culture, fallbackCulture, finalFallback);
    }

    public static string FieldTypeLabel(SigningFieldType type, ITmLocalizer localizer)
    {
        return type switch
        {
            SigningFieldType.Heading => localizer["TmSigning_Field_Heading"],
            SigningFieldType.Strikethrough => localizer["TmSigning_Field_Strikethrough"],
            SigningFieldType.Text => localizer["TmSigning_Field_Text"],
            SigningFieldType.Signature => localizer["TmSigning_Field_Signature"],
            SigningFieldType.Initials => localizer["TmSigning_Field_Initials"],
            SigningFieldType.Date or SigningFieldType.DateNow => localizer["TmSigning_Field_Date"],
            SigningFieldType.Number => localizer["TmSigning_Field_Number"],
            SigningFieldType.Image => localizer["TmSigning_Field_Image"],
            SigningFieldType.File => localizer["TmSigning_Field_File"],
            SigningFieldType.Select => localizer["TmSigning_Field_Select"],
            SigningFieldType.Checkbox => localizer["TmSigning_Field_Checkbox"],
            SigningFieldType.Multiple => localizer["TmSigning_Field_Multiple"],
            SigningFieldType.Radio => localizer["TmSigning_Field_Radio"],
            SigningFieldType.Cells => localizer["TmSigning_Field_Cells"],
            SigningFieldType.Stamp => localizer["TmSigning_Field_Stamp"],
            SigningFieldType.Phone => localizer["TmSigning_Field_Phone"],
            SigningFieldType.Verification => localizer["TmSigning_Field_Verification"],
            SigningFieldType.Kba => localizer["TmSigning_Field_Kba"],
            SigningFieldType.Payment => localizer["TmSigning_Field_Payment"],
            _ => localizer["TmSigning_Field_Text"]
        };
    }
}
