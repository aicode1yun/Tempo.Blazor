namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Specifies the recurrence pattern type for a <see cref="RecurrenceRule"/>.
/// </summary>
public enum RecurrencePattern
{
    /// <summary>Repeats every N days.</summary>
    Daily,

    /// <summary>Repeats every N weeks on selected days.</summary>
    Weekly,

    /// <summary>Repeats monthly on a specific day or position.</summary>
    Monthly,

    /// <summary>Repeats yearly on a specific month and day.</summary>
    Yearly
}
