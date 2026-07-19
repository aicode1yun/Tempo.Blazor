using Tempo.ReportServer.Web.Components;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

public sealed class ReportServerShellTests : ReportServerWebTestBase
{
    [Fact]
    public void Shell_RendersNavigationUserAndTenantSwitcher()
    {
        var session = SignIn();
        var cut = Render<ReportServerShell>(parameters => parameters
            .Add(component => component.Title, "Reports")
            .Add(component => component.ActiveSection, "reports")
            .AddChildContent("<div data-testid='shell-body'>Body</div>"));

        cut.Find("[data-testid='report-server-shell']").TextContent.Should().Contain("Tempo Report Server");
        cut.Find("[data-testid='signed-in-user']").TextContent.Should().Contain("Pavel Author");
        cut.Find("[data-testid='tenant-switcher']").Change("contoso");

        session.CurrentTenantId.Should().Be("contoso");
        cut.Find("[data-testid='nav-reports']").ClassList.Should().Contain("rs-nav__item--active");
    }
}
