using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSigningExternalStepTests : LocalizationTestBase
{
    [Fact]
    public void Verification_Loading_RendersLoadingState()
    {
        var cut = Render<TmSigningExternalStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "ID check", Type = SigningFieldType.Verification })
            .Add(p => p.IsLoading, true));

        cut.Find(".tm-signing-external-step__loading").TextContent.Should().Contain("Loading");
    }

    [Fact]
    public void Verification_Error_ShowsValidation()
    {
        var cut = Render<TmSigningExternalStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "ID check", Type = SigningFieldType.Verification })
            .Add(p => p.Error, "Provider failed"));

        cut.Find(".tm-signing-step-shell__validation").TextContent.Should().Be("Provider failed");
    }

    [Fact]
    public void Verification_ExternalUrl_RendersLink()
    {
        var cut = Render<TmSigningExternalStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "ID check", Type = SigningFieldType.Verification })
            .Add(p => p.ExternalUrl, "https://example.test/verify"));

        cut.Find(".tm-signing-external-step__link").GetAttribute("href").Should().Be("https://example.test/verify");
    }

    [Fact]
    public void Kba_WithoutQuestions_RendersStartButton()
    {
        var cut = Render<TmSigningExternalStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "KBA", Type = SigningFieldType.Kba }));

        cut.Find(".tm-signing-external-step__start-button").TextContent.Should().Contain("Start KBA");
    }

    [Fact]
    public void Kba_WithQuestions_RendersQuestionInputs()
    {
        var cut = Render<TmSigningExternalStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "KBA", Type = SigningFieldType.Kba })
            .Add(p => p.Questions, ["What city?"]));

        cut.Find(".tm-signing-external-step__question").TextContent.Should().Contain("What city?");
    }

    [Fact]
    public void Payment_RendersAmountAndCheckoutCallback()
    {
        SigningField? checkedOut = null;
        var field = new SigningField
        {
            Uuid = "payment",
            Name = "Payment",
            Type = SigningFieldType.Payment,
            Preferences = new SigningFieldPreferences { Currency = "EUR", Price = 19.5m }
        };
        var cut = Render<TmSigningExternalStep>(parameters => parameters
            .Add(p => p.Field, field)
            .Add(p => p.OnCheckout, EventCallback.Factory.Create<SigningField>(this, value => checkedOut = value)));

        cut.Find(".tm-signing-external-step__amount").TextContent.Should().Contain("EUR 19.50");
        cut.Find(".tm-signing-external-step__checkout").Click();

        checkedOut.Should().BeSameAs(field);
    }
}
