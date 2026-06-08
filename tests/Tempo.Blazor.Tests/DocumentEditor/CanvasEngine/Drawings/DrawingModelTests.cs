using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor.CanvasEngine.Drawings;

public sealed class DrawingModelTests
{
    [Fact]
    public void DocumentDrawingRun_CarriesShapeTextBoxConnectorChartAndGroupMetadata()
    {
        var drawing = new DocumentDrawingRun
        {
            Id = "drawing-run-1",
            ObjectId = "drawing-object-1",
            Kind = DocumentDrawingKind.Group,
            AltText = "Revenue diagram",
            Layout = new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Anchored,
                Anchor = { BlockId = "anchor-paragraph", Offset = 3 },
                Wrap = { Mode = DocumentWrapMode.Square, DistanceLeft = 10, DistanceRight = 12 },
                Position = { X = 48, Y = 72 },
                Transform = { Width = 320, Height = 180, Rotation = 15, Flip = new DocumentObjectFlip { Horizontal = true }, LockAspectRatio = false },
                Stacking = { ZIndex = 12, AllowOverlap = true }
            },
            Shape = new DocumentDrawingShape
            {
                Preset = "roundRectangle",
                Fill = new DocumentDrawingFill { Color = "#dbeafe", Opacity = 0.82 },
                Stroke = new DocumentDrawingStroke { Color = "#2563eb", Width = 2.5, Dash = "dash", StartArrow = "oval", EndArrow = "triangle" },
                Shadow = new DocumentDrawingShadow { Blur = 7, OffsetX = 2, OffsetY = 4 },
                Rotation = 15,
                Adjustments = { ["radius"] = 0.22 },
                StartConnection = new DocumentDrawingConnection { ObjectId = "source-shape", Site = "right" },
                EndConnection = new DocumentDrawingConnection { ObjectId = "target-shape", Site = "left" },
                Points = [new DocumentDrawingPoint { X = 0.2, Y = 0.5 }, new DocumentDrawingPoint { X = 0.8, Y = 0.5 }]
            },
            TextBody = new DocumentDrawingTextBody
            {
                InsetLeft = 12,
                InsetTop = 10,
                InsetRight = 12,
                InsetBottom = 10,
                VerticalAlignment = "middle",
                WrapText = true,
                Paragraphs =
                [
                    new DocumentDrawingTextParagraph
                    {
                        Text = "Inside shape",
                        Alignment = "center",
                        Style = new DocumentDrawingTextStyle { FontSize = 15, Bold = true, Color = "#0f172a" }
                    }
                ]
            },
            Chart = new DocumentDrawingChart
            {
                Type = "bar",
                Title = "Revenue",
                Categories = ["Q1", "Q2"],
                Series = [new DocumentDrawingChartSeries { Name = "Actual", Values = [3, 7], Color = "#16a34a" }]
            },
            Group = new DocumentDrawingGroup
            {
                ChildObjectIds = ["source-shape", "target-shape"],
                Origin = new DocumentDrawingPoint { X = 10, Y = 20 },
                Size = new DocumentDrawingPoint { X = 320, Y = 180 }
            },
            Metadata =
            {
                ["preserved:drawingml"] = "shape-properties"
            }
        };

        Assert.Equal(DocumentDrawingKind.Group, drawing.Kind);
        Assert.Equal(DocumentObjectLayoutKind.Anchored, drawing.Layout.Kind);
        Assert.Equal(DocumentWrapMode.Square, drawing.Layout.Wrap.Mode);
        Assert.Equal(15, drawing.Layout.Transform.Rotation);
        Assert.True(drawing.Layout.Transform.Flip?.Horizontal);
        Assert.Equal("roundRectangle", drawing.Shape?.Preset);
        Assert.Equal(0.22, drawing.Shape?.Adjustments["radius"]);
        Assert.Equal("right", drawing.Shape?.StartConnection?.Site);
        Assert.Equal("Inside shape", drawing.TextBody?.Paragraphs.Single().Text);
        Assert.Equal("Revenue", drawing.Chart?.Title);
        Assert.Equal(["source-shape", "target-shape"], drawing.Group?.ChildObjectIds);
        Assert.Equal("shape-properties", drawing.Metadata["preserved:drawingml"]);
    }

    [Fact]
    public void DocumentDrawingRun_JsonRoundTripPreservesE7Metadata()
    {
        var document = DocumentEditorDocument.Empty("e7-drawing-model-json");
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "drawing-paragraph",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new DocumentDrawingRun
                        {
                            Id = "connector-run",
                            ObjectId = "connector-1",
                            Kind = DocumentDrawingKind.Connector,
                            Size = new DocumentImageSize { Width = 180, Height = 60 },
                            NaturalSize = new DocumentImageSize { Width = 180, Height = 60 },
                            Layout = new DocumentObjectLayout
                            {
                                Kind = DocumentObjectLayoutKind.Anchored,
                                Anchor = { BlockId = "drawing-paragraph", Offset = 0 },
                                Position = { X = 22, Y = 34 },
                                Transform = { Width = 180, Height = 60 },
                                Wrap = { Mode = DocumentWrapMode.InFrontOfText },
                                Stacking = { ZIndex = 4 }
                            },
                            Shape = new DocumentDrawingShape
                            {
                                Preset = "bentConnector",
                                Stroke = new DocumentDrawingStroke { Color = "#16a34a", Width = 3, EndArrow = "triangle" },
                                StartConnection = new DocumentDrawingConnection { ObjectId = "shape-a", Site = "right" },
                                EndConnection = new DocumentDrawingConnection { ObjectId = "shape-b", Site = "left" }
                            }
                        }
                    ]
                }
            }
        ];

        var json = JsonSerializer.Serialize(document, DocumentEditorJson.Options);
        var restored = JsonSerializer.Deserialize<DocumentEditorDocument>(json, DocumentEditorJson.Options)!;
        var drawing = Assert.IsType<ParagraphBlockContent>(restored.Blocks.Single().Content)
            .Inlines
            .OfType<DocumentDrawingRun>()
            .Single();

        Assert.Equal(DocumentDrawingKind.Connector, drawing.Kind);
        Assert.Equal("bentConnector", drawing.Shape?.Preset);
        Assert.Equal("triangle", drawing.Shape?.Stroke.EndArrow);
        Assert.Equal("shape-a", drawing.Shape?.StartConnection?.ObjectId);
        Assert.Equal("shape-b", drawing.Shape?.EndConnection?.ObjectId);
        Assert.Equal(4, drawing.Layout.Stacking.ZIndex);
    }
}
