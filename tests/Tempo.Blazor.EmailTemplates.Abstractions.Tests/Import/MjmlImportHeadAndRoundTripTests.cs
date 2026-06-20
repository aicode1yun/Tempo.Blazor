using Tempo.Blazor.EmailTemplates.Abstractions.Import;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Import;

public class MjmlImportHeadAndRoundTripTests
{
    private static readonly MjmlImporter Importer = new();
    private static readonly MjmlGenerator Generator = new();

    [Fact]
    public void ImportsFullHead()
    {
        const string mjml = """
        <mjml><mj-head>
          <mj-breakpoint width="500px" />
          <mj-font name="Roboto" href="https://f/r.css" />
          <mj-attributes>
            <mj-all font-family="Arial"></mj-all>
            <mj-text color="#222"></mj-text>
            <mj-class name="cta" background-color="#f00"></mj-class>
          </mj-attributes>
          <mj-style>.a{color:red}</mj-style>
          <mj-style inline="inline">.b{color:blue}</mj-style>
          <mj-html-attributes>
            <mj-selector path=".a"><mj-html-attribute name="data-id">7</mj-html-attribute></mj-selector>
          </mj-html-attributes>
        </mj-head><mj-body><mj-section><mj-column><mj-text>x</mj-text></mj-column></mj-section></mj-body></mjml>
        """;

        var styles = Importer.Import(mjml).Document!.Styles;

        styles.Breakpoint.Should().Be("500px");
        styles.Fonts.Should().ContainSingle().Which.Name.Should().Be("Roboto");
        styles.Styles.Should().HaveCount(2);
        styles.Styles[1].Inline.Should().BeTrue();
        styles.Attributes.All["font-family"].Should().Be("Arial");
        styles.Attributes.PerTag["mj-text"]["color"].Should().Be("#222");
        styles.Attributes.Classes["cta"]["background-color"].Should().Be("#f00");
        styles.HtmlAttributes.Should().ContainSingle().Which.Attributes["data-id"].Should().Be("7");
    }

    [Fact]
    public void BodyLevelWrapper_IsHoisted_WithWarning()
    {
        const string mjml = "<mjml><mj-body><mj-wrapper><mj-section><mj-column><mj-text>w</mj-text></mj-column></mj-section></mj-wrapper></mj-body></mjml>";

        var result = Importer.Import(mjml);

        result.Document!.Sections.Should().ContainSingle();
        result.Warnings.Should().Contain(w => w.Key == ImportKeys.WrapperFlattened);
    }

    [Fact]
    public void MjInclude_IsResolvedThroughResolver()
    {
        const string mjml = "<mjml><mj-body><mj-include path=\"footer.mjml\" /></mj-body></mjml>";
        var resolver = new FakeResolver("<mj-section><mj-column><mj-text>included</mj-text></mj-column></mj-section>");

        var result = Importer.Import(mjml, resolver);

        result.Document!.Sections.Should().ContainSingle();
        result.Document.Sections[0].Columns[0].Blocks[0].Should().BeOfType<EmailTextBlock>()
            .Which.Content.Should().Contain("included");
    }

    [Fact]
    public void MjInclude_Unresolved_WarnsButDoesNotFail()
    {
        const string mjml = "<mjml><mj-body><mj-include path=\"missing.mjml\" /></mj-body></mjml>";

        var result = Importer.Import(mjml); // no resolver

        result.Document.Should().NotBeNull();
        result.Warnings.Should().Contain(w => w.Key == ImportKeys.IncludeUnresolved);
    }

    [Fact]
    public void RoundTrip_GeneratorOutput_IsIdempotent()
    {
        // EI.14: import(export(doc)) re-exported must equal the original export (lossless cycle).
        var doc = MjmlGoldenTests_BuildDoc();
        var firstExport = Generator.Generate(doc, MjmlGeneratorOptions.ForExport);

        var reimported = Importer.Import(firstExport);
        reimported.Errors.Should().BeEmpty();
        var secondExport = Generator.Generate(reimported.Document!, MjmlGeneratorOptions.ForExport);

        Normalize(secondExport).Should().Be(Normalize(firstExport));
    }

    private static EmailTemplateDocument MjmlGoldenTests_BuildDoc()
    {
        // Mirror of the golden document but with all containers nested in a column (how the generator
        // emits them), so the cycle is structurally consistent.
        var doc = new EmailTemplateDocument { Subject = "S", Preheader = "P", Language = "en" };
        doc.Styles.Breakpoint = "500px";
        doc.Styles.Fonts.Add(new EmailFont { Name = "Roboto", Href = "https://f/r.css" });
        doc.Styles.Styles.Add(new EmailStyle { Css = ".a{color:red}" });
        doc.Styles.Attributes.PerTag["mj-text"] = new() { ["color"] = "#222" };
        doc.Styles.Attributes.Classes["cta"] = new() { ["background-color"] = "#f00" };
        var sel = new MjHtmlSelector { Path = ".a" };
        sel.Attributes["data-id"] = "7";
        doc.Styles.HtmlAttributes.Add(sel);

        var section = new EmailSection { BackgroundColor = "#fff" };
        var col = new EmailColumn { Width = "100%" };
        col.Blocks.Add(new EmailTextBlock { Content = "<b>hi</b>", VisibleWhen = "is_member" });
        col.Blocks.Add(new EmailButtonBlock { Text = "Buy", Href = "https://a", CssClass = "cta" });
        col.Blocks.Add(new EmailImageBlock { Src = "https://a/b.png", Alt = "L" });
        col.Blocks.Add(new EmailDividerBlock());
        col.Blocks.Add(new EmailSpacerBlock());
        var hero = new EmailHeroBlock { BackgroundUrl = "https://a/h.png" };
        hero.Blocks.Add(new EmailTextBlock { Content = "Hero" });
        col.Blocks.Add(hero);
        section.Columns.Add(col);
        doc.Sections.Add(section);
        return doc;
    }

    private static string Normalize(string mjml)
        => string.Join('\n', mjml.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));

    private sealed class FakeResolver(string content) : IMjmlIncludeResolver
    {
        public string? Resolve(string path) => content;
    }
}
