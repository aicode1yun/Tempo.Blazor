using Tempo.Blazor.EmailTemplates.Abstractions.Import;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Import;

public class MjmlImporterTests
{
    private static readonly MjmlImporter Importer = new();

    [Fact]
    public void Import_HeadTitleAndPreview()
    {
        const string mjml = "<mjml lang=\"en\"><mj-head><mj-title>Subj</mj-title><mj-preview>Peek</mj-preview></mj-head>" +
                            "<mj-body><mj-section><mj-column><mj-text>hi</mj-text></mj-column></mj-section></mj-body></mjml>";

        var result = Importer.Import(mjml);

        result.Errors.Should().BeEmpty();
        result.Document.Should().NotBeNull();
        result.Document!.Language.Should().Be("en");
        result.Document.Subject.Should().Be("Subj");
        result.Document.Preheader.Should().Be("Peek");
    }

    [Fact]
    public void Import_SectionColumnText()
    {
        const string mjml = "<mjml><mj-body><mj-section background-color=\"#fafafa\"><mj-column width=\"50%\">" +
                            "<mj-text color=\"#222\">Hello</mj-text></mj-column></mj-section></mj-body></mjml>";

        var doc = Importer.Import(mjml).Document!;

        var section = doc.Sections.Should().ContainSingle().Subject;
        section.BackgroundColor.Should().Be("#fafafa");
        var column = section.Columns.Should().ContainSingle().Subject;
        column.Width.Should().Be("50%");
        var text = column.Blocks.Should().ContainSingle().Subject.Should().BeOfType<EmailTextBlock>().Subject;
        text.Content.Should().Contain("Hello");
        text.Color.Should().Be("#222");
    }

    [Fact]
    public void Import_BodyWidthAndBackground()
    {
        const string mjml = "<mjml><mj-body width=\"640px\" background-color=\"#eee\"><mj-section><mj-column>" +
                            "<mj-text>x</mj-text></mj-column></mj-section></mj-body></mjml>";

        var doc = Importer.Import(mjml).Document!;

        doc.Styles.ContentWidth.Should().Be("640px");
        doc.Styles.BackgroundColor.Should().Be("#eee");
    }

    [Fact]
    public void Import_MjRaw_PreservesVerbatimContent_EvenIfNotWellFormed()
    {
        const string mjml = "<mjml><mj-body><mj-section><mj-column>" +
                            "<mj-raw><!--[if mso]><br><img src=x></mj-raw></mj-column></mj-section></mj-body></mjml>";

        var doc = Importer.Import(mjml).Document!;

        var raw = doc.Sections[0].Columns[0].Blocks[0].Should().BeOfType<EmailRawBlock>().Subject;
        raw.Content.Should().Contain("<!--[if mso]>");
        raw.Content.Should().Contain("<br>");
    }

    [Fact]
    public void Import_ToleratesHtmlComments()
    {
        const string mjml = "<mjml><mj-body><!-- a comment --><mj-section><mj-column>" +
                            "<mj-text>x</mj-text></mj-column></mj-section></mj-body></mjml>";

        var result = Importer.Import(mjml);

        result.Errors.Should().BeEmpty();
        result.Document!.Sections.Should().ContainSingle();
    }

    [Fact]
    public void Import_EmptyString_IsError_NotCrash()
    {
        var result = Importer.Import("");
        result.Document.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Import_MalformedXml_IsError_NotCrash()
    {
        var act = () => Importer.Import("<mjml><mj-body><mj-section>");
        act.Should().NotThrow();
        Importer.Import("<mjml><mj-body><mj-section>").Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Import_NonMjmlRoot_IsError()
    {
        var result = Importer.Import("<html><body>hi</body></html>");
        result.Errors.Should().NotBeEmpty();
        result.Document.Should().BeNull();
    }
}
