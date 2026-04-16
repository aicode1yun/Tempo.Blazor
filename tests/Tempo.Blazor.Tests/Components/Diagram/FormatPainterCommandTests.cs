using FluentAssertions;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class FormatPainterCommandTests
{
    [Fact]
    public void PasteStyle_ChangesFillColor()
    {
        var source = new DiagramNode { Style = new DiagramStyle { Fill = "#ff0000" } };
        var target = new DiagramNode { Style = new DiagramStyle { Fill = "#0000ff" } };
        var doc = new DiagramDocument();
        doc.Nodes.Add(target);

        new CopyStyleCommand(source).Execute();
        new PasteStyleCommand(doc, new[] { target.Id }).Execute();

        target.Style.Fill.Should().Be("#ff0000");
    }

    [Fact]
    public void PasteStyle_Undo_RestoresOriginalFill()
    {
        var source = new DiagramNode { Style = new DiagramStyle { Fill = "#ff0000" } };
        var target = new DiagramNode { Style = new DiagramStyle { Fill = "#0000ff" } };
        var doc = new DiagramDocument();
        doc.Nodes.Add(target);

        var paste = new PasteStyleCommand(doc, new[] { target.Id });
        new CopyStyleCommand(source).Execute();
        paste.Execute();
        paste.Undo();

        target.Style.Fill.Should().Be("#0000ff");
    }

    [Fact]
    public void PasteSize_ChangesWidthAndHeight()
    {
        var source = new DiagramNode { W = 200, H = 150 };
        var target = new DiagramNode { W = 100, H = 100 };
        var doc = new DiagramDocument();
        doc.Nodes.Add(target);

        new CopyStyleCommand(source, includeSize: true).Execute();
        new PasteSizeCommand(doc, new[] { target.Id }).Execute();

        target.W.Should().Be(200);
        target.H.Should().Be(150);
    }

    [Fact]
    public void PasteSize_Undo_RestoresOriginalDimensions()
    {
        var source = new DiagramNode { W = 200, H = 150 };
        var target = new DiagramNode { W = 100, H = 100 };
        var doc = new DiagramDocument();
        doc.Nodes.Add(target);

        var paste = new PasteSizeCommand(doc, new[] { target.Id });
        new CopyStyleCommand(source, includeSize: true).Execute();
        paste.Execute();
        paste.Undo();

        target.W.Should().Be(100);
        target.H.Should().Be(100);
    }
}
