using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Rendering;

public class TextVersionGeneratorTests
{
    private static readonly TextVersionGenerator Generator = new();

    private static string Generate(params EmailBlockBase[] blocks)
    {
        var doc = new EmailTemplateDocument();
        var section = new EmailSection();
        var col = new EmailColumn();
        foreach (var block in blocks)
            col.Blocks.Add(block);
        section.Columns.Add(col);
        doc.Sections.Add(section);
        return Generator.Generate(doc);
    }

    [Fact]
    public void Text_StripsHtmlTagsAndDecodesEntities()
    {
        var text = Generate(new EmailTextBlock { Content = "<p>Hello <b>world</b> &amp; co</p>" });
        text.Should().Contain("Hello world & co");
        text.Should().NotContain("<");
    }

    [Fact]
    public void Button_RendersLabelWithUrlInParentheses()
    {
        var text = Generate(new EmailButtonBlock { Text = "Shop now", Href = "https://shop" });
        text.Should().Contain("Shop now (https://shop)");
    }

    [Fact]
    public void Image_UsesAltText()
    {
        var text = Generate(new EmailImageBlock { Src = "https://a/b.png", Alt = "Company logo" });
        text.Should().Contain("Company logo");
    }

    [Fact]
    public void Table_RendersRowsLineByLine()
    {
        var t = new EmailTableBlock();
        var r1 = new EmailTableRow();
        r1.Cells.Add(new EmailTableCell { Text = "Name" });
        r1.Cells.Add(new EmailTableCell { Text = "Price" });
        var r2 = new EmailTableRow();
        r2.Cells.Add(new EmailTableCell { Text = "Widget" });
        r2.Cells.Add(new EmailTableCell { Text = "10" });
        t.Rows.Add(r1);
        t.Rows.Add(r2);

        var text = Generate(t);

        text.Should().Contain("Name").And.Contain("Price");
        text.Should().Contain("Widget").And.Contain("10");
    }

    [Fact]
    public void WholeDocument_HasNoHtmlTags()
    {
        var text = Generator.Generate(SampleDocuments.FullyPopulated());
        text.Should().NotContain("<mj-");
        text.Should().NotContain("</");
    }
}
