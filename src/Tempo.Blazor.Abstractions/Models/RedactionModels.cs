using System.Globalization;
using System.Text;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>PII category of a redacted area (drives labeling and reporting, app-extensible via Other + Note).</summary>
public enum RedactionCategory
{
    /// <summary>Personal identification number (e.g. rodné číslo, SSN).</summary>
    PersonalId = 0,

    /// <summary>Personal or company name.</summary>
    Name = 1,

    /// <summary>Postal address.</summary>
    Address = 2,

    /// <summary>Contact detail (phone, e-mail).</summary>
    Contact = 3,

    /// <summary>Bank account or payment identifier.</summary>
    BankAccount = 4,

    /// <summary>Date (birth, signature, …).</summary>
    Date = 5,

    /// <summary>Anything else.</summary>
    Other = 6
}

/// <summary>
/// One rectangle marked for redaction, in page-relative normalized coordinates (0–1,
/// origin top-left). <see cref="PageNumber"/> is 1-based; image redactions use page 1.
/// </summary>
public sealed class RedactionArea
{
    /// <summary>Stable identifier of the area.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>1-based page number the rectangle belongs to.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>Left edge relative to the page width (0–1).</summary>
    public double X { get; set; }

    /// <summary>Top edge relative to the page height (0–1).</summary>
    public double Y { get; set; }

    /// <summary>Width relative to the page width (0–1).</summary>
    public double Width { get; set; }

    /// <summary>Height relative to the page height (0–1).</summary>
    public double Height { get; set; }

    /// <summary>PII category of the redacted content.</summary>
    public RedactionCategory Category { get; set; } = RedactionCategory.Other;

    /// <summary>Optional reviewer note (e.g. what was redacted, a ticket number).</summary>
    public string? Note { get; set; }

    /// <summary>Creates a deep copy.</summary>
    public RedactionArea Clone() => (RedactionArea)MemberwiseClone();
}

/// <summary>Persistence of redaction definitions per document.</summary>
public interface IRedactionProvider
{
    /// <summary>Loads the saved areas of a document (empty when none).</summary>
    Task<IReadOnlyList<RedactionArea>> LoadAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the saved areas of a document.</summary>
    Task SaveAsync(string documentId, IReadOnlyList<RedactionArea> areas, CancellationToken cancellationToken = default);
}

/// <summary>In-memory <see cref="IRedactionProvider"/> for demos and tests (clone-on-read/write).</summary>
public sealed class InMemoryRedactionProvider : IRedactionProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<RedactionArea>> _areas = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<IReadOnlyList<RedactionArea>> LoadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<RedactionArea> result = _areas.TryGetValue(documentId, out var list)
                ? list.Select(a => a.Clone()).ToList()
                : [];
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(string documentId, IReadOnlyList<RedactionArea> areas, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(areas);
        lock (_gate)
        {
            _areas[documentId] = areas.Select(a => a.Clone()).ToList();
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Builds the culture-invariant JSON payload driving the destructive JS export
/// (rasterize pages, burn black rectangles, rebuild the file WITHOUT the original
/// content streams). Rects are clamped to the page and degenerate rects dropped, so
/// the export never receives coordinates it could silently ignore.
/// </summary>
public static class RedactionExportPayloadBuilder
{
    /// <summary>Serializes the areas grouped by ascending page number.</summary>
    public static string Build(IEnumerable<RedactionArea> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        var pages = areas
            .Select(Clamp)
            .Where(a => a is { Width: > 0, Height: > 0 })
            .Cast<RedactionArea>()
            .GroupBy(a => a.PageNumber)
            .OrderBy(g => g.Key);

        var builder = new StringBuilder("{\"pages\":[");
        var firstPage = true;
        foreach (var page in pages)
        {
            if (!firstPage)
            {
                builder.Append(',');
            }

            firstPage = false;
            builder.Append("{\"pageNumber\":").Append(page.Key.ToString(CultureInfo.InvariantCulture)).Append(",\"rects\":[");
            var firstRect = true;
            foreach (var area in page)
            {
                if (!firstRect)
                {
                    builder.Append(',');
                }

                firstRect = false;
                builder.Append("{\"x\":").Append(Num(area.X))
                    .Append(",\"y\":").Append(Num(area.Y))
                    .Append(",\"width\":").Append(Num(area.Width))
                    .Append(",\"height\":").Append(Num(area.Height))
                    .Append('}');
            }

            builder.Append("]}");
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static RedactionArea? Clamp(RedactionArea area)
    {
        var x = Math.Clamp(area.X, 0d, 1d);
        var y = Math.Clamp(area.Y, 0d, 1d);
        var right = Math.Clamp(area.X + area.Width, 0d, 1d);
        var bottom = Math.Clamp(area.Y + area.Height, 0d, 1d);
        var width = right - x;
        var height = bottom - y;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var clone = area.Clone();
        clone.X = x;
        clone.Y = y;
        clone.Width = width;
        clone.Height = height;
        return clone;
    }

    private static string Num(double value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);
}
