#pragma warning disable MA0158

using Tempo.Blazor.Reporting.Models;

namespace Tempo.ReportServer.Web.Services;

/// <summary>In-memory catalog used by the F12 report server front end.</summary>
public sealed class ReportServerCatalogStore
{
    private readonly Dictionary<string, ReportServerTenantCatalog> _catalogs = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>Creates the seeded report catalog.</summary>
    public ReportServerCatalogStore()
    {
        _catalogs["northwind"] = CreateNorthwind();
        _catalogs["contoso"] = CreateContoso();
    }

    /// <summary>Gets a tenant catalog.</summary>
    public ReportServerTenantCatalog GetCatalog(string tenantId)
    {
        lock (_gate)
        {
            return _catalogs.TryGetValue(tenantId, out var catalog) ? catalog : _catalogs["northwind"];
        }
    }

    /// <summary>Gets a report by route path, for example <c>finance/sales-register</c>.</summary>
    public ReportExplorerReportItem? GetReportByPath(string tenantId, string path)
    {
        var normalized = NormalizeReportPath(path);
        lock (_gate)
        {
            return GetCatalog(tenantId).Reports.FirstOrDefault(report =>
                string.Equals(NormalizeReportPath(report.Path), normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Creates a folder under the selected parent.</summary>
    public void CreateFolder(string tenantId, string parentPath, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lock (_gate)
        {
            var catalog = GetCatalog(tenantId);
            var path = CombinePath(parentPath, name);
            if (FlattenFolders(catalog.RootFolder).Any(folder => string.Equals(folder.Path, path, StringComparison.Ordinal)))
            {
                return;
            }

            catalog.RootFolder = AddFolder(catalog.RootFolder, parentPath, new ReportExplorerFolder(path, name.Trim()));
        }
    }

    /// <summary>Moves a report to another folder.</summary>
    public void MoveReport(string tenantId, string reportId, string targetFolderPath)
    {
        lock (_gate)
        {
            var report = GetCatalog(tenantId).Reports.FirstOrDefault(item =>
                string.Equals(item.Id, reportId, StringComparison.Ordinal));
            if (report is null)
            {
                return;
            }

            var index = GetCatalog(tenantId).Reports.IndexOf(report);
            GetCatalog(tenantId).Reports[index] = new ReportExplorerReportItem(
                report.Id,
                report.Name,
                CombinePath("/reports", targetFolderPath.Trim('/'), Slug(report.Name)),
                targetFolderPath,
                report.Description,
                report.OwnerName,
                DateTimeOffset.UtcNow,
                report.Revision + 1,
                report.ThumbnailUrl,
                report.Tags);
        }
    }

    /// <summary>Tests a data source connection.</summary>
    public void TestDataSource(string tenantId, string dataSourceId)
    {
        lock (_gate)
        {
            var source = GetCatalog(tenantId).DataSources.FirstOrDefault(item =>
                string.Equals(item.Id, dataSourceId, StringComparison.Ordinal));
            if (source is null)
            {
                return;
            }

            source.LastTestSucceeded = true;
            source.LastTestMessage = $"Connected at {DateTimeOffset.Now:HH:mm:ss}";
        }
    }

    /// <summary>Adds a data source from the management form.</summary>
    public void AddDataSource(string tenantId, string name, string provider, string endpoint)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lock (_gate)
        {
            GetCatalog(tenantId).DataSources.Add(new ReportServerDataSource
            {
                Id = Slug(name),
                Name = name.Trim(),
                Provider = string.IsNullOrWhiteSpace(provider) ? "SQL" : provider.Trim(),
                Endpoint = string.IsNullOrWhiteSpace(endpoint) ? "Server=reporting;" : endpoint.Trim(),
            });
        }
    }

    /// <summary>Adds an ACL row from the permissions editor.</summary>
    public void AddPermission(string tenantId, string folderPath, string subject, string role, bool isDeny)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return;
        }

        lock (_gate)
        {
            GetCatalog(tenantId).Permissions.Add(new ReportServerPermissionEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                FolderPath = string.IsNullOrWhiteSpace(folderPath) ? "/" : folderPath,
                Subject = subject.Trim(),
                Role = string.IsNullOrWhiteSpace(role) ? "Viewer" : role,
                IsDeny = isDeny,
            });
        }
    }

    /// <summary>Marks a revision as active.</summary>
    public void RollbackRevision(string tenantId, string revisionId)
    {
        lock (_gate)
        {
            var catalog = GetCatalog(tenantId);
            var revision = catalog.Revisions.FirstOrDefault(item => string.Equals(item.Id, revisionId, StringComparison.Ordinal));
            if (revision is null)
            {
                return;
            }

            foreach (var item in catalog.Revisions.Where(item => string.Equals(item.ReportId, revision.ReportId, StringComparison.Ordinal)))
            {
                item.IsCurrent = string.Equals(item.Id, revisionId, StringComparison.Ordinal);
            }
        }
    }

    private static ReportServerTenantCatalog CreateNorthwind()
    {
        var now = DateTimeOffset.Parse("2026-06-20T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        return new ReportServerTenantCatalog
        {
            RootFolder = new ReportExplorerFolder(
                "/",
                "Reports",
                [
                    new ReportExplorerFolder("/finance", "Finance", [new ReportExplorerFolder("/finance/month-end", "Month End")]) { ReportCount = 3 },
                    new ReportExplorerFolder("/operations", "Operations") { ReportCount = 2 },
                    new ReportExplorerFolder("/executive", "Executive") { ReportCount = 2 },
                ]),
            Reports =
            [
                Report("sales-register", "Sales Register", "/finance", "Sales orders, totals and payment status.", "Sales Ops", now.AddHours(-5), 12, ["Sales", "Finance"]),
                Report("invoice-aging", "Invoice Aging", "/finance", "Open receivables grouped by due date.", "Finance", now.AddDays(-1), 7, ["AR", "Cash"]),
                Report("sales-dashboard", "Dashboard prodejů", "/executive", "Executive sales dashboard with three charts and an order table.", "Sales BI", now.AddHours(-7), 3, ["Executive", "Sales"]),
                Report("margin-watch", "Margin Watch", "/executive", "Gross margin trend by product family.", "FP&A", now.AddDays(-2), 4, ["Executive"]),
                Report("fulfillment-sla", "Fulfillment SLA", "/operations", "Warehouse SLA by region and carrier.", "Operations", now.AddDays(-3), 9, ["SLA"]),
            ],
            DataSources =
            [
                new ReportServerDataSource { Id = "erp-sql", Name = "ERP SQL", Provider = "SQL", Endpoint = "Server=erp-sql;Database=Reporting;", LastTestSucceeded = true, LastTestMessage = "Connected" },
                new ReportServerDataSource { Id = "crm-rest", Name = "CRM REST", Provider = "REST JSON", Endpoint = "https://api.example.test/crm", LastTestMessage = "Not tested" },
            ],
            Permissions =
            [
                new ReportServerPermissionEntry { Id = "acl-finance-admin", FolderPath = "/finance", Subject = "finance-admins", Role = "TenantAdmin" },
                new ReportServerPermissionEntry { Id = "acl-sales-author", FolderPath = "/finance", Subject = "sales-authors", Role = "Author" },
                new ReportServerPermissionEntry { Id = "acl-temp-deny", FolderPath = "/executive", Subject = "contractors", Role = "Viewer", IsDeny = true },
            ],
            Revisions =
            [
                Revision("sales-register", 12, "Pavel Author", now.AddHours(-2), "Added IncludeClosed parameter; changed Sales dataset timeout.", isCurrent: true),
                Revision("sales-register", 11, "Pavel Author", now.AddDays(-2), "Updated totals textbox format and footer metadata."),
                Revision("sales-dashboard", 3, "Pavel Author", now.AddHours(-7), "Added engine-drawn chart dashboard fixture.", isCurrent: true),
                Revision("invoice-aging", 7, "Eva Finance", now.AddDays(-1), "Changed aging buckets from 15 to 30 days.", isCurrent: true),
            ],
        };
    }

    private static ReportServerTenantCatalog CreateContoso()
        => new()
        {
            RootFolder = new ReportExplorerFolder(
                "/",
                "Reports",
                [
                    new ReportExplorerFolder("/operations", "Operations") { ReportCount = 2 },
                    new ReportExplorerFolder("/finance", "Finance") { ReportCount = 1 },
                ]),
            Reports =
            [
                Report("sales-register", "Sales Register", "/operations", "Contoso order register with regional filters.", "Ops Analytics", DateTimeOffset.UtcNow.AddDays(-1), 5, ["Sales"]),
                Report("fulfillment-sla", "Fulfillment SLA", "/operations", "Carrier and warehouse SLA report.", "Ops Analytics", DateTimeOffset.UtcNow.AddDays(-2), 8, ["SLA"]),
            ],
            DataSources =
            [
                new ReportServerDataSource { Id = "contoso-warehouse", Name = "Warehouse Lakehouse", Provider = "SQL", Endpoint = "Server=lakehouse;Database=Warehouse;", LastTestSucceeded = true, LastTestMessage = "Connected" },
            ],
            Permissions =
            [
                new ReportServerPermissionEntry { Id = "contoso-ops", FolderPath = "/operations", Subject = "contoso-ops", Role = "Author" },
            ],
            Revisions =
            [
                Revision("fulfillment-sla", 8, "Dana Ops", DateTimeOffset.UtcNow.AddHours(-8), "Added carrier filter and SLA breach badge.", isCurrent: true),
            ],
        };

    private static ReportExplorerReportItem Report(
        string id,
        string name,
        string folderPath,
        string description,
        string owner,
        DateTimeOffset modified,
        int revision,
        IReadOnlyList<string> tags)
        => new(
            id,
            name,
            CombinePath("/reports", folderPath.Trim('/'), id),
            folderPath,
            description,
            owner,
            modified,
            revision,
            ThumbnailDataUrl(name, tags.FirstOrDefault() ?? "Report"),
            tags);

    private static ReportServerRevision Revision(
        string reportId,
        int version,
        string author,
        DateTimeOffset createdAt,
        string diff,
        bool isCurrent = false)
        => new()
        {
            Id = $"{reportId}-r{version.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            ReportId = reportId,
            Version = version,
            Author = author,
            CreatedAt = createdAt,
            MetadataDiff = diff,
            IsCurrent = isCurrent,
        };

    private static ReportExplorerFolder AddFolder(ReportExplorerFolder folder, string parentPath, ReportExplorerFolder newFolder)
    {
        if (string.Equals(folder.Path, parentPath, StringComparison.Ordinal))
        {
            return new ReportExplorerFolder(folder.Path, folder.Name, [.. folder.Children, newFolder])
            {
                ReportCount = folder.ReportCount,
            };
        }

        return new ReportExplorerFolder(
            folder.Path,
            folder.Name,
            folder.Children.Select(child => AddFolder(child, parentPath, newFolder)).ToArray())
        {
            ReportCount = folder.ReportCount,
        };
    }

    private static IEnumerable<ReportExplorerFolder> FlattenFolders(ReportExplorerFolder folder)
    {
        yield return folder;
        foreach (var child in folder.Children)
        {
            foreach (var nested in FlattenFolders(child))
            {
                yield return nested;
            }
        }
    }

    private static string NormalizeReportPath(string path)
        => path.Trim('/').StartsWith("reports/", StringComparison.OrdinalIgnoreCase)
            ? path.Trim('/')
            : $"reports/{path.Trim('/')}";

    private static string CombinePath(params string[] parts)
        => "/" + string.Join('/', parts.Select(part => part.Trim('/')).Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string Slug(string value)
        => value.Trim().ToLowerInvariant().Replace(' ', '-');

    private static string ThumbnailDataUrl(string title, string label)
    {
        var escapedTitle = Uri.EscapeDataString(title);
        var escapedLabel = Uri.EscapeDataString(label);
        return "data:image/svg+xml;utf8," +
            $"<svg xmlns='http://www.w3.org/2000/svg' width='520' height='320' viewBox='0 0 520 320'><rect width='520' height='320' fill='%23f8fafc'/><rect x='34' y='32' width='452' height='256' rx='12' fill='%23ffffff' stroke='%23cbd5e1'/><rect x='62' y='70' width='170' height='18' rx='4' fill='%230f766e'/><rect x='62' y='112' width='390' height='12' rx='3' fill='%23cbd5e1'/><rect x='62' y='142' width='330' height='12' rx='3' fill='%23e2e8f0'/><rect x='62' y='188' width='96' height='54' rx='8' fill='%23dbeafe'/><rect x='178' y='166' width='96' height='76' rx='8' fill='%23ccfbf1'/><rect x='294' y='132' width='96' height='110' rx='8' fill='%23fee2e2'/><text x='62' y='274' font-family='Inter,Arial' font-size='20' font-weight='700' fill='%230f172a'>{escapedTitle}</text><text x='390' y='86' font-family='Inter,Arial' font-size='14' font-weight='700' fill='%23475569'>{escapedLabel}</text></svg>";
    }
}

#pragma warning restore MA0158
