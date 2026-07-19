using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmShareLinkPanelTests : LocalizationTestBase
{
    [Fact]
    public void Render_LinkCopyQrEmbedAndExpiration()
    {
        var cut = Render<TmShareLinkPanel>(parameters => parameters
            .Add(p => p.Link, "https://sign.example.test/s/abc")
            .Add(p => p.EmbedCode, "<iframe src=\"https://sign.example.test/s/abc\"></iframe>")
            .Add(p => p.ExpiresAt, new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero)));

        cut.Find(".tm-share-link-panel__input").GetAttribute("value").Should().Be("https://sign.example.test/s/abc");
        cut.Find("[data-testid='share-link-copy']").Should().NotBeNull();
        cut.Find(".tm-qr-code").Should().NotBeNull();
        cut.Find(".tm-share-link-panel__embed-code").TextContent.Should().Contain("iframe");
        cut.Markup.Should().Contain("Expires");
    }

    [Fact]
    public void Toggle_InvokesEnabledChangedAndDisablesInputs()
    {
        bool? enabled = null;
        var cut = Render<TmShareLinkPanel>(parameters => parameters
            .Add(p => p.Link, "https://sign.example.test/s/abc")
            .Add(p => p.EnabledChanged, EventCallback.Factory.Create<bool>(this, value => enabled = value)));

        cut.Find(".tm-share-link-panel__toggle input").Change(false);

        enabled.Should().BeFalse();
        cut.Find(".tm-share-link-panel").ClassList.Should().Contain("tm-share-link-panel--disabled");
        cut.Find(".tm-share-link-panel__input").HasAttribute("disabled").Should().BeTrue();
    }
}
