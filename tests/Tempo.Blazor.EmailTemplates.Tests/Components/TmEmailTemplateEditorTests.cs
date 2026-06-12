using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tempo.Blazor.EmailTemplates;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Components;
using Tempo.Blazor.Localization;

namespace Tempo.Blazor.EmailTemplates.Tests.Components;

public class TmEmailTemplateEditorTests : TestContext
{
    public TmEmailTemplateEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITmLocalizer>(new EchoTmLocalizer());
        Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        Services.AddHttpClient();
        Services.AddTempoEmailTemplates();
    }

    [Fact]
    public void Renders_Toolbar_And_ThreePanels()
    {
        var cut = RenderComponent<TmEmailTemplateEditor>();

        cut.Find("[data-tm-email-editor]").Should().NotBeNull();
        cut.Find("[role=toolbar]").Should().NotBeNull();
        cut.FindAll("[data-tm-toolbox]").Should().ContainSingle();
        cut.FindAll("[data-tm-canvas]").Should().ContainSingle();
        cut.FindAll("[data-tm-properties]").Should().ContainSingle();
    }

    [Fact]
    public void EmptyDocument_ShowsEmptyStateInCanvas()
    {
        var cut = RenderComponent<TmEmailTemplateEditor>();

        var canvas = cut.Find("[data-tm-canvas]");
        canvas.TextContent.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Save_InvokesOnSaveWithTheDocument()
    {
        EmailTemplateDocument? saved = null;
        var doc = new EmailTemplateDocument { Name = "X" };
        var doc2section = new EmailSection();
        doc2section.Columns.Add(new EmailColumn());
        doc.Sections.Add(doc2section);

        var cut = RenderComponent<TmEmailTemplateEditor>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.OnSave, d => saved = d));

        cut.Find("[data-tm-save]").Click();

        saved.Should().BeSameAs(doc);
    }

    [Fact]
    public void BoundDocument_IsUsedInsteadOfInternalDefault()
    {
        var doc = new EmailTemplateDocument();
        doc.Sections.Add(new EmailSection());

        var cut = RenderComponent<TmEmailTemplateEditor>(p => p.Add(c => c.Document, doc));

        // A document with a section should not show the empty-state hint text.
        cut.Markup.Should().NotContain("Drag a block here");
    }

    private sealed class EchoTmLocalizer : ITmLocalizer
    {
        public string this[string key] => key;
        public string this[string key, params object[] arguments] => key;
    }
}
