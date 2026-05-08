using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSigningCompletionPanelTests : LocalizationTestBase
{
    [Fact]
    public void Render_CompletedMessageAndDownloadLink()
    {
        var cut = RenderComponent<TmSigningCompletionPanel>(parameters => parameters
            .Add(p => p.DownloadUrl, "/signed.pdf"));

        cut.Find(".tm-signing-completion-panel__title").TextContent.Should().Contain("Document completed");
        cut.Find(".tm-signing-completion-panel__download").GetAttribute("href").Should().Be("/signed.pdf");
    }

    [Fact]
    public void Render_SendCopyButtonAndCustomAction()
    {
        var sendCopyClicked = false;
        var customClicked = false;
        var cut = RenderComponent<TmSigningCompletionPanel>(parameters => parameters
            .Add(p => p.CustomActionText, "Back to documents")
            .Add(p => p.OnSendCopy, EventCallback.Factory.Create(this, () => sendCopyClicked = true))
            .Add(p => p.OnCustomAction, EventCallback.Factory.Create(this, () => customClicked = true)));

        cut.Find(".tm-signing-completion-panel__send-copy").Click();
        cut.Find(".tm-signing-completion-panel__custom-action").Click();

        sendCopyClicked.Should().BeTrue();
        customClicked.Should().BeTrue();
    }

    [Fact]
    public void Render_WaitingForOthersState()
    {
        var cut = RenderComponent<TmSigningCompletionPanel>(parameters => parameters
            .Add(p => p.IsWaitingForOthers, true));

        cut.Markup.Should().Contain("Waiting for others");
        cut.Find(".tm-signing-completion-panel").GetAttribute("data-state").Should().Be("waiting");
        cut.Find(".tm-signing-completion-panel__send-copy").HasAttribute("disabled").Should().BeTrue();
    }
}
