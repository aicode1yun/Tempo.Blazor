using System.Globalization;
using Tempo.Blazor.EmailTemplates.Abstractions.Layout;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Layout;

public class LayoutPresetsTests
{
    [Fact]
    public void All_ExposesPresetsWithLocalizationKeys()
    {
        LayoutPresets.All.Should().NotBeEmpty();
        LayoutPresets.All.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.NameKey));
        LayoutPresets.All.Select(p => p.Preset).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(LayoutPreset.Single, 1)]
    [InlineData(LayoutPreset.TwoEqual, 2)]
    [InlineData(LayoutPreset.ThreeEqual, 3)]
    [InlineData(LayoutPreset.FourEqual, 4)]
    [InlineData(LayoutPreset.TwoThirdsOneThird, 2)]
    [InlineData(LayoutPreset.OneThirdTwoThirds, 2)]
    public void Create_ProducesSectionWithWidthsSummingTo100(LayoutPreset preset, int expectedColumns)
    {
        var section = LayoutPresets.Create(preset);

        section.Columns.Should().HaveCount(expectedColumns);
        section.Columns.Should().OnlyContain(c => c.Width != null);

        var sum = section.Columns.Sum(c =>
            decimal.Parse(c.Width!.TrimEnd('%'), CultureInfo.InvariantCulture));
        sum.Should().Be(100m);
    }
}
