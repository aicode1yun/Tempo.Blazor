using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.AITools;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.AITools;

public class TmAIPromptTests : LocalizationTestBase
{
    // ── AIP-7: render zobrazí prompt input a commands ───────────────────────

    [Fact]
    public void Render_WithCommands_DisplaysInputAndCommands()
    {
        var commands = new[]
        {
            new AIPromptCommand("summarize", "Summarize", icon: "file-text"),
            new AIPromptCommand("translate", "Translate", icon: "globe"),
        };

        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.Commands, commands));

        cut.Find(".tm-ai-prompt__input").Should().NotBeNull();
        cut.FindAll(".tm-ai-prompt__command").Count.Should().Be(2);
    }

    // ── AIP-7b: render bez commands nezobrazí command row ───────────────────

    [Fact]
    public void Render_WithoutCommands_HidesCommandRow()
    {
        var cut = Render<TmAIPrompt>();

        cut.FindAll(".tm-ai-prompt__commands").Should().BeEmpty();
        cut.Find(".tm-ai-prompt__input").Should().NotBeNull();
    }

    // ── AIP-8: submit promptu vyvolá OnPromptSubmit ─────────────────────────

    [Fact]
    public void SubmitPrompt_FiresOnPromptSubmit()
    {
        string? capturedPrompt = null;
        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.OnPromptSubmit, EventCallback.Factory.Create<string>(this, p => capturedPrompt = p)));

        var input = cut.Find(".tm-ai-prompt__input");
        input.Input("Hello AI");
        cut.Render();

        var submitBtn = cut.Find(".tm-ai-prompt__submit");
        submitBtn.Click();

        capturedPrompt.Should().Be("Hello AI");
    }

    // ── AIP-8b: Enter bez Shift odešle prompt ───────────────────────────────

    [Fact]
    public void PressEnter_SubmitsPrompt()
    {
        string? capturedPrompt = null;
        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.OnPromptSubmit, EventCallback.Factory.Create<string>(this, p => capturedPrompt = p)));

        var input = cut.Find(".tm-ai-prompt__input");
        input.Input("Test prompt");
        cut.Render();

        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        capturedPrompt.Should().Be("Test prompt");
    }

    // ── AIP-8c: prázdný prompt nejde odeslat ────────────────────────────────

    [Fact]
    public void EmptyPrompt_SubmitButtonDisabled()
    {
        var cut = Render<TmAIPrompt>();

        var submitBtn = cut.Find(".tm-ai-prompt__submit");
        submitBtn.HasAttribute("disabled").Should().BeTrue();
    }

    // ── AIP-9: output se zobrazí po submitu ─────────────────────────────────

    [Fact]
    public void Render_WithOutput_DisplaysOutputContent()
    {
        var output = new AIPromptOutput("1", "This is the AI response.", AIPromptOutputFormat.Text);

        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.Output, output));

        cut.Find(".tm-ai-prompt__output").Should().NotBeNull();
        cut.Find(".tm-ai-prompt__output-text").TextContent.Should().Contain("This is the AI response.");
    }

    // ── AIP-9b: output s kódem zobrazí <pre><code> ──────────────────────────

    [Fact]
    public void Render_WithCodeOutput_DisplaysCodeBlock()
    {
        var output = new AIPromptOutput("1", "console.log('hi');", AIPromptOutputFormat.Code);

        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.Output, output));

        cut.Find("pre.tm-ai-prompt__output-code").Should().NotBeNull();
        cut.Find("pre.tm-ai-prompt__output-code code").TextContent.Should().Contain("console.log('hi');");
    }

    // ── AIP-9c: loading output zobrazí spinner ──────────────────────────────

    [Fact]
    public void Render_WithLoadingOutput_DisplaysSpinner()
    {
        var output = new AIPromptOutput("1", "", AIPromptOutputFormat.Text, isLoading: true);

        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.Output, output));

        cut.Find(".tm-ai-prompt__output-loading").Should().NotBeNull();
    }

    // ── AIP-9d: output s titulkem zobrazí header ────────────────────────────

    [Fact]
    public void Render_WithTitledOutput_DisplaysHeader()
    {
        var output = new AIPromptOutput("1", "Content", title: "Summary");

        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.Output, output));

        cut.Find(".tm-ai-prompt__output-title").TextContent.Should().Be("Summary");
    }

    // ── AIP-10: click na command vyvolá OnCommandClick ──────────────────────

    [Fact]
    public void ClickCommand_FiresOnCommandClick()
    {
        var commands = new[]
        {
            new AIPromptCommand("fix", "Fix grammar"),
        };

        AIPromptCommand? capturedCommand = null;
        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.Commands, commands)
                      .Add(p => p.OnCommandClick, EventCallback.Factory.Create<AIPromptCommand>(this, c => capturedCommand = c)));

        var cmdBtn = cut.Find(".tm-ai-prompt__command");
        cmdBtn.Click();

        capturedCommand.Should().NotBeNull();
        capturedCommand!.Id.Should().Be("fix");
    }

    // ── AIP-10b: click na thumbs-up vyvolá OnOutputRate ─────────────────────

    [Fact]
    public void ClickRatePositive_FiresOnOutputRate()
    {
        var output = new AIPromptOutput("1", "Great answer!");
        (AIPromptOutput Output, bool? Rating)? captured = null;

        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.Output, output)
                      .Add(p => p.OnOutputRate, EventCallback.Factory.Create<(AIPromptOutput, bool?)>(this, r => captured = r)));

        var rateBtn = cut.FindAll(".tm-ai-prompt__action-btn")
                         .First(b => b.GetAttribute("aria-label")?.Contains("Helpful") == true
                              || b.GetAttribute("aria-label")?.Contains("Užitečné") == true);
        rateBtn.Click();

        captured.Should().NotBeNull();
        captured!.Value.Rating.Should().BeTrue();
    }

    // ── AIP-10c: copy button existuje u output ──────────────────────────────

    [Fact]
    public void Render_WithOutput_DisplaysCopyButton()
    {
        var output = new AIPromptOutput("1", "Content");

        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.Output, output));

        var copyBtn = cut.FindAll(".tm-ai-prompt__action-btn")
                         .FirstOrDefault(b => b.GetAttribute("aria-label")?.Contains("Copy") == true
                              || b.GetAttribute("aria-label")?.Contains("Kopírovat") == true);
        copyBtn.Should().NotBeNull();
    }

    // ── AIP-10d: disabled command nelze kliknout ────────────────────────────

    [Fact]
    public void ClickDisabledCommand_DoesNotFireEvent()
    {
        var commands = new[]
        {
            new AIPromptCommand("disabled", "Disabled", isDisabled: true),
        };

        bool fired = false;
        var cut = Render<TmAIPrompt>(parameters =>
            parameters.Add(p => p.Commands, commands)
                      .Add(p => p.OnCommandClick, EventCallback.Factory.Create<AIPromptCommand>(this, _ => fired = true)));

        var cmdBtn = cut.Find(".tm-ai-prompt__command");
        cmdBtn.HasAttribute("disabled").Should().BeTrue();
    }
}
