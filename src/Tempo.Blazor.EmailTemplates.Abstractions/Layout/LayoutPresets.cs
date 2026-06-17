using Tempo.Blazor.EmailTemplates.Abstractions.Model;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Layout;

/// <summary>Builds sections from predefined column layouts whose widths always total 100%.</summary>
public static class LayoutPresets
{
    /// <summary>Gets the catalogue of available presets.</summary>
    public static IReadOnlyList<LayoutPresetDescriptor> All { get; } = new[]
    {
        new LayoutPresetDescriptor(LayoutPreset.Single, "layout.preset.single", LayoutMath.EqualWidths(1)),
        new LayoutPresetDescriptor(LayoutPreset.TwoEqual, "layout.preset.two_equal", LayoutMath.EqualWidths(2)),
        new LayoutPresetDescriptor(LayoutPreset.ThreeEqual, "layout.preset.three_equal", LayoutMath.EqualWidths(3)),
        new LayoutPresetDescriptor(LayoutPreset.FourEqual, "layout.preset.four_equal", LayoutMath.EqualWidths(4)),
        new LayoutPresetDescriptor(LayoutPreset.TwoThirdsOneThird, "layout.preset.two_thirds_one_third", new[] { "66.67%", "33.33%" }),
        new LayoutPresetDescriptor(LayoutPreset.OneThirdTwoThirds, "layout.preset.one_third_two_thirds", new[] { "33.33%", "66.67%" }),
    };

    /// <summary>Creates a new section populated with the columns of the given preset.</summary>
    public static EmailSection Create(LayoutPreset preset)
    {
        var descriptor = All.First(p => p.Preset == preset);
        var section = new EmailSection();
        foreach (var width in descriptor.Widths)
            section.Columns.Add(new EmailColumn { Width = width });
        return section;
    }
}
