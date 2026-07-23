namespace Tempo.Blazor.Components.Charts;

/// <summary>Color palette used by <see cref="TmCalendarHeatmap"/>.</summary>
public enum CalendarHeatmapColorScheme
{
    /// <summary>Uses the primary color palette.</summary>
    Primary,

    /// <summary>Uses the success color palette.</summary>
    Success,

    /// <summary>Uses the danger color palette.</summary>
    Danger,

    /// <summary>Uses the neutral color palette.</summary>
    Neutral
}

/// <summary>Identifies a calendar heatmap day selected by the user.</summary>
/// <param name="Date">Selected calendar date.</param>
/// <param name="Value">Value associated with the date, or <see langword="null"/> when no value exists.</param>
public sealed record CalendarHeatmapDayClickEventArgs(DateOnly Date, decimal? Value);

internal sealed record CalendarHeatmapCell(
    DateOnly Date,
    int Week,
    int Day,
    int Level,
    int PaletteLevel,
    decimal? Value);

internal sealed record CalendarHeatmapMonthLabel(string Text, int Week);

internal sealed record CalendarHeatmapDayLabel(string Text, int Row);
