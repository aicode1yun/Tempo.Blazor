namespace Tempo.Reporting.Engine.Fonts;

/// <summary>Measures report text runs from precomputed font metric tables.</summary>
public interface ITextMeasurer
{
    /// <summary>Measures a single unwrapped text run.</summary>
    TextMeasurement MeasureRun(TextMeasureRequest request);
}
