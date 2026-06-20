using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSigningPhoneStepTests : LocalizationTestBase
{
    [Fact]
    public void Render_PhoneStep_RendersCountrySelectAndTelInput()
    {
        var cut = RenderComponent<TmSigningPhoneStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Phone", Type = SigningFieldType.Phone }));

        cut.Find(".tm-signing-phone-step__country").Should().NotBeNull();
        cut.Find("input[type='tel']").Should().NotBeNull();
    }

    [Fact]
    public void Change_Phone_NormalizesValue()
    {
        string? captured = null;
        var cut = RenderComponent<TmSigningPhoneStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Phone", Type = SigningFieldType.Phone })
            .Add(p => p.CountryCode, "+420")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => captured = value)));

        cut.Find("input[type='tel']").Input("777 123 456");

        captured.Should().Be("+420777123456");
    }

    [Fact]
    public void SendCode_InvokesCallbackAndShowsOtp()
    {
        string? sent = null;
        var cut = RenderComponent<TmSigningPhoneStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Phone", Type = SigningFieldType.Phone })
            .Add(p => p.PhoneNumber, "5550100")
            .Add(p => p.OnSendCode, EventCallback.Factory.Create<string>(this, value => sent = value)));

        cut.Find(".tm-signing-phone-step__send").Click();

        sent.Should().Be("+15550100");
        cut.Find(".tm-signing-phone-step__otp").Should().NotBeNull();
    }

    [Fact]
    public void SentState_ShowsResendCountdown()
    {
        var cut = RenderComponent<TmSigningPhoneStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Phone", Type = SigningFieldType.Phone })
            .Add(p => p.CodeSent, true)
            .Add(p => p.ResendSeconds, 12));

        cut.Find(".tm-signing-phone-step__resend").TextContent.Should().Contain("12");
    }
}
