using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database.Cells;

public abstract class TmNotionDbCellBase : ComponentBase
{
    [Parameter, EditorRequired] public IDatabaseField  Field    { get; set; } = default!;
    [Parameter]                 public object?         Value    { get; set; }
    [Parameter]                 public bool            ReadOnly { get; set; }
    [Parameter]                 public bool            IsEditing { get; set; }

    [Parameter] public EventCallback<object?> OnCommit       { get; set; }
    [Parameter] public EventCallback          OnCancel       { get; set; }
    [Parameter] public EventCallback          OnEditRequested { get; set; }

    private bool _wasEditing;

    protected override void OnParametersSet()
    {
        if (IsEditing && !_wasEditing)
            OnStartEdit();
        _wasEditing = IsEditing;
    }

    protected virtual void OnStartEdit() { }

    protected string StringValue => Value?.ToString() ?? string.Empty;

    protected async Task CommitAsync(object? value) => await OnCommit.InvokeAsync(value);
    protected async Task CancelAsync()              => await OnCancel.InvokeAsync();
    protected async Task RequestEditAsync()         => await OnEditRequested.InvokeAsync();

    protected async Task HandleKeyAsync(KeyboardEventArgs e, Func<Task> onCommit)
    {
        switch (e.Key)
        {
            case "Enter": await onCommit();    break;
            case "Escape": await CancelAsync(); break;
        }
    }

    protected static string AvatarColor(string seed)
    {
        var colors = new[]
        {
            "#3b82f6","#10b981","#f59e0b","#ef4444","#8b5cf6",
            "#06b6d4","#f97316","#84cc16","#ec4899","#14b8a6"
        };
        var idx = Math.Abs(seed.GetHashCode()) % colors.Length;
        return colors[idx];
    }

    protected static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[^1][0])}"
            : name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
    }
}
