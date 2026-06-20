using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Tests.Models;

public class SigningGeometryHelperTests
{
    [Fact]
    public void ToPixels_ConvertsNormalizedAreaToPixelRectangle()
    {
        var area = new SigningFieldArea
        {
            X = 0.1,
            Y = 0.25,
            Width = 0.5,
            Height = 0.125
        };

        var rect = SigningGeometryHelper.ToPixels(area, pageWidth: 1000, pageHeight: 800);

        rect.X.Should().Be(100);
        rect.Y.Should().Be(200);
        rect.Width.Should().Be(500);
        rect.Height.Should().Be(100);
    }

    [Fact]
    public void Clamp_KeepsAreaInsidePage()
    {
        var area = new SigningFieldArea
        {
            X = 0.9,
            Y = -0.2,
            Width = 0.3,
            Height = 0.4
        };

        var clamped = SigningGeometryHelper.Clamp(area);

        clamped.X.Should().BeApproximately(0.7, 0.000001);
        clamped.Y.Should().BeApproximately(0, 0.000001);
        clamped.Width.Should().BeApproximately(0.3, 0.000001);
        clamped.Height.Should().BeApproximately(0.4, 0.000001);
    }

    [Fact]
    public void Clamp_EnforcesMinimumSize()
    {
        var area = new SigningFieldArea
        {
            X = 0.2,
            Y = 0.2,
            Width = 0.001,
            Height = 0.002
        };

        var clamped = SigningGeometryHelper.Clamp(area, minWidth: 0.05, minHeight: 0.04);

        clamped.Width.Should().BeApproximately(0.05, 0.000001);
        clamped.Height.Should().BeApproximately(0.04, 0.000001);
    }

    [Fact]
    public void Move_AppliesDeltaAndClamps()
    {
        var area = new SigningFieldArea
        {
            X = 0.8,
            Y = 0.8,
            Width = 0.25,
            Height = 0.25
        };

        var moved = SigningGeometryHelper.Move(area, deltaX: 0.2, deltaY: 0.2);

        moved.X.Should().BeApproximately(0.75, 0.000001);
        moved.Y.Should().BeApproximately(0.75, 0.000001);
        moved.Width.Should().BeApproximately(0.25, 0.000001);
        moved.Height.Should().BeApproximately(0.25, 0.000001);
    }

    [Fact]
    public void Resize_SouthEastHandle_ChangesSizeAndClampsToPage()
    {
        var area = new SigningFieldArea
        {
            X = 0.6,
            Y = 0.7,
            Width = 0.3,
            Height = 0.2
        };

        var resized = SigningGeometryHelper.Resize(
            area,
            SigningResizeHandle.SouthEast,
            deltaX: 0.3,
            deltaY: 0.3,
            minWidth: 0.05,
            minHeight: 0.05);

        resized.X.Should().BeApproximately(0.6, 0.000001);
        resized.Y.Should().BeApproximately(0.7, 0.000001);
        resized.Width.Should().BeApproximately(0.4, 0.000001);
        resized.Height.Should().BeApproximately(0.3, 0.000001);
    }

    [Fact]
    public void GetSelectionRectangle_ReturnsBoundingBoxForAreas()
    {
        var areas = new[]
        {
            new SigningFieldArea { X = 0.1, Y = 0.2, Width = 0.2, Height = 0.2 },
            new SigningFieldArea { X = 0.4, Y = 0.1, Width = 0.3, Height = 0.5 }
        };

        var selection = SigningGeometryHelper.GetSelectionRectangle(areas);

        selection.X.Should().BeApproximately(0.1, 0.000001);
        selection.Y.Should().BeApproximately(0.1, 0.000001);
        selection.Width.Should().BeApproximately(0.6, 0.000001);
        selection.Height.Should().BeApproximately(0.5, 0.000001);
    }
}
