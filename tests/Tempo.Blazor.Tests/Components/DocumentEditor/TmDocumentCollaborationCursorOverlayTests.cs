using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public sealed class TmDocumentCollaborationCursorOverlayTests : LocalizationTestBase
{
    [Fact]
    public void CursorOverlay_RendersRemoteCursors()
    {
        var cut = Render<TmDocumentCollaborationCursorOverlay>(parameters =>
            parameters.Add(p => p.Cursors, new[]
            {
                new DocumentCollaborationCursor
                {
                    DocumentId = "doc-1",
                    ClientId = "remote",
                    DisplayName = "Remote User",
                    BlockId = "block-1",
                    Color = "#2563eb"
                }
            }));

        cut.Find("[data-testid='document-collaboration-cursor']").TextContent.Should().Contain("Remote User");
        cut.Find("[data-testid='document-collaboration-cursor']").GetAttribute("data-block-id").Should().Be("block-1");
    }
}
