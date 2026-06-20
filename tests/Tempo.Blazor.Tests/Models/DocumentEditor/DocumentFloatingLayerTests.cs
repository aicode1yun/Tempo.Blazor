using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentFloatingLayerTests
{
    // ── Push ───────────────────────────────────────────────────────────────

    [Fact]
    public void Push_AddsLayerToEmptyStack()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("a", DocumentFloatingLayerKind.FindPanel, zIndex: 10));

        stack.Layers.Should().HaveCount(1);
        stack.Layers[0].LayerId.Should().Be("a");
    }

    [Fact]
    public void Push_MultipleLayersSortedByZIndex()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("high", DocumentFloatingLayerKind.LinkDialog, zIndex: 300));
        stack.Push(MakeLayer("low", DocumentFloatingLayerKind.FindPanel, zIndex: 100));
        stack.Push(MakeLayer("mid", DocumentFloatingLayerKind.TokenMenu, zIndex: 200));

        stack.Layers.Select(l => l.LayerId).Should().Equal("low", "mid", "high");
    }

    [Fact]
    public void Push_SameIdReplacesExistingLayer()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("x", DocumentFloatingLayerKind.FindPanel, zIndex: 10));
        stack.Push(MakeLayer("x", DocumentFloatingLayerKind.LinkDialog, zIndex: 20));

        stack.Layers.Should().HaveCount(1);
        stack.Layers[0].Kind.Should().Be(DocumentFloatingLayerKind.LinkDialog);
        stack.Layers[0].ZIndex.Should().Be(20);
    }

    [Fact]
    public void Push_StoresPriorityAnchorAndRestoreTargetMetadata()
    {
        var anchor = new DocumentFloatingLayerAnchor
        {
            X = 12,
            Y = 24,
            Width = 120,
            Height = 32,
            Target = "selection"
        };
        var layer = new DocumentFloatingLayerState
        {
            LayerId = "mini-toolbar",
            Kind = DocumentFloatingLayerKind.MiniToolbar,
            ZIndex = 20,
            Priority = 80,
            Anchor = anchor,
            RestoreFocusTarget = "surface"
        };
        var stack = new DocumentFloatingLayerStack();

        stack.Push(layer);

        stack.Topmost.Should().BeSameAs(layer);
        stack.Topmost!.EffectivePriority.Should().Be(80);
        stack.Topmost.Anchor.Should().BeSameAs(anchor);
        stack.Topmost.RestoreFocusTarget.Should().Be("surface");
    }

    [Fact]
    public void Push_SortsByPriorityBeforeZIndex()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("high-z-low-priority", DocumentFloatingLayerKind.LinkDialog, zIndex: 900, priority: 10));
        stack.Push(MakeLayer("low-z-high-priority", DocumentFloatingLayerKind.FindPanel, zIndex: 20, priority: 80));

        stack.Topmost!.LayerId.Should().Be("low-z-high-priority");
    }

    [Fact]
    public void Push_NullThrowsArgumentNullException()
    {
        var stack = new DocumentFloatingLayerStack();
        var act = () => stack.Push(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Topmost ────────────────────────────────────────────────────────────

    [Fact]
    public void Topmost_EmptyStack_ReturnsNull()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Topmost.Should().BeNull();
    }

    [Fact]
    public void Topmost_ReturnsLayerWithHighestZIndex()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("a", DocumentFloatingLayerKind.FindPanel, zIndex: 50));
        stack.Push(MakeLayer("b", DocumentFloatingLayerKind.LinkDialog, zIndex: 200));
        stack.Push(MakeLayer("c", DocumentFloatingLayerKind.TokenMenu, zIndex: 100));

        stack.Topmost!.LayerId.Should().Be("b");
    }

    [Fact]
    public void HasOpenLayers_EmptyStack_ReturnsFalse()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.HasOpenLayers.Should().BeFalse();
    }

    [Fact]
    public void HasOpenLayers_AfterPush_ReturnsTrue()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("a", DocumentFloatingLayerKind.FindPanel));
        stack.HasOpenLayers.Should().BeTrue();
    }

    // ── Remove ─────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ExistingId_RemovesLayer()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("a", DocumentFloatingLayerKind.FindPanel));
        stack.Push(MakeLayer("b", DocumentFloatingLayerKind.LinkDialog));

        stack.Remove("a");

        stack.Layers.Should().HaveCount(1);
        stack.Layers[0].LayerId.Should().Be("b");
    }

    [Fact]
    public void Remove_UnknownId_DoesNotThrow()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("a", DocumentFloatingLayerKind.FindPanel));
        var act = () => stack.Remove("unknown");
        act.Should().NotThrow();
        stack.Layers.Should().HaveCount(1);
    }

    [Fact]
    public void Remove_LastLayer_StackBecomesEmpty()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("only", DocumentFloatingLayerKind.FindPanel));
        stack.Remove("only");

        stack.HasOpenLayers.Should().BeFalse();
        stack.Topmost.Should().BeNull();
    }

    // ── Clear ──────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_RemovesAllLayers()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("a", DocumentFloatingLayerKind.FindPanel, zIndex: 10));
        stack.Push(MakeLayer("b", DocumentFloatingLayerKind.LinkDialog, zIndex: 20));
        stack.Push(MakeLayer("c", DocumentFloatingLayerKind.TokenMenu, zIndex: 30));

        stack.Clear();

        stack.HasOpenLayers.Should().BeFalse();
        stack.Layers.Should().BeEmpty();
    }

    // ── CloseTopmostAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CloseTopmostAsync_EmptyStack_DoesNotThrow()
    {
        var stack = new DocumentFloatingLayerStack();
        await stack.Invoking(s => s.CloseTopmostAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task CloseTopmostAsync_InvokesCloseCallback()
    {
        var callbackInvoked = false;
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("a", DocumentFloatingLayerKind.FindPanel, zIndex: 10,
            closeAsync: () => { callbackInvoked = true; return Task.CompletedTask; }));

        await stack.CloseTopmostAsync();

        callbackInvoked.Should().BeTrue();
        stack.HasOpenLayers.Should().BeFalse();
    }

    [Fact]
    public async Task CloseTopmostAsync_NoCallback_RemovesLayerSilently()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("a", DocumentFloatingLayerKind.FindPanel, zIndex: 10, closeAsync: null));

        await stack.CloseTopmostAsync();

        stack.HasOpenLayers.Should().BeFalse();
    }

    [Fact]
    public async Task CloseTopmostAsync_ClosesOnlyTopLayer()
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("bottom", DocumentFloatingLayerKind.FindPanel, zIndex: 10));
        stack.Push(MakeLayer("top", DocumentFloatingLayerKind.LinkDialog, zIndex: 200));

        await stack.CloseTopmostAsync();

        stack.Layers.Should().HaveCount(1);
        stack.Layers[0].LayerId.Should().Be("bottom");
    }

    [Fact]
    public async Task CloseTopmostAsync_MultipleCallsClosesInZIndexOrder()
    {
        var closed = new List<string>();
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("a", DocumentFloatingLayerKind.FindPanel, zIndex: 10,
            closeAsync: () => { closed.Add("a"); return Task.CompletedTask; }));
        stack.Push(MakeLayer("b", DocumentFloatingLayerKind.LinkDialog, zIndex: 200,
            closeAsync: () => { closed.Add("b"); return Task.CompletedTask; }));
        stack.Push(MakeLayer("c", DocumentFloatingLayerKind.TokenMenu, zIndex: 100,
            closeAsync: () => { closed.Add("c"); return Task.CompletedTask; }));

        await stack.CloseTopmostAsync();
        await stack.CloseTopmostAsync();
        await stack.CloseTopmostAsync();

        closed.Should().Equal("b", "c", "a");
        stack.HasOpenLayers.Should().BeFalse();
    }

    [Fact]
    public async Task CloseTopmostDismissibleAsync_SkipsNonDismissibleTopLayer()
    {
        var closed = new List<string>();
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("dismissible", DocumentFloatingLayerKind.FindPanel, zIndex: 10,
            closeAsync: () => { closed.Add("dismissible"); return Task.CompletedTask; }));
        stack.Push(MakeLayer("modal", DocumentFloatingLayerKind.ImageDialog, zIndex: 200,
            closeAsync: () => { closed.Add("modal"); return Task.CompletedTask; },
            isDismissible: false));

        var didClose = await stack.CloseTopmostDismissibleAsync();

        didClose.Should().BeTrue();
        closed.Should().Equal("dismissible");
        stack.Layers.Select(layer => layer.LayerId).Should().Equal("modal");
    }

    [Fact]
    public async Task CloseForOutsideClickAsync_ClosesLayersAboveTargetPath()
    {
        var closed = new List<string>();
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("find", DocumentFloatingLayerKind.FindPanel, zIndex: 10,
            closeAsync: () => { closed.Add("find"); return Task.CompletedTask; }));
        stack.Push(MakeLayer("menu", DocumentFloatingLayerKind.TextContextMenu, zIndex: 20,
            closeAsync: () => { closed.Add("menu"); return Task.CompletedTask; }));
        stack.Push(MakeLayer("token", DocumentFloatingLayerKind.TokenMenu, zIndex: 30,
            closeAsync: () => { closed.Add("token"); return Task.CompletedTask; }));

        var closedIds = await stack.CloseForOutsideClickAsync(["menu"]);

        closedIds.Should().Equal("token");
        closed.Should().Equal("token");
        stack.Layers.Select(layer => layer.LayerId).Should().Equal("find", "menu");
    }

    [Fact]
    public async Task CloseForOutsideClickAsync_KeepsNonDismissibleLayerOpen()
    {
        var closed = new List<string>();
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("non-dismissible", DocumentFloatingLayerKind.ImageDialog, zIndex: 10,
            closeAsync: () => { closed.Add("non-dismissible"); return Task.CompletedTask; },
            isDismissible: false));
        stack.Push(MakeLayer("sticky", DocumentFloatingLayerKind.FindPanel, zIndex: 20,
            closeAsync: () => { closed.Add("sticky"); return Task.CompletedTask; },
            closeOnOutsideClick: false));

        var closedIds = await stack.CloseForOutsideClickAsync(null);

        closedIds.Should().BeEmpty();
        closed.Should().BeEmpty();
        stack.Layers.Select(layer => layer.LayerId).Should().Equal("non-dismissible", "sticky");
    }

    // ── Kind enum completeness ─────────────────────────────────────────────

    [Theory]
    [InlineData(DocumentFloatingLayerKind.FindPanel)]
    [InlineData(DocumentFloatingLayerKind.LinkDialog)]
    [InlineData(DocumentFloatingLayerKind.TokenMenu)]
    [InlineData(DocumentFloatingLayerKind.ImageDialog)]
    [InlineData(DocumentFloatingLayerKind.TextContextMenu)]
    [InlineData(DocumentFloatingLayerKind.TableContextMenu)]
    [InlineData(DocumentFloatingLayerKind.ImageSelectionToolbar)]
    [InlineData(DocumentFloatingLayerKind.MiniToolbar)]
    [InlineData(DocumentFloatingLayerKind.VersionDialog)]
    [InlineData(DocumentFloatingLayerKind.CompareDialog)]
    [InlineData(DocumentFloatingLayerKind.SidePanel)]
    [InlineData(DocumentFloatingLayerKind.Custom)]
    public void Push_AcceptsAllKinds(DocumentFloatingLayerKind kind)
    {
        var stack = new DocumentFloatingLayerStack();
        stack.Push(MakeLayer("id", kind));
        stack.Layers[0].Kind.Should().Be(kind);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static DocumentFloatingLayerState MakeLayer(
        string id,
        DocumentFloatingLayerKind kind,
        int zIndex = 100,
        Func<Task>? closeAsync = null,
        int? priority = null,
        bool isDismissible = true,
        bool closeOnOutsideClick = true) =>
        new()
        {
            LayerId = id,
            Kind = kind,
            ZIndex = zIndex,
            Priority = priority,
            IsDismissible = isDismissible,
            CloseOnOutsideClick = closeOnOutsideClick,
            CloseAsync = closeAsync
        };
}
