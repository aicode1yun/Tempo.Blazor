using Tempo.Blazor.EmailTemplates.Abstractions.Model;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Model;

public class SectionEditingTests
{
    [Fact]
    public void AddRemoveMoveDuplicateSection()
    {
        var doc = new EmailTemplateDocument();
        var s1 = new EmailSection();
        var s2 = new EmailSection();
        doc.AddSection(s1, 0);
        doc.AddSection(s2, 1);
        doc.Sections.Should().ContainInOrder(s1, s2);

        doc.MoveSection(s2.Id, 0).Should().BeTrue();
        doc.Sections.Should().ContainInOrder(s2, s1);

        var dup = doc.DuplicateSection(s1.Id);
        dup.Should().NotBeNull();
        dup!.Id.Should().NotBe(s1.Id);
        doc.Sections.Should().HaveCount(3);

        doc.RemoveSection(s1.Id).Should().BeTrue();
        doc.Sections.Should().NotContain(s1);
    }

    [Theory]
    [InlineData(1, new[] { "100%" })]
    [InlineData(2, new[] { "50%", "50%" })]
    [InlineData(3, new[] { "33.33%", "33.33%", "33.34%" })]
    [InlineData(4, new[] { "25%", "25%", "25%", "25%" })]
    public void AddColumn_RebalancesEqualWidths(int count, string[] expected)
    {
        var section = new EmailSection();
        for (int i = 0; i < count; i++)
            section.AddColumn(new EmailColumn());

        section.Columns.Select(c => c.Width).Should().ContainInOrder(expected);
    }

    [Fact]
    public void RemoveColumn_RebalancesRemaining()
    {
        var section = new EmailSection();
        section.AddColumn(new EmailColumn());
        section.AddColumn(new EmailColumn());
        var third = new EmailColumn();
        section.AddColumn(third);
        // now 3 columns at 33.33/33.33/33.34

        section.RemoveColumn(third.Id).Should().BeTrue();
        section.Columns.Select(c => c.Width).Should().ContainInOrder("50%", "50%");
    }

    [Fact]
    public void EqualWidths_AlwaysSumTo100()
    {
        foreach (var n in new[] { 1, 2, 3, 4, 5, 6, 7 })
        {
            var widths = LayoutMath.EqualWidths(n);
            var sum = widths.Sum(w => decimal.Parse(w.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture));
            sum.Should().Be(100m, $"the {n}-column split must total 100%");
        }
    }
}
