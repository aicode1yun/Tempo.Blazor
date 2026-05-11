using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentTextDiffHelperTests
{
    [Fact]
    public void Diff_DetectsAddedText()
    {
        var diff = DocumentTextDiffHelper.Diff("Smlouva je platná", "Smlouva je dnes platná");

        diff.Segments.Should().Contain(segment =>
            segment.Kind == DocumentTextDiffSegmentKind.Added && segment.Text == "dnes");
    }

    [Fact]
    public void Diff_DetectsDeletedText()
    {
        var diff = DocumentTextDiffHelper.Diff("Smlouva je stále platná", "Smlouva je platná");

        diff.Segments.Should().Contain(segment =>
            segment.Kind == DocumentTextDiffSegmentKind.Removed && segment.Text == "stále");
    }

    [Fact]
    public void Diff_DetectsChangedTextAsRemovalAndAddition()
    {
        var diff = DocumentTextDiffHelper.Diff("Klient zaplatí 1000 Kč", "Klient zaplatí 1200 Kč");

        diff.Segments.Should().Contain(segment =>
            segment.Kind == DocumentTextDiffSegmentKind.Removed && segment.Text == "1000");
        diff.Segments.Should().Contain(segment =>
            segment.Kind == DocumentTextDiffSegmentKind.Added && segment.Text == "1200");
    }

    [Fact]
    public void Diff_IsStableForCzechText()
    {
        var diff = DocumentTextDiffHelper.Diff("Příliš žluťoučký kůň úpěl", "Příliš žluťoučký kůň hlasitě úpěl");

        diff.Segments.Select(segment => $"{segment.Kind}:{segment.Text}")
            .Should().Equal(
                "Unchanged:Příliš žluťoučký kůň",
                "Added:hlasitě",
                "Unchanged:úpěl");
    }
}
