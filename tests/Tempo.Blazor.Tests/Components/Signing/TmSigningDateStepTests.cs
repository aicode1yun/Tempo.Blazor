using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSigningDateStepTests : LocalizationTestBase
{
    [Theory]
    [InlineData(null, "date")]
    [InlineData("month", "month")]
    [InlineData("datetime-local", "datetime-local")]
    public void Render_Format_RendersExpectedInputType(string? format, string expectedType)
    {
        var cut = RenderComponent<TmSigningDateStep>(parameters => parameters
            .Add(p => p.Field, new SigningField
            {
                Name = "Date",
                Type = SigningFieldType.Date,
                Preferences = new SigningFieldPreferences { Format = format }
            }));

        cut.Find("input.tm-signing-date-step__input").GetAttribute("type").Should().Be(expectedType);
    }

    [Fact]
    public void Render_DateToken_NormalizesMinToToday()
    {
        var cut = RenderComponent<TmSigningDateStep>(parameters => parameters
            .Add(p => p.Field, new SigningField
            {
                Name = "Date",
                Type = SigningFieldType.Date,
                Validation = new SigningFieldValidation { Min = "{{date}}" }
            }));

        cut.Find("input").GetAttribute("min").Should().NotBe("{{date}}");
    }

    [Fact]
    public void TodayButton_SetsValue()
    {
        string? captured = null;
        var cut = RenderComponent<TmSigningDateStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Date", Type = SigningFieldType.Date })
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => captured = value)));

        cut.Find(".tm-signing-date-step__today").Click();

        captured.Should().MatchRegex("^\\d{4}-\\d{2}-\\d{2}$");
    }
}
