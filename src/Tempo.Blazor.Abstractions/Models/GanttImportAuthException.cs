namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Thrown when an import source returns HTTP 401 Unauthorized.</summary>
public class GanttImportAuthException : Exception
{
    public GanttImportAuthException()
        : base("Authentication failed while importing Gantt data. Check your credentials.") { }
}
