namespace Tempo.Reporting.Engine.Fonts;

/// <summary>Unicode code-point pair for kerning adjustment lookup.</summary>
public readonly record struct FontKerningPair(int LeftCodePoint, int RightCodePoint);
