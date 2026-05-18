using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>
/// Manages restricted markers within a protected document, keeping marker ranges in sync as
/// text is inserted or deleted.
/// </summary>
public sealed class DocumentRestrictedEditingService
{
    private readonly List<DocumentRestrictedMarker> _markers = [];

    /// <summary>All currently registered markers (snapshot).</summary>
    public IReadOnlyList<DocumentRestrictedMarker> Markers => _markers;

    /// <summary>Add a new editable region.</summary>
    public void AddMarker(DocumentRestrictedMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        _markers.Add(marker);
    }

    /// <summary>Remove the marker with the given <paramref name="markerId"/>.</summary>
    /// <returns><c>true</c> if the marker was found and removed.</returns>
    public bool RemoveMarker(string markerId)
    {
        var idx = _markers.FindIndex(m => m.Id == markerId);
        if (idx < 0) return false;
        _markers.RemoveAt(idx);
        return true;
    }

    /// <summary>
    /// Update marker offsets after text is inserted at <paramref name="blockId"/> /
    /// <paramref name="offset"/> with <paramref name="length"/> characters.
    /// </summary>
    public void UpdateForInsert(string blockId, int offset, int length)
    {
        if (length <= 0) return;

        for (var i = 0; i < _markers.Count; i++)
        {
            var m = _markers[i];
            var newStart = m.StartBlockId == blockId
                ? AdjustOffsetForInsert(m.StartOffset, offset, length)
                : m.StartOffset;
            var newEnd = m.EndBlockId == blockId
                ? AdjustOffsetForInsert(m.EndOffset, offset, length)
                : m.EndOffset;

            if (newStart != m.StartOffset || newEnd != m.EndOffset)
                _markers[i] = m with { StartOffset = newStart, EndOffset = newEnd };
        }
    }

    /// <summary>
    /// Update marker offsets after text is deleted at <paramref name="blockId"/> /
    /// <paramref name="offset"/> spanning <paramref name="length"/> characters.
    /// </summary>
    public void UpdateForDelete(string blockId, int offset, int length)
    {
        if (length <= 0) return;

        for (var i = 0; i < _markers.Count; i++)
        {
            var m = _markers[i];
            var newStart = m.StartBlockId == blockId
                ? AdjustOffsetForDelete(m.StartOffset, offset, length)
                : m.StartOffset;
            var newEnd = m.EndBlockId == blockId
                ? AdjustOffsetForDelete(m.EndOffset, offset, length)
                : m.EndOffset;

            if (newStart != m.StartOffset || newEnd != m.EndOffset)
                _markers[i] = m with { StartOffset = newStart, EndOffset = newEnd };
        }
    }

    /// <summary>
    /// Returns <c>true</c> if the position (<paramref name="blockId"/>, <paramref name="offset"/>)
    /// falls inside at least one editable marker.
    /// </summary>
    public bool IsInsideEditableRegion(string blockId, int offset)
    {
        foreach (var m in _markers)
        {
            // Same-block simple case
            if (m.StartBlockId == blockId && m.EndBlockId == blockId)
            {
                if (offset >= m.StartOffset && offset < m.EndOffset)
                    return true;
            }
            // Position is in start block
            else if (m.StartBlockId == blockId && offset >= m.StartOffset)
            {
                return true;
            }
            // Position is in end block
            else if (m.EndBlockId == blockId && offset < m.EndOffset)
            {
                return true;
            }
        }
        return false;
    }

    private static int AdjustOffsetForInsert(int markerOffset, int insertAt, int length)
    {
        if (markerOffset >= insertAt)
            return markerOffset + length;
        return markerOffset;
    }

    private static int AdjustOffsetForDelete(int markerOffset, int deleteAt, int length)
    {
        if (markerOffset <= deleteAt)
            return markerOffset;
        if (markerOffset < deleteAt + length)
            return deleteAt;
        return markerOffset - length;
    }
}
