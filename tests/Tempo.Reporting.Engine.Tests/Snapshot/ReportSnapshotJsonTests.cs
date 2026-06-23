using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Reporting.Engine.Tests.Snapshot;

public sealed class ReportSnapshotJsonTests
{
    [Fact]
    public void SerializeDeserialize_RoundTripsDeterministicallyWithSchemaVersion()
    {
        var snapshot = new ReportSnapshot
        {
            SnapshotId = "snap-f0",
            Pages =
            [
                new ReportSnapshotPage
                {
                    PageNumber = 1,
                    Width = 794,
                    Height = 1123,
                    Commands =
                    [
                        ReportSnapshotCommand.Rectangle("page-background", 0, 0, 794, 1123, "#ffffff", "#d1d5db", 1),
                        ReportSnapshotCommand.TextRun("heading", "Fidelity žluťoučký 会社", 72, 96, 281.5, 24, "Tempo F0 Sans", 18, "#111827")
                    ]
                }
            ]
        };

        var json = ReportSnapshotJsonSerializer.Serialize(snapshot);
        var restored = ReportSnapshotJsonSerializer.Deserialize(json);
        var jsonAgain = ReportSnapshotJsonSerializer.Serialize(restored);

        json.Should().Contain("\"schemaVersion\":1");
        json.Should().Contain("\"type\":\"textRun\"");
        restored.SchemaVersion.Should().Be(ReportSnapshot.CurrentSchemaVersion);
        restored.Pages.Should().ContainSingle();
        restored.Pages[0].Commands.Should().HaveCount(2);
        restored.Pages[0].Commands[1].Text.Should().Be("Fidelity žluťoučký 会社");
        jsonAgain.Should().Be(json);
    }

    [Fact]
    public void Deserialize_RejectsUnsupportedSchemaVersion()
    {
        var json = """
        {"schemaVersion":999,"snapshotId":"bad","pages":[]}
        """;

        var act = () => ReportSnapshotJsonSerializer.Deserialize(json);

        act.Should().Throw<ReportSnapshotJsonException>()
            .WithMessage("*schema version*999*");
    }
}
