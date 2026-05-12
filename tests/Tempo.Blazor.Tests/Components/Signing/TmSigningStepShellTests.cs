using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSigningStepShellTests : LocalizationTestBase
{
    [Fact]
    public void Render_Field_RendersLabelAndRequiredMarker()
    {
        var cut = RenderComponent<TmSigningStepShell>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Full name", Required = true }));

        cut.Find(".tm-signing-step-shell__title").TextContent.Should().Be("Full name");
        cut.Find(".tm-signing-step-shell__required").TextContent.Should().Contain("Required");
    }

    [Fact]
    public void Render_OptionalField_RendersOptionalMarker()
    {
        var cut = RenderComponent<TmSigningStepShell>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Company" }));

        cut.Find(".tm-signing-step-shell__optional").TextContent.Should().Contain("Optional");
    }

    [Fact]
    public void Render_Description_RendersSimpleMarkdown()
    {
        var cut = RenderComponent<TmSigningStepShell>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Terms" })
            .Add(p => p.Description, "Accept **all** terms"));

        cut.Markup.Should().Contain("<strong>all</strong>");
    }

    [Fact]
    public void Render_LocalizedFieldText_UsesRequestedCulture()
    {
        var field = new SigningField
        {
            Name = "internal-name",
            Labels = { Translations = { ["cs"] = "Celé jméno" } },
            Descriptions = { Translations = { ["cs"] = "Vyplňte **jméno** podepisujícího." } }
        };

        var cut = RenderComponent<TmSigningStepShell>(parameters => parameters
            .Add(p => p.Field, field)
            .Add(p => p.Culture, "cs-CZ"));

        cut.Find(".tm-signing-step-shell__title").TextContent.Should().Be("Celé jméno");
        cut.Markup.Should().Contain("<strong>");
        cut.Find(".tm-signing-step-shell__description").TextContent.Should().Contain("jméno");
    }

    [Fact]
    public void Render_ValidationMessage_AddsInvalidClass()
    {
        var cut = RenderComponent<TmSigningStepShell>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Terms" })
            .Add(p => p.ValidationMessage, "Required"));

        cut.Find(".tm-signing-step-shell").ClassList.Should().Contain("tm-signing-step-shell--invalid");
        cut.Find(".tm-signing-step-shell__validation").TextContent.Should().Be("Required");
    }

    [Fact]
    public void Render_AppearsOn_ShowsDocumentPosition()
    {
        var cut = RenderComponent<TmSigningStepShell>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Terms" })
            .Add(p => p.AppearsOn, "Page 2"));

        cut.Find(".tm-signing-step-shell__appears-on").TextContent.Should().Be("Page 2");
    }
}
