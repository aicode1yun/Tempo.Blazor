using FluentAssertions;

namespace Tempo.Blazor.DocumentFormats.Tests;

public sealed class DocumentPackageRoundTripReportTests
{
    [Fact]
    public void Create_CombinesWarningsAndPreservedParts()
    {
        var import = new DocumentFormatImportResult
        {
            Format = DocumentFormatKind.Docx,
            Warnings =
            [
                new DocumentFormatCompatibilityWarning { Code = "import.warning", Message = "Import warning" }
            ],
            PreservedParts =
            [
                new DocumentFormatPreservedPart { Path = "/custom/item.xml", ContentType = "application/xml", Content = [1, 2, 3] }
            ]
        };
        var export = new DocumentFormatExportResult
        {
            Format = DocumentFormatKind.Odt,
            Warnings =
            [
                new DocumentFormatCompatibilityWarning { Code = "export.warning", Message = "Export warning" }
            ]
        };

        var report = DocumentPackageRoundTripReport.Create(import, export);

        report.SourceFormat.Should().Be(DocumentFormatKind.Docx);
        report.TargetFormat.Should().Be(DocumentFormatKind.Odt);
        report.IsLossless.Should().BeFalse();
        report.Warnings.Select(warning => warning.Code).Should().Equal("import.warning", "export.warning");
        report.PreservedParts.Should().ContainSingle(part => part.Path == "/custom/item.xml");
    }
}
