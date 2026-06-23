using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Reporting.Abstractions.Tests.Data;

public sealed class InMemoryReportDefinitionStoreTests
{
    [Fact]
    public async Task Store_IsTenantScopedAcrossFoldersReportsAndRevisions()
    {
        var store = new InMemoryReportDefinitionStore();
        var tenantA = new ReportExecutionContext("tenant-a", "author-a", "en-US");
        var tenantB = new ReportExecutionContext("tenant-b", "author-b", "en-US");

        var folderA = await store.SaveFolderAsync(
            new ReportFolderRecord { FolderId = "finance", Name = "Finance", Path = "/Finance" },
            tenantA);
        await store.SaveFolderAsync(
            new ReportFolderRecord { FolderId = "finance", Name = "Finance B", Path = "/Finance" },
            tenantB);

        var firstRevision = await store.SaveReportAsync(
            new ReportDefinitionRecord
            {
                ReportId = "orders",
                FolderId = folderA.FolderId,
                Name = "Orders",
                Description = "Tenant A orders",
            },
            "{\"schemaVersion\":1,\"name\":\"Orders\"}",
            publish: true,
            tenantA);
        var secondRevision = await store.SaveReportAsync(
            new ReportDefinitionRecord
            {
                ReportId = "orders",
                FolderId = folderA.FolderId,
                Name = "Orders v2",
            },
            "{\"schemaVersion\":1,\"name\":\"Orders v2\"}",
            publish: false,
            tenantA);

        var reportA = await store.LoadReportAsync("orders", tenantA);
        var reportB = await store.LoadReportAsync("orders", tenantB);
        var reportsA = await store.ListReportsAsync("finance", tenantA);
        var foldersB = await store.ListFoldersAsync(tenantB);
        var revisions = await store.ListRevisionsAsync("orders", tenantA);

        firstRevision.RevisionNumber.Should().Be(1);
        secondRevision.RevisionNumber.Should().Be(2);
        reportA!.LatestRevisionId.Should().Be(secondRevision.RevisionId);
        reportB.Should().BeNull();
        reportsA.Should().ContainSingle(r => r.ReportId == "orders");
        foldersB.Should().ContainSingle(f => f.Name == "Finance B");
        revisions.Select(r => r.RevisionNumber).Should().Equal(1, 2);
        (await store.LoadRevisionAsync("orders", firstRevision.RevisionId, tenantA))!.DefinitionJson
            .Should().Contain("\"Orders\"");
    }
}
