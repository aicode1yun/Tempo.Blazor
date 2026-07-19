using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Localization;

namespace Tempo.Blazor.Reporting.Tests.Fixtures;

public abstract class ReportingComponentTestBase : BunitContext
{
    protected ReportingComponentTestBase()
    {
        Services.AddSingleton<ITmLocalizer>(new ReportingTestLocalizer());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/Tempo.Blazor.Reporting/js/reporting/tm-report-viewer.bundle.js");
        module.Setup<string>("mount", _ => true).SetResult("viewer-test-handle");
        module.SetupVoid("update", _ => true).SetVoidResult();
        module.SetupVoid("downloadFile", _ => true).SetVoidResult();
        module.SetupVoid("printPdf", _ => true).SetVoidResult();
        module.SetupVoid("dispose", _ => true).SetVoidResult();
    }
}
