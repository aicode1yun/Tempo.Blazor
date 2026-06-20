using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Components.DocumentEditor.Clipboard;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public sealed class TmDocumentPasteReportTests : LocalizationTestBase
{
    [Fact]
    public void PasteReport_RendersWarnings()
    {
        var cut = RenderComponent<TmDocumentPasteReport>(p => p
            .Add(x => x.Warnings, [new DocumentClipboardWarning { Code = "stripped-element", Message = "Script removed" }]));

        cut.Find("[data-testid='document-paste-report']").TextContent.Should().Contain("Paste adjusted 1 item");
    }

    [Fact]
    public void PasteReport_TogglesDetails()
    {
        var cut = RenderComponent<TmDocumentPasteReport>(p => p
            .Add(x => x.Warnings, [new DocumentClipboardWarning { Code = "unsafe-link-removed", Message = "Link removed" }]));

        cut.FindAll("[data-testid='document-paste-report-details']").Should().BeEmpty();
        cut.Find("[data-testid='document-paste-report-toggle']").Click();

        cut.Find("[data-testid='document-paste-report-details']").TextContent.Should().Contain("unsafe-link-removed");
    }

    [Fact]
    public void PasteReport_CloseInvokesCallback()
    {
        var closed = false;
        var cut = RenderComponent<TmDocumentPasteReport>(p => p
            .Add(x => x.Warnings, [new DocumentClipboardWarning { Code = "w", Message = "m" }])
            .Add(x => x.OnClose, EventCallback.Factory.Create(this, () => closed = true)));

        cut.Find("[data-testid='document-paste-report-close']").Click();

        closed.Should().BeTrue();
    }
}
