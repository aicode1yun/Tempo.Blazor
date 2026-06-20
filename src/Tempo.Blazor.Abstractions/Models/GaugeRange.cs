namespace Tempo.Blazor.Models;

/// <summary>Type of gauge visualization.</summary>
public enum GaugeType
{
    /// <summary>Arc gauge (partial circle).</summary>
    Arc,
    /// <summary>Full circular gauge.</summary>
    Circular,
    /// <summary>Horizontal linear bar gauge.</summary>
    Linear
}

/// <summary>A colored range band for gauges.</summary>
public sealed record GaugeRange
{
    /// <summary>Start value of the range.</summary>
    public double From { get; init; }

    /// <summary>End value of the range.</summary>
    public double To { get; init; }

    /// <summary>Color for this range band.</summary>
    public string Color { get; init; } = string.Empty;

    public GaugeRange() { }

    public GaugeRange(double from, double to, string color)
    {
        From = from;
        To = to;
        Color = color;
    }
}
