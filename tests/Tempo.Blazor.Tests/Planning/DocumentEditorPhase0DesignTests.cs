using FluentAssertions;

namespace Tempo.Blazor.Tests.Planning;

public class DocumentEditorPhase0DesignTests
{
    [Fact]
    public void Phase0Design_ApprovesCorePublicApiDecisions()
    {
        var design = ReadPhase0Design();

        design.Should().Contain("Status: Phase 0 approved");
        design.Should().Contain("UI editoru má být ve stylu Wordu");
        design.Should().Contain("horní toolbar/ribbon");
        design.Should().Contain("Bloky jsou interní persistence/rendering/operation detail");
        design.Should().Contain("Slash menu není primární interakce");
        design.Should().Contain("public sealed partial class TmDocumentEditor");
        design.Should().Contain("DocumentId");
        design.Should().Contain("IDocumentEditorProvider Provider");
        design.Should().Contain("ReadOnly");
        design.Should().Contain("DocumentEditorMode Mode");
        design.Should().Contain("ShowToolbar");
        design.Should().Contain("ShowComments");
        design.Should().Contain("ShowVersionHistory");
        design.Should().Contain("AutoSaveInterval");
        design.Should().Contain("OnDocumentLoaded");
        design.Should().Contain("OnDocumentChanged");
        design.Should().Contain("OnSaveRequested");
        design.Should().Contain("OnVersionCreated");
        design.Should().Contain("OnCommentCreated");
        design.Should().Contain("OnAuditEvent");
    }

    [Fact]
    public void Phase0Design_ApprovesFormatAndPackageBoundaries()
    {
        var design = ReadPhase0Design();

        design.Should().Contain("JSON snapshot je source of truth");
        design.Should().Contain("DOCX/ODT nejsou source of truth");
        design.Should().Contain("Tempo.Blazor.DocumentFormats");
        design.Should().Contain("žádná reference z `Tempo.Blazor` na `Tempo.Blazor.DocumentFormats`");
        design.Should().Contain("použít Open XML SDK");
        design.Should().Contain("ZIP + XML parsing");
    }

    [Fact]
    public void Phase0Design_ApprovesV1CompatibilityTargets()
    {
        var design = ReadPhase0Design();

        design.Should().Contain("tables including horizontally and vertically merged cells");
        design.Should().Contain("headers/footers");
        design.Should().Contain("footnotes/endnotes");
        design.Should().Contain("comments");
        design.Should().Contain("section properties");
        design.Should().Contain("tracked changes as Word revisions");
        design.Should().Contain("floating/anchored images");
        design.Should().Contain("pixel-perfect Word layout is not guaranteed");
    }

    [Fact]
    public void Phase0Design_ApprovesSigningReadyBoundary()
    {
        var design = ReadPhase0Design();

        design.Should().Contain("DocumentRendition");
        design.Should().Contain("DocumentRenditionId");
        design.Should().Contain("DocumentVersionId");
        design.Should().Contain("TmDocumentEditor` nesmí přímo referencovat signing komponenty");
        design.Should().Contain("Souřadnice signing polí se ukládají vůči konkrétní rendition");
    }

    private static string ReadPhase0Design()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "planning", "document-editor-phase-0-design.md");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not locate planning/document-editor-phase-0-design.md from test output directory.");
    }
}
