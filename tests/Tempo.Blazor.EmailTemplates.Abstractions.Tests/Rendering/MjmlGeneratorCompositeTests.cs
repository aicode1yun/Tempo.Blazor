using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Rendering;

public class MjmlGeneratorCompositeTests
{
    private static readonly MjmlGenerator Generator = new();

    private static string Render(EmailBlockBase block)
    {
        var doc = new EmailTemplateDocument();
        var section = new EmailSection();
        var col = new EmailColumn();
        col.Blocks.Add(block);
        section.Columns.Add(col);
        doc.Sections.Add(section);
        return Generator.Generate(doc);
    }

    [Fact]
    public void Table_EscapesCellContent_AndUsesHeaderCells()
    {
        var t = new EmailTableBlock();
        var header = new EmailTableRow { IsHeader = true };
        header.Cells.Add(new EmailTableCell { Text = "A & <b>" });
        var row = new EmailTableRow();
        row.Cells.Add(new EmailTableCell { Text = "v", ColSpan = 2 });
        t.Rows.Add(header);
        t.Rows.Add(row);

        var mjml = Render(t);

        mjml.Should().Contain("<th>A &amp; &lt;b&gt;</th>");
        mjml.Should().Contain("<td colspan=\"2\">v</td>");
        mjml.Should().NotContain("<b>");
    }

    [Fact]
    public void Social_EmitsElements()
    {
        var s = new EmailSocialBlock { Mode = "vertical" };
        s.Elements.Add(new EmailSocialElement { Name = "facebook", Href = "https://fb", Label = "FB" });

        var mjml = Render(s);

        mjml.Should().Contain("<mj-social").And.Contain("mode=\"vertical\"");
        mjml.Should().Contain("<mj-social-element").And.Contain("name=\"facebook\"").And.Contain(">FB</mj-social-element>");
    }

    [Fact]
    public void Navbar_EmitsLinks()
    {
        var n = new EmailNavbarBlock();
        n.Links.Add(new EmailNavbarLink { Text = "Home", Href = "#h" });

        var mjml = Render(n);

        mjml.Should().Contain("<mj-navbar").And.Contain("<mj-navbar-link").And.Contain(">Home</mj-navbar-link>");
    }

    [Fact]
    public void Carousel_EmitsImages()
    {
        var c = new EmailCarouselBlock { Thumbnails = "hidden" };
        c.Images.Add(new EmailCarouselImage { Src = "https://a/1.png", Alt = "one" });

        var mjml = Render(c);

        mjml.Should().Contain("<mj-carousel").And.Contain("thumbnails=\"hidden\"");
        mjml.Should().Contain("<mj-carousel-image").And.Contain("src=\"https://a/1.png\"").And.Contain("alt=\"one\"");
    }

    [Fact]
    public void Accordion_EmitsTitleAndSanitizedText()
    {
        var ac = new EmailAccordionBlock();
        ac.Items.Add(new EmailAccordionItem { Title = "Q", Content = "<b>A</b><script>x</script>" });

        var mjml = Render(ac);

        mjml.Should().Contain("<mj-accordion-title>Q</mj-accordion-title>");
        mjml.Should().Contain("<mj-accordion-text><b>A</b></mj-accordion-text>");
        mjml.Should().NotContain("<script");
    }

    [Fact]
    public void Hero_RecursesIntoNestedBlocks()
    {
        var hero = new EmailHeroBlock { BackgroundUrl = "https://a/h.png" };
        hero.Blocks.Add(new EmailTextBlock { Content = "inside hero" });

        var mjml = Render(hero);

        mjml.Should().Contain("<mj-hero").And.Contain("background-url=\"https://a/h.png\"");
        mjml.Should().Contain("inside hero").And.Contain("</mj-hero>");
    }

    [Fact]
    public void Wrapper_RecursesIntoSections_AndGroupIntoColumns()
    {
        var wrapper = new EmailWrapperBlock { FullWidth = true };
        var section = new EmailSection();
        var col = new EmailColumn();
        var group = new EmailGroupBlock();
        var gcol = new EmailColumn { Width = "50%" };
        gcol.Blocks.Add(new EmailTextBlock { Content = "g" });
        group.Columns.Add(gcol);
        col.Blocks.Add(group);
        section.Columns.Add(col);
        wrapper.Sections.Add(section);

        var mjml = Render(wrapper);

        mjml.Should().Contain("<mj-wrapper").And.Contain("full-width=\"full-width\"");
        mjml.Should().Contain("<mj-group").And.Contain("</mj-group>");
        mjml.Should().Contain("width=\"50%\"");
    }

    [Fact]
    public void VisibleWhen_WrapsBlockInScribanCondition()
    {
        var mjml = Render(new EmailTextBlock { Content = "secret", VisibleWhen = "is_premium" });

        mjml.Should().Contain("{{ if is_premium }}");
        mjml.Should().Contain("{{ end }}");
        var ifIndex = mjml.IndexOf("{{ if is_premium }}", StringComparison.Ordinal);
        var textIndex = mjml.IndexOf("<mj-text", StringComparison.Ordinal);
        var endIndex = mjml.IndexOf("{{ end }}", StringComparison.Ordinal);
        ifIndex.Should().BeLessThan(textIndex);
        textIndex.Should().BeLessThan(endIndex);
    }

    [Fact]
    public void CssClassMjClassAndExtraAttributes_AreEmitted()
    {
        var text = new EmailTextBlock { Content = "x", CssClass = "promo" };
        text.MjClasses.Add("big");
        text.ExtraAttributes["data-id"] = "99";

        var mjml = Render(text);

        mjml.Should().Contain("css-class=\"promo\"");
        mjml.Should().Contain("mj-class=\"big\"");
        mjml.Should().Contain("data-id=\"99\"");
    }
}
