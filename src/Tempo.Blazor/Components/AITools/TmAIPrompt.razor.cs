using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.AITools;

/// <summary>
/// An AI prompt component with quick commands, output rendering, copy, and rating.
/// </summary>
public partial class TmAIPrompt : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Available quick-action commands displayed above the input.</summary>
    [Parameter] public IReadOnlyList<AIPromptCommand> Commands { get; set; } = [];

    /// <summary>The current output / response to display.</summary>
    [Parameter] public AIPromptOutput? Output { get; set; }

    /// <summary>Placeholder text for the prompt input.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Whether the input is disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Fired when the user submits a prompt.</summary>
    [Parameter] public EventCallback<string> OnPromptSubmit { get; set; }

    /// <summary>Fired when a command button is clicked.</summary>
    [Parameter] public EventCallback<AIPromptCommand> OnCommandClick { get; set; }

    /// <summary>Fired when the copy button is clicked.</summary>
    [Parameter] public EventCallback<AIPromptOutput> OnOutputCopy { get; set; }

    /// <summary>Fired when the user rates the output. Rating: true = positive, false = negative, null = cleared.</summary>
    [Parameter] public EventCallback<(AIPromptOutput Output, bool? Rating)> OnOutputRate { get; set; }

    private string _inputValue = string.Empty;
    private string _placeholderText => Placeholder ?? Loc["TmAIPrompt_Placeholder"];
    private bool _isSubmitDisabled => Disabled || string.IsNullOrWhiteSpace(_inputValue);

    private async Task HandleSubmitAsync()
    {
        if (_isSubmitDisabled) return;
        var prompt = _inputValue.Trim();
        _inputValue = string.Empty;
        await OnPromptSubmit.InvokeAsync(prompt);
    }

    private async Task HandleCommandClick(AIPromptCommand command)
    {
        if (command.IsDisabled) return;
        await OnCommandClick.InvokeAsync(command);
    }

    private async Task HandleCopyAsync()
    {
        if (Output is null) return;
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", Output.Content);
        }
        catch { /* ignore clipboard errors */ }
        await OnOutputCopy.InvokeAsync(Output);
    }

    private async Task HandleRateAsync(bool? rating)
    {
        if (Output is null) return;
        await OnOutputRate.InvokeAsync((Output, rating));
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await HandleSubmitAsync();
        }
    }
}
