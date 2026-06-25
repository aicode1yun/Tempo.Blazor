namespace Tempo.Reporting.Engine.Snapshot;

/// <summary>Primitive command kinds supported by the F0 reporting painter contract.</summary>
public enum ReportSnapshotCommandType
{
    /// <summary>Text run with an explicit expected run width.</summary>
    TextRun,

    /// <summary>Filled and optionally stroked rectangle.</summary>
    Rectangle,

    /// <summary>Straight line segment.</summary>
    Line,

    /// <summary>Vector path.</summary>
    Path,

    /// <summary>Image drawn into an absolute rectangle.</summary>
    Image,

    /// <summary>Pushes a rectangular clipping region.</summary>
    ClipPush,

    /// <summary>Pops the current clipping region.</summary>
    ClipPop
}
