using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class DocumentEditorSigningIntegrationTests
{
    [Fact]
    public async Task FinalizeForRendition_RequiresSavedVersion()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        var command = new FinalizeForRenditionCommand(new InMemoryDocumentRenditionProvider(provider));

        var result = await command.ExecuteAsync(new DocumentRenditionRequest
        {
            DocumentId = "doc-1"
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("saved document version");
    }

    [Fact]
    public async Task FinalizeForRendition_CreatesImmutableRenditionWithSourceSnapshotHash()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = "doc-1",
            Kind = DocumentVersionKind.Major
        });
        var command = new FinalizeForRenditionCommand(new InMemoryDocumentRenditionProvider(provider));

        var result = await command.ExecuteAsync(new DocumentRenditionRequest
        {
            DocumentId = "doc-1",
            DocumentVersionId = version.Id
        });

        result.Success.Should().BeTrue();
        result.Rendition.Should().NotBeNull();
        result.Rendition!.IsImmutable.Should().BeTrue();
        result.Rendition.Status.Should().Be(DocumentRenditionStatus.Finalized);
        result.Rendition.Hash.SourceSnapshotHash.Should().Be(version.Snapshot.Hash);
    }

    [Fact]
    public async Task FinalizedRendition_DoesNotChangeWhenSourceDocumentIsEditedAndNewVersionRequiresNewRendition()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var document = provider.SeedContractDocument("doc-1");
        var originalVersion = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = "doc-1",
            Kind = DocumentVersionKind.Major
        });
        var renditionProvider = new InMemoryDocumentRenditionProvider(provider);
        var command = new FinalizeForRenditionCommand(renditionProvider);
        var renditionResult = await command.ExecuteAsync(new DocumentRenditionRequest
        {
            DocumentId = "doc-1",
            DocumentVersionId = originalVersion.Id
        });

        document.Metadata.Title = "Edited after finalization";
        await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = document,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        });
        var editedVersion = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = "doc-1",
            Kind = DocumentVersionKind.Minor
        });
        var loadedRendition = await renditionProvider.GetRenditionAsync(renditionResult.Rendition!.Id);

        loadedRendition!.DocumentVersionId.Should().Be(originalVersion.Id);
        loadedRendition.Hash.SourceSnapshotHash.Should().Be(originalVersion.Snapshot.Hash);
        DocumentRenditionCompatibility.RequiresNewRendition(loadedRendition, editedVersion).Should().BeTrue();
        DocumentRenditionCompatibility.RequiresNewRendition(loadedRendition, originalVersion).Should().BeFalse();
    }

    [Fact]
    public void AnchorMapBuilder_MapsTokenAnchorToSigningFieldAreaCoordinates()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        var block = ParagraphBlock("block-1", new TokenRun { Key = "client.name", DisplayName = "Client" });
        document.Blocks.Add(block);
        var builder = new DocumentAnchorMapBuilder();

        var anchor = builder.Build(document).Single(item => item.Type == DocumentRenditionAnchorType.Token);
        var area = builder.ToSigningFieldArea(anchor, "rendition-pdf");

        anchor.Key.Should().Be("client.name");
        anchor.PageNumber.Should().Be(1);
        area.AttachmentUuid.Should().Be("rendition-pdf");
        area.Page.Should().Be(0);
        area.X.Should().BeInRange(0, 1);
        area.Y.Should().BeInRange(0, 1);
        area.Width.Should().BeGreaterThan(0);
        area.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AnchorMapBuilder_MapsExplicitSigningPlaceholderToSigningField()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(ParagraphBlock("signature-block", new TextRun { Text = "Signature:" }));
        document.Anchors.Add(new DocumentAnchor
        {
            Type = DocumentAnchorType.SigningPlaceholder,
            BlockId = "signature-block",
            Key = "client.signature",
            SigningPlaceholder = new DocumentSigningPlaceholder
            {
                Key = "client.signature",
                Label = "Client signature",
                SubmitterUuid = "client",
                FieldType = SigningFieldType.Signature,
                Required = true
            }
        });
        var builder = new DocumentAnchorMapBuilder();

        var anchor = builder.Build(document).Single(item => item.Type == DocumentRenditionAnchorType.Placeholder);
        var field = builder.ToSigningField(anchor, "rendition-pdf");

        anchor.SigningPlaceholder.Should().NotBeNull();
        field.SubmitterUuid.Should().Be("client");
        field.Type.Should().Be(SigningFieldType.Signature);
        field.Areas.Should().ContainSingle(area => area.AttachmentUuid == "rendition-pdf" && area.Page == 0);
    }

    [Fact]
    public void AnchorMapBuilder_LabelsHeaderFooterAnchorsWithScopeInformation()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "header-1",
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks =
            [
                ParagraphBlock("header-block", new TokenRun { Key = "matter.number", DisplayName = "Matter" })
            ]
        });

        var anchor = new DocumentAnchorMapBuilder().Build(document)
            .Single(item => item.Key == "matter.number");

        anchor.Scope.Should().Be(DocumentRenditionAnchorScope.Header);
        anchor.HeaderFooterId.Should().Be("header-1");
    }

    [Fact]
    public void AnchorMapBuilder_MapsMergedTableCellToExpandedBoundingBox()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "table-1",
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "merged-cell",
                                ColumnSpan = 2,
                                RowSpan = 1,
                                Blocks = [ParagraphBlock("cell-block", new TextRun { Text = "Signer" })]
                            },
                            new TableCellContent
                            {
                                Id = "side-cell",
                                Blocks = [ParagraphBlock("side-block", new TextRun { Text = "Date" })]
                            }
                        ]
                    }
                ]
            }
        });
        document.Anchors.Add(new DocumentAnchor
        {
            Type = DocumentAnchorType.SigningPlaceholder,
            TableCellId = "merged-cell",
            SigningPlaceholder = new DocumentSigningPlaceholder { Key = "table.signature" }
        });

        var anchor = new DocumentAnchorMapBuilder().Build(document)
            .Single(item => item.Key == "table.signature");

        anchor.SourceCellId.Should().Be("merged-cell");
        anchor.ColumnSpan.Should().Be(2);
        anchor.Width.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void AnchorMapBuilder_UsesFloatingLayoutForFloatingAnchors()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Anchors.Add(new DocumentAnchor
        {
            Type = DocumentAnchorType.SigningPlaceholder,
            SigningPlaceholder = new DocumentSigningPlaceholder
            {
                Key = "floating.signature",
                Width = 0.1,
                Height = 0.05
            },
            FloatingLayout = new DocumentFloatingLayout
            {
                Inline = false,
                X = 200,
                Y = 300
            }
        });

        var anchor = new DocumentAnchorMapBuilder().Build(document)
            .Single(item => item.Key == "floating.signature");

        anchor.Scope.Should().Be(DocumentRenditionAnchorScope.FloatingObject);
        anchor.X.Should().BeApproximately(200 / DocumentPageSize.A4.Width, 0.001);
        anchor.Y.Should().BeApproximately(300 / DocumentPageSize.A4.Height, 0.001);
        anchor.Width.Should().Be(0.1);
        anchor.Height.Should().Be(0.05);
    }

    private static DocumentBlock ParagraphBlock(string id, InlineContent inline)
    {
        return new DocumentBlock
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [inline]
            }
        };
    }
}
