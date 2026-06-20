namespace Tempo.Blazor.Models;

/// <summary>Type of stock chart rendering.</summary>
public enum StockChartType
{
    /// <summary>Candlestick bars.</summary>
    Candlestick,
    /// <summary>OHLC bars.</summary>
    OHLC,
    /// <summary>Line chart of closing prices.</summary>
    Line
}

/// <summary>A single data point for stock / financial charts.</summary>
public sealed record StockChartDataPoint
{
    /// <summary>Date/time of the data point.</summary>
    public DateTime Date { get; init; }

    /// <summary>Opening price.</summary>
    public double Open { get; init; }

    /// <summary>Highest price.</summary>
    public double High { get; init; }

    /// <summary>Lowest price.</summary>
    public double Low { get; init; }

    /// <summary>Closing price.</summary>
    public double Close { get; init; }

    /// <summary>Trading volume (optional).</summary>
    public double? Volume { get; init; }

    public StockChartDataPoint() { }

    public StockChartDataPoint(DateTime date, double open, double high, double low, double close, double? volume = null)
    {
        Date = date;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }
}
