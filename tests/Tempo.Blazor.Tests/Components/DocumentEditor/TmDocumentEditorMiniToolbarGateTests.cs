using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// B3 (UX fix 2026-06-11): the selection mini toolbar must surface for an object (image/drawing) selection,
/// not only a non-collapsed text range. The engine pushes an object selection with a collapsed text selection
/// plus an <see cref="WysiwygObjectSelectionSnapshot"/> payload; the C# visibility gate must accept it so the
/// floating image toolbar can anchor above the object.
/// </summary>
public sealed class TmDocumentEditorMiniToolbarGateTests
{
    [Fact]
    public void ObjectSelection_IsAccepted_EvenThoughTextSelectionIsCollapsed()
    {
        var request = new WysiwygMiniToolbarRequest
        {
            IsVisible = true,
            Reason = "canvas-object-selection",
            Selection = new WysiwygSelectionSnapshot
            {
                IsCollapsed = true,
                ObjectSelection = new WysiwygObjectSelectionSnapshot { ObjectId = "contract-left-wrap-image" }
            }
        };

        TmDocumentEditor.IsObjectMiniToolbarRequest(request).Should().BeTrue();
        TmDocumentEditor.IsVisibleRangeMiniToolbarRequest(request).Should().BeTrue();
    }

    [Fact]
    public void NonCollapsedTextSelection_IsStillAccepted()
    {
        var request = new WysiwygMiniToolbarRequest
        {
            IsVisible = true,
            Selection = new WysiwygSelectionSnapshot { IsCollapsed = false }
        };

        TmDocumentEditor.IsObjectMiniToolbarRequest(request).Should().BeFalse();
        TmDocumentEditor.IsVisibleRangeMiniToolbarRequest(request).Should().BeTrue();
    }

    [Fact]
    public void CollapsedCaretWithoutObject_IsRejected()
    {
        var request = new WysiwygMiniToolbarRequest
        {
            IsVisible = true,
            Selection = new WysiwygSelectionSnapshot { IsCollapsed = true }
        };

        TmDocumentEditor.IsObjectMiniToolbarRequest(request).Should().BeFalse();
        TmDocumentEditor.IsVisibleRangeMiniToolbarRequest(request).Should().BeFalse();
    }

    [Fact]
    public void HiddenObjectRequest_IsRejected()
    {
        var request = new WysiwygMiniToolbarRequest
        {
            IsVisible = false,
            Selection = new WysiwygSelectionSnapshot
            {
                IsCollapsed = true,
                ObjectSelection = new WysiwygObjectSelectionSnapshot { ObjectId = "img-1" }
            }
        };

        TmDocumentEditor.IsVisibleRangeMiniToolbarRequest(request).Should().BeFalse();
    }
}
