#pragma warning disable MA0016, MA0048

using Tempo.Blazor.Reporting.Models;

namespace Tempo.ReportServer.Web.Services;

/// <summary>Tenant available in the report server shell.</summary>
public sealed record ReportServerTenant(string Id, string Name);

/// <summary>Simple data source row used by the F12 management UI.</summary>
public sealed class ReportServerDataSource
{
    /// <summary>Stable data source identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Provider kind.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Connection endpoint or masked connection string.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Whether the latest test succeeded.</summary>
    public bool LastTestSucceeded { get; set; }

    /// <summary>Human-readable latest test result.</summary>
    public string LastTestMessage { get; set; } = "Not tested";
}

/// <summary>Permission row used by the F12 ACL editor.</summary>
public sealed class ReportServerPermissionEntry
{
    /// <summary>Stable ACL row identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Folder path where the ACL row applies.</summary>
    public string FolderPath { get; set; } = "/";

    /// <summary>User or group subject.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Role granted or denied.</summary>
    public string Role { get; set; } = "Viewer";

    /// <summary>Whether this row is an explicit deny.</summary>
    public bool IsDeny { get; set; }
}

/// <summary>Report revision row displayed by the F12 revision browser.</summary>
public sealed class ReportServerRevision
{
    /// <summary>Stable revision identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Report identifier this revision belongs to.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Revision number.</summary>
    public int Version { get; init; }

    /// <summary>User who created the revision.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Compact metadata diff text.</summary>
    public string MetadataDiff { get; init; } = string.Empty;

    /// <summary>Whether this revision is currently active.</summary>
    public bool IsCurrent { get; set; }
}

/// <summary>Snapshot of report server catalog data for one tenant.</summary>
public sealed class ReportServerTenantCatalog
{
    /// <summary>Folder tree shown in the explorer.</summary>
    public ReportExplorerFolder RootFolder { get; set; } = new("/", "Reports");

    /// <summary>Report items shown in the explorer.</summary>
    public List<ReportExplorerReportItem> Reports { get; init; } = [];

    /// <summary>Data source rows.</summary>
    public List<ReportServerDataSource> DataSources { get; init; } = [];

    /// <summary>Permission rows.</summary>
    public List<ReportServerPermissionEntry> Permissions { get; init; } = [];

    /// <summary>Revision rows.</summary>
    public List<ReportServerRevision> Revisions { get; init; } = [];
}

#pragma warning restore MA0016, MA0048
