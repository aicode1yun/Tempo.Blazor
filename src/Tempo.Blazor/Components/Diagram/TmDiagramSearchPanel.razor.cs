using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>Floating search panel for the diagram editor.</summary>
public partial class TmDiagramSearchPanel : ComponentBase
{
    /// <summary>Current search query.</summary>
    [Parameter] public string Query { get; set; } = string.Empty;

    /// <summary>Fires when the query changes.</summary>
    [Parameter] public EventCallback<string> QueryChanged { get; set; }

    /// <summary>Total number of matches.</summary>
    [Parameter] public int ResultsCount { get; set; }

    /// <summary>Zero-based index of the currently highlighted match.</summary>
    [Parameter] public int CurrentIndex { get; set; }

    /// <summary>Fires when the active match index changes.</summary>
    [Parameter] public EventCallback<int> CurrentIndexChanged { get; set; }

    /// <summary>Fires when the panel should close.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    private async Task OnQueryChanged(string value)
    {
        Query = value;
        await QueryChanged.InvokeAsync(value);
    }

    private async Task OnPreviousClick()
    {
        if (ResultsCount == 0) return;
        var idx = (CurrentIndex - 1 + ResultsCount) % ResultsCount;
        await CurrentIndexChanged.InvokeAsync(idx);
    }

    private async Task OnNextClick()
    {
        if (ResultsCount == 0) return;
        var idx = (CurrentIndex + 1) % ResultsCount;
        await CurrentIndexChanged.InvokeAsync(idx);
    }

    private async Task OnCloseClick()
        => await OnClose.InvokeAsync();
}
