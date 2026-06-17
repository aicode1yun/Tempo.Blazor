namespace Tempo.Blazor.EmailTemplates.Abstractions.Layout;

/// <summary>The available column-layout presets offered when adding a new section.</summary>
public enum LayoutPreset
{
    /// <summary>A single full-width column.</summary>
    Single,

    /// <summary>Two equal columns (50% / 50%).</summary>
    TwoEqual,

    /// <summary>Three equal columns.</summary>
    ThreeEqual,

    /// <summary>Four equal columns.</summary>
    FourEqual,

    /// <summary>A wide column then a narrow one (2/3 + 1/3).</summary>
    TwoThirdsOneThird,

    /// <summary>A narrow column then a wide one (1/3 + 2/3).</summary>
    OneThirdTwoThirds,
}
