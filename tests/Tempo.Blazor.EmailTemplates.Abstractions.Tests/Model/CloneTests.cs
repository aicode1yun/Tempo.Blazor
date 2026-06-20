using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Model;

public class CloneTests
{
    [Fact]
    public void DeepClone_IsIndependent_AndKeepsSameIds()
    {
        var doc = SampleDocuments.FullyPopulated();
        var originalBlockId = doc.Sections[0].Columns[0].Blocks[0].Id;

        var clone = doc.DeepClone();

        clone.Should().NotBeSameAs(doc);
        clone.Id.Should().Be(doc.Id);
        clone.Sections[0].Columns[0].Blocks[0].Id.Should().Be(originalBlockId);

        // mutating the clone must not touch the original
        ((EmailTextBlock)clone.Sections[0].Columns[0].Blocks[0]).Content = "changed";
        ((EmailTextBlock)doc.Sections[0].Columns[0].Blocks[0]).Content.Should().Be("<b>hi</b>");
    }

    [Fact]
    public void CloneWithNewIds_GivesEveryNodeAFreshId()
    {
        var hero = new EmailHeroBlock();
        var inner = new EmailTextBlock { Content = "x" };
        hero.Blocks.Add(inner);

        var clone = (EmailHeroBlock)hero.CloneWithNewIds();

        clone.Id.Should().NotBe(hero.Id);
        clone.Blocks[0].Id.Should().NotBe(inner.Id);
        ((EmailTextBlock)clone.Blocks[0]).Content.Should().Be("x");
    }

    [Fact]
    public void CloneWithNewIds_ReassignsNestedColumnAndSectionIds()
    {
        var wrapper = new EmailWrapperBlock();
        var section = new EmailSection();
        var column = new EmailColumn();
        column.Blocks.Add(new EmailTextBlock());
        section.Columns.Add(column);
        wrapper.Sections.Add(section);

        var clone = (EmailWrapperBlock)wrapper.CloneWithNewIds();

        clone.Sections[0].Id.Should().NotBe(section.Id);
        clone.Sections[0].Columns[0].Id.Should().NotBe(column.Id);
        clone.Sections[0].Columns[0].Blocks[0].Id.Should().NotBe(column.Blocks[0].Id);
    }
}
