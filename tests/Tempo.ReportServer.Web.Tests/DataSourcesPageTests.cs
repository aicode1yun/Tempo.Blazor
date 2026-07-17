using Tempo.ReportServer.Web.Pages;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class DataSourcesPageTests : ReportServerWebTestBase
{
    [Fact]
    public void DataSourcesPage_ListsSourcesFromTypedClient()
    {
        SignIn();
        var cut = RenderComponent<DataSourcesPage>();

        cut.Find("[data-testid='datasources-table']").TextContent.Should().Contain("ERP SQL");
    }

    [Fact]
    public void DataSourcesPage_AddSource_CallsTypedClient_AndShowsRow()
    {
        SignIn();
        var cut = RenderComponent<DataSourcesPage>();

        cut.Find("[data-testid='datasource-name']").Input("Warehouse Lakehouse");
        cut.Find("[data-testid='datasource-endpoint']").Input("Server=lakehouse;Database=Warehouse;");
        cut.Find("[data-testid='datasource-add']").Click();

        cut.Find("[data-testid='datasources-table']").TextContent.Should().Contain("Warehouse Lakehouse");
    }

    [Fact]
    public void DataSourcesPage_TestConnection_CallsTypedClient_AndShowsResult()
    {
        SignIn();
        var cut = RenderComponent<DataSourcesPage>();

        cut.Find("[data-testid='test-datasource-ds-erp']").Click();

        cut.Find("[data-testid='datasources-table']").TextContent.Should().Contain("Connection metadata is valid.");
    }
}
