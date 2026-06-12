using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests;

/// <summary>Shared fixtures used across model, serialization and operation tests.</summary>
internal static class SampleDocuments
{
    /// <summary>Builds a document exercising all 14 block types, nesting and the head model.</summary>
    public static EmailTemplateDocument FullyPopulated()
    {
        var doc = new EmailTemplateDocument
        {
            Name = "Sample",
            Subject = "Hello {{ name }}",
            Preheader = "Preview text",
            Language = "en",
        };
        doc.Styles.Fonts.Add(new EmailFont { Name = "Roboto", Href = "https://fonts/r.css" });
        doc.Styles.Styles.Add(new EmailStyle { Css = ".x{color:red}", Inline = true });

        var col = new EmailColumn { Width = "100%" };
        col.Blocks.Add(new EmailTextBlock { Content = "<b>hi</b>", VisibleWhen = "is_premium" });
        col.Blocks.Add(new EmailButtonBlock { Text = "Go", Href = "https://a" });
        col.Blocks.Add(new EmailImageBlock { Src = "https://a/b.png", Alt = "logo" });
        col.Blocks.Add(new EmailDividerBlock());
        col.Blocks.Add(new EmailSpacerBlock { Height = "30px" });
        col.Blocks.Add(new EmailRawBlock { Content = "<!-- raw -->" });

        var table = new EmailTableBlock();
        var row = new EmailTableRow { IsHeader = true };
        row.Cells.Add(new EmailTableCell { Text = "A", ColSpan = 2 });
        table.Rows.Add(row);
        col.Blocks.Add(table);

        var social = new EmailSocialBlock();
        social.Elements.Add(new EmailSocialElement { Name = "facebook", Href = "https://fb", Label = "FB" });
        col.Blocks.Add(social);

        var navbar = new EmailNavbarBlock();
        navbar.Links.Add(new EmailNavbarLink { Text = "Home", Href = "#home" });
        col.Blocks.Add(navbar);

        var carousel = new EmailCarouselBlock();
        carousel.Images.Add(new EmailCarouselImage { Src = "https://a/1.png", Alt = "1" });
        col.Blocks.Add(carousel);

        var accordion = new EmailAccordionBlock();
        accordion.Items.Add(new EmailAccordionItem { Title = "T", Content = "C" });
        col.Blocks.Add(accordion);

        var hero = new EmailHeroBlock { BackgroundUrl = "https://a/hero.png" };
        hero.Blocks.Add(new EmailTextBlock { Content = "hero text" });
        col.Blocks.Add(hero);

        var group = new EmailGroupBlock();
        var groupCol = new EmailColumn { Width = "50%" };
        groupCol.Blocks.Add(new EmailTextBlock { Content = "g" });
        group.Columns.Add(groupCol);
        col.Blocks.Add(group);

        var wrapper = new EmailWrapperBlock { BackgroundColor = "#eee" };
        var wrapSection = new EmailSection();
        var wrapCol = new EmailColumn();
        wrapCol.Blocks.Add(new EmailTextBlock { Content = "wrapped" });
        wrapSection.Columns.Add(wrapCol);
        wrapper.Sections.Add(wrapSection);
        col.Blocks.Add(wrapper);

        var section = new EmailSection { BackgroundColor = "#fff" };
        section.Columns.Add(col);
        doc.Sections.Add(section);
        return doc;
    }

    /// <summary>Counts every block in the document, recursing into hero/group/wrapper containers.</summary>
    public static int CountAllBlocks(EmailTemplateDocument doc)
    {
        int n = 0;
        void Walk(IEnumerable<EmailBlockBase> blocks)
        {
            foreach (var b in blocks)
            {
                n++;
                switch (b)
                {
                    case EmailHeroBlock h: Walk(h.Blocks); break;
                    case EmailGroupBlock g: foreach (var c in g.Columns) Walk(c.Blocks); break;
                    case EmailWrapperBlock w:
                        foreach (var s in w.Sections)
                            foreach (var c in s.Columns) Walk(c.Blocks);
                        break;
                }
            }
        }
        foreach (var s in doc.Sections)
            foreach (var c in s.Columns) Walk(c.Blocks);
        return n;
    }
}
