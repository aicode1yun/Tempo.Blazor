using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Layout;

/// <summary>
/// Declares a dockable pane inside a <see cref="TmDockManager"/>.
/// The pane registers itself via cascading parameter and its content
/// is rendered by the manager in the appropriate docking zone.
/// </summary>
public partial class TmDockPane : ComponentBase, IDisposable
{
    /// <summary>Parent dock manager.</summary>
    [CascadingParameter] public TmDockManager? Parent { get; set; }

    /// <summary>Unique identifier.</summary>
    [Parameter] public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display title.</summary>
    [Parameter] public string Title { get; set; } = string.Empty;

    /// <summary>Optional icon name.</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>Whether the pane can be floated.</summary>
    [Parameter] public bool CanFloat { get; set; } = true;

    /// <summary>Whether the pane can be closed.</summary>
    [Parameter] public bool CanClose { get; set; } = true;

    /// <summary>Whether the pane is visible.</summary>
    [Parameter] public bool IsVisible { get; set; } = true;

    /// <summary>Whether the pane is the active tab in its group.</summary>
    [Parameter] public bool IsActive { get; set; }

    /// <summary>Current docking position.</summary>
    [Parameter] public DockPosition Position { get; set; } = DockPosition.Center;

    /// <summary>Desired width in pixels.</summary>
    [Parameter] public double? Width { get; set; }

    /// <summary>Desired height in pixels.</summary>
    [Parameter] public double? Height { get; set; }

    /// <summary>Display order.</summary>
    [Parameter] public int Order { get; set; }

    /// <summary>Floating X position.</summary>
    [Parameter] public double FloatX { get; set; } = 100;

    /// <summary>Floating Y position.</summary>
    [Parameter] public double FloatY { get; set; } = 100;

    /// <summary>Content rendered inside the pane.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Converts this pane to its model representation.</summary>
    internal DockPane ToModel() => new()
    {
        Id = Id,
        Title = Title,
        Icon = Icon,
        CanFloat = CanFloat,
        CanClose = CanClose,
        IsVisible = IsVisible,
        IsActive = IsActive,
        Position = Position,
        Width = Width,
        Height = Height,
        Order = Order,
        FloatX = FloatX,
        FloatY = FloatY
    };

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Parent?.AddPane(this);
        base.OnInitialized();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Parent?.RemovePane(this);
    }
}
