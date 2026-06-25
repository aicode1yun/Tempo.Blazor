namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Severity level used to style a shared notification.</summary>
public enum TmNotificationSeverity
{
    /// <summary>Informational notification.</summary>
    Info,

    /// <summary>Successful outcome notification.</summary>
    Success,

    /// <summary>Warning notification.</summary>
    Warning,

    /// <summary>Error or failure notification.</summary>
    Error
}
