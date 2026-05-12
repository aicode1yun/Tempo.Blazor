namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Resolved localization metadata captured for a signing submission.</summary>
public class SigningSubmissionLocalizationSnapshot
{
    /// <summary>Culture used by the signer during the signing ceremony.</summary>
    public string? Culture { get; set; }

    /// <summary>Fallback culture used by the template.</summary>
    public string? FallbackCulture { get; set; }

    /// <summary>UTC timestamp when the snapshot was generated.</summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Whether the PDF page content was translated by the component library.</summary>
    public bool PdfContentTranslated { get; set; }

    /// <summary>Resolved labels and descriptions for fields visible in the submission.</summary>
    public List<SigningSubmissionFieldLocalizationSnapshot> Fields { get; set; } = [];
}

/// <summary>Resolved localization metadata for one signing field.</summary>
public class SigningSubmissionFieldLocalizationSnapshot
{
    /// <summary>Stable field identifier.</summary>
    public string FieldUuid { get; set; } = string.Empty;

    /// <summary>Resolved field label shown to the signer.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Resolved field title shown to the signer.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Resolved field description shown to the signer.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Resolved validation message, if configured.</summary>
    public string ValidationMessage { get; set; } = string.Empty;

    /// <summary>Resolved option labels for choice fields.</summary>
    public List<SigningSubmissionOptionLocalizationSnapshot> Options { get; set; } = [];
}

/// <summary>Resolved localization metadata for one choice option.</summary>
public class SigningSubmissionOptionLocalizationSnapshot
{
    /// <summary>Stable option identifier.</summary>
    public string OptionUuid { get; set; } = string.Empty;

    /// <summary>Stable option value stored by submissions and conditions.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Resolved option label shown to the signer.</summary>
    public string Label { get; set; } = string.Empty;
}
