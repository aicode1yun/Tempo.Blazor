namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Priority used when toolbar items move into overflow surfaces.</summary>
public enum ToolbarItemPriority
{
    /// <summary>Primary item that should stay prominent when possible.</summary>
    Primary,

    /// <summary>Secondary item that can move to overflow earlier.</summary>
    Secondary,

    /// <summary>Item intended mainly for overflow or low-frequency command surfaces.</summary>
    OverflowOnly
}
