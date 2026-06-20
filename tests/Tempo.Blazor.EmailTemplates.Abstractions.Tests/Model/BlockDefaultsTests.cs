using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Model;

public class BlockDefaultsTests
{
    [Fact]
    public void Base_HasIdentityAndCrossCuttingDefaults()
    {
        var b = new EmailTextBlock();
        b.Id.Should().NotBe(Guid.Empty);
        b.CssClass.Should().BeNull();
        b.MjClasses.Should().BeEmpty();
        b.ExtraAttributes.Should().BeEmpty();
        b.VisibleWhen.Should().BeNull();
    }

    [Fact]
    public void EachBlock_ReportsItsDiscriminatorType()
    {
        new EmailTextBlock().Type.Should().Be(BlockType.Text);
        new EmailButtonBlock().Type.Should().Be(BlockType.Button);
        new EmailImageBlock().Type.Should().Be(BlockType.Image);
        new EmailDividerBlock().Type.Should().Be(BlockType.Divider);
        new EmailSpacerBlock().Type.Should().Be(BlockType.Spacer);
        new EmailRawBlock().Type.Should().Be(BlockType.Raw);
        new EmailTableBlock().Type.Should().Be(BlockType.Table);
        new EmailSocialBlock().Type.Should().Be(BlockType.Social);
        new EmailHeroBlock().Type.Should().Be(BlockType.Hero);
        new EmailNavbarBlock().Type.Should().Be(BlockType.Navbar);
        new EmailCarouselBlock().Type.Should().Be(BlockType.Carousel);
        new EmailAccordionBlock().Type.Should().Be(BlockType.Accordion);
        new EmailWrapperBlock().Type.Should().Be(BlockType.Wrapper);
        new EmailGroupBlock().Type.Should().Be(BlockType.Group);
    }

    [Fact]
    public void Text_MjmlDefaults()
    {
        var t = new EmailTextBlock();
        t.Color.Should().Be("#000000");
        t.FontFamily.Should().Be("Ubuntu, Helvetica, Arial, sans-serif");
        t.FontSize.Should().Be("13px");
        t.LineHeight.Should().Be("1");
        t.Align.Should().Be("left");
        t.Padding.Should().Be("10px 25px");
        t.Content.Should().Be(string.Empty);
    }

    [Fact]
    public void Button_MjmlDefaults()
    {
        var b = new EmailButtonBlock();
        b.BackgroundColor.Should().Be("#414141");
        b.Color.Should().Be("#ffffff");
        b.FontSize.Should().Be("13px");
        b.BorderRadius.Should().Be("3px");
        b.InnerPadding.Should().Be("10px 25px");
        b.Padding.Should().Be("10px 25px");
        b.Align.Should().Be("center");
        b.Target.Should().Be("_blank");
    }

    [Fact]
    public void Image_MjmlDefaults_AndRequiredAlt()
    {
        var i = new EmailImageBlock();
        i.Src.Should().Be(string.Empty);
        i.Alt.Should().Be(string.Empty);
        i.Align.Should().Be("center");
        i.Border.Should().Be("0");
        i.Padding.Should().Be("10px 25px");
        i.Target.Should().Be("_blank");
    }

    [Fact]
    public void Divider_MjmlDefaults()
    {
        var d = new EmailDividerBlock();
        d.BorderColor.Should().Be("#000000");
        d.BorderStyle.Should().Be("solid");
        d.BorderWidth.Should().Be("4px");
        d.Width.Should().Be("100%");
        d.Align.Should().Be("center");
        d.Padding.Should().Be("10px 25px");
    }

    [Fact]
    public void Spacer_DefaultsHeight20()
    {
        new EmailSpacerBlock().Height.Should().Be("20px");
    }

    [Fact]
    public void Raw_HoldsVerbatimContent()
    {
        var r = new EmailRawBlock { Content = "<custom>hi</custom>" };
        r.Content.Should().Be("<custom>hi</custom>");
    }

    [Fact]
    public void Table_MjmlDefaults_AndRowModel()
    {
        var t = new EmailTableBlock();
        t.Width.Should().Be("100%");
        t.CellPadding.Should().Be("0");
        t.CellSpacing.Should().Be("0");
        t.Color.Should().Be("#000000");
        t.FontSize.Should().Be("13px");
        t.Align.Should().Be("left");
        t.Rows.Should().BeEmpty();

        var row = new EmailTableRow();
        row.Cells.Add(new EmailTableCell { Text = "A" });
        t.Rows.Add(row);
        t.Rows[0].Cells[0].Text.Should().Be("A");
    }

    [Fact]
    public void Social_MjmlDefaults_AndElements()
    {
        var s = new EmailSocialBlock();
        s.Mode.Should().Be("horizontal");
        s.IconSize.Should().Be("20px");
        s.Align.Should().Be("center");
        s.Elements.Should().BeEmpty();

        s.Elements.Add(new EmailSocialElement { Name = "facebook", Href = "https://fb.com", Label = "FB" });
        s.Elements[0].Name.Should().Be("facebook");
        s.Elements[0].Target.Should().Be("_blank");
    }

    [Fact]
    public void Hero_MjmlDefaults_AndNestedBlocks()
    {
        var h = new EmailHeroBlock();
        h.Mode.Should().Be("fluid-height");
        h.BackgroundColor.Should().Be("#ffffff");
        h.VerticalAlign.Should().Be("top");
        h.Blocks.Should().BeEmpty();

        h.Blocks.Add(new EmailTextBlock { Content = "hero" });
        h.Blocks.Should().ContainSingle();
    }

    [Fact]
    public void Navbar_Links()
    {
        var n = new EmailNavbarBlock();
        n.Links.Should().BeEmpty();
        n.Links.Add(new EmailNavbarLink { Text = "Home", Href = "#" });
        var link = n.Links[0];
        link.Color.Should().Be("#000000");
        link.TextDecoration.Should().Be("none");
        link.TextTransform.Should().Be("uppercase");
        link.Target.Should().Be("_blank");
    }

    [Fact]
    public void Carousel_MjmlDefaults_AndImages()
    {
        var c = new EmailCarouselBlock();
        c.Thumbnails.Should().Be("visible");
        c.Align.Should().Be("center");
        c.Images.Should().BeEmpty();
        c.Images.Add(new EmailCarouselImage { Src = "http://a/b.png", Alt = "x" });
        c.Images[0].Target.Should().Be("_blank");
    }

    [Fact]
    public void Accordion_MjmlDefaults_AndItems()
    {
        var a = new EmailAccordionBlock();
        a.IconPosition.Should().Be("right");
        a.Items.Should().BeEmpty();
        a.Items.Add(new EmailAccordionItem { Title = "T", Content = "C" });
        a.Items[0].Title.Should().Be("T");
        a.Items[0].Content.Should().Be("C");
    }

    [Fact]
    public void Wrapper_HoldsSections()
    {
        var w = new EmailWrapperBlock();
        w.TextAlign.Should().Be("center");
        w.Sections.Should().BeEmpty();
        w.Sections.Add(new EmailSection());
        w.Sections.Should().ContainSingle();
    }

    [Fact]
    public void Group_HoldsColumns()
    {
        var g = new EmailGroupBlock();
        g.Direction.Should().Be("ltr");
        g.Columns.Should().BeEmpty();
        g.Columns.Add(new EmailColumn());
        g.Columns.Should().ContainSingle();
    }
}
