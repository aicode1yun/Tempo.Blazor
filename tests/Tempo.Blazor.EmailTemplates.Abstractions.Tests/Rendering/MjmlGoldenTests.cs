using System.Runtime.CompilerServices;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Rendering;

public class MjmlGoldenTests
{
    [Fact]
    public void FullDocument_MatchesGolden()
    {
        var mjml = new MjmlGenerator().Generate(BuildGoldenDocument(), MjmlGeneratorOptions.ForExport);
        var actual = Normalize(mjml);

        var path = GoldenPath();
        if (!File.Exists(path))
        {
            // Bootstrap: record the golden on first run. Regenerate deliberately when the generator
            // changes and review the diff.
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
        }

        var expected = Normalize(File.ReadAllText(path));
        actual.Should().Be(expected,
            "the generated MJML must match the golden file; if the change is intentional, delete the golden and rerun to regenerate, then review the diff");
    }

    private static EmailTemplateDocument BuildGoldenDocument()
    {
        var doc = new EmailTemplateDocument
        {
            Name = "Golden",
            Subject = "Golden subject",
            Preheader = "Golden preview",
            Language = "en",
        };
        doc.Styles.ContentWidth = "640px";
        doc.Styles.Breakpoint = "500px";
        doc.Styles.FontFamily = "Helvetica, Arial, sans-serif";
        doc.Styles.Fonts.Add(new EmailFont { Name = "Roboto", Href = "https://fonts/roboto.css" });
        doc.Styles.Styles.Add(new EmailStyle { Css = ".brand{color:#0a0}" });
        doc.Styles.Styles.Add(new EmailStyle { Css = ".muted{color:#888}", Inline = true });
        doc.Styles.Attributes.All["font-family"] = "Helvetica, Arial, sans-serif";
        doc.Styles.Attributes.PerTag["mj-text"] = new() { ["color"] = "#222222" };
        doc.Styles.Attributes.Classes["cta"] = new() { ["background-color"] = "#0a0" };
        var selector = new MjHtmlSelector { Path = ".brand" };
        selector.Attributes["data-role"] = "brand";
        doc.Styles.HtmlAttributes.Add(selector);

        var section = new EmailSection { BackgroundColor = "#ffffff" };
        var col = new EmailColumn { Width = "100%" };
        col.Blocks.Add(new EmailTextBlock { Content = "<b>Hi</b> there", VisibleWhen = "is_member" });
        col.Blocks.Add(new EmailButtonBlock { Text = "Buy", Href = "https://shop", CssClass = "cta" });
        col.Blocks.Add(new EmailImageBlock { Src = "https://a/b.png", Alt = "Logo" });
        col.Blocks.Add(new EmailDividerBlock());
        col.Blocks.Add(new EmailSpacerBlock());
        col.Blocks.Add(new EmailRawBlock { Content = "<!-- promo -->" });

        var table = new EmailTableBlock();
        var headerRow = new EmailTableRow { IsHeader = true };
        headerRow.Cells.Add(new EmailTableCell { Text = "Item" });
        headerRow.Cells.Add(new EmailTableCell { Text = "Price" });
        table.Rows.Add(headerRow);
        col.Blocks.Add(table);

        var social = new EmailSocialBlock();
        social.Elements.Add(new EmailSocialElement { Name = "facebook", Href = "https://fb", Label = "FB" });
        col.Blocks.Add(social);

        var navbar = new EmailNavbarBlock();
        navbar.Links.Add(new EmailNavbarLink { Text = "Home", Href = "https://home" });
        col.Blocks.Add(navbar);

        var carousel = new EmailCarouselBlock();
        carousel.Images.Add(new EmailCarouselImage { Src = "https://a/1.png", Alt = "One" });
        col.Blocks.Add(carousel);

        var accordion = new EmailAccordionBlock();
        accordion.Items.Add(new EmailAccordionItem { Title = "Q", Content = "<b>A</b>" });
        col.Blocks.Add(accordion);

        var hero = new EmailHeroBlock { BackgroundUrl = "https://a/hero.png" };
        hero.Blocks.Add(new EmailTextBlock { Content = "Hero" });
        col.Blocks.Add(hero);

        var group = new EmailGroupBlock();
        var gcol = new EmailColumn { Width = "50%" };
        gcol.Blocks.Add(new EmailTextBlock { Content = "G" });
        group.Columns.Add(gcol);
        col.Blocks.Add(group);

        var wrapper = new EmailWrapperBlock { BackgroundColor = "#eeeeee" };
        var wsec = new EmailSection();
        var wcol = new EmailColumn();
        wcol.Blocks.Add(new EmailTextBlock { Content = "Wrapped" });
        wsec.Columns.Add(wcol);
        wrapper.Sections.Add(wsec);
        col.Blocks.Add(wrapper);

        section.Columns.Add(col);
        doc.Sections.Add(section);
        return doc;
    }

    private static string Normalize(string mjml)
        => string.Join('\n', mjml
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0));

    private static string GoldenPath([CallerFilePath] string? thisFile = null)
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "Golden", "full-document.mjml");
}
