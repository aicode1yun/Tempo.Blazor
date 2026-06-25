namespace Tempo.Reporting.Engine.Snapshot;

/// <summary>Thrown when a report snapshot cannot be read safely.</summary>
public sealed class ReportSnapshotJsonException : Exception
{
    /// <summary>Initializes a new snapshot JSON exception.</summary>
    public ReportSnapshotJsonException(string message)
        : base(message)
    {
    }
}
