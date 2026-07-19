using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSigningTextStepTests : LocalizationTestBase
{
    [Fact]
    public void Render_TextField_RendersSingleLineInput()
    {
        var cut = Render<TmSigningTextStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Name", Type = SigningFieldType.Text }));

        cut.Find("input.tm-signing-text-step__input[type='text']").Should().NotBeNull();
    }

    [Fact]
    public void Render_MultilineFormat_RendersTextarea()
    {
        var cut = Render<TmSigningTextStep>(parameters => parameters
            .Add(p => p.Field, new SigningField
            {
                Name = "Notes",
                Type = SigningFieldType.Text,
                Preferences = new SigningFieldPreferences { Format = "multiline" }
            }));

        cut.Find("textarea.tm-signing-text-step__textarea").Should().NotBeNull();
    }

    [Fact]
    public void Render_CellsField_SetsMaxLengthFromArea()
    {
        var cut = Render<TmSigningTextStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Code", Type = SigningFieldType.Cells })
            .Add(p => p.Area, new SigningFieldArea { Width = 0.4, CellWidth = 0.1 }));

        cut.Find("input").GetAttribute("maxlength").Should().Be("4");
    }

    [Fact]
    public void Change_InvalidPattern_ShowsCustomMessage()
    {
        var cut = Render<TmSigningTextStep>(parameters => parameters
            .Add(p => p.Field, new SigningField
            {
                Name = "Code",
                Type = SigningFieldType.Text,
                Validation = new SigningFieldValidation { Pattern = "^[0-9]+$", Message = "Digits only" }
            }));

        cut.Find("input").Change("abc");

        cut.Find(".tm-signing-step-shell__validation").TextContent.Should().Be("Digits only");
    }

    [Fact]
    public void Change_ValidValue_InvokesValueChanged()
    {
        string? captured = null;
        var cut = Render<TmSigningTextStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Name", Type = SigningFieldType.Text })
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => captured = value)));

        cut.Find("input").Change("Alice");

        captured.Should().Be("Alice");
    }
}
