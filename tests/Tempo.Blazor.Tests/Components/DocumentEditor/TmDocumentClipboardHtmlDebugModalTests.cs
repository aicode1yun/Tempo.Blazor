using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public sealed class TmDocumentClipboardHtmlDebugModalTests : LocalizationTestBase
{
    [Fact]
    public void Modal_WhenClosed_RendersNothing()
    {
        var cut = Render<TmDocumentClipboardHtmlDebugModal>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.RawHtml, "<p>raw</p>"));

        cut.FindAll("[data-testid='document-clipboard-html-debug-modal']").Should().BeEmpty();
    }

    [Fact]
    public void Modal_ShowsRawNormalizedAndWarnings()
    {
        var cut = Render<TmDocumentClipboardHtmlDebugModal>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.RawHtml, "<p>phase18 raw</p>")
            .Add(p => p.NormalizedJson, """[{"text":"phase18 normalized"}]""")
            .Add(p => p.WarningsJson, """[{"code":"stripped-element"}]"""));

        cut.Find("[data-testid='document-clipboard-html-debug-content']").TextContent
            .Should().Contain("phase18 raw");
        cut.Find("[data-testid='document-clipboard-normalized-debug-content']").TextContent
            .Should().Contain("phase18 normalized");
        cut.Find("[data-testid='document-clipboard-warnings-debug-content']").TextContent
            .Should().Contain("stripped-element");
    }
}
