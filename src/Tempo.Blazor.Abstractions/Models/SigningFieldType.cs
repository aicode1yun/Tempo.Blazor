namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Field types supported by signing templates and signing forms.</summary>
public enum SigningFieldType
{
    /// <summary>Static heading text displayed in the document.</summary>
    Heading,

    /// <summary>Static strike-through mark displayed in the document.</summary>
    Strikethrough,

    /// <summary>Free text field.</summary>
    Text,

    /// <summary>Handwritten, typed, or uploaded signature field.</summary>
    Signature,

    /// <summary>Handwritten, typed, or uploaded initials field.</summary>
    Initials,

    /// <summary>Date or date-time field.</summary>
    Date,

    /// <summary>Read-only signing date field.</summary>
    DateNow,

    /// <summary>Numeric input field.</summary>
    Number,

    /// <summary>Image upload field.</summary>
    Image,

    /// <summary>File attachment field.</summary>
    File,

    /// <summary>Single-choice select field.</summary>
    Select,

    /// <summary>Boolean checkbox field.</summary>
    Checkbox,

    /// <summary>Multiple-choice checkbox group field.</summary>
    Multiple,

    /// <summary>Single-choice radio group field.</summary>
    Radio,

    /// <summary>Comb-style text field with one character per cell.</summary>
    Cells,

    /// <summary>Generated digital signing stamp field.</summary>
    Stamp,

    /// <summary>Payment collection field.</summary>
    Payment,

    /// <summary>Verified phone number field.</summary>
    Phone,

    /// <summary>Identity verification field.</summary>
    Verification,

    /// <summary>Knowledge-based authentication field.</summary>
    Kba
}
