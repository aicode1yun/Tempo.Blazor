using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Demo.SharedUI.Services;

/// <summary>
/// Demo <see cref="IHolidayProvider"/> with the Czech public holiday calendar:
/// fixed-date holidays plus Good Friday and Easter Monday computed from the
/// Gregorian (Meeus) Easter algorithm.
/// </summary>
public sealed class CzechHolidayProvider : IHolidayProvider
{
    private static readonly (int Month, int Day, string Name)[] _fixed =
    [
        (1, 1, "Nový rok"),
        (5, 1, "Svátek práce"),
        (5, 8, "Den vítězství"),
        (7, 5, "Den slovanských věrozvěstů Cyrila a Metoděje"),
        (7, 6, "Den upálení mistra Jana Husa"),
        (9, 28, "Den české státnosti"),
        (10, 28, "Den vzniku samostatného československého státu"),
        (11, 17, "Den boje za svobodu a demokracii"),
        (12, 24, "Štědrý den"),
        (12, 25, "1. svátek vánoční"),
        (12, 26, "2. svátek vánoční")
    ];

    /// <inheritdoc />
    public Task<IReadOnlyList<DeadlineHoliday>> GetHolidaysAsync(int year, CancellationToken cancellationToken = default)
    {
        var holidays = _fixed
            .Select(h => new DeadlineHoliday { Date = new DateOnly(year, h.Month, h.Day), Name = h.Name })
            .ToList();

        var easterSunday = EasterSunday(year);
        holidays.Add(new DeadlineHoliday { Date = easterSunday.AddDays(-2), Name = "Velký pátek" });
        holidays.Add(new DeadlineHoliday { Date = easterSunday.AddDays(1), Name = "Velikonoční pondělí" });

        return Task.FromResult<IReadOnlyList<DeadlineHoliday>>(holidays);
    }

    // Meeus/Jones/Butcher Gregorian Easter algorithm.
    private static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
