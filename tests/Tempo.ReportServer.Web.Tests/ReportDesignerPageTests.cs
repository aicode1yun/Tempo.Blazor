using Tempo.ReportServer.Web.Pages;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class ReportDesignerPageTests : ReportServerWebTestBase
{
    [Fact]
    public void DesignerPage_RendersDesignerForCatalogReport()
    {
        SignIn();

        var cut = RenderComponent<ReportDesignerPage>(parameters => parameters
            .Add(component => component.ReportId, "sales-register"));

        cut.Find("[data-testid='f13-designer-page']").TextContent.Should().Contain("Sales Register");
        cut.Find("[data-testid='tm-report-designer']").Should().NotBeNull();
        cut.Find("[data-testid='nav-designer']").ClassList.Should().Contain("rs-nav__item--active");
    }
}
