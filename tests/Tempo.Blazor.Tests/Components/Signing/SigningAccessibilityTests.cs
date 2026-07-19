using Bunit.Rendering;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class SigningAccessibilityTests : LocalizationTestBase
{
    [Fact]
    public void IconOnlyButtons_HaveAccessibleNames()
    {
        var fieldEditor = Render<TmSigningFieldEditorPanel>(parameters =>
            parameters.Add(p => p.Field, CreateChoiceField()));
        AssertIconOnlyButtonsHaveAccessibleNames(fieldEditor);

        var roles = Render<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles()));
        AssertIconOnlyButtonsHaveAccessibleNames(roles);

        var conditions = Render<TmConditionBuilder>(parameters =>
            parameters.Add(p => p.Fields, CreateConditionFields())
                      .Add(p => p.CurrentFieldUuid, "target")
                      .Add(p => p.Conditions, [
                          new SigningFieldCondition { FieldUuid = "name", Action = SigningConditionAction.NotEmpty },
                          new SigningFieldCondition { FieldUuid = "consent", Action = SigningConditionAction.Checked }
                      ]));
        AssertIconOnlyButtonsHaveAccessibleNames(conditions);
    }

    [Fact]
    public void FieldOverlay_InvalidState_UsesAriaInvalid()
    {
        var cut = Render<TmSigningFieldOverlay>(parameters =>
            parameters.Add(p => p.Field, CreateTextField())
                      .Add(p => p.Invalid, true));

        cut.Find(".tm-signing-field").GetAttribute("aria-invalid").Should().Be("true");
    }

    [Fact]
    public void FieldOverlay_LocalizedLabelIsAccessibleName()
    {
        var field = CreateTextField();
        field.Labels.Translations["cs"] = "Celé jméno";

        var cut = Render<TmSigningFieldOverlay>(parameters =>
            parameters.Add(p => p.Field, field)
                      .Add(p => p.Culture, "cs-CZ"));

        cut.Find(".tm-signing-field").GetAttribute("aria-label").Should().Be("Celé jméno");
    }

    [Fact]
    public void TextStep_LocalizedValidationMessageIsDescribedByInput()
    {
        var field = CreateTextField();
        field.Required = true;
        field.Validation = new SigningFieldValidation
        {
            Messages = { Translations = { ["cs"] = "Jméno je povinné." } }
        };

        var cut = Render<TmSigningTextStep>(parameters =>
            parameters.Add(p => p.Field, field)
                      .Add(p => p.Culture, "cs-CZ"));

        cut.Find(".tm-signing-text-step__input").Change(string.Empty);

        var input = cut.Find(".tm-signing-text-step__input");
        var describedBy = input.GetAttribute("aria-describedby");
        describedBy.Should().NotBeNullOrWhiteSpace();
        cut.Find($"#{describedBy}").TextContent.Should().Be("Jméno je povinné.");
    }

    [Fact]
    public void FieldOverlay_EnterKey_InvokesSelection()
    {
        var invoked = false;
        var cut = Render<TmSigningFieldOverlay>(parameters =>
            parameters.Add(p => p.Field, CreateTextField())
                      .Add(p => p.OnClick, EventCallback.Factory.Create<TmSigningFieldOverlayPointerEventArgs>(this, _ => invoked = true)));

        cut.Find(".tm-signing-field").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        invoked.Should().BeTrue();
    }

    [Fact]
    public void RunnerProgress_HasAriaLabel()
    {
        var cut = Render<TmSigningFormRunner>(parameters =>
            parameters.Add(p => p.Fields, [CreateTextField()]));

        cut.Find(".tm-signing-form-runner__progress")
            .GetAttribute("aria-label")
            .Should()
            .Contain("Step 1 of 1");
    }

    [Fact]
    public void SigningCss_UsesDesignTokensInsteadOfHardcodedPalette()
    {
        var root = FindRepositoryRoot();
        var cssDirectory = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "components");
        var signingCssFileNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "_audit-trail-viewer.css",
            "_condition-builder.css",
            "_document-page-viewer.css",
            "_formula-builder.css",
            "_pdf-signature-verification.css",
            "_pdf-template-designer.css",
            "_recipient-role-editor.css",
            "_share-link-panel.css",
            "_signature-capture.css",
            "_signing-completion-panel.css",
            "_signing-field-editor-panel.css",
            "_signing-field-overlay.css",
            "_signing-form-runner.css",
            "_signing-step-shell.css",
            "_submission-status-timeline.css"
        };
        var signingCssFiles = Directory.GetFiles(cssDirectory, "*.css")
            .Where(path => signingCssFileNames.Contains(Path.GetFileName(path)))
            .ToArray();

        var hardcodedColors = signingCssFiles
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { Path = path, Line = line, Number = index + 1 }))
            .Where(item => item.Line.Contains('#')
                || item.Line.Contains("rgb(", StringComparison.OrdinalIgnoreCase)
                || item.Line.Contains("hsl(", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{Path.GetFileName(item.Path)}:{item.Number}: {item.Line.Trim()}")
            .ToArray();

        hardcodedColors.Should().BeEmpty("signing CSS should stay token-driven and avoid a one-note hardcoded palette");
    }

    private static void AssertIconOnlyButtonsHaveAccessibleNames<TComponent>(IRenderedComponent<TComponent> fragment)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        var buttons = fragment.FindAll("button")
            .Where(button => string.IsNullOrWhiteSpace(button.TextContent)
                && button.QuerySelector("svg, .tm-icon") is not null);

        foreach (var button in buttons)
        {
            var accessibleName = button.GetAttribute("aria-label")
                ?? button.GetAttribute("title")
                ?? button.GetAttribute("aria-labelledby");

            accessibleName.Should().NotBeNullOrWhiteSpace(button.OuterHtml);
        }
    }

    private static SigningField CreateTextField()
    {
        return new SigningField
        {
            Uuid = "name",
            Name = "Full name",
            Type = SigningFieldType.Text,
            Areas =
            [
                new SigningFieldArea
                {
                    AttachmentUuid = "doc",
                    Page = 0,
                    X = 0.1,
                    Y = 0.1,
                    Width = 0.3,
                    Height = 0.05
                }
            ]
        };
    }

    private static SigningField CreateChoiceField()
    {
        var field = CreateTextField();
        field.Type = SigningFieldType.Radio;
        field.Options =
        [
            new SigningFieldOption { Uuid = "option-a", Value = "Email" },
            new SigningFieldOption { Uuid = "option-b", Value = "Paper" }
        ];

        return field;
    }

    private static IReadOnlyList<SigningSubmitterRole> CreateRoles()
    {
        return
        [
            new SigningSubmitterRole
            {
                Uuid = "role-a",
                Name = "Signer A",
                Order = 0
            },
            new SigningSubmitterRole
            {
                Uuid = "role-b",
                Name = "Signer B",
                Order = 1
            }
        ];
    }

    private static IReadOnlyList<SigningField> CreateConditionFields()
    {
        return
        [
            new SigningField { Uuid = "target", Name = "Target", Type = SigningFieldType.Text },
            new SigningField { Uuid = "name", Name = "Name", Type = SigningFieldType.Text },
            new SigningField { Uuid = "consent", Name = "Consent", Type = SigningFieldType.Checkbox }
        ];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
