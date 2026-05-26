using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentDrawingRunSerializationTests
{
    [Fact]
    public void SerializeRoundTrip_PreservesDrawingRunPayloadAndLayoutVariants()
    {
        var document = DocumentEditorDocument.Empty("drawing-roundtrip");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Id = "text-before", Text = "Before " },
                    CreateDrawing("drawing-inline", DocumentImageSource.Url, "/inline.png", null, DocumentObjectLayoutKind.Inline, DocumentWrapMode.Inline, null, 0, 0),
                    new TextRun { Id = "text-middle", Text = " middle " },
                    CreateDrawing("drawing-square", DocumentImageSource.Asset, null, "asset-square", DocumentObjectLayoutKind.Anchored, DocumentWrapMode.Square, DocumentImageHorizontalPosition.Left, 3, 0),
                    CreateDrawing("drawing-top-bottom", DocumentImageSource.Url, "https://example.test/top-bottom.png", null, DocumentObjectLayoutKind.Anchored, DocumentWrapMode.TopBottom, DocumentImageHorizontalPosition.Center, 4, 5),
                    CreateDrawing("drawing-behind", DocumentImageSource.Url, "https://example.test/behind.png", null, DocumentObjectLayoutKind.Anchored, DocumentWrapMode.BehindText, DocumentImageHorizontalPosition.Right, 5, 7),
                    CreateDrawing("drawing-front", DocumentImageSource.Url, "https://example.test/front.png", null, DocumentObjectLayoutKind.Fixed, DocumentWrapMode.InFrontOfText, DocumentImageHorizontalPosition.Right, 9, 11),
                    new TextRun { Id = "text-after", Text = " after." }
                ]
            }
        });

        var json = DocumentEditorJson.Serialize(document);
        var restored = DocumentEditorJson.Deserialize(json);

        json.Should().Contain("\"$type\":\"drawing\"");
        json.Should().NotContain("\"$type\":\"image\"");
        restored.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        var restoredInlines = ((ParagraphBlockContent)restored.Blocks.Single().Content).Inlines;
        restoredInlines.OfType<DocumentDrawingRun>().Should().HaveCount(5);
        AssertDrawing(restoredInlines, "drawing-inline", DocumentImageSource.Url, "/inline.png", null, DocumentObjectLayoutKind.Inline, DocumentWrapMode.Inline, null, 0, 0);
        AssertDrawing(restoredInlines, "drawing-square", DocumentImageSource.Asset, null, "asset-square", DocumentObjectLayoutKind.Anchored, DocumentWrapMode.Square, DocumentImageHorizontalPosition.Left, 3, 0);
        AssertDrawing(restoredInlines, "drawing-top-bottom", DocumentImageSource.Url, "https://example.test/top-bottom.png", null, DocumentObjectLayoutKind.Anchored, DocumentWrapMode.TopBottom, DocumentImageHorizontalPosition.Center, 4, 5);
        AssertDrawing(restoredInlines, "drawing-behind", DocumentImageSource.Url, "https://example.test/behind.png", null, DocumentObjectLayoutKind.Anchored, DocumentWrapMode.BehindText, DocumentImageHorizontalPosition.Right, 5, 7);
        AssertDrawing(restoredInlines, "drawing-front", DocumentImageSource.Url, "https://example.test/front.png", null, DocumentObjectLayoutKind.Fixed, DocumentWrapMode.InFrontOfText, DocumentImageHorizontalPosition.Right, 9, 11);
    }

    [Fact]
    public void Deserialize_DrawingRunJson_RestoresPolymorphicInline()
    {
        const string json =
            """
            {
              "DocumentId": "drawing-json",
              "Blocks": [
                {
                  "Id": "paragraph-1",
                  "Type": 0,
                  "Content": {
                    "$type": "paragraph",
                    "Inlines": [
                      { "$type": "text", "Id": "text-1", "Text": "Before ", "Marks": [] },
                      {
                        "$type": "drawing",
                        "Id": "drawing-inline-1",
                        "ObjectId": "image-object-1",
                        "Kind": 0,
                        "Source": 1,
                        "AssetId": "asset-1",
                        "AltText": "Asset image",
                        "Caption": "Asset caption",
                        "Size": { "Width": 320, "Height": 180 },
                        "NaturalSize": { "Width": 640, "Height": 360 },
                        "Layout": {
                          "Kind": 1,
                          "Anchor": { "BlockId": "paragraph-1", "InlineIndex": 1, "Offset": 7 },
                          "Position": { "HorizontalAlignment": 0 },
                          "Wrap": { "Mode": 1, "DistanceLeft": 6, "DistanceRight": 8 },
                          "Transform": { "Width": 320, "Height": 180 },
                          "Stacking": { "ZIndex": 2 }
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var document = DocumentEditorJson.Deserialize(json);

        var drawing = ((ParagraphBlockContent)document.Blocks.Single().Content).Inlines[1]
            .Should().BeOfType<DocumentDrawingRun>().Subject;
        drawing.ObjectId.Should().Be("image-object-1");
        drawing.Source.Should().Be(DocumentImageSource.Asset);
        drawing.AssetId.Should().Be("asset-1");
        drawing.AltText.Should().Be("Asset image");
        drawing.Caption.Should().Be("Asset caption");
        drawing.Size.Width.Should().Be(320);
        drawing.NaturalSize.Width.Should().Be(640);
        drawing.Layout.Anchor.BlockId.Should().Be("paragraph-1");
        drawing.Layout.Anchor.InlineIndex.Should().Be(1);
        drawing.Layout.Anchor.Offset.Should().Be(7);
        drawing.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        drawing.Layout.Wrap.DistanceRight.Should().Be(8);
        drawing.Layout.Transform.Width.Should().Be(320);
        drawing.Layout.Stacking.ZIndex.Should().Be(2);
    }

    [Fact]
    public void Normalize_DrawingRuns_RemoveDisplayOnlyUrlsAndPreservePersistentPayloadAcrossScopes()
    {
        const string dataUrl = "data:image/png;base64,iVBORw0KGgo=";
        var assetDrawing = CreateDrawing("asset-image", DocumentImageSource.Asset, null, "asset-1", DocumentObjectLayoutKind.Anchored, DocumentWrapMode.Square, DocumentImageHorizontalPosition.Left, 3, 0);
        assetDrawing.Url = "blob:https://app.test/asset-display";
        var document = DocumentEditorDocument.Empty("drawing-persistence");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    assetDrawing,
                    CreateDrawing("safe-url-image", DocumentImageSource.Url, "https://cdn.example.test/safe.png", null, DocumentObjectLayoutKind.Inline, DocumentWrapMode.Inline, null, 0, 0),
                    CreateDrawing("data-url-image", DocumentImageSource.Url, dataUrl, null, DocumentObjectLayoutKind.Inline, DocumentWrapMode.Inline, null, 0, 0),
                    CreateDrawing("blob-url-image", DocumentImageSource.Url, "blob:https://app.test/view-only", null, DocumentObjectLayoutKind.Anchored, DocumentWrapMode.TopBottom, DocumentImageHorizontalPosition.Center, 4, 5)
                ]
            }
        });
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
                                Id = "cell-1",
                                Blocks =
                                [
                                    new DocumentBlock
                                    {
                                        Id = "cell-paragraph",
                                        Type = DocumentBlockType.Paragraph,
                                        Content = new ParagraphBlockContent
                                        {
                                            Inlines =
                                            [
                                                CreateScopedDrawing(
                                                    "cell-image",
                                                    DocumentRenditionAnchorScope.TableCell,
                                                    tableId: "table-1",
                                                    cellId: "cell-1",
                                                    headerFooterId: null)
                                            ]
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "header-primary",
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "header-paragraph",
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines =
                        [
                            CreateScopedDrawing(
                                "header-image",
                                DocumentRenditionAnchorScope.Header,
                                tableId: null,
                                cellId: null,
                                headerFooterId: "header-primary")
                        ]
                    }
                }
            ]
        });

        var normalizedJson = DocumentEditorJson.Normalize(DocumentEditorJson.Serialize(document));
        var restored = DocumentEditorJson.Deserialize(normalizedJson);
        var drawings = DocumentImagePersistence.EnumerateDrawingRuns(restored).ToArray();

        normalizedJson.Should().Contain("\"$type\":\"drawing\"");
        normalizedJson.Should().NotContain("blob:");
        normalizedJson.Should().Contain("asset-1");
        normalizedJson.Should().Contain("https://cdn.example.test/safe.png");
        normalizedJson.Should().Contain(dataUrl);
        drawings.Should().HaveCount(6);

        var asset = drawings.Single(drawing => drawing.ObjectId == "asset-image");
        asset.AssetId.Should().Be("asset-1");
        asset.Url.Should().BeNull();
        asset.Caption.Should().Be("asset-image caption");
        asset.AltText.Should().Be("asset-image alt");
        asset.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        asset.Layout.Anchor.BlockId.Should().Be("paragraph-1");
        asset.Layout.Transform.Width.Should().Be(220);

        drawings.Single(drawing => drawing.ObjectId == "safe-url-image").Url.Should().Be("https://cdn.example.test/safe.png");
        drawings.Single(drawing => drawing.ObjectId == "data-url-image").Url.Should().Be(dataUrl);
        drawings.Single(drawing => drawing.ObjectId == "blob-url-image").Url.Should().BeNull();

        var cell = drawings.Single(drawing => drawing.ObjectId == "cell-image");
        cell.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.TableCell);
        cell.Layout.Anchor.TableId.Should().Be("table-1");
        cell.Layout.Anchor.CellId.Should().Be("cell-1");

        var header = drawings.Single(drawing => drawing.ObjectId == "header-image");
        header.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.Header);
        header.Layout.Anchor.HeaderFooterId.Should().Be("header-primary");
    }

    private static DocumentDrawingRun CreateDrawing(
        string objectId,
        DocumentImageSource source,
        string? url,
        string? assetId,
        DocumentObjectLayoutKind kind,
        DocumentWrapMode wrapMode,
        DocumentImageHorizontalPosition? horizontalPosition,
        int zIndex,
        double rotation)
        => new()
        {
            Id = $"{objectId}-inline",
            ObjectId = objectId,
            Source = source,
            Url = source == DocumentImageSource.Url ? url : null,
            AssetId = source == DocumentImageSource.Asset ? assetId : null,
            AltText = $"{objectId} alt",
            Caption = $"{objectId} caption",
            Size = new DocumentImageSize { Width = 220, Height = 124 },
            NaturalSize = new DocumentImageSize { Width = 440, Height = 248 },
            Layout = new DocumentObjectLayout
            {
                Kind = kind,
                Anchor = new DocumentObjectAnchor
                {
                    BlockId = "paragraph-1",
                    InlineIndex = 1,
                    Offset = 7,
                    MoveWithText = kind != DocumentObjectLayoutKind.Fixed,
                    FixedOnPage = kind == DocumentObjectLayoutKind.Fixed,
                    LockAnchor = kind == DocumentObjectLayoutKind.Fixed
                },
                Position = new DocumentObjectPosition
                {
                    HorizontalAlignment = horizontalPosition,
                    HorizontalRelativeTo = DocumentRelativePosition.Page,
                    VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                    X = zIndex,
                    Y = zIndex + 1
                },
                Wrap = new DocumentObjectWrap
                {
                    Mode = wrapMode,
                    DistanceLeft = 5,
                    DistanceRight = 6,
                    DistanceTop = 7,
                    DistanceBottom = 8
                },
                Transform = new DocumentObjectTransform
                {
                    Width = 220,
                    Height = 124,
                    NaturalWidth = 440,
                    NaturalHeight = 248,
                    Rotation = rotation
                },
                Stacking = new DocumentObjectStacking
                {
                    ZIndex = zIndex,
                    AllowOverlap = wrapMode is DocumentWrapMode.BehindText or DocumentWrapMode.InFrontOfText
                }
            },
            Metadata = { ["phase"] = "2" }
        };

    private static DocumentDrawingRun CreateScopedDrawing(
        string objectId,
        DocumentRenditionAnchorScope scope,
        string? tableId,
        string? cellId,
        string? headerFooterId)
    {
        var drawing = CreateDrawing(
            objectId,
            DocumentImageSource.Url,
            $"/{objectId}.png",
            null,
            DocumentObjectLayoutKind.Anchored,
            DocumentWrapMode.Square,
            DocumentImageHorizontalPosition.Right,
            8,
            2);
        drawing.Layout.Anchor.Region = scope;
        drawing.Layout.Anchor.TableId = tableId;
        drawing.Layout.Anchor.CellId = cellId;
        drawing.Layout.Anchor.HeaderFooterId = headerFooterId;
        return drawing;
    }

    private static void AssertDrawing(
        IEnumerable<InlineContent> inlines,
        string objectId,
        DocumentImageSource source,
        string? url,
        string? assetId,
        DocumentObjectLayoutKind kind,
        DocumentWrapMode wrapMode,
        DocumentImageHorizontalPosition? horizontalPosition,
        int zIndex,
        double rotation)
    {
        var drawing = inlines.OfType<DocumentDrawingRun>().Single(run => run.ObjectId == objectId);
        drawing.Source.Should().Be(source);
        drawing.Url.Should().Be(url);
        drawing.AssetId.Should().Be(assetId);
        drawing.AltText.Should().Be($"{objectId} alt");
        drawing.Caption.Should().Be($"{objectId} caption");
        drawing.Size.Width.Should().Be(220);
        drawing.NaturalSize.Width.Should().Be(440);
        drawing.Layout.Kind.Should().Be(kind);
        drawing.Layout.Anchor.BlockId.Should().Be("paragraph-1");
        drawing.Layout.Anchor.MoveWithText.Should().Be(kind != DocumentObjectLayoutKind.Fixed);
        drawing.Layout.Anchor.FixedOnPage.Should().Be(kind == DocumentObjectLayoutKind.Fixed);
        drawing.Layout.Wrap.Mode.Should().Be(wrapMode);
        drawing.Layout.Wrap.DistanceLeft.Should().Be(5);
        drawing.Layout.Wrap.DistanceRight.Should().Be(6);
        drawing.Layout.Position.HorizontalAlignment.Should().Be(horizontalPosition);
        drawing.Layout.Transform.Width.Should().Be(220);
        drawing.Layout.Transform.Height.Should().Be(124);
        drawing.Layout.Transform.NaturalWidth.Should().Be(440);
        drawing.Layout.Transform.Rotation.Should().Be(rotation);
        drawing.Layout.Stacking.ZIndex.Should().Be(zIndex);
        drawing.Metadata.Should().ContainKey("phase").WhoseValue.Should().Be("2");
    }
}
