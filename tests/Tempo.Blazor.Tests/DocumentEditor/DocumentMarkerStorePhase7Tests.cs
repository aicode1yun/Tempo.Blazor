using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentMarkerStorePhase7Tests
{
    [Fact]
    public void Phase7_DocumentMarker_ModelContainsIdentityRangePrioritySourceAndPersistenceFlag()
    {
        var marker = new DocumentMarker
        {
            Id = "marker-1",
            Type = DocumentMarkerType.Comment,
            Range = DocumentMarkerRange.InBlock("block-1", 2, 8, "inline-1"),
            AffectsData = true,
            Priority = 50,
            Source = DocumentMarkerSource.Document,
            TargetId = "comment-1"
        };

        marker.Id.Should().Be("marker-1");
        marker.Type.Should().Be(DocumentMarkerType.Comment);
        marker.Range.StartBlockId.Should().Be("block-1");
        marker.Range.StartInlineId.Should().Be("inline-1");
        marker.Range.StartOffset.Should().Be(2);
        marker.Range.EndOffset.Should().Be(8);
        marker.AffectsData.Should().BeTrue();
        marker.Priority.Should().Be(50);
        marker.Source.Should().Be(DocumentMarkerSource.Document);
    }

    [Fact]
    public void Phase7_DocumentMarkerRange_DetectsBlockTouchesAndInlineOffsetOverlap()
    {
        var left = DocumentMarkerRange.InBlock("block-1", 2, 8);
        var overlapping = DocumentMarkerRange.InBlock("block-1", 7, 10);
        var disjoint = DocumentMarkerRange.InBlock("block-1", 10, 12);

        left.TouchesBlock("block-1").Should().BeTrue();
        left.TouchesBlock("block-2").Should().BeFalse();
        left.Overlaps(overlapping).Should().BeTrue();
        left.Overlaps(disjoint).Should().BeFalse();
    }

    [Fact]
    public void Phase7_DocumentMarkerPresentation_MapsKnownTypesToStableClasses()
    {
        DocumentMarkerPresentation.For(DocumentMarkerType.Search).CssClass.Should().Be("tm-wysiwyg-marker--search");
        DocumentMarkerPresentation.For(DocumentMarkerType.SearchActive).ActiveCssClass.Should().BeNull();
        DocumentMarkerPresentation.For(DocumentMarkerType.Comment).TestId.Should().Be("document-comment-marker");
        DocumentMarkerPresentation.For(DocumentMarkerType.RevisionDeletion).CssClass.Should().Be("tm-wysiwyg-marker--revision-delete");
        DocumentMarkerPresentation.For(DocumentMarkerType.RemoteSelection).CssClass.Should().Be("tm-wysiwyg-marker--remote-selection");
        DocumentMarkerPresentation.For(DocumentMarkerType.RestrictedRegion).CssClass.Should().Be("tm-wysiwyg-marker--restricted-region");
    }

    [Fact]
    public void Phase7_DocumentMarkerStore_AddRemoveUpdateAndQueryIndexesMarkers()
    {
        var store = new DocumentMarkerStore();
        var search = new DocumentMarker
        {
            Id = "search-1",
            Type = DocumentMarkerType.Search,
            Range = DocumentMarkerRange.InBlock("block-1", 0, 4),
            Priority = 10,
            Source = DocumentMarkerSource.Transient
        };
        var comment = new DocumentMarker
        {
            Id = "comment-1",
            Type = DocumentMarkerType.Comment,
            Range = DocumentMarkerRange.InBlock("block-1", 2, 6),
            Priority = 40,
            AffectsData = true,
            Source = DocumentMarkerSource.Document
        };

        store.Add(search);
        store.Add(comment);

        store.GetByBlock("block-1").Select(marker => marker.Id).Should().Equal("comment-1", "search-1");
        store.GetByType(DocumentMarkerType.Search).Should().ContainSingle(marker => marker.Id == "search-1");
        store.GetOverlapping(DocumentMarkerRange.InBlock("block-1", 3, 5)).Select(marker => marker.Id).Should().Equal("comment-1", "search-1");
        store.GetPersistentMarkers().Should().ContainSingle(marker => marker.Id == "comment-1");

        store.UpdateRange("search-1", DocumentMarkerRange.InBlock("block-2", 1, 3)).Should().BeTrue();
        store.GetByBlock("block-1").Should().ContainSingle(marker => marker.Id == "comment-1");
        store.GetByBlock("block-2").Should().ContainSingle(marker => marker.Id == "search-1");

        store.Remove("comment-1").Should().BeTrue();
        store.GetAll().Should().ContainSingle(marker => marker.Id == "search-1");
    }

    [Fact]
    public void Phase7_DocumentMarkerStore_SortsPriorityAndExcludesTransientMarkersFromPersistence()
    {
        var store = new DocumentMarkerStore();
        store.Add(new DocumentMarker
        {
            Id = "search-1",
            Type = DocumentMarkerType.Search,
            Range = DocumentMarkerRange.InBlock("block-1", 0, 4),
            Priority = 10,
            AffectsData = false,
            Source = DocumentMarkerSource.Transient
        });
        store.Add(new DocumentMarker
        {
            Id = "revision-1",
            Type = DocumentMarkerType.RevisionInsertion,
            Range = DocumentMarkerRange.InBlock("block-1", 0, 4),
            Priority = 80,
            AffectsData = true,
            Source = DocumentMarkerSource.Document
        });
        store.Add(new DocumentMarker
        {
            Id = "comment-1",
            Type = DocumentMarkerType.Comment,
            Range = DocumentMarkerRange.InBlock("block-1", 0, 4),
            Priority = 60,
            AffectsData = true,
            Source = DocumentMarkerSource.Document
        });

        store.GetOverlapping(DocumentMarkerRange.InBlock("block-1", 1, 2))
            .Select(marker => marker.Id)
            .Should()
            .Equal("revision-1", "comment-1", "search-1");
        store.GetPersistentMarkers().Select(marker => marker.Id).Should().Equal("revision-1", "comment-1");
    }
}
