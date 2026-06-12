using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Model;

public class DocumentStructureTests
{
    [Fact]
    public void NewDocument_HasIdentityEmptySectionsAndDefaultStyles()
    {
        var doc = new EmailTemplateDocument();

        doc.Id.Should().NotBe(Guid.Empty);
        doc.Sections.Should().BeEmpty();
        doc.Subject.Should().Be(string.Empty);
        doc.Preheader.Should().BeNull();
        doc.Language.Should().Be("cs");
        doc.Styles.Should().NotBeNull();
        doc.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        doc.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TemplateStyles_HasEmailSafeDefaults()
    {
        var s = new TemplateStyles();
        s.ContentWidth.Should().Be("600px");
        s.FontFamily.Should().Be("Ubuntu, Helvetica, Arial, sans-serif");
        s.BackgroundColor.Should().Be("#ffffff");
        s.Breakpoint.Should().Be("480px");
        s.Fonts.Should().BeEmpty();
        s.Styles.Should().BeEmpty();
    }

    [Fact]
    public void Section_Defaults()
    {
        var sec = new EmailSection();
        sec.Id.Should().NotBe(Guid.Empty);
        sec.Padding.Should().Be("20px 0");
        sec.TextAlign.Should().Be("center");
        sec.Direction.Should().Be("ltr");
        sec.FullWidth.Should().BeFalse();
        sec.BackgroundColor.Should().BeNull();
        sec.Columns.Should().BeEmpty();
    }

    [Fact]
    public void Column_Defaults()
    {
        var col = new EmailColumn();
        col.Id.Should().NotBe(Guid.Empty);
        col.VerticalAlign.Should().Be("top");
        col.Width.Should().BeNull(); // null = auto (MJML splits equally)
        col.Blocks.Should().BeEmpty();
    }

    [Fact]
    public void Document_ComposesSectionColumnBlock()
    {
        var doc = new EmailTemplateDocument();
        var section = new EmailSection();
        var column = new EmailColumn();
        column.Blocks.Add(new EmailTextBlock { Content = "hi" });
        section.Columns.Add(column);
        doc.Sections.Add(section);

        doc.Sections[0].Columns[0].Blocks[0].Should().BeOfType<EmailTextBlock>()
            .Which.Content.Should().Be("hi");
    }
}
