using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.TreeView;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Models;
using Tempo.Blazor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.TreeView;

public class TmTreeViewNodeDragTests : LocalizationTestBase
{
    private record DragNode(
        string Id, string Label, string? Icon,
        bool IsLeaf, bool IsLoading,
        IReadOnlyList<ITreeNode<string>> Children) : ITreeNode<string>;

    private static List<ITreeNode<string>> TwoNodes() =>
    [
        new DragNode("s1", "Suite A", null, true, false, []),
        new DragNode("s2", "Suite B", null, true, false, []),
    ];

    public TmTreeViewNodeDragTests()
    {
        Services.AddScoped<DragDropService>();
    }

    // ── AllowNodeDrag=false (default) ─────────────────────────────

    [Fact]
    public void AllowNodeDrag_False_Nodes_Are_Not_Draggable()
    {
        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, false));

        cut.FindAll(".tm-tree-node")
           .All(n => n.GetAttribute("draggable") != "true")
           .Should().BeTrue();
    }

    [Fact]
    public void AllowNodeDrag_False_RootDropZone_NotRendered()
    {
        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, false));

        cut.FindAll(".tm-tree-root-drop").Should().BeEmpty();
    }

    // ── AllowNodeDrag=true ────────────────────────────────────────

    [Fact]
    public void AllowNodeDrag_True_Nodes_Have_Draggable_Attribute()
    {
        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, true));

        cut.FindAll(".tm-tree-node")
           .All(n => n.GetAttribute("draggable") == "true")
           .Should().BeTrue();
    }

    [Fact]
    public void AllowNodeDrag_True_DragStart_SetsDragSourceToTreeView()
    {
        var dragDrop = Services.GetRequiredService<DragDropService>();

        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, true));

        cut.FindAll(".tm-tree-node").First()
           .TriggerEvent("ondragstart", new DragEventArgs());

        dragDrop.Source.Should().Be(DragSource.TreeView);
        dragDrop.IsDragging.Should().BeTrue();
    }

    [Fact]
    public void AllowNodeDrag_True_DragStart_AddsNodeIdToDraggedIds()
    {
        var dragDrop = Services.GetRequiredService<DragDropService>();

        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, true));

        cut.FindAll(".tm-tree-node").First()
           .TriggerEvent("ondragstart", new DragEventArgs());

        dragDrop.DraggedIds.Should().Contain("s1");
    }

    [Fact]
    public void AllowNodeDrag_True_DragEnd_ClearsDragDropService()
    {
        var dragDrop = Services.GetRequiredService<DragDropService>();

        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, true));

        var firstNode = cut.FindAll(".tm-tree-node").First();
        firstNode.TriggerEvent("ondragstart", new DragEventArgs());
        firstNode.TriggerEvent("ondragend", new DragEventArgs());

        dragDrop.IsDragging.Should().BeFalse();
        dragDrop.Source.Should().BeNull();
    }

    [Fact]
    public void AllowNodeDrag_True_DragStart_ShowsRootDropZone()
    {
        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, true));

        cut.FindAll(".tm-tree-node").First()
           .TriggerEvent("ondragstart", new DragEventArgs());

        cut.FindAll(".tm-tree-root-drop").Should().HaveCount(1);
    }

    [Fact]
    public void AllowNodeDrag_True_DragEnd_HidesRootDropZone()
    {
        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, true));

        var firstNode = cut.FindAll(".tm-tree-node").First();
        firstNode.TriggerEvent("ondragstart", new DragEventArgs());
        firstNode.TriggerEvent("ondragend",   new DragEventArgs());

        cut.FindAll(".tm-tree-root-drop").Should().BeEmpty();
    }

    // ── Drop onto node → OnNodeMove ───────────────────────────────

    [Fact]
    public void AllowNodeDrag_Drop_OnSecondNode_Fires_OnNodeMove_WithCorrectNodes()
    {
        TreeNodeMoveEventArgs<string>? received = null;

        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, true)
            .Add(c => c.OnNodeMove,
                EventCallback.Factory.Create<TreeNodeMoveEventArgs<string>>(this, a => received = a)));

        cut.FindAll(".tm-tree-node")[0].TriggerEvent("ondragstart", new DragEventArgs());
        // Re-find after re-render caused by dragstart
        cut.FindAll(".tm-tree-node")[1].TriggerEvent("ondragover", new DragEventArgs());
        cut.FindAll(".tm-tree-node")[1].Drop();

        received.Should().NotBeNull();
        received!.MovedNode.Id.Should().Be("s1");
        received!.NewParent!.Id.Should().Be("s2");
    }

    [Fact]
    public void AllowNodeDrag_Drop_OnSelf_DoesNotFire_OnNodeMove()
    {
        TreeNodeMoveEventArgs<string>? received = null;

        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, true)
            .Add(c => c.OnNodeMove,
                EventCallback.Factory.Create<TreeNodeMoveEventArgs<string>>(this, a => received = a)));

        cut.FindAll(".tm-tree-node")[0].TriggerEvent("ondragstart", new DragEventArgs());
        // Re-find after re-render caused by dragstart
        cut.FindAll(".tm-tree-node")[0].TriggerEvent("ondragover", new DragEventArgs());
        cut.FindAll(".tm-tree-node")[0].Drop();

        received.Should().BeNull();
    }

    // ── Drop onto root zone → OnNodeMove with NewParent=null ──────

    [Fact]
    public void AllowNodeDrag_Drop_OnRootZone_Fires_OnNodeMove_WithNullParent()
    {
        TreeNodeMoveEventArgs<string>? received = null;

        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowNodeDrag, true)
            .Add(c => c.OnNodeMove,
                EventCallback.Factory.Create<TreeNodeMoveEventArgs<string>>(this, a => received = a)));

        var firstNode = cut.FindAll(".tm-tree-node").First();
        firstNode.TriggerEvent("ondragstart", new DragEventArgs());

        cut.Find(".tm-tree-root-drop").Drop();

        received.Should().NotBeNull();
        received!.MovedNode.Id.Should().Be("s1");
        received!.NewParent.Should().BeNull();
    }

    // ── Coexistence with AllowDrop (external drops) ───────────────

    [Fact]
    public void AllowDrop_And_AllowNodeDrag_ExternalDrop_Fires_OnItemsDrop()
    {
        TreeDropEventArgs<string>? dropReceived = null;
        TreeNodeMoveEventArgs<string>? moveReceived = null;

        var dragDrop = Services.GetRequiredService<DragDropService>();
        // Simulate external drag (MVL)
        dragDrop.StartDrag(["tc-001"], DragSource.MultiViewList);

        var cut = RenderComponent<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowDrop, true)
            .Add(c => c.AllowNodeDrag, true)
            .Add(c => c.OnItemsDrop,
                EventCallback.Factory.Create<TreeDropEventArgs<string>>(this, a => dropReceived = a))
            .Add(c => c.OnNodeMove,
                EventCallback.Factory.Create<TreeNodeMoveEventArgs<string>>(this, a => moveReceived = a)));

        cut.FindAll(".tm-tree-node").First().Drop();

        dropReceived.Should().NotBeNull();
        moveReceived.Should().BeNull();
    }
}
