using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Blazor.Reporting.Tests.Fixtures;

internal static class ReportingSnapshots
{
    public static ReportSnapshot TwoPageSnapshot()
        => new()
        {
            SnapshotId = "viewer-test",
            Pages =
            [
                Page(1, "First page"),
                Page(2, "Second page"),
            ],
        };

    public static ReportSnapshot PageSnapshot(string text)
        => new()
        {
            SnapshotId = $"viewer-{text}",
            Pages = [Page(1, text)],
        };

    private static ReportSnapshotPage Page(int pageNumber, string text)
        => new()
        {
            PageNumber = pageNumber,
            Width = 320,
            Height = 200,
            Commands =
            [
                ReportSnapshotCommand.Rectangle($"p{pageNumber}-bg", 0, 0, 320, 200, "#ffffff", "#e5e7eb", 1),
                ReportSnapshotCommand.TextRun($"p{pageNumber}-text", text, 24, 56, 160, 18, "Inter", 14, "#111827"),
            ],
        };
}
