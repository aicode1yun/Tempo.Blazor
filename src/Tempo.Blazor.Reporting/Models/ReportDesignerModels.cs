#pragma warning disable MA0048

using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Blazor.Reporting.Models;

/// <summary>Designer save operation kind.</summary>
public enum ReportDesignerSaveKind
{
    /// <summary>Save as an unpublished draft revision.</summary>
    Draft,

    /// <summary>Publish the current definition as an active revision.</summary>
    Publish,
}

/// <summary>Event args raised when a designer save operation completes.</summary>
public sealed class ReportDesignerSaveEventArgs
{
    /// <summary>Creates designer save event args.</summary>
    public ReportDesignerSaveEventArgs(ReportDefinition definition, ReportDesignerSaveKind kind, int revision)
    {
        Definition = definition;
        Kind = kind;
        Revision = revision;
    }

    /// <summary>Saved report definition.</summary>
    public ReportDefinition Definition { get; }

    /// <summary>Save operation kind.</summary>
    public ReportDesignerSaveKind Kind { get; }

    /// <summary>Revision number assigned by the designer host.</summary>
    public int Revision { get; }
}

#pragma warning restore MA0048
