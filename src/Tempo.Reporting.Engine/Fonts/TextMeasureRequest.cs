namespace Tempo.Reporting.Engine.Fonts;

/// <summary>Text run measurement request.</summary>
public sealed record TextMeasureRequest(
    string Text,
    string FontFamily,
    double FontSize,
    bool Bold = false,
    bool Italic = false,
    double LetterSpacing = 0,
    bool Kerning = true);
