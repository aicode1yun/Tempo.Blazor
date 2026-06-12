using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Model;

public class DocumentEditingTests
{
    private static (EmailTemplateDocument doc, EmailColumn col) OneColumn(params EmailBlockBase[] blocks)
    {
        var doc = new EmailTemplateDocument();
        var section = new EmailSection();
        var col = new EmailColumn();
        col.Blocks.AddRange(blocks);
        section.Columns.Add(col);
        doc.Sections.Add(section);
        return (doc, col);
    }

    [Fact]
    public void FindBlock_FindsTopLevelAndNested()
    {
        var nested = new EmailTextBlock { Content = "deep" };
        var hero = new EmailHeroBlock();
        hero.Blocks.Add(nested);
        var (doc, _) = OneColumn(hero);

        doc.FindBlock(hero.Id).Should().BeSameAs(hero);
        doc.FindBlock(nested.Id).Should().BeSameAs(nested);
        doc.FindBlock(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void FindParentColumn_ReturnsColumnForDirectChild_NullForHeroNested()
    {
        var direct = new EmailTextBlock();
        var nested = new EmailTextBlock();
        var hero = new EmailHeroBlock();
        hero.Blocks.Add(nested);
        var (doc, col) = OneColumn(direct, hero);

        doc.FindParentColumn(direct.Id).Should().BeSameAs(col);
        doc.FindParentColumn(nested.Id).Should().BeNull();
    }

    [Fact]
    public void RemoveBlock_RemovesFromColumnOrNestedContainer()
    {
        var nested = new EmailTextBlock();
        var hero = new EmailHeroBlock();
        hero.Blocks.Add(nested);
        var (doc, col) = OneColumn(hero);

        doc.RemoveBlock(nested.Id).Should().BeTrue();
        hero.Blocks.Should().BeEmpty();
        doc.RemoveBlock(hero.Id).Should().BeTrue();
        col.Blocks.Should().BeEmpty();
        doc.RemoveBlock(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void AddBlock_InsertsAtIndex_AndValidatesColumn()
    {
        var a = new EmailTextBlock { Content = "a" };
        var b = new EmailTextBlock { Content = "b" };
        var (doc, col) = OneColumn(a, b);
        var c = new EmailTextBlock { Content = "c" };

        doc.AddBlock(col.Id, c, 1).Should().BeTrue();
        col.Blocks.Select(x => ((EmailTextBlock)x).Content).Should().ContainInOrder("a", "c", "b");

        doc.AddBlock(Guid.NewGuid(), new EmailTextBlock(), 0).Should().BeFalse();
    }

    [Fact]
    public void AddBlock_ClampsOutOfRangeIndex()
    {
        var (doc, col) = OneColumn(new EmailTextBlock { Content = "a" });
        doc.AddBlock(col.Id, new EmailTextBlock { Content = "z" }, 99).Should().BeTrue();
        ((EmailTextBlock)col.Blocks[^1]).Content.Should().Be("z");
    }

    [Fact]
    public void MoveBlock_BetweenColumns()
    {
        var doc = new EmailTemplateDocument();
        var section = new EmailSection();
        var c1 = new EmailColumn();
        var c2 = new EmailColumn();
        var block = new EmailTextBlock { Content = "x" };
        c1.Blocks.Add(block);
        section.Columns.Add(c1);
        section.Columns.Add(c2);
        doc.Sections.Add(section);

        doc.MoveBlock(block.Id, c2.Id, 0).Should().BeTrue();
        c1.Blocks.Should().BeEmpty();
        c2.Blocks.Should().ContainSingle().Which.Should().BeSameAs(block);
    }

    [Fact]
    public void MoveBlock_WithinSameColumn_Reorders()
    {
        var a = new EmailTextBlock { Content = "a" };
        var b = new EmailTextBlock { Content = "b" };
        var cc = new EmailTextBlock { Content = "c" };
        var (doc, col) = OneColumn(a, b, cc);

        // move "a" to the end
        doc.MoveBlock(a.Id, col.Id, 2).Should().BeTrue();
        col.Blocks.Select(x => ((EmailTextBlock)x).Content).Should().ContainInOrder("b", "c", "a");
    }

    [Fact]
    public void DuplicateBlock_InsertsCopyWithNewIdRightAfterOriginal()
    {
        var a = new EmailTextBlock { Content = "a" };
        var b = new EmailTextBlock { Content = "b" };
        var (doc, col) = OneColumn(a, b);

        var dup = doc.DuplicateBlock(a.Id);

        dup.Should().NotBeNull();
        dup!.Id.Should().NotBe(a.Id);
        col.Blocks.Should().HaveCount(3);
        col.Blocks[1].Should().BeSameAs(dup);
        ((EmailTextBlock)col.Blocks[1]).Content.Should().Be("a");
    }
}
