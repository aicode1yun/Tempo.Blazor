namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase42Tests
{
    [Fact]
    public void Phase42_ArchitectureNoteDocumentsDocxDrawingDecisions()
    {
        var note = ReadArchitectureNote();

        note.Should().Contain("wp:inline");
        note.Should().Contain("wp:anchor");
        note.Should().Contain("DocumentDrawingRun");
        note.Should().Contain("EMU");
        note.ToLowerInvariant().Should().Contain("media part security model");
        note.ToLowerInvariant().Should().Contain("unsupported drawingml preserve/warning policy");
        note.Should().Contain("zpetna kompatibilita se starym `ImageBlockContent` modelem neni cilova editacni architektura");
        note.Should().Contain("Fixture sada Word/OnlyOffice");
    }

    [Fact]
    public void Phase42_ArchitectureNoteDocumentsReleaseGate()
    {
        var note = ReadArchitectureNote();

        note.Should().Contain("dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter \"FullyQualifiedName~DocxDrawing\"");
        note.Should().Contain("dotnet test tests/Tempo.Blazor.Tests/Tempo.Blazor.Tests.csproj --filter \"FullyQualifiedName~DocumentEditorImageDrawing\"");
        note.Should().Contain("dotnet test tests/Tempo.Blazor.Demo.Api.Tests/Tempo.Blazor.Demo.Api.Tests.csproj --filter \"FullyQualifiedName~FormatExportImport\"");
        note.Should().Contain("DocumentEditorImageOnlyOfficeParityE2ETests");
        note.Should().Contain("DocumentEditorImageDocxPhase39E2ETests");
        note.Should().Contain("OnlyOffice");
        note.Should().Contain("Word Online");
    }

    private static string ReadArchitectureNote()
        => File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "planning",
            "tmdocumenteditor-docx-drawingml-architecture-2026-05-25.md"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Tempo.Blazor.DocumentFormats"))
                && Directory.Exists(Path.Combine(directory.FullName, "planning")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Tempo.Blazor repository root.");
    }
}
