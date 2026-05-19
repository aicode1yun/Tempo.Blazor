using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public sealed class DocumentRestrictedEditingServiceTests
{
    // ── Marker model ─────────────────────────────────────────────────────────

    [Fact]
    public void Marker_HasUniqueId_ByDefault()
    {
        var a = new DocumentRestrictedMarker();
        var b = new DocumentRestrictedMarker();
        Assert.NotEqual(a.Id, b.Id);
        Assert.NotEmpty(a.Id);
    }

    [Fact]
    public void Marker_RecordEquality_ComparesAllFields()
    {
        var m1 = new DocumentRestrictedMarker { Id = "x", StartBlockId = "b1", StartOffset = 0, EndBlockId = "b1", EndOffset = 5 };
        var m2 = m1 with { };
        Assert.Equal(m1, m2);
    }

    // ── AddMarker / RemoveMarker ──────────────────────────────────────────────

    [Fact]
    public void AddMarker_AppendsToList()
    {
        var svc = new DocumentRestrictedEditingService();
        var m = new DocumentRestrictedMarker { StartBlockId = "b1", EndBlockId = "b1", EndOffset = 10 };
        svc.AddMarker(m);
        Assert.Single(svc.Markers);
        Assert.Equal(m, svc.Markers[0]);
    }

    [Fact]
    public void AddMarker_NullThrows()
    {
        var svc = new DocumentRestrictedEditingService();
        Assert.Throws<ArgumentNullException>(() => svc.AddMarker(null!));
    }

    [Fact]
    public void RemoveMarker_ReturnsTrueAndRemoves()
    {
        var svc = new DocumentRestrictedEditingService();
        var m = new DocumentRestrictedMarker { Id = "mk1", StartBlockId = "b1", EndBlockId = "b1", EndOffset = 5 };
        svc.AddMarker(m);
        var removed = svc.RemoveMarker("mk1");
        Assert.True(removed);
        Assert.Empty(svc.Markers);
    }

    [Fact]
    public void RemoveMarker_ReturnsFalseWhenNotFound()
    {
        var svc = new DocumentRestrictedEditingService();
        Assert.False(svc.RemoveMarker("does-not-exist"));
    }

    // ── UpdateForInsert ───────────────────────────────────────────────────────

    [Fact]
    public void UpdateForInsert_ShiftsEndOffsetWhenInsertedBeforeEnd()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { Id = "m1", StartBlockId = "b1", StartOffset = 5, EndBlockId = "b1", EndOffset = 15 });

        svc.UpdateForInsert("b1", 10, 3); // insert 3 chars at offset 10

        var m = svc.Markers[0];
        Assert.Equal(5, m.StartOffset);  // before insert point → unchanged
        Assert.Equal(18, m.EndOffset);   // shifted by 3
    }

    [Fact]
    public void UpdateForInsert_ShiftsStartOffsetWhenInsertedBeforeStart()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { Id = "m1", StartBlockId = "b1", StartOffset = 10, EndBlockId = "b1", EndOffset = 20 });

        svc.UpdateForInsert("b1", 5, 4);

        var m = svc.Markers[0];
        Assert.Equal(14, m.StartOffset); // shifted by 4
        Assert.Equal(24, m.EndOffset);   // shifted by 4
    }

    [Fact]
    public void UpdateForInsert_NoChangeWhenDifferentBlock()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { Id = "m1", StartBlockId = "b1", StartOffset = 5, EndBlockId = "b1", EndOffset = 10 });

        svc.UpdateForInsert("b2", 0, 100);

        var m = svc.Markers[0];
        Assert.Equal(5, m.StartOffset);
        Assert.Equal(10, m.EndOffset);
    }

    [Fact]
    public void UpdateForInsert_ZeroLengthIsNoop()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { Id = "m1", StartBlockId = "b1", StartOffset = 0, EndBlockId = "b1", EndOffset = 10 });
        svc.UpdateForInsert("b1", 0, 0);
        Assert.Equal(10, svc.Markers[0].EndOffset);
    }

    // ── UpdateForDelete ───────────────────────────────────────────────────────

    [Fact]
    public void UpdateForDelete_ShiftsEndOffsetWhenDeletedInsideMarker()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { Id = "m1", StartBlockId = "b1", StartOffset = 5, EndBlockId = "b1", EndOffset = 20 });

        svc.UpdateForDelete("b1", 10, 5); // delete 5 chars starting at 10

        var m = svc.Markers[0];
        Assert.Equal(5, m.StartOffset);   // before delete → unchanged
        Assert.Equal(15, m.EndOffset);    // shifted by -5
    }

    [Fact]
    public void UpdateForDelete_ClampsMidDeleteToDeletePoint()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { Id = "m1", StartBlockId = "b1", StartOffset = 5, EndBlockId = "b1", EndOffset = 12 });

        svc.UpdateForDelete("b1", 10, 5); // delete 5 chars starting at 10 (marker end inside range)

        var m = svc.Markers[0];
        Assert.Equal(10, m.EndOffset); // clamped to delete point
    }

    [Fact]
    public void UpdateForDelete_NoChangeWhenDeletedAfterMarker()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { Id = "m1", StartBlockId = "b1", StartOffset = 0, EndBlockId = "b1", EndOffset = 5 });

        svc.UpdateForDelete("b1", 10, 3);

        Assert.Equal(5, svc.Markers[0].EndOffset);
    }

    [Fact]
    public void UpdateForDelete_ZeroLengthIsNoop()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { Id = "m1", StartBlockId = "b1", StartOffset = 0, EndBlockId = "b1", EndOffset = 10 });
        svc.UpdateForDelete("b1", 0, 0);
        Assert.Equal(10, svc.Markers[0].EndOffset);
    }

    // ── IsInsideEditableRegion ────────────────────────────────────────────────

    [Fact]
    public void IsInsideEditableRegion_ReturnsTrueInsideSingleBlockMarker()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { StartBlockId = "b1", StartOffset = 5, EndBlockId = "b1", EndOffset = 15 });

        Assert.True(svc.IsInsideEditableRegion("b1", 5));
        Assert.True(svc.IsInsideEditableRegion("b1", 14));
    }

    [Fact]
    public void IsInsideEditableRegion_ReturnsFalseOutsideMarker()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { StartBlockId = "b1", StartOffset = 5, EndBlockId = "b1", EndOffset = 15 });

        Assert.False(svc.IsInsideEditableRegion("b1", 4));
        Assert.False(svc.IsInsideEditableRegion("b1", 15)); // exclusive end
    }

    [Fact]
    public void IsInsideEditableRegion_ReturnsFalseWhenNoMarkers()
    {
        var svc = new DocumentRestrictedEditingService();
        Assert.False(svc.IsInsideEditableRegion("b1", 0));
    }

    [Fact]
    public void IsInsideEditableRegion_ReturnsTrueForStartBlock()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { StartBlockId = "b1", StartOffset = 3, EndBlockId = "b2", EndOffset = 10 });
        Assert.True(svc.IsInsideEditableRegion("b1", 5));
    }

    [Fact]
    public void IsInsideEditableRegion_ReturnsTrueForEndBlock()
    {
        var svc = new DocumentRestrictedEditingService();
        svc.AddMarker(new DocumentRestrictedMarker { StartBlockId = "b1", StartOffset = 0, EndBlockId = "b2", EndOffset = 10 });
        Assert.True(svc.IsInsideEditableRegion("b2", 0));
        Assert.False(svc.IsInsideEditableRegion("b2", 10)); // exclusive
    }

    // ── DocumentEditorDocument model ──────────────────────────────────────────

    [Fact]
    public void Document_IsProtected_DefaultsFalse()
    {
        var doc = new DocumentEditorDocument();
        Assert.False(doc.IsProtected);
    }

    [Fact]
    public void Document_RestrictedMarkers_DefaultsEmpty()
    {
        var doc = new DocumentEditorDocument();
        Assert.Empty(doc.RestrictedMarkers);
    }

    [Fact]
    public void Document_AcceptsRestrictedMarkersAndIsProtected()
    {
        var doc = new DocumentEditorDocument
        {
            IsProtected = true,
            RestrictedMarkers = [new DocumentRestrictedMarker { StartBlockId = "b1", EndBlockId = "b1", EndOffset = 10 }]
        };
        Assert.True(doc.IsProtected);
        Assert.Single(doc.RestrictedMarkers);
    }
}
