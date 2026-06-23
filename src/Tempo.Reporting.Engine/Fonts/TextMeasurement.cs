namespace Tempo.Reporting.Engine.Fonts;

/// <summary>Measured text run geometry.</summary>
public sealed record TextMeasurement(
    double Width,
    double Ascent,
    double Descent,
    double LineGap,
    double LineHeight,
    int GlyphCount,
    int FallbackGlyphCount,
    int MissingGlyphCount);
