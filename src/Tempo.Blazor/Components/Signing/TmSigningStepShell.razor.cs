using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Shared layout shell for linear signing steps.</summary>
public partial class TmSigningStepShell
{
    /// <summary>Signing field represented by this step.</summary>
    [Parameter] public SigningField? Field { get; set; }

    /// <summary>Explicit label overriding the field title or name.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Optional description rendered below the label.</summary>
    [Parameter] public string? Description { get; set; }

    /// <summary>Whether the step is required. Defaults to the field requirement.</summary>
    [Parameter] public bool? Required { get; set; }

    /// <summary>Validation message shown below the step content.</summary>
    [Parameter] public string? ValidationMessage { get; set; }

    /// <summary>Short text describing where the field appears in the document.</summary>
    [Parameter] public string? AppearsOn { get; set; }

    /// <summary>Step body content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsRequired => Required ?? Field?.Required == true;

    private bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    private string DisplayLabel => !string.IsNullOrWhiteSpace(Label)
        ? Label
        : !string.IsNullOrWhiteSpace(Field?.Title)
            ? Field.Title!
            : !string.IsNullOrWhiteSpace(Field?.Name)
                ? Field.Name!
                : GetFieldTypeLabel(Field?.Type ?? SigningFieldType.Text);

    private string RootClass
    {
        get
        {
            var classes = new List<string> { "tm-signing-step-shell" };
            if (IsRequired)
            {
                classes.Add("tm-signing-step-shell--required");
            }

            if (HasValidationMessage)
            {
                classes.Add("tm-signing-step-shell--invalid");
            }

            if (!string.IsNullOrWhiteSpace(Class))
            {
                classes.Add(Class);
            }

            return string.Join(" ", classes);
        }
    }

    private string GetFieldTypeLabel(SigningFieldType type)
    {
        return type switch
        {
            SigningFieldType.Text or SigningFieldType.Cells => Loc["TmSigning_Field_Text"],
            SigningFieldType.Signature => Loc["TmSigning_Field_Signature"],
            SigningFieldType.Initials => Loc["TmSigning_Field_Initials"],
            SigningFieldType.Date or SigningFieldType.DateNow => Loc["TmSigning_Field_Date"],
            SigningFieldType.Number => Loc["TmSigning_Field_Number"],
            SigningFieldType.Checkbox => Loc["TmSigning_Field_Checkbox"],
            SigningFieldType.Radio => Loc["TmSigning_Field_Radio"],
            SigningFieldType.Select => Loc["TmSigning_Field_Select"],
            SigningFieldType.Multiple => Loc["TmSigning_Field_Multiple"],
            SigningFieldType.File => Loc["TmSigning_Field_File"],
            SigningFieldType.Image => Loc["TmSigning_Field_Image"],
            SigningFieldType.Stamp => Loc["TmSigning_Field_Stamp"],
            SigningFieldType.Phone => Loc["TmSigning_Field_Phone"],
            SigningFieldType.Verification => Loc["TmSigning_Field_Verification"],
            SigningFieldType.Kba => Loc["TmSigning_Field_Kba"],
            SigningFieldType.Payment => Loc["TmSigning_Field_Payment"],
            _ => type.ToString()
        };
    }

    private static string FormatDescription(string description)
    {
        var encoded = WebUtility.HtmlEncode(description);
        encoded = Regex.Replace(encoded, "\\*\\*(.+?)\\*\\*", "<strong>$1</strong>");
        encoded = Regex.Replace(encoded, "\\*(.+?)\\*", "<em>$1</em>");
        return encoded.Replace("\n", "<br>");
    }

    private static string BoolText(bool value) => value ? "true" : "false";
}
