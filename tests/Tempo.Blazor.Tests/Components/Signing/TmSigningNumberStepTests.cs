using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSigningNumberStepTests : LocalizationTestBase
{
    [Fact]
    public void Render_NumberField_RendersNumberInput()
    {
        var cut = RenderComponent<TmSigningNumberStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Amount", Type = SigningFieldType.Number }));

        cut.Find("input.tm-signing-number-step__input[type='number']").Should().NotBeNull();
    }

    [Fact]
    public void Render_ValidationAttributes_RendersMinMaxStep()
    {
        var cut = RenderComponent<TmSigningNumberStep>(parameters => parameters
            .Add(p => p.Field, new SigningField
            {
                Name = "Amount",
                Type = SigningFieldType.Number,
                Validation = new SigningFieldValidation { Min = "1", Max = "10", Step = "0.5" }
            }));

        var input = cut.Find("input");
        input.GetAttribute("min").Should().Be("1");
        input.GetAttribute("max").Should().Be("10");
        input.GetAttribute("step").Should().Be("0.5");
    }

    [Fact]
    public void Change_Number_CastsValueToDecimal()
    {
        decimal? captured = null;
        var cut = RenderComponent<TmSigningNumberStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Amount", Type = SigningFieldType.Number })
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<decimal?>(this, value => captured = value)));

        cut.Find("input").Change("12.5");

        captured.Should().Be(12.5m);
    }

    [Fact]
    public void Change_RequiredEmpty_ShowsValidation()
    {
        var cut = RenderComponent<TmSigningNumberStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Amount", Type = SigningFieldType.Number, Required = true }));

        cut.Find("input").Change(string.Empty);

        cut.Find(".tm-signing-step-shell__validation").TextContent.Should().Contain("required");
    }
}
