#pragma warning disable MA0016, MA0048

namespace Tempo.Reporting.Abstractions.Definitions;

/// <summary>Band kind in a pixel/print-based report definition.</summary>
public enum ReportBandKind
{
    /// <summary>Renders once at the beginning of the report.</summary>
    ReportHeader,

    /// <summary>Renders once at the end of the report.</summary>
    ReportFooter,

    /// <summary>Renders at the top of each page.</summary>
    PageHeader,

    /// <summary>Renders at the bottom of each page.</summary>
    PageFooter,

    /// <summary>Renders before each group.</summary>
    GroupHeader,

    /// <summary>Renders after each group.</summary>
    GroupFooter,

    /// <summary>Renders for each detail row.</summary>
    Detail,
}

/// <summary>Report band containing absolutely positioned elements.</summary>
public sealed record ReportBand
{
    /// <summary>Kind of this band.</summary>
    public ReportBandKind Kind { get; init; } = ReportBandKind.Detail;

    /// <summary>Band height in page units.</summary>
    public double Height { get; init; }

    /// <summary>Whether the band should move to the next page when it does not fit the current page.</summary>
    public bool KeepTogether { get; init; }

    /// <summary>Elements rendered inside the band.</summary>
    public List<ReportElement> Elements { get; init; } = [];

    /// <summary>Optional visibility expression.</summary>
    public string? VisibleExpression { get; init; }
}

/// <summary>Top-level band slots for a report.</summary>
public sealed record ReportBandCollection
{
    /// <summary>Report header band.</summary>
    public ReportBand? ReportHeader { get; init; }

    /// <summary>Report footer band.</summary>
    public ReportBand? ReportFooter { get; init; }

    /// <summary>Page header band.</summary>
    public ReportBand? PageHeader { get; init; }

    /// <summary>Page footer band.</summary>
    public ReportBand? PageFooter { get; init; }

    /// <summary>Detail band.</summary>
    public ReportBand? Detail { get; init; }

    /// <summary>Groups with header and footer bands.</summary>
    public List<ReportGroupDefinition> Groups { get; init; } = [];
}

/// <summary>Grouping definition with optional header and footer bands.</summary>
public sealed record ReportGroupDefinition
{
    /// <summary>Unique group name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Expression that produces the group key.</summary>
    public string Expression { get; init; } = string.Empty;

    /// <summary>Optional group header band.</summary>
    public ReportBand? GroupHeader { get; init; }

    /// <summary>Optional group footer band.</summary>
    public ReportBand? GroupFooter { get; init; }
}

#pragma warning restore MA0016, MA0048
