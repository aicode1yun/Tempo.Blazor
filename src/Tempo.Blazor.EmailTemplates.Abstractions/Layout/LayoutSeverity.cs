namespace Tempo.Blazor.EmailTemplates.Abstractions.Layout;

/// <summary>Severity of a layout validation message.</summary>
public enum LayoutSeverity
{
    /// <summary>A non-fatal issue worth surfacing.</summary>
    Warning,

    /// <summary>A structural problem that would produce broken or invalid output.</summary>
    Error,
}
