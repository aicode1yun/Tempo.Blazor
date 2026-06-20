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

    /// <summary>Culture used to resolve field labels and descriptions.</summary>
    [Parameter] public string? Culture { get; set; }

    /// <summary>Fallback culture used when the requested culture is missing.</summary>
    [Parameter] public string? FallbackCulture { get; set; }

    /// <summary>Whether the step is required. Defaults to the field requirement.</summary>
    [Parameter] public bool? Required { get; set; }

    /// <summary>Validation message shown below the step content.</summary>
    [Parameter] public string? ValidationMessage { get; set; }

    /// <summary>Optional HTML id applied to the validation message for aria-describedby links.</summary>
    [Parameter] public string? ValidationMessageId { get; set; }

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
        : SigningTextResolver.FieldLabel(Field, Culture, FallbackCulture, Loc);

    private string ResolvedDescription => !string.IsNullOrWhiteSpace(Description)
        ? Description!
        : SigningTextResolver.FieldDescription(Field, Culture, FallbackCulture);

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
        return SigningTextResolver.FieldTypeLabel(type, Loc);
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
